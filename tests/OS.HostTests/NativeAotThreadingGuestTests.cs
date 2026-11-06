using OS.Tooling;

internal static class NativeAotThreadingGuestTests
{
    private const string Valid = "TEST_START nativeaot.suspension\n" +
        "NATIVEAOT_SUSPENSION_GC epoch=1 before=0 after=1 progressBefore=17 progressReturned=23 progressAfter=29\n" +
        "NATIVEAOT_SUSPENSION_GC epoch=2 before=1 after=3 progressBefore=31 progressReturned=37 progressAfter=43\n" +
        "NATIVEAOT_SUSPENSION_JOINED workers=1 collections=2 roots=true interior=true tls=true\n" +
        "TEST_PASS nativeaot.suspension\n";

    [HostTest("nativeaot.suspension output requires complete ordered collection progress and joined root witnesses")]
    private static void Observations()
    {
        NativeAotThreadingGuest.ValidateSuspensionOutput(Valid);
        NativeAotThreadingGuest.ValidateSuspensionOutput(Valid.Replace("\n", "\r\n", StringComparison.Ordinal));
        NativeAotThreadingGuest.ValidateSuspensionOutput(Valid.TrimEnd('\n'));
        foreach (string invalid in new[]
        {
            Valid.Replace("epoch=2", "epoch=1", StringComparison.Ordinal),
            Valid.Replace("before=0 after=1", "before=1 after=1", StringComparison.Ordinal),
            Valid.Replace("before=1 after=3", "before=0 after=3", StringComparison.Ordinal),
            Valid.Replace("progressReturned=23", "progressReturned=29", StringComparison.Ordinal),
            Valid.Replace("progressReturned=23", "progressReturned=16", StringComparison.Ordinal),
            Valid.Replace("progressAfter=29", "progressAfter=17", StringComparison.Ordinal),
            Valid.Replace("progressBefore=31", "progressBefore=28", StringComparison.Ordinal),
            Valid.Replace("progressBefore=17", "progressBefore=0", StringComparison.Ordinal),
            Valid.Replace("epoch=1", "epoch=01", StringComparison.Ordinal),
            Valid.Replace("after=3", "after=2147483648", StringComparison.Ordinal),
            Valid.Replace("progressAfter=43", "progressAfter=9223372036854775808", StringComparison.Ordinal),
            Valid.Replace("roots=true", "roots=false", StringComparison.Ordinal),
            Valid.Replace("interior=true", "interior=false", StringComparison.Ordinal),
            Valid.Replace("tls=true", "tls=false", StringComparison.Ordinal),
            Valid.Replace("workers=1", "workers=0", StringComparison.Ordinal),
            Valid.Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal),
            Valid.Replace("NATIVEAOT_SUSPENSION_JOINED", "NATIVEAOT_SUSPENSION_UNJOINED", StringComparison.Ordinal),
            string.Join('\n', Valid.Split('\n').Where(row => !row.Contains("epoch=2", StringComparison.Ordinal))) + "\n",
            Valid + "TEST_FAIL nativeaot.suspension late_failure\n", Valid + "\n", "", new string('x', 4097)
        })
            Reject(invalid);
    }

    [HostTest("nativeaot.suspension capture CLI rejects incomplete and malformed UTF8 without success")]
    private static void CaptureFile()
    {
        string directory = Directory.CreateTempSubdirectory("clros-suspension-output-").FullName;
        string path = Path.Combine(directory, "stdout.log");
        try
        {
            File.WriteAllText(path, Valid);
            NativeAotThreadingGuest.ValidateSuspensionFile(path);
            var result = HostToolProcess.RunTool("nativeaot", "threading", "validate-suspension", path);
            HostCheck.True(result.ExitCode == 0 && result.Output.Contains("TEST_PASS nativeaot.suspension-output", StringComparison.Ordinal));
            File.AppendAllText(path, "TEST_FAIL nativeaot.suspension failed_join\n");
            result = HostToolProcess.RunTool("nativeaot", "threading", "validate-suspension", path);
            HostCheck.True(result.ExitCode != 0 && !result.Output.Contains("TEST_PASS nativeaot.suspension-output", StringComparison.Ordinal));
            File.WriteAllBytes(path, [0xc0, 0xaf]);
            bool rejected = false;
            try
            {
                NativeAotThreadingGuest.ValidateSuspensionFile(path);
            }
            catch (System.Text.DecoderFallbackException)
            {
                rejected = true;
            }
            HostCheck.True(rejected);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static void Reject(string text)
    {
        NativeAotThreadingGuest.ValidateSuspensionOutput(Valid);
        bool rejected = false;
        try
        {
            NativeAotThreadingGuest.ValidateSuspensionOutput(text);
        }
        catch (ToolFailure error)
        {
            rejected = error.Message.StartsWith("nativeaot_suspension_", StringComparison.Ordinal);
        }
        HostCheck.True(rejected, text);
    }
}
