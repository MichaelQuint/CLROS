namespace OS.Kernel.Diagnostics;

public readonly struct SerialPortConfigurationResult
{
    private SerialPortConfigurationResult(bool isInitialized, SerialPortConfigurationStatus status, SerialPortConfiguration configuration)
    {
        IsInitialized = isInitialized;
        Status = status;
        Configuration = configuration;
    }

    public bool IsInitialized
    {
        get;
    }
    public SerialPortConfigurationStatus Status
    {
        get;
    }
    public SerialPortConfiguration Configuration
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == SerialPortConfigurationStatus.Success && Configuration.BasePort != 0 && Configuration.InputClockHz != 0 && Configuration.BaudRate != 0;

    public static SerialPortConfigurationResult Success(SerialPortConfiguration configuration)
    {
        return new SerialPortConfigurationResult(true, SerialPortConfigurationStatus.Success, configuration);
    }

    public static SerialPortConfigurationResult Fail(SerialPortConfigurationStatus status)
    {
        return new SerialPortConfigurationResult(true, status, default);
    }
}
