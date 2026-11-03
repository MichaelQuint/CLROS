using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Nic = OS.Drivers.ContinuingVirtioNetDevice;

internal static unsafe partial class ContinuingVirtioNetDeviceTests
{
    private struct Hardware
    {
        internal byte* Data;
        internal DmaBufferPool* Pool;
        internal Registers Rx, Tx;
        internal ulong Now;
        internal uint Calls, RxNotifies, TxNotifies, DriverOkWrites, Resets;
        internal ushort Selected, ConfigVector;
        internal byte Status;
        internal bool BusEnabled, Primed, HeldAtResetFence, DisableFailure, ResetWriteFailure, WrongReadback, NotifyFailure, ClockFailure, ResetFenceFailure;
    }
}
