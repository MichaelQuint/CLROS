using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;
public static partial class NativeAotServiceQuotaPeer
{
    // Capture capacities are independent of startup/operation deadlines.
    // Unexpected retries, polling or fragmentation that exhaust these retained
    // witness bounds must fail rather than truncate, even before a deadline.
    public const int WireMaximumConnections = 8192, WireMaximumExchanges = 8192, WireMaximumOperations = 65536, WireMaximumOperationBytes = 65536, WireMaximumRawBytes = 4 * 1024 * 1024, WireMaximumReportBytes = 32 * 1024 * 1024;
    /// <summary>Run the same bounded quota stimulus with actual serialized
    /// stream I/O observations. Preserves persistent clients, held sockets and
    /// four live connections. Owns/disposes every observing stream and joins
    /// its I/O before returning; overflow/failure throws without truncation.
    /// The synchronous callback receives one copied final snapshot after disposal/join,
    /// including peer failures, before the original exception is rethrown.
    /// A failed join yields no snapshot; callback/join errors are preserved with
    /// any original peer error. Callback must not block or start owned work.
    /// This launches no child/VM and supplies no original-capture authority.</summary>

    public static async Task<ObservedRun> RunObservedAsync(int port, CancellationToken cancellation = default, Action<WireTrace>? completedSnapshot = null)
    {
        WireNeed(port is >= 1 and <= 65535, "port");
        var wire = new WireCapture(port);
        Observation? service = null;
        Exception? failure = null;
        try
        {
            service = await RunCoreAsync(port, wire, cancellation);
        }
        catch (Exception error)
        {
            failure = error;
        }

        WireTrace trace;
        try
        {
            await wire.StopAsync();
            trace = wire.Snapshot(failure is null ? "quota-run" : "quota-partial");
            completedSnapshot?.Invoke(trace);
        }
        catch (Exception error)
        {
            if (failure is not null)
                throw new AggregateException(failure, error);
            throw;
        }

        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return new(service!, trace);
    }

    /// <summary>Real connected-loopback stream control only. The caller owns
    /// its TcpClient and callback task until return; this scope closes its actual
    /// NetworkStream and joins observed I/O even on cancellation. Callback errors
    /// propagate. No fake stream, process, HTTP or guest success is supplied.</summary>

    public static async Task<WireTrace> ObserveConnectedStreamForTestsAsync(TcpClient client, Func<Stream, CancellationToken, Task> exercise, CancellationToken cancellation = default, Action<WireTrace>? completedSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(exercise);
        WireNeed(client.Connected && client.Client.RemoteEndPoint is IPEndPoint, "test_connection");
        int port = ((IPEndPoint)client.Client.RemoteEndPoint!).Port;
        var wire = new WireCapture(port);
        wire.BindClock(Stopwatch.StartNew());
        try
        {
            await exercise(wire.Wrap(client.Client, client.GetStream(), "control"), cancellation);
        }
        finally
        {
            await wire.StopAsync();
            completedSnapshot?.Invoke(wire.Snapshot("stream-control"));
        }

        return wire.Snapshot("stream-control");
    }

    private static void WireNeed([DoesNotReturnIf(false)] bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("quota_wire_" + reason);
    }

    internal static string SignalRaceWireRole(string method, string path) => method == "POST" && path == "/__clros/churn" ? "churn" : method == "POST" && path == "/__clros/signalrace" ? "race" : "control";
    private static bool WireRole(string role, bool signalRace = false) => signalRace ? role is "control" or "churn" or "race" : role is "control" or "pressure" or "cancel" or "echo";
    private static bool WireRequest(string role, string method, string path, bool signalRace = false) => signalRace ? role == SignalRaceWireRole(method, path) && (method == "GET" && path is "/healthz" or "/__clros/churn/status" or "/__clros/signalrace/status" || method == "POST" && path is "/__clros/churn" or "/__clros/signalrace" or "/__clros/signalrace/start" or "/__clros/signalrace/recover" or "/__clros/signalprobe") : role switch
    {
        "control" => method == "GET" && (path is "/healthz" or "/__clros/service/status" or "/__clros/service/lifecycle/status" or "/__clros/service/quota/status") || method == "POST" && path == "/__clros/service/start",
        "pressure" => method == "POST" && path == Quota,
        "cancel" => method == "GET" && path == Lifecycle + "cancel",
        "echo" => method == "POST" && path == Lifecycle + "echo",
        _ => false
    };
}
