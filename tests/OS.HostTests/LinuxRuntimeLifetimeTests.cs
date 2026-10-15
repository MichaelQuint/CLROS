using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Model = LinuxThreadServicesTests.Model;
using Service = OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

internal static unsafe class LinuxRuntimeLifetimeTests
{
    private const ulong Base = LinuxThreadServicesTests.UserBase;
    private const ulong ParentTid = Base + 0x1160, ChildTid = Base + 0x1164, Word = Base + 0x3100;

    [HostTest("linux.runtime-lifetime repeated full owners cancel parked waits drain pending signals and reuse backing with fresh generations")]
    private static void RepeatedCapacityCleanup()
    {
        using Model m = new(capacity: 16);
        Signals.State* signals = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
        HostCheck.True(signals != null); new Span<byte>(signals, 4096).Clear();
        ulong previousFirst = 0;
        try
        {
            for (uint round = 0; round < 3; round++)
            {
                ulong key = LinuxThreadServicesTests.Key + round, signalKey = 0x7369676e616c6d64UL + round;
                uint processId = 101 + 100 * round;
                if (round != 0)
                {
                    m.ChangeMappingLifetime(round + 1);
                    HostCheck.True(m.Service->Initialize(key, 2, new()
                    {
                        Threads = m.Kernel.Table, ThreadKey = KernelThreadTableTests.Key, Slots = m.Slots, Workers = m.Workers,
                        Capacity = 16, SlotBytes = 16 * (uint)sizeof(Service.Slot), WorkerBytes = 16 * (uint)sizeof(Service.Worker),
                        ProcessId = processId, ObservedMxcsrMask = 0xffbf, User = m.Access, CopyToUser = m.UserCopyOut,
                        FirmwareExited = true, SingleOwnerValidated = true
                    }));
                }
                var futex = m.FutexPlan(); futex.OwnerKey += round;
                HostCheck.True(m.Service->EnableFutex(key, 2, futex));
                HostCheck.True(Signals.Initialize(signals, 4096, signalKey, 2, new()
                { Capacity = 16, User = m.Access, CopyToUser = m.UserCopyOut, FirmwareExited = true, SingleOwnerValidated = true }));
                HostCheck.True(m.Service->EnableSignals(key, 2, signals));
                if (round != 0) HostCheck.True(!Signals.End(signals, signalKey - 1, 2));

                var expected = m.PrepareNext();
                HostCheck.Equal((long)processId, m.Service->Start(key, 2, m.Initial, Transition.UserImageBytes, new(0), out var first));
                HostCheck.Equal(expected.Value, first.Value);
                HostCheck.True((uint)first.Value == 1 && first.Value != previousFirst); previousFirst = first.Value;
                HostCheck.Equal(first.Value, m.Kernel.Dispatch().Value);
                if (round != 0) HostCheck.True(!m.Service->CurrentImage(key - 1, 2, out _));

                for (uint i = 1; i < 16; i++)
                {
                    var next = m.PrepareNext();
                    var clone = Call(m, key, 56, Service.PthreadCloneFlags, Base + LinuxThreadServicesTests.UserBytes,
                        ParentTid, ChildTid, Base + 0x2100);
                    HostCheck.Equal(LinuxSyscallAction.Return, clone.Action); HostCheck.Equal((long)(processId + i), clone.Value);
                    HostCheck.True(m.Service->Inspect(key, 2, next, out var child) && child.Tid == processId + i);
                }
                HostCheck.Equal(16U, m.Service->ActiveThreads); HostCheck.Equal(15U, m.Kernel.Table->ReadyCount);
                uint writes = m.Writes, parentWord = m.Read32(ParentTid);
                byte[] before = new ReadOnlySpan<byte>(signals, sizeof(Signals.State)).ToArray();
                var full = Call(m, key, 56, Service.PthreadCloneFlags, Base + LinuxThreadServicesTests.UserBytes,
                    ParentTid, ChildTid, Base + 0x2100);
                HostCheck.Equal(LinuxSyscallAction.Return, full.Action); HostCheck.Equal(-11L, full.Value);
                HostCheck.Equal(16U, m.Service->ActiveThreads); HostCheck.Equal(15U, m.Kernel.Table->ReadyCount);
                HostCheck.Equal(writes, m.Writes); HostCheck.Equal(parentWord, m.Read32(ParentTid));
                HostCheck.True(new ReadOnlySpan<byte>(signals, sizeof(Signals.State)).SequenceEqual(before));

                HostCheck.Equal(0L, Call(m, key, 200, processId + 15, 35).Value);
                m.Put32(Word, 42);
                HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, key, 202, Word, 128, 42).Action);
                HostCheck.True(m.Service->CommitFutexWait(key, 2, m.Kernel.Capture(first), out bool handoff) && handoff);
                m.Kernel.Confirm(); HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(first).State);
                HostCheck.True(!m.Service->MayChangeUserRange(key, 2, Word, 4) && !m.Service->Release(key, 2));
                HostCheck.True(m.Service->Shutdown(key, 2, 9));
                uint reaped = 0;
                while (m.Service->ActiveThreads != 0)
                {
                    var thread = m.Kernel.Table->Current;
                    if (!thread.IsValid) thread = m.Kernel.Dispatch();
                    HostCheck.True(m.Service->CommitExit(key, 2, m.Kernel.Capture(thread), out long clear) && clear == 0);
                    m.Kernel.Confirm(); HostCheck.True(m.Service->Reap(key, 2, thread, m.Kernel.SupervisorRsp, out _)); reaped++;
                }
                HostCheck.Equal(16U, reaped); HostCheck.True(m.Service->MayChangeUserRange(key, 2, Word, 4));
                HostCheck.True(m.Service->Release(key, 2) && Signals.End(signals, signalKey, 2));
                HostCheck.True(!m.Service->Inspect(key, 2, first, out _) && !m.Kernel.Table->Current.IsValid && !m.Kernel.Table->HandoffPending);
                HostCheck.Equal(0U, m.Kernel.Table->ReadyCount); HostCheck.Equal(0U, m.Kernel.Table->DeadlineCount);
                HostCheck.True(new ReadOnlySpan<byte>(m.Slots, 16 * sizeof(Service.Slot)).IndexOfAnyExcept((byte)0) < 0);
                HostCheck.True(new ReadOnlySpan<byte>(m.FutexCells, 4 * sizeof(Service.FutexCell)).IndexOfAnyExcept((byte)0) < 0);
                HostCheck.True(new ReadOnlySpan<byte>(signals, sizeof(Signals.State)).IndexOfAnyExcept((byte)0) < 0);
                HostCheck.Equal(0U, m.Read32(ChildTid));
            }
        }
        finally { NativeMemory.AlignedFree(signals); }
    }

    // Model only the owner's already-captured syscall image. Real scheduling,
    // wait registration, signal ownership and supervisor-confirmed reaps use
    // the production services above; this fixture provides no native evidence.
    private static LinuxSyscallResult Call(Model m, ulong key, ulong number, ulong a0 = 0, ulong a1 = 0,
        ulong a2 = 0, ulong a3 = 0, ulong a4 = 0)
    {
        HostCheck.True(m.Service->CurrentImage(key, 2, out var image)); var frame = image->Frame;
        frame.Number = number; frame.Argument0 = a0; frame.Argument1 = a1; frame.Argument2 = a2; frame.Argument3 = a3; frame.Argument4 = a4;
        image->Frame = frame; var result = m.Service->Dispatch(key, 2, ref frame); image->Frame = frame;
        if (result.Action == LinuxSyscallAction.Return) image->Value = result.Value;
        return result;
    }
}
