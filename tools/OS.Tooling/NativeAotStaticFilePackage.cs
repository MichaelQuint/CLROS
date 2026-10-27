using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Finite host-only static-app envelopes. Staged data stays within the
/// existing seven-record/64KiB limits; the disk envelope contains no web assets.
/// Envelope validity alone grants no executable or CLROS guest authority.</summary>
public static class NativeAotStaticFilePackage
{
    public sealed record Asset(string Path, byte[] Bytes);

    /// <summary>Fresh, caller-owned small data controls. The complete Blazor
    /// publication belongs on disk and is never truncated to fit this fixture.</summary>
    public static Asset[] StagedAssets() =>
    [
        new("wwwroot/index.html", Encoding.UTF8.GetBytes("<!doctype html><html><head><meta charset=\"utf-8\"><link rel=\"stylesheet\" href=\"site.css\"></head><body><h1>CLROS staged files</h1><p id=\"status\">Loading</p><script src=\"app.js\"></script></body></html>\n")),
        new("wwwroot/site.css", "body{font:18px system-ui;margin:2rem}h1{color:#245}\n"u8.ToArray()),
        new("wwwroot/app.js", "fetch('/api/status').then(r=>r.json()).then(x=>document.querySelector('#status').textContent=x.message);\n"u8.ToArray()),
        new("wwwroot/data.json", Encoding.UTF8.GetBytes("{\"profile\":\"staged-v1\",\"text\":\"café\",\"optional\":null}\n")),
        new("wwwroot/one.bin", [0xa5]),
        new("wwwroot/boundary.bin", Enumerable.Range(0, 65536).Select(i => (byte)(i ^ (i >> 8) ^ 0x5a)).ToArray()),
        new("wwwroot/nested/info.txt", "CLROS staged nested asset\n"u8.ToArray())
    ];

    /// <summary>Validate borrowed ELF bytes and return an owned canonical
    /// V1 disk or V2 staged envelope. No input is changed or process started.
    /// Unsupported ELF, capacity or package admission fails before return.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf, bool staged)
    {
        if (elf.Length > LinuxElfPackage.MaximumBytes) throw new InvalidDataException("static_package_elf_length");
        string digest = NativeAotPayload.Inspect(elf).Sha256.ToLowerInvariant();
        byte[] manifest = Encoding.ASCII.GetBytes("format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
            "app.path=apps/" + NativeAotStaticFilePublisher.Application +
            "\nargs=--urls http://0.0.0.0:8080 --contentRoot /app\ndigest=" + digest +
            "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=32\nlisten=0.0.0.0:8080\n" +
            "env.CLROS_USER_IRQS=1\nenv.CLROS_STATIC_FILES_PROFILE=v1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n");
        Asset[] assets = staged ? StagedAssets() : [];
        int dataBytes = assets.Sum(asset => checked((int)LinuxElfPackage.DataRecordHeaderBytes + Encoding.ASCII.GetByteCount(asset.Path) + asset.Bytes.Length));
        int total = checked((int)LinuxElfPackage.HeaderBytes + manifest.Length + elf.Length + dataBytes);
        if (total > LinuxElfPackage.MaximumBytes) throw new InvalidDataException("static_package_capacity");
        byte[] result = new byte[total];
        BinaryPrimitives.WriteUInt64LittleEndian(result, LinuxElfPackage.Magic);
        W32(result, 8, staged ? 2u : 1u); W32(result, 12, staged ? 2u : 1u);
        W32(result, 16, LinuxElfPackage.HeaderBytes); W32(result, 20, (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(24), (ulong)elf.Length);
        W32(result, 32, (uint)dataBytes); W32(result, 36, (uint)assets.Length);
        manifest.CopyTo(result, (int)LinuxElfPackage.HeaderBytes);
        int at = (int)LinuxElfPackage.HeaderBytes + manifest.Length;
        elf.CopyTo(result.AsSpan(at)); at += elf.Length;
        foreach (var asset in assets)
        {
            byte[] path = Encoding.ASCII.GetBytes(asset.Path);
            int length = (int)LinuxElfPackage.DataRecordHeaderBytes + path.Length + asset.Bytes.Length;
            W32(result, at, (uint)length); W32(result, at + 4, (uint)path.Length); W32(result, at + 8, (uint)asset.Bytes.Length);
            SHA256.HashData(asset.Bytes).CopyTo(result, at + 16);
            path.CopyTo(result, at + (int)LinuxElfPackage.DataRecordHeaderBytes);
            asset.Bytes.CopyTo(result, at + (int)LinuxElfPackage.DataRecordHeaderBytes + path.Length);
            at += length;
        }
        var admitted = LinuxElfPackage.Validate(result);
        if (!admitted.Succeeded || admitted.DataFileCount != assets.Length)
            throw new InvalidDataException("static_package_admission " + admitted.Status);
        return result;
    }

    /// <summary>Revalidate the original Linux candidate under its source lease,
    /// then write/read back a fresh artifacts file. Original native referents must
    /// remain accessible. A failed final check retains output; it never authorizes
    /// instruction lookup, staging, disk reads or guest execution.</summary>
    public static string Write(string root, string recipeRoot, string evidence, string destination, bool staged)
    {
        root = Path.GetFullPath(root); recipeRoot = Path.GetFullPath(recipeRoot);
        evidence = Path.GetFullPath(evidence); destination = Path.GetFullPath(destination);
        if (!destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Path.Exists(destination))
            throw new InvalidDataException("static_package_fresh_artifacts_required");
        using var lease = NativeAotPayload.OpenQualificationLease(recipeRoot);
        NativeAotStaticFileContract.Validate(recipeRoot, evidence);
        string elf = Path.Combine(evidence, NativeAotStaticFilePublisher.Application, NativeAotStaticFilePublisher.Application);
        using AuthorityFiles inputs = new();
        var input = inputs.Open(evidence, elf, "static package ELF", "static_package_elf", LinuxElfPackage.MaximumBytes, singleLink: true);
        byte[] package = Create(input.ReadBytes(LinuxElfPackage.MaximumBytes, "static_package_elf"), staged);
        inputs.VerifyUnchanged();
        EfiBuildFiles.InitializeDirectory(Path.GetDirectoryName(destination)!, "static_package_output");
        EfiBuildFiles.RequirePlainOrMissing(root, destination, "static_package_output");
        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { output.Write(package); output.Flush(true); }
        inputs.VerifyUnchanged();
        string hash = Convert.ToHexString(SHA256.HashData(package));
        if (NativeAotWebPayload.HashFile(destination) != hash) throw new InvalidDataException("static_package_write_changed");
        NativeAotStaticFileContract.Validate(recipeRoot, evidence);
        return hash;
    }

    private static void W32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
