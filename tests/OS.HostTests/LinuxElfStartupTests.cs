using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;

internal static unsafe class LinuxElfStartupTests
{
    private const ulong Start = 0x6ffff000;
    private const uint Stack = 4096, Hwcap = 0x078bfbff;
    private static byte[] Manifest(string args = "\"one two\" a\"b c\"d \"\"", string env = "env.NAME=value=with=equals\nenv.EMPTY=\n") => Encoding.ASCII.GetBytes(
        "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/example.elf\nargs=" + args + "\n" + env +
        "digest=" + new string('0', 64) + "\nmemory=65536\nstack=4096\nthreads=1\ndescriptors=16\nlisten=127.0.0.1:8080\n");

    [HostTest("m16-006 Linux initial stack high pointers retain full argc argv envp auxv values", HostLane.Fast, 5)]
    private static void HighStack()
    {
        byte[] manifest = Manifest(), elf = Elf(), entropy = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        byte* stack = (byte*)NativeMemory.AlignedAlloc(Stack, 4096);
        const ulong high = 0x16ffff000;
        try
        {
            fixed (byte* m = manifest, e = elf, random = entropy)
            {
                HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(m, (uint)manifest.Length, e, (uint)elf.Length,
                    random, 32, stack, Stack, high, Hwcap, out var plan));
                HostCheck.True(plan.StackPointer >= high && plan.StackPointer < high + Stack && plan.RandomAddress >= high);
                ulong* frame = (ulong*)(stack + plan.StackPointer - high);
                HostCheck.Equal(4UL, frame[0]); HostCheck.True(frame[1] >= high && frame[1] < high + Stack);
                HostCheck.True(LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length,
                    random, 32, stack, Stack, high, Hwcap, plan));
                HostCheck.Equal(LinuxElfStartupStatus.InvalidStorage, LinuxElfStartup.Build(m, (uint)manifest.Length, e, (uint)elf.Length,
                    random, 32, stack, Stack, LinuxSyscallGateway.UserEnd, Hwcap, out _));
            }
        }
        finally { NativeMemory.AlignedFree(stack); }
    }
    private static byte[] Elf()
    {
        byte[] b = new byte[8192];
        W32(b, 0, 0x464c457f); b[4] = 2; b[5] = b[6] = 1;
        W16(b, 16, 2); W16(b, 18, 62); W32(b, 20, 1); W64(b, 24, 0x401010);
        W64(b, 32, 64); W16(b, 52, 64); W16(b, 54, 56); W16(b, 56, 2);
        Row(b, 0, 4, 0, 0x400000, 512, 512);
        Row(b, 1, 5, 4096, 0x401000, 32, 4096);
        b[4112] = 0xc3; return b;
    }
    private static void W16(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), v);
    private static void W32(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
    private static void W64(byte[] b, int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(o), v);
    private static void Row(byte[] b, int index, uint flags, ulong file, ulong address, ulong bytes, ulong memory)
    {
        int o = 64 + index * 56; W32(b, o, 1); W32(b, o + 4, flags); W64(b, o + 8, file); W64(b, o + 16, address);
        W64(b, o + 32, bytes); W64(b, o + 40, memory); W64(b, o + 48, 4096);
    }
    private static string Text(byte* stack, ulong address) => Marshal.PtrToStringUTF8((nint)(stack + address - Start))!;

    [HostTest("m16-004 independent transcript binds input ELF stack geometry and restored owned root", HostLane.Fast, 2)]
    private static void Transcript()
    {
        byte[] elf = Elf();
        byte[] manifest = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(Manifest()).Replace(new string('0', 64),
            Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant(), StringComparison.Ordinal));
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        W64(package, 0, 0x3130464c45524c43); W32(package, 8, 1); W32(package, 12, 1); W32(package, 16, 64);
        W32(package, 20, (uint)manifest.Length); W64(package, 24, (ulong)elf.Length);
        manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        string[] tail = ["TEST_START m16-004",
            "M16_004_IMAGE base=4194304 bytes=8192 loads=2 entry=4198416 phdr=4194368 tlsFile=0 tlsMemory=0",
            $"M16_004_STACK start={Start} bytes=4096 sp={Start + 3648} argc=4 envc=2 random={Start + 4000} entropyBytes=32 seedCleared=32",
            "M16_004_TABLES root=16777216 used=9 leaves=160 ranges=6",
            "M16_004_READBACK old=33554432 restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false",
            "CLROS_M16_004_READY", "TEST_PASS m16-004"];
        string[] rows = [.. Enumerable.Repeat("parent checked separately", QemuContracts.M03OwnedSubsystemMarkers.Count + 7), .. tail];
        HostCheck.Equal(rows.Length - 7, QemuTranscripts.ValidateLinuxStartupTail(rows, package).Length);
        foreach (var mutation in new (int Index, string Text)[]
        {
            (1, tail[1].Replace("phdr=4194368", "phdr=4194369", StringComparison.Ordinal)),
            (1, tail[1].Replace("tlsMemory=0", "tlsMemory=16", StringComparison.Ordinal)),
            (2, tail[2].Replace("argc=4", "argc=3", StringComparison.Ordinal)),
            (2, tail[2].Replace("seedCleared=32", "seedCleared=0", StringComparison.Ordinal)),
            (3, tail[3].Replace("used=9", "used=129", StringComparison.Ordinal)),
            (3, tail[3].Replace("leaves=160", "leaves=10", StringComparison.Ordinal)),
            (4, tail[4].Replace("old=33554432", "old=16777216", StringComparison.Ordinal)),
            (4, tail[4].Replace("guard=absent", "guard=present", StringComparison.Ordinal)),
            (4, tail[4].Replace("entry=false", "entry=true", StringComparison.Ordinal)),
            (6, "TEST_PASS m16-003")
        })
        {
            string[] bad = rows.ToArray(); bad[bad.Length - 7 + mutation.Index] = mutation.Text;
            bool rejected = false; try { _ = QemuTranscripts.ValidateLinuxStartupTail(bad, package); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("m16-004 QEMU routing defaults genuine entropy and orders startup before service shutdown", HostLane.Fast, 3)]
    private static void Routing()
    {
        var options = QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16004Probe", "-RequireMarker", "CLROS_M16_004_READY", "-DryRun");
        var plan = QemuProfiles.Create(options);
        HostCheck.True(options.FirmwareRngSource == "virtio" && plan.IsEnabled("EnableM16002Probe") && !plan.IsEnabled("EnableM16003Probe"));
        HostCheck.True(plan.Ordered.Contains("TEST_PASS m16-004") && plan.Unique.Contains("M16_004_READBACK"));
        var both = QemuProfiles.Create(QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16004Probe", "-EnableM16003Probe", "-DryRun"));
        HostCheck.True(both.Ordered.ToList().IndexOf("TEST_PASS m16-004") < both.Ordered.ToList().IndexOf("TEST_START m16-003"));
        foreach (string[] extra in new string[][]
        { ["-FirmwareRngSource", "arbitrary"], ["-LinuxElfImageVariant", "DigestFault"], ["-RequireMarker", "CLROS_M16_002_READY"] })
        {
            bool rejected = false; try { _ = QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-EnableM16004Probe", "-DryRun", .. extra])); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        var missing = QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16004Probe", "-FirmwareRngSource", "missing", "-DryRun");
        _ = QemuProfiles.Create(missing); HostCheck.Equal("missing", missing.FirmwareRngSource);
    }

    [HostTest("m16-004 independent argc argv envp auxv user addresses real PHDR entropy and 16-byte alignment", HostLane.Fast, 0)]
    private static void StackLayout()
    {
        byte[] manifest = Manifest(), elf = Elf(), entropy = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        byte* stack = (byte*)NativeMemory.AlignedAlloc(Stack, 4096);
        try
        {
            fixed (byte* m = manifest, e = elf, random = entropy)
            {
                new Span<byte>(stack, (int)Stack).Fill(0xa5);
                long before = GC.GetAllocatedBytesForCurrentThread();
                var status = LinuxElfStartup.Build(m, (uint)manifest.Length, e, (uint)elf.Length, random, 32, stack, Stack, Start, Hwcap, out var plan);
                long allocations = GC.GetAllocatedBytesForCurrentThread() - before;
                HostCheck.Equal(LinuxElfStartupStatus.Ready, status);
                HostCheck.True(plan.Succeeded && plan.ArgumentCount == 4 && plan.EnvironmentCount == 2 &&
                    plan.StackPointer % 16 == 0 && plan.RandomAddress % 16 == 0 && allocations == 0);
                ulong* frame = (ulong*)(stack + plan.StackPointer - Start);
                HostCheck.Equal(4UL, frame[0]);
                string[] expected = ["apps/example.elf", "one two", "ab cd", ""];
                for (int i = 0; i < expected.Length; i++) HostCheck.Equal(expected[i], Text(stack, frame[1 + i]));
                HostCheck.Equal(0UL, frame[5]);
                HostCheck.Equal("NAME=value=with=equals", Text(stack, frame[6]));
                HostCheck.Equal("EMPTY=", Text(stack, frame[7])); HostCheck.Equal(0UL, frame[8]);
                var aux = new Dictionary<ulong, ulong>();
                for (int i = 9; frame[i] != 0; i += 2) aux.Add(frame[i], frame[i + 1]);
                HostCheck.Equal(16, aux.Count);
                HostCheck.True(aux[3] == 0x400040 && aux[4] == 56 && aux[5] == 2 && aux[6] == 4096 && aux[7] == 0 && aux[8] == 0 && aux[9] == 0x401010);
                HostCheck.True(aux[11] == 0 && aux[12] == 0 && aux[13] == 0 && aux[14] == 0 && aux[23] == 0 && aux[16] == Hwcap && !aux.ContainsKey(33));
                HostCheck.Equal("x86_64", Text(stack, aux[15]));
                HostCheck.Equal("apps/example.elf", Text(stack, aux[31]));
                HostCheck.True(new ReadOnlySpan<byte>(stack + aux[25] - Start, 16).SequenceEqual(entropy.AsSpan(0, 16)));
                HostCheck.True(LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length, random, 32, stack, Stack, Start, Hwcap, plan));
                foreach (uint offset in new uint[] { 0, (uint)(plan.StackPointer - Start), (uint)(plan.RandomAddress - Start), Stack - 1 })
                {
                    stack[offset] ^= 1;
                    HostCheck.True(!LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length, random, 32, stack, Stack, Start, Hwcap, plan));
                    stack[offset] ^= 1;
                }
                var forged = plan; forged.StackPointer += 16;
                HostCheck.True(!LinuxElfStartup.ValidateReadBack(m, (uint)manifest.Length, e, (uint)elf.Length, random, 32, stack, Stack, Start, Hwcap, forged));
            }
        }
        finally { NativeMemory.AlignedFree(stack); }
    }

    [HostTest("m16-004 invalid entropy stale ELF short alias and exhausted stack reject before writes", HostLane.Fast, 1)]
    private static void RejectBeforeWrite()
    {
        byte[] elf = Elf(), entropy = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        byte* stack = (byte*)NativeMemory.AlignedAlloc(Stack, 4096);
        try
        {
            for (int variant = 0; variant < 8; variant++)
            {
                byte[] manifest = variant == 7 ? Manifest(string.Join(' ', Enumerable.Repeat("a", 128)),
                    string.Concat(Enumerable.Range(0, 16).Select(i => "env.K" + i + "=" + new string('v', 180) + "\n"))) : Manifest();
                byte[] seed = entropy.ToArray();
                if (variant == 0) Array.Clear(seed);
                if (variant == 1) Array.Clear(seed, 0, 16);
                new Span<byte>(stack, (int)Stack).Fill(0xa5);
                fixed (byte* m = manifest, e = elf, random = seed)
                {
                    var status = LinuxElfStartup.Build(m, (uint)manifest.Length, e, variant == 2 ? 63U : (uint)elf.Length,
                        variant == 3 ? stack + 16 : random, variant == 4 ? 16U : 32U,
                        variant == 5 ? stack + 1 : stack, variant == 6 ? Stack - 16 : Stack, Start, Hwcap, out var plan);
                    HostCheck.True(status != LinuxElfStartupStatus.Ready && !plan.Succeeded && plan.StackPointer == 0);
                    if (variant == 7) HostCheck.Equal(LinuxElfStartupStatus.InsufficientStack, status);
                    HostCheck.True(new ReadOnlySpan<byte>(stack, (int)Stack).IndexOfAnyExcept((byte)0xa5) == -1);
                }
            }
        }
        finally { NativeMemory.AlignedFree(stack); }
    }
}
