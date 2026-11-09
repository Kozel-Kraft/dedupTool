namespace DedupTool;

/// <summary>
/// Decides which documents of a batch go to the load file.
///
/// The unit of decision is the FAMILY (a top-level document plus everything below it), never an
/// individual attachment: an attachment is suppressed only together with its whole family, so a
/// family is always produced intact (spec 3.5).
///
/// Stages: index -> resolve family roots -> decide duplicate roots -> project rows.
/// Every stage is O(n); nothing here compares documents pairwise.
/// </summary>
public static class Deduper
{
    public static DedupResult Run(Batch batch)
    {
        var docs = batch.Documents;
        var index = BuildIndex(docs);
        LogInputShape(docs); // after BuildIndex: nulls are already read as empty text there
        var root = ResolveRoots(docs, index);
        var (keys, survivor) = DecideDuplicateRoots(docs, root);

        var rootDates = new string?[docs.Count]; // load-file date of each kept root, computed once per family
        var rows = new List<LoadFileRow>();
        var suppressed = 0;
        for (var i = 0; i < docs.Count; i++)
        {
            var d = docs[i];
            var r = root[i];

            if (survivor[keys[r]!] != r)
            {
                suppressed++;
                if (Log.IsEnabled(LogLevel.Debug))
                {
                    Log.Debug($"SUPPRESS {d.DocId} [{d.DocType}] '{d.FileName}' custodian={d.Custodian} " +
                              $"family={docs[r].DocId} duplicate-of-family={docs[survivor[keys[r]!]].DocId}");
                }
                continue;
            }

            // Every member carries its family's top-level email date; loose families have none.
            rootDates[r] ??= RootDate(docs[r]);
            var sentDate = rootDates[r]!;
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.Debug($"KEEP     {d.DocId} [{d.DocType}] '{d.FileName}' custodian={d.Custodian} family={docs[r].DocId}");
            }

            rows.Add(new LoadFileRow
            {
                DocId = d.DocId,
                FamilyId = docs[r].DocId,
                ParentId = d.ParentId ?? "",
                Custodian = d.Custodian,
                DocType = d.DocType,
                FileName = d.FileName,
                SentDate = sentDate,
                Hash = d.Sha256,
            });
        }

        // Canonical output: same batch => same bytes, whatever the input order (spec 6.1). Row order is
        // "not significant" for the ingest job (spec 5), so sorting by DocID satisfies both statements.
        rows.Sort((a, b) => string.CompareOrdinal(a.DocId, b.DocId));

