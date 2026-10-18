using System.Text;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only construction/description of the explicit continuing-service
/// V2 envelope. Reuses the checked immutable-data writer; grants no original ELF,
/// instruction, source, execution or guest qualification authority. Callers retain
/// stable borrowed input through return; malformed or different policy throws.</summary>
public static class NativeAotServicePackage
{
    public const string Application = "MinimalApiService";
    public const string ApplicationPath = "apps/MinimalApiService";
    public const string Arguments = "--clros-service=v1 --urls=http://0.0.0.0:8080 --contentRoot /app/apps";
    public const string SettingsJson = "{\"CLROS_SERVICE_CONTRACT\":\"v1\"}";
    public const uint MemoryBytes = 67_108_864, StackBytes = 1_572_864, Threads = 16, Descriptors = 16;

    /// <summary>Numeric/identity observation only. A matching envelope does not
    /// establish that its payload is the reviewed original service executable.</summary>
    public readonly record struct Description(string ElfSha256, uint Arguments, uint Environment, uint SettingsBytes);

    /// <summary>Construct a fresh caller-owned byte array from one inspected static
    /// Linux x64 ELF. Seal the diagnostic argument,8080 binding/content root,
    /// bounded resources, IRQ admission and one canonical immutable settings file.
    /// Does not publish, run, write files or admit unreviewed code to a guest.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf)
    {
        var image = NativeAotPayload.Inspect(elf);
        byte[] package = NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(SettingsJson), Encoding.ASCII.GetBytes(Manifest(image.Sha256)));
        _ = Describe(package); return package;
    }

    /// <summary>Validate the complete exact service envelope and immutable data
    /// without accepting ordinary web packages or caller-expanded policy. The
    /// future prepared service program must separately bind original sources,
    /// ELF digest and complete native instruction/syscall index.</summary>
    public static unsafe Description Describe(ReadOnlySpan<byte> package)
    {
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded && view.DataFileCount == 1, "service_package_envelope");
        var elf = package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes);
        var image = NativeAotPayload.Inspect(elf);
        Need(package.Slice((int)view.ManifestOffset, (int)view.ManifestBytes).SequenceEqual(Encoding.ASCII.GetBytes(Manifest(image.Sha256))), "service_package_policy");
        fixed (byte* source = package)
        {
            Need(LinuxElfPackage.GetDataFile(source, (uint)package.Length, 0, out var data) == LinuxElfStatus.Success, "service_package_settings");
            Need(package.Slice((int)data.PathOffset, (int)data.PathBytes).SequenceEqual("apps/appsettings.json"u8) &&
                package.Slice((int)data.DataOffset, (int)data.DataBytes).SequenceEqual(Encoding.ASCII.GetBytes(SettingsJson)), "service_package_settings_identity");
            return new(image.Sha256, 5, 2, data.DataBytes);
        }
    }

    private static string Manifest(string digest) => "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
        "app.path=" + ApplicationPath + "\nargs=" + Arguments + "\ndigest=" + digest.ToLowerInvariant() +
        "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\n" +
        "env.CLROS_USER_IRQS=1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n";

    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
}
