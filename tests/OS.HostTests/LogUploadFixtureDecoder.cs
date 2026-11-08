using System.Buffers.Binary;

internal static class LogUploadFixtureDecoder
{
    internal static ReadOnlySpan<byte> ElfRange(byte[] elf, ulong address, ulong bytes)
    {
        const ulong image = OS.Tooling.LinuxWritableDiskProofFixture.ImageBase;
        HostCheck.True(address >= image && bytes <= (ulong)elf.Length && address - image <= (ulong)elf.Length - bytes);
        return elf.AsSpan((int)(address - image), (int)bytes);
    }

    internal static ulong[] DecodeLogUploadFrame(byte[] elf, OS.Tooling.LinuxWritableDiskProofFixture.CallSpec spec)
    {
        HostCheck.True(spec.ReturnRip >= OS.Tooling.LinuxWritableDiskProofFixture.ImageBase + 40);
        ReadOnlySpan<byte> code = ElfRange(elf, spec.ReturnRip - 40, 40);
        ulong[] actual = new ulong[7];
        int cursor = 0;
        byte[] registers = [0xb8, 0xbf, 0xbe, 0xba, 0xba, 0xb8, 0xb9];
        for (int i = 0; i < actual.Length; i++)
        {
            if (i >= 4)
                HostCheck.Equal((byte)0x41, code[cursor++]);
            HostCheck.Equal(registers[i], code[cursor++]);
            actual[i] = BinaryPrimitives.ReadUInt32LittleEndian(code[cursor..]);
            cursor += 4;
        }

        HostCheck.True(cursor == 38 && code[cursor] == 0x0f && code[cursor + 1] == 0x05 && actual.SequenceEqual(new[] { spec.Number, spec.A0, spec.A1, spec.A2, spec.A3, spec.A4, spec.A5 }));
        if (spec.Number != 231)
        {
            ReadOnlySpan<byte> check = ElfRange(elf, spec.ReturnRip, 12);
            HostCheck.True(check[0] == 0x48 && check[1] == 0x3d && BinaryPrimitives.ReadInt32LittleEndian(check[2..]) == spec.Result && check[6] == 0x0f && check[7] == 0x85 && (long)(spec.ReturnRip + 12) + BinaryPrimitives.ReadInt32LittleEndian(check[8..]) == (long)OS.Tooling.LinuxWritableDiskProofFixture.ImageBase + OS.Tooling.LinuxWritableDiskProofFixture.FailureOffset);
        }

        return actual;
    }
}
