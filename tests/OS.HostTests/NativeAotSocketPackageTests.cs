using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotSocketPackageTests
{
    private static string[] Sources => NativeAotSocketPackage.CurrentSources().Select(source => source.Path).ToArray();

    [HostTest("socket package rejects changed relevant sources and new app or restore inputs")]
    private static void CurrentSources()
    {
        string root = HostToolProcess.FindRepositoryRoot(), temporary = Directory.CreateTempSubdirectory("clros-socket-sources-").FullName;
        try
        {
            foreach (string relative in Sources)
            {
                string path = Path.Combine(temporary, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(root, relative), path);
            }
            NativeAotSocketPackage.RequireCurrentSources(root);
            NativeAotSocketPackage.RequireCurrentSources(temporary);
            foreach (string relative in Sources)
            {
                string path = Path.Combine(temporary, relative);
                byte[] original = File.ReadAllBytes(path);
                File.AppendAllText(path, "\n");
                Reject(() => NativeAotSocketPackage.RequireCurrentSources(temporary), "source_identity");
                File.WriteAllBytes(path, original);
            }
            foreach (string relative in new[] { "apps/NativeAotSocketSmoke/Extra.cs", "NuGet.Config", "apps/Directory.Build.props" })
            {
                string path = Path.Combine(temporary, relative);
                File.WriteAllText(path, "new input");
                Reject(() => NativeAotSocketPackage.RequireCurrentSources(temporary), relative.EndsWith("Extra.cs", StringComparison.Ordinal) ? "source_inventory" : "source_override");
                File.Delete(path);
            }
            Directory.CreateDirectory(Path.Combine(temporary, "apps/NativeAotSocketSmoke/obj"));
            File.WriteAllText(Path.Combine(temporary, "apps/NativeAotSocketSmoke/obj/cache.cs"), "ignored cache");
            File.WriteAllText(Path.Combine(temporary, "unrelated-kernel.cs"), "independent edit");
            NativeAotSocketPackage.RequireCurrentSources(temporary);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [HostTest("socket package modes bind one original ELF explicit IRQs and the manifest endpoint")]
    private static void Policy()
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            byte[] text = Encoding.ASCII.GetBytes(NativeAotSocketPackage.Manifest(asynchronous));
            var parsed = NativeManifestParser.ParseLinuxElf(text);
            HostCheck.True(parsed.Succeeded && parsed.MemoryBytes == 67108864 && parsed.StackBytes == 1572864 && parsed.Threads == 16 &&
                parsed.Descriptors == 16 && parsed.EnvironmentCount == 1);
            HostCheck.Equal(asynchronous ? "--async" : "", Encoding.ASCII.GetString(text, (int)parsed.Args.Offset, (int)parsed.Args.Length));
            HostCheck.True(Encoding.ASCII.GetString(text).Contains("listen=0.0.0.0:8080\n", StringComparison.Ordinal));
            HostCheck.Equal(NativeAotSocketPackage.ElfSha256.ToLowerInvariant(), Encoding.ASCII.GetString(text, (int)parsed.Digest.Offset, (int)parsed.Digest.Length));
            fixed (byte* source = text)
                HostCheck.True(LinuxUserInterruptPolicy.TryRead(source, (uint)text.Length, parsed, out bool enabled) && enabled);
        }
    }

    [HostTest("socket package refuses a structurally valid replacement ELF and malformed canonical envelopes")]
    private static void Counterfeit()
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            byte[] elf = new byte[2117424];
            LinuxThreadProofFixture.CreateElf("Threads").CopyTo(elf, 0);
            string policy = NativeAotSocketPackage.Manifest(asynchronous);
            policy = policy.Replace(NativeAotSocketPackage.ElfSha256.ToLowerInvariant(), Convert.ToHexStringLower(SHA256.HashData(elf)), StringComparison.Ordinal);
            byte[] manifest = Encoding.ASCII.GetBytes(policy), package = new byte[64 + manifest.Length + elf.Length];
            BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
            manifest.CopyTo(package, 64);
            elf.CopyTo(package, 64 + manifest.Length);
            HostCheck.True(LinuxElfPackage.Validate(package).Succeeded, "valid format is insufficient authority");
            Reject(() => NativeAotSocketPackage.RequirePackage(package, asynchronous), "identity");
            Encoding.ASCII.GetBytes(NativeAotSocketPackage.Manifest(asynchronous)).CopyTo(package, 64);
            Reject(() => NativeAotSocketPackage.RequirePackage(package, asynchronous), "identity");
            Reject(() => NativeAotSocketPackage.RequirePackage(package, !asynchronous), "identity");
            package[32] = 1;
            Reject(() => NativeAotSocketPackage.RequirePackage(package, asynchronous), "identity");
            Reject(() => NativeAotSocketPackage.RequirePackage(package[..^1], asynchronous), "identity");
            Reject(() => NativeAotSocketPackage.RequirePackage([], asynchronous), "identity");
        }
    }

    [HostTest("socket package failed evidence creates no output and publication refuses overwrite or path escape")]
    private static void OutputFailures()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Path.Combine(root, "artifacts", "socket-package-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "app.pkg"), evidence = Path.Combine(directory, "absent");
        try
        {
            Reject(() => NativeAotSocketPackage.Write(root, evidence, false, path), "socket_evidence_path");
            HostCheck.True(!File.Exists(path) && Directory.GetFiles(directory).Length == 0);
            File.WriteAllBytes(path, [1, 3, 5, 7]);
            Reject(() => NativeAotSocketPackage.Write(root, evidence, true, path), "output_exists");
            HostCheck.True(File.ReadAllBytes(path).SequenceEqual(new byte[] { 1, 3, 5, 7 }));
            Reject(() => NativeAotSocketPackage.Write(root, evidence, false, Path.Combine(root, "socket-must-not-exist.pkg")), "output_path");
            var result = HostToolProcess.RunTool("linux", "socket-package", "invalid", evidence, Path.Combine(directory, "other.pkg"));
            HostCheck.True(result.ExitCode != 0 && !result.Output.Contains("TEST_PASS linux.socket-package", StringComparison.Ordinal) && !File.Exists(Path.Combine(directory, "other.pkg")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Reject(Action action, string reason)
    {
        string? failure = null;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or ToolFailure)
        {
            failure = error.Message;
        }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }
}
