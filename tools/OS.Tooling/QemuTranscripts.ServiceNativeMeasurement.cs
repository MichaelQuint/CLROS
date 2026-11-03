namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied values for explicitly retained mmap pressure and its
    /// release/recovery. These do not classify other native allocations or
    /// identify which physical/metadata limit produced ENOMEM.</summary>

    public readonly record struct ServiceNativeMeasurement(ulong RetainedBytes, ulong GcCommittedBytes);
}
