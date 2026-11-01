using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct NitroNvmeNamespaceIdentity
{
    internal NitroNvmeNamespaceIdentity(uint namespaceCount, uint namespaceId, ulong totalLogicalBlocks, ulong logicalBlockSizeBytes, ulong capacityBytes, byte lbaFormatIndex, byte lbaDataSizeShift, NvmeIdentityTextField serialNumber, NvmeIdentityTextField modelNumber)
    {
        NamespaceCount = namespaceCount;
        NamespaceId = namespaceId;
        TotalLogicalBlocks = totalLogicalBlocks;
        LogicalBlockSizeBytes = logicalBlockSizeBytes;
        CapacityBytes = capacityBytes;
        LbaFormatIndex = lbaFormatIndex;
        LbaDataSizeShift = lbaDataSizeShift;
        SerialNumber = serialNumber;
        ModelNumber = modelNumber;
    }

    public uint NamespaceCount { get; }
    public uint NamespaceId { get; }
    public ulong TotalLogicalBlocks { get; }
    public ulong LogicalBlockSizeBytes { get; }
    public ulong CapacityBytes { get; }
    public byte LbaFormatIndex { get; }
    public byte LbaDataSizeShift { get; }
    public NvmeIdentityTextField SerialNumber { get; }
    public NvmeIdentityTextField ModelNumber { get; }
}
