namespace OS.Pal;
public static class OSHostThreadConstants
{
    public const uint CreateOptionsVersion = 1;
    public const ulong CurrentThreadId = 1;
    public const ulong FirstCreatedThreadId = 2;
    public const ulong CapabilityCurrentThreadId = 1UL << 0;
    public const ulong CapabilityYield = 1UL << 1;
    public const ulong CapabilityMockThreadLifecycle = 1UL << 2;
    public const ulong CapabilityImmediateSleep = 1UL << 3;
    public const ulong CapabilitySingleThreadEarlyMode = 1UL << 4;
}
