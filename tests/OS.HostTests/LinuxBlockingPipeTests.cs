using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Descriptors = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using ThreadModel = LinuxThreadServicesTests.Model;

internal static unsafe partial class LinuxBlockingPipeTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Fds = Base + 0x1180, Data = Base + 0x1000, Output = Base + 0x3400;
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ThreadOwner")]
    private static extern ref ulong DescriptorThreadOwner(ref Descriptors.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Disk")]
    private static extern ref Descriptors.DiskState* AttachedDisk(ref Descriptors.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Mount")]
    private static extern ref OS.Storage.ReadOnlyWebRootFileSystem.Mount* DiskMount(ref Descriptors.DiskState state);
    [HostTest("linux.descriptor thread dispatch rejects changed ownership and mappings before selecting or mutating a pipe")]
    private static void ThreadCallAdmission()
    {
        for (int kind = 0; kind < 3; kind++)
            for (int damage = 0; damage < 2; damage++)
            {
                using Owner o = new();
                var m = o.Thread;
                var parent = m.Start();
                HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
                var pipe = o.Pipe(kind == 0 ? 0U : Descriptors.NonBlock);
                uint descriptors = o.Files.State->ActiveDescriptors;
                m.Put32(Data, 0x63);
                // Marshal.OffsetOf describes a different bool layout in Admission;
                // authenticate the actual managed field instead of hitting Disk.
                ref ulong changed = ref (damage == 0 ? ref DescriptorThreadOwner(ref *o.Files.State) : ref o.Files.Memory.Memory.Tables[1]);
                ulong original = changed;
                try
                {
                    changed ^= 2;
                    var result = kind == 2 ? o.Call(72, pipe.Write, 0, 3) : o.Call(1, pipe.Write, Data, 1);
                    HostCheck.Equal(LinuxSyscallAction.Rejected, result.Action);
                    HostCheck.Equal(descriptors, o.Files.State->ActiveDescriptors);
                    HostCheck.Equal(0U, o.Files.State->RetainedPipeOperations);
                    HostCheck.Equal(0U, o.Files.Admission.Pipes[0].BufferedBytes);
                    o.ScratchClean();
                }
                finally
                {
                    changed = original;
                }

                o.Drain();
            }
    }

    [HostTest("linux.descriptor malformed disk and mount pointers reject before classification dereference or pipe effects")]
    private static void MalformedDiskTreeAdmission()
    {
        using Owner o = new();
        var m = o.Thread;
        var parent = m.Start();
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        var pipe = o.Pipe();
        m.Put32(Data, 0x63);
        // Capture while the owner is valid. Model.Call would first query
        // CurrentImage, which must itself reject after the tree is poisoned.
        var frame = m.CurrentImage()->Frame;
        frame.Number = 1;
        frame.Argument0 = pipe.Write;
        frame.Argument1 = Data;
        frame.Argument2 = 1;
        Descriptors.DiskState candidate = default;
        OS.Storage.ReadOnlyWebRootFileSystem.Mount mount = default;
        var original = AttachedDisk(ref *o.Files.State);
        foreach (bool poisonMount in new[]
        {
            false,
            true
        }

        )
            foreach (ulong address in new[]
            {
                2UL,
                8UL,
                poisonMount ? (ulong)&mount + 1 : (ulong)&candidate + 1,
                0x0000800000000000UL,
                ulong.MaxValue
            }

            )
            {
                try
                {
                    DiskMount(ref candidate) = poisonMount ? (OS.Storage.ReadOnlyWebRootFileSystem.Mount*)address : &mount;
                    AttachedDisk(ref *o.Files.State) = poisonMount ? &candidate : (Descriptors.DiskState*)address;
                    byte[] before = new ReadOnlySpan<byte>(o.Files.State, sizeof(Descriptors.State)).ToArray();
                    var kernel = m.Kernel.Snapshot();
                    uint reads = m.Reads, copies = m.Writes;
                    HostCheck.Equal(LinuxSyscallAction.Rejected, m.Service->DispatchDescriptor(Key, 2, frame).Action);
                    HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(o.Files.State, sizeof(Descriptors.State))));
                    HostCheck.True(m.Reads == reads && m.Writes == copies && o.Files.State->RetainedPipeOperations == 0 && o.Files.Admission.Pipes[0].BufferedBytes == 0);
                    m.Kernel.Same(kernel);
                    o.ScratchClean();
                }
                finally
                {
                    AttachedDisk(ref *o.Files.State) = original;
                    DiskMount(ref candidate) = &mount;
                }
            }

        HostCheck.True(Descriptors.Audit(o.Files.State));
        o.Drain();
    }

    [HostTest("linux.blocking-pipe read parks and wakes on real bytes with scrubbed scratch and explicit descriptor ownership")]
    private static void ReadWrite()
    {
        using Owner o = new();
        var m = o.Thread;
        var parent = m.Start();
        m.Kernel.Dispatch();
        var child = m.Clone();
        var pipe = o.Pipe();
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output, 32).Action);
        var staged = m.View(parent);
        HostCheck.True(staged.WaitSource == 2 && staged.WaitDeadline == 0 && staged.Wait.IsValid);
        o.Park(parent);
        HostCheck.True(!Descriptors.End(o.Files.State));
        HostCheck.Equal(-14L, o.Files.Call(3, pipe.Write));
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        m.Put64(Data, 0x8877665544332211);
        HostCheck.Equal(8L, o.Value(1, pipe.Write, Data, 8));
        o.ScratchClean();
        HostCheck.Equal(KernelThreadTable.State.Ready, m.Kernel.Inspect(parent).State);
        HostCheck.True(m.View(parent).WaitSource == 2 && m.View(parent).Wait.Sequence == staged.Wait.Sequence);
        m.Yield(child);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        var complete = o.Complete();
        HostCheck.Equal(LinuxSyscallAction.Return, complete.Action);
        HostCheck.Equal(8L, complete.Value);
        HostCheck.Equal(0x8877665544332211UL, m.Read64(Output));
        HostCheck.True(!m.View(parent).Wait.IsValid);
        HostCheck.True(m.View(parent).WaitSource == 0 && m.View(parent).WaitDeadline == 0 && staged.WaitSource == 2);
        HostCheck.Equal(LinuxSyscallAction.Rejected, o.Complete().Action);
        o.Drain();
    }

    [HostTest("linux.blocking-pipe retained original description survives fd close and reuse")]
    private static void ReusedDescriptor()
    {
        using Owner o = new();
        var m = o.Thread;
        var parent = m.Start();
        m.Kernel.Dispatch();
        var child = m.Clone();
        var original = o.Pipe();
        HostCheck.True(Descriptors.Capture(o.Files.State, original.Read, out var token));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, original.Read, Output, 4).Action);
        o.Park(parent);
        HostCheck.Equal(1U, o.Files.State->RetainedPipeOperations);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, o.Value(3, original.Read));
        HostCheck.Equal(1U, o.Files.State->RetainedPipeOperations);
        HostCheck.Equal(1U, o.Files.Admission.Pipes[0].ReaderCount);
        var replacement = o.Pipe();
        HostCheck.Equal(original.Read, replacement.Read);
        HostCheck.True(!Descriptors.IsCurrent(o.Files.State, token));
        m.Put32(Data, 0x11111111);
        HostCheck.Equal(4L, o.Value(1, replacement.Write, Data, 4));
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State);
        m.Put32(Data, 0xabcdef01);
        HostCheck.Equal(4L, o.Value(1, original.Write, Data, 4));
        m.Yield(child);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(4L, o.Complete().Value);
        HostCheck.Equal(0xabcdef01U, m.Read32(Output));
        HostCheck.Equal(0U, o.Files.State->RetainedPipeOperations);
        HostCheck.Equal(4L, o.Value(0, replacement.Read, Output, 4));
        HostCheck.Equal(0x11111111U, m.Read32(Output));
        HostCheck.Equal(0L, o.Value(3, original.Write));
        HostCheck.True(!o.Files.Admission.Pipes[0].Initialized);
        o.Drain();
    }

    [HostTest("linux.blocking-pipe large write retains progress through repeated capacity waits and returns the whole count")]
    private static void LargeWrite()
    {
        using Owner o = new();
        var m = o.Thread;
        var parent = m.Start();
        m.Kernel.Dispatch();
        var child = m.Clone();
        var pipe = o.Pipe();
        byte* scratch = stackalloc byte[128];
        LinuxSyscallGateState parentGate = default, childGate = default;
        o.BeginGate(ref parentGate, scratch);
        o.BeginGate(ref childGate, scratch + 64);
        byte[] expected = new byte[9000];
        for (int i = 0; i < expected.Length; i++)
            expected[i] = (byte)(i * 7 + 3);
        expected.CopyTo(new Span<byte>(m.At(Data), 9000));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.GateCall(ref parentGate, 1, pipe.Write, Data, 9000).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.Blocked, parentGate.Status);
        HostCheck.Equal(0U, parentGate.BytesWritten);
        HostCheck.Equal(4096U, m.View(parent).PipeTransferredBytes);
        o.Park(parent);
        for (int chunk = 0; chunk < 2; chunk++)
        {
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(4096L, o.GateCall(ref childGate, 0, pipe.Read, Output, 4096).Value);
            HostCheck.True(expected.AsSpan(chunk * 4096, 4096).SequenceEqual(new ReadOnlySpan<byte>(m.At(Output), 4096)));
            m.Yield(child);
            HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            var result = LinuxSyscallGateway.CompleteBlocked(ref parentGate, o.Complete());
            if (chunk == 0)
            {
                HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action);
                HostCheck.Equal(8192U, m.View(parent).PipeTransferredBytes);
                o.Park(parent);
            }
            else
            {
                HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
                HostCheck.Equal(9000L, result.Value);
            }
        }

        HostCheck.Equal(LinuxSyscallGateStatus.Active, parentGate.Status);
        HostCheck.Equal(9000U, parentGate.BytesWritten);
        HostCheck.Equal(1U, parentGate.Calls);
        HostCheck.Equal(2U, childGate.Calls);
        HostCheck.Equal(0U, childGate.BytesWritten);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref parentGate, new(LinuxSyscallAction.Return, 9000)).Action);
        HostCheck.Equal(808L, o.Value(0, pipe.Read, Output, 4096));
        HostCheck.True(expected.AsSpan(8192).SequenceEqual(new ReadOnlySpan<byte>(m.At(Output), 808)));
        o.Drain();
    }

    [HostTest("linux.blocking-pipe peer close completes EOF and EPIPE without stranded endpoint references")]
    private static void PeerClose()
    {
        foreach (bool reader in new[]
        {
            true,
            false
        }

        )
        {
            using Owner o = new();
            var m = o.Thread;
            var parent = m.Start();
            m.Kernel.Dispatch();
            var child = m.Clone();
            var pipe = o.Pipe();
            if (!reader)
                HostCheck.Equal(4096L, o.Value(1, pipe.Write, Data, 4096));
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(reader ? 0UL : 1UL, reader ? pipe.Read : pipe.Write, Output, 1).Action);
            o.Park(parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(0L, o.Value(3, reader ? pipe.Write : pipe.Read));
            m.Yield(child);
            HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(reader ? 0L : -32L, o.Complete().Value);
            HostCheck.True(!m.Service->Failed);
            o.Drain();
        }
    }

    [HostTest("linux.blocking-pipe atomic waits observe shared nonblock changes after a real capacity event")]
    private static void AtomicAndNonblock()
    {
        using Owner o = new();
        var m = o.Thread;
        var parent = m.Start();
        m.Kernel.Dispatch();
        var child = m.Clone();
        var pipe = o.Pipe();
        HostCheck.Equal(4096L, o.Value(1, pipe.Write, Data, 4096));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(1, pipe.Write, Data, 4096).Action);
        HostCheck.Equal(0U, m.View(parent).PipeTransferredBytes);
        o.Park(parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, o.Value(72, pipe.Write, 4, Descriptors.NonBlock));
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State);
        HostCheck.Equal(1L, o.Value(0, pipe.Read, Output, 1));
        m.Yield(child);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(-11L, o.Complete().Value);
        HostCheck.Equal(4095U, o.Files.Admission.Pipes[0].BufferedBytes);
        o.Drain();
    }

    [HostTest("linux.blocking-pipe copy faults preserve queued data and large write faults return only the committed prefix")]
    private static void CopyFailures()
    {
        using (Owner o = new())
        {
            var m = o.Thread;
            var parent = m.Start();
            m.Kernel.Dispatch();
            var child = m.Clone();
            var pipe = o.Pipe();
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output, 4).Action);
            o.Park(parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            m.Put32(Data, 0x12345678);
            HostCheck.Equal(4L, o.Value(1, pipe.Write, Data, 4));
            m.Yield(child);
            m.Kernel.Dispatch();
            m.FailWrite(Output);
            HostCheck.Equal(-14L, o.Complete().Value);
            HostCheck.Equal(4U, o.Files.Admission.Pipes[0].BufferedBytes);
            m.FailWrite(ulong.MaxValue);
            HostCheck.Equal(4L, o.Value(0, pipe.Read, Output, 4));
            HostCheck.Equal(0x12345678U, m.Read32(Output));
            o.Drain();
        }

        using (Owner o = new())
        {
            var m = o.Thread;
            var parent = m.Start();
            m.Kernel.Dispatch();
            var child = m.Clone();
            var pipe = o.Pipe();
            HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(1, pipe.Write, Data, 8192).Action);
            o.Park(parent);
            m.Kernel.Dispatch();
            HostCheck.Equal(4096L, o.Value(0, pipe.Read, Output, 4096));
            m.Yield(child);
            m.Kernel.Dispatch();
            m.FailRead(Data + 4096);
            HostCheck.Equal(4096L, o.Complete().Value);
            HostCheck.Equal(0U, o.Files.Admission.Pipes[0].BufferedBytes);
            o.Drain();
        }
    }

    [HostTest("linux.blocking-pipe shutdown drains parked and pre park references before descriptor End")]
    private static void ShutdownAndAdmission()
    {
        using Owner o = new(attach: false);
        var m = o.Thread;
        HostCheck.True(!m.Service->EnablePipes(Key + 1, 2, o.Files.State));
        HostCheck.True(!m.Service->EnablePipes(Key, 0x202, o.Files.State));
        var copy = *m.Service;
        HostCheck.True(!copy.EnablePipes(Key, 2, o.Files.State));
        HostCheck.True(m.Service->EnablePipes(Key, 2, o.Files.State));
        var parent = m.Start();
        m.Kernel.Dispatch();
        var child = m.Clone();
        var pipe = o.Pipe();
        HostCheck.True(!m.Service->EnablePipes(Key, 2, o.Files.State));
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output, 4).Action);
        o.Park(parent);
        m.Kernel.Dispatch();
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Call(0, pipe.Read, Output + 16, 4).Action);
        var capture = m.Kernel.Capture(child);
        var invalid = capture;
        invalid.ResumeRip = m.Kernel.Code - 1;
        HostCheck.True(!m.Service->CommitPipeWait(Key, 2, invalid, out _));
        HostCheck.True(!Descriptors.End(o.Files.State));
        o.Drain();
    }
}
