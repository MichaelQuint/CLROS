namespace OS.Kernel.Memory;

public static unsafe partial class BootReservationTableValidator
{
    private struct ValidationState
    {
        public ValidationState(ulong reservationCount, ulong availableReservationCapacity)
        {
            ReservationCount = reservationCount;
            AvailableReservationCapacity = availableReservationCapacity;
            ValidatedReservationCount = 0;
            FirstLoadedImageIndex = BootReservationTableValidationResult.NoIndex;
            BootStackGuardIndex = BootReservationTableValidationResult.NoIndex;
            BootStackIndex = BootReservationTableValidationResult.NoIndex;
            BootStackGuard = default;
            BootStack = default;
            TotalPages = 0;
            TotalBytes = 0;
            BootStackArenaPages = 0;
            BootStackArenaBytes = 0;
        }

        public ulong ReservationCount;
        public ulong AvailableReservationCapacity;
        public ulong ValidatedReservationCount;
        public ulong FirstLoadedImageIndex;
        public ulong BootStackGuardIndex;
        public ulong BootStackIndex;
        public BootReservedRange BootStackGuard;
        public BootReservedRange BootStack;
        public ulong TotalPages;
        public ulong TotalBytes;
        public ulong BootStackArenaPages;
        public ulong BootStackArenaBytes;
    }
}
