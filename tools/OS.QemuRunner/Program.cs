using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

try
{
    RunnerOptions options = RunnerOptions.Parse(args);

    if (options.ShowHelp)
    {
        RunnerOptions.PrintHelp();
        return 0;
    }

    if (options.Preflight)
    {
        return RunPreflight(options);
    }

    if (options.DryRun)
    {
        Console.WriteLine("TEST_START qemu.runner.dryrun");
        Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
        Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
        Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
        Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
        Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));
        if (options.HttpProbe is not null)
        {
            WriteHttpProbe(options.HttpProbe.Value);
        }

        Console.WriteLine(QemuCommandBuilder.BuildCommandLine(options, options.OvmfCodePath));
        Console.WriteLine("TEST_PASS qemu.runner.dryrun");
        return 0;
    }

    if (string.IsNullOrWhiteSpace(options.ImagePath) || !File.Exists(options.ImagePath))
    {
        Console.Error.WriteLine("TEST_FAIL qemu.smoke image_not_found");
        Console.Error.WriteLine("Image path is required and must exist. Use --image <path>.");
        WriteSummaryLogBestEffort(
            options,
            "fail",
            Array.Empty<string>(),
            options.RequiredMarkers,
            Array.Empty<string>(),
            "image_not_found");
        return 2;
    }

    DiscoveryResult qemu = ResolveExecutable(options.QemuPath);
    DiscoveryResult ovmf = ResolveOvmfCode(options.OvmfCodePath, qemu.Path);

    WriteDiscovery("QEMU_DISCOVERY", qemu);
    WriteDiscovery("OVMF_DISCOVERY", ovmf);

    if (qemu.Path is null || ovmf.Path is null)
    {
        List<string> missing = new List<string>();
        if (qemu.Path is null)
        {
            missing.Add("qemu_not_found");
        }

        if (ovmf.Path is null)
        {
            missing.Add("ovmf_not_found");
        }

        Console.Error.WriteLine("TEST_FAIL qemu.smoke " + string.Join(" ", missing));
        WriteSummaryLogBestEffort(
            options,
            "fail",
            Array.Empty<string>(),
            options.RequiredMarkers,
            Array.Empty<string>(),
            FormatSummaryList(missing));
        return qemu.Path is null ? 3 : 6;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SerialLogPath)) ?? ".");
    if (File.Exists(options.SerialLogPath))
    {
        File.Delete(options.SerialLogPath);
    }

    Console.WriteLine("TEST_START qemu.smoke");
    Console.WriteLine("IMAGE_ARTIFACT " + options.ImagePath);
    Console.WriteLine("SERIAL_LOG " + options.SerialLogPath);
    Console.WriteLine("OVMF_CODE " + ovmf.Path);
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));

    using Process process = new Process();
    process.StartInfo.FileName = qemu.Path;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardError = true;
    process.StartInfo.RedirectStandardOutput = true;

    foreach (string argument in QemuCommandBuilder.BuildArguments(options, ovmf.Path))
    {
        process.StartInfo.ArgumentList.Add(argument);
    }

    process.Start();
    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
    Task<string> stderr = process.StandardError.ReadToEndAsync();

    bool markerSeen = false;
    bool forbiddenMarkerSeen = false;
    bool timedOut = false;
    Stopwatch stopwatch = Stopwatch.StartNew();
    while (true)
    {
        string currentSerial = ReadSerialLog(options.SerialLogPath);
        SerialMarkerSummary currentSummary = SerialMarkerSummary.Parse(
            currentSerial,
            options.RequiredMarkers,
            options.ForbiddenMarkers,
            options.OrderedMarkers);
        if (currentSummary.HasForbiddenMarkers)
        {
            forbiddenMarkerSeen = true;
            KillProcess(process);
            break;
        }

        if (currentSummary.HasAllRequiredMarkers)
        {
            markerSeen = true;
            if (options.HttpProbe is null)
            {
                KillProcess(process);
            }

            break;
        }

        if (process.HasExited)
        {
            break;
        }

        if (stopwatch.ElapsedMilliseconds >= options.TimeoutSeconds * 1000L)
        {
            timedOut = true;
            KillProcess(process);
            break;
        }

        Thread.Sleep(100);
    }

    HttpProbeResult? httpProbeResult = null;
    string preProbeSerial = ReadSerialLog(options.SerialLogPath);
    SerialMarkerSummary preProbeSummary = SerialMarkerSummary.Parse(
        preProbeSerial,
        options.RequiredMarkers,
        options.ForbiddenMarkers,
        options.OrderedMarkers);
    if (options.HttpProbe is not null
        && preProbeSummary.HasAllRequiredMarkers
        && !preProbeSummary.HasForbiddenMarkers)
    {
        httpProbeResult = ExecuteHttpProbe(options.HttpProbe.Value);
        KillProcess(process);
    }

    string qemuOutput = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
    if (!string.IsNullOrWhiteSpace(qemuOutput))
    {
        Console.Error.WriteLine("QEMU_OUTPUT_BEGIN");
        Console.Error.Write(qemuOutput);
        Console.Error.WriteLine("QEMU_OUTPUT_END");
    }

    string serial = ReadSerialLog(options.SerialLogPath);
    SerialMarkerSummary summary = SerialMarkerSummary.Parse(
        serial,
        options.RequiredMarkers,
        options.ForbiddenMarkers,
        options.OrderedMarkers);
    IReadOnlyList<string> observedMarkerLines = summary.ObservedMarkerLines;
    IReadOnlyList<string> forbiddenMarkers = summary.ForbiddenMarkerHits;
    IReadOnlyList<string> outOfOrderMarkers = summary.OutOfOrderMarkers;
    if (forbiddenMarkerSeen || forbiddenMarkers.Count != 0)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            forbiddenMarkers,
            "forbidden_marker",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers);
        string firstForbiddenMarker = forbiddenMarkers.Count == 0 ? "unknown" : forbiddenMarkers[0];
        Console.Error.WriteLine("OS_SMOKE_FAIL forbidden-marker=" + firstForbiddenMarker);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke forbidden_marker marker=" + firstForbiddenMarker);
        return 8;
    }

    if (outOfOrderMarkers.Count != 0 && summary.MissingMarkers.Count == 0)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            Array.Empty<string>(),
            Array.Empty<string>(),
            "out_of_order_marker",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke out_of_order_marker markers=" + FormatMarkers(outOfOrderMarkers));
        return 10;
    }

    if (markerSeen || summary.HasAllRequiredMarkers)
    {
        if (options.HttpProbe is not null)
        {
            httpProbeResult ??= ExecuteHttpProbe(options.HttpProbe.Value);
            KillProcess(process);

            if (!httpProbeResult.Value.Passed)
            {
                WriteSummaryLogBestEffort(
                    options,
                    "fail",
                    observedMarkerLines,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "http_probe_" + httpProbeResult.Value.Reason,
                    httpProbeResult,
                    summary.AppExitResults,
                    summary.MalformedAppExitMarkers,
                    outOfOrderMarkers);
                Console.Error.WriteLine("OS_SMOKE_FAIL http-probe=" + httpProbeResult.Value.Reason);
                Console.Error.WriteLine("TEST_FAIL qemu.runner.http_probe reason=" + httpProbeResult.Value.Reason);
                return 9;
            }
        }

        WriteSummaryLogBestEffort(
            options,
            "pass",
            observedMarkerLines,
            Array.Empty<string>(),
            Array.Empty<string>(),
            "none",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers);
        Console.WriteLine("TEST_PASS qemu.smoke");
        return 0;
    }

    IReadOnlyList<string> missingMarkers = summary.MissingMarkers;
    if (timedOut)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            missingMarkers,
            Array.Empty<string>(),
            "timeout",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke timeout");
        return 4;
    }

    WriteSummaryLogBestEffort(
        options,
        "fail",
        observedMarkerLines,
        missingMarkers,
        Array.Empty<string>(),
        "missing_marker",
        httpProbeResult,
        summary.AppExitResults,
        summary.MalformedAppExitMarkers,
        outOfOrderMarkers);
    Console.Error.WriteLine("TEST_FAIL qemu.smoke missing_marker required=" + FormatMarkers(options.RequiredMarkers) + " missing=" + FormatMarkers(missingMarkers));
    return 5;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("TEST_FAIL qemu.runner.invalid_args " + ex.Message);
    return 1;
}

