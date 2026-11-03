using System.Runtime.CompilerServices;
using System.Reflection;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetTransmitCompletionPlanTests
{
    private sealed class FakeNotifyWriteSink : IVirtioPciNotifyWriteSink
    {
        public VirtioPciNotifyWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value)
        {
            _ = aperture;
            _ = relativeOffset;
            _ = value;
            return VirtioPciNotifyWriteStatus.Success;
        }
    }
}
