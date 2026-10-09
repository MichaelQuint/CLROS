namespace OS.Std.Binary;

public static partial class EndianPrimitives
{
    // The caller must retain a readable range of at least four/eight bytes.
    // Byte loads permit unaligned input; these overloads allocate and throw nothing.
    public static unsafe uint ReadUInt32LittleEndian(byte* source) =>
        source[0] | ((uint)source[1] << 8) | ((uint)source[2] << 16) | ((uint)source[3] << 24);

    public static unsafe ulong ReadUInt64LittleEndian(byte* source) =>
        ReadUInt32LittleEndian(source) | ((ulong)ReadUInt32LittleEndian(source + 4) << 32);
}
