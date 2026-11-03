using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Model = LinuxThreadServicesTests.Model;

internal static unsafe partial class LinuxSignalServicesTests
{
    internal sealed class Owner : IDisposable
    {
        internal readonly Model Thread;
        internal Signals.State* State = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
        internal Signals.Admission Plan => new()
        {
            Capacity = Thread.Capacity,
            User = Thread.Access,
            CopyToUser = Thread.UserCopyOut,
            FirmwareExited = true,
            SingleOwnerValidated = true
        };

        internal Owner(bool attach = true, bool userInterrupts = false, uint capacity = 3)
        {
            Thread = new(futex: true, userInterrupts: userInterrupts, capacity: capacity);
            HostCheck.True(State != null && sizeof(Signals.State) <= 4096);
            new Span<byte>(State, 4096).Clear();
            HostCheck.True(Signals.Initialize(State, 4096, SignalKey, 2, Plan));
            if (attach)
                HostCheck.True(Thread.Service->EnableSignals(Key, 2, State));
        }

        internal long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            var r = Thread.Call(number, a0, a1, a2, a3);
            HostCheck.Equal(LinuxSyscallAction.Return, r.Action);
            return r.Value;
        }

        internal void Running()
        {
            var first = Thread.Start();
            HostCheck.Equal(first.Value, Thread.Kernel.Dispatch().Value);
        }

        internal void Action(ulong handler, ulong flags = 0, ulong mask = 0)
        {
            *(Signals.Disposition*)Thread.At(Input) = new()
            {
                Handler = handler,
                Flags = flags,
                Restorer = Base + 0x200,
                Mask = mask
            };
        }

        internal void Stack(ulong address, ulong bytes, uint flags = 0)
        {
            *(Signals.AlternateStack*)Thread.At(Input) = new()
            {
                Address = address,
                Bytes = bytes,
                Flags = flags,
                Padding = uint.MaxValue
            };
        }

        internal Signals.AlternateStack ReadStack()
        {
            HostCheck.Equal(0L, Call(131, 0, Output));
            return *(Signals.AlternateStack*)Thread.At(Output);
        }

        internal void ExitCurrent()
        {
            var identity = Thread.Kernel.Table->Current;
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, Thread.Call(60).Action);
            HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(identity), out long clear) && clear == 0);
            Thread.Kernel.Confirm();
            HostCheck.True(Thread.Service->Reap(Key, 2, identity, Thread.Kernel.SupervisorRsp, out _));
        }

        public void Dispose()
        {
            try
            {
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 42));
                while (Thread.Service->ActiveThreads != 0)
                {
                    var identity = Thread.Kernel.Table->Current;
                    if (!identity.IsValid)
                        identity = Thread.Kernel.Dispatch();
                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(identity), out long clear) && clear == 0);
                    Thread.Kernel.Confirm();
                    HostCheck.True(Thread.Service->Reap(Key, 2, identity, Thread.Kernel.SupervisorRsp, out _));
                }

                HostCheck.True(Thread.Service->Release(Key, 2));
                HostCheck.True(Signals.End(State, SignalKey, 2));
                HostCheck.True(new ReadOnlySpan<byte>(State, sizeof(Signals.State)).IndexOfAnyExcept((byte)0) < 0);
            }
            finally
            {
                NativeMemory.AlignedFree(State);
                State = null;
                Thread.Dispose();
            }
        }
    }
}
