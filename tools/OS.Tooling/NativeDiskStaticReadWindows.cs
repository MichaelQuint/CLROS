namespace OS.Tooling;
/// <summary>Host-only value join between positive confined file reads, independent
/// image sector coverage and already inspected block observations. This supplies
/// no scheduler, user-memory, residual metadata, native, process or HTTP authority.
/// The enclosing consumer must authenticate the read windows and hold the same
/// raw image for both input inspections through its final acceptance checks.</summary>

public static partial class NativeDiskStaticReadWindows
{
    public const int MaximumWindows = 4096;
    public const int MaximumCandidateLinks = 262144;
    public const uint MaximumReturnedBytes = 8192;
    /// <summary>Synchronously borrow stable inputs. Admit at most4096 positive
    /// syscall0/17 windows, at most one demand per inspected copied observation,
    /// and262144 candidate links. Each demanded data sector needs one distinct
    /// committed copy whose Submit, validated completion and Copy are strictly
    /// inside that window. Require a unique assignment; forced-degree propagation
    /// rejects missing coverage and unresolved alternatives without guessing.
    /// Compare all valid file bytes in assigned sectors with the independent
    /// inventory. Beyond-EOF padding and the identity of the whole raw image are
    /// checked by the enclosing consumer, not inferred from these value inputs.
    /// EOF, errors, cancellation returns, descriptor lifetime, initial output
    /// memory and thread scheduling must already be authenticated externally.
    /// No cross-domain clock comparison, I/O, callback or owner is introduced.
    /// Empty windows make no read claims and return all copies as residual.
    /// Malformed, oversized, missing, divergent or ambiguous joins throw ToolFailure.</summary>

