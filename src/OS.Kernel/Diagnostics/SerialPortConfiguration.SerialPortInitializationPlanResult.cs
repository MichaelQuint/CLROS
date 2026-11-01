namespace OS.Kernel.Diagnostics;
public readonly struct SerialPortInitializationPlanResult
{
    private SerialPortInitializationPlanResult(bool isInitialized, SerialPortConfigurationStatus status, int writesEmitted, int requiredWriteCount, SerialPortDivisorLatch divisorLatch)
    {
        IsInitialized = isInitialized;
        Status = status;
        WritesEmitted = writesEmitted;
        RequiredWriteCount = requiredWriteCount;
        DivisorLatch = divisorLatch;
    }

    public bool IsInitialized { get; }
    public SerialPortConfigurationStatus Status { get; }
    public int WritesEmitted { get; }
    public int RequiredWriteCount { get; }
    public SerialPortDivisorLatch DivisorLatch { get; }
    public bool Succeeded => IsInitialized && Status == SerialPortConfigurationStatus.Success && RequiredWriteCount > 0 && WritesEmitted == RequiredWriteCount && DivisorLatch.Divisor != 0;

    public static SerialPortInitializationPlanResult Success(int writesEmitted, int requiredWriteCount, SerialPortDivisorLatch divisorLatch)
    {
        return new SerialPortInitializationPlanResult(true, SerialPortConfigurationStatus.Success, writesEmitted, requiredWriteCount, divisorLatch);
    }

    public static SerialPortInitializationPlanResult Fail(SerialPortConfigurationStatus status, int requiredWriteCount)
    {
        return new SerialPortInitializationPlanResult(true, status, 0, requiredWriteCount, default);
    }
}
