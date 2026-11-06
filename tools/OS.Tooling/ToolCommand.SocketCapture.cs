namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectSocketCapture(string inspectMode, string inspectPackage, string inspectDump, string inspectSerial)
    {
        if (inspectMode is not ("sync" or "async"))
            throw new ArgumentException("socket_inspect_mode");
        Console.WriteLine("TEST_START qemu.socket.capture report_only=true");
        string root = Repository.FindRoot();
        inspectSerial = Path.GetFullPath(inspectSerial);
        inspectPackage = Path.GetFullPath(inspectPackage);
        inspectDump = Path.GetFullPath(inspectDump);
        using AuthorityFiles files = new();
        AuthorityFile Hold(string path, long maximum) => files.Open(Path.GetDirectoryName(path)!, path, "socket capture inspection", "socket_inspection", maximum, singleLink: true);
        var serial = Hold(inspectSerial, 8 * 1024 * 1024);
        var pcap = Hold(inspectSerial + ".pcap", 4 * 1024 * 1024);
        var peer = Hold(inspectSerial + ".peer.log", 65536);
        var package = Hold(inspectPackage, 16 * 1024 * 1024);
        var dump = Hold(inspectDump, NativeAotSocketProgram.MaximumDisassemblyBytes);
        AuthorityFiles.RequireDistinct(serial, pcap, peer, package, dump);
        var program = NativeAotSocketProgram.Read(root, inspectMode == "async", inspectPackage, inspectDump);
        NativeAotSocketPeer.InspectOutput(peer.ReadUtf8(65536, "socket_inspection_peer"));
        QemuTranscripts.AssertM03AcpiBootstrapTranscript(inspectSerial, firstRealExit: true, ownedSurvival: true, pciEnumeration: true,
            virtioNegotiation: true, virtqueuePolling: true, apicSetup: true, virtioNet: true, arpReceive: true, linuxElfPackagePath: inspectPackage,
            workloadServiceLoop: true, linuxStartup: true, linuxSyscalls: true, linuxMemory: true, linuxDescriptors: true, linuxThreads: true, linuxSignals: true,
            networkService: true, socketProgram: program);
        files.VerifyUnchanged();
        Console.WriteLine("TEST_PASS qemu.socket.capture report_only=true own_process_and_native_provenance_required=true");
        return 0;
    }
}
