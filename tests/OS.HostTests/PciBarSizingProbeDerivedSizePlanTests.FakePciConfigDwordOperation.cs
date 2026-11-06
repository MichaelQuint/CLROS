using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class PciBarSizingProbeDerivedSizePlanTests
{
    private readonly struct FakePciConfigDwordOperation
    {
        public FakePciConfigDwordOperation(PciBarSizingProbeCommandKind kind, ushort registerOffset, uint value)
        {
            Kind = kind;
            RegisterOffset = registerOffset;
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
        public uint Value
        {
            get;
        }
    }
}
