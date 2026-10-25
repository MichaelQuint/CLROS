using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using D = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

internal static unsafe class LinuxContinuingWritableDescriptorServicesTests
{
    private static byte Value(int i) => unchecked((byte)(i * 17 + 5));
    private delegate bool RetainedStorage(D.State* state, ulong epoch, ulong address, ulong bytes);
    private static readonly RetainedStorage RetainPredicate = typeof(D).GetMethod("CanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<RetainedStorage>();

    [HostTest("continuing path stat and lstat use copied names fresh paired chain facts and root allocation without publishing descriptions or cursors")]
    private static void PathStatFacts()
    {
        foreach (byte sectors in new byte[] { 1, 2, 4 })
        {
            using var f = new Owner(sectorsPerCluster: sectors); var a = f.Open(); var b = f.Open();
            D.Seek(f.State, a, 7, 0); D.Seek(f.State, b, 29, 0);
            byte[] descriptions = new ReadOnlySpan<byte>(f.Descriptions, 32 * sizeof(D.Description)).ToArray();
            byte[] slots = new ReadOnlySpan<byte>(f.Slots, 32 * sizeof(D.Descriptor)).ToArray();
            foreach (bool noFollow in new[] { false, true })
            {
                byte[] path = "/data/DATA.BIN"u8.ToArray(); var request = D.BeginPathStat(f.State, path, noFollow); path.AsSpan().Fill((byte)'x');
                HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase); D.StatMetadata facts = default;
                HostCheck.Equal(0L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
                HostCheck.True(!facts.Directory && facts.DescriptionGeneration == 0 && facts.Facts.File.Length == 900 &&
                    facts.Facts.ClusterBytes == sectors * 512U && facts.Facts.Allocated512Blocks == (sectors == 1 ? 2UL : sectors));
                var root = PathFacts(f, noFollow ? "/data/" : "/data");
                HostCheck.True(root.Directory && root.Facts.File.Owner == 0 && root.DescriptionGeneration == 0 &&
                    root.Root.Owner == (ulong)f.Storage.State && root.Root.Revision == f.Storage.State->VolumeRevision &&
                    root.Root.FirstSector == f.DataLba && root.Root.ClusterBytes == sectors * 512U && root.Root.AllocatedClusters == 1 &&
                    root.Root.Allocated512Blocks == sectors);
            }
            HostCheck.True(slots.SequenceEqual(new ReadOnlySpan<byte>(f.Slots, 32 * sizeof(D.Descriptor))) &&
                descriptions.SequenceEqual(new ReadOnlySpan<byte>(f.Descriptions, 32 * sizeof(D.Description))));
            HostCheck.True(f.Cursor(a) == 7 && f.Cursor(b) == 29 && f.Trace.All(row => row.Type == 0));
        }
        Console.WriteLine("HOST_WRITABLE_PATH_STAT_SIZE state=" + sizeof(D.State) + " stat=" + sizeof(D.StatMetadata) + " root=" + sizeof(C.RootFacts));
    }

    [HostTest("continuing path stat discovers actual missing journal WAL and SHM entries and preserves confinement and trailing slash errors")]
    private static void PathStatMissing()
    {
        using var f = new Owner(); uint submitted = f.Device->Notifies; D.StatMetadata facts = default;
        foreach (string name in new[] { "/data/contract.db", "/data/contract.db-journal", "/data/contract.db-wal", "/data/contract.db-shm" })
        {
            var request = D.BeginPathStat(f.State, Encoding.ASCII.GetBytes(name), true); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
            HostCheck.Equal(-2L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
            HostCheck.True(!f.State->RequestActive && facts.Facts.File.Owner == 0 && facts.Root.Owner == 0);
        }
        HostCheck.True(f.Device->Notifies > submitted);
        var trailing = D.BeginPathStat(f.State, "/data/DATA.BIN/"u8); f.Wait(trailing);
        HostCheck.Equal(-20L, D.CopyStat(f.State, trailing.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
        var missing = D.BeginPathStat(f.State, "/data/missing/"u8); f.Wait(missing);
        HostCheck.Equal(-2L, D.CopyStat(f.State, missing.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
        foreach (string path in new[] { "data/DATA.BIN", "/data/../DATA.BIN", "/data//DATA.BIN", "/data//", "/else/DATA.BIN" })
            HostCheck.True(D.BeginPathStat(f.State, Encoding.ASCII.GetBytes(path)).Value < 0 && !f.State->RequestActive);
        HostCheck.Equal(-95L, D.BeginPathStat(f.State, "/data/nested/file"u8).Value);
    }

    [HostTest("continuing held path stat output validates whole aliases and sizes before writes and never consumes metadata through generic Complete")]
    private static void PathStatOutput()
    {
        using var f = new Owner(locks: true, observe: true); var file = f.Open(); HostCheck.Equal(0L, D.Flock(f.State, file, 6));
        foreach (string path in new[] { "/data", "/data/DATA.BIN" })
        {
            var request = D.BeginPathStat(f.State, Encoding.ASCII.GetBytes(path)); f.Wait(request);
            HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase); D.StatMetadata facts = default;
            foreach (uint bytes in new uint[] { 0, (uint)sizeof(D.StatMetadata) - 1, (uint)sizeof(D.StatMetadata) + 1 })
                HostCheck.Equal(-14L, D.CopyStat(f.State, request.Request, &facts, bytes).Value);
            foreach (var range in ProtectedStorage(f))
                foreach (ulong pointer in new[] { range.Start, (range.Start + range.Bytes - 8) & ~7UL })
                    HostCheck.Equal(-14L, D.CopyStat(f.State, request.Request, (D.StatMetadata*)pointer, (uint)sizeof(D.StatMetadata)).Value);
            HostCheck.True(f.State->RequestActive && facts.Facts.File.Owner == 0 && facts.Root.Owner == 0);
            HostCheck.Equal(0L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
        }
        HostCheck.Equal(0L, D.Flock(f.State, file, 6)); HostCheck.Equal(0UL, f.Cursor(file));
    }

    [HostTest("continuing path metadata pending and held cancellation joins the original read without late output for root or regular paths")]
    private static void PathStatCancellation()
    {
        foreach (string path in new[] { "/data", "/data/DATA.BIN" })
            foreach (bool held in new[] { false, true })
            {
                using var f = new Owner(); var request = D.BeginPathStat(f.State, Encoding.ASCII.GetBytes(path));
                if (held) f.Wait(request); else { D.Pump(f.State); HostCheck.True(f.Device->Pending); }
                var cancelled = D.Cancel(f.State, request.Request);
                if (!held) { f.CompleteHardware(); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase); }
                else HostCheck.Equal(D.Progress.Ready, cancelled.Phase);
                D.StatMetadata output = default; new Span<byte>(&output, sizeof(D.StatMetadata)).Fill(0xa5);
                HostCheck.Equal(-4L, D.CopyStat(f.State, request.Request, &output, (uint)sizeof(D.StatMetadata)).Value);
                HostCheck.True(new ReadOnlySpan<byte>(&output, sizeof(D.StatMetadata)).IndexOfAnyExcept((byte)0xa5) < 0 &&
                    !f.State->RequestActive && !f.Storage.State->RequestActive);
            }
    }

    [HostTest("continuing path stat revokes pending process and request generations on exit before any metadata publication")]
    private static void PathStatExit()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State);
        var request = D.BeginPathStat(f.State, "/data"u8); D.Pump(f.State); HostCheck.True(f.Device->Pending);
        HostCheck.Equal(D.Progress.Pending, D.ExitProcess(f.State, primary).Phase);
        var replacement = D.RegisterProcess(f.State, primary.Pid).Process; HostCheck.True(replacement.Generation != primary.Generation);
        f.CompleteHardware(); f.Drain(); D.StatMetadata facts = default;
        HostCheck.Equal(D.Progress.Rejected, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Phase);
        HostCheck.Equal(-9L, D.BeginPathStatForProcess(f.State, primary, "/data"u8).Value);
        HostCheck.True(!f.State->RequestActive && !f.Storage.State->RequestActive);
    }

    [HostTest("continuing fresh path metadata sees own creation growth and removal while stopped device and foreign revision revoke held copies")]
    private static void PathStatMutationLifetime()
    {
        using (var f = new Owner(growth: true))
        {
            var database = f.Open(flags: 2); HostCheck.Equal(0L, D.Flock(f.State, database, 6));
            var journal = f.Open("/data/contract.db-journal", 2 | D.Create | D.Exclusive);
            HostCheck.Equal(0U, PathFacts(f, "/data/contract.db-journal").Facts.File.Length);
            HostCheck.Equal(37L, f.Run(D.BeginWriteWithFreeSearch(f.State, journal, new byte[37], new(8, 64, 128), 0, true)).Value);
            HostCheck.Equal(37U, PathFacts(f, "/data/contract.db-journal").Facts.File.Length);
            HostCheck.Equal(0L, D.Close(f.State, journal)); HostCheck.Equal(0L, f.Run(D.BeginUnlink(f.State, "/data/contract.db-journal"u8)).Value);
            var request = D.BeginPathStat(f.State, "/data/contract.db-journal"u8); f.Wait(request); D.StatMetadata facts = default;
            HostCheck.Equal(-2L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
            HostCheck.Equal(0L, D.Flock(f.State, database, 6));
        }
        foreach (bool revision in new[] { false, true })
        {
            using var f = new Owner(); var request = D.BeginPathStat(f.State, "/data"u8); f.Wait(request); D.StatMetadata facts = default;
            if (revision) { ulong* value = (ulong*)((byte*)f.Storage.State + WritableBlockSessionTests.ManagedOffset(f.Storage.State, "Revision")); (*value)++; }
            else B.Stop(f.Storage.Device.State);
            HostCheck.Equal(-5L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
            HostCheck.True(f.State->RequestActive && facts.Root.Owner == 0 && facts.Facts.File.Owner == 0);
        }
    }

    private static D.StatMetadata PathFacts(Owner f, string path)
    {
        var request = D.BeginPathStat(f.State, Encoding.ASCII.GetBytes(path)); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        D.StatMetadata facts = default; HostCheck.Equal(0L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value); return facts;
    }

    [HostTest("continuing flock supports measured nonblocking modes validates flags and keeps directory and blocking modes explicit")]
    private static void FlockModes()
    {
        using var f = new Owner(); var a = f.Open(); var b = f.Open(flags: 1); var directory = f.Open("/data");
        uint calls = f.Device->Calls, notifies = f.Device->Notifies;
        HostCheck.Equal(0L, D.Flock(f.State, a, 6)); // Linux permits an exclusive advisory lock on a readonly fd.
        HostCheck.Equal(-11L, D.Flock(f.State, b, 5));
        foreach (uint operation in new uint[] { 0, 3, 4, 7, 9, 10, 13, 16, uint.MaxValue })
            HostCheck.Equal(-22L, D.Flock(f.State, a, operation));
        HostCheck.Equal(-38L, D.Flock(f.State, a, 1)); HostCheck.Equal(-38L, D.Flock(f.State, a, 2));
        HostCheck.Equal(-11L, D.Flock(f.State, b, 5)); // Invalid requests did not unlock a.
        HostCheck.Equal(0L, D.Flock(f.State, a, 12)); HostCheck.Equal(0L, D.Flock(f.State, b, 5));
        HostCheck.Equal(0L, D.Flock(f.State, a, 5)); HostCheck.Equal(0L, D.Flock(f.State, a, 8));
        HostCheck.Equal(-95L, D.Flock(f.State, directory, 5)); HostCheck.Equal(-95L, D.Flock(f.State, directory, 8));
        HostCheck.True(f.Device->Calls == calls && f.Device->Notifies == notifies && f.Cursor(a) == 0 && f.Cursor(b) == 0);
        Console.WriteLine("HOST_WRITABLE_FLOCK_SIZE state=" + sizeof(D.State) + " description=" + sizeof(D.Description));
    }

    [HostTest("continuing flock belongs to independent open descriptions and remains separate from process byte range locks")]
    private static void FlockDescriptions()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var sameProcess = f.Open(flags: 2); var b = f.OpenFor(other, flags: 2); var different = f.Open("/data/KEEP.TXT");
        HostCheck.Equal(0L, D.Flock(f.State, a, 5)); HostCheck.Equal(0L, D.FlockForProcess(f.State, other, b, 5));
        HostCheck.Equal(-11L, D.Flock(f.State, sameProcess, 6));
        HostCheck.Equal(0L, D.Flock(f.State, different, 6));
        HostCheck.Equal(0L, D.Flock(f.State, a, 8)); HostCheck.Equal(0L, D.FlockForProcess(f.State, other, b, 8));
        HostCheck.Equal(0L, L(f, primary, a, 6, 1, 0, 0).Value);
        HostCheck.Equal(0L, D.FlockForProcess(f.State, other, b, 6));
        HostCheck.Equal(-11L, L(f, other, b, 6, 1, 0, 1).Value); // fcntl conflict is independent of flock ownership.
        HostCheck.Equal(0L, L(f, primary, a, 6, 2, 0, 0).Value);
        HostCheck.Equal(-11L, D.Flock(f.State, sameProcess, 5));
        HostCheck.Equal(-9L, D.FlockForProcess(f.State, primary, b, 8));
        HostCheck.Equal(0L, D.FlockForProcess(f.State, other, b, 8));
    }

    [HostTest("continuing flock failed nonblocking conversion drops the old shared mode and duplicate unlock uses the same description")]
    private static void FlockConversion()
    {
        using var f = new Owner(); var a = f.Open(); var b = f.Open(); var duplicate = D.Dup(f.State, a).Descriptor;
        HostCheck.Equal(0L, D.Flock(f.State, a, 5)); HostCheck.Equal(0L, D.Flock(f.State, b, 5));
        HostCheck.Equal(-11L, D.Flock(f.State, duplicate, 6));
        HostCheck.Equal(0L, D.Flock(f.State, b, 6)); // a's failed upgrade removed its shared lock.
        HostCheck.Equal(-11L, D.Flock(f.State, a, 5)); HostCheck.Equal(0L, D.Flock(f.State, b, 8));
        HostCheck.Equal(0L, D.Flock(f.State, a, 6)); HostCheck.Equal(0L, D.Flock(f.State, duplicate, 8));
        HostCheck.Equal(0L, D.Flock(f.State, b, 6)); HostCheck.Equal(0L, D.Flock(f.State, b, 5));
    }

    [HostTest("continuing flock survives one duplicate close and the last pending borrower until actual cancellation retirement")]
    private static void FlockBorrowLifetime()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Owner(); var a = f.Open(); var b = f.Open(); var duplicate = D.Dup(f.State, a).Descriptor;
            HostCheck.Equal(0L, D.Flock(f.State, a, 6)); HostCheck.Equal(0L, D.Close(f.State, a));
            HostCheck.Equal(-11L, D.Flock(f.State, b, 5));
            var read = D.BeginRead(f.State, duplicate, 8);
            if (held) HostCheck.Equal(D.Progress.Ready, f.Wait(read).Phase);
            else { D.Pump(f.State); HostCheck.True(f.Device->Pending); }
            HostCheck.Equal(0L, D.Close(f.State, duplicate));
            HostCheck.Equal(f.State->RequestActive ? -11L : 0L, D.Flock(f.State, b, 5));
            if (f.State->RequestActive) { f.CompleteHardware(); f.Drain(); }
            HostCheck.True(!f.State->RequestActive); HostCheck.Equal(0L, D.Flock(f.State, b, 6));
            HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, read.Request, new byte[8]).Phase);
            var reused = f.Open(); HostCheck.True(reused.Number == a.Number && reused.Generation != a.Generation);
            HostCheck.Equal(-9L, D.Flock(f.State, a, 8)); HostCheck.Equal(-11L, D.Flock(f.State, reused, 5));
        }
    }

