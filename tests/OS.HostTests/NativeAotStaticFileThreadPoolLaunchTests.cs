using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotStaticFileThreadPoolLaunchTests
{
    private const string Minimum = "DOTNET_ThreadPool_ForceMinWorkerThreads";
    private const string Maximum = "DOTNET_ThreadPool_ForceMaxWorkerThreads";

    [HostTest("static thread-pool launch recognizes exact bounded worker keys without writes")]
    private static void KnownKeys()
    {
        foreach (string name in new[] { Minimum, Maximum })
        {
            byte[] bytes = Enumerable.Repeat((byte)0xa5, name.Length + 2).ToArray();
            Encoding.ASCII.GetBytes(name).CopyTo(bytes, 1);
            byte[] before = bytes.ToArray();
            fixed (byte* key = bytes)
            {
                HostCheck.Equal(39, name.Length);
                HostCheck.True(LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(key + 1, 39));
                foreach (uint length in new[] { 0u, 1u, 18u, 38u, 40u, uint.MaxValue })
                    HostCheck.True(!LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(key + 1, length));
            }
            HostCheck.True(bytes.AsSpan().SequenceEqual(before));
        }
        HostCheck.True(!LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(null, 39));
        HostCheck.True(!LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(null, 0));
        HostCheck.True(!LinuxAppConfiguration.IsThreadPoolWorkerLimitKey((byte*)(ulong.MaxValue - 20), 39));
    }

    [HostTest("static thread-pool launch rejects changed key bytes and unsupported mixed-case names")]
    private static void ExactNames()
    {
        foreach (string name in new[] { Minimum, Maximum })
        {
            byte[] bytes = Encoding.ASCII.GetBytes(name);
            fixed (byte* key = bytes)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte saved = bytes[i];
                    bytes[i] = (byte)'!';
                    HostCheck.True(!LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(key, (uint)bytes.Length));
                    bytes[i] = saved;
                }
                HostCheck.True(LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(key, (uint)bytes.Length));
            }
        }
        foreach (string name in UnsupportedNames())
            HostCheck.True(!Recognized(name));
        // This remains a valid generic uppercase environment name. It is not
        // the runtime's exact mixed-case key and is not the canonical policy.
        HostCheck.True(!Recognized("DOTNET_THREADPOOL_FORCEMINWORKERTHREADS"));
    }

    [HostTest("static thread-pool launch manifest admits only the named mixed-case exceptions")]
    private static void ManifestAdmission()
    {
        byte[] elf = Elf(), staged = NativeAotStaticFilePackage.Create(elf, true);
        string original = ManifestText(staged);
        var valid = Parse(original + $"env.{Minimum}=2\nenv.{Maximum}=4\n");
        HostCheck.Equal(LinuxElfStatus.Success, valid.Status);
        HostCheck.Equal(5u, valid.EnvironmentCount);
        foreach (string name in UnsupportedNames())
            HostCheck.Equal(LinuxElfStatus.InvalidEnvironment, Parse(original + $"env.{name}=2\n").Status);
        foreach (string name in new[] { Minimum, Maximum })
            HostCheck.Equal(LinuxElfStatus.DuplicateField,
                Parse(original + $"env.{name}=2\nenv.{name}=4\n").Status);
        HostCheck.Equal(LinuxElfStatus.Success,
            Parse(original + "env.DOTNET_THREADPOOL_FORCEMINWORKERTHREADS=2\n").Status);
        HostCheck.Equal(LinuxElfStatus.Success, Parse(original + "env.TEST_123=ordinary\n").Status);
        // Key admission deliberately supplies no numeric/effective-policy
        // guarantee. Only the static package factory fixes the values to 2/4.
        foreach (string value in new[] { "not-hex", "0", "-1", "40000" })
            HostCheck.Equal(LinuxElfStatus.Success, Parse(original + $"env.{Minimum}={value}\n").Status);
        HostCheck.Equal(LinuxElfStatus.InvalidEnvironment,
            Parse(original + $"env.{Minimum}={new string('2', 257)}\n").Status);
    }

    [HostTest("static thread-pool launch policy is disk only and preserves staged manifest and ELF bytes")]
    private static void PackagePolicy()
    {
        byte[] elf = Elf();
        byte[] staged = NativeAotStaticFilePackage.Create(elf, true), disk = NativeAotStaticFilePackage.Create(elf, false);
        string expectedStaged = "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
            "app.path=apps/MinimalApiStaticFiles\nargs=--urls http://0.0.0.0:8080 --contentRoot /app\ndigest=" +
            Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() +
            "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=32\nlisten=0.0.0.0:8080\n" +
            "env.CLROS_USER_IRQS=1\nenv.CLROS_STATIC_FILES_PROFILE=v1\nenv.DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false\n";
        HostCheck.True(ManifestBytes(staged).AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(expectedStaged)));
        string[] previous = ["CLROS_USER_IRQS=1", "CLROS_STATIC_FILES_PROFILE=v1", "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false"];
        HostCheck.True(EnvironmentValues(staged).SequenceEqual(previous));
        HostCheck.True(EnvironmentValues(disk).SequenceEqual([.. previous, Minimum + "=2", Maximum + "=4"]));
        foreach (byte[] package in new[] { staged, disk })
        {
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded);
            HostCheck.Equal(16u, view.Manifest.Threads);
            HostCheck.Equal(32u, view.Manifest.Descriptors);
            HostCheck.True(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).SequenceEqual(elf));
            HostCheck.True(package.AsSpan().SequenceEqual(NativeAotStaticFilePackage.Create(elf, ReferenceEquals(package, staged))));
        }
        HostCheck.Equal(7u, LinuxElfPackage.Validate(staged).DataFileCount);
        HostCheck.Equal(0u, LinuxElfPackage.Validate(disk).DataFileCount);
    }

    [HostTest("static thread-pool launch reaches the actual initial envp with exact values and readback")]
    private static void InitialEnvironment()
    {
        byte[] elf = Elf(), entropy = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        foreach (bool staged in new[] { false, true })
        {
            byte[] package = NativeAotStaticFilePackage.Create(elf, staged), manifest = ManifestBytes(package);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded);
            uint bytes = view.Manifest.StackBytes;
            ulong start = 0x70000000UL - bytes;
            byte* allocation = (byte*)NativeMemory.AlignedAlloc(bytes + 8192u, 4096);
            HostCheck.True(allocation != null);
            try
            {
                new Span<byte>(allocation, checked((int)bytes + 8192)).Fill(0xa5);
                byte* stack = allocation + 4096;
                fixed (byte* m = manifest, e = elf, random = entropy)
                {
                    HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(m, (uint)manifest.Length, e,
                        (uint)elf.Length, random, (uint)entropy.Length, stack, bytes, start, 0x078bfbff, out var plan));
                    HostCheck.True(LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length,
                        random, (uint)entropy.Length, stack, bytes, start, 0x078bfbff, plan));
                    string[] expected = EnvironmentValues(package), actual = new string[plan.EnvironmentCount];
                    ulong* frame = (ulong*)(stack + plan.StackPointer - start);
                    HostCheck.Equal((uint)expected.Length, plan.EnvironmentCount);
                    HostCheck.Equal(0UL, frame[plan.ArgumentCount + 1]);
                    for (uint i = 0; i < plan.EnvironmentCount; i++)
                    {
                        ulong address = frame[plan.ArgumentCount + 2 + i];
                        HostCheck.True(address >= start && address < start + bytes);
                        var text = new ReadOnlySpan<byte>(stack + address - start, checked((int)(start + bytes - address)));
                        int nul = text.IndexOf((byte)0);
                        HostCheck.True(nul >= 0);
                        actual[i] = Encoding.ASCII.GetString(text[..nul]);
                    }
                    HostCheck.Equal(0UL, frame[plan.ArgumentCount + 2 + plan.EnvironmentCount]);
                    HostCheck.True(actual.SequenceEqual(expected));
                    if (!staged)
                    {
                        int index = Array.IndexOf(actual, Minimum + "=2");
                        HostCheck.True(index >= 0);
                        byte* value = stack + frame[plan.ArgumentCount + 2 + (uint)index] - start + Minimum.Length + 1;
                        byte saved = *value;
                        *value = (byte)'3';
                        HostCheck.True(!LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length,
                            random, (uint)entropy.Length, stack, bytes, start, 0x078bfbff, plan));
                        *value = saved;
                        HostCheck.True(LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length,
                            random, (uint)entropy.Length, stack, bytes, start, 0x078bfbff, plan));
                    }
                }
                for (uint i = 0; i < 4096; i++)
                    HostCheck.True(allocation[i] == 0xa5 && allocation[4096 + bytes + i] == 0xa5);
            }
            finally
            {
                NativeMemory.AlignedFree(allocation);
            }
        }
    }

    private static string[] UnsupportedNames() =>
    [
        "DOTNET_Threadpool_ForceMinWorkerThreads", "DOTNET_ThreadPool_ForceminWorkerThreads",
        "DOTNET_ThreadPool_ForceMaxWorkerThread", "DOTNET_ThreadPool_ForceMaxWorkerThreadS",
        "DOTNET_ThreadPool_ForceMinIOCompletionThreads", "DOTNET_ThreadPool_MinThreads",
        "COMPlus_ThreadPool_ForceMinWorkerThreads", "TEST_lower", "DOTNET-ThreadPool_ForceMinWorkerThreads"
    ];

    private static bool Recognized(string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name);
        fixed (byte* p = bytes)
            return LinuxAppConfiguration.IsThreadPoolWorkerLimitKey(p, (uint)bytes.Length);
    }
    private static LinuxElfManifest Parse(string text) => NativeManifestParser.ParseLinuxElf(Encoding.ASCII.GetBytes(text));
    private static byte[] Elf()
    {
        byte[] package = LinuxElfPackageTests.CapturePackage();
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        return package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
    }
    private static byte[] ManifestBytes(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        return package.AsSpan((int)view.ManifestOffset, (int)view.ManifestBytes).ToArray();
    }
    private static string ManifestText(byte[] package) => Encoding.ASCII.GetString(ManifestBytes(package));
    private static string[] EnvironmentValues(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        var manifest = view.Manifest;
        string[] result = new string[manifest.EnvironmentCount];
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
        {
            uint row = i * 4;
            string key = Encoding.ASCII.GetString(package, checked((int)(view.ManifestOffset + manifest.EnvironmentRanges[row])),
                checked((int)manifest.EnvironmentRanges[row + 1]));
            string value = Encoding.ASCII.GetString(package, checked((int)(view.ManifestOffset + manifest.EnvironmentRanges[row + 2])),
                checked((int)manifest.EnvironmentRanges[row + 3]));
            result[i] = key + "=" + value;
        }
        return result;
    }
}
