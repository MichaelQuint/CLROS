using System.Security.Cryptography;

namespace OS.X64TextSerializerGenerator;

internal static class Program
{
    private const string TestName = "x64.serializer.generate";
    private const string BarrierTestName = "m04.010.thunks.generate";
    private const string DispatchTestName = "m04.016.thunk.generate";

    private static int Main(string[] args)
    {
        string testName = Array.IndexOf(args, "--filtered-catch") >= 0 || Array.IndexOf(args, "--filter-invoke") >= 0 ? "m04.021.filters.thunk.generate"
            : Array.IndexOf(args, "--constructor-failure") >= 0 ? "m04.021.ctor-failure.thunk.generate"
            : Array.IndexOf(args, "--frame-dispatch") >= 0 ? "m04.021.frames.thunk.generate"
            : Array.IndexOf(args, "--nested-finally-catch") >= 0 ? "m04.021.nested-finally.thunk.generate"
            : Array.IndexOf(args, "--rethrow-inner") >= 0 || Array.IndexOf(args, "--rethrow-outer") >= 0
            ? "m04.021.rethrow.thunk.generate"
            : Array.IndexOf(args, "--single-finally-catch") >= 0 ? "m04.021.finally.thunk.generate"
            : Array.IndexOf(args, "--simple-throw-catch") >= 0 ? "m04.021.thunk.generate"
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
        bool generated = parseResult.FilterMode != 0
            ? TryGenerateFilter(parseResult.FilterMode == 2, out objectBytes, out generationError)
            : parseResult.ConstructorFailure
            ? TryGenerateConstructorFailure(out objectBytes, out generationError)
            : parseResult.FrameDispatch
            ? TryGenerateFrameDispatch(out objectBytes, out generationError)
            : parseResult.NestedFinallyCatch
            ? TryGenerateNestedFinally(out objectBytes, out generationError)
            : parseResult.RethrowMode != 0
            ? TryGenerateRethrow(parseResult.RethrowMode == 2, out objectBytes, out generationError)
            : parseResult.SingleFinallyCatch
            ? TryGenerateSingleFinallyCatch(out objectBytes, out generationError)
            : parseResult.SimpleThrowCatch
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
        string ready = parseResult.RethrowMode != 0 ? "M04_RETHROW_THUNK_READY role=" + (parseResult.RethrowMode == 2 ? "outer" : "inner") : parseResult.SingleFinallyCatch ? "M04_SINGLE_FINALLY_THUNK_READY" : parseResult.SimpleThrowCatch ? "M04_SIMPLE_CATCH_THUNK_READY" : parseResult.InterfaceDispatch ? "M04_INTERFACE_DISPATCH_THUNK_READY" : parseResult.CheckedReferences ? "M04_CHECKED_REFERENCE_THUNKS_READY" : "X64_SERIALIZER_OBJECT_READY";
        if (parseResult.NestedFinallyCatch) ready = "M04_NESTED_FINALLY_THUNK_READY";
        if (parseResult.FrameDispatch) ready = "M04_FRAME_DISPATCH_THUNK_READY";
        if (parseResult.ConstructorFailure) ready = "M04_CCTOR_FAILURE_THUNK_READY";
        if (parseResult.FilterMode != 0) ready = parseResult.FilterMode == 2 ? "M04_FILTER_INVOKE_THUNK_READY" : "M04_FILTERED_CATCH_THUNK_READY";
        Console.WriteLine(
            $"{ready} bytes={objectBytes.Length} " +
            $"sha256={hash} state={writeResult.State}");
        Console.WriteLine($"TEST_PASS {testName}");
        return 0;
    }

    private static bool TryGenerateFilter(bool invoke, out byte[] objectBytes, out CoffWriteError error)
    {
        var definition = invoke ? ExceptionFilterThunkGenerator.Define() : ExceptionHandlingThunkGenerator.DefineFilteredCatch();
        objectBytes = new byte[invoke ? 771 : 1361];
        if (!Amd64CoffWriter.TryWrite(in definition, objectBytes, true, true, out int written, out error)) return false;
        string hash = invoke ? "E6D84F39A3F159EDEFAA7A68DD552BF34CCBFA39EFBE61F5ACDFAA80B838B8A1" : "16FC99408418187DA12BAB063932E8C0489DD7FC1E0DADB986136F700D0859A3";
        if (written != objectBytes.Length || Convert.ToHexString(SHA256.HashData(objectBytes)) != hash) { error = CoffWriteError.InternalInvariant; return false; }
        return true;
    }

