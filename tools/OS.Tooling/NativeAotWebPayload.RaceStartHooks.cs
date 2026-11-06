namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    // Copied, validated six-field phase2 status is only scheduling input. Each
    // production hook must bind it to its actual owned native/serial evidence.
    // The native owner separately owns restoration/session-stop in its finally.
    internal sealed record RaceStartHooks(Func<ulong[], CancellationToken, Task> BeforeChurn, Func<CancellationToken, Task> BeforeStart, Func<CancellationToken, Task> AfterAcknowledged);
}
