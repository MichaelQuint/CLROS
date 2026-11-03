using OS.Tooling;

internal static partial class NativeAotServiceQuotaTemporalTests
{
    private sealed record Vector(QemuTranscripts.ServiceQuotaStreamJoinInspection Joined, QemuTranscripts.ServiceQuotaSocketIo[] Calls);
}
