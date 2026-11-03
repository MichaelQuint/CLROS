namespace OS.Drivers;

public readonly struct VirtioDeviceStatusWriteSequence
{
    private readonly VirtioDeviceStatus _status0;
    private readonly VirtioDeviceStatus _status1;
    private readonly VirtioDeviceStatus _status2;
    private readonly VirtioDeviceStatus _status3;
    public VirtioDeviceStatusWriteSequence(byte count, VirtioDeviceStatus status0, VirtioDeviceStatus status1, VirtioDeviceStatus status2, VirtioDeviceStatus status3)
    {
        Count = count;
        _status0 = status0;
        _status1 = status1;
        _status2 = status2;
        _status3 = status3;
    }

    public byte Count
    {
        get;
    }

    public bool TryGetStatus(byte index, out VirtioDeviceStatus status)
    {
        if (index >= Count)
        {
            status = VirtioDeviceStatus.None;
            return false;
        }

        switch (index)
        {
            case 0:
                status = _status0;
                return true;
            case 1:
                status = _status1;
                return true;
            case 2:
                status = _status2;
                return true;
            case 3:
                status = _status3;
                return true;
            default:
                status = VirtioDeviceStatus.None;
                return false;
        }
    }
}
