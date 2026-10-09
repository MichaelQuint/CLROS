using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static class AcpiRsdpSnapshotTests
{

    [HostTest("acpi.rsdp.snapshot reads acpi1 base", HostLane.Fast, 0)]
    private static bool TestReadsAcpi10Base()
    {
        byte[] rsdp = CreateValidAcpi10();
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(rsdp);
        Span<byte> scratch = stackalloc byte[AcpiRsdpValidator.ExtendedHeaderLength];

        AcpiRsdpSnapshotResult result = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x100000,
            scratch);

        return HostCheck.Result(result.Succeeded
            && result.Status == AcpiRsdpSnapshotStatus.Success
            && result.PhysicalAddress == 0x100000
            && result.BytesRead == AcpiRsdpValidator.BaseLength
            && result.RequiredBytes == AcpiRsdpValidator.BaseLength
            && !result.ExtendedReadAttempted
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.Success
            && result.ValidationStatus == AcpiRsdpValidationStatus.Success
            && result.RevisionKind == AcpiRsdpRevisionKind.Acpi10
            && result.Revision == 0
            && result.DeclaredLength == AcpiRsdpValidator.BaseLength
            && result.RsdtAddress == 0x00100000
            && result.XsdtAddress == 0
            && physicalMemory.ReadCount == 1
            && physicalMemory.RequestedBytes[0] == AcpiRsdpValidator.BaseLength
            && scratch[0] == (byte)'R'
            && scratch[7] == (byte)' ');
    }

    [HostTest("acpi.rsdp.snapshot rereads acpi2 extended length", HostLane.Fast, 1)]
    private static bool TestReadsAcpi20ExtendedLength()
    {
        byte[] rsdp = CreateValidAcpi20(length: 40);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(rsdp);
        Span<byte> scratch = stackalloc byte[64];

        AcpiRsdpSnapshotResult result = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x200000,
            scratch);

        return HostCheck.Result(result.Succeeded
            && result.Status == AcpiRsdpSnapshotStatus.Success
            && result.PhysicalAddress == 0x200000
            && result.BytesRead == 40
            && result.RequiredBytes == 40
            && result.ExtendedReadAttempted
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.Success
            && result.ValidationStatus == AcpiRsdpValidationStatus.Success
            && result.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater
            && result.Revision == 2
            && result.DeclaredLength == 40
            && result.RsdtAddress == 0x00100000
            && result.XsdtAddress == 0x0000000000300000
            && physicalMemory.ReadCount == 3
            && physicalMemory.RequestedBytes[0] == AcpiRsdpValidator.BaseLength
            && physicalMemory.RequestedBytes[1] == AcpiRsdpValidator.ExtendedHeaderLength
            && physicalMemory.RequestedBytes[2] == 40
            && scratch[39] == rsdp[39]);
    }

    [HostTest("acpi.rsdp.snapshot reports scratch capacity", HostLane.Fast, 2)]
    private static bool TestReportsScratchCapacity()
    {
        byte[] rsdp = CreateValidAcpi20(length: 40);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(rsdp);
        Span<byte> tooSmallForBase = stackalloc byte[AcpiRsdpValidator.BaseLength - 1];
        Span<byte> tooSmallForHeader = stackalloc byte[AcpiRsdpValidator.BaseLength];
        Span<byte> tooSmallForExtended = stackalloc byte[AcpiRsdpValidator.ExtendedHeaderLength];

        AcpiRsdpSnapshotResult invalidPointer = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0,
            tooSmallForBase);
        AcpiRsdpSnapshotResult baseCapacity = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x200000,
            tooSmallForBase);
        AcpiRsdpSnapshotResult unavailableMemory = AcpiRsdpSnapshotReader.ReadAndValidate(
            null!,
            0x200000,
            tooSmallForHeader);
        AcpiRsdpSnapshotResult headerCapacity = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x200000,
            tooSmallForHeader);
        AcpiRsdpSnapshotResult extendedCapacity = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x200000,
            tooSmallForExtended);

        return HostCheck.Result(!invalidPointer.Succeeded
            && invalidPointer.Status == AcpiRsdpSnapshotStatus.InvalidPhysicalAddress
            && !baseCapacity.Succeeded
            && baseCapacity.Status == AcpiRsdpSnapshotStatus.ScratchTooSmall
            && baseCapacity.RequiredBytes == AcpiRsdpValidator.BaseLength
            && !unavailableMemory.Succeeded
            && unavailableMemory.Status == AcpiRsdpSnapshotStatus.PhysicalMemoryUnavailable
            && unavailableMemory.PhysicalAddress == 0x200000
            && unavailableMemory.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceUnavailable
            && !headerCapacity.Succeeded
            && headerCapacity.Status == AcpiRsdpSnapshotStatus.ScratchTooSmall
            && headerCapacity.RequiredBytes == AcpiRsdpValidator.ExtendedHeaderLength
            && !extendedCapacity.Succeeded
            && extendedCapacity.Status == AcpiRsdpSnapshotStatus.ScratchTooSmall
            && extendedCapacity.RequiredBytes == 40);
    }

    [HostTest("acpi.rsdp.snapshot reports physical read failure", HostLane.Fast, 3)]
    private static bool TestReportsPhysicalReadFailure()
    {
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.FailOnCall(
            Array.Empty<byte>(),
            failOnCall: 1,
            PlatformPhysicalMemoryStatus.SourceFailed);
        Span<byte> scratch = stackalloc byte[64];

        AcpiRsdpSnapshotResult result = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x300000,
            scratch);

        return HostCheck.Result(!result.Succeeded
            && result.Status == AcpiRsdpSnapshotStatus.PhysicalReadFailed
            && result.PhysicalAddress == 0x300000
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceFailed
            && result.BytesRead == 0
            && result.RequiredBytes == AcpiRsdpValidator.BaseLength
            && !result.ExtendedReadAttempted
            && physicalMemory.ReadCount == 1);
    }

    [HostTest("acpi.rsdp.snapshot reports extended read failure", HostLane.Fast, 4)]
    private static bool TestReportsExtendedReadFailure()
    {
        byte[] rsdp = CreateValidAcpi20(length: 40);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.FailOnCall(
            rsdp,
            failOnCall: 2,
            PlatformPhysicalMemoryStatus.SourceUnavailable);
        Span<byte> scratch = stackalloc byte[64];

        AcpiRsdpSnapshotResult result = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            0x300000,
            scratch);

        return HostCheck.Result(!result.Succeeded
            && result.Status == AcpiRsdpSnapshotStatus.ExtendedReadFailed
            && result.PhysicalAddress == 0x300000
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceUnavailable
            && result.ExtendedReadAttempted
            && physicalMemory.ReadCount == 2);
    }

    [HostTest("acpi.rsdp.snapshot reports validation failure", HostLane.Fast, 5)]
    private static bool TestReportsValidationFailure()
    {
        byte[] invalidBase = CreateValidAcpi10();
        invalidBase[0] = (byte)'X';

        byte[] invalidExtended = CreateValidAcpi20(length: 40);
        invalidExtended[39]++;

        PhysicalMemoryDouble invalidBaseMemory = PhysicalMemoryDouble.Success(invalidBase);
        PhysicalMemoryDouble invalidExtendedMemory = PhysicalMemoryDouble.Success(invalidExtended);
        Span<byte> scratch = stackalloc byte[64];

        AcpiRsdpSnapshotResult baseResult = AcpiRsdpSnapshotReader.ReadAndValidate(
            invalidBaseMemory,
            0x400000,
            scratch);
        scratch.Clear();
        AcpiRsdpSnapshotResult extendedResult = AcpiRsdpSnapshotReader.ReadAndValidate(
            invalidExtendedMemory,
            0x500000,
            scratch);

        return HostCheck.Result(!baseResult.Succeeded
            && baseResult.Status == AcpiRsdpSnapshotStatus.BaseValidationFailed
            && baseResult.ValidationStatus == AcpiRsdpValidationStatus.InvalidSignature
            && !baseResult.ExtendedReadAttempted
            && !extendedResult.Succeeded
            && extendedResult.Status == AcpiRsdpSnapshotStatus.ExtendedValidationFailed
            && extendedResult.ValidationStatus == AcpiRsdpValidationStatus.InvalidExtendedChecksum
            && extendedResult.ExtendedReadAttempted);
    }

    [HostTest("acpi.rsdp.snapshot result contains no managed references", HostLane.Fast, 6)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<AcpiRsdpSnapshotResult>());
    }

    private static byte[] CreateValidAcpi10()
    {
        byte[] rsdp = new byte[AcpiRsdpValidator.BaseLength];
        WriteBaseFields(rsdp, revision: 0);
        SetBaseChecksum(rsdp);
        return rsdp;
    }

    private static byte[] CreateValidAcpi20(int length)
    {
        byte[] rsdp = new byte[length];
        WriteBaseFields(rsdp, revision: 2);
        EndianPrimitives.WriteUInt32LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.LengthOffset, sizeof(uint)),
            (uint)length);
        EndianPrimitives.WriteUInt64LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.XsdtAddressOffset, sizeof(ulong)),
            0x0000000000300000);

        for (int index = AcpiRsdpValidator.ExtendedHeaderLength; index < rsdp.Length; index++)
        {
            rsdp[index] = (byte)(0xa0 + index);
        }

        SetBaseChecksum(rsdp);
        SetExtendedChecksum(rsdp, length);
        return rsdp;
    }

    private static void WriteBaseFields(byte[] rsdp, byte revision)
    {
        rsdp[0] = (byte)'R';
        rsdp[1] = (byte)'S';
        rsdp[2] = (byte)'D';
        rsdp[3] = (byte)' ';
        rsdp[4] = (byte)'P';
        rsdp[5] = (byte)'T';
        rsdp[6] = (byte)'R';
        rsdp[7] = (byte)' ';
        rsdp[AcpiRsdpValidator.OemIdOffset] = (byte)'O';
        rsdp[AcpiRsdpValidator.OemIdOffset + 1] = (byte)'S';
        rsdp[AcpiRsdpValidator.OemIdOffset + 2] = (byte)'T';
        rsdp[AcpiRsdpValidator.OemIdOffset + 3] = (byte)'E';
        rsdp[AcpiRsdpValidator.OemIdOffset + 4] = (byte)'S';
        rsdp[AcpiRsdpValidator.OemIdOffset + 5] = (byte)'T';
        rsdp[AcpiRsdpValidator.RevisionOffset] = revision;
        EndianPrimitives.WriteUInt32LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.RsdtAddressOffset, sizeof(uint)),
            0x00100000);
    }

    private static void SetBaseChecksum(byte[] rsdp)
    {
        rsdp[AcpiRsdpValidator.ChecksumOffset] = 0;
        rsdp[AcpiRsdpValidator.ChecksumOffset] = CreateChecksum(
            rsdp.AsSpan(0, AcpiRsdpValidator.BaseLength));
    }

    private static void SetExtendedChecksum(byte[] rsdp, int length)
    {
        rsdp[AcpiRsdpValidator.ExtendedChecksumOffset] = 0;
        rsdp[AcpiRsdpValidator.ExtendedChecksumOffset] = CreateChecksum(
            rsdp.AsSpan(0, length));
    }

    private static byte CreateChecksum(ReadOnlySpan<byte> source)
    {
        uint sum = 0;
        for (int index = 0; index < source.Length; index++)
        {
            sum += source[index];
        }

        return unchecked((byte)(0 - sum));
    }

    private sealed class PhysicalMemoryDouble : IPlatformPhysicalMemory
    {
        private readonly byte[] _source;
        private readonly int _failOnCall;
        private readonly PlatformPhysicalMemoryStatus _failureStatus;

        private PhysicalMemoryDouble(
            byte[] source,
            int failOnCall,
            PlatformPhysicalMemoryStatus failureStatus)
        {
            _source = source;
            _failOnCall = failOnCall;
            _failureStatus = failureStatus;
        }

        public int ReadCount { get; private set; }

        public int[] RequestedBytes { get; } = new int[8];

        public static PhysicalMemoryDouble Success(byte[] source)
        {
            return new PhysicalMemoryDouble(
                source,
                failOnCall: -1,
                PlatformPhysicalMemoryStatus.Success);
        }

        public static PhysicalMemoryDouble FailOnCall(
            byte[] source,
            int failOnCall,
            PlatformPhysicalMemoryStatus failureStatus)
        {
            return new PhysicalMemoryDouble(source, failOnCall, failureStatus);
        }

        public PlatformPhysicalMemoryResult ReadBytes(
            ulong physicalAddress,
            int byteCount,
            Span<byte> destination)
        {
            ReadCount++;
            RequestedBytes[ReadCount - 1] = byteCount;

            FirmwareCallResult guard = FirmwareCallResult.Allowed(
                PlatformPhysicalMemoryOperations.ReadBytes,
                FirmwareServicesMode.BootServicesAvailable);

            if (ReadCount == _failOnCall)
            {
                PlatformPhysicalMemorySourceResult failedSource =
                    _failureStatus == PlatformPhysicalMemoryStatus.SourceUnavailable
                        ? PlatformPhysicalMemorySourceResult.Unavailable()
                        : PlatformPhysicalMemorySourceResult.Fault(0x8000000000000009);

                return PlatformPhysicalMemoryResult.Read(
                    guard,
                    failedSource,
                    physicalAddress,
                    byteCount);
            }

            int copyLength = Math.Min(byteCount, _source.Length);
            _source.AsSpan(0, copyLength).CopyTo(destination);

            return PlatformPhysicalMemoryResult.Read(
                guard,
                PlatformPhysicalMemorySourceResult.Read(copyLength),
                physicalAddress,
                byteCount);
        }
    }
}
