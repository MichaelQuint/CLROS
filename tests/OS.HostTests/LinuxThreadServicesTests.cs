using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using KernelModel = KernelThreadTableTests.Model;
using Services = OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

internal static unsafe partial class LinuxThreadServicesTests
{
    internal const ulong Key = 0x6c696e7578746872;
    internal const ulong UserBase = 0x400000, UserBytes = 0x5000;
    private const ulong ParentTidAddress = UserBase + 0x1160, ChildTidAddress = UserBase + 0x1164;
    private const ulong MaskInput = UserBase + 0x1180, ParentOldMask = UserBase + 0x1190, ChildOldMask = UserBase + 0x11a0;
    private const ulong InheritedMask = 0x380000002;
    [HostTest("linux.thread copied dynamic output preflights real missing pages then writes exact bytes after mapping")]
    private static void DynamicOutputCapability()
    {
        using Model m = new();
        using LinuxMemoryServicesTests.Owner memory = new();
        memory.Begin();
        const ulong page = LinuxMemoryServices.DynamicStart + 0x20000, address = page + 4096 - 64;
        HostCheck.Equal((long)page, LinuxMemoryServices.Map(memory.State, page, 4096, 3, 0x100022, ulong.MaxValue, 0));
        m.UseMemory(memory.Memory);
        new Span<byte>(memory.At(address), 64).Fill(0x91);
        byte* source = stackalloc byte[144];
        for (int i = 0; i < 144; i++)
            source[i] = (byte)(i * 37);
        uint writes = m.Writes;
        HostCheck.Equal((byte)0, m.UserCopyOut(m.Access.Context, address, source, 144));
        HostCheck.True(m.Writes == writes && new ReadOnlySpan<byte>(memory.At(address), 64).IndexOfAnyExcept((byte)0x91) < 0);
        HostCheck.Equal((long)(page + 4096), LinuxMemoryServices.Map(memory.State, page + 4096, 4096, 3, 0x100022, ulong.MaxValue, 0));
        HostCheck.Equal((byte)1, m.UserCopyOut(m.Access.Context, address, source, 144));
        for (uint i = 0; i < 144; i++)
            HostCheck.Equal(source[i], *memory.At(address + i));
        HostCheck.Equal(writes + 1, m.Writes);
    }

    private static bool UserRange(ulong address, uint bytes, bool write) => bytes != 0 && address >= UserBase + (write ? 4096UL : 0) && bytes <= UserBytes && address - UserBase <= UserBytes - bytes;
    [UnmanagedCallersOnly]
    private static long Unsupported(void* context, LinuxSyscallFrame* frame) => -38;
    [UnmanagedCallersOnly]
    private static LinuxSyscallResult ThreadDispatch(void* context, LinuxSyscallFrame* frame) => ((UserMemory*)context)->Service->Dispatch(Key, 2, ref *frame); // IRQ-off host model only.
    [UnmanagedCallersOnly]
    private static byte PageAccess(void* context, ulong page)
    {
        if (context == null)
            return 0;
        if (UserRange(page, 4096, false))
            return page == UserBase ? (byte)5 : (byte)3;
        var memory = ((UserMemory*)context)->Memory;
        if (memory.Tables == null || !OS.Kernel.Memory.X64UserAddressSpace.TryReadLeaf(memory.Tables, memory.RootPhysical, memory.TablePages, page, out ulong leaf) || (leaf & 5) != 5)
            return 0;
        return (byte)(1 | ((leaf & 2) != 0 ? 2 : 0) | ((leaf & 0x8000000000000000) == 0 ? 4 : 0));
    }

