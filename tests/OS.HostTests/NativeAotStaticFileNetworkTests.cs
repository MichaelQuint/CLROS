using OS.Tooling;

internal static class NativeAotStaticFileNetworkTests
{
    private const string Cleanup = "M10_017_CLEANUP process=drained cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained";
    private static string[] Envelope()
    {
        List<string> rows = [];
        foreach (string original in NativeAotSocketIntegrationTests.Envelope(true))
        {
            if (original.StartsWith("M16_011_TIMEWAIT ", StringComparison.Ordinal) ||
                original.StartsWith("M16_011_SLOT ", StringComparison.Ordinal) && original.Contains("slot=3 ", StringComparison.Ordinal))
                continue;
            string row = original;
            if (row.StartsWith("M16_011_SLOT event=1 slot=0 ", StringComparison.Ordinal))
                row = row.Replace("clock=240000000005 deadline=240000000005 duration=240000000000", "clock=10 deadline=0 duration=0", StringComparison.Ordinal);
            if (row.StartsWith("M16_011_END ", StringComparison.Ordinal))
                row = row.Replace("fresh=4 retired=4", "fresh=3 retired=3", StringComparison.Ordinal)
                    .Replace("end=240000000005 maxDeadline=240000000005", "end=10 maxDeadline=0", StringComparison.Ordinal);
            if (row == "CLROS_M07_026_READY")
                rows.AddRange([Cleanup, "CLROS_M10_017_READY", "TEST_PASS m10.017"]);
            rows.Add(row);
        }
        return rows.ToArray();
    }
    private static void Reject(string[] rows, string reason)
    {
        try
        {
            _ = QemuTranscripts.InspectNativeAotStaticFileNetworkForTests(rows);
            throw new InvalidOperationException("accepted invalid " + reason);
        }
        catch (ToolFailure error) { HostCheck.True(error.Message.Contains(reason, StringComparison.Ordinal), error.Message); }
    }
    [HostTest("finite static network accepts three passive-close lifetimes with complete original NIC cleanup")]
    private static void Complete()
    {
        var result = QemuTranscripts.InspectNativeAotStaticFileNetworkForTests(Envelope());
        HostCheck.True(result.Fresh == 3 && result.Retired == 3 && result.TimeWaitLifetimes == 0 && result.MaximumDeadline == 0);
        Reject(NativeAotSocketIntegrationTests.Envelope(true), "socket_transport_complete");
    }
    [HostTest("finite static network rejects missing duplicate or out-of-order application cleanup")]
    private static void CleanupControls()
    {
        foreach (string missing in new[] { Cleanup, "CLROS_M10_017_READY", "TEST_PASS m10.017" })
            Reject(Envelope().Where(row => row != missing).ToArray(), "socket_parent_order_");
        Reject([.. Envelope(), Cleanup], "socket_parent_extra_record");
        Reject(Envelope().Select(row => row == Cleanup ? row.Replace("dma=zero", "dma=retained", StringComparison.Ordinal) : row).ToArray(), "socket_parent_order_");
    }
    [HostTest("finite static network rejects stale tuple generations early retirement and incomplete DMA return")]
    private static void OwnershipControls()
    {
        Reject(Envelope().Select(row => row.StartsWith("M16_011_SLOT event=1 slot=1 ", StringComparison.Ordinal) ?
            row.Replace("generation=1", "generation=2", StringComparison.Ordinal) : row).ToArray(), "socket_transport_generation");
        Reject(Envelope().Where(row => !row.StartsWith("M16_011_SLOT event=1 slot=0 ", StringComparison.Ordinal)).ToArray(), "socket_transport_complete");
        Reject(Envelope().Select(row => row.Replace("stage=5 a=8 b=0", "stage=5 a=8 b=1", StringComparison.Ordinal)).ToArray(), "socket_nic_cleanup");
        Reject(Envelope().Select(row => row.Replace("fresh=3 retired=3", "fresh=3 retired=2", StringComparison.Ordinal)).ToArray(), "socket_network_accounting");
    }
    [HostTest("finite static network bounds supplied rows and rejects controls before parsing")]
    private static void Bounds()
    {
        Reject([], "static_network_input");
        Reject([new string('x', 2049)], "static_network_input");
        Reject(["bad\0row"], "static_network_input");
        Reject(new string[100001], "static_network_input");
    }
}
