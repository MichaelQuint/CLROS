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

internal static partial class BootReservationTableValidatorTests
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct RawBootReservedRange
    {
        [FieldOffset(0)]
        public BootReservationKind Kind;
        [FieldOffset(1)]
        public byte Reserved0;
        [FieldOffset(2)]
        public byte Reserved1;
        [FieldOffset(3)]
        public byte Reserved2;
        [FieldOffset(4)]
        public byte Reserved3;
        [FieldOffset(5)]
        public byte Reserved4;
        [FieldOffset(6)]
        public byte Reserved5;
        [FieldOffset(7)]
        public byte Reserved6;
        [FieldOffset(8)]
        public PhysicalAddress PhysicalStart;
        [FieldOffset(16)]
        public PhysicalAddress PhysicalEndExclusive;
        [FieldOffset(24)]
        public ulong PageCount;
        [FieldOffset(32)]
        public ulong ByteLength;
    }
}
