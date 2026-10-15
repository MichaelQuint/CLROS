using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotHelloGuestTests
{
    [HostTest("nativeaot.hello-guest current raw app and producer-policy sources bind without whole-repository identity")]
    private static void SourceIdentity()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        NativeAotHelloGuest.RequireCurrentSources(root);
        string temporary = Directory.CreateTempSubdirectory("clros-hello-sources-").FullName;
        string[] sources = ["apps/NativeAotHello/Program.cs", "apps/NativeAotHello/NativeAotHello.csproj",
            "global.json", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props",
            "tools/OS.Tooling/NativeAotPayload.cs"];
        try
        {
            foreach (string source in sources)
            {
                string path = Path.Combine(temporary, source);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(root, source), path);
            }
            File.WriteAllText(Path.Combine(temporary, "unrelated.cs"), "changed kernel/tooling source");
            NativeAotHelloGuest.RequireCurrentSources(temporary);
            foreach (string source in sources)
            {
                string path = Path.Combine(temporary, source);
                byte[] original = File.ReadAllBytes(path);
                File.AppendAllText(path, "\n"); // Even an otherwise harmless raw-byte change requires requalification.
                Reject(() => NativeAotHelloGuest.RequireCurrentSources(temporary), "hello_source_identity " + source);
                File.WriteAllBytes(path, original);
                NativeAotHelloGuest.RequireCurrentSources(temporary);
            }
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [HostTest("nativeaot.hello-guest manifest retains bounded upstream startup policy and explicit IRQ authority")]
    private static void Policy()
    {
        byte[] bytes = Encoding.ASCII.GetBytes(NativeAotHelloGuest.Manifest);
        var parsed = NativeManifestParser.ParseLinuxElf(bytes);
        HostCheck.True(parsed.Succeeded && parsed.MemoryBytes == 64 * 1024 * 1024 && parsed.StackBytes == 1_572_864 &&
            parsed.Threads == 4 && parsed.Descriptors == 16 && parsed.EnvironmentCount == 1 && parsed.Args.Length == 0);
        HostCheck.Equal("apps/NativeAotHello", Encoding.ASCII.GetString(bytes, (int)parsed.Path.Offset, (int)parsed.Path.Length));
        HostCheck.Equal(NativeAotHelloGuest.ElfSha256.ToLowerInvariant(),
            Encoding.ASCII.GetString(bytes, (int)parsed.Digest.Offset, (int)parsed.Digest.Length));
        fixed (byte* p = bytes)
            HostCheck.True(LinuxUserInterruptPolicy.TryRead(p, (uint)bytes.Length, parsed, out bool enabled) && enabled);
        HostCheck.True(!NativeAotHelloGuest.Manifest.Contains("DOTNET_", StringComparison.Ordinal));
    }

    [HostTest("nativeaot.hello-guest rejects valid mechanical substituted ELF even with matching length and recomputed digest")]
    private static void PackageSubstitution()
    {
        // A valid complete ELF package independent of the Hello packager: pad a
        // mechanical ELF to the real payload length and supply its honest digest.
        byte[] elf = new byte[NativeAotHelloGuest.ElfBytes];
        LinuxThreadProofFixture.CreateElf("Threads").CopyTo(elf, 0);
        string digest = Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant();
        byte[] manifest = Encoding.ASCII.GetBytes(NativeAotHelloGuest.Manifest.Replace(
            NativeAotHelloGuest.ElfSha256.ToLowerInvariant(), digest, StringComparison.Ordinal));
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, 0x3130464c45524c43);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        HostCheck.True(LinuxElfPackage.Validate(package).Succeeded, "valid substitution control");
        Reject(() => NativeAotHelloGuest.RequirePackage(package), "hello_package_identity");
        Reject(() => NativeAotHelloGuest.RequirePackage(package[..^1]), "hello_package_identity");
        Reject(() => NativeAotHelloGuest.RequirePackage(LinuxThreadProofFixture.CreatePackage("Threads")), "hello_package_identity");
        Encoding.ASCII.GetBytes(NativeAotHelloGuest.Manifest).CopyTo(package, 64);
        Reject(() => NativeAotHelloGuest.RequirePackage(package), "hello_package_identity");
    }

    [HostTest("nativeaot.hello-guest failed evidence leaves no output and fresh publication never overwrites")]
    private static void OutputFailures()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "hello-package-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "hello.pkg");
        string absent = Path.Combine(directory, "absent-evidence");
        try
        {
            bool rejected = false;
            try { NativeAotHelloGuest.WritePackage(root, absent, path); }
            catch (DirectoryNotFoundException) { rejected = true; }
            HostCheck.True(rejected && !File.Exists(path) && Directory.GetFiles(directory).Length == 0);
            File.WriteAllBytes(path, [1, 3, 5, 7]);
            rejected = false;
            try { NativeAotHelloGuest.WritePackage(root, absent, path); }
            catch (IOException error) { rejected = error.Message == "hello_output_exists"; }
            HostCheck.True(rejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(new byte[] { 1, 3, 5, 7 }));
            Reject(() => NativeAotHelloGuest.WritePackage(root, absent, Path.Combine(root, "outside.pkg")), "hello_output_outside_artifacts");
            rejected = false;
            try { NativeAotHelloGuest.WritePackage(root, absent, Path.Combine(directory, "missing-parent", "hello.pkg")); }
            catch (DirectoryNotFoundException error) { rejected = error.Message == "hello_output_parent_missing"; }
            HostCheck.True(rejected && !Directory.Exists(Path.Combine(directory, "missing-parent")));
            var result = HostToolProcess.RunTool("linux", "hello-package", absent, Path.Combine(directory, "rejected.pkg"));
            HostCheck.True(result.ExitCode != 0 && result.Error.Contains("TEST_FAIL tooling", StringComparison.Ordinal) &&
                !result.Output.Contains("TEST_PASS linux.hello-package", StringComparison.Ordinal) && !File.Exists(Path.Combine(directory, "rejected.pkg")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try { action(); }
        catch (InvalidDataException error) { rejected = error.Message == reason; }
        HostCheck.True(rejected, reason);
    }
}
