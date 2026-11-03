using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class LinuxSyscallProofFixtureTests
{
    [HostTest("m16-005 mechanical fixtures use genuine static ELF package and mapped PHDR W^X geometry", HostLane.Fast, 0)]
    private static void PackageGeometry()
    {
        foreach (string variant in LinuxSyscallProofFixture.Variants)
        {
            byte[] package = LinuxSyscallProofFixture.CreatePackage(variant);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded);
            fixed (byte* bytes = package)
            {
                HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes,
                    view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
                HostCheck.Equal(2U, plan.LoadCount);
                HostCheck.Equal(12288UL, plan.ImageBytes);
                HostCheck.Equal(0x400040UL, plan.ProgramHeaders);
                HostCheck.Equal(variant == "ReturnToData" ? 0x400ffeUL : 0x400200UL, plan.Entry);
                HostCheck.Equal(5UL, plan.Loads[4]);
                HostCheck.Equal(6UL, plan.Loads[9]);
                HostCheck.Equal(0UL, plan.TlsMemoryBytes);
            }
            HostCheck.True(package.AsSpan().SequenceEqual(LinuxSyscallProofFixture.CreatePackage(variant)));
        }
    }

    [HostTest("m16-005 independent fixture opcodes witness actual selectors syscalls cross-page copy and required SIMD", HostLane.Fast, 1)]
    private static void OpcodeControls()
    {
        byte[] elf = LinuxSyscallProofFixture.CreateElf("WriteExit");
        ReadOnlySpan<byte> code = elf.AsSpan(0x200, 0xd00);
        HostCheck.True(code[..11].SequenceEqual(new byte[] { 0x31, 0xc0, 0x66, 0x8c, 0xc8, 0x48, 0x3d, 0x3b, 0, 0, 0 }));
        HostCheck.True(code.IndexOf(new byte[] { 0x66, 0x8c, 0xd0 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0x68, 0xd7, 0x0c, 0x20, 0, 0x9d, 0x0f, 0x05, 0x9c, 0x8f, 0x05 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0xf3, 0x0f, 0x6f, 0x05 }) >= 0 && code.IndexOf(new byte[] { 0xf3, 0x0f, 0x6f, 0x35 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0x64, 0x48, 0xa1 }) >= 0 && code.IndexOf(new byte[] { 0x65, 0x48, 0xa1 }) >= 0);
        byte[] witness = System.Text.Encoding.ASCII.GetBytes(LinuxSyscallProofFixture.Witness);
        int at = (int)(LinuxSyscallProofFixture.BufferAddress - LinuxSyscallProofFixture.ImageBase);
        HostCheck.True(at / 4096 != (at + witness.Length - 1) / 4096 && elf.AsSpan(at, witness.Length).SequenceEqual(witness));
        HostCheck.Equal(0x3f80U, BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(0x1120)));
        HostCheck.True(LinuxSyscallProofFixture.CreateElf("KernelReadFault").AsSpan(0x200, 100).IndexOf(new byte[] { 0x0f, 1, 5 }) >= 0);
        HostCheck.True(LinuxSyscallProofFixture.CreateElf("PrivilegedFault").AsSpan(0x200, 100).IndexOf((byte)0xf4) >= 0);
        HostCheck.True(LinuxSyscallProofFixture.CreateElf("InvalidRsp").AsSpan(0x200, 100).IndexOf(new byte[] { 0x48, 0xbc, 0, 0x10, 0, 0, 0, 0x80, 0xff, 0xff }) >= 0);
        HostCheck.True(LinuxSyscallProofFixture.CreateElf("ReturnToData").AsSpan(4094, 2).SequenceEqual(new byte[] { 0x0f, 5 }));
    }
    [HostTest("m16-005 negative receipts bind exact fault opcodes or hostile returns package and restoration", HostLane.Fast, 2)]
    private static void NegativeReceipts()
    {
        int parentCount = QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17;
        string[] parents = Enumerable.Range(0, parentCount).Select(i => "parent " + i).ToArray();
        foreach (string variant in LinuxSyscallProofFixture.Variants.Skip(1))
        {
            byte[] elf = LinuxSyscallProofFixture.CreateElf(variant), package = LinuxSyscallProofFixture.CreatePackage(variant);
            bool fault = variant is "PrivilegedFault" or "KernelReadFault";
            ulong rip = variant == "ReturnToData" ? 0x401000UL : LinuxSyscallProofFixture.ImageBase + 0x200UL +
                (uint)elf.AsSpan(0x200, 0x100).IndexOf(variant switch
                {
                    "PrivilegedFault" => new byte[] { 0xf4 },
                    "KernelReadFault" => new byte[] { 0x48, 0x8b, 0 },
                    _ => new byte[] { 0x0f, 5 }
                });
            if (variant == "InvalidRsp")
                rip += 2;
            string receipt = fault
                ? $"M16_005_FAULT vector={(variant == "PrivilegedFault" ? 13 : 14)} error={(variant == "PrivilegedFault" ? 0 : 5)} rip={rip} rsp=1879047808 cr2={(variant == "PrivilegedFault" ? 0 : 2098688)} cs=59 ss=51 disposition=terminate"
                : $"M16_005_CALL nr={(variant == "InvalidRsp" ? 999 : 0)} a0=0 a1=0 a2=0 a3=0 a4=0 a5=0 rip={rip} rsp={(variant == "InvalidRsp" ? 0xffff800000001000UL : 1879047808)} flags=70 valueBits=0 action=2 status=5";
            string[] tail = ["TEST_START m16-005",
                "M16_005_ARMED root=3145728 cs=59 ss=51 kernelStack=2179072 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
                receipt, "M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
                "TEST_FAIL m16-005 user-boundary-terminated status=" + (fault ? 1 : 5)];
            HostCheck.True(QemuTranscripts.ValidateLinuxSyscallNegativeTail([.. parents, .. tail], package, variant).SequenceEqual(parents));
            for (int mutation = 0; mutation < 8; mutation++)
            {
                string[] bad = (string[])tail.Clone();
                byte[] changedPackage = (byte[])package.Clone();
                switch (mutation)
                {
                    case 0:
                        bad[4] += "0";
                        break;
                    case 1:
                        bad[3] = bad[3].Replace("fp=true", "fp=false");
                        break;
                    case 2:
                        bad[2] = fault ? bad[2].Replace("cs=59", "cs=8") : bad[2].Replace("action=2", "action=1");
                        break;
                    case 3:
                        bad = [.. bad, "CLROS_M16_005_READY"];
                        break;
                    case 4:
                        changedPackage[^1] ^= 1;
                        break;
                    case 5:
                        bad[2] = bad[2].Replace("rip=" + rip, "rip=" + (rip + 1));
                        break;
                    case 6:
                        bad[1] = bad[1].Replace("kernelFsGs=owned-null", "kernelFsGs=firmware");
                        break;
                    default:
                        bad = [.. bad[..3], bad[2], .. bad[3..]];
                        break;
                }
                bool rejected = false;
                try
                {
                    _ = QemuTranscripts.ValidateLinuxSyscallNegativeTail([.. parents, .. bad], changedPackage, variant);
                }
                catch (ToolFailure) { rejected = true; }
                HostCheck.True(rejected, variant + " negative mutation " + mutation);
            }
        }
    }
}
