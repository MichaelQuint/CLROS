namespace OS.Pal;
public static class OSHostWaitConstants
{
    public const uint TimeoutKindImmediate = OSHostSocketConstants.TimeoutKindImmediate;
    public const uint WaitFlagWaitAll = 1;
    public const ulong CapabilityManualResetEvent = 1UL << 0;
    public const ulong CapabilityAutoResetEvent = 1UL << 1;
    public const ulong CapabilityImmediateWaitOne = 1UL << 2;
    public const ulong CapabilityImmediateWaitMany = 1UL << 3;
    public const ulong CapabilityMockEventLifecycle = 1UL << 4;
    public const ulong CapabilitySingleThreadEarlyMode = 1UL << 5;
    public const uint ResultKindNone = 0;
    public const uint ResultKindSignaled = 1;
    public const uint ResultKindWouldBlock = 2;
    public const uint ResultKindInterrupted = 3;
}
