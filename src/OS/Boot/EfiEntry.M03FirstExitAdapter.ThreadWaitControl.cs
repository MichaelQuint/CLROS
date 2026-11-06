#if M09_009_PROBE || M09_010_PROBE
using OS.Kernel.Boot;
using OS.Kernel.Diagnostics;
using OS.Pal;
using Waiter = OS.Kernel.Threading.WaitAddress;
using TlsOwner = OS.Kernel.Threading.ThreadLocalStorage;
using WaitProgram = OS.Kernel.Threading.ThreadWaitProgram;
using WorkerProgram = OS.Kernel.Threading.KernelThreadProgram;
using WorkerEntry = OS.Kernel.Threading.KernelThreadEntry;
using Lifecycle = OS.Kernel.Threading.KernelThreadTable;
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;

namespace OS.Boot;

#pragma warning disable 649 // Adjacent caller-owned slots are reached through their first element.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // One extra internally leased RW/NX page holds registrations and TLS
        // ownership. It never aliases the existing context/control/stack spans.
        private struct ThreadWaitControl
        {
            internal Waiter Addresses;
            internal Waiter.Slot AddressSlotA, AddressSlotB;
            internal Waiter.Registration RegistrationA, RegistrationB;
            internal Waiter.Binding AddressA, AddressB;
            internal TlsOwner Tls;
            internal TlsOwner.Slot TlsSlotA, TlsSlotB;
            internal TlsOwner.Binding TlsA, TlsB;
            internal uint NextA, NextB;
        }

    }
}
#endif
