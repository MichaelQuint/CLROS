using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static unsafe partial class PlatformConfigurationTableGuardTests
{
    private sealed class GuardedConfigurationTablesDouble : IPlatformConfigurationTables
    {
        private readonly IPlatformFirmware _firmware;
        private readonly byte[] _tableBytes;
        private readonly ulong _tableCount;
        private readonly bool _forceNullAddress;
        public GuardedConfigurationTablesDouble(IPlatformFirmware firmware, byte[] tableBytes, ulong tableCount, bool forceNullAddress = false)
        {
            _firmware = firmware;
            _tableBytes = tableBytes;
            _tableCount = tableCount;
            _forceNullAddress = forceNullAddress;
        }

        public PlatformConfigurationTableScanResult ScanAcpiRsdp()
        {
            FirmwareCallResult guardResult = _firmware.RequireBootServices(PlatformConfigurationTableOperations.ScanAcpiRsdp);
            if (_forceNullAddress)
            {
                return PlatformConfigurationTableScanResult.ScanAcpiRsdp(guardResult, new PlatformConfigurationTableMetadata(0, _tableCount));
            }

            fixed (byte* tablePointer = _tableBytes)
            {
                return PlatformConfigurationTableScanResult.ScanAcpiRsdp(guardResult, new PlatformConfigurationTableMetadata((ulong)tablePointer, _tableCount));
            }
        }
    }
}
