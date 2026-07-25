namespace OS.Storage;

public readonly struct BlockReadResult
{
    private BlockReadResult(BlockDeviceStatus status, ulong blocksRead, ulong bytesRead)
    {
        IsInitialized = true;
        Status = status;
        BlocksRead = blocksRead;
        BytesRead = bytesRead;
    }

    public bool IsInitialized { get; }

    public BlockDeviceStatus Status { get; }

    public ulong BlocksRead { get; }

    public ulong BytesRead { get; }

    public bool Succeeded => IsInitialized && Status == BlockDeviceStatus.Success;

    public static BlockReadResult Success(ulong blocksRead, ulong bytesRead)
    {
        return new BlockReadResult(BlockDeviceStatus.Success, blocksRead, bytesRead);
    }

    public static BlockReadResult Fail(BlockDeviceStatus status)
    {
        return new BlockReadResult(status, 0, 0);
    }
}
