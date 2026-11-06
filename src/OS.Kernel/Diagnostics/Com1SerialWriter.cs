namespace OS.Kernel.Diagnostics;

public sealed class Com1SerialWriter
{
    public const int LineStatusRegisterOffset = 5;
    public const byte LineStatusTransmitEmptyMask = 0x20;
    public const int DefaultTransmitReadyPollLimit = 1024;
    private readonly IBytePortBackend _portBackend;
    private readonly SerialPortConfiguration _configuration;
    private readonly int _transmitReadyPollLimit;
    private bool _initialized;
    public Com1SerialWriter(IBytePortBackend portBackend) : this(portBackend, SerialPortConfiguration.Com1Default, DefaultTransmitReadyPollLimit)
    {
    }

    public Com1SerialWriter(IBytePortBackend portBackend, SerialPortConfiguration configuration, int transmitReadyPollLimit)
    {
        _portBackend = portBackend;
        _configuration = configuration;
        _transmitReadyPollLimit = transmitReadyPollLimit <= 0 ? 1 : transmitReadyPollLimit;
    }

    public ushort DataRegisterPort
    {
        get
        {
            return (ushort)(_configuration.BasePort + SerialPortConfiguration.DataRegisterOffset);
        }
    }

    public ushort LineStatusRegisterPort
    {
        get
        {
            return (ushort)(_configuration.BasePort + LineStatusRegisterOffset);
        }
    }

    public int TransmitReadyPollLimit => _transmitReadyPollLimit;
    public bool IsInitialized => _initialized;

    public Com1SerialWriterResult Initialize()
    {
        Span<SerialPortWrite> initializationWrites = stackalloc SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount];
        SerialPortInitializationPlanResult plan = _configuration.EmitInitializationSequence(initializationWrites);
        if (!plan.Succeeded)
        {
            return Com1SerialWriterResult.InitializationFailed(plan.Status);
        }

        for (int index = 0; index < plan.WritesEmitted; index++)
        {
            SerialPortWrite write = initializationWrites[index];
            if (!_portBackend.TryWriteByte(write.Port, write.Value))
            {
                return Com1SerialWriterResult.Fail(Com1SerialWriterStatus.BackendFailure, 0, index, 0);
            }
        }

        _initialized = true;
        return Com1SerialWriterResult.Success(0, plan.WritesEmitted, 0);
    }

    public Com1SerialWriterResult WriteAscii(ReadOnlySpan<byte> asciiBytes)
    {
        if (!_initialized)
        {
            return Com1SerialWriterResult.Fail(Com1SerialWriterStatus.NotInitialized, 0, 0, 0);
        }

        for (int index = 0; index < asciiBytes.Length; index++)
        {
            if (asciiBytes[index] > 0x7f)
            {
                return Com1SerialWriterResult.Fail(Com1SerialWriterStatus.InvalidAscii, 0, 0, 0);
            }
        }

        int totalPolls = 0;
        for (int index = 0; index < asciiBytes.Length; index++)
        {
            if (!WaitForTransmitEmpty(out int polls, out Com1SerialWriterStatus waitStatus))
            {
                return Com1SerialWriterResult.Fail(waitStatus, index, index, totalPolls + polls);
            }

            totalPolls += polls;
            if (!_portBackend.TryWriteByte(DataRegisterPort, asciiBytes[index]))
            {
                return Com1SerialWriterResult.Fail(Com1SerialWriterStatus.BackendFailure, index, index, totalPolls);
            }
        }

        return Com1SerialWriterResult.Success(asciiBytes.Length, asciiBytes.Length, totalPolls);
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
