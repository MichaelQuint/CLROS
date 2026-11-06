using System.Globalization;

internal sealed partial class KestrelSignalRaceState
{
    internal readonly record struct Snapshot(int Phase, int Process, int Waiter, int Actor, int Callback, bool RecoveryRequested);
}
