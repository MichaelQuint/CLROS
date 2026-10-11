using OS.Drivers;

namespace OS.Storage;

public enum BlockCompletionRoute : byte
{
    Uninitialized, InvalidWait, Interrupt, PollingRecovery
}

/// <summary>Classifies an authenticated single-BSP wait receipt after delivery
/// is masked. The caller still owns live IDT/code/mapping/queue validation and
/// transport cleanup. Selecting a route establishes no I/O completion.</summary>
public static unsafe class PollingCompletionMSIXRemovingPollingRecoveryPath
{
    public static BlockCompletionRoute Select(MsixInterruptContext* context, ulong code,
        ulong originalIdt, ulong temporaryIdt, ulong localApic, uint limit, uint returned,
        bool deliveryMasked)
    {
        if (!deliveryMasked) return BlockCompletionRoute.InvalidWait;
        if (X64MsixInterrupt.ValidateCompletion(context, code, originalIdt, temporaryIdt, localApic, limit, returned))
            return BlockCompletionRoute.Interrupt;
        if (X64MsixInterrupt.ValidatePollingRecovery(context, code, originalIdt, temporaryIdt, localApic, limit, returned))
            return BlockCompletionRoute.PollingRecovery;
        return BlockCompletionRoute.InvalidWait;
    }
}
