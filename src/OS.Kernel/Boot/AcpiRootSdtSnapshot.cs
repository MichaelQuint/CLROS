using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiRootSdtSnapshotStatus
{
    Success = 0,
    InvalidRsdpSnapshot = 1,
    PhysicalMemoryUnavailable = 2,
    NoUsableRootTableAddress = 3,
    ScratchTooSmall = 4,
    DestinationTooSmall = 5,
    HeaderReadFailed = 6,
    FullReadFailed = 7,
    InvalidSignature = 8,
    InvalidTableLength = 9,
    InvalidPointerArrayLength = 10,
    InvalidChecksum = 11
}

public readonly struct AcpiRootSdtSnapshotResult
{
    private AcpiRootSdtSnapshotResult(
        AcpiRootSdtSnapshotStatus status,
        AcpiSdtTableKind tableKind,
        ulong physicalAddress,
        int bytesRead,
        int requiredBytes,
        uint declaredLength,
        int entryCount,
        bool usedRsdtFallback,
        PlatformPhysicalMemoryStatus physicalReadStatus,
        AcpiRsdpSnapshotStatus rsdpStatus,
        uint tableSignature)
    {
        Status = status;
        TableKind = tableKind;
        PhysicalAddress = physicalAddress;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        DeclaredLength = declaredLength;
        EntryCount = entryCount;
        UsedRsdtFallback = usedRsdtFallback;
        PhysicalReadStatus = physicalReadStatus;
        RsdpStatus = rsdpStatus;
        TableSignature = tableSignature;
    }

    public AcpiRootSdtSnapshotStatus Status { get; }

    public bool Succeeded => Status == AcpiRootSdtSnapshotStatus.Success;

    public AcpiSdtTableKind TableKind { get; }

    public ulong PhysicalAddress { get; }

    public int BytesRead { get; }

    public int RequiredBytes { get; }

    public uint DeclaredLength { get; }

    public int EntryCount { get; }

    public bool UsedRsdtFallback { get; }

    public PlatformPhysicalMemoryStatus PhysicalReadStatus { get; }

    public AcpiRsdpSnapshotStatus RsdpStatus { get; }

    public uint TableSignature { get; }

    public static AcpiRootSdtSnapshotResult Success(
        AcpiSdtTableKind tableKind,
        ulong physicalAddress,
        int bytesRead,
        uint declaredLength,
        int entryCount,
        bool usedRsdtFallback)
    {
        return new AcpiRootSdtSnapshotResult(
            AcpiRootSdtSnapshotStatus.Success,
            tableKind,
            physicalAddress,
            bytesRead,
            bytesRead,
            declaredLength,
            entryCount,
            usedRsdtFallback,
            PlatformPhysicalMemoryStatus.Success,
            AcpiRsdpSnapshotStatus.Success,
            SignatureFor(tableKind));
    }

    public static AcpiRootSdtSnapshotResult InvalidRsdpSnapshot(
        AcpiRsdpSnapshotStatus rsdpStatus)
    {
        return Fail(
            AcpiRootSdtSnapshotStatus.InvalidRsdpSnapshot,
            rsdpStatus: rsdpStatus);
    }

    public static AcpiRootSdtSnapshotResult PhysicalMemoryUnavailable()
    {
        return Fail(
            AcpiRootSdtSnapshotStatus.PhysicalMemoryUnavailable,
            physicalReadStatus: PlatformPhysicalMemoryStatus.SourceUnavailable);
    }

    public static AcpiRootSdtSnapshotResult NoUsableRootTableAddress(
        AcpiRsdpSnapshotStatus rsdpStatus)
    {
        return Fail(
            AcpiRootSdtSnapshotStatus.NoUsableRootTableAddress,
            rsdpStatus: rsdpStatus);
    }

    public static AcpiRootSdtSnapshotResult ScratchTooSmall(
        int requiredBytes,
        AcpiSdtTableKind tableKind = AcpiSdtTableKind.None,
        ulong physicalAddress = 0,
        bool usedRsdtFallback = false)
    {
        return Fail(
            AcpiRootSdtSnapshotStatus.ScratchTooSmall,
            tableKind,
            physicalAddress,
            requiredBytes: requiredBytes,
            usedRsdtFallback: usedRsdtFallback);
    }

    public static AcpiRootSdtSnapshotResult DestinationTooSmall(
        AcpiSdtTableKind tableKind,
        ulong physicalAddress,
        int requiredBytes,
        uint declaredLength,
        bool usedRsdtFallback)
    {
        return Fail(
            AcpiRootSdtSnapshotStatus.DestinationTooSmall,
            tableKind,
            physicalAddress,
            requiredBytes: requiredBytes,
            declaredLength: declaredLength,
            usedRsdtFallback: usedRsdtFallback);
    }

    public static AcpiRootSdtSnapshotResult PhysicalReadFailed(
        AcpiRootSdtSnapshotStatus status,
        AcpiSdtTableKind tableKind,
        ulong physicalAddress,
        PlatformPhysicalMemoryResult readResult,
        bool usedRsdtFallback)
    {
        return new AcpiRootSdtSnapshotResult(
            status,
            tableKind,
            physicalAddress,
            readResult.BytesRead,
            readResult.RequiredBytes,
            0,
            0,
            usedRsdtFallback,
            readResult.Status,
            AcpiRsdpSnapshotStatus.Success,
            SignatureFor(tableKind));
    }

    public static AcpiRootSdtSnapshotResult InvalidTable(
        AcpiRootSdtSnapshotStatus status,
        AcpiSdtTableKind tableKind,
        ulong physicalAddress,
        int requiredBytes,
        uint declaredLength,
        bool usedRsdtFallback,
        uint tableSignature)
    {
        return Fail(
            status,
            tableKind,
            physicalAddress,
            requiredBytes: requiredBytes,
            declaredLength: declaredLength,
            usedRsdtFallback: usedRsdtFallback,
            tableSignature: tableSignature);
    }

    private static AcpiRootSdtSnapshotResult Fail(
        AcpiRootSdtSnapshotStatus status,
        AcpiSdtTableKind tableKind = AcpiSdtTableKind.None,
        ulong physicalAddress = 0,
        int bytesRead = 0,
        int requiredBytes = 0,
        uint declaredLength = 0,
        int entryCount = 0,
        bool usedRsdtFallback = false,
        PlatformPhysicalMemoryStatus physicalReadStatus = PlatformPhysicalMemoryStatus.Unknown,
        AcpiRsdpSnapshotStatus rsdpStatus = AcpiRsdpSnapshotStatus.Success,
        uint tableSignature = 0)
    {
        return new AcpiRootSdtSnapshotResult(
            status,
            tableKind,
            physicalAddress,
            bytesRead,
            requiredBytes,
            declaredLength,
            entryCount,
            usedRsdtFallback,
            physicalReadStatus,
            rsdpStatus,
            tableSignature);
    }

    private static uint SignatureFor(AcpiSdtTableKind tableKind)
    {
        return tableKind == AcpiSdtTableKind.Xsdt
            ? AcpiSdtParser.XsdtSignature
            : tableKind == AcpiSdtTableKind.Rsdt
                ? AcpiSdtParser.RsdtSignature
                : 0;
    }
}

