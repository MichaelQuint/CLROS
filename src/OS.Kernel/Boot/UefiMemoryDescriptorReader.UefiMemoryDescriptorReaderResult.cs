using OS.Std.Binary;

namespace OS.Boot;

public readonly struct UefiMemoryDescriptorReaderResult
{
    private UefiMemoryDescriptorReaderResult(bool succeeded, UefiMemoryDescriptorReaderStatus status, int descriptorsWritten, ulong requiredSourceBytes, ulong requiredDestinationCount)
    {
        Succeeded = succeeded;
        Status = status;
        DescriptorsWritten = descriptorsWritten;
        RequiredSourceBytes = requiredSourceBytes;
        RequiredDestinationCount = requiredDestinationCount;
    }

    public bool Succeeded
    {
        get;
    }
    public UefiMemoryDescriptorReaderStatus Status
    {
        get;
    }
    public int DescriptorsWritten
    {
        get;
    }
    public ulong RequiredSourceBytes
    {
        get;
    }
    public ulong RequiredDestinationCount
    {
        get;
    }

    internal static UefiMemoryDescriptorReaderResult Success(int descriptorsWritten, ulong requiredSourceBytes)
    {
        return new UefiMemoryDescriptorReaderResult(true, UefiMemoryDescriptorReaderStatus.Success, descriptorsWritten, requiredSourceBytes, (ulong)descriptorsWritten);
    }

    internal static UefiMemoryDescriptorReaderResult Fail(UefiMemoryDescriptorReaderStatus status, ulong requiredSourceBytes = 0, ulong requiredDestinationCount = 0)
    {
        return new UefiMemoryDescriptorReaderResult(false, status, 0, requiredSourceBytes, requiredDestinationCount);
    }
}
