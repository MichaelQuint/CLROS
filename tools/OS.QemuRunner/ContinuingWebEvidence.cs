using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

sealed class ContinuingWebEvidence
{
    public WebRaceRendezvous.Report? RaceGate;
    public bool AliveAfterRaceGate;
    public WebSocketControls.Report? Controls;
    public WebFrameControl.Report? Frames;
    public bool AliveAfterControls;
    public readonly List<(DateTime Started, DateTime Completed)> ProbeWindows = [];
    public HttpProbeResult? ConfigResult, LogResult, LogRepeatResult, VectorResult, HealthAfterResult;
    public bool PreLaunchAvailable, AliveBeforeHealth, AliveAfterHealth, AliveAfterConfig;
    public bool AliveAfterLog, AliveAfterLogRepeat, AliveAfterVectors, AliveAfterFinalHealth;
    public bool TerminationRequested, DeathConfirmed, SpontaneousExit;
    public bool? StoppedEndpointRefused;
    public int? ProcessId, ExitCode;
    public string? StartUtc, EndUtc, Failure;
    public long? LifetimeMilliseconds;
}
