using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class VirtioPciBindingDerivedBarSizePlanTests
{
    private sealed class FakePciConfigDwordAccessor : IPciConfigDwordAccessor
    {
        private readonly uint[] _readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        public int OperationCount { get; private set; }

        public void SetReadback(ushort registerOffset, uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            _readbacks[barIndex] = value;
        }

        public PciConfigDwordAccessStatus Write32(PciEcamConfigurationAddressResult address, uint value)
        {
            OperationCount++;
            return PciConfigDwordAccessStatus.Success;
        }

        public PciConfigDwordReadResult Read32(PciEcamConfigurationAddressResult address)
        {
            byte barIndex = (byte)((address.RegisterOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            OperationCount++;
            return new PciConfigDwordReadResult(PciConfigDwordAccessStatus.Success, _readbacks[barIndex]);
        }
    }
}