static void KillProcess(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }
    catch
    {
        // Process cleanup failure should not hide smoke status.
    }
}

static string FormatMarkers(IReadOnlyList<string> markers)
{
    return string.Join(";", markers);
}

static string FormatSummaryList(IReadOnlyList<string> values)
{
    return values.Count == 0
        ? "none"
        : FormatMarkers(values);
}

static int RunPreflight(RunnerOptions options)
{
    Console.WriteLine("TEST_START qemu.runner.preflight");
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));

    if (!File.Exists(options.ImagePath))
    {
        Console.WriteLine("QEMU_PREFLIGHT_IMAGE path=" + options.ImagePath + " exists=false bytes=0");
        return FailPreflight("image_not_found", 2);
    }

    long imageBytes;
    try
    {
        imageBytes = new FileInfo(options.ImagePath).Length;
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
    {
        Console.WriteLine("QEMU_PREFLIGHT_IMAGE path=" + options.ImagePath + " exists=true bytes=unknown");
        return FailPreflight("image_read_failed error=" + ex.GetType().Name, 11);
    }

    Console.WriteLine("QEMU_PREFLIGHT_IMAGE path=" + options.ImagePath + " exists=true bytes=" + imageBytes);
    if (imageBytes == 0)
    {
        return FailPreflight("image_empty", 11);
    }

    string detectedDiskFormat;
    try
    {
        detectedDiskFormat = DetectImageFormat(options.ImagePath);
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
    {
        return FailPreflight("image_read_failed error=" + ex.GetType().Name, 11);
    }

    Console.WriteLine(
        "QEMU_PREFLIGHT_DISK_FORMAT expected=" + options.DiskFormat
        + " detected=" + detectedDiskFormat);

    if (string.Equals(options.DiskFormat, "qcow2", StringComparison.Ordinal) &&
        !string.Equals(detectedDiskFormat, "qcow2", StringComparison.Ordinal))
    {
        return FailPreflight("disk_format_mismatch expected=qcow2 detected=" + detectedDiskFormat, 12);
    }

    if (string.Equals(options.DiskFormat, "raw", StringComparison.Ordinal) &&
        string.Equals(detectedDiskFormat, "qcow2", StringComparison.Ordinal))
    {
        return FailPreflight("disk_format_mismatch expected=raw detected=qcow2", 12);
    }

    string serialDirectory = GetPathDirectoryOrCurrent(options.SerialLogPath);
    Console.WriteLine(
        "QEMU_PREFLIGHT_SERIAL_DIRECTORY path=" + serialDirectory
        + " exists=" + FormatBool(Directory.Exists(serialDirectory))
        + " runner_creates=true");

    bool ovmfConfigured = !string.IsNullOrWhiteSpace(options.OvmfCodePath);
    bool ovmfExists = ovmfConfigured && File.Exists(options.OvmfCodePath);
    Console.WriteLine(
        "QEMU_PREFLIGHT_OVMF configured=" + FormatBool(ovmfConfigured)
        + " exists=" + (ovmfConfigured ? FormatBool(ovmfExists) : "not_checked")
        + " path=" + (ovmfConfigured ? options.OvmfCodePath : "none"));
    if (ovmfConfigured && !ovmfExists)
    {
        return FailPreflight("ovmf_not_found", 6);
    }

    IReadOnlyList<string> arguments = QemuCommandBuilder.BuildArguments(options, options.OvmfCodePath);
    Console.WriteLine("QEMU_PREFLIGHT_COMMAND_ARGUMENTS count=" + arguments.Count);
    Console.WriteLine("QEMU_PREFLIGHT_COMMAND_LINE " + QemuCommandBuilder.BuildCommandLine(options, options.OvmfCodePath));

    if (options.HttpProbe is not null)
    {
        WriteHttpProbe(options.HttpProbe.Value);
    }

    Console.WriteLine("TEST_PASS qemu.runner.preflight");
    return 0;
}

static int FailPreflight(string reason, int exitCode)
{
    Console.WriteLine("TEST_FAIL qemu.runner.preflight " + reason);
    return exitCode;
}

static string DetectImageFormat(string imagePath)
{
    Span<byte> header = stackalloc byte[4];
    using FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    int bytesRead = stream.Read(header);
    if (bytesRead == 4 &&
        header[0] == (byte)'Q' &&
        header[1] == (byte)'F' &&
        header[2] == (byte)'I' &&
        header[3] == 0xFB)
    {
        return "qcow2";
    }

    return "raw-compatible";
}

static string GetPathDirectoryOrCurrent(string path)
{
    string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
    return string.IsNullOrWhiteSpace(directory)
        ? Directory.GetCurrentDirectory()
        : directory;
}

static string FormatBool(bool value)
{
    return value ? "true" : "false";
}

static void WriteSummaryLogBestEffort(
    RunnerOptions options,
    string status,
    IReadOnlyList<string> observedMarkers,
    IReadOnlyList<string> missingMarkers,
    IReadOnlyList<string> forbiddenMarkers,
    string failReason,
    HttpProbeResult? httpProbeResult = null,
    IReadOnlyList<AppExitMarkerResult>? appExitResults = null,
    IReadOnlyList<string>? malformedAppExitMarkers = null,
    IReadOnlyList<string>? outOfOrderMarkers = null)
{
    if (string.IsNullOrWhiteSpace(options.SummaryLogPath))
    {
        return;
    }

    try
    {
        string fullPath = Path.GetFullPath(options.SummaryLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");

        StringBuilder builder = new StringBuilder();
        builder.Append("QEMU_SMOKE_STATUS ").AppendLine(status);
        builder.Append("IMAGE_ARTIFACT ").AppendLine(options.ImagePath);
        builder.Append("SERIAL_LOG ").AppendLine(options.SerialLogPath);
        builder.Append("WORKLOAD_PROFILE ").AppendLine(options.WorkloadProfile);
        builder.Append("MARKER_PRESETS ").AppendLine(FormatSummaryList(options.MarkerPresets));
        builder.Append("REQUIRED_MARKERS ").AppendLine(FormatMarkers(options.RequiredMarkers));
        builder.Append("ORDERED_MARKERS ").AppendLine(FormatSummaryList(options.OrderedMarkers));
        builder.Append("FORBIDDEN_MARKERS ").AppendLine(FormatSummaryList(options.ForbiddenMarkers));
        AppendHttpProbeSummary(builder, options, httpProbeResult);
        AppendObservedMarkerLines(builder, observedMarkers);
        AppendAppExitSummary(builder, appExitResults, malformedAppExitMarkers);
        builder.Append("MISSING_MARKERS ").AppendLine(FormatSummaryList(missingMarkers));
        builder.Append("OUT_OF_ORDER_MARKERS ").AppendLine(FormatSummaryList(outOfOrderMarkers ?? Array.Empty<string>()));
        builder.Append("FORBIDDEN_MARKER_HIT ").AppendLine(FormatSummaryList(forbiddenMarkers));
        builder.Append("FAIL_REASON ").AppendLine(failReason);
        AppendSerialTailSummary(builder, ReadSerialLog(options.SerialLogPath));

        File.WriteAllText(options.SummaryLogPath, builder.ToString(), Encoding.UTF8);
        Console.WriteLine("SUMMARY_LOG " + options.SummaryLogPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("SUMMARY_LOG_WRITE_FAILED path=" + options.SummaryLogPath + " reason=" + ex.GetType().Name + ": " + ex.Message);
    }
}

static void AppendSerialTailSummary(StringBuilder builder, string serial)
{
    const int maxLines = 8;
    const int maxChars = 240;

    builder.Append("SERIAL_TAIL_LIMIT lines=")
        .Append(maxLines.ToString())
        .Append(" chars=")
        .AppendLine(maxChars.ToString());

    string[] tailLines = GetSerialTailLines(serial, maxLines, maxChars);
    if (tailLines.Length == 0)
    {
        builder.Append("SERIAL_TAIL_LINE none").AppendLine();
        return;
    }

    foreach (string line in tailLines)
    {
        builder.Append("SERIAL_TAIL_LINE ").AppendLine(line);
    }
}

static string[] GetSerialTailLines(string serial, int maxLines, int maxChars)
{
    Queue<string> tail = new Queue<string>(maxLines);
    using StringReader reader = new StringReader(serial);
    while (reader.ReadLine() is { } line)
    {
        string normalized = NormalizeSerialTailLine(line, maxChars);
        if (normalized.Length == 0)
        {
            continue;
        }

        if (tail.Count == maxLines)
        {
            tail.Dequeue();
        }

        tail.Enqueue(normalized);
    }

    return tail.ToArray();
}

static string NormalizeSerialTailLine(string line, int maxChars)
{
    string trimmed = line.Trim();
    if (trimmed.Length <= maxChars)
    {
        return trimmed;
    }

    return trimmed[..(maxChars - 3)] + "...";
}

static void AppendObservedMarkerLines(StringBuilder builder, IReadOnlyList<string> observedMarkers)
{
    if (observedMarkers.Count == 0)
    {
        builder.Append("OBSERVED_MARKER none").AppendLine();
        return;
    }

    foreach (string marker in observedMarkers)
    {
        builder.Append("OBSERVED_MARKER ").AppendLine(marker);
    }
}

static void AppendAppExitSummary(
    StringBuilder builder,
    IReadOnlyList<AppExitMarkerResult>? appExitResults,
    IReadOnlyList<string>? malformedAppExitMarkers)
{
    if (appExitResults is null || appExitResults.Count == 0)
    {
        builder.Append("APP_EXIT_RESULT none").AppendLine();
    }
    else
    {
        foreach (AppExitMarkerResult result in appExitResults)
        {
            builder.Append("APP_EXIT_RESULT code=")
                .Append(result.ExitCode.ToString())
                .Append(" status=")
                .AppendLine(result.Status);
        }
    }

    if (malformedAppExitMarkers is null || malformedAppExitMarkers.Count == 0)
    {
        builder.Append("APP_EXIT_MALFORMED none").AppendLine();
        return;
    }

    foreach (string marker in malformedAppExitMarkers)
    {
        builder.Append("APP_EXIT_MALFORMED ").AppendLine(marker);
    }
}

static void AppendHttpProbeSummary(StringBuilder builder, RunnerOptions options, HttpProbeResult? result)
{
    if (options.HttpProbe is null)
    {
        return;
    }

    HttpProbeOptions probe = options.HttpProbe.Value;
    builder.Append("HTTP_PROBE_ENDPOINT ").AppendLine(FormatProbeEndpoint(probe.Url));
    builder.Append("HTTP_PROBE_EXPECTED_STATUS ").AppendLine(probe.ExpectedStatusCode.ToString());
    builder.Append("HTTP_PROBE_EXPECTED_BODY_BYTES ").AppendLine(probe.ExpectedBodyByteLength.ToString());
    builder.Append("HTTP_PROBE_TIMEOUT_SECONDS ").AppendLine(probe.TimeoutSeconds.ToString());

    if (result is null)
    {
        builder.Append("HTTP_PROBE_RESULT not_run").AppendLine();
        builder.Append("HTTP_PROBE_REASON none").AppendLine();
        builder.Append("HTTP_PROBE_STATUS none").AppendLine();
        builder.Append("HTTP_PROBE_BODY_BYTES none").AppendLine();
        builder.Append("HTTP_PROBE_BODY_SHA256 none").AppendLine();
        builder.Append("HTTP_PROBE_ELAPSED_MS none").AppendLine();
        return;
    }

    HttpProbeResult value = result.Value;
    builder.Append("HTTP_PROBE_RESULT ").AppendLine(value.Passed ? "pass" : "fail");
    builder.Append("HTTP_PROBE_REASON ").AppendLine(value.Reason);
    builder.Append("HTTP_PROBE_STATUS ").AppendLine(value.StatusCodeText);
    builder.Append("HTTP_PROBE_BODY_BYTES ").AppendLine(value.BodyByteCount.ToString());
    builder.Append("HTTP_PROBE_BODY_SHA256 ").AppendLine(value.BodySha256);
    builder.Append("HTTP_PROBE_ELAPSED_MS ").AppendLine(value.ElapsedMilliseconds.ToString());
}

static void WriteHttpProbe(HttpProbeOptions probe)
{
    Console.WriteLine(
        "HTTP_PROBE url=" + probe.Url
        + " expected_status=" + probe.ExpectedStatusCode
        + " expected_body_bytes=" + probe.ExpectedBodyByteLength
        + " timeout_seconds=" + probe.TimeoutSeconds);
}

static HttpProbeResult ExecuteHttpProbe(HttpProbeOptions probe)
{
    Console.WriteLine(
        "HTTP_PROBE_START endpoint=" + FormatProbeEndpoint(probe.Url)
        + " expected_status=" + probe.ExpectedStatusCode
        + " expected_body_bytes=" + probe.ExpectedBodyByteLength
        + " timeout_seconds=" + probe.TimeoutSeconds);

    Stopwatch stopwatch = Stopwatch.StartNew();
    try
    {
        Uri uri = new Uri(probe.Url, UriKind.Absolute);
        RawHttpProbeResponse response = SendRawHttpGet(uri, probe.TimeoutSeconds);
        stopwatch.Stop();

        int statusCode = response.StatusCode;
        byte[] body = response.Body;
        string bodySha256 = ComputeSha256(body);
        string reason = "none";
        bool passed = true;
        if (statusCode != probe.ExpectedStatusCode)
        {
            passed = false;
            reason = "status_mismatch";
        }
        else if (probe.ExpectedBodyConfigured
            && !Encoding.UTF8.GetBytes(probe.ExpectedBody).AsSpan().SequenceEqual(body))
        {
            passed = false;
            reason = "body_mismatch";
        }

        HttpProbeResult result = new HttpProbeResult(
            passed,
            reason,
            statusCode,
            body.Length,
            bodySha256,
            stopwatch.ElapsedMilliseconds,
            string.Empty);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (TaskCanceledException ex)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("timeout", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (TimeoutException ex)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("timeout", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (MalformedHttpResponseException ex)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("malformed_response", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("request_failed", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
}

static RawHttpProbeResponse SendRawHttpGet(Uri uri, int timeoutSeconds)
{
    using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
    try
    {
        return SendRawHttpGetAsync(uri, cancellation.Token).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested)
    {
        throw new TimeoutException("HTTP probe timed out.", ex);
    }
}

static async Task<RawHttpProbeResponse> SendRawHttpGetAsync(Uri uri, CancellationToken cancellationToken)
{
    using TcpClient client = new TcpClient();
    await client.ConnectAsync(uri.Host, uri.Port, cancellationToken).ConfigureAwait(false);
    await using NetworkStream stream = client.GetStream();

    string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
    string host = uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port;
    byte[] requestBytes = Encoding.ASCII.GetBytes(
        "GET " + path + " HTTP/1.1\r\n"
        + "Host: " + host + "\r\n"
        + "Connection: close\r\n"
        + "Accept: */*\r\n"
        + "\r\n");
    await stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);

    using MemoryStream response = new MemoryStream();
    byte[] buffer = new byte[4096];
    while (true)
    {
        int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            break;
        }

        response.Write(buffer, 0, read);
        if (response.Length > 1024 * 1024)
        {
            throw new MalformedHttpResponseException("HTTP probe response exceeded 1 MiB.");
        }
    }

    return ParseRawHttpResponse(response.ToArray());
}

static RawHttpProbeResponse ParseRawHttpResponse(byte[] response)
{
    int headerEnd = IndexOf(response, new byte[] { 13, 10, 13, 10 });
    if (headerEnd < 0)
    {
        throw new MalformedHttpResponseException("HTTP response header terminator was not found.");
    }

    int statusLineEnd = IndexOf(response.AsSpan(0, headerEnd), new byte[] { 13, 10 });
    if (statusLineEnd < 0)
    {
        throw new MalformedHttpResponseException("HTTP response status line was not terminated.");
    }

    string statusLine = Encoding.ASCII.GetString(response, 0, statusLineEnd);
    int firstSpace = statusLine.IndexOf(' ');
    if (firstSpace < 0 || firstSpace + 4 > statusLine.Length)
    {
        throw new MalformedHttpResponseException("HTTP response status code was missing.");
    }

    string statusCodeText = statusLine.Substring(firstSpace + 1, 3);
    if (!int.TryParse(statusCodeText, out int statusCode))
    {
        throw new MalformedHttpResponseException("HTTP response status code was invalid.");
    }

    int bodyOffset = headerEnd + 4;
    byte[] body = new byte[response.Length - bodyOffset];
    Array.Copy(response, bodyOffset, body, 0, body.Length);
    return new RawHttpProbeResponse(statusCode, body);
}

static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
{
    if (value.Length == 0 || value.Length > source.Length)
    {
        return -1;
    }

    for (int i = 0; i <= source.Length - value.Length; i++)
    {
        if (source.Slice(i, value.Length).SequenceEqual(value))
        {
            return i;
        }
    }

    return -1;
}

static void WriteHttpProbeResult(HttpProbeResult result, HttpProbeOptions probe)
{
    string prefix = result.Passed ? "HTTP_PROBE_PASS" : "HTTP_PROBE_FAIL";
    string line = prefix
        + " endpoint=" + FormatProbeEndpoint(probe.Url)
        + " status=" + result.StatusCodeText
        + " body_bytes=" + result.BodyByteCount
        + " body_sha256=" + result.BodySha256
        + " elapsed_ms=" + result.ElapsedMilliseconds;
    if (!result.Passed)
    {
        line += " reason=" + result.Reason;
        if (!string.IsNullOrWhiteSpace(result.ErrorType))
        {
            line += " error_type=" + result.ErrorType;
        }
    }

    Console.WriteLine(line);
}

static string ComputeSha256(byte[] bytes)
{
    return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

static string FormatProbeEndpoint(string url)
{
    Uri uri = new Uri(url, UriKind.Absolute);
    string host = uri.Host.Contains(':', StringComparison.Ordinal)
        ? "[" + uri.Host + "]"
        : uri.Host;
    return uri.Scheme + "://" + host + ":" + uri.Port;
}

static string ReadSerialLog(string path)
{
    if (!File.Exists(path))
    {
        return string.Empty;
    }

    try
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    catch (IOException)
    {
        return string.Empty;
    }
}

static DiscoveryResult ResolveExecutable(string executable)
{
    List<string> checkedPaths = new List<string>();

    if (Path.IsPathFullyQualified(executable)
        || executable.Contains(Path.DirectorySeparatorChar)
        || executable.Contains(Path.AltDirectorySeparatorChar))
    {
        checkedPaths.Add(executable);
        return File.Exists(executable)
            ? new DiscoveryResult(executable, checkedPaths)
            : new DiscoveryResult(null, checkedPaths);
    }

    string? path = Environment.GetEnvironmentVariable("PATH");
    if (path is null)
    {
        return new DiscoveryResult(null, checkedPaths);
    }

    foreach (string directory in path.Split(Path.PathSeparator))
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            continue;
        }

        string candidate = Path.Combine(directory, executable);
        checkedPaths.Add(candidate);
        if (File.Exists(candidate))
        {
            return new DiscoveryResult(candidate, checkedPaths);
        }

        string candidateExe = candidate + ".exe";
        checkedPaths.Add(candidateExe);
        if (File.Exists(candidateExe))
        {
            return new DiscoveryResult(candidateExe, checkedPaths);
        }
    }

    return new DiscoveryResult(null, checkedPaths);
}

static DiscoveryResult ResolveOvmfCode(string requestedPath, string? qemuPath)
{
    List<string> candidates = new List<string>();

    if (!string.IsNullOrWhiteSpace(requestedPath))
    {
        candidates.Add(requestedPath);
        return File.Exists(requestedPath)
            ? new DiscoveryResult(requestedPath, candidates)
            : new DiscoveryResult(null, candidates);
    }

    AddEnvironmentCandidate(candidates, "OS_OVMF_CODE");
    AddEnvironmentCandidate(candidates, "OVMF_CODE");
    AddEnvironmentCandidate(candidates, "QEMU_OVMF_CODE");

    if (!string.IsNullOrWhiteSpace(qemuPath))
    {
        string? qemuDirectory = Path.GetDirectoryName(Path.GetFullPath(qemuPath));
        if (!string.IsNullOrWhiteSpace(qemuDirectory))
        {
            AddOvmfCandidates(candidates, qemuDirectory);
            AddOvmfCandidates(candidates, Path.Combine(qemuDirectory, "share"));
            AddOvmfCandidates(candidates, Path.Combine(qemuDirectory, "..", "share", "qemu"));
        }
    }

    AddOvmfCandidates(candidates, @"C:\Program Files\qemu");
    AddOvmfCandidates(candidates, @"C:\Program Files\qemu\share");
    AddOvmfCandidates(candidates, @"C:\Program Files (x86)\qemu");
    AddOvmfCandidates(candidates, @"C:\ProgramData\chocolatey\lib\qemu\tools");
    AddOvmfCandidates(candidates, @"C:\msys64\mingw64\share\qemu");
    AddOvmfCandidates(candidates, @"/usr/share/OVMF");
    AddOvmfCandidates(candidates, @"/usr/share/edk2/ovmf");
    AddOvmfCandidates(candidates, @"/usr/share/qemu");

    List<string> uniqueCandidates = new List<string>();
    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (string candidate in candidates)
    {
        string normalized = Path.GetFullPath(candidate);
        if (seen.Add(normalized))
        {
            uniqueCandidates.Add(normalized);
        }
    }

    foreach (string candidate in uniqueCandidates)
    {
        if (File.Exists(candidate))
        {
            return new DiscoveryResult(candidate, uniqueCandidates);
        }
    }

    return new DiscoveryResult(null, uniqueCandidates);
}

static void AddEnvironmentCandidate(List<string> candidates, string variableName)
{
    string? value = Environment.GetEnvironmentVariable(variableName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        candidates.Add(value);
    }
}

static void AddOvmfCandidates(List<string> candidates, string directory)
{
    candidates.Add(Path.Combine(directory, "OVMF_CODE.fd"));
    candidates.Add(Path.Combine(directory, "OVMF.fd"));
    candidates.Add(Path.Combine(directory, "edk2-x86_64-code.fd"));
    candidates.Add(Path.Combine(directory, "edk2-x86_64-secure-code.fd"));
}

static void WriteDiscovery(string prefix, DiscoveryResult result)
{
    string found = result.Path ?? "not_found";
    string checkedPaths = result.CheckedPaths.Count == 0
        ? "none"
        : string.Join(";", result.CheckedPaths);
    Console.WriteLine(prefix + " result=" + found + " checked=" + checkedPaths);
}

readonly struct DiscoveryResult
{
    public DiscoveryResult(string? path, IReadOnlyList<string> checkedPaths)
    {
        Path = path;
        CheckedPaths = checkedPaths;
    }

    public string? Path { get; }

    public IReadOnlyList<string> CheckedPaths { get; }
}

sealed class RunnerOptions
{
    private static readonly string[] DefaultRequiredMarkers = { "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS" };
    private static readonly string[] DefaultForbiddenMarkers = { "OS_SMOKE_FAIL" };
    private const string DefaultWorkloadProfile = OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
    private readonly List<string> requiredMarkers = new List<string>();
    private readonly List<string> forbiddenMarkers = new List<string>(DefaultForbiddenMarkers);
    private readonly List<string> markerPresets = new List<string>();
    private readonly List<string> orderedMarkers = new List<string>();

    public string QemuPath { get; private set; } = "qemu-system-x86_64";

    public string ImagePath { get; private set; } = Path.Combine("artifacts", "images", "os.raw");

    public string DiskFormat { get; private set; } = "raw";

    public string OvmfCodePath { get; private set; } = string.Empty;

    public string SerialLogPath { get; private set; } = Path.Combine("artifacts", "logs", "qemu-smoke.log");

    public string SummaryLogPath { get; private set; } = string.Empty;

    public string Memory { get; private set; } = "256M";

    public string WorkloadProfile { get; private set; } = DefaultWorkloadProfile;

    public IReadOnlyList<string> RequiredMarkers
    {
        get
        {
            return requiredMarkers.Count == 0
                ? DefaultRequiredMarkers
                : requiredMarkers;
        }
    }

    public IReadOnlyList<string> ForbiddenMarkers => forbiddenMarkers;

    public IReadOnlyList<string> MarkerPresets => markerPresets;

    public IReadOnlyList<string> OrderedMarkers => orderedMarkers;

    public int TimeoutSeconds { get; private set; } = 30;

    public HttpProbeOptions? HttpProbe { get; private set; }

    public bool DryRun { get; private set; }

    public bool Preflight { get; private set; }

    public bool ShowHelp { get; private set; }

    private bool httpProbeConfigured;

    private string? httpProbeUrl;

    private int httpProbeExpectedStatusCode = 200;

    private string httpProbeExpectedBody = string.Empty;

    private bool httpProbeExpectedBodyConfigured;

    private int httpProbeTimeoutSeconds = 10;

    public static RunnerOptions Parse(string[] args)
    {
        RunnerOptions options = new RunnerOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "preflight":
                case "--preflight":
                    options.Preflight = true;
                    break;
                case "--qemu":
                    options.QemuPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--image":
                    options.ImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--disk-format":
                    options.SetDiskFormat(ReadValue(args, ref i, arg));
                    break;
                case "--ovmf-code":
                    options.OvmfCodePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--serial-log":
                    options.SerialLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--summary-log":
                    options.SummaryLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--memory":
                    options.Memory = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--workload-profile":
                    options.WorkloadProfile = NormalizeWorkloadProfile(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--require-marker":
                    options.AddRequiredMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--marker-preset":
                    options.AddMarkerPreset(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--forbid-marker":
                    options.AddForbiddenMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--timeout":
                    options.TimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--http-probe-url":
                    options.SetHttpProbeUrl(ReadValue(args, ref i, arg));
                    break;
                case "--http-probe-expected-status":
                    options.SetHttpProbeExpectedStatusCode(ReadHttpStatusCode(args, ref i, arg));
                    break;
                case "--http-probe-expected-body":
                    options.SetHttpProbeExpectedBody(ReadValue(args, ref i, arg));
                    break;
                case "--http-probe-timeout":
                    options.SetHttpProbeTimeoutSeconds(ReadPositiveInt(args, ref i, arg));
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        options.FinishParsing();
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("OS.QemuRunner [preflight|--preflight] --image <raw|qcow2 image> [--disk-format raw|qcow2] [--workload-profile SingleMinimalApi|MultiMinimalApi] [--ovmf-code <OVMF_CODE.fd>] [--serial-log <path>] [--summary-log <path>] [--timeout <seconds>] [--require-marker <marker>]... [--marker-preset " + SmokeMarkerPreset.SupportedValues + "]... [--forbid-marker <marker>]... [--http-probe-url <http-url>] [--http-probe-expected-status <100-599>] [--http-probe-expected-body <text>] [--http-probe-timeout <seconds>] [--dry-run]");
        Console.WriteLine("preflight validates local runner inputs and image format hints without launching QEMU or requiring libvirt.");
    }

    private void AddRequiredMarker(string marker)
    {
        AddDistinct(requiredMarkers, marker);
    }

    private void AddMarkerPreset(string preset)
    {
        string normalized = SmokeMarkerPreset.Normalize(preset);
        AddDistinct(markerPresets, normalized);

        foreach (string marker in SmokeMarkerPreset.GetRequiredMarkers(normalized))
        {
            AddRequiredMarker(marker);
        }

        foreach (string marker in SmokeMarkerPreset.GetOrderedMarkers(normalized))
        {
            AddDistinct(orderedMarkers, marker);
        }
    }

    private void AddForbiddenMarker(string marker)
    {
        AddDistinct(forbiddenMarkers, marker);
    }

    private void SetDiskFormat(string value)
    {
        if (string.Equals(value, "raw", StringComparison.OrdinalIgnoreCase))
        {
            DiskFormat = "raw";
            return;
        }

        if (string.Equals(value, "qcow2", StringComparison.OrdinalIgnoreCase))
        {
            DiskFormat = "qcow2";
            return;
        }

        throw new ArgumentException("Value for --disk-format must be raw or qcow2.");
    }

    private void SetHttpProbeUrl(string value)
    {
        httpProbeConfigured = true;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for --http-probe-url cannot be empty.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Value for --http-probe-url must be an absolute HTTP URL with a host.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("HTTP probe URL must use http scheme; https/TLS is not supported.");
        }

        httpProbeUrl = value;
    }

    private void SetHttpProbeExpectedStatusCode(int statusCode)
    {
        httpProbeConfigured = true;
        httpProbeExpectedStatusCode = statusCode;
    }

    private void SetHttpProbeExpectedBody(string body)
    {
        httpProbeConfigured = true;
        httpProbeExpectedBody = body;
        httpProbeExpectedBodyConfigured = true;
    }

    private void SetHttpProbeTimeoutSeconds(int timeoutSeconds)
    {
        httpProbeConfigured = true;
        httpProbeTimeoutSeconds = timeoutSeconds;
    }

    private void FinishParsing()
    {
        if (DryRun && Preflight)
        {
            throw new ArgumentException("--dry-run and preflight are mutually exclusive.");
        }

        if (!httpProbeConfigured)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(httpProbeUrl))
        {
            throw new ArgumentException("HTTP probe requires --http-probe-url.");
        }

        HttpProbe = new HttpProbeOptions(
            httpProbeUrl,
            httpProbeExpectedStatusCode,
            httpProbeExpectedBody,
            httpProbeExpectedBodyConfigured,
            httpProbeTimeoutSeconds);
    }

    private static string NormalizeWorkloadProfile(string value)
    {
        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
        }

        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName;
        }

        throw new ArgumentException("Invalid workload profile '" + value + "'. Valid values: SingleMinimalApi, MultiMinimalApi.");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("Missing value for " + option);
        }

        index++;
        return args[index];
    }

    private static string RequireNonEmpty(string value, string option)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for " + option + " cannot be empty.");
        }

        return value;
    }

    private static void AddDistinct(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static int ReadPositiveInt(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException("Value for " + option + " must be a positive integer.");
        }

        return parsed;
    }

    private static int ReadHttpStatusCode(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed < 100 || parsed > 599)
        {
            throw new ArgumentException("Value for " + option + " must be an HTTP status code from 100 to 599.");
        }

        return parsed;
    }
}

