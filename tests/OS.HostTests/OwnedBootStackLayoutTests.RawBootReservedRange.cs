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

internal static partial class OwnedBootStackLayoutTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RawBootReservedRange
    {
        public BootReservationKind Kind;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Reserved3;
        public byte Reserved4;
        public byte Reserved5;
        public byte Reserved6;
        public PhysicalAddress PhysicalStart;
        public PhysicalAddress PhysicalEndExclusive;
        public ulong PageCount;
        public ulong ByteLength;
    }
}
