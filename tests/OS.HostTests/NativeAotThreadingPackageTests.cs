using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotThreadingPackageTests
{
    private static readonly string[] Shared = ["global.json", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props",
        "tools/OS.Tooling/NativeAotPayload.cs", "tools/OS.Tooling/NativeAotThreadingPayload.cs", "tools/OS.Tooling/NativeAotThreadingGuest.cs"];

    [HostTest("nativeaot.threading-package exact current app and producer sources reject raw changes without freezing unrelated kernel files")]
    private static void Sources()
    {
        string root = HostToolProcess.FindRepositoryRoot(), temporary = Directory.CreateTempSubdirectory("clros-threading-sources-").FullName;
        try
        {
            foreach (string app in NativeAotThreadingPayload.Applications)
            {
                string[] paths = [.. Shared, $"apps/{app}/Program.cs", $"apps/{app}/{app}.csproj"];
                foreach (string relative in paths)
                {
                    string path = Path.Combine(temporary, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(Path.Combine(root, relative), path, overwrite: true);
                }
                NativeAotThreadingPackage.RequireCurrentSources(root, app); NativeAotThreadingPackage.RequireCurrentSources(temporary, app);
                File.WriteAllText(Path.Combine(temporary, "unrelated-kernel.cs"), "unrelated change");
                string extra = Path.Combine(temporary, "apps", app, "Extra.cs"); File.WriteAllText(extra, "new compile input");
                Reject(() => NativeAotThreadingPackage.RequireCurrentSources(temporary, app), "source_inventory"); File.Delete(extra);
                string cache = Path.Combine(temporary, "apps", app, "obj"); Directory.CreateDirectory(cache); File.WriteAllText(Path.Combine(cache, "generated.cs"), "ignored build cache");
                NativeAotThreadingPackage.RequireCurrentSources(temporary, app);
                string config = Path.Combine(temporary, "NuGet.Config"); File.WriteAllText(config, "new restore policy");
                Reject(() => NativeAotThreadingPackage.RequireCurrentSources(temporary, app), "source_override"); File.Delete(config);
                foreach (string relative in paths)
                {
                    string path = Path.Combine(temporary, relative); byte[] original = File.ReadAllBytes(path);
                    File.AppendAllText(path, "\n"); Reject(() => NativeAotThreadingPackage.RequireCurrentSources(temporary, app), "source_identity");
                    File.WriteAllBytes(path, original); NativeAotThreadingPackage.RequireCurrentSources(temporary, app);
                }
            }
            Reject(() => NativeAotThreadingPackage.RequireCurrentSources(root, "../NativeAotHello"), "threading_package_app");
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [HostTest("nativeaot.threading-package manifests preserve exact qualified digests bounded slots and explicit user IRQs")]
    private static void Policy()
    {
        foreach (string app in NativeAotThreadingPayload.Applications)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(NativeAotThreadingPackage.Manifest(app)); var parsed = NativeManifestParser.ParseLinuxElf(bytes);
            HostCheck.True(parsed.Succeeded && parsed.MemoryBytes == 67108864 && parsed.StackBytes == 1572864 && parsed.Descriptors == 16 &&
                parsed.Threads == (app == "NativeAotSuspensionSmoke" ? 4U : 8U) && parsed.EnvironmentCount == 1 && parsed.Args.Length == 0);
            HostCheck.Equal("apps/" + app, Encoding.ASCII.GetString(bytes, (int)parsed.Path.Offset, (int)parsed.Path.Length));
            HostCheck.Equal(app == "NativeAotSuspensionSmoke" ? "f135eac7774d92c9091c0a6954b915e60e403b80c50253fd396e2dd26370b8a1" :
                "e7a89ecabf8ff979ac018026dc6ac9a3174b973b9539feb221270f09532a9d24", Encoding.ASCII.GetString(bytes, (int)parsed.Digest.Offset, (int)parsed.Digest.Length));
            fixed (byte* source = bytes) HostCheck.True(LinuxUserInterruptPolicy.TryRead(source, (uint)bytes.Length, parsed, out bool enabled) && enabled);
            HostCheck.True(!Encoding.ASCII.GetString(bytes).Contains("DOTNET_", StringComparison.Ordinal));
        }
    }

    [HostTest("nativeaot.threading-package structurally valid substituted ELF manifest and reserved-header bytes grant no admission")]
    private static void Substitutions()
    {
        foreach (string app in NativeAotThreadingPayload.Applications)
        {
            byte[] elf = new byte[app == "NativeAotSuspensionSmoke" ? 1320176 : 1577712];
            LinuxThreadProofFixture.CreateElf("Threads").CopyTo(elf, 0);
            string policy = NativeAotThreadingPackage.Manifest(app);
            int digest = policy.IndexOf("digest=", StringComparison.Ordinal) + 7;
            policy = policy[..digest] + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() + policy[(digest + 64)..];
            byte[] manifest = Encoding.ASCII.GetBytes(policy), package = new byte[64 + manifest.Length + elf.Length];
            BinaryPrimitives.WriteUInt64LittleEndian(package, 0x3130464c45524c43);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1); BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64); BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length); manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
            HostCheck.True(LinuxElfPackage.Validate(package).Succeeded, "structurally valid counterfeit control");
            Reject(() => NativeAotThreadingPackage.RequirePackage(app, package), "identity");
            Encoding.ASCII.GetBytes(NativeAotThreadingPackage.Manifest(app)).CopyTo(package, 64);
            Reject(() => NativeAotThreadingPackage.RequirePackage(app, package), "identity");
            package[32] = 1; Reject(() => NativeAotThreadingPackage.RequirePackage(app, package), "identity");
            Reject(() => NativeAotThreadingPackage.RequirePackage(app, package[..^1]), "identity");
            Reject(() => NativeAotThreadingPackage.RequirePackage(app, []), "identity");
        }
    }

    [HostTest("nativeaot.threading-package missing evidence creates no output and fresh publication never overwrites or escapes artifacts")]
    private static void OutputFailures()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Path.Combine(root, "artifacts", "threading-package-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "app.pkg"), evidence = Path.Combine(directory, "absent");
        try
        {
            Reject(() => NativeAotThreadingPackage.Write(root, evidence, "NativeAotSuspensionSmoke", path), "threading_evidence_path");
            HostCheck.True(!File.Exists(path) && Directory.GetFiles(directory).Length == 0);
            File.WriteAllBytes(path, [1, 3, 5, 7]); Reject(() => NativeAotThreadingPackage.Write(root, evidence, "NativeAotSuspensionSmoke", path), "output_exists");
            HostCheck.True(File.ReadAllBytes(path).SequenceEqual(new byte[] { 1, 3, 5, 7 }));
            Reject(() => NativeAotThreadingPackage.Write(root, evidence, "NativeAotThreadPoolSmoke", Path.Combine(root, "threading-must-not-exist.pkg")), "output_path");
            var result = HostToolProcess.RunTool("linux", "threading-package", "NativeAotThreadPoolSmoke", evidence, Path.Combine(directory, "second.pkg"));
            HostCheck.True(result.ExitCode != 0 && !result.Output.Contains("TEST_PASS linux.threading-package", StringComparison.Ordinal) && !File.Exists(Path.Combine(directory, "second.pkg")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Reject(Action action, string reason)
    {
        string? failure = null; try { action(); } catch (Exception error) when (error is InvalidDataException or ArgumentException or ToolFailure) { failure = error.Message; }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }
}
