namespace OS.Kernel.Memory;
/// <summary>
/// Validates a bounded, already-sorted caller-owned reservation table without
/// allocating, sorting, mutating, or dereferencing a pointer before metadata
/// and capacity checks succeed.
/// </summary>

public static unsafe partial class BootReservationTableValidator
{
    public const ulong MaximumReservationCount = 256;
    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong RequiredTableAlignment = 8;
    public static BootReservationTableValidationResult Validate(BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity)
    {
        ValidationState state = new ValidationState(reservationCount, reservationCapacity);
        if (reservationCount == 0)
        {
            return Fail(BootReservationTableStatus.InvalidReservationCount, BootReservedRangeStatus.Uninitialized, state, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex);
        }

        if (reservationCount > MaximumReservationCount)
        {
            return Fail(BootReservationTableStatus.TooManyReservations, BootReservedRangeStatus.Uninitialized, state, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex);
        }

        if (reservationCount > reservationCapacity)
        {
            return Fail(BootReservationTableStatus.ReservationCapacityTooSmall, BootReservedRangeStatus.Uninitialized, state, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex);
        }

        if (reservations == null)
        {
            return Fail(BootReservationTableStatus.MissingReservationTable, BootReservedRangeStatus.Uninitialized, state, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex);
        }

        if ((((ulong)reservations) & (RequiredTableAlignment - 1)) != 0)
        {
            return Fail(BootReservationTableStatus.MisalignedReservationTable, BootReservedRangeStatus.Uninitialized, state, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex);
        }

        ulong tableByteLength = reservationCount * (ulong)sizeof(BootReservedRange);
        ulong tableStart = (ulong)reservations;
        if (tableStart > MaximumUnsignedLong - tableByteLength)
        {
            return Fail(BootReservationTableStatus.ReservationTableAddressRangeOverflow, BootReservedRangeStatus.Uninitialized, state, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex);
        }

        BootReservedRange previous = default;
        for (ulong index = 0; index < reservationCount; index++)
        {
            BootReservedRange current = reservations[index];
            BootReservedRangeStatus rangeStatus = current.Validate();
            if (rangeStatus != BootReservedRangeStatus.Success)
            {
                return Fail(BootReservationTableStatus.InvalidReservation, rangeStatus, state, index, BootReservationTableValidationResult.NoIndex);
            }

            if (index != 0)
            {
                OS.Boot.AddressValueStatus startOrderStatus = current.PhysicalStart.TryCompareTo(previous.PhysicalStart, out int startOrder);
                if (startOrderStatus != OS.Boot.AddressValueStatus.Success)
                {
                    return Fail(BootReservationTableStatus.AddressComparisonFailed, BootReservedRangeStatus.Success, state, index, index - 1);
                }

                if (startOrder < 0)
                {
                    return Fail(BootReservationTableStatus.UnsortedRange, BootReservedRangeStatus.Success, state, index, index - 1);
                }

                OS.Boot.AddressValueStatus overlapStatus = current.PhysicalStart.TryCompareTo(previous.PhysicalEndExclusive, out int startVersusPreviousEnd);
                if (overlapStatus != OS.Boot.AddressValueStatus.Success)
                {
                    return Fail(BootReservationTableStatus.AddressComparisonFailed, BootReservedRangeStatus.Success, state, index, index - 1);
                }

                if (startVersusPreviousEnd < 0)
                {
                    OS.Boot.AddressValueStatus endOrderStatus = current.PhysicalEndExclusive.TryCompareTo(previous.PhysicalEndExclusive, out int endOrder);
                    if (endOrderStatus != OS.Boot.AddressValueStatus.Success)
                    {
                        return Fail(BootReservationTableStatus.AddressComparisonFailed, BootReservedRangeStatus.Success, state, index, index - 1);
                    }

                    bool currentContained = endOrder <= 0;
                    bool previousContained = startOrder == 0 && endOrder >= 0;
                    if (IsDuplicateLoadedImageCoverage(previous, current, currentContained, previousContained))
                    {
                        return Fail(BootReservationTableStatus.DuplicateLoadedImageCoverage, BootReservedRangeStatus.Success, state, index, index - 1);
                    }

                    if (startOrder == 0 && endOrder == 0)
                    {
                        return Fail(BootReservationTableStatus.DuplicateRange, BootReservedRangeStatus.Success, state, index, index - 1);
                    }

                    if (currentContained || previousContained)
                    {
                        return Fail(BootReservationTableStatus.NestedRange, BootReservedRangeStatus.Success, state, index, index - 1);
                    }

                    return Fail(BootReservationTableStatus.OverlappingRange, BootReservedRangeStatus.Success, state, index, index - 1);
                }
            }

            if (current.Kind == BootReservationKind.BootStackGuard)
            {
                if (state.BootStackGuardIndex != BootReservationTableValidationResult.NoIndex)
                {
                    return Fail(BootReservationTableStatus.MultipleBootStackGuards, BootReservedRangeStatus.Success, state, index, state.BootStackGuardIndex);
                }

                state.BootStackGuardIndex = index;
                state.BootStackGuard = current;
            }
            else if (current.Kind == BootReservationKind.BootStack)
            {
                if (state.BootStackIndex != BootReservationTableValidationResult.NoIndex)
                {
                    return Fail(BootReservationTableStatus.MultipleBootStacks, BootReservedRangeStatus.Success, state, index, state.BootStackIndex);
                }

                state.BootStackIndex = index;
                state.BootStack = current;
            }

            if (!TryAdd(state.TotalPages, current.PageCount, out ulong totalPages))
            {
                return Fail(BootReservationTableStatus.TotalPageCountOverflow, BootReservedRangeStatus.Success, state, index, BootReservationTableValidationResult.NoIndex);
            }

            if (!TryAdd(state.TotalBytes, current.ByteLength, out ulong totalBytes))
            {
                return Fail(BootReservationTableStatus.TotalByteCountOverflow, BootReservedRangeStatus.Success, state, index, BootReservationTableValidationResult.NoIndex);
            }

            if (current.Kind == BootReservationKind.LoadedImage && state.FirstLoadedImageIndex == BootReservationTableValidationResult.NoIndex)
            {
                state.FirstLoadedImageIndex = index;
            }

            state.TotalPages = totalPages;
            state.TotalBytes = totalBytes;
            state.ValidatedReservationCount = index + 1;
            previous = current;
        }

        return ValidateBootStackArena(state);
    }

