using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class EfiLoadedImageRangeCapturePlanTests
{
    [StructLayout(LayoutKind.Sequential)]
    private record struct RawCapturePlan
    {
        public EfiLoadedImageRangeCaptureStatus Status;
        public AddressValueStatus AddressStatus;
        public BootReservedRangeStatus ReservationStatus;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
        public byte Reserved4;
        public ulong ObservationCount;
        public ulong ValidatedObservationCount;
        public ulong AvailableObservationCapacity;
        public ulong InvalidObservationIndex;
        public ulong PageSize;
        public ulong FirmwareStatus;
        public ulong ImageHandle;
        public EfiLoadedImageRange Range;
    }
}
