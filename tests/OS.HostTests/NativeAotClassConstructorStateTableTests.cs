using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class NativeAotClassConstructorStateTableTests
{
    private const uint ModuleId = 1;
    private const uint TypeId = 7;
    private const uint FailureCode = 0xC0DE0001;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("runtime.cctor state begins and completes", TestBeginsAndCompletes, ref failures);
        Run("runtime.cctor state detects reentry cycle", TestDetectsReentryCycle, ref failures);
        Run("runtime.cctor state preserves failure", TestPreservesFailure, ref failures);
        Run("runtime.cctor state rejects duplicate keys", TestRejectsDuplicateKeys, ref failures);
        Run("runtime.cctor state default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("runtime.cctor state layouts contain no managed references", TestLayoutsContainNoManagedReferences, ref failures);
        Run("runtime.cctor state formatter formats success marker", TestFormatterFormatsSuccessMarker, ref failures);
        Run("runtime.cctor state formatter reports short destination", TestFormatterReportsShortDestination, ref failures);
        Run("runtime.cctor state formatter rejects default result", TestFormatterRejectsDefaultResult, ref failures);
        Run(
            "runtime.cctor state formatter uses shared ascii text builder",
            TestFormatterUsesSharedAsciiTextBuilder,
            ref failures);
        Run("runtime.cctor runner preflight ready", TestRunnerPreflightReady, ref failures);
        Run("runtime.cctor runner preflight rejects default table", TestRunnerPreflightRejectsDefaultTable, ref failures);
        Run("runtime.cctor runner preflight rejects failed state", TestRunnerPreflightRejectsFailedState, ref failures);
        Run("runtime.cctor runner preflight rejects pending state", TestRunnerPreflightRejectsPendingState, ref failures);
        Run("runtime.cctor runner preflight rejects missing metadata", TestRunnerPreflightRejectsMissingMetadata, ref failures);
        Run("runtime.cctor runner preflight formatter reports short destination", TestRunnerPreflightFormatterReportsShortDestination, ref failures);
        Run(
            "runtime.cctor runner preflight formatter uses shared ascii text builder",
            TestRunnerPreflightFormatterUsesSharedAsciiTextBuilder,
            ref failures);
        Run("boot.sequence cctor runner preflight ready", TestBootSequenceCctorRunnerPreflightReady, ref failures);
        Run("boot.sequence cctor runner preflight rejects failed result", TestBootSequenceCctorRunnerPreflightRejectsFailedResult, ref failures);
        Run("boot.sequence cctor runner preflight rejects default result", TestBootSequenceCctorRunnerPreflightRejectsDefaultResult, ref failures);
        Run("boot.sequence cctor runner preflight preserves default markers", TestBootSequenceCctorRunnerPreflightPreservesDefaultMarkers, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("NativeAOT class constructor state table tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestBeginsAndCompletes()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey key = Key();

        NativeAotClassConstructorStateTableResult registered =
            NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTableResult begun =
            NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTableResult completed =
            NativeAotClassConstructorStateTable.MarkInitialized(entries, key);
        NativeAotClassConstructorStateTableResult readback =
            NativeAotClassConstructorStateTable.TryGet(entries, key);
        NativeAotClassConstructorStateTableResult secondBegin =
            NativeAotClassConstructorStateTable.TryBegin(entries, key);

        return registered.Succeeded
            && registered.State == NativeAotClassConstructorState.Uninitialized
            && registered.SlotIndex == 0
            && registered.OccupiedCount == 1
            && begun.Succeeded
            && begun.PreviousState == NativeAotClassConstructorState.Uninitialized
            && begun.State == NativeAotClassConstructorState.Initializing
            && entries[0].State == NativeAotClassConstructorState.Initialized
            && entries[0].TransitionCount == 2
            && completed.Succeeded
            && completed.PreviousState == NativeAotClassConstructorState.Initializing
            && completed.State == NativeAotClassConstructorState.Initialized
            && readback.Succeeded
            && readback.State == NativeAotClassConstructorState.Initialized
            && readback.FailureCode == 0
            && !secondBegin.Succeeded
            && secondBegin.Status == NativeAotClassConstructorStateTableStatus.AlreadyInitialized;
    }

    private static bool TestDetectsReentryCycle()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key();

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTableResult reentry =
            NativeAotClassConstructorStateTable.TryBegin(entries, key);

        return !reentry.Succeeded
            && reentry.Status == NativeAotClassConstructorStateTableStatus.ReentryCycle
            && reentry.PreviousState == NativeAotClassConstructorState.Initializing
            && reentry.State == NativeAotClassConstructorState.Initializing
            && reentry.SlotIndex == 0
            && reentry.DuplicateSlotIndex == -1
            && entries[0].State == NativeAotClassConstructorState.Initializing
            && entries[0].TransitionCount == 1;
    }

    private static bool TestPreservesFailure()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey key = Key();
        NativeAotClassConstructorKey secondKey = new NativeAotClassConstructorKey(ModuleId, TypeId + 1);

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTableResult failed =
            NativeAotClassConstructorStateTable.MarkFailed(entries, key, FailureCode);
        NativeAotClassConstructorStateTableResult readback =
            NativeAotClassConstructorStateTable.TryGet(entries, key);
        NativeAotClassConstructorStateTableResult retryBegin =
            NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTableResult lateInitialized =
            NativeAotClassConstructorStateTable.MarkInitialized(entries, key);

        NativeAotClassConstructorStateTable.Register(entries, secondKey);
        NativeAotClassConstructorStateTable.TryBegin(entries, secondKey);
        NativeAotClassConstructorStateTableResult zeroFailure =
            NativeAotClassConstructorStateTable.MarkFailed(entries, secondKey, failureCode: 0);

        return failed.Succeeded
            && failed.PreviousState == NativeAotClassConstructorState.Initializing
            && failed.State == NativeAotClassConstructorState.Failed
            && failed.FailureCode == FailureCode
            && entries[0].State == NativeAotClassConstructorState.Failed
            && entries[0].FailureCode == FailureCode
            && readback.Succeeded
            && readback.State == NativeAotClassConstructorState.Failed
            && readback.FailureCode == FailureCode
            && !retryBegin.Succeeded
            && retryBegin.Status == NativeAotClassConstructorStateTableStatus.PreviouslyFailed
            && retryBegin.FailureCode == FailureCode
            && !lateInitialized.Succeeded
            && lateInitialized.Status == NativeAotClassConstructorStateTableStatus.PreviouslyFailed
            && lateInitialized.FailureCode == FailureCode
            && !zeroFailure.Succeeded
            && zeroFailure.Status == NativeAotClassConstructorStateTableStatus.FailureCodeRequired
            && entries[1].State == NativeAotClassConstructorState.Initializing;
    }

    private static bool TestRejectsDuplicateKeys()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key();
        NativeAotClassConstructorKey missingKey = new NativeAotClassConstructorKey(ModuleId, TypeId + 10);

        NativeAotClassConstructorStateTableResult first =
            NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTableResult duplicate =
            NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTableResult missing =
            NativeAotClassConstructorStateTable.TryBegin(entries, missingKey);
        NativeAotClassConstructorStateTableResult capacity =
            NativeAotClassConstructorStateTable.Register(entries, missingKey);

        Span<NativeAotClassConstructorStateEntry> corrupt =
            stackalloc NativeAotClassConstructorStateEntry[2];
        corrupt[0] = NativeAotClassConstructorStateEntry.Occupied(
            key,
            NativeAotClassConstructorState.Uninitialized,
            failureCode: 0,
            transitionCount: 0);
        corrupt[1] = NativeAotClassConstructorStateEntry.Occupied(
            key,
            NativeAotClassConstructorState.Initialized,
            failureCode: 0,
            transitionCount: 1);
        NativeAotClassConstructorStateTableResult corruptDuplicate =
            NativeAotClassConstructorStateTable.TryGet(corrupt, key);

        return first.Succeeded
            && !duplicate.Succeeded
            && duplicate.Status == NativeAotClassConstructorStateTableStatus.DuplicateKey
            && duplicate.SlotIndex == 0
            && duplicate.OccupiedCount == 1
            && !missing.Succeeded
            && missing.Status == NativeAotClassConstructorStateTableStatus.MissingKey
            && !capacity.Succeeded
            && capacity.Status == NativeAotClassConstructorStateTableStatus.CapacityExceeded
            && !corruptDuplicate.Succeeded
            && corruptDuplicate.Status == NativeAotClassConstructorStateTableStatus.DuplicateKey
            && corruptDuplicate.SlotIndex == 0
            && corruptDuplicate.DuplicateSlotIndex == 1
            && corruptDuplicate.OccupiedCount == 2;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];

        NativeAotClassConstructorStateTableResult defaultResult = default;
        NativeAotClassConstructorKey defaultKey = default;
        NativeAotClassConstructorStateEntry defaultEntry = default;
        NativeAotClassConstructorStateTableResult invalidKey =
            NativeAotClassConstructorStateTable.Register(entries, defaultKey);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == NativeAotClassConstructorStateTableStatus.Success
            && !defaultKey.IsValid
            && !defaultEntry.IsOccupied
            && !invalidKey.Succeeded
            && invalidKey.Status == NativeAotClassConstructorStateTableStatus.InvalidKey
            && entries[0].State == NativeAotClassConstructorState.Uninitialized;
    }

    private static bool TestLayoutsContainNoManagedReferences()
    {
        return NativeAotClassConstructorStateTable.UsesCallerOwnedStorage
            && !NativeAotClassConstructorStateTable.ExecutesConstructors
            && !NativeAotClassConstructorStateTable.UsesReflection
            && !NativeAotClassConstructorStateTable.UsesExceptionControlFlow
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorKey>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorStateEntry>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorStateTableResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorStateDiagnosticFormatResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorRunnerPreflightResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorRunnerPreflightFormatResult>();
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        NativeAotClassConstructorStateTableResult result = CreateInitializedResult();
        NativeAotClassConstructorStateDiagnosticFormatResult query =
            NativeAotClassConstructorStateDiagnosticFormatter.QueryRequiredBytes(in result);
        if (!query.Succeeded || query.RequiredBytes <= 0 || query.BytesWritten != 0)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[query.RequiredBytes];
        NativeAotClassConstructorStateDiagnosticFormatResult format =
            NativeAotClassConstructorStateDiagnosticFormatter.FormatResult(in result, destination);
        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        return format.Succeeded
            && format.TableStatus == NativeAotClassConstructorStateTableStatus.Success
            && format.BytesWritten == query.RequiredBytes
            && format.RequiredBytes == query.RequiredBytes
            && marker ==
                "NATIVEAOT_CCTOR_STATE status=Success moduleId=1 typeId=7 previousState=Initializing state=Initialized failureCode=0 slotIndex=0 duplicateSlotIndex=-1 occupiedCount=1 capacity=2";
    }

    private static bool TestFormatterReportsShortDestination()
    {
        NativeAotClassConstructorStateTableResult result = CreateInitializedResult();
        NativeAotClassConstructorStateDiagnosticFormatResult query =
            NativeAotClassConstructorStateDiagnosticFormatter.QueryRequiredBytes(in result);
        if (!query.Succeeded || query.RequiredBytes <= 1)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[query.RequiredBytes - 1];
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = 0xA5;
        }

        NativeAotClassConstructorStateDiagnosticFormatResult format =
            NativeAotClassConstructorStateDiagnosticFormatter.FormatResult(in result, destination);

        return !format.Succeeded
            && format.IsInitialized
            && format.Status == NativeAotClassConstructorStateDiagnosticFormatStatus.DestinationTooSmall
            && format.TableStatus == NativeAotClassConstructorStateTableStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xA5);
    }

    private static bool TestFormatterRejectsDefaultResult()
    {
        NativeAotClassConstructorStateTableResult defaultTableResult = default;
        NativeAotClassConstructorStateDiagnosticFormatResult defaultFormatResult = default;

        NativeAotClassConstructorStateDiagnosticFormatResult query =
            NativeAotClassConstructorStateDiagnosticFormatter.QueryRequiredBytes(in defaultTableResult);
        Span<byte> destination = stackalloc byte[128];
        NativeAotClassConstructorStateDiagnosticFormatResult format =
            NativeAotClassConstructorStateDiagnosticFormatter.FormatResult(in defaultTableResult, destination);

        return !defaultFormatResult.IsInitialized
            && !defaultFormatResult.Succeeded
            && !query.Succeeded
            && query.Status == NativeAotClassConstructorStateDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && !format.Succeeded
            && format.Status == NativeAotClassConstructorStateDiagnosticFormatStatus.InvalidResult
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length;
    }

    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Runtime",
                "NativeAotClassConstructorStateDiagnosticFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.Key.ModuleId)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.Key.TypeId)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.FailureCode)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.SlotIndex)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.DuplicateSlotIndex)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void Append(",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal);
    }

    private static bool TestRunnerPreflightReady()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey key = Key();
        NativeAotClassConstructorKey secondKey = new NativeAotClassConstructorKey(ModuleId, TypeId + 1);

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTable.MarkInitialized(entries, key);
        NativeAotClassConstructorStateTable.Register(entries, secondKey);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 2, targetMetadataCount: 2);
        NativeAotClassConstructorRunnerPreflightFormatResult query =
            NativeAotClassConstructorRunnerPreflightDiagnosticFormatter.QueryRequiredBytes(in preflight);
        if (!query.Succeeded || query.RequiredBytes <= 0)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[query.RequiredBytes];
        NativeAotClassConstructorRunnerPreflightFormatResult format =
            NativeAotClassConstructorRunnerPreflightDiagnosticFormatter.FormatResult(in preflight, destination);
        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        return preflight.Succeeded
            && preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.Ready
            && preflight.TypeCount == 2
            && preflight.TargetMetadataCount == 2
            && preflight.OccupiedCount == 2
            && preflight.Capacity == 2
            && preflight.FirstProblemIndex == -1
            && !NativeAotClassConstructorRunnerPreflight.ExecutesConstructors
            && !NativeAotClassConstructorRunnerPreflight.UsesReflection
            && !NativeAotClassConstructorRunnerPreflight.UsesExceptionControlFlow
            && NativeAotClassConstructorRunnerPreflight.RequiresTargetMetadata
            && format.Succeeded
            && marker ==
                "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status=Ready initialized=true executesConstructors=false typeCount=2 targetMetadataCount=2 occupiedCount=2 capacity=2 firstProblemIndex=-1 firstProblemState=Uninitialized firstProblemFailureCode=0";
    }

    private static bool TestRunnerPreflightRejectsDefaultTable()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 0, targetMetadataCount: 0);
        NativeAotClassConstructorRunnerPreflightResult defaultResult = default;
        NativeAotClassConstructorRunnerPreflightFormatResult defaultFormatResult = default;

        NativeAotClassConstructorRunnerPreflightFormatResult invalidQuery =
            NativeAotClassConstructorRunnerPreflightDiagnosticFormatter.QueryRequiredBytes(in defaultResult);

        return !preflight.Succeeded
            && preflight.IsInitialized
            && preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.NotInitialized
            && preflight.OccupiedCount == 0
            && preflight.Capacity == 2
            && !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && !defaultFormatResult.IsInitialized
            && !defaultFormatResult.Succeeded
            && !invalidQuery.Succeeded
            && invalidQuery.Status == NativeAotClassConstructorRunnerPreflightFormatStatus.InvalidResult;
    }

    private static bool TestRunnerPreflightRejectsFailedState()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key();

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTable.MarkFailed(entries, key, FailureCode);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);

        return !preflight.Succeeded
            && preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor
            && preflight.FirstProblemIndex == 0
            && preflight.FirstProblemState == NativeAotClassConstructorState.Failed
            && preflight.FirstProblemFailureCode == FailureCode;
    }

    private static bool TestRunnerPreflightRejectsPendingState()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key();

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);

        return !preflight.Succeeded
            && preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor
            && preflight.FirstProblemIndex == 0
            && preflight.FirstProblemState == NativeAotClassConstructorState.Initializing;
    }

    private static bool TestRunnerPreflightRejectsMissingMetadata()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey key = Key();
        NativeAotClassConstructorKey secondKey = new NativeAotClassConstructorKey(ModuleId, TypeId + 1);

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.Register(entries, secondKey);

        NativeAotClassConstructorRunnerPreflightResult missingMetadata =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 2, targetMetadataCount: 1);
        NativeAotClassConstructorRunnerPreflightResult extraMetadata =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 2, targetMetadataCount: 3);
        NativeAotClassConstructorRunnerPreflightResult typeMismatch =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);

        return !missingMetadata.Succeeded
            && missingMetadata.Status == NativeAotClassConstructorRunnerPreflightStatus.MissingTargetMetadata
            && !extraMetadata.Succeeded
            && extraMetadata.Status == NativeAotClassConstructorRunnerPreflightStatus.TargetMetadataCountMismatch
            && !typeMismatch.Succeeded
            && typeMismatch.Status == NativeAotClassConstructorRunnerPreflightStatus.TypeCountMismatch
            && typeMismatch.OccupiedCount == 2;
    }

    private static bool TestRunnerPreflightFormatterReportsShortDestination()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorStateTable.Register(entries, Key());

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        NativeAotClassConstructorRunnerPreflightFormatResult query =
            NativeAotClassConstructorRunnerPreflightDiagnosticFormatter.QueryRequiredBytes(in preflight);
        if (!query.Succeeded || query.RequiredBytes <= 1)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[query.RequiredBytes - 1];
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = 0xA5;
        }

        NativeAotClassConstructorRunnerPreflightFormatResult format =
            NativeAotClassConstructorRunnerPreflightDiagnosticFormatter.FormatResult(
                in preflight,
                destination);

        return !format.Succeeded
            && format.Status == NativeAotClassConstructorRunnerPreflightFormatStatus.DestinationTooSmall
            && format.PreflightStatus == NativeAotClassConstructorRunnerPreflightStatus.Ready
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xA5);
    }

    private static bool TestRunnerPreflightFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Runtime",
                "NativeAotClassConstructorRunnerPreflight.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.TypeCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.TargetMetadataCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.OccupiedCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.Capacity)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.FirstProblemIndex)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt32Digits(result.FirstProblemFailureCode)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void Append(",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceCctorRunnerPreflightReady()
    {
        NativeAotClassConstructorRunnerPreflightResult preflight = CreateReadyPreflight();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker =
            BootSequence.CreateNativeAotClassConstructorRunnerPreflightReadyMarker(preflight);

        return preflight.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY status=Ready initialized=true preflight=Ready typeCount=2 targetMetadataCount=2 occupiedCount=2 capacity=2 firstProblemIndex=-1 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false requiresTargetMetadata=true";
    }

    private static bool TestBootSequenceCctorRunnerPreflightRejectsFailedResult()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key();
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTable.MarkFailed(entries, key, FailureCode);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker =
            BootSequence.CreateNativeAotClassConstructorRunnerPreflightNotReadyMarker(
                NativeAotClassConstructorRunnerPreflightReadinessStatus.PreflightFailed,
                preflight);

        return !preflight.Succeeded
            && preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAotClassConstructorRunnerPreflightReadinessStatus.PreflightFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_NOT_READY status=PreflightFailed initialized=true preflight=FailedConstructor typeCount=1 targetMetadataCount=1 occupiedCount=1 capacity=1 firstProblemIndex=0 firstProblemState=Failed firstProblemFailureCode=3235774465 executesConstructors=false usesReflection=false usesExceptionControlFlow=false requiresTargetMetadata=true";
    }

    private static bool TestBootSequenceCctorRunnerPreflightRejectsDefaultResult()
    {
        NativeAotClassConstructorRunnerPreflightResult preflight = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker =
            BootSequence.CreateNativeAotClassConstructorRunnerPreflightNotReadyMarker(
                NativeAotClassConstructorRunnerPreflightReadinessStatus.NotInitialized,
                preflight);

        return !preflight.IsInitialized
            && !preflight.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAotClassConstructorRunnerPreflightReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_NOT_READY status=NotInitialized initialized=false preflight=Ready typeCount=0 targetMetadataCount=0 occupiedCount=0 capacity=0 firstProblemIndex=0 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false requiresTargetMetadata=true";
    }

    private static bool TestBootSequenceCctorRunnerPreflightPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return false;
        }

        for (int index = 0; index < serial.Count; index++)
        {
            string line = serial.LineAt(index);
            if (line.StartsWith(
                    BootSequence.NativeAotClassConstructorRunnerPreflightReadyPrefix,
                    StringComparison.Ordinal) ||
                line.StartsWith(
                    BootSequence.NativeAotClassConstructorRunnerPreflightNotReadyPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static NativeAotClassConstructorRunnerPreflightResult CreateReadyPreflight()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey key = Key();
        NativeAotClassConstructorKey secondKey = new NativeAotClassConstructorKey(ModuleId, TypeId + 1);

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTable.MarkInitialized(entries, key);
        NativeAotClassConstructorStateTable.Register(entries, secondKey);

        return NativeAotClassConstructorRunnerPreflight.Validate(
            entries,
            typeCount: 2,
            targetMetadataCount: 2);
    }

    private static NativeAotClassConstructorStateTableResult CreateInitializedResult()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey key = Key();

        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        return NativeAotClassConstructorStateTable.MarkInitialized(entries, key);
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> value, byte expected)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static NativeAotClassConstructorKey Key()
    {
        return new NativeAotClassConstructorKey(ModuleId, TypeId);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }
    }
}
