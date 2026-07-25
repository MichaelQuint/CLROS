using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class OwnedSerialSmokeMarkersTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "diagnostics.serialmarkers exposes smoke marker constants",
            TestExposesSmokeMarkerConstants,
            ref failures);
        Run(
            "diagnostics.serialmarkers recognizes test marker prefixes",
            TestRecognizesTestMarkerPrefixes,
            ref failures);
        Run(
            "diagnostics.serialmarkers rejects invalid markers",
            TestRejectsInvalidMarkers,
            ref failures);
        Run(
            "diagnostics.serialmarkers preserves no-allocation contract",
            TestPreservesNoAllocationContract,
            ref failures);
        Run(
            "diagnostics.serialmarkers uses shared ascii text builder",
            TestUsesSharedAsciiTextBuilder,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Owned serial smoke marker host tests failed: " + failures);
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

    private static bool TestExposesSmokeMarkerConstants()
    {
        return OwnedSerialSmokeMarkers.TestStartText == SerialSmokeMarkers.TestStart
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
                OwnedSerialSmokeMarkers.DiagnosticsPanicPrefixText);
    }

    private static bool TestRecognizesTestMarkerPrefixes()
    {
        return OwnedSerialSmokeMarkers.IsTestMarkerPrefix(OwnedSerialSmokeMarkers.TestStartMarker)
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
                "OS_SMOKE_FAIL subsystem=diagnostics reason=owned-serial"u8);
    }

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

        return !OwnedSerialSmokeMarkers.IsValidMarker(ReadOnlySpan<byte>.Empty)
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
            && destination[^1] == 0xA5;
    }

    private static bool TestPreservesNoAllocationContract()
    {
        Span<byte> destination = stackalloc byte[OwnedSerialSmokeMarkers.MaximumMarkerBytes];
        OwnedSerialSmokeMarkerFormatResult warmup =
            OwnedSerialSmokeMarkers.FormatTestPassMarker("warmup"u8, destination);
        if (!warmup.Succeeded)
        {
            return false;
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
                return false;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        return after == before
            && result.IsInitialized
            && result.Status == OwnedSerialSmokeMarkerFormatStatus.Success
            && result.PrefixBytes == OwnedSerialSmokeMarkers.TestPassPrefix.Length
            && result.TokenBytes == "contract"u8.Length
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedSerialSmokeMarkerFormatResult>();
    }

    private static bool SpanTextEquals(ReadOnlySpan<byte> bytes, string text)
    {
        return Encoding.ASCII.GetString(bytes) == text;
    }

    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "OwnedSerialSmokeMarkers.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendByte(destination, offset, value)", StringComparison.Ordinal)
            && !source.Contains("prefix.CopyTo(destination)", StringComparison.Ordinal)
            && !source.Contains("token.CopyTo(destination.Slice", StringComparison.Ordinal)
            && !source.Contains("destination[prefix.Length] = Space", StringComparison.Ordinal);
    }
}
