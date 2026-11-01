using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiRootSdtSnapshotResult
{
    private AcpiRootSdtSnapshotResult(AcpiRootSdtSnapshotStatus status, AcpiSdtTableKind tableKind, ulong physicalAddress, int bytesRead, int requiredBytes, uint declaredLength, int entryCount, bool usedRsdtFallback, PlatformPhysicalMemoryStatus physicalReadStatus, AcpiRsdpSnapshotStatus rsdpStatus, uint tableSignature)
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

    public static AcpiRootSdtSnapshotResult Success(AcpiSdtTableKind tableKind, ulong physicalAddress, int bytesRead, uint declaredLength, int entryCount, bool usedRsdtFallback)
    {
        return new AcpiRootSdtSnapshotResult(AcpiRootSdtSnapshotStatus.Success, tableKind, physicalAddress, bytesRead, bytesRead, declaredLength, entryCount, usedRsdtFallback, PlatformPhysicalMemoryStatus.Success, AcpiRsdpSnapshotStatus.Success, SignatureFor(tableKind));
    }

    public static AcpiRootSdtSnapshotResult InvalidRsdpSnapshot(AcpiRsdpSnapshotStatus rsdpStatus)
    {
        return Fail(AcpiRootSdtSnapshotStatus.InvalidRsdpSnapshot, rsdpStatus: rsdpStatus);
    }

    public static AcpiRootSdtSnapshotResult PhysicalMemoryUnavailable()
    {
        return Fail(AcpiRootSdtSnapshotStatus.PhysicalMemoryUnavailable, physicalReadStatus: PlatformPhysicalMemoryStatus.SourceUnavailable);
    }

    public static AcpiRootSdtSnapshotResult NoUsableRootTableAddress(AcpiRsdpSnapshotStatus rsdpStatus)
    {
        return Fail(AcpiRootSdtSnapshotStatus.NoUsableRootTableAddress, rsdpStatus: rsdpStatus);
    }

    public static AcpiRootSdtSnapshotResult ScratchTooSmall(int requiredBytes, AcpiSdtTableKind tableKind = AcpiSdtTableKind.None, ulong physicalAddress = 0, bool usedRsdtFallback = false)
    {
        return Fail(AcpiRootSdtSnapshotStatus.ScratchTooSmall, tableKind, physicalAddress, requiredBytes: requiredBytes, usedRsdtFallback: usedRsdtFallback);
    }

    public static AcpiRootSdtSnapshotResult DestinationTooSmall(AcpiSdtTableKind tableKind, ulong physicalAddress, int requiredBytes, uint declaredLength, bool usedRsdtFallback)
    {
        return Fail(AcpiRootSdtSnapshotStatus.DestinationTooSmall, tableKind, physicalAddress, requiredBytes: requiredBytes, declaredLength: declaredLength, usedRsdtFallback: usedRsdtFallback);
    }

    public static AcpiRootSdtSnapshotResult PhysicalReadFailed(AcpiRootSdtSnapshotStatus status, AcpiSdtTableKind tableKind, ulong physicalAddress, PlatformPhysicalMemoryResult readResult, bool usedRsdtFallback)
    {
        return new AcpiRootSdtSnapshotResult(status, tableKind, physicalAddress, readResult.BytesRead, readResult.RequiredBytes, 0, 0, usedRsdtFallback, readResult.Status, AcpiRsdpSnapshotStatus.Success, SignatureFor(tableKind));
    }

    public static AcpiRootSdtSnapshotResult InvalidTable(AcpiRootSdtSnapshotStatus status, AcpiSdtTableKind tableKind, ulong physicalAddress, int requiredBytes, uint declaredLength, bool usedRsdtFallback, uint tableSignature)
    {
        return Fail(status, tableKind, physicalAddress, requiredBytes: requiredBytes, declaredLength: declaredLength, usedRsdtFallback: usedRsdtFallback, tableSignature: tableSignature);
    }

    private static AcpiRootSdtSnapshotResult Fail(AcpiRootSdtSnapshotStatus status, AcpiSdtTableKind tableKind = AcpiSdtTableKind.None, ulong physicalAddress = 0, int bytesRead = 0, int requiredBytes = 0, uint declaredLength = 0, int entryCount = 0, bool usedRsdtFallback = false, PlatformPhysicalMemoryStatus physicalReadStatus = PlatformPhysicalMemoryStatus.Unknown, AcpiRsdpSnapshotStatus rsdpStatus = AcpiRsdpSnapshotStatus.Success, uint tableSignature = 0)
    {
        return new AcpiRootSdtSnapshotResult(status, tableKind, physicalAddress, bytesRead, requiredBytes, declaredLength, entryCount, usedRsdtFallback, physicalReadStatus, rsdpStatus, tableSignature);
    }

    private static uint SignatureFor(AcpiSdtTableKind tableKind)
    {
        return tableKind == AcpiSdtTableKind.Xsdt ? AcpiSdtParser.XsdtSignature : tableKind == AcpiSdtTableKind.Rsdt ? AcpiSdtParser.RsdtSignature : 0;
    }
}