public static class AcpiRootSdtSnapshotReader
{
    public static AcpiRootSdtSnapshotResult ReadAndValidate(
        AcpiRsdpSnapshotResult rsdpSnapshot,
        IPlatformPhysicalMemory physicalMemory,
        Span<byte> headerScratch,
        Span<byte> tableDestination)
    {
        if (!rsdpSnapshot.Succeeded)
        {
            return AcpiRootSdtSnapshotResult.InvalidRsdpSnapshot(rsdpSnapshot.Status);
        }

        if (physicalMemory is null)
        {
            return AcpiRootSdtSnapshotResult.PhysicalMemoryUnavailable();
        }

        AcpiSdtTableKind tableKind = SelectRootTableKind(rsdpSnapshot, out ulong physicalAddress, out bool usedRsdtFallback);
        if (physicalAddress == 0)
        {
            return AcpiRootSdtSnapshotResult.NoUsableRootTableAddress(rsdpSnapshot.Status);
        }

        if (headerScratch.Length < AcpiSdtParser.HeaderLength)
        {
            return AcpiRootSdtSnapshotResult.ScratchTooSmall(
                AcpiSdtParser.HeaderLength,
                tableKind,
                physicalAddress,
                usedRsdtFallback);
        }

        PlatformPhysicalMemoryResult headerRead = physicalMemory.ReadBytes(
            physicalAddress,
            AcpiSdtParser.HeaderLength,
            headerScratch);

        if (!headerRead.Succeeded)
        {
            return AcpiRootSdtSnapshotResult.PhysicalReadFailed(
                AcpiRootSdtSnapshotStatus.HeaderReadFailed,
                tableKind,
                physicalAddress,
                headerRead,
                usedRsdtFallback);
        }

        uint signature = EndianPrimitives.ReadUInt32LittleEndian(
            headerScratch.Slice(AcpiSdtParser.SignatureOffset, sizeof(uint)));
        uint expectedSignature = tableKind == AcpiSdtTableKind.Xsdt
            ? AcpiSdtParser.XsdtSignature
            : AcpiSdtParser.RsdtSignature;

        if (signature != expectedSignature)
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(
                AcpiRootSdtSnapshotStatus.InvalidSignature,
                tableKind,
                physicalAddress,
                AcpiSdtParser.HeaderLength,
                declaredLength: 0,
                usedRsdtFallback,
                signature);
        }

        uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(
            headerScratch.Slice(AcpiSdtParser.LengthOffset, sizeof(uint)));

        if (declaredLength < AcpiSdtParser.HeaderLength || declaredLength > int.MaxValue)
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(
                AcpiRootSdtSnapshotStatus.InvalidTableLength,
                tableKind,
                physicalAddress,
                AcpiSdtParser.HeaderLength,
                declaredLength,
                usedRsdtFallback,
                signature);
        }

        int tableLength = (int)declaredLength;
        if (tableDestination.Length < tableLength)
        {
            return AcpiRootSdtSnapshotResult.DestinationTooSmall(
                tableKind,
                physicalAddress,
                tableLength,
                declaredLength,
                usedRsdtFallback);
        }

        PlatformPhysicalMemoryResult tableRead = physicalMemory.ReadBytes(
            physicalAddress,
            tableLength,
            tableDestination);

        if (!tableRead.Succeeded)
        {
            return AcpiRootSdtSnapshotResult.PhysicalReadFailed(
                AcpiRootSdtSnapshotStatus.FullReadFailed,
                tableKind,
                physicalAddress,
                tableRead,
                usedRsdtFallback);
        }

        ReadOnlySpan<byte> table = tableDestination.Slice(0, tableLength);
        if (!IsChecksumValid(table))
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(
                AcpiRootSdtSnapshotStatus.InvalidChecksum,
                tableKind,
                physicalAddress,
                tableLength,
                declaredLength,
                usedRsdtFallback,
                signature);
        }

        int entryBytes = tableLength - AcpiSdtParser.HeaderLength;
        int pointerSize = tableKind == AcpiSdtTableKind.Xsdt
            ? AcpiSdtParser.XsdtPointerSize
            : AcpiSdtParser.RsdtPointerSize;

        if ((entryBytes % pointerSize) != 0)
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(
                AcpiRootSdtSnapshotStatus.InvalidPointerArrayLength,
                tableKind,
                physicalAddress,
                tableLength + (pointerSize - (entryBytes % pointerSize)),
                declaredLength,
                usedRsdtFallback,
                signature);
        }

        return AcpiRootSdtSnapshotResult.Success(
            tableKind,
            physicalAddress,
            tableLength,
            declaredLength,
            entryBytes / pointerSize,
            usedRsdtFallback);
    }

    private static AcpiSdtTableKind SelectRootTableKind(
        AcpiRsdpSnapshotResult rsdpSnapshot,
        out ulong physicalAddress,
        out bool usedRsdtFallback)
    {
        if (rsdpSnapshot.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater
            && rsdpSnapshot.XsdtAddress != 0)
        {
            physicalAddress = rsdpSnapshot.XsdtAddress;
            usedRsdtFallback = false;
            return AcpiSdtTableKind.Xsdt;
        }

        physicalAddress = rsdpSnapshot.RsdtAddress;
        usedRsdtFallback = rsdpSnapshot.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater
            && rsdpSnapshot.XsdtAddress == 0
            && rsdpSnapshot.RsdtAddress != 0;
        return AcpiSdtTableKind.Rsdt;
    }

    private static bool IsChecksumValid(ReadOnlySpan<byte> source)
    {
        uint sum = 0;
        for (int index = 0; index < source.Length; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }
}
