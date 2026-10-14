using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;

internal static class LinuxSignalFaultProofFixtureTests
{
    [HostTest("m16-009 fault additions preserve the four original canonical signal package identities")]
    private static void OriginalPackages()
    {
        (string Variant,string Hash)[] originals = [
            ("AlternateStack","622D7BA2D40FF23FEA22A7DB446EAE1CF16B02C7A123EFF9E713EE8C58D78A54"),
            ("UserStack","2DEA83575725817DCA88219775282FC6E7D80A1E4FB8DA6B8765DDE51D299EBE"),
            ("BadReturn","73439983C09F7F5D277CE7357A99CE71D7B454F80515F21B66A5B4470AD9A8E9"),
            ("BadRestorer","80233BCD62009D6B97903E2060AAE1ABE4528DA187F9A678600E74698457A424")];
        foreach (var item in originals) HostCheck.Equal(item.Hash,Convert.ToHexString(SHA256.HashData(F.CreatePackage(item.Variant))));
        HostCheck.True(F.Variants.SequenceEqual(F.SyscallVariants.Concat(F.FaultVariants).Append(F.IrqVariant).Concat(F.WaitVariants).Concat(F.NestedVariants)) &&
            F.Variants.Distinct(StringComparer.Ordinal).Count() == 19);
    }

    [HostTest("m16-009 fault packages bind genuine read write NX CLI and null-stack instructions to actual saved continuations")]
    private static void FaultInstructions()
    {
        HostCheck.True(F.UnmappedFaultAddress >= OS.Kernel.Memory.X64UserAddressSpace.PhysicalEnd && F.UnmappedFaultAddress < OS.Kernel.Memory.X64UserAddressSpace.UserEnd);
        uint[] calls = [39,186,158,158,13,131,186,14,131,1,15,14,131,1,231];
        foreach (string variant in F.FaultVariants)
        {
            byte[] elf = F.CreateElf(variant); var fault = F.ExpectedFault(variant);
            int at = checked((int)(fault.TriggerRip-0x400000)), length;
            if (variant=="FaultGp") { HostCheck.Equal((byte)0xfa,elf[at]); length=1; }
            else if (variant=="FaultWrite")
            {
                HostCheck.True(elf.AsSpan(at,2).SequenceEqual(new byte[] {0xc7,5}));
                HostCheck.Equal(0x180,at+10+I32(elf,at+2)); HostCheck.Equal(0xdeadbeefU,U32(elf,at+6)); length=10;
            }
            else if (variant is "FaultExecute" or "FaultReturn")
            { HostCheck.Equal((byte)0xe9,elf[at]); HostCheck.Equal(0x3000,at+5+I32(elf,at+1)); length=5; }
            else
            { HostCheck.True(elf.AsSpan(at,10).SequenceEqual(new byte[] {0x48,0xa1,0,0,0,0x50,1,0,0,0})); length=10; }
            HostCheck.Equal(fault.TriggerRip+(uint)length,fault.ResumeRip);
            HostCheck.Equal(variant is "FaultExecute" or "FaultReturn" ? 0x403000UL : fault.TriggerRip,fault.SavedRip);
            HostCheck.Equal(variant=="FaultStack" ? 0UL : 0x405000UL,fault.SavedRsp);
            var instructions = F.ExpectedInvocations(variant); int count = variant=="FaultFatal" ? 6 : variant=="FaultReturn" ? 11 : 15;
            HostCheck.True(instructions.Count==count && instructions.Select(i=>i.Number).SequenceEqual(calls.Take(count)));
            for (int i=0;i<count;i++)
            {
                int end=checked((int)(instructions[i].ReturnRip-0x400000));
                HostCheck.True(elf.AsSpan(end-2,2).SequenceEqual(new byte[] {15,5}));
                int start=end-(i==10 ? 7 : 40); HostCheck.Equal((byte)0xb8,elf[start]); HostCheck.Equal(calls[i],U32(elf,start+1));
                HostCheck.Equal(i is >=6 and <=10,instructions[i].Handler);
            }
        }
    }

