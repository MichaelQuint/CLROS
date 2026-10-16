using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static class QemuRunnerHttpProbeTests
{
    [HostFixture(HostLane.Native)]
    public static void Initialize()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-qemu-http-probe-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            Run("qemu.runner.http_probe accepts expected response", () => TestAcceptsExpectedResponse(tempRoot), ref failures);
            Run("qemu.runner.http_probe rejects status mismatch", () => TestRejectsStatusMismatch(tempRoot), ref failures);
            Run("qemu.runner.http_probe rejects body mismatch", () => TestRejectsBodyMismatch(tempRoot), ref failures);
            Run("qemu.runner.http_probe times out deterministically", () => TestTimesOutDeterministically(tempRoot), ref failures);
            Run("qemu.runner.continuing_web validates bounded selection", () => TestContinuingWebSelection(tempRoot), ref failures);
            Run("qemu.runner.continuing_web rejects occupied endpoint before launch", () => TestContinuingWebOccupiedPort(tempRoot), ref failures);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Cleanup failure should not hide probe validation.
            }
        }

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL qemu.runner.http_probe.tests failures=" + failures);
            Environment.Exit(1);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestAcceptsExpectedResponse(string tempRoot)
    {
        const string expectedBody = "OS_MINIMAL_API_OK";

        ProbeRunResult result = RunQemuRunnerWithProbe(
            tempRoot,
            actualStatusCode: 200,
            actualBody: expectedBody,
            hangBeforeResponse: false,
            expectedStatusCode: 200,
            expectedBody,
            timeoutSeconds: 3,
            includeExpectedBody: true);

        string output = result.Process.Output;
        string summary = File.ReadAllText(result.SummaryLogPath);
        string bodyHash = Sha256Utf8(expectedBody);

        bool passed = result.Process.ExitCode == 0
            && output.Contains("HTTP_PROBE_START endpoint=http://127.0.0.1:", StringComparison.Ordinal)
            && output.Contains("HTTP_PROBE_PASS endpoint=http://127.0.0.1:", StringComparison.Ordinal)
            && output.Contains("status=200", StringComparison.Ordinal)
            && output.Contains("body_bytes=17", StringComparison.Ordinal)
            && output.Contains("body_sha256=" + bodyHash, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.smoke", StringComparison.Ordinal)
            && !output.Contains(expectedBody, StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_RESULT pass", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_REASON none", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_STATUS 200", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_BODY_BYTES 17", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_BODY_SHA256 " + bodyHash, StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON none", StringComparison.Ordinal)
            && !summary.Contains(expectedBody, StringComparison.Ordinal);

        return PassOrReport("accepts expected response", passed, result);
    }

    private static bool TestRejectsStatusMismatch(string tempRoot)
    {
        const string expectedBody = "OS_MINIMAL_API_OK";
        const string actualBody = "SERVICE_DOWN";

        ProbeRunResult result = RunQemuRunnerWithProbe(
            tempRoot,
            actualStatusCode: 503,
            actualBody,
            hangBeforeResponse: false,
            expectedStatusCode: 200,
            expectedBody,
            timeoutSeconds: 3,
            includeExpectedBody: true);

        string output = result.Process.Output;
        string summary = File.ReadAllText(result.SummaryLogPath);

        bool passed = result.Process.ExitCode == 9
            && output.Contains("HTTP_PROBE_FAIL endpoint=http://127.0.0.1:", StringComparison.Ordinal)
            && output.Contains("status=503", StringComparison.Ordinal)
            && output.Contains("reason=status_mismatch", StringComparison.Ordinal)
            && output.Contains("OS_SMOKE_FAIL http-probe=status_mismatch", StringComparison.Ordinal)
            && output.Contains("TEST_FAIL qemu.runner.http_probe reason=status_mismatch", StringComparison.Ordinal)
            && !output.Contains(expectedBody, StringComparison.Ordinal)
            && !output.Contains(actualBody, StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_RESULT fail", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_REASON status_mismatch", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_STATUS 503", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON http_probe_status_mismatch", StringComparison.Ordinal)
            && !summary.Contains(expectedBody, StringComparison.Ordinal)
            && !summary.Contains(actualBody, StringComparison.Ordinal);

        return PassOrReport("rejects status mismatch", passed, result);
    }

    private static bool TestRejectsBodyMismatch(string tempRoot)
    {
        const string expectedBody = "OS_MINIMAL_API_OK";
        const string actualBody = "WRONG_BODY";

        ProbeRunResult result = RunQemuRunnerWithProbe(
            tempRoot,
            actualStatusCode: 200,
            actualBody,
            hangBeforeResponse: false,
            expectedStatusCode: 200,
            expectedBody,
            timeoutSeconds: 3,
            includeExpectedBody: true);

        string output = result.Process.Output;
        string summary = File.ReadAllText(result.SummaryLogPath);

        bool passed = result.Process.ExitCode == 9
            && output.Contains("HTTP_PROBE_FAIL endpoint=http://127.0.0.1:", StringComparison.Ordinal)
            && output.Contains("status=200", StringComparison.Ordinal)
            && output.Contains("reason=body_mismatch", StringComparison.Ordinal)
            && output.Contains("TEST_FAIL qemu.runner.http_probe reason=body_mismatch", StringComparison.Ordinal)
            && !output.Contains(expectedBody, StringComparison.Ordinal)
            && !output.Contains(actualBody, StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_RESULT fail", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_REASON body_mismatch", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_STATUS 200", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON http_probe_body_mismatch", StringComparison.Ordinal)
            && !summary.Contains(expectedBody, StringComparison.Ordinal)
            && !summary.Contains(actualBody, StringComparison.Ordinal);

        return PassOrReport("rejects body mismatch", passed, result);
    }

    private static bool TestTimesOutDeterministically(string tempRoot)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        ProbeRunResult result = RunQemuRunnerWithProbe(
            tempRoot,
            actualStatusCode: 200,
            actualBody: "unused",
            hangBeforeResponse: true,
            expectedStatusCode: 200,
            expectedBody: string.Empty,
            timeoutSeconds: 1,
            includeExpectedBody: false);

        stopwatch.Stop();
        string output = result.Process.Output;
        string summary = File.ReadAllText(result.SummaryLogPath);

        bool passed = result.Process.ExitCode == 9
            && stopwatch.ElapsedMilliseconds < 10000
            && output.Contains("HTTP_PROBE_FAIL endpoint=http://127.0.0.1:", StringComparison.Ordinal)
            && output.Contains("reason=timeout", StringComparison.Ordinal)
            && output.Contains("TEST_FAIL qemu.runner.http_probe reason=timeout", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_RESULT fail", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_REASON timeout", StringComparison.Ordinal)
            && summary.Contains("HTTP_PROBE_STATUS none", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON http_probe_timeout", StringComparison.Ordinal);

        return PassOrReport("times out deterministically", passed, result);
    }

    private static bool PassOrReport(string scenario, bool passed, ProbeRunResult result)
    {
        if (passed)
        {
            return true;
        }

        Console.WriteLine(
            "TEST_FAIL_DETAIL qemu.runner.http_probe scenario=" + scenario +
            " exit=" + result.Process.ExitCode);
        Console.WriteLine("TEST_FAIL_DETAIL qemu.runner.http_probe.output.begin");
        Console.Write(result.Process.Output);
        Console.WriteLine("TEST_FAIL_DETAIL qemu.runner.http_probe.output.end");
        if (File.Exists(result.SummaryLogPath))
        {
            Console.WriteLine("TEST_FAIL_DETAIL qemu.runner.http_probe.summary.begin");
            Console.Write(File.ReadAllText(result.SummaryLogPath));
            Console.WriteLine("TEST_FAIL_DETAIL qemu.runner.http_probe.summary.end");
        }

        return false;
    }

    private static List<string> ContinuingWebArguments(string tempRoot, int port)
        => ["--continuing-web", "--socket-forward-port", port.ToString(),
            "--virtio-negotiation-device", "--arp-receive-fixture",
            "--ethernet-capture", Path.Combine(tempRoot, "web-" + Guid.NewGuid().ToString("N") + ".pcap"),
            "--summary-log", Path.Combine(tempRoot, "web-" + Guid.NewGuid().ToString("N") + ".summary"),
            "--replace-default-required-markers", "--http-probe-url", "http://127.0.0.1:" + port + "/healthz",
            "--http-probe-expected-body", "OS_MINIMAL_API_OK", "--web-config-body", "{\"selected\":true}"];

    private static bool TestContinuingWebSelection(string tempRoot)
    {
        List<string> valid = ContinuingWebArguments(tempRoot, 20080);
        valid.AddRange(["--require-marker", "OS_SMOKE_PASS"]);
        ProcessResult accepted = RunQemuRunner([.. valid, "--dry-run"]);
        if (accepted.ExitCode != 0 || !accepted.Output.Contains("CLROS_M10_004_READY", StringComparison.Ordinal) ||
            !accepted.Output.Contains("TEST_PASS m10.004", StringComparison.Ordinal) ||
            !accepted.Output.Contains("OS_SMOKE_PASS", StringComparison.Ordinal) ||
            !accepted.Output.Contains("hostfwd=tcp:127.0.0.1:20080-:8080", StringComparison.Ordinal) ||
            accepted.Output.Contains("selected", StringComparison.Ordinal)) return false;
        bool Reject(string key, string? value = null, bool remove = false)
        {
            List<string> changed = [.. valid];
            int index = changed.IndexOf(key);
            if (remove) { changed.RemoveAt(index); if (value is not null) changed.RemoveAt(index); }
            else if (index >= 0 && value is not null) changed[index + 1] = value;
            else { changed.Add(key); if (value is not null) changed.Add(value); }
            ProcessResult result = RunQemuRunner([.. changed, "--dry-run"]);
            return result.ExitCode == 1 && result.Output.Contains("qemu.runner.invalid_args", StringComparison.Ordinal);
        }
        return Reject("--continuing-web", remove: true)
            && Reject("--web-config-body", "", remove: true)
            && Reject("--socket-forward-port", "", remove: true)
            && Reject("--http-probe-url", "http://127.0.0.1:20081/healthz")
            && Reject("--http-probe-url", "http://localhost:20080/healthz")
            && Reject("--http-probe-url", "http://127.0.0.1:20080/config")
            && Reject("--http-probe-url", "http://127.0.0.1:20080/healthz?x=1")
            && Reject("--http-probe-url", "http://127.0.0.1:20080/healthz#x")
            && Reject("--http-probe-url", "http://user@127.0.0.1:20080/healthz")
            && Reject("--http-probe-expected-status", "201")
            && Reject("--http-probe-expected-body", "")
            && Reject("--http-probe-timeout", "31")
            && Reject("--timeout", "286")
            && Reject("--workload-profile", "MultiMinimalApi")
            && Reject("--continuing-network-peer");
    }

    private static bool TestContinuingWebOccupiedPort(string tempRoot)
    {
        using TcpListener occupied = new(IPAddress.Loopback, 0);
        occupied.Start(1);
        int port = ((IPEndPoint)occupied.LocalEndpoint).Port;
        List<string> arguments = ContinuingWebArguments(tempRoot, port);
        string summaryPath = arguments[arguments.IndexOf("--summary-log") + 1];
        FakeQemuHttpEndpoint fake = CreateFakeQemuHttpEndpoint(tempRoot, 200, "unused", false);
        arguments.AddRange(["--qemu", fake.QemuPath,
            "--image", CreateEmptyFile(tempRoot, "occupied-web.raw"),
            "--ovmf-code", CreateEmptyFile(tempRoot, "occupied-web-OVMF_CODE.fd"),
            "--serial-log", Path.Combine(tempRoot, "occupied-web.serial")]);
        ProcessResult result = RunQemuRunner([.. arguments]);
        string summary = File.Exists(summaryPath) ? File.ReadAllText(summaryPath) : string.Empty;
        return result.ExitCode == 20 && summary.Contains("FAIL_REASON web_endpoint_occupied_before_launch", StringComparison.Ordinal)
            && summary.Contains("WEB_ENDPOINT_PRELAUNCH_AVAILABLE false", StringComparison.Ordinal)
            && summary.Contains("WEB_PROCESS_ID none", StringComparison.Ordinal)
            && summary.Contains("WEB_CONFIG_PROBE_RESULT not_run", StringComparison.Ordinal);
    }

    private static ProbeRunResult RunQemuRunnerWithProbe(
        string tempRoot,
        int actualStatusCode,
        string actualBody,
        bool hangBeforeResponse,
        int expectedStatusCode,
        string expectedBody,
        int timeoutSeconds,
        bool includeExpectedBody)
    {
        string imagePath = CreateEmptyFile(tempRoot, "http-probe-" + Guid.NewGuid().ToString("N") + ".raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "http-probe-" + Guid.NewGuid().ToString("N") + "-OVMF_CODE.fd");
        string serialLog = Path.Combine(tempRoot, "http-probe-" + Guid.NewGuid().ToString("N") + "-serial.log");
        string summaryLog = Path.Combine(tempRoot, "http-probe-" + Guid.NewGuid().ToString("N") + "-summary.log");
        FakeQemuHttpEndpoint endpoint =
            CreateFakeQemuHttpEndpoint(tempRoot, actualStatusCode, actualBody, hangBeforeResponse);

        List<string> args =
        [
            "--qemu",
            endpoint.QemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "5",
            "--http-probe-url",
            endpoint.Url,
            "--http-probe-expected-status",
            expectedStatusCode.ToString(),
            "--http-probe-timeout",
            timeoutSeconds.ToString()
        ];

        if (includeExpectedBody)
        {
            args.Add("--http-probe-expected-body");
            args.Add(expectedBody);
        }

        return new ProbeRunResult(RunQemuRunner(args.ToArray()), summaryLog);
    }

    private static FakeQemuHttpEndpoint CreateFakeQemuHttpEndpoint(
        string tempRoot,
        int statusCode,
        string body,
        bool hangBeforeResponse)
    {
        int port = ReserveFreeLoopbackPort();
        string qemuScript = Path.Combine(tempRoot, "fake-qemu-" + Guid.NewGuid().ToString("N") + ".cmd");

        List<string> script =
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto serve",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":serve",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12",
            QemuTestControl.Command("http", "%serial%", port.ToString(), statusCode.ToString(), Convert.ToBase64String(Encoding.UTF8.GetBytes(body)), hangBeforeResponse ? "1" : "0"),
            "exit /b %ERRORLEVEL%"
        ];

        File.WriteAllLines(qemuScript, script, Encoding.ASCII);
        return new FakeQemuHttpEndpoint(qemuScript, "http://127.0.0.1:" + port + "/healthz");
    }

    private static int ReserveFreeLoopbackPort()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string CreateEmptyFile(string tempRoot, string fileName)
    {
        string path = Path.Combine(tempRoot, fileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static ProcessResult RunQemuRunner(params string[] args)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string toolPath = HostToolProcess.GetBuiltToolPath(repositoryRoot, "OS.QemuRunner");
        if (!File.Exists(toolPath))
        {
            return new ProcessResult(-1, "qemu runner tool not found: " + toolPath);
        }

        ProcessStartInfo startInfo = HostToolProcess.CreateDotnetToolStartInfo(repositoryRoot, "OS.QemuRunner");

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        bool exited = process.WaitForExit(15000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("OS.QemuRunner did not exit within 15 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
    }

    private static string Sha256Utf8(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private readonly struct FakeQemuHttpEndpoint
    {
        public FakeQemuHttpEndpoint(string qemuPath, string url)
        {
            QemuPath = qemuPath;
            Url = url;
        }

        public string QemuPath { get; }

        public string Url { get; }
    }

    private readonly struct ProbeRunResult
    {
        public ProbeRunResult(ProcessResult process, string summaryLogPath)
        {
            Process = process;
            SummaryLogPath = summaryLogPath;
        }

        public ProcessResult Process { get; }

        public string SummaryLogPath { get; }
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; }

        public string Output { get; }
    }
}
