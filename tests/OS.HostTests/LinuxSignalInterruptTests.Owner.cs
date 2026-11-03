using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Descriptors = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxSignalInterruptTests
{
    private sealed class Owner : IDisposable
    {
        internal readonly LinuxBlockingPipeTests.Owner Core = new();
        internal LinuxThreadServicesTests.Model M => Core.Thread;

        internal Signals.State* State = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
        internal OSHostHandle Parent, Child;
        internal Owner(bool restart = false)
        {
            HostCheck.True(State != null && sizeof(Signals.State) <= 4096);
            new Span<byte>(State, 4096).Clear();
            HostCheck.True(Signals.Initialize(State, 4096, SignalKey, 2, new()
            {
                User = M.Access,
                CopyToUser = M.UserCopyOut,
                Capacity = 3,
                FirmwareExited = true,
                SingleOwnerValidated = true
            }));
            HostCheck.True(M.Service->EnableSignals(Key, 2, State));
            Parent = M.Start();
            M.Kernel.Dispatch();
            Child = M.Clone();
            *(ushort*)M.At(After - 2) = 0x050f;
            Action(restart);
            M.Put64(Input, 0);
            HostCheck.Equal(0L, Value(14, 2, Input, 0, 8));
        }

        internal LinuxSyscallResult Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            var image = M.CurrentImage();
            image->Gpr[3] = a0;
            image->Gpr[2] = a1;
            image->Gpr[4] = a2;
            image->Gpr[7] = a3;
            image->Gpr[5] = a4;
            image->Gpr[6] = a5;
            return M.Call(number, a0, a1, a2, a3, a4, a5); // Model the actual already captured syscall GPRs too.
        }

        internal long Value(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            var r = Call(number, a0, a1, a2, a3, a4, a5);
            HostCheck.Equal(LinuxSyscallAction.Return, r.Action);
            return r.Value;
        }

        internal void Action(bool restart, ulong handler = Base + 0x180)
        {
            *(Signals.Disposition*)M.At(Input) = new()
            {
                Handler = handler,
                Flags = 0x04000004UL | (restart ? 0x10000000UL : 0),
                Restorer = Base + 0x200
            };
            HostCheck.Equal(0L, Value(13, 10, Input, 0, 8));
        }

        internal void Park()
        {
            var current = M.Kernel.Table->Current;
            bool pipe = M.View(current).PipePending;
            bool ok = pipe ? M.Service->CommitPipeWait(Key, 2, M.Kernel.Capture(current), out bool handoff) : M.Service->CommitFutexWait(Key, 2, M.Kernel.Capture(current), out handoff);
            HostCheck.True(ok && handoff);
            M.Kernel.Confirm();
        }

        internal void Sender()
        {
            Park();
            HostCheck.Equal(Child.Value, M.Kernel.Dispatch().Value);
        }

        internal void Send() => HostCheck.Equal(0L, Value(234, 101, 101, 10));
        internal void ParentAgain()
        {
            M.Yield(Child);
            HostCheck.Equal(Parent.Value, M.Kernel.Dispatch().Value);
        }

        internal LinuxSyscallResult Complete()
        {
            return M.View(Parent).PipePending ? M.Service->CompletePipeWait(Key, 2) : M.Service->CompleteFutexWait(Key, 2);
        }

        internal Signals.Delivery Deliver() => M.Service->DeliverPendingSignals(Key, 2);
        internal ulong Caught()
        {
            HostCheck.Equal(Signals.Delivery.Delivered, Deliver());
            return M.CurrentImage()->Frame.Rsp;
        }

        internal LinuxSyscallResult Return(ulong frame)
        {
            M.CurrentImage()->Frame.Rsp = frame + 8;
            return Call(15);
        }

        internal void Futex(bool timed = false, bool bitset = false, ulong nanos = 100)
        {
            M.Put32(Word, 7);
            M.Put64(Timeout, 0);
            M.Put64(Timeout + 8, nanos);
            HostCheck.Equal(LinuxSyscallAction.Blocked, Call(202, Word, bitset ? 137UL : 128UL, 7, timed ? Timeout : 0, 0, bitset ? 4UL : 0).Action);
        }

        public void Dispose()
        {
            try
            {
                Core.Drain();
                HostCheck.True(Signals.End(State, SignalKey, 2));
            }
            finally
            {
                NativeMemory.AlignedFree(State);
                State = null;
                Core.Dispose();
            }
        }
    }
}
