namespace OS.Net;

public readonly struct TcpConnectionSequenceMetadata
{
    private readonly bool initialized;
    public TcpConnectionSequenceMetadata(uint localNextSequenceNumber, uint remoteNextSequenceNumber) : this(localNextSequenceNumber, remoteNextSequenceNumber, localNextSequenceNumber)
    {
    }

    /// <summary>Track the oldest unacknowledged local byte separately from
    /// next-to-send. Modular outstanding distance must stay below half the
    /// sequence space; explicit zero is valid. This is metadata, not ACK proof.</summary>

    public TcpConnectionSequenceMetadata(uint localNextSequenceNumber, uint remoteNextSequenceNumber, uint localAcknowledgedSequenceNumber)
    {
        initialized = true;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        LocalAcknowledgedSequenceNumber = localAcknowledgedSequenceNumber;
    }

    public uint LocalNextSequenceNumber
    {
        get;
    }
    public uint RemoteNextSequenceNumber
    {
        get;
    }
    public uint LocalAcknowledgedSequenceNumber
    {
        get;
    }
    // Every uint is a TCP sequence number, including zero after wrap. Only an
    // uninitialized value lacks sequence metadata.
    public bool HasValidMetadata => initialized && unchecked(LocalNextSequenceNumber - LocalAcknowledgedSequenceNumber) < 0x80000000U;
}
