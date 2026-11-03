using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;

public readonly struct FetchAppHttpCompletionExpectedMarkerCopyResult
{
    private const uint InitializedFlag = 1;
    public FetchAppHttpCompletionExpectedMarkerCopyResult(FetchAppHttpCompletionExpectationStatus status, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpCompletionExpectationStatus Status
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public int AvailableBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpCompletionExpectationStatus.Success && BytesWritten == RequiredBytes && RequiredBytes > 0 && RequiredBytes <= AvailableBytes;
}
