using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class ResetACKNOWLEDGEDRIVERFEATURESOKNegotiationTests
{
    private struct Device
    {
        public ulong Offered, Selected;
        public uint DeviceSelector, DriverSelector, Calls, FailCall, ResetDelay, ResetReads;
        public byte Status, RejectStatus, RejectValue;
        public bool RejectFeatures, InvalidAccess, DriverOkWritten, ResetPending, NeedsResetAfterFeatures;
        public uint StatusWrites;
        public fixed byte History[8];
    }
}
