using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using D = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

internal static unsafe class LinuxContinuingWritableDescriptorServicesTests
{
    private static byte Value(int i) => unchecked((byte)(i * 17 + 5));

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

    private static D.LockResult L(Owner f, D.ProcessToken process, D.DescriptorToken file, uint command, short type, long start, long length) =>
        D.FcntlLockForProcess(f.State, process, file, command, new(type, 0, start, length));

    private sealed class Owner : IDisposable
    {
        internal readonly ContinuingFat32MutationTests.Fixture Storage = new();
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
        internal uint? FailureType;
        internal ulong? FailureSector;
        internal uint AtAttach;
        internal WritableBlockSessionTests.Hardware* Device => Storage.Device.Hardware;
        internal List<(uint Type, ulong Sector)> Trace => Storage.Trace;
        internal ulong DataLba => Storage.Input.PartitionFirst + 32 + 2 * 512;
        internal Owner(bool attach = true, uint fdCount = 32, uint descriptionCount = 32, bool locks = false)
        {
            Slots = (D.Descriptor*)NativeMemory.AllocZeroed(fdCount, (nuint)sizeof(D.Descriptor));
            Descriptions = (D.Description*)NativeMemory.AllocZeroed(descriptionCount, (nuint)sizeof(D.Description));
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
        public void Dispose()
        {
            // Fault cases end this simulated machine only after actual block
            // reset. They never count a refused dirty Detach/Stop as success.
            if (Attached && !State->Retired)
            {
                var result = D.Detach(State);
                if (result.Phase == D.Progress.Pending) { Drain(); result = D.Detach(State); }
                HostCheck.True(result.Phase == D.Progress.Retired || result.Phase == D.Progress.RecoveryRequired && Storage.State->Dirty);
            }
            Storage.Dispose(); NativeMemory.Free(Scratch); NativeMemory.Free(Locks); NativeMemory.Free(Processes);
            NativeMemory.Free(InputBytes); NativeMemory.Free(Descriptions); NativeMemory.Free(Slots); NativeMemory.Free(State);
        }
    }
}
