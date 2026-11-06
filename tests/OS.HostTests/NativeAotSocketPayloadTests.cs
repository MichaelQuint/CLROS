using System.Text.Json;
using OS.Tooling;

internal static class NativeAotSocketPayloadTests
{
    private const string Output = "TEST_START nativeaot.socket\n" +
        "SOCKET_LISTEN port=8080 connections=4 deadline_ms=30000\n" +
        "SOCKET_CONNECTION index=0 received=20 sent=65536 accept_blocked=4 receive_blocked=5 send_blocked=6 send_partial=3 eof=0 reset=0\n" +
        "SOCKET_CONNECTION index=1 received=1 sent=0 accept_blocked=0 receive_blocked=3 send_blocked=0 send_partial=0 eof=0 reset=1\n" +
        "SOCKET_CONNECTION index=2 received=20 sent=18 accept_blocked=0 receive_blocked=0 send_blocked=0 send_partial=0 eof=1 reset=0\n" +
        "SOCKET_CONNECTION index=3 received=20 sent=18 accept_blocked=0 receive_blocked=0 send_blocked=0 send_partial=0 eof=1 reset=0\n" +
        "TEST_PASS nativeaot.socket connections=4 received=61 sent=65572 accept_blocked=4 receive_blocked=8 send_blocked=6 send_partial=3 eof=2 reset=1\n";

    [HostTest("nativeaot.socket output binds four ordered connections and observed counters")]
    private static void ObservedOutput()
    {
        var observed = NativeAotSocketPayload.ValidateOutput(Output.Replace("\n", "\r\n", StringComparison.Ordinal));
        HostCheck.Equal(61, observed.Received);
        HostCheck.Equal(65572, observed.Sent);
        HostCheck.Equal(3, observed.PartialSends);
        HostCheck.Equal(6, observed.BlockedSends);
    }

    [HostTest("nativeaot.socket output rejects missing failure stale order and contradictory counts")]
    private static void RejectedOutput()
    {
        foreach (string text in new[] { "", Output[..Output.LastIndexOf("TEST_PASS", StringComparison.Ordinal)],
            Output + "TEST_FAIL nativeaot.socket bad\n", "TEST_START nativeaot.socket\n" + Output,
            Output.Replace("index=2", "index=3", StringComparison.Ordinal),
            Output.Replace("received=61", "received=62", StringComparison.Ordinal),
            Output.Replace("sent=65536", "sent=65535", StringComparison.Ordinal),
            Output.Replace("eof=0 reset=1", "eof=1 reset=0", StringComparison.Ordinal),
            Output.Replace("send_partial=3", "send_partial=-3", StringComparison.Ordinal) })
            Reject(() => NativeAotSocketPayload.ValidateOutput(text));
    }

    [HostTest("nativeaot.socket recipe reuses qualified arguments and rejects changed native policy")]
    private static void Recipe()
    {
        string destination = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "socket-publish"));
        string[] args = ["publish", "apps/NativeAotHello/NativeAotHello.csproj", "-c", "Release", "-f", "net11.0",
            "-r", "linux-musl-x64", "--self-contained", "true", "-o", "/old/output", "--nologo",
            "-p:PublishAot=true", "-p:StaticExecutable=true", "-p:PositionIndependentExecutable=false",
            "-p:InvariantGlobalization=true", "-p:ServerGarbageCollection=false", "-p:EventSourceSupport=false",
            "-p:EnableDiagnostics=false", "-p:DebuggerSupport=false",
            "-p:RuntimeFrameworkVersion=11.0.0-rc.3.26522.148", "-p:TargetLatestRuntimePatch=false",
            "-p:UseSharedCompilation=false", "-nr:false"];
        static string Json(string[] values) => JsonSerializer.Serialize(new[] { new { executable = "dotnet", arguments = values } });
        string[] actual = NativeAotSocketPayload.PublishArguments(Json(args), destination);
        HostCheck.Equal("apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj", actual[1]);
        HostCheck.Equal(destination, actual[Array.IndexOf(actual, "-o") + 1]);
        foreach (string change in new[] { "-p:StaticExecutable=false", "-p:PositionIndependentExecutable=true",
            "-p:RuntimeFrameworkVersion=10.0.12", "-p:EnableDiagnostics=true" })
        {
            string[] altered = (string[])args.Clone();
            string prefix = change[..change.IndexOf('=')];
            altered[Array.FindIndex(altered, value => value.StartsWith(prefix + "=", StringComparison.Ordinal))] = change;
            Reject(() => NativeAotSocketPayload.PublishArguments(Json(altered), destination));
        }
        Reject(() => NativeAotSocketPayload.PublishArguments(Json(args.Append("extra").ToArray()), destination));
        Reject(() => NativeAotSocketPayload.PublishArguments("[]", destination));
    }

    internal static string FixtureOutput(bool asynchronous) => asynchronous ? AsyncOutput() : Output;
    private static string AsyncOutput() => Output.Replace("nativeaot.socket", "nativeaot.socket.async", StringComparison.Ordinal)
        .Replace("SOCKET_LISTEN", "SOCKET_ASYNC_LISTEN", StringComparison.Ordinal)
        .Replace("SOCKET_CONNECTION", "SOCKET_ASYNC_CONNECTION", StringComparison.Ordinal)
        .Replace("accept_blocked=", "accept_pending=", StringComparison.Ordinal)
        .Replace("receive_blocked=", "receive_pending=", StringComparison.Ordinal)
        .Replace("send_blocked=", "send_pending=", StringComparison.Ordinal)
        .Replace("TEST_PASS", "SOCKET_ASYNC_TERMINAL cancelled_accept=1 closed_accept=1\nTEST_PASS", StringComparison.Ordinal);

    [HostTest("nativeaot.socket async output binds pending operations cancellation and close separately from EAGAIN")]
    private static void AsyncObservedOutput()
    {
        var observed = NativeAotSocketPayload.ValidateAsyncOutput(AsyncOutput());
        HostCheck.Equal(61, observed.Received);
        HostCheck.Equal(65572, observed.Sent);
        HostCheck.Equal(4, observed.PendingAccepts);
        HostCheck.Equal(8, observed.PendingReceives);
        HostCheck.Equal(6, observed.PendingSends);
        HostCheck.Equal(3, observed.PartialSends);
    }

    [HostTest("nativeaot.socket async output rejects sync traces missing terminal wakes and contradictory counters")]
    private static void AsyncRejectedOutput()
    {
        string output = AsyncOutput();
        foreach (string text in new[] { Output, output.Replace("cancelled_accept=1", "cancelled_accept=0", StringComparison.Ordinal),
            output.Replace("closed_accept=1", "closed_accept=0", StringComparison.Ordinal),
            output.Replace("SOCKET_ASYNC_TERMINAL cancelled_accept=1 closed_accept=1\n", "", StringComparison.Ordinal),
            output.Replace("send_pending=6", "send_pending=-1", StringComparison.Ordinal),
            output.Replace("SOCKET_ASYNC_CONNECTION index=0", "SOCKET_CONNECTION index=0", StringComparison.Ordinal),
            output.Replace("TEST_PASS nativeaot.socket.async", "TEST_PASS nativeaot.socket", StringComparison.Ordinal),
            output.Replace("received=61", "received=62", StringComparison.Ordinal), output + "unexpected\n" })
            Reject(() => NativeAotSocketPayload.ValidateAsyncOutput(text));
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or OverflowException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
