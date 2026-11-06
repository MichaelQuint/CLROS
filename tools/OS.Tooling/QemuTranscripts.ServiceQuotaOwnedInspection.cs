namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Internal joined observations from one held original capture.
    /// Source/native locks remain the caller's responsibility. This result
    /// grants no issue closure, component-budget fit or broader guest gate.</summary>

    internal sealed record ServiceQuotaOwnedInspection(ServiceQuotaCaptureReport Capture, ServiceQuotaRunnerInspection Runner, ServiceQuotaStreamJoinInspection Streams, ServiceQuotaTemporalInspection Temporal, ServiceQuotaOverlapInspection Overlap, ServiceNicAccountingInspection Nic);
}
