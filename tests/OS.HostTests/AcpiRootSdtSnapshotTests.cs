using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static class AcpiRootSdtSnapshotTests
{

    [HostTest("acpi.root_sdt.snapshot reads rsdt from acpi1 rsdp", HostLane.Fast, 0)]
    private static bool TestReadsRsdtFromAcpi10Rsdp()
    {
        byte[] rsdt = CreateRsdt(0x00110000, 0x00120000);
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi10(0x00200000),
            0x00100000);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(0x00200000, rsdt));
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult result = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            physicalMemory,
            header,
            table);

        return HostCheck.Result(result.Succeeded
            && result.Status == AcpiRootSdtSnapshotStatus.Success
            && result.TableKind == AcpiSdtTableKind.Rsdt
            && result.PhysicalAddress == 0x00200000
            && result.BytesRead == rsdt.Length
            && result.RequiredBytes == rsdt.Length
            && result.DeclaredLength == rsdt.Length
            && result.EntryCount == 2
            && !result.UsedRsdtFallback
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.Success
            && result.TableSignature == AcpiSdtParser.RsdtSignature
            && physicalMemory.ReadCount == 2
            && physicalMemory.RequestedBytes[0] == AcpiSdtParser.HeaderLength
            && physicalMemory.RequestedBytes[1] == rsdt.Length
            && table[0] == (byte)'R');
    }

    [HostTest("acpi.root_sdt.snapshot prefers xsdt from acpi2 rsdp", HostLane.Fast, 1)]
    private static bool TestPrefersXsdtFromAcpi20Rsdp()
    {
        byte[] xsdt = CreateXsdt(0x0000000100100000, 0x0000000100200000);
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi20(0x00200000, 0x00300000),
            0x00100000);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(0x00200000, CreateRsdt(0x00110000)),
            new PhysicalMemoryEntry(0x00300000, xsdt));
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult result = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            physicalMemory,
            header,
            table);

        return HostCheck.Result(result.Succeeded
            && result.TableKind == AcpiSdtTableKind.Xsdt
            && result.PhysicalAddress == 0x00300000
            && result.BytesRead == xsdt.Length
            && result.EntryCount == 2
            && !result.UsedRsdtFallback
            && result.TableSignature == AcpiSdtParser.XsdtSignature
            && table[0] == (byte)'X');
    }

    [HostTest("acpi.root_sdt.snapshot falls back to rsdt when xsdt address is zero", HostLane.Fast, 2)]
    private static bool TestFallsBackToRsdtWhenXsdtMissing()
    {
        byte[] rsdt = CreateRsdt(0x00110000);
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi20(0x00200000, xsdtAddress: 0),
            0x00100000);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(0x00200000, rsdt));
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult result = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            physicalMemory,
            header,
            table);

        return HostCheck.Result(result.Succeeded
            && result.TableKind == AcpiSdtTableKind.Rsdt
            && result.PhysicalAddress == 0x00200000
            && result.EntryCount == 1
            && result.UsedRsdtFallback);
    }

    [HostTest("acpi.root_sdt.snapshot rejects invalid rsdp input", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidRsdpInput()
    {
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];
        AcpiRootSdtSnapshotResult result = AcpiRootSdtSnapshotReader.ReadAndValidate(
            AcpiRsdpSnapshotResult.InvalidPhysicalAddress(),
            PhysicalMemoryDouble.Success(),
            header,
            table);

        return HostCheck.Result(!result.Succeeded
            && result.Status == AcpiRootSdtSnapshotStatus.InvalidRsdpSnapshot
            && result.RsdpStatus == AcpiRsdpSnapshotStatus.InvalidPhysicalAddress);
    }

    [HostTest("acpi.root_sdt.snapshot rejects missing root address", HostLane.Fast, 4)]
    private static bool TestRejectsMissingRootAddress()
    {
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi20(rsdtAddress: 0, xsdtAddress: 0),
            0x00100000);
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult unavailable = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            null!,
            header,
            table);
        AcpiRootSdtSnapshotResult missing = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.Success(),
            header,
            table);

        return HostCheck.Result(!unavailable.Succeeded
            && unavailable.Status == AcpiRootSdtSnapshotStatus.PhysicalMemoryUnavailable
            && unavailable.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceUnavailable
            && !missing.Succeeded
            && missing.Status == AcpiRootSdtSnapshotStatus.NoUsableRootTableAddress);
    }

    [HostTest("acpi.root_sdt.snapshot reports scratch and destination capacity", HostLane.Fast, 5)]
    private static bool TestReportsScratchAndDestinationCapacity()
    {
        byte[] rsdt = CreateRsdt(0x00110000, 0x00120000);
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi10(0x00200000),
            0x00100000);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(0x00200000, rsdt));
        Span<byte> shortHeader = stackalloc byte[AcpiSdtParser.HeaderLength - 1];
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> shortTable = stackalloc byte[AcpiSdtParser.HeaderLength];

        AcpiRootSdtSnapshotResult scratchResult = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            physicalMemory,
            shortHeader,
            shortTable);
        AcpiRootSdtSnapshotResult destinationResult = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            physicalMemory,
            header,
            shortTable);

        return HostCheck.Result(!scratchResult.Succeeded
            && scratchResult.Status == AcpiRootSdtSnapshotStatus.ScratchTooSmall
            && scratchResult.RequiredBytes == AcpiSdtParser.HeaderLength
            && !destinationResult.Succeeded
            && destinationResult.Status == AcpiRootSdtSnapshotStatus.DestinationTooSmall
            && destinationResult.RequiredBytes == rsdt.Length
            && destinationResult.DeclaredLength == rsdt.Length);
    }

    [HostTest("acpi.root_sdt.snapshot reports physical read failures", HostLane.Fast, 6)]
    private static bool TestReportsPhysicalReadFailures()
    {
        byte[] rsdt = CreateRsdt(0x00110000);
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi10(0x00200000),
            0x00100000);
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult headerFailure = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.FailOnCall(1, PlatformPhysicalMemoryStatus.SourceFailed, new PhysicalMemoryEntry(0x00200000, rsdt)),
            header,
            table);
        AcpiRootSdtSnapshotResult fullFailure = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.FailOnCall(2, PlatformPhysicalMemoryStatus.SourceUnavailable, new PhysicalMemoryEntry(0x00200000, rsdt)),
            header,
            table);

        return HostCheck.Result(!headerFailure.Succeeded
            && headerFailure.Status == AcpiRootSdtSnapshotStatus.HeaderReadFailed
            && headerFailure.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceFailed
            && !fullFailure.Succeeded
            && fullFailure.Status == AcpiRootSdtSnapshotStatus.FullReadFailed
            && fullFailure.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceUnavailable);
    }

    [HostTest("acpi.root_sdt.snapshot rejects invalid signature length and checksum", HostLane.Fast, 7)]
    private static bool TestRejectsInvalidSignatureLengthAndChecksum()
    {
        byte[] wrongSignature = CreateTable(
            AcpiTableDirectory.McfgSignature,
            AcpiSdtParser.XsdtPointerSize,
            0x00110000);
        byte[] badLength = CreateRsdt(0x00110000);
        WriteLength(badLength, AcpiSdtParser.HeaderLength - 1);
        byte[] badChecksum = CreateRsdt(0x00110000);
        badChecksum[AcpiSdtParser.HeaderLength]++;

        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi10(0x00200000),
            0x00100000);
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult signatureResult = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(0x00200000, wrongSignature)),
            header,
            table);
        AcpiRootSdtSnapshotResult lengthResult = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(0x00200000, badLength)),
            header,
            table);
        AcpiRootSdtSnapshotResult checksumResult = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(0x00200000, badChecksum)),
            header,
            table);

        return HostCheck.Result(!signatureResult.Succeeded
            && signatureResult.Status == AcpiRootSdtSnapshotStatus.InvalidSignature
            && signatureResult.TableSignature == AcpiTableDirectory.McfgSignature
            && !lengthResult.Succeeded
            && lengthResult.Status == AcpiRootSdtSnapshotStatus.InvalidTableLength
            && lengthResult.DeclaredLength == AcpiSdtParser.HeaderLength - 1
            && !checksumResult.Succeeded
            && checksumResult.Status == AcpiRootSdtSnapshotStatus.InvalidChecksum
            && checksumResult.DeclaredLength == badChecksum.Length);
    }

    [HostTest("acpi.root_sdt.snapshot rejects invalid pointer array length", HostLane.Fast, 8)]
    private static bool TestRejectsInvalidPointerArrayLength()
    {
        byte[] invalidRsdt = CreateTableWithPayload(
            AcpiSdtParser.RsdtSignature,
            new byte[AcpiSdtParser.RsdtPointerSize - 1]);
        AcpiRsdpSnapshotResult rsdp = CreateRsdpSnapshot(
            CreateValidAcpi10(0x00200000),
            0x00100000);
        Span<byte> header = stackalloc byte[AcpiSdtParser.HeaderLength];
        Span<byte> table = stackalloc byte[128];

        AcpiRootSdtSnapshotResult result = AcpiRootSdtSnapshotReader.ReadAndValidate(
            rsdp,
            PhysicalMemoryDouble.Success(new PhysicalMemoryEntry(0x00200000, invalidRsdt)),
            header,
            table);

        return HostCheck.Result(!result.Succeeded
            && result.Status == AcpiRootSdtSnapshotStatus.InvalidPointerArrayLength
            && result.RequiredBytes == AcpiSdtParser.HeaderLength + AcpiSdtParser.RsdtPointerSize);
    }

    [HostTest("acpi.root_sdt.snapshot result contains no managed references", HostLane.Fast, 9)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<AcpiRootSdtSnapshotResult>());
    }

    private static AcpiRsdpSnapshotResult CreateRsdpSnapshot(
        byte[] rsdp,
        ulong physicalAddress)
    {
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(physicalAddress, rsdp));
        Span<byte> scratch = stackalloc byte[64];
        AcpiRsdpSnapshotResult result = AcpiRsdpSnapshotReader.ReadAndValidate(
            physicalMemory,
            physicalAddress,
            scratch);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("test RSDP fixture is invalid: " + result.Status);
        }

        return result;
    }

    private static byte[] CreateValidAcpi10(ulong rsdtAddress)
    {
        byte[] rsdp = new byte[AcpiRsdpValidator.BaseLength];
        WriteRsdpBaseFields(rsdp, revision: 0, rsdtAddress);
        SetBaseChecksum(rsdp);
        return rsdp;
    }

    private static byte[] CreateValidAcpi20(ulong rsdtAddress, ulong xsdtAddress)
    {
        byte[] rsdp = new byte[40];
        WriteRsdpBaseFields(rsdp, revision: 2, rsdtAddress);
        EndianPrimitives.WriteUInt32LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.LengthOffset, sizeof(uint)),
            (uint)rsdp.Length);
        EndianPrimitives.WriteUInt64LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.XsdtAddressOffset, sizeof(ulong)),
            xsdtAddress);
        SetBaseChecksum(rsdp);
        SetExtendedChecksum(rsdp, rsdp.Length);
        return rsdp;
    }

    private static void WriteRsdpBaseFields(byte[] rsdp, byte revision, ulong rsdtAddress)
    {
        rsdp[0] = (byte)'R';
        rsdp[1] = (byte)'S';
        rsdp[2] = (byte)'D';
        rsdp[3] = (byte)' ';
        rsdp[4] = (byte)'P';
        rsdp[5] = (byte)'T';
        rsdp[6] = (byte)'R';
        rsdp[7] = (byte)' ';
        WriteAscii(rsdp, AcpiRsdpValidator.OemIdOffset, "OSTEST");
        rsdp[AcpiRsdpValidator.RevisionOffset] = revision;
        EndianPrimitives.WriteUInt32LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.RsdtAddressOffset, sizeof(uint)),
            (uint)rsdtAddress);
    }

    private static byte[] CreateRsdt(params uint[] pointers)
    {
        byte[] table = new byte[
            AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.RsdtPointerSize)];
        WriteSdtHeader(table, AcpiSdtParser.RsdtSignature);

        for (int index = 0; index < pointers.Length; index++)
        {
            EndianPrimitives.WriteUInt32LittleEndian(
                table.AsSpan(
                    AcpiSdtParser.HeaderLength + (index * AcpiSdtParser.RsdtPointerSize),
                    sizeof(uint)),
                pointers[index]);
        }

        SetSdtChecksum(table);
        return table;
    }

    private static byte[] CreateXsdt(params ulong[] pointers)
    {
        byte[] table = new byte[
            AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.XsdtPointerSize)];
        WriteSdtHeader(table, AcpiSdtParser.XsdtSignature);

        for (int index = 0; index < pointers.Length; index++)
        {
            EndianPrimitives.WriteUInt64LittleEndian(
                table.AsSpan(
                    AcpiSdtParser.HeaderLength + (index * AcpiSdtParser.XsdtPointerSize),
                    sizeof(ulong)),
                pointers[index]);
        }

        SetSdtChecksum(table);
        return table;
    }

    private static byte[] CreateTable(uint signature, int pointerSize, ulong pointer)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + pointerSize];
        WriteSdtHeader(table, signature);
        if (pointerSize == AcpiSdtParser.XsdtPointerSize)
        {
            EndianPrimitives.WriteUInt64LittleEndian(
                table.AsSpan(AcpiSdtParser.HeaderLength, sizeof(ulong)),
                pointer);
        }
        else
        {
            EndianPrimitives.WriteUInt32LittleEndian(
                table.AsSpan(AcpiSdtParser.HeaderLength, sizeof(uint)),
                (uint)pointer);
        }

        SetSdtChecksum(table);
        return table;
    }

    private static byte[] CreateTableWithPayload(uint signature, byte[] payload)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + payload.Length];
        WriteSdtHeader(table, signature);
        payload.CopyTo(table.AsSpan(AcpiSdtParser.HeaderLength));
        SetSdtChecksum(table);
        return table;
    }

    private static void WriteSdtHeader(byte[] table, uint signature)
    {
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.SignatureOffset, sizeof(uint)),
            signature);
        WriteLength(table, (uint)table.Length);
        table[AcpiSdtParser.RevisionOffset] = 1;
        WriteAscii(table, AcpiSdtParser.OemIdOffset, "OSTEST");
        WriteAscii(table, AcpiSdtParser.OemTableIdOffset, "ROOTTEST");
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.OemRevisionOffset, sizeof(uint)),
            1);
        WriteAscii(table, AcpiSdtParser.CreatorIdOffset, "OS  ");
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.CreatorRevisionOffset, sizeof(uint)),
            1);
    }

    private static void WriteLength(byte[] table, uint length)
    {
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.LengthOffset, sizeof(uint)),
            length);
    }

    private static void WriteAscii(byte[] destination, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }
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

    private static void SetSdtChecksum(byte[] table)
    {
        table[AcpiSdtParser.ChecksumOffset] = 0;
        table[AcpiSdtParser.ChecksumOffset] = CreateChecksum(table);
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

    private readonly struct PhysicalMemoryEntry
    {
        public PhysicalMemoryEntry(ulong physicalAddress, byte[] bytes)
        {
            PhysicalAddress = physicalAddress;
            Bytes = bytes;
        }

        public ulong PhysicalAddress { get; }

        public byte[] Bytes { get; }
    }

    private sealed class PhysicalMemoryDouble : IPlatformPhysicalMemory
    {
        private readonly PhysicalMemoryEntry[] _entries;
        private readonly int _failOnCall;
        private readonly PlatformPhysicalMemoryStatus _failureStatus;

        private PhysicalMemoryDouble(
            PhysicalMemoryEntry[] entries,
            int failOnCall,
            PlatformPhysicalMemoryStatus failureStatus)
        {
            _entries = entries;
            _failOnCall = failOnCall;
            _failureStatus = failureStatus;
        }

        public int ReadCount { get; private set; }

        public int[] RequestedBytes { get; } = new int[8];

        public static PhysicalMemoryDouble Success(params PhysicalMemoryEntry[] entries)
        {
            return new PhysicalMemoryDouble(
                entries,
                failOnCall: -1,
                PlatformPhysicalMemoryStatus.Success);
        }

        public static PhysicalMemoryDouble FailOnCall(
            int failOnCall,
            PlatformPhysicalMemoryStatus failureStatus,
            params PhysicalMemoryEntry[] entries)
        {
            return new PhysicalMemoryDouble(entries, failOnCall, failureStatus);
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

            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].PhysicalAddress != physicalAddress)
                {
                    continue;
                }

                int copyLength = Math.Min(byteCount, _entries[index].Bytes.Length);
                _entries[index].Bytes.AsSpan(0, copyLength).CopyTo(destination);
                return PlatformPhysicalMemoryResult.Read(
                    guard,
                    PlatformPhysicalMemorySourceResult.Read(copyLength),
                    physicalAddress,
                    byteCount);
            }

            return PlatformPhysicalMemoryResult.Read(
                guard,
                PlatformPhysicalMemorySourceResult.Unavailable(),
                physicalAddress,
                byteCount);
        }
    }
}
