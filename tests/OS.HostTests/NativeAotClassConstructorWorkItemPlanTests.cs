using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class NativeAotClassConstructorWorkItemPlanTests
{
    private const uint ModuleId = 11;
    private const uint TypeId = 101;
    private const uint FailureCode = 0xC0DE1001;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("runtime.cctor work item plans uninitialized entries", TestPlansUninitializedEntries, ref failures);
        Run("runtime.cctor work item skips initialized entries", TestSkipsInitializedEntries, ref failures);
        Run("runtime.cctor work item rejects missing target token", TestRejectsMissingTargetToken, ref failures);
        Run("runtime.cctor work item rejects missing target metadata", TestRejectsMissingTargetMetadata, ref failures);
        Run("runtime.cctor work item reports destination capacity", TestReportsDestinationCapacity, ref failures);
        Run("runtime.cctor work item rejects failed preflight", TestRejectsFailedPreflight, ref failures);
        Run("runtime.cctor work item rejects pending preflight", TestRejectsPendingPreflight, ref failures);
        Run("runtime.cctor work item rejects default preflight", TestRejectsDefaultPreflight, ref failures);
        Run("runtime.cctor work item formatter formats success marker", TestFormatterFormatsSuccessMarker, ref failures);
        Run("runtime.cctor work item formatter formats failure marker", TestFormatterFormatsFailureMarker, ref failures);
        Run("runtime.cctor work item formatter reports short destination", TestFormatterReportsShortDestination, ref failures);
        Run("runtime.cctor work item formatter rejects default result", TestFormatterRejectsDefaultResult, ref failures);
        Run(
            "runtime.cctor work item formatter uses shared ascii text builder",
            TestFormatterUsesSharedAsciiTextBuilder,
            ref failures);
        Run("boot.sequence cctor work item ready", TestBootSequenceCctorWorkItemReady, ref failures);
        Run(
            "boot.sequence cctor work item rejects failed result",
            TestBootSequenceCctorWorkItemRejectsFailedResult,
            ref failures);
        Run(
            "boot.sequence cctor work item rejects default result",
            TestBootSequenceCctorWorkItemRejectsDefaultResult,
            ref failures);
        Run(
            "boot.sequence cctor work item preserves default markers",
            TestBootSequenceCctorWorkItemPreservesDefaultMarkers,
            ref failures);
        Run("runtime.cctor work item layouts contain no managed references", TestLayoutsContainNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("NativeAOT class constructor work item plan tests failed: " + failures);
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

    private static bool TestPlansUninitializedEntries()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[3];
        NativeAotClassConstructorKey first = Key(0);
        NativeAotClassConstructorKey second = Key(1);
        NativeAotClassConstructorKey third = Key(2);

        NativeAotClassConstructorStateTable.Register(entries, first);
        NativeAotClassConstructorStateTable.Register(entries, second);
        NativeAotClassConstructorStateTable.TryBegin(entries, second);
        NativeAotClassConstructorStateTable.MarkInitialized(entries, second);
        NativeAotClassConstructorStateTable.Register(entries, third);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 3, targetMetadataCount: 3);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[3];
        targets[0] = Target(first, 0x1000);
        targets[1] = Target(second, 0x2000);
        targets[2] = Target(third, 0x3000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[2];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return preflight.Succeeded
            && result.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.Ready
            && result.PreflightStatus == NativeAotClassConstructorRunnerPreflightStatus.Ready
            && result.TypeCount == 3
            && result.TargetMetadataCount == 3
            && result.PlannedCount == 2
            && result.SkippedInitializedCount == 1
            && result.RequiredWorkItemCapacity == 2
            && result.AvailableWorkItemCapacity == 2
            && result.FirstProblemIndex == -1
            && workItems[0].IsValid
            && workItems[0].Key.Equals(first)
            && workItems[0].TargetToken == (nuint)0x1000
            && workItems[0].StateSlotIndex == 0
            && workItems[1].IsValid
            && workItems[1].Key.Equals(third)
            && workItems[1].TargetToken == (nuint)0x3000
            && workItems[1].StateSlotIndex == 2
            && NativeAotClassConstructorWorkItemPlanner.UsesCallerOwnedStorage
            && !NativeAotClassConstructorWorkItemPlanner.ExecutesConstructors
            && !NativeAotClassConstructorWorkItemPlanner.UsesManagedDelegates
            && !NativeAotClassConstructorWorkItemPlanner.DiscoversMetadata;
    }

    private static bool TestSkipsInitializedEntries()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key(0);
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTable.MarkInitialized(entries, key);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        targets[0] = Target(key, 0x1000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[0];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return result.Succeeded
            && result.PlannedCount == 0
            && result.SkippedInitializedCount == 1
            && result.RequiredWorkItemCapacity == 0
            && result.AvailableWorkItemCapacity == 0;
    }

    private static bool TestRejectsMissingTargetToken()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key(0);
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        targets[0] = Target(key, 0);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return !result.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.MissingTargetToken
            && result.FirstProblemIndex == 0
            && result.FirstProblemState == NativeAotClassConstructorState.Uninitialized
            && result.RequiredWorkItemCapacity == 1
            && result.AvailableWorkItemCapacity == 1
            && !workItems[0].IsValid;
    }

    private static bool TestRejectsMissingTargetMetadata()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key(0);
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        targets[0] = Target(Key(10), 0x9000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return !result.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.MissingTargetMetadata
            && result.FirstProblemIndex == 0
            && result.FirstProblemState == NativeAotClassConstructorState.Uninitialized
            && result.PlannedCount == 0
            && result.SkippedInitializedCount == 0;
    }

    private static bool TestReportsDestinationCapacity()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey first = Key(0);
        NativeAotClassConstructorKey second = Key(1);
        NativeAotClassConstructorStateTable.Register(entries, first);
        NativeAotClassConstructorStateTable.Register(entries, second);
        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 2, targetMetadataCount: 2);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[2];
        targets[0] = Target(first, 0x1000);
        targets[1] = Target(second, 0x2000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return !result.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.DestinationTooSmall
            && result.PlannedCount == 1
            && result.RequiredWorkItemCapacity == 2
            && result.AvailableWorkItemCapacity == 1
            && result.FirstProblemIndex == 1
            && workItems[0].IsValid
            && workItems[0].Key.Equals(first)
            && workItems[0].TargetToken == (nuint)0x1000;
    }

    private static bool TestRejectsFailedPreflight()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key(0);
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorStateTable.MarkFailed(entries, key, FailureCode);
        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        targets[0] = Target(key, 0x1000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return !preflight.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.FailedConstructor
            && result.PreflightStatus == NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor
            && result.FirstProblemIndex == 0
            && result.FirstProblemState == NativeAotClassConstructorState.Failed
            && result.FirstProblemFailureCode == FailureCode
            && result.PlannedCount == 0;
    }

    private static bool TestRejectsPendingPreflight()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key(0);
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorStateTable.TryBegin(entries, key);
        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        targets[0] = Target(key, 0x1000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);

        return !preflight.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.PendingOrReentrantConstructor
            && result.PreflightStatus == NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor
            && result.FirstProblemIndex == 0
            && result.FirstProblemState == NativeAotClassConstructorState.Initializing
            && result.PlannedCount == 0;
    }

    private static bool TestRejectsDefaultPreflight()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];
        NativeAotClassConstructorWorkItemPlanResult defaultResult = default;

        NativeAotClassConstructorWorkItemPlanResult result =
            NativeAotClassConstructorWorkItemPlanner.Create(entries, default, targets, workItems);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAotClassConstructorWorkItemPlanStatus.NotInitialized
            && result.PreflightStatus == NativeAotClassConstructorRunnerPreflightStatus.Ready
            && result.FirstProblemIndex == 0
            && result.AvailableWorkItemCapacity == 1;
    }

    private static bool TestLayoutsContainNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorTargetMetadata>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorWorkItem>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotClassConstructorWorkItemPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult>()
            && NativeAotClassConstructorWorkItemPlanner.UsesCallerOwnedStorage
            && !NativeAotClassConstructorWorkItemPlanner.ExecutesConstructors
            && !NativeAotClassConstructorWorkItemPlanner.UsesReflection
            && !NativeAotClassConstructorWorkItemPlanner.UsesExceptionControlFlow
            && !NativeAotClassConstructorWorkItemPlanner.UsesManagedDelegates
            && !NativeAotClassConstructorWorkItemPlanner.DiscoversMetadata;
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        NativeAotClassConstructorWorkItemPlanResult result = CreateReadyPlan();
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult query =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.QueryRequiredBytes(in result);
        Span<byte> destination = stackalloc byte[query.RequiredBytes];

        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult format =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.FormatResult(in result, destination);
        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        const string Expected =
            "NATIVEAOT_CCTOR_WORK_ITEMS status=Ready initialized=true preflight=Ready typeCount=2 targetMetadataCount=2 plannedCount=1 skippedInitializedCount=1 requiredWorkItemCapacity=1 availableWorkItemCapacity=1 firstProblemIndex=-1 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false usesManagedDelegates=false discoversMetadata=false";

        return query.Succeeded
            && query.BytesWritten == 0
            && format.Succeeded
            && format.Status == NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success
            && format.PlanStatus == NativeAotClassConstructorWorkItemPlanStatus.Ready
            && format.BytesWritten == query.RequiredBytes
            && format.RequiredBytes == query.RequiredBytes
            && marker == Expected;
    }

    private static bool TestFormatterFormatsFailureMarker()
    {
        NativeAotClassConstructorWorkItemPlanResult result = CreateMissingTargetTokenPlan();
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult query =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.QueryRequiredBytes(in result);
        Span<byte> destination = stackalloc byte[query.RequiredBytes];

        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult format =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.FormatResult(in result, destination);
        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        const string Expected =
            "NATIVEAOT_CCTOR_WORK_ITEMS status=MissingTargetToken initialized=true preflight=Ready typeCount=1 targetMetadataCount=1 plannedCount=0 skippedInitializedCount=0 requiredWorkItemCapacity=1 availableWorkItemCapacity=1 firstProblemIndex=0 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false usesManagedDelegates=false discoversMetadata=false";

        return !result.Succeeded
            && query.Succeeded
            && format.Succeeded
            && format.PlanStatus == NativeAotClassConstructorWorkItemPlanStatus.MissingTargetToken
            && marker == Expected;
    }

    private static bool TestFormatterReportsShortDestination()
    {
        NativeAotClassConstructorWorkItemPlanResult result = CreateReadyPlan();
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult query =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.QueryRequiredBytes(in result);
        Span<byte> destination = stackalloc byte[query.RequiredBytes - 1];
        destination.Fill(0xCC);

        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult format =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.FormatResult(in result, destination);

        bool preserved = true;
        for (int index = 0; index < destination.Length; index++)
        {
            preserved &= destination[index] == 0xCC;
        }

        return query.Succeeded
            && !format.Succeeded
            && format.Status == NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.DestinationTooSmall
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && preserved;
    }

    private static bool TestFormatterRejectsDefaultResult()
    {
        NativeAotClassConstructorWorkItemPlanResult defaultResult = default;
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult defaultFormat = default;
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xAA);

        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult query =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.QueryRequiredBytes(in defaultResult);
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult format =
            NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.FormatResult(in defaultResult, destination);

        bool preserved = true;
        for (int index = 0; index < destination.Length; index++)
        {
            preserved &= destination[index] == 0xAA;
        }

        return !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && !query.Succeeded
            && query.Status == NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.InvalidResult
            && !format.Succeeded
            && format.Status == NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.InvalidResult
            && format.BytesWritten == 0
            && preserved;
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
                "NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.TypeCount)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.TargetMetadataCount)",
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
                "private static int GetInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt32DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt32",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal);
    }

    private static bool TestBootSequenceCctorWorkItemReady()
    {
        NativeAotClassConstructorWorkItemPlanResult plan = CreateReadyPlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotClassConstructorWorkItemReadiness(
            plan,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAotClassConstructorWorkItemReadyMarker(plan);

        return plan.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVEAOT_CCTOR_WORK_ITEMS_READY status=Ready initialized=true plan=Ready preflight=Ready typeCount=2 targetMetadataCount=2 plannedCount=1 skippedInitializedCount=1 requiredWorkItemCapacity=1 availableWorkItemCapacity=1 firstProblemIndex=-1 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false usesManagedDelegates=false discoversMetadata=false"
            && expectedMarker.Contains("executesConstructors=false", StringComparison.Ordinal)
            && expectedMarker.Contains("usesReflection=false", StringComparison.Ordinal)
            && expectedMarker.Contains("usesManagedDelegates=false", StringComparison.Ordinal)
            && expectedMarker.Contains("discoversMetadata=false", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceCctorWorkItemRejectsFailedResult()
    {
        NativeAotClassConstructorWorkItemPlanResult plan = CreateMissingTargetTokenPlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotClassConstructorWorkItemReadiness(
            plan,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAotClassConstructorWorkItemNotReadyMarker(
            NativeAotClassConstructorWorkItemReadinessStatus.PlanFailed,
            plan);

        return !plan.Succeeded
            && plan.Status == NativeAotClassConstructorWorkItemPlanStatus.MissingTargetToken
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAotClassConstructorWorkItemReadinessStatus.PlanFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAotClassConstructorWorkItemPlanStatus.MissingTargetToken),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY status=PlanFailed initialized=true plan=MissingTargetToken preflight=Ready typeCount=1 targetMetadataCount=1 plannedCount=0 skippedInitializedCount=0 requiredWorkItemCapacity=1 availableWorkItemCapacity=1 firstProblemIndex=0 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false usesManagedDelegates=false discoversMetadata=false";
    }

    private static bool TestBootSequenceCctorWorkItemRejectsDefaultResult()
    {
        NativeAotClassConstructorWorkItemPlanResult plan = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotClassConstructorWorkItemReadiness(
            plan,
            serial,
            events);
        string expectedMarker = BootSequence.CreateNativeAotClassConstructorWorkItemNotReadyMarker(
            NativeAotClassConstructorWorkItemReadinessStatus.NotInitialized,
            plan);

        return !plan.IsInitialized
            && !plan.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAotClassConstructorWorkItemReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY status=NotInitialized initialized=false plan=Ready preflight=Ready typeCount=0 targetMetadataCount=0 plannedCount=0 skippedInitializedCount=0 requiredWorkItemCapacity=0 availableWorkItemCapacity=0 firstProblemIndex=0 firstProblemState=Uninitialized firstProblemFailureCode=0 executesConstructors=false usesReflection=false usesExceptionControlFlow=false usesManagedDelegates=false discoversMetadata=false";
    }

    private static bool TestBootSequenceCctorWorkItemPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return false;
        }

        return !serial.ContainsPrefix(BootSequence.NativeAotClassConstructorWorkItemReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.NativeAotClassConstructorWorkItemNotReadyPrefix);
    }

    private static NativeAotClassConstructorWorkItemPlanResult CreateReadyPlan()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[2];
        NativeAotClassConstructorKey first = Key(0);
        NativeAotClassConstructorKey second = Key(1);
        NativeAotClassConstructorStateTable.Register(entries, first);
        NativeAotClassConstructorStateTable.Register(entries, second);
        NativeAotClassConstructorStateTable.TryBegin(entries, second);
        NativeAotClassConstructorStateTable.MarkInitialized(entries, second);

        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 2, targetMetadataCount: 2);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[2];
        targets[0] = Target(first, 0x1000);
        targets[1] = Target(second, 0x2000);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        return NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);
    }

    private static NativeAotClassConstructorWorkItemPlanResult CreateMissingTargetTokenPlan()
    {
        Span<NativeAotClassConstructorStateEntry> entries =
            stackalloc NativeAotClassConstructorStateEntry[1];
        NativeAotClassConstructorKey key = Key(0);
        NativeAotClassConstructorStateTable.Register(entries, key);
        NativeAotClassConstructorRunnerPreflightResult preflight =
            NativeAotClassConstructorRunnerPreflight.Validate(entries, typeCount: 1, targetMetadataCount: 1);
        Span<NativeAotClassConstructorTargetMetadata> targets =
            stackalloc NativeAotClassConstructorTargetMetadata[1];
        targets[0] = Target(key, 0);
        Span<NativeAotClassConstructorWorkItem> workItems =
            stackalloc NativeAotClassConstructorWorkItem[1];

        return NativeAotClassConstructorWorkItemPlanner.Create(entries, preflight, targets, workItems);
    }

    private static NativeAotClassConstructorKey Key(uint offset)
    {
        return new NativeAotClassConstructorKey(ModuleId, TypeId + offset);
    }

    private static NativeAotClassConstructorTargetMetadata Target(
        NativeAotClassConstructorKey key,
        nuint targetToken)
    {
        return new NativeAotClassConstructorTargetMetadata(key, targetToken);
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

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
