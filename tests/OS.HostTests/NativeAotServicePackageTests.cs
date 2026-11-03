using System.Security.Cryptography;
using System.Text;
using OS.Manifest;
using OS.Tooling;

internal static class NativeAotServicePackageTests
{
    // This is an explicitly mechanical ELF. A matching envelope grants no
    // original NativeAOT code/source or CLROS guest authority.
    private static byte[] Image()
    {
        byte[] package = LinuxElfPackageTests.CapturePackage();
        var view = LinuxElfPackage.Validate(package);
        return package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
    }

    [HostTest("service package seals distinct workload exact arguments config and bounded resources")]
    private static void RoundTrip()
    {
        byte[] elf = Image(), package = NativeAotServicePackage.Create(elf);
        byte[] held = package.ToArray();
        var description = NativeAotServicePackage.Describe(package);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(elf)), description.ElfSha256);
        HostCheck.Equal(5U, description.Arguments);
        HostCheck.Equal(2U, description.Environment);
        HostCheck.Equal((uint)Encoding.ASCII.GetByteCount("{\"CLROS_SERVICE_CONTRACT\":\"v1\"}"), description.SettingsBytes);
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        HostCheck.Equal(67_108_864U, view.Manifest.MemoryBytes);
        HostCheck.Equal(1_572_864U, view.Manifest.StackBytes);
        HostCheck.Equal(16U, view.Manifest.Threads);
        HostCheck.Equal(16U, view.Manifest.Descriptors);
        HostCheck.Equal(0U, view.Manifest.Ipv4);
        HostCheck.Equal(8080U, view.Manifest.Port);
        string manifest = Encoding.ASCII.GetString(package.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes));
        HostCheck.True(manifest.Contains("app.path=apps/MinimalApiService\nargs=--clros-service=v1 --urls=http://0.0.0.0:8080 --contentRoot /app/apps\n", StringComparison.Ordinal));
        HostCheck.True(held.AsSpan().SequenceEqual(package));
    }

    [HostTest("service lifecycle package seals explicit v2 arguments and settings while rejecting crossed modes")]
    private static unsafe void LifecyclePolicy()
    {
        byte[] elf = Image(), original = NativeAotServicePackage.Create(elf), lifecycle = NativeAotServicePackage.Create(elf, lifecycle: true);
        HostCheck.Equal(NativeAotServicePackage.Describe(original).ElfSha256, NativeAotServicePackage.Describe(lifecycle, lifecycle: true).ElfSha256);
        Reject(lifecycle);
        bool rejected = false;
        try
        {
            _ = NativeAotServicePackage.Describe(original, lifecycle: true);
        }
        catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected && !original.AsSpan().SequenceEqual(lifecycle));
        var view = LinuxElfPackage.Validate(lifecycle);
        byte[] manifest = lifecycle.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes).ToArray();
        string text = Encoding.ASCII.GetString(manifest);
        HostCheck.True(text.Contains("args=--clros-service=v2 --urls=http://0.0.0.0:8080 --contentRoot /app/apps\n", StringComparison.Ordinal));
        HostCheck.Equal(3U, NativeAotServicePackage.Describe(lifecycle, lifecycle: true).Environment);
        HostCheck.True(text.Contains("env.DOTNET_GCHeapHardLimit=1000000\n", StringComparison.Ordinal));
        fixed (byte* p = manifest)
        {
            var budget = LinuxAppConfiguration.ReadGcBudget(p, (uint)manifest.Length, view.Manifest, 8192, 16 * 1024 * 1024, 16 * 1024 * 1024, true);
            HostCheck.True(budget.Succeeded);
            HostCheck.Equal(NativeAotServicePackage.LifecycleGcLimitBytes, budget.GcLimitBytes);
        }
        foreach (byte[] changed in new[]
        {
            NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.SettingsJson), manifest),
            NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.LifecycleSettingsJson), Encoding.ASCII.GetBytes(text.Replace("--clros-service=v2", "--clros-service=v1", StringComparison.Ordinal))),
            NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.LifecycleSettingsJson), Encoding.ASCII.GetBytes(text.Replace("1000000\n", "800000\n", StringComparison.Ordinal))),
            NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.LifecycleSettingsJson), Encoding.ASCII.GetBytes(text.Replace("env.DOTNET_GCHeapHardLimit=1000000\n", "", StringComparison.Ordinal)))
        })
        {
            rejected = false;
            try
            {
                _ = NativeAotServicePackage.Describe(changed, lifecycle: true);
            }
            catch (InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("service package rejects valid generic envelopes with broadened or mismatched service policy")]
    private static void Policy()
    {
        byte[] elf = Image(), original = NativeAotServicePackage.Create(elf);
        var view = LinuxElfPackage.Validate(original);
        string manifest = Encoding.ASCII.GetString(original.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes));
        foreach (var change in new[] {
            ("apps/MinimalApiService", "apps/MinimalApiSmoke"), ("--clros-service=v1", "--clros-service=v2"),
            ("--contentRoot /app/apps", "--contentRoot /"), ("--urls=http://0.0.0.0:8080", "--urls=http://0.0.0.0:8081"),
            ("memory=67108864", "memory=134217728"), ("stack=1572864", "stack=2097152"),
            ("threads=16", "threads=17"), ("descriptors=16", "descriptors=17"),
            ("listen=0.0.0.0:8080", "listen=0.0.0.0:8081"), ("env.CLROS_USER_IRQS=1", "env.CLROS_USER_IRQS=0") })
            Reject(NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.SettingsJson), Encoding.ASCII.GetBytes(manifest.Replace(change.Item1, change.Item2, StringComparison.Ordinal))));
        Reject(NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.SettingsJson), Encoding.ASCII.GetBytes(manifest + "env.CLROS_SERVICE_CONTRACT=v1\n")));
        Reject(NativeAotWebPackage.Create(elf, "{\"OS_SMOKE_EXPECTED_BODY\":\"OS_MINIMAL_API_OK\"}"u8));
    }

    [HostTest("service package rejects changed settings missing data corrupt payload and malformed input")]
    private static void Integrity()
    {
        byte[] elf = Image(), original = NativeAotServicePackage.Create(elf);
        var view = LinuxElfPackage.Validate(original);
        byte[] manifest = original.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes).ToArray();
        foreach (string settings in new[] { "{}", "{\"CLROS_SERVICE_CONTRACT\":\"v2\"}", "{\"CLROS_SERVICE_CONTRACT\":\"v1\",\"other\":true}" })
            Reject(NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(settings), manifest));
        byte[] changed = original.ToArray();
        changed[(int)view.PayloadOffset + 100] ^= 1;
        Reject(changed);
        changed = original.ToArray();
        changed[^1] ^= 1;
        Reject(changed);
        Reject(original.AsSpan(0, original.Length - 1).ToArray());
        Reject([]);
    }

    [HostTest("service quota package seals explicit termination and rejects crossed positive contracts")]
    private static unsafe void QuotaPolicy()
    {
        byte[] elf = Image(), quota = NativeAotServicePackage.Create(elf, NativeAotServiceContract.QuotaFailure);
        var description = NativeAotServicePackage.Describe(quota, NativeAotServiceContract.QuotaFailure);
        HostCheck.Equal(4U, description.Environment);
        HostCheck.Equal(5U, description.Arguments);
        var view = LinuxElfPackage.Validate(quota);
        byte[] manifest = quota.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes).ToArray();
        string text = Encoding.ASCII.GetString(manifest);
        HostCheck.True(text.Contains("args=--clros-service=v3-quota --urls=http://0.0.0.0:8080 --contentRoot /app/apps\n", StringComparison.Ordinal));
        HostCheck.True(text.EndsWith("env.DOTNET_GCHeapHardLimit=1000000\nenv.CLROS_MEMORY_EXHAUSTION=terminate\n", StringComparison.Ordinal));
        fixed (byte* p = manifest)
        {
            var budget = LinuxAppConfiguration.ReadGcBudget(p, (uint)manifest.Length, view.Manifest, 8192, 16 * 1024 * 1024, 16 * 1024 * 1024, true);
            HostCheck.True(budget.Succeeded && budget.TerminateOnPhysicalBackingExhaustion);
            HostCheck.Equal(NativeAotServicePackage.LifecycleGcLimitBytes, budget.GcLimitBytes);
        }
        foreach (var contract in new[] { NativeAotServiceContract.V1, NativeAotServiceContract.V2 })
        {
            RejectContract(quota, contract);
            RejectContract(NativeAotServicePackage.Create(elf, contract), NativeAotServiceContract.QuotaFailure);
        }
    }

    [HostTest("service quota package rejects missing mismatched and broadened immutable termination policy")]
    private static void QuotaPolicyMutations()
    {
        byte[] elf = Image(), quota = NativeAotServicePackage.Create(elf, NativeAotServiceContract.QuotaFailure);
        var view = LinuxElfPackage.Validate(quota);
        string manifest = Encoding.ASCII.GetString(quota.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes));
        foreach (var change in new[] {
            ("env.CLROS_MEMORY_EXHAUSTION=terminate\n", ""),
            ("env.CLROS_MEMORY_EXHAUSTION=terminate\n", "env.CLROS_MEMORY_EXHAUSTION=errno\n"),
            ("env.DOTNET_GCHeapHardLimit=1000000\n", ""),
            ("--clros-service=v3-quota", "--clros-service=v2"),
            ("env.DOTNET_GCHeapHardLimit=1000000\n", "env.DOTNET_GCHeapHardLimit=800000\n") })
            RejectContract(NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.QuotaSettingsJson),
                Encoding.ASCII.GetBytes(manifest.Replace(change.Item1, change.Item2, StringComparison.Ordinal))), NativeAotServiceContract.QuotaFailure);
        RejectContract(NativeAotWebPackage.Create(elf, Encoding.ASCII.GetBytes(NativeAotServicePackage.LifecycleSettingsJson),
            Encoding.ASCII.GetBytes(manifest)), NativeAotServiceContract.QuotaFailure);
    }

    [HostTest("service package rejects unknown contract selectors before producing an envelope")]
    private static void UnknownContract()
    {
        byte[] elf = Image(), package = NativeAotServicePackage.Create(elf);
        foreach (var contract in new[] { (NativeAotServiceContract)0, (NativeAotServiceContract)4, (NativeAotServiceContract)(-1) })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotServicePackage.Create(elf, contract);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            HostCheck.True(rejected);
            rejected = false;
            try
            {
                _ = NativeAotServicePackage.Describe(package, contract);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    private static void RejectContract(byte[] package, NativeAotServiceContract contract)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotServicePackage.Describe(package, contract);
        }
        catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected, "noncanonical service contract accepted");
    }

    private static void Reject(byte[] package)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotServicePackage.Describe(package);
        }
        catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected, "noncanonical service envelope accepted");
    }
}
