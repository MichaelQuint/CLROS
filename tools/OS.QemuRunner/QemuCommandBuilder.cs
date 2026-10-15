internal static class QemuCommandBuilder
{
    public static string BuildCommandLine(RunnerOptions options, string ovmfCodePath)
    {
        return options.QemuPath + " " + string.Join(" ", BuildArguments(options, ovmfCodePath).Select(Quote));
    }

    public static IReadOnlyList<string> BuildArguments(RunnerOptions options, string ovmfCodePath)
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
            "if=none,id=esp,format=" + options.DiskFormat + (separateBoot ? ",snapshot=on" : options.VirtioBlockQueueDevice ? ",readonly=on" : "") + ",file=" + (options.VirtioBlockBootImagePath ?? options.ImagePath),
            "-device",
            options.VirtioBlockQueueDevice && !separateBoot ? "virtio-blk-pci,drive=esp,addr=06.0,disable-legacy=on,romfile=" : "ide-hd,drive=esp,bus=ahci.0",
            .. separateBoot ? new[] { "-drive", "if=none,id=gptdata,format=raw,readonly=on,file=" + options.ImagePath,
                "-device", "virtio-blk-pci,drive=gptdata,addr=06.0,disable-legacy=on,romfile=" } : Array.Empty<string>(),
            "-netdev",
            "user,id=n0",
            "-device",
            options.VirtioNegotiationDevice ? "virtio-net-pci,netdev=n0,addr=06.0,disable-legacy=on,romfile=" +
                (options.ArpReceiveFixture ? ",mac=02:00:00:00:00:01" : "") : "virtio-net-pci,netdev=n0"
        ]);

        if (options.PciBarTestDevice)
            arguments.AddRange(["-device", "pci-testdev,addr=06.0,membar=2M"]);

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
