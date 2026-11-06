using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static partial class PciVirtioDescriptorCatalogPlannerTests
{
    private sealed class FakePciConfigDwordAccessor : IPciConfigDwordAccessor
    {
        private readonly uint[] _defaultReadbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        private readonly uint[] _deviceReadbacks = new uint[(PciEcamConfigurationSpace.MaximumDevice + 1) * PciBarSizingProbeExecutor.RequiredReadbackCount];
        private readonly bool[] _hasDeviceReadback = new bool[(PciEcamConfigurationSpace.MaximumDevice + 1) * PciBarSizingProbeExecutor.RequiredReadbackCount];
        public void SetReadback(ushort registerOffset, uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            _defaultReadbacks[barIndex] = value;
        }

        public void SetReadback(byte device, ushort registerOffset, uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            int index = device * PciBarSizingProbeExecutor.RequiredReadbackCount + barIndex;
            _deviceReadbacks[index] = value;
            _hasDeviceReadback[index] = true;
        }

        public PciConfigDwordAccessStatus Write32(PciEcamConfigurationAddressResult address, uint value)
        {
            return PciConfigDwordAccessStatus.Success;
        }

        public PciConfigDwordReadResult Read32(PciEcamConfigurationAddressResult address)
        {
            byte barIndex = (byte)((address.RegisterOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) / PciBarSizingProbePlanner.BarRegisterStride);
            int index = address.Device * PciBarSizingProbeExecutor.RequiredReadbackCount + barIndex;
            uint value = _hasDeviceReadback[index] ? _deviceReadbacks[index] : _defaultReadbacks[barIndex];
            return new PciConfigDwordReadResult(PciConfigDwordAccessStatus.Success, value);
        }
    }
}
