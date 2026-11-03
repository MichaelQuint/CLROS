using System.Reflection;
using OS.Tooling;

internal static class LinuxSignalEpollSelectionTests
{
    private static readonly Func<QemuSmokeOptions, IReadOnlyDictionary<string, bool>, bool> Bind =
        typeof(LinuxSignalEpollSelection).GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<QemuSmokeOptions, IReadOnlyDictionary<string, bool>, bool>>();
    private static readonly Action<Stream> RequirePackage = typeof(LinuxSignalEpollSelection)
        .GetMethod("RequirePackage", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<Action<Stream>>();
    private static Dictionary<string, bool> Flags() => new()
    {
        ["EnableM16012Probe"] = true,
        ["EnableM16011Probe"] = true,
        ["EnableM16009Probe"] = true
    };

    [HostTest("mechanical epoll selection requires explicit mode and rejects runtime profile or application mixtures")]
    private static void Selection()
    {
        QemuSmokeOptions options = new()
        {
            LinuxThreadingApplication = "untouched"
        };
        HostCheck.True(!Bind(options, Flags()));
        HostCheck.Equal("untouched", options.LinuxThreadingApplication);
        options = new()
        {
            SignalEpollRace = true
        };
        HostCheck.True(Bind(options, Flags()));
        HostCheck.True(options.LinuxThreadingApplication.Length == 0 && options.LinuxThreadingDisassembly.Length == 0);
        foreach (string required in new[] { "M16012", "M16011", "M16009" })
        {
            var flags = Flags();
            flags["Enable" + required + "Probe"] = false;
            Reject(() => Bind(options, flags), "signal_epoll_profile_conflict");
        }
        foreach (string conflict in new[] { "M16013", "M07027", "M07028", "M09011", "M09012", "M16010", "M10004", "M16014" })
        {
            var flags = Flags();
            flags["Enable" + conflict + "Probe"] = true;
            Reject(() => Bind(options, flags), "signal_epoll_profile_conflict");
        }
        foreach (string key in new[] { "LinuxThreadingApplication", "LinuxThreadingDisassembly", "LinuxHelloDisassembly" })
        {
            var mixed = new QemuSmokeOptions { SignalEpollRace = true };
            mixed.Explicit.Add(key);
            Reject(() => Bind(mixed, Flags()), "signal_epoll_prepared_application_conflict");
        }
        options.ServiceLifecycle = true;
        Reject(() => Bind(options, Flags()), "signal_epoll_profile_conflict");
    }

    [HostTest("mechanical epoll profile retains socket ancestors while excluding inferred upstream app and runtime timeout")]
    private static void Profile()
    {
        var options = QemuSmokeOptions.Parse(["-BuildEfi", "-EnableM16012Probe", "-SignalEpollRace"]);
        var plan = QemuProfiles.Create(options);
        HostCheck.True(plan.IsEnabled("EnableM16011Probe") && plan.IsEnabled("EnableM16009Probe") && plan.IsEnabled("EnableM07026Probe"));
        HostCheck.True(options.SignalEpollRace && options.LinuxThreadingApplication.Length == 0 && options.LinuxThreadingDisassembly.Length == 0 && options.TimeoutSeconds == 90);
        HostCheck.True(plan.Required.Contains("CLROS_M16_012_READY") && plan.Required.Contains("M16_011_BOUND"));
        var ordinary = QemuSmokeOptions.Parse(["-BuildEfi", "-EnableM16012Probe"]);
        Reject(() => QemuProfiles.Create(ordinary), "socket_disassembly_required");
        HostCheck.Equal("NativeAotSocketSmokeAsync", ordinary.LinuxThreadingApplication);
    }

    [HostTest("mechanical epoll input admission checks canonical bytes under retained read lock and rewinds only success")]
    private static void Package()
    {
        byte[] expected = LinuxSignalProofFixture.CreatePackage(LinuxSignalProofFixture.EpollStolenWaitVariant);
        using (var input = new MemoryStream(expected))
        {
            RequirePackage(input);
            HostCheck.Equal(0L, input.Position);
        }
        foreach (byte[] bytes in new[] { LinuxSignalProofFixture.CreatePackage(LinuxSignalProofFixture.StolenWaitVariant), expected[..^1], expected.Concat(new byte[] { 0 }).ToArray() })
        {
            using var input = new MemoryStream(bytes);
            Reject(() => RequirePackage(input), "signal_epoll_package_identity");
        }
        byte[] altered = (byte[])expected.Clone();
        altered[^1] ^= 1;
        using (var input = new MemoryStream(altered))
            Reject(() => RequirePackage(input), "signal_epoll_package_identity");
        using (var input = new MemoryStream(expected))
        {
            input.Position = 1;
            Reject(() => RequirePackage(input), "signal_epoll_package_identity");
        }
    }

    [HostTest("mechanical epoll network envelope requires zero TCP lifetimes and joins original NIC cleanup")]
    private static void Network()
    {
        var rows = NativeAotSocketIntegrationTests.Envelope(true).ToList();
        int first = rows.IndexOf("TEST_START m16-009"), last = rows.IndexOf("TEST_PASS m16-009");
        rows.RemoveRange(first + 1, last - first - 1);
        void Set(string prefix, string value) => rows[rows.FindIndex(row => row.StartsWith(prefix, StringComparison.Ordinal))] = value;
        Set("M16_011_WIRE ", "M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=0");
        Set("M16_011_END ", "M16_011_END polls=1 received=0 copied=0 fresh=0 retired=0 start=0 end=1 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped");
        Set("M07_026_ROW stage=4 ", "M07_026_ROW stage=4 a=1 b=1 c=1 d=2");
        QemuTranscripts.InspectSignalEpollNetworkForTests(rows.ToArray());
        // This value-only seam checks the envelope; the complete consumer also
        // validates canonical startup, mechanical execution and original PCAP.
        Reject(() => QemuTranscripts.InspectSocketNetworkForTests(rows.ToArray(), true), "socket_transport_complete");
        rows.Insert(first + 1, "M16_011_SLOT event=0");
        Reject(() => QemuTranscripts.InspectSignalEpollNetworkForTests(rows.ToArray()), "signal_epoll_no_tcp_lifetimes");
        rows.RemoveAt(first + 1);
        Set("M16_011_WIRE ", "M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=1");
        Reject(() => QemuTranscripts.InspectSignalEpollNetworkForTests(rows.ToArray()), "socket_network_accounting");
    }

    [HostTest("mechanical epoll packet inspection retains ARP proof and rejects actual TCP traffic")]
    private static void Packets()
    {
        var fixture = NativeAotSocketIntegrationTests.Packets();
        byte[] capture = NativeAotSocketIntegrationTests.Capture(fixture.Frames.Take(8).ToList());
        var report = QemuTranscripts.InspectSignalEpollPacketsForTests(capture, fixture.Anchor, fixture.Arp);
        HostCheck.True(report.Connections == 0 && report.Received == 0 && report.Sent == 0 && report.PeerReceived == 1 && report.PumpTcpCopied == 0);
        Reject(() => QemuTranscripts.InspectSignalEpollPacketsForTests(NativeAotSocketIntegrationTests.Capture(fixture.Frames.Take(9).ToList()), fixture.Anchor, fixture.Arp), "signal_epoll_unexpected_tcp_packet");
        Reject(() => QemuTranscripts.InspectSignalEpollPacketsForTests(NativeAotSocketIntegrationTests.Capture(fixture.Frames.Take(7).ToList()), fixture.Anchor, fixture.Arp), "socket_wire_complete");
    }

    private static void Reject(Action action, string reason)
    {
        try
        {
            action();
            throw new InvalidOperationException("accepted " + reason);
        }
        catch (ToolFailure failure) { HostCheck.True(failure.Message.Contains(reason, StringComparison.Ordinal)); }
    }
}
