using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using Fixture = OS.Tooling.LinuxSignalProofFixture;

internal static unsafe class LinuxSignalProofFixtureTests
{
    [HostTest("m16-009 signal packages have exact executable geometry and distinguish complete canonical inputs")]
    private static void Packages()
    {
        foreach (string variant in Fixture.Variants)
        {
            byte[] package = Fixture.CreatePackage(variant);
            var view = LinuxElfPackage.Validate(package);
            uint imageBytes = Fixture.IsWaitVariant(variant) ? 0xa000U : 0x7000U;
            HostCheck.True(view.Succeeded && view.PayloadBytes == imageBytes && view.Manifest.Threads == 2 && view.Manifest.Descriptors == 16);
            HostCheck.Equal(variant, Fixture.PackageVariant(package));
            string manifest = Encoding.ASCII.GetString(package, 64, (int)view.PayloadOffset - 64);
            HostCheck.True(manifest.Contains("app.path=apps/m16signals.elf\nargs=" + variant + "\n", StringComparison.Ordinal));
            fixed (byte* bytes = package)
            {
                HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes,
                    view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
                HostCheck.True(plan.LoadCount == 2 && plan.ImageBytes == imageBytes && plan.Entry == 0x400200 && plan.TlsMemoryBytes == 0);
            }
            using MemoryStream stream = new(package);
            Fixture.RequirePackage(stream);
            HostCheck.Equal(0L, stream.Position);
            foreach (byte[] invalid in new[] { package[..^1], (byte[])package.Clone(), LinuxThreadProofFixture.CreatePackage("Threads") })
            {
                if (invalid.Length == package.Length)
                    invalid[^1] ^= 1;
                Reject(() => Fixture.PackageVariant(invalid), "m16_009_package_identity");
                using MemoryStream bad = new(invalid);
                Reject(() => Fixture.RequirePackage(bad), "m16_009_package_identity");
            }
        }
    }

