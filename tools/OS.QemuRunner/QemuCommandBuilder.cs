internal static class QemuCommandBuilder
{
    public static string BuildCommandLine(RunnerOptions options, string ovmfCodePath)
    {
        return options.QemuPath + " " + string.Join(" ", BuildArguments(options, ovmfCodePath).Select(Quote));
    }

    public static IReadOnlyList<string> BuildArguments(RunnerOptions options, string ovmfCodePath)
    {
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
            "if=none,id=esp,format=" + options.DiskFormat + ",file=" + options.ImagePath,
            "-device",
            "ide-hd,drive=esp,bus=ahci.0",
            "-netdev",
            "user,id=n0",
            "-device",
            "virtio-net-pci,netdev=n0"
        ]);

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
