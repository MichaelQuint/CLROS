namespace OS.Drivers;

/// <summary>
/// Microsoft x64 leaf: ENDBR64; MFENCE; MOV EAX,1; RET. Ignores RCX context and
/// preserves nonvolatile registers and RSP. Invocation requires authenticated RX
/// code storage; emitting or validating bytes does not execute a memory barrier.
/// </summary>
public static unsafe class X64VirtqueueMemoryFence
{
    public const uint Bytes = 16;

    public static bool Emit(byte* destination, uint capacity)
    {
        if (destination == null || capacity < Bytes || (ulong)destination > ~0UL - Bytes) return false;
        for (uint i = 0; i < Bytes; i++) destination[i] = InstructionByte(i);
        return Validate(destination, capacity);
    }

    public static bool Validate(byte* destination, uint capacity)
    {
        if (destination == null || capacity < Bytes || (ulong)destination > ~0UL - Bytes) return false;
        for (uint i = 0; i < Bytes; i++) if (destination[i] != InstructionByte(i)) return false;
        return true;
    }

    private static byte InstructionByte(uint offset) =>
        (byte)((offset < 8 ? 0xb8f0ae0ffa1e0ff3UL : 0x000000c300000001UL) >> ((int)(offset & 7) * 8));
}
