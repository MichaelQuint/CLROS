internal static class QemuCommandBuilder
{
    public static string BuildCommandLine(RunnerOptions options, string ovmfCodePath)
    {
        return options.QemuPath + " " + string.Join(" ", BuildArguments(options, ovmfCodePath).Select(Quote));
    }

    public static IReadOnlyList<string> BuildArguments(RunnerOptions options, string ovmfCodePath, string? ethernetPeerNetdev = null,
        IReadOnlyList<string>? webFrameArguments = null)
    {
        bool separateBoot = options.VirtioBlockBootImagePath is not null;
        List<string> arguments =
        [
            "-machine",
            "q35,accel=tcg",
            "-m",
            options.Memory,
            "-no-reboot",
            "-display",
            "none",
            "-serial",
            "file:" + options.SerialLogPath
        ];

        // The finite web fixture may resolve EFI_UNSPECIFIED_TIMEZONE only
        // under this declared UTC RTC contract. Retain these arguments in the
        // runner receipt; arbitrary firmware/local-time configurations differ.
        if (options.ContinuingWeb || options.FiniteStaticFiles) arguments.AddRange(["-rtc", "base=utc,clock=host"]);

        if (!string.IsNullOrWhiteSpace(ovmfCodePath))
        {
            arguments.AddRange(
            [
                "-drive",
                "if=pflash,format=raw,readonly=on,file=" + ovmfCodePath
            ]);
        }

        arguments.AddRange(
        [
            "-device",
            "ich9-ahci,id=ahci",
            "-drive",
            // The web verifier holds the exact image read-only. AHCI needs a
            // writable disk, so keep its writes in QEMU's temporary overlay.
            "if=none,id=esp,format=" + options.DiskFormat + (separateBoot || options.ContinuingWeb || options.ContinuingService || options.FiniteStaticFiles || options.ReadOnlyWebRootDisk ? ",snapshot=on" : options.VirtioBlockQueueDevice ? ",readonly=on" : "") + ",file=" + (options.VirtioBlockBootImagePath ?? options.ImagePath),
            "-device",
            options.VirtioBlockQueueDevice && !separateBoot ? "virtio-blk-pci,drive=esp,addr=06.0,disable-legacy=on,romfile=" : "ide-hd,drive=esp,bus=ahci.0",
            .. separateBoot ? new[] { "-drive", "if=none,id=gptdata,format=raw,readonly=on,file=" + options.ImagePath,
                "-device", "virtio-blk-pci,drive=gptdata,addr=06.0,disable-legacy=on,romfile=" } : Array.Empty<string>(),
            "-netdev",
            options.ContinuingNetworkPeer || options.SocketContainmentPeer ? ethernetPeerNetdev ?? "socket,id=n0,connect=127.0.0.1:<ephemeral>" :
                options.SocketForwardPort is int port ? "user,id=n0,hostfwd=tcp:127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-:8080" : "user,id=n0",
            "-device",
            options.VirtioNegotiationDevice ? "virtio-net-pci,netdev=n0,addr=06.0,disable-legacy=on,romfile=" +
                (options.ArpReceiveFixture ? ",mac=02:00:00:00:00:01" : "") : "virtio-net-pci,netdev=n0"
        ]);

        // The same immutable image supplies boot bytes through a temporary
        // AHCI overlay and demand reads through a distinct read-only device.
        // Slot6 remains the NIC; the disk owns only function0 at slot7.
        if (options.ReadOnlyWebRootDisk)
            arguments.AddRange(["-drive", "if=none,id=webroot,format=raw,readonly=on,file=" + options.ImagePath,
                "-device", "virtio-blk-pci,drive=webroot,addr=07.0,disable-legacy=on,romfile="]);

        if (options.PciBarTestDevice)
            arguments.AddRange(["-device", "pci-testdev,addr=06.0,membar=2M"]);

        // Backend TX is SLIRP -> guest. Put the redirector before the dump so
        // reinjected TX (including the diagnostic VLAN) reaches the guest-side
        // capture. Guest RX traverses the list in reverse and bypasses TX.
        if (webFrameArguments is not null && !options.ContinuingWeb && !options.ContinuingService) throw new InvalidOperationException("Frame filter requires continuing web or service.");
        if (options.ContinuingWeb || options.ContinuingService)
            arguments.AddRange(webFrameArguments ?? new[] { "-chardev", "socket,id=clroswebout,host=127.0.0.1,port=<ephemeral-out>",
                "-chardev", "socket,id=clroswebin,host=127.0.0.1,port=<ephemeral-in>",
                "-object", "filter-redirector,id=clroswebfilter,netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout" });

        if (options.EthernetCapturePath is not null)
            arguments.AddRange(["-object", "filter-dump,id=clroscapture,netdev=n0,file=" + options.EthernetCapturePath + ",maxlen=65536"]);

        // .NET 11 NativeAOT requires x86-64-v2. Nehalem supplies its SSE4/POPCNT/
        // CX16 baseline without AVX/XSAVE state beyond CLROS's x87/SSE contract.
        // Keep an explicit model: QEMU's default qemu64 lacks the v2 baseline.
        arguments.AddRange(["-cpu", options.FirmwareRngSource is null ? "Nehalem" : "Nehalem,-rdrand,-rdseed"]);
        if (options.FirmwareRngSource is not null)
        {
            if (string.Equals(options.FirmwareRngSource, "virtio", StringComparison.Ordinal))
                arguments.AddRange(["-object", "rng-builtin,id=clrosrng", "-device", "virtio-rng-pci,rng=clrosrng"]);
        }

        if (options.WatchStartup)
            arguments.AddRange(["-qmp", "stdio"]);

        return arguments;
    }

    private static string Quote(string value)
    {
        if (!value.Any(char.IsWhiteSpace))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