    private static bool TryGenerateConstructorFailure(out byte[] objectBytes, out CoffWriteError error)
    {
        var definition = ExceptionHandlingThunkGenerator.DefineConstructorFailure();
        objectBytes = new byte[1545];
        if (!Amd64CoffWriter.TryWrite(in definition, objectBytes, true, true, out int written, out error)) return false;
        if (written != objectBytes.Length || Convert.ToHexString(SHA256.HashData(objectBytes)) !=
            "6F17A868DBBCCFBAAF0DC46527F8DA2C2C92487A7075995EB42DF7D6CB019240")
        { error = CoffWriteError.InternalInvariant; return false; }
        return true;
    }

    private static bool TryGenerateFrameDispatch(out byte[] objectBytes, out CoffWriteError error)
    {
        var definition = ExceptionHandlingThunkGenerator.DefineFrameDispatch();
        objectBytes = new byte[1869];
        if (!Amd64CoffWriter.TryWrite(in definition, objectBytes, true, true, out int written, out error)) return false;
        if (written != objectBytes.Length || Convert.ToHexString(SHA256.HashData(objectBytes)) !=
            "356BCDDD67D7E11C70E46BB235452FC58D66608907DF0F8688A0DD2BCD6B6E96")
        { error = CoffWriteError.InternalInvariant; return false; }
        return true;
    }

    private static bool TryGenerateNestedFinally(out byte[] objectBytes, out CoffWriteError error)
    {
        var definition = ExceptionHandlingThunkGenerator.DefineNestedFinally();
        byte[] buffer = new byte[8192];
        bool success = Amd64CoffWriter.TryWrite(in definition, buffer, true, true, out int written, out error);
        objectBytes = success ? buffer.AsSpan(0, written).ToArray() : [];
        return success;
    }

