using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalThreadTableTests
{

    [HostTest("pal.thread table creates joinable thread", HostLane.Fast, 0)]
    private static bool TestCreatesJoinableThread()
    {
        OSHostThreadTable table = new(capacity: 1);
        OSHostThreadCreateResult create = table.Create(CreateOptions());
        if (!create.Succeeded ||
            create.ThreadId != OSHostThreadConstants.FirstCreatedThreadId ||
            table.TryGetEntry(create.Thread, out OSHostThreadTableEntry entry) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostThreadJoinResult beforeExit = table.Join(create.Thread);
        OSHostStatus exitStatus = table.MarkExited(create.Thread, exitCode: 42);
        OSHostThreadJoinResult afterExit = table.Join(create.Thread);

        return HostCheck.Result(entry.State == OSHostThreadLifecycleState.Created
            && entry.Options.Start.EntryPoint == 0x4000
            && beforeExit.Status == OSHostStatus.WouldBlock
            && !beforeExit.Succeeded
            && exitStatus == OSHostStatus.Ok
            && afterExit.Succeeded
            && afterExit.ExitCode == 42
            && table.Join(create.Thread).Status == OSHostStatus.InvalidArgument
            && table.ActiveCount == 0);
    }

    [HostTest("pal.thread table rejects invalid create options", HostLane.Fast, 1)]
    private static bool TestRejectsInvalidCreateOptions()
    {
        OSHostThreadTable table = new(capacity: 1);

        return HostCheck.Result(table.Create(new OSHostThreadCreateOptions(
                version: 0,
                flags: 0,
                stackSizeBytes: 4096,
                guardSizeBytes: 4096,
                nameUtf8: default,
                start: new OSHostThreadStart(entryPoint: 0x4000, context: 0))).Status == OSHostStatus.InvalidArgument
            && table.Create(new OSHostThreadCreateOptions(
                OSHostThreadConstants.CreateOptionsVersion,
                flags: 1,
                stackSizeBytes: 4096,
                guardSizeBytes: 4096,
                nameUtf8: default,
                start: new OSHostThreadStart(entryPoint: 0x4000, context: 0))).Status == OSHostStatus.InvalidArgument
            && table.Create(new OSHostThreadCreateOptions(
                OSHostThreadConstants.CreateOptionsVersion,
                flags: 0,
                stackSizeBytes: 0,
                guardSizeBytes: 0,
                nameUtf8: default,
                start: new OSHostThreadStart(entryPoint: 0x4000, context: 0))).Status == OSHostStatus.InvalidArgument
            && table.Create(new OSHostThreadCreateOptions(
                OSHostThreadConstants.CreateOptionsVersion,
                flags: 0,
                stackSizeBytes: 4096,
                guardSizeBytes: 8192,
                nameUtf8: default,
                start: new OSHostThreadStart(entryPoint: 0x4000, context: 0))).Status == OSHostStatus.InvalidArgument
            && table.Create(new OSHostThreadCreateOptions(
                OSHostThreadConstants.CreateOptionsVersion,
                flags: 0,
                stackSizeBytes: 4096,
                guardSizeBytes: 4096,
                nameUtf8: default,
                start: new OSHostThreadStart(entryPoint: 0, context: 0))).Status == OSHostStatus.InvalidArgument
            && table.Create(new OSHostThreadCreateOptions(
                OSHostThreadConstants.CreateOptionsVersion,
                flags: 0,
                stackSizeBytes: 4096,
                guardSizeBytes: 4096,
                nameUtf8: new OSHostByteSpan(address: 0, length: 4),
                start: new OSHostThreadStart(entryPoint: 0x4000, context: 0))).Status == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.thread table reports exhaustion and reuse", HostLane.Fast, 2)]
    private static bool TestReportsExhaustionAndReuse()
    {
        OSHostThreadTable table = new(capacity: 1);
        OSHostThreadCreateResult first = table.Create(CreateOptions());
        OSHostThreadCreateResult exhausted = table.Create(CreateOptions(entryPoint: 0x5000));
        if (!first.Succeeded ||
            exhausted.Status != OSHostStatus.OutOfMemory ||
            table.MarkExited(first.Thread, exitCode: 7) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostThreadJoinResult joined = table.Join(first.Thread);
        OSHostThreadCreateResult reused = table.Create(CreateOptions(entryPoint: 0x6000));

        return HostCheck.Result(joined.Succeeded
            && joined.ExitCode == 7
            && reused.Succeeded
            && reused.Thread.Value != first.Thread.Value
            && reused.ThreadId == OSHostThreadConstants.FirstCreatedThreadId + 1
            && table.MarkExited(first.Thread, exitCode: 8) == OSHostStatus.InvalidArgument
            && table.TryGetEntry(reused.Thread, out OSHostThreadTableEntry entry) == OSHostStatus.Ok
            && entry.Options.Start.EntryPoint == 0x6000);
    }

    [HostTest("pal.thread table current id and yield are stable", HostLane.Fast, 4)]
    private static bool TestCurrentIdAndYieldAreStable()
    {
        OSHostThreadTable table = new(capacity: 0);
        OSHostThreadIdResult current = table.GetCurrentThreadId();

        return HostCheck.Result(current.Succeeded
            && current.ThreadId == OSHostThreadConstants.CurrentThreadId
            && table.Yield() == OSHostStatus.Ok);
    }

    [HostTest("pal.thread table reports early mode capabilities", HostLane.Fast, 3)]
    private static bool TestReportsEarlyModeCapabilities()
    {
        OSHostThreadTable table = new(capacity: 2);
        OSHostThreadCapabilityResult initial = table.GetCapabilities();
        OSHostThreadCreateResult create = table.Create(CreateOptions());
        OSHostThreadCapabilityResult afterCreate = table.GetCapabilities();

        return HostCheck.Result(initial.Succeeded
            && !initial.ReadyForHostedRuntime
            && initial.MaxThreadCount == 2
            && initial.ActiveThreadCount == 0
            && initial.HasCapability(OSHostThreadConstants.CapabilityCurrentThreadId)
            && initial.HasCapability(OSHostThreadConstants.CapabilityYield)
            && initial.HasCapability(OSHostThreadConstants.CapabilityMockThreadLifecycle)
            && initial.HasCapability(OSHostThreadConstants.CapabilityImmediateSleep)
            && initial.HasCapability(OSHostThreadConstants.CapabilitySingleThreadEarlyMode)
            && create.Succeeded
            && afterCreate.Succeeded
            && afterCreate.ActiveThreadCount == 1
            && afterCreate.MaxThreadCount == 2
            && !afterCreate.ReadyForHostedRuntime);
    }

    [HostTest("pal.thread table sleep reports unsupported blocking waits", HostLane.Fast, 5)]
    private static bool TestSleepReportsUnsupportedBlockingWaits()
    {
        OSHostThreadTable table = new(capacity: 0);
        OSHostThreadSleepResult immediate = table.Sleep(OSHostSocketTimeouts.Immediate);
        OSHostThreadSleepResult infinite = table.Sleep(OSHostSocketTimeouts.Infinite);
        OSHostThreadSleepResult relative = table.Sleep(new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindRelativeTicks,
            flags: 0,
            ticks: 1,
            tickFrequency: 1_000));
        OSHostThreadSleepResult absolute = table.Sleep(new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindAbsoluteTicks,
            flags: 0,
            ticks: 1,
            tickFrequency: 1_000));

        return HostCheck.Result(immediate.Succeeded
            && immediate.Status == OSHostStatus.Ok
            && immediate.TimeoutKind == OSHostSocketConstants.TimeoutKindImmediate
            && immediate.CompletedSynchronously
            && infinite.Status == OSHostStatus.NotSupported
            && !infinite.Succeeded
            && infinite.TimeoutKind == OSHostSocketConstants.TimeoutKindInfinite
            && relative.Status == OSHostStatus.NotSupported
            && !relative.Succeeded
            && absolute.Status == OSHostStatus.NotSupported
            && !absolute.Succeeded);
    }

    [HostTest("pal.thread table rejects invalid timeout and handles", HostLane.Fast, 6)]
    private static bool TestRejectsInvalidTimeoutAndHandles()
    {
        OSHostThreadTable table = new(capacity: 1);
        OSHostThreadSleepResult invalidKind = table.Sleep(new OSHostTimeout(
            kind: 99,
            flags: 0,
            ticks: 0,
            tickFrequency: 0));
        OSHostThreadSleepResult invalidFlags = table.Sleep(new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindImmediate,
            flags: 1,
            ticks: 0,
            tickFrequency: 0));
        OSHostThreadSleepResult invalidFrequency = table.Sleep(new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindRelativeTicks,
            flags: 0,
            ticks: 1,
            tickFrequency: 0));

        return HostCheck.Result(invalidKind.Status == OSHostStatus.InvalidArgument
            && invalidKind.TimeoutKind == 99
            && !invalidKind.Succeeded
            && invalidFlags.Status == OSHostStatus.InvalidArgument
            && invalidFrequency.Status == OSHostStatus.InvalidArgument
            && table.MarkExited(default, exitCode: 0) == OSHostStatus.InvalidArgument
            && table.Join(default).Status == OSHostStatus.InvalidArgument
            && table.TryGetEntry(default, out OSHostThreadTableEntry entry) == OSHostStatus.InvalidArgument
            && entry.Thread.Value == 0
            && entry.ThreadId == 0
            && entry.State == OSHostThreadLifecycleState.None);
    }

    [HostTest("pal.thread table default results are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostThreadCreateResult create = default;
        OSHostThreadJoinResult join = default;
        OSHostThreadIdResult current = default;
        OSHostThreadCapabilityResult capabilities = default;
        OSHostThreadSleepResult sleep = default;
        OSHostThreadCapabilityResult emptyTableCapabilities = new OSHostThreadTable(capacity: 0).GetCapabilities();

        return HostCheck.Result(create.Status == OSHostStatus.Ok
            && !create.Succeeded
            && !create.IsInitialized
            && create.Thread.Value == 0
            && join.Status == OSHostStatus.Ok
            && !join.Succeeded
            && !join.IsInitialized
            && current.Status == OSHostStatus.Ok
            && !current.Succeeded
            && !current.IsInitialized
            && capabilities.Status == OSHostStatus.Ok
            && !capabilities.Succeeded
            && !capabilities.ReadyForHostedRuntime
            && !capabilities.IsInitialized
            && sleep.Status == OSHostStatus.Ok
            && !sleep.Succeeded
            && !sleep.CompletedSynchronously
            && !sleep.IsInitialized
            && emptyTableCapabilities.Succeeded
            && !emptyTableCapabilities.ReadyForHostedRuntime
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostThreadCreateResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostThreadJoinResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostThreadIdResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostThreadCapabilityResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostThreadSleepResult>());
    }

    private static OSHostThreadCreateOptions CreateOptions(ulong entryPoint = 0x4000)
    {
        return new OSHostThreadCreateOptions(
            OSHostThreadConstants.CreateOptionsVersion,
            flags: 0,
            stackSizeBytes: 64 * 1024,
            guardSizeBytes: 4096,
            nameUtf8: new OSHostByteSpan(address: 0x1000, length: 6),
            start: new OSHostThreadStart(entryPoint, context: 0x1234));
    }
}
