using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class PciBarSizingProbeExecutionPlanTests
{
    private readonly struct FakePciConfigDwordOperation
    {
        public FakePciConfigDwordOperation(PciBarSizingProbeCommandKind kind, ushort registerOffset, ulong physicalAddress, uint value)
        {
            Kind = kind;
            RegisterOffset = registerOffset;
            PhysicalAddress = physicalAddress;
            Value = value;
        }

        public PciBarSizingProbeCommandKind Kind
        {
            get;
        }
        public ushort RegisterOffset
        {
            get;
        }
        public ulong PhysicalAddress
        {
            get;
        }
        public uint Value
        {
            get;
        }
    }
}
