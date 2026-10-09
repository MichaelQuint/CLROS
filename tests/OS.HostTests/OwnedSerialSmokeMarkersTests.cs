using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class OwnedSerialSmokeMarkersTests
{

    [HostTest("diagnostics.serialmarkers exposes smoke marker constants", HostLane.Fast, 0)]
    private static bool TestExposesSmokeMarkerConstants()
    {
        return HostCheck.Result(OwnedSerialSmokeMarkers.TestStartText == SerialSmokeMarkers.TestStart
            && OwnedSerialSmokeMarkers.TestPassText == SerialSmokeMarkers.TestPass
            && OwnedSerialSmokeMarkers.TestFailText == SerialSmokeMarkers.TestFail
            && OwnedSerialSmokeMarkers.BootOkText == SerialSmokeMarkers.BootOk
            && OwnedSerialSmokeMarkers.OsSmokePassText == SerialSmokeMarkers.OsSmokePass
            && OwnedSerialSmokeMarkers.OsSmokeFailText == SerialSmokeMarkers.OsSmokeFail
            && OwnedSerialSmokeMarkers.PanicPreambleText == SerialSmokeMarkers.OsSmokeFail
            && OwnedSerialSmokeMarkers.TestPassPrefixText ==
                SerialSmokeMarkers.TestPass + " " + OwnedSerialSmokeMarkers.TestName
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.TestStartMarker,
                OwnedSerialSmokeMarkers.TestStartText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.TestPassMarker,
                OwnedSerialSmokeMarkers.TestPassText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.TestFailMarker,
                OwnedSerialSmokeMarkers.TestFailText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.BootOkMarker,
                OwnedSerialSmokeMarkers.BootOkText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.OsSmokePassMarker,
                OwnedSerialSmokeMarkers.OsSmokePassText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.OsSmokeFailMarker,
                OwnedSerialSmokeMarkers.OsSmokeFailText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.TestPassPrefix,
                OwnedSerialSmokeMarkers.TestPassPrefixText)
            && SpanTextEquals(
                OwnedSerialSmokeMarkers.DiagnosticsPanicPrefix,
                OwnedSerialSmokeMarkers.DiagnosticsPanicPrefixText));
    }

    [HostTest("diagnostics.serialmarkers recognizes test marker prefixes", HostLane.Fast, 1)]
    private static bool TestRecognizesTestMarkerPrefixes()
    {
        return HostCheck.Result(OwnedSerialSmokeMarkers.IsTestMarkerPrefix(OwnedSerialSmokeMarkers.TestStartMarker)
            && OwnedSerialSmokeMarkers.IsTestMarkerPrefix(OwnedSerialSmokeMarkers.TestPassMarker)
            && OwnedSerialSmokeMarkers.IsTestMarkerPrefix(OwnedSerialSmokeMarkers.TestFailMarker)
            && !OwnedSerialSmokeMarkers.IsTestMarkerPrefix(OwnedSerialSmokeMarkers.OsSmokePassMarker)
            && OwnedSerialSmokeMarkers.HasTestMarkerPrefix("TEST_START diagnostics.serialmarkers"u8)
            && OwnedSerialSmokeMarkers.HasTestMarkerPrefix(
                "TEST_PASS diagnostics.serialmarkers exposes smoke marker constants"u8)
            && OwnedSerialSmokeMarkers.HasTestMarkerPrefix(
                "TEST_FAIL diagnostics.serialmarkers reason"u8)
            && !OwnedSerialSmokeMarkers.HasTestMarkerPrefix("TEST_PASSdiagnostics.serialmarkers"u8)
            && !OwnedSerialSmokeMarkers.HasTestMarkerPrefix("OS_SMOKE_PASS"u8)
            && OwnedSerialSmokeMarkers.IsKnownSmokeMarker("OS_BOOT_OK"u8)
            && OwnedSerialSmokeMarkers.IsKnownSmokeMarker("OS_SMOKE_PASS"u8)
            && OwnedSerialSmokeMarkers.IsKnownSmokeMarker(
                "OS_SMOKE_FAIL subsystem=diagnostics reason=owned-serial"u8));
    }

    [HostTest("diagnostics.serialmarkers rejects invalid markers", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidMarkers()
    {
        Span<byte> tooLong = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes + 1];
        tooLong.Fill((byte)'A');

        Span<byte> destination = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes];
        destination.Fill(0xA5);

        OwnedSerialSmokeMarkerFormatResult emptyToken =
            OwnedSerialSmokeMarkers.FormatTestPassMarker(ReadOnlySpan<byte>.Empty, destination);
        OwnedSerialSmokeMarkerFormatResult spacedToken =
            OwnedSerialSmokeMarkers.FormatTestPassMarker("bad token"u8, destination);
        OwnedSerialSmokeMarkerFormatResult panicToken =
            OwnedSerialSmokeMarkers.FormatDiagnosticsPanicMarker("bad reason"u8, destination);

        return HostCheck.Result(!OwnedSerialSmokeMarkers.IsValidMarker(ReadOnlySpan<byte>.Empty)
            && !OwnedSerialSmokeMarkers.IsValidMarker(" TEST_PASS diagnostics.serialmarkers"u8)
            && !OwnedSerialSmokeMarkers.IsValidMarker("TEST_PASS diagnostics.serialmarkers "u8)
            && !OwnedSerialSmokeMarkers.IsValidMarker("TEST_PASS diagnostics.serialmarkers\n"u8)
            && !OwnedSerialSmokeMarkers.IsValidMarker(tooLong)
            && !OwnedSerialSmokeMarkers.IsKnownSmokeMarker("UNKNOWN diagnostics.serialmarkers"u8)
            && emptyToken.IsInitialized
            && !emptyToken.Succeeded
            && emptyToken.Status == OwnedSerialSmokeMarkerFormatStatus.InvalidToken
            && spacedToken.IsInitialized
            && !spacedToken.Succeeded
            && spacedToken.Status == OwnedSerialSmokeMarkerFormatStatus.InvalidToken
            && panicToken.IsInitialized
            && !panicToken.Succeeded
            && panicToken.Status == OwnedSerialSmokeMarkerFormatStatus.InvalidToken
            && destination[0] == 0xA5
            && destination[^1] == 0xA5);
    }

    [HostTest("diagnostics.serialmarkers preserves no-allocation contract", HostLane.Fast, 3)]
    private static bool TestPreservesNoAllocationContract()
    {
        Span<byte> destination = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes];
        OwnedSerialSmokeMarkerFormatResult warmup =
            OwnedSerialSmokeMarkers.FormatTestPassMarker("warmup"u8, destination);
        if (!warmup.Succeeded)
        {
            return HostCheck.Result(false);
        }

        _ = OwnedSerialSmokeMarkers.HasTestMarkerPrefix(destination[..warmup.BytesWritten]);

        long before = GC.GetAllocatedBytesForCurrentThread();
        OwnedSerialSmokeMarkerFormatResult result = default;

        for (int index = 0; index < 64; index++)
        {
            destination.Clear();
            result = OwnedSerialSmokeMarkers.FormatTestPassMarker("contract"u8, destination);
            if (!result.Succeeded ||
                !OwnedSerialSmokeMarkers.HasTestMarkerPrefix(destination[..result.BytesWritten]))
            {
                return HostCheck.Result(false);
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        return HostCheck.Result(after == before
            && result.IsInitialized
            && result.Status == OwnedSerialSmokeMarkerFormatStatus.Success
            && result.PrefixBytes == OwnedSerialSmokeMarkers.TestPassPrefix.Length
            && result.TokenBytes == "contract"u8.Length
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedSerialSmokeMarkerFormatResult>());
    }

    private static bool SpanTextEquals(ReadOnlySpan<byte> bytes, string text)
    {
        return Encoding.ASCII.GetString(bytes) == text;
    }

    [HostTest("diagnostics.serialmarkers uses shared ascii text builder", HostLane.Fast, 4)]
    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "OwnedSerialSmokeMarkers.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendByte(destination, offset, value)", StringComparison.Ordinal)
            && !source.Contains("prefix.CopyTo(destination)", StringComparison.Ordinal)
            && !source.Contains("token.CopyTo(destination.Slice", StringComparison.Ordinal)
            && !source.Contains("destination[prefix.Length] = Space", StringComparison.Ordinal));
    }
}
