using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;

// Independent numeric media and actual fake IN/OUT/FLUSH, not a snapshot passed
// into the engine. Fault controls end the simulated machine after quiescence;
// they do not claim a successful dirty-engine Stop or recovered filesystem.
internal static unsafe class ContinuingFat32MutationTests
{
    private const int First = 8, Reserved = 32, FatSectors = 512, Clusters = 65525;
    private const int Data = First + Reserved + 2 * FatSectors, Sectors = Data + Clusters;
    private static byte Value(int i) => unchecked((byte)(i * 17 + 5));
    private static void Put16(Span<byte> target, int offset, ushort value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(target[offset..], value);
    private static void Put32(Span<byte> target, int offset, uint value) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(target[offset..], value);
    private static uint U32(ReadOnlySpan<byte> source, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
    private static C.Status Open(Fixture f, string name, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginOpenRoot(f.State, name, &value); request = value; return status; }
    private static C.FileToken File(Fixture f, string name = "DATA.BIN")
    {
        HostCheck.Equal(C.Status.Pending, Open(f, name, out var request)); HostCheck.Equal(C.Status.Ready, f.Run());
        C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(f.State, request, &file)); return file;
    }
    private static C.Status Overwrite(Fixture f, C.FileToken file, ulong offset, ReadOnlySpan<byte> bytes, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginOverwrite(f.State, file, offset, bytes, &value); request = value; return status; }
    private static C.Status Create(Fixture f, string name, ReadOnlySpan<byte> alias, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginCreateEmpty(f.State, name, alias, &value); request = value; return status; }
    private static C.Status Grow(Fixture f, C.FileToken file, uint length, ReadOnlySpan<uint> candidates, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginGrow(f.State, file, length, candidates, &value); request = value; return status; }
    private static C.Status Read(Fixture f, C.FileToken file, ulong offset, uint bytes, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginRead(f.State, file, offset, bytes, &value); request = value; return status; }
    private static C.Status Truncate(Fixture f, C.FileToken file, uint length, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginTruncate(f.State, file, length, &value); request = value; return status; }
    private static C.Status Delete(Fixture f, C.FileToken file, out C.RequestToken request)
    { C.RequestToken value = default; var status = C.BeginDelete(f.State, file, &value); request = value; return status; }
    private static C.FreeCursor Cursor(Fixture f, uint first = 2)
    { C.FreeCursor value = default; HostCheck.Equal(C.Status.Success, C.CreateFreeCursor(f.State, first, &value)); return value; }
    private static C.RequestToken Search(Fixture f, C.FreeCursor cursor, uint wanted, uint clusters, uint reads)
    { C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginFindFree(f.State, cursor, wanted, clusters, reads, &request)); return request; }
    private static C.FreeResult Free(Fixture f, C.RequestToken request, Span<uint> output, C.Status expected = C.Status.Success)
    { C.FreeResult result = default; HostCheck.Equal(expected, C.CopyFree(f.State, request, output, &result)); return result; }
    private static C.Result Result(Fixture f, C.RequestToken request, C.Status expected = C.Status.Success)
    { C.Result result = default; HostCheck.Equal(expected, C.CopyResult(f.State, request, &result)); HostCheck.Equal(expected, result.Status); return result; }

