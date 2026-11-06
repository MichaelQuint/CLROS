namespace OS.Kernel.Memory;

internal static unsafe class X64Cr3ProbeTranscriptAuditor
{
    private const ulong MaximumUnsignedLong = ~0UL;
    public static X64Cr3ProbeAuditResult Audit(X64Cr3LoadStubPlan plan, X64Cr3ProbeEvent* events, ulong eventCount, ulong eventCapacity)
    {
        if ((ulong)sizeof(X64Cr3ProbeEvent) != X64Cr3ProbeEvent.StructureSize || (ulong)sizeof(X64Cr3ProbeAuditResult) != X64Cr3ProbeAuditResult.StructureSize)
        {
            return Fail(X64Cr3LoadStubPlanStatus.ContractLayoutMismatch, eventCount, eventCapacity);
        }

        if (!plan.IsValid)
        {
            return Fail(X64Cr3LoadStubPlanStatus.Uninitialized, eventCount, eventCapacity);
        }

        if (eventCount > X64Cr3LoadStubPlan.MaximumEventCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.TooManyEvents, eventCount, eventCapacity);
        }

        if (eventCapacity > X64Cr3LoadStubPlan.MaximumEventCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.TooManyEvents, eventCount, eventCapacity);
        }

        if (eventCount > eventCapacity)
        {
            return Fail(X64Cr3LoadStubPlanStatus.EventCapacityTooSmall, eventCount, eventCapacity);
        }

        if (eventCount > X64Cr3LoadStubPlan.RequiredEventCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidEventCount, eventCount, eventCapacity, X64Cr3LoadStubPlan.RequiredEventCount, X64Cr3LoadStubPlan.RequiredEventCount);
        }

        if (events == null)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MissingEvents, eventCount, eventCapacity);
        }

        ulong eventAddress = (ulong)events;
        if ((eventAddress & (X64Cr3LoadStubPlan.RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MisalignedEvents, eventCount, eventCapacity);
        }

        if (!TryGetSpanEnd(eventAddress, eventCapacity, X64Cr3ProbeEvent.StructureSize, out ulong ignoredEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.EventSpanOverflow, eventCount, eventCapacity);
        }

        ulong rawFirmwareCr3 = plan.RawFirmwareCr3;
        ulong ownedRoot = 0;
        plan.OwnedRoot.TryGetRawValueForHardwareRegisterBoundary(out ownedRoot);
        plan.UsableStackStart.TryGetRawValueForHardwareRegisterBoundary(out ulong usableStackStart);
        plan.UsableStackEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong usableStackEnd);
        ulong validated = 0;
        ulong previousElapsed = 0;
        for (ulong index = 0; index < eventCount; index++)
        {
            X64Cr3ProbeEvent current = events[index];
            if (!current.IsInitialized)
            {
                return Fail(X64Cr3LoadStubPlanStatus.InvalidEvent, eventCount, eventCapacity, validated, index);
            }

            X64Cr3LoadStubPlanStatus terminal = GetTerminalStatus(current.Kind);
            if (terminal != X64Cr3LoadStubPlanStatus.Success)
            {
                return Fail(terminal, eventCount, eventCapacity, validated, index, current);
            }

            if (current.ElapsedTicks < previousElapsed || current.ElapsedTicks > plan.Policy.TimeoutTicks)
            {
                return Fail(X64Cr3LoadStubPlanStatus.Timeout, eventCount, eventCapacity, validated, index, current);
            }

            X64Cr3ProbeEventKind expectedKind = GetExpectedKind(index);
            if (current.Kind != expectedKind)
            {
                X64Cr3LoadStubPlanStatus status = current.Kind == X64Cr3ProbeEventKind.PostMarkerEmitted ? X64Cr3LoadStubPlanStatus.EarlyPostMarker : index == 0 ? X64Cr3LoadStubPlanStatus.MissingPreMarker : X64Cr3LoadStubPlanStatus.UnexpectedEvent;
                return Fail(status, eventCount, eventCapacity, validated, index, current);
            }

            if (current.ObservedRsp.TryGetRawValueForHardwareRegisterBoundary(out ulong observedRsp) != OS.Boot.AddressValueStatus.Success || usableStackStart > MaximumUnsignedLong - X64Cr3LoadStubPlan.RequiredProbeStackHeadroomBytes || observedRsp < usableStackStart + X64Cr3LoadStubPlan.RequiredProbeStackHeadroomBytes || observedRsp >= usableStackEnd)
            {
                return Fail(X64Cr3LoadStubPlanStatus.RspEscapedOwnedStack, eventCount, eventCapacity, validated, index, current);
            }

            ulong expectedRoot = index >= 2 && index <= 4 ? ownedRoot : rawFirmwareCr3;
            if (current.ObservedRawCr3 != expectedRoot)
            {
                return Fail(X64Cr3LoadStubPlanStatus.RootReadbackMismatch, eventCount, eventCapacity, validated, index, current);
            }

            ulong expectedCanary = index < 4 ? plan.StorageLayout.InitialCanaryValue : plan.StorageLayout.UpdatedCanaryValue;
            if (current.ObservedCanary != expectedCanary)
            {
                X64Cr3LoadStubPlanStatus status = index == 4 ? X64Cr3LoadStubPlanStatus.CanaryNotUpdated : index >= 7 ? X64Cr3LoadStubPlanStatus.CanaryValidationFailed : X64Cr3LoadStubPlanStatus.UnexpectedEvent;
                return Fail(status, eventCount, eventCapacity, validated, index, current);
            }

            previousElapsed = current.ElapsedTicks;
            validated = index + 1;
        }

        if (eventCount < X64Cr3LoadStubPlan.RequiredEventCount)
        {
            X64Cr3LoadStubPlanStatus status = eventCount == 0 ? X64Cr3LoadStubPlanStatus.MissingPreMarker : X64Cr3LoadStubPlanStatus.MissingPostMarker;
            return Fail(status, eventCount, eventCapacity, validated);
        }

        if (eventCount != X64Cr3LoadStubPlan.RequiredEventCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidEventCount, eventCount, eventCapacity, validated, X64Cr3LoadStubPlan.RequiredEventCount);
        }

        X64Cr3ProbeEvent finalEvent = events[X64Cr3LoadStubPlan.RequiredEventCount - 1];
        return X64Cr3ProbeAuditResult.Create(X64Cr3LoadStubPlanStatus.Success, eventCount, eventCapacity, validated, X64Cr3LoadStubPlan.NoIndex, finalEvent.ObservedRawCr3, finalEvent.ObservedCanary, finalEvent.ElapsedTicks);
    }

    private static X64Cr3ProbeEventKind GetExpectedKind(ulong index)
    {
        if (index == 0)
            return X64Cr3ProbeEventKind.PreMarkerEmitted;
        if (index == 1)
            return X64Cr3ProbeEventKind.TransitionEntered;
        if (index == 2)
            return X64Cr3ProbeEventKind.OwnedRootLoaded;
        if (index == 3)
            return X64Cr3ProbeEventKind.OwnedRootReadBack;
        if (index == 4)
            return X64Cr3ProbeEventKind.CanaryUpdated;
        if (index == 5)
            return X64Cr3ProbeEventKind.FirmwareRootRestored;
        if (index == 6)
            return X64Cr3ProbeEventKind.FirmwareRootReadBack;
        if (index == 7)
            return X64Cr3ProbeEventKind.CanaryValidated;
        return index == 8 ? X64Cr3ProbeEventKind.PostMarkerEmitted : X64Cr3ProbeEventKind.Uninitialized;
    }

    private static X64Cr3LoadStubPlanStatus GetTerminalStatus(X64Cr3ProbeEventKind kind)
    {
        if (kind == X64Cr3ProbeEventKind.FirmwareCallbackAttempted)
            return X64Cr3LoadStubPlanStatus.FirmwareCallbackInsideInterval;
        if (kind == X64Cr3ProbeEventKind.InterruptReenabledInsideInterval)
            return X64Cr3LoadStubPlanStatus.InterruptReenabledInsideInterval;
        if (kind == X64Cr3ProbeEventKind.Timeout)
            return X64Cr3LoadStubPlanStatus.Timeout;
        if (kind == X64Cr3ProbeEventKind.Reset)
            return X64Cr3LoadStubPlanStatus.Reset;
        if (kind == X64Cr3ProbeEventKind.TripleFault)
            return X64Cr3LoadStubPlanStatus.TripleFault;
        return X64Cr3LoadStubPlanStatus.Success;
    }

    private static X64Cr3ProbeAuditResult Fail(X64Cr3LoadStubPlanStatus status, ulong eventCount, ulong eventCapacity, ulong validatedEventCount = 0, ulong invalidEventIndex = X64Cr3LoadStubPlan.NoIndex, X64Cr3ProbeEvent observation = default)
    {
        return X64Cr3ProbeAuditResult.Create(status, eventCount, eventCapacity, validatedEventCount, invalidEventIndex, observation.ObservedRawCr3, observation.ObservedCanary, observation.ElapsedTicks);
    }

    private static bool TryGetSpanEnd(ulong start, ulong capacity, ulong elementSize, out ulong end)
    {
        end = 0;
        if (capacity != 0 && elementSize > MaximumUnsignedLong / capacity)
        {
            return false;
        }

        ulong byteLength = capacity * elementSize;
        if (start > MaximumUnsignedLong - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }
}
