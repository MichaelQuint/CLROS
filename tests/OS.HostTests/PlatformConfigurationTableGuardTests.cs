using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static unsafe class PlatformConfigurationTableGuardTests
{

    [HostTest("platform.config acpi scan allowed before UEFI gone", HostLane.Fast, 0)]
    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        IPlatformConfigurationTables configurationTables = new GuardedConfigurationTablesDouble(
            firmware,
            CreateTableBytes(
            [
                new EfiConfigurationTableEntry(EfiConfigurationTableScanner.Acpi10TableGuid, 0x1000),
                new EfiConfigurationTableEntry(EfiConfigurationTableScanner.Acpi20TableGuid, 0x2000)
            ]),
            tableCount: 2);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp
            && result.Metadata.TableAddress != 0
            && result.Metadata.TableCount == 2
            && result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.Ok
            && result.TableKind == EfiAcpiTableKind.Acpi20
            && result.RsdpAddress == 0x2000);
    }

    [HostTest("platform.config acpi scan denied in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        IPlatformConfigurationTables configurationTables = CreateSingleAcpi20Table(firmware);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp
            && !result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.NotFound
            && result.TableKind == EfiAcpiTableKind.None
            && result.RsdpAddress == 0);
    }

    [HostTest("platform.config acpi scan denied after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        IPlatformConfigurationTables configurationTables = CreateSingleAcpi20Table(firmware);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && !result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.NotFound
            && result.TableKind == EfiAcpiTableKind.None
            && result.RsdpAddress == 0);
    }

    [HostTest("platform.config acpi scan reports invalid metadata", HostLane.Fast, 3)]
    private static bool TestInvalidMetadataStatus()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        IPlatformConfigurationTables configurationTables = new GuardedConfigurationTablesDouble(
            firmware,
            tableBytes: [],
            tableCount: 1,
            forceNullAddress: true);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.AcpiRsdpScanFailed
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Metadata.TableAddress == 0
            && result.Metadata.TableCount == 1
            && result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress
            && result.TableKind == EfiAcpiTableKind.None
            && result.RsdpAddress == 0);
    }

    [HostTest("platform.config acpi2 preferred", HostLane.Fast, 4)]
    private static bool TestAcpi20Preferred()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        IPlatformConfigurationTables configurationTables = new GuardedConfigurationTablesDouble(
            firmware,
            CreateTableBytes(
            [
                new EfiConfigurationTableEntry(EfiConfigurationTableScanner.Acpi10TableGuid, 0x3000),
                new EfiConfigurationTableEntry(CreateOtherGuid(), 0x4000),
                new EfiConfigurationTableEntry(EfiConfigurationTableScanner.Acpi20TableGuid, 0x5000)
            ]),
            tableCount: 3);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.Success
            && result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.Ok
            && result.TableKind == EfiAcpiTableKind.Acpi20
            && result.RsdpAddress == 0x5000);
    }

    [HostTest("platform.config acpi1 fallback", HostLane.Fast, 5)]
    private static bool TestAcpi10Fallback()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        IPlatformConfigurationTables configurationTables = new GuardedConfigurationTablesDouble(
            firmware,
            CreateTableBytes(
            [
                new EfiConfigurationTableEntry(CreateOtherGuid(), 0x6000),
                new EfiConfigurationTableEntry(EfiConfigurationTableScanner.Acpi10TableGuid, 0x7000)
            ]),
            tableCount: 2);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.Success
            && result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.Ok
            && result.TableKind == EfiAcpiTableKind.Acpi10
            && result.RsdpAddress == 0x7000);
    }

    [HostTest("platform.config acpi missing", HostLane.Fast, 6)]
    private static bool TestAcpiMissing()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        IPlatformConfigurationTables configurationTables = new GuardedConfigurationTablesDouble(
            firmware,
            CreateTableBytes(
            [
                new EfiConfigurationTableEntry(CreateOtherGuid(), 0x8000)
            ]),
            tableCount: 1);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.AcpiRsdpScanFailed
            && result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.NotFound
            && result.TableKind == EfiAcpiTableKind.None
            && result.RsdpAddress == 0);
    }

    [HostTest("platform.config metadata contains no managed references", HostLane.Fast, 7)]
    private static bool TestMetadataContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformConfigurationTableMetadata>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformConfigurationTableScanResult>());
    }

    private static IPlatformConfigurationTables CreateSingleAcpi20Table(IPlatformFirmware firmware)
    {
        return new GuardedConfigurationTablesDouble(
            firmware,
            CreateTableBytes(
            [
                new EfiConfigurationTableEntry(EfiConfigurationTableScanner.Acpi20TableGuid, 0x9000)
            ]),
            tableCount: 1);
    }

    private static byte[] CreateTableBytes(EfiConfigurationTableEntry[] entries)
    {
        byte[] tableBytes = new byte[entries.Length * (int)EfiConfigurationTableScanner.EntrySize];

        for (int index = 0; index < entries.Length; index++)
        {
            WriteEntry(
                tableBytes.AsSpan(
                    index * (int)EfiConfigurationTableScanner.EntrySize,
                    (int)EfiConfigurationTableScanner.EntrySize),
                entries[index]);
        }

        return tableBytes;
    }

    private static EfiConfigurationTableGuid CreateOtherGuid()
    {
        return new EfiConfigurationTableGuid(
            0x11111111,
            0x2222,
            0x3333,
            0x44,
            0x55,
            0x66,
            0x77,
            0x88,
            0x99,
            0xaa,
            0xbb);
    }

    private static void WriteEntry(Span<byte> destination, EfiConfigurationTableEntry entry)
    {
        WriteGuid(destination[..16], entry.VendorGuid);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), entry.VendorTable);
    }

    private static void WriteGuid(Span<byte> destination, EfiConfigurationTableGuid value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), value.Data1);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), value.Data2);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), value.Data3);
        destination[8] = value.Data4;
        destination[9] = value.Data5;
        destination[10] = value.Data6;
        destination[11] = value.Data7;
        destination[12] = value.Data8;
        destination[13] = value.Data9;
        destination[14] = value.Data10;
        destination[15] = value.Data11;
    }

    private sealed class GuardedConfigurationTablesDouble : IPlatformConfigurationTables
    {
        private readonly IPlatformFirmware _firmware;
        private readonly byte[] _tableBytes;
        private readonly ulong _tableCount;
        private readonly bool _forceNullAddress;

        public GuardedConfigurationTablesDouble(
            IPlatformFirmware firmware,
            byte[] tableBytes,
            ulong tableCount,
            bool forceNullAddress = false)
        {
            _firmware = firmware;
            _tableBytes = tableBytes;
            _tableCount = tableCount;
            _forceNullAddress = forceNullAddress;
        }

        public PlatformConfigurationTableScanResult ScanAcpiRsdp()
        {
            FirmwareCallResult guardResult = _firmware.RequireBootServices(
                PlatformConfigurationTableOperations.ScanAcpiRsdp);

            if (_forceNullAddress)
            {
                return PlatformConfigurationTableScanResult.ScanAcpiRsdp(
                    guardResult,
                    new PlatformConfigurationTableMetadata(0, _tableCount));
            }

            fixed (byte* tablePointer = _tableBytes)
            {
                return PlatformConfigurationTableScanResult.ScanAcpiRsdp(
                    guardResult,
                    new PlatformConfigurationTableMetadata((ulong)tablePointer, _tableCount));
            }
        }
    }
}
