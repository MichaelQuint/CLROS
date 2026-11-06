using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only creation of the native V2 immutable-config envelope.
/// ELF execution, guest startup and Kestrel acceptance remain separate. The
/// sole data file is appsettings.json; no TPA/CoreCLR payloads are introduced.</summary>
public static class NativeAotWebPackage
{
    public const string SettingsPath = "apps/appsettings.json";

    /// <summary>Runs only explicit configured-package selection on the calling
    /// root. Borrows arguments/output synchronously, writes stable start/fail/pass
    /// records, and returns nonzero on any admission or I/O failure. The original
    /// pinned Linux producer dispatcher stays unchanged. No child or guest starts.</summary>
    public static int RunConfigured(string root, string[] args, TextWriter output)
    {
        const string marker = "nativeaot.web.package";
        output.WriteLine("TEST_START " + marker);
        try
        {
            if (args is not ["package", "--recipe-root", var recipeRoot, "--recipe-evidence", var evidence,
                "--output", var destination, "--manifest", var manifest, "--settings", var settings])
                throw new ArgumentException("nativeaot web package --recipe-root <source> --recipe-evidence <evidence> --output <fresh-package> --manifest <manifest> --settings <settings>");
            output.WriteLine("NATIVEAOT_WEB_PACKAGE sha256=" + WriteConfigured(root, recipeRoot, evidence, destination, manifest, settings));
            output.WriteLine("TEST_PASS " + marker);
            return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL " + marker + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    /// <summary>Creates owned package bytes from a supplied static ELF and a
    /// bounded JSON object. This validates format/digests, not producer identity.
    /// Missing/unsupported/oversized inputs throw before returning any package.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf, ReadOnlySpan<byte> settings)
        => CreateCore(elf, settings, default, true);

    /// <summary>Creates owned V2 bytes using an explicit bounded Linux manifest
    /// and immutable JSON object. The existing package validator checks arguments,
    /// environment keys/counts, paths, resource fields and the exact ELF digest.
    /// Inputs are borrowed for this synchronous call; invalid input throws before
    /// returning bytes. This establishes format, not producer or guest authority.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf, ReadOnlySpan<byte> settings, ReadOnlySpan<byte> manifest)
        => CreateCore(elf, settings, manifest, false);

    private static byte[] CreateCore(ReadOnlySpan<byte> elf, ReadOnlySpan<byte> settings,
        ReadOnlySpan<byte> manifest, bool useDefaultManifest)
    {
        if (elf.Length > LinuxElfPackage.MaximumBytes || settings.Length is <= 0 or > (int)LinuxElfPackage.MaximumDataBytes ||
            (!useDefaultManifest && manifest.Length is <= 0 or > (int)LinuxElfPackage.MaximumManifestBytes))
            throw new InvalidDataException("web_package_input_length");
        NativeAotPayload.Inspection image = NativeAotPayload.Inspect(elf);
        using JsonDocument json = JsonDocument.Parse(settings.ToArray());
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("web_package_config_object");
        if (useDefaultManifest)
            manifest = Encoding.ASCII.GetBytes("format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
                "app.path=apps/MinimalApiSmoke\nargs=--urls http://0.0.0.0:8080\ndigest=" + image.Sha256.ToLowerInvariant() +
                "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\n" +
                "env.CLROS_USER_IRQS=1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n");
        byte[] path = Encoding.ASCII.GetBytes(SettingsPath);
        int recordBytes = checked((int)LinuxElfPackage.DataRecordHeaderBytes + path.Length + settings.Length);
        int length = checked((int)LinuxElfPackage.HeaderBytes + manifest.Length + elf.Length + recordBytes);
        if (length > LinuxElfPackage.MaximumBytes)
            throw new InvalidDataException("web_package_length");
        byte[] package = new byte[length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
        W32(package, 8, 2);
        W32(package, 12, 2);
        W32(package, 16, LinuxElfPackage.HeaderBytes);
        W32(package, 20, (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        W32(package, 32, (uint)recordBytes);
        W32(package, 36, 1);
        manifest.CopyTo(package.AsSpan((int)LinuxElfPackage.HeaderBytes));
        int at = (int)LinuxElfPackage.HeaderBytes + manifest.Length;
        elf.CopyTo(package.AsSpan(at));
        at += elf.Length;
        W32(package, at, (uint)recordBytes);
        W32(package, at + 4, (uint)path.Length);
        W32(package, at + 8, (uint)settings.Length);
        SHA256.HashData(settings).CopyTo(package, at + 16);
        path.CopyTo(package, at + (int)LinuxElfPackage.DataRecordHeaderBytes);
        settings.CopyTo(package.AsSpan(at + (int)LinuxElfPackage.DataRecordHeaderBytes + path.Length));
        LinuxElfPackageView view = LinuxElfPackage.Validate(package);
        if (!view.Succeeded || view.DataFileCount != 1)
            throw new InvalidDataException("web_package_admission " + view.Status);
        return package;
    }

    /// <summary>Packages exact retained Linux web outputs after source/native/
    /// output revalidation under the original root's lease. Owns only a fresh
    /// file beneath the calling root's artifacts directory. No guest is started.</summary>
    public static string Write(string root, string recipeRoot, string evidence, string destination)
        => WriteCore(root, recipeRoot, evidence, destination, null, null);

    /// <summary>Retains distinct plain single-link manifest/settings snapshots
    /// below the calling root while revalidating the original Linux web producer.
    /// Writes only a fresh artifacts output after bounded package admission.
    /// Inputs stay unchanged through completion; errors throw without a success
    /// result. A failed final I/O check retains its output for diagnosis. Selected
    /// settings are explicit caller configuration, not original builder output.</summary>
    public static string WriteConfigured(string root, string recipeRoot, string evidence, string destination,
        string manifestPath, string settingsPath)
    {
        using AuthorityFiles files = new();
        var manifest = files.Open(root, manifestPath, "web manifest", "web_manifest", LinuxElfPackage.MaximumManifestBytes, singleLink: true);
        var settings = files.Open(root, settingsPath, "web settings", "web_settings", LinuxElfPackage.MaximumDataBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(manifest, settings);
        byte[] manifestBytes = manifest.ReadBytes(LinuxElfPackage.MaximumManifestBytes, "web_manifest"),
            settingsBytes = settings.ReadBytes(LinuxElfPackage.MaximumDataBytes, "web_settings");
        files.VerifyUnchanged();
        string hash = WriteCore(root, recipeRoot, evidence, destination, manifestBytes, settingsBytes);
        files.VerifyUnchanged();
        return hash;
    }

    private static string WriteCore(string root, string recipeRoot, string evidence, string destination,
        byte[]? manifest, byte[]? selectedSettings)
    {
        root = Path.GetFullPath(root);
        recipeRoot = Path.GetFullPath(recipeRoot);
        evidence = Path.GetFullPath(evidence);
        destination = Path.GetFullPath(destination);
        using var lease = NativeAotPayload.OpenQualificationLease(recipeRoot);
        NativeAotWebPayload.ValidateQualification(recipeRoot, evidence);
        if (!destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Path.Exists(destination))
            throw new InvalidDataException("web_package_fresh_artifacts_output");
        string elf = Path.Combine(evidence, "MinimalApiSmoke", "MinimalApiSmoke");
        string settings = Path.Combine(evidence, "MinimalApiSmoke", "appsettings.json");
        if (new FileInfo(elf).Length > LinuxElfPackage.MaximumBytes ||
            new FileInfo(settings).Length > LinuxElfPackage.MaximumDataBytes)
            throw new InvalidDataException("web_package_input_length");
        byte[] package = manifest is null ? Create(File.ReadAllBytes(elf), File.ReadAllBytes(settings)) :
            Create(File.ReadAllBytes(elf), selectedSettings!, manifest);
        EfiBuildFiles.InitializeDirectory(Path.GetDirectoryName(destination)!, "web_package_output_path");
        EfiBuildFiles.RequirePlainOrMissing(root, destination, "web_package_output_path");
        using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            file.Write(package);
        string hash = Convert.ToHexString(SHA256.HashData(package));
        if (NativeAotWebPayload.HashFile(destination) != hash)
            throw new InvalidDataException("web_package_write_changed");
        return hash;
    }

    private static void W32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
}
