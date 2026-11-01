namespace OS.Storage;

public readonly struct PartitionBlockDeviceCreateResult
{
    private PartitionBlockDeviceCreateResult(
        BlockDeviceStatus status,
        PartitionBlockDevice? device)
    {
        IsInitialized = true;
        Status = status;
        Device = device;
    }

    public bool IsInitialized { get; }

    public BlockDeviceStatus Status { get; }

    public PartitionBlockDevice? Device { get; }

    public bool Succeeded => IsInitialized && Status == BlockDeviceStatus.Success;

    public static PartitionBlockDeviceCreateResult Success(PartitionBlockDevice device)
    {
        return new PartitionBlockDeviceCreateResult(BlockDeviceStatus.Success, device);
    }

    public static PartitionBlockDeviceCreateResult Fail(BlockDeviceStatus status)
    {
        return new PartitionBlockDeviceCreateResult(status, null);
    }
}
