using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Run the unchanged bounded controller against one owned IPv4
    /// loopback forward with actual HTTP/1.1 stream observations. Owns three
    /// persistent role handlers, their sockets and every started I/O until joined.
    /// Creates fresh selected/attempt reports and a separate .wire.json companion;
    /// partial failures retain raw successful reads/attempted writes after close
    /// and join before the original error propagates. A failed join grants no
    /// final snapshot. Snapshot/write errors preserve any controller error too.
    /// Stream IDs/endpoints/ticks confer no NAT tuple, PCAP, native process or
    /// guest authority. HTTP framing and the owned runner join remain separate.</summary>

    public static Task<string> CaptureObservedSignalRaceAsync(int port, string expectedHealthBody, string observationPath, CancellationToken cancellation = default) => CaptureObservedSignalRaceCoreAsync(port, expectedHealthBody, observationPath, cancellation, null);
    /// <summary>Capture the explicit event-owned v3 controller over owned
    /// loopback HTTP/1.1 streams. One 150-second capture deadline owns the
    /// request tasks. Retain partial selected/attempt reports after request
    /// joins, then close/join role handlers before the raw wire snapshot. Framed
    /// selector headers require separate HTTP inspection. The caller owns the
    /// endpoint; this establishes no native process, wait, cleanup or guest proof.</summary>

    public static Task<string> CaptureObservedEventOwnedSignalRaceAsync(int port, string expectedHealthBody, string observationPath, CancellationToken cancellation = default) => CaptureObservedSignalRaceCoreAsync(port, expectedHealthBody, observationPath, cancellation, null, startGate: true, eventOwned: true);
    private static async Task<string> CaptureObservedSignalRaceCoreAsync(int port, string expectedHealthBody, string observationPath, CancellationToken cancellation, Action<string, CreatedRaceReport, CreatedRaceReport, CreatedRaceReport>? retainCreated, bool startGate = false, RaceStartHooks? startHooks = null, bool eventOwned = false)
    {
        string endpoint = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        RaceInputs(endpoint, expectedHealthBody);
        string path = Path.GetFullPath(observationPath);
        AuthorityFiles.RequirePlainPath(Path.GetDirectoryName(path)!, Path.GetDirectoryName(path)!, "web_signalrace_output_path");
        using var output = new FileStream(path + ".wire.json", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var wire = new NativeAotServiceQuotaPeer.WireCapture(port, signalRace: true);
        wire.BindClock(Stopwatch.StartNew());
        string? selected = null;
        Exception? failure = null;
        CreatedRaceReport? selectedFile = null, attemptFile = null, wireFile = null;
        try
        {
            using var client = new HttpClient(new RaceRoutingHandler(wire, port), disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan,
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            selected = await CaptureSignalRaceCoreAsync(client, endpoint, expectedHealthBody, path, cancellation, wire, retainCreated is null ? null : (first, second) =>
            {
                selectedFile = first;
                attemptFile = second;
            }, startGate, startHooks, eventOwned);
        }
        catch (Exception error)
        {
            failure = error;
        }

        try
        {
            await wire.StopAsync();
            var trace = wire.Snapshot(failure is null ? "signalrace-run" : "signalrace-partial");
            byte[] bytes = Encoding.UTF8.GetBytes(SerializeSignalRaceWire(trace));
            await output.WriteAsync(bytes);
            await output.FlushAsync();
            if (failure is null && retainCreated is not null)
            {
                wireFile = CreatedRaceReport.Retain(output, bytes);
                retainCreated(selected!, selectedFile!, attemptFile!, wireFile);
                selectedFile = null;
                attemptFile = null;
                wireFile = null;
            }
        }
        catch (Exception error)
        {
            if (failure is not null)
                throw new AggregateException(failure, error);
            throw;
        }
        finally
        {
            selectedFile?.Dispose();
            attemptFile?.Dispose();
            wireFile?.Dispose();
        }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        return selected!;
    }

    internal static async Task<OwnedRaceCapture> CaptureOwnedObservedSignalRaceAsync(int port, string health, string path, CancellationToken cancellation, RaceStartHooks startHooks, bool eventOwned = false)
    {
        OwnedRaceCapture? result = null;
        try
        {
            _ = await CaptureObservedSignalRaceCoreAsync(port, health, path, cancellation, (text, selected, attempts, wire) => result = new(text, selected, attempts, wire), startGate: true, startHooks: startHooks, eventOwned: eventOwned);
            return result ?? throw new InvalidDataException("web_signalrace_created_reports_missing");
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }
}
