using System.Security.Cryptography;

namespace OS.X64TextSerializerGenerator;

internal static class Program
{
    private const string TestName = "x64.serializer.generate";
    private const string BarrierTestName = "m04.010.thunks.generate";
    private const string DispatchTestName = "m04.016.thunk.generate";

    private static int Main(string[] args)
    {
        string testName = Array.IndexOf(args, "--simple-throw-catch") >= 0 ? "m04.021.thunk.generate"
            : Array.IndexOf(args, "--interface-dispatch") >= 0 ? DispatchTestName
            : Array.IndexOf(args, "--checked-reference-barriers") >= 0 ? BarrierTestName : TestName;
        Console.WriteLine($"TEST_START {testName}");

        CliParseResult parseResult = ParseArguments(args);
        if (!parseResult.Success)
        {
            return Fail(parseResult.ErrorCode!, exitCode: 2, testName);
        }

        if (!TryResolveOutput(
                parseResult.OutputRoot!,
                parseResult.OutputFile!,
                out string? outputPath,
                out string? validationError))
        {
            return Fail(validationError!, exitCode: 2, testName);
        }

        byte[] objectBytes;
        CoffWriteError generationError;
        bool generated = parseResult.SimpleThrowCatch
            ? TryGenerateSimpleThrowCatch(out objectBytes, out generationError)
            : parseResult.InterfaceDispatch
            ? TryGenerateInterfaceDispatch(out objectBytes, out generationError)
            : parseResult.CheckedReferences
            ? TryGenerateCheckedReferences(out objectBytes, out generationError)
            : InstructionPublicationSerializerGenerator.TryGenerate(
                InstructionPublicationSerializerGenerator.ObjectSizeBytes,
                out objectBytes, out generationError);
        if (!generated)
        {
            return Fail(
                "generation_" + Amd64CoffWriter.GetStableErrorCode(generationError),
                exitCode: 1, testName);
        }

        OutputWriteResult writeResult = WriteAtomically(outputPath!, objectBytes);
        if (!writeResult.Success)
        {
            return Fail(writeResult.ErrorCode!, exitCode: 1, testName);
        }

        string hash = Convert.ToHexString(SHA256.HashData(objectBytes));
        string ready = parseResult.SimpleThrowCatch ? "M04_SIMPLE_CATCH_THUNK_READY" : parseResult.InterfaceDispatch ? "M04_INTERFACE_DISPATCH_THUNK_READY" : parseResult.CheckedReferences ? "M04_CHECKED_REFERENCE_THUNKS_READY" : "X64_SERIALIZER_OBJECT_READY";
        Console.WriteLine(
            $"{ready} bytes={objectBytes.Length} " +
            $"sha256={hash} state={writeResult.State}");
        Console.WriteLine($"TEST_PASS {testName}");
        return 0;
    }

    private static bool TryGenerateSimpleThrowCatch(out byte[] objectBytes, out CoffWriteError error)
    {
        objectBytes = new byte[1350];
        if (!ExceptionHandlingThunkGenerator.TryGenerate(objectBytes, out int written, out error)) return false;
        if (written != 1350 || Convert.ToHexString(SHA256.HashData(objectBytes)) !=
            "DD32DC30808C3B6DD42981DF652B95F62236BDC8C45C1DAA3DDE32BAB1C981AB")
        { error = CoffWriteError.InternalInvariant; return false; }
        return true;
    }

    private static bool TryGenerateCheckedReferences(out byte[] objectBytes, out CoffWriteError error)
    {
        objectBytes = new byte[1645];
        if (!CheckedReferenceBarrierThunkGenerator.TryGenerate(objectBytes, out int written, out error)) return false;
        if (written != 1645 || Convert.ToHexString(SHA256.HashData(objectBytes)) !=
            "E96ABF595503B9D1AE38DEE740F93AE9B3EB84F05D6395B931E4B5D74F65D808")
        {
            // A changed fixed emitter must be explicitly reviewed with a new literal oracle.
            error = CoffWriteError.InternalInvariant;
            return false;
        }
        return true;
    }

