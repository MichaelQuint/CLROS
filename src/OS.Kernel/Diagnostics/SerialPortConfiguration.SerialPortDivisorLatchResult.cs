namespace OS.Kernel.Diagnostics;
public readonly struct SerialPortDivisorLatchResult
{
    private SerialPortDivisorLatchResult(bool isInitialized, SerialPortConfigurationStatus status, SerialPortDivisorLatch divisorLatch)
    {
        IsInitialized = isInitialized;
        Status = status;
        DivisorLatch = divisorLatch;
    }

    public bool IsInitialized { get; }
    public SerialPortConfigurationStatus Status { get; }
    public SerialPortDivisorLatch DivisorLatch { get; }
    public bool Succeeded => IsInitialized && Status == SerialPortConfigurationStatus.Success && DivisorLatch.Divisor != 0;

    public static SerialPortDivisorLatchResult Success(SerialPortDivisorLatch divisorLatch)
    {
        return new SerialPortDivisorLatchResult(true, SerialPortConfigurationStatus.Success, divisorLatch);
    }

    public static SerialPortDivisorLatchResult Fail(SerialPortConfigurationStatus status)
    {
        return new SerialPortDivisorLatchResult(true, status, default);
    }
}
