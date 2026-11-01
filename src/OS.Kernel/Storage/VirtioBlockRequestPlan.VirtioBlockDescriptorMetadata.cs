using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorMetadata
{
    public const ushort NextFlag = 1;
    public const ushort DeviceWriteFlag = 2;

    public VirtioBlockDescriptorMetadata(
        ushort index,
        uint length,
        VirtioBlockDescriptorAccess access,
        bool hasNext,
        ushort nextIndex)
    {
        Index = index;
        Length = length;
        Access = access;
        HasNext = hasNext;
        NextIndex = nextIndex;
    }

    public ushort Index { get; }

    public uint Length { get; }

    public VirtioBlockDescriptorAccess Access { get; }

    public bool HasNext { get; }

    public ushort NextIndex { get; }

    public bool DeviceWrites => Access == VirtioBlockDescriptorAccess.DeviceWrites;

    public bool DeviceReads => Access == VirtioBlockDescriptorAccess.DeviceReads;

    public ushort Flags
    {
        get
        {
            ushort flags = 0;

            if (HasNext)
            {
                flags |= NextFlag;
            }

            if (DeviceWrites)
            {
                flags |= DeviceWriteFlag;
            }

            return flags;
        }
    }
}