    [HostTest("m16-009 fault instructions seed every full GPR and independently check Linux context offsets and repairs")]
    private static void CompleteRegisters()
    {
        foreach (string variant in F.FaultVariants)
        {
            byte[] elf=F.CreateElf(variant); var fault=F.ExpectedFault(variant);
            int seed=checked((int)(fault.TriggerRip-0x400000))-156;
            for (uint register=0;register<16;register++) if (register!=4)
            {
                HostCheck.True(elf[seed]==(register<8 ? 0x48 : 0x49) && elf[seed+1]==0xb8+(register&7));
                HostCheck.Equal(0x1122334400000000UL+register*0x01010101UL,U64(elf,seed+2)); seed+=10;
            }
            HostCheck.Equal((byte)0xbc,elf[seed]); HostCheck.Equal((uint)fault.SavedRsp,U32(elf,seed+1)); HostCheck.Equal((byte)0xfd,elf[seed+5]);
            ReadOnlySpan<byte> handler=elf.AsSpan(0x800,0x600);
            foreach (uint offset in Enumerable.Range(0,15).Select(i=>40U+(uint)i*8))
                HostCheck.True(handler.IndexOf(new byte[] {0x48,0x8b,0x82,(byte)offset,0,0,0})>=0);
            HostCheck.True(handler.IndexOf(new byte[] {0x48,0xc7,0x82,0x90,0,0,0,0x78,0x56,0x34,0x12})>=0);
            bool repairs=handler.IndexOf(new byte[] {0x48,0xc7,0x82,0xa8,0,0,0})>=0;
            HostCheck.Equal(variant!="FaultReturn",repairs);
            HostCheck.True(handler.IndexOf(new byte[] {0x48,0xc7,0x82,0xa0,0,0,0,0,0x50,0x40,0})>=0);
            HostCheck.True(handler.IndexOf(new byte[] {0x25,0,4,1,0})>=0); // Hardware RF and interrupted DF.
            HostCheck.Equal(variant=="FaultFatal" ? 0UL : 0x400800UL,U64(elf,0x1100));
            HostCheck.True(U64(elf,0x1108)==0x0c000004 && U64(elf,0x1110)==0x400e00);
        }
    }

    [HostTest("m16-009 fault proofs retain FS GS low and high SIMD x87 controls and canonical fatal exit distinctions")]
    private static void FloatingPointAndBases()
    {
        foreach (string variant in F.FaultVariants)
        {
            byte[] elf=F.CreateElf(variant); ReadOnlySpan<byte> parent=elf.AsSpan(0x200,0x600),handler=elf.AsSpan(0x800,0x600);
            HostCheck.True(U64(elf,0x2000)==0x13579bdf && U64(elf,0x2100)==0x2468ace0);
            HostCheck.True(parent.IndexOf(new byte[] {0x66,0x44,0x0f,0x6e,0xf8})>=0 && parent.IndexOf(new byte[] {0x66,0x44,0x0f,0x7e,0xf8})>=0);
            HostCheck.True(handler.IndexOf(new byte[] {0x66,0x45,0x0f,0x76,0xff})>=0 && handler.IndexOf(new byte[] {0x41,0x8b,0x82,0x90,1,0,0})>=0);
            foreach (byte prefix in new byte[] {0x64,0x65})
            { byte[] load=[prefix,0x48,0x8b,4,0x25,0,0,0,0]; HostCheck.True(parent.IndexOf(load)>=0 && handler.IndexOf(load)>=0); }
            HostCheck.True(elf.AsSpan(0x180,15).SequenceEqual("M16_SIGNALS_OK!"u8) && elf.AsSpan(0x1a0,15).SequenceEqual("M16_HANDLER_OK!"u8));
            HostCheck.Equal(variant is "FaultReturn" or "FaultFatal" ? 139U : 42U,F.ExpectedExitCode(variant));
            HostCheck.Equal(variant,F.PackageVariant(F.CreatePackage(variant)));
        }
    }
    private static int I32(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static uint U32(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static ulong U64(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at));
}
