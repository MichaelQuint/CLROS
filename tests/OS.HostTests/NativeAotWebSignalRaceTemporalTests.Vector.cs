using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;

internal static partial class NativeAotWebSignalRaceTemporalTests
{
    private sealed record Vector(Q.WebSignalRaceStreamJoinInspection Joined, Q.WebSignalRaceSocketCall[] Calls);
}