    private static bool TryGenerateRethrow(bool outer, out byte[] objectBytes, out CoffWriteError error)
    {
        var definition = ExceptionHandlingThunkGenerator.DefineRethrow(outer);
        objectBytes = new byte[outer ? 1382 : 1732];
        if (!Amd64CoffWriter.TryWrite(in definition, objectBytes, true, true, out int written, out error)) return false;
        string expected = outer ? "635C903FF77EE3C7DE3D1EDCE3E6F015DDEF204454A50432DFCF9692686E337B"
            : "D30D37F5AD93A70A66D2ED2E69A184A75DB903C5A1D69C4E31571569016E7A10";
        if (written != objectBytes.Length || Convert.ToHexString(SHA256.HashData(objectBytes)) != expected)
        { error = CoffWriteError.InternalInvariant; return false; }
        return true;
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

    private static bool TryGenerateSingleFinallyCatch(out byte[] objectBytes, out CoffWriteError error)
    {
        objectBytes = new byte[1707];
        if (!ExceptionHandlingThunkGenerator.TryGenerate(objectBytes, out int written, out error, singleFinally: true)) return false;
        if (written != 1707 || Convert.ToHexString(SHA256.HashData(objectBytes)) !=
            "D24DA7653745DA1D37F1FC9D286A2E370C32B687A0415E90FC626A597326025A")
        { error = CoffWriteError.InternalInvariant; return false; }
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
        bool singleFinallyCatch = false;
        int rethrowMode = 0;
        bool nestedFinallyCatch = false;
        bool frameDispatch = false;
        bool constructorFailure = false;
        int filterMode = 0;

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option is "--filtered-catch" or "--filter-invoke")
            {
                int mode = option == "--filtered-catch" ? 1 : 2;
                if (filterMode == mode) return CliParseResult.Fail("argument_duplicate");
                if (filterMode != 0 || constructorFailure || frameDispatch || nestedFinallyCatch || rethrowMode != 0 || singleFinallyCatch || simpleThrowCatch || interfaceDispatch || checkedReferences) return CliParseResult.Fail("argument_mode_conflict");
                filterMode = mode; continue;
            }
            if (filterMode != 0 && option is "--constructor-failure" or "--frame-dispatch" or "--nested-finally-catch" or "--rethrow-inner" or "--rethrow-outer" or "--single-finally-catch" or "--simple-throw-catch" or "--interface-dispatch" or "--checked-reference-barriers") return CliParseResult.Fail("argument_mode_conflict");
            if (option == "--constructor-failure")
            {
                if (constructorFailure) return CliParseResult.Fail("argument_duplicate");
                if (frameDispatch || nestedFinallyCatch || rethrowMode != 0 || singleFinallyCatch || simpleThrowCatch || interfaceDispatch || checkedReferences)
                    return CliParseResult.Fail("argument_mode_conflict");
                constructorFailure = true;
                continue;
            }
            if (constructorFailure && option is "--frame-dispatch" or "--nested-finally-catch" or "--rethrow-inner" or "--rethrow-outer" or
                "--single-finally-catch" or "--simple-throw-catch" or "--interface-dispatch" or "--checked-reference-barriers")
                return CliParseResult.Fail("argument_mode_conflict");
            if (option == "--frame-dispatch")
            {
                if (frameDispatch) return CliParseResult.Fail("argument_duplicate");
                if (nestedFinallyCatch || rethrowMode != 0 || singleFinallyCatch || simpleThrowCatch || interfaceDispatch || checkedReferences)
                    return CliParseResult.Fail("argument_mode_conflict");
                frameDispatch = true;
                continue;
            }
            if (frameDispatch && option is "--nested-finally-catch" or "--rethrow-inner" or "--rethrow-outer" or
                "--single-finally-catch" or "--simple-throw-catch" or "--interface-dispatch" or "--checked-reference-barriers")
                return CliParseResult.Fail("argument_mode_conflict");
            if (option == "--nested-finally-catch")
            {
                if (nestedFinallyCatch) return CliParseResult.Fail("argument_duplicate");
                if (rethrowMode != 0 || singleFinallyCatch || simpleThrowCatch || interfaceDispatch || checkedReferences)
                    return CliParseResult.Fail("argument_mode_conflict");
                nestedFinallyCatch = true;
                continue;
            }
            if (nestedFinallyCatch && option is "--rethrow-inner" or "--rethrow-outer" or "--single-finally-catch" or
                "--simple-throw-catch" or "--interface-dispatch" or "--checked-reference-barriers")
                return CliParseResult.Fail("argument_mode_conflict");
            if (option is "--rethrow-inner" or "--rethrow-outer")
            {
                int mode = option == "--rethrow-inner" ? 1 : 2;
                if (rethrowMode == mode) return CliParseResult.Fail("argument_duplicate");
                if (rethrowMode != 0 || singleFinallyCatch || simpleThrowCatch || interfaceDispatch || checkedReferences)
                    return CliParseResult.Fail("argument_mode_conflict");
                rethrowMode = mode;
                continue;
            }
            if (option == "--single-finally-catch")
            {
                if (singleFinallyCatch) return CliParseResult.Fail("argument_duplicate");
                if (rethrowMode != 0 || simpleThrowCatch || interfaceDispatch || checkedReferences) return CliParseResult.Fail("argument_mode_conflict");
                singleFinallyCatch = true;
                continue;
            }
            if (option == "--simple-throw-catch")
            {
                if (simpleThrowCatch) return CliParseResult.Fail("argument_duplicate");
                if (rethrowMode != 0 || singleFinallyCatch || interfaceDispatch || checkedReferences) return CliParseResult.Fail("argument_mode_conflict");
                simpleThrowCatch = true;
                continue;
            }
            if (option == "--interface-dispatch")
            {
                if (interfaceDispatch) return CliParseResult.Fail("argument_duplicate");
                if (rethrowMode != 0 || singleFinallyCatch || checkedReferences || simpleThrowCatch) return CliParseResult.Fail("argument_mode_conflict");
                interfaceDispatch = true;
                continue;
            }
            if (option == "--checked-reference-barriers")
            {
                if (rethrowMode != 0 || singleFinallyCatch || interfaceDispatch || simpleThrowCatch) return CliParseResult.Fail("argument_mode_conflict");
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

        return new(true, outputRoot, outputFile, null, checkedReferences, interfaceDispatch, simpleThrowCatch, singleFinallyCatch, rethrowMode, nestedFinallyCatch, frameDispatch, constructorFailure, filterMode);
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
        bool SimpleThrowCatch = false,
        bool SingleFinallyCatch = false,
        int RethrowMode = 0,
        bool NestedFinallyCatch = false,
        bool FrameDispatch = false,
        bool ConstructorFailure = false,
        int FilterMode = 0)
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
