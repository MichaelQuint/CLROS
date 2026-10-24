using OS.Tooling;
using OS.Manifest;
using OS.Kernel.Compatibility.LinuxX64;
using System.Runtime.InteropServices;
using System.Globalization;

internal static class LinuxDiskParentTests
{
    private const string End = "M16_025_END submitted=40 completed=40 copied=40 bootstrapCompleted=37 bootstrapCopied=37 fsReads=6 cacheHits=20 cacheMisses=2 device=retired fs=retired dma=zero descriptors=closed reservation=retained";
    private static string[] Envelope()
    {
        var rows = NativeAotSocketIntegrationTests.Envelope(true).ToList();
        int first = rows.IndexOf("TEST_START m16-009"), last = rows.IndexOf("TEST_PASS m16-009");
        rows.RemoveRange(first + 1, last - first - 1);
        void Set(string prefix, string value) => rows[rows.FindIndex(row => row.StartsWith(prefix, StringComparison.Ordinal))] = value;
        Set("M16_011_WIRE ", "M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=0");
        Set("M16_011_END ", "M16_011_END polls=1 received=0 copied=0 fresh=0 retired=0 start=0 end=1 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped");
        Set("M07_026_ROW stage=4 ", "M07_026_ROW stage=4 a=1 b=1 c=1 d=2");
        rows.InsertRange(rows.IndexOf("CLROS_M07_026_READY"), [End, "CLROS_M16_023_READY", "TEST_PASS m16.023",
            "CLROS_M16_024_READY", "TEST_PASS m16.024", "CLROS_M16_025_READY", "TEST_PASS m16.025"]);
        return rows.ToArray();
    }
    private static void Reject(string[] rows, string reason)
    {
        string? failure = null;
        try { QemuTranscripts.InspectLinuxDiskNetworkForTests(rows); } catch (ToolFailure error) { failure = error.Message; }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }

    [HostTest("disk parent envelope requires drained disk counts and original NIC teardown without TCP")]
    private static void Complete()
    {
        string[] rows = Envelope(), before = rows.ToArray();
        QemuTranscripts.InspectLinuxDiskNetworkForTests(rows); HostCheck.True(rows.SequenceEqual(before));
        // The old mechanical envelope must still reject the disk-specific tail.
        bool rejected = false;
        try { QemuTranscripts.InspectSignalEpollNetworkForTests(rows); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("disk parent envelope rejects missing duplicate moved or retained disk cleanup")]
    private static void Cleanup()
    {
        string[] rows = Envelope(); int at = Array.IndexOf(rows, End);
        for (int i = at; i < at + 7; i++)
        {
            Reject(rows.Where((_, index) => index != i).ToArray(), i == at ? "disk_parent_cleanup" : "socket_parent_order_");
            Reject([.. rows[..i], rows[i], .. rows[i..]], i == at ? "socket_parent_order_" : "socket_parent_order_");
        }
        Reject(rows.Select(row => row.Replace("device=retired fs=retired", "device=retained fs=retired", StringComparison.Ordinal)).ToArray(), "disk_parent_cleanup");
        Reject([.. rows, End], "socket_parent_extra_record");
    }

    [HostTest("disk parent envelope rejects mismatched bootstrap final copy and filesystem counters")]
    private static void Counters()
    {
        foreach (var change in new[] { ("submitted=40", "submitted=41"), ("completed=40", "completed=39"),
            ("copied=40", "copied=39"), ("bootstrapCompleted=37", "bootstrapCompleted=40"),
            ("bootstrapCopied=37", "bootstrapCopied=36"), ("fsReads=6", "fsReads=5"), ("cacheMisses=2", "cacheMisses=7") })
            Reject(Envelope().Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray(), "disk_parent_counters");
        Reject(Envelope().Select(row => row.Replace("tcpCopied=0", "tcpCopied=1", StringComparison.Ordinal)).ToArray(), "socket_network_accounting");
        Reject(Envelope().Select(row => row.Replace("stage=5 a=8 b=0", "stage=5 a=8 b=1", StringComparison.Ordinal)).ToArray(), "socket_nic_cleanup");
    }

    [HostTest("disk parent envelope rejects excessive nonascii empty and unknown input")]
    private static void Bounds()
    {
        Reject([], "disk_parent_input"); Reject(["bad\0row"], "disk_parent_input");
        Reject([new string('x', 1537)], "disk_parent_input"); Reject(new string[131073], "disk_parent_input");
        Reject([.. Envelope(), "unknown"], "socket_parent_extra_record");
    }

    [HostTest("disk startup accepts exactly65 ranges only for the canonical mechanical package")]
    private static unsafe void StartupRanges()
    {
        byte[] package = LinuxDiskFileProofFixture.CreatePackage(); var view = LinuxElfPackage.Validate(package);
        byte* seed = stackalloc byte[32]; new Span<byte>(seed, 32).Fill(1);
        byte* stack = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        try
        {
            LinuxElfStartupPlan startup;
            fixed (byte* bytes = package)
                HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(bytes + view.ManifestOffset, view.ManifestBytes,
                    bytes + view.PayloadOffset, view.PayloadBytes, seed, 32, stack, view.Manifest.StackBytes,
                    0x70000000UL - view.Manifest.StackBytes, 0, out startup));
            string stackRow = string.Create(CultureInfo.InvariantCulture,
                $"M16_004_STACK start={startup.StackStart} bytes={startup.StackBytes} sp={startup.StackPointer} argc={startup.ArgumentCount} envc={startup.EnvironmentCount} random={startup.RandomAddress} entropyBytes=32 seedCleared=32");
            string[] rows = [.. Enumerable.Repeat("parents checked separately", QemuContracts.M03OwnedSubsystemMarkers.Count + 7),
                "TEST_START m16-004", "M16_004_IMAGE base=4194304 bytes=28672 loads=2 entry=4194816 phdr=4194368 tlsFile=0 tlsMemory=0",
                stackRow, "M16_004_TABLES root=16777216 used=17 leaves=1737 ranges=65",
                "M16_004_READBACK old=33554432 restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false",
                "CLROS_M16_004_READY", "TEST_PASS m16-004"];
            HostCheck.Equal(rows.Length - 7, QemuTranscripts.ValidateLinuxStartupTail(rows, package, diskFiles: true).Length);
            void Denied(string[] input, byte[] candidate, bool disk)
            {
                bool rejected = false;
                try { _ = QemuTranscripts.ValidateLinuxStartupTail(input, candidate, diskFiles: disk); } catch (ToolFailure) { rejected = true; }
                HostCheck.True(rejected);
            }
            Denied(rows, package, false);
            foreach (int count in new[] { 64, 66, 135 })
                Denied(rows.Select(row => row.Replace("ranges=65", "ranges=" + count, StringComparison.Ordinal)).ToArray(), package, true);
            Denied(rows, LinuxThreadProofFixture.CreatePackage("Threads"), true);
        }
        finally { NativeMemory.AlignedFree(stack); }
    }
}
