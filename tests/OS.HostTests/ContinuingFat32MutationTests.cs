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
    private static C.Result Result(Fixture f, C.RequestToken request, C.Status expected = C.Status.Success)
    { C.Result result = default; HostCheck.Equal(expected, C.CopyResult(f.State, request, &result)); HostCheck.Equal(expected, result.Status); return result; }

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
    private sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device = new(Sectors);
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
        internal Fixture()
        {
            new Span<byte>(arena, 208896).Fill(0xa7); byte[] image = new byte[Sectors * 512];
            Span<byte> boot = image.AsSpan(First * 512, 512); Put16(boot, 11, 512); boot[13] = 1; Put16(boot, 14, Reserved); boot[16] = 2;
            Put32(boot, 32, Sectors - First); Put32(boot, 36, FatSectors); Put32(boot, 44, 2); boot[510] = 0x55; boot[511] = 0xaa;
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors })
            { Put32(image, lba * 512, 0xffffff8); Put32(image, lba * 512 + 4, 0xfffffff); Put32(image, lba * 512 + 8, 0xfffffff); Put32(image, lba * 512 + 12, 0xfffffff); Put32(image, lba * 512 + 16, 6); Put32(image, lba * 512 + 24, 0xfffffff); }
            var keep = image.AsSpan(Data * 512, 32); "KEEP    TXT"u8.CopyTo(keep); keep[11] = 0x20; Put16(keep, 26, 3); Put32(keep, 28, 3);
            var file = image.AsSpan(Data * 512 + 32, 32); "DATA    BIN"u8.CopyTo(file); file[11] = 0x20; Put16(file, 26, 4); Put32(file, 28, 900);
            for (int i = 0; i < 900; i++) image[(i < 512 ? Data + 2 : Data + 4) * 512 + i % 512] = Value(i);
            Device.UseMedium(image); HostCheck.Equal(B.Status.Success, Device.Start());
            ulong cursor = (ulong)arena + 4096;
            void* Take(ulong bytes) { cursor = (cursor + 15) & ~15UL; ulong value = cursor; cursor += bytes; return (void*)value; }
            Input = new()
            {
                Device = Device.State, Owner = 111, Epoch = 321, PartitionFirst = First, PartitionLast = Sectors - 1,
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
