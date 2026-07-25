using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;

internal static class NativeAotHeapBootstrapPlanTests
{
    private const ulong PageSize = 4096;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("runtime.nativeaot heap bootstrap plans page budget", TestPlansPageBudget, ref failures);
        Run("runtime.nativeaot heap bootstrap rejects allocator state", TestRejectsAllocatorState, ref failures);
        Run("runtime.nativeaot heap bootstrap rejects allocator byte metadata", TestRejectsAllocatorByteMetadata, ref failures);
        Run("runtime.nativeaot heap bootstrap rejects page budget", TestRejectsPageBudget, ref failures);
        Run("runtime.nativeaot heap bootstrap rejects object alignment", TestRejectsObjectAlignment, ref failures);
        Run("runtime.nativeaot heap bootstrap layout contains no managed references", TestLayoutContainsNoManagedReferences, ref failures);
        Run("runtime.nativeaot heap bootstrap default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("boot.sequence nativeaot heap bootstrap ready", TestBootSequenceNativeAotHeapBootstrapReady, ref failures);
        Run("boot.sequence nativeaot heap bootstrap rejects failed plan", TestBootSequenceNativeAotHeapBootstrapRejectsFailedPlan, ref failures);
        Run("boot.sequence nativeaot heap bootstrap blocks default result", TestBootSequenceNativeAotHeapBootstrapBlocksDefaultResult, ref failures);
        Run("boot.sequence nativeaot heap bootstrap preserves default markers", TestBootSequenceNativeAotHeapBootstrapPreservesDefaultMarkers, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("NativeAOT heap bootstrap plan tests failed: " + failures);
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

    private static bool TestPlansPageBudget()
    {
        BootPageAllocatorState allocatorState = ReadyState(freePages: 16);

        NativeAotHeapBootstrapPlanResult result = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 4,
            objectAlignment: 16);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAotHeapBootstrapPlanStatus.Success
            && result.PageSize == PageSize
            && result.AllocatorFreePages == 16
            && result.AllocatorFreeBytes == 16 * PageSize
            && result.ExpectedAllocatorFreeBytes == 16 * PageSize
            && result.RequestedHeapPages == 4
            && result.RequestedHeapBytes == 4 * PageSize
            && result.RemainingFreePages == 12
            && result.RemainingFreeBytes == 12 * PageSize
            && result.ObjectAlignment == 16
            && result.RequiresPageAllocatorMutation
            && result.RequiresGcRootScanning
            && result.RequiresWriteBarriers
            && !result.SatisfiesObjectAllocationHelpers;
    }

    private static bool TestRejectsAllocatorState()
    {
        NativeAotHeapBootstrapPlanResult uninitialized = NativeAotHeapBootstrapPlanner.Plan(
            default,
            requestedHeapPages: 1,
            objectAlignment: 16);

        NativeAotHeapBootstrapPlanResult invalidPageSize = NativeAotHeapBootstrapPlanner.Plan(
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: 3000,
                headNodeIndex: 0,
                freeNodeCount: 1,
                freePageCount: 1,
                freeByteCount: 3000,
                allocatedPageCount: 0,
                allocatedByteCount: 0),
            requestedHeapPages: 1,
            objectAlignment: 16);

        NativeAotHeapBootstrapPlanResult noFreePages = NativeAotHeapBootstrapPlanner.Plan(
            ReadyState(freePages: 0),
            requestedHeapPages: 1,
            objectAlignment: 16);

        return !uninitialized.Succeeded
            && uninitialized.IsInitialized
            && uninitialized.Status == NativeAotHeapBootstrapPlanStatus.AllocatorStateNotInitialized
            && uninitialized.RequestedHeapPages == 1
            && !invalidPageSize.Succeeded
            && invalidPageSize.Status == NativeAotHeapBootstrapPlanStatus.InvalidPageSize
            && !noFreePages.Succeeded
            && noFreePages.Status == NativeAotHeapBootstrapPlanStatus.NoFreePages;
    }

