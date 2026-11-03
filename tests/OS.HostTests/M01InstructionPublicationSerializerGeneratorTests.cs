using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
    private const string AggregateName = "m01.033";
    private const string ToolProjectName = "OS.X64TextSerializerGenerator";
    private const string ObjectFileName = "instruction-publication.obj";
    private const int ExpectedObjectSize = OS.Tooling.EfiContracts.SerializerObjectBytes;
    private static readonly string ExpectedSha256 = OS.Tooling.EfiContracts.SerializerObjectSha256.ToUpperInvariant();
    private const ushort Amd64Machine = 0x8664;
    private const ushort Amd64Addr32Nb = 0x0003;
    private const uint TextCharacteristics = 0x60500020;
    private const uint ReadOnlyDataCharacteristics = 0x40300040;
    private static readonly byte[] ExpectedText = [0x53, 0xB8, 0x00, 0x00, 0x00, 0x00, 0xB9, 0x00, 0x00, 0x00, 0x00, 0x0F, 0xA2, 0xB8, 0x43, 0x4C, 0x52, 0x53, 0x5B, 0xC3];
    private static readonly byte[] ExpectedUnwind = [0x01, 0x01, 0x01, 0x00, 0x01, 0x30, 0x00, 0x00];
    private static readonly byte[] ExpectedStringTable = BuildExpectedStringTable();
    private static string? _boundedProcessHelperPath;
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run(AggregateName, () =>
        {
            try
            {
                Execute();
                Console.WriteLine("CLROS_M01_033_READY bytes=" + ExpectedObjectSize.ToString(CultureInfo.InvariantCulture) + " sha256=" + ExpectedSha256);
            }
            catch (M01TestFailureException ex)
            {
                throw new InvalidOperationException("M01-033 serializer generator tests failed: " + ex.Code, ex);
            }
            catch (Exception ex)
            {
                string code = "unexpected-" + ex.GetType().Name;
                throw new InvalidOperationException("M01-033 serializer generator tests failed: " + code, ex);
            }
        });
    }

    private static void Execute()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "tools", ToolProjectName);
        string projectPath = Path.Combine(projectRoot, ToolProjectName + ".csproj");
        Require(File.Exists(projectPath), "tool-project-missing");
        Require(File.Exists(Path.Combine(projectRoot, "Program.cs")), "tool-program-missing");
        Require(File.Exists(Path.Combine(projectRoot, "Amd64CoffWriter.cs")), "tool-writer-missing");
        Require(File.Exists(Path.Combine(projectRoot, "InstructionPublicationSerializerGenerator.cs")), "tool-generator-missing");
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "clros-m01-033-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            _boundedProcessHelperPath = BuildBoundedProcessHelper(temporaryRoot);
            string checkoutA = CreateCleanToolCheckout(repositoryRoot, temporaryRoot, "checkout-a");
            string checkoutB = CreateCleanToolCheckout(repositoryRoot, temporaryRoot, "checkout-b");
            string toolA = BuildCleanTool(checkoutA, "a");
            string toolB = BuildCleanTool(checkoutB, "b");
            string outputRootA = Path.Combine(temporaryRoot, "output-a");
            string outputRootB = Path.Combine(temporaryRoot, "different", "absolute", "output-b");
            Directory.CreateDirectory(outputRootA);
            Directory.CreateDirectory(outputRootB);
            ProcessResult createdA = RunGenerator(toolA, outputRootA, ObjectFileName, timeoutMilliseconds: 15_000);
            AssertGeneratorSuccess(createdA, "created", "generator-create-a");
            string objectPathA = Path.Combine(outputRootA, ObjectFileName);
            byte[] objectA = ReadExpectedObject(objectPathA, "object-a");
            ParsedCoff parsed = ParseAndValidateExpected(objectA);
            ValidateSerializerAbi(parsed);
            ValidatePublicWriterContract(checkoutA);
            DateTime frozenWriteTime = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(objectPathA, frozenWriteTime);
            ProcessResult unchanged = RunGenerator(toolA, outputRootA, ObjectFileName, timeoutMilliseconds: 15_000);
            AssertGeneratorSuccess(unchanged, "unchanged", "generator-idempotent");
            Require(File.GetLastWriteTimeUtc(objectPathA) == frozenWriteTime, "generator-idempotent-rewrote-file");
            Require(File.ReadAllBytes(objectPathA).AsSpan().SequenceEqual(objectA), "generator-idempotent-bytes-changed");
            Dictionary<string, string?> cultureEnvironment = new(StringComparer.Ordinal)
            {
                ["DOTNET_CLI_UI_LANGUAGE"] = "tr-TR",
                ["LANG"] = "tr_TR.UTF-8",
                ["LC_ALL"] = "tr_TR.UTF-8"
            };
            ProcessResult createdB = RunGenerator(toolB, outputRootB, "INSTRUCTION-PUBLICATION.OBJ", timeoutMilliseconds: 15_000, cultureEnvironment);
            AssertGeneratorSuccess(createdB, "created", "generator-create-b");
            byte[] objectB = ReadExpectedObject(Path.Combine(outputRootB, "INSTRUCTION-PUBLICATION.OBJ"), "object-b");
            Require(objectB.AsSpan().SequenceEqual(objectA), "checkout-path-nondeterministic");
            Require(ComputeSha256(objectB) == ExpectedSha256, "culture-hash-mismatch");
            ValidateCliFailureBoundaries(toolA, temporaryRoot, outputRootA, objectA);
            ValidateIndependentParserMutationFamilies(objectA);
            ValidateBoundedProcessAndPartialObject(temporaryRoot);
        }
        finally
        {
            _boundedProcessHelperPath = null;
            DeleteTemporaryTree(temporaryRoot);
            Require(!Directory.Exists(temporaryRoot), "temporary-cleanup-failed");
        }
    }

    private static string BuildBoundedProcessHelper(string temporaryRoot)
    {
        string helperRoot = Path.Combine(temporaryRoot, "bounded-process-helper");
        Directory.CreateDirectory(helperRoot);
        string projectPath = Path.Combine(helperRoot, "BoundedProcessHelper.csproj");
        string sourcePath = Path.Combine(helperRoot, "Program.cs");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net11.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(sourcePath, """
            using System.Diagnostics;
            using System.Globalization;
            using System.Text;

            try
            {
                return Run(args);
            }
            catch (Exception)
            {
                return 70;
            }

            static int Run(string[] args)
            {
                if (!TryParseArguments(
                    args,
                    out string resultRoot,
                    out int timeoutMilliseconds,
                    out int maximumBytes,
                    out string workingDirectory,
                    out string executable,
                    out string[] childArguments))
                {
                    return 64;
                }

                Directory.CreateDirectory(resultRoot);
                var start = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                };
                foreach (string argument in childArguments)
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = new Process { StartInfo = start };
                var standardOutput = new BoundedCapture(maximumBytes);
                var standardError = new BoundedCapture(maximumBytes);
                int exitCode = -1;
                bool timedOut = false;
                DrainState standardOutputState = DrainState.Faulted;
                DrainState standardErrorState = DrainState.Faulted;
                bool childStarted = false;

                try
                {
                    if (process.Start())
                    {
                        childStarted = true;
                        Task<DrainState> outputDrain =
                            DrainAsync(process.StandardOutput.BaseStream, standardOutput);
                        Task<DrainState> errorDrain =
                            DrainAsync(process.StandardError.BaseStream, standardError);
                        bool exited = process.WaitForExit(timeoutMilliseconds);
                        timedOut = !exited;
                        if (timedOut)
                        {
                            exited = BestEffortTerminateAndReap(
                                process,
                                timeoutMilliseconds: 10_000);
                        }

                        if (exited)
                        {
                            exitCode = process.ExitCode;
                        }

                        bool drained = WaitForDrains(
                            outputDrain,
                            errorDrain,
                            timeoutMilliseconds: 2_000);
                        standardOutputState = CompletedState(outputDrain, drained);
                        standardErrorState = CompletedState(errorDrain, drained);
                    }
                }
                catch (Exception)
                {
                    standardOutputState = DrainState.Faulted;
                    standardErrorState = DrainState.Faulted;
                }
                finally
                {
                    if (childStarted)
                    {
                        _ = BestEffortTerminateAndReap(
                            process,
                            timeoutMilliseconds: 10_000);
                    }
                }

                CapturedBytes output = standardOutput.Snapshot();
                CapturedBytes error = standardError.Snapshot();
                WriteAtomic(
                    Path.Combine(resultRoot, "stdout.bin"),
                    output.Bytes);
                WriteAtomic(
                    Path.Combine(resultRoot, "stderr.bin"),
                    error.Bytes);
                string metadata = string.Join(
                    "\n",
                    "format=1",
                    "exitCode=" + exitCode.ToString(CultureInfo.InvariantCulture),
                    "timedOut=" + timedOut.ToString(CultureInfo.InvariantCulture),
                    "stdoutOverflow=" + output.Overflow.ToString(CultureInfo.InvariantCulture),
                    "stderrOverflow=" + error.Overflow.ToString(CultureInfo.InvariantCulture),
                    "stdoutDrain=" + standardOutputState,
                    "stderrDrain=" + standardErrorState) + "\n";
                WriteAtomic(
                    Path.Combine(resultRoot, "result.txt"),
                    Encoding.ASCII.GetBytes(metadata));
                return 0;
            }

            static bool TryParseArguments(
                string[] values,
                out string resultRoot,
                out int timeoutMilliseconds,
                out int maximumBytes,
                out string workingDirectory,
                out string executable,
                out string[] childArguments)
            {
                resultRoot = string.Empty;
                timeoutMilliseconds = 0;
                maximumBytes = 0;
                workingDirectory = string.Empty;
                executable = string.Empty;
                childArguments = [];
                if (values.Length < 11 ||
                    values[0] != "--result-root" ||
                    values[2] != "--timeout-ms" ||
                    values[4] != "--max-bytes" ||
                    values[6] != "--working-directory" ||
                    values[8] != "--executable" ||
                    values[10] != "--" ||
                    !int.TryParse(
                        values[3],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out timeoutMilliseconds) ||
                    !int.TryParse(
                        values[5],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out maximumBytes) ||
                    timeoutMilliseconds <= 0 ||
                    maximumBytes <= 0 ||
                    maximumBytes > 16 * 1024 * 1024 ||
                    !Path.IsPathFullyQualified(values[1]) ||
                    !Directory.Exists(values[7]) ||
                    string.IsNullOrWhiteSpace(values[9]))
                {
                    return false;
                }

                resultRoot = values[1];
                workingDirectory = values[7];
                executable = values[9];
                childArguments = values[11..];
                return true;
            }

            static async Task<DrainState> DrainAsync(
                Stream stream,
                BoundedCapture capture)
            {
                byte[] buffer = new byte[4096];
                try
                {
                    while (true)
                    {
                        int count = await stream
                            .ReadAsync(buffer.AsMemory(0, buffer.Length))
                            .ConfigureAwait(false);
                        if (count == 0)
                        {
                            return DrainState.EndOfStream;
                        }

                        capture.Append(buffer.AsSpan(0, count));
                    }
                }
                catch (Exception)
                {
                    return DrainState.Faulted;
                }
            }

            static bool WaitForDrains(
                Task<DrainState> standardOutput,
                Task<DrainState> standardError,
                int timeoutMilliseconds)
            {
                try
                {
                    return Task.WaitAll(
                        [standardOutput, standardError],
                        timeoutMilliseconds);
                }
                catch (AggregateException)
                {
                    return standardOutput.IsCompleted &&
                        standardError.IsCompleted;
                }
            }

            static bool BestEffortTerminateAndReap(
                Process process,
                int timeoutMilliseconds)
            {
                bool knownExited;
                try
                {
                    knownExited = process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    knownExited = false;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    knownExited = false;
                }

                if (!knownExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }
                }

                try
                {
                    if (process.WaitForExit(timeoutMilliseconds))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }

                try
                {
                    return process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return false;
                }
            }

            static DrainState CompletedState(
                Task<DrainState> drain,
                bool bothDrained)
            {
                if (drain.IsCompletedSuccessfully)
                {
                    return drain.Result;
                }

                _ = bothDrained;
                return DrainState.Incomplete;
            }

            static void WriteAtomic(string path, byte[] bytes)
            {
                string temporaryPath = path + ".tmp";
                File.WriteAllBytes(temporaryPath, bytes);
                File.Move(temporaryPath, path, overwrite: true);
            }

            enum DrainState
            {
                EndOfStream,
                Faulted,
                Incomplete
            }

            readonly record struct CapturedBytes(byte[] Bytes, bool Overflow);

            sealed class BoundedCapture
            {
                private readonly object _gate = new();
                private readonly byte[] _retained;
                private int _count;
                private bool _overflow;

                public BoundedCapture(int maximumBytes)
                {
                    _retained = new byte[maximumBytes];
                }

                public void Append(ReadOnlySpan<byte> bytes)
                {
                    lock (_gate)
                    {
                        int remaining = _retained.Length - _count;
                        int copyCount = Math.Min(remaining, bytes.Length);
                        if (copyCount > 0)
                        {
                            bytes[..copyCount].CopyTo(
                                _retained.AsSpan(_count, copyCount));
                            _count += copyCount;
                        }

                        if (copyCount != bytes.Length)
                        {
                            _overflow = true;
                        }
                    }
                }

                public CapturedBytes Snapshot()
                {
                    lock (_gate)
                    {
                        return new CapturedBytes(
                            _retained.AsSpan(0, _count).ToArray(),
                            _overflow);
                    }
                }
            }
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ProcessStartInfo build = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
            WorkingDirectory = helperRoot
        };
        build.ArgumentList.Add("build");
        build.ArgumentList.Add(projectPath);
        build.ArgumentList.Add("--nologo");
        build.ArgumentList.Add("--configuration");
        build.ArgumentList.Add("Release");
        build.ArgumentList.Add("--no-incremental");
        build.ArgumentList.Add("--disable-build-servers");
        build.ArgumentList.Add("-noconsolelogger");
        build.ArgumentList.Add("-m:1");
        build.ArgumentList.Add("-nodeReuse:false");
        build.ArgumentList.Add("-p:UseSharedCompilation=false");
        build.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        build.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        build.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        build.Environment["DOTNET_NOLOGO"] = "1";
        using Process process = new Process
        {
            StartInfo = build
        };
        bool processStarted = false;
        try
        {
            Require(process.Start(), "process-helper-build-start-failed");
            processStarted = true;
            bool exited = process.WaitForExit(120_000);
            if (!exited)
            {
                Require(BestEffortTerminateAndReap(process, 10_000), "process-helper-build-kill-timeout");
            }

            Require(exited, "process-helper-build-timeout");
            Require(process.ExitCode == 0, "process-helper-build-failed");
        }
        finally
        {
            if (processStarted)
            {
                _ = BestEffortTerminateAndReap(process, 10_000);
            }
        }

        string helperPath = Path.Combine(helperRoot, "bin", "Release", "net11.0", OperatingSystem.IsWindows() ? "BoundedProcessHelper.exe" : "BoundedProcessHelper");
        Require(File.Exists(helperPath), "process-helper-build-output-missing");
        return helperPath;
    }

    private static bool BestEffortTerminateAndReap(Process process, int timeoutMilliseconds)
    {
        bool knownExited;
        try
        {
            knownExited = process.HasExited;
        }
        catch (InvalidOperationException)
        {
            knownExited = false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            knownExited = false;
        }

        if (!knownExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        try
        {
            if (process.WaitForExit(timeoutMilliseconds))
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string CreateCleanToolCheckout(string repositoryRoot, string temporaryRoot, string checkoutName)
    {
        string checkoutRoot = Path.Combine(temporaryRoot, checkoutName);
        string toolsRoot = Path.Combine(checkoutRoot, "tools");
        string destinationProjectRoot = Path.Combine(toolsRoot, ToolProjectName);
        string sourceProjectRoot = Path.Combine(repositoryRoot, "tools", ToolProjectName);
        Directory.CreateDirectory(destinationProjectRoot);
        CopyFile(repositoryRoot, checkoutRoot, "global.json");
        CopyFile(repositoryRoot, checkoutRoot, "Directory.Build.props");
        CopyFile(Path.Combine(repositoryRoot, "tools"), toolsRoot, "Directory.Build.props");
        string[] sourceFiles = [ToolProjectName + ".csproj", "Program.cs", "Amd64CoffWriter.cs", "CheckedReferenceBarrierThunkGenerator.cs", "InterfaceDispatchThunkGenerator.cs", "ExceptionHandlingThunkGenerator.cs", "ExceptionFilterThunkGenerator.cs", "NormalFinallyThunkGenerator.cs", "EhContributionCatalogGenerator.cs", "InstructionPublicationSerializerGenerator.cs"];
        foreach (string sourceFile in sourceFiles)
        {
            string source = Path.Combine(sourceProjectRoot, sourceFile);
            string destination = Path.Combine(destinationProjectRoot, sourceFile);
            Require(File.Exists(source), "clean-checkout-source-missing");
            File.Copy(source, destination, overwrite: false);
        }

        Require(!Directory.Exists(Path.Combine(destinationProjectRoot, "bin")) && !Directory.Exists(Path.Combine(destinationProjectRoot, "obj")), "clean-checkout-contained-build-output");
        return checkoutRoot;
    }

    private static void CopyFile(string sourceRoot, string destinationRoot, string relativePath)
    {
        string source = Path.Combine(sourceRoot, relativePath);
        string destination = Path.Combine(destinationRoot, relativePath);
        Require(File.Exists(source), "clean-checkout-support-file-missing");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
    }

    private static string BuildCleanTool(string checkoutRoot, string suffix)
    {
        string projectPath = Path.Combine(checkoutRoot, "tools", ToolProjectName, ToolProjectName + ".csproj");
        ProcessResult result = RunCleanBuild(checkoutRoot, projectPath, "tool-" + suffix, timeoutMilliseconds: 120_000);
        Require(!result.TimedOut, "tool-build-" + suffix + "-timeout");
        Require(!result.StandardOutputOverflow && !result.StandardErrorOverflow, "tool-build-" + suffix + "-output-overflow");
        Require(!result.StandardOutputIncomplete && !result.StandardErrorIncomplete, "tool-build-" + suffix + "-output-incomplete");
        Require(result.ExitCode == 0, "tool-build-" + suffix + "-failed");
        string toolPath = Path.Combine(checkoutRoot, "tools", ToolProjectName, "bin", "Release", "net11.0", OperatingSystem.IsWindows() ? ToolProjectName + ".exe" : ToolProjectName);
        Require(File.Exists(toolPath), "tool-build-" + suffix + "-output-missing");
        return toolPath;
    }

    private static ProcessResult RunGenerator(string toolPath, string outputRoot, string outputFile, int timeoutMilliseconds, IReadOnlyDictionary<string, string?>? environment = null)
    {
        ProcessStartInfo start = NewExecutableStartInfo(toolPath, Path.GetDirectoryName(toolPath)!);
        start.ArgumentList.Add("--output-root");
        start.ArgumentList.Add(outputRoot);
        start.ArgumentList.Add("--output-file");
        start.ArgumentList.Add(outputFile);
        AddEnvironment(start, environment);
        return RunBounded(start, timeoutMilliseconds);
    }

    private static ProcessResult RunGeneratorArguments(string toolPath, int timeoutMilliseconds, params string[] arguments)
    {
        ProcessStartInfo start = NewExecutableStartInfo(toolPath, Path.GetDirectoryName(toolPath)!);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return RunBounded(start, timeoutMilliseconds);
    }

    private static ProcessStartInfo NewDotnetStartInfo(string workingDirectory)
    {
        return new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true
        };
    }

    private static ProcessStartInfo NewExecutableStartInfo(string executable, string workingDirectory)
    {
        return new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true
        };
    }

    private static void AddEnvironment(ProcessStartInfo start, IReadOnlyDictionary<string, string?>? environment)
    {
        if (environment is null)
        {
            return;
        }

        foreach ((string key, string? value)in environment)
        {
            start.Environment[key] = value;
        }
    }

    private static void AssertGeneratorSuccess(ProcessResult result, string expectedState, string failureCode)
    {
        Require(!result.TimedOut, failureCode + "-timeout");
        Require(!result.StandardOutputOverflow && !result.StandardErrorOverflow, failureCode + "-output-overflow");
        Require(!result.StandardOutputIncomplete, failureCode + "-stdout-incomplete-" + result.StandardOutputDrainResult);
        Require(!result.StandardErrorIncomplete, failureCode + "-stderr-incomplete-" + result.StandardErrorDrainResult);
        Require(result.ExitCode == 0, failureCode + "-exit");
        Require(string.IsNullOrWhiteSpace(result.StandardError), failureCode + "-stderr");
        string[] lines = NonEmptyLines(result.StandardOutput);
        string ready = "X64_SERIALIZER_OBJECT_READY bytes=" + ExpectedObjectSize.ToString(CultureInfo.InvariantCulture) + " sha256=" + ExpectedSha256 + " state=" + expectedState;
        string[] expected = ["TEST_START x64.serializer.generate", ready, "TEST_PASS x64.serializer.generate"];
        Require(lines.SequenceEqual(expected, StringComparer.Ordinal), failureCode + "-markers");
    }

    private static byte[] ReadExpectedObject(string path, string failurePrefix)
    {
        Require(File.Exists(path), failurePrefix + "-missing");
        byte[] bytes = File.ReadAllBytes(path);
        Require(bytes.Length == ExpectedObjectSize, failurePrefix + "-size");
        Require(ComputeSha256(bytes) == ExpectedSha256, failurePrefix + "-hash");
        return bytes;
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void ValidateCliFailureBoundaries(string toolPath, string temporaryRoot, string validOutputRoot, byte[] validObject)
    {
        ProcessResult missingA = RunGeneratorArguments(toolPath, 15_000);
        ProcessResult missingB = RunGeneratorArguments(toolPath, 15_000);
        AssertStableGeneratorFailure(missingA, missingB, 2, "arguments_missing", "cli-default");
        AssertGeneratorFailure(RunGeneratorArguments(toolPath, 15_000, "--output-root", " ", "--output-file", ObjectFileName), 2, "output_root_invalid", "cli-empty-root");
        AssertGeneratorFailure(RunGeneratorArguments(toolPath, 15_000, "--output-root", validOutputRoot, "--output-file", " "), 2, "output_file_invalid", "cli-empty-file");
        AssertGeneratorFailure(RunGeneratorArguments(toolPath, 15_000, "--output-root", validOutputRoot, "--output-root", validOutputRoot, "--output-file", ObjectFileName), 2, "argument_duplicate", "cli-duplicate-option");
        AssertGeneratorFailure(RunGeneratorArguments(toolPath, 15_000, "--unknown", "value", "--output-root", validOutputRoot, "--output-file", ObjectFileName), 2, "argument_unknown", "cli-unknown-option");
        AssertGeneratorFailure(RunGeneratorArguments(toolPath, 15_000, "--output-root", "relative-root", "--output-file", ObjectFileName), 2, "output_root_invalid", "cli-relative-root");
        string absentRoot = Path.Combine(temporaryRoot, "absent-output-root");
        AssertGeneratorFailure(RunGeneratorArguments(toolPath, 15_000, "--output-root", absentRoot, "--output-file", ObjectFileName), 2, "output_root_invalid", "cli-absent-root");
        Require(!Directory.Exists(absentRoot), "cli-absent-root-created");
        string[] unsafeNames = [".." + Path.DirectorySeparatorChar + "escape.obj", "sub" + Path.DirectorySeparatorChar + "escape.obj", Path.Combine(validOutputRoot, "rooted.obj"), "wrong.bin", "CON.obj", "bad:stream.obj", "trailing.obj ", ".obj", ".", ".."];
        for (int unsafeIndex = 0; unsafeIndex < unsafeNames.Length; unsafeIndex++)
        {
            string unsafeName = unsafeNames[unsafeIndex];
            ProcessResult failure = RunGeneratorArguments(toolPath, 15_000, "--output-root", validOutputRoot, "--output-file", unsafeName);
            AssertGeneratorFailure(failure, 2, "output_file_invalid", "cli-unsafe-name-" + unsafeIndex.ToString(CultureInfo.InvariantCulture));
        }

        string escapePath = Path.Combine(Path.GetDirectoryName(validOutputRoot)!, "escape.obj");
        Require(!File.Exists(escapePath), "cli-path-traversal-created-output");
        string staleName = "stale.obj";
        string stalePath = Path.Combine(validOutputRoot, staleName);
        byte[] staleBytes = (byte[])validObject.Clone();
        staleBytes[140] ^= 0x01;
        File.WriteAllBytes(stalePath, staleBytes);
        ProcessResult staleA = RunGenerator(toolPath, validOutputRoot, staleName, 15_000);
        ProcessResult staleB = RunGenerator(toolPath, validOutputRoot, staleName, 15_000);
        AssertStableGeneratorFailure(staleA, staleB, 1, "existing_output_mismatch", "cli-stale-output");
        Require(File.ReadAllBytes(stalePath).AsSpan().SequenceEqual(staleBytes), "cli-stale-output-overwritten");
        string blockedName = "blocked.obj";
        string blockedPath = Path.Combine(validOutputRoot, blockedName);
        Directory.CreateDirectory(blockedPath);
        ProcessResult blocked = RunGenerator(toolPath, validOutputRoot, blockedName, 15_000);
        AssertGeneratorFailure(blocked, 1, "io_failure", "cli-blocked-destination");
        Require(Directory.Exists(blockedPath), "cli-blocked-directory-removed");
        Require(!File.Exists(blockedPath), "cli-blocked-destination-became-file");
        Require(Directory.GetFiles(validOutputRoot, ".clros-x64-*.tmp", SearchOption.TopDirectoryOnly).Length == 0, "cli-blocked-destination-left-temporary-file");
        string[] filesAfterFailure = Directory.GetFiles(validOutputRoot, "*", SearchOption.TopDirectoryOnly);
        Require(filesAfterFailure.All(path => string.Equals(Path.GetFileName(path), ObjectFileName, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFileName(path), staleName, StringComparison.OrdinalIgnoreCase)), "cli-failure-left-temporary-file");
    }

    private static void ValidatePublicWriterContract(string checkoutRoot)
    {
        string probeRoot = Path.Combine(checkoutRoot, "writer-contract-probe");
        Directory.CreateDirectory(probeRoot);
        string projectPath = Path.Combine(probeRoot, "WriterContractProbe.csproj");
        string programPath = Path.Combine(probeRoot, "Program.cs");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net11.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.csproj" />
              </ItemGroup>
            </Project>
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(programPath, """
            using System.Security.Cryptography;
            using OS.X64TextSerializerGenerator;

            const string ExpectedHash =
                "__SERIALIZER_OBJECT_SHA256__";
            const int ExpectedSize = __SERIALIZER_OBJECT_BYTES__;
            const string ExpectedSymbol = "__SERIALIZER_SYMBOL__";

            var failures = new List<string>();
            Amd64CoffObjectDefinition baseline = CreateBaseline();

            ExpectWriterError(
                "default-definition",
                default,
                CoffWriteError.UnsupportedMachine);
            ExpectWriterError(
                "unsupported-machine",
                baseline with { Machine = 0x014C },
                CoffWriteError.UnsupportedMachine);
            ExpectWriterError(
                "nonzero-timestamp",
                baseline with { TimeDateStamp = 1 },
                CoffWriteError.NonDeterministicTimestamp);
            ExpectWriterError(
                "null-sections",
                baseline with { Sections = null },
                CoffWriteError.InvalidSectionCount);
            ExpectWriterError(
                "empty-sections",
                baseline with { Sections = [] },
                CoffWriteError.InvalidSectionCount);
            ExpectWriterError(
                "null-symbols",
                baseline with { Symbols = null },
                CoffWriteError.InvalidSymbolCount);
            ExpectWriterError(
                "empty-symbols",
                baseline with { Symbols = [] },
                CoffWriteError.InvalidSymbolCount);

            CoffSectionDefinition[] duplicateSections = CloneSections(baseline);
            duplicateSections[1] = duplicateSections[1] with
            {
                Name = duplicateSections[0].Name
            };
            ExpectWriterError(
                "duplicate-section",
                baseline with { Sections = duplicateSections },
                CoffWriteError.DuplicateSection);

            CoffSectionDefinition[] emptyNameSections = CloneSections(baseline);
            emptyNameSections[0] = emptyNameSections[0] with { Name = "" };
            ExpectWriterError(
                "empty-section-name",
                baseline with { Sections = emptyNameSections },
                CoffWriteError.InvalidName);

            CoffSectionDefinition[] malformedNameSections = CloneSections(baseline);
            malformedNameSections[0] = malformedNameSections[0] with { Name = "/4" };
            ExpectWriterError(
                "malformed-section-name",
                baseline with { Sections = malformedNameSections },
                CoffWriteError.InvalidName);

            CoffSectionDefinition[] nullRawDataSections = CloneSections(baseline);
            nullRawDataSections[0] = nullRawDataSections[0] with { RawData = null };
            ExpectWriterError(
                "null-section-raw-data",
                baseline with { Sections = nullRawDataSections },
                CoffWriteError.EmptySection);

            CoffSectionDefinition[] emptyRawDataSections = CloneSections(baseline);
            emptyRawDataSections[0] = emptyRawDataSections[0] with { RawData = [] };
            ExpectWriterError(
                "empty-section-raw-data",
                baseline with { Sections = emptyRawDataSections },
                CoffWriteError.EmptySection);

            CoffSectionDefinition[] nullRelocationsSections = CloneSections(baseline);
            nullRelocationsSections[0] = nullRelocationsSections[0] with
            {
                Relocations = null
            };
            ExpectWriterError(
                "null-section-relocations",
                baseline with { Sections = nullRelocationsSections },
                CoffWriteError.InvalidConfiguration);

            ExpectSectionCharacteristicsError("zero-characteristics", baseline, 0, 0);
            ExpectSectionCharacteristicsError(
                "writable-executable",
                baseline,
                0,
                0xE0500020);
            ExpectSectionCharacteristicsError(
                "missing-read",
                baseline,
                0,
                0x20500020);
            ExpectSectionCharacteristicsError(
                "zero-alignment",
                baseline,
                0,
                0x60000020);
            ExpectSectionCharacteristicsError(
                "code-without-execute",
                baseline,
                0,
                0x40500020);
            ExpectSectionCharacteristicsError(
                "data-with-execute",
                baseline,
                1,
                0x60300040);
            ExpectSectionCharacteristicsError(
                "unknown-characteristic",
                baseline,
                1,
                0x40300041);

            CoffSymbolDefinition[] duplicateSymbols = CloneSymbols(baseline);
            duplicateSymbols[1] = duplicateSymbols[1] with
            {
                Name = duplicateSymbols[0].Name
            };
            ExpectWriterError(
                "duplicate-symbol",
                baseline with { Symbols = duplicateSymbols },
                CoffWriteError.DuplicateSymbol);

            CoffSymbolDefinition[] invalidSymbols = CloneSymbols(baseline);
            invalidSymbols[0] = invalidSymbols[0] with { SectionNumber = 0 };
            ExpectWriterError(
                "invalid-symbol-section",
                baseline with { Symbols = invalidSymbols },
                CoffWriteError.InvalidSymbol);

            CoffSymbolDefinition[] invalidStorageSymbols = CloneSymbols(baseline);
            invalidStorageSymbols[0] = invalidStorageSymbols[0] with { StorageClass = 0 };
            ExpectWriterError(
                "invalid-symbol-storage",
                baseline with { Symbols = invalidStorageSymbols },
                CoffWriteError.InvalidSymbol);

            CoffSymbolDefinition[] emptyNameSymbols = CloneSymbols(baseline);
            emptyNameSymbols[0] = emptyNameSymbols[0] with { Name = "" };
            ExpectWriterError(
                "empty-symbol-name",
                baseline with { Symbols = emptyNameSymbols },
                CoffWriteError.InvalidName);

            CoffSymbolDefinition[] malformedNameSymbols = CloneSymbols(baseline);
            malformedNameSymbols[0] = malformedNameSymbols[0] with
            {
                Name = "bad symbol"
            };
            ExpectWriterError(
                "malformed-symbol-name",
                baseline with { Symbols = malformedNameSymbols },
                CoffWriteError.InvalidName);

            ExpectRelocationError(
                "wrong-relocation-type",
                baseline,
                new CoffRelocationDefinition(0, 0, 0));
            ExpectRelocationError(
                "relocation-symbol-out-of-range",
                baseline,
                new CoffRelocationDefinition(0, 2, 0x0003));
            ExpectRelocationError(
                "relocation-offset-out-of-range",
                baseline,
                new CoffRelocationDefinition(12, 0, 0x0003));

            CoffSectionDefinition[] duplicateRelocationSections = CloneSections(baseline);
            duplicateRelocationSections[2] = duplicateRelocationSections[2] with
            {
                Relocations =
                [
                    new CoffRelocationDefinition(0, 0, 0x0003),
                    new CoffRelocationDefinition(0, 1, 0x0003)
                ]
            };
            ExpectWriterError(
                "duplicate-relocation-offset",
                baseline with { Sections = duplicateRelocationSections },
                CoffWriteError.InvalidRelocation);

            if (!Amd64CoffWriter.TryGetRequiredSize(
                    in baseline,
                    out int requiredSize,
                    out CoffWriteError sizeError) ||
                requiredSize != ExpectedSize ||
                sizeError != CoffWriteError.None)
            {
                failures.Add("required-size");
            }
            else
            {
                byte[] oneShort = Enumerable.Repeat((byte)0xA5, requiredSize - 1).ToArray();
                if (Amd64CoffWriter.TryWrite(
                        in baseline,
                        oneShort,
                        out int shortWritten,
                        out CoffWriteError shortError) ||
                    shortWritten != 0 ||
                    shortError != CoffWriteError.CapacityExceeded ||
                    oneShort.Any(value => value != 0xA5))
                {
                    failures.Add("writer-one-short-sentinel");
                }

                byte[] exact = Enumerable.Repeat((byte)0xA5, requiredSize).ToArray();
                if (!Amd64CoffWriter.TryWrite(
                        in baseline,
                        exact,
                        out int exactWritten,
                        out CoffWriteError exactError) ||
                    exactWritten != requiredSize ||
                    exactError != CoffWriteError.None ||
                    Convert.ToHexString(SHA256.HashData(exact)) != ExpectedHash)
                {
                    failures.Add("writer-exact-size");
                }
            }

            byte[] generatorShort = Enumerable.Repeat((byte)0xA5, 335).ToArray();
            if (InstructionPublicationSerializerGenerator.TryGenerate(
                    generatorShort,
                    out int generatorShortWritten,
                    out CoffWriteError generatorShortError) ||
                generatorShortWritten != 0 ||
                generatorShortError != CoffWriteError.CapacityExceeded ||
                generatorShort.Any(value => value != 0xA5))
            {
                failures.Add("generator-span-one-short");
            }

            foreach (int maximum in new[] { 0, 335 })
            {
                if (InstructionPublicationSerializerGenerator.TryGenerate(
                        maximum,
                        out byte[] rejected,
                        out CoffWriteError error) ||
                    rejected.Length != 0 ||
                    error != CoffWriteError.CapacityExceeded)
                {
                    failures.Add("generator-maximum-" + maximum);
                }
            }

            byte[] generatorExact = Enumerable.Repeat((byte)0xA5, ExpectedSize).ToArray();
            if (!InstructionPublicationSerializerGenerator.TryGenerate(
                    generatorExact,
                    out int generated,
                    out CoffWriteError generatorError) ||
                generated != ExpectedSize ||
                generatorError != CoffWriteError.None ||
                Convert.ToHexString(SHA256.HashData(generatorExact)) != ExpectedHash)
            {
                failures.Add("generator-exact-size");
            }

            if (failures.Count != 0)
            {
                Console.Error.WriteLine("WRITER_CONTRACT_PROBE_FAIL " + string.Join(",", failures));
                return 1;
            }

            Console.WriteLine("WRITER_CONTRACT_PROBE_PASS");
            return 0;

            void ExpectSectionCharacteristicsError(
                string name,
                Amd64CoffObjectDefinition definition,
                int sectionIndex,
                uint characteristics)
            {
                CoffSectionDefinition[] sections = CloneSections(definition);
                sections[sectionIndex] = sections[sectionIndex] with
                {
                    Characteristics = characteristics
                };
                ExpectWriterError(
                    name,
                    definition with { Sections = sections },
                    CoffWriteError.InvalidSectionCharacteristics);
            }

            void ExpectRelocationError(
                string name,
                Amd64CoffObjectDefinition definition,
                CoffRelocationDefinition relocation)
            {
                CoffSectionDefinition[] sections = CloneSections(definition);
                sections[2] = sections[2] with { Relocations = [relocation] };
                ExpectWriterError(
                    name,
                    definition with { Sections = sections },
                    CoffWriteError.InvalidRelocation);
            }

            void ExpectWriterError(
                string name,
                Amd64CoffObjectDefinition definition,
                CoffWriteError expected)
            {
                if (Amd64CoffWriter.TryGetRequiredSize(
                        in definition,
                        out int requiredSize,
                        out CoffWriteError error) ||
                    requiredSize != 0 ||
                    error != expected)
                {
                    failures.Add(name);
                }
            }

            static CoffSectionDefinition[] CloneSections(
                Amd64CoffObjectDefinition definition) =>
                definition.Sections!.ToArray();

            static CoffSymbolDefinition[] CloneSymbols(
                Amd64CoffObjectDefinition definition) =>
                definition.Symbols!.ToArray();

            static Amd64CoffObjectDefinition CreateBaseline()
            {
                CoffSectionDefinition[] sections =
                [
                    new(
                        ".text$clrossm",
                        [
                            0x53,
                            0xB8, 0, 0, 0, 0,
                            0xB9, 0, 0, 0, 0,
                            0x0F, 0xA2,
                            0xB8, 0x43, 0x4C, 0x52, 0x53,
                            0x5B,
                            0xC3
                        ],
                        0x60500020,
                        []),
                    new(
                        ".xdata$clrossm",
                        [0x01, 0x01, 0x01, 0x00, 0x01, 0x30, 0x00, 0x00],
                        0x40300040,
                        []),
                    new(
                        ".pdata$clrossm",
                        [0, 0, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0],
                        0x40300040,
                        [
                            new CoffRelocationDefinition(0, 0, 0x0003),
                            new CoffRelocationDefinition(4, 0, 0x0003),
                            new CoffRelocationDefinition(8, 1, 0x0003)
                        ])
                ];
                CoffSymbolDefinition[] symbols =
                [
                    new(
                        ExpectedSymbol,
                        0,
                        1,
                        0x20,
                        0x02),
                    new(".xdata$clrossm", 0, 2, 0, 0x03)
                ];
                return new(0x8664, 0, sections, symbols);
            }
            """.Replace("__SERIALIZER_OBJECT_SHA256__", ExpectedSha256, StringComparison.Ordinal).Replace("__SERIALIZER_OBJECT_BYTES__", ExpectedObjectSize.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("__SERIALIZER_SYMBOL__", OS.Tooling.EfiContracts.SerializerSymbol, StringComparison.Ordinal), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ProcessResult buildResult = RunCleanBuild(checkoutRoot, projectPath, "writer-probe", timeoutMilliseconds: 120_000);
        Require(!buildResult.TimedOut, "writer-probe-build-timeout");
        Require(!buildResult.StandardOutputOverflow && !buildResult.StandardErrorOverflow, "writer-probe-build-output-overflow");
        Require(!buildResult.StandardOutputIncomplete && !buildResult.StandardErrorIncomplete, "writer-probe-build-output-incomplete");
        Require(buildResult.ExitCode == 0, "writer-probe-build-failed");
        string probeExecutable = Path.Combine(probeRoot, "bin", "Release", "net11.0", OperatingSystem.IsWindows() ? "WriterContractProbe.exe" : "WriterContractProbe");
        Require(File.Exists(probeExecutable), "writer-probe-output-missing");
        ProcessStartInfo run = NewExecutableStartInfo(probeExecutable, checkoutRoot);
        ProcessResult runResult = RunBounded(run, 15_000);
        Require(!runResult.TimedOut, "writer-probe-run-timeout");
        Require(!runResult.StandardOutputOverflow && !runResult.StandardErrorOverflow, "writer-probe-run-output-overflow");
        Require(!runResult.StandardOutputIncomplete && !runResult.StandardErrorIncomplete, "writer-probe-run-output-incomplete");
        Require(runResult.ExitCode == 0, "writer-probe-run-failed");
        Require(NonEmptyLines(runResult.StandardOutput).SequenceEqual(["WRITER_CONTRACT_PROBE_PASS"], StringComparer.Ordinal), "writer-probe-pass-marker");
        Require(string.IsNullOrWhiteSpace(runResult.StandardError), "writer-probe-stderr");
    }

    private static ProcessResult RunCleanBuild(string checkoutRoot, string projectPath, string logName, int timeoutMilliseconds)
    {
        _ = logName;
        ProcessStartInfo start = NewDotnetStartInfo(checkoutRoot);
        start.ArgumentList.Add("build");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("--configuration");
        start.ArgumentList.Add("Release");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("-nodeReuse:false");
        start.ArgumentList.Add("-p:UseSharedCompilation=false");
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        return RunBounded(start, timeoutMilliseconds);
    }

    private static void AssertStableGeneratorFailure(ProcessResult first, ProcessResult second, int expectedExitCode, string expectedDiagnosticCode, string failureCode)
    {
        AssertGeneratorFailure(first, expectedExitCode, expectedDiagnosticCode, failureCode + "-first");
        AssertGeneratorFailure(second, expectedExitCode, expectedDiagnosticCode, failureCode + "-second");
        Require(string.Equals(first.StandardError, second.StandardError, StringComparison.Ordinal), failureCode + "-diagnostic-unstable");
        Require(string.Equals(first.StandardOutput, second.StandardOutput, StringComparison.Ordinal), failureCode + "-stdout-unstable");
    }

    private static void AssertGeneratorFailure(ProcessResult result, int expectedExitCode, string expectedDiagnosticCode, string failureCode)
    {
        Require(!result.TimedOut, failureCode + "-timeout");
        Require(!result.StandardOutputOverflow && !result.StandardErrorOverflow, failureCode + "-output-overflow");
        Require(!result.StandardOutputIncomplete && !result.StandardErrorIncomplete, failureCode + "-output-incomplete");
        Require(result.ExitCode == expectedExitCode, failureCode + "-exit");
        Require(TryGetSingleOutputLine(result.StandardOutput, out string actualOutputLine) && actualOutputLine == "TEST_START x64.serializer.generate", failureCode + "-stdout");
        Require(!result.StandardOutput.Contains("X64_SERIALIZER_OBJECT_READY", StringComparison.Ordinal) && !result.StandardOutput.Contains("TEST_PASS x64.serializer.generate", StringComparison.Ordinal), failureCode + "-accepted-output");
        string expectedMarker = "TEST_FAIL x64.serializer.generate " + expectedDiagnosticCode;
        Require(TryGetSingleOutputLine(result.StandardError, out string actualErrorLine), failureCode + "-diagnostic-count");
        Require(string.Equals(actualErrorLine, expectedMarker, StringComparison.Ordinal), failureCode + "-diagnostic-marker");
        Require(actualErrorLine.All(character => character >= 0x20 && character <= 0x7E), failureCode + "-diagnostic-not-ascii");
    }

    private static bool TryGetSingleOutputLine(string text, out string line)
    {
        line = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.EndsWith('\n'))
        {
            normalized = normalized[..^1];
        }

        if (normalized.Length == 0 || normalized.Contains('\n'))
        {
            return false;
        }

        line = normalized;
        return true;
    }

    private static string[] NonEmptyLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static ParsedCoff ParseAndValidateExpected(byte[] bytes)
    {
        ParsedCoff coff = IndependentCoffParser.Parse(bytes);
        Require(bytes.Length == ExpectedObjectSize, "coff-size");
        Require(coff.Machine == Amd64Machine, "coff-machine");
        Require(coff.SectionCount == 3, "coff-section-count");
        Require(coff.TimeDateStamp == 0, "coff-timestamp");
        Require(coff.SymbolTablePointer == 210, "coff-symbol-table-pointer");
        Require(coff.SymbolCount == 2, "coff-symbol-count");
        Require(coff.OptionalHeaderSize == 0, "coff-optional-header");
        Require(coff.Characteristics == 0, "coff-characteristics");
        Require(coff.Sections.Count == 3, "coff-parsed-section-count");
        Require(coff.Symbols.Count == 2, "coff-parsed-symbol-count");
        Require(bytes.AsSpan(20, 8).SequenceEqual(new byte[] { (byte)'/', (byte)'4', 0, 0, 0, 0, 0, 0 }), "text-raw-name");
        Require(bytes.AsSpan(60, 8).SequenceEqual(new byte[] { (byte)'/', (byte)'1', (byte)'8', 0, 0, 0, 0, 0 }), "xdata-raw-name");
        Require(bytes.AsSpan(100, 8).SequenceEqual(new byte[] { (byte)'/', (byte)'3', (byte)'3', 0, 0, 0, 0, 0 }), "pdata-raw-name");
        Require(coff.StringTable.AsSpan().SequenceEqual(ExpectedStringTable), "coff-string-table");
        ValidateSection(coff.Sections[0], ".text$clrossm", rawSize: 20, rawPointer: 140, relocationPointer: 0, relocationCount: 0, TextCharacteristics, "text");
        ValidateSection(coff.Sections[1], ".xdata$clrossm", rawSize: 8, rawPointer: 160, relocationPointer: 0, relocationCount: 0, ReadOnlyDataCharacteristics, "xdata");
        ValidateSection(coff.Sections[2], ".pdata$clrossm", rawSize: 12, rawPointer: 168, relocationPointer: 180, relocationCount: 3, ReadOnlyDataCharacteristics, "pdata");
        Require(coff.Sections[0].RawData.AsSpan().SequenceEqual(ExpectedText), "text-bytes");
        Require(coff.Sections[1].RawData.AsSpan().SequenceEqual(ExpectedUnwind), "unwind-bytes");
        byte[] pdata = coff.Sections[2].RawData;
        Require(ReadUInt32(pdata, 0) == 0, "pdata-begin-addend");
        Require(ReadUInt32(pdata, 4) == 20, "pdata-end-addend");
        Require(ReadUInt32(pdata, 8) == 0, "pdata-unwind-addend");
        Require(coff.Sections[0].Relocations.Count == 0, "text-relocations");
        Require(coff.Sections[1].Relocations.Count == 0, "xdata-relocations");
        IReadOnlyList<ParsedRelocation> relocations = coff.Sections[2].Relocations;
        Require(relocations.Count == 3, "pdata-relocation-count");
        ValidateRelocation(relocations[0], 0, 0, "pdata-begin-relocation");
        ValidateRelocation(relocations[1], 4, 0, "pdata-end-relocation");
        ValidateRelocation(relocations[2], 8, 1, "pdata-unwind-relocation");
        ParsedSymbol exported = coff.Symbols[0];
        Require(exported.Name == OS.Tooling.EfiContracts.SerializerSymbol, "external-symbol-name");
        Require(exported.RawNameStringOffset == 48, "external-symbol-name-offset");
        Require(exported.Value == 0, "external-symbol-value");
        Require(exported.SectionNumber == 1, "external-symbol-section");
        Require(exported.Type == 0x20, "external-symbol-type");
        Require(exported.StorageClass == 2, "external-symbol-storage");
        Require(exported.AuxiliaryCount == 0, "external-symbol-aux");
        ParsedSymbol xdataBase = coff.Symbols[1];
        Require(xdataBase.Name == ".xdata$clrossm", "xdata-symbol-name");
        Require(xdataBase.RawNameStringOffset == 18, "xdata-symbol-name-offset");
        Require(xdataBase.Value == 0, "xdata-symbol-value");
        Require(xdataBase.SectionNumber == 2, "xdata-symbol-section");
        Require(xdataBase.Type == 0, "xdata-symbol-type");
        Require(xdataBase.StorageClass == 3, "xdata-symbol-storage");
        Require(xdataBase.AuxiliaryCount == 0, "xdata-symbol-aux");
        Require(coff.Symbols.Count(symbol => symbol.StorageClass == 2) == 1, "external-symbol-count");
        Require(coff.Sections.All(section => section.LineNumberCount == 0), "coff-line-numbers-present");
        Require(coff.Sections.All(section => section.LineNumberPointer == 0), "coff-line-number-pointer");
        return coff;
    }

    private static void ValidateSection(ParsedSection section, string expectedName, uint rawSize, uint rawPointer, uint relocationPointer, ushort relocationCount, uint characteristics, string failurePrefix)
    {
        Require(section.Name == expectedName, failurePrefix + "-name");
        Require(section.VirtualSize == 0, failurePrefix + "-virtual-size");
        Require(section.VirtualAddress == 0, failurePrefix + "-virtual-address");
        Require(section.RawSize == rawSize, failurePrefix + "-raw-size");
        Require(section.RawPointer == rawPointer, failurePrefix + "-raw-pointer");
        Require(section.RelocationPointer == relocationPointer, failurePrefix + "-relocation-pointer");
        Require(section.RelocationCount == relocationCount, failurePrefix + "-relocation-count");
        Require(section.Characteristics == characteristics, failurePrefix + "-characteristics");
        Require(section.RawSize != 0 && section.RawPointer != 0, failurePrefix + "-not-file-backed");
        bool writable = (section.Characteristics & 0x80000000) != 0;
        bool executable = (section.Characteristics & 0x20000000) != 0;
        bool code = (section.Characteristics & 0x00000020) != 0;
        bool initializedData = (section.Characteristics & 0x00000040) != 0;
        bool uninitializedData = (section.Characteristics & 0x00000080) != 0;
        bool remove = (section.Characteristics & 0x00000800) != 0;
        bool comdat = (section.Characteristics & 0x00001000) != 0;
        bool discardable = (section.Characteristics & 0x02000000) != 0;
        bool relocationOverflow = (section.Characteristics & 0x01000000) != 0;
        Require(!writable, failurePrefix + "-writable");
        Require(!uninitializedData, failurePrefix + "-uninitialized");
        Require(!remove, failurePrefix + "-remove");
        Require(!comdat, failurePrefix + "-comdat");
        Require(!discardable, failurePrefix + "-discardable");
        Require(!relocationOverflow, failurePrefix + "-relocation-overflow");
        if (failurePrefix == "text")
        {
            Require(executable && code && !initializedData, "text-memory-contract");
            Require((section.Characteristics & 0x00F00000) == 0x00500000, "text-alignment");
        }
        else
        {
            Require(!executable && !code && initializedData, failurePrefix + "-memory-contract");
            Require((section.Characteristics & 0x00F00000) == 0x00300000, failurePrefix + "-alignment");
        }
    }

    private static void ValidateRelocation(ParsedRelocation relocation, uint expectedOffset, uint expectedSymbolIndex, string failurePrefix)
    {
        Require(relocation.VirtualAddress == expectedOffset, failurePrefix + "-offset");
        Require(relocation.SymbolIndex == expectedSymbolIndex, failurePrefix + "-symbol");
        Require(relocation.Type == Amd64Addr32Nb, failurePrefix + "-type");
    }

    private static void ValidateSerializerAbi(ParsedCoff coff)
    {
        byte[] text = coff.Sections[0].RawData;
        Require(text.Length == 20, "abi-length");
        Require(text[0] == 0x53, "abi-push-rbx");
        Require(text[1] == 0xB8 && ReadUInt32(text, 2) == 0, "abi-cpuid-leaf");
        Require(text[6] == 0xB9 && ReadUInt32(text, 7) == 0, "abi-cpuid-subleaf");
        Require(text[11] == 0x0F && text[12] == 0xA2, "abi-cpuid");
        Require(text[13] == 0xB8, "abi-cookie-mov");
        Require(ReadUInt32(text, 14) == 0x53524C43, "abi-cookie");
        Require(text[18] == 0x5B, "abi-pop-rbx");
        Require(text[19] == 0xC3, "abi-ret");
        Require(CountSequence(text, [0x0F, 0xA2]) == 1, "abi-cpuid-count");
        Require(CountByte(text, 0x53) == 2, "abi-cookie-or-push-count");
        Require(ComputeStackDelta(text) == 0, "abi-rsp-not-restored");
        Require(InstructionsPreserveRflags(text), "abi-rflags-not-preserved");
        Require(!ContainsCallOrConditionalBranch(text), "abi-control-transfer");
        byte[] unwind = coff.Sections[1].RawData;
        Require((unwind[0] & 0x07) == 1, "unwind-version");
        Require((unwind[0] >> 3) == 0, "unwind-flags");
        Require(unwind[1] == 1, "unwind-prolog-size");
        Require(unwind[2] == 1, "unwind-code-count");
        Require(unwind[3] == 0, "unwind-frame-register");
        Require(unwind[4] == 1, "unwind-code-offset");
        Require((unwind[5] & 0x0F) == 0, "unwind-operation");
        Require((unwind[5] >> 4) == 3, "unwind-register");
        Require(unwind[6] == 0 && unwind[7] == 0, "unwind-padding");
    }

    private static int CountSequence(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> sequence)
    {
        int count = 0;
        for (int index = 0; index <= bytes.Length - sequence.Length; index++)
        {
            if (bytes.Slice(index, sequence.Length).SequenceEqual(sequence))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountByte(ReadOnlySpan<byte> bytes, byte value)
    {
        int count = 0;
        foreach (byte current in bytes)
        {
            if (current == value)
            {
                count++;
            }
        }

        return count;
    }

    private static int ComputeStackDelta(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ExpectedText.Length)
        {
            return int.MinValue;
        }

        int delta = 0;
        if (bytes[0] == 0x53)
        {
            delta -= 8;
        }

        if (bytes[18] == 0x5B)
        {
            delta += 8;
        }

        return delta;
    }

    private static bool InstructionsPreserveRflags(ReadOnlySpan<byte> bytes)
    {
        return bytes.SequenceEqual(ExpectedText) && bytes[0] == 0x53 && bytes[1] == 0xB8 && bytes[6] == 0xB9 && bytes[11] == 0x0F && bytes[12] == 0xA2 && bytes[13] == 0xB8 && bytes[18] == 0x5B && bytes[19] == 0xC3;
    }

    private static bool ContainsCallOrConditionalBranch(ReadOnlySpan<byte> bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            byte opcode = bytes[index];
            if (opcode is 0xE8 or 0x9A || opcode is >= 0x70 and <= 0x7F)
            {
                return true;
            }

            if (opcode == 0x0F && index + 1 < bytes.Length && bytes[index + 1] is >= 0x80 and <= 0x8F)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateIndependentParserMutationFamilies(byte[] valid)
    {
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] truncated = valid.AsSpan(0, length).ToArray();
            RequireRejected(truncated, "truncated-" + length.ToString(CultureInfo.InvariantCulture));
        }

        byte[] extended = new byte[valid.Length + 1];
        valid.CopyTo(extended, 0);
        RequireRejected(extended, "trailing-data");
        for (int byteIndex = 0; byteIndex < valid.Length; byteIndex++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                byte[] mutated = (byte[])valid.Clone();
                mutated[byteIndex] ^= (byte)(1 << bit);
                RequireRejected(mutated, "bit-mutation-" + byteIndex.ToString(CultureInfo.InvariantCulture) + "-" + bit.ToString(CultureInfo.InvariantCulture));
            }
        }

        AssertUInt16MutationRejected(valid, 0, 0, "machine-zero");
        AssertUInt16MutationRejected(valid, 2, ushort.MaxValue, "section-capacity");
        AssertUInt32MutationRejected(valid, 4, 1, "timestamp-nondeterministic");
        AssertUInt32MutationRejected(valid, 8, uint.MaxValue, "symbol-pointer-overflow");
        AssertUInt32MutationRejected(valid, 12, uint.MaxValue, "symbol-count-overflow");
        AssertUInt16MutationRejected(valid, 16, ushort.MaxValue, "optional-header-overflow");
        AssertUInt16MutationRejected(valid, 18, ushort.MaxValue, "header-characteristics");
        AssertUInt32MutationRejected(valid, 20 + 20, 0, "text-non-file-backed");
        AssertUInt32MutationRejected(valid, 20 + 36, TextCharacteristics | 0x80000000, "text-writable");
        AssertUInt32MutationRejected(valid, 20 + 36, TextCharacteristics & ~0x20000000u, "text-not-executable");
        AssertUInt32MutationRejected(valid, 20 + 36, TextCharacteristics & ~0x20u, "text-not-code");
        AssertUInt32MutationRejected(valid, 60 + 36, ReadOnlyDataCharacteristics | 0x20000000, "xdata-executable");
        AssertUInt32MutationRejected(valid, 100 + 36, ReadOnlyDataCharacteristics | 0x80000000, "pdata-writable");
        AssertUInt16MutationRejected(valid, 100 + 32, 2, "relocation-missing");
        AssertUInt16MutationRejected(valid, 100 + 32, 4, "relocation-extra");
        AssertUInt32MutationRejected(valid, 180, 1, "relocation-offset");
        AssertUInt32MutationRejected(valid, 184, 1, "relocation-symbol");
        AssertUInt16MutationRejected(valid, 188, 0, "relocation-type");
        AssertUInt32MutationRejected(valid, 168 + 4, 19, "pdata-function-range");
        AssertByteMutationRejected(valid, 160, 0, "unwind-version");
        AssertByteMutationRejected(valid, 161, 2, "unwind-prolog");
        AssertByteMutationRejected(valid, 162, 0, "unwind-code-count");
        AssertByteMutationRejected(valid, 163, 0x10, "unwind-frame");
        AssertByteMutationRejected(valid, 164, 0, "unwind-code-offset");
        AssertByteMutationRejected(valid, 165, 0x20, "unwind-operation");
        AssertByteMutationRejected(valid, 165, 0x40, "unwind-register");
        AssertByteMutationRejected(valid, 166, 1, "unwind-padding");
        AssertByteMutationRejected(valid, 140, 0x90, "abi-push-missing");
        AssertByteMutationRejected(valid, 151, 0x90, "abi-cpuid-missing");
        AssertByteMutationRejected(valid, 154, 0, "abi-cookie-wrong");
        AssertByteMutationRejected(valid, 158, 0x90, "abi-rbx-not-restored");
        AssertByteMutationRejected(valid, 159, 0x90, "abi-ret-missing");
        byte[] malformedSectionName = (byte[])valid.Clone();
        malformedSectionName[20] = (byte)'x';
        RequireRejected(malformedSectionName, "malformed-section-name");
        byte[] emptySectionName = (byte[])valid.Clone();
        Array.Clear(emptySectionName, 20, 8);
        RequireRejected(emptySectionName, "empty-section-name");
        byte[] directiveSection = (byte[])valid.Clone();
        WriteShortName(directiveSection, 20, ".drectve");
        RequireRejected(directiveSection, "unexpected-directive");
        byte[] debugSection = (byte[])valid.Clone();
        WriteShortName(debugSection, 20, ".debug");
        RequireRejected(debugSection, "unexpected-debug");
        byte[] duplicateSymbol = (byte[])valid.Clone();
        valid.AsSpan(210, 18).CopyTo(duplicateSymbol.AsSpan(228, 18));
        RequireRejected(duplicateSymbol, "duplicate-symbol");
        byte[] emptySymbol = (byte[])valid.Clone();
        Array.Clear(emptySymbol, 210, 8);
        RequireRejected(emptySymbol, "empty-symbol");
        byte[] missingStringTerminator = (byte[])valid.Clone();
        missingStringTerminator[335] = (byte)'X';
        RequireRejected(missingStringTerminator, "string-unterminated");
        byte[] overlappingRaw = (byte[])valid.Clone();
        WriteUInt32(overlappingRaw, 60 + 20, 140);
        RequireRejected(overlappingRaw, "raw-ranges-overlap");
        byte[] rawOutOfBounds = (byte[])valid.Clone();
        WriteUInt32(rawOutOfBounds, 20 + 20, uint.MaxValue);
        RequireRejected(rawOutOfBounds, "raw-range-overflow");
        byte[] relocOutOfBounds = (byte[])valid.Clone();
        WriteUInt32(relocOutOfBounds, 100 + 24, uint.MaxValue);
        RequireRejected(relocOutOfBounds, "relocation-range-overflow");
    }

    private static void AssertByteMutationRejected(byte[] valid, int offset, byte value, string name)
    {
        byte[] mutated = (byte[])valid.Clone();
        mutated[offset] = value;
        RequireRejected(mutated, name);
    }

    private static void AssertUInt16MutationRejected(byte[] valid, int offset, ushort value, string name)
    {
        byte[] mutated = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(mutated.AsSpan(offset, 2), value);
        RequireRejected(mutated, name);
    }

    private static void AssertUInt32MutationRejected(byte[] valid, int offset, uint value, string name)
    {
        byte[] mutated = (byte[])valid.Clone();
        WriteUInt32(mutated, offset, value);
        RequireRejected(mutated, name);
    }

    private static void WriteShortName(byte[] destination, int offset, string name)
    {
        Array.Clear(destination, offset, 8);
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 8)).CopyTo(destination.AsSpan(offset, 8));
    }

    private static void RequireRejected(byte[] bytes, string mutationName)
    {
        try
        {
            ParseAndValidateExpected(bytes);
        }
        catch (M01TestFailureException)
        {
            return;
        }
        catch (CoffFormatException)
        {
            return;
        }
        catch (OverflowException)
        {
            return;
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new M01TestFailureException("mutation-accepted-" + mutationName);
    }

    private static void ValidateBoundedProcessAndPartialObject(string temporaryRoot)
    {
        // These temporary children prove the focused runner's timeout, bounded-capture,
        // and partial-file rejection behavior. They do not add a production hook or
        // imply that the real serializer generator intentionally blocks or truncates.
        string fakeRoot = Path.Combine(temporaryRoot, "fake-child");
        Directory.CreateDirectory(fakeRoot);
        string partialPath = Path.Combine(fakeRoot, "partial.obj");
        ProcessStartInfo partialStart = NewControlStartInfo(fakeRoot, "partial-object");
        partialStart.ArgumentList.Add(partialPath);
        ProcessResult partial = RunBounded(partialStart, 10_000);
        Require(!partial.TimedOut && partial.ExitCode == 0, "fake-partial-process");
        Require(!partial.StandardOutputOverflow && !partial.StandardErrorOverflow, "fake-partial-output-overflow");
        Require(!partial.StandardOutputIncomplete && !partial.StandardErrorIncomplete, "fake-partial-output-incomplete");
        Require(partial.StandardOutput.Contains("partial-object-written", StringComparison.Ordinal), "fake-partial-marker");
        Require(File.Exists(partialPath), "fake-partial-file-missing");
        RequireRejected(File.ReadAllBytes(partialPath), "partial-process-output");
        ProcessStartInfo timeoutStart = NewControlStartInfo(fakeRoot, "partial-before-timeout");
        ProcessResult timeout = RunBounded(timeoutStart, 2_000);
        Require(timeout.TimedOut, "fake-child-timeout-not-detected");
        Require(!timeout.StandardOutputOverflow && !timeout.StandardErrorOverflow, "fake-child-timeout-output-overflow");
        Require(!timeout.StandardOutputIncomplete && !timeout.StandardErrorIncomplete, "fake-child-timeout-output-incomplete");
        Require(timeout.StandardOutput.Contains("partial-before-timeout", StringComparison.Ordinal), "fake-child-timeout-lost-partial-output");
        ProcessStartInfo overflowStart = NewControlStartInfo(fakeRoot, "uppercase-output");
        ProcessResult overflow = RunBounded(overflowStart, timeoutMilliseconds: 10_000, maximumBytesPerStream: 1024);
        Require(!overflow.TimedOut && overflow.ExitCode == 0, "fake-child-overflow-process");
        Require(overflow.StandardOutputOverflow, "fake-child-overflow-not-detected");
        Require(!overflow.StandardErrorOverflow, "fake-child-stderr-overflow");
        Require(!overflow.StandardOutputIncomplete && !overflow.StandardErrorIncomplete, "fake-child-overflow-output-incomplete");
        Require(overflow.StandardOutput.Length == 1024, "fake-child-overflow-cap");
        Require(overflow.StandardOutput.All(character => character == 'X'), "fake-child-overflow-capture");
    }

    private static ProcessStartInfo NewControlStartInfo(string workingDirectory, string control)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(HostTestRunner).Assembly.Location);
        start.ArgumentList.Add("--runner-control");
        start.ArgumentList.Add(control);
        return start;
    }

    private static ProcessResult RunBounded(ProcessStartInfo start, int timeoutMilliseconds, int maximumBytesPerStream = 1_048_576)
    {
        Require(timeoutMilliseconds > 0, "process-timeout-invalid");
        Require(maximumBytesPerStream > 0 && maximumBytesPerStream <= 16 * 1024 * 1024, "process-output-cap-invalid");
        Require(string.IsNullOrEmpty(start.Arguments), "process-arguments-string-unsupported");
        string helperPath = _boundedProcessHelperPath ?? string.Empty;
        Require(File.Exists(helperPath), "process-helper-missing");
        string resultRoot = Path.Combine(Path.GetTempPath(), "clros-m01-process-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(resultRoot);
        try
        {
            string workingDirectory = string.IsNullOrWhiteSpace(start.WorkingDirectory) ? Environment.CurrentDirectory : Path.GetFullPath(start.WorkingDirectory);
            var helperStart = new ProcessStartInfo(helperPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(helperPath)!
            };
            helperStart.Environment.Clear();
            foreach ((string key, string? value)in start.Environment)
            {
                if (value is not null)
                {
                    helperStart.Environment[key] = value;
                }
            }

            helperStart.ArgumentList.Add("--result-root");
            helperStart.ArgumentList.Add(resultRoot);
            helperStart.ArgumentList.Add("--timeout-ms");
            helperStart.ArgumentList.Add(timeoutMilliseconds.ToString(CultureInfo.InvariantCulture));
            helperStart.ArgumentList.Add("--max-bytes");
            helperStart.ArgumentList.Add(maximumBytesPerStream.ToString(CultureInfo.InvariantCulture));
            helperStart.ArgumentList.Add("--working-directory");
            helperStart.ArgumentList.Add(workingDirectory);
            helperStart.ArgumentList.Add("--executable");
            helperStart.ArgumentList.Add(start.FileName);
            helperStart.ArgumentList.Add("--");
            foreach (string argument in start.ArgumentList)
            {
                helperStart.ArgumentList.Add(argument);
            }

            int helperTimeout;
            try
            {
                helperTimeout = checked(timeoutMilliseconds + 20_000);
            }
            catch (OverflowException)
            {
                throw new M01TestFailureException("process-helper-timeout-overflow");
            }

            using Process helper = new Process
            {
                StartInfo = helperStart
            };
            bool helperStarted = false;
            try
            {
                Require(helper.Start(), "process-helper-start-failed");
                helperStarted = true;
                bool helperExited = helper.WaitForExit(helperTimeout);
                if (!helperExited)
                {
                    Require(BestEffortTerminateAndReap(helper, 10_000), "process-helper-kill-timeout");
                }

                Require(helperExited, "process-helper-timeout");
                Require(helper.ExitCode == 0, "process-helper-failed");
                return ReadBoundedProcessResult(resultRoot, maximumBytesPerStream);
            }
            finally
            {
                if (helperStarted)
                {
                    _ = BestEffortTerminateAndReap(helper, 10_000);
                }
            }
        }
        finally
        {
            DeleteTemporaryTree(resultRoot);
        }
    }

    private static ProcessResult ReadBoundedProcessResult(string resultRoot, int maximumBytesPerStream)
    {
        string stdoutPath = Path.Combine(resultRoot, "stdout.bin");
        string stderrPath = Path.Combine(resultRoot, "stderr.bin");
        string metadataPath = Path.Combine(resultRoot, "result.txt");
        Require(File.Exists(stdoutPath), "process-helper-stdout-missing");
        Require(File.Exists(stderrPath), "process-helper-stderr-missing");
        Require(File.Exists(metadataPath), "process-helper-metadata-missing");
        Require(new FileInfo(stdoutPath).Length <= maximumBytesPerStream && new FileInfo(stderrPath).Length <= maximumBytesPerStream, "process-helper-capture-oversized");
        Require(new FileInfo(metadataPath).Length <= 2_048, "process-helper-metadata-oversized");
        string[] metadata = File.ReadAllLines(metadataPath, Encoding.ASCII);
        Require(metadata.Length == 7, "process-helper-metadata-line-count");
        Require(metadata[0] == "format=1", "process-helper-metadata-format");
        Require(int.TryParse(MetadataValue(metadata[1], "exitCode="), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int exitCode), "process-helper-exit-code");
        Require(bool.TryParse(MetadataValue(metadata[2], "timedOut="), out bool timedOut), "process-helper-timed-out");
        Require(bool.TryParse(MetadataValue(metadata[3], "stdoutOverflow="), out bool stdoutOverflow), "process-helper-stdout-overflow");
        Require(bool.TryParse(MetadataValue(metadata[4], "stderrOverflow="), out bool stderrOverflow), "process-helper-stderr-overflow");
        Require(Enum.TryParse(MetadataValue(metadata[5], "stdoutDrain="), ignoreCase: false, out StreamDrainResult stdoutDrain) && Enum.IsDefined(stdoutDrain), "process-helper-stdout-drain");
        Require(Enum.TryParse(MetadataValue(metadata[6], "stderrDrain="), ignoreCase: false, out StreamDrainResult stderrDrain) && Enum.IsDefined(stderrDrain), "process-helper-stderr-drain");
        byte[] stdout = File.ReadAllBytes(stdoutPath);
        byte[] stderr = File.ReadAllBytes(stderrPath);
        bool stdoutComplete = stdoutDrain == StreamDrainResult.EndOfStream;
        bool stderrComplete = stderrDrain == StreamDrainResult.EndOfStream;
        return new ProcessResult(exitCode, Encoding.UTF8.GetString(stdout), Encoding.UTF8.GetString(stderr), timedOut, stdoutOverflow, stderrOverflow, StandardOutputIncomplete: !stdoutComplete, StandardErrorIncomplete: !stderrComplete, stdoutDrain, stderrDrain);
    }

    private static string MetadataValue(string line, string prefix)
    {
        Require(line.StartsWith(prefix, StringComparison.Ordinal), "process-helper-metadata-key");
        return line[prefix.Length..];
    }

    private static void DeleteTemporaryTree(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        Require(offset >= 0 && offset <= bytes.Length - sizeof(uint), "test-read-u32-out-of-range");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);
    }

    private static byte[] BuildExpectedStringTable()
    {
        byte[] result = new byte[90];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), 90);
        WriteNullTerminatedAscii(result, 4, ".text$clrossm");
        WriteNullTerminatedAscii(result, 18, ".xdata$clrossm");
        WriteNullTerminatedAscii(result, 33, ".pdata$clrossm");
        WriteNullTerminatedAscii(result, 48, OS.Tooling.EfiContracts.SerializerSymbol);
        return result;
    }

    private static void WriteNullTerminatedAscii(byte[] destination, int offset, string value)
    {
        int count = Encoding.ASCII.GetBytes(value, destination.AsSpan(offset));
        destination[offset + count] = 0;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition)
        {
            throw new M01TestFailureException(code);
        }
    }
}

internal static partial class M01InstructionPublicationSerializerGeneratorTests
{
}
