namespace OS.Kernel.Diagnostics;

public interface IBytePortBackend
{
    bool TryReadByte(ushort port, out byte value);

    bool TryWriteByte(ushort port, byte value);
}

public enum Com1SerialWriterStatus
{
    Success = 0,
    InitializationFailed = 1,
    NotInitialized = 2,
    TransmitTimeout = 3,
    InvalidAscii = 4,
    BackendFailure = 5
}

public readonly struct Com1SerialWriterResult
{
    private Com1SerialWriterResult(
        bool isInitialized,
        Com1SerialWriterStatus status,
        SerialPortConfigurationStatus configurationStatus,
        int bytesWritten,
        int portWritesEmitted,
        int lineStatusPolls)
    {
        IsInitialized = isInitialized;
        Status = status;
        ConfigurationStatus = configurationStatus;
        BytesWritten = bytesWritten;
        PortWritesEmitted = portWritesEmitted;
        LineStatusPolls = lineStatusPolls;
    }

    public bool IsInitialized { get; }

    public Com1SerialWriterStatus Status { get; }

    public SerialPortConfigurationStatus ConfigurationStatus { get; }

    public int BytesWritten { get; }

    public int PortWritesEmitted { get; }

    public int LineStatusPolls { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == Com1SerialWriterStatus.Success &&
        ConfigurationStatus == SerialPortConfigurationStatus.Success &&
        BytesWritten >= 0 &&
        PortWritesEmitted >= 0 &&
        LineStatusPolls >= 0;

    public static Com1SerialWriterResult Success(
        int bytesWritten,
        int portWritesEmitted,
        int lineStatusPolls)
    {
        return new Com1SerialWriterResult(
            true,
            Com1SerialWriterStatus.Success,
            SerialPortConfigurationStatus.Success,
            bytesWritten,
            portWritesEmitted,
            lineStatusPolls);
    }

    public static Com1SerialWriterResult Fail(
        Com1SerialWriterStatus status,
        int bytesWritten,
        int portWritesEmitted,
        int lineStatusPolls)
    {
        return new Com1SerialWriterResult(
            true,
            status,
            SerialPortConfigurationStatus.Success,
            bytesWritten,
            portWritesEmitted,
            lineStatusPolls);
    }

    public static Com1SerialWriterResult InitializationFailed(
        SerialPortConfigurationStatus configurationStatus)
    {
        return new Com1SerialWriterResult(
            true,
            Com1SerialWriterStatus.InitializationFailed,
            configurationStatus,
            0,
            0,
            0);
    }
}

public sealed class Com1SerialWriter
{
    public const int LineStatusRegisterOffset = 5;
    public const byte LineStatusTransmitEmptyMask = 0x20;
    public const int DefaultTransmitReadyPollLimit = 1024;

    private readonly IBytePortBackend _portBackend;
    private readonly SerialPortConfiguration _configuration;
    private readonly int _transmitReadyPollLimit;
    private bool _initialized;

    public Com1SerialWriter(IBytePortBackend portBackend)
        : this(
            portBackend,
            SerialPortConfiguration.Com1Default,
            DefaultTransmitReadyPollLimit)
    {
    }

    public Com1SerialWriter(
        IBytePortBackend portBackend,
        SerialPortConfiguration configuration,
        int transmitReadyPollLimit)
    {
        _portBackend = portBackend;
        _configuration = configuration;
        _transmitReadyPollLimit =
            transmitReadyPollLimit <= 0 ? 1 : transmitReadyPollLimit;
    }

    public ushort DataRegisterPort
    {
        get { return (ushort)(_configuration.BasePort + SerialPortConfiguration.DataRegisterOffset); }
    }

    public ushort LineStatusRegisterPort
    {
        get { return (ushort)(_configuration.BasePort + LineStatusRegisterOffset); }
    }

    public int TransmitReadyPollLimit => _transmitReadyPollLimit;

    public bool IsInitialized => _initialized;

    public Com1SerialWriterResult Initialize()
    {
        Span<SerialPortWrite> initializationWrites =
            stackalloc SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount];

        SerialPortInitializationPlanResult plan =
            _configuration.EmitInitializationSequence(initializationWrites);
        if (!plan.Succeeded)
        {
            return Com1SerialWriterResult.InitializationFailed(plan.Status);
        }

        for (int index = 0; index < plan.WritesEmitted; index++)
        {
            SerialPortWrite write = initializationWrites[index];
            if (!_portBackend.TryWriteByte(write.Port, write.Value))
            {
                return Com1SerialWriterResult.Fail(
                    Com1SerialWriterStatus.BackendFailure,
                    0,
                    index,
                    0);
            }
        }

        _initialized = true;
        return Com1SerialWriterResult.Success(0, plan.WritesEmitted, 0);
    }

    public Com1SerialWriterResult WriteAscii(ReadOnlySpan<byte> asciiBytes)
    {
        if (!_initialized)
        {
            return Com1SerialWriterResult.Fail(
                Com1SerialWriterStatus.NotInitialized,
                0,
                0,
                0);
        }

        for (int index = 0; index < asciiBytes.Length; index++)
        {
            if (asciiBytes[index] > 0x7f)
            {
                return Com1SerialWriterResult.Fail(
                    Com1SerialWriterStatus.InvalidAscii,
                    0,
                    0,
                    0);
            }
        }

        int totalPolls = 0;
        for (int index = 0; index < asciiBytes.Length; index++)
        {
            if (!WaitForTransmitEmpty(out int polls, out Com1SerialWriterStatus waitStatus))
            {
                return Com1SerialWriterResult.Fail(
                    waitStatus,
                    index,
                    index,
                    totalPolls + polls);
            }

            totalPolls += polls;
            if (!_portBackend.TryWriteByte(DataRegisterPort, asciiBytes[index]))
            {
                return Com1SerialWriterResult.Fail(
                    Com1SerialWriterStatus.BackendFailure,
                    index,
                    index,
                    totalPolls);
            }
        }

        return Com1SerialWriterResult.Success(
            asciiBytes.Length,
            asciiBytes.Length,
            totalPolls);
    }

    private bool WaitForTransmitEmpty(out int polls, out Com1SerialWriterStatus status)
    {
        polls = 0;
        for (int attempt = 0; attempt < _transmitReadyPollLimit; attempt++)
        {
            polls++;
            if (!_portBackend.TryReadByte(LineStatusRegisterPort, out byte lineStatus))
            {
                status = Com1SerialWriterStatus.BackendFailure;
                return false;
            }

            if ((lineStatus & LineStatusTransmitEmptyMask) != 0)
            {
                status = Com1SerialWriterStatus.Success;
                return true;
            }
        }

        status = Com1SerialWriterStatus.TransmitTimeout;
        return false;
    }
}
