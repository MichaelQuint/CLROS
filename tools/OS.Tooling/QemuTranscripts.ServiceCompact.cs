namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record ServiceCompact(ServiceBulk Bulk, ulong Generation, ulong Original, ulong Deadline, ulong Duration, ulong Clock);
}
