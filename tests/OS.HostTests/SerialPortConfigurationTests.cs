using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class SerialPortConfigurationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("serial port config exposes com1 defaults", TestExposesCom1Defaults, ref failures);
        Run("serial port config calculates divisor latch", TestCalculatesDivisorLatch, ref failures);
        Run("serial port config emits init sequence", TestEmitsInitializationSequence, ref failures);
        Run("serial port config rejects invalid inputs", TestRejectsInvalidInputs, ref failures);
        Run("serial port config default results are not success", TestDefaultResultsAreNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Serial port configuration host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestExposesCom1Defaults()
    {
        SerialPortConfiguration config = SerialPortConfiguration.Com1Default;
        SerialPortConfigurationResult result = SerialPortConfiguration.Create(
            SerialPortConfiguration.Com1BasePort,
            SerialPortConfiguration.DefaultInputClockHz,
            SerialPortConfiguration.DefaultBaudRate);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == SerialPortConfigurationStatus.Success
            && config.BasePort == 0x03f8
            && config.InputClockHz == 1_843_200
            && config.BaudRate == 115_200
            && config.DataBits == 8
            && config.Parity == SerialParity.None
            && config.StopBits == 1
            && config.LineControlValue == 0x03
            && result.Configuration.BasePort == config.BasePort
            && result.Configuration.InputClockHz == config.InputClockHz
            && result.Configuration.BaudRate == config.BaudRate;
    }

    private static bool TestCalculatesDivisorLatch()
    {
        SerialPortDivisorLatchResult defaultResult = SerialPortConfiguration.CalculateDivisorLatch(
            SerialPortConfiguration.DefaultInputClockHz,
            SerialPortConfiguration.DefaultBaudRate);

        SerialPortDivisorLatchResult lowSpeedResult = SerialPortConfiguration.CalculateDivisorLatch(
            SerialPortConfiguration.DefaultInputClockHz,
            300);

        return defaultResult.Succeeded
            && defaultResult.IsInitialized
            && defaultResult.Status == SerialPortConfigurationStatus.Success
            && defaultResult.DivisorLatch.Divisor == 1
            && defaultResult.DivisorLatch.LowByte == 0x01
            && defaultResult.DivisorLatch.HighByte == 0x00
            && lowSpeedResult.Succeeded
            && lowSpeedResult.IsInitialized
            && lowSpeedResult.DivisorLatch.Divisor == 384
            && lowSpeedResult.DivisorLatch.LowByte == 0x80
            && lowSpeedResult.DivisorLatch.HighByte == 0x01;
    }

    private static bool TestEmitsInitializationSequence()
    {
        SerialPortConfiguration config = SerialPortConfiguration.Com1Default;
        SerialPortWrite[] writes = new SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount];

        SerialPortInitializationPlanResult result = config.EmitInitializationSequence(writes);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == SerialPortConfigurationStatus.Success
            && result.WritesEmitted == SerialPortConfiguration.RequiredInitializationWriteCount
            && result.RequiredWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount
            && result.DivisorLatch.Divisor == 1
            && IsWrite(writes[0], 0x03f9, 0x00)
            && IsWrite(writes[1], 0x03fb, 0x80)
            && IsWrite(writes[2], 0x03f8, 0x01)
            && IsWrite(writes[3], 0x03f9, 0x00)
            && IsWrite(writes[4], 0x03fb, 0x03)
            && IsWrite(writes[5], 0x03fa, 0xc7)
            && IsWrite(writes[6], 0x03fc, 0x0b);
    }

    private static bool TestRejectsInvalidInputs()
    {
        SerialPortConfigurationResult zeroBase = SerialPortConfiguration.Create(
            0,
            SerialPortConfiguration.DefaultInputClockHz,
            SerialPortConfiguration.DefaultBaudRate);

        SerialPortConfigurationResult overflowingBase = SerialPortConfiguration.Create(
            0x10000,
            SerialPortConfiguration.DefaultInputClockHz,
            SerialPortConfiguration.DefaultBaudRate);

        SerialPortDivisorLatchResult zeroClock = SerialPortConfiguration.CalculateDivisorLatch(
            0,
            SerialPortConfiguration.DefaultBaudRate);

        SerialPortDivisorLatchResult zeroBaud = SerialPortConfiguration.CalculateDivisorLatch(
            SerialPortConfiguration.DefaultInputClockHz,
            0);

        SerialPortDivisorLatchResult outOfRangeDivisor = SerialPortConfiguration.CalculateDivisorLatch(
            SerialPortConfiguration.DefaultInputClockHz,
            1);

        SerialPortWrite[] shortBuffer = new SerialPortWrite[SerialPortConfiguration.RequiredInitializationWriteCount - 1];
        shortBuffer[0] = new SerialPortWrite(0xffff, 0xa5);

        SerialPortInitializationPlanResult shortBufferResult =
            SerialPortConfiguration.Com1Default.EmitInitializationSequence(shortBuffer);

        return !zeroBase.Succeeded
            && zeroBase.IsInitialized
            && zeroBase.Status == SerialPortConfigurationStatus.InvalidBasePort
            && !overflowingBase.Succeeded
            && overflowingBase.IsInitialized
            && overflowingBase.Status == SerialPortConfigurationStatus.InvalidBasePort
            && !zeroClock.Succeeded
            && zeroClock.IsInitialized
            && zeroClock.Status == SerialPortConfigurationStatus.InvalidInputClock
            && !zeroBaud.Succeeded
            && zeroBaud.IsInitialized
            && zeroBaud.Status == SerialPortConfigurationStatus.InvalidBaudRate
            && !outOfRangeDivisor.Succeeded
            && outOfRangeDivisor.IsInitialized
            && outOfRangeDivisor.Status == SerialPortConfigurationStatus.DivisorOutOfRange
            && !shortBufferResult.Succeeded
            && shortBufferResult.IsInitialized
            && shortBufferResult.Status == SerialPortConfigurationStatus.OutputBufferTooSmall
            && shortBufferResult.RequiredWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount
            && shortBufferResult.WritesEmitted == 0
            && IsWrite(shortBuffer[0], 0xffff, 0xa5);
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        SerialPortConfigurationResult configuration = default;
        SerialPortDivisorLatchResult divisorLatch = default;
        SerialPortInitializationPlanResult initialization = default;

        return !configuration.IsInitialized
            && !configuration.Succeeded
            && configuration.Status == SerialPortConfigurationStatus.Success
            && configuration.Configuration.BasePort == 0
            && !divisorLatch.IsInitialized
            && !divisorLatch.Succeeded
            && divisorLatch.Status == SerialPortConfigurationStatus.Success
            && divisorLatch.DivisorLatch.Divisor == 0
            && !initialization.IsInitialized
            && !initialization.Succeeded
            && initialization.Status == SerialPortConfigurationStatus.Success
            && initialization.WritesEmitted == 0
            && initialization.RequiredWriteCount == 0
            && initialization.DivisorLatch.Divisor == 0;
    }

    private static bool IsWrite(SerialPortWrite write, ushort port, byte value)
    {
        return write.Port == port && write.Value == value;
    }
}
