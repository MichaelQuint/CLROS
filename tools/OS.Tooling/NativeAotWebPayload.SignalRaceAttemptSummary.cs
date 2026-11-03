using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Counts copied logical HTTP attempts after every owned task has
    /// joined. SemanticComplete covers the HTTP controller contract only;
    /// these values grant no wire, native, process-death or guest authority.</summary>

    public readonly record struct SignalRaceAttemptSummary(int Attempts, int Responses, int Cancelled, int Faulted, bool SemanticComplete);
}
