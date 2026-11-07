using System.Text.Json;
using OS.Tooling;

internal static class EfiSourceInputTests
{
    [HostTest("EFI source-only inventory distinguishes default inputs from the explicit TCP flight compile selection")]
    private static async Task Selections()
    {
        foreach (bool network in new[] { false, true })
        {
            using StringWriter output = new();
            await EfiSourceInputs.RunAsync(Repository.FindRoot(), network ? ["EnableM07026Probe"] : [], output);
            using var value = JsonDocument.Parse(output.ToString());
            var json = value.RootElement;
            HostCheck.True(json.GetProperty("SourceOnly").GetBoolean() && !json.GetProperty("NativePublication").GetBoolean() && !json.GetProperty("GuestExecuted").GetBoolean());
            var flight = json.GetProperty("Sources").EnumerateArray().Where(row => row.GetProperty("Path").GetString() == "src/OS.Kernel/Net/TcpSendWindow.Flight.cs").ToArray();
            HostCheck.True(network ? flight.Length == 1 && flight[0].GetProperty("ActiveMembers").GetInt32() > 0 : flight.Length == 0);
        }
    }

    [HostTest("EFI source-only inventory rejects unknown and duplicate probe selectors before evaluation")]
    private static async Task Refusals()
    {
        foreach (string[] selectors in new[] { new[] { "EnableUnknownProbe" }, new[] { "EnableM07026Probe", "EnableM07026Probe" } })
        {
            bool refused = false;
            try
            {
                await EfiSourceInputs.RunAsync(Repository.FindRoot(), selectors, TextWriter.Null);
            }
            catch (ToolFailure error)
            {
                refused = error.Reason == "efi_source_selector";
            }
            HostCheck.True(refused);
        }
    }
}
