using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class LinuxDiskFileProofFixtureTests
{
    [HostTest("disk mechanical ELF admits separate executable and writable bounds", HostLane.Fast, 0)]
    private static void LoadGeometry()
    {
        byte[] package = LinuxDiskFileProofFixture.CreatePackage();
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded && view.DataFileCount == 0);
        HostCheck.Equal((ulong)LinuxDiskFileProofFixture.ElfBytes, view.PayloadBytes);
        HostCheck.True(package.AsSpan().SequenceEqual(LinuxDiskFileProofFixture.CreatePackage()));
        fixed (byte* bytes = package)
        {
            HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset,
                view.PayloadBytes, view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
            HostCheck.True(plan.LoadCount == 2 && plan.Entry == 0x400200 && plan.ImageBytes == 0x7000);
        }
        var elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes);
        HostCheck.Equal(5U, BinaryPrimitives.ReadUInt32LittleEndian(elf[68..]));
        HostCheck.Equal(6U, BinaryPrimitives.ReadUInt32LittleEndian(elf[124..]));
        HostCheck.Equal(4096UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[96..]));
        HostCheck.Equal(6UL * 4096, BinaryPrimitives.ReadUInt64LittleEndian(elf[152..]));
    }

    [HostTest("disk mechanical package admission rejects changed executable and policy", HostLane.Fast, 1)]
    private static void CanonicalEnvelope()
    {
        byte[] package = LinuxDiskFileProofFixture.CreatePackage();
        LinuxDiskFileProofFixture.RequirePackage(package);
        byte[] changed = (byte[])package.Clone(); changed[^1] ^= 1;
        Reject(changed);
        Reject(package.AsSpan(0, package.Length - 1));
        Reject(LinuxThreadProofFixture.CreatePackage("Threads"));
        Reject([]);

        static void Reject(ReadOnlySpan<byte> input)
        {
            bool rejected = false;
            try { LinuxDiskFileProofFixture.RequirePackage(input); }
            catch (ToolFailure failure) when (failure.Reason == "m16_025_fixture_package_identity") { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("disk mechanical syscall continuations exclude assertion-failure exit", HostLane.Fast, 2)]
    private static void Continuations()
    {
        byte[] elf = LinuxDiskFileProofFixture.CreateElf();
        var observed = LinuxDiskFileProofFixture.ExpectedReturnRips();
        HostCheck.True(observed.Count is > 40 and <= 128 && observed.Distinct().Count() == observed.Count);
        HostCheck.Equal((uint)observed.Count, LinuxDiskFileProofFixture.ExpectedCalls);
        foreach (ulong rip in observed)
        {
            int offset = checked((int)(rip - LinuxDiskFileProofFixture.ImageBase));
            HostCheck.True(offset > LinuxDiskFileProofFixture.EntryOffset && offset < LinuxDiskFileProofFixture.FailureOffset);
            HostCheck.True(elf[offset - 2] == 0x0f && elf[offset - 1] == 5);
        }
        HostCheck.Equal(observed[^1], LinuxDiskFileProofFixture.SuccessRip);
        HostCheck.True(!observed.Contains(LinuxDiskFileProofFixture.FailureExitRip));
        HostCheck.True(elf.AsSpan().IndexOf("/app/wwwroot/../../private/sentinel.txt\0"u8) >= 4096);
        HostCheck.True(elf.AsSpan().IndexOf("/app/wwwroot/fixture/fragmented.bin\0"u8) >= 4096);
    }
}
