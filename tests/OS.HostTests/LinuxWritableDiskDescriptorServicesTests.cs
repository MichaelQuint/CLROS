using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe class LinuxWritableDiskDescriptorServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, User = LinuxThreadServicesTests.UserBase;
    private const ulong Path = User + 0x1200, Vector = User + 0x1400, Flock = User + 0x1500, Output = User + 0x2000, Payload = User + 0x3000;
    private delegate bool RetainTree(F.State* state, ulong address, ulong bytes);
    private delegate bool RetainTreeWithUserBacking(F.State* state, ulong address, ulong bytes, bool includeUserBacking);
    private static readonly RetainTree Tree = RetainMethod(typeof(F.State).MakePointerType(), typeof(ulong), typeof(ulong)).CreateDelegate<RetainTree>();
    private static readonly RetainTreeWithUserBacking TreeWithUserBacking = RetainMethod(typeof(F.State).MakePointerType(), typeof(ulong), typeof(ulong), typeof(bool)).CreateDelegate<RetainTreeWithUserBacking>();
    private static MethodInfo RetainMethod(params Type[] arguments) => typeof(F).GetMethod("WritableDiskCanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic,
        binder: null, types: arguments, modifiers: null)!;
    private static byte Value(int i) => unchecked((byte)(i * 17 + 5));

    [HostTest("writable canonical mechanical descriptor calls execute their exact copied wire values and checked read bytes over the actual shared media")]
    private static void MechanicalDescriptorCalls()
    {
        using var f = new Owner(); byte[] elf = OS.Tooling.LinuxWritableDiskProofFixture.CreateElf();
        elf.CopyTo(new Span<byte>(f.Thread.At(User), elf.Length));
        byte[] payload = OS.Tooling.LinuxWritableDiskProofFixture.ExpectedPayload();
        var calls = OS.Tooling.LinuxWritableDiskProofFixture.Calls(); int completed = 0;
        foreach (var spec in calls)
        {
            // Stdout and exit are existing thread/gateway obligations. This
            // host call driver never executes the ELF or grants source5 proof.
            if (spec.Number is 1 or 231) continue;
            var call = f.Call(spec.Number, spec.A0, spec.A1, spec.A2, spec.A3, spec.A4, spec.A5);
            var begun = f.Begin(call); long value;
            if (begun.Progress == F.WritableDiskProgress.NotHandled)
            {
                var core = f.Thread.DescriptorCall(spec.Number, spec.A0, spec.A1, spec.A2, spec.A3);
                HostCheck.Equal(LinuxSyscallAction.Return, core.Action); value = core.Value;
            }
            else if (begun.Progress == F.WritableDiskProgress.Immediate) value = begun.Value;
            else
            {
                HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
                var done = F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, spec.OutputBytes);
                HostCheck.Equal(F.WritableDiskProgress.Immediate, done.Progress); value = done.Value;
            }
            HostCheck.Equal(spec.Result, value); completed++;
            const ulong output = OS.Tooling.LinuxWritableDiskProofFixture.Output;
            if (spec.Role == "read.hole-boundary") HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), 16).SequenceEqual(new byte[8].Concat(payload.Take(8)).ToArray()));
            if (spec.Role == "read.hole") HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), 16).IndexOfAnyExcept((byte)0) < 0);
            if (spec.Role == "read.payload") HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), 8).SequenceEqual(payload.AsSpan(0, 8)));
            if (spec.Role == "read.reopened")
            {
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(output), payload.Length).SequenceEqual(payload) && f.Thread.At(output)[payload.Length] == 0xa5);
                byte[] expected = new byte[5126]; payload.CopyTo(expected, 4094); f.Independent("m16-write.bin", expected);
            }
            if (spec.Role == "lock.get-own") HostCheck.Equal(2U, *(uint*)f.Thread.At(OS.Tooling.LinuxWritableDiskProofFixture.QueryLock));
        }
        HostCheck.Equal(calls.Count - 2, completed); HostCheck.True(completed == 28 && !f.Disk->RequestActive && !f.Provider->RequestActive);
    }

    [HostTest("writable x64 single iovec grows a hole from independently verified free FAT entries and preserves copied input and independent cursors")]
    private static void PositionedGrowth()
    {
        using var f = new Owner(); uint fd = f.Open("/data/m16-write.bin", 2 | W.Create | W.Exclusive | W.CloseOnExec);
        byte[] payload = Enumerable.Range(0, 1032).Select(Value).ToArray(), expected = new byte[5126]; payload.CopyTo(expected, 4094);
        var call = f.WriteCall(fd, payload, 4094); var begun = f.Begin(call);
        new Span<byte>(f.Thread.At(Payload), payload.Length).Fill(0xee); *(ulong*)f.Thread.At(Vector) = 0;
        HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress);
        HostCheck.Equal(1032L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
        HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1))); HostCheck.Equal(5126L, f.Run(f.Call(8, fd, 0, 2)));
        HostCheck.Equal(16L, f.Run(f.Call(17, fd, Output, 16, 4086)));
        HostCheck.True(f.Bytes(16).SequenceEqual(new byte[8].Concat(payload.Take(8))));
        uint other = f.Open("/data/m16-write.bin"); HostCheck.Equal(16L, f.Run(f.Call(0, other, Output, 16)));
        HostCheck.True(f.Bytes(16).All(value => value == 0)); HostCheck.Equal(5126L, f.Run(f.Call(8, fd, 0, 1)));
        HostCheck.Equal(16L, f.Run(f.Call(8, other, 0, 1))); f.Independent("m16-write.bin", expected);
        HostCheck.True(f.Trace.Any(row => row.Type == 0) && f.Trace.Any(row => row.Type == 1) && f.Trace.Any(row => row.Type == 4) &&
            f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->DurableBytes && !f.Storage.State->Dirty);
    }

    [HostTest("writable positioned wire rejects unsupported vectors flags compat high offset overflow and failed copies before media effects")]
    private static void PositionedInput()
    {
        using var f = new Owner(); uint fd = f.Open(flags: 2); var good = f.WriteCall(fd, new byte[] { 9, 8, 7 }, 5); uint calls = f.Hardware->Calls;
        var bad = good; bad.Argument2 = 2; HostCheck.Equal(-95L, f.Run(bad));
        bad = good; bad.Argument4 = 1; HostCheck.Equal(-95L, f.Run(bad));
        bad = good; bad.Argument5 = 0; HostCheck.Equal(-95L, f.Run(bad));
        bad = good; bad.Argument3 = ulong.MaxValue; HostCheck.Equal(-22L, f.Run(bad));
        bad = good; bad.Argument3 = 0x100000001; HostCheck.Equal(-75L, f.Run(bad));
        *(ulong*)(f.Thread.At(Vector) + 8) = W.IoBytes + 1; HostCheck.Equal(-22L, f.Run(good));
        *(ulong*)(f.Thread.At(Vector) + 8) = 3; f.Thread.FailRead(Vector); HostCheck.Equal(-14L, f.Run(good));
        f.Thread.FailRead(Payload); HostCheck.Equal(-14L, f.Run(good)); f.Thread.FailRead(0);
        HostCheck.True(!f.Disk->RequestActive && !f.Provider->RequestActive && f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        HostCheck.Equal(3L, f.Run(good)); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
    }

    [HostTest("writable file and root fsync issue actual flush while closed unlink authenticates and removes the file before directory sync")]
    private static void SyncAndClosedUnlink()
    {
        using var f = new Owner(); uint fd = f.Open("/data/m16-write.bin", 2 | W.Create | W.Exclusive);
        HostCheck.Equal(3L, f.Run(f.WriteCall(fd, new byte[] { 1, 2, 3 }, 1)));
        foreach (ulong sync in new ulong[] { 74, 75 })
        { int trace = f.Trace.Count; HostCheck.Equal(0L, f.Run(f.Call(sync, fd))); HostCheck.True(f.Trace.Skip(trace).Any(row => row.Type == 4)); }
        uint root = f.Open("/data", W.DirectoryOnly | W.CloseOnExec | W.LargeFile); int before = f.Trace.Count;
        HostCheck.Equal(0L, f.Run(f.Call(74, root))); HostCheck.True(f.Trace.Skip(before).Any(row => row.Type == 4));
        before = f.Trace.Count; HostCheck.Equal(-22L, f.Run(f.Call(75, root))); HostCheck.Equal(before, f.Trace.Count);
        f.PutPath("/data/m16-write.bin"); HostCheck.Equal(-16L, f.Run(f.Call(87, Path)));
        HostCheck.Equal(0L, f.Core(3, fd)); HostCheck.Equal(0L, f.Run(f.Call(87, Path)));
        f.PutPath("/data/m16-write.bin"); HostCheck.Equal(-2L, f.Run(f.Call(2, Path, 0)));
        before = f.Trace.Count; HostCheck.Equal(0L, f.Run(f.Call(74, root))); HostCheck.True(f.Trace.Skip(before).Any(row => row.Type == 4));
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == f.Storage.Device.State->DurableBytes);
    }

    [HostTest("writable copied x64 flock query preserves padding and PID data while any close releases actual process locks")]
    private static void CopiedLocks()
    {
        using var f = new Owner(); uint first = f.Open(flags: 2), second = f.Open();
        f.PutLock(1, 1073741825, 1, 777); HostCheck.Equal(0L, f.Run(f.Call(72, first, 6, Flock)));
        HostCheck.True(f.HasLocks()); byte[] original = new ReadOnlySpan<byte>(f.Thread.At(Flock), 32).ToArray();
        HostCheck.Equal(0L, f.Run(f.Call(72, second, 5, Flock))); original[0] = 2;
        HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(Flock), 32).SequenceEqual(original));
        f.PutLock(1, 0, 1); HostCheck.Equal(-9L, f.Run(f.Call(72, second, 6, Flock)));
        f.PutLock(2, 0, 1); HostCheck.Equal(-22L, f.Run(f.Call(72, first, 5, Flock)));
        f.PutLock(1, 0, 1); *(short*)(f.Thread.At(Flock) + 2) = 1; HostCheck.Equal(-22L, f.Run(f.Call(72, first, 6, Flock)));
        f.PutLock(1, 0, 1); f.Thread.FailRead(Flock); HostCheck.Equal(-14L, f.Run(f.Call(72, first, 6, Flock))); f.Thread.FailRead(0);
        f.Thread.FailWrite(Flock); HostCheck.Equal(-14L, f.Run(f.Call(72, first, 5, Flock))); f.Thread.FailWrite(0);
        HostCheck.True(f.HasLocks()); HostCheck.Equal(0L, f.Core(3, second)); HostCheck.True(!f.HasLocks());
        HostCheck.Equal(-95L, f.Core(72, first, 7, Flock)); HostCheck.Equal(-95L, f.Core(72, first, 37, Flock));
    }

    [HostTest("writable already ready write sync and unlink results win interruption without replay or new hardware publication")]
    private static void ReadyMutations()
    {
        foreach (int operation in new[] { 0, 1, 2 })
        {
            using var f = new Owner(); uint fd = f.Open(flags: 2); LinuxSyscallFrame call;
            if (operation == 0) call = f.WriteCall(fd, new byte[] { 71, 72 }, 5);
            else if (operation == 1) call = f.Call(74, fd);
            else { HostCheck.Equal(0L, f.Core(3, fd)); f.PutPath("/data/DATA.BIN"); call = f.Call(87, Path); }
            var begun = f.Begin(call); HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(begun.Token).Progress); uint calls = f.Hardware->Calls;
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
            HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Ready && cancelled.Worker.Value == f.Worker.Value);
            HostCheck.Equal(operation == 0 ? 2L : 0L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.Equal(calls, f.Hardware->Calls);
        }
    }

    [HostTest("writable dirty cancellation retains adapter and whole storage tree without live worker copy End or successful detach authority")]
    private static void DirtyRetention()
    {
        using var f = new Owner(); uint fd = f.Open(flags: 2); var call = f.WriteCall(fd, new byte[] { 71, 72 }, 5); var begun = f.Begin(call);
        for (int i = 0; i < 100000 && !f.Storage.State->Dirty; i++) { f.Pump(); f.CompleteHardware(); }
        HostCheck.True(f.Storage.State->Dirty); f.AllowRetainedFailure = true;
        var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.RecoveryRequired && !cancelled.Worker.IsValid && f.Disk->RequestActive);
        byte value = 19; HostCheck.True(Tree(f.Files.State, (ulong)&value, 1));
        HostCheck.True(!W.CanBorrowStorage(f.Provider, f.ProviderAdmission.Epoch, (ulong)&value, 1));
        HostCheck.True(!F.DetachWritableDisk(f.Files.State) && !F.End(f.Files.State));
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
    }

    [HostTest("writable pending positioned write close cancels authentication before OUT and fd reuse cannot receive its completion or cursor")]
    private static void ClosedWrite()
    {
        using var f = new Owner(); uint fd = f.Open(flags: 2); var call = f.WriteCall(fd, new byte[] { 71, 72 }, 5); var begun = f.Begin(call);
        f.Pump(); HostCheck.True(f.Hardware->Pending && !f.Storage.State->Dirty);
        HostCheck.Equal(0L, f.Core(3, fd)); f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
        var result = f.Ready(begun.Token); HostCheck.True(result.Progress == F.WritableDiskProgress.Ready && result.Worker.Value == f.Worker.Value);
        HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes == 0 && f.Storage.State->VolumeRevision == 1);
        f.Independent("DATA.BIN", Enumerable.Range(0, 900).Select(Value).ToArray());
        HostCheck.Equal(0L, f.Core(8, fd, 0, 1)); HostCheck.Equal(8L, f.Core(0, fd, Output, 8));
    }

    [HostTest("writable failed actual flush keeps recovery request backing and revokes worker on exit without treating reset as successful sync")]
    private static void FailedFlush()
    {
        using var f = new Owner(); uint fd = f.Open(flags: 2); var call = f.WriteCall(fd, new byte[] { 71, 72 }, 5); var begun = f.Begin(call);
        bool failed = false;
        for (int i = 0; i < 100000 && !failed; i++)
        {
            f.Pump(); if (!f.Hardware->Pending) continue;
            if (f.Hardware->Type == 4) { f.Storage.Device.Complete(status: 1); failed = true; }
            else f.CompleteHardware();
        }
        HostCheck.True(failed); var result = f.Ready(begun.Token); f.AllowRetainedFailure = true;
        HostCheck.True(result.Progress == F.WritableDiskProgress.RecoveryRequired && result.Value == -5 && f.Storage.State->Dirty);
        result = F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call);
        HostCheck.True(result.Progress == F.WritableDiskProgress.RecoveryRequired && !result.Worker.IsValid);
        HostCheck.True(!F.DetachWritableDisk(f.Files.State) && f.Provider->RequestActive && f.Disk->RequestActive);
        HostCheck.True(f.Storage.Device.State->SubmittedBytes > f.Storage.Device.State->DurableBytes);
        byte outside = 7; HostCheck.True(Tree(f.Files.State, (ulong)&outside, 1));
    }

    [HostTest("writable global binding shares package fd namespace and commits only its independent common read cursor")]
    private static void NamespaceAndCursors()
    {
        using var f = new Owner(); f.PutPath("/app/fixture.data"); HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
        uint first = f.Open(), second = f.Open(); HostCheck.True(first == 4 && second == 5);
        HostCheck.Equal(16L, f.Run(f.Call(0, first, Output, 16))); HostCheck.True(f.Bytes(16).SequenceEqual(Enumerable.Range(0, 16).Select(Value)));
        HostCheck.Equal(16L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(17, first, Output, 8, 500))); HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(500, 8).Select(Value)));
        HostCheck.Equal(16L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(0, second, Output, 8))); HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
        HostCheck.Equal(8L, f.Run(f.Call(8, second, 0, 1)));
        HostCheck.Equal(-95L, f.Core(72, first, 0, 0)); HostCheck.Equal(-95L, f.Core(72, first, 1030, 0));
        HostCheck.Equal(8L, f.Run(f.Call(0, first, Output, 8))); HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(16, 8).Select(Value)));
        HostCheck.Equal(24L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(8, second, 0, 1)));
        var privateHandle = W.Capture(f.Provider, 3).Descriptor;
        HostCheck.Equal(0L, W.Seek(f.Provider, privateHandle, 0, 1).Value);
    }

    [HostTest("writable create reserves global fd and description before media effects while ordinary package and pipe allocations skip them")]
    private static void Reservations()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data/new-global-file.bin"); var call = f.Call(2, Path, 2 | W.Create | W.Exclusive | W.CloseOnExec, 0x180);
            var started = f.Begin(call); HostCheck.Equal(F.WritableDiskProgress.Pending, started.Progress);
            HostCheck.True(f.Disk->RequestActive && !F.Capture(f.Files.State, 3, out _) && f.Files.State->ActiveDescriptors == 3);
            f.PutPath("/app/fixture.data"); HostCheck.Equal(4L, f.Core(2, Path, 0x88100));
            HostCheck.Equal(0L, f.Core(293, Output, F.NonBlock));
            HostCheck.True(f.Thread.Read32(Output) == 5 && f.Thread.Read32(Output + 4) == 6);
            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(started.Token).Progress);
            HostCheck.Equal(3L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, started.Token, call).Value);
            HostCheck.True(F.Capture(f.Files.State, 3, out _) && !f.Disk->RequestActive && f.Storage.State->VolumeRevision > 1);
            HostCheck.Equal(0L, f.Run(f.Call(0, 3, Output, 1)));
        }
        foreach (bool fdFull in new[] { true, false })
        {
            using var f = new Owner(descriptors: fdFull ? 3U : 32U, descriptions: 3);
            f.PutPath("/data/must-not-create.bin"); uint calls = f.Hardware->Calls;
            HostCheck.Equal(fdFull ? -24L : -23L, f.Run(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180)));
            HostCheck.True(!f.Provider->RequestActive && !f.Disk->RequestActive && f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        }
    }

    [HostTest("writable failed final user copy or captured output ceiling cannot advance the common cursor and a fresh read retries exact bytes")]
    private static void FailedCopy()
    {
        using var f = new Owner(); uint fd = f.Open(); new Span<byte>(f.Thread.At(Output), 64).Fill(0xa5);
        f.Thread.FailWrite(Output); HostCheck.Equal(-14L, f.Run(f.Call(0, fd, Output, 37)));
        HostCheck.True(f.Bytes(37).All(value => value == 0xa5)); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        f.Thread.FailWrite(0); var call = f.Call(0, fd, Output, 37); var begun = f.Begin(call); f.Ready(begun.Token);
        uint writes = f.Thread.Writes;
        HostCheck.Equal(-14L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, 8).Value);
        HostCheck.Equal(writes, f.Thread.Writes); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        HostCheck.Equal(37L, f.Run(f.Call(0, fd, Output, 37)));
        HostCheck.True(f.Bytes(37).SequenceEqual(Enumerable.Range(0, 37).Select(Value)) && f.Thread.At(Output)[37] == 0xa5);
        HostCheck.Equal(37L, f.Run(f.Call(8, fd, 0, 1)));
    }

    [HostTest("writable closed pending and held reads preserve exact live error waiter and reject late bytes after the global fd is reused")]
    private static void CloseAndReuse()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Owner(); uint fd = f.Open(); var call = f.Call(0, fd, Output, 37); var begun = f.Begin(call);
            if (held) f.Ready(begun.Token); else { f.Pump(); HostCheck.True(f.Hardware->Pending); }
            HostCheck.True(F.Capture(f.Files.State, fd, out var old)); HostCheck.Equal(0L, f.Core(3, fd));
            f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
            HostCheck.True(!F.IsCurrent(f.Files.State, old)); new Span<byte>(f.Thread.At(Output), 37).Fill(0xa5); uint writes = f.Thread.Writes;
            var ready = f.Ready(begun.Token); HostCheck.True(ready.Worker.Value == f.Worker.Value && ready.Value == -9);
            HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.True(f.Bytes(37).All(value => value == 0xa5) && f.Thread.Writes == writes && F.Audit(f.Files.State));
            HostCheck.Equal(0L, f.Core(8, fd, 0, 1));
        }
    }

    [HostTest("writable explicit cancellation transfers a pending read to a no wake orphan while known ready open results win live interruption")]
    private static void CancelAndReady()
    {
        using var f = new Owner(); uint fd = f.Open(); var call = f.Call(0, fd, Output, 8); var begun = f.Begin(call);
        f.Pump(); HostCheck.True(f.Hardware->Pending); uint writes = f.Thread.Writes;
        var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid && f.Disk->RequestActive);
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
        f.Drain(); HostCheck.Equal(F.WritableDiskProgress.Rejected, F.PollWritableDisk(f.Files.State, f.ThreadOwner, begun.Token).Progress);
        HostCheck.True(f.Thread.Writes == writes); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        f.PutPath("/data/DATA.BIN"); call = f.Call(2, Path, 0); begun = f.Begin(call); f.Ready(begun.Token); uint calls = f.Hardware->Calls;
        var winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
        HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
        HostCheck.True(F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value >= 3 && f.Hardware->Calls == calls);
        f.PutPath("/data/known-created.bin"); call = f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180); begun = f.Begin(call); f.Ready(begun.Token);
        calls = f.Hardware->Calls; HostCheck.True(f.Storage.State->VolumeRevision > 1 && f.Storage.Device.State->SubmittedBytes > 0);
        winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
        HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
        HostCheck.True(F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value >= 3 && f.Hardware->Calls == calls);
    }

    [HostTest("writable cancelled open reservation stays unavailable until drain and abandoned completed creation is neither published nor replayed")]
    private static void CancelOpenReservation()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data/not-created.bin"); var begun = f.Begin(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180));
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
            HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid && f.Disk->RequestActive);
            f.PutPath("/app/fixture.data"); HostCheck.Equal(4L, f.Core(2, Path, 0x88100)); f.Drain();
            HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
            HostCheck.True(f.Storage.State->VolumeRevision == 1 && f.Storage.Device.State->SubmittedBytes == 0);
        }
        using (var f = new Owner())
        {
            f.PutPath("/data/abandoned-created.bin"); var begun = f.Begin(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180)); f.Ready(begun.Token);
            ulong writes = f.Storage.Device.State->SubmittedBytes, revision = f.Storage.State->VolumeRevision;
            HostCheck.True(writes > 0 && revision > 1);
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true); HostCheck.True(!cancelled.Worker.IsValid);
            f.Drain(); HostCheck.True(!F.Capture(f.Files.State, 3, out _)); HostCheck.Equal(3U, f.Open("/data/abandoned-created.bin"));
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == writes && f.Storage.State->VolumeRevision == revision);
        }
    }

    [HostTest("writable root directory and unsupported paths flags mode and legacy stat return explicit errors before unowned effects")]
    private static void FiniteNamespace()
    {
        using var f = new Owner(); uint root = f.Open("/data"); uint calls = f.Hardware->Calls;
        HostCheck.Equal(-21L, f.Run(f.Call(0, root, Output, 1))); HostCheck.Equal(-29L, f.Run(f.Call(8, root, 0, 0)));
        f.PutPath("/data/../private/sentinel"); HostCheck.Equal(-13L, f.Run(f.Call(2, Path, 0)));
        f.PutPath("/data/nested/file.bin"); HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 0)));
        f.PutPath("/data/mode.bin"); HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 2 | W.Create, 0x1ff)));
        HostCheck.Equal(-20L, f.Run(f.Call(2, Path, W.DirectoryOnly)));
        HostCheck.True(f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        HostCheck.Equal(-95L, f.Core(5, root, Output)); HostCheck.Equal(-95L, f.Core(72, root, 4, F.NonBlock));
        uint file = f.Open(flags: W.NoFollow | W.CloseOnExec); HostCheck.Equal(1L, f.Core(72, file, 1));
        HostCheck.Equal((long)(W.NoFollow | W.LargeFile), f.Core(72, file, 3));
    }

    [HostTest("writable attachment full tree query excludes adapter provider DMA and observer backing and survives original block stop without copy authority")]
    private static void TreeAliases()
    {
        using var f = new Owner(observe: true); uint fd = f.Open(flags: 2); byte external = 0x93; ulong address = (ulong)&external;
        HostCheck.True(Tree(f.Files.State, address, 1)); uint calls = f.Hardware->Calls;
        var a = f.Storage.Input; var b = f.Storage.Device.Input;
        foreach (var extent in new (ulong Start, ulong Bytes)[] {
            ((ulong)f.Disk, (ulong)sizeof(F.WritableDiskState)), ((ulong)f.Bindings, f.Files.Admission.DescriptionCount * (ulong)sizeof(F.WritableDiskBinding)),
            ((ulong)f.Provider, (ulong)sizeof(W.State)), ((ulong)f.ProviderInput, W.IoBytes),
            ((ulong)f.Slots, 32UL * (ulong)sizeof(W.Descriptor)), ((ulong)f.Descriptions, 32UL * (ulong)sizeof(W.Description)),
            ((ulong)f.Processes, 4UL * (ulong)sizeof(W.ProcessSlot)), ((ulong)f.Locks, 32UL * (ulong)sizeof(W.RangeLock)), ((ulong)f.Scratch, 65UL * (ulong)sizeof(W.RangeLock)),
            ((ulong)f.Storage.State, (ulong)sizeof(C.State)), ((ulong)a.Before, C.JournalChanges * 512UL), ((ulong)a.After, C.JournalChanges * 512UL),
            ((ulong)f.Storage.Device.State, (ulong)sizeof(B.State)), ((ulong)f.Storage.Device.Data, 4096),
            ((ulong)f.Storage.Device.Observation, (ulong)sizeof(B.Observation)), (b.Common.VirtualStart, b.Common.Bytes),
            ((ulong)f.Files.State, (ulong)sizeof(F.State)), ((ulong)f.Files.Admission.Scratch, f.Files.Admission.ScratchBytes) })
        {
            HostCheck.True(!Tree(f.Files.State, extent.Start, extent.Bytes) && !Tree(f.Files.State, extent.Start - 1, 2) &&
                !Tree(f.Files.State, extent.Start + extent.Bytes - 1, 2) && !Tree(f.Files.State, extent.Start - 8, extent.Bytes + 16));
        }
        HostCheck.True(!Tree(f.Files.State, ulong.MaxValue - 1, 8) && f.Hardware->Calls == calls && external == 0x93);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.True(Tree(f.Files.State, address, 1) && !W.CanBorrowStorage(f.Provider, f.ProviderAdmission.Epoch, address, 1));
        f.PutLock(1, 0, 1); uint writes = f.Thread.Writes;
        HostCheck.Equal(-5L, f.Run(f.Call(72, fd, 5, Flock))); HostCheck.Equal(writes, f.Thread.Writes);
        HostCheck.Equal(-5L, f.Run(f.Call(72, fd, 6, Flock))); HostCheck.True(!f.HasLocks());
        HostCheck.Equal(-5L, f.Run(f.Call(8, fd, 0, 0)));
    }

    [HostTest("writable retained tree deliberate futex pool alias preserves default exclusions and rejects crossing pool or supervisor backing")]
    private static void RetainedUserPool()
    {
        using var f = new Owner(observe: true); var memory = f.Files.Memory.Memory;
        ulong pool = (ulong)memory.Pool, bytes = memory.PoolPages * 4096UL; HostCheck.True(bytes >= 4096);
        foreach (var range in new (ulong Start, ulong Bytes)[] { (pool, 4), (pool + 128, 4), (pool + bytes - 4, 4), (pool, bytes) })
        {
            HostCheck.True(!Tree(f.Files.State, range.Start, range.Bytes) && !TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, true));
            HostCheck.True(TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, false));
        }
        foreach (var range in new (ulong Start, ulong Bytes)[] { (pool - 1, 2), (pool + bytes - 1, 2), (pool - 8, bytes + 16) })
            HostCheck.True(!TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, false));
        var a = f.Storage.Input; var b = f.Storage.Device.Input;
        foreach (var range in new (ulong Start, ulong Bytes)[] {
            ((ulong)f.Files.State, (ulong)sizeof(F.State)), ((ulong)f.Files.Admission.Scratch, f.Files.Admission.ScratchBytes),
            ((ulong)f.Files.Admission.Slots, f.Files.Admission.DescriptorCount * (ulong)sizeof(F.Slot)), ((ulong)memory.Tables, memory.TablePages * 4096UL),
            ((ulong)memory.CandidateTables, memory.TablePages * 4096UL), ((ulong)f.Disk, (ulong)sizeof(F.WritableDiskState)),
            ((ulong)f.Bindings, f.Files.Admission.DescriptionCount * (ulong)sizeof(F.WritableDiskBinding)),
            ((ulong)f.Provider, (ulong)sizeof(W.State)), ((ulong)f.ProviderInput, W.IoBytes),
            ((ulong)f.Storage.State, (ulong)sizeof(C.State)), ((ulong)a.Before, C.JournalChanges * 512UL),
            ((ulong)f.Storage.Device.State, (ulong)sizeof(B.State)), ((ulong)f.Storage.Device.Data, 4096),
            ((ulong)f.Storage.Device.Observation, (ulong)sizeof(B.Observation)), (b.Common.VirtualStart, b.Common.Bytes) })
        {
            HostCheck.True(!TreeWithUserBacking(f.Files.State, range.Start, range.Bytes, false) &&
                !TreeWithUserBacking(f.Files.State, range.Start - 1, 2, false) &&
                !TreeWithUserBacking(f.Files.State, range.Start + range.Bytes - 1, 2, false));
        }
        byte outside = 19; uint calls = f.Hardware->Calls;
        HostCheck.True(TreeWithUserBacking(f.Files.State, (ulong)&outside, 1, false) &&
            !TreeWithUserBacking(f.Files.State, ulong.MaxValue - 1, 8, false));
        HostCheck.True(f.Hardware->Calls == calls && outside == 19);
    }

    [HostTest("writable global attachment validates original epoch primary and disjoint metadata before changing any caller storage")]
    private static void Admission()
    {
        HostCheck.True(sizeof(F.WritableDiskState) <= 4096 && sizeof(F.WritableDiskBinding) * F.MaximumDescriptors <= 4096);
        Console.WriteLine($"HOST_LAYOUT writable-descriptor globalState={sizeof(F.State)} attachment={sizeof(F.WritableDiskState)} request={sizeof(F.WritableDiskRequest)} binding={sizeof(F.WritableDiskBinding)} admission={sizeof(F.WritableDiskAdmission)}");
        using var f = new Owner(attach: false, observe: true); byte[] state = new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray();
        var a = f.Admission; a.Epoch++; HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        a = f.Admission; a.Process = default; HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        a = f.Admission; a.Bindings = (F.WritableDiskBinding*)f.ProviderInput; HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, (F.WritableDiskState*)f.Storage.Input.Before, f.Admission));
        HostCheck.True(state.SequenceEqual(new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray()));
        HostCheck.True(F.AttachWritableDisk(f.Files.State, f.Disk, f.Admission)); f.Attached = true;
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, f.Admission) && F.Audit(f.Files.State));
    }

    private sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files;
        internal readonly ContinuingFat32MutationTests.Fixture Storage;
        internal readonly W.State* Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        internal readonly W.Descriptor* Slots = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        internal readonly W.Description* Descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        internal readonly byte* ProviderInput = (byte*)NativeMemory.AllocZeroed(W.IoBytes);
        internal readonly W.ProcessSlot* Processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
        internal readonly W.RangeLock* Locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
        internal readonly W.RangeLock* Scratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
        internal readonly F.WritableDiskState* Disk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
        internal readonly F.WritableDiskBinding* Bindings;
        internal readonly List<F.WritableDiskToken> Tokens = [];
        internal readonly List<(uint Type, ulong Sector)> Trace = [];
        internal readonly W.Admission ProviderAdmission;
        internal OSHostHandle Worker;
        internal bool Attached, AllowRetainedFailure;
        internal ulong ThreadOwner => (ulong)Thread.Service;
        internal WritableBlockSessionTests.Hardware* Hardware => Storage.Device.Hardware;
        internal F.WritableDiskAdmission Admission => new() { Provider = Provider, Process = W.PrimaryProcess(Provider), Bindings = Bindings,
            BindingCount = Files.Admission.DescriptionCount, Epoch = ProviderAdmission.Epoch, Search = new(2, 64, 256), FirmwareExited = true, SingleOwnerValidated = true };
        internal Owner(bool attach = true, bool observe = false, uint descriptors = 32, uint descriptions = 32)
        {
            Files = new(descriptors, descriptions); Bindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(descriptions, (nuint)sizeof(F.WritableDiskBinding));
            Storage = new(observe: observe); Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff; Storage.Mount();
            ProviderAdmission = new() { Engine = Storage.State, Epoch = Storage.Input.Epoch, Descriptors = Slots, Descriptions = Descriptions,
                DescriptorCount = 32, DescriptionCount = 32, Input = ProviderInput, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true };
            HostCheck.True(W.Start(Provider, ProviderAdmission));
            HostCheck.True(W.EnableLocks(Provider, new() { Processes = Processes, ProcessCount = 4, Locks = Locks, LockCount = 32,
                Scratch = Scratch, ScratchCount = 65, PrimaryPid = 101 }));
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut; Files.Begin();
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (attach) { HostCheck.True(F.AttachWritableDisk(Files.State, Disk, Admission)); Attached = true; }
            Worker = Thread.Start(); HostCheck.Equal(Worker.Value, Thread.Kernel.Dispatch().Value);
        }
        internal LinuxSyscallFrame Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        { var call = Thread.CurrentImage()->Frame; call.Number = number; call.Argument0 = a0; call.Argument1 = a1; call.Argument2 = a2; call.Argument3 = a3; call.Argument4 = a4; call.Argument5 = a5; return call; }
        internal void PutPath(string path) { byte[] value = Encoding.ASCII.GetBytes(path + '\0'); value.CopyTo(new Span<byte>(Thread.At(Path), value.Length)); }
        internal LinuxSyscallFrame WriteCall(uint fd, byte[] payload, ulong offset)
        {
            payload.CopyTo(new Span<byte>(Thread.At(Payload), payload.Length));
            *(ulong*)Thread.At(Vector) = Payload; *(ulong*)(Thread.At(Vector) + 8) = (ulong)payload.Length;
            return Call(328, fd, Vector, 1, offset, 0, W.RwfNoAppend);
        }
        internal void PutLock(short type, long start, long bytes, int pid = 0)
        {
            byte* wire = Thread.At(Flock); new Span<byte>(wire, 32).Fill(0xa5);
            *(short*)wire = type; *(short*)(wire + 2) = 0; *(long*)(wire + 8) = start; *(long*)(wire + 16) = bytes; *(int*)(wire + 24) = pid;
        }
        internal bool HasLocks()
        {
            FieldInfo active = typeof(W.RangeLock).GetField("Active", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (int i = 0; i < 32; i++) if ((bool)active.GetValue(Locks[i])!) return true;
            return false;
        }
        internal void Independent(string name, byte[] expected)
        {
            byte[] actual = new byte[expected.Length];
            var read = Fat32Parser.ReadRootFile(new PersistentDevice(Hardware), Storage.Input.PartitionFirst, name, actual);
            HostCheck.True(read.Succeeded && read.BytesRead == (ulong)expected.Length && actual.SequenceEqual(expected));
        }
        internal byte[] Bytes(int bytes) => new ReadOnlySpan<byte>(Thread.At(Output), bytes).ToArray();
        internal F.WritableDiskResult Begin(LinuxSyscallFrame call)
        { var begun = F.BeginWritableDisk(Files.State, ThreadOwner, Worker, call); if (begun.Token.IsActive) Tokens.Add(begun.Token); return begun; }
        internal F.WritableDiskResult Pump() => F.PumpWritableDiskOne(Files.State, ThreadOwner);
        internal void CompleteHardware()
        { if (Hardware->Pending) { Trace.Add((Hardware->Type, Hardware->Sector)); Storage.Device.Complete(); } }
        internal F.WritableDiskResult Ready(F.WritableDiskToken token)
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = Pump(); CompleteHardware();
                if (result.Progress is F.WritableDiskProgress.Ready or F.WritableDiskProgress.RecoveryRequired or F.WritableDiskProgress.Rejected) return result;
            }
            throw new InvalidOperationException("writable global request did not complete");
        }
        internal long Run(LinuxSyscallFrame call)
        {
            var begun = Begin(call); if (begun.Progress == F.WritableDiskProgress.Immediate) return begun.Value;
            HostCheck.True(begun.Progress is F.WritableDiskProgress.Pending or F.WritableDiskProgress.Ready);
            HostCheck.Equal(F.WritableDiskProgress.Ready, Ready(begun.Token).Progress);
            var result = F.CompleteWritableDisk(Files.State, ThreadOwner, Worker, begun.Token, call);
            HostCheck.Equal(F.WritableDiskProgress.Immediate, result.Progress); return result.Value;
        }
        internal uint Open(string path = "/data/DATA.BIN", uint flags = 0)
        { PutPath(path); long value = Run(Call(2, Path, flags, (flags & W.Create) != 0 ? 0x180UL : 0)); HostCheck.True(value >= 0); return (uint)value; }
        internal long Core(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        { var result = Thread.DescriptorCall(number, a0, a1, a2); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value; }
        internal void Drain()
        {
            for (int i = 0; i < 100000 && Disk->RequestActive; i++)
            { var result = Pump(); CompleteHardware(); HostCheck.True(result.Progress != F.WritableDiskProgress.RecoveryRequired); }
            HostCheck.True(!Disk->RequestActive);
        }
        public void Dispose()
        {
            try
            {
                // A failed dirty simulated machine is discarded only after its
                // block reset below. Refused adapter/provider retirement is
                // preserved as the assertion, never reported as cleanup success.
                if (AllowRetainedFailure)
                {
                    HostCheck.True(Storage.State->Dirty && Disk->RequestActive && !F.DetachWritableDisk(Files.State));
                    return;
                }
                if (Attached)
                {
                    foreach (var token in Tokens) F.CancelWritableDisk(Files.State, ThreadOwner, token, true);
                    Drain();
                    for (uint fd = 3; fd < Files.Admission.DescriptorCount; fd++) if (F.Capture(Files.State, fd, out _)) HostCheck.Equal(0L, Core(3, fd));
                    HostCheck.True(F.DetachWritableDisk(Files.State));
                }
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(Worker), out long clear) && clear == 0);
                Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, Worker, Thread.Kernel.SupervisorRsp, out _));
                HostCheck.True(Thread.Service->Release(Key, 2)); HostCheck.True(F.End(Files.State));
                HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            {
                Storage.Dispose(); Files.Dispose(); Thread.Dispose(); NativeMemory.Free(Disk); NativeMemory.Free(Bindings);
                NativeMemory.Free(Provider); NativeMemory.Free(Slots); NativeMemory.Free(Descriptions); NativeMemory.Free(ProviderInput);
                NativeMemory.Free(Processes); NativeMemory.Free(Locks); NativeMemory.Free(Scratch);
            }
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
