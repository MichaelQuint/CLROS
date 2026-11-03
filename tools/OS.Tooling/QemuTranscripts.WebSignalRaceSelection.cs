namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied HTTP selectors for one literal Smoke race. Tids only
    /// select identities from actual replay; they authenticate no callback code.</summary>

    public sealed record WebSignalRaceSelection(int Pid, int WaiterTid, int ActorTid, int CallbackTid, int RecoveryWriterTid);
}
