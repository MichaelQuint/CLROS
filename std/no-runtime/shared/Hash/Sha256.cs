namespace OS.Std.Hash;

/// <summary>One-shot SHA-256 (FIPS 180-4), with caller-owned storage.
/// Trusted readable/writable pointers are required; invalid lengths and overlap
/// fail before writing. No allocation, exceptions, incremental state or signing.</summary>
public static unsafe class Sha256
{
    public const uint MaximumBytes = 128 * 1024 * 1024;
    public static bool TryCompute(byte* source, uint length, byte* digest, uint capacity)
    {
        if (length > MaximumBytes || (length != 0 && source == null) || digest == null || capacity < 32 ||
            (ulong)source > ulong.MaxValue - length || (ulong)digest > ulong.MaxValue - 32 ||
            (length != 0 && (ulong)source < (ulong)digest + 32 && (ulong)digest < (ulong)source + length)) return false;
        uint* h = stackalloc uint[8];
        h[0] = 0x6a09e667; h[1] = 0xbb67ae85; h[2] = 0x3c6ef372; h[3] = 0xa54ff53a;
        h[4] = 0x510e527f; h[5] = 0x9b05688c; h[6] = 0x1f83d9ab; h[7] = 0x5be0cd19;
        uint offset = 0;
        while (length - offset >= 64) { Compress(source + offset, h); offset += 64; }
        byte* tail = stackalloc byte[128];
        for (uint i = 0; i < 128; i++) tail[i] = 0;
        uint remaining = length - offset;
        for (uint i = 0; i < remaining; i++) tail[i] = source[offset + i];
        tail[remaining] = 0x80;
        uint padded = remaining < 56 ? 64u : 128u;
        ulong bits = (ulong)length * 8;
        for (uint i = 0; i < 8; i++) tail[padded - 1 - i] = (byte)(bits >> (int)(i * 8));
        Compress(tail, h); if (padded == 128) Compress(tail + 64, h);
        for (uint i = 0; i < 8; i++) for (uint j = 0; j < 4; j++) digest[i * 4 + j] = (byte)(h[i] >> (int)(24 - j * 8));
        return true;
    }

    private static uint R(uint x, int n) => (x >> n) | (x << (32 - n));
    private static void Compress(byte* block, uint* h)
    {
        uint* w = stackalloc uint[64];
        for (uint i = 0; i < 16; i++) w[i] = ((uint)block[i * 4] << 24) | ((uint)block[i * 4 + 1] << 16) | ((uint)block[i * 4 + 2] << 8) | block[i * 4 + 3];
        for (int i = 16; i < 64; i++)
        {
            uint x = w[i - 15], y = w[i - 2];
            w[i] = unchecked(w[i - 16] + (R(x, 7) ^ R(x, 18) ^ (x >> 3)) + w[i - 7] + (R(y, 17) ^ R(y, 19) ^ (y >> 10)));
        }
        uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], z = h[7];
        for (int i = 0; i < 64; i++)
        {
            uint t1 = unchecked(z + (R(e, 6) ^ R(e, 11) ^ R(e, 25)) + ((e & f) ^ (~e & g)) + K(i) + w[i]);
            uint t2 = unchecked((R(a, 2) ^ R(a, 13) ^ R(a, 22)) + ((a & b) ^ (a & c) ^ (b & c)));
            z = g; g = f; f = e; e = unchecked(d + t1); d = c; c = b; b = a; a = unchecked(t1 + t2);
        }
        h[0] = unchecked(h[0] + a); h[1] = unchecked(h[1] + b); h[2] = unchecked(h[2] + c); h[3] = unchecked(h[3] + d);
        h[4] = unchecked(h[4] + e); h[5] = unchecked(h[5] + f); h[6] = unchecked(h[6] + g); h[7] = unchecked(h[7] + z);
    }

    // Constants from FIPS 180-4 section 4.2.2; a switch needs no managed array.
    private static uint K(int i) => i switch
    {
        0 => 0x428a2f98, 1 => 0x71374491, 2 => 0xb5c0fbcf, 3 => 0xe9b5dba5,
        4 => 0x3956c25b, 5 => 0x59f111f1, 6 => 0x923f82a4, 7 => 0xab1c5ed5,
        8 => 0xd807aa98, 9 => 0x12835b01, 10 => 0x243185be, 11 => 0x550c7dc3,
        12 => 0x72be5d74, 13 => 0x80deb1fe, 14 => 0x9bdc06a7, 15 => 0xc19bf174,
        16 => 0xe49b69c1, 17 => 0xefbe4786, 18 => 0x0fc19dc6, 19 => 0x240ca1cc,
        20 => 0x2de92c6f, 21 => 0x4a7484aa, 22 => 0x5cb0a9dc, 23 => 0x76f988da,
        24 => 0x983e5152, 25 => 0xa831c66d, 26 => 0xb00327c8, 27 => 0xbf597fc7,
        28 => 0xc6e00bf3, 29 => 0xd5a79147, 30 => 0x06ca6351, 31 => 0x14292967,
        32 => 0x27b70a85, 33 => 0x2e1b2138, 34 => 0x4d2c6dfc, 35 => 0x53380d13,
        36 => 0x650a7354, 37 => 0x766a0abb, 38 => 0x81c2c92e, 39 => 0x92722c85,
        40 => 0xa2bfe8a1, 41 => 0xa81a664b, 42 => 0xc24b8b70, 43 => 0xc76c51a3,
        44 => 0xd192e819, 45 => 0xd6990624, 46 => 0xf40e3585, 47 => 0x106aa070,
        48 => 0x19a4c116, 49 => 0x1e376c08, 50 => 0x2748774c, 51 => 0x34b0bcb5,
        52 => 0x391c0cb3, 53 => 0x4ed8aa4a, 54 => 0x5b9cca4f, 55 => 0x682e6ff3,
        56 => 0x748f82ee, 57 => 0x78a5636f, 58 => 0x84c87814, 59 => 0x8cc70208,
        60 => 0x90befffa, 61 => 0xa4506ceb, 62 => 0xbef9a3f7, _ => 0xc67178f2
    };
}