    public static Inspection Inspect(WebRootDiskInventory.Inspection inventory, NativeDiskReadObservations.Inspection device, IReadOnlyList<int> ioRecordIndices, IReadOnlyList<ReadWindow> suppliedWindows)
    {
        int windowCount = suppliedWindows?.Count ?? -1;
        Need(inventory is not null && device is not null && windowCount is >= 0 and <= MaximumWindows, "input");
        int rows = device!.Rows.Count;
        Need(rows is> 0 and <= NativeDiskReadObservations.MaximumRows && ioRecordIndices is not null && ioRecordIndices.Count == rows, "io_indices");
        int[] indices = new int[rows];
        for (int i = 0; i < rows; i++)
        {
            indices[i] = ioRecordIndices![i];
            Need(indices[i] >= 0 && (i == 0 || indices[i] > indices[i - 1]), "io_indices");
        }

        List<SectorCopy> copies = [];
        Dictionary<ulong, List<int>> bySector = [];
        int submitted = -1, validated = -1;
        for (int i = 0; i < rows; i++)
        {
            var observation = device.Rows[i];
            if (observation.Kind == NativeDiskReadObservations.Kind.Submitted)
            {
                submitted = i;
                validated = -1;
                continue;
            }

            if (observation.Kind == NativeDiskReadObservations.Kind.CompletionValidated)
            {
                validated = i;
                continue;
            }

            Need(submitted >= 0 && validated == submitted + 1 && validated < i, "copy_stages");
            var submit = device.Rows[submitted];
            var validation = device.Rows[validated];
            Need(submit.Kind == NativeDiskReadObservations.Kind.Submitted && validation.Kind == NativeDiskReadObservations.Kind.CompletionValidated && validation.Bytes == observation.Bytes && submit.Generation == observation.Generation && validation.Generation == observation.Generation && submit.Sector == observation.Sector && validation.Sector == observation.Sector, "copy_stages");
            int copy = copies.Count;
            copies.Add(new(i, indices[submitted], indices[validated], indices[i], observation));
            if (!bySector.TryGetValue(observation.DataSector, out var list))
                bySector.Add(observation.DataSector, list = []);
            list.Add(copy);
        }

        ReadWindow[] windows = new ReadWindow[windowCount];
        HashSet<(ulong, ulong)> calls = [];
        HashSet<ulong> requests = [];
        Dictionary<ulong, WebRootDiskInventory.Entry> entries = inventory!.Entries.ToDictionary(e => e.Inode);
        List<Demand> demands = [];
        for (int w = 0; w < windowCount; w++)
        {
            var window = suppliedWindows![w];
            windows[w] = window;
            Need(window.Identity != 0 && window.Ordinal != 0 && window.Number is 0 or 17 && window.DescriptorGeneration != 0 && window.RequestGeneration != 0 && window.BeginRecordIndex >= 0 && window.CompleteRecordIndex > window.BeginRecordIndex && window.Requested != 0 && window.OutputPrefix is> 0 and <= MaximumReturnedBytes && window.ReturnedBytes is> 0 and <= MaximumReturnedBytes && window.ReturnedBytes <= window.Requested && window.ReturnedBytes <= window.OutputPrefix && window.OutputPrefix <= window.Requested && window.Offset <= long.MaxValue, "window");
            Need(calls.Add((window.Identity, window.Ordinal)) && requests.Add(window.RequestGeneration), "duplicate_window");
            Need(entries.TryGetValue(window.Inode, out var entry) && !entry.IsDirectory && window.Offset < entry.Bytes && window.ReturnedBytes <= entry.Bytes - window.Offset, "file_range");
            foreach (var sector in entry!.Cover(window.Offset, window.ReturnedBytes))
            {
                Need(demands.Count < copies.Count, "demand_bound");
                demands.Add(new(w, entry, sector) { Index = demands.Count });
            }
        }

        // Candidate lists are explicit and bounded. Each committed sector is a
        // point with ordered three-stage positions; binary searches avoid a full
        // window-by-observation scan for unrelated LBAs.
        List<int>[] reverse = new List<int>[copies.Count];
        for (int i = 0; i < reverse.Length; i++)
            reverse[i] = [];
        int links = 0;
        Dictionary<ulong, byte[]> content = [];
        Dictionary<int, (ulong Inode, ulong Offset)> verified = [];
        foreach (var demand in demands)
        {
            var window = windows[demand.Window];
            Need(bySector.TryGetValue(demand.Sector.Lba, out var candidates), "missing");
            int first = FirstSubmitAfter(candidates!, copies, window.BeginRecordIndex);
            int end = FirstCopyAtOrAfter(candidates!, copies, window.CompleteRecordIndex);
            Need(first < end, "missing");
            Need(end - first <= MaximumCandidateLinks - links, "candidate_bound");
            links += end - first;
            for (int p = first; p < end; p++)
            {
                int copy = candidates![p];
                var observed = copies[copy];
                if (verified.TryGetValue(copy, out var prior))
                    Need(prior == (demand.Entry.Inode, demand.Sector.FileOffset), "sector_alias");
                else
                {
                    if (!content.TryGetValue(demand.Entry.Inode, out var bytes))
                        content.Add(demand.Entry.Inode, bytes = demand.Entry.CopyBytes());
                    byte[] actual = Convert.FromHexString(observed.Observation.DataHex);
                    Need(actual.Length == 512 && demand.Sector.ValidBytes is> 0 and <= 512 && actual.AsSpan(0, (int)demand.Sector.ValidBytes).SequenceEqual(bytes.AsSpan(checked((int)demand.Sector.FileOffset), (int)demand.Sector.ValidBytes)), "image_bytes");
                    verified.Add(copy, (demand.Entry.Inode, demand.Sector.FileOffset));
                }

                demand.Candidates.Add(copy);
                reverse[copy].Add(demand.Index);
            }

            demand.Remaining = demand.Candidates.Count;
        }

        // A unique left-saturating matching exposes a forced demand edge.
        // Removing forced copies propagates other forced edges. A zero degree
        // means no covering matching; unresolved components do not establish
        // unique coverage and must reject.
        Queue<int> ready = new();
        int[] owner = Enumerable.Repeat(-1, copies.Count).ToArray();
        for (int i = 0; i < demands.Count; i++)
        {
            if (demands[i].Remaining == 1)
                ready.Enqueue(i);
        }

        int assigned = 0;
        while (ready.Count != 0)
        {
            int d = ready.Dequeue();
            var demand = demands[d];
            if (demand.Assigned >= 0)
                continue;
            Need(demand.Remaining == 1, "missing");
            int chosen = demand.Candidates.First(c => owner[c] < 0);
            demand.Assigned = chosen;
            owner[chosen] = d;
            assigned++;
            foreach (int other in reverse[chosen])
            {
                if (demands[other].Assigned >= 0)
                    continue;
                Need(--demands[other].Remaining > 0, "missing");
                if (demands[other].Remaining == 1)
                    ready.Enqueue(other);
            }
        }

        Need(assigned == demands.Count, "ambiguous");
        return new(windows, demands.Select(d => new AssignedCopy(windows[d.Window], d.Entry.Path, d.Sector, copies[d.Assigned])).ToArray(), copies.Where((_, i) => owner[i] < 0).ToArray());
    }

    private static int FirstSubmitAfter(List<int> list, List<SectorCopy> copies, int begin)
    {
        int first = 0, end = list.Count;
        while (first < end)
        {
            int middle = first + (end - first) / 2;
            if (copies[list[middle]].SubmitRecordIndex <= begin)
                first = middle + 1;
            else
                end = middle;
        }

        return first;
    }

    private static int FirstCopyAtOrAfter(List<int> list, List<SectorCopy> copies, int complete)
    {
        int first = 0, end = list.Count;
        while (first < end)
        {
            int middle = first + (end - first) / 2;
            if (copies[list[middle]].CopyRecordIndex < complete)
                first = middle + 1;
            else
                end = middle;
        }

        return first;
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("disk_static_windows_" + reason);
    }
}
