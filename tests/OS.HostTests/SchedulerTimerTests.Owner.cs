using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Threading;
using OS.Pal;
using Timer = OS.Kernel.Threading.SchedulerTimer;

internal static unsafe partial class SchedulerTimerTests
{
    private sealed class Owner : IDisposable
    {
        public readonly Model* M = (Model*)NativeMemory.AllocZeroed((nuint)sizeof(Model));
        public readonly DeadlineQueue.Entry* Entries = (DeadlineQueue.Entry*)NativeMemory.AllocZeroed(128);
        public Owner()
        {
            M->Registers[0x320 / 4] = 0x10051;
            M->Registers[0x3e0 / 4] = 0xb;
            M->Registers[0x80 / 4] = 0xd0;
            M->Registers[0xf0 / 4] = 0x1ff;
        }

        public XApicSetupAccess Access => new()
        {
            Context = M,
            Read32 = &Read,
            Write32 = &Write
        };

        public Timer Prepare(uint maximumDeferredTicks = 3, ulong initialTick = 0)
        {
            Timer timer = default;
            HostCheck.Equal(Timer.Status.Ok, timer.Prepare(Key, Apic, Reload, maximumDeferredTicks, Access, 2, initialTick));
            return timer; // Transfer sole ownership before any physical writes.
        }

        public DeadlineQueue Queue(ulong initialTick = 0)
        {
            DeadlineQueue queue = default;
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Initialize(Key, Entries, 128, 8, initialTick));
            return queue;
        }

        public void Expire()
        {
            M->Registers[0x390 / 4] = 0;
            M->Registers[0x170 / 4] = 2;
        }

        public Timer.Result Tick(ref Timer timer, ref DeadlineQueue queue, Timer.Phase phase = Timer.Phase.Safe)
        {
            Expire();
            return timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, phase);
        }

        public void Dispose()
        {
            NativeMemory.Free(M);
            NativeMemory.Free(Entries);
        }
    }
}
