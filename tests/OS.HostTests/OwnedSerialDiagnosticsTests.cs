using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static partial class OwnedSerialDiagnosticsTests
{
    [HostTest("diagnostics.owned_serial writes firmware serial before uefi gone", HostLane.Fast, 0)]
    private static bool TestWritesFirmwareSerialBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        SerialTransportDouble transport = SerialTransportDouble.Success(PlatformSerialTransportKind.FirmwareBacked);
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        OwnedSerialDiagnosticWriteResult result = WriteTestPassMarker(serial, "firmware_before"u8);
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Status == OwnedSerialDiagnosticWriteStatus.Success && result.SerialStatus == PlatformSerialWriteStatus.Success && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.TransportKind == PlatformSerialTransportKind.FirmwareBacked && result.BytesWritten == result.RequiredBytes && transport.WriteCount == 1 && HasCrLf(transport.LastBytes) && StartsWith(transport.LastBytes, OwnedSerialDiagnostics.TestPassOwnedSerialPrefix));
    }

    [HostTest("diagnostics.owned_serial writes owned serial after uefi gone", HostLane.Fast, 1)]
    private static bool TestWritesOwnedSerialAfterUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        SerialTransportDouble transport = SerialTransportDouble.Success(PlatformSerialTransportKind.Owned);
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        OwnedSerialDiagnosticWriteResult result = WriteTestPassMarker(serial, "owned_after_uefi_gone"u8);
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Status == OwnedSerialDiagnosticWriteStatus.Success && result.SerialStatus == PlatformSerialWriteStatus.Success && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.TransportKind == PlatformSerialTransportKind.Owned && result.BytesWritten == result.RequiredBytes && transport.WriteCount == 1 && HasCrLf(transport.LastBytes) && StartsWith(transport.LastBytes, OwnedSerialDiagnostics.TestPassOwnedSerialPrefix));
    }

    [HostTest("diagnostics.owned_serial rejects firmware serial after uefi gone", HostLane.Fast, 2)]
    private static bool TestRejectsFirmwareSerialAfterUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        SerialTransportDouble transport = SerialTransportDouble.Success(PlatformSerialTransportKind.FirmwareBacked);
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        OwnedSerialDiagnosticWriteResult result = WriteTestPassMarker(serial, "firmware_rejected"u8);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == OwnedSerialDiagnosticWriteStatus.SerialWriteFailed && result.SerialStatus == PlatformSerialWriteStatus.UefiGoneRequiresOwnedSerial && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.TransportKind == PlatformSerialTransportKind.FirmwareBacked && result.BytesWritten == 0 && transport.WriteCount == 0);
    }

    [HostTest("diagnostics.owned_serial reports missing owned transport", HostLane.Fast, 3)]
    private static bool TestReportsMissingOwnedTransport()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        SerialTransportDouble transport = SerialTransportDouble.Success(PlatformSerialTransportKind.None);
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        OwnedSerialDiagnosticWriteResult result = WriteTestPassMarker(serial, "missing_transport"u8);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == OwnedSerialDiagnosticWriteStatus.SerialWriteFailed && result.SerialStatus == PlatformSerialWriteStatus.NoOwnedTransport && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.TransportKind == PlatformSerialTransportKind.None && result.BytesWritten == 0 && transport.WriteCount == 0);
    }

    [HostTest("diagnostics.owned_serial reports owned serial unavailable", HostLane.Fast, 4)]
    private static bool TestReportsOwnedSerialUnavailable()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        SerialTransportDouble transport = SerialTransportDouble.Unavailable(PlatformSerialTransportKind.Owned);
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        OwnedSerialDiagnosticWriteResult result = WriteTestPassMarker(serial, "unavailable"u8);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == OwnedSerialDiagnosticWriteStatus.SerialWriteFailed && result.SerialStatus == PlatformSerialWriteStatus.OwnedSerialUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.TransportKind == PlatformSerialTransportKind.Owned && result.BytesWritten == 0 && transport.WriteCount == 1);
    }

    [HostTest("diagnostics.owned_serial reports short write", HostLane.Fast, 5)]
    private static bool TestReportsShortWrite()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        SerialTransportDouble transport = SerialTransportDouble.ShortWrite(PlatformSerialTransportKind.Owned, missingBytes: 1);
        IPlatformSerial serial = new GuardedPlatformSerial(firmware, transport);
        OwnedSerialDiagnosticWriteResult result = WriteTestPassMarker(serial, "short_write"u8);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == OwnedSerialDiagnosticWriteStatus.SerialWriteFailed && result.SerialStatus == PlatformSerialWriteStatus.ShortWrite && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.TransportKind == PlatformSerialTransportKind.Owned && result.BytesWritten == result.RequiredBytes - 1 && transport.WriteCount == 1 && transport.LastBytes.Length == result.BytesWritten);
    }

    [HostTest("diagnostics.owned_serial formats boot and panic markers", HostLane.Fast, 6)]
    private static bool TestFormatsBootAndPanicMarkers()
    {
        Span<byte> bootText = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes];
        OwnedSerialSmokeMarkerFormatResult bootTextResult = OwnedSerialSmokeMarkers.FormatTestPassMarker("formatter"u8, bootText);
        Span<byte> bootMarker = stackalloc byte[128];
        OwnedSerialMarkerFormatResult boot = bootTextResult.Succeeded ? OwnedSerialDiagnostics.FormatMarkerLine(bootText.Slice(0, bootTextResult.BytesWritten), bootMarker) : default;
        Span<byte> panicText = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes];
        OwnedSerialSmokeMarkerFormatResult panicTextResult = OwnedSerialSmokeMarkers.FormatDiagnosticsPanicMarker("owned-serial"u8, panicText);
        Span<byte> panicMarker = stackalloc byte[128];
        OwnedSerialMarkerFormatResult panic = panicTextResult.Succeeded ? OwnedSerialDiagnostics.FormatMarkerLine(panicText.Slice(0, panicTextResult.BytesWritten), panicMarker) : default;
        Span<byte> tooSmallDestination = stackalloc byte[4];
        OwnedSerialMarkerFormatResult tooSmall = bootTextResult.Succeeded ? OwnedSerialDiagnostics.FormatMarkerLine(bootText.Slice(0, bootTextResult.BytesWritten), tooSmallDestination) : default;
        Span<byte> invalidMarker = stackalloc byte[64];
        invalidMarker[0] = 0x20;
        OwnedSerialSmokeMarkers.TestPassPrefix.CopyTo(invalidMarker.Slice(1));
        Span<byte> invalidDestination = stackalloc byte[64];
        OwnedSerialMarkerFormatResult invalid = OwnedSerialDiagnostics.FormatMarkerLine(invalidMarker.Slice(0, OwnedSerialSmokeMarkers.TestPassPrefix.Length + 1), invalidDestination);
        return HostCheck.Result(bootTextResult.Succeeded && panicTextResult.Succeeded && boot.Succeeded && boot.IsInitialized && boot.BytesWritten == boot.RequiredBytes && StartsWith(bootMarker.Slice(0, boot.BytesWritten), OwnedSerialDiagnostics.TestPassOwnedSerialPrefix) && HasCrLf(bootMarker.Slice(0, boot.BytesWritten)) && panic.Succeeded && panic.IsInitialized && StartsWith(panicMarker.Slice(0, panic.BytesWritten), OwnedSerialDiagnostics.PanicPreamblePrefix) && HasCrLf(panicMarker.Slice(0, panic.BytesWritten)) && !tooSmall.Succeeded && tooSmall.IsInitialized && tooSmall.Status == OwnedSerialMarkerFormatStatus.DestinationTooSmall && !invalid.Succeeded && invalid.IsInitialized && invalid.Status == OwnedSerialMarkerFormatStatus.InvalidMarker);
    }

    [HostTest("diagnostics.owned_serial results contain no managed references", HostLane.Fast, 7)]
    private static bool TestResultStructsContainNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformSerialTransportResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformSerialWriteResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedSerialMarkerFormatResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedSerialDiagnosticWriteResult>());
    }

    [HostTest("diagnostics.owned_serial default results are not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OwnedSerialMarkerFormatResult format = default;
        OwnedSerialDiagnosticWriteResult write = default;
        return HostCheck.Result(!format.IsInitialized && !format.Succeeded && format.Status == OwnedSerialMarkerFormatStatus.Success && format.MarkerBytes == 0 && format.BytesWritten == 0 && format.RequiredBytes == 0 && !write.IsInitialized && !write.Succeeded && write.Status == OwnedSerialDiagnosticWriteStatus.Success && write.FormatStatus == OwnedSerialMarkerFormatStatus.Success && write.SerialStatus == PlatformSerialWriteStatus.Unknown && write.MarkerBytes == 0 && write.BytesWritten == 0 && write.RequiredBytes == 0);
    }

    [HostTest("diagnostics.owned_serial formatter uses shared ascii text builder", HostLane.Fast, 9)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "src", "OS.Kernel", "Diagnostics", "OwnedSerialDiagnostics.cs"));
        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal) && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, marker)", StringComparison.Ordinal) && source.Contains("AsciiTextBuilder.AppendByte(destination, offset, CarriageReturn)", StringComparison.Ordinal) && source.Contains("AsciiTextBuilder.AppendByte(destination, offset, LineFeed)", StringComparison.Ordinal) && !source.Contains("marker.CopyTo(destination)", StringComparison.Ordinal) && !source.Contains("destination[marker.Length] = CarriageReturn", StringComparison.Ordinal) && !source.Contains("destination[marker.Length + 1] = LineFeed", StringComparison.Ordinal));
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix)
    {
        return bytes.Length >= prefix.Length && bytes.Slice(0, prefix.Length).SequenceEqual(prefix);
    }

    private static bool HasCrLf(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 2 && bytes[bytes.Length - 2] == OwnedSerialDiagnostics.CarriageReturn && bytes[bytes.Length - 1] == OwnedSerialDiagnostics.LineFeed;
    }

    private static OwnedSerialDiagnosticWriteResult WriteTestPassMarker(IPlatformSerial serial, ReadOnlySpan<byte> detail)
    {
        Span<byte> marker = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes];
        OwnedSerialSmokeMarkerFormatResult markerResult = OwnedSerialSmokeMarkers.FormatTestPassMarker(detail, marker);
        if (!markerResult.Succeeded)
        {
            throw new InvalidOperationException("Owned serial marker formatting failed: " + markerResult.Status);
        }

        return OwnedSerialDiagnostics.WriteMarkerLine(serial, marker.Slice(0, markerResult.BytesWritten));
    }
}