    [HostTest("m16-009 exact syscall instruction inventory includes real handler and restorer without replayed returns")]
    private static void Invocations()
    {
        uint[] all = [39, 186, 158, 13, 131, 14, 234, 14, 14, 186, 14, 131, 1, 15, 14, 131, 1, 231];
        foreach (string variant in Fixture.SyscallVariants)
        {
            byte[] elf = Fixture.CreateElf(variant);
            var calls = Fixture.ExpectedInvocations(variant);
            int count = variant == "BadRestorer" ? 9 : variant == "BadReturn" ? 14 : 18;
            HostCheck.True(calls.Select(c => c.Number).SequenceEqual(all.Take(count)) && calls.Count == count);
            HostCheck.Equal(variant.StartsWith("Bad", StringComparison.Ordinal) ? 139U : 42U, Fixture.ExpectedExitCode(variant));
            foreach (var call in calls)
            {
                int end = checked((int)(call.ReturnRip - 0x400000));
                HostCheck.True(elf[end - 2] == 0x0f && elf[end - 1] == 0x05);
                int start = end - (call.Number == 15 ? 7 : call.Number == 234 ? 52 : 40);
                HostCheck.True(elf[start] == 0xb8 && U32(elf, start + 1) == call.Number);
                HostCheck.True(call.Handler == (end > 0xa00 && end < 0xf00));
            }
            HostCheck.True(elf.AsSpan(0xe00, 9).SequenceEqual(new byte[] { 0xb8, 15, 0, 0, 0, 0x0f, 5, 0x0f, 0x0b }));
            int send = checked((int)(calls[6].ReturnRip - 0x400000));
            HostCheck.True(elf.AsSpan(send - 14, 2).SequenceEqual(new byte[] { 0x8b, 0x3d }) &&
                send - 8 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(send - 12)) == 0x1160);
            HostCheck.True(elf.AsSpan(send - 8, 2).SequenceEqual(new byte[] { 0x8b, 0x35 }) &&
                send - 2 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(send - 6)) == 0x1160);
        }
    }

    [HostTest("m16-009 Linux kernel action stack and saved-register vectors are independent of runtime-generated frames")]
    private static void AbiVectors()
    {
        foreach (string variant in Fixture.SyscallVariants)
        {
            byte[] elf = Fixture.CreateElf(variant);
            HostCheck.True(U64(elf, 0x1100) == 0x400a00 && U64(elf, 0x1108) == (variant == "UserStack" ? 0x04000004UL : 0x0c000004UL));
            HostCheck.Equal(variant == "BadRestorer" ? 0x500000UL : 0x400e00UL, U64(elf, 0x1110));
            HostCheck.True(U64(elf, 0x1120) == 0x406000 && U64(elf, 0x1128) == 0 && U64(elf, 0x1130) == 4096);
            HostCheck.True(U64(elf, 0x1140) == 0x200 && U64(elf, 0x1148) == 0 && U64(elf, 0x2000) == 0x13579bdf);
            ReadOnlySpan<byte> handler = elf.AsSpan(0xa00, 0x400);
            HostCheck.True(handler.IndexOf(new byte[] { 0x48, 0xc7, 0x82, 0x90, 0, 0, 0, 0x78, 0x56, 0x34, 0x12 }) >= 0); // Saved RAX.
            HostCheck.True((handler.IndexOf(new byte[] { 0x66, 0xc7, 0x82, 0xb8, 0, 0, 0, 8, 0 }) >= 0) == (variant == "BadReturn")); // Saved CS.
            HostCheck.True(handler.IndexOf(new byte[] { 0x48, 0x8b, 0x82, 0xa0, 0, 0, 0 }) >= 0 &&
                handler.IndexOf(new byte[] { 0x48, 0x63, 0x46, 8 }) >= 0); // Saved RSP and signed si_code.
        }
    }

    [HostTest("m16-009 FP GPR DF and red-zone proof instructions retain nontrivial interrupted state")]
    private static void RegisterVectors()
    {
        byte[] elf = Fixture.CreateElf("AlternateStack");
        HostCheck.True(BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(0x1168)) == 0x27f && U32(elf, 0x116c) == 0x5f80);
        ReadOnlySpan<byte> parent = elf.AsSpan(0x200, 0x800), handler = elf.AsSpan(0xa00, 0x400);
        HostCheck.True(parent.IndexOf(new byte[] { 0xbb, 0xdf, 0x9b, 0x57, 0x13 }) >= 0 &&
            handler.IndexOf(new byte[] { 0xbb, 0x10, 0x32, 0x54, 0x76 }) >= 0);
        HostCheck.True(parent.IndexOf(new byte[] { 0x66, 0x0f, 0x6e, 0xc0 }) >= 0 &&
            parent.IndexOf(new byte[] { 0x66, 0x0f, 0x7e, 0xc0 }) >= 0 && handler.IndexOf(new byte[] { 0x66, 0x0f, 0x76, 0xc0 }) >= 0);
        HostCheck.True(parent.IndexOf(new byte[] { 0xfd, 0xb8, 14, 0, 0, 0 }) >= 0 &&
            handler.IndexOf(new byte[] { 0x9c, 0x58, 0x25, 0, 4, 0, 0, 0x48, 0x3d, 0, 0, 0, 0 }) >= 0);
        HostCheck.True(elf.AsSpan(0x180, 15).SequenceEqual("M16_SIGNALS_OK!"u8) && elf.AsSpan(0x1a0, 15).SequenceEqual("M16_HANDLER_OK!"u8));
    }

    [HostTest("m16-009 fresh atomic package command never overwrites and rejects unsupported variants")]
    private static void CommandAndAdmission()
    {
        string directory = Directory.CreateTempSubdirectory("clros-signal-fixture-").FullName;
        string path = Path.Combine(directory, "signal.pkg");
        try
        {
            var result = HostToolProcess.RunTool("linux", "signal-fixture", path, "BadReturn");
            HostCheck.True(result.ExitCode == 0 && result.Output.Contains("TEST_PASS linux.signal-fixture", StringComparison.Ordinal));
            byte[] before = File.ReadAllBytes(path);
            bool rejected = false;
            try
            {
                Fixture.WritePackage(path);
            }
            catch (IOException) { rejected = true; }
            HostCheck.True(rejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(before));
            foreach (string invalid in new[] { "", "alternatestack", "BadReturn ", "Threads", "Hello" })
            {
                Reject(() => Fixture.CreateElf(invalid), "m16_009_fixture_variant");
                Reject(() => Fixture.ExpectedInvocations(invalid), "m16_009_fixture_variant");
                Reject(() => Fixture.ExpectedExitCode(invalid), "m16_009_fixture_variant");
            }
            byte[] copy = Fixture.CreateElf("AlternateStack");
            copy[0x180] ^= 1;
            HostCheck.True(Fixture.CreateElf("AlternateStack").AsSpan(0x180, 15).SequenceEqual("M16_SIGNALS_OK!"u8));
            HostCheck.True(!ReferenceEquals(Fixture.ExpectedInvocations("UserStack"), Fixture.ExpectedInvocations("UserStack")));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    [HostTest("m16-009 mechanical helper reuse preserves all four original M16-008 package identities")]
    private static void AncestorPackages()
    {
        (string Variant, string Hash)[] original = [
            ("Threads","1DD7D9828914DB92A8C0FAB494C6841ED3CD02217F2634F4A1F44A813ABA374E"),
            ("PipeReadClose","2DEC92AACEB1F7CCD6EFF1E1E382F2656F5D8609AD857DB6396E2421B7839D9F"),
            ("PipeWriteClose","10701621C7A348C3588C0A9F76CC12519FC57DB6706A62CCE8B582E5DF9755FC"),
            ("PipeShutdown","562E754486865EA0A06E93099B39309C50402A5218B5B452004FA4924BDE2163")];
        foreach (var item in original)
            HostCheck.Equal(item.Hash, Convert.ToHexString(SHA256.HashData(LinuxThreadProofFixture.CreatePackage(item.Variant))));
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]);
    private static void Reject(Action action, string reason)
    {
        string? actual = null;
        try
        {
            action();
        }
        catch (ToolFailure error) { actual = error.Reason; }
        HostCheck.Equal(reason, actual!);
    }
}
