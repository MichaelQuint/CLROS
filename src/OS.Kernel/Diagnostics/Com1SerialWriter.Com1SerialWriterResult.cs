namespace OS.Kernel.Diagnostics;

public readonly struct Com1SerialWriterResult
{
    private Com1SerialWriterResult(bool isInitialized, Com1SerialWriterStatus status, SerialPortConfigurationStatus configurationStatus, int bytesWritten, int portWritesEmitted, int lineStatusPolls)
    {
        IsInitialized = isInitialized;
        Status = status;
        ConfigurationStatus = configurationStatus;
        BytesWritten = bytesWritten;
        PortWritesEmitted = portWritesEmitted;
        LineStatusPolls = lineStatusPolls;
    }

    public bool IsInitialized
    {
        get;
    }
    public Com1SerialWriterStatus Status
    {
        get;
    }
    public SerialPortConfigurationStatus ConfigurationStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int PortWritesEmitted
    {
        get;
    }
    public int LineStatusPolls
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == Com1SerialWriterStatus.Success && ConfigurationStatus == SerialPortConfigurationStatus.Success && BytesWritten >= 0 && PortWritesEmitted >= 0 && LineStatusPolls >= 0;

    public static Com1SerialWriterResult Success(int bytesWritten, int portWritesEmitted, int lineStatusPolls)
    {
        return new Com1SerialWriterResult(true, Com1SerialWriterStatus.Success, SerialPortConfigurationStatus.Success, bytesWritten, portWritesEmitted, lineStatusPolls);
    }

    public static Com1SerialWriterResult Fail(Com1SerialWriterStatus status, int bytesWritten, int portWritesEmitted, int lineStatusPolls)
    {
        return new Com1SerialWriterResult(true, status, SerialPortConfigurationStatus.Success, bytesWritten, portWritesEmitted, lineStatusPolls);
    }

    public static Com1SerialWriterResult InitializationFailed(SerialPortConfigurationStatus configurationStatus)
    {
        return new Com1SerialWriterResult(true, Com1SerialWriterStatus.InitializationFailed, configurationStatus, 0, 0, 0);
    }
}
