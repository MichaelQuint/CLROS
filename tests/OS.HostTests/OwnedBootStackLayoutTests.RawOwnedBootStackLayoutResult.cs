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
    private record struct RawOwnedBootStackLayoutResult
    {
        public OwnedBootStackLayoutStatus Status;
        public AddressValueStatus AddressStatus;
        public BootReservationTableStatus ReservationTableStatus;
        public BootReservedRangeStatus RangeStatus;
        public OwnedBootStackLifetime PreviousLifetime;
        public OwnedBootStackLifetime RequestedLifetime;
        public OwnedBootStackLifetime CurrentLifetime;
        public byte Reserved0;
        public ulong ReservationCount;
        public ulong ValidatedReservationCount;
        public ulong AvailableReservationCapacity;
        public ulong InvalidReservationIndex;
        public ulong ConflictingReservationIndex;
        public OwnedBootStackLayout Layout;
    }
}
