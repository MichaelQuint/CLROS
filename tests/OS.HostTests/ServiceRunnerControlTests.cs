using System.Diagnostics;

internal static class ServiceRunnerControlTests
{
    [HostTest("service runner CLI admits bounded modern captured service dry run without creating owners", HostLane.Fast, 0)]
    private static void Selection()
    {
        foreach (string timeout in new[] { "780", "900" })
        {
            string[] args = Fixture(timeout); var result = Run(args);
            HostCheck.Equal(0, result.ExitCode); HostCheck.Equal("", result.Error);
            HostCheck.True(result.Output.Contains("CONTINUING_SERVICE true", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("REQUIRED_MARKERS TEST_PASS m16-012", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("hostfwd=tcp:127.0.0.1:20880-:8080", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("disable-legacy=on", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("filter-dump,id=clroscapture", StringComparison.Ordinal));
            HostCheck.True(result.Output.Contains("snapshot=on",StringComparison.Ordinal) && result.Output.IndexOf("filter-redirector,id=clroswebfilter",StringComparison.Ordinal)>=0 &&
                result.Output.IndexOf("filter-redirector,id=clroswebfilter",StringComparison.Ordinal)<result.Output.IndexOf("filter-dump,id=clroscapture",StringComparison.Ordinal));
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
        foreach (string timeout in new[] { "779", "901" }) Reject(Fixture(timeout));
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
            Reject([..Fixture(), ..conflict]);
    }

    [HostTest("service runner CLI preserves ordinary dry run schema when service mode is absent", HostLane.Fast, 3)]
    private static void Legacy()
    {
        var result = Run(Remove(Fixture("30"), "--continuing-service", false));
        HostCheck.Equal(0, result.ExitCode); HostCheck.Equal("", result.Error);
        HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("CONTINUING_SERVICE", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("SERVICE_PROCESS_", StringComparison.Ordinal));
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
    { int at = Array.IndexOf(args, flag); return args.Where((_, i) => i != at && (!value || i != at + 1)).ToArray(); }
    private static string[] Replace(string[] args, string flag, string value)
    { args[Array.IndexOf(args, flag) + 1] = value; return args; }
    private static void Reject(string[] args)
    {
        var result = Run(args); HostCheck.Equal(1, result.ExitCode);
        HostCheck.True(result.Error.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal));
        HostCheck.True(!result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
    }
    private static HostToolProcess.ToolResult Run(string[] args)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ProcessStartInfo start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.QemuRunner");
        foreach (string argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Runner child did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(20_000))
        {
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000)) throw new TimeoutException("Runner child cleanup incomplete.");
            throw new TimeoutException("Runner CLI timed out.");
        }
        return new(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
