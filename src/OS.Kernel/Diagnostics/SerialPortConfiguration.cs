namespace OS.Kernel.Diagnostics;

public readonly struct SerialPortConfiguration
{
    public const int Com1BasePort = 0x3f8;
    public const uint DefaultInputClockHz = 1_843_200;
    public const uint DefaultBaudRate = 115_200;
    public const byte DefaultDataBits = 8;
    public const byte DefaultStopBits = 1;
    public const int DataRegisterOffset = 0;
    public const int InterruptEnableRegisterOffset = 1;
    public const int FifoControlRegisterOffset = 2;
    public const int LineControlRegisterOffset = 3;
    public const int ModemControlRegisterOffset = 4;
    public const int MaximumRegisterOffset = 7;
    public const byte DisableInterruptsValue = 0x00;
    public const byte EnableDivisorLatchAccessValue = 0x80;
    public const byte LineControl8N1Value = 0x03;
    public const byte FifoControlEnableAndClearValue = 0xc7;
    public const byte ModemControlReadyValue = 0x0b;
    public const int RequiredInitializationWriteCount = 7;
    private const ulong BaudRateDivisorMultiplier = 16;
    private SerialPortConfiguration(ushort basePort, uint inputClockHz, uint baudRate)
    {
        BasePort = basePort;
        InputClockHz = inputClockHz;
        BaudRate = baudRate;
    }

    public ushort BasePort
    {
        get;
    }
    public uint InputClockHz
    {
        get;
    }
    public uint BaudRate
    {
        get;
    }
    public byte DataBits => DefaultDataBits;
    public SerialParity Parity => SerialParity.None;
    public byte StopBits => DefaultStopBits;
    public byte LineControlValue => LineControl8N1Value;

    public static SerialPortConfiguration Com1Default
    {
        get
        {
            return new SerialPortConfiguration(Com1BasePort, DefaultInputClockHz, DefaultBaudRate);
        }
    }

    public static SerialPortConfigurationResult Create(int basePort, uint inputClockHz, uint baudRate)
    {
        SerialPortConfigurationStatus status = Validate(basePort, inputClockHz, baudRate);
        if (status != SerialPortConfigurationStatus.Success)
        {
            return SerialPortConfigurationResult.Fail(status);
        }

        return SerialPortConfigurationResult.Success(new SerialPortConfiguration((ushort)basePort, inputClockHz, baudRate));
    }

    public static SerialPortDivisorLatchResult CalculateDivisorLatch(uint inputClockHz, uint baudRate)
    {
        SerialPortConfigurationStatus status = ValidateClockAndBaud(inputClockHz, baudRate);
        if (status != SerialPortConfigurationStatus.Success)
        {
            return SerialPortDivisorLatchResult.Fail(status);
        }

        ulong divisor = inputClockHz / ((ulong)baudRate * BaudRateDivisorMultiplier);
        return SerialPortDivisorLatchResult.Success(new SerialPortDivisorLatch((ushort)divisor));
    }

    public SerialPortDivisorLatchResult CalculateDivisorLatch()
    {
        return CalculateDivisorLatch(InputClockHz, BaudRate);
    }

    public SerialPortInitializationPlanResult EmitInitializationSequence(Span<SerialPortWrite> destination)
    {
        SerialPortConfigurationStatus status = Validate(BasePort, InputClockHz, BaudRate);
        if (status != SerialPortConfigurationStatus.Success)
        {
            return SerialPortInitializationPlanResult.Fail(status, RequiredInitializationWriteCount);
        }

        SerialPortDivisorLatchResult divisorResult = CalculateDivisorLatch(InputClockHz, BaudRate);
        if (!divisorResult.Succeeded)
        {
            return SerialPortInitializationPlanResult.Fail(divisorResult.Status, RequiredInitializationWriteCount);
        }

        if (destination.Length < RequiredInitializationWriteCount)
        {
            return SerialPortInitializationPlanResult.Fail(SerialPortConfigurationStatus.OutputBufferTooSmall, RequiredInitializationWriteCount);
        }

        SerialPortDivisorLatch divisorLatch = divisorResult.DivisorLatch;
        destination[0] = CreateWrite(InterruptEnableRegisterOffset, DisableInterruptsValue);
        destination[1] = CreateWrite(LineControlRegisterOffset, EnableDivisorLatchAccessValue);
        destination[2] = CreateWrite(DataRegisterOffset, divisorLatch.LowByte);
        destination[3] = CreateWrite(InterruptEnableRegisterOffset, divisorLatch.HighByte);
        destination[4] = CreateWrite(LineControlRegisterOffset, LineControl8N1Value);
        destination[5] = CreateWrite(FifoControlRegisterOffset, FifoControlEnableAndClearValue);
        destination[6] = CreateWrite(ModemControlRegisterOffset, ModemControlReadyValue);
        return SerialPortInitializationPlanResult.Success(RequiredInitializationWriteCount, RequiredInitializationWriteCount, divisorLatch);
    }

    private static SerialPortConfigurationStatus Validate(int basePort, uint inputClockHz, uint baudRate)
    {
        if (basePort <= 0 || basePort > ushort.MaxValue - MaximumRegisterOffset)
        {
            return SerialPortConfigurationStatus.InvalidBasePort;
        }

        return ValidateClockAndBaud(inputClockHz, baudRate);
    }

    private static SerialPortConfigurationStatus ValidateClockAndBaud(uint inputClockHz, uint baudRate)
    {
        if (inputClockHz == 0)
        {
            return SerialPortConfigurationStatus.InvalidInputClock;
        }

        if (baudRate == 0)
        {
            return SerialPortConfigurationStatus.InvalidBaudRate;
        }

        ulong divisorDenominator = (ulong)baudRate * BaudRateDivisorMultiplier;
        if ((ulong)inputClockHz % divisorDenominator != 0)
        {
            return SerialPortConfigurationStatus.InvalidBaudRate;
        }

        ulong divisor = inputClockHz / divisorDenominator;
        if (divisor == 0 || divisor > ushort.MaxValue)
        {
            return SerialPortConfigurationStatus.DivisorOutOfRange;
        }

        return SerialPortConfigurationStatus.Success;
    }

    private SerialPortWrite CreateWrite(int offset, byte value)
    {
        return new SerialPortWrite((ushort)(BasePort + offset), value);
    }
}
