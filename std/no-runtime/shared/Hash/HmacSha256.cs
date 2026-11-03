using OS.Std.Memory;

namespace OS.Std.Hash;

/// <summary>Allocation-free HMAC-SHA-256 (FIPS198-1), restricted to keys0..64
/// and messages0..256 bytes. Trusted mapped caller storage is required. Output
/// is32 bytes, disjoint from key/message; invalid admission writes nothing.
/// Key hashing for longer keys, streams, concurrency and certification are not
/// supplied. Addressed temporary pads/digests are erased before return.</summary>
public static unsafe class HmacSha256
{
    public const uint MaximumKeyBytes = 64, MaximumMessageBytes = 256;

    public static bool TryCompute(byte* key, uint keyBytes, byte* message, uint bytes, byte* digest, uint capacity)
    {
        if (keyBytes > MaximumKeyBytes || bytes > MaximumMessageBytes || !Range(key, keyBytes) ||
            !Range(message, bytes) || capacity < 32 || !Range(digest, 32) ||
            Overlap(key, keyBytes, digest, 32) || Overlap(message, bytes, digest, 32))
            return false;
        byte* inner = stackalloc byte[320];
        byte* outer = stackalloc byte[96];
        byte* hash = stackalloc byte[32];
        for (uint i = 0; i < 64; i++)
        {
            byte value = i < keyBytes ? key[i] : (byte)0;
            inner[i] = (byte)(value ^ 0x36);
            outer[i] = (byte)(value ^ 0x5c);
        }
        for (uint i = 0; i < bytes; i++)
            inner[64 + i] = message[i];
        bool success = Sha256.TryCompute(inner, 64 + bytes, hash, 32);
        if (success)
        {
            for (uint i = 0; i < 32; i++)
                outer[64 + i] = hash[i];
            success = Sha256.TryCompute(outer, 96, digest, capacity);
        }
        SensitiveMemory.Clear(inner, 320);
        SensitiveMemory.Clear(outer, 96);
        SensitiveMemory.Clear(hash, 32);
        return success;
    }

    internal static bool Range(void* address, uint bytes) => bytes == 0 || address != null && (ulong)address <= ulong.MaxValue - bytes;
    internal static bool Overlap(void* a, uint an, void* b, uint bn) => an != 0 && bn != 0 &&
        (ulong)a < (ulong)b + bn && (ulong)b < (ulong)a + an;
}
