using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    private sealed record RaceHttpReply(RaceHttpAttempt Attempt, string Body)
    {
        internal int Status => Attempt.status;
    }
}
