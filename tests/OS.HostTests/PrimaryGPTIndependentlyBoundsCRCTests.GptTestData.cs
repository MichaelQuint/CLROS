using System.Buffers.Binary;
using OS.Storage;

internal static class GptTestData
{
    internal const ulong Capacity = 131072;
    internal static (byte[] Header, byte[] Entries) Create(bool backup = false)
    {
        byte[] header = new byte[512], entries = new byte[16384];
        "EFI PART"u8.CopyTo(header);
        U32(header, 8, 0x10000);
        U32(header, 12, 92);
        U64(header, 24, backup ? Capacity - 1 : 1);
        U64(header, 32, backup ? 1 : Capacity - 1);
        U64(header, 40, 34);
        U64(header, 48, Capacity - 34);
        U64(header, 56, 0x0123456789abcdef);
        U64(header, 64, 0xfedcba9876543210);
        U64(header, 72, backup ? Capacity - 33 : 2);
        U32(header, 80, 128);
        U32(header, 84, 128);
        U64(entries, 0, 0x11d2f81fc12a7328);
        U64(entries, 8, 0x3bc93ec9a0004bba);
        U64(entries, 16, 0x0102030405060708);
        U64(entries, 24, 0x1112131415161718);
        U64(entries, 32, 2048);
        U64(entries, 40, 131038);
        RehashArray(header, entries);
        return (header, entries);
    }

    internal static void RehashHeader(byte[] header)
    {
        U32(header, 16, 0);
        U32(header, 16, OS.Std.Hash.Crc32.Compute(header.AsSpan(0, (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)))));
    }

    internal static void RehashArray(byte[] header, byte[] entries)
    {
        U32(header, 88, OS.Std.Hash.Crc32.Compute(entries));
        RehashHeader(header);
    }

    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void U64(byte[] bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
}