    [HostTest("continuing flock process exit retains a pending description until drain and rejects reused PID and fd authority")]
    private static void FlockExit()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(); var b = f.OpenFor(other); HostCheck.Equal(0L, D.Flock(f.State, a, 6));
        var read = D.BeginRead(f.State, a, 8); D.Pump(f.State); HostCheck.True(f.Device->Pending);
        HostCheck.Equal(D.Progress.Pending, D.ExitProcess(f.State, primary).Phase);
        HostCheck.Equal(-11L, D.FlockForProcess(f.State, other, b, 6));
        var replacement = D.RegisterProcess(f.State, primary.Pid).Process;
        HostCheck.True(replacement.Generation != primary.Generation);
        HostCheck.Equal(-9L, D.FlockForProcess(f.State, primary, a, 8));
        f.CompleteHardware(); f.Drain(); HostCheck.Equal(0L, D.FlockForProcess(f.State, other, b, 6));
        var fresh = f.OpenFor(replacement); HostCheck.True(fresh.Number == a.Number && fresh.Generation != a.Generation);
        HostCheck.Equal(-9L, D.FlockForProcess(f.State, primary, fresh, 8));
        HostCheck.Equal(-11L, D.FlockForProcess(f.State, replacement, fresh, 5));
        HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, read.Request, new byte[8]).Phase);
    }

    [HostTest("continuing flock retains its inode lineage through own writes and refuses dirty mutation or unrelated revision effects")]
    private static void FlockRevision()
    {
        using (var f = new Owner())
        {
            var a = f.Open(flags: 2); var b = f.Open(); HostCheck.Equal(0L, D.Flock(f.State, a, 6));
            var write = D.BeginWrite(f.State, a, new byte[700]);
            for (int i = 0; i < 10000 && !f.Storage.State->Dirty; i++) f.Tick();
            HostCheck.True(f.Storage.State->Dirty); HostCheck.Equal(-5L, D.Flock(f.State, a, 8));
            HostCheck.Equal(-5L, D.Flock(f.State, b, 5)); HostCheck.Equal(700L, f.Run(write).Value);
            HostCheck.Equal(-11L, D.Flock(f.State, b, 5)); HostCheck.Equal(0L, D.Flock(f.State, a, 8));
            HostCheck.Equal(0L, D.Flock(f.State, b, 6));
        }
        using (var f = new Owner())
        {
            var a = f.Open(); HostCheck.Equal(0L, D.Flock(f.State, a, 6));
            ulong* revision = (ulong*)((byte*)f.Storage.State + WritableBlockSessionTests.ManagedOffset(f.Storage.State, "Revision"));
            (*revision)++; byte[] before = new ReadOnlySpan<byte>(f.Descriptions, 32 * sizeof(D.Description)).ToArray();
            HostCheck.Equal(-5L, D.Flock(f.State, a, 8)); HostCheck.True(f.State->Quarantined);
            HostCheck.True(before.SequenceEqual(new ReadOnlySpan<byte>(f.Descriptions, 32 * sizeof(D.Description))));
        }
    }

    [HostTest("continuing flock audits retained extents and original DMA generation before changing any mode")]
    private static void FlockStorageLifetime()
    {
        using var f = new Owner(); var a = f.Open(); var b = f.Open(); HostCheck.Equal(0L, D.Flock(f.State, a, 6));
        D.Admission* input = (D.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input")); var saved = *input;
        try
        {
            input->Descriptions = (D.Description*)input->Input;
            HostCheck.Equal(-5L, D.Flock(f.State, a, 8));
        }
        finally { *input = saved; }
        ulong* generation = (ulong*)((byte*)f.Storage.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.Slot, "Generation"));
        ulong old = *generation; uint calls = f.Device->Calls;
        try
        {
            (*generation)++; HostCheck.Equal(-5L, D.Flock(f.State, a, 8)); HostCheck.Equal(-5L, D.Flock(f.State, b, 5));
        }
        finally { *generation = old; }
        HostCheck.True(f.Device->Calls == calls); HostCheck.Equal(-11L, D.Flock(f.State, b, 5));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); HostCheck.Equal(-5L, D.Flock(f.State, a, 8));
        HostCheck.Equal(0L, D.Close(f.State, a)); HostCheck.Equal(0L, D.Close(f.State, b));
    }

    [HostTest("continuing flock failed flush and exiting writer retain the borrowed description lock and dirty recovery backing")]
    private static void FlockDirtyExit()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var b = f.OpenFor(other); HostCheck.Equal(0L, D.Flock(f.State, a, 6));
        uint index = *(uint*)((byte*)(f.Slots + a.Number) + WritableBlockSessionTests.ManagedOffset(f.Slots + a.Number, "Description")) - 1;
        byte* mode = (byte*)(f.Descriptions + index) + WritableBlockSessionTests.ManagedOffset(f.Descriptions + index, "FlockMode");
        f.FailureType = 4; var write = D.BeginWrite(f.State, a, new byte[37]);
        HostCheck.Equal(D.Progress.RecoveryRequired, f.Wait(write).Phase);
        HostCheck.True(f.Storage.State->Dirty && *mode == 2 && f.State->RequestActive);
        HostCheck.Equal(D.Progress.RecoveryRequired, D.ExitProcess(f.State, primary).Phase);
        HostCheck.Equal(-5L, D.FlockForProcess(f.State, other, b, 6));
        HostCheck.True(*mode == 2 && f.State->RequestActive && !f.State->Retired);
        byte[] journal = new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).ToArray();
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        HostCheck.True(*mode == 2 && f.State->RequestActive && journal.SequenceEqual(new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024)));
    }

    [HostTest("continuing flock bounded mode scans make no managed allocations device calls or cursor changes")]
    private static void FlockAllocation()
    {
        using var f = new Owner(); var a = f.Open(); var b = f.Open(); f.Record = false;
        HostCheck.Equal(0L, D.Flock(f.State, a, 6)); HostCheck.Equal(-11L, D.Flock(f.State, b, 5)); HostCheck.Equal(0L, D.Flock(f.State, a, 8));
        uint calls = f.Device->Calls, notifies = f.Device->Notifies; long allocated = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 64; i++)
            good &= D.Flock(f.State, a, 6) == 0 && D.Flock(f.State, b, 5) == -11 && D.Flock(f.State, a, 8) == 0;
        HostCheck.True(good && GC.GetAllocatedBytesForCurrentThread() == allocated && calls == f.Device->Calls && notifies == f.Device->Notifies);
        HostCheck.True(f.Cursor(a) == 0 && f.Cursor(b) == 0);
    }

    [HostTest("continuing fresh stat walks actual FAT geometry for read only and write only handles without moving independent cursors")]
    private static void FreshStatGeometry()
    {
        foreach (byte sectors in new byte[] { 1, 2, 4, 8 })
        {
            using var f = new Owner(sectorsPerCluster: sectors); var first = f.Open(); var second = f.Open(flags: 1);
            HostCheck.Equal(7L, D.Seek(f.State, first, 7, 0).Value); HostCheck.Equal(29L, D.Seek(f.State, second, 29, 0).Value);
            foreach (var file in new[] { first, second })
            {
                int trace = f.Trace.Count; var facts = f.Stat(file).Facts;
                HostCheck.True(facts.File.Owner == (ulong)f.Storage.State && facts.File.Epoch == f.Input.Epoch && facts.File.Length == 900);
                HostCheck.Equal((uint)sectors * 512U, facts.ClusterBytes);
                HostCheck.Equal(sectors == 1 ? 2U : 1U, facts.AllocatedClusters);
                HostCheck.Equal((ulong)facts.AllocatedClusters * sectors, facts.Allocated512Blocks);
                HostCheck.True(f.Trace.Skip(trace).Any(row => row.Type == 0) && !f.Trace.Skip(trace).Any(row => row.Type != 0));
            }
            HostCheck.Equal(7UL, f.Cursor(first)); HostCheck.Equal(29UL, f.Cursor(second));
        }
    }

    [HostTest("continuing stat output rejects short oversized overflowing and retained full or partial aliases without consuming fresh facts")]
    private static void StatOutputBounds()
    {
        using var f = new Owner(locks: true, observe: true); var file = f.Open(); var request = D.BeginStat(f.State, file);
        HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase); HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
        D.StatMetadata facts = default;
        foreach (uint bytes in new uint[] { 0, (uint)sizeof(D.StatMetadata) - 1, (uint)sizeof(D.StatMetadata) + 1 })
            HostCheck.Equal(-14L, D.CopyStat(f.State, request.Request, &facts, bytes).Value);
        foreach (var extent in ProtectedStorage(f))
        {
            // The inventory includes deliberately unmapped fake MMIO ranges.
            // Audit their numeric aliases without dereferencing those addresses.
            byte[] before = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray();
            foreach (ulong pointer in new[] { extent.Start, (extent.Start + extent.Bytes - 8) & ~7UL })
                HostCheck.Equal(-14L, D.CopyStat(f.State, request.Request, (D.StatMetadata*)pointer, (uint)sizeof(D.StatMetadata)).Value);
            HostCheck.True(before.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()));
        }
        HostCheck.Equal(-14L, D.CopyStat(f.State, request.Request, (D.StatMetadata*)(ulong.MaxValue - 7), (uint)sizeof(D.StatMetadata)).Value);
        HostCheck.True(f.State->RequestActive && f.Cursor(file) == 0);
        HostCheck.Equal(0L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
        HostCheck.True(facts.Facts.File.Length == 900 && !f.State->RequestActive);
    }

    [HostTest("continuing stat pending cancellation drains actual IN and held cancellation cannot publish cached facts")]
    private static void StatCancellation()
    {
        using var f = new Owner(); var file = f.Open(); D.StatMetadata facts = default;
        var request = D.BeginStat(f.State, file); D.Pump(f.State); HostCheck.True(f.Device->Pending);
        HostCheck.Equal(D.Progress.Pending, D.Cancel(f.State, request.Request).Phase); f.CompleteHardware();
        HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        HostCheck.Equal(-4L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
        HostCheck.True(facts.Facts.File.Owner == 0 && !f.Storage.State->RequestActive && !f.State->RequestActive);
        request = D.BeginStat(f.State, file); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        HostCheck.Equal(D.Progress.Ready, D.Cancel(f.State, request.Request).Phase);
        HostCheck.Equal(-4L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
        HostCheck.Equal(0UL, f.Cursor(file));
    }

    [HostTest("continuing stat original fd generation process ownership and exit reject late facts after close or PID reuse")]
    private static void StatFdAndProcessLifetime()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 202).Process;
        var first = f.Open(); HostCheck.Equal(-9L, D.BeginStatForProcess(f.State, other, first).Value);
        var request = D.BeginStat(f.State, first); D.Pump(f.State); HostCheck.True(f.Device->Pending);
        HostCheck.Equal(0L, D.Close(f.State, first)); f.CompleteHardware(); f.Drain();
        var replacement = f.Open(); HostCheck.True(first.Number == replacement.Number && first.Generation != replacement.Generation);
        D.StatMetadata facts = default; HostCheck.Equal(D.Progress.Rejected, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Phase);
        var second = f.OpenFor(other); HostCheck.Equal(-9L, D.BeginStat(f.State, second).Value);
        request = D.BeginStatForProcess(f.State, other, second); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        HostCheck.Equal(0L, D.ExitProcess(f.State, other).Value); var reused = D.RegisterProcess(f.State, other.Pid).Process;
        HostCheck.True(other.Generation != reused.Generation && D.BeginStatForProcess(f.State, other, replacement).Value == -9);
        HostCheck.Equal(D.Progress.Rejected, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Phase);
        HostCheck.True(facts.Facts.File.Owner == 0 && D.BeginStatForProcess(f.State, other, replacement).Value == -9);
        HostCheck.True(f.Stat(replacement).Facts.File.Length == 900 && D.PrimaryProcess(f.State).Generation == primary.Generation);
    }

    [HostTest("continuing stat refresh rejects externally changed same entry fingerprint rather than binding a new file")]
    private static void StatStalePhysicalFacts()
    {
        using var f = new Owner(); var file = f.Open();
        f.Storage.Volatile[(int)(f.DataLba * 512 + 32 + 28)] = 0x83; // 899 instead of original 900
        var request = D.BeginStat(f.State, file); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        D.StatMetadata facts = default; var result = D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata));
        HostCheck.True(result.Value == -116 && result.Failure == C.Status.StaleToken && facts.Facts.File.Owner == 0 && !f.State->RequestActive);
    }

    [HostTest("continuing stat authenticates known own growth shrink and empty allocation without confusing unrelated descriptions")]
    private static void StatOwnMutationLineage()
    {
        using var f = new Owner(growth: true); var first = f.Open(flags: 2); var second = f.Open();
        HostCheck.Equal(0L, f.Run(D.BeginGrow(f.State, first, 1200, new uint[] { 8 })).Value);
        var grown = f.Stat(second).Facts; HostCheck.True(grown.File.Length == 1200 && grown.AllocatedClusters == 3 && grown.Allocated512Blocks == 3);
        HostCheck.Equal(0L, f.Run(D.BeginTruncate(f.State, first, 0)).Value);
        var empty = f.Stat(second).Facts; HostCheck.True(empty.File.Length == 0 && empty.File.FirstCluster == 0 && empty.AllocatedClusters == 0 && empty.Allocated512Blocks == 0);
        HostCheck.True(grown.File.EntryOffset == empty.File.EntryOffset && grown.File.Revision < empty.File.Revision && f.Cursor(first) == 0 && f.Cursor(second) == 0);
        var directory = f.Open("/data", D.DirectoryOnly); HostCheck.Equal(-95L, D.BeginStat(f.State, directory).Value);
    }

    [HostTest("continuing stat reports actual device read failure without facts and rejects device retirement after readiness")]
    private static void StatDeviceLifetime()
    {
        using (var f = new Owner())
        {
            var file = f.Open(); f.FailureType = 0; var request = D.BeginStat(f.State, file);
            HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase); D.StatMetadata facts = default;
            var failed = D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata));
            HostCheck.True(failed.Value == -5 && failed.Failure == C.Status.BlockFailure && facts.Facts.File.Owner == 0 && !f.State->RequestActive);
        }
        using (var f = new Owner())
        {
            var file = f.Open(); var request = D.BeginStat(f.State, file); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
            HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); D.StatMetadata facts = default;
            HostCheck.Equal(-5L, D.CopyStat(f.State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value);
            HostCheck.True(facts.Facts.File.Owner == 0 && f.State->RequestActive); D.Cancel(f.State, request.Request); D.Complete(f.State, request.Request);
        }
    }

    [HostTest("continuing writable descriptors retain bounded POD without image snapshot or managed references")]
    private static void Pod()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<D.State>() && !RuntimeHelpers.IsReferenceOrContainsReferences<D.Admission>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<D.Descriptor>() && !RuntimeHelpers.IsReferenceOrContainsReferences<D.Description>());
        HostCheck.True(sizeof(D.State) + 32 * sizeof(D.Descriptor) + 32 * sizeof(D.Description) + 8192 < 32768);
        using var f = new Owner(); HostCheck.True(!f.State->RequestActive && !f.State->Quarantined);
        Console.WriteLine("HOST_WRITABLE_PROVIDER_SIZE state=" + sizeof(D.State) + " descriptor=" + sizeof(D.Descriptor) +
            " description=" + sizeof(D.Description) + " tablesInput=" + (32 * sizeof(D.Descriptor) + 32 * sizeof(D.Description) + 8192) +
            " engineBacking=" + f.Storage.BackingBytes + " engineState=" + sizeof(C.State) + " blockState=" + sizeof(B.State));
        HostCheck.Equal(-9L, D.Capture(f.State, 0).Value); HostCheck.Equal(0U, f.Device->Notifies - f.AtAttach);
        HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase); HostCheck.True(f.State->Retired);
        HostCheck.True(f.Storage.State->IsMounted && !f.Storage.State->Retired && f.Storage.Device.State->Running);
    }

    [HostTest("continuing descriptor admission rejects alias substituted epoch dirty tables and incorrect bounds without effects")]
    private static void Admission()
    {
        using var f = new Owner(attach: false); var a = f.Input; byte[] before = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray();
        a.Epoch++; HostCheck.True(!D.Start(f.State, a)); a = f.Input; a.Input = f.Storage.Input.Before; HostCheck.True(!D.Start(f.State, a));
        a = f.Input; a.Descriptions = (D.Description*)f.Slots; HostCheck.True(!D.Start(f.State, a));
        a = f.Input; a.DescriptorCount = 33; HostCheck.True(!D.Start(f.State, a)); a = f.Input; a.InputBytes--; HostCheck.True(!D.Start(f.State, a));
        a = f.Input; a.ExclusiveOwnerValidated = false; HostCheck.True(!D.Start(f.State, a));
        f.InputBytes[0] = 1; HostCheck.True(!D.Start(f.State, f.Input)); f.InputBytes[0] = 0;
        HostCheck.True(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).SequenceEqual(before) && f.Device->Notifies == f.AtAttach);
        HostCheck.True(D.Start(f.State, f.Input)); f.Attached = true; HostCheck.True(!D.Start(f.State, f.Input));
    }

    [HostTest("continuing writable opens confine exact root components and reject unsupported namespaces before I O")]
    private static void Paths()
    {
        using var f = new Owner(); uint before = f.Device->Notifies;
        foreach (string path in new[] { "/outside/file", "/data/../private/sentinel", "/data/.", "/data/a/../b", "/data//x", "/data/a." })
            HostCheck.Equal(-13L, D.Open(f.State, Encoding.ASCII.GetBytes(path), 0).Value);
        HostCheck.Equal(-95L, D.Open(f.State, "/data/nested/child"u8, 0).Value);
        HostCheck.Equal(-22L, D.Open(f.State, "/data/bad?name"u8, 0).Value);
        HostCheck.Equal(-21L, D.Open(f.State, "/data"u8, 2).Value);
        HostCheck.Equal(-20L, D.Open(f.State, "/data/DATA.BIN"u8, D.DirectoryOnly).Value);
        HostCheck.Equal(-95L, D.Open(f.State, "/data/DATA.BIN"u8, 0x200).Value);
        HostCheck.Equal(-14L, D.Open(f.State, new ReadOnlySpan<byte>(f.InputBytes, 16), 0).Value);
        HostCheck.Equal(before, f.Device->Notifies);
        var root = f.Open("/data/"); HostCheck.True(D.InspectCopied(f.State, root) is { Valid: true, Directory: true });
        HostCheck.Equal(-29L, D.Seek(f.State, root, 0, 0).Value); HostCheck.Equal(-21L, D.BeginRead(f.State, root, 1).Value);
        HostCheck.Equal(-22L, D.BeginSync(f.State, root, true).Value);
        HostCheck.Equal(-2L, f.Run(D.Open(f.State, "/data/missing.bin"u8, 0)).Value);
    }

    [HostTest("continuing writable existing opens use live metadata and exclusive create never replaces a file")]
    private static void OpenMetadata()
    {
        using var f = new Owner(); int start = f.Trace.Count; var file = f.Open(); var info = D.InspectCopied(f.State, file);
        HostCheck.True(info.Valid && !info.Directory && info.Length == 900 && info.EntryOffset == f.DataLba * 512 + 32);
        HostCheck.True(f.Trace.Skip(start).Any(row => row.Type == 0) && f.Trace.Skip(start).All(row => row.Type == 0));
        HostCheck.Equal(file.Generation, D.Capture(f.State, file.Number).Descriptor.Generation);
        HostCheck.Equal(-17L, f.Run(D.Open(f.State, "/data/DATA.BIN"u8, D.Create | D.Exclusive | 2)).Value);
        HostCheck.Equal(0UL, f.Storage.Device.State->SubmittedBytes);
        f.Storage.Volatile[(int)(f.DataLba * 512 + 32 + 11)] |= 1;
        HostCheck.Equal(-13L, f.Run(D.Open(f.State, "/data/DATA.BIN"u8, 2)).Value);
        HostCheck.True(f.Run(D.Open(f.State, "/data/DATA.BIN"u8, 0)).Value >= 3);
    }

    [HostTest("continuing confined non TTY opens admit measured O NOCTTY flags without changing access creation or stored status authority")]
    private static void NonTtyOpen()
    {
        using var f = new Owner(); uint before = f.Device->Notifies;
        var root = f.Open("/data", 0x98100); HostCheck.True(D.InspectCopied(f.State, root).Directory && f.Device->Notifies == before);
        var read = f.Open(flags: 0x88100); var write = f.Open(flags: 0x88101);
        HostCheck.True(f.Read(read, 9).SequenceEqual(Enumerable.Range(0, 9).Select(Value)));
        HostCheck.Equal(-9L, D.BeginWrite(f.State, read, new byte[] { 71 }, 5, true).Value);
        byte[] expected = Enumerable.Range(0, 900).Select(Value).ToArray(); expected[5] = 71;
        HostCheck.Equal(1L, f.Run(D.BeginWrite(f.State, write, new byte[] { 71 }, 5, true)).Value); f.Independent("DATA.BIN", expected);
        uint description = *(uint*)((byte*)(f.Slots + write.Number) + WritableBlockSessionTests.ManagedOffset(f.Slots + write.Number, "Description"));
        uint flags = *(uint*)((byte*)(f.Descriptions + description - 1) + WritableBlockSessionTests.ManagedOffset(f.Descriptions + description - 1, "Flags"));
        HostCheck.True((flags & D.NoControllingTerminal) == 0 && (flags & 3) == 1);
        var created = f.Open("/data/no-terminal-created.bin", 0x881c1);
        HostCheck.True(D.InspectCopied(f.State, created) is { Valid: true, Length: 0 });
        before = f.Device->Notifies;
        foreach (uint unsupported in new uint[] { 0x200, 0x400, 0x800, 3 })
            HostCheck.Equal(-95L, D.Open(f.State, "/data/DATA.BIN"u8, D.NoControllingTerminal | unsupported).Value);
        HostCheck.Equal(-13L, D.Open(f.State, "/data/../private/sentinel"u8, D.NoControllingTerminal).Value);
        HostCheck.Equal(-21L, D.Open(f.State, "/data"u8, D.NoControllingTerminal | 1).Value);
        HostCheck.Equal(before, f.Device->Notifies);
    }

    [HostTest("continuing descriptors preserve independent opens shared dup cursors positioned reads and explicit EOF")]
    private static void Offsets()
    {
        using var f = new Owner(); var a = f.Open(flags: D.CloseOnExec); var b = f.Open(); var duplicate = D.Dup(f.State, a).Descriptor;
        HostCheck.Equal(D.CloseOnExec, *(uint*)((byte*)(f.Slots + a.Number) + WritableBlockSessionTests.ManagedOffset(f.Slots + a.Number, "Flags")));
        HostCheck.Equal(0U, *(uint*)((byte*)(f.Slots + duplicate.Number) + WritableBlockSessionTests.ManagedOffset(f.Slots + duplicate.Number, "Flags")));
        HostCheck.True(f.Read(a, 13).SequenceEqual(Enumerable.Range(0, 13).Select(Value)));
        HostCheck.True(f.Read(duplicate, 7).SequenceEqual(Enumerable.Range(13, 7).Select(Value)));
        HostCheck.Equal(20L, D.Seek(f.State, a, 0, 1).Value);
        HostCheck.True(f.Read(b, 5).SequenceEqual(Enumerable.Range(0, 5).Select(Value)));
        HostCheck.Equal(5L, D.Seek(f.State, b, 0, 1).Value);
        HostCheck.True(f.Read(a, 8, 899, true).SequenceEqual(new byte[] { Value(899) }));
        HostCheck.Equal(20L, D.Seek(f.State, duplicate, 0, 1).Value);
        HostCheck.Equal(900L, D.Seek(f.State, a, 0, 2).Value); HostCheck.Equal(0, f.Read(a, 1).Length);
        HostCheck.Equal(-22L, D.Seek(f.State, a, -901, 1).Value);
        HostCheck.Equal(-22L, D.BeginRead(f.State, a, 8193).Value);
        HostCheck.Equal(0L, D.Close(f.State, a)); HostCheck.Equal(-9L, D.Seek(f.State, a, 0, 1).Value);
        HostCheck.True(D.InspectCopied(f.State, duplicate).Valid);
    }

    [HostTest("continuing writable copied inputs fragmented writes and empty create refresh only known physical lineage")]
    private static void OwnMutations()
    {
        using var f = new Owner(); var writer = f.Open(flags: 2); var reader = f.Open(); byte[] input = Enumerable.Range(0, 37).Select(i => (byte)(91 + i)).ToArray();
        byte[] expected = Enumerable.Range(0, 900).Select(Value).ToArray(); input.CopyTo(expected, 500);
        var pending = D.Pwritev2Single(f.State, writer, input, 500, 0x20); input.AsSpan().Clear();
        HostCheck.Equal(37L, f.Run(pending).Value); HostCheck.Equal(0L, D.Seek(f.State, writer, 0, 1).Value);
        HostCheck.Equal(2UL, f.Storage.State->VolumeRevision); HostCheck.True(!f.State->Quarantined && f.Read(reader, 900, 0, true).SequenceEqual(expected));
        HostCheck.True(f.Trace.Any(row => row.Type == 1 && row.Sector == f.DataLba + 2) && f.Trace.Any(row => row.Type == 1 && row.Sector == f.DataLba + 4) && f.Trace[^1].Type == 0);
        var created = f.Open("/data/a-long-empty-file.txt", 2 | D.Create | D.Exclusive); var info = D.InspectCopied(f.State, created);
        HostCheck.True(info.Valid && info.Length == 0 && f.Storage.State->VolumeRevision == 3 && !f.State->Quarantined);
        HostCheck.Equal(0, f.Read(created, 1).Length); HostCheck.True(f.Read(reader, 8, 500, true).SequenceEqual(expected.AsSpan(500, 8).ToArray()));
        uint before = f.Device->Notifies;
        HostCheck.Equal(-95L, D.BeginWrite(f.State, writer, new byte[2], 899, true).Value);
        HostCheck.Equal(-95L, D.BeginWrite(f.State, created, new byte[1]).Value);
        HostCheck.Equal(-95L, D.Pwritev2Single(f.State, writer, new byte[1], 0, 0).Value);
        HostCheck.Equal(before, f.Device->Notifies);
    }

    [HostTest("continuing file and directory sync wait for their own actual flush completion without data writes")]
    private static void Flush()
    {
        using var f = new Owner(); var file = f.Open(); var root = f.Open("/data"); ulong writes = f.Storage.Device.State->SubmittedBytes;
        foreach (var descriptor in new[] { file, root })
        {
            int start = f.Trace.Count; var request = D.BeginSync(f.State, descriptor);
            HostCheck.Equal(D.Progress.Pending, request.Phase);
            HostCheck.Equal(D.Progress.Pending, D.Pump(f.State).Phase); HostCheck.True(f.Device->Pending && f.Device->Type == 4);
            HostCheck.Equal(D.Progress.Pending, D.Poll(f.State, request.Request).Phase);
            f.CompleteHardware(); HostCheck.Equal(D.Progress.Ready, D.Pump(f.State).Phase);
            HostCheck.Equal(0L, D.Complete(f.State, request.Request).Value);
            HostCheck.True(f.Trace.Skip(start).Select(row => row.Type).SequenceEqual(new uint[] { 4 }));
        }
        HostCheck.Equal(writes, f.Storage.Device.State->SubmittedBytes);
        HostCheck.Equal(0L, f.Run(D.BeginSync(f.State, file, true)).Value);
    }

    [HostTest("continuing held read close and fd reuse revoke late copy while a duplicate keeps its original cursor")]
    private static void HeldReadClose()
    {
        using (var cancelled = new Owner())
        {
            var original = cancelled.Open(); var held = D.BeginRead(cancelled.State, original, 8); cancelled.Wait(held);
            HostCheck.Equal(D.Progress.Ready, D.Cancel(cancelled.State, held.Request).Phase);
            HostCheck.True(!cancelled.Storage.State->RequestActive);
            HostCheck.Equal(-4L, D.CopyRead(cancelled.State, held.Request, new byte[8]).Value);
            HostCheck.Equal(0L, D.Seek(cancelled.State, original, 0, 1).Value);
        }
        using var f = new Owner(); var file = f.Open(); var duplicate = D.Dup(f.State, file).Descriptor;
        var request = D.BeginRead(f.State, file, 37); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        byte[] output = Enumerable.Repeat((byte)0xa5, 64).ToArray();
        HostCheck.Equal(-14L, D.CopyRead(f.State, request.Request, output.AsSpan(0, 36)).Value);
        HostCheck.Equal(-14L, D.CopyRead(f.State, request.Request, new byte[8193]).Value);
        HostCheck.Equal(D.Progress.Ready, D.Poll(f.State, request.Request).Phase);
        HostCheck.Equal(-14L, D.CopyRead(f.State, request.Request, new Span<byte>(f.InputBytes, 64)).Value);
        HostCheck.Equal(0L, D.Close(f.State, file)); var replacement = f.Open("/data");
        HostCheck.True(replacement.Number == file.Number && replacement.Generation != file.Generation && !f.State->RequestActive);
        HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, request.Request, output).Phase);
        HostCheck.True(output.All(value => value == 0xa5)); HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 1).Value);
        HostCheck.Equal(-9L, D.Close(f.State, file));
    }

    [HostTest("continuing pending close drains original device token before releasing borrowed description")]
    private static void PendingClose()
    {
        using var f = new Owner(); var file = f.Open(); var duplicate = D.Dup(f.State, file).Descriptor;
        var request = D.BeginRead(f.State, file, 8); HostCheck.Equal(D.Progress.Pending, D.Pump(f.State).Phase);
        HostCheck.True(f.Device->Pending); HostCheck.Equal(0L, D.Close(f.State, file)); var root = f.Open("/data");
        HostCheck.True(root.Number == file.Number && root.Generation != file.Generation && f.State->RequestActive);
        f.CompleteHardware(); f.Drain(); HostCheck.True(!f.State->RequestActive);
        HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, request.Request, new byte[8]).Phase);
        HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 1).Value);
        HostCheck.True(f.Read(duplicate, 8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
    }

    [HostTest("continuing final read copy rejects a retired original block owner without output or cursor publication")]
    private static void RetiredDeviceCopy()
    {
        using var f = new Owner(); var file = f.Open(); var request = D.BeginRead(f.State, file, 37); f.Wait(request);
        byte[] output = Enumerable.Repeat((byte)0xa5, 64).ToArray();
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        var result = D.CopyRead(f.State, request.Request, output);
        HostCheck.True(result.Phase == D.Progress.Immediate && result.Value == -5 && f.State->Quarantined && output.All(v => v == 0xa5));
        HostCheck.Equal(0UL, *(ulong*)((byte*)f.Descriptions + WritableBlockSessionTests.ManagedOffset(f.Descriptions, "Position")));
        HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
    }

    [HostTest("continuing write cancellation before publication and ready wins never commit a closed cursor")]
    private static void WriteCancellation()
    {
        using (var f = new Owner())
        {
            var file = f.Open(flags: 2); var request = D.BeginWrite(f.State, file, new byte[37]);
            HostCheck.Equal(D.Progress.Pending, D.Cancel(f.State, request.Request).Phase);
            HostCheck.Equal(-4L, f.Run(request).Value); HostCheck.Equal(0UL, f.Storage.Device.State->SubmittedBytes);
        }
        using (var f = new Owner())
        {
            var file = f.Open(flags: 2); var duplicate = D.Dup(f.State, file).Descriptor;
            var request = D.BeginWrite(f.State, file, new byte[37]); D.Pump(f.State);
            HostCheck.True(f.Device->Pending && f.Device->Type == 0);
            HostCheck.Equal(0L, D.Close(f.State, file)); var root = f.Open("/data");
            HostCheck.True(root.Number == file.Number && root.Generation != file.Generation);
            f.CompleteHardware(); f.Drain(); HostCheck.True(!f.State->RequestActive);
            HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
            HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 1).Value);
            HostCheck.True(f.Read(duplicate, 8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
            HostCheck.Equal(0UL, f.Storage.Device.State->SubmittedBytes);
        }
        using (var f = new Owner())
        {
            var file = f.Open(flags: 2); var duplicate = D.Dup(f.State, file).Descriptor;
            var request = D.BeginWrite(f.State, file, Enumerable.Repeat((byte)71, 37).ToArray()); HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
            HostCheck.Equal(D.Progress.Ready, D.Cancel(f.State, request.Request).Phase); HostCheck.Equal(37L, D.Complete(f.State, request.Request).Value);
            HostCheck.Equal(37L, D.Seek(f.State, duplicate, 0, 1).Value);
            HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 0).Value);
            request = D.BeginWrite(f.State, file, Enumerable.Repeat((byte)72, 37).ToArray()); f.Wait(request);
            HostCheck.Equal(0L, D.Close(f.State, file)); HostCheck.True(!f.State->RequestActive);
            HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
            HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 1).Value);
            HostCheck.True(f.Read(duplicate, 37).All(value => value == 72));
        }
    }

    [HostTest("continuing dirty cancellation retains provider engine and journal ownership even after device reset")]
    private static void DirtyCancellation()
    {
        using var f = new Owner(); var file = f.Open(flags: 2); var request = D.BeginWrite(f.State, file, new byte[700]);
        for (int i = 0; i < 10000 && f.Storage.State->IssuedChanges == 0; i++) f.Tick();
        HostCheck.True(f.Storage.State->Dirty); HostCheck.Equal(D.Progress.RecoveryRequired, D.Cancel(f.State, request.Request).Phase);
        byte[] before = new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).ToArray();
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase); HostCheck.True(!f.State->Retired && f.State->RequestActive);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        HostCheck.True(new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).SequenceEqual(before));
    }

    [HostTest("continuing descriptor read flush and issued write failures return errors and preserve quarantine recovery ownership")]
    private static void Failures()
    {
        foreach (bool read in new[] { false, true })
        {
            using var f = new Owner(); var file = f.Open(); f.FailureType = read ? 0U : 4U;
            if (read) f.FailureSector = f.DataLba + 2;
            var begun = read ? D.BeginRead(f.State, file, 37) : D.BeginSync(f.State, file);
            var ready = f.Wait(begun); HostCheck.Equal(D.Progress.Ready, ready.Phase);
            byte[] output = Enumerable.Repeat((byte)0xa5, 37).ToArray();
            var result = read ? D.CopyRead(f.State, begun.Request, output) : D.Complete(f.State, begun.Request);
            HostCheck.True(result.Phase == D.Progress.Immediate && result.Value == -5 && f.State->Quarantined && !f.Storage.State->Dirty);
            HostCheck.True(output.All(v => v == 0xa5)); HostCheck.Equal(-5L, D.BeginSync(f.State, file).Value);
            HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
        }
        foreach (uint type in new uint[] { 1, 4 })
        {
            using var f = new Owner(); var file = f.Open(flags: 2); f.FailureType = type;
            var request = D.BeginWrite(f.State, file, new byte[37], 500, true);
            HostCheck.Equal(D.Progress.RecoveryRequired, f.Wait(request).Phase);
            HostCheck.True(f.Storage.State->Dirty && f.State->RequestActive && !f.State->Retired);
            HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
            HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
            HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        }
    }

    [HostTest("continuing descriptors reject changed physical fingerprints and external revisions rather than rebind stale lineage")]
    private static void StaleLineage()
    {
        using (var f = new Owner())
        {
            var file = f.Open(); f.Storage.Volatile[(int)(f.DataLba * 512 + 32 + 12)] ^= 8;
            var request = D.BeginRead(f.State, file, 1); f.Wait(request);
            HostCheck.Equal(-116L, D.CopyRead(f.State, request.Request, new byte[1]).Value);
            HostCheck.Equal(0UL, f.Storage.Device.State->SubmittedBytes);
        }
        using (var f = new Owner())
        {
            var file = f.Open(); C.RequestToken request = default; C.FileToken physical = default; C.Result result = default;
            HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(f.Storage.State, "DATA.BIN", &request)); f.Storage.Run();
            HostCheck.Equal(C.Status.Success, C.CopyOpen(f.Storage.State, request, &physical));
            HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Storage.State, physical, 0, new byte[] { 9 }, &request)); f.Storage.Run();
            HostCheck.Equal(C.Status.Success, C.CopyResult(f.Storage.State, request, &result));
            HostCheck.Equal(-5L, D.BeginRead(f.State, file, 1).Value); HostCheck.True(f.State->Quarantined && !D.InspectCopied(f.State, file).Valid);
            HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
        }
    }

    [HostTest("continuing descriptor reservations generation exhaustion forged tokens and retirement have finite bounds")]
    private static void BoundsAndRetirement()
    {
        using (var f = new Owner(fdCount: 4, descriptionCount: 1))
        {
            var request = D.Open(f.State, "/data/DATA.BIN"u8, 0);
            HostCheck.Equal(-24L, D.Open(f.State, "/data"u8, 0).Value);
            var file = f.Run(request).Descriptor; var wrong = file; ((ulong*)&wrong)[2]++;
            HostCheck.Equal(-9L, D.Close(f.State, wrong)); HostCheck.Equal(0L, D.Close(f.State, file));
            var root = f.Open("/data"); HostCheck.True(root.Generation != file.Generation);
            D.Close(f.State, root);
            ulong* generation = (ulong*)((byte*)(f.Slots + 3) + WritableBlockSessionTests.ManagedOffset(f.Slots + 3, "Generation")); *generation = ulong.MaxValue;
            HostCheck.Equal(-24L, D.Open(f.State, "/data"u8, 0).Value);
        }
        using (var f = new Owner())
        {
            var file = f.Open(); uint before = f.Device->Notifies;
            *(ulong*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "NextGeneration")) = ulong.MaxValue;
            HostCheck.Equal(-75L, D.BeginRead(f.State, file, 1).Value);
            HostCheck.Equal(-75L, D.Open(f.State, "/data/DATA.BIN"u8, 0).Value);
            HostCheck.Equal(before, f.Device->Notifies);
        }
        using (var f = new Owner())
        {
            var file = f.Open(); var request = D.BeginRead(f.State, file, 8); D.Pump(f.State);
            HostCheck.Equal(D.Progress.Pending, D.Detach(f.State).Phase); HostCheck.True(f.State->IsRetiring);
            f.CompleteHardware(); f.Drain(); HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
            HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, request.Request, new byte[8]).Phase);
            HostCheck.Equal(-9L, D.Open(f.State, "/data"u8, 0).Value);
            HostCheck.True(f.Storage.State->IsMounted && f.Storage.Device.State->Running);
            HostCheck.Equal(C.Status.Success, C.Stop(f.Storage.State));
            HostCheck.Equal(D.Progress.Rejected, D.Pump(f.State).Phase);
        }
    }

    [HostTest("continuing provider request progress copies and cursor commits allocate no managed per request storage")]
    private static void Allocation()
    {
        using var f = new Owner(); f.Record = false; var file = f.Open(flags: 2);
        byte[] input = new byte[37], output = new byte[37];
        f.Run(D.BeginWrite(f.State, file, input, 500, true)); var warm = D.BeginRead(f.State, file, 37, 500, true); f.Wait(warm); D.CopyRead(f.State, warm.Request, output);
        long before = GC.GetAllocatedBytesForCurrentThread(); bool valid = true;
        for (int i = 0; i < 3; i++)
        {
            var write = D.BeginWrite(f.State, file, input, 500, true); valid &= f.Run(write).Value == 37;
            var read = D.BeginRead(f.State, file, 37, 500, true); valid &= f.Wait(read).Phase == D.Progress.Ready && D.CopyRead(f.State, read.Request, output).Value == 37;
        }
        HostCheck.True(valid && GC.GetAllocatedBytesForCurrentThread() == before);
    }

    [HostTest("continuing process lock admission audits fixed POD tables aliases and copied positive owner PIDs")]
    private static void LockAdmission()
    {
        using var f = new Owner(); f.PrepareLocks(); var a = f.LockInput;
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<D.RangeLock>() && !RuntimeHelpers.IsReferenceOrContainsReferences<D.ProcessSlot>());
        a.PrimaryPid = 0; HostCheck.True(!D.EnableLocks(f.State, a)); a = f.LockInput; a.ProcessCount = 5; HostCheck.True(!D.EnableLocks(f.State, a));
        a = f.LockInput; a.LockCount = 31; HostCheck.True(!D.EnableLocks(f.State, a)); a = f.LockInput; a.ScratchCount = 64; HostCheck.True(!D.EnableLocks(f.State, a));
        a = f.LockInput; a.Scratch = a.Locks; HostCheck.True(!D.EnableLocks(f.State, a));
        a = f.LockInput; a.Processes = (D.ProcessSlot*)f.Storage.Input.Before; HostCheck.True(!D.EnableLocks(f.State, a));
        a = f.LockInput; a.Locks = (D.RangeLock*)f.Descriptions; HostCheck.True(!D.EnableLocks(f.State, a));
        ((byte*)f.Locks)[0] = 1; HostCheck.True(!D.EnableLocks(f.State, f.LockInput)); ((byte*)f.Locks)[0] = 0;
        HostCheck.True(D.EnableLocks(f.State, f.LockInput)); HostCheck.True(!D.EnableLocks(f.State, f.LockInput));
        var primary = D.PrimaryProcess(f.State); HostCheck.True(primary.Pid == 11 && primary.Generation == 1);
        HostCheck.Equal(-14L, D.Open(f.State, new ReadOnlySpan<byte>(f.Locks, 16), 0).Value);
        var file = f.Open(flags: 2); HostCheck.Equal(-14L, D.BeginWrite(f.State, file, new ReadOnlySpan<byte>(f.Processes, 8)).Value);
        Console.WriteLine("HOST_WRITABLE_LOCK_SIZE process=" + sizeof(D.ProcessSlot) + " range=" + sizeof(D.RangeLock) +
            " backing=" + (4 * sizeof(D.ProcessSlot) + 97 * sizeof(D.RangeLock)) + " state=" + sizeof(D.State) + " description=" + sizeof(D.Description));
        using var active = new Owner(); active.PrepareLocks(); var root = active.Open("/data");
        HostCheck.True(!D.EnableLocks(active.State, active.LockInput)); D.Close(active.State, root); HostCheck.True(D.EnableLocks(active.State, active.LockInput));
    }

    [HostTest("continuing advisory locks distinguish readonly GETLK from SETLK access and reject unsupported wire shapes")]
    private static void LockValidation()
    {
        using var f = new Owner(locks: true); var read = f.Open(); var write = f.Open(flags: 1);
        var query = new D.LockRequest(1, 0, 123, 9, 456); var result = D.FcntlLock(f.State, read, 5, query);
        HostCheck.True(result.Value == 0 && result.Lock.Type == 2 && result.Lock.Start == 123 && result.Lock.Length == 9 && result.Lock.Pid == 456);
        HostCheck.Equal(-9L, D.FcntlLock(f.State, read, 6, query).Value);
        HostCheck.Equal(-9L, D.FcntlLock(f.State, write, 6, new(0, 0, 123, 9)).Value);
        foreach (uint command in new uint[] { 7, 36, 37, 38, 999 }) HostCheck.Equal(-38L, D.FcntlLock(f.State, read, command, query).Value);
        foreach (var bad in new[] { new D.LockRequest(2, 0, 0, 1), new(1, 1, 0, 1), new(1, 0, -1, 1), new(1, 0, 0, -1), new(3, 0, 0, 1) })
            HostCheck.Equal(-22L, D.FcntlLock(f.State, read, 5, bad).Value);
        HostCheck.Equal(-75L, D.FcntlLock(f.State, read, 5, new(1, 0, long.MaxValue, 2)).Value);
        var writable = f.Open(flags: 2);
        HostCheck.Equal(0L, D.FcntlLock(f.State, writable, 6, new(1, 0, long.MaxValue, 0)).Value);
        HostCheck.Equal(0L, D.FcntlLock(f.State, read, 6, new(2, 0, long.MaxValue, 0)).Value);
        using var unconfigured = new Owner(); var plain = unconfigured.Open(); HostCheck.Equal(-38L, D.FcntlLock(unconfigured.State, plain, 5, query).Value);
    }

    [HostTest("continuing process locks share reads reject conflicting writes and report exact conflicting owner ranges")]
    private static void LockConflicts()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var b = f.OpenFor(other, flags: 2); const long start = 1073741826;
        HostCheck.Equal(0L, L(f, primary, a, 6, 0, start, 510).Value); HostCheck.Equal(0L, L(f, other, b, 6, 0, start, 510).Value);
        byte[] original = f.LockBytes(); HostCheck.Equal(-11L, L(f, other, b, 6, 1, start, 1).Value); HostCheck.True(f.LockBytes().SequenceEqual(original));
        var conflict = L(f, other, b, 5, 1, start, 1);
        HostCheck.True(conflict.Value == 0 && conflict.Lock.Type == 0 && conflict.Lock.Start == start && conflict.Lock.Length == 510 && conflict.Lock.Pid == 11 && conflict.Process.Generation == primary.Generation);
        HostCheck.Equal((short)2, L(f, primary, a, 5, 0, start, 510).Lock.Type);
        HostCheck.Equal(0L, L(f, other, b, 6, 2, start, 510).Value);
        HostCheck.Equal(0L, L(f, primary, a, 6, 1, start, 1).Value);
        conflict = L(f, other, b, 5, 1, start, 1); HostCheck.True(conflict.Lock.Type == 1 && conflict.Lock.Start == start && conflict.Lock.Length == 1 && conflict.Lock.Pid == 11);
        HostCheck.Equal((short)2, L(f, primary, a, 5, 1, start, 510).Lock.Type);
    }

    [HostTest("continuing own process lock replacement splits coalesces and unlocks exact finite intervals")]
    private static void LockReplacement()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var b = f.OpenFor(other);
        HostCheck.Equal(0L, L(f, primary, a, 6, 1, 100, 100).Value);
        HostCheck.Equal(0L, L(f, primary, a, 6, 0, 120, 20).Value); HostCheck.Equal(3, f.ActiveLocks());
        var middle = L(f, other, b, 5, 1, 125, 1); HostCheck.True(middle.Lock.Type == 0 && middle.Lock.Start == 120 && middle.Lock.Length == 20);
        var right = L(f, other, b, 5, 1, 150, 1); HostCheck.True(right.Lock.Type == 1 && right.Lock.Start == 140 && right.Lock.Length == 60);
        HostCheck.Equal(0L, L(f, primary, a, 6, 1, 120, 20).Value); HostCheck.Equal(1, f.ActiveLocks());
        HostCheck.Equal(0L, L(f, primary, a, 6, 2, 130, 20).Value); HostCheck.Equal(2, f.ActiveLocks());
        HostCheck.Equal((short)2, L(f, other, b, 5, 1, 140, 1).Lock.Type);
        right = L(f, other, b, 5, 1, 151, 1); HostCheck.True(right.Lock.Start == 150 && right.Lock.Length == 50);
        HostCheck.Equal(0L, L(f, primary, a, 6, 2, 0, 0).Value); HostCheck.Equal(0, f.ActiveLocks());
    }

    [HostTest("continuing lock identity joins VFAT and short aliases without conflating another physical file")]
    private static void LockAliases()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var original = f.Open("/data/a-long-empty-file.txt", 2 | D.Create | D.Exclusive);
        var alias = f.OpenFor(other, "/data/F0000001.DAT"); var unrelated = f.OpenFor(other);
        HostCheck.Equal(0L, L(f, primary, original, 6, 1, 1073741825, 1).Value);
        HostCheck.Equal(11, L(f, other, alias, 5, 1, 1073741825, 1).Lock.Pid);
        HostCheck.Equal((short)2, L(f, other, unrelated, 5, 1, 1073741825, 1).Lock.Type);
    }

    [HostTest("continuing any close on an inode releases that process locks including independent opens and dup")]
    private static void LockClose()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var independent = f.Open(); var duplicate = D.Dup(f.State, a).Descriptor; var b = f.OpenFor(other); var different = f.Open("/data/KEEP.TXT");
        L(f, primary, a, 6, 1, 10, 10); D.Close(f.State, different); HostCheck.Equal(11, L(f, other, b, 5, 1, 10, 1).Lock.Pid);
        HostCheck.Equal(0L, D.Close(f.State, independent)); HostCheck.Equal((short)2, L(f, other, b, 5, 1, 10, 1).Lock.Type);
        L(f, primary, a, 6, 1, 10, 10); HostCheck.Equal(0L, D.Close(f.State, duplicate)); HostCheck.Equal((short)2, L(f, other, b, 5, 1, 10, 1).Lock.Type);
        L(f, primary, a, 6, 1, 10, 10); HostCheck.Equal(0L, D.CloseForProcess(f.State, other, b)); b = f.OpenFor(other);
        HostCheck.Equal(11, L(f, other, b, 5, 1, 10, 1).Lock.Pid);
    }

    [HostTest("continuing process owners cannot lock close duplicate capture or exit another owner handles")]
    private static void LockOwnership()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process; var b = f.OpenFor(other, flags: 2);
        HostCheck.Equal(-9L, L(f, primary, b, 6, 1, 0, 1).Value); HostCheck.Equal(-9L, D.FcntlLock(f.State, b, 5, new(1, 0, 0, 1)).Value);
        HostCheck.Equal(-9L, D.Close(f.State, b)); HostCheck.Equal(-9L, D.CloseForProcess(f.State, primary, b));
        HostCheck.Equal(-9L, D.Dup(f.State, b).Value); HostCheck.Equal(-9L, D.Capture(f.State, b.Number).Value);
        var wrong = other; ((ulong*)&wrong)[2]++; HostCheck.Equal(-9L, D.ExitProcess(f.State, wrong).Value); HostCheck.Equal(-9L, L(f, wrong, b, 6, 1, 0, 1).Value);
        wrong = other; ((uint*)&wrong)[7]++; HostCheck.Equal(-9L, D.CloseForProcess(f.State, wrong, b));
        HostCheck.Equal(b.Generation, D.CaptureForProcess(f.State, other, b.Number).Descriptor.Generation);
        HostCheck.Equal(0L, L(f, other, b, 6, 1, 0, 1).Value); HostCheck.Equal(0L, D.CloseForProcess(f.State, other, b));
    }

    [HostTest("continuing process exit releases locks and PID generation reuse cannot revive previous handles or primary owner")]
    private static void LockExit()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var b = f.OpenFor(other); L(f, primary, a, 6, 1, 0, 0);
        HostCheck.Equal(0L, D.ExitProcess(f.State, primary).Value); HostCheck.Equal((short)2, L(f, other, b, 5, 1, 0, 1).Lock.Type);
        var replacement = D.RegisterProcess(f.State, 11); HostCheck.True(replacement.Value == 0 && replacement.Process.Index == primary.Index && replacement.Process.Generation != primary.Generation);
        HostCheck.True(D.PrimaryProcess(f.State).Owner == 0); HostCheck.Equal(-9L, D.Open(f.State, "/data"u8, 0).Value);
        var fresh = f.OpenFor(replacement.Process, flags: 2); L(f, replacement.Process, fresh, 6, 1, 0, 1);
        HostCheck.Equal(-9L, L(f, primary, fresh, 6, 2, 0, 0).Value); HostCheck.Equal(-9L, D.ExitProcess(f.State, primary).Value);
        var query = L(f, other, b, 5, 1, 0, 1); HostCheck.True(query.Lock.Pid == 11 && query.Process.Generation == replacement.Process.Generation);
    }

    [HostTest("continuing process exit drains pending or held reads before reused PID and fd can acquire late bytes")]
    private static void LockExitRead()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var a = f.Open();
            var request = D.BeginRead(f.State, a, 8); if (held) f.Wait(request); else D.Pump(f.State);
            var exited = D.ExitProcess(f.State, primary); HostCheck.True(exited.Phase is D.Progress.Immediate or D.Progress.Pending);
            var replacement = D.RegisterProcess(f.State, 11).Process; var root = f.OpenFor(replacement, "/data");
            HostCheck.True(root.Number == a.Number && root.Generation != a.Generation);
            f.CompleteHardware(); f.Drain(); byte[] output = Enumerable.Repeat((byte)0xa5, 8).ToArray();
            HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, request.Request, output).Phase); HostCheck.True(output.All(v => v == 0xa5));
        }
    }

    [HostTest("continuing fixed lock table exhaustion and split overflow leave persistent ranges byte unchanged")]
    private static void LockCapacity()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var a = f.Open(flags: 2);
        HostCheck.Equal(0L, L(f, primary, a, 6, 1, 0, 3).Value);
        for (int i = 0; i < 31; i++) HostCheck.Equal(0L, L(f, primary, a, 6, 1, 100 + i * 4, 1).Value);
        byte[] before = f.LockBytes(); HostCheck.Equal(-37L, L(f, primary, a, 6, 1, 1000, 1).Value); HostCheck.True(before.SequenceEqual(f.LockBytes()));
        HostCheck.Equal(-37L, L(f, primary, a, 6, 2, 1, 1).Value); HostCheck.True(before.SequenceEqual(f.LockBytes()));
        HostCheck.True(new ReadOnlySpan<byte>(f.Scratch, 65 * sizeof(D.RangeLock)).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(0L, L(f, primary, a, 6, 2, 0, 0).Value); HostCheck.Equal(0, f.ActiveLocks());
        HostCheck.Equal(0L, D.RegisterProcess(f.State, 22).Value); HostCheck.Equal(0L, D.RegisterProcess(f.State, 33).Value); HostCheck.Equal(0L, D.RegisterProcess(f.State, 44).Value);
        HostCheck.Equal(-23L, D.RegisterProcess(f.State, 55).Value); HostCheck.Equal(-17L, D.RegisterProcess(f.State, 22).Value);
        HostCheck.Equal(0L, D.ExitProcess(f.State, primary).Value);
        *(ulong*)((byte*)f.Processes + WritableBlockSessionTests.ManagedOffset(f.Processes, "Generation")) = ulong.MaxValue;
        HostCheck.Equal(-23L, D.RegisterProcess(f.State, 55).Value);
        HostCheck.Equal(ulong.MaxValue, *(ulong*)((byte*)f.Processes + WritableBlockSessionTests.ManagedOffset(f.Processes, "Generation")));
    }

    [HostTest("continuing own data revisions preserve process locks while another owner exit leaves pending writes alone")]
    private static void LockDataProgress()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var b = f.OpenFor(other); L(f, primary, a, 6, 1, 1073741825, 1);
        HostCheck.Equal(37L, f.Run(D.BeginWrite(f.State, a, new byte[37], 500, true)).Value);
        HostCheck.Equal(11, L(f, other, b, 5, 1, 1073741825, 1).Lock.Pid);
        var write = D.BeginWrite(f.State, a, new byte[700]);
        for (int i = 0; i < 10000 && f.Storage.State->IssuedChanges == 0; i++) f.Tick();
        HostCheck.True(f.Storage.State->Dirty); HostCheck.Equal(0L, D.ExitProcess(f.State, other).Value); HostCheck.True(!f.State->Quarantined);
        HostCheck.Equal(700L, f.Run(write).Value);
    }

    [HostTest("continuing exiting dirty writer revokes process locks but retains original request and recovery backing")]
    private static void LockDirtyExit()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var a = f.Open(flags: 2);
        HostCheck.Equal(0L, L(f, primary, a, 6, 1, 1073741825, 1).Value);
        var write = D.BeginWrite(f.State, a, new byte[700]);
        for (int i = 0; i < 10000 && f.Storage.State->IssuedChanges == 0; i++) f.Tick();
        HostCheck.True(f.Storage.State->Dirty);
        HostCheck.Equal(D.Progress.RecoveryRequired, D.ExitProcess(f.State, primary).Phase);
        HostCheck.True(f.State->RequestActive && !f.State->Retired && f.ActiveLocks() == 0 && D.PrimaryProcess(f.State).Owner == 0);
        HostCheck.Equal(-9L, D.CloseForProcess(f.State, primary, a));
        var replacement = D.RegisterProcess(f.State, 11).Process;
        HostCheck.True(replacement.Generation != primary.Generation);
        HostCheck.Equal(-5L, D.OpenForProcess(f.State, replacement, "/data"u8, 0).Value);
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Complete(f.State, write.Request).Phase);
        byte[] before = new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).ToArray();
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        HostCheck.True(new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).SequenceEqual(before));
    }

    [HostTest("continuing process lock replacement queries and unlocks allocate no managed storage or disk operations")]
    private static void LockAllocation()
    {
        using var f = new Owner(locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var a = f.Open(flags: 2); var b = f.OpenFor(other); uint notifies = f.Device->Notifies;
        L(f, primary, a, 6, 1, 100, 100); L(f, primary, a, 6, 2, 0, 0); L(f, other, b, 5, 1, 0, 0);
        long before = GC.GetAllocatedBytesForCurrentThread(); bool valid = true;
        for (int i = 0; i < 32; i++)
        {
            valid &= L(f, primary, a, 6, 1, 100, 100).Value == 0;
            valid &= L(f, primary, a, 6, 0, 120, 20).Value == 0;
            valid &= L(f, other, b, 5, 1, 125, 1).Lock.Pid == 11;
            valid &= L(f, primary, a, 6, 2, 0, 0).Value == 0;
        }
        HostCheck.True(valid && GC.GetAllocatedBytesForCurrentThread() == before && f.Device->Notifies == notifies);
    }

    [HostTest("continuing explicit copied allocation grows the mechanical file with a zero hole and independently extracted payload")]
    private static void MechanicalGrowth()
    {
        using var f = new Owner(growth: true); var file = f.Open("/data/m16-write.bin", 2 | D.Create | D.Exclusive);
        uint[] candidates = Enumerable.Range(10, 11).Select(i => (uint)i).ToArray();
        byte[] payload = Enumerable.Range(0, 1032).Select(Value).ToArray(); byte[] expected = new byte[5126]; payload.CopyTo(expected, 4094);
        int start = f.Trace.Count; var write = D.Pwritev2SingleWithGrowth(f.State, file, payload, 4094, D.RwfNoAppend, candidates);
        payload.AsSpan().Clear(); candidates.AsSpan().Fill(4);
        HostCheck.Equal(1032L, f.Run(write).Value); HostCheck.Equal(0L, D.Seek(f.State, file, 0, 1).Value);
        HostCheck.True(D.InspectCopied(f.State, file) is { Valid: true, Length: 5126 });
        HostCheck.True(f.Read(file, 5126, 0, true).SequenceEqual(expected)); f.Independent("m16-write.bin", expected);
        HostCheck.True(f.Trace.Skip(start).Any(row => row.Type == 1) && f.Trace.Skip(start).Count(row => row.Type == 4) >= 3);
        HostCheck.Equal(0L, D.Close(f.State, file)); HostCheck.Equal(0L, f.Run(D.BeginUnlink(f.State, "/data/m16-write.bin"u8)).Value);
        HostCheck.Equal(-2L, f.Run(D.Open(f.State, "/data/m16-write.bin"u8, 0)).Value);
        var root = f.Open("/data"); HostCheck.Equal(0L, f.Run(D.BeginSync(f.State, root)).Value);
    }

    [HostTest("continuing growth truncation and regrowth retain all inode handles locks and independent cursor values")]
    private static void GrowthLineage()
    {
        using var f = new Owner(locks: true, growth: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var writer = f.Open(flags: 2); var independent = f.Open(); var duplicate = D.Dup(f.State, writer).Descriptor; var remote = f.OpenFor(other);
        HostCheck.Equal(0L, L(f, primary, writer, 6, 1, 1073741825, 1).Value);
        HostCheck.Equal(800L, D.Seek(f.State, writer, 800, 0).Value); HostCheck.Equal(17L, D.Seek(f.State, independent, 17, 0).Value);
        HostCheck.Equal(0L, f.Run(D.BeginGrow(f.State, writer, 2500, new uint[] { 8, 12, 10 })).Value);
        HostCheck.True(D.InspectCopied(f.State, duplicate).Length == 2500 && D.InspectCopied(f.State, independent).Length == 2500);
        HostCheck.Equal(11, L(f, other, remote, 5, 1, 1073741825, 1).Lock.Pid);
        byte[] expected = new byte[2500]; Enumerable.Range(0, 900).Select(Value).ToArray().CopyTo(expected, 0);
        HostCheck.True(f.Read(independent, 2500, 0, true).SequenceEqual(expected)); f.Independent("DATA.BIN", expected);
        HostCheck.Equal(0L, f.Run(D.BeginTruncate(f.State, writer, 700)).Value);
        HostCheck.Equal(800L, D.Seek(f.State, duplicate, 0, 1).Value); HostCheck.Equal(17L, D.Seek(f.State, independent, 0, 1).Value);
        HostCheck.Equal(0, f.Read(duplicate, 1).Length); HostCheck.Equal(11, L(f, other, remote, 5, 1, 1073741825, 1).Lock.Pid);
        HostCheck.Equal(0L, f.Run(D.BeginGrow(f.State, writer, 900, Array.Empty<uint>())).Value);
        expected = new byte[900]; Enumerable.Range(0, 700).Select(Value).ToArray().CopyTo(expected, 0);
        HostCheck.True(f.Read(independent, 900, 0, true).SequenceEqual(expected)); f.Independent("DATA.BIN", expected);
        HostCheck.Equal(0L, f.Run(D.BeginTruncate(f.State, writer, 0)).Value);
        HostCheck.True(D.InspectCopied(f.State, independent).Length == 0); HostCheck.Equal(800L, D.Seek(f.State, writer, 0, 1).Value);
        HostCheck.Equal(11, L(f, other, remote, 5, 1, 1073741825, 1).Lock.Pid);
        HostCheck.Equal(0L, f.Run(D.BeginGrow(f.State, writer, 512, new uint[] { 8 })).Value);
        HostCheck.True(f.Read(independent, 512, 0, true).All(value => value == 0)); f.Independent("DATA.BIN", new byte[512]);
    }

    [HostTest("continuing explicit allocation validates candidate bounds aliases real FAT occupancy and exact count before writes")]
    private static void AllocationCandidates()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2); uint before = f.Device->Notifies;
        HostCheck.Equal(-22L, D.BeginGrow(f.State, file, 2500, new uint[17]).Value);
        HostCheck.Equal(-14L, D.BeginGrow(f.State, file, 2500, new ReadOnlySpan<uint>(f.InputBytes, 3)).Value);
        HostCheck.Equal(-14L, D.BeginGrow(f.State, file, 2500, new ReadOnlySpan<uint>(f.Storage.Input.FileMap, 3)).Value);
        HostCheck.Equal(-22L, D.BeginGrow(f.State, file, 900, Array.Empty<uint>()).Value);
        HostCheck.Equal(-95L, D.BeginTruncate(f.State, file, 901).Value);
        HostCheck.Equal(-22L, D.BeginWriteWithGrowth(f.State, file, new byte[1], new uint[] { 8 }, 0, true).Value);
        HostCheck.Equal(-75L, D.BeginWriteWithGrowth(f.State, file, new byte[1], Array.Empty<uint>(), uint.MaxValue, true).Value);
        HostCheck.Equal(before, f.Device->Notifies);
        foreach (uint[] candidates in new[] { new uint[] { 4, 8, 10 }, new uint[] { 8, 8, 10 }, new uint[] { 8 }, new uint[] { 0, 8, 10 } })
        {
            HostCheck.Equal(-22L, f.Run(D.BeginGrow(f.State, file, 2500, candidates)).Value);
            HostCheck.True(!f.State->Quarantined && D.InspectCopied(f.State, file).Length == 900);
        }
        HostCheck.Equal(0UL, f.Storage.Device.State->SubmittedBytes);
        int start = f.Trace.Count; HostCheck.Equal(0L, f.Run(D.BeginTruncate(f.State, file, 900)).Value);
        HostCheck.True(f.Trace.Skip(start).All(row => row.Type == 0) && f.Storage.State->VolumeRevision == 1);
    }

    [HostTest("continuing unlink refuses every open alias and closed reuse never grants stale fd authority")]
    private static void ClosedUnlink()
    {
        using var f = new Owner(growth: true, locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var file = f.Open("/data/a-long-empty-file.txt", 2 | D.Create | D.Exclusive); ulong entry = D.InspectCopied(f.State, file).EntryOffset;
        var alias = f.OpenFor(other, "/data/F0000001.DAT"); var duplicate = D.Dup(f.State, file).Descriptor;
        L(f, primary, file, 6, 1, 10, 1); ulong writes = f.Storage.Device.State->SubmittedBytes;
        HostCheck.Equal(-16L, f.Run(D.BeginUnlink(f.State, "/data/a-long-empty-file.txt"u8)).Value);
        HostCheck.Equal(0L, D.Close(f.State, file)); HostCheck.Equal(0L, D.Close(f.State, duplicate));
        HostCheck.Equal(-16L, f.Run(D.BeginUnlink(f.State, "/data/F0000001.DAT"u8)).Value);
        HostCheck.Equal(writes, f.Storage.Device.State->SubmittedBytes); HostCheck.Equal(0L, D.CloseForProcess(f.State, other, alias));
        HostCheck.Equal(0L, f.Run(D.BeginUnlink(f.State, "/data/F0000001.DAT"u8)).Value);
        HostCheck.Equal(-2L, f.Run(D.Open(f.State, "/data/a-long-empty-file.txt"u8, 0)).Value);
        var replacement = f.Open("/data/a-long-empty-file.txt", 2 | D.Create | D.Exclusive);
        HostCheck.True(D.InspectCopied(f.State, replacement).EntryOffset == entry && replacement.Generation != file.Generation);
        uint io = f.Device->Notifies; HostCheck.Equal(-9L, D.BeginSync(f.State, file).Value);
        HostCheck.Equal(-9L, D.BeginTruncate(f.State, file, 0).Value); HostCheck.Equal(-9L, D.Close(f.State, file));
        HostCheck.Equal(io, f.Device->Notifies); HostCheck.Equal(0UL, D.InspectCopied(f.State, replacement).Length);
        HostCheck.Equal(-21L, D.BeginUnlink(f.State, "/data"u8).Value);
        HostCheck.Equal(-13L, D.BeginUnlink(f.State, "/data/../private/sentinel"u8).Value);
    }

    [HostTest("continuing closed unlink revalidates physical metadata before any mutation and preserves read only entries")]
    private static void UnlinkMetadata()
    {
        using var f = new Owner(growth: true); f.Storage.Volatile[(int)(f.DataLba * 512 + 32 + 11)] |= 1;
        HostCheck.Equal(-13L, f.Run(D.BeginUnlink(f.State, "/data/DATA.BIN"u8)).Value);
        HostCheck.Equal(-2L, f.Run(D.BeginUnlink(f.State, "/data/missing.bin"u8)).Value);
        HostCheck.True(!f.State->Quarantined && f.Storage.Device.State->SubmittedBytes == 0);
    }

    [HostTest("continuing growth cancellation drains copied metadata without allocations or late fd reuse effects")]
    private static void GrowthCancel()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2); var duplicate = D.Dup(f.State, file).Descriptor;
        var request = D.BeginGrow(f.State, file, 2500, new uint[] { 8, 12, 10 }); D.Pump(f.State);
        HostCheck.True(f.Device->Pending && f.Device->Type == 0); HostCheck.Equal(0L, D.Close(f.State, file)); var root = f.Open("/data");
        HostCheck.True(root.Number == file.Number && root.Generation != file.Generation);
        f.CompleteHardware(); f.Drain(); HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
        HostCheck.True(D.InspectCopied(f.State, duplicate).Length == 900 && f.Storage.Device.State->SubmittedBytes == 0);
        HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 1).Value);
    }

    [HostTest("continuing successful engine mutation receipt updates surviving handles before closed request is discarded")]
    private static void MutationReadyClose()
    {
        foreach (bool truncate in new[] { false, true })
        {
            using var f = new Owner(growth: true, locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
            var file = f.Open(flags: 2); var survivor = f.Open(); var remote = f.OpenFor(other);
            L(f, primary, file, 6, 1, 1073741825, 1);
            // Any close on the inode intentionally releases this process's
            // locks, while the receipt must still refresh surviving handles.
            var request = truncate ? D.BeginTruncate(f.State, file, 700) : D.BeginGrow(f.State, file, 2500, new uint[] { 8, 12, 10 });
            f.HoldMutationReady(); HostCheck.True(f.Storage.State->IsReady && !f.Storage.State->Dirty);
            HostCheck.Equal(0L, D.Close(f.State, file)); var root = f.Open("/data");
            HostCheck.True(root.Number == file.Number && root.Generation != file.Generation); f.Drain();
            HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
            HostCheck.Equal(truncate ? 700UL : 2500UL, D.InspectCopied(f.State, survivor).Length);
            HostCheck.Equal((short)2, L(f, other, remote, 5, 1, 1073741825, 1).Lock.Type);
            HostCheck.Equal(0L, D.Seek(f.State, survivor, 0, 1).Value); HostCheck.True(f.Read(survivor, 8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
        }
    }

    [HostTest("continuing issued growth truncate and unlink failures retain original provider engine and recovery journals")]
    private static void MutationFailure()
    {
        foreach (int operation in new[] { 0, 1, 2 }) foreach (uint failure in new uint[] { 1, 4 })
        {
            using var f = new Owner(growth: true); var file = f.Open(flags: 2); if (operation == 2) D.Close(f.State, file);
            f.FailureType = failure;
            var request = operation == 0 ? D.BeginGrow(f.State, file, 2500, new uint[] { 8, 12, 10 }) : operation == 1 ?
                D.BeginTruncate(f.State, file, 300) : D.BeginUnlink(f.State, "/data/DATA.BIN"u8);
            HostCheck.Equal(D.Progress.RecoveryRequired, f.Wait(request).Phase);
            HostCheck.True(f.Storage.State->Dirty && f.State->RequestActive && !f.State->Retired);
            HostCheck.Equal(D.Progress.RecoveryRequired, D.Cancel(f.State, request.Request).Phase);
            byte[] journal = new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).ToArray();
            HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
            HostCheck.True(new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).SequenceEqual(journal));
        }
    }

    [HostTest("continuing growing sequential writes commit the shared cursor only after final authenticated payload completion")]
    private static void GrowingWriteCursor()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2); var duplicate = D.Dup(f.State, file).Descriptor; var independent = f.Open();
        D.Seek(f.State, file, 899, 0); D.Seek(f.State, independent, 17, 0);
        byte[] payload = Enumerable.Repeat((byte)0xc1, 600).ToArray(); var request = D.BeginWriteWithGrowth(f.State, file, payload, new uint[] { 8 });
        HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase);
        HostCheck.Equal(899UL, f.Cursor(file)); HostCheck.Equal(-11L, D.Seek(f.State, duplicate, 0, 1).Value);
        HostCheck.Equal(D.Progress.Ready, D.Cancel(f.State, request.Request).Phase); HostCheck.Equal(600L, D.Complete(f.State, request.Request).Value);
        HostCheck.Equal(1499L, D.Seek(f.State, duplicate, 0, 1).Value); HostCheck.Equal(17L, D.Seek(f.State, independent, 0, 1).Value);
        byte[] expected = new byte[1499]; Enumerable.Range(0, 899).Select(Value).ToArray().CopyTo(expected, 0); payload.CopyTo(expected, 899);
        HostCheck.True(f.Read(independent, 1499, 0, true).SequenceEqual(expected)); f.Independent("DATA.BIN", expected);
    }

    [HostTest("continuing closed growing write retains the completed zero extension and discards payload and old cursor")]
    private static void GrowingWriteClose()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2); var survivor = f.Open(); var duplicate = D.Dup(f.State, file).Descriptor;
        var request = D.BeginWriteWithGrowth(f.State, file, Enumerable.Repeat((byte)0xcc, 600).ToArray(), new uint[] { 8 }, 899, true);
        f.HoldMutationReady(); HostCheck.Equal(0L, D.Close(f.State, file)); var root = f.Open("/data");
        HostCheck.True(root.Number == file.Number && root.Generation != file.Generation); f.Drain();
        HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase); HostCheck.Equal(0L, D.Seek(f.State, duplicate, 0, 1).Value);
        byte[] expected = new byte[1499]; Enumerable.Range(0, 900).Select(Value).ToArray().CopyTo(expected, 0);
        HostCheck.True(D.InspectCopied(f.State, survivor).Length == 1499 && f.Read(survivor, 1499, 0, true).SequenceEqual(expected));
        f.Independent("DATA.BIN", expected);
    }

    [HostTest("continuing unconsumable authenticated lineage retains original request and backing after clean physical flush")]
    private static void CorruptHeldLineage()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2); ulong entry = D.InspectCopied(f.State, file).EntryOffset;
        var request = D.BeginGrow(f.State, file, 2500, new uint[] { 8, 12, 10 }); f.HoldMutationReady();
        uint selected = uint.MaxValue;
        for (uint i = 0; i < f.Storage.State->ChangeCount; i++)
            if (f.Storage.Input.Changes[i].Sector == entry / 512 && f.Storage.Input.Changes[i].Phase == Fat32Mutation.Stage.PublishDirectory)
            { HostCheck.Equal(uint.MaxValue, selected); selected = i; }
        HostCheck.True(selected != uint.MaxValue); f.Storage.Input.After[selected * 512 + entry % 512] ^= 1;
        byte[] retained = new ReadOnlySpan<byte>(f.Storage.Input.After + selected * 512, 512).ToArray();
        var result = D.Pump(f.State); f.AllowCleanRetainedFailure = true;
        HostCheck.True(result.Phase == D.Progress.RecoveryRequired && result.Failure == C.Status.InvalidState &&
            f.State->Quarantined && f.State->RequestActive && f.Storage.State->RequestActive && !f.Storage.State->Dirty);
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase); HostCheck.True(!f.State->Retired);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        HostCheck.True(new ReadOnlySpan<byte>(f.Storage.Input.After + selected * 512, 512).SequenceEqual(retained));
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Poll(f.State, request.Request).Phase);
    }

    [HostTest("continuing path mutation process exit cancels and joins before copied PID reuse without description access")]
    private static void UnlinkExit()
    {
        using var f = new Owner(growth: true, locks: true); var primary = D.PrimaryProcess(f.State);
        var request = D.BeginUnlink(f.State, "/data/DATA.BIN"u8); D.Pump(f.State); HostCheck.True(f.Device->Pending);
        HostCheck.Equal(D.Progress.Pending, D.ExitProcess(f.State, primary).Phase);
        var replacement = D.RegisterProcess(f.State, 11).Process; HostCheck.True(replacement.Generation != primary.Generation);
        f.CompleteHardware(); f.Drain(); HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0); var file = f.OpenFor(replacement);
        HostCheck.Equal(0L, D.FcntlLockForProcess(f.State, replacement, file, 5, new(1, 0, 0, 0)).Value);
    }

    [HostTest("continuing process exit and PID reuse at mutation phases revoke original authority and preserve other owner lineage")]
    private static void MutationProcessPhases()
    {
        // Metadata IN, first issued OUT and final clean engine Ready are
        // distinct ownership boundaries. The final case holds the original
        // authenticated engine result before the provider consumes it.
        foreach (int operation in new[] { 0, 1, 2 }) foreach (int phase in new[] { 0, 1, 2 })
        {
            using var f = new Owner(growth: true, locks: true); var originalOwner = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
            var original = f.Open(flags: 2); string retainedPath = operation == 2 ? "/data/KEEP.TXT" : "/data/DATA.BIN";
            var retained = f.OpenFor(other, retainedPath, 2); C.FileToken originalRetained = f.CopiedFile(retained);
            HostCheck.Equal(0L, L(f, other, retained, 6, 1, 1073741825, 1).Value);
            if (operation == 2) D.Close(f.State, original);
            var request = operation == 0 ? D.BeginWriteWithGrowth(f.State, original, new byte[600], new uint[] { 8 }, 899, true) : operation == 1 ?
                D.BeginTruncate(f.State, original, 300) : D.BeginUnlink(f.State, "/data/DATA.BIN"u8);
            if (phase == 0) { D.Pump(f.State); HostCheck.True(f.Device->Pending && f.Device->Type == 0); }
            else if (phase == 1)
            {
                for (int i = 0; i < 100000 && !f.Storage.State->Dirty; i++) f.Tick();
                HostCheck.True(f.Storage.State->Dirty);
            }
            else f.HoldMutationReady();
            var exited = D.ExitProcess(f.State, originalOwner);
            HostCheck.Equal(phase == 1 ? D.Progress.RecoveryRequired : D.Progress.Pending, exited.Phase);
            var replacement = D.RegisterProcess(f.State, 11).Process;
            HostCheck.True(replacement.Index == originalOwner.Index && replacement.Generation != originalOwner.Generation);
            HostCheck.Equal(-9L, D.CloseForProcess(f.State, originalOwner, original));
            HostCheck.Equal(-9L, D.FcntlLockForProcess(f.State, originalOwner, retained, 5, new(1, 0, 0, 1)).Value);
            if (phase == 1)
            {
                HostCheck.True(f.State->RequestActive && !f.State->Retired && f.Storage.State->Dirty);
                HostCheck.Equal(-5L, D.OpenForProcess(f.State, replacement, "/data"u8, 0).Value);
                byte[] journal = new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).ToArray();
                HostCheck.Equal(D.Progress.RecoveryRequired, D.Complete(f.State, request.Request).Phase);
                HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
                HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
                HostCheck.True(new ReadOnlySpan<byte>(f.Storage.Input.Before, 1024).SequenceEqual(journal));
                continue;
            }
            var reused = f.OpenFor(replacement, "/data"); HostCheck.True(reused.Number == original.Number && reused.Generation != original.Generation);
            f.CompleteHardware(); f.Drain(); HostCheck.True(!f.State->RequestActive && !f.State->Quarantined);
            HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
            var queryFile = f.OpenFor(replacement, retainedPath); var conflict = L(f, replacement, queryFile, 5, 1, 1073741825, 1);
            HostCheck.True(conflict.Lock.Pid == 22 && conflict.Process.Generation == other.Generation);
            C.FileToken current = f.CopiedFile(retained);
            HostCheck.True(current.Owner == originalRetained.Owner && current.EntryOffset == originalRetained.EntryOffset && f.Cursor(retained) == 0);
            if (phase == 0)
            {
                HostCheck.True(current.Length == originalRetained.Length && f.Storage.Device.State->SubmittedBytes == 0);
                f.Independent("DATA.BIN", Enumerable.Range(0, 900).Select(Value).ToArray());
            }
            else if (operation == 2)
            {
                HostCheck.True(current.Length == originalRetained.Length && current.Fingerprint0 == originalRetained.Fingerprint0);
                HostCheck.Equal(-2L, f.Run(D.OpenForProcess(f.State, replacement, "/data/DATA.BIN"u8, 0)).Value);
            }
            else
            {
                uint length = operation == 0 ? 1499U : 300U; HostCheck.Equal(length, current.Length);
                byte[] expected = new byte[length]; Enumerable.Range(0, operation == 0 ? 900 : 300).Select(Value).ToArray().CopyTo(expected, 0);
                f.Independent("DATA.BIN", expected);
            }
        }
    }

    [HostTest("continuing bounded discovery serves the full mechanical growing write from actual mirrored FAT candidates")]
    private static void DiscoveredMechanicalWrite()
    {
        using var f = new Owner(growth: true, locks: true); var primary = D.PrimaryProcess(f.State); var other = D.RegisterProcess(f.State, 22).Process;
        var file = f.Open("/data/m16-write.bin", 2 | D.Create | D.Exclusive); var retained = f.Open(); var remote = f.OpenFor(other, "/data/F0000001.DAT");
        L(f, primary, file, 6, 1, 1073741825, 1); byte[] bytes = Enumerable.Range(0, 1032).Select(Value).ToArray();
        byte[] expected = new byte[5126]; bytes.CopyTo(expected, 4094);
        var request = D.Pwritev2SingleWithFreeSearch(f.State, file, bytes, 4094, D.RwfNoAppend, new(2, 64, 4)); bytes.AsSpan().Clear();
        HostCheck.Equal(1032L, f.Run(request).Value); HostCheck.Equal(0L, D.Seek(f.State, file, 0, 1).Value);
        HostCheck.True(D.InspectCopied(f.State, file).Length == 5126 && f.CopiedFile(remote).Length == 5126);
        HostCheck.Equal(11, L(f, other, remote, 5, 1, 1073741825, 1).Lock.Pid);
        HostCheck.True(f.Read(file, 5126, 0, true).SequenceEqual(expected)); f.Independent("m16-write.bin", expected);
        HostCheck.True(f.Read(retained, 8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
    }

    [HostTest("continuing discovery budgets and physical range end are distinct retryable facts with no allocation")]
    private static void DiscoveryBudgets()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2);
        foreach (D.FreeSearch policy in new D.FreeSearch[] { new(2, 0, 0), new(2, 64, 1), new(2, 5, 2) })
        {
            var result = f.Run(D.BeginGrowWithFreeSearch(f.State, file, 2500, policy));
            HostCheck.True(result.Value == -11 && result.Failure == C.Status.SearchBudgetExceeded);
            HostCheck.True(D.InspectCopied(f.State, file).Length == 900 && !f.State->Quarantined && !f.State->RequestActive);
        }
        uint end = (uint)(f.Storage.Input.PartitionLast - f.DataLba + 3);
        var ended = f.Run(D.BeginGrowWithFreeSearch(f.State, file, 2500, new(end, 64, 4)));
        HostCheck.True(ended.Value == -11 && ended.Failure == C.Status.EndOfScan);
        HostCheck.Equal(-22L, f.Run(D.BeginGrowWithFreeSearch(f.State, file, 2500, new(1, 64, 4))).Value);
        HostCheck.Equal(-28L, f.Run(D.BeginGrowWithFreeSearch(f.State, file, 2500, new(2, 100001, 4))).Value);
        HostCheck.Equal(0UL, f.Storage.Device.State->SubmittedBytes);
        HostCheck.Equal(0L, f.Run(D.BeginGrowWithFreeSearch(f.State, file, 2500, new(2, 64, 4))).Value);
        HostCheck.True(D.InspectCopied(f.State, file).Length == 2500 && !f.State->Quarantined);
    }

    [HostTest("continuing no-growth and tail-only updates bypass candidate search without assuming cluster bytes")]
    private static void DiscoveryBypass()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2);
        HostCheck.Equal(1L, f.Run(D.BeginWriteWithFreeSearch(f.State, file, new byte[] { 9 }, default, 5, true)).Value);
        HostCheck.Equal(0L, f.Run(D.BeginGrowWithFreeSearch(f.State, file, 1000, default)).Value);
        byte[] expected = new byte[1000]; Enumerable.Range(0, 900).Select(Value).ToArray().CopyTo(expected, 0); expected[5] = 9;
        HostCheck.True(f.Read(file, 1000, 0, true).SequenceEqual(expected)); f.Independent("DATA.BIN", expected);
        HostCheck.Equal(-95L, D.Pwritev2SingleWithFreeSearch(f.State, file, new byte[1], 0, 0, new(2, 64, 4)).Value);
    }

    [HostTest("continuing candidate search crosses FAT sectors only within actual independent read bounds")]
    private static void DiscoverySectorBoundary()
    {
        using var f = new Owner(growth: true); var file = f.Open("/data/edge.bin", 2 | D.Create | D.Exclusive);
        var result = f.Run(D.BeginGrowWithFreeSearch(f.State, file, 5126, new(126, 11, 3)));
        HostCheck.True(result.Value == -11 && result.Failure == C.Status.SearchBudgetExceeded && D.InspectCopied(f.State, file).Length == 0);
        HostCheck.Equal(0L, f.Run(D.BeginGrowWithFreeSearch(f.State, file, 5126, new(126, 11, 4))).Value);
        HostCheck.True(f.CopiedFile(file).FirstCluster == 126 && f.Read(file, 5126, 0, true).All(value => value == 0));
        f.Independent("edge.bin", new byte[5126]);
    }

    [HostTest("continuing selected clusters remain unreserved and actual growth rejects newly occupied mirrored FAT entries")]
    private static void DiscoveryNotReservation()
    {
        using var f = new Owner(growth: true); var file = f.Open(flags: 2);
        var request = D.BeginGrowWithFreeSearch(f.State, file, 2500, new(2, 64, 4)); f.HoldSearchReady();
        // Cluster5 is independently free in this fixture's FAT: root2,
        // KEEP3 and DATA4->6 are allocated. Change both physical copies after
        // discovery completed; its copied fact cannot authorize allocation.
        f.Storage.Fat(5, 0x0fffffff);
        HostCheck.Equal(-22L, f.Run(request).Value);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && D.InspectCopied(f.State, file).Length == 900 && !f.State->Quarantined);
    }

    [HostTest("continuing search mirror errors reject before writes and held copy rejects a retired original block owner")]
    private static void DiscoveryErrors()
    {
        using (var f = new Owner(growth: true))
        {
            var file = f.Open(flags: 2); var request = D.BeginGrowWithFreeSearch(f.State, file, 2500, new(2, 64, 4)); f.HoldSearchStart();
            int entry = (int)((f.Storage.Input.PartitionFirst + 32 + 512) * 512 + 5 * 4);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(f.Storage.Volatile[entry..], 0x0fffffff);
            var result = f.Run(request); HostCheck.True(result.Value == -5 && result.Failure == C.Status.MirrorMismatch);
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && D.InspectCopied(f.State, file).Length == 900);
        }
        using (var f = new Owner(growth: true))
        {
            var file = f.Open(flags: 2); var request = D.BeginGrowWithFreeSearch(f.State, file, 2500, new(2, 64, 4)); f.HoldSearchReady();
            HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
            var result = f.Run(request); HostCheck.True(result.Value == -5 && f.State->Quarantined && f.CopiedFile(file).Length == 900);
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && f.Cursor(file) == 0);
            HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
        }
    }

    [HostTest("continuing pending and held candidate search close drains without late growth payload or cursor effects")]
    private static void DiscoveryCancellation()
    {
        foreach (bool held in new[] { false, true }) foreach (bool close in new[] { false, true })
        {
            using var f = new Owner(growth: true); var file = f.Open(flags: 2); var duplicate = D.Dup(f.State, file).Descriptor;
            var request = D.BeginWriteWithFreeSearch(f.State, file, new byte[600], new(2, 64, 4), 899, true);
            if (held) f.HoldSearchReady(); else { f.HoldSearchStart(); D.Pump(f.State); HostCheck.True(f.Device->Pending); }
            if (close)
            {
                HostCheck.Equal(0L, D.Close(f.State, file)); var replacement = f.Open("/data");
                HostCheck.True(replacement.Number == file.Number && replacement.Generation != file.Generation);
                f.CompleteHardware(); f.Drain(); HostCheck.Equal(D.Progress.Rejected, D.Complete(f.State, request.Request).Phase);
            }
            else
            {
                HostCheck.Equal(held ? D.Progress.Ready : D.Progress.Pending, D.Cancel(f.State, request.Request).Phase);
                f.CompleteHardware(); HostCheck.Equal(-4L, f.Run(request).Value);
            }
            HostCheck.True(!f.State->RequestActive && !f.State->Quarantined && D.InspectCopied(f.State, duplicate).Length == 900);
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && D.Seek(f.State, duplicate, 0, 1).Value == 0);
        }
    }

    [HostTest("continuing structural borrow rejects every complete partial and covering provider engine block DMA and MMIO alias without effects")]
    private static void BorrowAliases()
    {
        using var f = new Owner(locks: true); HostCheck.True(f.Storage.Device.Observation == null); CheckBorrowAliases(f);
    }

    [HostTest("continuing observed provider borrow excludes the full optional receipt with partial and covering overlaps without modifying past facts")]
    private static void BorrowObservationAliases()
    {
        using var f = new Owner(locks: true, observe: true); HostCheck.True(f.Storage.Device.Observation != null);
        byte[] observed = new ReadOnlySpan<byte>(f.Storage.Device.Observation, sizeof(B.Observation)).ToArray();
        CheckBorrowAliases(f);
        HostCheck.True(observed.SequenceEqual(new ReadOnlySpan<byte>(f.Storage.Device.Observation, sizeof(B.Observation)).ToArray()));
    }

    private static void CheckBorrowAliases(Owner f)
    {
        byte external = 0xa5; ulong address = (ulong)&external;
        HostCheck.True(Borrow(f, address, 1)); uint calls = f.Device->Calls, notifies = f.Device->Notifies;
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray();
        foreach (var span in ProtectedStorage(f))
        {
            HostCheck.True(!Borrow(f, span.Start, span.Bytes));
            HostCheck.True(!Borrow(f, span.Start, 1) && !Borrow(f, span.Start + span.Bytes - 1, 1));
            HostCheck.True(!Borrow(f, span.Start - 1, 2) && !Borrow(f, span.Start + span.Bytes - 1, 2));
            HostCheck.True(!Borrow(f, span.Start - 8, span.Bytes + 16));
        }
        HostCheck.True(external == 0xa5 && state.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()) &&
            f.Device->Calls == calls && f.Device->Notifies == notifies);
    }

    [HostTest("continuing structural borrow checks range arithmetic self epoch and original primary process generation before admission")]
    private static void BorrowIdentity()
    {
        using var f = new Owner(locks: true); byte external = 0x5a; ulong address = (ulong)&external;
        HostCheck.True(!D.CanBorrowStorage(null, f.Input.Epoch, address, 1));
        HostCheck.True(!D.CanBorrowStorage((D.State*)ulong.MaxValue, f.Input.Epoch, address, 1));
        HostCheck.True(!D.CanBorrowStorage(f.State, 0, address, 1) && !D.CanBorrowStorage(f.State, f.Input.Epoch + 1, address, 1));
        foreach (var range in new (ulong Start, ulong Bytes)[] { (0, 1), (4095, 1), (address, 0), (ulong.MaxValue - 3, 8),
            (0x0000800000000000, 1), (0x00007fffffffffff, 2), (4096, ulong.MaxValue) }) HostCheck.True(!Borrow(f, range.Start, range.Bytes));
        D.State* copy = (D.State*)NativeMemory.AllocZeroed((nuint)sizeof(D.State));
        try { *copy = *f.State; HostCheck.True(!D.CanBorrowStorage(copy, f.Input.Epoch, address, 1)); }
        finally { NativeMemory.Free(copy); }
        var primary = D.PrimaryProcess(f.State); HostCheck.Equal(0L, D.ExitProcess(f.State, primary).Value);
        HostCheck.True(!Borrow(f, address, 1)); var replacement = D.RegisterProcess(f.State, primary.Pid).Process;
        HostCheck.True(replacement.Generation != primary.Generation && !Borrow(f, address, 1) && external == 0x5a);
    }

    [HostTest("continuing structural borrow rejects invalid provider shapes internal aliases and overflowed retained extents without mutation")]
    private static void BorrowMetadata()
    {
        using var f = new Owner(locks: true); byte external = 0x77; ulong address = (ulong)&external;
        D.Admission* retained = (D.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input")); D.Admission original = *retained;
        foreach (int defect in Enumerable.Range(0, 6))
        {
            *retained = original;
            switch (defect)
            {
                case 0: retained->DescriptorCount = uint.MaxValue; break;
                case 1: retained->InputBytes--; break;
                case 2: retained->Descriptions = (D.Description*)retained->Descriptors; break;
                case 3: retained->Input = f.Storage.Input.Before; break;
                case 4: retained->Descriptors = (D.Descriptor*)0x00007ffffffffff8; break;
                case 5: retained->ExclusiveOwnerValidated = false; break;
            }
            byte[] before = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray();
            HostCheck.True(!Borrow(f, address, 1) && before.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()));
        }
        *retained = original;
        D.LockAdmission* locks = (D.LockAdmission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "LockInput")); var oldLocks = *locks;
        locks->Scratch = locks->Locks; HostCheck.True(!Borrow(f, address, 1)); *locks = oldLocks;
        locks->ProcessCount = 0; HostCheck.True(!Borrow(f, address, 1)); *locks = oldLocks;
        locks->PrimaryPid++; HostCheck.True(!Borrow(f, address, 1)); *locks = oldLocks;
        ulong* revision = (ulong*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "KnownRevision")); ulong oldRevision = *revision;
        (*revision)++; HostCheck.True(!Borrow(f, address, 1) && !f.State->Quarantined); *revision = oldRevision;
        HostCheck.True(Borrow(f, address, 1) && external == 0x77);
    }

    [HostTest("continuing structural borrow rejects stale block and DMA epochs generations and ownership with no hidden device access")]
    private static void BorrowDeviceIdentity()
    {
        using var f = new Owner(); byte external = 0x33; ulong address = (ulong)&external; uint calls = f.Device->Calls;
        B.Admission* block = (B.Admission*)((byte*)f.Storage.Device.State + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.State, "Input")); var original = *block;
        block->Epoch++; HostCheck.True(!Borrow(f, address, 1)); *block = original;
        ulong* poolEpoch = (ulong*)((byte*)f.Storage.Device.Pool + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.Pool, "Epoch")); ulong epoch = *poolEpoch;
        (*poolEpoch)++; HostCheck.True(!Borrow(f, address, 1)); *poolEpoch = epoch;
        ulong* generation = (ulong*)((byte*)f.Storage.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.Slot, "Generation")); ulong oldGeneration = *generation;
        (*generation)++; HostCheck.True(!Borrow(f, address, 1)); *generation = oldGeneration;
        var owner = (OS.Drivers.DmaBufferOwnership*)((byte*)f.Storage.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.Slot, "Owner")); var oldOwner = *owner;
        *owner = OS.Drivers.DmaBufferOwnership.Cpu; HostCheck.True(!Borrow(f, address, 1)); *owner = oldOwner;
        HostCheck.True(Borrow(f, address, 1) && f.Device->Calls == calls && external == 0x33);
    }

    [HostTest("continuing structural borrow admits clean pending and held reads but keeps dirty recovery backing unadmitted")]
    private static void BorrowProgress()
    {
        using (var f = new Owner())
        {
            byte external = 1; ulong address = (ulong)&external; var file = f.Open(); var request = D.BeginRead(f.State, file, 8);
            HostCheck.True(Borrow(f, address, 1)); D.Pump(f.State); HostCheck.True(f.Device->Pending && Borrow(f, address, 1));
            f.CompleteHardware(); f.Wait(request); HostCheck.True(Borrow(f, address, 1));
            HostCheck.Equal(8L, D.CopyRead(f.State, request.Request, new byte[8]).Value); HostCheck.True(Borrow(f, address, 1));
        }
        using (var f = new Owner())
        {
            byte external = 1; ulong address = (ulong)&external; var file = f.Open(flags: 2); var request = D.BeginWrite(f.State, file, new byte[37]);
            for (int i = 0; i < 100000 && !f.Storage.State->Dirty; i++) f.Tick();
            HostCheck.True(f.Storage.State->Dirty && !Borrow(f, address, 1));
            HostCheck.Equal(D.Progress.RecoveryRequired, D.Cancel(f.State, request.Request).Phase);
            HostCheck.True(!Borrow(f, address, 1)); HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
            HostCheck.True(!Borrow(f, address, 1)); HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        }
    }

    [HostTest("continuing structural borrow retirement fails closed while separate structural teardown remains available after device stop")]
    private static void BorrowRetirement()
    {
        using (var f = new Owner())
        {
            byte external = 1; ulong address = (ulong)&external; HostCheck.True(Borrow(f, address, 1));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); HostCheck.True(!Borrow(f, address, 1));
            HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase); HostCheck.True(!Borrow(f, address, 1));
        }
        using (var f = new Owner())
        {
            byte external = 1; ulong address = (ulong)&external; HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
            D.Admission* retained = (D.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input")); var original = *retained;
            retained->Engine = (C.State*)ulong.MaxValue; HostCheck.True(!Borrow(f, address, 1)); *retained = original;
            HostCheck.Equal(C.Status.Success, C.Stop(f.Storage.State)); HostCheck.True(!Borrow(f, address, 1));
        }
    }

    [HostTest("continuing structural borrow performs no managed allocation callback state publication or I O")]
    private static void BorrowAllocation()
    {
        using (var f = new Owner(locks: true)) CheckBorrowAllocation(f);
        using (var f = new Owner(locks: true, observe: true)) CheckBorrowAllocation(f);
    }

    private static void CheckBorrowAllocation(Owner f)
    {
        byte external = 0x91; ulong address = (ulong)&external;
        HostCheck.True(Borrow(f, address, 1)); uint calls = f.Device->Calls, notifies = f.Device->Notifies;
        byte[] before = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray(); long allocated = GC.GetAllocatedBytesForCurrentThread(); bool valid = true;
        for (int i = 0; i < 64; i++) valid &= Borrow(f, address, 1) && !Borrow(f, (ulong)f.InputBytes, 1);
        HostCheck.True(valid && GC.GetAllocatedBytesForCurrentThread() == allocated && f.Device->Calls == calls && f.Device->Notifies == notifies && external == 0x91);
        HostCheck.True(before.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()));
    }

    [HostTest("continuing observed provider rejects receipt backed input descriptor and lock tables before writing any retained metadata")]
    private static void BorrowObservationAdmission()
    {
        using var f = new Owner(attach: false, observe: true); var observed = f.Storage.Device.Observation;
        byte[] receipt = new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray();
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray();
        foreach (int shape in Enumerable.Range(0, 3))
        {
            var a = f.Input;
            if (shape == 0) a.Input = (byte*)observed;
            else if (shape == 1) a.Descriptors = (D.Descriptor*)observed;
            else a.Descriptions = (D.Description*)observed;
            HostCheck.True(!D.Start(f.State, a) && state.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()));
        }
        HostCheck.True(D.Start(f.State, f.Input)); f.Attached = true; f.PrepareLocks();
        state = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray();
        foreach (int shape in Enumerable.Range(0, 3))
        {
            var a = f.LockInput;
            if (shape == 0) a.Processes = (D.ProcessSlot*)observed;
            else if (shape == 1) a.Locks = (D.RangeLock*)observed;
            else a.Scratch = (D.RangeLock*)observed;
            HostCheck.True(!D.EnableLocks(f.State, a) && state.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()));
        }
        HostCheck.True(receipt.SequenceEqual(new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray()));
        HostCheck.True(D.EnableLocks(f.State, f.LockInput)); byte external = 1; HostCheck.True(Borrow(f, (ulong)&external, 1));
    }

    [HostTest("continuing observed pending and held reads retain valid locks and input while receipt output aliases cannot commit bytes or cursor")]
    private static void BorrowObservationProgress()
    {
        using var f = new Owner(locks: true, observe: true); byte external = 0x71; ulong address = (ulong)&external;
        var file = f.Open(); var process = D.PrimaryProcess(f.State); HostCheck.Equal(0L, L(f, process, file, 6, 0, 0, 1).Value);
        byte[] locks = f.LockBytes(), input = new ReadOnlySpan<byte>(f.InputBytes, 8192).ToArray();
        var request = D.BeginRead(f.State, file, 8); HostCheck.True(Borrow(f, address, 1));
        D.Pump(f.State); HostCheck.True(f.Device->Pending && Borrow(f, address, 1));
        f.CompleteHardware(); f.Wait(request); HostCheck.True(Borrow(f, address, 1));
        var observed = f.Storage.Device.Observation;
        HostCheck.True(observed->Request.Owner == (ulong)f.Storage.Device.State && observed->Kind == B.Operation.Read &&
            (observed->Flags & B.ObservationFlags.ReadCopied) != 0);
        byte[] receipt = new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray(); uint calls = f.Device->Calls;
        HostCheck.Equal(-14L, D.CopyRead(f.State, request.Request, new Span<byte>(observed, 8)).Value);
        HostCheck.True(f.State->RequestActive && f.Cursor(file) == 0 && Borrow(f, address, 1) &&
            receipt.SequenceEqual(new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray()));
        HostCheck.True(locks.SequenceEqual(f.LockBytes()) && input.SequenceEqual(new ReadOnlySpan<byte>(f.InputBytes, 8192).ToArray()) && f.Device->Calls == calls);
        byte[] output = new byte[8]; HostCheck.Equal(8L, D.CopyRead(f.State, request.Request, output).Value);
        HostCheck.True(output.SequenceEqual(Enumerable.Range(0, 8).Select(Value)) && f.Cursor(file) == 8 && Borrow(f, address, 1) && external == 0x71);
    }

    [HostTest("continuing observed borrow rejects invalid receipt extents and stale DMA ownership while preserving past receipt through retirement")]
    private static void BorrowObservationLifetime()
    {
        using var f = new Owner(locks: true, observe: true); byte external = 0x6c; ulong address = (ulong)&external;
        var observed = f.Storage.Device.Observation; ulong observedAddress = (ulong)observed;
        B.Admission* block = (B.Admission*)((byte*)f.Storage.Device.State + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.State, "Input"));
        byte[] receipt = new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray();
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray(); uint calls = f.Device->Calls;
        try
        {
            foreach (ulong invalid in new ulong[] { (ulong)observed + 1, 0x00007ffffffffff8 }
                .Concat(ProtectedStorage(f).Where(extent => extent.Start != observedAddress).Select(extent => extent.Start)))
            { block->Observation = (B.Observation*)invalid; HostCheck.True(!Borrow(f, address, 1), "observer aliases retained extent " + invalid.ToString("x")); }
        }
        finally { block->Observation = observed; }
        ulong* generation = (ulong*)((byte*)f.Storage.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.Slot, "Generation")); ulong original = *generation;
        try { (*generation)++; HostCheck.True(!Borrow(f, address, 1)); }
        finally { *generation = original; }
        HostCheck.True(Borrow(f, address, 1) && !f.State->Quarantined && f.Device->Calls == calls && external == 0x6c &&
            state.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()) &&
            receipt.SequenceEqual(new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray()));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); HostCheck.True(!Borrow(f, address, 1));
        HostCheck.True(receipt.SequenceEqual(new ReadOnlySpan<byte>(observed, sizeof(B.Observation)).ToArray()));
        HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase);
    }

    [HostTest("continuing retained structural audit excludes all provider engine block DMA MMIO and observation aliases without callbacks allocation or writes")]
    private static void RetainAliases()
    {
        foreach (bool observe in new[] { false, true })
        {
            using var f = new Owner(locks: true, observe: observe); byte external = 0x35; ulong address = (ulong)&external;
            HostCheck.True(Retain(f, address, 1)); byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray(); uint calls = f.Device->Calls;
            foreach (var extent in ProtectedStorage(f))
            {
                HostCheck.True(!Retain(f, extent.Start, extent.Bytes) && !Retain(f, extent.Start, 1) && !Retain(f, extent.Start + extent.Bytes - 1, 1));
                HostCheck.True(!Retain(f, extent.Start - 1, 2) && !Retain(f, extent.Start + extent.Bytes - 1, 2) && !Retain(f, extent.Start - 8, extent.Bytes + 16));
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread(); bool valid = true;
            for (int i = 0; i < 64; i++) valid &= Retain(f, address, 1) && !Retain(f, (ulong)f.InputBytes, 1);
            HostCheck.True(valid && allocated == GC.GetAllocatedBytesForCurrentThread() && f.Device->Calls == calls && external == 0x35 &&
                state.SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(D.State)).ToArray()));
        }
    }

    [HostTest("continuing retained structural audit rejects copied stale self epoch overflow and corrupted retained shapes without granting volume authority")]
    private static void RetainIdentity()
    {
        using var f = new Owner(locks: true); byte external = 0x63; ulong address = (ulong)&external;
        HostCheck.True(!RetainPredicate(null, f.Input.Epoch, address, 1) && !RetainPredicate((D.State*)ulong.MaxValue, f.Input.Epoch, address, 1));
        HostCheck.True(!RetainPredicate(f.State, 0, address, 1) && !RetainPredicate(f.State, f.Input.Epoch + 1, address, 1));
        HostCheck.True(!Retain(f, address, 0) && !Retain(f, 4095, 1) && !Retain(f, ulong.MaxValue - 3, 8));
        D.State* copy = (D.State*)NativeMemory.AllocZeroed((nuint)sizeof(D.State));
        try { *copy = *f.State; HostCheck.True(!RetainPredicate(copy, f.Input.Epoch, address, 1)); }
        finally { NativeMemory.Free(copy); }
        D.Admission* input = (D.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input")); var original = *input;
        try
        {
            foreach (int defect in Enumerable.Range(0, 5))
            {
                *input = original;
                if (defect == 0) input->DescriptorCount = uint.MaxValue;
                else if (defect == 1) input->InputBytes--;
                else if (defect == 2) input->Descriptions = (D.Description*)input->Descriptors;
                else if (defect == 3) input->Input = f.Storage.Input.Before;
                else input->ExclusiveOwnerValidated = false;
                HostCheck.True(!Retain(f, address, 1));
            }
        }
        finally { *input = original; }
        D.ProcessToken* primary = (D.ProcessToken*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "PrimaryOwner"));
        ulong* ownerEpoch = (ulong*)((byte*)primary + WritableBlockSessionTests.ManagedOffset(primary, "Epoch")); ulong processEpoch = *ownerEpoch;
        try { (*ownerEpoch)++; HostCheck.True(!Retain(f, address, 1)); }
        finally { *ownerEpoch = processEpoch; }
        ulong* revision = (ulong*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "KnownRevision")); ulong old = *revision;
        try { (*revision)++; HostCheck.True(Retain(f, address, 1) && !Borrow(f, address, 1) && !f.State->Quarantined); }
        finally { *revision = old; }
        HostCheck.True(Retain(f, address, 1) && Borrow(f, address, 1) && external == 0x63);
    }

    [HostTest("continuing retained dirty recovery ownership survives cancellation and original block stop without allowing new admission or release")]
    private static void RetainDirtyRecovery()
    {
        using var f = new Owner(locks: true, observe: true); byte external = 0x49; ulong address = (ulong)&external;
        var file = f.Open(flags: 2); var request = D.BeginWrite(f.State, file, new byte[37]);
        for (int i = 0; i < 100000 && !f.Storage.State->Dirty; i++) f.Tick();
        HostCheck.True(f.Storage.State->Dirty && Retain(f, address, 1) && !Borrow(f, address, 1));
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Cancel(f.State, request.Request).Phase);
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
        HostCheck.True(f.State->RequestActive && f.State->IsRetiring && Retain(f, address, 1) && !Borrow(f, address, 1));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.True(Retain(f, address, 1) && !Borrow(f, address, 1) && !f.State->Retired && f.Storage.State->Dirty);
        HostCheck.Equal(D.Progress.RecoveryRequired, D.Detach(f.State).Phase);
    }

    [HostTest("continuing retained orphan read ownership outlives fd close primary exit and PID reuse without reviving strict admission")]
    private static void RetainOrphan()
    {
        using var f = new Owner(locks: true, observe: true); byte external = 0x27; ulong address = (ulong)&external;
        var primary = D.PrimaryProcess(f.State); var file = f.Open(); var request = D.BeginRead(f.State, file, 8);
        D.Pump(f.State); HostCheck.True(f.Device->Pending); HostCheck.Equal(0L, D.Close(f.State, file));
        HostCheck.True(f.State->RequestActive && Retain(f, address, 1)); HostCheck.Equal(-9L, D.Capture(f.State, file.Number).Value);
        HostCheck.Equal(0L, D.ExitProcess(f.State, primary).Value); HostCheck.True(Retain(f, address, 1) && !Borrow(f, address, 1));
        f.CompleteHardware(); f.Drain(); HostCheck.True(!f.State->RequestActive && Retain(f, address, 1));
        HostCheck.Equal(D.Progress.Rejected, D.CopyRead(f.State, request.Request, new byte[8]).Phase);
        var replacement = D.RegisterProcess(f.State, primary.Pid).Process;
        HostCheck.True(replacement.Generation != primary.Generation && Retain(f, address, 1) && !Borrow(f, address, 1));
        HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase); HostCheck.True(!Retain(f, address, 1));
    }

    [HostTest("continuing retained fault backing remains auditable until provider retirement which rejects before any released engine dereference")]
    private static void RetainFaultRetirement()
    {
        using var f = new Owner(locks: true, observe: true); byte external = 0x18; ulong address = (ulong)&external;
        var file = f.Open(); f.FailureType = 0; f.FailureSector = f.DataLba + 2; var request = D.BeginRead(f.State, file, 8);
        HostCheck.Equal(D.Progress.Ready, f.Wait(request).Phase); byte[] output = Enumerable.Repeat((byte)0xa5, 8).ToArray();
        HostCheck.Equal(-5L, D.CopyRead(f.State, request.Request, output).Value);
        HostCheck.True(f.State->Quarantined && Retain(f, address, 1) && !Borrow(f, address, 1) && output.All(value => value == 0xa5));
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State)); HostCheck.True(Retain(f, address, 1));
        HostCheck.Equal(D.Progress.Retired, D.Detach(f.State).Phase); HostCheck.True(!Retain(f, address, 1));
        D.Admission* input = (D.Admission*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Input")); var original = *input;
        try { input->Engine = (C.State*)ulong.MaxValue; HostCheck.True(!Retain(f, address, 1) && !Borrow(f, address, 1)); }
        finally { *input = original; }
    }

    [HostTest("continuing retained audit refuses substituted engine and original block epochs even though the same metadata address remains mapped")]
    private static void RetainEpochs()
    {
        using var f = new Owner(observe: true); byte external = 0x71; ulong address = (ulong)&external;
        B.Admission* block = (B.Admission*)((byte*)f.Storage.Device.State + WritableBlockSessionTests.ManagedOffset(f.Storage.Device.State, "Input")); ulong blockEpoch = block->Epoch;
        try { block->Epoch++; HostCheck.True(!Retain(f, address, 1)); }
        finally { block->Epoch = blockEpoch; }
        C.Admission* engine = (C.Admission*)((byte*)f.Storage.State + WritableBlockSessionTests.ManagedOffset(f.Storage.State, "Input")); ulong engineEpoch = engine->Epoch;
        try { engine->Epoch++; HostCheck.True(!Retain(f, address, 1)); }
        finally { engine->Epoch = engineEpoch; }
        ulong* self = (ulong*)((byte*)f.State + WritableBlockSessionTests.ManagedOffset(f.State, "Self")); ulong original = *self;
        try { (*self)++; HostCheck.True(!Retain(f, address, 1)); }
        finally { *self = original; }
        HostCheck.True(Retain(f, address, 1) && Borrow(f, address, 1));
    }

    private static bool Retain(Owner f, ulong address, ulong bytes) => RetainPredicate(f.State, f.Input.Epoch, address, bytes);
    private static bool Borrow(Owner f, ulong address, ulong bytes) => D.CanBorrowStorage(f.State, f.Input.Epoch, address, bytes);
    private static (ulong Start, ulong Bytes)[] ProtectedStorage(Owner f)
    {
        var a = f.Storage.Input; var block = f.Storage.Device.Input;
        var extents = new List<(ulong Start, ulong Bytes)> { ((ulong)f.State, (ulong)sizeof(D.State)), ((ulong)f.Slots, 32UL * (ulong)sizeof(D.Descriptor)),
            ((ulong)f.Descriptions, 32UL * (ulong)sizeof(D.Description)), ((ulong)f.InputBytes, 8192),
            ((ulong)f.Processes, 4UL * (ulong)sizeof(D.ProcessSlot)), ((ulong)f.Locks, 32UL * (ulong)sizeof(D.RangeLock)),
            ((ulong)f.Scratch, 65UL * (ulong)sizeof(D.RangeLock)), ((ulong)f.Storage.State, (ulong)sizeof(C.State)),
            ((ulong)a.Changes, C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)), ((ulong)a.Before, C.JournalChanges * 512UL),
            ((ulong)a.After, C.JournalChanges * 512UL), ((ulong)a.DirectoryMap, C.MaximumClusters * 4UL), ((ulong)a.FileMap, C.MaximumClusters * 4UL),
            ((ulong)a.WalkMap, C.MaximumClusters * 4UL), ((ulong)a.Sector, 512), ((ulong)a.Fat0, 512), ((ulong)a.Fat1, 512),
            ((ulong)a.Input, C.WriteBytes), ((ulong)a.Name, C.NameWords * 2UL), ((ulong)a.LongName, C.VfatWords * 2UL),
            ((ulong)a.EncodedEntries, C.EncodedEntryBytes), ((ulong)f.Storage.Device.State, (ulong)sizeof(B.State)),
            ((ulong)f.Storage.Device.Pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)),
            ((ulong)f.Storage.Device.Slot, (ulong)sizeof(OS.Drivers.DmaBufferSlot)), ((ulong)f.Storage.Device.Data, 4096),
            (block.Common.VirtualStart, block.Common.Bytes), (block.Notify.VirtualStart, block.Notify.Bytes), (block.Device.VirtualStart, block.Device.Bytes) };
        if (block.Observation != null) extents.Add(((ulong)block.Observation, (ulong)sizeof(B.Observation)));
        return extents.ToArray();
    }

    private static D.LockResult L(Owner f, D.ProcessToken process, D.DescriptorToken file, uint command, short type, long start, long length) =>
        D.FcntlLockForProcess(f.State, process, file, command, new(type, 0, start, length));

    private sealed class Owner : IDisposable
    {
        internal readonly ContinuingFat32MutationTests.Fixture Storage;
        internal readonly D.State* State = (D.State*)NativeMemory.AllocZeroed((nuint)sizeof(D.State));
        internal readonly D.Descriptor* Slots;
        internal readonly D.Description* Descriptions;
        internal readonly byte* InputBytes = (byte*)NativeMemory.AllocZeroed(8192);
        internal D.ProcessSlot* Processes;
        internal D.RangeLock* Locks, Scratch;
        internal D.LockAdmission LockInput;
        internal D.Admission Input;
        internal bool Attached;
        internal bool Record = true;
        internal bool AllowCleanRetainedFailure;
        internal uint? FailureType;
        internal ulong? FailureSector;
        internal uint AtAttach;
        internal WritableBlockSessionTests.Hardware* Device => Storage.Device.Hardware;
        internal List<(uint Type, ulong Sector)> Trace => Storage.Trace;
        internal ulong DataLba => Storage.Input.PartitionFirst + 32 + 2 * 512;
        internal Owner(bool attach = true, uint fdCount = 32, uint descriptionCount = 32, bool locks = false, bool growth = false, bool observe = false, byte sectorsPerCluster = 1)
        {
            Storage = new(sectorsPerCluster, observe: observe);
            if (sectorsPerCluster != 1) Storage.Fat(4, 0x0fffffff);
            Slots = (D.Descriptor*)NativeMemory.AllocZeroed(fdCount, (nuint)sizeof(D.Descriptor));
            Descriptions = (D.Description*)NativeMemory.AllocZeroed(descriptionCount, (nuint)sizeof(D.Description));
            // Explicitly unknown FSInfo is supported by the pinned engine;
            // allocation still validates every supplied cluster in both FATs.
            if (growth) { Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff; Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff; }
            Storage.Mount(); AtAttach = Device->Notifies;
            Input = new() { Engine = Storage.State, Epoch = Storage.Input.Epoch, Descriptors = Slots, Descriptions = Descriptions,
                DescriptorCount = fdCount, DescriptionCount = descriptionCount, Input = InputBytes, InputBytes = 8192, ExclusiveOwnerValidated = true };
            if (attach) { HostCheck.True(D.Start(State, Input)); Attached = true; }
            if (locks) { PrepareLocks(); HostCheck.True(D.EnableLocks(State, LockInput)); }
        }
        internal void PrepareLocks()
        {
            Processes = (D.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(D.ProcessSlot));
            Locks = (D.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(D.RangeLock)); Scratch = (D.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(D.RangeLock));
            LockInput = new() { Processes = Processes, Locks = Locks, Scratch = Scratch, ProcessCount = 4, LockCount = 32, ScratchCount = 65, PrimaryPid = 11 };
        }
        internal byte[] LockBytes() => new ReadOnlySpan<byte>(Locks, 32 * sizeof(D.RangeLock)).ToArray();
        internal int ActiveLocks()
        { int result = 0, offset = WritableBlockSessionTests.ManagedOffset(Locks, "Active"); for (int i = 0; i < 32; i++) if (*((byte*)(Locks + i) + offset) != 0) result++; return result; }
        internal void CompleteHardware()
        {
            if (!Device->Pending) return;
            if (Record) Trace.Add((Device->Type, Device->Sector));
            bool failed = FailureType == Device->Type && (FailureSector is null || FailureSector == Device->Sector);
            if (failed) { FailureType = null; FailureSector = null; }
            Storage.Device.Complete(length: failed && Device->Type == 0 ? 1U : uint.MaxValue, status: failed ? (byte)1 : (byte)0);
        }
        internal D.Result Tick()
        { var result = D.Pump(State); CompleteHardware(); return result; }
        internal D.Result Wait(D.Result begun)
        {
            if (begun.Phase != D.Progress.Pending) return begun;
            for (int i = 0; i < 100000; i++)
            { var result = Tick(); if (result.Phase is D.Progress.Ready or D.Progress.RecoveryRequired or D.Progress.Rejected) return result; }
            throw new InvalidOperationException("continuing descriptor did not complete");
        }
        internal D.Result Run(D.Result begun)
        { var result = Wait(begun); return result.Phase == D.Progress.Ready ? D.Complete(State, begun.Request) : result; }
        internal D.DescriptorToken Open(string path = "/data/DATA.BIN", uint flags = 0)
        { var result = Run(D.Open(State, Encoding.ASCII.GetBytes(path), flags)); HostCheck.True(result.Phase == D.Progress.Immediate && result.Value >= 3); return result.Descriptor; }
        internal D.DescriptorToken OpenFor(D.ProcessToken process, string path = "/data/DATA.BIN", uint flags = 0)
        { var result = Run(D.OpenForProcess(State, process, Encoding.ASCII.GetBytes(path), flags)); HostCheck.True(result.Phase == D.Progress.Immediate && result.Value >= 3); return result.Descriptor; }
        internal D.StatMetadata Stat(D.DescriptorToken file)
        {
            var request = D.BeginStat(State, file); HostCheck.Equal(D.Progress.Ready, Wait(request).Phase); D.StatMetadata facts = default;
            HostCheck.Equal(0L, D.CopyStat(State, request.Request, &facts, (uint)sizeof(D.StatMetadata)).Value); return facts;
        }
        internal byte[] Read(D.DescriptorToken file, uint bytes, ulong offset = 0, bool positioned = false)
        {
            var begun = D.BeginRead(State, file, bytes, offset, positioned); HostCheck.Equal(D.Progress.Ready, Wait(begun).Phase);
            byte[] output = new byte[bytes]; var result = D.CopyRead(State, begun.Request, output);
            HostCheck.True(result.Phase == D.Progress.Immediate && result.Value >= 0); return output.AsSpan(0, (int)result.Value).ToArray();
        }
        internal void Drain()
        {
            for (int i = 0; i < 100000 && State->RequestActive; i++)
            { var result = Tick(); if (result.Phase == D.Progress.RecoveryRequired) break; }
        }
        internal void HoldMutationReady()
        {
            // Pump the original provider until it has accepted the mutation's
            // issued revision. Then finish only that retained engine request,
            // leaving its authenticated result unconsumed at the handoff.
            for (int i = 0; i < 100000 && !Storage.State->Dirty; i++) Tick();
            HostCheck.True(Storage.State->Dirty);
            for (int i = 0; i < 100000; i++)
            {
                var status = C.Pump(Storage.State); CompleteHardware();
                if (status == C.Status.Ready) return;
                HostCheck.Equal(C.Status.Pending, status);
            }
            throw new InvalidOperationException("retained engine mutation did not become ready");
        }
        internal void HoldSearchStart()
        {
            for (int i = 0; i < 100000; i++)
            {
                object pending = typeof(D.State).GetField("Work", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(*State)!;
                if (pending.GetType().GetField("Step", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(pending)!.ToString() == "FindFree")
                { HostCheck.True(Storage.State->RequestActive && !Device->Pending && !Storage.State->Dirty); return; }
                HostCheck.Equal(D.Progress.Pending, Tick().Phase);
            }
            throw new InvalidOperationException("provider did not begin bounded free search");
        }
        internal void HoldSearchReady()
        {
            HoldSearchStart();
            for (int i = 0; i < 100000; i++)
            {
                var status = C.Pump(Storage.State); CompleteHardware();
                if (status == C.Status.Ready) return;
                HostCheck.Equal(C.Status.Pending, status);
            }
            throw new InvalidOperationException("retained free search did not become ready");
        }
        internal void Independent(string name, byte[] expected)
        {
            var device = new PersistentDevice(Device); byte[] actual = new byte[expected.Length];
            var result = Fat32Parser.ReadRootFile(device, Storage.Input.PartitionFirst, name, actual);
            HostCheck.True(result.Succeeded && result.BytesRead == (ulong)expected.Length && actual.SequenceEqual(expected));
        }
        internal ulong Cursor(D.DescriptorToken file)
        {
            uint description = *(uint*)((byte*)(Slots + file.Number) + WritableBlockSessionTests.ManagedOffset(Slots + file.Number, "Description"));
            return *(ulong*)((byte*)(Descriptions + description - 1) + WritableBlockSessionTests.ManagedOffset(Descriptions + description - 1, "Position"));
        }
        internal C.FileToken CopiedFile(D.DescriptorToken file)
        {
            uint description = *(uint*)((byte*)(Slots + file.Number) + WritableBlockSessionTests.ManagedOffset(Slots + file.Number, "Description"));
            return *(C.FileToken*)((byte*)(Descriptions + description - 1) + WritableBlockSessionTests.ManagedOffset(Descriptions + description - 1, "File"));
        }
        public void Dispose()
        {
            // Fault cases end this simulated machine only after actual block
            // reset. They never count a refused dirty Detach/Stop as success.
            if (Attached && !State->Retired)
            {
                var result = D.Detach(State);
                if (result.Phase == D.Progress.Pending) { Drain(); result = D.Detach(State); }
                HostCheck.True(result.Phase == D.Progress.Retired || result.Phase == D.Progress.RecoveryRequired &&
                    (Storage.State->Dirty || AllowCleanRetainedFailure && State->Quarantined && State->RequestActive));
            }
            Storage.Dispose(); NativeMemory.Free(Scratch); NativeMemory.Free(Locks); NativeMemory.Free(Processes);
            NativeMemory.Free(InputBytes); NativeMemory.Free(Descriptions); NativeMemory.Free(Slots); NativeMemory.Free(State);
        }
    }

    private sealed class PersistentDevice(WritableBlockSessionTests.Hardware* hardware) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, hardware->MediumSectors).Info;
        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> target)
        {
            if (first >= hardware->MediumSectors || count > hardware->MediumSectors - first || target.Length != count * 512)
                return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            new ReadOnlySpan<byte>(hardware->Persistent + first * 512, checked((int)count * 512)).CopyTo(target);
            return BlockReadResult.Success(count, count * 512UL);
        }
    }
}
