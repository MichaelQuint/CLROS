using System.Collections;
using OS.Tooling;
using Inventory = OS.Tooling.WebRootDiskInventory;
using Reads = OS.Tooling.NativeDiskReadObservations;
using ReadWindows = OS.Tooling.NativeDiskStaticReadWindows;

internal static class NativeDiskStaticReadWindowsTests
{
    private readonly record struct Plan(ulong Sector, int At, bool Cancelled = false);
    private sealed record Vector(Reads.Inspection Device, List<int> Indices, List<string> Rows);

    [HostTest("disk static windows join fragmented unaligned EOF data and preserve unclassified metadata cancellation", HostLane.Native)]
    private static void Positive() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!;
        var wasm = inventory.Find(Inventory.Root + "/_framework/Example.Project.wasm")!;
        var a = Window(file, 19, 53, 65535, 1027);
        var b = Window(wasm, 99, 113, 4000, 97, identity: 2, request: 1001, number: 0);
        var first = file.Cover(a.Offset, a.ReturnedBytes); var second = wasm.Cover(b.Offset, b.ReturnedBytes);
        HostCheck.True(first.Count == 4 && second.Count == 2 && second[^1].ValidBytes == 1);
        List<Plan> plans = [new(inventory.Find(Inventory.Root)!.ShortEntryLba, 10)];
        plans.AddRange(first.Select((s, i) => new Plan(s.Lba, 20 + i * 10)));
        plans.AddRange(second.Select((s, i) => new Plan(s.Lba, 100 + i * 10)));
        plans.Add(new(file.ShortEntryLba, 150, true));
        var vector = Device(raw, plans); var result = Inspect(inventory, vector, [a, b]);
        HostCheck.Equal(6, result.Assigned.Count); HostCheck.Equal(1, result.Residual.Count);
        HostCheck.True(result.Assigned.Take(4).Select(s => s.Sector).SequenceEqual(first) &&
            result.Assigned.Skip(4).Select(s => s.Sector).SequenceEqual(second));
        HostCheck.True(first[0].Lba + 1 != first[1].Lba && result.Residual[0].CopyRecordIndex == 12);
        foreach (var assigned in result.Assigned)
        {
            HostCheck.True(assigned.Window.BeginRecordIndex < assigned.Copy.SubmitRecordIndex &&
                assigned.Copy.SubmitRecordIndex < assigned.Copy.ValidationRecordIndex &&
                assigned.Copy.ValidationRecordIndex < assigned.Copy.CopyRecordIndex &&
                assigned.Copy.CopyRecordIndex < assigned.Window.CompleteRecordIndex);
            HostCheck.True(assigned.Copy.Observation.Sector == assigned.Sector.Lba && assigned.Copy.Observation.Bytes == 512 &&
                assigned.Window.RequestGeneration != assigned.Copy.Observation.Generation &&
                assigned.Window.DescriptorGeneration != assigned.Copy.Observation.Generation);
            HostCheck.Equal(vector.Device.Rows[assigned.Copy.ObservationIndex], assigned.Copy.Observation);
        }
        HostCheck.True(result.Assigned.Select(s => s.Copy.ObservationIndex).Distinct().Count() == 6 && vector.Device.Cancelled == 1);
        HostCheck.True(!result.CheckedResidualMetadata && !result.CheckedSchedulerOrMemory &&
            !result.GrantsNativeProcessOrHttpAuthority && !result.AuthenticatedSameWholeImage);
        var empty = Inspect(inventory, vector, []);
        HostCheck.True(empty.Assigned.Count == 0 && empty.Residual.Count == 7);
    });

    [HostTest("disk static windows propagate forced copies across overlapping same-file windows without guessing", HostLane.Native)]
    private static void ForcedOverlap() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!; ulong lba = file.Sectors[0].Lba;
        var vector = Device(raw, [new(lba, 20), new(lba, 40), new(lba, 60)]);
        var wide = Window(file, 10, 70);
        var medium = Window(file, 10, 50, identity: 2, request: 1001);
        var narrow = Window(file, 10, 30, identity: 3, request: 1002);
        var result = Inspect(inventory, vector, [wide, medium, narrow]);
        HostCheck.True(result.Assigned.Select(a => a.Copy.CopyRecordIndex).SequenceEqual(new[] { 62, 42, 22 }));
        HostCheck.Equal(0, result.Residual.Count);
        var reversed = Inspect(inventory, vector, [narrow, medium, wide]);
        HostCheck.True(reversed.Assigned.Select(a => a.Copy.CopyRecordIndex).SequenceEqual(new[] { 22, 42, 62 }));
    });

    [HostTest("disk static windows reject ambiguous same-file attribution and duplicate candidate copies", HostLane.Native)]
    private static void Ambiguous() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!; ulong lba = file.Sectors[0].Lba;
        var vector = Device(raw, [new(lba, 20), new(lba, 40)]);
        var a = Window(file, 10, 50); var b = Window(file, 10, 50, identity: 2, request: 1001);
        Reject(() => Inspect(inventory, vector, [a]), "ambiguous"); // Extra matching copy cannot silently become residual.
        Reject(() => Inspect(inventory, vector, [a, b]), "ambiguous");
        var conflict = Device(raw, [new(lba, 20), new(file.ShortEntryLba, 40)]);
        Reject(() => Inspect(inventory, conflict, [a, b]), "missing"); // One sector cannot cover two reads.
        var unique = Inspect(inventory, vector, [a with { CompleteRecordIndex = 30 }, b]);
        HostCheck.True(unique.Assigned[0].Copy.CopyRecordIndex == 22 && unique.Assigned[1].Copy.CopyRecordIndex == 42);
    });

    [HostTest("disk static windows reject missing LBA cross-window stages and malformed original index lists", HostLane.Native)]
    private static void MissingAndCausality() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!;
        var vector = Device(raw, [new(file.Sectors[0].Lba, 10)]); var good = Window(file, 9, 13);
        HostCheck.Equal(1, Inspect(inventory, vector, [good]).Assigned.Count);
        foreach (var mutant in new[] { good with { BeginRecordIndex = 10 }, good with { BeginRecordIndex = 11 },
            good with { CompleteRecordIndex = 12 }, good with { CompleteRecordIndex = 11 }, good with { Offset = 512 } })
            Reject(() => Inspect(inventory, vector, [mutant]), "missing");
        Reject(() => Inspect(inventory, vector, [good with { ReturnedBytes = 513, Requested = 513, OutputPrefix = 513 }]), "demand_bound");
        var wrongFile = good with { Inode = inventory.Find(Inventory.Root + "/fixture/large.bin")!.Inode };
        Reject(() => Inspect(inventory, vector, [wrongFile]), "missing"); // Equal fixture patterns do not prove physical identity.
        foreach (int[] indexes in new[] { new[] { 10, 10, 12 }, new[] { 10, 12, 11 }, new[] { -1, 11, 12 }, new[] { 10, 11 }, new[] { 10, 11, 12, 13 } })
            Reject(() => ReadWindows.Inspect(inventory, vector.Device, indexes, [good]), "io_indices");
        Reject(() => ReadWindows.Inspect(inventory, vector.Device, null!, [good]), "io_indices");
    });

    [HostTest("disk static windows reject forged positive metadata ceilings identities and finite work overflows", HostLane.Native)]
    private static void BoundsAndWindows() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!;
        var vector = Device(raw, [new(file.Sectors[0].Lba, 10)]); var good = Window(file, 9, 13);
        var ceiling = Window(file, 9, 173, 511, 8192) with { Requested = 65536 };
        var atCeiling = Device(raw, file.Cover(ceiling.Offset, ceiling.ReturnedBytes).Select((s, i) => new Plan(s.Lba, 10 + i * 10)));
        HostCheck.Equal(17, Inspect(inventory, atCeiling, [ceiling]).Assigned.Count);
        foreach (var mutant in new[]
        {
            good with { Identity = 0 }, good with { Ordinal = 0 }, good with { Number = 1 }, good with { Number = 18 },
            good with { DescriptorGeneration = 0 }, good with { RequestGeneration = 0 }, good with { BeginRecordIndex = -1 },
            good with { CompleteRecordIndex = 9 }, good with { Requested = 0 }, good with { OutputPrefix = 0 },
            good with { OutputPrefix = 8193, Requested = 8193 }, good with { ReturnedBytes = 0 },
            good with { ReturnedBytes = 8193, Requested = 8193, OutputPrefix = 8192 },
            good with { ReturnedBytes = 2 }, good with { Requested = 2, ReturnedBytes = 2 },
            good with { Offset = ulong.MaxValue }, good with { Offset = file.Bytes },
            good with { Offset = file.Bytes - 1, ReturnedBytes = 2, Requested = 2, OutputPrefix = 2 },
            good with { Inode = inventory.Find(Inventory.Root)!.Inode }, good with { Inode = ulong.MaxValue }
        }) Reject(() => Inspect(inventory, vector, [mutant]));
        var two = Device(raw, [new(file.Sectors[0].Lba, 10), new(file.Sectors[0].Lba, 30)]);
        Reject(() => Inspect(inventory, two, [good, good with { BeginRecordIndex = 29, CompleteRecordIndex = 33, RequestGeneration = 1001 }]), "duplicate_window");
        Reject(() => Inspect(inventory, two, [good, good with { Identity = 2, BeginRecordIndex = 29, CompleteRecordIndex = 33 }]), "duplicate_window");
        Reject(() => ReadWindows.Inspect(inventory, vector.Device, vector.Indices, new CountOnly<ReadWindows.ReadWindow>(ReadWindows.MaximumWindows + 1)), "input");
        Reject(() => ReadWindows.Inspect(null!, vector.Device, vector.Indices, [good]), "input");
        Reject(() => ReadWindows.Inspect(inventory, null!, vector.Indices, [good]), "input");
        var many = Device(raw, Enumerable.Range(0, 513).Select(i => new Plan(file.Sectors[0].Lba, 10 + i * 10)));
        var crowded = Enumerable.Range(0, 512).Select(i => Window(file, 0, 6000, identity: (ulong)i + 1, request: (ulong)i + 1)).ToArray();
        Reject(() => Inspect(inventory, many, crowded), "candidate_bound");
    });

    [HostTest("disk static windows compare complete valid sector file bytes against independent inventory and expose padding boundary", HostLane.Native)]
    private static void IndependentBytes() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!;
        var window = Window(file, 9, 13); var plan = new Plan(file.Sectors[0].Lba, 10);
        byte[] changed = raw.ToArray(); changed[checked((int)plan.Sector * 512 + 400)] ^= 1;
        // The device parser independently accepts its supplied raw image. The
        // new join must still reject differing inventory data, even outside the
        // single returned byte but inside this required sector's valid file bytes.
        var foreign = Device(changed, [plan]); Reject(() => Inspect(inventory, foreign, [window]), "image_bytes");
        var tail = inventory.Find(Inventory.Root + "/_framework/Example.Project.wasm")!;
        HostCheck.Equal(1U, tail.Sectors[^1].ValidBytes);
        var last = new Plan(tail.Sectors[^1].Lba, 10); var end = Window(tail, 9, 13, 4096);
        changed = raw.ToArray(); changed[checked((int)last.Sector * 512)] ^= 1;
        Reject(() => Inspect(inventory, Device(changed, [last]), [end]), "image_bytes");
        changed = raw.ToArray(); changed[checked((int)last.Sector * 512 + 1)] ^= 1;
        var padding = Inspect(inventory, Device(changed, [last]), [end]);
        HostCheck.True(padding.Assigned.Count == 1 && !padding.AuthenticatedSameWholeImage);
    });

    [HostTest("disk static windows own immutable assignments windows indices and residual values after borrowed lists change", HostLane.Native)]
    private static void Immutable() => WithInventory((raw, inventory) =>
    {
        var file = inventory.Find(Inventory.Root + "/fixture/fragmented.bin")!;
        var vector = Device(raw, [new(file.Sectors[0].Lba, 10), new(file.ShortEntryLba, 30)]);
        List<ReadWindows.ReadWindow> windows = [Window(file, 9, 13)];
        var result = Inspect(inventory, vector, windows); var assigned = result.Assigned[0]; var residual = result.Residual[0];
        vector.Indices[0] = int.MaxValue; vector.Indices.Clear(); vector.Rows.Clear(); windows[0] = default; windows.Clear(); Array.Clear(raw);
        HostCheck.Equal(assigned, result.Assigned[0]); HostCheck.Equal(residual, result.Residual[0]);
        HostCheck.Equal(assigned.Window, result.Windows[0]); HostCheck.Equal(1024, assigned.Copy.Observation.DataHex.Length);
        bool a = false, w = false, r = false;
        try { ((IList<ReadWindows.AssignedCopy>)result.Assigned)[0] = default; } catch (NotSupportedException) { a = true; }
        try { ((IList<ReadWindows.ReadWindow>)result.Windows)[0] = default; } catch (NotSupportedException) { w = true; }
        try { ((IList<ReadWindows.SectorCopy>)result.Residual)[0] = default; } catch (NotSupportedException) { r = true; }
        HostCheck.True(a && w && r);
    });

    private static ReadWindows.ReadWindow Window(Inventory.Entry entry, int begin, int complete,
        ulong offset = 0, uint bytes = 1, ulong identity = 1, ulong request = 1000, ulong number = 17)
        => new(identity, 7, number, 500, request, begin, complete, entry.Inode, offset, bytes, bytes, bytes);
    private static ReadWindows.Inspection Inspect(Inventory.Inspection inventory, Vector vector, IReadOnlyList<ReadWindows.ReadWindow> windows)
        => ReadWindows.Inspect(inventory, vector.Device, vector.Indices, windows);
    private static Vector Device(byte[] image, IEnumerable<Plan> source)
    {
        const ulong bootstrap = 65534, bootstrapCopied = 65530;
        ulong submitted = bootstrap, completed = bootstrap, copied = bootstrapCopied, clock = 1;
        List<string> rows = []; List<int> indices = [];
        foreach (var plan in source)
        {
            ulong generation = ++submitted;
            Row(0, submitted, 512, plan.At); completed++;
            Row(1, completed, plan.Cancelled ? 0U : 512U, plan.At + 1);
            if (!plan.Cancelled) Row(2, ++copied, 512, plan.At + 2);
            void Row(uint kind, ulong sequence, uint bytes, int at)
            {
                string row = FormattableString.Invariant($"M16_023_IO kind={kind} owner=4194312 epoch=1193046 generation={generation} sector={plan.Sector} sequence={sequence} bytes={bytes} available={unchecked((ushort)submitted)} consumed={unchecked((ushort)completed)} clock={clock++}");
                if (kind == 2) row += " copied=committed data=" + Convert.ToHexString(image.AsSpan(checked((int)plan.Sector * 512), 512)).ToLowerInvariant();
                rows.Add(row); indices.Add(at);
            }
        }
        var boundary = new Reads.Boundary(4194312, 1193046, bootstrap, bootstrapCopied, submitted, completed, copied);
        return new(Reads.Inspect(rows, image, boundary), indices, rows);
    }
    private static void WithInventory(Action<byte[], Inventory.Inspection> action)
        => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
        {
            string output = Path.Combine(directory, "static-read-windows.raw");
            var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), output);
            byte[] raw = File.ReadAllBytes(output); action(raw, Inventory.Inspect(output, raw, receipt));
        });
    private static void Reject(Action action, string? reason = null)
    {
        bool rejected = false;
        try { action(); } catch (ToolFailure failure) { rejected = reason is null || failure.Reason == "disk_static_windows_" + reason; }
        HostCheck.True(rejected, reason ?? "expected rejected disk static read window");
    }
    private sealed class CountOnly<T>(int count) : IReadOnlyList<T>
    {
        public int Count => count;
        public T this[int index] => throw new InvalidOperationException("count_rejection_must_precede_indexing");
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("count_rejection_must_precede_enumeration");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
