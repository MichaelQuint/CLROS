using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Text;
using System.Runtime.ExceptionServices;
using OS.Tooling.Native;

namespace OS.Tooling;
/// <summary>Direct executable invocation. No shell, command-string parsing, or environment logging.</summary>

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Executable);
        if (request.Timeout <= TimeSpan.Zero || request.OutputCharacterLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.StandardErrorHandle is { } errorHandle && (!OperatingSystem.IsWindows() || !request.RetainOwnershipUntilJoined || errorHandle.IsClosed || errorHandle.IsInvalid))
            throw new ArgumentException("process_external_stderr_requires_live_handle_and_retained_windows_job");
        cancellation.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
            return await RunWindowsAsync(request, cancellation);
        if (request.RetainOwnershipUntilJoined)
            throw new PlatformNotSupportedException("retained_process_join_requires_windows_job");
        var start = new ProcessStartInfo(request.Executable)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in request.Arguments)
            start.ArgumentList.Add(argument);
        foreach (var (key, value) in request.EnvironmentOverrides)
            start.Environment[key] = value;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(request.Timeout);
        using Process process = new()
        {
            StartInfo = start
        };
        Stopwatch time = Stopwatch.StartNew();
        process.Start();
        int characters = 0, overflow = 0;
        // Retain already captured output when a timeout cancels a read.
        async Task<string> CaptureRetained(StreamReader reader, bool isError)
        {
            StringBuilder text = new();
            char[] buffer = new char[4096];
            try
            {
                while (true)
                {
                    int read = await reader.ReadAsync(buffer.AsMemory(), deadline.Token);
                    if (read == 0)
                        break;
                    int total = Interlocked.Add(ref characters, read);
                    int keep = Math.Clamp(request.OutputCharacterLimit - (total - read), 0, read);
                    text.Append(buffer, 0, keep);
                    if (keep != 0)
                        request.OutputReceived?.Invoke(new string(buffer, 0, keep), isError);
                    if (keep < read)
                    {
                        Interlocked.Exchange(ref overflow, 1);
                        deadline.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
            }

            return text.ToString();
        }

        Task<string> stdout = CaptureRetained(process.StandardOutput, false), stderr = CaptureRetained(process.StandardError, true);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }

            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cleanup.Token);
        }

        // A child retaining an inherited pipe must not keep the caller alive indefinitely.
        string output = await stdout;
        string error = await stderr;
        return new(process.ExitCode, output, error, deadline.IsCancellationRequested && overflow == 0 && !cancellation.IsCancellationRequested, overflow != 0, cancellation.IsCancellationRequested, time.Elapsed)
        {
            // Process.Kill's best-effort descendant traversal cannot prove
            // ownership after a root exits. Only the Windows job path is qualified.
            TerminationConfirmed = false,
            FailureReason = "process_tree_confirmation_unavailable"
        };
    }

    private static Task<ProcessResult> RunWindowsAsync(ProcessRequest request, CancellationToken cancellation) => Task.Run(() =>
    {
        Stopwatch time = Stopwatch.StartNew();
        string executable = ResolveExecutable(request);
        Exception? callbackFailure = null;
        var boundedRequest = request with
        {
            OutputReceived = request.OutputReceived is null ? null : (text, error) =>
            {
                try
                {
                    request.OutputReceived(text, error);
                }
                catch (Exception failure)
                {
                    Interlocked.CompareExchange(ref callbackFailure, failure, null);
                    throw;
                }
            }
        };
        var result = BoundedJobProcessRunner.RunManaged(executable, boundedRequest, cancellation);
        if (callbackFailure is not null && result.TerminationConfirmed)
            ExceptionDispatchInfo.Capture(callbackFailure).Throw();
        bool ordinary = result.Status is "Success" or "NonZeroExit" or "TimedOut" or "OutputOverflow" or "Cancelled";
        return new ProcessResult(result.ExitCode, result.StandardOutput, result.StandardError, result.Status == "TimedOut", result.Status == "OutputOverflow", result.Status == "Cancelled", time.Elapsed)
        {
            StandardErrorExternallyOwned = request.StandardErrorHandle is not null,
            TerminationConfirmed = result.TerminationConfirmed,
            FailureReason = ordinary ? "" : result.Status + (result.Detail.Length == 0 ? "" : " " + result.Detail)
        };
    });
    private static string ResolveExecutable(ProcessRequest request)
    {
        string name = request.Executable;
        if (Path.IsPathRooted(name))
            return name;
        if (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            return Path.GetFullPath(name, request.WorkingDirectory);
        IEnumerable<string> directories = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Environment.SystemDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (string directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            string path = Path.Combine(directory.Trim('"'), name);
            if (File.Exists(path))
                return Path.GetFullPath(path);
            if (Path.GetExtension(path).Length == 0 && File.Exists(path + ".exe"))
                return Path.GetFullPath(path + ".exe");
        }

        throw new FileNotFoundException("process_executable_not_found", name);
    }
}
