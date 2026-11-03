using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;
using OS.Tooling;

internal static class NativeAotStagedStaticFilesTests
{
    private static byte[] Elf()
    {
        byte[] original = LinuxElfPackageTests.CapturePackage();
        var view = LinuxElfPackage.Validate(original);
        return original.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
    }

    [HostTest("static staged envelope retains seven bounded data records and the original ELF")]
    private static void Staged()
    {
        byte[] elf = Elf(), package = NativeAotStaticFilePackage.Create(elf, true);
        HostCheck.True(package.AsSpan().SequenceEqual(NativeAotStaticFilePackage.Create(elf, true)));
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        HostCheck.Equal(7u, view.DataFileCount);
        HostCheck.True(package.AsSpan((int)view.PayloadOffset, elf.Length).SequenceEqual(elf));
        int at = (int)view.PayloadOffset + elf.Length, records = 0, largest = 0;
        List<string> paths = [];
        while (at < package.Length)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(at)));
            int pathBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(at + 4)));
            int dataBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(at + 8)));
            string path = Encoding.ASCII.GetString(package, at + 48, pathBytes);
            paths.Add(path);
            HostCheck.True(path.StartsWith("wwwroot/", StringComparison.Ordinal) && dataBytes is > 0 and <= 65536);
            HostCheck.Equal(48 + pathBytes + dataBytes, length);
            HostCheck.True(SHA256.HashData(package.AsSpan(at + 48 + pathBytes, dataBytes)).AsSpan()
                .SequenceEqual(package.AsSpan(at + 16, 32)));
            largest = Math.Max(largest, dataBytes);
            records++;
            at += length;
        }
        HostCheck.Equal(7, records);
        HostCheck.Equal(65536, largest);
        HostCheck.True(paths.SequenceEqual(new[] { "wwwroot/index.html", "wwwroot/site.css", "wwwroot/app.js", "wwwroot/data.json",
            "wwwroot/one.bin", "wwwroot/boundary.bin", "wwwroot/nested/info.txt" }));
        HostCheck.Equal(package.Length, at);
    }

    [HostTest("static disk envelope contains zero staged assets with identical application bytes")]
    private static void Disk()
    {
        byte[] elf = Elf(), disk = NativeAotStaticFilePackage.Create(elf, false), staged = NativeAotStaticFilePackage.Create(elf, true);
        var diskView = LinuxElfPackage.Validate(disk);
        var stagedView = LinuxElfPackage.Validate(staged);
        HostCheck.True(diskView.Succeeded && stagedView.Succeeded);
        HostCheck.Equal(0u, diskView.DataFileCount);
        HostCheck.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(disk.AsSpan(32)));
        HostCheck.True(disk.AsSpan((int)diskView.PayloadOffset).SequenceEqual(elf));
        string manifest = Encoding.ASCII.GetString(disk, (int)diskView.ManifestOffset, (int)diskView.ManifestBytes);
        string stagedManifest = Encoding.ASCII.GetString(staged, (int)stagedView.ManifestOffset, (int)stagedView.ManifestBytes);
        HostCheck.Equal(stagedManifest + "env.DOTNET_ThreadPool_ForceMinWorkerThreads=2\nenv.DOTNET_ThreadPool_ForceMaxWorkerThreads=4\n", manifest);
        HostCheck.True(manifest.Contains("env.CLROS_STATIC_FILES_PROFILE=v1\n", StringComparison.Ordinal));
        HostCheck.True(manifest.Contains("descriptors=32\n", StringComparison.Ordinal));
    }

    [HostTest("static envelope corruption and unsupported ELF never obtain format admission")]
    private static void Rejections()
    {
        byte[] package = NativeAotStaticFilePackage.Create(Elf(), true);
        foreach (int offset in new[] { 0, 8, 36, package.Length - 1 })
        {
            byte[] changed = package.ToArray();
            changed[offset] ^= 0x80;
            HostCheck.True(!LinuxElfPackage.Validate(changed).Succeeded);
        }
        bool rejected = false;
        try
        {
            NativeAotStaticFilePackage.Create(new byte[64], true);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }

    [HostTest("static staged namespace is explicit and cannot alias private roots or traversal")]
    private static void Namespace()
    {
        byte[] original = NativeAotStaticFilePackage.Create(Elf(), true);
        var view = LinuxElfPackage.Validate(original);
        HostCheck.True(view.Succeeded);
        int start = checked((int)view.DataTableOffset + 48);
        foreach (string prefix in new[] { "private/", "WWWROOT/", "webroot/", "/wwroot/", "../root/", "wwwrooX/" })
        {
            byte[] changed = original.ToArray();
            Encoding.ASCII.GetBytes(prefix).CopyTo(changed, start);
            HostCheck.Equal(LinuxElfStatus.InvalidPath, LinuxElfPackage.Validate(changed).Status);
        }
        byte[] dot = original.ToArray();
        "wwwroot/../"u8.CopyTo(dot.AsSpan(start));
        HostCheck.Equal(LinuxElfStatus.InvalidPath, LinuxElfPackage.Validate(dot).Status);
    }
}
