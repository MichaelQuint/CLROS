using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Pal;

internal static class PalWaitFacadeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.wait facade creates manual and auto events", TestCreatesManualAndAutoEvents, ref failures);
        Run("pal.wait facade set reset close translate statuses", TestSetResetCloseTranslateStatuses, ref failures);
        Run("pal.wait facade wait one reports signal and timeout", TestWaitOneReportsSignalAndTimeout, ref failures);
        Run("pal.wait facade wait many reports first signaled", TestWaitManyReportsFirstSignaled, ref failures);
        Run("pal.wait facade translates invalid and stale handles", TestTranslatesInvalidAndStaleHandles, ref failures);
        Run("pal.wait facade rejects unsupported wait all and timeout", TestRejectsUnsupportedWaitAllAndTimeout, ref failures);
        Run("pal.wait facade rejects null table", TestRejectsNullTable, ref failures);
        Run("pal.wait facade reports exhaustion", TestReportsExhaustion, ref failures);
        Run("pal.wait facade layouts are stable", TestLayoutsAreStable, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL wait facade host tests failed: " + failures);
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

    private static unsafe bool TestCreatesManualAndAutoEvents()
    {
        OSHostWaitTable table = new(capacity: 2);
        OSHostWaitFacade facade = new(table);
        OSHostHandle manual;
        OSHostHandle autoReset;

        if (facade.EventCreate(ManualEvent(initiallySignaled: false), &manual) != OSHostStatus.Ok ||
            facade.EventCreate(AutoEvent(initiallySignaled: true), &autoReset) != OSHostStatus.Ok ||
            !manual.IsValid ||
            !autoReset.IsValid ||
            manual.Value == autoReset.Value)
        {
            return false;
        }

        return table.TryGetEntry(manual, out OSHostWaitTableEntry manualEntry) == OSHostStatus.Ok
            && manualEntry.Kind == OSHostWaitableKind.ManualResetEvent
            && !manualEntry.Signaled
            && table.TryGetEntry(autoReset, out OSHostWaitTableEntry autoEntry) == OSHostStatus.Ok
            && autoEntry.Kind == OSHostWaitableKind.AutoResetEvent
            && autoEntry.Signaled
            && table.ActiveCount == 2;
    }

    private static unsafe bool TestSetResetCloseTranslateStatuses()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostWaitFacade facade = new(table);
        OSHostHandle waitable;
        OSHostWaitResult result;

        if (facade.EventCreate(ManualEvent(initiallySignaled: false), &waitable) != OSHostStatus.Ok ||
            facade.EventSet(waitable) != OSHostStatus.Ok ||
            facade.WaitOne(WaitOne(waitable), &result) != OSHostStatus.Ok ||
            !IsSignaled(result, waitable, expectedIndex: 0) ||
            facade.EventReset(waitable) != OSHostStatus.Ok ||
            facade.WaitOne(WaitOne(waitable), &result) != OSHostStatus.WouldBlock ||
            result.Kind != OSHostWaitConstants.ResultKindWouldBlock ||
            facade.EventClose(waitable) != OSHostStatus.Ok)
        {
            return false;
        }

        return facade.EventClose(waitable) == OSHostStatus.InvalidArgument
            && facade.EventSet(waitable) == OSHostStatus.InvalidArgument
            && facade.EventReset(waitable) == OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestWaitOneReportsSignalAndTimeout()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostWaitFacade facade = new(table);
        OSHostHandle waitable;
        OSHostWaitResult first;
        OSHostWaitResult second;

        if (facade.EventCreate(AutoEvent(initiallySignaled: true), &waitable) != OSHostStatus.Ok ||
            facade.WaitOne(WaitOne(waitable), &first) != OSHostStatus.Ok ||
            facade.WaitOne(WaitOne(waitable), &second) != OSHostStatus.WouldBlock)
        {
            return false;
        }

        return IsSignaled(first, waitable, expectedIndex: 0)
            && second.StatusCode == (int)OSHostStatus.WouldBlock
            && second.Kind == OSHostWaitConstants.ResultKindWouldBlock
            && second.SignaledHandle.Value == 0;
    }

    private static unsafe bool TestWaitManyReportsFirstSignaled()
    {
        OSHostWaitTable table = new(capacity: 3);
        OSHostWaitFacade facade = new(table);
        OSHostHandle first;
        OSHostHandle second;
        OSHostHandle third;

        if (facade.EventCreate(ManualEvent(initiallySignaled: false), &first) != OSHostStatus.Ok ||
            facade.EventCreate(AutoEvent(initiallySignaled: true), &second) != OSHostStatus.Ok ||
            facade.EventCreate(ManualEvent(initiallySignaled: true), &third) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostHandle[] waitables = [first, second, third];
        OSHostWaitManyRequest request = WaitMany(waitables);
        OSHostWaitResult firstResult;
        OSHostWaitResult secondResult;

        return facade.WaitMany(request, waitables, &firstResult) == OSHostStatus.Ok
            && IsSignaled(firstResult, second, expectedIndex: 1)
            && facade.WaitMany(request, waitables, &secondResult) == OSHostStatus.Ok
            && IsSignaled(secondResult, third, expectedIndex: 2);
    }

    private static unsafe bool TestTranslatesInvalidAndStaleHandles()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostWaitFacade facade = new(table);
        OSHostHandle waitable;
        OSHostHandle invalid = new(0);
        OSHostWaitResult result;

        if (facade.EventSet(invalid) != OSHostStatus.InvalidArgument ||
            facade.EventReset(invalid) != OSHostStatus.InvalidArgument ||
            facade.EventClose(invalid) != OSHostStatus.InvalidArgument ||
            facade.WaitOne(WaitOne(invalid), &result) != OSHostStatus.InvalidArgument ||
            result.StatusCode != (int)OSHostStatus.InvalidArgument ||
            facade.WaitOne(WaitOne(invalid), null) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (facade.EventCreate(ManualEvent(initiallySignaled: true), &waitable) != OSHostStatus.Ok ||
            facade.EventClose(waitable) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostHandle[] waitables = [waitable];
        return facade.WaitOne(WaitOne(waitable), &result) == OSHostStatus.InvalidArgument
            && result.StatusCode == (int)OSHostStatus.InvalidArgument
            && facade.WaitMany(WaitMany(waitables), waitables, &result) == OSHostStatus.InvalidArgument
            && result.StatusCode == (int)OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestRejectsUnsupportedWaitAllAndTimeout()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostWaitFacade facade = new(table);
        OSHostHandle waitable;
        OSHostHandle scratch;
        OSHostWaitResult result;

        if (facade.EventCreate(ManualEvent(initiallySignaled: true), &waitable) != OSHostStatus.Ok ||
            facade.EventCreate(new OSHostEventCreateOptions(0, 0, 0, 0), &scratch) != OSHostStatus.InvalidArgument ||
            facade.EventCreate(new OSHostEventCreateOptions((uint)OSHostWaitableKind.ManualResetEvent, 2, 0, 0), &scratch) != OSHostStatus.InvalidArgument ||
            facade.EventCreate(new OSHostEventCreateOptions((uint)OSHostWaitableKind.ManualResetEvent, 0, 1, 0), &scratch) != OSHostStatus.InvalidArgument ||
            facade.EventCreate(new OSHostEventCreateOptions((uint)OSHostWaitableKind.ManualResetEvent, 0, 0, 1), &scratch) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        OSHostTimeout unsupportedTimeout = new(
            OSHostSocketConstants.TimeoutKindRelativeTicks,
            flags: 0,
            ticks: 10,
            tickFrequency: 1_000);
        OSHostHandle[] waitables = [waitable];

        return facade.WaitOne(new OSHostWaitOneRequest(waitable, unsupportedTimeout, flags: 0, reserved: 0), &result) == OSHostStatus.NotSupported
            && result.StatusCode == (int)OSHostStatus.NotSupported
            && facade.WaitMany(WaitMany(waitables, OSHostWaitConstants.WaitFlagWaitAll), waitables, &result) == OSHostStatus.NotSupported
            && result.StatusCode == (int)OSHostStatus.NotSupported
            && facade.WaitMany(new OSHostWaitManyRequest(
                new OSHostByteSpan(0x1000, 1),
                OSHostWaitTable.ImmediateTimeout(),
                flags: 0,
                reserved: 0), waitables, &result) == OSHostStatus.InvalidArgument
            && facade.WaitMany(new OSHostWaitManyRequest(
                new OSHostByteSpan(0, HandleSpanByteLength(waitables.Length)),
                OSHostWaitTable.ImmediateTimeout(),
                flags: 0,
                reserved: 0), waitables, &result) == OSHostStatus.InvalidArgument
            && facade.WaitMany(new OSHostWaitManyRequest(
                new OSHostByteSpan(0x1000, HandleSpanByteLength(waitables.Length)),
                OSHostWaitTable.ImmediateTimeout(),
                flags: 0,
                reserved: 1), waitables, &result) == OSHostStatus.InvalidArgument
            && facade.WaitMany(WaitMany(waitables), waitables, null) == OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestRejectsNullTable()
    {
        OSHostWaitFacade facade = new(table: null);
        OSHostHandle waitable;
        OSHostWaitResult result;
        OSHostHandle invalid = new(1);
        OSHostHandle[] waitables = [invalid];

        if (facade.EventCreate(ManualEvent(initiallySignaled: false), &waitable) != OSHostStatus.InvalidArgument ||
            waitable.IsValid ||
            facade.EventCreate(ManualEvent(initiallySignaled: false), null) != OSHostStatus.InvalidArgument ||
            facade.EventSet(invalid) != OSHostStatus.InvalidArgument ||
            facade.EventReset(invalid) != OSHostStatus.InvalidArgument ||
            facade.EventClose(invalid) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        return facade.WaitOne(WaitOne(invalid), &result) == OSHostStatus.InvalidArgument
            && result.StatusCode == (int)OSHostStatus.InvalidArgument
            && facade.WaitMany(WaitMany(waitables), waitables, &result) == OSHostStatus.InvalidArgument
            && result.StatusCode == (int)OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestReportsExhaustion()
    {
        OSHostWaitTable table = new(capacity: 1);
        OSHostWaitFacade facade = new(table);
        OSHostHandle first;
        OSHostHandle second = new(0xFFFF);

        if (facade.EventCreate(ManualEvent(initiallySignaled: false), &first) != OSHostStatus.Ok ||
            !first.IsValid ||
            facade.EventCreate(AutoEvent(initiallySignaled: false), &second) != OSHostStatus.OutOfMemory ||
            second.IsValid)
        {
            return false;
        }

        OSHostHandle reused;
        return facade.EventClose(first) == OSHostStatus.Ok
            && facade.EventCreate(AutoEvent(initiallySignaled: true), &reused) == OSHostStatus.Ok
            && reused.IsValid
            && reused.Value != first.Value;
    }

    private static bool TestLayoutsAreStable()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<OSHostEventCreateOptions>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<OSHostWaitResult>())
        {
            return false;
        }

        return Marshal.SizeOf<OSHostEventCreateOptions>() == 16
            && Offset<OSHostEventCreateOptions>(nameof(OSHostEventCreateOptions.Kind)) == 0
            && Offset<OSHostEventCreateOptions>(nameof(OSHostEventCreateOptions.InitiallySignaled)) == 4
            && Offset<OSHostEventCreateOptions>(nameof(OSHostEventCreateOptions.Flags)) == 8
            && Offset<OSHostEventCreateOptions>(nameof(OSHostEventCreateOptions.Reserved)) == 12;
    }

    private static OSHostEventCreateOptions ManualEvent(bool initiallySignaled)
    {
        return new OSHostEventCreateOptions(
            (uint)OSHostWaitableKind.ManualResetEvent,
            initiallySignaled ? 1U : 0U,
            flags: 0,
            reserved: 0);
    }

    private static OSHostEventCreateOptions AutoEvent(bool initiallySignaled)
    {
        return new OSHostEventCreateOptions(
            (uint)OSHostWaitableKind.AutoResetEvent,
            initiallySignaled ? 1U : 0U,
            flags: 0,
            reserved: 0);
    }

    private static OSHostWaitOneRequest WaitOne(OSHostHandle waitable)
    {
        return new OSHostWaitOneRequest(
            waitable,
            OSHostWaitTable.ImmediateTimeout(),
            flags: 0,
            reserved: 0);
    }

    private static OSHostWaitManyRequest WaitMany(
        OSHostHandle[] waitables,
        uint flags = 0)
    {
        return new OSHostWaitManyRequest(
            new OSHostByteSpan(0x1000, HandleSpanByteLength(waitables.Length)),
            OSHostWaitTable.ImmediateTimeout(),
            flags,
            reserved: 0);
    }

    private static ulong HandleSpanByteLength(int count)
    {
        return (ulong)count * 8;
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

    private static int Offset<T>(string fieldName)
    {
        return (int)Marshal.OffsetOf<T>(fieldName);
    }
}