    private static bool TryGenerateInterfaceDispatch(out byte[] objectBytes, out CoffWriteError error)
    {
        objectBytes = new byte[828];
        if (!InterfaceDispatchThunkGenerator.TryGenerate(objectBytes, out int written, out error)) return false;
        if (written != 828 || Convert.ToHexString(SHA256.HashData(objectBytes)) !=
            "228630B0AD4D6AB298414FDA0FCF07E76CB4699A63339FA59BA331AFDC3AEB61")
        {
            error = CoffWriteError.InternalInvariant;
            return false;
        }
        return true;
    }

    private static CliParseResult ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return CliParseResult.Fail("arguments_missing");
        }

        string? outputRoot = null;
        string? outputFile = null;
        bool checkedReferences = false;
        bool interfaceDispatch = false;
        bool simpleThrowCatch = false;

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--simple-throw-catch")
            {
                if (simpleThrowCatch) return CliParseResult.Fail("argument_duplicate");
                if (interfaceDispatch || checkedReferences) return CliParseResult.Fail("argument_mode_conflict");
                simpleThrowCatch = true;
                continue;
            }
            if (option == "--interface-dispatch")
            {
                if (interfaceDispatch) return CliParseResult.Fail("argument_duplicate");
                if (checkedReferences || simpleThrowCatch) return CliParseResult.Fail("argument_mode_conflict");
                interfaceDispatch = true;
                continue;
            }
            if (option == "--checked-reference-barriers")
            {
                if (interfaceDispatch || simpleThrowCatch) return CliParseResult.Fail("argument_mode_conflict");
                if (checkedReferences) return CliParseResult.Fail("argument_duplicate");
                checkedReferences = true;
                continue;
            }
            if (option is not "--output-root" and not "--output-file")
            {
                return CliParseResult.Fail("argument_unknown");
            }

            if (index + 1 >= args.Length ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return CliParseResult.Fail("argument_value_missing");
            }

            string value = args[++index];
            if (option == "--output-root")
            {
                if (outputRoot is not null)
                {
                    return CliParseResult.Fail("argument_duplicate");
                }

                outputRoot = value;
            }
            else
            {
                if (outputFile is not null)
                {
                    return CliParseResult.Fail("argument_duplicate");
                }

                outputFile = value;
            }
        }

        if (outputRoot is null || outputFile is null)
        {
            return CliParseResult.Fail("arguments_missing");
        }

        return new(true, outputRoot, outputFile, null, checkedReferences, interfaceDispatch, simpleThrowCatch);
    }

    private static bool TryResolveOutput(
        string outputRoot,
        string outputFile,
        out string? outputPath,
        out string? errorCode)
    {
        outputPath = null;
        errorCode = null;

        if (string.IsNullOrWhiteSpace(outputRoot) ||
            !Path.IsPathFullyQualified(outputRoot) ||
            !Directory.Exists(outputRoot))
        {
            errorCode = "output_root_invalid";
            return false;
        }

        if (!IsSafeObjectFileName(outputFile))
        {
            errorCode = "output_file_invalid";
            return false;
        }

        try
        {
            string normalizedRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(outputRoot));
            string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, outputFile));
            if (!string.Equals(
                    Path.GetDirectoryName(candidate),
                    normalizedRoot,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                errorCode = "output_file_invalid";
                return false;
            }

            outputPath = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            errorCode = "output_file_invalid";
            return false;
        }
    }

    private static bool IsSafeObjectFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Length > 128 ||
            fileName is "." or ".." ||
            Path.IsPathRooted(fileName) ||
            fileName.EndsWith(' ') ||
            fileName.EndsWith('.') ||
            !string.Equals(
                Path.GetExtension(fileName),
                ".obj",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (char character in fileName)
        {
            if (character < 0x20 ||
                character is '"' or '<' or '>' or '|' or ':' or '*' or '?' or '/' or '\\')
            {
                return false;
            }
        }

        string deviceStem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(deviceStem))
        {
            return false;
        }

        int firstDot = deviceStem.IndexOf('.');
        if (firstDot >= 0)
        {
            deviceStem = deviceStem[..firstDot];
        }

        string normalizedDeviceStem = deviceStem.TrimEnd(' ', '.');
        if (normalizedDeviceStem.Length != deviceStem.Length)
        {
            return false;
        }

        return !IsWindowsDeviceName(normalizedDeviceStem);
    }

    private static bool IsWindowsDeviceName(string stem)
    {
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4 &&
            stem[3] is >= '1' and <= '9' &&
            (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
             stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase));
    }

    private static OutputWriteResult WriteAtomically(
        string outputPath,
        ReadOnlySpan<byte> objectBytes)
    {
        if (File.Exists(outputPath))
        {
            if (!TryCompareExistingOutput(outputPath, objectBytes, out bool matches))
            {
                return OutputWriteResult.Fail("io_failure");
            }

            return matches
                ? OutputWriteResult.Unchanged
                : OutputWriteResult.Fail("existing_output_mismatch");
        }

        string outputDirectory = Path.GetDirectoryName(outputPath)!;
        string temporaryPath = Path.Combine(
            outputDirectory,
            $".clros-x64-{Guid.NewGuid():N}.tmp");

        OutputWriteResult result;
        bool temporaryOwned = false;
        bool cleanupFailed = false;
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                temporaryOwned = true;
                stream.Write(objectBytes);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(temporaryPath, outputPath, overwrite: false);
                temporaryOwned = false;
                result = OutputWriteResult.Created;
            }
            catch (IOException) when (File.Exists(outputPath))
            {
                if (!TryCompareExistingOutput(
                        outputPath,
                        objectBytes,
                        out bool matches))
                {
                    result = OutputWriteResult.Fail("io_failure");
                }
                else
                {
                    result = matches
                        ? OutputWriteResult.Unchanged
                        : OutputWriteResult.Fail("existing_output_mismatch");
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            result = OutputWriteResult.Fail("io_failure");
        }
        finally
        {
            try
            {
                if (temporaryOwned && File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
            {
                cleanupFailed = true;
            }
        }

        if (cleanupFailed)
        {
            return OutputWriteResult.Fail("io_failure");
        }

        if (!result.Success)
        {
            return result;
        }

        if (!TryCompareExistingOutput(outputPath, objectBytes, out bool finalMatches))
        {
            return OutputWriteResult.Fail("io_failure");
        }

        if (!finalMatches)
        {
            return OutputWriteResult.Fail("existing_output_mismatch");
        }

        return result;
    }

    private static bool TryCompareExistingOutput(
        string outputPath,
        ReadOnlySpan<byte> expected,
        out bool matches)
    {
        matches = false;
        try
        {
            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length != expected.Length)
            {
                return true;
            }

            byte[] existing = File.ReadAllBytes(outputPath);
            matches = existing.AsSpan().SequenceEqual(expected);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            return false;
        }
    }

    private static int Fail(string errorCode, int exitCode, string testName = TestName)
    {
        Console.Error.WriteLine($"TEST_FAIL {testName} {errorCode}");
        return exitCode;
    }

    private readonly record struct CliParseResult(
        bool Success,
        string? OutputRoot,
        string? OutputFile,
        string? ErrorCode,
        bool CheckedReferences = false,
        bool InterfaceDispatch = false,
        bool SimpleThrowCatch = false)
    {
        public static CliParseResult Fail(string errorCode) =>
            new(false, null, null, errorCode);
    }

    private readonly record struct OutputWriteResult(
        bool Success,
        string? State,
        string? ErrorCode)
    {
        public static OutputWriteResult Created => new(true, "created", null);
        public static OutputWriteResult Unchanged => new(true, "unchanged", null);

        public static OutputWriteResult Fail(string errorCode) =>
            new(false, null, errorCode);
    }

}
