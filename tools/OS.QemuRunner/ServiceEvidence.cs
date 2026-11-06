using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

sealed class ServiceEvidence
{
    public bool QuotaFailure;
    public long? TerminalBytes, QuietMilliseconds;
    public bool TerminationRequested, DeathConfirmed, SpontaneousExit, CleanupFailed;
    public int? ExitCode;
    public ServiceSocketControls.Report? Controls;
    public WebFrameControl.ServiceReport? Frames;
    public bool PreLaunchAvailable;
    public bool? StoppedEndpointRefused;
    public int? ProcessId;
    public string? StartUtc, EndUtc, Failure;
    public long? LifetimeMilliseconds;
}
