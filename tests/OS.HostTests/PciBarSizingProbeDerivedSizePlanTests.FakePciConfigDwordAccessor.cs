using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class PciBarSizingProbeDerivedSizePlanTests
{
    private sealed class FakePciConfigDwordAccessor : IPciConfigDwordAccessor
    {
        private readonly uint[] _readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        public FakePciConfigDwordOperation[] Operations { get; } = new FakePciConfigDwordOperation[PciBarSizingProbePlanner.MaximumCommandCount];
        public int OperationCount
        {
            get; private set;
        }
        public int FailReadAtOperation { get; set; } = -1;

        public void SetReadback(ushort registerOffset, uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            _readbacks[barIndex] = value;
        }

        public PciConfigDwordAccessStatus Write32(PciEcamConfigurationAddressResult address, uint value)
        {
            PciBarSizingProbeCommandKind kind = value == uint.MaxValue ? PciBarSizingProbeCommandKind.WriteAllOnes : PciBarSizingProbeCommandKind.RestoreOriginal;
            Operations[OperationCount] = new FakePciConfigDwordOperation(kind, address.RegisterOffset, value);
            OperationCount++;
            return PciConfigDwordAccessStatus.Success;
        }

        public PciConfigDwordReadResult Read32(PciEcamConfigurationAddressResult address)
        {
            byte barIndex = (byte)((address.RegisterOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            uint value = _readbacks[barIndex];
            Operations[OperationCount] = new FakePciConfigDwordOperation(PciBarSizingProbeCommandKind.ReadSizingReadback, address.RegisterOffset, value);
            OperationCount++;
            return OperationCount - 1 == FailReadAtOperation ? new PciConfigDwordReadResult(PciConfigDwordAccessStatus.AccessFailed, value: 0) : new PciConfigDwordReadResult(PciConfigDwordAccessStatus.Success, value);
        }
    }
}
