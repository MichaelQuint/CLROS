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

internal static partial class Program
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
            Console.Error.WriteLine("M01_RAW_OBSERVATION_SHIM_FAIL reason=" + Sanitize(exception.Message));
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
        string privateSerial = FullPath(RequiredEnvironment("CLROS_M01_PRIVATE_SERIAL"));
        string observationLog = FullPath(RequiredEnvironment("CLROS_M01_RAW_OBSERVATION_LOG"));
        string qmpLog = FullPath(RequiredEnvironment("CLROS_M01_QMP_TRANSCRIPT_LOG"));
        string scratchDirectory = FullPath(RequiredEnvironment("CLROS_M01_CAPTURE_SCRATCH"));
        int guestTimeoutSeconds = RequiredPositiveInt("CLROS_M01_GUEST_TIMEOUT_SECONDS");
        int captureTimeoutSeconds = RequiredPositiveInt("CLROS_M01_CAPTURE_TIMEOUT_SECONDS");
        bool m02SafeStubEnabled = RequiredBoolean("CLROS_M02_SAFE_STUB_PROBE");
        string accelerator = Environment.GetEnvironmentVariable("CLROS_M01_ACCELERATOR") ?? "tcg";
        RequireCreateNewPath(privateSerial, "private_serial");
        RequireCreateNewPath(observationLog, "observation_log");
        RequireCreateNewPath(qmpLog, "qmp_log");
        if (Directory.Exists(scratchDirectory) || File.Exists(scratchDirectory))
        {
            throw new InvalidOperationException("scratch_path_exists");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(qmpLog) ?? throw new InvalidOperationException("qmp_log_parent_missing"));
        {
            FileStream logStream = new FileStream(qmpLog, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            s_log = new StreamWriter(logStream, new UTF8Encoding(false), 4096, leaveOpen: false)
            {
                AutoFlush = true
            };
            List<string> childArguments = new List<string>(args);
            string publicSerial = ReplaceAndValidateArguments(childArguments, privateSerial, accelerator);
            RequireDistinctPaths(publicSerial, privateSerial, observationLog, qmpLog, scratchDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(publicSerial) ?? throw new InvalidOperationException("public_serial_parent_missing"));
            Directory.CreateDirectory(Path.GetDirectoryName(privateSerial) ?? throw new InvalidOperationException("private_serial_parent_missing"));
            Directory.CreateDirectory(Path.GetDirectoryName(observationLog) ?? throw new InvalidOperationException("observation_parent_missing"));
            Directory.CreateDirectory(scratchDirectory);
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            Process? child = null;
            bool childStarted = false;
            WhpxPrelaunch? prelaunch = null;
            Stopwatch setupClock = Stopwatch.StartNew();
            try
            {
                listener.Start(1);
                int qmpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                childArguments.Add("-qmp");
                childArguments.Add("tcp:127.0.0.1:" + qmpPort.ToString(CultureInfo.InvariantCulture));
                Log("schema=clros.m01.qmp-transcript.v1");
                Log("qmpTransport=outbound-loopback");
                Log("qmpServerMode=false");
                Log("guestMemoryBytes=" + GuestMemoryBytes);
                Log("captureChunkBytes=" + CaptureChunkBytes);
                Log("realQemuBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(realQemu)));
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

                child = new Process
                {
                    StartInfo = startInfo
                };
                prelaunch = WhpxPrelaunch.Create(child, accelerator, publicSerial);
                for (int index = 0; index < startInfo.ArgumentList.Count; index++)
                    Log("childArg" + index.ToString("D3", CultureInfo.InvariantCulture) + "Base64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(startInfo.ArgumentList[index])));
                if (!child.Start())
                {
                    throw new InvalidOperationException("qemu_process_start_failed");
                }

                childStarted = true;
                prelaunch?.ResumeAfterStart(TimeSpan.FromSeconds(Math.Min(guestTimeoutSeconds, 15)) - setupClock.Elapsed);
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
                if (!acceptTask.Wait(TimeSpan.FromSeconds(Math.Max(0, Math.Min(guestTimeoutSeconds, 15) - setupClock.Elapsed.TotalSeconds))))
                {
                    throw new TimeoutException("qmp_connect_timeout");
                }

                using TcpClient qmpSocket = acceptTask.Result;
                listener.Stop();
                using QmpConnection qmp = new QmpConnection(qmpSocket, captureTimeoutSeconds, Log);
                TimeSpan setupRemaining = TimeSpan.FromSeconds(Math.Min(guestTimeoutSeconds, 15)) - setupClock.Elapsed;
                if (setupRemaining <= TimeSpan.Zero)
                    throw new TimeoutException("qmp_setup_timeout");
                using (var setupDeadline = new CancellationTokenSource(setupRemaining))
                using (setupDeadline.Token.Register(() => qmpSocket.Dispose()))
                {
                    qmp.Initialize();
                    qmp.RequireMemoryShape();
                    if (setupDeadline.IsCancellationRequested)
                        throw new TimeoutException("qmp_setup_timeout");
                }

                using FileStream publicStream = new FileStream(publicSerial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough);
                SerialTerminalGate gate = new SerialTerminalGate(publicStream, m02SafeStubEnabled ? M02GuestMarkers : GuestMarkers, m02SafeStubEnabled ? M02ForbiddenMarkers : ForbiddenMarkers);
                long privateOffset = 0;
                DateTime guestDeadline = DateTime.UtcNow.AddSeconds(guestTimeoutSeconds);
                while (!gate.TerminalWithheld)
                {
                    privateOffset = DrainPrivateSerial(privateSerial, privateOffset, gate);
                    if (gate.TerminalWithheld)
                    {
                        break;
                    }

                    if (child.HasExited)
                    {
                        privateOffset = DrainPrivateSerial(privateSerial, privateOffset, gate);
                        throw new InvalidOperationException("qemu_exited_before_terminal_" + child.ExitCode.ToString(CultureInfo.InvariantCulture));
                    }

                    if (DateTime.UtcNow >= guestDeadline)
                    {
                        throw new TimeoutException("guest_terminal_timeout");
                    }

                    Thread.Sleep(20);
                }

                gate.ValidateCompleteMarkerContract();
                qmp.StopAndRequirePaused();
                long previousOffset = -1;
                for (int stablePass = 0; stablePass < 3;)
                {
                    privateOffset = DrainPrivateSerial(privateSerial, privateOffset, gate);
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
                string predictedPublicHash = Sha256FilePlusBytes(publicSerial, withheldBytes);
                if (!string.Equals(privateSerialHash, predictedPublicHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("serial_prediction_hash_mismatch");
                }

                ObservationScanner.ObservationEvidence evidence = ObservationScanner.Capture(qmp, scratchDirectory, captureTimeoutSeconds);
                DeleteEmptyCaptureScratch(scratchDirectory);
                WriteObservationSidecar(observationLog, evidence, privateSerialHash, predictedPublicHash, guestTimeoutSeconds, captureTimeoutSeconds);
                Log("M01_RAW_OBSERVATION_CAPTURED");
                Log("publicSerialSha256=" + predictedPublicHash);
                Log("privateSerialSha256=" + privateSerialHash);
                if (prelaunch?.Failure is string stderrFailure)
                    throw new IOException(stderrFailure);
                Log("terminalReleaseAuthorized=true");
                publicStream.Position = publicStream.Length;
                publicStream.Write(withheldBytes, 0, withheldBytes.Length);
                publicStream.Flush(flushToDisk: true);
                string actualPublicHash = Sha256File(publicSerial);
                if (!string.Equals(actualPublicHash, predictedPublicHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("released_public_serial_hash_mismatch");
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

                try
                {
                    prelaunch?.Dispose();
                }
                finally
                {
                    child?.Dispose();
                    TryDeleteCaptureScratch(scratchDirectory);
                }
            }
        }
    }

    private static string ReplaceAndValidateArguments(List<string> arguments, string privateSerial, string accelerator)
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
                    throw new InvalidOperationException("serial_argument_duplicate_or_missing");
                }

                serialIndex = index;
            }
            else if (string.Equals(argument, "-m", StringComparison.Ordinal))
            {
                if (memoryIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("memory_argument_duplicate_or_missing");
                }

                memoryIndex = index;
            }
            else if (string.Equals(argument, "-machine", StringComparison.Ordinal))
            {
                if (machineIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("machine_argument_duplicate_or_missing");
                }

                machineIndex = index;
            }
            else if (string.Equals(argument, "-display", StringComparison.Ordinal))
            {
                if (displayIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("display_argument_duplicate_or_missing");
                }

                displayIndex = index;
            }
            else if (string.Equals(argument, "-no-reboot", StringComparison.Ordinal))
            {
                noRebootCount++;
            }
            else if (argument is "-accel" or "-enable-kvm" or "--enable-kvm")
            {
                throw new InvalidOperationException("accelerator_must_match_machine_selection");
            }
            else if (string.Equals(argument, "-qmp", StringComparison.Ordinal) || string.Equals(argument, "-monitor", StringComparison.Ordinal) || string.Equals(argument, "-chardev", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("preexisting_monitor_argument");
            }
        }

        if (serialIndex < 0)
        {
            throw new InvalidOperationException("serial_argument_missing");
        }

        if (memoryIndex < 0 || !string.Equals(arguments[memoryIndex + 1], "256M", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("guest_memory_must_be_256M");
        }

        if (machineIndex < 0 || !string.Equals(arguments[machineIndex + 1], OS.Tooling.QemuContracts.MachineArgument(accelerator), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("machine_must_be_q35_selected_accelerator");
        }

        if (displayIndex < 0 || !string.Equals(arguments[displayIndex + 1], "none", StringComparison.Ordinal) || noRebootCount != 1)
        {
            throw new InvalidOperationException("headless_no_reboot_contract_invalid");
        }

        string serialValue = arguments[serialIndex + 1];
        if (!serialValue.StartsWith("file:", StringComparison.Ordinal) || serialValue.Length == 5)
        {
            throw new InvalidOperationException("serial_must_be_file_backend");
        }

        string publicSerial = FullPath(serialValue.Substring(5));
        arguments[serialIndex + 1] = "file:" + privateSerial;
        return publicSerial;
    }

    private static long DrainPrivateSerial(string privateSerial, long offset, SerialTerminalGate gate)
    {
        if (!File.Exists(privateSerial))
        {
            return offset;
        }

        using FileStream stream = new FileStream(privateSerial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < offset)
        {
            throw new InvalidDataException("private_serial_truncated");
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

    private static void WriteObservationSidecar(string path, ObservationScanner.ObservationEvidence evidence, string privateSerialHash, string predictedPublicHash, int guestTimeoutSeconds, int captureTimeoutSeconds)
    {
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        bool moved = false;
        try
        {
            using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.WriteLine("schema=clros.m01.raw-observations.v1");
                writer.WriteLine("captureMethod=qmp-pmemsave-stopped-before-terminal-release");
                writer.WriteLine("guestMemoryBytes=" + GuestMemoryBytes);
                writer.WriteLine("captureChunkBytes=" + CaptureChunkBytes);
                writer.WriteLine("guestTimeoutSeconds=" + guestTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("captureTimeoutSeconds=" + captureTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("terminalWithheld=true");
                writer.WriteLine("qmpPaused=true");
                writer.WriteLine("qmpMemoryShapeValidated=true");
                writer.WriteLine("memoryDumpRetained=false");
                writer.WriteLine("memorySha256=" + evidence.MemorySha256);
                writer.WriteLine("preflightCandidateCount=" + evidence.PreflightCandidateCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("preflightPhysicalAddress=" + Hex(evidence.PreflightPhysicalAddress));
                writer.WriteLine("preflightPhysicalAddresses=" + evidence.PreflightPhysicalAddresses);
                writer.WriteLine("preflightPodSha256=" + evidence.PreflightPodSha256);
                writer.WriteLine("preflightPodBase64=" + evidence.PreflightPodBase64);
                writer.WriteLine("cr0Before=" + Hex(evidence.Cr0));
                writer.WriteLine("firmwareCr3Before=" + Hex(evidence.RawCr3));
                writer.WriteLine("rawCr4Before=" + Hex(evidence.RawCr4));
                writer.WriteLine("ia32EferBefore=" + Hex(evidence.Ia32Efer));
                writer.WriteLine("cpuidMaximumExtendedLeaf=" + Hex32(evidence.CpuidMaximumExtendedLeaf));
                writer.WriteLine("cpuidAddressSizesEax=" + Hex32(evidence.CpuidAddressSizesEax));
                writer.WriteLine("physicalAddressWidth=" + evidence.PhysicalAddressWidth.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("physicalAddressMask=" + Hex(evidence.PhysicalAddressMask));
                writer.WriteLine("ownedCr3=" + Hex(evidence.OwnedCr3));
                writer.WriteLine("rflagsBefore=" + Hex(evidence.Rflags));
                writer.WriteLine("runtimeStatePhysicalAddress=" + Hex(evidence.RuntimeStatePhysicalAddress));
                writer.WriteLine("runtimeStatePodSha256=" + evidence.RuntimeStatePodSha256);
                writer.WriteLine("runtimeStatePodBase64=" + evidence.RuntimeStatePodBase64);
                writer.WriteLine("transitionCanaryPhysicalAddress=" + Hex(evidence.TransitionCanaryPhysicalAddress));
                writer.WriteLine("transitionCanaryValue=" + Hex(evidence.TransitionCanaryValue));
                writer.WriteLine("callGatePhysicalAddress=" + Hex(evidence.CallGatePhysicalAddress));
                writer.WriteLine("callGatePodSha256=" + evidence.CallGatePodSha256);
                writer.WriteLine("callGatePodBase64=" + evidence.CallGatePodBase64);
                writer.WriteLine("firmwareCr3After=" + Hex(evidence.PostReturnCr3));
                writer.WriteLine("rawCr4After=" + Hex(evidence.PostReturnCr4));
                writer.WriteLine("rflagsAfter=" + Hex(evidence.PostReturnRflags));
                writer.WriteLine("runtimeFirmwareCr3Readback=" + Hex(evidence.FirmwareCr3Readback));
                writer.WriteLine("runtimeRestoredCr4Readback=" + Hex(evidence.RestoredCr4Readback));
                writer.WriteLine("runtimeRestoredRflagsIntent=" + Hex(evidence.RestoredRflagsIntent));
                writer.WriteLine("privateSerialSha256=" + privateSerialHash);
                writer.WriteLine("predictedPublicSerialSha256=" + predictedPublicHash);
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
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string Sha256FilePlusBytes(string path, byte[] suffix)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
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
            throw new InvalidOperationException("missing_environment_" + name);
        }

        return value;
    }

    private static int RequiredPositiveInt(string name)
    {
        string value = RequiredEnvironment(name);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0 || parsed > 300)
        {
            throw new InvalidOperationException("invalid_environment_" + name);
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

        throw new InvalidOperationException("invalid_boolean_environment_" + name);
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
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> normalizedPaths = new List<string>();
        foreach (string path in paths)
        {
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!seen.Add(normalized))
            {
                throw new InvalidOperationException("capture_path_overlap");
            }

            normalizedPaths.Add(normalized);
        }

        for (int left = 0; left < normalizedPaths.Count; left++)
        {
            for (int right = left + 1; right < normalizedPaths.Count; right++)
            {
                string leftPrefix = normalizedPaths[left] + Path.DirectorySeparatorChar;
                string rightPrefix = normalizedPaths[right] + Path.DirectorySeparatorChar;
                if (normalizedPaths[right].StartsWith(leftPrefix, StringComparison.OrdinalIgnoreCase) || normalizedPaths[left].StartsWith(rightPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("capture_path_containment");
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
        if (!Directory.Exists(directory) || Directory.GetFileSystemEntries(directory).Length != 0)
        {
            throw new IOException("capture_scratch_not_empty_after_scan");
        }

        Directory.Delete(directory, recursive: false);
        if (Directory.Exists(directory))
        {
            throw new IOException("capture_scratch_delete_failed");
        }
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.' ? character : '_');
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
}