        Log.Info($"Dedup finished: {docs.Count} in, {rows.Count} out, {suppressed} suppressed.");
        return new DedupResult(rows, suppressed);
    }

    // ---- stage 1: index ---------------------------------------------------------------------

    private static Dictionary<string, int> BuildIndex(List<Document> docs)
    {
        var index = new Dictionary<string, int>(docs.Count, StringComparer.Ordinal);
        List<string>? repeated = null, unnamed = null, nulls = null;
        for (var i = 0; i < docs.Count; i++)
        {
            var d = docs[i];
            if (d == null) { (nulls ??= new()).Add("#" + i); continue; }

            // JSON can carry explicit nulls for text fields; read them as empty text rather than crash later.
            d.DocId ??= ""; d.Custodian ??= ""; d.DocType ??= ""; d.FileName ??= ""; d.Sha256 ??= "";

            if (string.IsNullOrWhiteSpace(d.DocId)) { (unnamed ??= new()).Add("#" + i); continue; }
            if (!index.TryAdd(d.DocId, i)) (repeated ??= new()).Add(d.DocId);
        }

        if (nulls != null)
        {
            throw new BatchValidationException(
                $"{nulls.Count} entry(ies) in 'documents' are null (positions: {Sample(nulls)}).");
        }
        if (unnamed != null)
        {
            throw new BatchValidationException(
                $"{unnamed.Count} document(s) have no docId (positions: {Sample(unnamed)}).");
        }
        if (repeated != null)
        {
            throw new BatchValidationException(
                $"docId must be unique within a batch but {repeated.Count} repeated: {Sample(repeated)}.");
        }
        return index;
    }

    // ---- stage 2: family roots --------------------------------------------------------------

    /// <summary>
    /// For every document, the index of its top-level document (follows parentId to the top, any depth).
    /// Iterative with memoisation, so deep chains neither overflow the stack nor cost O(depth) each.
    /// </summary>
    private static int[] ResolveRoots(List<Document> docs, Dictionary<string, int> index)
    {
        const byte InProgress = 1, Done = 2; // 0 = not visited yet
        var root = new int[docs.Count];
        var state = new byte[docs.Count];
        var path = new List<int>();

        for (var start = 0; start < docs.Count; start++)
        {
            if (state[start] == Done) continue;

            path.Clear();
            var current = start;
            int top;
            while (true)
            {
                if (state[current] == Done) { top = root[current]; break; }
                if (state[current] == InProgress)
                {
                    var loop = path.SkipWhile(p => p != current).Select(p => docs[p].DocId);
                    throw new BatchValidationException($"parentId cycle: {string.Join(" -> ", loop)} -> {docs[current].DocId}.");
                }

                state[current] = InProgress;
                path.Add(current);

                var parentId = docs[current].ParentId;
                if (parentId == null) { top = current; break; }
                if (!index.TryGetValue(parentId, out var parent))
                {
                    throw new BatchValidationException(
                        $"{docs[current].DocId} has parentId '{parentId}' which is not in the batch (the spec says the parent is always present).");
                }
                current = parent;
            }

            foreach (var p in path) { root[p] = top; state[p] = Done; }
        }
        return root;
    }

    // ---- stage 3: duplicate roots -----------------------------------------------------------

    /// <summary>
    /// Groups top-level documents by equality key and picks one survivor per group: the lowest DocId
    /// (ordinal), so the result depends on the data only, never on input order (spec 6.1).
    /// Returns each root's key and, per key, the index of its surviving root.
    /// </summary>
    private static (string?[] keys, Dictionary<string, int> survivor) DecideDuplicateRoots(List<Document> docs, int[] root)
    {
        var keys = new string?[docs.Count];
        var survivor = new Dictionary<string, int>(StringComparer.Ordinal);

        // sha256 of everything below each root (one pass); part of an email's identity.
        var below = new Dictionary<int, List<string>>();
        for (var i = 0; i < docs.Count; i++)
        {
            if (root[i] == i) continue;
            if (!below.TryGetValue(root[i], out var list)) below[root[i]] = list = new List<string>();
            list.Add(docs[i].Sha256);
        }

        for (var i = 0; i < docs.Count; i++)
        {
            if (root[i] != i) continue;

            var key = RootKey(docs[i], below.GetValueOrDefault(i));
            keys[i] = key;
            if (!survivor.TryGetValue(key, out var best) ||
                string.CompareOrdinal(docs[i].DocId, docs[best].DocId) < 0)
            {
                survivor[key] = i;
            }
        }
        return (keys, survivor);
    }

    private static string RootDate(Document root)
    {
        if (root.DocType != "email") return "";

        var date = UtcDate.ToLoadFile(root.SentDate);
        if (date == null)
        {
            Log.Warn($"{root.DocId} is an email whose sentDate is {(string.IsNullOrWhiteSpace(root.SentDate) ? "missing" : $"not parseable ('{root.SentDate}')")}; " +
                     "its family is written with an empty SentDate.");
        }
        return date ?? "";
    }

    // Emails are compared by content (3.3); loose files and anything else by sha256 (3.1).
    private static string RootKey(Document d, List<string>? hashesBelow)
    {
        if (d.DocType == "email")
        {
            // An attachment with no hash cannot be compared, so this email cannot be proven identical to another
            // one: collapsing it could drop an attachment (same failure class as INGEST-4471). Keep it.
            var unhashed = hashesBelow?.Count(h => h.Length == 0) ?? 0;
            if (unhashed > 0)
            {
                Log.Warn($"{d.DocId} has {unhashed} attachment(s) with no sha256, so the email cannot be compared; it is kept.");
                return "U:" + d.DocId;
            }
            return EmailIdentity.Key(d, hashesBelow);
        }

        if (d.Sha256.Length == 0)
        {
            // An empty hash is not a hash: grouping on it would suppress unrelated files as "duplicates".
            Log.Warn($"{d.DocId} has no sha256, so it cannot be compared; it is kept.");
            return "U:" + d.DocId;
        }
        return "L:" + d.Sha256.ToLowerInvariant();
    }

    // ---- helpers ----------------------------------------------------------------------------

    private static void LogInputShape(List<Document> docs)
    {
        var byType = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var custodians = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in docs)
        {
            byType[d.DocType] = byType.GetValueOrDefault(d.DocType) + 1;
            custodians.Add(d.Custodian);
        }
        Log.Info($"Input shape: {docs.Count} documents ({string.Join(", ", byType.Select(kv => $"{kv.Key}={kv.Value}"))}), " +
                 $"{custodians.Count} custodian(s).");
    }

    private static string Sample(List<string> ids) =>
        string.Join(", ", ids.Take(10)) + (ids.Count > 10 ? ", ..." : "");
}
