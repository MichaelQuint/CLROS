using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Descriptors = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using ThreadModel = LinuxThreadServicesTests.Model;

internal static unsafe class LinuxBlockingPipeTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Fds = Base + 0x1180, Data = Base + 0x1000, Output = Base + 0x3400;
    internal sealed class Owner : IDisposable
    {
        internal readonly ThreadModel Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new();
        internal Owner(bool attach = true)
        {
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut; Files.Begin();
            if (attach) HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
        }
        internal LinuxSyscallResult Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0) => Thread.DescriptorCall(number, a0, a1, a2);
        internal long Value(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        { var r = Call(number, a0, a1, a2); HostCheck.Equal(LinuxSyscallAction.Return, r.Action); return r.Value; }
        internal (uint Read, uint Write) Pipe(uint flags = 0)
        { HostCheck.Equal(0L, Value(293, Fds, flags)); return (Thread.Read32(Fds), Thread.Read32(Fds + 4)); }
        internal void ScratchClean() => HostCheck.True(new ReadOnlySpan<byte>(Files.Admission.Scratch, (int)Files.Admission.ScratchBytes).IndexOfAnyExcept((byte)0) == -1);
        internal void Park(OSHostHandle thread)
        {
            ScratchClean(); HostCheck.True(!Thread.Service->CurrentImage(Key, 2, out _));
            HostCheck.True(Thread.Service->CommitPipeWait(Key, 2, Thread.Kernel.Capture(thread), out bool handoff) && handoff);
            Thread.Kernel.Confirm(); HostCheck.Equal(KernelThreadTable.State.Waiting, Thread.Kernel.Inspect(thread).State);
        }
        internal LinuxSyscallResult Complete() { var r = Thread.Service->CompletePipeWait(Key, 2); ScratchClean(); return r; }
        internal void BeginGate(ref LinuxSyscallGateState gate, byte* scratch) => HostCheck.Equal(LinuxSyscallGateStatus.Active,
            LinuxSyscallGateway.Begin(ref gate, new() { UserRoot = Thread.Kernel.Owner.Root, MaximumCalls = 64, MaximumWrite = 64,
                Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true,
                EnableStartup = true, EnableDescriptors = true, EnableThreads = true }, Thread.GatewayAccess));
        internal LinuxSyscallResult GateCall(ref LinuxSyscallGateState gate, ulong number, ulong fd, ulong address, ulong bytes)
        {
            var image = Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = number; frame.Argument0 = fd; frame.Argument1 = address; frame.Argument2 = bytes;
            image->Frame = frame;
            var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame); image->Frame = frame;
            if (result.Action == LinuxSyscallAction.Return) image->Value = result.Value;
            return result;
        }
        internal void Drain()
        {
            HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
            while (Thread.Service->ActiveThreads != 0)
            {
                var thread = Thread.Kernel.Table->Current;
                if (!thread.IsValid) thread = Thread.Kernel.Dispatch();
                if (Thread.Kernel.Inspect(thread).State == KernelThreadTable.State.Parking)
                {
                    bool ok = Thread.View(thread).PipePending
                        ? Thread.Service->CommitPipeWait(Key, 2, Thread.Kernel.Capture(thread), out bool handoff)
                        : Thread.Service->CommitFutexWait(Key, 2, Thread.Kernel.Capture(thread), out handoff);
                    HostCheck.True(ok);
                    if (handoff) Thread.Kernel.Confirm();
                    continue;
                }
                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(thread), out _)); Thread.Kernel.Confirm();
                HostCheck.True(Thread.Service->Reap(Key, 2, thread, Thread.Kernel.SupervisorRsp, out _));
            }
            HostCheck.True(Descriptors.Audit(Files.State));
            HostCheck.True(Thread.Service->Release(Key, 2)); HostCheck.True(Descriptors.End(Files.State)); ScratchClean();
            HostCheck.True(new ReadOnlySpan<byte>(Files.Admission.PipeBuffers, (int)(Files.Admission.PipeCount * Files.Admission.PipeCapacity)).IndexOfAnyExcept((byte)0) == -1);
        }
        public void Dispose() { Files.Dispose(); Thread.Dispose(); }
    }

    [HostTest("linux.blocking-pipe read parks and wakes on real bytes with scrubbed scratch and explicit descriptor ownership")]
    private static void ReadWrite()
    {
        using Owner o = new(); var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output, 32).Action); o.Park(parent);
        HostCheck.True(!Descriptors.End(o.Files.State)); HostCheck.Equal(-14L, o.Files.Call(3, pipe.Write));
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); m.Put64(Data, 0x8877665544332211);
        HostCheck.Equal(8L, o.Value(1, pipe.Write, Data, 8)); o.ScratchClean();
        HostCheck.Equal(KernelThreadTable.State.Ready, m.Kernel.Inspect(parent).State);
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        var complete = o.Complete(); HostCheck.Equal(LinuxSyscallAction.Return, complete.Action); HostCheck.Equal(8L, complete.Value);
        HostCheck.Equal(0x8877665544332211UL, m.Read64(Output)); HostCheck.True(!m.View(parent).Wait.IsValid);
        HostCheck.Equal(LinuxSyscallAction.Rejected, o.Complete().Action); o.Drain();
    }

    [HostTest("linux.blocking-pipe retained original description survives fd close and reuse")]
    private static void ReusedDescriptor()
    {
        using Owner o = new(); var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var original = o.Pipe();
        HostCheck.True(Descriptors.Capture(o.Files.State, original.Read, out var token));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, original.Read, Output, 4).Action); o.Park(parent);
        HostCheck.Equal(1U, o.Files.State->RetainedPipeOperations);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(0L, o.Value(3, original.Read));
        HostCheck.Equal(1U, o.Files.State->RetainedPipeOperations);
        HostCheck.Equal(1U, o.Files.Admission.Pipes[0].ReaderCount);
        var replacement = o.Pipe(); HostCheck.Equal(original.Read, replacement.Read); HostCheck.True(!Descriptors.IsCurrent(o.Files.State, token));
        m.Put32(Data, 0x11111111); HostCheck.Equal(4L, o.Value(1, replacement.Write, Data, 4));
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State);
        m.Put32(Data, 0xabcdef01); HostCheck.Equal(4L, o.Value(1, original.Write, Data, 4));
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(4L, o.Complete().Value);
        HostCheck.Equal(0xabcdef01U, m.Read32(Output)); HostCheck.Equal(0U, o.Files.State->RetainedPipeOperations);
        HostCheck.Equal(4L, o.Value(0, replacement.Read, Output, 4)); HostCheck.Equal(0x11111111U, m.Read32(Output));
        HostCheck.Equal(0L, o.Value(3, original.Write)); HostCheck.True(!o.Files.Admission.Pipes[0].Initialized); o.Drain();
    }

    [HostTest("linux.blocking-pipe large write retains progress through repeated capacity waits and returns the whole count")]
    private static void LargeWrite()
    {
        using Owner o = new(); var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
        byte* scratch = stackalloc byte[128]; LinuxSyscallGateState parentGate = default, childGate = default;
        o.BeginGate(ref parentGate, scratch); o.BeginGate(ref childGate, scratch + 64);
        byte[] expected = new byte[9000]; for (int i = 0; i < expected.Length; i++) expected[i] = (byte)(i * 7 + 3);
        expected.CopyTo(new Span<byte>(m.At(Data), 9000));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.GateCall(ref parentGate, 1, pipe.Write, Data, 9000).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.Blocked, parentGate.Status); HostCheck.Equal(0U, parentGate.BytesWritten);
        HostCheck.Equal(4096U, m.View(parent).PipeTransferredBytes); o.Park(parent);
        for (int chunk = 0; chunk < 2; chunk++)
        {
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(4096L, o.GateCall(ref childGate, 0, pipe.Read, Output, 4096).Value);
            HostCheck.True(expected.AsSpan(chunk * 4096, 4096).SequenceEqual(new ReadOnlySpan<byte>(m.At(Output), 4096)));
            m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            var result = LinuxSyscallGateway.CompleteBlocked(ref parentGate, o.Complete());
            if (chunk == 0) { HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action); HostCheck.Equal(8192U, m.View(parent).PipeTransferredBytes); o.Park(parent); }
            else { HostCheck.Equal(LinuxSyscallAction.Return, result.Action); HostCheck.Equal(9000L, result.Value); }
        }
        HostCheck.Equal(LinuxSyscallGateStatus.Active, parentGate.Status); HostCheck.Equal(9000U, parentGate.BytesWritten);
        HostCheck.Equal(1U, parentGate.Calls); HostCheck.Equal(2U, childGate.Calls); HostCheck.Equal(0U, childGate.BytesWritten);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref parentGate, new(LinuxSyscallAction.Return, 9000)).Action);
        HostCheck.Equal(808L, o.Value(0, pipe.Read, Output, 4096));
        HostCheck.True(expected.AsSpan(8192).SequenceEqual(new ReadOnlySpan<byte>(m.At(Output), 808))); o.Drain();
    }

    [HostTest("linux.blocking-pipe peer close completes EOF and EPIPE without stranded endpoint references")]
    private static void PeerClose()
    {
        foreach (bool reader in new[] { true, false })
        {
            using Owner o = new(); var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
            if (!reader) HostCheck.Equal(4096L, o.Value(1, pipe.Write, Data, 4096));
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(reader ? 0UL : 1UL, reader ? pipe.Read : pipe.Write, Output, 1).Action); o.Park(parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(0L, o.Value(3, reader ? pipe.Write : pipe.Read));
            m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(reader ? 0L : -32L, o.Complete().Value); HostCheck.True(!m.Service->Failed); o.Drain();
        }
    }

    [HostTest("linux.blocking-pipe atomic waits observe shared nonblock changes after a real capacity event")]
    private static void AtomicAndNonblock()
    {
        using Owner o = new(); var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
        HostCheck.Equal(4096L, o.Value(1, pipe.Write, Data, 4096));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(1, pipe.Write, Data, 4096).Action);
        HostCheck.Equal(0U, m.View(parent).PipeTransferredBytes); o.Park(parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(0L, o.Value(72, pipe.Write, 4, Descriptors.NonBlock));
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State);
        HostCheck.Equal(1L, o.Value(0, pipe.Read, Output, 1));
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(-11L, o.Complete().Value);
        HostCheck.Equal(4095U, o.Files.Admission.Pipes[0].BufferedBytes); o.Drain();
    }

    [HostTest("linux.blocking-pipe copy faults preserve queued data and large write faults return only the committed prefix")]
    private static void CopyFailures()
    {
        using (Owner o = new())
        {
            var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output, 4).Action); o.Park(parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); m.Put32(Data, 0x12345678); HostCheck.Equal(4L, o.Value(1, pipe.Write, Data, 4));
            m.Yield(child); m.Kernel.Dispatch(); m.FailWrite(Output); HostCheck.Equal(-14L, o.Complete().Value);
            HostCheck.Equal(4U, o.Files.Admission.Pipes[0].BufferedBytes); m.FailWrite(ulong.MaxValue);
            HostCheck.Equal(4L, o.Value(0, pipe.Read, Output, 4)); HostCheck.Equal(0x12345678U, m.Read32(Output)); o.Drain();
        }
        using (Owner o = new())
        {
            var m = o.Thread; var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(1, pipe.Write, Data, 8192).Action); o.Park(parent);
            m.Kernel.Dispatch(); HostCheck.Equal(4096L, o.Value(0, pipe.Read, Output, 4096));
            m.Yield(child); m.Kernel.Dispatch(); m.FailRead(Data + 4096); HostCheck.Equal(4096L, o.Complete().Value);
            HostCheck.Equal(0U, o.Files.Admission.Pipes[0].BufferedBytes); o.Drain();
        }
    }

    [HostTest("linux.blocking-pipe shutdown drains parked and pre park references before descriptor End")]
    private static void ShutdownAndAdmission()
    {
        using Owner o = new(attach: false); var m = o.Thread;
        HostCheck.True(!m.Service->EnablePipes(Key + 1, 2, o.Files.State));
        HostCheck.True(!m.Service->EnablePipes(Key, 0x202, o.Files.State));
        var copy = *m.Service; HostCheck.True(!copy.EnablePipes(Key, 2, o.Files.State));
        HostCheck.True(m.Service->EnablePipes(Key, 2, o.Files.State));
        var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var pipe = o.Pipe();
        HostCheck.True(!m.Service->EnablePipes(Key, 2, o.Files.State));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output, 4).Action); o.Park(parent);
        m.Kernel.Dispatch(); HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output + 16, 4).Action);
        var capture = m.Kernel.Capture(child); var invalid = capture; invalid.ResumeRip = m.Kernel.Code - 1;
        HostCheck.True(!m.Service->CommitPipeWait(Key, 2, invalid, out _));
        HostCheck.True(!Descriptors.End(o.Files.State)); o.Drain();
    }
}