    [UnmanagedCallersOnly]
    private static byte CopyFromUser(void* context, ulong address, byte* output, uint bytes)
    {
        if (context == null || output == null || !UserRange(address, bytes, false))
            return 0;
        UserMemory* user = (UserMemory*)context;
        user->Reads++;
        user->LastRead = address;
        if (address == user->FailReadAddress)
            return 0;
        for (uint i = 0; i < bytes; i++)
            output[i] = user->Bytes[address - UserBase + i];
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte CopyToUser(void* context, ulong address, byte* input, uint bytes)
    {
        if (context == null || input == null || bytes == 0)
            return 0;
        UserMemory* user = (UserMemory*)context;
        bool direct = UserRange(address, bytes, true);
        if (!direct)
        {
            if (address < 4096 || address >= LinuxSyscallGateway.UserEnd || bytes > LinuxSyscallGateway.UserEnd - address)
                return 0;
            // Real test page tables/pool, not a permission flag which would
            // make the newly-mapped-tail negative unconditionally fail later.
            for (uint done = 0; done < bytes;)
            {
                uint chunk = Math.Min(bytes - done, 4096U - (uint)((address + done) & 4095));
                if (!DynamicOutput(user, address + done, chunk, out _))
                    return 0;
                done += chunk;
            }
        }

        user->Writes++;
        user->LastWrite = address;
        if (address == user->FailWriteAddress)
        {
            user->FailedWrites++;
            return 0;
        }

        if (direct)
            for (uint i = 0; i < bytes; i++)
                user->Bytes[address - UserBase + i] = input[i];
        else
            for (uint done = 0; done < bytes;)
            {
                uint chunk = Math.Min(bytes - done, 4096U - (uint)((address + done) & 4095));
                if (!DynamicOutput(user, address + done, chunk, out byte* target))
                    return 0;
                new ReadOnlySpan<byte>(input + done, (int)chunk).CopyTo(new Span<byte>(target, (int)chunk));
                done += chunk;
            }

        return 1;
    }

    private static bool DynamicOutput(UserMemory* user, ulong address, uint bytes, out byte* target)
    {
        target = null;
        var memory = user->Memory;
        if (memory.Tables == null || memory.Pool == null || !OS.Kernel.Memory.X64UserAddressSpace.TryReadLeaf(memory.Tables, memory.RootPhysical, memory.TablePages, address, out ulong leaf) || (leaf & 7) != 7)
            return false;
        ulong physical = (leaf & 0x000ffffffffff000) + (address & 4095), poolBytes = memory.PoolPages * 4096UL;
        if (physical < memory.PoolPhysical || bytes > poolBytes || physical - memory.PoolPhysical > poolBytes - bytes)
            return false;
        target = memory.Pool + physical - memory.PoolPhysical;
        return true;
    }

    [UnmanagedCallersOnly]
    private static byte ResolveFutex(void* context, ulong address, uint** alias, ulong* lifetime)
    {
        if (context == null || alias == null || lifetime == null || (address & 3) != 0)
            return 0;
        UserMemory* user = (UserMemory*)context;
        if (UserRange(address, 4, false))
            *alias = (uint*)(user->Bytes + address - UserBase);
        else
        {
            var memory = user->Memory;
            if (memory.Tables == null || !OS.Kernel.Memory.X64UserAddressSpace.TryReadLeaf(memory.Tables, memory.RootPhysical, memory.TablePages, address, out ulong leaf) || (leaf & 5) != 5)
                return 0;
            ulong physical = (leaf & 0x000ffffffffff000) + (address & 4095);
            if (physical < memory.PoolPhysical || physical - memory.PoolPhysical > memory.PoolPages * 4096UL - 4)
                return 0;
            *alias = (uint*)(memory.Pool + physical - memory.PoolPhysical);
        }

        *lifetime = user->Lifetime;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Monotonic(void* context, ulong* now)
    {
        if (context == null || now == null || ((UserMemory*)context)->ClockFails)
            return 0;
        UserMemory* user = (UserMemory*)context;
        if (user->ClockStep > ulong.MaxValue - user->Now)
            return 0;
        *now = user->Now;
        user->Now += user->ClockStep;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Sink(void* context, uint channel, byte* source, uint bytes) => context != null && source != null && channel is 1 or 2 && bytes <= LinuxDescriptorServices.MaximumIo ? (byte)1 : (byte)0;
    [HostTest("linux.thread-services start publishes a complete real binding and authenticates the current generation")]
    private static void StartAndIdentity()
    {
        using Model m = new();
        var identity = m.Start();
        HostCheck.Equal(0x500000001UL, identity.Value);
        HostCheck.Equal(1U, m.Service->ActiveThreads);
        var ready = m.Kernel.Inspect(identity);
        HostCheck.Equal(KernelThreadTable.State.Ready, ready.State);
        HostCheck.Equal((ulong)m.Slots, ready.Argument);
        HostCheck.Equal(m.Workers[0].Entry, ready.Entry);
        HostCheck.Equal(identity.Value, ready.Context.Identity);
        HostCheck.Equal(ThreadContextSwitch.ResumeKind.Entry, ready.Context.Kind);
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.AdmitResume(m.Kernel.Owner, ready.Context));
        HostCheck.Equal(InheritedMask, m.View(identity).BlockedSignals);
        HostCheck.True(!m.Service->CurrentImage(Key, 2, out var inactive) && inactive == null);
        var frame = m.Initial->Frame;
        HostCheck.Equal(LinuxSyscallAction.Rejected, m.Service->Dispatch(Key, 2, ref frame).Action);
        HostCheck.Equal(identity.Value, m.Kernel.Dispatch().Value);
        var image = m.CurrentImage();
        HostCheck.True(new ReadOnlySpan<byte>(image, 728).SequenceEqual(new ReadOnlySpan<byte>(m.Initial, 728)));
        var pid = m.Call(39);
        HostCheck.Equal(LinuxSyscallAction.Return, pid.Action);
        HostCheck.Equal(101L, pid.Value);
        var tid = m.Call(186, 999);
        HostCheck.Equal(LinuxSyscallAction.Return, tid.Action);
        HostCheck.Equal(101L, tid.Value);
        HostCheck.Equal(101U, m.View(identity).Tid);
        HostCheck.True(!m.Service->Inspect(Key, 2, new(identity.Value + (1UL << 32)), out var stale) && !stale.Kernel.IsValid);
        HostCheck.Equal(0U, m.Reads);
        HostCheck.Equal(0U, m.Writes);
    }

    [HostTest("linux.thread-services sched_yield stages no wait and real captured FIFO resumes the ready peer first")]
    private static void YieldToReadyPeer()
    {
        using Model m = new();
        var parent = m.Start();
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        var child = m.Clone();
        uint reads = m.Reads, writes = m.Writes;
        var result = m.Call(24, ulong.MaxValue, ulong.MaxValue);
        HostCheck.True(result.Action == LinuxSyscallAction.Yielded && result.Value == 0 && result.ExitCode == 0);
        HostCheck.Equal(parent.Value, m.Kernel.Table->Current.Value);
        HostCheck.Equal(1U, m.Kernel.Table->ReadyCount);
        HostCheck.True(!m.View(parent).Wait.IsValid && m.View(parent).WaitSource == 0);
        HostCheck.Equal(reads, m.Reads);
        HostCheck.Equal(writes, m.Writes);
        HostCheck.True(m.Kernel.Table->Yield(KernelThreadTableTests.Key, 2, parent, m.Kernel.Capture(parent)).Succeeded);
        m.Kernel.Confirm();
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.True(m.Kernel.Table->Yield(KernelThreadTableTests.Key, 2, child, m.Kernel.Capture(child)).Succeeded);
        m.Kernel.Confirm();
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.CurrentImage()->Value);
        HostCheck.True(!m.View(parent).Wait.IsValid && m.Service->ActiveThreads == 2);
    }

    [HostTest("linux.thread-services exact pthread clone inherits independent images and masks with real user copies")]
    private static void CloneAndMasks()
    {
        using Model m = new();
        var parent = m.Start();
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        var parentImage = m.CurrentImage();
        var child = m.Clone();
        var expectedChildFrame = parentImage->Frame;
        expectedChildFrame.Rsp = UserBase + UserBytes;
        expectedChildFrame.FsBase = UserBase + 0x2100;
        HostCheck.Equal(102L, parentImage->Value);
        HostCheck.Equal(0x7d0f00UL, Services.PthreadCloneFlags);
        HostCheck.Equal(102U, m.Read32(ParentTidAddress));
        HostCheck.Equal(0x5a5a5a5aU, m.Read32(ChildTidAddress));
        HostCheck.Equal(2U, m.Service->ActiveThreads);
        HostCheck.Equal(ChildTidAddress, m.View(child).ClearTid);
        HostCheck.Equal(InheritedMask, m.View(child).BlockedSignals);
        HostCheck.Equal(UserBase + 0x3000, parentImage->Frame.Rsp);
        HostCheck.Equal(UserBase + 0x1100, parentImage->Frame.FsBase);
        m.Put64(MaskInput, (1UL << 5) | LinuxSignalMask.Unblockable);
        HostCheck.Equal(0L, m.Call(14, LinuxSignalMask.Set, MaskInput, ParentOldMask, 8).Value);
        HostCheck.Equal(InheritedMask, m.Read64(ParentOldMask));
        HostCheck.Equal(1UL << 5, m.View(parent).BlockedSignals);
        HostCheck.Equal(InheritedMask, m.View(child).BlockedSignals);
        m.Yield(parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        var childImage = m.CurrentImage();
        HostCheck.True(childImage != parentImage);
        HostCheck.Equal(0L, childImage->Value);
        HostCheck.True(new ReadOnlySpan<byte>(&expectedChildFrame, 96).SequenceEqual(new ReadOnlySpan<byte>(&childImage->Frame, 96)));
        HostCheck.Equal(UserBase + UserBytes, childImage->Frame.Rsp);
        HostCheck.Equal(UserBase + 0x2100, childImage->Frame.FsBase);
        HostCheck.Equal(parentImage->Frame.GsBase, childImage->Frame.GsBase);
        HostCheck.Equal(56UL, childImage->Frame.Number);
        HostCheck.Equal(0x7d0f00UL, childImage->Frame.Argument0);
        for (uint i = 0; i < 14; i++)
        {
            ulong expected = 0x8100000000000001UL + i * 0x0101010101010101UL;
            HostCheck.Equal(expected, parentImage->Gpr[i]);
            HostCheck.Equal(expected, childImage->Gpr[i]);
        }

        HostCheck.True(new ReadOnlySpan<byte>(parentImage->Fx, 512).SequenceEqual(new ReadOnlySpan<byte>(childImage->Fx, 512)));
        byte parentFp = parentImage->Fx[200];
        childImage->Gpr[0] ^= 1;
        childImage->Fx[200] ^= 0x5a;
        HostCheck.Equal(0x8100000000000001UL, parentImage->Gpr[0]);
        HostCheck.Equal(parentFp, parentImage->Fx[200]);
        HostCheck.Equal(101L, m.Call(39).Value);
        HostCheck.Equal(102L, m.Call(186).Value);
        m.Put64(MaskInput, 1UL << 7);
        HostCheck.Equal(0L, m.Call(14, LinuxSignalMask.Set, MaskInput, ChildOldMask, 8).Value);
        HostCheck.Equal(InheritedMask, m.Read64(ChildOldMask));
        HostCheck.Equal(1UL << 7, m.View(child).BlockedSignals);
        HostCheck.Equal(1UL << 5, m.View(parent).BlockedSignals);
        m.Put64(MaskInput, m.Read64(ChildOldMask));
        HostCheck.Equal(0L, m.Call(14, LinuxSignalMask.Set, MaskInput, ChildOldMask, 8).Value);
        HostCheck.Equal(InheritedMask, m.View(child).BlockedSignals);
        HostCheck.Equal(1UL << 5, m.View(parent).BlockedSignals);
        m.Yield(child);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        m.Put64(MaskInput, m.Read64(ParentOldMask));
        HostCheck.Equal(0L, m.Call(14, LinuxSignalMask.Set, MaskInput, ParentOldMask, 8).Value);
        HostCheck.Equal(InheritedMask, m.View(parent).BlockedSignals);
        HostCheck.Equal(InheritedMask, m.View(child).BlockedSignals);
        HostCheck.Equal(4U, m.Reads);
        HostCheck.Equal(5U, m.Writes);
        HostCheck.True(!m.Service->Failed);
    }

    [HostTest("linux.thread-services staged exit confirms supervisor before reap and retains best effort clear results")]
    private static void ExitReapAndReuse()
    {
        foreach (bool failedClear in new[]
        {
            false,
            true
        }

        )
        {
            using Model m = new();
            var parent = m.Start();
            m.Kernel.Dispatch();
            var child = m.Clone();
            m.Yield(parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            m.Put32(ChildTidAddress, 102);
            if (failedClear)
                m.FailWrite(ChildTidAddress);
            var staged = m.Call(60, 77);
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, staged.Action);
            HostCheck.Equal(77U, staged.ExitCode);
            HostCheck.True(m.View(child).Exiting);
            HostCheck.Equal(KernelThreadTable.State.Running, m.Kernel.Inspect(child).State);
            HostCheck.Equal(102U, m.Read32(ChildTidAddress));
            HostCheck.True(m.Service->CommitExit(Key, 2, m.Kernel.Capture(child), out long clearResult));
            HostCheck.Equal(failedClear ? -14L : 0L, clearResult);
            HostCheck.Equal(failedClear ? 102U : 0U, m.Read32(ChildTidAddress));
            HostCheck.Equal(2U, m.Writes);
            HostCheck.Equal(failedClear ? 1U : 0U, m.FailedWrites);
            HostCheck.True(!m.Service->Failed);
            HostCheck.Equal(KernelThreadTable.State.Exiting, m.Kernel.Inspect(child).State);
            HostCheck.True(m.Kernel.Table->HandoffPending);
            HostCheck.Equal(0UL, m.View(child).ClearTid);
            HostCheck.Equal(2U, m.Service->ActiveThreads);
            m.Kernel.Confirm();
            HostCheck.Equal(KernelThreadTable.State.Exited, m.Kernel.Inspect(child).State);
            HostCheck.Equal(77, m.Kernel.Inspect(child).ExitValue);
            HostCheck.True(m.Service->Reap(Key, 2, child, m.Kernel.SupervisorRsp, out var released));
            HostCheck.Equal(1U, m.Service->ActiveThreads);
            HostCheck.True(!m.Service->Inspect(Key, 2, child, out _));
            HostCheck.Equal(KernelThreadTable.Status.StaleIdentity, m.Kernel.Inspect(child).Code);
            HostCheck.True(new ReadOnlySpan<byte>(released.Context, (int)released.ContextBytes).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.True(new ReadOnlySpan<byte>((void*)(released.StackGuardStart + 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.True(new ReadOnlySpan<byte>(m.Slots + 1, sizeof(Services.Slot)).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            var replacement = m.Clone();
            HostCheck.Equal(child.Value + (1UL << 32), replacement.Value);
            HostCheck.Equal(103U, m.View(replacement).Tid);
            HostCheck.Equal(103U, m.Read32(ParentTidAddress));
            HostCheck.True(!m.Service->Inspect(Key, 2, child, out _));
            HostCheck.Equal(KernelThreadTable.State.Ready, m.Kernel.Inspect(replacement).State);
        }
    }

    [HostTest("linux.thread-services clone rejects bad ABI and user ranges before publication or TID writes")]
    private static void CloneAdmissionFailures()
    {
        using Model m = new();
        var parent = m.Start();
        m.Kernel.Dispatch();
        m.PrepareNext();
        ulong stack = UserBase + UserBytes, tls = UserBase + 0x2100;
        (ulong Flags, ulong Stack, ulong Parent, ulong Child, ulong Tls, long Error)[] cases = [(0, stack, ParentTidAddress, ChildTidAddress, tls, -22), (Services.PthreadCloneFlags | 17, stack, ParentTidAddress, ChildTidAddress, tls, -22), (Services.PthreadCloneFlags, stack - 1, ParentTidAddress, ChildTidAddress, tls, -22), (Services.PthreadCloneFlags, 0, ParentTidAddress, ChildTidAddress, tls, -22), (Services.PthreadCloneFlags, stack, ParentTidAddress, ChildTidAddress, 0, -22), (Services.PthreadCloneFlags, stack, 0, ChildTidAddress, tls, -14), (Services.PthreadCloneFlags, stack, ParentTidAddress + 1, ChildTidAddress, tls, -14), (Services.PthreadCloneFlags, stack, UserBase + 0x100, ChildTidAddress, tls, -14), (Services.PthreadCloneFlags, stack, ParentTidAddress, UserBase + UserBytes, tls, -14), (Services.PthreadCloneFlags, UserBase + 0x800, ParentTidAddress, ChildTidAddress, tls, -14), (Services.PthreadCloneFlags, stack, ParentTidAddress, ChildTidAddress, stack, -14)];
        foreach (var c in cases)
        {
            var result = m.Call(56, c.Flags, c.Stack, c.Parent, c.Child, c.Tls);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            HostCheck.Equal(c.Error, result.Value);
            HostCheck.Equal(1U, m.Service->ActiveThreads);
            HostCheck.Equal(0U, m.Kernel.Table->ReadyCount);
            HostCheck.Equal(parent.Value, m.Kernel.Table->Current.Value);
            HostCheck.Equal(0U, m.Writes);
            HostCheck.Equal(0x5a5a5a5aU, m.Read32(ParentTidAddress));
            HostCheck.True(new ReadOnlySpan<byte>(m.Slots + 1, sizeof(Services.Slot) * 2).IndexOfAnyExcept((byte)0) == -1);
        }

        var child = m.Clone();
        HostCheck.Equal(102U, m.View(child).Tid);
        var last = m.Clone();
        HostCheck.Equal(103U, m.View(last).Tid);
        var full = m.Call(56, Services.PthreadCloneFlags, stack, ParentTidAddress, ChildTidAddress, tls);
        HostCheck.Equal(LinuxSyscallAction.Return, full.Action);
        HostCheck.Equal(-11L, full.Value);
        HostCheck.Equal(3U, m.Service->ActiveThreads);
        HostCheck.Equal(2U, m.Kernel.Table->ReadyCount);
        HostCheck.Equal(2U, m.Writes);
        HostCheck.Equal(103U, m.Read32(ParentTidAddress));
    }

    [HostTest("linux.thread-services failed published TID copy prevents resumption and drains through real exits")]
    private static void FailedCloneShutdown()
    {
        using Model m = new();
        var parent = m.Start();
        m.Kernel.Dispatch();
        var child = m.PrepareNext();
        m.FailWrite(ParentTidAddress);
        var failed = m.Call(56, Services.PthreadCloneFlags, UserBase + UserBytes, ParentTidAddress, ChildTidAddress, UserBase + 0x2100);
        HostCheck.Equal(LinuxSyscallAction.Rejected, failed.Action);
        HostCheck.True(m.Service->Failed);
        HostCheck.Equal(2U, m.Service->ActiveThreads);
        HostCheck.Equal(1U, m.Kernel.Table->ReadyCount);
        HostCheck.Equal(102U, m.View(child).Tid);
        HostCheck.Equal(0x5a5a5a5aU, m.Read32(ParentTidAddress));
        HostCheck.Equal(LinuxSyscallAction.Rejected, m.Call(39).Action);
        HostCheck.True(m.Service->Shutdown(Key, 2, 33));
        HostCheck.True(!m.Service->Release(Key, 2));
        foreach (var identity in new[]
        {
            parent,
            child
        }

        )
        {
            HostCheck.True(m.View(identity).Exiting);
            HostCheck.True(m.Kernel.Inspect(identity).CancellationRequested);
            HostCheck.True(m.Service->CommitExit(Key, 2, m.Kernel.Capture(identity), out long clear));
            HostCheck.Equal(0L, clear);
            m.Kernel.Confirm();
            HostCheck.True(m.Service->Reap(Key, 2, identity, m.Kernel.SupervisorRsp, out _));
            if (identity.Value == parent.Value)
                HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        }

        HostCheck.Equal(0U, m.Service->ActiveThreads);
        HostCheck.Equal(0U, m.Read32(ChildTidAddress));
        HostCheck.True(m.Service->Release(Key, 2));
        HostCheck.Equal(0U, m.Kernel.Table->ReadyCount);
        HostCheck.True(!m.Service->Inspect(Key, 2, parent, out _));
    }

    [HostTest("linux.thread-services mask oldset failure retains the Linux update and FS errors retain the base")]
    private static void UserCopyAndFsFailures()
    {
        using Model m = new();
        var parent = m.Start();
        m.Kernel.Dispatch();
        m.Put64(MaskInput, 1UL << 6);
        m.FailWrite(ParentOldMask);
        HostCheck.Equal(-14L, m.Call(14, LinuxSignalMask.Set, MaskInput, ParentOldMask, 8).Value);
        HostCheck.Equal(1UL << 6, m.View(parent).BlockedSignals);
        HostCheck.Equal(0x5a5a5a5a5a5a5a5aUL, m.Read64(ParentOldMask));
        m.FailWrite(ulong.MaxValue);
        HostCheck.Equal(0L, m.Call(14, ulong.MaxValue, 0, ParentOldMask, 8).Value);
        HostCheck.Equal(1UL << 6, m.Read64(ParentOldMask));
        HostCheck.Equal(-22L, m.Call(14, LinuxSignalMask.Set, MaskInput, ParentOldMask, 128).Value);
        HostCheck.Equal(-14L, m.Call(14, LinuxSignalMask.Set, UserBase + UserBytes, 0, 8).Value);
        HostCheck.Equal(-14L, m.Call(14, 3, UserBase + UserBytes, 0, 8).Value);
        HostCheck.Equal(-22L, m.Call(14, 3, MaskInput, 0, 8).Value);
        m.Put64(MaskInput, 1UL << 7);
        HostCheck.Equal(0L, m.Call(14, 0xffffffff00000002, MaskInput, 0, 8).Value);
        HostCheck.Equal(1UL << 7, m.View(parent).BlockedSignals);
        m.Put64(MaskInput, 1UL << 6);
        HostCheck.Equal(-14L, m.Call(14, LinuxSignalMask.Set, MaskInput, UserBase, 8).Value);
        HostCheck.Equal(1UL << 6, m.View(parent).BlockedSignals);
        ulong before = m.CurrentImage()->Frame.FsBase;
        HostCheck.Equal(-14L, m.Call(158, 0x1002, UserBase + UserBytes).Value);
        HostCheck.Equal(before, m.CurrentImage()->Frame.FsBase);
        ulong priorGs = m.CurrentImage()->Frame.GsBase;
        HostCheck.Equal(-14L, m.Call(158, 0x1001, UserBase + UserBytes).Value);
        HostCheck.Equal(priorGs, m.CurrentImage()->Frame.GsBase);
        HostCheck.Equal(0L, m.Call(158, 0xffffffff00001001, UserBase + 0x2180).Value);
        HostCheck.Equal(UserBase + 0x2180, m.CurrentImage()->Frame.GsBase);
        HostCheck.Equal(before, m.CurrentImage()->Frame.FsBase);
        HostCheck.Equal(0L, m.Call(158, 0xffffffff00001002, UserBase + 0x2100).Value);
        HostCheck.Equal(0L, m.Call(158, 0x1003, ParentOldMask).Value);
        HostCheck.Equal(UserBase + 0x2100, m.Read64(ParentOldMask));
        HostCheck.Equal(0L, m.Call(158, 0x1004, ParentOldMask).Value);
        HostCheck.Equal(UserBase + 0x2180, m.Read64(ParentOldMask));
        HostCheck.Equal(-22L, m.Call(158, 0x1005, UserBase + 0x2100).Value);
        HostCheck.Equal(0L, m.Call(158, 0x1001, 0).Value);
        HostCheck.Equal(0UL, m.CurrentImage()->Frame.GsBase);
        HostCheck.Equal(0L, m.Call(158, 0x1002, 0).Value);
        HostCheck.Equal(0UL, m.CurrentImage()->Frame.FsBase);
        HostCheck.Equal(101L, m.Call(218, 0).Value);
        HostCheck.Equal(0UL, m.View(parent).ClearTid);
        HostCheck.Equal(101L, m.Call(218, ChildTidAddress + 1).Value);
        HostCheck.Equal(ChildTidAddress + 1, m.View(parent).ClearTid);
        HostCheck.Equal(-38L, m.Call(202).Value);
        HostCheck.True(!m.Service->Failed);
    }

    [HostTest("linux.thread-services real gateway GS changes survive clone and remain private to the running generation")]
    private static void GatewayGsAndClone()
    {
        using Model m = new();
        var parent = m.Start();
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        byte* scratch = stackalloc byte[64];
        LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        {
            UserRoot = 0x200000,
            MaximumCalls = 16,
            MaximumWrite = 64,
            Scratch = scratch,
            ScratchBytes = 64,
            FirmwareExited = true,
            OwnedBoundaryValidated = true,
            EnableStartup = true,
            EnableDescriptors = true,
            EnableThreads = true
        }, m.GatewayAccess));
        var frame = m.CurrentImage()->Frame;
        ulong fs = frame.FsBase;
        frame.Number = 158;
        frame.Argument0 = 0x1001;
        frame.Argument1 = UserBase + 0x2180;
        m.CurrentImage()->Frame = frame;
        var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
        HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 0 && gate.Status == LinuxSyscallGateStatus.Active);
        HostCheck.True(frame.GsBase == UserBase + 0x2180 && frame.FsBase == fs && m.CurrentImage()->Frame.GsBase == frame.GsBase);
        frame.Argument0 = 0x1004;
        frame.Argument1 = ParentOldMask;
        HostCheck.Equal(LinuxSyscallAction.Return, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
        HostCheck.Equal(UserBase + 0x2180, m.Read64(ParentOldMask));
        var child = m.Clone();
        m.Yield(parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(UserBase + 0x2180, m.CurrentImage()->Frame.GsBase);
        HostCheck.Equal(0L, m.Call(158, 0x1001, UserBase + 0x2200).Value);
        m.Yield(child);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(UserBase + 0x2180, m.CurrentImage()->Frame.GsBase);
    }

    [HostTest("linux.thread-services set tid address stores raw pointers and exit clears only accessible bytes")]
    private static void RawClearTidPointers()
    {
        foreach (ulong address in new[]
        {
            0UL,
            ChildTidAddress + 1,
            UserBase + UserBytes,
            ulong.MaxValue
        }

        )
        {
            using Model m = new();
            var parent = m.Start();
            m.Kernel.Dispatch();
            HostCheck.Equal(101L, m.Call(218, address).Value);
            HostCheck.Equal(address, m.View(parent).ClearTid);
            HostCheck.Equal(0U, m.Reads);
            HostCheck.Equal(0U, m.Writes);
            bool accessible = address == ChildTidAddress + 1;
            if (accessible)
                m.Put32(address, 101);
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, m.Call(60).Action);
            HostCheck.True(m.Service->CommitExit(Key, 2, m.Kernel.Capture(parent), out long clear));
            HostCheck.Equal(address == 0 || accessible ? 0L : -14L, clear);
            HostCheck.Equal(accessible ? 1U : 0U, m.Writes);
            if (accessible)
                HostCheck.Equal(0U, m.Read32(address));
            m.Kernel.Confirm();
            HostCheck.True(m.Service->Reap(Key, 2, parent, m.Kernel.SupervisorRsp, out _));
            HostCheck.True(m.Service->Release(Key, 2));
        }
    }

    [HostTest("linux.thread-services owner and native capture failures retain live storage and user TID")]
    private static void OwnerAndExitFailures()
    {
        using Model m = new();
        var parent = m.Start();
        m.Kernel.Dispatch();
        HostCheck.Equal(101L, m.Call(218, ChildTidAddress).Value);
        m.Put32(ChildTidAddress, 101);
        Services copy = *m.Service;
        HostCheck.True(!copy.CurrentImage(Key, 2, out var image) && image == null);
        HostCheck.True(!m.Service->CurrentImage(Key + 1, 2, out image) && image == null);
        HostCheck.True(!m.Service->CurrentImage(Key, 0x202, out image) && image == null);
        HostCheck.True(!m.Service->Shutdown(Key + 1, 2, 99));
        HostCheck.True(!m.View(parent).Exiting);
        HostCheck.Equal(LinuxSyscallAction.ThreadExited, m.Call(60, 44).Action);
        var capture = m.Kernel.Capture(parent);
        var wrong = capture;
        wrong.ResumeRip = m.Kernel.Code - 1;
        HostCheck.True(!m.Service->CommitExit(Key, 2, wrong, out _));
        HostCheck.Equal(0U, m.Writes);
        HostCheck.Equal(101U, m.Read32(ChildTidAddress));
        HostCheck.Equal(KernelThreadTable.State.Running, m.Kernel.Inspect(parent).State);
        HostCheck.True(m.Service->CommitExit(Key, 2, capture, out long clear));
        HostCheck.Equal(0L, clear);
        HostCheck.True(!m.Service->Reap(Key, 2, parent, m.Kernel.SupervisorRsp, out _));
        HostCheck.Equal(1U, m.Service->ActiveThreads);
        m.Kernel.Confirm();
        HostCheck.True(!m.Service->Reap(Key, 2, parent, m.Kernel.Stack(0) + 8192 - 64, out _));
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Kernel.Table->Pin(KernelThreadTableTests.Key, 2, parent, true));
        HostCheck.True(!m.Service->Reap(Key, 2, parent, m.Kernel.SupervisorRsp, out _));
        HostCheck.Equal(1U, m.Service->ActiveThreads);
        HostCheck.Equal(101U, m.View(parent).Tid);
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Kernel.Table->Pin(KernelThreadTableTests.Key, 2, parent, false));
        HostCheck.True(m.Service->Reap(Key, 2, parent, m.Kernel.SupervisorRsp, out _));
        HostCheck.True(m.Service->Release(Key, 2));
        using Model bypass = new();
        var foreignExit = bypass.Start();
        bypass.Kernel.Dispatch();
        bypass.Kernel.Exit(foreignExit);
        HostCheck.True(!bypass.Service->Reap(Key, 2, foreignExit, bypass.Kernel.SupervisorRsp, out _));
        HostCheck.Equal(1U, bypass.Service->ActiveThreads);
    }
}
