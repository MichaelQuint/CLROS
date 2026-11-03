using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe partial class LinuxThreadProofFixtureTests
{
    [HostTest("m16-008 compiled package writer never overwrites and preflight rejects noncanonical retained inputs")]
    private static void PackageFileAndPreflight()
    {
        string directory = Directory.CreateTempSubdirectory("clros-thread-fixture-").FullName;
        string path = Path.Combine(directory, "thread.pkg");
        try
        {
            LinuxThreadProofFixture.WritePackage(path);
            byte[] expected = LinuxThreadProofFixture.CreatePackage("Threads");
            HostCheck.True(File.ReadAllBytes(path).AsSpan().SequenceEqual(expected));
            bool rejected = false;
            try
            {
                LinuxThreadProofFixture.WritePackage(path);
            }
            catch (IOException)
            {
                rejected = true;
            }

            HostCheck.True(rejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(expected));
            using (FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LinuxThreadProofFixture.RequirePackage(input);
                HostCheck.Equal(0L, input.Position);
            }

            foreach (byte[] invalid in new[]
            {
                Array.Empty<byte>(),
                expected[..^1],
                LinuxDescriptorProofFixture.CreatePackage("Pipes"),
                (byte[])expected.Clone()
            }

            )
            {
                if (invalid.Length == expected.Length)
                    invalid[^1] ^= 1;
                using MemoryStream input = new(invalid);
                rejected = false;
                try
                {
                    LinuxThreadProofFixture.RequirePackage(input);
                }
                catch (ToolFailure)
                {
                    rejected = true;
                }

                HostCheck.True(rejected);
            }

            byte[] foreign = [1, 2, 3];
            File.WriteAllBytes(path, foreign);
            rejected = false;
            try
            {
                LinuxThreadProofFixture.WritePackage(path);
            }
            catch (IOException)
            {
                rejected = true;
            }

            HostCheck.True(rejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(foreign));
        }
        finally
        {
            HostCheck.True(Path.GetDirectoryName(Path.GetFullPath(path)) == Path.GetFullPath(directory));
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [HostTest("m16-008 thread package retains one RX page separate static stacks TLS and complete deterministic ELF digest", HostLane.Fast, 0)]
    private static void PackageGeometry()
    {
        byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded);
        HostCheck.Equal(0x7000UL, view.PayloadBytes);
        HostCheck.Equal(2U, view.Manifest.Threads);
        HostCheck.Equal(16U, view.Manifest.Descriptors);
        ReadOnlySpan<byte> elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes);
        HostCheck.True(elf[..7].SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }));
        HostCheck.Equal((ushort)2, U16(elf, 16));
        HostCheck.Equal((ushort)62, U16(elf, 18));
        HostCheck.Equal(0x400200UL, U64(elf, 24));
        HostCheck.Equal(64UL, U64(elf, 32));
        HostCheck.Equal((ushort)64, U16(elf, 52));
        HostCheck.Equal((ushort)56, U16(elf, 54));
        HostCheck.Equal((ushort)3, U16(elf, 56));
        HostCheck.True(U32(elf, 64) == 1 && U32(elf, 68) == 5 && U64(elf, 72) == 0 && U64(elf, 80) == 0x400000 && U64(elf, 96) == 4096 && U64(elf, 104) == 4096 && U64(elf, 112) == 4096);
        HostCheck.True(U32(elf, 120) == 1 && U32(elf, 124) == 6 && U64(elf, 128) == 4096 && U64(elf, 136) == 0x401000 && U64(elf, 152) == 0x6000 && U64(elf, 160) == 0x6000 && U64(elf, 168) == 4096);
        HostCheck.True(U32(elf, 176) == 0x6474e551 && U32(elf, 180) == 6 && U64(elf, 224) == 16);
        string manifest = Encoding.ASCII.GetString(package.AsSpan(64, checked((int)view.PayloadOffset - 64)));
        HostCheck.True(manifest.Contains("app.path=apps/m16threads.elf\nargs=Threads\n", StringComparison.Ordinal) && manifest.Contains("digest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() + "\n", StringComparison.Ordinal));
        fixed (byte* bytes = package)
        {
            HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes, view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
            HostCheck.True(plan.LoadCount == 2 && plan.ImageBytes == 0x7000 && plan.Entry == 0x400200 && plan.ProgramHeaders == 0x400040 && plan.TlsMemoryBytes == 0);
        }

        HostCheck.True(package.AsSpan().SequenceEqual(LinuxThreadProofFixture.CreatePackage("Threads")));
        HostCheck.True(SHA256.HashData(package).AsSpan().SequenceEqual(SHA256.HashData(LinuxThreadProofFixture.CreatePackage("Threads"))));
        package[^1] ^= 1;
        HostCheck.True(!LinuxElfPackage.Validate(package).Succeeded);
    }

    [HostTest("m16-008 thread fixture independently decodes parent child syscall inventories and live fd clone TID arguments", HostLane.Fast, 1)]
    private static void CallInventories()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        var parent = Decode(elf, 0x200, 0x900);
        var child = Decode(elf, 0x900, 0xf00);
        uint[] parentNumbers = [39, 186, 158, 14, 293, 56, 202, 14, 158, 56, 0, 56, 39, 158, 14, 202, 0, 202, 0, 202, 202, 3, 3, 1, 231];
        uint[] childNumbers = [39, 186, 158, 14, 14, 14, 158, 158, 1, 202, 1, 60];
        HostCheck.True(parent.Calls.Select(c => c.Number).SequenceEqual(parentNumbers) && child.Calls.Select(c => c.Number).SequenceEqual(childNumbers));
        HostCheck.Equal(55U, LinuxThreadProofFixture.ExpectedCalls("Threads"));
        HostCheck.Equal(48U, LinuxThreadProofFixture.ExpectedCalls("Threads", child: true));
        ulong[] parentPath = [..parent.Calls.Take(9).Select(c => c.Rip), ..Enumerable.Range(0, 4).SelectMany(_ => parent.Calls.Skip(9).Take(10).Select(c => c.Rip)), ..parent.Calls.Skip(19).Select(c => c.Rip)];
        ulong[] childPath = Enumerable.Range(0, 4).SelectMany(_ => child.Calls.Select(c => c.Rip)).ToArray();
        HostCheck.True(parentPath.SequenceEqual(LinuxThreadProofFixture.ExpectedReturnRips("Threads")) && childPath.SequenceEqual(LinuxThreadProofFixture.ExpectedReturnRips("Threads", child: true)));
        HostCheck.Equal(parent.Calls[9].Rip, LinuxThreadProofFixture.CloneReturnRip("Threads"));
        HostCheck.Equal(0x400439UL, parent.Calls[9].Rip);
        HostCheck.True(!child.Calls.Any(c => c.Rip == parent.Calls[9].Rip)); // Child begins at a saved clone continuation, not a replayed syscall.
        HostCheck.Equal(parent.Calls[^1].Rip, LinuxThreadProofFixture.SuccessRip("Threads"));
        HostCheck.Equal(child.Calls[^1].Rip, LinuxThreadProofFixture.ChildExitRip("Threads"));
        HostCheck.Equal(0x400861UL, parent.Calls[^1].Rip);
        HostCheck.Equal(0x400c63UL, child.Calls[^1].Rip);
        Check(parent.Calls[2], 158, 0x1002, 0x402000, 0);
        Check(parent.Calls[3], 14, 2, 0x401020, 0, 8);
        Check(parent.Calls[4], 293, 0x401000, 0, 0);
        Check(parent.Calls[5], 56, 0x7d0f01, 0x406000, 0x401008, 0x40100c, 0x403000);
        Check(parent.Calls[6], 202, 0x401011, 128, 0);
        Check(parent.Calls[7], 14, 2, 0x401020, 0, 16);
        Check(parent.Calls[8], 158, 0x1002, 0x500000, 0);
        Check(parent.Calls[9], 56, 0x7d0f00, 0x406000, 0x401008, 0x40100c, 0x403000);
        HostCheck.True(parent.Calls[10].FdOffset == 0x1000 && parent.Calls[10].A1 == 0x401078 && parent.Calls[10].A2 == 4);
        Check(parent.Calls[11], 56, 0x7d0f00, 0x406000, 0x401008, 0x40100c, 0x403000);
        HostCheck.True(parent.Calls[21].FdOffset == 0x1000 && parent.Calls[22].FdOffset == 0x1004);
        HostCheck.True(child.Calls[8].FdOffset == 0x1004 && child.Calls[8].A1 == 0x401070 && child.Calls[8].A2 == 4);
        HostCheck.True(parent.Calls.Count(c => c.FdOffset >= 0) == 5 && child.Calls.Count(c => c.FdOffset >= 0) == 2);
        HostCheck.True(parent.Calls[16].FdOffset == 0x1000 && parent.Calls[16].A1 == 0x406000 && parent.Calls[16].A2 == 4096 && parent.Calls[18].FdOffset == 0x1000 && parent.Calls[18].A1 == 0x401078 && parent.Calls[18].A2 == 4 && child.Calls[10].FdOffset == 0x1004 && child.Calls[10].A1 == 0x402000 && child.Calls[10].A2 == 4100);
        Check(parent.Calls[13], 158, 0x1003, 0x401048, 0);
        Check(parent.Calls[14], 14, 2, 0, 0x401030, 8);
        Check(parent.Calls[15], 202, 0x401010, 129, 1);
        Check(parent.Calls[17], 202, 0x40100c, 0, 1);
        Check(parent.Calls[19], 202, 0x401010, 128, 0);
        Check(parent.Calls[20], 202, 0x401014, 128, 0, 0x401058);
        Check(child.Calls[2], 158, 0x1003, 0x401050, 0);
        Check(child.Calls[3], 14, 2, 0, 0x401038, 8);
        Check(child.Calls[4], 14, 2, 0x401028, 0x401040, 8);
        Check(child.Calls[5], 14, 2, 0, 0x401038, 8);
        Check(child.Calls[6], 158, 0x1002, 0x403040, 0);
        Check(child.Calls[7], 158, 0x1003, 0x401050, 0);
        Check(child.Calls[9], 202, 0x401010, 128, 0);
        Check(child.Calls[^1], 60, 0, 0, 0);
        Check(parent.Calls[^2], 1, 1, 0x400180, 15);
        Check(parent.Calls[^1], 231, 42, 0, 0);
    }

    [HostTest("m16-008 thread fixture independently binds child branch FS stack masks real timeout and terminal failure", HostLane.Fast, 2)]
    private static void ControlAndMetadata()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        var parent = Decode(elf, 0x200, 0x900);
        var child = Decode(elf, 0x900, 0xf00);
        var failure = Decode(elf, 0xf00, 0xf2a);
        var loop = parent.Branches.Single(b => b.Target < b.At);
        HostCheck.True(parent.Branches.Count(b => b.Target == 0x900) == 1 && parent.Branches.All(b => b.Target is 0x900 or 0xf00 || b == loop));
        HostCheck.True(elf.AsSpan(loop.Target - 6, 6).SequenceEqual(new byte[] { 0x41, 0xbd, 4, 0, 0, 0 }) && elf.AsSpan(loop.At - 3, 5).SequenceEqual(new byte[] { 0x49, 0xff, 0xcd, 0x0f, 0x85 }));
        HostCheck.True(elf[loop.Target] == 0xc7 && elf[loop.Target + 1] == 5 && U32(elf, loop.Target + 6) == 0 && loop.Target + 10 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(loop.Target + 2)) == 0x1010 && elf[loop.Target + 10] == 0xc7 && elf[loop.Target + 11] == 5 && U32(elf, loop.Target + 16) == 1 && loop.Target + 20 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(loop.Target + 12)) == 0x100c);
        HostCheck.True(loop.At > checked((int)(parent.Calls[18].Rip - 0x400000)) && loop.Target < checked((int)(parent.Calls[9].Rip - 0x400000)));
        HostCheck.True(child.Branches.All(b => b.Target == 0xf00));
        int cloneReturn = checked((int)(parent.Calls[9].Rip - 0x400000));
        HostCheck.True(elf.AsSpan(cloneReturn, 5).SequenceEqual(new byte[] { 0x48, 0x85, 0xc0, 0x0f, 0x84 }));
        HostCheck.Equal(0x900, cloneReturn + 9 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(cloneReturn + 5)));
        HostCheck.True(elf.AsSpan(0x900, 8).SequenceEqual(new byte[] { 0x48, 0x3d, 0, 0, 0, 0, 0x0f, 0x85 }));
        ReadOnlySpan<byte> p = elf.AsSpan(0x200, 0x700), c = elf.AsSpan(0x900, 0x600);
        HostCheck.True(p[..11].SequenceEqual(new byte[] { 0x31, 0xc0, 0x66, 0x8c, 0xc8, 0x48, 0x3d, 0x3b, 0, 0, 0 }));
        HostCheck.True(p.IndexOf(new byte[] { 0xbc, 0, 0x50, 0x40, 0 }) >= 0 && c.IndexOf(new byte[] { 0x48, 0x89, 0xe0, 0x48, 0x3d, 0, 0x60, 0x40, 0 }) >= 0);
        HostCheck.True(p.IndexOf(new byte[] { 0x41, 0x89, 0xc4 }) >= 0 && c.IndexOf(new byte[] { 0x41, 0xf7, 0xd4 }) >= 0);
        byte[] fs = [0x64, 0x48, 0x8b, 0x04, 0x25, 0, 0, 0, 0];
        HostCheck.True(parent.Instructions.Count(at => elf.AsSpan(at, Math.Min(9, elf.Length - at)).SequenceEqual(fs)) == 1 && child.Instructions.Count(at => elf.AsSpan(at, Math.Min(9, elf.Length - at)).SequenceEqual(fs)) == 2);
        foreach (int errno in new[]
        {
            -11,
            -14,
            -22,
            -110
        }

        )
        {
            byte[] compare = [0x48, 0x3d, 0, 0, 0, 0, 0x0f, 0x85];
            BinaryPrimitives.WriteInt32LittleEndian(compare.AsSpan(2), errno);
            HostCheck.True(p.IndexOf(compare) >= 0);
        }

        HostCheck.True(parent.Memory.Contains(0x1008) && parent.Memory.Contains(0x100c) && parent.Memory.Contains(0x4ff8) && child.Memory.Contains(0x1018) && child.Memory.Contains(0x1008) && child.Memory.Contains(0x101c) && child.Memory.Contains(0x5ff8));
        HostCheck.True(MemoryCheck(elf, parent, 0x100c, 1) && MemoryCheck(elf, parent, 0x100c, 0) && MemoryCheck(elf, parent, 0x1078, 0x45504950) && MemoryCheck(elf, parent, 0x4ff8, 0x13579bdf));
        HostCheck.True(MemoryCheck(elf, parent, 0x1048, 0x402000, true) && MemoryCheck(elf, parent, 0x1030, 0x200, true) && MemoryCheck(elf, child, 0x1050, 0x403000, true) && MemoryCheck(elf, child, 0x1050, 0x403040, true));
        HostCheck.True(MemoryCheck(elf, parent, 0x6000, 0x13579bdf, true) && MemoryCheck(elf, parent, 0x6ff8, 0x5a5a5a5a, true) && MemoryCheck(elf, parent, 0x1078, 0x2468ace0));
        HostCheck.True(MemoryCheck(elf, child, 0x1038, 0x200, true) && MemoryCheck(elf, child, 0x1040, 0x200, true) && MemoryCheck(elf, child, 0x1038, 0x800, true) && MemoryCheck(elf, child, 0x1010, 1) && MemoryCheck(elf, child, 0x5ff8, 0x2468ace0));
        HostCheck.True(FsCheck(p, 0x13579bdf) && FsCheck(c, 0x2468ace0) && FsCheck(c, 0x31415926));
        HostCheck.Equal(1U, U32(elf, 0x100c));
        HostCheck.Equal(0UL, U64(elf, 0x1010));
        HostCheck.Equal(0x200UL, U64(elf, 0x1020));
        HostCheck.Equal(0x800UL, U64(elf, 0x1028));
        HostCheck.Equal(0UL, U64(elf, 0x1058));
        HostCheck.Equal(1_000_000UL, U64(elf, 0x1060));
        HostCheck.True(elf.AsSpan(0x1070, 4).SequenceEqual("PIPE"u8));
        HostCheck.Equal(0x13579bdfUL, U64(elf, 0x2000));
        HostCheck.Equal(0x2468ace0UL, U64(elf, 0x3000));
        HostCheck.Equal(0x31415926UL, U64(elf, 0x3040));
        HostCheck.Equal(0x5a5a5a5aUL, U64(elf, 0x2ff8));
        HostCheck.True(elf.AsSpan(0x4000, 12288).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(elf.AsSpan(0x180, 15).SequenceEqual("M16_THREADS_OK!"u8));
        HostCheck.Equal(1, failure.Calls.Count);
        Check(failure.Calls[0], 231, 99, 0, 0);
        HostCheck.Equal(0x400f29UL, failure.Calls[0].Rip);
        HostCheck.Equal(0x400f29UL, LinuxThreadProofFixture.FailureExitRip);
        HostCheck.True(parent.Calls.Concat(child.Calls).All(call => call.Rip != failure.Calls[0].Rip));
        HostCheck.Equal((byte)0xf4, elf[0xf29]);
    }

    [HostTest("m16-008 thread fixture exposes fresh exact inventories and rejects every unsupported variant", HostLane.Fast, 3)]
    private static void FreshAndRejected()
    {
        HostCheck.True(LinuxThreadProofFixture.Variants.SequenceEqual(new[] { "Threads", "PipeReadClose", "PipeWriteClose", "PipeShutdown" }));
        byte[] changed = LinuxThreadProofFixture.CreateElf("Threads");
        changed[0x180] ^= 1;
        HostCheck.True(LinuxThreadProofFixture.CreateElf("Threads").AsSpan(0x180, 15).SequenceEqual("M16_THREADS_OK!"u8));
        HostCheck.True(!ReferenceEquals(LinuxThreadProofFixture.ExpectedReturnRips("Threads"), LinuxThreadProofFixture.ExpectedReturnRips("Threads")));
        foreach (string variant in new[]
        {
            "",
            "threads",
            "Threads ",
            "Pipes",
            "Hello"
        }

        )
        {
            Reject(() => LinuxThreadProofFixture.CreateElf(variant));
            Reject(() => LinuxThreadProofFixture.CreatePackage(variant));
            Reject(() => LinuxThreadProofFixture.ExpectedReturnRips(variant));
            Reject(() => LinuxThreadProofFixture.ExpectedReturnRips(variant, child: true));
            Reject(() => LinuxThreadProofFixture.ExpectedCalls(variant));
            Reject(() => LinuxThreadProofFixture.CloneReturnRip(variant));
            Reject(() => LinuxThreadProofFixture.SuccessRip(variant));
            Reject(() => LinuxThreadProofFixture.ChildExitRip(variant));
            Reject(() => LinuxThreadProofFixture.Witness(variant));
        }
    }

    [HostTest("m16-008 peer-close and shutdown ELF controls bind actual pipe calls opposite endpoint close and no resumed shutdown child")]
    private static void PipeControlInstructions()
    {
        HostCheck.Equal("1DD7D9828914DB92A8C0FAB494C6841ED3CD02217F2634F4A1F44A813ABA374E", Convert.ToHexString(SHA256.HashData(LinuxThreadProofFixture.CreatePackage("Threads"))));
        foreach (string variant in new[]
        {
            "PipeReadClose",
            "PipeWriteClose",
            "PipeShutdown"
        }

        )
        {
            bool write = variant == "PipeWriteClose", shutdown = variant == "PipeShutdown";
            byte[] elf = LinuxThreadProofFixture.CreateElf(variant);
            var p = Decode(elf, 0x200, 0x900);
            var c = Decode(elf, 0x900, 0xf00);
            uint[] numbers = write ? [39, 186, 158, 14, 293, 0, 1, 56, 1, 3, 1, 231] : shutdown ? [39, 186, 158, 14, 293, 0, 56, 202, 1, 231] : [39, 186, 158, 14, 293, 1, 56, 0, 3, 1, 231];
            HostCheck.True(p.Calls.Select(x => x.Number).SequenceEqual(numbers) && c.Calls.Select(x => x.Number).SequenceEqual(shutdown ? new uint[] { 0 } : new uint[] { 3, 60 }));
            HostCheck.True(p.Calls.Select(x => x.Rip).SequenceEqual(LinuxThreadProofFixture.ExpectedReturnRips(variant)) && c.Calls.Select(x => x.Rip).SequenceEqual(LinuxThreadProofFixture.ExpectedReturnRips(variant, child: true)));
            int clone = write ? 7 : 6, block = clone + 1;
            Check(p.Calls[clone], 56, 0x7d0f00, 0x406000, 0x401008, 0x40100c, 0x403000);
            HostCheck.Equal(p.Calls[clone].Rip, LinuxThreadProofFixture.CloneReturnRip(variant));
            HostCheck.True(p.Calls[5].FdOffset == (write || shutdown ? 0x1000 : 0x1004) && p.Calls[5].A1 == 0 && p.Calls[5].A2 == 0);
            if (write)
                HostCheck.True(p.Calls[6].FdOffset == 0x1004 && p.Calls[6].A1 == 0x402000 && p.Calls[6].A2 == 4096);
            if (shutdown)
                Check(p.Calls[block], 202, 0x401010, 128, 0, 0x401058);
            else
                HostCheck.True(p.Calls[block].FdOffset == (write ? 0x1004 : 0x1000) && p.Calls[block].A1 == 0x401078 && p.Calls[block].A2 == 4);
            HostCheck.True(c.Calls[0].FdOffset == (write || shutdown ? 0x1000 : 0x1004));
            if (shutdown)
            {
                HostCheck.True(c.Calls[0].A1 == 0x401078 && c.Calls[0].A2 == 4 && elf[(int)(c.Calls[0].Rip - 0x400000)] == 0xe9);
                HostCheck.Equal(0UL, LinuxThreadProofFixture.ChildExitRip(variant));
            }
            else
            {
                HostCheck.True(MemoryCheck(elf, p, 0x100c, 0));
                Check(c.Calls[1], 60, 0, 0, 0);
                HostCheck.Equal(c.Calls[1].Rip, LinuxThreadProofFixture.ChildExitRip(variant));
                HostCheck.True(p.Calls[block + 1].Number == 3 && p.Calls[block + 1].FdOffset == p.Calls[block].FdOffset);
            }

            HostCheck.True(FsCheck(elf.AsSpan(0x200, 0x700), 0x13579bdf) && FsCheck(elf.AsSpan(0x900, 0x600), 0x2468ace0));
            int returned = (int)(p.Calls[block].Rip - 0x400000);
            HostCheck.True(elf[returned] == 0x48 && elf[returned + 1] == 0x3d && BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(returned + 2)) == (shutdown ? -110 : write ? -32 : 0));
            Check(p.Calls[^2], 1, 1, 0x400180, 15);
            Check(p.Calls[^1], 231, 42, 0, 0);
            HostCheck.True(p.Branches.All(x => x.Target is 0x900 or 0xf00) && c.Branches.All(x => x.Target == 0xf00));
        }
    }

    [HostTest("m16-008 canonical pipe packages preflight all variants without trusting manifest labels or overwriting output")]
    private static void PipeControlPackages()
    {
        string directory = Directory.CreateTempSubdirectory("clros-thread-controls-").FullName;
        try
        {
            foreach (string variant in LinuxThreadProofFixture.Variants)
            {
                string path = Path.Combine(directory, variant + ".pkg");
                LinuxThreadProofFixture.WritePackage(path, variant);
                byte[] package = File.ReadAllBytes(path);
                HostCheck.Equal(variant, LinuxThreadProofFixture.PackageVariant(package));
                HostCheck.True(LinuxElfPackage.Validate(package).Succeeded);
                using (FileStream input = File.OpenRead(path))
                {
                    LinuxThreadProofFixture.RequirePackage(input);
                    HostCheck.Equal(0L, input.Position);
                }

                bool rejected = false;
                try
                {
                    LinuxThreadProofFixture.WritePackage(path, "Threads");
                }
                catch (IOException)
                {
                    rejected = true;
                }

                HostCheck.True(rejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(package));
                package[64] ^= 1;
                rejected = false;
                try
                {
                    _ = LinuxThreadProofFixture.PackageVariant(package);
                }
                catch (ToolFailure)
                {
                    rejected = true;
                }

                HostCheck.True(rejected);
                File.Delete(path);
            }
        }
        finally
        {
            foreach (string path in Directory.EnumerateFiles(directory))
                File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure e)
        {
            rejected = e.Message == "m16_008_fixture_variant";
        }

        HostCheck.True(rejected);
    }

    private static void Check(Call call, uint number, ulong a0, ulong a1, ulong a2, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
    {
        HostCheck.True(call.Number == number && call.FdOffset == -1 && call.A0 == a0 && call.A1 == a1 && call.A2 == a2 && call.A3 == a3 && call.A4 == a4 && call.A5 == a5);
    }

    private static bool MemoryCheck(byte[] elf, Decoded decoded, int target, uint value, bool qword = false)
    {
        foreach (int at in decoded.Instructions)
        {
            int size = qword ? 7 : 6;
            if (qword ? elf[at] != 0x48 || elf[at + 1] != 0x8b || elf[at + 2] != 5 : elf[at] != 0x8b || elf[at + 1] != 5)
                continue;
            if (at + size + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + size - 4)) == target && elf[at + size] == 0x48 && elf[at + size + 1] == 0x3d && U32(elf, at + size + 2) == value)
                return true;
        }

        return false;
    }

    private static bool FsCheck(ReadOnlySpan<byte> bytes, uint value)
    {
        byte[] expected = [0x64, 0x48, 0x8b, 4, 0x25, 0, 0, 0, 0, 0x48, 0x3d, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(11), value);
        return bytes.IndexOf(expected) >= 0;
    }

    // Independent instruction-boundary decoder: immediates cannot invent calls,
    // branch targets or returned-fd loads. It never runs the emitted program.
    private static Decoded Decode(byte[] elf, int start, int end)
    {
        List<Call> calls = [];
        List<(int, int)> branches = [];
        HashSet<int> memory = [];
        List<int> instructions = [];
        ulong[] registers = new ulong[16];
        int fd = -1;
        for (int at = start; at < end && elf[at] != 0xcc;)
        {
            int size = Size(elf, at);
            instructions.Add(at);
            HostCheck.True(at + size <= end);
            if (elf[at] is >= 0xb8 and <= 0xbf)
            {
                int register = elf[at] - 0xb8;
                registers[register] = U32(elf, at + 1);
                if (register == 7)
                    fd = -1;
            }
            else if (elf[at] == 0x41 && elf[at + 1] is >= 0xb8 and <= 0xbf)
                registers[8 + elf[at + 1] - 0xb8] = U32(elf, at + 2);
            else if (elf[at] == 0x0f && elf[at + 1] == 5)
                calls.Add(new((uint)registers[0], registers[7], registers[6], registers[2], registers[10], registers[8], registers[9], fd, 0x400000UL + (uint)(at + 2)));
            if (elf[at] == 0xe9 || elf[at] == 0x0f && elf[at + 1] is 0x84 or 0x85 or 0x8e)
                branches.Add((at, at + size + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + size - 4))));
            bool rip = elf[at] is 0x8b or 0x89 or 0x3b or 0xc7 || elf[at] == 0x48 && elf[at + 1] == 0x8b;
            if (rip)
            {
                int tail = elf[at] == 0xc7 ? 4 : 0;
                int target = at + size + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + size - tail - 4));
                memory.Add(target);
                if (elf[at] == 0x8b && elf[at + 1] == 0x3d)
                    fd = target;
            }

            at += size;
        }

        return new(calls, branches, memory, instructions);
    }

    private static int Size(byte[] b, int at)
    {
        byte op = b[at];
        if (op is >= 0xb8 and <= 0xbf)
            return 5;
        if (op == 0x41 && b[at + 1] is >= 0xb8 and <= 0xbf)
            return 6;
        if (op == 0x41 && b[at + 1] is 0x89 or 0xf7)
            return 3;
        if (op == 0x49 && b[at + 1] == 0xff && b[at + 2] == 0xcd)
            return 3;
        if (op == 0x48 && b[at + 1] == 0x3d)
            return 6;
        if (op == 0x48 && b[at + 1] == 0x8b && b[at + 2] == 5)
            return 7;
        if (op == 0x48 && b[at + 1] is 0x85 or 0x89 || op == 0x4c && b[at + 1] == 0x39)
            return 3;
        if (op is 0x8b or 0x89 or 0x3b)
            return 6;
        if (op == 0xc7 && b[at + 1] == 5)
            return 10;
        if (op == 0x64 && b.AsSpan(at, 9).SequenceEqual(new byte[] { 0x64, 0x48, 0x8b, 4, 0x25, 0, 0, 0, 0 }))
            return 9;
        if (op == 0x0f && b[at + 1] == 5)
            return 2;
        if (op == 0x0f && b[at + 1] is 0x84 or 0x85 or 0x8e)
            return 6;
        if (op == 0x31 && b[at + 1] == 0xc0)
            return 2;
        if (op == 0x66 && b[at + 1] == 0x8c && b[at + 2] == 0xc8)
            return 3;
        if (op == 0xe9)
            return 5;
        if (op is 0xfc or 0xf4)
            return 1;
        throw new InvalidOperationException("unexpected thread fixture opcode at " + at);
    }
}
