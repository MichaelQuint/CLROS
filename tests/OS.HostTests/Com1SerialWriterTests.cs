using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class Com1SerialWriterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("com1 writer initializes through port backend", TestInitializesThroughPortBackend, ref failures);
        Run("com1 writer writes ascii bytes when ready", TestWritesAsciiBytesWhenReady, ref failures);
        Run("com1 writer rejects writes before initialization", TestRejectsWritesBeforeInitialization, ref failures);
        Run("com1 writer reports transmit timeout", TestReportsTransmitTimeout, ref failures);
        Run("com1 writer reports backend failure", TestReportsBackendFailure, ref failures);
        Run("com1 writer default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("COM1 serial writer host tests failed: " + failures);
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

    private static bool TestInitializesThroughPortBackend()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        Com1SerialWriter writer = new Com1SerialWriter(backend);

        Com1SerialWriterResult result = writer.Initialize();

        return result.Succeeded
            && result.IsInitialized
            && result.Status == Com1SerialWriterStatus.Success
            && result.PortWritesEmitted == SerialPortConfiguration.RequiredInitializationWriteCount
            && result.BytesWritten == 0
            && result.LineStatusPolls == 0
            && writer.IsInitialized
            && backend.WriteCount == SerialPortConfiguration.RequiredInitializationWriteCount
            && backend.ReadCount == 0
            && IsWrite(backend.GetWrite(0), 0x03f9, 0x00)
            && IsWrite(backend.GetWrite(1), 0x03fb, 0x80)
            && IsWrite(backend.GetWrite(2), 0x03f8, 0x01)
            && IsWrite(backend.GetWrite(3), 0x03f9, 0x00)
            && IsWrite(backend.GetWrite(4), 0x03fb, 0x03)
            && IsWrite(backend.GetWrite(5), 0x03fa, 0xc7)
            && IsWrite(backend.GetWrite(6), 0x03fc, 0x0b);
    }

    private static bool TestWritesAsciiBytesWhenReady()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1SerialWriter writer = new Com1SerialWriter(backend);

        Com1SerialWriterResult initializeResult = writer.Initialize();
        backend.Clear();

        ReadOnlySpan<byte> payload = "OS"u8;
        Com1SerialWriterResult writeResult = writer.WriteAscii(payload);

        return initializeResult.Succeeded
            && initializeResult.IsInitialized
            && writeResult.Succeeded
            && writeResult.IsInitialized
            && writeResult.Status == Com1SerialWriterStatus.Success
            && writeResult.BytesWritten == 2
            && writeResult.PortWritesEmitted == 2
            && writeResult.LineStatusPolls == 2
            && backend.ReadCount == 2
            && backend.WriteCount == 2
            && backend.GetReadPort(0) == 0x03fd
            && backend.GetReadPort(1) == 0x03fd
            && IsWrite(backend.GetWrite(0), 0x03f8, (byte)'O')
            && IsWrite(backend.GetWrite(1), 0x03f8, (byte)'S');
    }

    private static bool TestRejectsWritesBeforeInitialization()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1SerialWriter writer = new Com1SerialWriter(backend);

        Com1SerialWriterResult result = writer.WriteAscii("X"u8);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == Com1SerialWriterStatus.NotInitialized
            && result.BytesWritten == 0
            && result.PortWritesEmitted == 0
            && result.LineStatusPolls == 0
            && backend.ReadCount == 0
            && backend.WriteCount == 0;
    }

    private static bool TestReportsTransmitTimeout()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = 0x00;
        Com1SerialWriter writer = new Com1SerialWriter(
            backend,
            SerialPortConfiguration.Com1Default,
            transmitReadyPollLimit: 3);

        Com1SerialWriterResult initializeResult = writer.Initialize();
        backend.Clear();

        Com1SerialWriterResult writeResult = writer.WriteAscii("A"u8);

        return initializeResult.Succeeded
            && initializeResult.IsInitialized
            && !writeResult.Succeeded
            && writeResult.IsInitialized
            && writeResult.Status == Com1SerialWriterStatus.TransmitTimeout
            && writeResult.BytesWritten == 0
            && writeResult.PortWritesEmitted == 0
            && writeResult.LineStatusPolls == 3
            && backend.ReadCount == 3
            && backend.WriteCount == 0
            && backend.GetReadPort(0) == 0x03fd
            && backend.GetReadPort(1) == 0x03fd
            && backend.GetReadPort(2) == 0x03fd;
    }

    private static bool TestReportsBackendFailure()
    {
        FakeBytePortBackend failingInitializeBackend = new FakeBytePortBackend();
        failingInitializeBackend.FailWriteAfter = 2;
        Com1SerialWriter initWriter = new Com1SerialWriter(failingInitializeBackend);

        Com1SerialWriterResult initResult = initWriter.Initialize();

        FakeBytePortBackend failingReadBackend = new FakeBytePortBackend();
        failingReadBackend.FailReads = true;
        Com1SerialWriter readWriter = new Com1SerialWriter(failingReadBackend);
        Com1SerialWriterResult readInitResult = readWriter.Initialize();
        failingReadBackend.Clear();
        failingReadBackend.FailReads = true;

        Com1SerialWriterResult readResult = readWriter.WriteAscii("A"u8);

        FakeBytePortBackend failingDataBackend = new FakeBytePortBackend();
        failingDataBackend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1SerialWriter dataWriter = new Com1SerialWriter(failingDataBackend);
        Com1SerialWriterResult dataInitResult = dataWriter.Initialize();
        failingDataBackend.Clear();
        failingDataBackend.FailWriteAfter = 0;

        Com1SerialWriterResult dataResult = dataWriter.WriteAscii("A"u8);

        FakeBytePortBackend asciiBackend = new FakeBytePortBackend();
        asciiBackend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1SerialWriter asciiWriter = new Com1SerialWriter(asciiBackend);
        Com1SerialWriterResult asciiInitResult = asciiWriter.Initialize();
        asciiBackend.Clear();

        Com1SerialWriterResult asciiResult = asciiWriter.WriteAscii(new byte[] { 0x80 });

        return !initResult.Succeeded
            && initResult.IsInitialized
            && initResult.Status == Com1SerialWriterStatus.BackendFailure
            && initResult.PortWritesEmitted == 2
            && readInitResult.Succeeded
            && readInitResult.IsInitialized
            && !readResult.Succeeded
            && readResult.IsInitialized
            && readResult.Status == Com1SerialWriterStatus.BackendFailure
            && readResult.LineStatusPolls == 1
            && readResult.BytesWritten == 0
            && dataInitResult.Succeeded
            && dataInitResult.IsInitialized
            && !dataResult.Succeeded
            && dataResult.IsInitialized
            && dataResult.Status == Com1SerialWriterStatus.BackendFailure
            && dataResult.LineStatusPolls == 1
            && dataResult.BytesWritten == 0
            && asciiInitResult.Succeeded
            && asciiInitResult.IsInitialized
            && !asciiResult.Succeeded
            && asciiResult.IsInitialized
            && asciiResult.Status == Com1SerialWriterStatus.InvalidAscii
            && asciiBackend.ReadCount == 0
            && asciiBackend.WriteCount == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        Com1SerialWriterResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == Com1SerialWriterStatus.Success
            && result.ConfigurationStatus == SerialPortConfigurationStatus.Success
            && result.BytesWritten == 0
            && result.PortWritesEmitted == 0
            && result.LineStatusPolls == 0;
    }

    private static bool IsWrite(SerialPortWrite write, ushort port, byte value)
    {
        return write.Port == port && write.Value == value;
    }

    private sealed class FakeBytePortBackend : IBytePortBackend
    {
        private readonly SerialPortWrite[] _writes = new SerialPortWrite[32];
        private readonly ushort[] _readPorts = new ushort[32];

        public byte LineStatusValue { get; set; }

        public int FailWriteAfter { get; set; } = -1;

        public bool FailReads { get; set; }

        public int WriteCount { get; private set; }

        public int ReadCount { get; private set; }

        public bool TryReadByte(ushort port, out byte value)
        {
            _readPorts[ReadCount] = port;
            ReadCount++;

            if (FailReads)
            {
                value = 0;
                return false;
            }

            value = LineStatusValue;
            return true;
        }

        public bool TryWriteByte(ushort port, byte value)
        {
            if (FailWriteAfter >= 0 && WriteCount >= FailWriteAfter)
            {
                return false;
            }

            _writes[WriteCount] = new SerialPortWrite(port, value);
            WriteCount++;
            return true;
        }

        public SerialPortWrite GetWrite(int index)
        {
            return _writes[index];
        }

        public ushort GetReadPort(int index)
        {
            return _readPorts[index];
        }

        public void Clear()
        {
            WriteCount = 0;
            ReadCount = 0;
            FailWriteAfter = -1;
            FailReads = false;
        }
    }
}
