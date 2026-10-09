using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalWaitTableTests
{

    [HostTest("pal.wait manual reset event stays signaled", HostLane.Fast, 0)]
    private static bool TestManualResetEventStaysSignaled()
    {
        OSHostWaitTable table = new(capacity: 2);
        OSHostEventCreateResult create =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: false);
        if (!create.Succeeded ||
            table.WaitOne(create.Waitable).StatusCode != (int)OSHostStatus.WouldBlock ||
            table.Set(create.Waitable) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostWaitResult first = table.WaitOne(create.Waitable);
        OSHostWaitResult second = table.WaitOne(create.Waitable);

        return HostCheck.Result(IsSignaled(first, create.Waitable, expectedIndex: 0)
            && IsSignaled(second, create.Waitable, expectedIndex: 0)
            && table.TryGetEntry(create.Waitable, out OSHostWaitTableEntry entry) == OSHostStatus.Ok
            && entry.Kind == OSHostWaitableKind.ManualResetEvent
            && entry.Signaled);
    }

    [HostTest("pal.wait auto reset consumes signal", HostLane.Fast, 1)]
    private static bool TestAutoResetConsumesSignal()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostEventCreateResult create =
            table.CreateEvent(OSHostWaitableKind.AutoResetEvent, initiallySignaled: true);
        if (!create.Succeeded)
        {
            return HostCheck.Result(false);
        }

        OSHostWaitResult first = table.WaitOne(create.Waitable);
        OSHostWaitResult second = table.WaitOne(create.Waitable);
        if (!IsSignaled(first, create.Waitable, expectedIndex: 0) ||
            second.StatusCode != (int)OSHostStatus.WouldBlock ||
            second.Kind != OSHostWaitConstants.ResultKindWouldBlock)
        {
            return HostCheck.Result(false);
        }

        if (table.Set(create.Waitable) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostWaitResult afterSet = table.WaitOne(create.Waitable);
        return HostCheck.Result(IsSignaled(afterSet, create.Waitable, expectedIndex: 0)
            && table.WaitOne(create.Waitable).StatusCode == (int)OSHostStatus.WouldBlock);
    }

    [HostTest("pal.wait many reports first signaled", HostLane.Fast, 2)]
    private static bool TestWaitManyReportsFirstSignaled()
    {
        OSHostWaitTable table = new(capacity: 3);
        OSHostEventCreateResult first =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: false);
        OSHostEventCreateResult second =
            table.CreateEvent(OSHostWaitableKind.AutoResetEvent, initiallySignaled: true);
        OSHostEventCreateResult third =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: true);
        if (!first.Succeeded || !second.Succeeded || !third.Succeeded)
        {
            return HostCheck.Result(false);
        }

        OSHostHandle[] waitables = [first.Waitable, second.Waitable, third.Waitable];
        OSHostWaitResult firstMany = table.WaitMany(waitables, OSHostWaitTable.ImmediateTimeout());
        OSHostWaitResult secondMany = table.WaitMany(waitables, OSHostWaitTable.ImmediateTimeout());

        return HostCheck.Result(IsSignaled(firstMany, second.Waitable, expectedIndex: 1)
            && IsSignaled(secondMany, third.Waitable, expectedIndex: 2));
    }

    [HostTest("pal.wait reset and close reject invalid", HostLane.Fast, 3)]
    private static bool TestResetAndCloseRejectInvalid()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostEventCreateResult create =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: true);
        if (!create.Succeeded ||
            table.Reset(create.Waitable) != OSHostStatus.Ok ||
            table.WaitOne(create.Waitable).StatusCode != (int)OSHostStatus.WouldBlock)
        {
            return HostCheck.Result(false);
        }

        if (table.Close(create.Waitable) != OSHostStatus.Ok ||
            table.WaitOne(create.Waitable).StatusCode != (int)OSHostStatus.InvalidArgument ||
            table.Set(create.Waitable) != OSHostStatus.InvalidArgument ||
            table.Reset(create.Waitable) != OSHostStatus.InvalidArgument ||
            table.Close(create.Waitable) != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        OSHostEventCreateResult reused =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: false);
        if (!reused.Succeeded
            || reused.Waitable.Value == create.Waitable.Value
            || table.WaitOne(reused.Waitable).StatusCode != (int)OSHostStatus.WouldBlock)
        {
            return HostCheck.Result(false);
        }

        OSHostWaitTable cancelTable = new(capacity: 2);
        OSHostEventCreateResult cancel =
            cancelTable.CreateEvent(OSHostWaitableKind.AutoResetEvent, initiallySignaled: true);
        OSHostEventCreateResult afterCancel =
            cancelTable.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: true);
        if (!cancel.Succeeded
            || !afterCancel.Succeeded
            || cancelTable.Cancel(cancel.Waitable) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostWaitResult canceledOne = cancelTable.WaitOne(cancel.Waitable);
        OSHostWaitResult canceledMany = cancelTable.WaitMany(
            [cancel.Waitable, afterCancel.Waitable],
            OSHostWaitTable.ImmediateTimeout());

        return HostCheck.Result(canceledOne.StatusCode == (int)OSHostStatus.Interrupted
            && canceledOne.Kind == OSHostWaitConstants.ResultKindInterrupted
            && canceledMany.StatusCode == (int)OSHostStatus.Interrupted
            && canceledMany.Kind == OSHostWaitConstants.ResultKindInterrupted
            && cancelTable.Set(cancel.Waitable) == OSHostStatus.Interrupted
            && cancelTable.Reset(cancel.Waitable) == OSHostStatus.Interrupted
            && cancelTable.Close(cancel.Waitable) == OSHostStatus.Ok
            && cancelTable.Cancel(cancel.Waitable) == OSHostStatus.InvalidArgument
            && reused.Waitable.Value != create.Waitable.Value
            && table.WaitOne(reused.Waitable).StatusCode == (int)OSHostStatus.WouldBlock);
    }

    [HostTest("pal.wait reports early mode capabilities", HostLane.Fast, 4)]
    private static bool TestReportsEarlyModeCapabilities()
    {
        OSHostWaitTable table = new(capacity: 2);
        OSHostWaitCapabilityResult initial = table.GetCapabilities();
        OSHostEventCreateResult create =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: false);
        OSHostWaitCapabilityResult afterCreate = table.GetCapabilities();

        return HostCheck.Result(initial.Succeeded
            && !initial.ReadyForHostedRuntime
            && initial.MaxWaitableCount == 2
            && initial.ActiveWaitableCount == 0
            && initial.HasCapability(OSHostWaitConstants.CapabilityManualResetEvent)
            && initial.HasCapability(OSHostWaitConstants.CapabilityAutoResetEvent)
            && initial.HasCapability(OSHostWaitConstants.CapabilityImmediateWaitOne)
            && initial.HasCapability(OSHostWaitConstants.CapabilityImmediateWaitMany)
            && initial.HasCapability(OSHostWaitConstants.CapabilityMockEventLifecycle)
            && initial.HasCapability(OSHostWaitConstants.CapabilitySingleThreadEarlyMode)
            && create.Succeeded
            && afterCreate.Succeeded
            && afterCreate.ActiveWaitableCount == 1
            && afterCreate.MaxWaitableCount == 2
            && !afterCreate.ReadyForHostedRuntime);
    }

    [HostTest("pal.wait rejects invalid timeout and unsupported waits", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidTimeoutAndUnsupportedWaits()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostEventCreateResult create =
            table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: false);
        if (!create.Succeeded)
        {
            return HostCheck.Result(false);
        }

        OSHostWaitResult immediate = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            OSHostSocketTimeouts.Immediate,
            flags: 0,
            reserved: 0));
        OSHostWaitResult infinite = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            OSHostSocketTimeouts.Infinite,
            flags: 0,
            reserved: 0));
        OSHostWaitResult relative = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            new OSHostTimeout(
                OSHostSocketConstants.TimeoutKindRelativeTicks,
                flags: 0,
                ticks: 1,
                tickFrequency: 1_000),
            flags: 0,
            reserved: 0));
        OSHostWaitResult absolute = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            new OSHostTimeout(
                OSHostSocketConstants.TimeoutKindAbsoluteTicks,
                flags: 0,
                ticks: 1,
                tickFrequency: 1_000),
            flags: 0,
            reserved: 0));
        OSHostWaitResult invalidKind = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            new OSHostTimeout(
                kind: 99,
                flags: 0,
                ticks: 0,
                tickFrequency: 0),
            flags: 0,
            reserved: 0));
        OSHostWaitResult invalidImmediatePayload = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            new OSHostTimeout(
                OSHostSocketConstants.TimeoutKindImmediate,
                flags: 0,
                ticks: 1,
                tickFrequency: 0),
            flags: 0,
            reserved: 0));
        OSHostWaitResult invalidFlags = table.WaitOne(new OSHostWaitOneRequest(
            create.Waitable,
            new OSHostTimeout(
                OSHostSocketConstants.TimeoutKindImmediate,
                flags: 1,
                ticks: 0,
                tickFrequency: 0),
            flags: 0,
            reserved: 0));
        OSHostWaitResult waitManyInfinite =
            table.WaitMany([create.Waitable], OSHostSocketTimeouts.Infinite);

        return HostCheck.Result(immediate.StatusCode == (int)OSHostStatus.WouldBlock
            && immediate.Kind == OSHostWaitConstants.ResultKindWouldBlock
            && infinite.StatusCode == (int)OSHostStatus.NotSupported
            && infinite.Kind == OSHostWaitConstants.ResultKindNone
            && relative.StatusCode == (int)OSHostStatus.NotSupported
            && absolute.StatusCode == (int)OSHostStatus.NotSupported
            && invalidKind.StatusCode == (int)OSHostStatus.InvalidArgument
            && invalidImmediatePayload.StatusCode == (int)OSHostStatus.InvalidArgument
            && invalidFlags.StatusCode == (int)OSHostStatus.InvalidArgument
            && waitManyInfinite.StatusCode == (int)OSHostStatus.NotSupported);
    }

    [HostTest("pal.wait invalid options are rejected", HostLane.Fast, 6)]
    private static bool TestInvalidOptionsAreRejected()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostHandle zero = new(0);
        OSHostTimeout invalidTimeout = new(
            kind: 99,
            flags: 0,
            ticks: 0,
            tickFrequency: 0);

        OSHostEventCreateResult valid =
            table.CreateEvent(OSHostWaitableKind.AutoResetEvent, initiallySignaled: false);
        if (!valid.Succeeded)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(OSHostWaitTable.ImmediateTimeout().Kind == OSHostSocketConstants.TimeoutKindImmediate
            && table.CreateEvent(OSHostWaitableKind.None, initiallySignaled: false).Status == OSHostStatus.InvalidArgument
            && table.CreateEvent(OSHostWaitableKind.ManualResetEvent, initiallySignaled: false).Status == OSHostStatus.OutOfMemory
            && table.WaitOne(zero).StatusCode == (int)OSHostStatus.InvalidArgument
            && table.WaitOne(new OSHostWaitOneRequest(
                valid.Waitable,
                OSHostWaitTable.ImmediateTimeout(),
                flags: 0,
                reserved: 1)).StatusCode == (int)OSHostStatus.InvalidArgument
            && table.WaitOne(new OSHostWaitOneRequest(
                valid.Waitable,
                invalidTimeout,
                flags: 0,
                reserved: 0)).StatusCode == (int)OSHostStatus.InvalidArgument
            && table.WaitMany(
                [],
                OSHostWaitTable.ImmediateTimeout()).StatusCode == (int)OSHostStatus.InvalidArgument
            && table.WaitMany(
                [valid.Waitable],
                OSHostWaitTable.ImmediateTimeout(),
                OSHostWaitConstants.WaitFlagWaitAll).StatusCode == (int)OSHostStatus.NotSupported);
    }

    [HostTest("pal.wait default results are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostEventCreateResult create = default;
        OSHostWaitResult wait = default;
        OSHostWaitCapabilityResult capabilities = default;
        OSHostWaitCapabilityResult emptyTableCapabilities = new OSHostWaitTable(capacity: 0).GetCapabilities();

        return HostCheck.Result(create.Status == OSHostStatus.Ok
            && !create.Succeeded
            && create.Waitable.Value == 0
            && wait.StatusCode == (int)OSHostStatus.Ok
            && wait.Kind == OSHostWaitConstants.ResultKindNone
            && wait.SignaledHandle.Value == 0
            && capabilities.Status == OSHostStatus.Ok
            && !capabilities.Succeeded
            && !capabilities.ReadyForHostedRuntime
            && !capabilities.IsInitialized
            && emptyTableCapabilities.Succeeded
            && !emptyTableCapabilities.ReadyForHostedRuntime
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostEventCreateResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostWaitCapabilityResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostWaitResult>());
    }

    private static bool IsSignaled(
        OSHostWaitResult result,
        OSHostHandle expectedHandle,
        uint expectedIndex)
    {
        return result.StatusCode == (int)OSHostStatus.Ok
            && result.Kind == OSHostWaitConstants.ResultKindSignaled
            && result.SignaledIndex == expectedIndex
            && result.SignaledHandle.Value == expectedHandle.Value;
    }
}