readonly struct HttpProbeOptions
{
    public HttpProbeOptions(string url, int expectedStatusCode, string expectedBody, bool expectedBodyConfigured, int timeoutSeconds)
    {
        Url = url;
        ExpectedStatusCode = expectedStatusCode;
        ExpectedBody = expectedBody;
        ExpectedBodyConfigured = expectedBodyConfigured;
        TimeoutSeconds = timeoutSeconds;
    }

    public string Url { get; }

    public int ExpectedStatusCode { get; }

    public string ExpectedBody { get; }

    public bool ExpectedBodyConfigured { get; }

    public int ExpectedBodyByteLength
    {
        get
        {
            return Encoding.UTF8.GetByteCount(ExpectedBody);
        }
    }

    public int TimeoutSeconds { get; }
}

readonly struct HttpProbeResult
{
    public HttpProbeResult(
        bool passed,
        string reason,
        int? statusCode,
        int bodyByteCount,
        string bodySha256,
        long elapsedMilliseconds,
        string errorType)
    {
        Passed = passed;
        Reason = reason;
        StatusCode = statusCode;
        BodyByteCount = bodyByteCount;
        BodySha256 = bodySha256;
        ElapsedMilliseconds = elapsedMilliseconds;
        ErrorType = errorType;
    }

    public bool Passed { get; }

    public string Reason { get; }

    public int? StatusCode { get; }

    public string StatusCodeText => StatusCode.HasValue ? StatusCode.Value.ToString() : "none";

    public int BodyByteCount { get; }

    public string BodySha256 { get; }

    public long ElapsedMilliseconds { get; }

    public string ErrorType { get; }

    public static HttpProbeResult Failed(string reason, long elapsedMilliseconds, string errorType)
    {
        return new HttpProbeResult(
            false,
            reason,
            null,
            0,
            "none",
            elapsedMilliseconds,
            errorType);
    }
}

readonly struct RawHttpProbeResponse
{
    public RawHttpProbeResponse(int statusCode, byte[] body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public int StatusCode { get; }

    public byte[] Body { get; }
}

sealed class MalformedHttpResponseException : Exception
{
    public MalformedHttpResponseException(string message)
        : base(message)
    {
    }
}