    private delegate bool RetainedReadRange(C.State* state, ulong epoch, B.State* block, ulong blockEpoch, ulong first, ulong last);
    private static readonly RetainedReadRange ReadRangePredicate = typeof(C).GetMethod("CanRetainReadRange",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.CreateDelegate<RetainedReadRange>();
    private static bool Range(Fixture f, ulong first = 1, ulong last = First - 1) =>
        ReadRangePredicate(f.State, f.Input.Epoch, f.Device.State, f.Device.Input.Epoch, first, last);

    [HostTest("continuing retained readonly range uses original partition inclusive bounds capacity and exact engine block identities without effects")]
    private static void RetainReadRangeBounds()
    {
        using var f = new Fixture(spareSectors: 16); f.Mount(); ulong end = f.Input.PartitionLast, capacity = f.Device.State->SectorCount;
        HostCheck.True(Range(f) && Range(f, end + 1, capacity - 1));
        foreach (var pair in new (ulong First, ulong Last)[] { (0, 0), (0, 7), (2, 1), (1, First), (First, First),
            (end, end + 1), (end, end), (capacity, capacity), (1, ulong.MaxValue) }) HostCheck.True(!Range(f, pair.First, pair.Last));
        HostCheck.True(!ReadRangePredicate(f.State, 0, f.Device.State, f.Device.Input.Epoch, 1, 7) &&
            !ReadRangePredicate(f.State, f.Input.Epoch + 1, f.Device.State, f.Device.Input.Epoch, 1, 7) &&
            !ReadRangePredicate(f.State, f.Input.Epoch, f.Device.State, f.Device.Input.Epoch + 1, 1, 7));
        B.State* other = (B.State*)NativeMemory.AllocZeroed((nuint)sizeof(B.State));
        try { *other = *f.Device.State; HostCheck.True(!ReadRangePredicate(f.State, f.Input.Epoch, other, f.Device.Input.Epoch, 1, 7)); }
        finally { NativeMemory.Free(other); }
        byte[] engine = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray(); uint calls = f.Device.Hardware->Calls;
        long allocated = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 64; i++) good &= Range(f) && !Range(f, First, First);
        HostCheck.True(good && allocated == GC.GetAllocatedBytesForCurrentThread() && calls == f.Device.Hardware->Calls &&
            engine.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(C.State))));
    }

    [HostTest("continuing retained readonly range refuses rewritten data geometry epoch ABA and overlapping backing while retaining original stopped owner")]
    private static void RetainReadRangeTamper()
    {
        using var f = new Fixture(observe: true); f.Mount();
        C.Admission* input = (C.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input")); var saved = *input;
        try
        {
            input->PartitionFirst = 1; input->PartitionLast = 7; HostCheck.True(!Range(f)); *input = saved;
            input->Before = input->After; HostCheck.True(!Range(f));
        }
        finally { *input = saved; }
        LiveFat32Volume* volume = (LiveFat32Volume*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Volume")); var originalVolume = *volume;
        try { volume->PartitionFirstLba = 1; volume->PartitionLastLba = 7; HostCheck.True(!Range(f)); }
        finally { *volume = originalVolume; }
        B.Admission* block = (B.Admission*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "Input")); ulong epoch = block->Epoch;
        try { block->Epoch++; HostCheck.True(!Range(f)); } finally { block->Epoch = epoch; }
        ulong* poolEpoch = (ulong*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "PoolEpoch")); ulong pool = *poolEpoch;
        try { (*poolEpoch)++; HostCheck.True(!Range(f)); } finally { *poolEpoch = pool; }
        HostCheck.True(Range(f)); HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.True(Range(f));
        HostCheck.Equal(C.Status.Success, C.Stop(f.State)); HostCheck.True(!Range(f));
    }

    [HostTest("continuing external IN pending and held results retain structural range but exclude every new engine turn and Stop until actual drain")]
    private static void RetainReadRangeQueueExclusion()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); B.RequestToken read = default;
        HostCheck.Equal(B.Status.Success, B.SubmitRead(f.Device.State, 1, &read));
        for (int phase = 0; phase < 2; phase++)
        {
            HostCheck.True(Range(f) && f.Device.State->RequestPending && !f.State->RequestActive);
            ulong submitted = f.Device.State->Submitted; C.RequestToken rejected = default;
            HostCheck.Equal(C.Status.Busy, C.BeginInspectRoot(f.State, &rejected));
            HostCheck.Equal(C.Status.Busy, C.BeginOpenRoot(f.State, "DATA.BIN", &rejected));
            HostCheck.Equal(C.Status.Busy, C.BeginOverwrite(f.State, file, 0, new byte[1], &rejected));
            HostCheck.Equal(C.Status.Busy, C.Stop(f.State)); HostCheck.True(submitted == f.Device.State->Submitted && rejected.Owner == 0);
            if (phase == 0)
            { f.Device.Complete(); B.Completion completion = default; HostCheck.Equal(B.Status.Ready, B.Poll(f.Device.State, &completion)); }
        }
        HostCheck.Equal(B.Status.Success, B.Cancel(f.Device.State, read)); HostCheck.True(!f.Device.State->RequestPending);
        C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginInspectRoot(f.State, &request));
        HostCheck.True(f.State->RequestActive && Range(f)); // Structural true never grants a port submission.
        HostCheck.Equal(C.Status.Ready, f.Run()); C.RootFacts facts = default;
        HostCheck.Equal(C.Status.Success, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
        HostCheck.True(Range(f));
    }

    [HostTest("continuing retained readonly range survives clean faults and dirty recovery solely as ownership while device reset never authorizes discard")]
    private static void RetainReadRangeFailureOwnership()
    {
        using (var f = new Fixture())
        {
            f.Mount(); C.RequestToken request = default; C.BeginInspectRoot(f.State, &request); f.FailAt = f.Device.Hardware->Notifies + 1;
            HostCheck.Equal(C.Status.BlockFailure, f.Run()); HostCheck.True(Range(f));
            B.Stop(f.Device.State); HostCheck.True(Range(f)); HostCheck.Equal(C.Status.Success, C.Stop(f.State)); HostCheck.True(!Range(f));
        }
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); Overwrite(f, file, 0, new byte[37], out var request);
            for (int i = 0; i < 10000 && !f.State->Dirty; i++) f.Turn();
            HostCheck.True(f.State->Dirty && Range(f)); var cancelled = C.Cancel(f.State, request);
            HostCheck.True(cancelled is C.Status.Pending or C.Status.RecoveryRequired);
            if (cancelled == C.Status.Pending) HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
            HostCheck.True(Range(f)); B.Stop(f.Device.State); HostCheck.True(Range(f));
            HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State)); HostCheck.True(Range(f) && f.State->Dirty);
        }
    }

    [HostTest("continuing FAT root facts walk actual fragmented paired FAT sectors and report complete multisector allocation without file tokens")]
    private static void RootAllocationFacts()
    {
        foreach (byte sectors in new byte[] { 1, 2, 4 })
        {
            using var f = new Fixture(sectors); f.Fat(2, 129); f.Fat(129, 5); f.Fat(5, 0xfffffff); f.Mount();
            C.RequestToken request = default; C.RootFacts facts = default; ulong submitted = f.Device.State->Submitted;
            int trace = f.Trace.Count; HostCheck.Equal(C.Status.Pending, C.BeginInspectRoot(f.State, &request));
            HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(submitted + 6, f.Device.State->Submitted);
            HostCheck.True(f.Trace.Skip(trace).All(row => row.Type == 0));
            uint calls = f.Device.Hardware->Calls; long allocated = GC.GetAllocatedBytesForCurrentThread();
            HostCheck.Equal(C.Status.Success, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
            HostCheck.True(calls == f.Device.Hardware->Calls && allocated == GC.GetAllocatedBytesForCurrentThread());
            HostCheck.True(facts.Owner == (ulong)f.State && facts.Epoch == f.Input.Epoch && facts.Revision == f.State->VolumeRevision &&
                facts.FirstSector == Data && facts.ClusterBytes == sectors * 512U && facts.AllocatedClusters == 3 && facts.Allocated512Blocks == 3UL * sectors);
            HostCheck.True(!f.State->RequestActive && !f.State->Dirty && !RuntimeHelpers.IsReferenceOrContainsReferences<C.RootFacts>());
            HostCheck.Equal(C.Status.StaleToken, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
        }
        Console.WriteLine("HOST_FAT_ROOT_FACTS_SIZE state=" + sizeof(C.State) + " rootFacts=" + sizeof(C.RootFacts));
    }

    [HostTest("continuing FAT root facts reject wrong sizes aliases kinds and stale requests before consuming held allocation")]
    private static void RootFactsOutput()
    {
        using var f = new Fixture(); f.Mount(); C.RequestToken request = default;
        HostCheck.Equal(C.Status.InvalidStorage, C.BeginInspectRoot(f.State, (C.RequestToken*)f.Input.Before));
        HostCheck.Equal(C.Status.Pending, C.BeginInspectRoot(f.State, &request)); HostCheck.Equal(C.Status.Ready, f.Run());
        C.RootFacts* output = (C.RootFacts*)f.Output; new Span<byte>(output, sizeof(C.RootFacts)).Fill(0xa5);
        foreach (uint bytes in new uint[] { 0, (uint)sizeof(C.RootFacts) - 1, (uint)sizeof(C.RootFacts) + 1 })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyRootFacts(f.State, request, output, bytes));
        HostCheck.True(new ReadOnlySpan<byte>(output, sizeof(C.RootFacts)).IndexOfAnyExcept((byte)0xa5) < 0);
        foreach (ulong address in new[] { (ulong)f.State, (ulong)f.Input.Before, (ulong)f.Input.Before + 65528,
            (ulong)f.Input.DirectoryMap, (ulong)f.Device.Data, 8UL, ulong.MaxValue - 7 })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyRootFacts(f.State, request, (C.RootFacts*)address, (uint)sizeof(C.RootFacts)));
        var wrong = request; ((ulong*)&wrong)[2]++; HostCheck.Equal(C.Status.StaleToken, C.CopyRootFacts(f.State, wrong, output, (uint)sizeof(C.RootFacts)));
        HostCheck.True(f.State->IsReady && output->Owner == 0);
        HostCheck.Equal(C.Status.Success, C.CopyRootFacts(f.State, request, output, (uint)sizeof(C.RootFacts)));
        Open(f, "DATA.BIN", out request); f.Run();
        HostCheck.Equal(C.Status.StaleToken, C.CopyRootFacts(f.State, request, output, (uint)sizeof(C.RootFacts)));
        C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(f.State, request, &file));
    }

    [HostTest("continuing FAT root facts retain actual mirror cycle and I O failures instead of publishing geometry as successful allocation")]
    private static void RootFactsCorruption()
    {
        foreach (bool mirror in new[] { false, true })
        {
            using var f = new Fixture(); f.Mount();
            if (mirror) Put32(f.Volatile, (First + Reserved + FatSectors) * 512 + 8, 5); else f.Fat(2, 2);
            C.RequestToken request = default; C.RootFacts facts = default; C.BeginInspectRoot(f.State, &request);
            HostCheck.Equal(C.Status.Ready, f.Run());
            HostCheck.Equal(mirror ? C.Status.MirrorMismatch : C.Status.CorruptChain, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
            HostCheck.True(facts.Owner == 0 && !f.State->RequestActive && !f.State->Dirty);
        }
        using (var f = new Fixture())
        {
            f.Mount(); C.RequestToken request = default; C.RootFacts facts = default; C.BeginInspectRoot(f.State, &request);
            f.FailAt = f.Device.Hardware->Notifies + 1; HostCheck.Equal(C.Status.BlockFailure, f.Run());
            HostCheck.Equal(C.Status.BlockFailure, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
            HostCheck.True(facts.Owner == 0 && !f.State->Dirty);
        }
    }

    [HostTest("continuing FAT root facts cancel pending and held reads and reject original DMA retirement and request reuse")]
    private static void RootFactsLifetime()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(); f.Mount(); C.RequestToken request = default; C.RootFacts facts = default;
            C.BeginInspectRoot(f.State, &request);
            if (held) f.Run(); else { C.Pump(f.State); HostCheck.True(f.Device.Hardware->Pending); }
            HostCheck.Equal(held ? C.Status.Ready : C.Status.Pending, C.Cancel(f.State, request));
            HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(C.Status.Cancelled, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
            HostCheck.True(facts.Owner == 0 && !f.State->RequestActive);
            C.RequestToken next = default; C.BeginInspectRoot(f.State, &next); f.Run();
            HostCheck.Equal(C.Status.StaleToken, C.CopyRootFacts(f.State, request, &facts, (uint)sizeof(C.RootFacts)));
            ulong* generation = (ulong*)((byte*)f.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Device.Slot, "Generation")); ulong saved = *generation;
            try { (*generation)++; HostCheck.Equal(C.Status.BlockFailure, C.CopyRootFacts(f.State, next, &facts, (uint)sizeof(C.RootFacts))); }
            finally { *generation = saved; }
            HostCheck.True(f.State->IsReady && facts.Owner == 0);
            B.Stop(f.Device.State); HostCheck.Equal(C.Status.BlockFailure, C.CopyRootFacts(f.State, next, &facts, (uint)sizeof(C.RootFacts)));
            HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
    }

    [HostTest("continuing FAT copied open allocation facts use actual paired chain and multisector cluster geometry without IO or layout growth")]
    private static void OpenAllocationFacts()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<C.OpenFacts>() && sizeof(C.OpenFacts) == sizeof(C.FileToken) + 16);
        foreach (byte sectors in new byte[] { 1, 2, 4, 8 })
        {
            using var f = new Fixture(sectors); if (sectors != 1) f.Fat(4, 0xfffffff);
            f.Mount(); HostCheck.Equal(C.Status.Pending, Open(f, "DATA.BIN", out var request)); HostCheck.Equal(C.Status.Ready, f.Run());
            uint calls = f.Device.Hardware->Calls; ulong submitted = f.Device.State->Submitted;
            C.OpenFacts facts = default; long allocated = GC.GetAllocatedBytesForCurrentThread();
            HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
            HostCheck.True(GC.GetAllocatedBytesForCurrentThread() == allocated && f.Device.Hardware->Calls == calls && f.Device.State->Submitted == submitted);
            HostCheck.True(facts.ClusterBytes == sectors * 512U && facts.AllocatedClusters == (sectors == 1 ? 2U : 1U) &&
                facts.Allocated512Blocks == facts.AllocatedClusters * (ulong)sectors && facts.File.Length == 900 && facts.File.FirstCluster == 4 &&
                facts.File.Revision == f.State->VolumeRevision && !f.State->RequestActive && f.Trace.All(row => row.Type == 0));
            if (sectors >= 4) HostCheck.True(facts.Allocated512Blocks != (facts.File.Length + 511UL) / 512);
            PhysicalFingerprint(facts.File, f.Volatile.Slice(Data * 512 + 32, 32));
            // Independently follow the literal low-28 entries in both actual
            // fake-medium FATs, rather than deriving an allocation from size.
            uint cluster = facts.File.FirstCluster, count = 0;
            while (true)
            {
                uint left = U32(f.Volatile, (First + Reserved) * 512 + checked((int)cluster) * 4) & 0xfffffff;
                uint right = U32(f.Volatile, (First + Reserved + FatSectors) * 512 + checked((int)cluster) * 4) & 0xfffffff;
                HostCheck.Equal(left, right); HostCheck.True(++count <= 2);
                if (left >= 0xffffff8) break; cluster = left;
            }
            HostCheck.Equal(count, facts.AllocatedClusters);
            C.FileToken old = default; HostCheck.Equal(C.Status.StaleToken, C.CopyOpen(f.State, request, &old));
            C.OpenFacts duplicate = default; HostCheck.Equal(C.Status.StaleToken, C.CopyOpenFacts(f.State, request, &duplicate, (uint)sizeof(C.OpenFacts)));
            HostCheck.Equal(0UL, duplicate.File.Owner); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
        Console.WriteLine($"CONTINUING_OPEN_FACTS_LAYOUT state={sizeof(C.State)} token={sizeof(C.FileToken)} facts={sizeof(C.OpenFacts)}");
    }

    [HostTest("continuing FAT empty open allocation is zero while raw historical metadata remains unchanged")]
    private static void EmptyOpenFacts()
    {
        using var f = new Fixture(4); Span<byte> raw = f.Volatile.Slice(Data * 512 + 32, 32);
        Put16(raw, 26, 0); Put32(raw, 28, 0); raw[11] = 0x21; Put16(raw, 24, 33);
        f.Mount(); Open(f, "DATA.BIN", out var request); HostCheck.Equal(C.Status.Ready, f.Run()); C.OpenFacts facts = default;
        HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(facts.ClusterBytes == 2048 && facts.AllocatedClusters == 0 && facts.Allocated512Blocks == 0 &&
            facts.File.Length == 0 && facts.File.FirstCluster == 0 && facts.File.EntryOffset == (ulong)Data * 512 + 32);
        PhysicalFingerprint(facts.File, raw); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        HostCheck.True(facts.ClusterBytes == 2048 && facts.File.Length == 0); // Copied historical value survives owner retirement.
    }

    [HostTest("continuing FAT open allocation rejects preallocated overlong mirrored and directory targets without consuming failed opens")]
    private static void InvalidOpenFacts()
    {
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            using var f = new Fixture(); Span<byte> raw = f.Volatile.Slice(Data * 512 + 32, 32);
            C.Status expected = C.Status.CorruptChain;
            if (mode == 0) Put32(raw, 28, 0); // A zero-length file may not retain a cluster.
            if (mode == 1) { f.Fat(6, 8); f.Fat(8, 0xfffffff); } // Three clusters for900B is overlong.
            if (mode == 2)
            {
                // Put the target in a different FAT sector: corruption in the
                // root's shared FAT sector correctly rejects mount itself.
                Put16(raw, 26, 128); f.Fat(128, 130); f.Fat(130, 0xfffffff);
                Put32(f.Volatile, (First + Reserved + FatSectors) * 512 + 128 * 4, 0xfffffff); expected = C.Status.MirrorMismatch;
            }
            if (mode == 3) { raw[11] = 0x10; Put32(raw, 28, 0); expected = C.Status.Unsupported; }
            f.Mount(); Open(f, "DATA.BIN", out var request); HostCheck.Equal(C.Status.Ready, f.Run());
            ulong submitted = f.Device.State->Submitted; uint calls = f.Device.Hardware->Calls; C.OpenFacts facts = default;
            HostCheck.Equal(expected, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
            HostCheck.Equal(expected, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
            HostCheck.True(f.State->RequestActive && facts.File.Owner == 0 && facts.ClusterBytes == 0 && facts.AllocatedClusters == 0 && facts.Allocated512Blocks == 0 &&
                f.Device.State->Submitted == submitted && f.Device.Hardware->Calls == calls && f.Device.State->SubmittedBytes == 0);
            C.FileToken file = default; HostCheck.Equal(expected, C.CopyOpen(f.State, request, &file));
            HostCheck.True(!f.State->RequestActive && file.Owner == 0); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT open facts validate complete output size and aliases before touching caller engine device or ready ownership")]
    private static void OpenFactsAliases()
    {
        using var f = new Fixture(observe: true); f.Mount(); Open(f, "DATA.BIN", out var request); f.Run();
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray(), backing = new ReadOnlySpan<byte>(f.Input.Changes, (int)f.BackingBytes).ToArray();
        byte[] device = new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).ToArray(), dma = new ReadOnlySpan<byte>(f.Device.Data, 4096).ToArray();
        byte[] observed = new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).ToArray(); uint calls = f.Device.Hardware->Calls;
        new Span<byte>(f.Output, 128).Fill(0x5a);
        foreach (uint bytes in new uint[] { 0, (uint)sizeof(C.FileToken), (uint)sizeof(C.OpenFacts) - 1, (uint)sizeof(C.OpenFacts) + 1, uint.MaxValue })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyOpenFacts(f.State, request, (C.OpenFacts*)f.Output, bytes));
        foreach (ulong pointer in new[] { 0UL, 8UL, 0x0000800000000000UL, 0x00007ffffffffff8UL, (ulong)f.Output + 1,
            (ulong)f.State, (ulong)f.Input.Changes, (ulong)f.Input.Before, (ulong)f.Input.After, (ulong)f.Input.DirectoryMap, (ulong)f.Input.FileMap,
            (ulong)f.Input.WalkMap, (ulong)f.Input.Sector, (ulong)f.Input.Fat0, (ulong)f.Input.Fat1, (ulong)f.Input.Input, (ulong)f.Input.Name,
            (ulong)f.Input.LongName, (ulong)f.Input.EncodedEntries, (ulong)f.Input.Before - 80,
            (ulong)f.Device.State, (ulong)f.Device.Pool, (ulong)f.Device.Slot, (ulong)f.Device.Data, (ulong)f.Device.Observation, 0x400000UL, 0x402000UL, 0x403000UL })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyOpenFacts(f.State, request, (C.OpenFacts*)pointer, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(new ReadOnlySpan<byte>(f.Output, 128).IndexOfAnyExcept((byte)0x5a) < 0 && f.Device.Hardware->Calls == calls &&
            new ReadOnlySpan<byte>(f.State, sizeof(C.State)).SequenceEqual(state) && new ReadOnlySpan<byte>(f.Input.Changes, (int)f.BackingBytes).SequenceEqual(backing) &&
            new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).SequenceEqual(device) && new ReadOnlySpan<byte>(f.Device.Data, 4096).SequenceEqual(dma) &&
            new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).SequenceEqual(observed));
        HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, request, (C.OpenFacts*)f.Output, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(((C.OpenFacts*)f.Output)->Allocated512Blocks == 2 && !f.State->RequestActive); C.Stop(f.State);
    }

    [HostTest("continuing FAT open facts distinguish pending cancelled winner foreign and stale request generations without IO")]
    private static void OpenFactsLifetime()
    {
        using var f = new Fixture(); f.Mount(); Open(f, "DATA.BIN", out var old); C.OpenFacts facts = default;
        uint calls = f.Device.Hardware->Calls;
        HostCheck.Equal(C.Status.Pending, C.CopyOpenFacts(f.State, old, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(facts.File.Owner == 0 && f.State->RequestActive && calls == f.Device.Hardware->Calls);
        HostCheck.Equal(C.Status.Ready, C.Cancel(f.State, old));
        HostCheck.Equal(C.Status.Cancelled, C.CopyOpenFacts(f.State, old, &facts, (uint)sizeof(C.OpenFacts))); HostCheck.True(f.State->RequestActive);
        C.FileToken none = default; HostCheck.Equal(C.Status.Cancelled, C.CopyOpen(f.State, old, &none));
        Open(f, "DATA.BIN", out var current); HostCheck.Equal(C.Status.Ready, f.Run()); calls = f.Device.Hardware->Calls;
        foreach (int field in new[] { 0, 1, 2 })
        {
            C.RequestToken foreign = current; ((ulong*)&foreign)[field]++;
            HostCheck.Equal(C.Status.StaleToken, C.CopyOpenFacts(f.State, foreign, &facts, (uint)sizeof(C.OpenFacts)));
        }
        HostCheck.Equal(C.Status.StaleToken, C.CopyOpenFacts(f.State, old, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(f.State->RequestActive && facts.File.Owner == 0 && calls == f.Device.Hardware->Calls);
        HostCheck.Equal(C.Status.Ready, C.Cancel(f.State, current)); // Already validated Open Ready remains the winner.
        HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, current, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(facts.AllocatedClusters == 2 && !f.State->RequestActive); C.Stop(f.State);
    }

    [HostTest("continuing FAT open allocation facts retain copied lineage across mutation but cannot recapture stale tokens")]
    private static void OpenFactsRevisions()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); Open(f, "DATA.BIN", out var first); f.Run(); C.OpenFacts original = default;
        HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, first, &original, (uint)sizeof(C.OpenFacts)));
        Create(f, "SIDE.TXT", "SIDE    TXT"u8, out var mutation); f.Run(); Result(f, mutation);
        C.OpenFacts facts = default; HostCheck.Equal(C.Status.StaleToken, C.CopyOpenFacts(f.State, first, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.Equal(C.Status.StaleToken, Read(f, original.File, 0, 1, out _));
        Open(f, "DATA.BIN", out var next); f.Run(); HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, next, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(facts.File.Revision == original.File.Revision + 1 && facts.AllocatedClusters == original.AllocatedClusters &&
            facts.Allocated512Blocks == original.Allocated512Blocks && facts.ClusterBytes == original.ClusterBytes && facts.File.EntryOffset == original.File.EntryOffset);
        Truncate(f, facts.File, 0, out mutation); f.Run(); C.MutationResult result = default;
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, mutation, &result));
        Open(f, "DATA.BIN", out next); f.Run(); HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, next, &facts, (uint)sizeof(C.OpenFacts)));
        HostCheck.True(facts.File.Length == 0 && facts.AllocatedClusters == 0 && facts.Allocated512Blocks == 0 && original.Allocated512Blocks == 2 && original.File.Length == 900);
        C.Stop(f.State);
    }

    [HostTest("continuing FAT open facts require original running DMA and preserve error or retired request ownership")]
    private static void OpenFactsDeviceLifetime()
    {
        using (var f = new Fixture())
        {
            f.Mount(); Open(f, "DATA.BIN", out var request); f.Run();
            ulong* generation = (ulong*)((byte*)f.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Device.Slot, "Generation")); ulong original = *generation;
            C.OpenFacts facts = default; uint calls = f.Device.Hardware->Calls; (*generation)++;
            try
            {
                HostCheck.Equal(C.Status.BlockFailure, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
                HostCheck.True(f.State->RequestActive && facts.File.Owner == 0 && f.Device.Hardware->Calls == calls);
            }
            finally { *generation = original; }
            HostCheck.Equal(C.Status.Success, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
            Open(f, "DATA.BIN", out request); f.Run(); B.Stop(f.Device.State);
            HostCheck.Equal(C.Status.BlockFailure, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
            HostCheck.True(f.State->RequestActive && facts.File.Owner == 0); C.Stop(f.State);
            HostCheck.Equal(C.Status.InvalidState, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
        }
        using (var f = new Fixture())
        {
            f.Mount(); Open(f, "DATA.BIN", out var request); f.FailAt = f.Device.Hardware->Notifies + 1;
            HostCheck.Equal(C.Status.BlockFailure, f.Run()); C.OpenFacts facts = default;
            HostCheck.Equal(C.Status.BlockFailure, C.CopyOpenFacts(f.State, request, &facts, (uint)sizeof(C.OpenFacts)));
            HostCheck.True(facts.File.Owner == 0 && facts.Allocated512Blocks == 0); B.Stop(f.Device.State); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT POD backing is bounded and mount open use actual sectors without a raw image admission")]
    private static void PodMount()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<C.State>() && !RuntimeHelpers.IsReferenceOrContainsReferences<C.Admission>());
        using var f = new Fixture(); HostCheck.True(f.BackingBytes + (ulong)sizeof(C.State) + (ulong)sizeof(B.State) + 4096 + 112 <= 200 * 1024);
        HostCheck.Equal(C.Status.Pending, C.Start(f.State, f.Input)); HostCheck.Equal(0UL, f.Device.State->Submitted);
        HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.True(f.State->IsMounted && f.Device.State->Submitted == 3 && f.Device.State->SubmittedBytes == 0);
        var file = File(f); HostCheck.True(file.FirstCluster == 4 && file.Length == 900 && file.EntryOffset == (ulong)Data * 512 + 32 && file.Revision == 1);
        HostCheck.True(f.Trace.All(row => row.Type == 0)); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        HostCheck.True(f.State->Retired && f.Device.State->Running); f.Device.CheckGuards();
    }

    [HostTest("continuing FAT copied open facts retain exact raw attributes size timestamps and physical identity without inventing timestamp validity")]
    private static void CopiedFileMetadata()
    {
        ushort leap = (ushort)(((2024 - 1980) << 9) | (2 << 5) | 29), clock = (ushort)((23 << 11) | (58 << 5) | 28);
        long leapSeconds = new DateTimeOffset(2024, 2, 29, 23, 58, 56, TimeSpan.Zero).ToUnixTimeSeconds();
        foreach (var row in new (ushort Date, ushort Time, bool Valid, long Seconds)[]
        {
            (0, 0, true, 0), (33, 0, true, 315532800), (leap, clock, true, leapSeconds),
            ((ushort)(((2023 - 1980) << 9) | (2 << 5) | 29), clock, false, 0),
            (leap, (ushort)(24 << 11), false, 0), (0, 1, false, 0)
        })
        {
            using var f = new Fixture(); var raw = f.Volatile.Slice(Data * 512 + 32, 32);
            raw[11] = 0x21; raw[13] = 79; Put16(raw, 14, 0x645c); Put16(raw, 16, 0x41a1); Put16(raw, 18, 0x42e1);
            Put16(raw, 22, row.Time); Put16(raw, 24, row.Date);
            f.Mount(); var file = File(f); PhysicalFingerprint(file, raw);
            var independent = Fat32Parser.FindRootDirectoryEntry(new Device(f.Device.Hardware->Medium), First, "DATA.BIN");
            HostCheck.True(independent.Succeeded && independent.Entry.Attributes == 0x21 && independent.Entry.FileSize == file.Length &&
                independent.Entry.FirstCluster == file.FirstCluster && file.Length == 900 && file.FirstCluster == 4 &&
                file.Owner == (ulong)f.State && file.Epoch == f.Input.Epoch && file.Revision == f.State->VolumeRevision &&
                file.EntryOffset == (ulong)Data * 512 + 32 && file.EntryOffset / 32 + 256 == (ulong)Data * 16 + 257 &&
                (byte)(file.Fingerprint1 >> 24) == 0x21 && (byte)(file.Fingerprint1 >> 40) == 79 &&
                (ushort)(file.Fingerprint1 >> 48) == 0x645c && (ushort)file.Fingerprint2 == 0x41a1 &&
                (ushort)(file.Fingerprint2 >> 16) == 0x42e1 && (ushort)(file.Fingerprint2 >> 48) == row.Time && (ushort)file.Fingerprint3 == row.Date);
            // CopyOpen authenticates the raw entry and chain, not Gregorian
            // semantics. A provider must decode/reject these nonzero-invalid
            // values, rather than manufacturing a successful timestamp/stat.
            HostCheck.Equal(row.Valid, CopiedModifiedSeconds(file, out long seconds)); HostCheck.Equal(row.Seconds, seconds);
            var keep = File(f, "KEEP.TXT");
            HostCheck.True(keep.EntryOffset != file.EntryOffset && file.Revision == f.State->VolumeRevision &&
                CopiedModifiedSeconds(file, out seconds) == row.Valid && seconds == row.Seconds && f.Device.State->SubmittedBytes == 0 && f.Trace.All(x => x.Type == 0));
            C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT copied metadata stays historical across unrelated and target revisions and cannot authenticate a reused physical inode")]
    private static void FileMetadataRevisions()
    {
        using var f = new Fixture(); PrepareGrowth(f);
        ushort date = (ushort)(((2024 - 1980) << 9) | (2 << 5) | 29), time = (ushort)((12 << 11) | (34 << 5) | 28);
        Put16(f.Volatile, Data * 512 + 32 + 22, time); Put16(f.Volatile, Data * 512 + 32 + 24, date);
        f.Mount(); var original = File(f);
        HostCheck.True(CopiedModifiedSeconds(original, out long modified) && modified == new DateTimeOffset(2024, 2, 29, 12, 34, 56, TimeSpan.Zero).ToUnixTimeSeconds());
        Create(f, "SIDE.TXT", "SIDE    TXT"u8, out var create); f.Run(); Result(f, create);
        uint notifies = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.StaleToken, Read(f, original, 0, 1, out _)); HostCheck.Equal(notifies, f.Device.Hardware->Notifies);
        var current = File(f); PhysicalFingerprint(current, f.Persistent.Slice((int)current.EntryOffset, 32));
        HostCheck.True(current.Revision == original.Revision + 1 && current.EntryOffset == original.EntryOffset && current.Length == original.Length &&
            current.FirstCluster == original.FirstCluster && current.Fingerprint0 == original.Fingerprint0 && current.Fingerprint1 == original.Fingerprint1 &&
            current.Fingerprint2 == original.Fingerprint2 && current.Fingerprint3 == original.Fingerprint3 && CopiedModifiedSeconds(current, out long seconds) && seconds == modified);
        Truncate(f, current, 513, out var shrink); f.Run(); C.MutationResult receipt = default;
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, shrink, &receipt));
        var shrunk = File(f); PhysicalFingerprint(shrunk, f.Persistent.Slice((int)shrunk.EntryOffset, 32));
        HostCheck.True(SameFile(shrunk, receipt.NewFile) && SameFile(current, receipt.OriginalFile) && shrunk.Length == 513 &&
            shrunk.Revision == current.Revision + 1 && CopiedModifiedSeconds(shrunk, out seconds) && seconds == modified);
        notifies = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.StaleToken, Read(f, current, 0, 1, out _)); HostCheck.Equal(notifies, f.Device.Hardware->Notifies);
        Delete(f, shrunk, out var deletion); f.Run(); HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, deletion, &receipt));
        HostCheck.True(receipt.Deleted && receipt.NewFile.Owner == 0);
        Create(f, "OTHER.BIN", "OTHER   BIN"u8, out create); f.Run(); Result(f, create); var reused = File(f, "OTHER.BIN");
        HostCheck.True(reused.EntryOffset == original.EntryOffset && reused.EntryOffset / 32 + 256 == original.EntryOffset / 32 + 256 &&
            reused.Revision > shrunk.Revision && reused.Fingerprint0 != original.Fingerprint0 && reused.Length == 0 && reused.FirstCluster == 0 &&
            CopiedModifiedSeconds(reused, out seconds) && seconds == 0 && CopiedModifiedSeconds(original, out seconds) && seconds == modified);
        notifies = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.StaleToken, Read(f, original, 0, 1, out _)); HostCheck.Equal(notifies, f.Device.Hardware->Notifies);
        C.Stop(f.State);
    }

    // Test-side projection of already copied FAT words using the existing
    // readonly directory UTC/zero-sentinel policy. It grants no live-token,
    // Linux stat, allocation count or timestamp-update authority.
    private static bool CopiedModifiedSeconds(C.FileToken file, out long seconds)
    {
        ushort date = (ushort)file.Fingerprint3, time = (ushort)(file.Fingerprint2 >> 48); seconds = 0;
        return date == 0 && time == 0 || OS.Std.Time.GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15),
            (uint)(date & 31), (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out seconds);
    }

    [HostTest("continuing FAT fragmented in-place RMW preserves boundary bytes and acknowledges only actual final FLUSH")]
    private static void FragmentedOverwrite()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); byte[] prefix = Enumerable.Range(0, 37).Select(i => (byte)(91 + i)).ToArray();
        byte[] expected = Enumerable.Range(0, 900).Select(Value).ToArray(); prefix.CopyTo(expected, 500);
        HostCheck.Equal(C.Status.Pending, Overwrite(f, file, 500, prefix, out var request)); prefix.AsSpan().Clear();
        int before = f.Trace.Count; ulong priorDurable = f.Device.State->DurableBytes;
        while (f.State->IssuedChanges == 0) { HostCheck.Equal(C.Status.Pending, f.Turn()); HostCheck.True(f.Device.State->DurableBytes == priorDurable); }
        HostCheck.True(f.State->Dirty && f.State->ChangeCount == 2 && !f.State->IsReady);
        HostCheck.Equal(C.Status.Pending, C.CopyResult(f.State, request, (C.Result*)f.Output));
        HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(37U, Result(f, request).Bytes);
        var writes = f.Trace.Skip(before).Where(row => row.Type == 1).Select(row => row.Sector).ToArray();
        HostCheck.True(writes.SequenceEqual(new ulong[] { Data + 2, Data + 4 }) && f.Trace[^1].Type == 4);
        HostCheck.True(!f.State->Dirty && f.State->VolumeRevision == 2 && f.Device.State->DurableBytes == 1024);
        Independent(f, expected); HostCheck.True(f.Volatile.SequenceEqual(f.Persistent));
        HostCheck.Equal(C.Status.StaleToken, Overwrite(f, file, 0, new byte[] { 1 }, out _));
        HostCheck.Equal(C.Status.Success, C.Stop(f.State));
    }

    [HostTest("continuing FAT explicit root name token capacity and output alias controls publish nothing")]
    private static void AdmissionAndTokens()
    {
        using var f = new Fixture(); var invalid = f.Input; invalid.ChangeCapacity = 127;
        HostCheck.Equal(C.Status.InvalidStorage, C.Start(f.State, invalid)); invalid = f.Input; invalid.After = invalid.Before;
        HostCheck.Equal(C.Status.InvalidStorage, C.Start(f.State, invalid)); invalid = f.Input; invalid.Device = (B.State*)8;
        HostCheck.Equal(C.Status.InvalidStorage, C.Start(f.State, invalid)); HostCheck.Equal(0UL, f.Device.State->Submitted);
        f.Mount(); uint before = f.Device.Hardware->Notifies;
        foreach (string name in new[] { "", ".", "..", "a/child", "a.", new string('a', 256), "\ud800", "\udc00" })
            HostCheck.Equal(C.Status.InvalidName, Open(f, name, out _));
        HostCheck.Equal(before, f.Device.Hardware->Notifies);
        HostCheck.Equal(C.Status.Pending, Open(f, "DATA.BIN", out var request)); HostCheck.Equal(C.Status.Ready, f.Run());
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray();
        HostCheck.Equal(C.Status.InvalidStorage, C.CopyOpen(f.State, request, (C.FileToken*)f.State));
        HostCheck.True(f.State->IsReady && new ReadOnlySpan<byte>(f.State, sizeof(C.State)).SequenceEqual(state));
        C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(f.State, request, &file));
        HostCheck.Equal(C.Status.StaleToken, C.CopyOpen(f.State, request, &file)); file = File(f);
        for (int field = 0; field < 3; field++)
        { C.FileToken wrong = file; ((ulong*)&wrong)[field]++; HostCheck.Equal(C.Status.StaleToken, Overwrite(f, wrong, 0, new byte[] { 1 }, out _)); }
        before = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.Unsupported, Overwrite(f, file, 899, new byte[2], out _));
        HostCheck.Equal(C.Status.CapacityExceeded, Overwrite(f, file, 0, new byte[C.WriteBytes + 1], out _));
        C.RequestToken token = default;
        HostCheck.Equal(C.Status.InvalidStorage, C.BeginOverwrite(f.State, file, 0, new ReadOnlySpan<byte>(f.Input.Before, 1), &token));
        HostCheck.Equal(C.Status.InvalidStorage, C.BeginOverwrite(f.State, file, 0, new byte[] { 1 }, (C.RequestToken*)f.Device.Data));
        HostCheck.Equal(before, f.Device.Hardware->Notifies); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
    }

    [HostTest("continuing FAT every metadata and original-data read failure precedes any OUT and retains device cleanup")]
    private static void ReadFailures()
    {
        int reads;
        using (var baseline = new Fixture())
        {
            baseline.Mount(); var file = File(baseline); int first = baseline.Trace.Count;
            Overwrite(baseline, file, 500, new byte[37], out var request); HostCheck.Equal(C.Status.Ready, baseline.Run()); Result(baseline, request);
            reads = baseline.Trace.Skip(first).Count(row => row.Type == 0); HostCheck.True(reads >= 5); C.Stop(baseline.State);
        }
        for (int at = 1; at <= reads; at++)
        {
            using var f = new Fixture(); f.Mount(); var file = File(f); byte[] original = f.Volatile.ToArray(); uint start = f.Device.Hardware->Notifies;
            f.FailAt = start + (uint)at; Overwrite(f, file, 500, new byte[37], out _);
            HostCheck.Equal(C.Status.BlockFailure, f.Run()); HostCheck.True(!f.State->Dirty && f.State->IssuedChanges == 0 && f.Volatile.SequenceEqual(original));
            HostCheck.Equal(B.Status.DeviceIoError, f.State->LastBlockFailure); HostCheck.Equal(C.Status.Busy, C.Stop(f.State));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
    }

    [HostTest("continuing FAT every issued write or final FLUSH failure retains exact dirty before images after actual device reset")]
    private static void IssuedFailures()
    {
        for (int failWrite = 1; failWrite <= 3; failWrite++)
        {
            using var f = new Fixture(); f.Mount(); var file = File(f); f.FailPublication = failWrite;
            byte[] bytes = Enumerable.Repeat((byte)0x72, 700).ToArray(); Overwrite(f, file, 7, bytes, out var request);
            HostCheck.Equal(C.Status.RecoveryRequired, f.Run()); HostCheck.True(f.State->Dirty && f.State->ChangeCount == 2 && f.State->VolumeRevision == 2);
            HostCheck.Equal(B.Status.DeviceIoError, f.State->LastBlockFailure);
            byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 1024).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 1024).ToArray();
            for (int i = 0; i < 512; i++) HostCheck.Equal(Value(i), before[i]);
            HostCheck.Equal(C.Status.RecoveryRequired, C.CopyResult(f.State, request, (C.Result*)f.Output));
            HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State)); HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State));
            HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State));
            HostCheck.True(new ReadOnlySpan<byte>(f.Input.Before, 1024).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 1024).SequenceEqual(after));
        }
    }

    [HostTest("continuing FAT cancellation drains metadata before writes and retains issued mutations while Ready completion wins")]
    private static void Cancellation()
    {
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); Overwrite(f, file, 10, new byte[10], out var request);
            HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.True(f.Device.Hardware->Pending);
            HostCheck.Equal(C.Status.Pending, C.Cancel(f.State, request)); f.Device.Complete(); HostCheck.Equal(C.Status.Ready, f.Run());
            HostCheck.Equal(0U, Result(f, request, C.Status.Cancelled).Bytes); HostCheck.True(!f.State->Dirty && f.State->VolumeRevision == 1);
            C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); Overwrite(f, file, 0, new byte[700], out var request);
            while (f.State->IssuedChanges == 0) HostCheck.Equal(C.Status.Pending, f.Turn());
            HostCheck.Equal(C.Status.RecoveryRequired, C.Cancel(f.State, request)); HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); Overwrite(f, file, 3, new byte[] { 77 }, out var request); HostCheck.Equal(C.Status.Ready, f.Run());
            HostCheck.Equal(C.Status.Ready, C.Cancel(f.State, request)); HostCheck.Equal(1U, Result(f, request).Bytes); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT stale fingerprint mirror loops crosslinks readonly and unsupported directories fail before publication")]
    private static void CorruptMetadata()
    {
        for (int mode = 0; mode < 7; mode++)
        {
            using var f = new Fixture(); f.Mount(); var file = File(f); uint issued = f.Device.Hardware->Notifies;
            if (mode == 0) f.Volatile[Data * 512 + 32 + 12] ^= 8;
            else if (mode == 1) Put32(f.Volatile, (First + Reserved + FatSectors) * 512 + 4 * 4, 0xfffffff);
            else if (mode == 2) f.Fat(6, 4);
            else if (mode == 3) { Put16(f.Volatile, Data * 512 + 26, 4); Put32(f.Volatile, Data * 512 + 28, 900); }
            else if (mode == 4) { f.Volatile[Data * 512 + 64] = (byte)'S'; f.Volatile[Data * 512 + 64 + 11] = 0x10; }
            else if (mode == 5) f.Fat(4, 0xfffffff);
            else { f.Fat(6, 7); f.Fat(7, 0xfffffff); }
            Overwrite(f, file, 0, new byte[] { 1 }, out var request); HostCheck.Equal(C.Status.Ready, f.Run());
            C.Status expected = mode == 0 ? C.Status.StaleToken : mode == 1 ? C.Status.MirrorMismatch : mode == 4 ? C.Status.CorruptDirectory : C.Status.CorruptChain;
            Result(f, request, expected); HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0 && f.Device.Hardware->Notifies > issued); C.Stop(f.State);
        }
        using var readOnly = new Fixture(); readOnly.Volatile[Data * 512 + 32 + 11] |= 1; readOnly.Mount(); var token = File(readOnly);
        Overwrite(readOnly, token, 0, new byte[] { 1 }, out var readonlyRequest); HostCheck.Equal(C.Status.Ready, readOnly.Run()); Result(readOnly, readonlyRequest, C.Status.ReadOnly); C.Stop(readOnly.State);
    }

    [HostTest("continuing FAT root VFAT names cross fragmented directory clusters with bounded input and live decoder")]
    private static void LongRoot()
    {
        using var f = new Fixture(); f.Fat(2, 5); f.Fat(5, 0xfffffff);
        f.Volatile.Slice(Data * 512, 512).Clear(); f.Volatile.Slice((Data + 3) * 512, 512).Clear();
        for (int i = 0; i < 15; i++)
        {
            var raw = f.Volatile.Slice(Data * 512 + i * 32, 32); System.Text.Encoding.ASCII.GetBytes("N" + i.ToString("D7") + "TXT").CopyTo(raw); raw[11] = 0x20;
        }
        Span<byte> entry = stackalloc byte[32]; entry.Clear(); "LONG    BIN"u8.CopyTo(entry); entry[11] = 0x20; Put16(entry, 26, 4); Put32(entry, 28, 900);
        string name = "a-long-root-file-name.bin"; byte[] run = new byte[128];
        HostCheck.Equal(Fat32Mutation.Status.Success, Fat32Mutation.EncodeEntryRun(name, entry, run, out int slots));
        for (int i = 0; i < slots; i++) run.AsSpan(i * 32, 32).CopyTo(f.Volatile.Slice(i == 0 ? Data * 512 + 15 * 32 : (Data + 3) * 512 + (i - 1) * 32, 32));
        f.Mount(); var file = File(f, name); HostCheck.True(file.FirstCluster == 4 && file.Length == 900); C.Stop(f.State);
    }

    [HostTest("continuing FAT cached scan consumes at most one loaded sector and keeps directory completion separate")]
    private static void CachedScanSector()
    {
        using var f = new Fixture(observe: true); ScanRoot(f); f.Mount();
        HostCheck.Equal(C.Status.Pending, Create(f, "NEW.TXT", "NEW     TXT"u8, out var request)); ScanStart(f);
        uint work = *Field<uint>(f, "Work"); ulong submitted = f.Device.State->Submitted;
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(*Field<bool>(f, "IoActive") && f.Device.Hardware->Pending && f.Device.Hardware->Sector == Data &&
            *Field<uint>(f, "DirectorySlot") == 0 && *Field<uint>(f, "Work") == work && f.Device.State->Submitted == submitted + 1);
        f.Device.Complete(); HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(!*Field<bool>(f, "IoActive") && *Field<ulong>(f, "LoadedDirectoryLba") == Data &&
            *Field<uint>(f, "DirectorySlot") == 0 && *Field<uint>(f, "Work") == work);
        var unchanged = MarkDevice(f);
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(*Field<uint>(f, "DirectorySector") == 1 && *Field<uint>(f, "DirectorySlot") == 0 &&
            *Field<uint>(f, "Work") == work + 16 && *Field<ulong>(f, "LoadedDirectoryLba") == Data);
        SameDevice(f, unchanged);
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(f.Device.Hardware->Pending && f.Device.Hardware->Sector == Data + 3 &&
            f.Device.State->Submitted == submitted + 2 && *Field<uint>(f, "Work") == work + 16);
        f.Device.Complete(); HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(*Field<ulong>(f, "LoadedDirectoryLba") == Data + 3 && *Field<uint>(f, "DirectorySlot") == 0 &&
            *Field<uint>(f, "Work") == work + 16);
        unchanged = MarkDevice(f); HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(StateIs(f, "Step", "Plan") && *Field<uint>(f, "Work") == work + 17); SameDevice(f, unchanged);
        HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request);
        HostCheck.True(f.Persistent.Slice((Data + 3) * 512, 11).SequenceEqual("NEW     TXT"u8)); C.Stop(f.State);
    }

    [HostTest("continuing FAT cached scan stops immediately for other and target chains or a terminal empty match")]
    private static void CachedScanTransitions()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new Fixture(observe: true);
            if (mode != 0)
            {
                f.Volatile.Slice(Data * 512 + 32, 32).CopyTo(f.Volatile.Slice(Data * 512, 32)); f.Volatile.Slice(Data * 512 + 32, 32).Clear();
                if (mode == 2) { Put16(f.Volatile, Data * 512 + 26, 0); Put32(f.Volatile, Data * 512 + 28, 0); }
            }
            f.Mount(); C.RequestToken request;
            if (mode == 0) Create(f, "NEW.TXT", "NEW     TXT"u8, out request); else Open(f, "DATA.BIN", out request);
            ScanLoaded(f); uint work = *Field<uint>(f, "Work"); var unchanged = MarkDevice(f);
            HostCheck.Equal(mode == 2 ? C.Status.Ready : C.Status.Pending, C.Pump(f.State));
            HostCheck.True(*Field<uint>(f, "Work") == work + 1 && *Field<uint>(f, "DirectorySlot") == 1);
            SameDevice(f, unchanged);
            if (mode == 2)
            { C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(f.State, request, &file)); HostCheck.True(file.Length == 0 && file.FirstCluster == 0); }
            else
            {
                HostCheck.True(StateIs(f, "Step", mode == 0 ? "OtherChain" : "TargetChain"));
                HostCheck.Equal(C.Status.Ready, C.Cancel(f.State, request));
                if (mode == 0) Result(f, request, C.Status.Cancelled);
                else { C.FileToken file = default; HostCheck.Equal(C.Status.Cancelled, C.CopyOpen(f.State, request, &file)); }
            }
            HostCheck.Equal(0UL, f.Device.State->SubmittedBytes); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT cached scan stops on same-sector empty growth reset before another directory read")]
    private static void CachedScanReset()
    {
        using var f = new Fixture(observe: true); f.Volatile.Slice(Data * 512, 512).Clear();
        "EMPTY   TXT"u8.CopyTo(f.Volatile.Slice(Data * 512, 11)); f.Volatile[Data * 512 + 11] = 0x20;
        f.Mount(); var file = File(f, "EMPTY.TXT"); Grow(f, file, 1, new uint[] { 8 }, out var request); ScanLoaded(f);
        uint work = *Field<uint>(f, "Work"); var unchanged = MarkDevice(f);
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
        HostCheck.True(StateIs(f, "Step", "Scan") && *Field<uint>(f, "Work") == work + 1 &&
            *Field<uint>(f, "DirectorySlot") == 0 && *Field<ulong>(f, "LoadedDirectoryLba") == ulong.MaxValue);
        SameDevice(f, unchanged);
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.True(f.Device.Hardware->Pending && f.Device.Hardware->Sector == Data);
        HostCheck.Equal(C.Status.Pending, C.Cancel(f.State, request)); f.Device.Complete(); HostCheck.Equal(C.Status.Ready, f.Run());
        Result(f, request, C.Status.Cancelled); HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
    }

    [HostTest("continuing FAT cached scan preserves split VFAT state and rejects checksum or deleted interruption before publication")]
    private static void CachedScanVfatBoundary()
    {
        Span<byte> entry = stackalloc byte[32];
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new Fixture(observe: true); ScanRoot(f);
            entry.Clear(); "LONG    TXT"u8.CopyTo(entry); entry[11] = 0x20;
            byte[] run = new byte[64]; HostCheck.Equal(Fat32Mutation.Status.Success, Fat32Mutation.EncodeEntryRun("a-long.txt", entry, run, out int slots));
            HostCheck.Equal(2, slots); run.AsSpan(0, 32).CopyTo(f.Volatile.Slice(Data * 512 + 15 * 32, 32));
            run.AsSpan(32, 32).CopyTo(f.Volatile.Slice((Data + 3) * 512, 32));
            if (mode == 1) f.Volatile[(Data + 3) * 512] = (byte)'M';
            if (mode == 2) f.Volatile[(Data + 3) * 512] = 0xe5;
            f.Mount(); Open(f, "a-long.txt", out var request); ScanLoaded(f); uint work = *Field<uint>(f, "Work"); var unchanged = MarkDevice(f);
            HostCheck.Equal(C.Status.Pending, C.Pump(f.State));
            HostCheck.True(*Field<uint>(f, "DirectorySector") == 1 && *Field<uint>(f, "DirectorySlot") == 0 &&
                Field<LiveVfatState>(f, "Vfat")->Slots == 1 && *Field<uint>(f, "ScanRunStart") == 15 && *Field<uint>(f, "Work") == work + 16);
            SameDevice(f, unchanged); ScanLoaded(f, 1, Data + 3); unchanged = MarkDevice(f);
            HostCheck.Equal(C.Status.Ready, C.Pump(f.State)); HostCheck.Equal(work + 17, *Field<uint>(f, "Work")); SameDevice(f, unchanged);
            C.FileToken file = default; HostCheck.Equal(mode == 0 ? C.Status.Success : C.Status.CorruptDirectory, C.CopyOpen(f.State, request, &file));
            if (mode == 0) HostCheck.True(file.EntryOffset == (Data + 3) * 512UL && *Field<uint>(f, "RunStart") == 15 && *Field<uint>(f, "RunSlots") == 2);
            HostCheck.Equal(0UL, f.Device.State->SubmittedBytes); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT cached scan charges each physical entry and preserves late name alias and corruption rejection")]
    private static void CachedScanBudgetsAndCollisions()
    {
        using (var f = new Fixture(observe: true))
        {
            ScanRoot(f); f.Input.WorkBudget = 6; f.Mount(); Create(f, "NEW.TXT", "NEW     TXT"u8, out var request); ScanLoaded(f);
            HostCheck.Equal(3U, *Field<uint>(f, "Work")); var unchanged = MarkDevice(f);
            HostCheck.Equal(C.Status.Ready, C.Pump(f.State));
            HostCheck.True(*Field<uint>(f, "Work") == 6 && *Field<uint>(f, "DirectorySlot") == 3); SameDevice(f, unchanged);
            Result(f, request, C.Status.BudgetExceeded); HostCheck.Equal(0UL, f.Device.State->SubmittedBytes); C.Stop(f.State);
        }
        for (int mode = 0; mode < 4; mode++)
        {
            using var f = new Fixture(observe: true); ScanRoot(f);
            for (int i = 0; i < 16; i++) { f.Volatile.Slice(Data * 512 + i * 32, 32).Clear(); f.Volatile[Data * 512 + i * 32] = 0xe5; }
            Span<byte> raw = f.Volatile.Slice(Data * 512 + 15 * 32, 32); raw.Clear(); "TARGET  TXT"u8.CopyTo(raw); raw[11] = 0x20;
            if (mode == 2)
            {
                "OTHER   TXT"u8.CopyTo(raw); byte[] run = new byte[64];
                HostCheck.Equal(Fat32Mutation.Status.Success, Fat32Mutation.EncodeEntryRun("TARGET.TXT", raw, run, out int slots)); HostCheck.Equal(2, slots);
                run.CopyTo(f.Volatile.Slice(Data * 512 + 14 * 32, 64));
            }
            if (mode == 3) raw[11] = 0xc0;
            f.Mount(); byte[] original = f.Volatile.Slice(Data * 512, 512).ToArray();
            Create(f, mode == 1 ? "target.txt" : mode == 2 ? "unrelated" : "TARGET.TXT", mode == 2 ? "TARGET  TXT"u8 : "NEW     TXT"u8, out var request);
            ScanLoaded(f); uint work = *Field<uint>(f, "Work"); var unchanged = MarkDevice(f);
            HostCheck.Equal(C.Status.Ready, C.Pump(f.State));
            HostCheck.True(*Field<uint>(f, "Work") == work + 16 && *Field<bool>(f, "CreateHasRun")); SameDevice(f, unchanged);
            Result(f, request, mode == 3 ? C.Status.CorruptDirectory : C.Status.AlreadyExists);
            HostCheck.True(f.Device.State->SubmittedBytes == 0 && f.Volatile.Slice(Data * 512, 512).SequenceEqual(original)); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT cached scan cancellation and original device retirement prevent parsing or late sector copy")]
    private static void CachedScanLifetime()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            using var f = new Fixture(observe: true); ScanRoot(f); f.Mount(); Create(f, "NEW.TXT", "NEW     TXT"u8, out var request); ScanLoaded(f);
            if (mode == 1) { C.Pump(f.State); C.Pump(f.State); HostCheck.True(f.Device.Hardware->Pending); }
            uint work = *Field<uint>(f, "Work"); byte[] sector = new ReadOnlySpan<byte>(f.Input.Sector, 512).ToArray();
            if (mode < 2)
            {
                HostCheck.Equal(mode == 0 ? C.Status.Ready : C.Status.Pending, C.Cancel(f.State, request));
                if (mode == 1) f.Device.Complete();
                HostCheck.Equal(C.Status.Ready, C.Pump(f.State)); Result(f, request, C.Status.Cancelled);
            }
            else
            {
                ulong* epoch = mode == 2 ? &((B.Admission*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "Input")))->Epoch :
                    (ulong*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "PoolEpoch"));
                ulong original = *epoch; (*epoch)++;
                try { HostCheck.Equal(C.Status.BlockFailure, C.Pump(f.State)); }
                finally { *epoch = original; }
                HostCheck.True(!f.State->IsReady && f.State->LastBlockFailure == B.Status.InvalidState);
            }
            HostCheck.True(*Field<uint>(f, "Work") == work && f.Device.State->SubmittedBytes == 0 && !f.State->Dirty &&
                new ReadOnlySpan<byte>(f.Input.Sector, 512).SequenceEqual(sector));
        }
    }

    private static T* Field<T>(Fixture f, string name) where T : unmanaged =>
        (T*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, name));
    private static bool StateIs(Fixture f, string field, string value)
    {
        var info = typeof(C.State).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return *Field<byte>(f, field) == Convert.ToByte(Enum.Parse(info.FieldType, value));
    }
    private static void ScanRoot(Fixture f)
    {
        f.Fat(2, 5); f.Fat(5, 0xfffffff);
        f.Volatile.Slice(Data * 512, 512).Clear(); f.Volatile.Slice((Data + 3) * 512, 512).Clear();
        for (int i = 0; i < 16; i++)
        { Span<byte> raw = f.Volatile.Slice(Data * 512 + i * 32, 32); System.Text.Encoding.ASCII.GetBytes("F" + i.ToString("D7") + "TXT").CopyTo(raw); raw[11] = 0x20; }
    }
    private static void ScanStart(Fixture f)
    {
        for (int i = 0; i < 100; i++)
        { if (StateIs(f, "Step", "Scan") && !*Field<bool>(f, "IoActive")) return; HostCheck.Equal(C.Status.Pending, f.Turn()); }
        throw new InvalidOperationException("scan phase not reached");
    }
    private static void ScanLoaded(Fixture f, uint sector = 0, ulong lba = Data)
    {
        for (int i = 0; i < 100; i++)
        {
            if (StateIs(f, "Step", "Scan") && !*Field<bool>(f, "IoActive") && *Field<uint>(f, "DirectorySector") == sector &&
                *Field<uint>(f, "DirectorySlot") == 0 && *Field<ulong>(f, "LoadedDirectoryLba") == lba) return;
            HostCheck.Equal(C.Status.Pending, f.Turn());
        }
        throw new InvalidOperationException("loaded scan sector not reached");
    }
    private static (uint Calls, byte[] State, byte[] Observation) MarkDevice(Fixture f) =>
        (f.Device.Hardware->Calls, new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).ToArray(),
            new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).ToArray());
    private static void SameDevice(Fixture f, (uint Calls, byte[] State, byte[] Observation) mark) =>
        HostCheck.True(mark.Calls == f.Device.Hardware->Calls && new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).SequenceEqual(mark.State) &&
            new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).SequenceEqual(mark.Observation));

    [HostTest("continuing FAT zero byte operations and real clean barriers retain exact frontier and traversal budgets")]
    private static void BoundsAndBarrier()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); uint start = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.Ready, Overwrite(f, file, 900, [], out var request)); HostCheck.Equal(0U, Result(f, request).Bytes); HostCheck.Equal(start, f.Device.Hardware->Notifies);
        C.RequestToken barrier = default; HostCheck.Equal(C.Status.Pending, C.BeginFlush(f.State, &barrier)); HostCheck.Equal(C.Status.Ready, f.Run());
        HostCheck.Equal(0U, Result(f, barrier).Bytes); HostCheck.Equal(start + 1, f.Device.Hardware->Notifies); HostCheck.Equal(4U, f.Trace[^1].Type); C.Stop(f.State);
        using var small = new Fixture(); var input = small.Input; input.WorkBudget = 2;
        HostCheck.Equal(C.Status.Pending, C.Start(small.State, input)); HostCheck.Equal(C.Status.Ready, small.Run());
        Open(small, "DATA.BIN", out var limited); HostCheck.Equal(C.Status.Ready, small.Run());
        C.FileToken ignored = default; HostCheck.Equal(C.Status.BudgetExceeded, C.CopyOpen(small.State, limited, &ignored));
        HostCheck.Equal(0UL, small.Device.State->SubmittedBytes); C.Stop(small.State);
    }

    [HostTest("continuing FAT repeated metadata planning and sector overwrites allocate no per-turn managed buffers")]
    private static void Allocation()
    {
        using var f = new Fixture(); f.Record = false; f.Mount(); byte[] input = [71];
        var file = File(f); Overwrite(f, file, 0, input, out var request); f.Run(); Result(f, request);
        long start = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 8; i++)
        {
            C.RequestToken open = default; C.FileToken next = default; C.Result result = default;
            good &= C.BeginOpenRoot(f.State, "DATA.BIN", &open) == C.Status.Pending && f.Run() == C.Status.Ready && C.CopyOpen(f.State, open, &next) == C.Status.Success;
            good &= C.BeginOverwrite(f.State, next, (uint)i, input, &request) == C.Status.Pending && f.Run() == C.Status.Ready && C.CopyResult(f.State, request, &result) == C.Status.Success && result.Bytes == 1;
        }
        HostCheck.True(good && start == GC.GetAllocatedBytesForCurrentThread()); C.Stop(f.State);
    }

    [HostTest("continuing FAT empty short root creation publishes one directory sector and a real FLUSH with an independent reopen")]
    private static void CreateShort()
    {
        using var f = new Fixture(); f.Mount(); var old = File(f); byte[] alias = "EMPTY   TXT"u8.ToArray();
        // Bytes after the original end are unowned garbage, not new entries.
        "GHOST   TXT"u8.CopyTo(f.Volatile.Slice(Data * 512 + 96, 11)); f.Volatile[Data * 512 + 107] = 0x20;
        byte[] original = f.Volatile.ToArray(); int first = f.Trace.Count;
        HostCheck.Equal(C.Status.Pending, Create(f, "EMPTY.TXT", alias, out var request)); alias.AsSpan().Clear();
        HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(0U, Result(f, request).Bytes);
        var issued = f.Trace.Skip(first).Where(row => row.Type != 0).ToArray();
        HostCheck.True(issued.SequenceEqual(new (uint, ulong)[] { (1, Data), (4, 0) }) && f.State->VolumeRevision == 2 && !f.State->Dirty);
        HostCheck.True(f.Persistent.Slice(Data * 512 + 64, 11).SequenceEqual("EMPTY   TXT"u8) && f.Persistent[Data * 512 + 75] == 0x20 &&
            f.Persistent.Slice(Data * 512 + 76, 52).IndexOfAnyExcept((byte)0) < 0);
        original.AsSpan(Data * 512 + 64, 64).Clear(); f.Persistent.Slice(Data * 512 + 64, 64).CopyTo(original.AsSpan(Data * 512 + 64));
        HostCheck.True(f.Persistent.SequenceEqual(original));
        var found = Fat32Parser.FindRootDirectoryEntry(new Device(f.Device.Hardware->Persistent), First, "EMPTY.TXT");
        HostCheck.True(found.Succeeded && found.Entry.FileSize == 0 && !Fat32Parser.FindRootDirectoryEntry(new Device(f.Device.Hardware->Persistent), First, "GHOST.TXT").Succeeded);
        var opened = File(f, "EMPTY.TXT"); HostCheck.True(opened.FirstCluster == 0 && opened.Length == 0);
        HostCheck.Equal(C.Status.StaleToken, Overwrite(f, old, 0, new byte[] { 1 }, out _)); C.Stop(f.State);
    }

    [HostTest("continuing FAT empty create rejects collisions malformed aliases full root and caller aliases without publication")]
    private static void CreateAdmission()
    {
        using (var f = new Fixture())
        {
            f.Mount(); uint before = f.Device.Hardware->Notifies;
            foreach (var item in new (string Name, byte[] Alias)[] { ("A.TXT", "bad"u8.ToArray()), ("A.TXT", "A       TXT"u8.ToArray()), ("A.TXT", "A       TX?"u8.ToArray()), ("\ud800", "A       TXT"u8.ToArray()) })
            {
                var status = Create(f, item.Name, item.Alias, out var attempted);
                HostCheck.Equal(item.Name == "A.TXT" && item.Alias.AsSpan().SequenceEqual("A       TXT"u8) ? C.Status.Pending : C.Status.InvalidName, status);
                if (status == C.Status.Pending) { HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, attempted); }
            }
            HostCheck.True(f.Device.Hardware->Notifies > before);
            before = f.Device.Hardware->Notifies;
            C.RequestToken token = default;
            HostCheck.Equal(C.Status.InvalidStorage, C.BeginCreateEmpty(f.State, "B.TXT", new ReadOnlySpan<byte>(f.Input.Before, 11), &token));
            HostCheck.Equal(C.Status.InvalidStorage, C.BeginCreateEmpty(f.State, "B.TXT", "B       TXT"u8, (C.RequestToken*)f.State));
            HostCheck.Equal(before, f.Device.Hardware->Notifies);
            HostCheck.Equal(C.Status.Pending, Create(f, "DATA.BIN", "DATA    BIN"u8, out var duplicate)); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, duplicate, C.Status.AlreadyExists);
            Create(f, "TARGET.TXT", "OTHER   TXT"u8, out var longName); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, longName);
            ulong published = f.Device.State->SubmittedBytes;
            Create(f, "unrelated", "TARGET  TXT"u8, out duplicate); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, duplicate, C.Status.AlreadyExists);
            Create(f, "OTHER.TXT", "THIRD   TXT"u8, out duplicate); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, duplicate, C.Status.AlreadyExists);
            HostCheck.Equal(published, f.Device.State->SubmittedBytes);
            HostCheck.True(!f.State->Dirty); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            f.Volatile.Slice(Data * 512, 512).Clear();
            for (int i = 0; i < 16; i++) { var raw = f.Volatile.Slice(Data * 512 + i * 32, 32); System.Text.Encoding.ASCII.GetBytes("F" + i.ToString("D7") + "TXT").CopyTo(raw); raw[11] = 0x20; }
            f.Mount(); HostCheck.Equal(C.Status.Pending, Create(f, "FULL.TXT", "FULL    TXT"u8, out var request));
            HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request, C.Status.CapacityExceeded); HostCheck.Equal(0UL, f.Device.State->SubmittedBytes); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT empty create reuses scrubbed deleted short holes while partial directory publication retains recovery")]
    private static void CreateFailureAndHole()
    {
        using (var f = new Fixture())
        {
            f.Volatile.Slice(Data * 512, 32).Clear(); f.Volatile[Data * 512] = 0xe5; f.Mount();
            HostCheck.Equal(C.Status.Pending, Create(f, "NEW.TXT", "NEW     TXT"u8, out var request)); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request);
            HostCheck.True(f.Persistent.Slice(Data * 512, 11).SequenceEqual("NEW     TXT"u8)); Independent(f, Enumerable.Range(0, 900).Select(Value).ToArray()); C.Stop(f.State);
        }
        for (int failure = 1; failure <= 2; failure++)
        {
            using var f = new Fixture(); f.Mount(); f.FailPublication = failure;
            HostCheck.Equal(C.Status.Pending, Create(f, "NEW.TXT", "NEW     TXT"u8, out var request)); HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
            HostCheck.True(f.State->Dirty && f.State->ChangeCount == 1 && f.State->VolumeRevision == 2);
            HostCheck.Equal(C.Status.RecoveryRequired, C.CopyResult(f.State, request, (C.Result*)f.Output));
            byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 512).ToArray(); HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State));
            HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State)); HostCheck.True(new ReadOnlySpan<byte>(f.Input.Before, 512).SequenceEqual(before));
        }
    }

    [HostTest("continuing FAT maximum VFAT creation spans three fragmented directory sectors and independently reopens exact names")]
    private static void CreateLong()
    {
        using var f = new Fixture(); PrepareLongRoot(f); string name = new('q', 251); name += ".bin";
        f.Mount(); int first = f.Trace.Count;
        HostCheck.Equal(C.Status.Pending, Create(f, name, "MAXLONG BIN"u8, out var request)); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request);
        HostCheck.True(f.Trace.Skip(first).Where(row => row.Type == 1).Select(row => row.Sector).SequenceEqual(new ulong[] { Data, Data + 3, Data + 5 }) && f.State->ChangeCount == 3 && !f.State->Dirty);
        byte checksum = 0; foreach (byte b in "MAXLONG BIN"u8) checksum = unchecked((byte)((checksum >> 1) + ((checksum & 1) << 7) + b));
        for (int i = 0; i < 20; i++)
        {
            ReadOnlySpan<byte> raw = Logical(f, 15 + i); int ordinal = 20 - i;
            HostCheck.True(raw[0] == (ordinal | (i == 0 ? 0x40 : 0)) && raw[11] == 15 && raw[13] == checksum);
            for (int word = 0; word < 13; word++)
            {
                int position = (ordinal - 1) * 13 + word, at = word < 5 ? 1 + word * 2 : word < 11 ? 14 + (word - 5) * 2 : 28 + (word - 11) * 2;
                ushort actual = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw[at..]);
                HostCheck.Equal(position < name.Length ? (ushort)name[position] : position == name.Length ? (ushort)0 : ushort.MaxValue, actual);
            }
        }
        HostCheck.True(Logical(f, 35)[..11].SequenceEqual("MAXLONG BIN"u8) && Logical(f, 36).IndexOfAnyExcept((byte)0) < 0);
        var device = new Device(f.Device.Hardware->Persistent);
        HostCheck.True(Fat32Parser.FindRootDirectoryEntry(device, First, name).Succeeded && Fat32Parser.FindRootDirectoryEntry(device, First, "MAXLONG.BIN").Succeeded);
        HostCheck.True(File(f, name).Length == 0); uint before = f.Device.Hardware->Notifies;
        Create(f, name.ToUpperInvariant(), "OTHER   BIN"u8, out var duplicate); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, duplicate, C.Status.AlreadyExists);
        Create(f, "a distinct name", "MAXLONG BIN"u8, out duplicate); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, duplicate, C.Status.AlreadyExists);
        HostCheck.True(f.Device.Hardware->Notifies > before && f.Device.State->SubmittedBytes == 1536); C.Stop(f.State);
        static ReadOnlySpan<byte> Logical(Fixture fixture, int slot)
        { int cluster = slot < 16 ? Data : slot < 32 ? Data + 3 : Data + 5; return fixture.Persistent.Slice(cluster * 512 + slot % 16 * 32, 32); }
    }

    [HostTest("continuing FAT every partial maximum VFAT publication or barrier failure retains its exact three-sector journal")]
    private static void CreateLongFailures()
    {
        for (int failure = 1; failure <= 4; failure++)
        {
            using var f = new Fixture(); PrepareLongRoot(f); f.Mount(); f.FailPublication = failure;
            HostCheck.Equal(C.Status.Pending, Create(f, new string('n', 255), "NEWLONG BIN"u8, out _));
            HostCheck.Equal(C.Status.RecoveryRequired, f.Run()); HostCheck.True(f.State->Dirty && f.State->ChangeCount == 3 && f.State->VolumeRevision == 2);
            byte[] retained = new ReadOnlySpan<byte>(f.Input.Before, 1536).ToArray(); B.Stop(f.Device.State);
            HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State)); HostCheck.True(new ReadOnlySpan<byte>(f.Input.Before, 1536).SequenceEqual(retained));
        }
        using var interrupted = new Fixture(); Span<byte> entry = stackalloc byte[32]; entry.Clear(); "LFN     TXT"u8.CopyTo(entry); entry[11] = 0x20;
        byte[] encoded = new byte[96]; HostCheck.Equal(Fat32Mutation.Status.Success, Fat32Mutation.EncodeEntryRun("long text", entry, encoded, out _));
        encoded.AsSpan(0, 32).CopyTo(interrupted.Volatile.Slice(Data * 512 + 64, 32)); interrupted.Volatile[Data * 512 + 96] = 0xe5;
        interrupted.Mount(); Create(interrupted, "new text", "NEW     TXT"u8, out var request); HostCheck.Equal(C.Status.Ready, interrupted.Run());
        Result(interrupted, request, C.Status.CorruptDirectory); HostCheck.Equal(0UL, interrupted.Device.State->SubmittedBytes); C.Stop(interrupted.State);
        using var full = new Fixture(); full.Mount(); Create(full, new string('z', 255), "FULL    TXT"u8, out request);
        HostCheck.Equal(C.Status.Ready, full.Run()); Result(full, request, C.Status.CapacityExceeded); HostCheck.Equal(0UL, full.Device.State->SubmittedBytes); C.Stop(full.State);
    }

    [HostTest("continuing FAT copied VFAT create metadata allocates no managed per-request buffers")]
    private static void CreateAllocation()
    {
        using var f = new Fixture(); f.Record = false; f.Mount();
        string[] names = ["one long name", "two long name", "three long name"];
        byte[][] aliases = ["ONE     TXT"u8.ToArray(), "TWO     TXT"u8.ToArray(), "THREE   TXT"u8.ToArray()];
        Create(f, "warm long name", "WARM    TXT"u8, out var warm); f.Run(); Result(f, warm);
        long start = GC.GetAllocatedBytesForCurrentThread(); bool valid = true;
        for (int i = 0; i < names.Length; i++)
        {
            C.RequestToken request = default; C.Result result = default;
            valid &= C.BeginCreateEmpty(f.State, names[i], aliases[i], &request) == C.Status.Pending && f.Run() == C.Status.Ready && C.CopyResult(f.State, request, &result) == C.Status.Success;
        }
        HostCheck.True(valid && GC.GetAllocatedBytesForCurrentThread() == start); C.Stop(f.State);
    }

    [HostTest("continuing FAT cleanup rejects a substituted retired device generation before scrubbing retained backing")]
    private static void RetiredIdentity()
    {
        using var f = new Fixture(); f.Mount(); f.FailAt = f.Device.Hardware->Notifies + 1;
        Open(f, "DATA.BIN", out _); HostCheck.Equal(C.Status.BlockFailure, f.Run()); B.Stop(f.Device.State);
        int offset = WritableBlockSessionTests.ManagedOffset(f.Device.State, "Input"); B.Admission* input = (B.Admission*)((byte*)f.Device.State + offset);
        ulong epoch = input->Epoch; byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 512).ToArray(); input->Epoch++;
        HostCheck.Equal(C.Status.InvalidState, C.Stop(f.State)); HostCheck.True(!f.State->Retired && new ReadOnlySpan<byte>(f.Input.Before, 512).SequenceEqual(before));
        input->Epoch = epoch; HostCheck.Equal(C.Status.Success, C.Stop(f.State));
    }

    [HostTest("continuing FAT explicit fragmented growth zeroes old tail and new clusters then flushes data both FATs directory and FSInfo in order")]
    private static void GrowFragmented()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); uint[] candidates = [8, 12, 10];
        int first = f.Trace.Count; HostCheck.Equal(C.Status.Pending, Grow(f, file, 2500, candidates, out var request)); candidates.AsSpan().Clear(); int flushes = 0;
        while (true)
        {
            int before = f.Trace.Count; var status = f.Turn();
            if (f.Trace.Count != before && f.Trace[^1].Type == 4)
            {
                flushes++; uint visible = U32(f.Persistent, Data * 512 + 32 + 28), link = U32(f.Persistent, (First + Reserved) * 512 + 6 * 4) & 0x0fffffff;
                HostCheck.True(f.State->Dirty && !f.State->IsReady);
                HostCheck.Equal(flushes < 3 ? 900U : 2500U, visible); HostCheck.Equal(flushes < 2 ? 0x0fffffffU : 8U, link);
                HostCheck.Equal(flushes < 4 ? 123U : uint.MaxValue, U32(f.Persistent, (First + 1) * 512 + 488));
            }
            if (status == C.Status.Ready) break; HostCheck.Equal(C.Status.Pending, status);
        }
        HostCheck.True(flushes == 4 && f.State->ChangeCount == 9 && !f.State->Dirty); HostCheck.Equal(1600U, Result(f, request).Bytes);
        HostCheck.True(f.Persistent.Slice((First + Reserved) * 512, FatSectors * 512).SequenceEqual(f.Persistent.Slice((First + Reserved + FatSectors) * 512, FatSectors * 512)));
        HostCheck.True(U32(f.Persistent, (First + Reserved) * 512 + 6 * 4) == 0xb0000008 && U32(f.Persistent, (First + Reserved) * 512 + 8 * 4) == 0xa000000c);
        HostCheck.Equal(uint.MaxValue, U32(f.Persistent, (First + 7) * 512 + 492));
        byte[] expected = new byte[2500]; for (int i = 0; i < 900; i++) expected[i] = Value(i); Independent(f, expected);
        foreach (int cluster in new[] { 8, 12, 10 }) HostCheck.True(f.Persistent.Slice((Data + cluster - 2) * 512, 512).IndexOfAnyExcept((byte)0) < 0);
        var writes = f.Trace.Skip(first).Where(row => row.Type != 0).ToArray(); HostCheck.True(writes.Length == 13 && f.Device.State->DurableBytes == 9 * 512);
        HostCheck.Equal(C.Status.StaleToken, Grow(f, file, 3000, new uint[] { 14 }, out _)); C.Stop(f.State);
    }

    [HostTest("continuing FAT empty-file growth admits eleven copied clusters for5126bytes and reopens before a positioned overwrite")]
    private static void GrowEmpty()
    {
        using var f = new Fixture(); PrepareGrowth(f); Put16(f.Volatile, Data * 512 + 32 + 26, 0); Put32(f.Volatile, Data * 512 + 32 + 28, 0);
        f.Mount(); var file = File(f); uint[] candidates = Enumerable.Range(20, 11).Select(i => (uint)i).ToArray();
        for (int i = 20; i <= 30; i++) f.Volatile.Slice((Data + i - 2) * 512, 512).Fill(0xd9);
        HostCheck.Equal(C.Status.Pending, Grow(f, file, 5126, candidates, out var request)); HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(5126U, Result(f, request).Bytes);
        HostCheck.Equal(16U, f.State->ChangeCount); Independent(f, new byte[5126]);
        var reopened = File(f); HostCheck.True(reopened.FirstCluster == 20 && reopened.Length == 5126);
        Overwrite(f, reopened, 5121, "proof"u8, out request); HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(5U, Result(f, request).Bytes);
        byte[] expected = new byte[5126]; "proof"u8.CopyTo(expected.AsSpan(5121)); Independent(f, expected); C.Stop(f.State);
    }

    [HostTest("continuing FAT growth within an existing tail has no FAT or FSInfo write and exposes exactly the zeroed new bytes")]
    private static void GrowTail()
    {
        using var f = new Fixture(); f.Volatile.Slice((Data + 4) * 512 + 388, 124).Fill(0xc3); f.Mount(); var file = File(f); int first = f.Trace.Count;
        HostCheck.Equal(C.Status.Pending, Grow(f, file, 950, [], out var request)); HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(50U, Result(f, request).Bytes);
        HostCheck.True(f.Trace.Skip(first).Where(row => row.Type != 0).SequenceEqual(new (uint, ulong)[] { (1, Data + 4), (4, 0), (1, Data), (4, 0) }));
        byte[] expected = new byte[950]; for (int i = 0; i < 900; i++) expected[i] = Value(i); Independent(f, expected);
        HostCheck.True(f.Persistent.Slice((Data + 4) * 512 + 438, 74).IndexOfAnyExcept((byte)0xc3) < 0); C.Stop(f.State);
    }

    [HostTest("continuing FAT growth rejects malformed candidates aliases readonly stale chains mirrors and FSInfo before any OUT")]
    private static void GrowAdmission()
    {
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); uint before = f.Device.Hardware->Notifies;
            foreach (var entry in new (uint Length, uint[] Candidates, C.Status Status)[]
            {
                (1300, [], C.Status.InvalidAllocation), (1300, [1], C.Status.InvalidAllocation), (1300, [Clusters + 2], C.Status.InvalidAllocation),
                (1700, [8, 8], C.Status.InvalidAllocation), (19 * 512, Enumerable.Range(8, 17).Select(i => (uint)i).ToArray(), C.Status.CapacityExceeded),
                (900, [], C.Status.Unsupported), (899, [], C.Status.Unsupported)
            }) HostCheck.Equal(entry.Status, Grow(f, file, entry.Length, entry.Candidates, out _));
            C.RequestToken request = default;
            HostCheck.Equal(C.Status.InvalidStorage, C.BeginGrow(f.State, file, 1300, new ReadOnlySpan<uint>(f.Input.Before, 1), &request));
            HostCheck.Equal(C.Status.InvalidStorage, C.BeginGrow(f.State, file, 1300, new uint[] { 8 }, (C.RequestToken*)f.State));
            HostCheck.Equal(before, f.Device.Hardware->Notifies); C.Stop(f.State);
        }
        for (int mode = 0; mode < 9; mode++)
        {
            using var f = new Fixture(); PrepareGrowth(f);
            if (mode == 3) f.Volatile[Data * 512 + 32 + 11] |= 1;
            if (mode == 5) Put16(f.Volatile, First * 512 + 48, 0);
            if (mode == 6) Put16(f.Volatile, First * 512 + 50, 31);
            f.Mount(); var file = File(f); uint[] candidates = [8];
            if (mode == 0) candidates[0] = 4;
            if (mode == 1) Put32(f.Volatile, (First + Reserved + FatSectors) * 512 + 8 * 4, 2);
            if (mode == 2) f.Volatile[Data * 512 + 32 + 12] ^= 8;
            if (mode == 4) Put32(f.Volatile, (First + 1) * 512 + 484, 0);
            if (mode == 7) { f.Fat(6, 7); f.Fat(7, 0xfffffff); }
            if (mode == 8) Put16(f.Volatile, Data * 512 + 26, 8); // Referenced-but-free is never allocated.
            HostCheck.Equal(C.Status.Pending, Grow(f, file, 1300, candidates, out var request)); HostCheck.Equal(C.Status.Ready, f.Run());
            C.Status expected = mode switch { 0 => C.Status.InvalidAllocation, 1 => C.Status.MirrorMismatch, 2 => C.Status.StaleToken, 3 => C.Status.ReadOnly, 4 or 5 or 6 => C.Status.InvalidVolume, _ => C.Status.CorruptChain };
            Result(f, request, expected); HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0 && f.State->VolumeRevision == 1); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT every growth preflight read failure changes no medium and requires actual faulted device retirement")]
    private static void GrowReadFailures()
    {
        int reads;
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); int first = f.Trace.Count;
            Grow(f, file, 2500, new uint[] { 8, 12, 10 }, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request);
            reads = f.Trace.Skip(first).Count(row => row.Type == 0); HostCheck.True(reads >= 12); C.Stop(f.State);
        }
        for (int at = 1; at <= reads; at++)
        {
            using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); byte[] before = f.Volatile.ToArray();
            f.FailAt = f.Device.Hardware->Notifies + (uint)at; Grow(f, file, 2500, new uint[] { 8, 12, 10 }, out _);
            HostCheck.Equal(C.Status.BlockFailure, f.Run()); HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0 && f.Volatile.SequenceEqual(before));
            HostCheck.Equal(C.Status.Busy, C.Stop(f.State)); B.Stop(f.Device.State); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
    }

    [HostTest("continuing FAT every growth OUT and stage FLUSH failure retains all before after bytes despite durable intermediate frontiers")]
    private static void GrowIssuedFailures()
    {
        for (int failure = 1; failure <= 13; failure++)
        {
            using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); f.FailPublication = failure;
            Grow(f, file, 2500, new uint[] { 8, 12, 10 }, out var request); HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
            HostCheck.True(f.State->Dirty && f.State->VolumeRevision == 2 && f.State->ChangeCount == 9);
            HostCheck.True(f.Device.State->DurableBytes <= f.Device.State->CompletedBytes && f.Device.State->CompletedBytes <= f.Device.State->SubmittedBytes);
            if (failure >= 6) HostCheck.True(f.Device.State->DurableBytes != 0);
            byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 9 * 512).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 9 * 512).ToArray();
            HostCheck.Equal(C.Status.RecoveryRequired, C.CopyResult(f.State, request, (C.Result*)f.Output)); B.Stop(f.Device.State);
            HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State));
            HostCheck.True(new ReadOnlySpan<byte>(f.Input.Before, 9 * 512).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 9 * 512).SequenceEqual(after));
        }
    }

    [HostTest("continuing FAT large-cluster growth exhausts128-change journal before OUT without admitting a whole-volume snapshot")]
    private static void GrowJournalCapacity()
    {
        // The fake retains only a small prefix of a nearly4GiB logical medium. All
        // planned sectors are in that prefix; any out-of-prefix OUT is rejected.
        using var f = new Fixture(128); Put16(f.Volatile, First * 512 + 48, 0xffff);
        Put16(f.Volatile, Data * 512 + 32 + 26, 0); Put32(f.Volatile, Data * 512 + 32 + 28, 0);
        f.Mount(); var file = File(f); HostCheck.True(f.Device.State->SectorCount * 512 > 3UL * 1024 * 1024 * 1024);
        Grow(f, file, 1, new uint[] { 7 }, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request, C.Status.CapacityExceeded);
        HostCheck.True(f.State->ChangeCount == 128 && !f.State->Dirty && f.State->VolumeRevision == 1 && f.Device.State->SubmittedBytes == 0 && f.Trace.All(row => row.Type == 0)); C.Stop(f.State);
    }

    [HostTest("continuing FAT growth cancellation before publication drains reads and after a data barrier retains recovery ownership")]
    private static void GrowCancellation()
    {
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); Grow(f, file, 1300, new uint[] { 8 }, out var request);
            C.Pump(f.State); HostCheck.True(f.Device.Hardware->Pending); HostCheck.Equal(C.Status.Pending, C.Cancel(f.State, request)); f.Device.Complete();
            HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request, C.Status.Cancelled); HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); Grow(f, file, 2500, new uint[] { 8, 12, 10 }, out var request);
            while (!f.Trace.Any(row => row.Type == 4)) HostCheck.Equal(C.Status.Pending, f.Turn());
            HostCheck.Equal(C.Status.Pending, C.Cancel(f.State, request)); HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
            HostCheck.True(f.State->Dirty && f.Device.State->DurableBytes == 4 * 512 && U32(f.Persistent, Data * 512 + 32 + 28) == 900); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT bounded demand reads follow fragmented sectors retain copied prefixes and preserve EOF output tails")]
    private static void ReadRange()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); int first = f.Trace.Count;
        HostCheck.Equal(C.Status.Pending, Read(f, file, 500, 8192, out var request)); HostCheck.Equal(C.Status.Ready, f.Run());
        HostCheck.True(f.State->IsReady && f.State->LastFailure == C.Status.Success && f.Device.State->SubmittedBytes == 0 && f.State->ChangeCount == 0);
        HostCheck.True(f.Trace.Skip(first).Where(row => row.Sector == Data + 2 || row.Sector == Data + 4).Select(row => row.Sector).SequenceEqual(new ulong[] { Data + 2, Data + 4 }));
        // Retained CPU bytes represent the completed reads, not a later device
        // lookup or borrowed caller memory.
        f.Volatile.Slice((Data + 4) * 512, 388).Clear(); byte[] target = Enumerable.Repeat((byte)0xc1, 8192).ToArray(); C.Result result = default;
        HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, target, &result)); HostCheck.Equal(400U, result.Bytes);
        for (int i = 0; i < 400; i++) HostCheck.Equal(Value(i + 500), target[i]); HostCheck.True(target.AsSpan(400).IndexOfAnyExcept((byte)0xc1) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.Input.Input, 400).IndexOfAnyExcept((byte)0) < 0);
        uint before = f.Device.Hardware->Notifies;
        foreach (ulong offset in new[] { 900UL, ulong.MaxValue })
        { HostCheck.Equal(C.Status.Ready, Read(f, file, offset, 1, out request)); HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, [], &result)); HostCheck.Equal(0U, result.Bytes); }
        HostCheck.Equal(C.Status.Ready, Read(f, file, 1, 0, out request)); HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, [], &result));
        HostCheck.Equal(C.Status.CapacityExceeded, Read(f, file, 0, 8193, out _)); HostCheck.Equal(before, f.Device.Hardware->Notifies); C.Stop(f.State);
    }

    [HostTest("continuing FAT held read blocks other work and cancellation revokes Ready or drains partially accumulated IN")]
    private static void ReadCancellation()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f);
        Read(f, file, 500, 400, out var request); HostCheck.Equal(C.Status.Ready, f.Run());
        HostCheck.Equal(C.Status.Busy, Open(f, "DATA.BIN", out _)); HostCheck.Equal(C.Status.Busy, Overwrite(f, file, 0, new byte[] { 1 }, out _));
        HostCheck.Equal(C.Status.Busy, Grow(f, file, 1300, new uint[] { 8 }, out _)); HostCheck.Equal(C.Status.Busy, Create(f, "NEW.TXT", "NEW     TXT"u8, out _));
        C.RequestToken barrier = default; HostCheck.Equal(C.Status.Busy, C.BeginFlush(f.State, &barrier));
        HostCheck.Equal(C.Status.Cancelled, C.Cancel(f.State, request)); HostCheck.True(!f.State->IsReady && f.State->LastFailure == C.Status.Cancelled);
        byte[] target = Enumerable.Repeat((byte)0xc2, 400).ToArray(); C.Result result = default;
        HostCheck.Equal(C.Status.StaleToken, C.CopyRead(f.State, request, target, &result)); HostCheck.True(target.AsSpan().IndexOfAnyExcept((byte)0xc2) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.Input.Input, 400).IndexOfAnyExcept((byte)0) < 0);
        Read(f, file, 500, 400, out var next); HostCheck.True(next.Generation != request.Generation);
        HostCheck.Equal(C.Status.StaleToken, C.Cancel(f.State, request));
        while (!f.Trace.Skip(f.Trace.FindLastIndex(row => row.Sector == Data + 4) + 1).Any(row => row.Sector == Data + 2)) HostCheck.Equal(C.Status.Pending, f.Turn());
        HostCheck.Equal(C.Status.Pending, f.Turn()); // Copy first range fragment.
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.True(f.Device.Hardware->Pending);
        HostCheck.Equal(C.Status.Pending, C.Cancel(f.State, next)); f.Device.Complete(); HostCheck.Equal(C.Status.Ready, f.Run());
        HostCheck.Equal(C.Status.Cancelled, C.CopyRead(f.State, next, [], &result)); HostCheck.Equal(0U, result.Bytes);
        HostCheck.True(!f.State->Dirty && new ReadOnlySpan<byte>(f.Input.Input, 400).IndexOfAnyExcept((byte)0) < 0 && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
    }

    [HostTest("continuing FAT read result and byte aliases preserve held data state DMA ownership and caller outputs")]
    private static void ReadAliases()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); Read(f, file, 5, 37, out var request); f.Run();
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray(), input = new ReadOnlySpan<byte>(f.Input.Input, 37).ToArray();
        byte[] target = Enumerable.Repeat((byte)0xd1, 37).ToArray(); new Span<byte>(f.Output, 128).Fill(0xd2);
        foreach (ulong pointer in new[] { 0UL, 8UL, 0x0000800000000000UL, (ulong)f.State, (ulong)f.Input.Before, (ulong)f.Device.Data, (ulong)f.Device.Pool })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyRead(f.State, request, target, (C.Result*)pointer));
        foreach (ulong pointer in new[] { 8UL, (ulong)f.State, (ulong)f.Input.Input, (ulong)f.Input.Before, (ulong)f.Device.Data })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyRead(f.State, request, new Span<byte>((void*)pointer, 37), (C.Result*)f.Output));
        HostCheck.Equal(C.Status.InvalidStorage, C.CopyRead(f.State, request, new Span<byte>(f.Output, 37), (C.Result*)(f.Output + 8)));
        HostCheck.Equal(C.Status.InvalidStorage, C.CopyRead(f.State, request, target.AsSpan(0, 36), (C.Result*)f.Output));
        HostCheck.True(new ReadOnlySpan<byte>(f.Output, 128).IndexOfAnyExcept((byte)0xd2) < 0 && target.AsSpan().IndexOfAnyExcept((byte)0xd1) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.State, sizeof(C.State)).SequenceEqual(state) && new ReadOnlySpan<byte>(f.Input.Input, 37).SequenceEqual(input));
        C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, target, &result)); HostCheck.Equal(37U, result.Bytes); C.Stop(f.State);
    }

    [HostTest("continuing FAT every range IN failure publishes no partial bytes and exact original device retirement precedes cleanup")]
    private static void ReadFailuresAndReset()
    {
        int reads;
        using (var f = new Fixture()) { f.Mount(); var file = File(f); int before = f.Trace.Count; Read(f, file, 500, 400, out var request); f.Run(); reads = f.Trace.Count - before; C.Result result = default; C.CopyRead(f.State, request, new byte[400], &result); C.Stop(f.State); }
        for (int at = 1; at <= reads; at++)
        {
            using var f = new Fixture(); f.Mount(); var file = File(f); f.FailAt = f.Device.Hardware->Notifies + (uint)at;
            Read(f, file, 500, 400, out var request); HostCheck.Equal(C.Status.BlockFailure, f.Run());
            byte[] target = Enumerable.Repeat((byte)0xe1, 400).ToArray(); C.Result result = default;
            HostCheck.Equal(C.Status.BlockFailure, C.CopyRead(f.State, request, target, &result)); HostCheck.True(result.Bytes == 0 && target.AsSpan().IndexOfAnyExcept((byte)0xe1) < 0 && !f.State->Dirty);
            HostCheck.Equal(C.Status.Busy, C.Stop(f.State)); B.Stop(f.Device.State); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); Read(f, file, 5, 37, out var request); f.Run(); B.Stop(f.Device.State);
            byte[] target = Enumerable.Repeat((byte)0xe2, 37).ToArray(); C.Result result = default;
            HostCheck.Equal(C.Status.BlockFailure, C.CopyRead(f.State, request, target, &result)); HostCheck.True(result.Bytes == 0 && target.AsSpan().IndexOfAnyExcept((byte)0xe2) < 0);
            HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
    }

    [HostTest("continuing FAT read completion does not acquire a new clock dependency and repeated ranges allocate no managed buffers")]
    private static void ReadAllocation()
    {
        using var f = new Fixture(); f.Record = false; f.Mount(); var file = File(f); byte[] target = new byte[37];
        Read(f, file, 5, 37, out var request); f.Run(); C.Result result = default;
        f.Device.Hardware->ClockFailure = true; uint callbacks = f.Device.Hardware->Calls;
        HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, target, &result)); HostCheck.Equal(callbacks, f.Device.Hardware->Calls); f.Device.Hardware->ClockFailure = false;
        long start = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 8; i++)
        { C.RequestToken read = default; good &= C.BeginRead(f.State, file, (uint)i, 37, &read) == C.Status.Pending && f.Run() == C.Status.Ready && C.CopyRead(f.State, read, target, &result) == C.Status.Success && result.Bytes == 37; }
        HostCheck.True(good && GC.GetAllocatedBytesForCurrentThread() == start); C.Stop(f.State);
    }

    [HostTest("continuing FAT dirty recovery journal cannot be borrowed as read scratch or overwritten by a new read")]
    private static void DirtyRead()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); f.FailPublication = 1;
        Overwrite(f, file, 0, new byte[] { 71 }, out _); HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
        byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 512).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 512).ToArray(); uint calls = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.RecoveryRequired, Read(f, file, 0, 1, out _));
        HostCheck.True(f.Device.Hardware->Notifies == calls && new ReadOnlySpan<byte>(f.Input.Before, 512).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 512).SequenceEqual(after));
    }

    private delegate bool AdmissionCheck(C.State* state, ulong epoch);
    private static AdmissionCheck AdmissionPredicate() => typeof(C).GetMethod("CanAdmitStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<AdmissionCheck>();

    [HostTest("continuing FAT live provider admission allows clean pending and held reads without callbacks or allocations")]
    private static void ProviderAdmission()
    {
        using var f = new Fixture(); var admit = AdmissionPredicate();
        HostCheck.True(!admit(f.State, f.Input.Epoch)); f.Mount(); var file = File(f);
        HostCheck.True(admit(f.State, f.Input.Epoch) && !admit(f.State, 0) && !admit(f.State, f.Input.Epoch + 1));
        HostCheck.True(!admit(null, f.Input.Epoch) && !admit((C.State*)8, f.Input.Epoch) && !admit((C.State*)0x0000800000000000UL, f.Input.Epoch));
        HostCheck.Equal(C.Status.Pending, Read(f, file, 0, 8, out var request));
        HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.True(f.Device.State->ReadPending && admit(f.State, f.Input.Epoch));
        f.Device.Complete(); HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.True(admit(f.State, f.Input.Epoch));
        HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.True(admit(f.State, f.Input.Epoch));
        uint calls = f.Device.Hardware->Calls; long allocated = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 32; i++) good &= admit(f.State, f.Input.Epoch);
        HostCheck.True(good && f.Device.Hardware->Calls == calls && allocated == GC.GetAllocatedBytesForCurrentThread());
        C.Result result = default; byte[] bytes = new byte[8]; HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, bytes, &result));
        HostCheck.Equal(C.Status.Success, C.Stop(f.State)); HostCheck.True(!admit(f.State, f.Input.Epoch));
    }

    [HostTest("continuing FAT live provider admission rejects exact stale DMA lease while retained teardown remains possible")]
    private static void ProviderAdmissionLease()
    {
        using var f = new Fixture(); f.Mount(); var admit = AdmissionPredicate();
        var borrow = typeof(C).GetMethod("CanBorrowStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();
        ulong* generation = (ulong*)((byte*)f.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Device.Slot, "Generation")); ulong original = *generation;
        uint calls = f.Device.Hardware->Calls; (*generation)++;
        HostCheck.True(!admit(f.State, f.Input.Epoch) && borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        *generation = original; HostCheck.True(admit(f.State, f.Input.Epoch) && calls == f.Device.Hardware->Calls);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State));
        HostCheck.True(!admit(f.State, f.Input.Epoch) && borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        HostCheck.Equal(C.Status.Success, C.Stop(f.State));
    }

    [HostTest("continuing FAT live provider admission rejects issued dirty and faulted state without weakening structural cleanup")]
    private static void ProviderAdmissionDirtyFault()
    {
        using (var f = new Fixture())
        {
            f.Mount(); var admit = AdmissionPredicate(); var file = File(f);
            Overwrite(f, file, 0, new byte[] { 71 }, out var request);
            while (f.State->IssuedChanges == 0) HostCheck.Equal(C.Status.Pending, f.Turn());
            HostCheck.True(f.State->Dirty && !admit(f.State, f.Input.Epoch));
            HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.True(admit(f.State, f.Input.Epoch)); Result(f, request); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            f.Mount(); var admit = AdmissionPredicate(); var file = File(f); f.FailAt = f.Device.Hardware->Notifies + 1;
            Read(f, file, 0, 1, out _); HostCheck.Equal(C.Status.BlockFailure, f.Run());
            HostCheck.True(!admit(f.State, f.Input.Epoch));
            var borrow = typeof(C).GetMethod("CanBorrowStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();
            HostCheck.True(borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
    }

    private delegate bool BorrowCheck(C.State* state, ulong epoch, ulong address, ulong bytes);
    private static BorrowCheck RetainPredicate() => typeof(C).GetMethod("CanRetainStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();

    [HostTest("continuing FAT retained original ownership permits clean pending and stopped DMA without I O or allocation")]
    private static void RetainedOriginalOwnership()
    {
        using var f = new Fixture(observe: true); f.Mount(); var file = File(f); var retain = RetainPredicate();
        HostCheck.True(retain(f.State, f.Input.Epoch, (ulong)f.Output, 8) && !retain(f.State, f.Input.Epoch + 1, (ulong)f.Output, 8) &&
            !retain(f.State, f.Input.Epoch, (ulong)f.State, 8));
        Read(f, file, 0, 8, out var request); HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.True(f.Device.State->ReadPending);
        uint calls = f.Device.Hardware->Calls; long allocated = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 32; i++) good &= retain(f.State, f.Input.Epoch, (ulong)f.Output, 8);
        HostCheck.True(good && calls == f.Device.Hardware->Calls && allocated == GC.GetAllocatedBytesForCurrentThread());
        f.Device.Complete(); HostCheck.Equal(C.Status.Pending, C.Pump(f.State)); HostCheck.Equal(C.Status.Ready, f.Run());
        HostCheck.True(retain(f.State, f.Input.Epoch, (ulong)f.Output, 8)); C.Result result = default;
        HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, request, new byte[8], &result));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.True(f.Device.State->Retired && f.Device.Pool->AllocatedMask == 0 && retain(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        HostCheck.Equal(C.Status.Success, C.Stop(f.State)); HostCheck.True(!retain(f.State, f.Input.Epoch, (ulong)f.Output, 8));
    }

    [HostTest("continuing FAT retained original ownership preserves dirty fault journal after actual device retirement")]
    private static void RetainedDirtyOwnership()
    {
        using var f = new Fixture(observe: true); f.Mount(); var file = File(f); var retain = RetainPredicate();
        f.FailPublication = 1; Overwrite(f, file, 0, new byte[] { 81 }, out _); HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
        byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 512).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 512).ToArray();
        uint calls = f.Device.Hardware->Calls; HostCheck.True(f.State->Dirty && retain(f.State, f.Input.Epoch, (ulong)f.Output, 8) && calls == f.Device.Hardware->Calls);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); calls = f.Device.Hardware->Calls;
        HostCheck.True(retain(f.State, f.Input.Epoch, (ulong)f.Output, 8) && calls == f.Device.Hardware->Calls &&
            new ReadOnlySpan<byte>(f.Input.Before, 512).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 512).SequenceEqual(after));
        HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State)); HostCheck.True(retain(f.State, f.Input.Epoch, (ulong)f.Output, 8));
    }

    [HostTest("continuing FAT retained original ownership rejects block epoch pool epoch self and real same address restart ABA")]
    private static void RetainedOriginalAba()
    {
        using var f = new Fixture(observe: true); f.Mount(); var retain = RetainPredicate();
        var borrow = typeof(C).GetMethod("CanBorrowStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();
        B.Admission* input = (B.Admission*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "Input"));
        ulong epoch = input->Epoch; uint calls = f.Device.Hardware->Calls;
        try { input->Epoch++; HostCheck.True(borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8) && !retain(f.State, f.Input.Epoch, (ulong)f.Output, 8)); }
        finally { input->Epoch = epoch; }
        foreach (string field in new[] { "PoolEpoch", "Self" })
        {
            ulong* value = (ulong*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, field)); ulong prior = *value;
            try { *value = prior + 8; HostCheck.True(!retain(f.State, f.Input.Epoch, (ulong)f.Output, 8)); }
            finally { *value = prior; }
        }
        HostCheck.True(calls == f.Device.Hardware->Calls && retain(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State));
        new Span<byte>(f.Device.State, sizeof(B.State)).Clear(); f.Device.Negotiate(); var next = f.Device.Input; next.Epoch = epoch + 1;
        HostCheck.Equal(B.Status.Success, B.Start(f.Device.State, next));
        HostCheck.True(borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8) && !retain(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        HostCheck.Equal(C.Status.InvalidState, C.Stop(f.State)); HostCheck.True(f.State->IsMounted);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.True(!retain(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        // The old engine cannot adopt the replacement lifetime. The simulated
        // machine ends after both actual device lifetimes were quiesced.
    }

    [HostTest("continuing FAT retained observer full partial and crossing aliases into every engine extent reject external admission")]
    private static void ProviderObserverRetainedAliases()
    {
        using var f = new Fixture(observe: true); f.Mount(); var admit = AdmissionPredicate();
        var borrow = typeof(C).GetMethod("CanBorrowStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();
        B.Admission* blockInput = (B.Admission*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "Input"));
        var original = blockInput->Observation;
        (ulong Start, ulong Bytes)[] owned =
        [
            ((ulong)f.State, (ulong)sizeof(C.State)), ((ulong)f.Input.Changes, C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)),
            ((ulong)f.Input.Before, C.JournalChanges * 512UL), ((ulong)f.Input.After, C.JournalChanges * 512UL),
            ((ulong)f.Input.DirectoryMap, C.MaximumClusters * 4UL), ((ulong)f.Input.FileMap, C.MaximumClusters * 4UL), ((ulong)f.Input.WalkMap, C.MaximumClusters * 4UL),
            ((ulong)f.Input.Sector, 512UL), ((ulong)f.Input.Fat0, 512UL), ((ulong)f.Input.Fat1, 512UL), ((ulong)f.Input.Input, C.WriteBytes),
            ((ulong)f.Input.Name, C.NameWords * 2UL), ((ulong)f.Input.LongName, C.VfatWords * 2UL), ((ulong)f.Input.EncodedEntries, C.EncodedEntryBytes)
        ];
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray(), before = new ReadOnlySpan<byte>(f.Input.Before, (int)C.JournalChanges * 512).ToArray();
        byte[] receipt = new ReadOnlySpan<byte>(original, sizeof(B.Observation)).ToArray(); uint calls = f.Device.Hardware->Calls;
        try
        {
            foreach (var extent in owned) foreach (ulong address in new[] { extent.Start, extent.Start + 8, extent.Start + extent.Bytes - 8 })
            {
                blockInput->Observation = (B.Observation*)address;
                HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8), $"borrow accepted observer={address:x}");
                HostCheck.True(!admit(f.State, f.Input.Epoch), $"live admission accepted observer={address:x}");
            }
        }
        finally { blockInput->Observation = original; }
        HostCheck.True(calls == f.Device.Hardware->Calls && new ReadOnlySpan<byte>(f.State, sizeof(C.State)).SequenceEqual(state) &&
            new ReadOnlySpan<byte>(f.Input.Before, (int)C.JournalChanges * 512).SequenceEqual(before) && new ReadOnlySpan<byte>(original, sizeof(B.Observation)).SequenceEqual(receipt));
        HostCheck.True(admit(f.State, f.Input.Epoch) && borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        B.Stop(f.Device.State); HostCheck.True(!admit(f.State, f.Input.Epoch) && borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8)); C.Stop(f.State);
    }

    [HostTest("continuing FAT retained backing substitutions reject new borrows while valid fault cleanup remains structural")]
    private static void ProviderBackingRetainedAliases()
    {
        using var f = new Fixture(observe: true); f.Mount(); var admit = AdmissionPredicate();
        var borrow = typeof(C).GetMethod("CanBorrowStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();
        C.Admission* input = (C.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input"));
        byte* before = input->Before; uint calls = f.Device.Hardware->Calls;
        try
        {
            input->Before = input->After;
            HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8) && !admit(f.State, f.Input.Epoch));
            input->Before = (byte*)f.Device.Observation;
            HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8) && !admit(f.State, f.Input.Epoch));
        }
        finally { input->Before = before; }
        HostCheck.True(calls == f.Device.Hardware->Calls && admit(f.State, f.Input.Epoch));
        var file = File(f); f.FailAt = f.Device.Hardware->Notifies + 1; Read(f, file, 0, 1, out _);
        HostCheck.Equal(C.Status.BlockFailure, f.Run()); HostCheck.True(!admit(f.State, f.Input.Epoch) && borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8));
        B.Stop(f.Device.State); HostCheck.True(borrow(f.State, f.Input.Epoch, (ulong)f.Output, 8)); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
    }

    [HostTest("continuing FAT structural provider borrow rejects all owned extents and retired APIs never dereference released backing")]
    private static void BorrowAndRetired()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f);
        var borrow = typeof(C).GetMethod("CanBorrowStorage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<BorrowCheck>();
        HostCheck.True(borrow(f.State, f.Input.Epoch, (ulong)f.Output, 128)); HostCheck.True(!borrow(f.State, f.Input.Epoch + 1, (ulong)f.Output, 128));
        foreach (ulong address in new[] { 8UL, (ulong)f.State, (ulong)f.Input.Changes, (ulong)f.Input.Before, (ulong)f.Input.After, (ulong)f.Input.DirectoryMap, (ulong)f.Input.FileMap,
            (ulong)f.Input.WalkMap, (ulong)f.Input.Sector, (ulong)f.Input.Fat0, (ulong)f.Input.Fat1, (ulong)f.Input.Input, (ulong)f.Input.Name, (ulong)f.Input.LongName, (ulong)f.Input.EncodedEntries,
            (ulong)f.Device.State, (ulong)f.Device.Pool, (ulong)f.Device.Slot, (ulong)f.Device.Data, 0x400000UL, 0x402000UL, 0x403000UL })
            HostCheck.True(!borrow(f.State, f.Input.Epoch, address, 8));
        int offset = WritableBlockSessionTests.ManagedOffset(f.State, "Input"); C.Admission* input = (C.Admission*)((byte*)f.State + offset);
        var before = input->Before; input->Before = (byte*)8;
        HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Output, 128)); HostCheck.Equal(C.Status.InvalidStorage, C.Stop(f.State)); HostCheck.True(f.State->IsMounted);
        input->Before = before;
        int epochOffset = WritableBlockSessionTests.ManagedOffset(f.Device.Pool, "Epoch"); ulong* poolEpoch = (ulong*)((byte*)f.Device.Pool + epochOffset); ulong originalEpoch = *poolEpoch;
        (*poolEpoch)++; HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Output, 128)); *poolEpoch = originalEpoch;
        HostCheck.Equal(C.Status.Success, C.Stop(f.State)); HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Output, 128));
        var device = input->Device; input->Device = (B.State*)8;
        C.RequestToken request = default; C.Result result = default; C.FileToken copied = default;
        HostCheck.Equal(C.Status.InvalidState, C.BeginRead(f.State, file, 0, 1, &request)); HostCheck.Equal(C.Status.InvalidState, C.CopyRead(f.State, request, [], &result));
        HostCheck.Equal(C.Status.InvalidState, C.BeginOpenRoot(f.State, "DATA.BIN", &request)); HostCheck.Equal(C.Status.InvalidState, C.CopyOpen(f.State, request, &copied));
        HostCheck.Equal(C.Status.InvalidState, C.BeginOverwrite(f.State, file, 0, [], &request)); HostCheck.Equal(C.Status.InvalidState, C.BeginGrow(f.State, file, 1300, new uint[] { 8 }, &request));
        HostCheck.Equal(C.Status.InvalidState, C.BeginCreateEmpty(f.State, "A.TXT", "A       TXT"u8, &request)); HostCheck.Equal(C.Status.InvalidState, C.BeginFlush(f.State, &request));
        HostCheck.Equal(C.Status.InvalidState, C.CopyResult(f.State, request, &result)); HostCheck.Equal(C.Status.InvalidState, C.Pump(f.State)); HostCheck.Equal(C.Status.Success, C.Stop(f.State)); input->Device = device;
    }

    [HostTest("continuing FAT shrink flushes shorter directory before both FAT releases and FSInfo then independently reopens the preserved prefix")]
    private static void Shrink()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); int first = f.Trace.Count, flushes = 0;
        HostCheck.Equal(C.Status.Pending, Truncate(f, file, 300, out var request));
        while (true)
        {
            int before = f.Trace.Count; var status = f.Turn();
            if (f.Trace.Count != before && f.Trace[^1].Type == 4)
            {
                flushes++; HostCheck.Equal(300U, U32(f.Persistent, Data * 512 + 32 + 28));
                HostCheck.Equal(flushes < 2 ? 6U : 0x0fffffffU, U32(f.Persistent, (First + Reserved) * 512 + 4 * 4) & 0x0fffffff);
                HostCheck.Equal(flushes < 3 ? 123U : uint.MaxValue, U32(f.Persistent, (First + 1) * 512 + 488)); HostCheck.True(f.State->Dirty);
            }
            if (status == C.Status.Ready) break; HostCheck.Equal(C.Status.Pending, status);
        }
        HostCheck.Equal(600U, Result(f, request).Bytes); HostCheck.True(flushes == 3 && f.State->ChangeCount == 5 && !f.State->Dirty);
        HostCheck.Equal(0xb0000000U, U32(f.Persistent, (First + Reserved) * 512 + 6 * 4)); Independent(f, Enumerable.Range(0, 300).Select(Value).ToArray());
        HostCheck.True(f.Trace.Skip(first).Where(row => row.Type == 1).Select(row => row.Sector).SequenceEqual(new ulong[] { Data, First + Reserved, First + Reserved + FatSectors, First + 1, First + 7 }));
        HostCheck.Equal(C.Status.StaleToken, Read(f, file, 0, 1, out _)); C.Stop(f.State);
    }

    [HostTest("continuing FAT truncate-to-zero frees the whole old chain and later explicit growth safely reuses its clusters")]
    private static void TruncateZero()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f);
        Truncate(f, file, 0, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(900U, Result(f, request).Bytes);
        var empty = File(f); HostCheck.True(empty.Length == 0 && empty.FirstCluster == 0);
        HostCheck.True((U32(f.Persistent, (First + Reserved) * 512 + 4 * 4) & 0x0fffffff) == 0 && (U32(f.Persistent, (First + Reserved) * 512 + 6 * 4) & 0x0fffffff) == 0);
        Grow(f, empty, 700, new uint[] { 4, 6 }, out request); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request);
        Independent(f, new byte[700]); C.Stop(f.State);
    }

    [HostTest("continuing FAT same-cluster shrink preserves hidden bytes and later growth zeros only newly exposed data")]
    private static void ShrinkTail()
    {
        using var f = new Fixture(); f.Mount(); var file = File(f); uint before = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.Ready, Truncate(f, file, 900, out var noChange)); HostCheck.Equal(0U, Result(f, noChange).Bytes); HostCheck.Equal(before, f.Device.Hardware->Notifies);
        HostCheck.Equal(C.Status.Unsupported, Truncate(f, file, 901, out _));
        Truncate(f, file, 800, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(100U, Result(f, request).Bytes);
        HostCheck.Equal(1U, f.State->ChangeCount); HostCheck.Equal(Value(899), f.Persistent[(Data + 4) * 512 + 387]);
        var shrunk = File(f); Grow(f, shrunk, 900, [], out request); f.Run(); Result(f, request);
        byte[] expected = new byte[900]; for (int i = 0; i < 800; i++) expected[i] = Value(i); Independent(f, expected); C.Stop(f.State);
    }

    [HostTest("continuing FAT delete scrubs every owned maximum VFAT entry while preserving later entries and rejecting reused-slot tokens")]
    private static void DeleteLong()
    {
        using var f = new Fixture(); PrepareLongRoot(f); f.Mount(); string name = new('x', 255);
        Create(f, name, "LONGEST BIN"u8, out var request); f.Run(); Result(f, request);
        Create(f, "LATER.TXT", "LATER   TXT"u8, out request); f.Run(); Result(f, request); var file = File(f, name);
        Delete(f, file, out request); HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(0U, Result(f, request).Bytes); HostCheck.Equal(3U, f.State->ChangeCount);
        for (int i = 15; i <= 35; i++)
        { int sector = i < 16 ? Data : i < 32 ? Data + 3 : Data + 5; var raw = f.Persistent.Slice(sector * 512 + i % 16 * 32, 32); HostCheck.True(raw[0] == 0xe5 && raw[1..].IndexOfAnyExcept((byte)0) < 0); }
        var device = new Device(f.Device.Hardware->Persistent);
        HostCheck.True(!Fat32Parser.FindRootDirectoryEntry(device, First, name).Succeeded && Fat32Parser.FindRootDirectoryEntry(device, First, "LATER.TXT").Succeeded);
        Create(f, "REUSE.BIN", "REUSE   BIN"u8, out request); f.Run(); Result(f, request); var next = File(f, "REUSE.BIN");
        HostCheck.True(next.EntryOffset != file.EntryOffset && next.Revision > file.Revision); HostCheck.Equal(C.Status.StaleToken, Delete(f, file, out _)); C.Stop(f.State);
    }

    [HostTest("continuing FAT nonempty short delete releases both FAT copies after tombstone durability and cannot recapture a reused entry")]
    private static void DeleteShort()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f);
        Delete(f, file, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request);
        HostCheck.True(f.Persistent[Data * 512 + 32] == 0xe5 && f.Persistent.Slice(Data * 512 + 33, 31).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True((U32(f.Persistent, (First + Reserved) * 512 + 4 * 4) & 0x0fffffff) == 0 && (U32(f.Persistent, (First + Reserved + FatSectors) * 512 + 6 * 4) & 0x0fffffff) == 0);
        Create(f, "OTHER.BIN", "OTHER   BIN"u8, out request); f.Run(); Result(f, request); var replacement = File(f, "OTHER.BIN");
        HostCheck.True(replacement.EntryOffset == file.EntryOffset && replacement.Fingerprint0 != file.Fingerprint0);
        HostCheck.Equal(C.Status.StaleToken, Read(f, file, 0, 1, out _));
        HostCheck.True(Fat32Parser.FindRootDirectoryEntry(new Device(f.Device.Hardware->Persistent), First, "KEEP.TXT").Succeeded); C.Stop(f.State);
    }

    [HostTest("continuing FAT every shrink or delete OUT and stage FLUSH failure retains the full unrecovered journal")]
    private static void RetireIssuedFailures()
    {
        foreach (bool deletion in new[] { false, true })
        for (int failure = 1; failure <= 8; failure++)
        {
            using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); f.FailPublication = failure;
            if (deletion) Delete(f, file, out _); else Truncate(f, file, 300, out _);
            HostCheck.Equal(C.Status.RecoveryRequired, f.Run()); HostCheck.True(f.State->Dirty && f.State->ChangeCount == 5 && f.State->VolumeRevision == 2);
            byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 5 * 512).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 5 * 512).ToArray();
            B.Stop(f.Device.State); HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.State));
            HostCheck.True(new ReadOnlySpan<byte>(f.Input.Before, 5 * 512).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 5 * 512).SequenceEqual(after));
        }
    }

    [HostTest("continuing FAT every shrink delete preflight read failure leaves media unchanged before any OUT")]
    private static void RetireReadFailures()
    {
        foreach (bool deletion in new[] { false, true })
        {
            int reads;
            using (var f = new Fixture())
            { PrepareGrowth(f); f.Mount(); var file = File(f); int start = f.Trace.Count; C.RequestToken request; if (deletion) Delete(f, file, out request); else Truncate(f, file, 300, out request); f.Run(); Result(f, request); reads = f.Trace.Skip(start).Count(row => row.Type == 0); C.Stop(f.State); }
            for (int failure = 1; failure <= reads; failure++)
            {
                using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); byte[] before = f.Volatile.ToArray(); f.FailAt = f.Device.Hardware->Notifies + (uint)failure;
                if (deletion) Delete(f, file, out _); else Truncate(f, file, 300, out _);
                HostCheck.Equal(C.Status.BlockFailure, f.Run()); HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0 && f.Volatile.SequenceEqual(before));
                B.Stop(f.Device.State); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
            }
        }
    }

    [HostTest("continuing FAT fragmented large delete rejects its journal capacity before tombstones or FAT publication")]
    private static void DeleteCapacity()
    {
        using var f = new Fixture(); PrepareGrowth(f);
        for (uint i = 0; i < 65; i++) f.Fat(4 + i * 128, i == 64 ? 0x0fffffff : 4 + (i + 1) * 128);
        Put32(f.Volatile, Data * 512 + 32 + 28, 65 * 512); f.Mount(); var file = File(f);
        Delete(f, file, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); Result(f, request, C.Status.CapacityExceeded);
        HostCheck.True(f.State->ChangeCount == 128 && !f.State->Dirty && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
    }

    [HostTest("continuing FAT shrink delete readonly controls and cancellation preserve visibility and recovery frontiers")]
    private static void RetireCancellation()
    {
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Volatile[Data * 512 + 32 + 11] |= 1; f.Mount(); var file = File(f);
            Truncate(f, file, 0, out var request); f.Run(); Result(f, request, C.Status.ReadOnly); Delete(f, file, out request); f.Run(); Result(f, request, C.Status.ReadOnly);
            HostCheck.Equal(0UL, f.Device.State->SubmittedBytes); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); Delete(f, file, out var request); C.Pump(f.State); C.Cancel(f.State, request); f.Device.Complete(); f.Run(); Result(f, request, C.Status.Cancelled);
            HostCheck.True(!f.State->Dirty && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); Truncate(f, file, 0, out var request);
            while (!f.Trace.Any(row => row.Type == 4)) f.Turn(); C.Cancel(f.State, request); HostCheck.Equal(C.Status.RecoveryRequired, f.Run());
            HostCheck.True(f.State->Dirty && U32(f.Persistent, Data * 512 + 32 + 28) == 0 && (U32(f.Persistent, (First + Reserved) * 512 + 4 * 4) & 0x0fffffff) == 6);
        }
    }

    [HostTest("continuing FAT copied growth lineage waits for actual final FLUSH and binds request original target and new full fingerprint")]
    private static void MutationGrowthReceipt()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var original = File(f);
        Grow(f, original, 1300, new uint[] { 8 }, out var request); C.MutationResult receipt = default;
        while (f.Trace.Count(row => row.Type == 4) != 4)
        {
            HostCheck.Equal(C.Status.Pending, C.CopyMutationResult(f.State, request, &receipt));
            HostCheck.True(receipt.Status == C.Status.Uninitialized && receipt.NewFile.Owner == 0 && !receipt.Deleted);
            HostCheck.Equal(C.Status.Pending, f.Turn());
        }
        HostCheck.Equal(C.Status.Pending, C.CopyMutationResult(f.State, request, &receipt));
        HostCheck.Equal(C.Status.Ready, f.Run()); HostCheck.Equal(C.Status.Ready, C.Cancel(f.State, request));
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, request, &receipt));
        HostCheck.True(receipt.Status == C.Status.Success && receipt.Bytes == 400 && !receipt.Deleted &&
            SameRequest(receipt.Request, request) && SameFile(original, receipt.OriginalFile) &&
            receipt.NewFile.Revision == original.Revision + 1 && receipt.NewFile.Length == 1300 && receipt.NewFile.FirstCluster == 4 &&
            receipt.NewFile.EntryOffset == original.EntryOffset && f.Device.State->CompletedBytes == f.Device.State->DurableBytes);
        PhysicalFingerprint(receipt.NewFile, f.Persistent.Slice((int)original.EntryOffset, 32));
        HostCheck.Equal(C.Status.StaleToken, Read(f, original, 0, 1, out _));
        Read(f, receipt.NewFile, 0, 37, out var read); f.Run(); byte[] bytes = new byte[37]; C.Result readResult = default;
        HostCheck.Equal(C.Status.Success, C.CopyRead(f.State, read, bytes, &readResult));
        HostCheck.True(bytes.SequenceEqual(Enumerable.Range(0, 37).Select(Value)) && SameFile(receipt.NewFile, File(f)));
        HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.State, request, &receipt)); C.Stop(f.State);
    }

    [HostTest("continuing FAT copied empty growth and shrink lineage preserve exact first cluster length and physical bytes")]
    private static void MutationShapeReceipt()
    {
        foreach (uint length in new uint[] { 300, 0 })
        {
            using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var original = File(f);
            Truncate(f, original, length, out var request); HostCheck.Equal(C.Status.Ready, f.Run()); C.MutationResult receipt = default;
            HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, request, &receipt));
            HostCheck.True(SameFile(original, receipt.OriginalFile) && receipt.Bytes == 900 - length && !receipt.Deleted &&
                receipt.NewFile.Length == length && receipt.NewFile.FirstCluster == (length == 0 ? 0 : 4) && receipt.NewFile.Revision == original.Revision + 1);
            PhysicalFingerprint(receipt.NewFile, f.Persistent.Slice((int)original.EntryOffset, 32));
            HostCheck.True(SameFile(receipt.NewFile, File(f))); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            PrepareGrowth(f); Put16(f.Volatile, Data * 512 + 32 + 26, 0); Put32(f.Volatile, Data * 512 + 32 + 28, 0);
            f.Mount(); var original = File(f); Grow(f, original, 5126, Enumerable.Range(20, 11).Select(x => (uint)x).ToArray(), out var request); f.Run();
            C.MutationResult receipt = default; HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, request, &receipt));
            HostCheck.True(receipt.Bytes == 5126 && receipt.NewFile.Length == 5126 && receipt.NewFile.FirstCluster == 20 &&
                SameFile(receipt.OriginalFile, original) && SameFile(receipt.NewFile, File(f))); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT copied deletion retires admitted lineage before a reused physical directory slot can acquire a new name")]
    private static void MutationDeleteReceipt()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var original = File(f);
        Delete(f, original, out var request); f.Run(); C.MutationResult receipt = default;
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, request, &receipt));
        HostCheck.True(receipt.Deleted && receipt.Bytes == 0 && receipt.NewFile.Owner == 0 && SameFile(receipt.OriginalFile, original) && SameRequest(receipt.Request, request));
        HostCheck.True(f.Persistent[(int)original.EntryOffset] == 0xe5 && f.Persistent.Slice((int)original.EntryOffset + 1, 31).IndexOfAnyExcept((byte)0) < 0);
        Create(f, "REUSE.BIN", "REUSE   BIN"u8, out var create); f.Run(); Result(f, create);
        var replacement = File(f, "REUSE.BIN"); HostCheck.True(replacement.EntryOffset == original.EntryOffset && replacement.Revision > original.Revision &&
            replacement.Fingerprint0 != original.Fingerprint0 && SameFile(receipt.OriginalFile, original));
        HostCheck.Equal(C.Status.StaleToken, Delete(f, original, out _));
        C.MutationResult stale = default; HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.State, request, &stale)); C.Stop(f.State);
    }

    [HostTest("continuing FAT equal truncate copied lineage has no new revision I/O callbacks or allocation and retired outputs reject safely")]
    private static void MutationNoChangeReceipt()
    {
        using var f = new Fixture(); f.Record = false; f.Mount(); var original = File(f); C.MutationResult receipt = default;
        uint callbacks = f.Device.Hardware->Calls; ulong revision = f.State->VolumeRevision; bool good = true;
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 16; i++)
        {
            C.RequestToken request = default;
            good &= C.BeginTruncate(f.State, original, original.Length, &request) == C.Status.Ready &&
                C.CopyMutationResult(f.State, request, &receipt) == C.Status.Success && SameRequest(receipt.Request, request) &&
                SameFile(receipt.OriginalFile, original) && SameFile(receipt.NewFile, original) && receipt.Bytes == 0 && !receipt.Deleted;
        }
        HostCheck.True(good && f.Device.Hardware->Calls == callbacks && f.State->VolumeRevision == revision && GC.GetAllocatedBytesForCurrentThread() == allocation);
        HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        int offset = WritableBlockSessionTests.ManagedOffset(f.State, "Input"); C.Admission* input = (C.Admission*)((byte*)f.State + offset);
        var device = input->Device; input->Device = (B.State*)8;
        HostCheck.Equal(C.Status.InvalidState, C.CopyMutationResult(f.State, default, &receipt)); input->Device = device;
    }

    [HostTest("continuing FAT mutation receipt aliases reject before state DMA lease or journal publication")]
    private static void MutationReceiptAliases()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); Truncate(f, file, 300, out var request); f.Run();
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray(), device = new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).ToArray();
        byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 65536).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 65536).ToArray(), dma = new ReadOnlySpan<byte>(f.Device.Data, 512).ToArray();
        foreach (ulong pointer in new[] { 0UL, 8UL, 0x0000800000000000UL, (ulong)f.Output + 1, (ulong)f.State, (ulong)f.Input.Before, (ulong)f.Input.After,
            (ulong)f.Input.Changes, (ulong)f.Input.DirectoryMap, (ulong)f.Input.Input, (ulong)f.Device.State, (ulong)f.Device.Data, (ulong)f.Device.Pool, 0x400000UL, 0x402000UL, 0x403000UL })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyMutationResult(f.State, request, (C.MutationResult*)pointer));
        HostCheck.True(new ReadOnlySpan<byte>(f.State, sizeof(C.State)).SequenceEqual(state) && new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).SequenceEqual(device) &&
            new ReadOnlySpan<byte>(f.Input.Before, 65536).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 65536).SequenceEqual(after) && new ReadOnlySpan<byte>(f.Device.Data, 512).SequenceEqual(dma));
        C.MutationResult receipt = default; HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, request, &receipt)); C.Stop(f.State);
    }

    [HostTest("continuing FAT mutation receipt rejects stale request and wrong kinds without changing legacy result consumption")]
    private static void MutationReceiptKinds()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); Open(f, "DATA.BIN", out var open); f.Run(); C.MutationResult receipt = default;
        HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.State, open, &receipt)); C.FileToken file = default;
        HostCheck.Equal(C.Status.Success, C.CopyOpen(f.State, open, &file));
        Grow(f, file, 1300, new uint[] { 8 }, out var request); f.Run(); var wrong = request; ((ulong*)&wrong)[2]++;
        HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.State, wrong, &receipt));
        HostCheck.True(receipt.NewFile.Owner == 0 && f.State->IsReady); HostCheck.Equal(400U, Result(f, request).Bytes);
        HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.State, request, &receipt));
        C.RequestToken barrier = default; C.BeginFlush(f.State, &barrier); f.Run();
        HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.State, barrier, &receipt)); Result(f, barrier); C.Stop(f.State);
    }

    [HostTest("continuing FAT cancelled readonly device and dirty failures cannot publish successful new mutation lineage")]
    private static void MutationReceiptFailures()
    {
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f); Delete(f, file, out var request); C.Pump(f.State); C.Cancel(f.State, request); f.Device.Complete(); f.Run();
            C.MutationResult receipt = default; HostCheck.Equal(C.Status.Cancelled, C.CopyMutationResult(f.State, request, &receipt));
            HostCheck.True(receipt.Status == C.Status.Cancelled && receipt.Bytes == 0 && receipt.NewFile.Owner == 0 && !receipt.Deleted && SameFile(receipt.OriginalFile, file)); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Volatile[Data * 512 + 32 + 11] |= 1; f.Mount(); var file = File(f); Truncate(f, file, 0, out var request); f.Run();
            C.MutationResult receipt = default; HostCheck.Equal(C.Status.ReadOnly, C.CopyMutationResult(f.State, request, &receipt));
            HostCheck.True(receipt.Status == C.Status.ReadOnly && receipt.NewFile.Owner == 0 && !receipt.Deleted && SameFile(receipt.OriginalFile, file)); C.Stop(f.State);
        }
        foreach (bool dirty in new[] { false, true })
        {
            using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f);
            if (dirty) f.FailPublication = 8; else f.FailAt = f.Device.Hardware->Notifies + 1;
            Truncate(f, file, 300, out var request); C.Status expected = dirty ? C.Status.RecoveryRequired : C.Status.BlockFailure;
            HostCheck.Equal(expected, f.Run()); C.MutationResult receipt = default; HostCheck.Equal(expected, C.CopyMutationResult(f.State, request, &receipt));
            HostCheck.True(receipt.Status == C.Status.Uninitialized && receipt.NewFile.Owner == 0 && !receipt.Deleted && f.State->Dirty == dirty);
            B.Stop(f.Device.State); HostCheck.Equal(dirty ? C.Status.RecoveryRequired : C.Status.Success, C.Stop(f.State));
        }
    }

    [HostTest("continuing FAT mutation receipt authenticates exact staged directory fingerprint without consuming malformed Ready")]
    private static void MutationReceiptStagedBytes()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Mount(); var file = File(f); Truncate(f, file, 300, out var request); f.Run();
        uint index = 0; while (f.Input.Changes[index].Sector != file.EntryOffset / 512) index++;
        byte* entry = f.Input.After + index * 512 + file.EntryOffset % 512; byte original = entry[0]; entry[0] ^= 1;
        C.MutationResult receipt = default; HostCheck.Equal(C.Status.InvalidState, C.CopyMutationResult(f.State, request, &receipt));
        HostCheck.True(f.State->IsReady && receipt.NewFile.Owner == 0); entry[0] = original;
        byte length = entry[28]; entry[28] ^= 1; HostCheck.Equal(C.Status.InvalidState, C.CopyMutationResult(f.State, request, &receipt));
        entry[28] = length; HostCheck.Equal(C.Status.Success, C.CopyMutationResult(f.State, request, &receipt)); C.Stop(f.State);
    }

    [HostTest("continuing FAT discovery reads actual mirrored FATs admits only low28 free clusters and leaves journals frontiers and FSInfo untouched")]
    private static void FreeDiscovery()
    {
        using var f = new Fixture(); PrepareGrowth(f); f.Fat(5, 0xa0000000); f.Fat(7, 0x0ffffff7); f.Fat(8, 0x0ffffff2); f.Fat(9, 0x0fffffff); f.Fat(10, 0xb0000000); f.Fat(11, 1);
        f.Mount(); byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 65536).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 65536).ToArray();
        byte[] maps = new ReadOnlySpan<byte>(f.Input.DirectoryMap, (int)C.MaximumClusters * 4).ToArray(), info = f.Volatile.Slice((First + 1) * 512, 512).ToArray();
        int start = f.Trace.Count; var cursor = Cursor(f); var request = Search(f, cursor, 4, 12, 2); HostCheck.Equal(C.Status.Ready, f.Run());
        uint[] output = Enumerable.Repeat(0xdeadbeefU, 16).ToArray(); var result = Free(f, request, output);
        HostCheck.True(result.Status == C.Status.Success && result.Count == 4 && result.Scanned == 12 && result.SectorReads == 2 && SameRequest(result.Request, request) &&
            output.AsSpan(0, 4).SequenceEqual(new uint[] { 5, 10, 12, 13 }) && output.AsSpan(4).IndexOfAnyExcept(0xdeadbeefU) < 0 && result.NextCursor.NextCluster == 14 &&
            result.NextCursor.Owner == (ulong)f.State && result.NextCursor.Epoch == f.Input.Epoch && result.NextCursor.Revision == 1 && result.NextCursor.Generation == request.Generation);
        HostCheck.True(f.Trace.Skip(start).Select(row => row.Type).SequenceEqual(new uint[] { 0, 0 }) && f.State->ChangeCount == 0 && f.State->VolumeRevision == 1 &&
            f.Device.State->SubmittedBytes == 0 && f.Device.State->CompletedBytes == 0 && f.Device.State->DurableBytes == 0 && !f.State->Dirty);
        HostCheck.True(new ReadOnlySpan<byte>(f.Input.Before, 65536).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 65536).SequenceEqual(after) &&
            new ReadOnlySpan<byte>(f.Input.DirectoryMap, (int)C.MaximumClusters * 4).SequenceEqual(maps) && f.Volatile.Slice((First + 1) * 512, 512).SequenceEqual(info)); C.Stop(f.State);
    }

    [HostTest("continuing FAT discovery distinguishes cluster sector and zero budgets and resumes only from a copied generation cursor")]
    private static void FreeBudgets()
    {
        using var f = new Fixture(); f.Mount(); uint[] output = new uint[16];
        foreach (var sample in new (uint Clusters, uint Reads, uint Count, uint Scanned, uint ActualReads, uint Next)[]
        { (0, 2, 0, 0, 0, 2), (8, 0, 0, 0, 0, 2), (8, 1, 0, 0, 1, 2), (1, 2, 0, 1, 2, 3), (4, 2, 1, 4, 2, 6), (8, 2, 4, 8, 2, 10) })
        {
            var request = Search(f, Cursor(f), 16, sample.Clusters, sample.Reads); f.Run(); var result = Free(f, request, output, C.Status.SearchBudgetExceeded);
            HostCheck.True(result.Status == C.Status.SearchBudgetExceeded && result.Count == sample.Count && result.Scanned == sample.Scanned &&
                result.SectorReads == sample.ActualReads && result.NextCursor.NextCluster == sample.Next);
        }
        var original = Cursor(f); var first = Search(f, original, 4, 4, 2); f.Run(); var partial = Free(f, first, output, C.Status.SearchBudgetExceeded);
        HostCheck.True(partial.Count == 1 && output[0] == 5 && partial.NextCursor.NextCluster == 6);
        C.RequestToken invalid = default; HostCheck.Equal(C.Status.StaleToken, C.BeginFindFree(f.State, original, 3, 5, 2, &invalid));
        var continuation = Search(f, partial.NextCursor, 3, 5, 2); f.Run(); var complete = Free(f, continuation, output);
        HostCheck.True(complete.Count == 3 && complete.Scanned == 4 && complete.SectorReads == 2 && output.AsSpan(0, 3).SequenceEqual(new uint[] { 7, 8, 9 })); C.Stop(f.State);
    }

    [HostTest("continuing FAT discovery crosses FAT sector boundaries with exact read budgets and no cached half-sector candidate publication")]
    private static void FreeSectorBoundary()
    {
        using var f = new Fixture(); f.Fat(126, 0xfffffff); f.Fat(129, 0xfffffff); f.Mount(); uint[] output = new uint[16];
        var request = Search(f, Cursor(f, 126), 3, 5, 2); f.Run(); var partial = Free(f, request, output, C.Status.SearchBudgetExceeded);
        HostCheck.True(partial.Count == 1 && output[0] == 127 && partial.Scanned == 2 && partial.SectorReads == 2 && partial.NextCursor.NextCluster == 128);
        request = Search(f, partial.NextCursor, 2, 3, 2); f.Run(); var result = Free(f, request, output);
        HostCheck.True(result.Count == 2 && result.Scanned == 3 && result.SectorReads == 2 && output.AsSpan(0, 2).SequenceEqual(new uint[] { 128, 130 }));
        request = Search(f, Cursor(f, 126), 3, 5, 4); f.Run(); result = Free(f, request, output);
        HostCheck.True(result.Count == 3 && result.SectorReads == 4 && result.NextCursor.NextCluster == 131); C.Stop(f.State);
    }

    [HostTest("continuing FAT discovery physical EndOfScan returns only remaining-range candidates and never wraps or invents ENOSPC")]
    private static void FreeEndOfScan()
    {
        using var f = new Fixture(); f.Mount(); uint[] output = new uint[16];
        var request = Search(f, Cursor(f, Clusters), 3, 10, 2); f.Run(); var result = Free(f, request, output, C.Status.EndOfScan);
        HostCheck.True(result.Count == 2 && result.Scanned == 2 && result.SectorReads == 2 && result.NextCursor.NextCluster == Clusters + 2 &&
            output.AsSpan(0, 2).SequenceEqual(new uint[] { Clusters, Clusters + 1 }));
        request = Search(f, result.NextCursor, 1, 0, 0); f.Run(); result = Free(f, request, [], C.Status.EndOfScan);
        HostCheck.True(result.Count == 0 && result.Scanned == 0 && result.SectorReads == 0 && result.NextCursor.NextCluster == Clusters + 2);
        f.Fat(Clusters, 0xfffffff); f.Fat(Clusters + 1, 0xfffffff);
        request = Search(f, Cursor(f, Clusters), 1, 2, 2); f.Run(); result = Free(f, request, [], C.Status.EndOfScan);
        HostCheck.True(result.Count == 0 && result.Scanned == 2 && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
    }

    [HostTest("continuing FAT discovery cursor rejects wrong epoch revision owner generation range and unrelated request reuse before I/O")]
    private static void FreeCursorGuards()
    {
        using var f = new Fixture(); f.Mount(); var original = Cursor(f); var file = File(f); C.RequestToken request = default;
        uint before = f.Device.Hardware->Notifies;
        HostCheck.Equal(C.Status.StaleToken, C.BeginFindFree(f.State, original, 1, 1, 2, &request));
        var valid = Cursor(f);
        for (int field = 0; field < 4; field++)
        { var wrong = valid; ((ulong*)&wrong)[field]++; HostCheck.Equal(C.Status.StaleToken, C.BeginFindFree(f.State, wrong, 1, 1, 2, &request)); }
        foreach (uint next in new uint[] { 0, 1, Clusters + 3, uint.MaxValue })
        { var wrong = valid; ((uint*)&wrong)[8] = next; HostCheck.Equal(C.Status.StaleToken, C.BeginFindFree(f.State, wrong, 1, 1, 2, &request)); }
        HostCheck.Equal(C.Status.InvalidAllocation, C.BeginFindFree(f.State, valid, 0, 1, 2, &request));
        HostCheck.Equal(C.Status.CapacityExceeded, C.BeginFindFree(f.State, valid, 17, 1, 2, &request));
        HostCheck.Equal(C.Status.CapacityExceeded, C.BeginFindFree(f.State, valid, 1, f.Input.WorkBudget + 1, 2, &request));
        C.FreeCursor invalid = default; HostCheck.Equal(C.Status.InvalidAllocation, C.CreateFreeCursor(f.State, 1, &invalid));
        HostCheck.Equal(before, f.Device.Hardware->Notifies);
        Grow(f, file, 950, [], out request); f.Run(); Result(f, request);
        var staleRevision = valid; ((ulong*)&staleRevision)[3] = request.Generation;
        HostCheck.Equal(C.Status.StaleToken, C.BeginFindFree(f.State, staleRevision, 1, 1, 2, &request)); C.Stop(f.State);
    }

    [HostTest("continuing FAT geometry-only required-candidate helper handles empty tails different cluster sizes aliases stale and oversized requests without I/O")]
    private static void GrowthCandidateGeometry()
    {
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); uint count = 99, before = f.Device.Hardware->Calls;
            foreach (var row in new (uint Length, uint Count)[] { (900, 0), (950, 0), (1024, 0), (1025, 1), (5126, 9) })
            { HostCheck.Equal(C.Status.Success, C.GrowthCandidatesRequired(f.State, file, row.Length, &count)); HostCheck.Equal(row.Count, count); }
            HostCheck.Equal(C.Status.Unsupported, C.GrowthCandidatesRequired(f.State, file, 899, &count)); HostCheck.Equal(0U, count);
            HostCheck.Equal(C.Status.CapacityExceeded, C.GrowthCandidatesRequired(f.State, file, 19 * 512, &count));
            HostCheck.Equal(C.Status.CapacityExceeded, C.GrowthCandidatesRequired(f.State, file, uint.MaxValue, &count));
            foreach (ulong pointer in new[] { 0UL, 8UL, (ulong)f.State, (ulong)f.Input.After, (ulong)f.Device.Data, 0x400000UL, (ulong)f.Output + 1 })
                HostCheck.Equal(C.Status.InvalidStorage, C.GrowthCandidatesRequired(f.State, file, 1300, (uint*)pointer));
            HostCheck.Equal(before, f.Device.Hardware->Calls);
            var request = Search(f, Cursor(f), 1, 10, 2); HostCheck.Equal(C.Status.Busy, C.GrowthCandidatesRequired(f.State, file, 1300, &count));
            C.Cancel(f.State, request); f.Run(); Free(f, request, [], C.Status.Cancelled);
            Grow(f, file, 950, [], out request); f.Run(); Result(f, request);
            HostCheck.Equal(C.Status.StaleToken, C.GrowthCandidatesRequired(f.State, file, 1300, &count)); HostCheck.Equal(0U, count); C.Stop(f.State);
            HostCheck.Equal(C.Status.InvalidState, C.GrowthCandidatesRequired(f.State, file, 1300, &count));
        }
        using (var f = new Fixture(2))
        {
            f.Fat(4, 0xfffffff); f.Mount(); var file = File(f); uint count = 0;
            HostCheck.Equal(C.Status.Success, C.GrowthCandidatesRequired(f.State, file, 5126, &count)); HostCheck.Equal(5U, count); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            Put16(f.Volatile, Data * 512 + 32 + 26, 0); Put32(f.Volatile, Data * 512 + 32 + 28, 0); f.Mount(); var file = File(f); uint count = 0;
            HostCheck.Equal(C.Status.Success, C.GrowthCandidatesRequired(f.State, file, 5126, &count)); HostCheck.Equal(11U, count); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT discovery output capacity alias checks preserve held Ready metadata buffers and DMA ownership")]
    private static void FreeAliases()
    {
        using var f = new Fixture(); f.Mount(); var request = Search(f, Cursor(f, 8), 4, 4, 2); f.Run();
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(C.State)).ToArray(), device = new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).ToArray();
        uint[] candidates = Enumerable.Repeat(0xcacacacaU, 16).ToArray(); C.FreeResult result = default;
        new Span<byte>(&result, sizeof(C.FreeResult)).Fill(0xcb);
        foreach (ulong pointer in new[] { 0UL, 8UL, 0x0000800000000000UL, (ulong)f.Output + 1, (ulong)f.State, (ulong)f.Input.Before, (ulong)f.Input.After,
            (ulong)f.Input.Fat0, (ulong)f.Device.State, (ulong)f.Device.Pool, (ulong)f.Device.Data, 0x400000UL, 0x402000UL, 0x403000UL })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyFree(f.State, request, candidates, (C.FreeResult*)pointer));
        foreach (ulong pointer in new[] { 8UL, (ulong)f.State, (ulong)f.Input.After, (ulong)f.Input.Fat0, (ulong)f.Device.Data })
            HostCheck.Equal(C.Status.InvalidStorage, C.CopyFree(f.State, request, new Span<uint>((void*)pointer, 16), &result));
        HostCheck.Equal(C.Status.InvalidStorage, C.CopyFree(f.State, request, candidates.AsSpan(0, 3), &result));
        new Span<byte>(f.Output, 128).Fill(0xcc);
        HostCheck.Equal(C.Status.InvalidStorage, C.CopyFree(f.State, request, new Span<uint>(f.Output, 16), (C.FreeResult*)(f.Output + 8)));
        HostCheck.True(new ReadOnlySpan<byte>(&result, sizeof(C.FreeResult)).IndexOfAnyExcept((byte)0xcb) < 0 && candidates.AsSpan().IndexOfAnyExcept(0xcacacacaU) < 0 &&
            new ReadOnlySpan<byte>(f.Output, 128).IndexOfAnyExcept((byte)0xcc) < 0 && new ReadOnlySpan<byte>(f.State, sizeof(C.State)).SequenceEqual(state) &&
            new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).SequenceEqual(device));
        Free(f, request, candidates); C.Stop(f.State);
    }

    [HostTest("continuing FAT discovery cancellation and mirror failure revoke partial or held candidates without a continuation cursor")]
    private static void FreeCancellation()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(); f.Mount(); var request = Search(f, Cursor(f), 4, 20, 2); uint[] output = Enumerable.Repeat(99U, 16).ToArray();
            if (held) f.Run(); else { C.Pump(f.State); C.Cancel(f.State, request); f.Device.Complete(); f.Run(); }
            if (held) HostCheck.Equal(C.Status.Cancelled, C.Cancel(f.State, request));
            var result = Free(f, request, output, held ? C.Status.StaleToken : C.Status.Cancelled);
            HostCheck.True(result.NextCursor.Owner == 0 && result.Count == 0 && output.AsSpan().IndexOfAnyExcept(99U) < 0 && !f.State->RequestActive && !f.State->Dirty); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            f.Mount(); Put32(f.Volatile, (First + Reserved + FatSectors) * 512 + 128 * 4, 1);
            var request = Search(f, Cursor(f, 127), 2, 2, 4); f.Run(); uint[] output = Enumerable.Repeat(99U, 16).ToArray();
            var result = Free(f, request, output, C.Status.MirrorMismatch);
            HostCheck.True(result.Count == 0 && result.NextCursor.Owner == 0 && output.AsSpan().IndexOfAnyExcept(99U) < 0 && !f.State->Dirty); C.Stop(f.State);
        }
    }

    [HostTest("continuing FAT every discovery IN failure and a reset after held Ready publish no candidates and require actual retirement")]
    private static void FreeReadFailures()
    {
        for (uint failure = 1; failure <= 4; failure++)
        {
            using var f = new Fixture(); f.Mount(); f.FailAt = f.Device.Hardware->Notifies + failure;
            var request = Search(f, Cursor(f, 127), 3, 3, 4); HostCheck.Equal(C.Status.BlockFailure, f.Run());
            uint[] output = Enumerable.Repeat(99U, 16).ToArray(); var result = Free(f, request, output, C.Status.BlockFailure);
            HostCheck.True(result.Count == 0 && result.NextCursor.Owner == 0 && output.AsSpan().IndexOfAnyExcept(99U) < 0 && f.Device.State->SubmittedBytes == 0);
            HostCheck.Equal(C.Status.Busy, C.Stop(f.State)); B.Stop(f.Device.State); HostCheck.Equal(C.Status.Success, C.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            f.Mount(); var request = Search(f, Cursor(f, 8), 2, 2, 2); f.Run(); B.Stop(f.Device.State);
            uint[] output = Enumerable.Repeat(99U, 16).ToArray(); var result = Free(f, request, output, C.Status.BlockFailure);
            HostCheck.True(result.Count == 0 && result.NextCursor.Owner == 0 && output.AsSpan().IndexOfAnyExcept(99U) < 0); C.Stop(f.State);
            C.FreeCursor cursor = default; HostCheck.Equal(C.Status.InvalidState, C.CreateFreeCursor(f.State, 2, &cursor));
            HostCheck.Equal(C.Status.InvalidState, C.BeginFindFree(f.State, cursor, 1, 1, 2, &request));
        }
    }

    [HostTest("continuing FAT discovered clusters are not reservations and growth revalidates changed mirrored allocation before any OUT")]
    private static void FreeGrowthRevalidation()
    {
        using (var f = new Fixture())
        {
            PrepareGrowth(f); f.Mount(); var file = File(f);
            var request = Search(f, Cursor(f, 8), 1, 1, 2); f.Run(); uint[] candidates = new uint[16]; var result = Free(f, request, candidates);
            HostCheck.True(result.Count == 1 && candidates[0] == 8); f.Fat(8, 0xfffffff);
            Grow(f, file, 1300, candidates.AsSpan(0, 1), out request); f.Run(); Result(f, request, C.Status.InvalidAllocation);
            uint required = 0; HostCheck.Equal(C.Status.Success, C.GrowthCandidatesRequired(f.State, file, 1300, &required)); HostCheck.Equal(1U, required);
            HostCheck.True(f.Device.State->SubmittedBytes == 0 && f.State->VolumeRevision == 1 && !f.State->Dirty); C.Stop(f.State);
        }
        using (var f = new Fixture())
        {
            f.Mount(); var file = File(f); f.FailPublication = 1; Overwrite(f, file, 0, "x"u8, out _); f.Run();
            byte[] before = new ReadOnlySpan<byte>(f.Input.Before, 512).ToArray(), after = new ReadOnlySpan<byte>(f.Input.After, 512).ToArray();
            uint calls = f.Device.Hardware->Notifies, count = 1; C.FreeCursor cursor = default; C.RequestToken request = default;
            HostCheck.Equal(C.Status.RecoveryRequired, C.CreateFreeCursor(f.State, 2, &cursor));
            HostCheck.Equal(C.Status.RecoveryRequired, C.BeginFindFree(f.State, cursor, 1, 1, 2, &request));
            HostCheck.Equal(C.Status.RecoveryRequired, C.GrowthCandidatesRequired(f.State, file, 1300, &count));
            HostCheck.True(count == 0 && cursor.Owner == 0 && f.Device.Hardware->Notifies == calls &&
                new ReadOnlySpan<byte>(f.Input.Before, 512).SequenceEqual(before) && new ReadOnlySpan<byte>(f.Input.After, 512).SequenceEqual(after));
        }
    }

    [HostTest("continuing FAT repeated discovery allocates no managed buffers and final candidate copy has no new clock callback")]
    private static void FreeAllocation()
    {
        using var f = new Fixture(); f.Record = false; f.Mount(); uint[] output = new uint[16];
        var request = Search(f, Cursor(f, 8), 16, 16, 2); f.Run(); C.FreeResult result = default;
        f.Device.Hardware->ClockFailure = true; uint calls = f.Device.Hardware->Calls;
        HostCheck.Equal(C.Status.Success, C.CopyFree(f.State, request, output, &result)); HostCheck.Equal(calls, f.Device.Hardware->Calls); f.Device.Hardware->ClockFailure = false;
        bool good = true; long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 8; i++)
        {
            C.FreeCursor cursor = default; C.RequestToken next = default;
            good &= C.CreateFreeCursor(f.State, 8, &cursor) == C.Status.Success && C.BeginFindFree(f.State, cursor, 16, 16, 2, &next) == C.Status.Pending &&
                f.Run() == C.Status.Ready && C.CopyFree(f.State, next, output, &result) == C.Status.Success && result.Count == 16;
        }
        HostCheck.True(good && GC.GetAllocatedBytesForCurrentThread() == allocated && f.Device.State->SubmittedBytes == 0); C.Stop(f.State);
    }

    private static bool SameRequest(C.RequestToken a, C.RequestToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation;
    private static bool SameFile(C.FileToken a, C.FileToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Revision == b.Revision && a.EntryOffset == b.EntryOffset &&
        a.FirstCluster == b.FirstCluster && a.Length == b.Length && a.Fingerprint0 == b.Fingerprint0 && a.Fingerprint1 == b.Fingerprint1 && a.Fingerprint2 == b.Fingerprint2 && a.Fingerprint3 == b.Fingerprint3;
    private static void PhysicalFingerprint(C.FileToken file, ReadOnlySpan<byte> raw)
    {
        HostCheck.True(file.Fingerprint0 == System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(raw) &&
            file.Fingerprint1 == System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(raw[8..]) &&
            file.Fingerprint2 == System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(raw[16..]) &&
            file.Fingerprint3 == System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(raw[24..]));
    }

    private static void PrepareGrowth(Fixture f)
    {
        Put16(f.Volatile, First * 512 + 48, 1); Put16(f.Volatile, First * 512 + 50, 6);
        foreach (int sector in new[] { First + 1, First + 7 })
        { var raw = f.Volatile.Slice(sector * 512, 512); Put32(raw, 0, 0x41615252); Put32(raw, 484, 0x61417272); Put32(raw, 488, 123); Put32(raw, 492, 8); Put32(raw, 508, 0xaa550000); }
        f.Fat(6, 0xbfffffff); foreach (uint cluster in new uint[] { 8, 12, 10 }) { f.Fat(cluster, 0xa0000000); f.Volatile.Slice((Data + (int)cluster - 2) * 512, 512).Fill(0xd7); }
    }

    private static void PrepareLongRoot(Fixture f)
    {
        f.Fat(2, 5); f.Fat(5, 7); f.Fat(7, 0xfffffff);
        foreach (int sector in new[] { Data, Data + 3, Data + 5 }) f.Volatile.Slice(sector * 512, 512).Clear();
        for (int i = 0; i < 15; i++)
        { var raw = f.Volatile.Slice(Data * 512 + i * 32, 32); System.Text.Encoding.ASCII.GetBytes("F" + i.ToString("D7") + "TXT").CopyTo(raw); raw[11] = 0x20; }
    }

    private static void Independent(Fixture f, ReadOnlySpan<byte> expected)
    {
        byte[] read = new byte[expected.Length]; var result = Fat32Parser.ReadRootFile(new Device(f.Device.Hardware->Persistent), First, "DATA.BIN", read);
        HostCheck.True(result.Succeeded && result.BytesRead == (ulong)read.Length && read.AsSpan().SequenceEqual(expected));
    }
    private sealed class Device(byte* bytes) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, Sectors).Info;
        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> target)
        {
            if (first >= Sectors || count > Sectors - first || target.Length != count * 512) return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            new ReadOnlySpan<byte>(bytes + first * 512, checked((int)count * 512)).CopyTo(target); return BlockReadResult.Success(count, count * 512UL);
        }
    }
    internal sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* arena = (byte*)NativeMemory.AlignedAlloc(208896, 4096);
        internal readonly byte* Output = (byte*)NativeMemory.AllocZeroed(128);
        internal C.Admission Input;
        internal ulong BackingBytes;
        internal readonly List<(uint Type, ulong Sector)> Trace = [];
        internal bool Record = true;
        internal uint FailAt;
        internal int FailPublication;
        private int publication;
        internal Span<byte> Volatile => new(Device.Hardware->Medium, Sectors * 512);
        internal Span<byte> Persistent => new(Device.Hardware->Persistent, Sectors * 512);
        internal Fixture(byte sectorsPerCluster = 1, bool observe = false, uint spareSectors = 0)
        {
            ulong capacity = (ulong)Data + Clusters * (ulong)sectorsPerCluster; Device = new(capacity + spareSectors, observe);
            new Span<byte>(arena, 208896).Fill(0xa7); byte[] image = new byte[Sectors * 512];
            Span<byte> boot = image.AsSpan(First * 512, 512); Put16(boot, 11, 512); boot[13] = sectorsPerCluster; Put16(boot, 14, Reserved); boot[16] = 2;
            Put32(boot, 32, (uint)(capacity - First)); Put32(boot, 36, FatSectors); Put32(boot, 44, 2); boot[510] = 0x55; boot[511] = 0xaa;
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors })
            { Put32(image, lba * 512, 0xffffff8); Put32(image, lba * 512 + 4, 0xfffffff); Put32(image, lba * 512 + 8, 0xfffffff); Put32(image, lba * 512 + 12, 0xfffffff); Put32(image, lba * 512 + 16, 6); Put32(image, lba * 512 + 24, 0xfffffff); }
            var keep = image.AsSpan(Data * 512, 32); "KEEP    TXT"u8.CopyTo(keep); keep[11] = 0x20; Put16(keep, 26, 3); Put32(keep, 28, 3);
            var file = image.AsSpan(Data * 512 + 32, 32); "DATA    BIN"u8.CopyTo(file); file[11] = 0x20; Put16(file, 26, 4); Put32(file, 28, 900);
            for (int i = 0; i < 900; i++) image[(i < 512 ? Data + 2 : Data + 4) * 512 + i % 512] = Value(i);
            Device.UseMedium(image, sectorsPerCluster != 1 || spareSectors != 0); HostCheck.Equal(B.Status.Success, Device.Start());
            ulong cursor = (ulong)arena + 4096;
            void* Take(ulong bytes) { cursor = (cursor + 15) & ~15UL; ulong value = cursor; cursor += bytes; return (void*)value; }
            Input = new()
            {
                Device = Device.State, Owner = 111, Epoch = 321, PartitionFirst = First, PartitionLast = capacity - 1,
                ChangeCapacity = C.JournalChanges, ChainCapacity = C.MaximumClusters, InputCapacity = C.WriteBytes, WorkBudget = 100000,
                Changes = (Fat32Mutation.Change*)Take(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)),
                Before = (byte*)Take(C.JournalChanges * 512UL), After = (byte*)Take(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)Take(C.MaximumClusters * 4UL), FileMap = (uint*)Take(C.MaximumClusters * 4UL), WalkMap = (uint*)Take(C.MaximumClusters * 4UL),
                Sector = (byte*)Take(512), Fat0 = (byte*)Take(512), Fat1 = (byte*)Take(512), Input = (byte*)Take(C.WriteBytes),
                Name = (ushort*)Take(C.NameWords * 2UL), LongName = (ushort*)Take(C.VfatWords * 2UL), EncodedEntries = (byte*)Take(C.EncodedEntryBytes)
            };
            BackingBytes = cursor - ((ulong)arena + 4096); new Span<byte>(arena + 4096, (int)BackingBytes).Clear();
        }
        internal void Mount() { HostCheck.Equal(C.Status.Pending, C.Start(State, Input)); HostCheck.Equal(C.Status.Ready, Run()); HostCheck.True(State->IsMounted); }
        internal C.Status Turn()
        {
            var status = C.Pump(State);
            if (Device.Hardware->Pending)
            {
                uint type = Device.Hardware->Type; if (Record) Trace.Add((type, Device.Hardware->Sector));
                if (type != 0) publication++;
                bool fail = Device.Hardware->Notifies == FailAt || type != 0 && publication == FailPublication;
                Device.Complete(length: fail && type == 0 ? 1U : uint.MaxValue, status: fail ? (byte)1 : (byte)0);
            }
            return status;
        }
        internal C.Status Run()
        { for (int i = 0; i < 100000; i++) { var status = Turn(); if (status != C.Status.Pending) return status; } throw new InvalidOperationException("continuing mutation did not terminate"); }
        internal void Fat(uint cluster, uint value)
        { foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors }) Put32(Volatile, lba * 512 + (int)cluster * 4, value); }
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Device.State));
            if (State->LastFailure != C.Status.Uninitialized && !State->Dirty) C.Stop(State);
            HostCheck.True(new ReadOnlySpan<byte>(arena, 4096).IndexOfAnyExcept((byte)0xa7) < 0 && new ReadOnlySpan<byte>(arena + 204800, 4096).IndexOfAnyExcept((byte)0xa7) < 0);
            Device.CheckGuards(); Device.Dispose(); NativeMemory.AlignedFree(arena); NativeMemory.Free(State); NativeMemory.Free(Output);
        }
    }
}
