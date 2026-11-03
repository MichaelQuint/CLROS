using System.Diagnostics;
using System.Text;
using OS.Tools.QemuRunner;

internal static class ServiceRunnerControlTests
{
    [HostTest("finite static runner keeps the held base immutable and declares UTC before guest entry")]
    private static void StaticImageOwnership()
    {
        string[] args = Fixture("675");
        args[Array.IndexOf(args, "--continuing-service")] = "--finite-static-files";
        var result = Run(args);
        HostCheck.Equal(0, result.ExitCode);
        HostCheck.Equal("", result.Error);
        HostCheck.True(result.Output.Contains("if=none,id=esp,format=raw,snapshot=on,file=", StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("-rtc base=utc,clock=host", StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("CLROS_M10_017_READY", StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("TEST_PASS m16-012", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("filter-redirector", StringComparison.Ordinal));
        foreach (string flag in new[] { "--image", "--serial-log", "--summary-log", "--ethernet-capture" })
            HostCheck.True(!File.Exists(args[Array.IndexOf(args, flag) + 1]));
        Reject([.. args, "--continuing-web"]);
        Reject([.. args, "--continuing-service"]);
        Reject(Replace((string[])args.Clone(), "--timeout", "599"));
        Reject(Replace((string[])args.Clone(), "--timeout", "901"));
    }

    [HostTest("service runner CLI admits bounded modern captured service dry run without creating owners", HostLane.Fast, 0)]
    private static void Selection()
    {
        foreach (string timeout in new[] { "780", "900", "1080" })
        {
            string[] args = Fixture(timeout);
            var result = Run(args);
            HostCheck.Equal(0, result.ExitCode);
            HostCheck.Equal("", result.Error);
            HostCheck.True(result.Output.Contains("CONTINUING_SERVICE true", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("REQUIRED_MARKERS TEST_PASS m16-012", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("hostfwd=tcp:127.0.0.1:20880-:8080", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("disable-legacy=on", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("filter-dump,id=clroscapture", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("snapshot=on", StringComparison.Ordinal) && result.Output.IndexOf("filter-redirector,id=clroswebfilter", StringComparison.Ordinal) >= 0 &&
                result.Output.IndexOf("filter-redirector,id=clroswebfilter", StringComparison.Ordinal) < result.Output.IndexOf("filter-dump,id=clroscapture", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("virtio-rng-pci", StringComparison.Ordinal));
            HostCheck.True(!result.Output.Contains("SERVICE_SOCKET_CONTROLS ", StringComparison.Ordinal));
            foreach (string flag in new[] { "--image", "--serial-log", "--summary-log", "--ethernet-capture" })
                HostCheck.True(!File.Exists(args[Array.IndexOf(args, flag) + 1]));
        }
    }

    [HostTest("service runner CLI rejects missing captures forwards summary RNG and lifetime bounds", HostLane.Fast, 1)]
    private static void RequiredInputs()
    {
        foreach (string flag in new[] { "--virtio-negotiation-device", "--arp-receive-fixture", "--ethernet-capture", "--socket-forward-port", "--summary-log", "--firmware-rng-source" })
            Reject(Remove(Fixture(), flag, flag is not ("--virtio-negotiation-device" or "--arp-receive-fixture")));
        foreach (string timeout in new[] { "779", "1081" })
            Reject(Fixture(timeout));
        Reject(Replace(Fixture(), "--workload-profile", "MultiMinimalApi"));
        Reject(Replace(Fixture(), "--firmware-rng-source", "missing"));
    }

    [HostTest("service runner CLI rejects web scripted peer HTTP panic and duplicate unions", HostLane.Fast, 2)]
    private static void Conflicts()
    {
        foreach (string[] conflict in new string[][] { ["--continuing-web"], ["--continuing-network-peer"], ["--socket-containment-peer"],
            ["--http-probe-url", "http://127.0.0.1:20880/healthz"], ["--http-probe-expected-status", "200"],
            ["--http-probe-expected-body", "body"], ["--http-probe-timeout", "1"], ["--web-config-body", "config"],
            ["--web-log-case", "normal"], ["--expect-unexpected-vector-panic"], ["--expect-owned-subsystem-panic"],
            ["--expect-unhandled-exception"], ["--continuing-service"] })
            Reject([.. Fixture(), .. conflict]);
    }

    [HostTest("service runner CLI preserves ordinary dry run schema when service mode is absent", HostLane.Fast, 3)]
    private static void Legacy()
    {
        var result = Run(Remove(Fixture("30"), "--continuing-service", false));
        HostCheck.Equal(0, result.ExitCode);
        HostCheck.Equal("", result.Error);
        HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("CONTINUING_SERVICE", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("SERVICE_PROCESS_", StringComparison.Ordinal));
    }

    [HostTest("service marker stream accepts exactly64MiB rejects excess and preserves ordinary16MiB", HostLane.Fast, 4)]
    private static void SerialCapacity()
    {
        HostCheck.Equal(16L * 1024 * 1024, SerialMarkerStateMachine.DefaultByteCapacity);
        HostCheck.Equal(64L * 1024 * 1024, SerialMarkerStateMachine.ContinuingServiceByteCapacity);
        var ordinary = new SerialMarkerStateMachine(["TEST_PASS m16-012"], ["OS_SMOKE_FAIL"], successTerminalMarker: "TEST_PASS m16-012");
        var service = new SerialMarkerStateMachine(["TEST_PASS m16-012"], ["OS_SMOKE_FAIL"], successTerminalMarker: "TEST_PASS m16-012",
            byteCapacity: SerialMarkerStateMachine.ContinuingServiceByteCapacity);
        byte[] chunk = Encoding.ASCII.GetBytes(new string('x', 4094) + "\r\n");
        for (long total = 0; total < SerialMarkerStateMachine.DefaultByteCapacity; total += chunk.Length)
        {
            HostCheck.True(ordinary.Feed(chunk).Succeeded);
            HostCheck.True(service.Feed(chunk).Succeeded);
        }
        HostCheck.Equal(SerialMarkerFeedStatus.TotalCapacityExceeded, ordinary.Feed([13]).Status);
        HostCheck.Equal(SerialMarkerStateMachine.DefaultByteCapacity, ordinary.TotalBytes);
        while (service.TotalBytes < SerialMarkerStateMachine.ContinuingServiceByteCapacity - chunk.Length)
            HostCheck.True(service.Feed(chunk).Succeeded);
        byte[] terminal = Encoding.ASCII.GetBytes("TEST_PASS m16-012\r\n");
        chunk.AsSpan().Fill((byte)'x');
        chunk[chunk.Length - terminal.Length - 2] = 13;
        chunk[chunk.Length - terminal.Length - 1] = 10;
        terminal.CopyTo(chunk, chunk.Length - terminal.Length);
        HostCheck.True(service.Feed(chunk).Succeeded);
        HostCheck.Equal(1, service.SuccessTerminalCount);
        HostCheck.Equal(SerialMarkerStateMachine.ContinuingServiceByteCapacity, service.TotalBytes);
        HostCheck.Equal(SerialMarkerFeedStatus.TotalCapacityExceeded, service.Feed([13]).Status);
        HostCheck.Equal(SerialMarkerStateMachine.ContinuingServiceByteCapacity, service.TotalBytes);
    }

    [HostTest("quota runner CLI selects only explicit custom capture with default forbidden rules intact")]
    private static void QuotaSelection()
    {
        var result = Run([.. Fixture(), "--service-quota-failure"]);
        HostCheck.Equal(0, result.ExitCode);
        HostCheck.Equal("", result.Error);
        HostCheck.True(result.Output.Contains("SERVICE_QUOTA_FAILURE true", StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("REQUIRED_MARKERS " + ServiceQuotaCapture.Terminal, StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("REQUIRED_MARKERS TEST_PASS m16-012", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("SERVICE_SOCKET_CONTROLS ", StringComparison.Ordinal));
        var boot = Run([..Fixture(), "--service-quota-failure", "--require-marker", ServiceQuotaCapture.EarlyBootSmokePass,
            "--require-ordered-marker", ServiceQuotaCapture.EarlyBootSmokePass, "--require-ordered-marker", "TEST_START m16-011",
            "--require-unique-marker", ServiceQuotaCapture.EarlyBootSmokePass]);
        HostCheck.Equal(0, boot.ExitCode);
        HostCheck.Equal("", boot.Error);
    }

    [HostTest("quota runner CLI refuses missing real service inputs mode unions and ordinary success requirements")]
    private static void QuotaInputs()
    {
        Reject([.. Fixture("901"), "--service-quota-failure"]);
        foreach (string flag in new[] { "--continuing-service", "--replace-default-required-markers", "--virtio-negotiation-device",
            "--arp-receive-fixture", "--ethernet-capture", "--socket-forward-port", "--summary-log", "--firmware-rng-source" })
            Reject([.. Remove(Fixture(), flag, flag is "--ethernet-capture" or "--socket-forward-port" or "--summary-log" or "--firmware-rng-source"), "--service-quota-failure"]);
        foreach (string[] extra in new string[][] { ["--service-quota-failure"], ["--continuing-web"], ["--expect-owned-subsystem-panic"],
            ["--require-marker", "TEST_PASS m16-012"], ["--require-unique-marker", "TEST_PASS m16.014"] })
            Reject([.. Fixture(), "--service-quota-failure", .. extra]);
        foreach (string flag in new[] { "--require-marker", "--require-ordered-marker", "--require-unique-marker" })
            Reject([.. Fixture(), "--service-quota-failure", flag, ServiceQuotaCapture.EarlyBootSmokePass + " forged"]);
        Reject([.. Fixture(), "--service-quota-failure=true"]);
        Reject([.. Fixture(), "--service-quota-failure", "false"]);
    }

    [HostTest("quota capture value shape requires actual failure row exact terminal and no trailing ordinary success")]
    private static void QuotaTrailer()
    {
        string before = "TEST_START m16.014\r\n" + ServiceQuotaCapture.ProcessFailure + "\r\n";
        string terminal = ServiceQuotaCapture.Terminal + "\r\n";
        HostCheck.True(ServiceQuotaCapture.Failure(before + terminal) is null);
        foreach (string bad in new[] { before, terminal, before + terminal + "\r\n", before + terminal + "x",
            before + terminal + terminal, before + ServiceQuotaCapture.Terminal + " suffix\r\n", before + ServiceQuotaCapture.Terminal + "\n",
            before + ServiceQuotaCapture.ProcessFailure + "\r\n" + terminal, terminal + before,
            before.Replace("exit=99", "exit=0", StringComparison.Ordinal) + terminal,
            before.Replace("cause=physical-backing", "cause=app", StringComparison.Ordinal) + terminal })
            HostCheck.True(ServiceQuotaCapture.Failure(bad) is not null);
        foreach (string marker in ServiceQuotaCapture.ForbiddenSuccessMarkers)
            HostCheck.True(ServiceQuotaCapture.Failure(before + marker + "\r\n" + terminal) is not null);
        HostCheck.True(ServiceQuotaCapture.Failure(null) is not null);
    }

    [HostTest("quota capture permits only one exact inherited boot pass before the unique service gate")]
    private static void QuotaBootPrefix()
    {
        string pass = ServiceQuotaCapture.EarlyBootSmokePass + "\r\n", gate = "TEST_START m16-011\r\n";
        string trailer = ServiceQuotaCapture.ProcessFailure + "\r\n" + ServiceQuotaCapture.Terminal + "\r\n";
        HostCheck.True(ServiceQuotaCapture.Failure("OS_BOOT_OK\r\nTEST_PASS boot.sequence\r\n" + pass + gate + "TEST_PASS m16.014.startup\r\n" + trailer) is null);
        HostCheck.True(ServiceQuotaCapture.Failure(gate + trailer) is null);
        foreach (var (prefix, reason) in new[] {
            (pass, "quota_boot_success_without_service_start"),
            (pass + pass + gate, "quota_boot_success_shape"),
            (gate + pass, "quota_boot_success_shape"),
            (pass + gate + pass, "quota_boot_success_shape"),
            (ServiceQuotaCapture.EarlyBootSmokePass + " suffix\r\n" + gate, "quota_boot_success_shape"),
            (ServiceQuotaCapture.EarlyBootSmokePass + ".suffix\r\n" + gate, "quota_boot_success_shape"),
            (ServiceQuotaCapture.EarlyBootSmokePass + "\n" + gate, "quota_boot_success_shape"),
            (pass + gate + gate, "quota_service_start_shape"),
            (pass + "TEST_START m16-011 suffix\r\n", "quota_service_start_shape"),
            (pass + "TEST_START m16-011\n", "quota_service_start_shape") })
            HostCheck.Equal(reason, ServiceQuotaCapture.Failure(prefix + trailer) ?? "accepted");
    }

    private static string[] Fixture(string timeout = "780")
    {
        string path = Path.Combine(Path.GetTempPath(), "clros-service-runner-" + Guid.NewGuid().ToString("N"));
        return ["--dry-run", "--qemu", "qemu-service-test", "--image", path + ".raw", "--serial-log", path + ".serial",
            "--summary-log", path + ".summary", "--virtio-negotiation-device", "--arp-receive-fixture", "--ethernet-capture", path + ".pcap",
            "--socket-forward-port", "20880", "--workload-profile", "SingleMinimalApi", "--firmware-rng-source", "virtio",
            "--replace-default-required-markers", "--timeout", timeout, "--continuing-service"];
    }
    private static string[] Remove(string[] args, string flag, bool value)
    {
        int at = Array.IndexOf(args, flag);
        return args.Where((_, i) => i != at && (!value || i != at + 1)).ToArray();
    }
    private static string[] Replace(string[] args, string flag, string value)
    {
        args[Array.IndexOf(args, flag) + 1] = value;
        return args;
    }
    private static void Reject(string[] args)
    {
        var result = Run(args);
        HostCheck.Equal(1, result.ExitCode);
        HostCheck.True(result.Error.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
    }
    private static HostToolProcess.ToolResult Run(string[] args)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ProcessStartInfo start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.QemuRunner");
        foreach (string argument in args)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Runner child did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(20_000))
        {
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000))
                throw new TimeoutException("Runner child cleanup incomplete.");
            throw new TimeoutException("Runner CLI timed out.");
        }
        return new(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
