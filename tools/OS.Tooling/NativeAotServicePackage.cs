using System.Text;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only construction/description of the explicit continuing-service
/// envelope. Reuses the checked immutable-data writer; grants no original ELF,
/// instruction, source, execution or guest qualification authority. Callers retain
/// stable borrowed input through return; malformed or different policy throws.</summary>
public static class NativeAotServicePackage
{
    public const string Application = "MinimalApiService";
    public const string ApplicationPath = "apps/MinimalApiService";
    public const string Arguments = "--clros-service=v1 --urls=http://0.0.0.0:8080 --contentRoot /app/apps";
    public const string SettingsJson = "{\"CLROS_SERVICE_CONTRACT\":\"v1\"}";
    public const string LifecycleArguments = "--clros-service=v2 --urls=http://0.0.0.0:8080 --contentRoot /app/apps";
    public const string LifecycleSettingsJson = "{\"CLROS_SERVICE_CONTRACT\":\"v2\"}";
    public const string QuotaArguments = "--clros-service=v3-quota --urls=http://0.0.0.0:8080 --contentRoot /app/apps";
    public const string QuotaSettingsJson = "{\"CLROS_SERVICE_CONTRACT\":\"v3-quota\"}";
    public const uint MemoryBytes = 67_108_864, StackBytes = 1_572_864, Threads = 16, Descriptors = 16;
    public const ulong LifecycleGcLimitBytes = 16_777_216;

    /// <summary>Numeric/identity observation only. A matching envelope does not
    /// establish that its payload is the reviewed original service executable.</summary>
    public readonly record struct Description(string ElfSha256, uint Arguments, uint Environment, uint SettingsBytes);

    /// <summary>Construct a fresh caller-owned byte array from one inspected static
    /// Linux x64 ELF. Seal the diagnostic argument,8080 binding/content root,
    /// bounded resources, IRQ admission and one canonical immutable settings file.
    /// Does not publish, run, write files or admit unreviewed code to a guest.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf, bool lifecycle = false)
        => Create(elf, lifecycle ? NativeAotServiceContract.V2 : NativeAotServiceContract.V1);

    /// <summary>Construct only one named immutable contract. Unknown values reject;
    /// quota mode seals both configured GC and physical-exhaustion termination.
    /// This host-only envelope does not prove that a guest enforces the policy.</summary>
    public static byte[] Create(ReadOnlySpan<byte> elf, NativeAotServiceContract contract)
    {
        var image = NativeAotPayload.Inspect(elf);
        byte[] package = NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(Settings(contract)), Encoding.ASCII.GetBytes(Manifest(image.Sha256, contract)));
        _ = Describe(package, contract); return package;
    }

    /// <summary>Validate the complete exact service envelope and immutable data
    /// without accepting ordinary web packages or caller-expanded policy. The
    /// future prepared service program must separately bind original sources,
    /// ELF digest and complete native instruction/syscall index.</summary>
    public static Description Describe(ReadOnlySpan<byte> package, bool lifecycle = false)
        => Describe(package, lifecycle ? NativeAotServiceContract.V2 : NativeAotServiceContract.V1);

    /// <summary>Require exactly the selected complete envelope, including both
    /// quota policy and configured GC. Crossed, absent, broadened or unknown
    /// policies reject. Borrowed bytes remain caller owned through return.</summary>
    public static unsafe Description Describe(ReadOnlySpan<byte> package, NativeAotServiceContract contract)
    {
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded && view.DataFileCount == 1, "service_package_envelope");
        var elf = package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes);
        var image = NativeAotPayload.Inspect(elf);
        Need(package.Slice((int)view.ManifestOffset, (int)view.ManifestBytes).SequenceEqual(Encoding.ASCII.GetBytes(Manifest(image.Sha256, contract))), "service_package_policy");
        fixed (byte* source = package)
        {
            Need(LinuxElfPackage.GetDataFile(source, (uint)package.Length, 0, out var data) == LinuxElfStatus.Success, "service_package_settings");
            Need(package.Slice((int)data.PathOffset, (int)data.PathBytes).SequenceEqual("apps/appsettings.json"u8) &&
                package.Slice((int)data.DataOffset, (int)data.DataBytes).SequenceEqual(Encoding.ASCII.GetBytes(Settings(contract))), "service_package_settings_identity");
            return new(image.Sha256, 5, view.Manifest.EnvironmentCount, data.DataBytes);
        }
    }

    private static string Settings(NativeAotServiceContract contract) => contract switch
    {
        NativeAotServiceContract.V1 => SettingsJson,
        NativeAotServiceContract.V2 => LifecycleSettingsJson,
        NativeAotServiceContract.QuotaFailure => QuotaSettingsJson,
        _ => throw new ArgumentOutOfRangeException(nameof(contract))
    };

    private static string ArgumentsFor(NativeAotServiceContract contract) => contract switch
    {
        NativeAotServiceContract.V1 => Arguments,
        NativeAotServiceContract.V2 => LifecycleArguments,
        NativeAotServiceContract.QuotaFailure => QuotaArguments,
        _ => throw new ArgumentOutOfRangeException(nameof(contract))
    };

    private static string Manifest(string digest, NativeAotServiceContract contract) => "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
        "app.path=" + ApplicationPath + "\nargs=" + ArgumentsFor(contract) + "\ndigest=" + digest.ToLowerInvariant() +
        "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\n" +
        "env.CLROS_USER_IRQS=1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n" +
        (contract != NativeAotServiceContract.V1 ? "env.DOTNET_GCHeapHardLimit=1000000\n" : "") +
        (contract == NativeAotServiceContract.QuotaFailure ? "env.CLROS_MEMORY_EXHAUSTION=terminate\n" : "");

    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
}