    private static BootReservationTableValidationResult ValidateBootStackArena(ValidationState state)
    {
        bool hasGuard = state.BootStackGuardIndex != BootReservationTableValidationResult.NoIndex;
        bool hasStack = state.BootStackIndex != BootReservationTableValidationResult.NoIndex;
        if (!hasGuard && !hasStack)
        {
            return Succeed(state);
        }

        if (!hasGuard)
        {
            return Fail(BootReservationTableStatus.MissingBootStackGuard, BootReservedRangeStatus.Success, state, state.BootStackIndex, BootReservationTableValidationResult.NoIndex);
        }

        if (!hasStack)
        {
            return Fail(BootReservationTableStatus.MissingBootStack, BootReservedRangeStatus.Success, state, state.BootStackGuardIndex, BootReservationTableValidationResult.NoIndex);
        }

        BootReservedRange guard = state.BootStackGuard;
        BootReservedRange stack = state.BootStack;
        OS.Boot.AddressValueStatus orderStatus = guard.PhysicalStart.TryCompareTo(stack.PhysicalStart, out int guardBeforeStack);
        if (orderStatus != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(BootReservationTableStatus.AddressComparisonFailed, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        if (guardBeforeStack >= 0 || state.BootStackIndex != state.BootStackGuardIndex + 1)
        {
            return Fail(BootReservationTableStatus.BootStackOrderInvalid, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        OS.Boot.AddressValueStatus adjacencyStatus = guard.PhysicalEndExclusive.TryCompareTo(stack.PhysicalStart, out int guardEndVersusStackStart);
        if (adjacencyStatus != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(BootReservationTableStatus.AddressComparisonFailed, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        if (guardEndVersusStackStart != 0)
        {
            return Fail(BootReservationTableStatus.BootStackArenaGap, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        if (!TryAdd(guard.PageCount, stack.PageCount, out ulong arenaPages) || !TryAdd(guard.ByteLength, stack.ByteLength, out ulong arenaBytes) || arenaPages > MaximumUnsignedLong / BootReservedRange.PageSize)
        {
            return Fail(BootReservationTableStatus.BootStackArenaAccountingOverflow, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        if (arenaPages * BootReservedRange.PageSize != arenaBytes)
        {
            return Fail(BootReservationTableStatus.BootStackArenaAccountingMismatch, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        OS.Boot.AddressValueStatus arenaEndStatus = guard.PhysicalStart.TryGetEndExclusive(arenaBytes, out OS.Boot.PhysicalAddress expectedArenaEnd);
        if (arenaEndStatus != OS.Boot.AddressValueStatus.Success || !expectedArenaEnd.Equals(stack.PhysicalEndExclusive))
        {
            return Fail(BootReservationTableStatus.BootStackArenaAccountingMismatch, BootReservedRangeStatus.Success, state, state.BootStackIndex, state.BootStackGuardIndex);
        }

        state.BootStackArenaPages = arenaPages;
        state.BootStackArenaBytes = arenaBytes;
        return Succeed(state);
    }

    private static bool IsDuplicateLoadedImageCoverage(BootReservedRange previous, BootReservedRange current, bool currentContained, bool previousContained)
    {
        return previous.Kind == BootReservationKind.LoadedImage && IsImageCoveredMetadata(current.Kind) && currentContained || current.Kind == BootReservationKind.LoadedImage && IsImageCoveredMetadata(previous.Kind) && previousContained;
    }

    private static bool IsImageCoveredMetadata(BootReservationKind kind)
    {
        return kind == BootReservationKind.OwnedMemoryMap || kind == BootReservationKind.AcpiSnapshot || kind == BootReservationKind.AcpiScratch || kind == BootReservationKind.DiagnosticState || kind == BootReservationKind.BootHandoff || kind == BootReservationKind.FramebufferMetadata;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        value = 0;
        if (left > MaximumUnsignedLong - right)
        {
            return false;
        }

        value = left + right;
        return true;
    }

    private static BootReservationTableValidationResult Succeed(ValidationState state)
    {
        return BootReservationTableValidationResult.Create(BootReservationTableStatus.Success, BootReservedRangeStatus.Success, state.ReservationCount, state.ValidatedReservationCount, state.AvailableReservationCapacity, BootReservationTableValidationResult.NoIndex, BootReservationTableValidationResult.NoIndex, state.FirstLoadedImageIndex, state.BootStackGuardIndex, state.BootStackIndex, state.TotalPages, state.TotalBytes, state.BootStackArenaPages, state.BootStackArenaBytes);
    }

    private static BootReservationTableValidationResult Fail(BootReservationTableStatus status, BootReservedRangeStatus rangeStatus, ValidationState state, ulong invalidReservationIndex, ulong conflictingReservationIndex)
    {
        return BootReservationTableValidationResult.Create(status, rangeStatus, state.ReservationCount, state.ValidatedReservationCount, state.AvailableReservationCapacity, invalidReservationIndex, conflictingReservationIndex, state.FirstLoadedImageIndex, state.BootStackGuardIndex, state.BootStackIndex, state.TotalPages, state.TotalBytes, state.BootStackArenaPages, state.BootStackArenaBytes);
    }
}
