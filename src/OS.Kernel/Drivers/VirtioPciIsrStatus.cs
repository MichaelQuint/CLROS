namespace OS.Drivers;
public readonly struct VirtioPciIsrStatus
{
    public VirtioPciIsrStatus(byte rawStatus)
    {
        RawStatus = rawStatus;
    }

    public byte RawStatus { get; }
    public bool HasQueueInterrupt => (RawStatus & VirtioPciIsrStatusParser.QueueInterruptMask) != 0;
    public bool HasDeviceConfigurationChange => (RawStatus & VirtioPciIsrStatusParser.DeviceConfigurationChangeMask) != 0;
    public byte ReservedBits => (byte)(RawStatus & VirtioPciIsrStatusParser.ReservedBitsMask);
}
