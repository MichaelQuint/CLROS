using OS.Tooling;

internal static class NativeAotSocketContainmentOutputTests
{
    private const string Output = """
        TEST_START nativeaot.socket.containment
        SOCKET_CONTAINMENT_LISTEN port=8080 peers=3 rounds=15 interval_ms=5000 deadline_ms=100000
        SOCKET_CONTAINMENT_TIMEOUT elapsed_ms=63005 queued=32768 worker=260
        SOCKET_CONTAINMENT_HEALTHY role=1 rounds=15 elapsed_ms=70100 worker=289
        SOCKET_CONTAINMENT_HEALTHY role=2 rounds=15 elapsed_ms=70200 worker=290
        TEST_PASS nativeaot.socket.containment peers=3 timedout=1 queued=32768 eof=1 shutdown=1 worker_before=5 worker_timeout=260 worker=291 elapsed_ms=70500
        """ + "\n";

    [HostTest("nativeaot.socket.containment stdout exact values and immutable peers", HostLane.Fast, 0)]
    private static void Valid()
    {
        foreach (string text in new[] { Output, Output.Replace("\n", "\r\n", StringComparison.Ordinal) })
        {
            var value = NativeAotSocketContainmentOutput.Read(text);
            HostCheck.Equal(32768UL, value.Queued);
            HostCheck.Equal(63005UL, value.TimeoutMilliseconds);
            HostCheck.Equal(5UL, value.WorkerBefore);
            HostCheck.Equal(260UL, value.WorkerAtTimeout);
            HostCheck.Equal(291UL, value.Worker);
            HostCheck.Equal(70500UL, value.ElapsedMilliseconds);
            HostCheck.Equal(2, value.Peers.Count);
            HostCheck.True(((IList<NativeAotSocketContainmentOutput.Healthy>)value.Peers).IsReadOnly);
        }
        HostCheck.Equal(2UL, NativeAotSocketContainmentOutput.Read(Output.Replace("HEALTHY role=1", "HEALTHY role=9", StringComparison.Ordinal)
            .Replace("HEALTHY role=2", "HEALTHY role=1", StringComparison.Ordinal).Replace("HEALTHY role=9", "HEALTHY role=2", StringComparison.Ordinal)).Peers[0].Role);
        HostCheck.Equal(32768UL, NativeAotSocketContainmentOutput.Read(Output.Replace("port=8080", "port=8081", StringComparison.Ordinal), 8081).Queued);
    }

    [HostTest("nativeaot.socket.containment stdout rejects early timeout and missing independent progress", HostLane.Fast, 1)]
    private static void Progress()
    {
        foreach ((string Before, string After) change in new (string, string)[] {
            ("elapsed_ms=63005", "elapsed_ms=59999"), ("elapsed_ms=63005", "elapsed_ms=100000"),
            ("elapsed_ms=70100", "elapsed_ms=69999"), ("elapsed_ms=70200", "elapsed_ms=100000"),
            ("worker=289", "worker=260"), ("worker=290", "worker=288"), ("worker_before=5", "worker_before=260"),
            ("worker_timeout=260", "worker_timeout=261"), ("worker=291", "worker=289"),
            ("elapsed_ms=70500", "elapsed_ms=70199"), ("elapsed_ms=70500", "elapsed_ms=100000"),
            ("HEALTHY role=2", "HEALTHY role=1"), ("rounds=15 elapsed_ms=70200", "rounds=14 elapsed_ms=70200"),
            ("queued=32768 eof", "queued=32769 eof"), ("eof=1", "eof=0"), ("shutdown=1", "shutdown=0"),
            ("timedout=1", "timedout=0"), ("queued=32768 worker", "queued=65536 worker") })
            Reject(Output.Replace(change.Before, change.After, StringComparison.Ordinal));
    }

    [HostTest("nativeaot.socket.containment stdout rejects malformed extra and unjoined rows", HostLane.Fast, 2)]
    private static void Malformed()
    {
        foreach (string text in new[] { "", Output.TrimEnd('\n'), Output + "\n", Output + "TEST_PASS invented\n",
            Output.Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal), Output.Replace("worker=289", "worker=0289", StringComparison.Ordinal),
            Output.Replace("worker=289", "worker=-1", StringComparison.Ordinal), Output.Replace("worker=289", "worker=18446744073709551616", StringComparison.Ordinal),
            Output.Replace("worker=289", "worker=2147483648", StringComparison.Ordinal), Output.Replace("port=8080", "port=8081", StringComparison.Ordinal),
            Output.Replace(" timedout=1", " timedout=1 extra=0", StringComparison.Ordinal), Output.Replace(" worker_timeout", "\r worker_timeout", StringComparison.Ordinal),
            Output.Replace("SOCKET_CONTAINMENT_TIMEOUT", "SOCKET_CONTAINMENT_HEALTHY", StringComparison.Ordinal) })
            Reject(text);
    }

    private static void Reject(string text)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotSocketContainmentOutput.Read(text);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
