using OS.Std.Memory;

namespace OS.Std.Hash;

/// <summary>HMAC_DRBG/SHA-256 from NIST SP800-90A rev1,10.1.2. One serialized
/// owner supplies trusted mapped noncopyable state and independently acquired
/// entropy. No entropy source/quality claim, prediction resistance, additional
/// generate input or certification is supplied. Seed bytes must never be logged.
/// Instantiate needs32 entropy+16 nonce bytes; reseed needs32 fresh entropy bytes.
/// Requests are1..8192 bytes; exceeding2^48 requests requires explicit reseed.</summary>
public static unsafe class HmacSha256Drbg
{
    public const uint MaximumRequestBytes = 8192, MaximumPersonalizationBytes = 32;
    public const ulong ReseedInterval = 1UL << 48;

    /// <summary>Initially zero, exclusive and supervisor-owned through Erase.
    /// A live value cannot be copied or moved. Counter is an observation only;
    /// other fields are private generator state, never application output.</summary>
    public struct State
    {
        internal ulong Self, Counter;
        internal fixed byte Key[32], Value[32];
        public readonly ulong RequestsSinceReseed => Counter == 0 ? 0 : Counter - 1;
        public readonly bool RequiresReseed => Counter > ReseedInterval;
    }

    /// <summary>Validate the exact mapped owner address/counter without output
    /// or mutation. A reseed-required owner remains initialized but Generate
    /// refuses it. This establishes no entropy provenance or borrowing authority.</summary>
    public static bool IsInitialized(State* state) => Live(state);

    /// <summary>Initialize exclusive zeroed state from32 entropy+16 nonce bytes,
    /// optionally personalized with0..32 bytes. All input must be readable and
    /// disjoint from state. Invalid admission writes nothing. Successful caller
    /// admission must erase temporary seeds; this method retains no seed pointer.</summary>
    public static bool Instantiate(State* state, byte* entropy, byte* nonce, byte* personalization, uint personalizationBytes)
    {
        if (!Storage(state) || !Zero((byte*)state, (uint)sizeof(State)) || personalizationBytes > MaximumPersonalizationBytes ||
            !Input(state, entropy, 32) || !Input(state, nonce, 16) || !Input(state, personalization, personalizationBytes)) return false;
        byte* seed = stackalloc byte[80];
        Copy(entropy, seed, 32); Copy(nonce, seed + 32, 16); Copy(personalization, seed + 48, personalizationBytes);
        for (uint i = 0; i < 32; i++) state->Value[i] = 1;
        bool success = Update(state, seed, 48 + personalizationBytes);
        SensitiveMemory.Clear(seed, 80);
        if (!success) { SensitiveMemory.Clear(state, (uint)sizeof(State)); return false; }
        state->Self = (ulong)state; state->Counter = 1; return true;
    }

    /// <summary>Generate into disjoint caller storage, updating key and value
    /// after every request, including partial final blocks. Invalid state/range
    /// or reseed exhaustion rejects unchanged. The caller must consume the whole
    /// request even if a later user copy fails; rollback would repeat output.</summary>
    public static bool Generate(State* state, byte* output, uint bytes)
    {
        if (!Live(state) || state->Counter > ReseedInterval || bytes is 0 or > MaximumRequestBytes ||
            !Input(state, output, bytes)) return false;
        byte* digest = stackalloc byte[32];
        for (uint offset = 0; offset < bytes;)
        {
            if (!HmacSha256.TryCompute(state->Key, 32, state->Value, 32, digest, 32))
            { SensitiveMemory.Clear(digest, 32); return false; }
            Copy(digest, state->Value, 32);
            uint count = bytes - offset; if (count > 32) count = 32;
            Copy(digest, output + offset, count); offset += count;
        }
        bool success = Update(state, null, 0);
        SensitiveMemory.Clear(digest, 32);
        if (success) state->Counter++;
        return success;
    }

    /// <summary>Mix32 newly acquired entropy bytes; failed source acquisition
    /// must not call this method or fall back to clocks. Admission failure leaves
    /// state unchanged. Freshness and source provenance belong to the caller.</summary>
    public static bool Reseed(State* state, byte* entropy)
    {
        if (!Live(state) || !Input(state, entropy, 32)) return false;
        bool success = Update(state, entropy, 32); if (success) state->Counter = 1;
        return success;
    }

    /// <summary>Erase exactly this live owner's state after all borrowers drain.
    /// A copied/invalid owner rejects unchanged. Reuse requires a fresh caller
    /// lifetime with newly acquired entropy, never saved seed/state restoration.</summary>
    public static bool Erase(State* state) => Live(state) && SensitiveMemory.Clear(state, (uint)sizeof(State));

    private static bool Update(State* state, byte* seed, uint bytes)
    {
        byte* input = stackalloc byte[113]; byte* digest = stackalloc byte[32];
        Copy(state->Value, input, 32); input[32] = 0; Copy(seed, input + 33, bytes);
        bool success = HmacSha256.TryCompute(state->Key, 32, input, 33 + bytes, digest, 32);
        if (success)
        {
            Copy(digest, state->Key, 32);
            success = HmacSha256.TryCompute(state->Key, 32, state->Value, 32, digest, 32);
            if (success) Copy(digest, state->Value, 32);
        }
        if (success && bytes != 0)
        {
            Copy(state->Value, input, 32); input[32] = 1;
            success = HmacSha256.TryCompute(state->Key, 32, input, 33 + bytes, digest, 32);
            if (success)
            {
                Copy(digest, state->Key, 32);
                success = HmacSha256.TryCompute(state->Key, 32, state->Value, 32, digest, 32);
                if (success) Copy(digest, state->Value, 32);
            }
        }
        SensitiveMemory.Clear(input, 113); SensitiveMemory.Clear(digest, 32); return success;
    }

    private static bool Storage(State* state) => state != null && ((ulong)state & 7) == 0 && HmacSha256.Range(state, (uint)sizeof(State));
    private static bool Live(State* state) => Storage(state) && state->Self == (ulong)state && state->Counter is > 0 and <= ReseedInterval + 1;
    private static bool Input(State* state, byte* bytes, uint count) => HmacSha256.Range(bytes, count) &&
        !HmacSha256.Overlap(state, (uint)sizeof(State), bytes, count);
    private static bool Zero(byte* value, uint bytes) { for (uint i = 0; i < bytes; i++) if (value[i] != 0) return false; return true; }
    private static void Copy(byte* source, byte* target, uint bytes) { for (uint i = 0; i < bytes; i++) target[i] = source[i]; }
}
