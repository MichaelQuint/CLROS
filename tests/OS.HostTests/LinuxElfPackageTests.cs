using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

internal static unsafe class LinuxElfPackageTests
{
    internal static byte[] CapturePackage() => Package();
    private static byte[] Elf()
    {
        byte[] elf = new byte[4096]; "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2; elf[5] = 1; elf[6] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(16), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(18), 62);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(20), 1);
        for (int i = 64; i < elf.Length; i++) elf[i] = (byte)(i * 31);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(24), 0x400200);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(32), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(52), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(54), 56);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(56), 1);
        elf.AsSpan(64, 56).Clear(); W32(elf, 64, 1); W32(elf, 68, 5);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(80), 0x400000);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(96), 4096);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(104), 4096);
        return elf;
    }
    private static string Manifest(byte[] elf) => "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/hello.elf\nargs=\"one two\" three \"\"\nenv.TEST=value=with=equals\ndigest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() + "\nmemory=67108864\nstack=1572864\nthreads=8\ndescriptors=64\nlisten=127.0.0.1:8080\n";
    private static byte[] Package(byte[]? elf = null, string? manifest = null)
    {
        elf ??= Elf(); byte[] text = Encoding.ASCII.GetBytes(manifest ?? Manifest(elf));
        byte[] bytes = new byte[64 + text.Length + elf.Length];
        "CLRELF01"u8.CopyTo(bytes); W32(bytes, 8, 1); W32(bytes, 12, 1); W32(bytes, 16, 64); W32(bytes, 20, (uint)text.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), (ulong)elf.Length);
        text.CopyTo(bytes, 64); elf.CopyTo(bytes, 64 + text.Length); return bytes;
    }
    private static void W32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
    private static LinuxElfPackageView Validate(byte[] bytes) { fixed (byte* p = bytes) return LinuxElfPackage.Validate(p, (uint)bytes.Length); }
    private static LinuxElfManifest Parse(string text) => NativeManifestParser.ParseLinuxElf(Encoding.ASCII.GetBytes(text));

    [HostTest("linux.elf SHA256 independent standard and padding vectors")]
    private static void HashVectors()
    {
        foreach (int n in new[] { 0, 1, 3, 55, 56, 63, 64, 65, 119, 120, 127, 128, 129, 4096, 1000000 })
        {
            byte[] bytes = Enumerable.Repeat((byte)'a', n).ToArray(); byte[] digest = new byte[32];
            fixed (byte* input = bytes) fixed (byte* output = digest) HostCheck.True(OS.Std.Hash.Sha256.TryCompute(input, (uint)n, output, 32));
            HostCheck.True(digest.SequenceEqual(SHA256.HashData(bytes)), "hash length=" + n);
        }
        byte[] abc = "abc"u8.ToArray(), hash = new byte[32];
        fixed (byte* p = abc) fixed (byte* h = hash) HostCheck.True(OS.Std.Hash.Sha256.TryCompute(p, 3, h, 32));
        HostCheck.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", Convert.ToHexString(hash));
    }

    [HostTest("linux.elf SHA256 rejects invalid bounds and overlap without writes")]
    private static void HashBounds()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xcc, 128).ToArray(); byte[] original = bytes.ToArray();
        fixed (byte* p = bytes)
        {
            HostCheck.True(!OS.Std.Hash.Sha256.TryCompute(null, 1, p, 32));
            HostCheck.True(!OS.Std.Hash.Sha256.TryCompute(p, OS.Std.Hash.Sha256.MaximumBytes + 1, p + 64, 32));
            HostCheck.True(!OS.Std.Hash.Sha256.TryCompute(p, 1, p + 64, 31));
            HostCheck.True(!OS.Std.Hash.Sha256.TryCompute(p, 65, p + 64, 32));
            HostCheck.True(!OS.Std.Hash.Sha256.TryCompute(p, 1, null, 32));
        }
        HostCheck.True(bytes.SequenceEqual(original));
    }

    [HostTest("linux.elf package manifest digest metadata and exact byte copy")]
    private static void Positive()
    {
        byte[] bytes = Package(), original = bytes.ToArray(), destination = new byte[bytes.Length];
        var v = Validate(bytes); HostCheck.True(v.Succeeded); HostCheck.Equal((uint)4096, v.PayloadBytes);
        HostCheck.Equal(0x7f000001u, v.Manifest.Ipv4); HostCheck.Equal(8080u, v.Manifest.Port);
        HostCheck.Equal(1u, v.Manifest.EnvironmentCount); HostCheck.Equal(1572864u, v.Manifest.StackBytes);
        fixed (byte* p = bytes) fixed (byte* d = destination)
        {
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.Copy(p, (uint)bytes.Length, d, (uint)destination.Length, out var copy));
            HostCheck.True(copy.Succeeded); HostCheck.Equal(v.PayloadOffset, copy.PayloadOffset);
        }
        HostCheck.True(destination.SequenceEqual(bytes)); HostCheck.True(bytes.SequenceEqual(original));
        var reordered = Parse(string.Join('\n', Manifest(Elf()).TrimEnd('\n').Split('\n').Reverse()));
        HostCheck.True(reordered.Succeeded);
        HostCheck.True(Parse(Manifest(Elf()).Replace("\n", "\r\n")).Succeeded);
    }

    [HostTest("linux.elf rejects header truncation profile lengths reserved and stale bytes")]
    private static void Headers()
    {
        byte[] source = Package();
        for (int n = 0; n < 64; n++) HostCheck.True(!Validate(source[..n]).Succeeded);
        foreach (int at in new[] { 0, 7, 8, 12, 16, 20, 24, 31, 32, 63, source.Length - 1 })
        {
            byte[] mutated = source.ToArray(); mutated[at] ^= 0x80; HostCheck.True(!Validate(mutated).Succeeded, "byte=" + at);
        }
        HostCheck.True(!Validate(source[..^1]).Succeeded); HostCheck.True(!Validate([.. source, 0]).Succeeded);
        fixed (byte* p = source) HostCheck.True(!LinuxElfPackage.Validate(p, LinuxElfPackage.MaximumBytes + 1).Succeeded);
        HostCheck.True(!default(LinuxElfPackageView).Succeeded); HostCheck.True(!default(LinuxElfManifest).Succeeded);
        byte[] elf = Elf(); elf[16] = 3; HostCheck.Equal(LinuxElfStatus.UnsupportedElf, Validate(Package(elf)).Status);
        foreach (uint type in new uint[] { 2, 3 }) { elf = Elf(); W32(elf, 64, type); HostCheck.Equal(LinuxElfStatus.UnsupportedElf, Validate(Package(elf)).Status); }
        elf = Elf(); W32(elf, 68, 7); HostCheck.Equal(LinuxElfStatus.UnsupportedElf, Validate(Package(elf)).Status);
        elf = Elf(); elf[56] = 0; HostCheck.Equal(LinuxElfStatus.UnsupportedElf, Validate(Package(elf)).Status);
        string digest = Convert.ToHexString(SHA256.HashData(Elf())).ToLowerInvariant();
        HostCheck.Equal(LinuxElfStatus.DigestMismatch, Validate(Package(manifest: Manifest(Elf()).Replace(digest, new string('0', 64)))).Status);
    }

    [HostTest("linux.elf manifest rejects missing duplicate malformed unknown and v0 mixing")]
    private static void Fields()
    {
        string good = Manifest(Elf());
        foreach (string line in good.TrimEnd('\n').Split('\n').Where(x => !x.StartsWith("env.", StringComparison.Ordinal)))
        {
            HostCheck.Equal(LinuxElfStatus.MissingField, Parse(good.Replace(line + "\n", "")).Status);
            HostCheck.Equal(LinuxElfStatus.DuplicateField, Parse(good + line + "\n").Status);
        }
        HostCheck.Equal(LinuxElfStatus.UnknownField, Parse(good + "extra=1\n").Status);
        HostCheck.Equal(LinuxElfStatus.DuplicateField, Parse(good + "env.TEST=x\n").Status);
        HostCheck.Equal(LinuxElfStatus.InvalidEnvironment, Parse(good + "env.bad=x\n").Status);
        HostCheck.True(!Parse(good + "\n").Succeeded); HostCheck.True(!Parse(good + "broken\n").Succeeded);
        HostCheck.True(!Parse(good.Replace("version=1", "version=0")).Succeeded);
        HostCheck.True(!NativeManifestParser.Parse(Encoding.ASCII.GetBytes(good)).Succeeded);
        HostCheck.True(!Parse(good.Replace("clros-linux-elf", "os-native-manifest")).Succeeded);
        HostCheck.True(!Parse(good.Replace("linux-musl-x64-static-v1", "linux-x64")).Succeeded);
        HostCheck.True(!Parse(good.Replace("\n", "\r")).Succeeded);
    }

    [HostTest("linux.elf manifest rejects unsafe paths endpoints resources and environment limits")]
    private static void Limits()
    {
        string good = Manifest(Elf());
        foreach (string path in new[] { "", "/app", "../app", "a/./b", "a//b", "a/", "C:app", "a\\b", new string('a', 129) }) HostCheck.True(!Parse(good.Replace("apps/hello.elf", path)).Succeeded);
        foreach (string endpoint in new[] { "localhost:8080", "[::1]:8080", "256.0.0.1:80", "127.0.0.1:0", "127.0.0.1:65536", "127.0.0.1:80:90", "01.0.0.1:80" }) HostCheck.True(!Parse(good.Replace("127.0.0.1:8080", endpoint)).Succeeded);
        foreach (string value in new[] { "0", "4095", "1073745920", "4294967296", "18446744073709551615", "01", "-1", "+1" }) HostCheck.True(!Parse(good.Replace("memory=67108864", "memory=" + value)).Succeeded);
        HostCheck.True(!Parse(good.Replace("stack=1572864", "stack=67108864")).Succeeded);
        HostCheck.True(!Parse(good.Replace("threads=8", "threads=65")).Succeeded);
        HostCheck.True(!Parse(good.Replace("descriptors=64", "descriptors=1025")).Succeeded);
        HostCheck.True(!Parse(good.Replace("args=\"one two\" three \"\"", "args=\"unclosed")).Succeeded);
        string env = good.Replace("env.TEST=value=with=equals\n", "");
        for (int i = 0; i < 16; i++) env += "env.K" + i + "=\n";
        HostCheck.True(Parse(env).Succeeded); HostCheck.True(!Parse(env + "env.EXTRA=x\n").Succeeded);
        HostCheck.True(!Parse(good.Replace("env.TEST=value=with=equals", "env.TEST=" + new string('x', 257))).Succeeded);
    }

    [HostTest("linux.elf failed staging leaves caller bytes unchanged")]
    private static void CopyFailures()
    {
        byte[] source = Package(), destination = Enumerable.Repeat((byte)0xcc, source.Length).ToArray(), original = destination.ToArray();
        fixed (byte* p = source) fixed (byte* d = destination)
        {
            HostCheck.Equal(LinuxElfStatus.InvalidDestination, LinuxElfPackage.Copy(p, (uint)source.Length, d, (uint)source.Length - 1, out var v));
            HostCheck.True(!v.Succeeded);
            HostCheck.Equal(LinuxElfStatus.InvalidDestination, LinuxElfPackage.Copy(p, (uint)source.Length, p, (uint)source.Length, out v));
            HostCheck.True(!v.Succeeded);
            source[^1] ^= 1;
            HostCheck.Equal(LinuxElfStatus.DigestMismatch, LinuxElfPackage.Copy(p, (uint)source.Length, d, (uint)destination.Length, out v));
            HostCheck.True(!v.Succeeded);
        }
        HostCheck.True(destination.SequenceEqual(original));
    }

    [HostTest("linux.elf immutable file namespace exact path EOF and alias rejection")]
    private static void FileNamespace()
    {
        byte[] source = Package(), path = "apps/hello.elf"u8.ToArray(), output = new byte[17], elf = Elf();
        fixed (byte* p = source) fixed (byte* name = path) fixed (byte* d = output)
        {
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.ReadFile(p, (uint)source.Length, name, (uint)path.Length, 64, d, 17, out uint n));
            HostCheck.Equal(17u, n); HostCheck.True(output.SequenceEqual(elf[64..81]));
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.ReadFile(p, (uint)source.Length, name, (uint)path.Length, 4096, d, 17, out n)); HostCheck.Equal(0u, n);
            HostCheck.Equal(LinuxElfStatus.InvalidDestination, LinuxElfPackage.ReadFile(p, (uint)source.Length, name, (uint)path.Length, 4097, d, 17, out n));
            HostCheck.Equal(LinuxElfStatus.InvalidDestination, LinuxElfPackage.ReadFile(p, (uint)source.Length, name, (uint)path.Length, 0, p, 17, out n));
            HostCheck.Equal(LinuxElfStatus.InvalidPath, LinuxElfPackage.ReadFile(p, (uint)source.Length, name, (uint)path.Length - 1, 0, d, 17, out n));
            source[^1] ^= 1;
            HostCheck.Equal(LinuxElfStatus.DigestMismatch, LinuxElfPackage.ReadFile(p, (uint)source.Length, name, (uint)path.Length, 0, d, 17, out n));
        }
    }

    [HostTest("linux.elf profile owns prerequisites and rejects unsafe combinations")]
    private static void Profile()
    {
        HostCheck.True(!OS.Kernel.Diagnostics.Probes.EnableM16002Probe);
        var profile = OS.Tooling.EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16002Probe"] = true });
        HostCheck.Equal("Diagnostic", profile.BootPolicy);
        HostCheck.Equal(6, profile.Flags.Values.Count(x => x));
        foreach (string flag in new[] { "M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery", "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival", "M16002" }) HostCheck.True(profile.IsEnabled(flag));
        foreach (var extra in new[] { ("EnableM03FirstRealExitBootServicesProbe", false), ("EnableM05PciEnumerationProbe", true), ("EnableM04SimpleThrowCatchProbe", true) })
        {
            bool rejected = false;
            try { _ = OS.Tooling.EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16002Probe"] = true, [extra.Item1] = extra.Item2 }); }
            catch (OS.Tooling.ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        var plan = OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16002Probe", "-RequireMarker", "CLROS_M16_002_READY", "-DryRun"));
        HostCheck.True(plan.IsEnabled("EnableM16002Probe"));
        HostCheck.True(plan.Required.Contains("TEST_PASS m16-002"));
        HostCheck.True(plan.Forbidden.Contains("M16_002_STAGE_REJECTED"));
    }

    [HostTest("linux.elf transcript binds exact payload package ownership and seven records")]
    private static void Transcript()
    {
        byte[] package = Package(); var view = Validate(package);
        string payloadHash = Convert.ToHexString(SHA256.HashData(Elf())).ToLowerInvariant();
        string packageHash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        string[] tail = ["TEST_START m16-002", $"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length + 4095) / 4096} handlesClosed=2 reads=1",
            "M16_002_DIGEST " + payloadHash, "M16_002_PACKAGE_DIGEST " + packageHash,
            "M16_002_OWNED firmware=exited package=read-only-nx ledger=valid fileBytes=64 entry=false", "CLROS_M16_002_READY", "TEST_PASS m16-002"];
        string[] lines = [.. OS.Tooling.QemuContracts.M03OwnedSubsystemMarkers, .. tail];
        HostCheck.True(OS.Tooling.QemuTranscripts.ValidateLinuxElfTail(lines, package).SequenceEqual(OS.Tooling.QemuContracts.M03OwnedSubsystemMarkers));
        for (int i = lines.Length - 7; i < lines.Length; i++)
        {
            string[] changed = lines.ToArray(); changed[i] += " extra"; RejectTranscript(changed, package);
        }
        RejectTranscript(lines[..^1], package); RejectTranscript([.. lines, "TEST_PASS m16-002"], package);
        string[] bad = lines.ToArray(); bad[^6] = bad[^6].Replace("handlesClosed=2", "handlesClosed=1"); RejectTranscript(bad, package);
        bad = lines.ToArray(); bad[^6] = bad[^6].Replace("reads=1", "reads=0"); RejectTranscript(bad, package);
        bad = lines.ToArray(); bad[^3] = bad[^3].Replace("entry=false", "entry=true"); RejectTranscript(bad, package);
    }
    private static void RejectTranscript(string[] lines, byte[] package)
    {
        bool rejected = false; try { _ = OS.Tooling.QemuTranscripts.ValidateLinuxElfTail(lines, package); } catch (OS.Tooling.ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("linux.elf fault image fixture preserves EFI and changes only declared bytes")]
    private static void ImageFaults()
    {
        byte[] image = new byte[64 * 1024 * 1024], package = Package(), efi = new byte[1024];
        efi[0] = (byte)'M'; efi[1] = (byte)'Z';
        const int partition = 2048 * 512, fat = partition + 32 * 512, fatBytes = 1024 * 512, data = fat + fatBytes * 2;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(partition + 11), 512); image[partition + 13] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(partition + 14), 32); image[partition + 16] = 2;
        W32(image, partition + 36, 1024); W32(image, partition + 44, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(partition + 510), 0xaa55);
        int Cluster(uint n) => data + ((int)n - 2) * 512;
        void Link(uint n, uint next) { W32(image, fat + (int)n * 4, next); W32(image, fat + fatBytes + (int)n * 4, next); }
        void Entry(uint directory, int index, string name, byte attributes, ushort first, uint bytes)
        {
            int at = Cluster(directory) + index * 32; Encoding.ASCII.GetBytes(name).CopyTo(image, at); image[at + 11] = attributes;
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(at + 26), first); W32(image, at + 28, bytes);
        }
        Entry(2, 0, "EFI        ", 0x10, 3, 0); Entry(2, 1, "OS         ", 0x10, 7, 0);
        Entry(3, 0, "BOOT       ", 0x10, 4, 0); Entry(4, 0, "BOOTX64 EFI", 0x20, 5, 1024);
        Entry(7, 0, "APP     PKG", 0x20, 8, (uint)package.Length); efi.CopyTo(image, Cluster(5)); package.CopyTo(image, Cluster(8));
        foreach (uint n in new uint[] { 2, 3, 4, 6, 7 }) Link(n, 0x0fffffff); Link(5, 6);
        uint count = ((uint)package.Length + 511) / 512;
        for (uint i = 0; i < count; i++) Link(8 + i, i + 1 == count ? 0x0fffffffu : 9 + i);
        string root = Path.Combine(Path.GetTempPath(), "clros-linux-elf-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string disk = Path.Combine(root, "image.raw"), elf = Path.Combine(root, "boot.efi"), input = Path.Combine(root, "app.pkg");
        try
        {
            File.WriteAllBytes(elf, efi); File.WriteAllBytes(input, package);
            foreach (string variant in new[] { "DigestFault", "Oversized" })
            {
                File.WriteAllBytes(disk, image);
                var receipt = OS.Tooling.LinuxElfImageFixture.Apply(disk, elf, input, variant);
                byte[] changed = File.ReadAllBytes(disk); int differences = 0;
                for (int i = 0; i < image.Length; i++) if (image[i] != changed[i])
                { differences++; HostCheck.True(i >= receipt.ChangedOffset && i < receipt.ChangedOffset + (variant == "DigestFault" ? 1 : 4)); }
                HostCheck.Equal(receipt.ChangedBytes, differences); HostCheck.True(differences > 0);
                HostCheck.True(changed.AsSpan(Cluster(5), 1024).SequenceEqual(efi));
                bool rejected = false; try { _ = OS.Tooling.LinuxElfImageFixture.Apply(disk, elf, input, variant); } catch (OS.Tooling.ToolFailure) { rejected = true; }
                HostCheck.True(rejected);
            }
        }
        finally { File.Delete(disk); File.Delete(elf); File.Delete(input); Directory.Delete(root); }
    }
}
