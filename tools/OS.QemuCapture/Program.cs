using OS.Tools.QemuRunner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private const int GuestMemoryBytes = 256 * 1024 * 1024;
    private const int CaptureChunkBytes = 16 * 1024 * 1024;
    private const int ScanCarryBytes = 1024;
    private const string SuccessTerminal = "OS_SMOKE_PASS";

    private static readonly string[] GuestMarkers = OS.Tooling.QemuContracts.M01StageThreeGuestMarkers.ToArray();

    private static readonly string[] M02GuestMarkers = OS.Tooling.QemuContracts.M02SafeStubRequiredMarkers.ToArray();

    private static readonly string[] ForbiddenMarkers = OS.Tooling.QemuContracts.M01StageThreeForbiddenMarkers.ToArray();

    private static readonly string[] M02ForbiddenMarkers = OS.Tooling.QemuContracts.M02SafeStubForbiddenMarkers.ToArray();

    private static readonly object LogLock = new object();
    private static StreamWriter? s_log;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            Log("M01_RAW_OBSERVATION_SHIM_FAIL reason=" + Sanitize(exception.Message));
            Console.Error.WriteLine(
                "M01_RAW_OBSERVATION_SHIM_FAIL reason="
                + Sanitize(exception.Message));
            return 90;
        }
        finally
        {
            if (s_log is not null)
            {
                s_log.Flush();
                s_log.Dispose();
            }
        }
    }

    private static int Run(string[] args)
    {
        string realQemu = RequiredEnvironment("CLROS_M01_REAL_QEMU");
        string privateSerial = FullPath(
            RequiredEnvironment("CLROS_M01_PRIVATE_SERIAL"));
        string observationLog = FullPath(
            RequiredEnvironment("CLROS_M01_RAW_OBSERVATION_LOG"));
        string qmpLog = FullPath(
            RequiredEnvironment("CLROS_M01_QMP_TRANSCRIPT_LOG"));
        string scratchDirectory = FullPath(
            RequiredEnvironment("CLROS_M01_CAPTURE_SCRATCH"));
        int guestTimeoutSeconds = RequiredPositiveInt(
            "CLROS_M01_GUEST_TIMEOUT_SECONDS");
        int captureTimeoutSeconds = RequiredPositiveInt(
            "CLROS_M01_CAPTURE_TIMEOUT_SECONDS");
        bool m02SafeStubEnabled = RequiredBoolean(
            "CLROS_M02_SAFE_STUB_PROBE");
        string accelerator = Environment.GetEnvironmentVariable("CLROS_M01_ACCELERATOR") ?? "tcg";

        RequireCreateNewPath(privateSerial, "private_serial");
        RequireCreateNewPath(observationLog, "observation_log");
        RequireCreateNewPath(qmpLog, "qmp_log");
        if (Directory.Exists(scratchDirectory)
            || File.Exists(scratchDirectory))
        {
            throw new InvalidOperationException("scratch_path_exists");
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(qmpLog)
                ?? throw new InvalidOperationException("qmp_log_parent_missing"));
        {
            FileStream logStream = new FileStream(
                qmpLog,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            s_log = new StreamWriter(
                logStream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: false)
            {
                AutoFlush = true
            };

            List<string> childArguments = new List<string>(args);
            string publicSerial = ReplaceAndValidateArguments(
                childArguments,
                privateSerial,
                accelerator);
            RequireDistinctPaths(
                publicSerial,
                privateSerial,
                observationLog,
                qmpLog,
                scratchDirectory);

            Directory.CreateDirectory(
                Path.GetDirectoryName(publicSerial)
                    ?? throw new InvalidOperationException(
                        "public_serial_parent_missing"));
            Directory.CreateDirectory(
                Path.GetDirectoryName(privateSerial)
                    ?? throw new InvalidOperationException(
                        "private_serial_parent_missing"));
            Directory.CreateDirectory(
                Path.GetDirectoryName(observationLog)
                    ?? throw new InvalidOperationException(
                        "observation_parent_missing"));
            Directory.CreateDirectory(scratchDirectory);

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            Process? child = null;
            bool childStarted = false;
            WhpxPrelaunch? prelaunch = null;
            Stopwatch setupClock = Stopwatch.StartNew();
            try
            {
                listener.Start(1);
                int qmpPort =
                    ((IPEndPoint)listener.LocalEndpoint).Port;
                childArguments.Add("-qmp");
                childArguments.Add(
                    "tcp:127.0.0.1:"
                    + qmpPort.ToString(CultureInfo.InvariantCulture));

                Log("schema=clros.m01.qmp-transcript.v1");
                Log("qmpTransport=outbound-loopback");
                Log("qmpServerMode=false");
                Log("guestMemoryBytes=" + GuestMemoryBytes);
                Log("captureChunkBytes=" + CaptureChunkBytes);
                Log("realQemuBase64=" + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(realQemu)));

                ProcessStartInfo startInfo = new ProcessStartInfo(realQemu)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };
                foreach (string argument in childArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                child = new Process { StartInfo = startInfo };
                prelaunch = WhpxPrelaunch.Create(child, accelerator, publicSerial);
                for (int index = 0; index < startInfo.ArgumentList.Count; index++)
                    Log("childArg" + index.ToString("D3", CultureInfo.InvariantCulture) + "Base64=" +
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(startInfo.ArgumentList[index])));
                if (!child.Start())
                {
                    throw new InvalidOperationException(
                        "qemu_process_start_failed");
                }
                childStarted = true;
                prelaunch?.ResumeAfterStart(TimeSpan.FromSeconds(Math.Min(guestTimeoutSeconds, 15)) - setupClock.Elapsed);

                Task<TcpClient> acceptTask =
                    listener.AcceptTcpClientAsync();
                if (!acceptTask.Wait(
                        TimeSpan.FromSeconds(
                            Math.Max(0, Math.Min(guestTimeoutSeconds, 15) - setupClock.Elapsed.TotalSeconds))))
                {
                    throw new TimeoutException("qmp_connect_timeout");
                }

                using TcpClient qmpSocket = acceptTask.Result;
                listener.Stop();
                using QmpConnection qmp = new QmpConnection(
                    qmpSocket,
                    captureTimeoutSeconds,
                    Log);
                TimeSpan setupRemaining = TimeSpan.FromSeconds(Math.Min(guestTimeoutSeconds, 15)) - setupClock.Elapsed;
                if (setupRemaining <= TimeSpan.Zero) throw new TimeoutException("qmp_setup_timeout");
                using (var setupDeadline = new CancellationTokenSource(setupRemaining))
                using (setupDeadline.Token.Register(() => qmpSocket.Dispose()))
                {
                    qmp.Initialize();
                    qmp.RequireMemoryShape();
                    if (setupDeadline.IsCancellationRequested) throw new TimeoutException("qmp_setup_timeout");
                }

                using FileStream publicStream = new FileStream(
                    publicSerial,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.WriteThrough);
                SerialTerminalGate gate = new SerialTerminalGate(
                    publicStream,
                    m02SafeStubEnabled ? M02GuestMarkers : GuestMarkers,
                    m02SafeStubEnabled
                        ? M02ForbiddenMarkers
                        : ForbiddenMarkers);
                long privateOffset = 0;
                DateTime guestDeadline =
                    DateTime.UtcNow.AddSeconds(guestTimeoutSeconds);

                while (!gate.TerminalWithheld)
                {
                    privateOffset = DrainPrivateSerial(
                        privateSerial,
                        privateOffset,
                        gate);
                    if (gate.TerminalWithheld)
                    {
                        break;
                    }

                    if (child.HasExited)
                    {
                        privateOffset = DrainPrivateSerial(
                            privateSerial,
                            privateOffset,
                            gate);
                        throw new InvalidOperationException(
                            "qemu_exited_before_terminal_"
                            + child.ExitCode.ToString(
                                CultureInfo.InvariantCulture));
                    }

                    if (DateTime.UtcNow >= guestDeadline)
                    {
                        throw new TimeoutException(
                            "guest_terminal_timeout");
                    }

                    Thread.Sleep(20);
                }

                gate.ValidateCompleteMarkerContract();
                qmp.StopAndRequirePaused();

                long previousOffset = -1;
                for (int stablePass = 0; stablePass < 3;)
                {
                    privateOffset = DrainPrivateSerial(
                        privateSerial,
                        privateOffset,
                        gate);
                    if (privateOffset == previousOffset)
                    {
                        stablePass++;
                    }
                    else
                    {
                        stablePass = 0;
                        previousOffset = privateOffset;
                    }
                    Thread.Sleep(20);
                }
                gate.CompleteAfterStop();

                publicStream.Flush(flushToDisk: true);
                byte[] withheldBytes = gate.GetWithheldBytes();
                string privateSerialHash = Sha256File(privateSerial);
                string predictedPublicHash = Sha256FilePlusBytes(
                    publicSerial,
                    withheldBytes);
                if (!string.Equals(
                        privateSerialHash,
                        predictedPublicHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "serial_prediction_hash_mismatch");
                }

                ObservationScanner.ObservationEvidence evidence =
                    ObservationScanner.Capture(
                        qmp,
                        scratchDirectory,
                        captureTimeoutSeconds);
                DeleteEmptyCaptureScratch(scratchDirectory);
                WriteObservationSidecar(
                    observationLog,
                    evidence,
                    privateSerialHash,
                    predictedPublicHash,
                    guestTimeoutSeconds,
                    captureTimeoutSeconds);

                Log("M01_RAW_OBSERVATION_CAPTURED");
                Log("publicSerialSha256=" + predictedPublicHash);
                Log("privateSerialSha256=" + privateSerialHash);
                if (prelaunch?.Failure is string stderrFailure) throw new IOException(stderrFailure);
                Log("terminalReleaseAuthorized=true");

                publicStream.Position = publicStream.Length;
                publicStream.Write(
                    withheldBytes,
                    0,
                    withheldBytes.Length);
                publicStream.Flush(flushToDisk: true);
                string actualPublicHash = Sha256File(publicSerial);
                if (!string.Equals(
                        actualPublicHash,
                        predictedPublicHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "released_public_serial_hash_mismatch");
                }

                Log("terminalReleased=true");

                while (true)
                {
                    if (child.HasExited)
                    {
                        return child.ExitCode;
                    }
                    Thread.Sleep(1000);
                }
            }
            catch
            {
                if (child is not null && childStarted)
                {
                    try
                    {
                        if (!child.HasExited)
                        {
                            child.Kill(entireProcessTree: true);
                            child.WaitForExit(5000);
                        }
                    }
                    catch
                    {
                    }
                }
                throw;
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
                try { prelaunch?.Dispose(); }
                finally { child?.Dispose(); TryDeleteCaptureScratch(scratchDirectory); }
            }
        }
    }

    private static string ReplaceAndValidateArguments(
        List<string> arguments,
        string privateSerial,
        string accelerator)
    {
        if (accelerator is not ("whpx" or "tcg"))
            throw new InvalidOperationException("capture_accelerator_invalid");
        int serialIndex = -1;
        int memoryIndex = -1;
        int machineIndex = -1;
        int displayIndex = -1;
        int noRebootCount = 0;
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (string.Equals(argument, "-serial", StringComparison.Ordinal))
            {
                if (serialIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "serial_argument_duplicate_or_missing");
                }
                serialIndex = index;
            }
            else if (string.Equals(argument, "-m", StringComparison.Ordinal))
            {
                if (memoryIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "memory_argument_duplicate_or_missing");
                }
                memoryIndex = index;
            }
            else if (string.Equals(
                argument,
                "-machine",
                StringComparison.Ordinal))
            {
                if (machineIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "machine_argument_duplicate_or_missing");
                }
                machineIndex = index;
            }
            else if (string.Equals(
                argument,
                "-display",
                StringComparison.Ordinal))
            {
                if (displayIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "display_argument_duplicate_or_missing");
                }
                displayIndex = index;
            }
            else if (string.Equals(
                argument,
                "-no-reboot",
                StringComparison.Ordinal))
            {
                noRebootCount++;
            }
            else if (argument is "-accel" or "-enable-kvm" or "--enable-kvm")
            {
                throw new InvalidOperationException("accelerator_must_match_machine_selection");
            }
            else if (string.Equals(argument, "-qmp", StringComparison.Ordinal)
                || string.Equals(
                    argument,
                    "-monitor",
                    StringComparison.Ordinal)
                || string.Equals(
                    argument,
                    "-chardev",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "preexisting_monitor_argument");
            }
        }

        if (serialIndex < 0)
        {
            throw new InvalidOperationException("serial_argument_missing");
        }
        if (memoryIndex < 0
            || !string.Equals(
                arguments[memoryIndex + 1],
                "256M",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "guest_memory_must_be_256M");
        }
        if (machineIndex < 0
            || !string.Equals(
                arguments[machineIndex + 1],
                OS.Tooling.QemuContracts.MachineArgument(accelerator),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "machine_must_be_q35_selected_accelerator");
        }
        if (displayIndex < 0
            || !string.Equals(
                arguments[displayIndex + 1],
                "none",
                StringComparison.Ordinal)
            || noRebootCount != 1)
        {
            throw new InvalidOperationException(
                "headless_no_reboot_contract_invalid");
        }

        string serialValue = arguments[serialIndex + 1];
        if (!serialValue.StartsWith("file:", StringComparison.Ordinal)
            || serialValue.Length == 5)
        {
            throw new InvalidOperationException(
                "serial_must_be_file_backend");
        }
        string publicSerial = FullPath(serialValue.Substring(5));
        arguments[serialIndex + 1] = "file:" + privateSerial;
        return publicSerial;
    }

    private static long DrainPrivateSerial(
        string privateSerial,
        long offset,
        SerialTerminalGate gate)
    {
        if (!File.Exists(privateSerial))
        {
            return offset;
        }

        using FileStream stream = new FileStream(
            privateSerial,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < offset)
        {
            throw new InvalidDataException(
                "private_serial_truncated");
        }
        stream.Position = offset;
        byte[] buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int index = 0; index < read; index++)
            {
                gate.Accept(buffer[index]);
            }
        }
        return stream.Position;
    }

    private static void WriteObservationSidecar(
        string path,
        ObservationScanner.ObservationEvidence evidence,
        string privateSerialHash,
        string predictedPublicHash,
        int guestTimeoutSeconds,
        int captureTimeoutSeconds)
    {
        string temporaryPath =
            path + ".tmp-" + Guid.NewGuid().ToString("N");
        bool moved = false;
        try
        {
            using (FileStream stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: true))
            {
                writer.WriteLine("schema=clros.m01.raw-observations.v1");
                writer.WriteLine(
                    "captureMethod=qmp-pmemsave-stopped-before-terminal-release");
                writer.WriteLine("guestMemoryBytes=" + GuestMemoryBytes);
                writer.WriteLine("captureChunkBytes=" + CaptureChunkBytes);
                writer.WriteLine(
                    "guestTimeoutSeconds="
                    + guestTimeoutSeconds.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine(
                    "captureTimeoutSeconds="
                    + captureTimeoutSeconds.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine("terminalWithheld=true");
                writer.WriteLine("qmpPaused=true");
                writer.WriteLine("qmpMemoryShapeValidated=true");
                writer.WriteLine("memoryDumpRetained=false");
                writer.WriteLine(
                    "memorySha256=" + evidence.MemorySha256);
                writer.WriteLine(
                    "preflightCandidateCount="
                    + evidence.PreflightCandidateCount.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine(
                    "preflightPhysicalAddress="
                    + Hex(evidence.PreflightPhysicalAddress));
                writer.WriteLine(
                    "preflightPhysicalAddresses="
                    + evidence.PreflightPhysicalAddresses);
                writer.WriteLine(
                    "preflightPodSha256=" + evidence.PreflightPodSha256);
                writer.WriteLine(
                    "preflightPodBase64=" + evidence.PreflightPodBase64);
                writer.WriteLine("cr0Before=" + Hex(evidence.Cr0));
                writer.WriteLine(
                    "firmwareCr3Before=" + Hex(evidence.RawCr3));
                writer.WriteLine("rawCr4Before=" + Hex(evidence.RawCr4));
                writer.WriteLine(
                    "ia32EferBefore=" + Hex(evidence.Ia32Efer));
                writer.WriteLine(
                    "cpuidMaximumExtendedLeaf="
                    + Hex32(evidence.CpuidMaximumExtendedLeaf));
                writer.WriteLine(
                    "cpuidAddressSizesEax="
                    + Hex32(evidence.CpuidAddressSizesEax));
                writer.WriteLine(
                    "physicalAddressWidth="
                    + evidence.PhysicalAddressWidth.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine(
                    "physicalAddressMask="
                    + Hex(evidence.PhysicalAddressMask));
                writer.WriteLine("ownedCr3=" + Hex(evidence.OwnedCr3));
                writer.WriteLine("rflagsBefore=" + Hex(evidence.Rflags));
                writer.WriteLine(
                    "runtimeStatePhysicalAddress="
                    + Hex(evidence.RuntimeStatePhysicalAddress));
                writer.WriteLine(
                    "runtimeStatePodSha256="
                    + evidence.RuntimeStatePodSha256);
                writer.WriteLine(
                    "runtimeStatePodBase64="
                    + evidence.RuntimeStatePodBase64);
                writer.WriteLine(
                    "transitionCanaryPhysicalAddress="
                    + Hex(evidence.TransitionCanaryPhysicalAddress));
                writer.WriteLine(
                    "transitionCanaryValue="
                    + Hex(evidence.TransitionCanaryValue));
                writer.WriteLine(
                    "callGatePhysicalAddress="
                    + Hex(evidence.CallGatePhysicalAddress));
                writer.WriteLine(
                    "callGatePodSha256=" + evidence.CallGatePodSha256);
                writer.WriteLine(
                    "callGatePodBase64=" + evidence.CallGatePodBase64);
                writer.WriteLine(
                    "firmwareCr3After="
                    + Hex(evidence.PostReturnCr3));
                writer.WriteLine(
                    "rawCr4After="
                    + Hex(evidence.PostReturnCr4));
                writer.WriteLine(
                    "rflagsAfter="
                    + Hex(evidence.PostReturnRflags));
                writer.WriteLine(
                    "runtimeFirmwareCr3Readback="
                    + Hex(evidence.FirmwareCr3Readback));
                writer.WriteLine(
                    "runtimeRestoredCr4Readback="
                    + Hex(evidence.RestoredCr4Readback));
                writer.WriteLine(
                    "runtimeRestoredRflagsIntent="
                    + Hex(evidence.RestoredRflagsIntent));
                writer.WriteLine(
                    "privateSerialSha256=" + privateSerialHash);
                writer.WriteLine(
                    "predictedPublicSerialSha256="
                    + predictedPublicHash);
                writer.WriteLine("exactEqualityValidation=pass");
                writer.WriteLine("terminalReleaseAuthorized=true");
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path);
            moved = true;
        }
        finally
        {
            if (!moved && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string Sha256File(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string Sha256FilePlusBytes(
        string path,
        byte[] suffix)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        {
            byte[] buffer = new byte[65536];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        hash.AppendData(suffix);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string RequiredEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "missing_environment_" + name);
        }
        return value;
    }

    private static int RequiredPositiveInt(string name)
    {
        string value = RequiredEnvironment(name);
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsed)
            || parsed <= 0
            || parsed > 300)
        {
            throw new InvalidOperationException(
                "invalid_environment_" + name);
        }
        return parsed;
    }

    private static bool RequiredBoolean(string name)
    {
        string value = RequiredEnvironment(name);
        if (string.Equals(value, "true", StringComparison.Ordinal))
        {
            return true;
        }
        if (string.Equals(value, "false", StringComparison.Ordinal))
        {
            return false;
        }
        throw new InvalidOperationException(
            "invalid_boolean_environment_" + name);
    }

    private static string FullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static void RequireCreateNewPath(string path, string name)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException(name + "_already_exists");
        }
    }

    private static void RequireDistinctPaths(params string[] paths)
    {
        HashSet<string> seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<string> normalizedPaths = new List<string>();
        foreach (string path in paths)
        {
            string normalized = Path.GetFullPath(path)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            if (!seen.Add(normalized))
            {
                throw new InvalidOperationException("capture_path_overlap");
            }
            normalizedPaths.Add(normalized);
        }
        for (int left = 0; left < normalizedPaths.Count; left++)
        {
            for (int right = left + 1;
                right < normalizedPaths.Count;
                right++)
            {
                string leftPrefix =
                    normalizedPaths[left]
                    + Path.DirectorySeparatorChar;
                string rightPrefix =
                    normalizedPaths[right]
                    + Path.DirectorySeparatorChar;
                if (normalizedPaths[right].StartsWith(
                        leftPrefix,
                        StringComparison.OrdinalIgnoreCase)
                    || normalizedPaths[left].StartsWith(
                        rightPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "capture_path_containment");
                }
            }
        }
    }

    private static void TryDeleteCaptureScratch(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(directory))
            {
                File.Delete(file);
            }
            Directory.Delete(directory, recursive: false);
        }
        catch
        {
        }
    }

    private static void DeleteEmptyCaptureScratch(string directory)
    {
        if (!Directory.Exists(directory)
            || Directory.GetFileSystemEntries(directory).Length != 0)
        {
            throw new IOException(
                "capture_scratch_not_empty_after_scan");
        }
        Directory.Delete(directory, recursive: false);
        if (Directory.Exists(directory))
        {
            throw new IOException(
                "capture_scratch_delete_failed");
        }
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(
                char.IsLetterOrDigit(character)
                    || character == '_'
                    || character == '-'
                    || character == '.'
                    ? character
                    : '_');
        }
        return builder.ToString();
    }

    private static string Hex(ulong value)
    {
        return "0x" + value.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static string Hex32(uint value)
    {
        return "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
    }

    private static void Log(string value)
    {
        lock (LogLock)
        {
            s_log?.WriteLine(value);
            s_log?.Flush();
        }
    }

    private sealed class SerialTerminalGate
    {
        private readonly FileStream _publicStream;
        private readonly string[] _guestMarkers;
        private readonly string[] _forbiddenMarkers;
        private readonly List<byte> _line = new List<byte>();
        private readonly MemoryStream _withheld = new MemoryStream();
        private int _nextMarker;

        public SerialTerminalGate(
            FileStream publicStream,
            string[] guestMarkers,
            string[] forbiddenMarkers)
        {
            _publicStream = publicStream;
            _guestMarkers = guestMarkers;
            _forbiddenMarkers = forbiddenMarkers;
        }

        public bool TerminalWithheld { get; private set; }

        public void Accept(byte value)
        {
            _line.Add(value);
            if (value == (byte)'\n')
            {
                ProcessCompleteLine();
            }
        }

        public void ValidateCompleteMarkerContract()
        {
            if (!TerminalWithheld
                || _nextMarker != _guestMarkers.Length)
            {
                throw new InvalidDataException(
                    "marker_contract_incomplete");
            }
        }

        public void CompleteAfterStop()
        {
            if (_line.Count != 0)
            {
                if (!TerminalWithheld)
                {
                    throw new InvalidDataException(
                        "partial_line_before_terminal");
                }
                byte[] partial = _line.ToArray();
                _withheld.Write(partial, 0, partial.Length);
                _line.Clear();
            }
        }

        public byte[] GetWithheldBytes()
        {
            return _withheld.ToArray();
        }

        private void ProcessCompleteLine()
        {
            byte[] bytes = _line.ToArray();
            _line.Clear();
            string line = Encoding.UTF8.GetString(bytes)
                .TrimEnd('\r', '\n');

            foreach (string forbidden in _forbiddenMarkers)
            {
                if (string.Equals(line, forbidden, StringComparison.Ordinal)
                    || line.StartsWith(
                        forbidden + " ",
                        StringComparison.Ordinal))
                {
                    WriteCurrent(bytes);
                    throw new InvalidDataException(
                        "forbidden_marker_" + Sanitize(forbidden));
                }
            }
            if (string.Equals(
                    line,
                    "M01_OWNED_CR3_ENTERED",
                    StringComparison.Ordinal)
                || line.StartsWith(
                    "M01_OWNED_CR3_ENTERED ",
                    StringComparison.Ordinal))
            {
                WriteCurrent(bytes);
                throw new InvalidDataException(
                    "owned_cr3_serial_marker_forbidden");
            }

            int markerIndex = Array.IndexOf(_guestMarkers, line);
            if (markerIndex >= 0)
            {
                if (markerIndex != _nextMarker)
                {
                    WriteCurrent(bytes);
                    throw new InvalidDataException(
                        "marker_order_or_duplicate_"
                        + Sanitize(line));
                }
                _nextMarker++;
            }

            if (string.Equals(
                    line,
                    SuccessTerminal,
                    StringComparison.Ordinal))
            {
                if (TerminalWithheld)
                {
                    throw new InvalidDataException(
                        "duplicate_success_terminal");
                }
                TerminalWithheld = true;
                _withheld.Write(bytes, 0, bytes.Length);
                return;
            }

            WriteCurrent(bytes);
        }

        private void WriteCurrent(byte[] bytes)
        {
            if (TerminalWithheld)
            {
                _withheld.Write(bytes, 0, bytes.Length);
                return;
            }
            _publicStream.Write(bytes, 0, bytes.Length);
            _publicStream.Flush();
        }
    }

    private sealed class QmpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly Action<string> _log;
        private int _nextId;

        public QmpConnection(
            TcpClient client,
            int timeoutSeconds,
            Action<string> log)
        {
            _client = client;
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = checked(timeoutSeconds * 1000);
            stream.WriteTimeout = checked(timeoutSeconds * 1000);
            _reader = new StreamReader(
                stream,
                new UTF8Encoding(false),
                false,
                4096,
                leaveOpen: true);
            _writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            _log = log;
        }

        public void Initialize()
        {
            string greeting = ReadLineRequired("qmp_greeting_missing");
            _log("qmpGreetingBase64=" + Convert.ToBase64String(
                Encoding.UTF8.GetBytes(greeting)));
            using JsonDocument document = JsonDocument.Parse(greeting);
            if (!document.RootElement.TryGetProperty(
                    "QMP",
                    out JsonElement qmp)
                || !qmp.TryGetProperty(
                    "version",
                    out JsonElement version)
                || !version.TryGetProperty(
                    "qemu",
                    out JsonElement qemuVersion)
                || !qemuVersion.TryGetProperty(
                    "major",
                    out _))
            {
                throw new InvalidDataException(
                    "qmp_greeting_invalid");
            }
            Send("qmp_capabilities", null);
        }

        public void RequireMemoryShape()
        {
            JsonElement summary = Send(
                "query-memory-size-summary",
                null);
            long pluggedMemoryBytes = 0;
            if (!summary.TryGetProperty(
                    "base-memory",
                    out JsonElement baseMemory)
                || !baseMemory.TryGetInt64(out long baseMemoryBytes)
                || baseMemoryBytes != GuestMemoryBytes
                || (summary.TryGetProperty(
                        "plugged-memory",
                        out JsonElement pluggedMemory)
                    && !pluggedMemory.TryGetInt64(
                        out pluggedMemoryBytes))
                || pluggedMemoryBytes != 0)
            {
                throw new InvalidDataException(
                    "qmp_memory_shape_invalid");
            }
            _log("qmpMemoryShapeValidated=true");
        }

        public void StopAndRequirePaused()
        {
            Send("stop", null);
            JsonElement status = Send("query-status", null);
            if (!status.TryGetProperty(
                    "running",
                    out JsonElement running)
                || running.ValueKind != JsonValueKind.False
                || !status.TryGetProperty(
                    "status",
                    out JsonElement statusName)
                || !string.Equals(
                    statusName.GetString(),
                    "paused",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "qmp_stop_not_paused");
            }
            _log("qmpPaused=true");
        }

        public void Pmemsave(
            long physicalAddress,
            int size,
            string path,
            int chunkIndex)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>
                {
                    ["val"] = physicalAddress,
                    ["size"] = size,
                    ["filename"] = path
                };
            Send(
                "pmemsave",
                arguments,
                "pmemsave-"
                    + chunkIndex.ToString(
                        "D2",
                        CultureInfo.InvariantCulture));
        }

        public JsonElement Send(
            string execute,
            Dictionary<string, object>? arguments,
            string? explicitId = null)
        {
            string id = explicitId
                ?? "command-"
                    + Interlocked.Increment(ref _nextId)
                        .ToString(
                            "D3",
                            CultureInfo.InvariantCulture);
            Dictionary<string, object> request =
                new Dictionary<string, object>
                {
                    ["execute"] = execute,
                    ["id"] = id
                };
            if (arguments is not null)
            {
                request["arguments"] = arguments;
            }
            string json = JsonSerializer.Serialize(request);
            _log("qmpRequestBase64=" + Convert.ToBase64String(
                Encoding.UTF8.GetBytes(json)));
            _writer.WriteLine(json);

            while (true)
            {
                string response = ReadLineRequired(
                    "qmp_response_missing_" + Sanitize(execute));
                _log("qmpResponseBase64=" + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(response)));
                using JsonDocument document = JsonDocument.Parse(response);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty(
                        "event",
                        out JsonElement eventName))
                {
                    string? name = eventName.GetString();
                    if (string.Equals(
                                name,
                                "RESET",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "SHUTDOWN",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "GUEST_PANICKED",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "WATCHDOG",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "POWERDOWN",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "RESUME",
                                StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "qmp_terminal_event_"
                            + Sanitize(name ?? "unknown"));
                    }
                    continue;
                }
                if (!root.TryGetProperty(
                        "id",
                        out JsonElement responseId)
                    || !string.Equals(
                        responseId.GetString(),
                        id,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (root.TryGetProperty("error", out JsonElement error))
                {
                    throw new InvalidDataException(
                        "qmp_error_"
                        + Sanitize(execute)
                        + "_"
                        + Sanitize(error.ToString()));
                }
                if (!root.TryGetProperty(
                        "return",
                        out JsonElement result))
                {
                    throw new InvalidDataException(
                        "qmp_return_missing_"
                        + Sanitize(execute));
                }
                return result.Clone();
            }
        }

        public void Dispose()
        {
            _writer.Dispose();
            _reader.Dispose();
            _client.Dispose();
        }

        private string ReadLineRequired(string reason)
        {
            string? line = _reader.ReadLine();
            if (line is null)
            {
                throw new EndOfStreamException(reason);
            }
            return line;
        }
    }

    private sealed class ObservationScanner
    {
        private readonly List<PreflightCandidate> _preflight =
            new List<PreflightCandidate>();
        private readonly List<RuntimeCandidate> _runtime =
            new List<RuntimeCandidate>();
        private readonly HashSet<long> _preflightAddresses =
            new HashSet<long>();
        private readonly HashSet<long> _runtimeAddresses =
            new HashSet<long>();

        public static ObservationEvidence Capture(
            QmpConnection qmp,
            string scratchDirectory,
            int captureTimeoutSeconds)
        {
            DateTime deadline =
                DateTime.UtcNow.AddSeconds(captureTimeoutSeconds);
            ObservationScanner scanner = new ObservationScanner();
            using IncrementalHash memoryHash =
                IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] carry = Array.Empty<byte>();

            for (int chunkIndex = 0;
                chunkIndex < GuestMemoryBytes / CaptureChunkBytes;
                chunkIndex++)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        "observation_capture_timeout");
                }
                long physicalAddress =
                    (long)chunkIndex * CaptureChunkBytes;
                string chunkPath = Path.Combine(
                    scratchDirectory,
                    "memory-"
                        + chunkIndex.ToString(
                            "D2",
                            CultureInfo.InvariantCulture)
                        + ".bin");
                if (File.Exists(chunkPath))
                {
                    throw new IOException(
                        "capture_chunk_already_exists");
                }
                try
                {
                    qmp.Pmemsave(
                        physicalAddress,
                        CaptureChunkBytes,
                        chunkPath,
                        chunkIndex);
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException(
                            "observation_capture_timeout");
                    }
                    FileInfo info = new FileInfo(chunkPath);
                    if (!info.Exists
                        || info.Length != CaptureChunkBytes)
                    {
                        throw new InvalidDataException(
                            "capture_chunk_size_mismatch_"
                            + chunkIndex.ToString(
                                CultureInfo.InvariantCulture));
                    }
                    byte[] bytes = File.ReadAllBytes(chunkPath);
                    memoryHash.AppendData(bytes);

                    byte[] scanBytes =
                        new byte[carry.Length + bytes.Length];
                    Buffer.BlockCopy(
                        carry,
                        0,
                        scanBytes,
                        0,
                        carry.Length);
                    Buffer.BlockCopy(
                        bytes,
                        0,
                        scanBytes,
                        carry.Length,
                        bytes.Length);
                    scanner.Scan(
                        scanBytes,
                        physicalAddress - carry.Length);
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException(
                            "observation_capture_timeout");
                    }

                    int carryLength = Math.Min(
                        ScanCarryBytes,
                        scanBytes.Length);
                    carry = new byte[carryLength];
                    Buffer.BlockCopy(
                        scanBytes,
                        scanBytes.Length - carryLength,
                        carry,
                        0,
                        carryLength);
                }
                finally
                {
                    if (File.Exists(chunkPath))
                    {
                        File.Delete(chunkPath);
                    }
                }
            }

            string memorySha256 = Convert.ToHexString(
                memoryHash.GetHashAndReset());
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    "observation_capture_timeout");
            }
            return scanner.Complete(memorySha256);
        }

        private void Scan(byte[] bytes, long physicalBase)
        {
            long firstAligned8 =
                (physicalBase + 7L) & ~7L;
            for (long physical = firstAligned8;
                physical >= 0
                    && physical + PreflightCandidate.Size
                        <= physicalBase + bytes.Length;
                physical += 8)
            {
                int offset = checked((int)(physical - physicalBase));
                if (ReadUInt32(bytes, offset + 48) == 0x31464c50U
                    && ReadUInt32(bytes, offset + 52) == 0
                    && _preflightAddresses.Add(physical))
                {
                    _preflight.Add(
                        PreflightCandidate.Read(
                            bytes,
                            offset,
                            physical));
                }
            }

            long firstPage =
                (physicalBase + 4095L) & ~4095L;
            for (long physical = firstPage;
                physical >= 0
                    && physical + RuntimeCandidate.TotalSpan
                        <= physicalBase + bytes.Length;
                physical += 4096)
            {
                int offset = checked((int)(physical - physicalBase));
                RuntimeCandidate? candidate =
                    RuntimeCandidate.TryRead(
                        bytes,
                        offset,
                        physical);
                if (candidate is not null
                    && _runtimeAddresses.Add(physical))
                {
                    _runtime.Add(candidate);
                }
            }
        }

        private ObservationEvidence Complete(string memorySha256)
        {
            if (_runtime.Count != 1)
            {
                throw new InvalidDataException(
                    "runtime_candidate_count_"
                    + _runtime.Count.ToString(
                        CultureInfo.InvariantCulture));
            }
            RuntimeCandidate runtime = _runtime[0];
            List<PreflightCandidate> matching =
                new List<PreflightCandidate>();
            foreach (PreflightCandidate candidate in _preflight)
            {
                if (candidate.Matches(runtime))
                {
                    matching.Add(candidate);
                }
            }
            if (matching.Count == 0)
            {
                throw new InvalidDataException(
                    "preflight_candidate_count_"
                    + matching.Count.ToString(
                        CultureInfo.InvariantCulture));
            }
            PreflightCandidate preflight = matching[0];
            for (int index = 1; index < matching.Count; index++)
            {
                if (!preflight.Bytes.AsSpan().SequenceEqual(
                        matching[index].Bytes))
                {
                    throw new InvalidDataException(
                        "preflight_candidate_bytes_conflict");
                }
            }
            preflight.ValidateModeAndAddress(runtime);
            return new ObservationEvidence(
                memorySha256,
                matching,
                preflight,
                runtime);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(
                bytes[offset]
                | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(
                bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return
                bytes[offset]
                | ((ulong)bytes[offset + 1] << 8)
                | ((ulong)bytes[offset + 2] << 16)
                | ((ulong)bytes[offset + 3] << 24)
                | ((ulong)bytes[offset + 4] << 32)
                | ((ulong)bytes[offset + 5] << 40)
                | ((ulong)bytes[offset + 6] << 48)
                | ((ulong)bytes[offset + 7] << 56);
        }

        private static byte[] Copy(
            byte[] source,
            int offset,
            int count)
        {
            byte[] copy = new byte[count];
            Buffer.BlockCopy(source, offset, copy, 0, count);
            return copy;
        }

        public sealed class PreflightCandidate
        {
            public const int Size = 56;

            private PreflightCandidate(
                long physicalAddress,
                byte[] bytes,
                ulong cr0,
                ulong rawCr3,
                ulong rawCr4,
                ulong ia32Efer,
                uint cpuidMaximumExtendedLeaf,
                uint cpuidAddressSizesEax,
                ulong rflags)
            {
                PhysicalAddress = physicalAddress;
                Bytes = bytes;
                Cr0 = cr0;
                RawCr3 = rawCr3;
                RawCr4 = rawCr4;
                Ia32Efer = ia32Efer;
                CpuidMaximumExtendedLeaf =
                    cpuidMaximumExtendedLeaf;
                CpuidAddressSizesEax = cpuidAddressSizesEax;
                Rflags = rflags;
            }

            public long PhysicalAddress { get; }
            public byte[] Bytes { get; }
            public ulong Cr0 { get; }
            public ulong RawCr3 { get; }
            public ulong RawCr4 { get; }
            public ulong Ia32Efer { get; }
            public uint CpuidMaximumExtendedLeaf { get; }
            public uint CpuidAddressSizesEax { get; }
            public byte PhysicalAddressWidth =>
                (byte)CpuidAddressSizesEax;
            public ulong Rflags { get; }

            public static PreflightCandidate Read(
                byte[] bytes,
                int offset,
                long physicalAddress)
            {
                return new PreflightCandidate(
                    physicalAddress,
                    Copy(bytes, offset, Size),
                    ReadUInt64(bytes, offset),
                    ReadUInt64(bytes, offset + 8),
                    ReadUInt64(bytes, offset + 16),
                    ReadUInt64(bytes, offset + 24),
                    ReadUInt32(bytes, offset + 32),
                    ReadUInt32(bytes, offset + 36),
                    ReadUInt64(bytes, offset + 40));
            }

            public bool Matches(RuntimeCandidate runtime)
            {
                return Cr0 == runtime.OriginalCr0
                    && RawCr3 == runtime.OriginalCr3
                    && RawCr4 == runtime.OriginalCr4
                    && Ia32Efer == runtime.OriginalEfer
                    && Rflags == runtime.OriginalRflags;
            }

            public void ValidateModeAndAddress(
                RuntimeCandidate runtime)
            {
                const ulong RequiredCr0 = 0x80010001UL;
                const ulong RequiredEfer = 0x0d00UL;
                const ulong Cr4Pae = 1UL << 5;
                const ulong Cr4La57 = 1UL << 12;
                const ulong Cr4Pcide = 1UL << 17;
                const ulong Cr4Cet = 1UL << 23;
                const ulong RflagsOne = 1UL << 1;
                const ulong RflagsTrap = 1UL << 8;
                const ulong RflagsDirection = 1UL << 10;
                const ulong Cr3AllowedLow = (1UL << 3) | (1UL << 4);

                byte width = PhysicalAddressWidth;
                if (CpuidMaximumExtendedLeaf < 0x80000008U
                    || width < 36
                    || width > 52
                    || (Cr0 & RequiredCr0) != RequiredCr0
                    || (RawCr4 & Cr4Pae) == 0
                    || (RawCr4
                        & (Cr4La57 | Cr4Pcide | Cr4Cet)) != 0
                    || (Ia32Efer & RequiredEfer) != RequiredEfer
                    || (Rflags & RflagsOne) == 0
                    || (Rflags
                        & (RflagsTrap | RflagsDirection)) != 0)
                {
                    throw new InvalidDataException(
                        "preflight_mode_invalid");
                }
                ulong physicalMask = (1UL << width) - 1;
                ulong allowedCr3 =
                    (physicalMask & ~0xfffUL) | Cr3AllowedLow;
                ulong firmwareRoot =
                    RawCr3 & (physicalMask & ~0xfffUL);
                if (runtime.PhysicalAddressMask != physicalMask
                    || (RawCr3 & ~allowedCr3) != 0
                    || firmwareRoot == 0)
                {
                    throw new InvalidDataException(
                        "preflight_cr3_or_width_invalid");
                }
            }
        }

        public sealed class RuntimeCandidate
        {
            public const int StateSize = 480;
            public const int GateOffset = 0x300;
            public const int GateSize = 160;
            public const int TotalSpan = GateOffset + GateSize;

            private RuntimeCandidate(
                long physicalAddress,
                byte[] stateBytes,
                byte[] gateBytes,
                ulong transitionCanaryValue)
            {
                PhysicalAddress = physicalAddress;
                StateBytes = stateBytes;
                GateBytes = gateBytes;
                OriginalRflags = ReadUInt64(stateBytes, 200);
                OriginalCr0 = ReadUInt64(stateBytes, 208);
                OriginalCr3 = ReadUInt64(stateBytes, 216);
                OriginalCr4 = ReadUInt64(stateBytes, 224);
                OriginalEfer = ReadUInt64(stateBytes, 232);
                PhysicalAddressMask = ReadUInt64(stateBytes, 240);
                OwnedCr3 = ReadUInt64(stateBytes, 8);
                TransitionCanaryValue = transitionCanaryValue;
                FirmwareCr3Readback = ReadUInt64(stateBytes, 408);
                RestoredCr4Readback = ReadUInt64(stateBytes, 424);
                RestoredRflagsIntent = ReadUInt64(stateBytes, 432);
                PostReturnRflags = ReadUInt64(gateBytes, 104);
                PostReturnCr3 = ReadUInt64(gateBytes, 112);
                PostReturnCr4 = ReadUInt64(gateBytes, 120);
            }

            public long PhysicalAddress { get; }
            public long GatePhysicalAddress =>
                PhysicalAddress + GateOffset;
            public byte[] StateBytes { get; }
            public byte[] GateBytes { get; }
            public ulong OriginalRflags { get; }
            public ulong OriginalCr0 { get; }
            public ulong OriginalCr3 { get; }
            public ulong OriginalCr4 { get; }
            public ulong OriginalEfer { get; }
            public ulong PhysicalAddressMask { get; }
            public ulong OwnedCr3 { get; }
            public ulong TransitionCanaryValue { get; }
            public ulong FirmwareCr3Readback { get; }
            public ulong RestoredCr4Readback { get; }
            public ulong RestoredRflagsIntent { get; }
            public ulong PostReturnRflags { get; }
            public ulong PostReturnCr3 { get; }
            public ulong PostReturnCr4 { get; }

            public static RuntimeCandidate? TryRead(
                byte[] bytes,
                int offset,
                long physicalAddress)
            {
                if (ReadUInt32(bytes, offset) != StateSize
                    || ReadUInt16(bytes, offset + 4) != 1
                    || bytes[offset + 6] != 1
                    || bytes[offset + 7] != 0
                    || ReadUInt64(bytes, offset + 184)
                        != (ulong)physicalAddress)
                {
                    return null;
                }

                byte[] state = Copy(
                    bytes,
                    offset,
                    StateSize);
                byte[] gate = Copy(
                    bytes,
                    offset + GateOffset,
                    GateSize);
                ulong originalRflags = ReadUInt64(state, 200);
                ulong originalCr3 = ReadUInt64(state, 216);
                ulong originalCr4 = ReadUInt64(state, 224);
                ulong ownedCr3 = ReadUInt64(state, 8);
                ulong cliRflags = ReadUInt64(state, 272);
                ulong physicalPageMask =
                    ReadUInt64(state, 240) & ~0xfffUL;
                ulong allowedOwnedCr3Mask =
                    physicalPageMask | (1UL << 3) | (1UL << 4);
                const ulong MutableBeforeCliReadback =
                    (1UL << 0)
                    | (1UL << 2)
                    | (1UL << 4)
                    | (1UL << 6)
                    | (1UL << 7)
                    | (1UL << 9)
                    | (1UL << 11);
                bool originalPge =
                    (originalCr4 & (1UL << 7)) != 0;

                if (ReadUInt32(gate, 0) != GateSize
                    || ReadUInt16(gate, 4) != 1
                    || gate[6] != 1
                    || gate[7] != 3
                    || ReadUInt64(gate, 8)
                        != (ulong)(physicalAddress + GateOffset)
                    || ReadUInt64(gate, 48) != originalRflags
                    || ReadUInt64(gate, 56) != originalCr3
                    || ReadUInt64(gate, 64) != originalCr4
                    || ReadUInt64(gate, 72) == 0
                    || ReadUInt32(gate, 80) != 1
                    || gate[84]
                        != (((originalRflags & (1UL << 9)) != 0)
                            ? (byte)1
                            : (byte)0)
                    || gate[85]
                        != (originalPge ? (byte)1 : (byte)0)
                    || gate[86] != 1
                    || gate[87] != 1
                    || ReadUInt64(gate, 88)
                        != ReadUInt64(gate, 72)
                    || ReadUInt64(gate, 96)
                        != ReadUInt64(gate, 72)
                    || ReadUInt64(gate, 104) != originalRflags
                    || ReadUInt64(gate, 112) != originalCr3
                    || ReadUInt64(gate, 120) != originalCr4
                    || ReadUInt64(gate, 128)
                        != ReadUInt64(gate, 72)
                    || ReadUInt32(gate, 136) != 1
                    || gate[140] != 1
                    || gate[141] != 1
                    || ReadUInt16(gate, 142) != 0
                    || ReadUInt32(gate, 144) != 0x4b47534fU
                    || ReadUInt32(gate, 148) != 0
                    || ReadUInt64(gate, 152) != 0
                    || ReadUInt32(state, 180) != 0
                    || physicalPageMask == 0
                    || (ownedCr3 & ~allowedOwnedCr3Mask) != 0
                    || (ownedCr3 & physicalPageMask) == 0
                    || (ownedCr3 & physicalPageMask)
                        == (originalCr3 & physicalPageMask)
                    || (cliRflags & ~MutableBeforeCliReadback)
                        != (originalRflags & ~MutableBeforeCliReadback)
                    || (cliRflags & (1UL << 9)) != 0
                    || ReadUInt64(state, 280)
                        != (originalCr4 & ~(1UL << 7))
                    || ReadUInt64(state, 288) != ownedCr3
                    || ReadUInt64(state, 408) != originalCr3
                    || ReadUInt64(state, 424) != originalCr4
                    || ReadUInt64(state, 432) != originalRflags
                    || ReadUInt64(state, 248)
                        != ReadUInt64(state, 440)
                    || ReadUInt64(state, 448) != 1
                    || ReadUInt64(state, 456) != 1
                    || state[464] != 1
                    || state[465] != 17
                    || state[466] < 1
                    || state[466] > 4
                    || state[467] != 1
                    || state[468]
                        != (originalPge ? (byte)1 : (byte)0)
                    || state[469] != 1
                    || state[470] != 1
                    || state[471] != 12
                    || ReadUInt64(state, 472) != 0
                    || ReadUInt64(state, 296)
                        != ReadUInt64(state, 160)
                    || ReadUInt64(state, 304)
                        != ReadUInt64(state, 168)
                    || ReadUInt64(state, 416)
                        != ReadUInt64(state, 168)
                    || ReadUInt64(bytes, offset + StateSize)
                        != ReadUInt64(state, 168))
                {
                    return null;
                }

                return new RuntimeCandidate(
                    physicalAddress,
                    state,
                    gate,
                    ReadUInt64(bytes, offset + StateSize));
            }
        }

        public sealed class ObservationEvidence
        {
            public ObservationEvidence(
                string memorySha256,
                List<PreflightCandidate> preflightCandidates,
                PreflightCandidate preflight,
                RuntimeCandidate runtime)
            {
                MemorySha256 = memorySha256;
                PreflightCandidateCount = preflightCandidates.Count;
                StringBuilder addresses = new StringBuilder();
                for (int index = 0;
                    index < preflightCandidates.Count;
                    index++)
                {
                    if (index != 0)
                    {
                        addresses.Append(';');
                    }
                    addresses.Append(
                        Hex(checked(
                            (ulong)preflightCandidates[index]
                                .PhysicalAddress)));
                }
                PreflightPhysicalAddresses = addresses.ToString();
                PreflightPhysicalAddress =
                    checked((ulong)preflight.PhysicalAddress);
                PreflightPodSha256 = HashBytes(preflight.Bytes);
                PreflightPodBase64 =
                    Convert.ToBase64String(preflight.Bytes);
                Cr0 = preflight.Cr0;
                RawCr3 = preflight.RawCr3;
                RawCr4 = preflight.RawCr4;
                Ia32Efer = preflight.Ia32Efer;
                CpuidMaximumExtendedLeaf =
                    preflight.CpuidMaximumExtendedLeaf;
                CpuidAddressSizesEax =
                    preflight.CpuidAddressSizesEax;
                PhysicalAddressWidth =
                    preflight.PhysicalAddressWidth;
                PhysicalAddressMask =
                    runtime.PhysicalAddressMask;
                OwnedCr3 = runtime.OwnedCr3;
                Rflags = preflight.Rflags;
                RuntimeStatePhysicalAddress =
                    checked((ulong)runtime.PhysicalAddress);
                RuntimeStatePodSha256 =
                    HashBytes(runtime.StateBytes);
                RuntimeStatePodBase64 =
                    Convert.ToBase64String(runtime.StateBytes);
                TransitionCanaryPhysicalAddress = checked(
                    (ulong)(runtime.PhysicalAddress
                        + RuntimeCandidate.StateSize));
                TransitionCanaryValue =
                    runtime.TransitionCanaryValue;
                CallGatePhysicalAddress =
                    checked((ulong)runtime.GatePhysicalAddress);
                CallGatePodSha256 =
                    HashBytes(runtime.GateBytes);
                CallGatePodBase64 =
                    Convert.ToBase64String(runtime.GateBytes);
                PostReturnRflags = runtime.PostReturnRflags;
                PostReturnCr3 = runtime.PostReturnCr3;
                PostReturnCr4 = runtime.PostReturnCr4;
                FirmwareCr3Readback =
                    runtime.FirmwareCr3Readback;
                RestoredCr4Readback =
                    runtime.RestoredCr4Readback;
                RestoredRflagsIntent =
                    runtime.RestoredRflagsIntent;
            }

            public string MemorySha256 { get; }
            public int PreflightCandidateCount { get; }
            public string PreflightPhysicalAddresses { get; }
            public ulong PreflightPhysicalAddress { get; }
            public string PreflightPodSha256 { get; }
            public string PreflightPodBase64 { get; }
            public ulong Cr0 { get; }
            public ulong RawCr3 { get; }
            public ulong RawCr4 { get; }
            public ulong Ia32Efer { get; }
            public uint CpuidMaximumExtendedLeaf { get; }
            public uint CpuidAddressSizesEax { get; }
            public byte PhysicalAddressWidth { get; }
            public ulong PhysicalAddressMask { get; }
            public ulong OwnedCr3 { get; }
            public ulong Rflags { get; }
            public ulong RuntimeStatePhysicalAddress { get; }
            public string RuntimeStatePodSha256 { get; }
            public string RuntimeStatePodBase64 { get; }
            public ulong TransitionCanaryPhysicalAddress { get; }
            public ulong TransitionCanaryValue { get; }
            public ulong CallGatePhysicalAddress { get; }
            public string CallGatePodSha256 { get; }
            public string CallGatePodBase64 { get; }
            public ulong PostReturnRflags { get; }
            public ulong PostReturnCr3 { get; }
            public ulong PostReturnCr4 { get; }
            public ulong FirmwareCr3Readback { get; }
            public ulong RestoredCr4Readback { get; }
            public ulong RestoredRflagsIntent { get; }

            private static string HashBytes(byte[] bytes)
            {
                return Convert.ToHexString(
                    SHA256.HashData(bytes));
            }
        }
    }
}
