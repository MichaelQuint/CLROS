using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static class Com1PlatformSerialTransportTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.com1_transport writes through fake com1 backend", TestWritesThroughFakeCom1Backend, ref failures);
        Run("diagnostics.com1_transport writes owned serial after uefi gone", TestWritesOwnedSerialAfterUefiGone, ref failures);
        Run("diagnostics.com1_transport reports unavailable readiness", TestReportsUnavailableReadiness, ref failures);
        Run("diagnostics.com1_transport maps initialization failure", TestMapsInitializationFailure, ref failures);
        Run("diagnostics.com1_transport maps transmit timeout", TestMapsTransmitTimeout, ref failures);
        Run("diagnostics.com1_transport reports short write", TestReportsShortWrite, ref failures);
        Run("diagnostics.com1_transport results contain no managed references", TestResultStructsContainNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("COM1 platform serial transport host tests failed: " + failures);
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

    private static bool TestWritesThroughFakeCom1Backend()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend);

        PlatformSerialTransportResult first = transport.Write("OS"u8);
        PlatformSerialTransportResult second = transport.Write("!"u8);

        return first.Succeeded
            && first.Status == PlatformSerialTransportStatus.Success
            && first.BytesWritten == 2
            && first.BackendStatus == Com1PlatformSerialTransport.EncodeBackendStatus(
                Com1SerialWriterResult.Success(2, 2, 2))
            && second.Succeeded
            && second.BytesWritten == 1
            && backend.InitializationWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount
            && backend.DataWriteCount == 3
            && backend.ReadCount == 3
            && backend.DataByteAt(0) == (byte)'O'
            && backend.DataByteAt(1) == (byte)'S'
            && backend.DataByteAt(2) == (byte)'!';
    }

    private static bool TestWritesOwnedSerialAfterUefiGone()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        IPlatformSerial serial = new GuardedPlatformSerial(
            firmware,
            new Com1PlatformSerialTransport(backend));

        OwnedSerialDiagnosticWriteResult result = OwnedSerialDiagnostics.WriteMarkerLine(
            serial,
            "TEST_PASS diagnostics.com1_transport owned_after_uefi_gone"u8);

        return result.Succeeded
            && result.SerialStatus == PlatformSerialWriteStatus.Success
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.TransportKind == PlatformSerialTransportKind.Owned
            && result.BytesWritten == result.RequiredBytes
            && backend.InitializationWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount
            && backend.DataWriteCount == result.RequiredBytes
            && backend.ReadCount == result.RequiredBytes
            && backend.DataStartsWith("TEST_PASS diagnostics.com1_transport"u8)
            && backend.DataEndsWithCrLf();
    }

    private static bool TestReportsUnavailableReadiness()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(
            backend,
            default);

        PlatformSerialTransportResult result = transport.Write("OS"u8);

        return !result.Succeeded
            && result.Status == PlatformSerialTransportStatus.Unavailable
            && result.BytesWritten == 0
            && result.BackendStatus == 0
            && backend.WriteCount == 0
            && backend.ReadCount == 0;
    }

    private static bool TestMapsInitializationFailure()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.FailWriteAfter = 2;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend);

        PlatformSerialTransportResult result = transport.Write("A"u8);

        return !result.Succeeded
            && result.Status == PlatformSerialTransportStatus.BackendFailure
            && result.BytesWritten == 0
            && DecodeWriterStatus(result.BackendStatus) == Com1SerialWriterStatus.BackendFailure
            && DecodePortWrites(result.BackendStatus) == 2
            && backend.WriteCount == 2
            && backend.ReadCount == 0;
    }

    private static bool TestMapsTransmitTimeout()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = 0;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(
            backend,
            SerialPortConfiguration.Com1Default,
            transmitReadyPollLimit: 2);

        PlatformSerialTransportResult result = transport.Write("A"u8);

        return !result.Succeeded
            && result.Status == PlatformSerialTransportStatus.BackendFailure
            && result.BytesWritten == 0
            && DecodeWriterStatus(result.BackendStatus) == Com1SerialWriterStatus.TransmitTimeout
            && DecodeLinePolls(result.BackendStatus) == 2
            && backend.InitializationWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount
            && backend.ReadCount == 2;
    }

    private static bool TestReportsShortWrite()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.SetLineStatusSequence(
            Com1SerialWriter.LineStatusTransmitEmptyMask,
            0);
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(
            backend,
            SerialPortConfiguration.Com1Default,
            transmitReadyPollLimit: 1);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);

        PlatformSerialWriteResult result = serial.Write("AB"u8);

        return !result.Succeeded
            && result.Status == PlatformSerialWriteStatus.ShortWrite
            && result.BytesWritten == 1
            && result.RequestedBytes == 2
            && DecodeWriterStatus(result.BackendStatus) == Com1SerialWriterStatus.TransmitTimeout
            && DecodeBytesWritten(result.BackendStatus) == 1
            && backend.DataWriteCount == 1
            && backend.DataByteAt(0) == (byte)'A';
    }

    private static bool TestResultStructsContainNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformSerialTransportResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformSerialWriteResult>();
    }

    private static Com1SerialWriterStatus DecodeWriterStatus(ulong value)
    {
        return (Com1SerialWriterStatus)(value & 0xff);
    }

    private static int DecodeBytesWritten(ulong value)
    {
        return (int)((value >> 16) & 0xffff);
    }

    private static int DecodePortWrites(ulong value)
    {
        return (int)((value >> 32) & 0xffff);
    }

    private static int DecodeLinePolls(ulong value)
    {
        return (int)((value >> 48) & 0xffff);
    }

    private sealed class FakeBytePortBackend : IBytePortBackend
    {
        private readonly SerialPortWrite[] _writes = new SerialPortWrite[1024];
        private readonly byte[] _dataBytes = new byte[1024];
        private readonly byte[] _lineStatusSequence = new byte[64];
        private int _lineStatusSequenceLength;

        public byte LineStatusValue { get; set; }

        public int FailWriteAfter { get; set; } = -1;

        public int WriteCount { get; private set; }

        public int ReadCount { get; private set; }

        public int InitializationWriteCount { get; private set; }

        public int DataWriteCount { get; private set; }

        public bool TryReadByte(ushort port, out byte value)
        {
            ReadCount++;

            if (_lineStatusSequenceLength != 0 && ReadCount <= _lineStatusSequenceLength)
            {
                value = _lineStatusSequence[ReadCount - 1];
                return true;
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

            if (InitializationWriteCount < SerialPortConfiguration.RequiredInitializationWriteCount)
            {
                InitializationWriteCount++;
            }
            else if (port == SerialPortConfiguration.Com1BasePort)
            {
                _dataBytes[DataWriteCount] = value;
                DataWriteCount++;
            }

            return true;
        }

        public byte DataByteAt(int index)
        {
            if (index < 0 || index >= DataWriteCount)
            {
                return 0;
            }

            return _dataBytes[index];
        }

        public bool DataStartsWith(ReadOnlySpan<byte> prefix)
        {
            if (DataWriteCount < prefix.Length)
            {
                return false;
            }

            for (int index = 0; index < prefix.Length; index++)
            {
                if (DataByteAt(index) != prefix[index])
                {
                    return false;
                }
            }

            return true;
        }

        public bool DataEndsWithCrLf()
        {
            return DataWriteCount >= 2
                && DataByteAt(DataWriteCount - 2) == OwnedSerialDiagnostics.CarriageReturn
                && DataByteAt(DataWriteCount - 1) == OwnedSerialDiagnostics.LineFeed;
        }

        public void SetLineStatusSequence(byte first, byte second)
        {
            _lineStatusSequence[0] = first;
            _lineStatusSequence[1] = second;
            _lineStatusSequenceLength = 2;
        }
    }
}
