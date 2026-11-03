using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe partial class LinuxOwnerTerminationTests
{
    private sealed class Owner : IDisposable
    {
        internal readonly LinuxBlockingPipeTests.Owner Core = new();
        internal LinuxThreadServicesTests.Model M => Core.Thread;

        internal Signals.State* State = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
        internal OSHostHandle Parent, Child;
        internal Owner(bool child = false, bool attached = true)
        {
            HostCheck.True(State != null && sizeof(Signals.State) <= 4096);
            new Span<byte>(State, 4096).Clear();
            HostCheck.True(Signals.Initialize(State, 4096, SignalKey, 2, new() { User = M.Access, CopyToUser = M.UserCopyOut, Capacity = 3, FirmwareExited = true, SingleOwnerValidated = true }));
            if (attached)
                HostCheck.True(M.Service->EnableSignals(Key, 2, State));
            Parent = M.Start();
            HostCheck.Equal(Parent.Value, M.Kernel.Dispatch().Value);
            if (child)
                Child = M.Clone();
            *(ushort*)M.At(After - 2) = 0x050f;
            if (attached)
            {
                Action();
                Mask(0);
            }
        }

        internal void Action(ulong handler = Base + 0x180, bool restart = false)
        {
            *(Signals.Disposition*)M.At(Input) = new()
            {
                Handler = handler,
                Flags = 0x04000004UL | (restart ? 0x10000000UL : 0),
                Restorer = Base + 0x200
            };
            HostCheck.Equal(0L, Value(13, 15, Input, 0, 8));
        }

        internal void Mask(ulong mask)
        {
            M.Put64(Input, mask);
            HostCheck.Equal(0L, Value(14, 2, Input, 0, 8));
        }

        internal long Value(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            var result = M.Call(number, a0, a1, a2, a3);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            return result.Value;
        }

        internal bool Request() => M.Service->RequestTermination(Key, 2, Parent);
        internal void Supervisor() => M.Yield(M.Kernel.Table->Current);
        internal void ResumeParent()
        {
            var next = M.Kernel.Dispatch();
            if (next.Value == Child.Value && Child.IsValid)
            {
                M.Yield(Child);
                next = M.Kernel.Dispatch();
            }

            HostCheck.Equal(Parent.Value, next.Value);
        }

        internal void Futex(bool timed = false)
        {
            M.Put32(Word, 7);
            M.Put64(Timeout, 0);
            M.Put64(Timeout + 8, 100);
            HostCheck.Equal(LinuxSyscallAction.Blocked, M.Call(202, Word, 128, 7, timed ? Timeout : 0).Action);
            HostCheck.True(M.Service->CommitFutexWait(Key, 2, M.Kernel.Capture(Parent), out bool handoff) && handoff);
            M.Kernel.Confirm();
        }

        internal ulong Catch(int code = 128)
        {
            HostCheck.Equal(Signals.Delivery.Delivered, M.Service->DeliverPendingSignals(Key, 2));
            ulong frame = M.CurrentImage()->Frame.Rsp;
            HostCheck.Equal(15U, M.Read32(frame + 312));
            HostCheck.Equal(0U, M.Read32(frame + 316));
            HostCheck.Equal(unchecked((uint)code), M.Read32(frame + 320));
            if (code == 128)
                HostCheck.True(new ReadOnlySpan<byte>(M.At(frame + 324), 116).IndexOfAnyExcept((byte)0) < 0);
            else
            {
                HostCheck.Equal(101U, M.Read32(frame + 328));
                HostCheck.True(new ReadOnlySpan<byte>(M.At(frame + 332), 108).IndexOfAnyExcept((byte)0) < 0);
            }

            return frame;
        }

        internal void Return(ulong frame)
        {
            M.CurrentImage()->Frame.Rsp = frame + 8;
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, M.Call(15).Action);
        }

        public void Dispose()
        {
            try
            {
                Core.Drain();
                HostCheck.True(Signals.End(State, SignalKey, 2));
                HostCheck.True(new ReadOnlySpan<byte>(State, sizeof(Signals.State)).IndexOfAnyExcept((byte)0) < 0);
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