    private static bool TestRejectsAllocatorByteMetadata()
    {
        NativeAotHeapBootstrapPlanResult overflow = NativeAotHeapBootstrapPlanner.Plan(
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: 1UL << 63,
                headNodeIndex: 0,
                freeNodeCount: 1,
                freePageCount: 2,
                freeByteCount: ulong.MaxValue,
                allocatedPageCount: 0,
                allocatedByteCount: 0),
            requestedHeapPages: 1,
            objectAlignment: 16);

        NativeAotHeapBootstrapPlanResult mismatch = NativeAotHeapBootstrapPlanner.Plan(
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: PageSize,
                headNodeIndex: 0,
                freeNodeCount: 1,
                freePageCount: 4,
                freeByteCount: (4 * PageSize) - 1,
                allocatedPageCount: 0,
                allocatedByteCount: 0),
            requestedHeapPages: 1,
            objectAlignment: 16);

        return !overflow.Succeeded
            && overflow.Status == NativeAotHeapBootstrapPlanStatus.AllocatorFreeByteCountOverflow
            && overflow.ExpectedAllocatorFreeBytes == 0
            && !mismatch.Succeeded
            && mismatch.Status == NativeAotHeapBootstrapPlanStatus.AllocatorFreeByteCountMismatch
            && mismatch.ExpectedAllocatorFreeBytes == 4 * PageSize
            && mismatch.AllocatorFreeBytes == (4 * PageSize) - 1;
    }

    private static bool TestRejectsPageBudget()
    {
        BootPageAllocatorState allocatorState = ReadyState(freePages: 4);

        NativeAotHeapBootstrapPlanResult zeroPages = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 0,
            objectAlignment: 16);

        NativeAotHeapBootstrapPlanResult tooManyPages = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 5,
            objectAlignment: 16);

        return !zeroPages.Succeeded
            && zeroPages.Status == NativeAotHeapBootstrapPlanStatus.RequestedHeapPagesZero
            && !zeroPages.SatisfiesObjectAllocationHelpers
            && !tooManyPages.Succeeded
            && tooManyPages.Status == NativeAotHeapBootstrapPlanStatus.RequestedHeapPagesExceedFreePages
            && tooManyPages.RequestedHeapPages == 5
            && tooManyPages.AllocatorFreePages == 4;
    }

    private static bool TestRejectsObjectAlignment()
    {
        BootPageAllocatorState allocatorState = ReadyState(freePages: 4);

        NativeAotHeapBootstrapPlanResult zeroAlignment = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 1,
            objectAlignment: 0);

        NativeAotHeapBootstrapPlanResult nonPowerOfTwo = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 1,
            objectAlignment: 24);

        NativeAotHeapBootstrapPlanResult tooSmall = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 1,
            objectAlignment: 4);

        NativeAotHeapBootstrapPlanResult tooLarge = NativeAotHeapBootstrapPlanner.Plan(
            allocatorState,
            requestedHeapPages: 1,
            objectAlignment: PageSize * 2);

        return !zeroAlignment.Succeeded
            && zeroAlignment.Status == NativeAotHeapBootstrapPlanStatus.InvalidObjectAlignment
            && !nonPowerOfTwo.Succeeded
            && nonPowerOfTwo.Status == NativeAotHeapBootstrapPlanStatus.InvalidObjectAlignment
            && !tooSmall.Succeeded
            && tooSmall.Status == NativeAotHeapBootstrapPlanStatus.ObjectAlignmentTooSmall
            && !tooLarge.Succeeded
            && tooLarge.Status == NativeAotHeapBootstrapPlanStatus.ObjectAlignmentExceedsPageSize;
    }

    private static bool TestLayoutContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotHeapBootstrapPlanResult>();
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAotHeapBootstrapPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAotHeapBootstrapPlanStatus.Success
            && !result.RequiresPageAllocatorMutation
            && !result.RequiresGcRootScanning
            && !result.RequiresWriteBarriers
            && !result.SatisfiesObjectAllocationHelpers;
    }

    private static bool TestBootSequenceNativeAotHeapBootstrapReady()
    {
        NativeAotHeapBootstrapPlanResult plan = NativeAotHeapBootstrapPlanner.Plan(
            ReadyState(freePages: 16),
            requestedHeapPages: 4,
            objectAlignment: 16);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotHeapBootstrapReadiness(plan, serial, events);
        string expectedMarker = BootSequence.CreateNativeAotHeapBootstrapReadyMarker(plan);

        return plan.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "NATIVEAOT_HEAP_BOOTSTRAP_READY status=Ready initialized=true plan=Success pageSize=4096 allocatorFreePages=16 allocatorFreeBytes=65536 expectedAllocatorFreeBytes=65536 requestedHeapPages=4 requestedHeapBytes=16384 remainingFreePages=12 remainingFreeBytes=49152 objectAlignment=16 requiresPageAllocatorMutation=true requiresGcRootScanning=true requiresWriteBarriers=true satisfiesObjectAllocationHelpers=false";
    }

    private static bool TestBootSequenceNativeAotHeapBootstrapRejectsFailedPlan()
    {
        NativeAotHeapBootstrapPlanResult plan = NativeAotHeapBootstrapPlanner.Plan(
            ReadyState(freePages: 4),
            requestedHeapPages: 0,
            objectAlignment: 16);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotHeapBootstrapReadiness(plan, serial, events);
        string expectedMarker = BootSequence.CreateNativeAotHeapBootstrapNotReadyMarker(
            NativeAotHeapBootstrapReadinessStatus.PlanFailed,
            plan);

        return !plan.Succeeded
            && plan.Status == NativeAotHeapBootstrapPlanStatus.RequestedHeapPagesZero
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAotHeapBootstrapReadinessStatus.PlanFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "NATIVEAOT_HEAP_BOOTSTRAP_NOT_READY status=PlanFailed initialized=true plan=RequestedHeapPagesZero pageSize=4096 allocatorFreePages=4 allocatorFreeBytes=16384 expectedAllocatorFreeBytes=16384 requestedHeapPages=0 requestedHeapBytes=0 remainingFreePages=0 remainingFreeBytes=0 objectAlignment=16 requiresPageAllocatorMutation=false requiresGcRootScanning=false requiresWriteBarriers=false satisfiesObjectAllocationHelpers=false";
    }

    private static bool TestBootSequenceNativeAotHeapBootstrapBlocksDefaultResult()
    {
        NativeAotHeapBootstrapPlanResult plan = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAotHeapBootstrapReadiness(plan, serial, events);
        string expectedMarker = BootSequence.CreateNativeAotHeapBootstrapNotReadyMarker(
            NativeAotHeapBootstrapReadinessStatus.NotInitialized,
            plan);

        return !plan.IsInitialized
            && !plan.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAotHeapBootstrapReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "NATIVEAOT_HEAP_BOOTSTRAP_NOT_READY status=NotInitialized initialized=false plan=Success pageSize=0 allocatorFreePages=0 allocatorFreeBytes=0 expectedAllocatorFreeBytes=0 requestedHeapPages=0 requestedHeapBytes=0 remainingFreePages=0 remainingFreeBytes=0 objectAlignment=0 requiresPageAllocatorMutation=false requiresGcRootScanning=false requiresWriteBarriers=false satisfiesObjectAllocationHelpers=false";
    }

    private static bool TestBootSequenceNativeAotHeapBootstrapPreservesDefaultMarkers()
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
            if (line.StartsWith(BootSequence.NativeAotHeapBootstrapReadyPrefix, StringComparison.Ordinal)
                || line.StartsWith(BootSequence.NativeAotHeapBootstrapNotReadyPrefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static BootPageAllocatorState ReadyState(ulong freePages)
    {
        return new BootPageAllocatorState(
            isInitialized: true,
            PageSize,
            headNodeIndex: 0,
            freeNodeCount: freePages == 0 ? 0 : 1,
            freePages,
            freePages * PageSize,
            allocatedPageCount: 0,
            allocatedByteCount: 0);
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
