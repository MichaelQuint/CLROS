using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static partial class Com1PlatformSerialTransportTests
{
    [HostTest("diagnostics.com1_transport writes through fake com1 backend", HostLane.Fast, 0)]
    private static bool TestWritesThroughFakeCom1Backend()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend);
        PlatformSerialTransportResult first = transport.Write("OS"u8);
        PlatformSerialTransportResult second = transport.Write("!"u8);
        return HostCheck.Result(first.Succeeded && first.Status == PlatformSerialTransportStatus.Success && first.BytesWritten == 2 && first.BackendStatus == Com1PlatformSerialTransport.EncodeBackendStatus(Com1SerialWriterResult.Success(2, 2, 2)) && second.Succeeded && second.BytesWritten == 1 && backend.InitializationWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount && backend.DataWriteCount == 3 && backend.ReadCount == 3 && backend.DataByteAt(0) == (byte)'O' && backend.DataByteAt(1) == (byte)'S' && backend.DataByteAt(2) == (byte)'!');
    }

    [HostTest("diagnostics.com1_transport writes owned serial after uefi gone", HostLane.Fast, 1)]
    private static bool TestWritesOwnedSerialAfterUefiGone()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, new Com1PlatformSerialTransport(backend));
        OwnedSerialDiagnosticWriteResult result = OwnedSerialDiagnostics.WriteMarkerLine(serial, "TEST_PASS diagnostics.com1_transport owned_after_uefi_gone"u8);
        return HostCheck.Result(result.Succeeded && result.SerialStatus == PlatformSerialWriteStatus.Success && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.TransportKind == PlatformSerialTransportKind.Owned && result.BytesWritten == result.RequiredBytes && backend.InitializationWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount && backend.DataWriteCount == result.RequiredBytes && backend.ReadCount == result.RequiredBytes && backend.DataStartsWith("TEST_PASS diagnostics.com1_transport"u8) && backend.DataEndsWithCrLf());
    }

    [HostTest("diagnostics.com1_transport reports unavailable readiness", HostLane.Fast, 2)]
    private static bool TestReportsUnavailableReadiness()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = Com1SerialWriter.LineStatusTransmitEmptyMask;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend, default);
        PlatformSerialTransportResult result = transport.Write("OS"u8);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformSerialTransportStatus.Unavailable && result.BytesWritten == 0 && result.BackendStatus == 0 && backend.WriteCount == 0 && backend.ReadCount == 0);
    }

    [HostTest("diagnostics.com1_transport maps initialization failure", HostLane.Fast, 3)]
    private static bool TestMapsInitializationFailure()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.FailWriteAfter = 2;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend);
        PlatformSerialTransportResult result = transport.Write("A"u8);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformSerialTransportStatus.BackendFailure && result.BytesWritten == 0 && DecodeWriterStatus(result.BackendStatus) == Com1SerialWriterStatus.BackendFailure && DecodePortWrites(result.BackendStatus) == 2 && backend.WriteCount == 2 && backend.ReadCount == 0);
    }

    [HostTest("diagnostics.com1_transport maps transmit timeout", HostLane.Fast, 4)]
    private static bool TestMapsTransmitTimeout()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.LineStatusValue = 0;
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend, SerialPortConfiguration.Com1Default, transmitReadyPollLimit: 2);
        PlatformSerialTransportResult result = transport.Write("A"u8);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformSerialTransportStatus.BackendFailure && result.BytesWritten == 0 && DecodeWriterStatus(result.BackendStatus) == Com1SerialWriterStatus.TransmitTimeout && DecodeLinePolls(result.BackendStatus) == 2 && backend.InitializationWriteCount == SerialPortConfiguration.RequiredInitializationWriteCount && backend.ReadCount == 2);
    }

    [HostTest("diagnostics.com1_transport reports short write", HostLane.Fast, 5)]
    private static bool TestReportsShortWrite()
    {
        FakeBytePortBackend backend = new FakeBytePortBackend();
        backend.SetLineStatusSequence(Com1SerialWriter.LineStatusTransmitEmptyMask, 0);
        Com1PlatformSerialTransport transport = new Com1PlatformSerialTransport(backend, SerialPortConfiguration.Com1Default, transmitReadyPollLimit: 1);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        PlatformSerialWriteResult result = serial.Write("AB"u8);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformSerialWriteStatus.ShortWrite && result.BytesWritten == 1 && result.RequestedBytes == 2 && DecodeWriterStatus(result.BackendStatus) == Com1SerialWriterStatus.TransmitTimeout && DecodeBytesWritten(result.BackendStatus) == 1 && backend.DataWriteCount == 1 && backend.DataByteAt(0) == (byte)'A');
    }

    [HostTest("diagnostics.com1_transport results contain no managed references", HostLane.Fast, 6)]
    private static bool TestResultStructsContainNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformSerialTransportResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformSerialWriteResult>());
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
}
