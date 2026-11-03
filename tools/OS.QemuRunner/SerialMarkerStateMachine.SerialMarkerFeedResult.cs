using System.Text;

namespace OS.Tools.QemuRunner;
public readonly struct SerialMarkerFeedResult
{
    private readonly bool _isInitialized;
    private SerialMarkerFeedResult(SerialMarkerFeedStatus status, int bytesConsumed, long totalBytes)
    {
        _isInitialized = true;
        Status = status;
        BytesConsumed = bytesConsumed;
        TotalBytes = totalBytes;
    }

    public SerialMarkerFeedStatus Status { get; }
    public int BytesConsumed { get; }
    public long TotalBytes { get; }
    public bool Succeeded => _isInitialized && Status == SerialMarkerFeedStatus.Accepted && BytesConsumed >= 0 && TotalBytes >= BytesConsumed;

    internal static SerialMarkerFeedResult Create(SerialMarkerFeedStatus status, int bytesConsumed, long totalBytes)
    {
        return new SerialMarkerFeedResult(status, bytesConsumed, totalBytes);
    }
}
