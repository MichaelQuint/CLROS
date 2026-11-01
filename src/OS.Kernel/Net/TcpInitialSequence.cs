using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Net;
/// <summary>RFC9293 section3.4.1 ISN = four-microsecond clock + keyed tuple PRF,
/// modulo2^32. HMAC-SHA256 uses an independently acquired32-byte secret, never
/// guest AT_RANDOM or application random output. One serialized supervisor owner
/// retains mapped noncopyable state; source quality/freshness belongs to admission.
/// No allocation, clock callback, packet validation or TCP ownership occurs here.</summary>

public static unsafe partial class TcpInitialSequence
{
    // Conservative RFC6528 section3 key-replacement quiet time, also at startup
    // when no previous connection history is available. Never shorten TIME-WAIT.
    public const ulong QuietNanoseconds = 240_000_000_000;
    /// <summary>Copy exactly32 trusted fresh entropy bytes from disjoint readable
    /// storage into zeroed owner. Invalid admission writes nothing. Caller erases
    /// temporary seed material and drops source callbacks before firmware exit.
    /// The first Acquire starts quiet time using the actual monotonic clock.</summary>

    public static bool Initialize(Owner* owner, byte* secret, ulong epoch)
    {
        if (!Storage(owner) || epoch == 0 || !Input(owner, secret) || !Zero((byte*)owner, (uint)sizeof(Owner)))
            return false;
        for (uint i = 0; i < 32; i++)
            owner->Key[i] = secret[i];
        owner->Self = (ulong)owner;
        owner->Epoch = epoch;
        return true;
    }

    /// <summary>Borrow once for the complete network lifetime, including pending,
    /// fd-less closing and TIME-WAIT connections. Caller admits disjoint borrower
    /// backing before this call. Overflow, clock reversal and live borrow reject
    /// unchanged. A new key must wait240 seconds before issuing its first ISN.</summary>

    public static Lease Acquire(Owner* owner, void* borrower, ulong now)
    {
        if (!Live(owner) || borrower == null || owner->Borrower != 0 || owner->Generation == ulong.MaxValue || now < owner->LastNow || owner->ClockStarted == 0 && now > ulong.MaxValue - QuietNanoseconds)
            return default;
        if (owner->ClockStarted == 0)
        {
            owner->QuietUntil = now + QuietNanoseconds;
            owner->ClockStarted = 1;
        }

        owner->LastNow = now;
        owner->Generation++;
        owner->Borrower = (ulong)borrower;
        return new(owner);
    }

    /// <summary>Read-only exact lease validation. Caller must already trust and
    /// retain the mapped owner address; this method is not a memory probe.</summary>

    public static bool Current(Owner* owner, Lease lease) => Live(owner) && lease.IsValid && lease.Address == (ulong)owner && lease.Epoch == owner->Epoch && lease.Generation == owner->Generation && lease.Borrower == owner->Borrower;
    /// <summary>For an admitted new SYN tuple, return M + first32 HMAC bits in
    /// network byte order. Message = ASCII CLROS.TCP.ISN.v1 + network-order local
    /// IPv4/port/remote IPv4/port. Clock is actual monotonic nanoseconds /4000;
    /// its32-bit wrap is ordinary TCP modular arithmetic, never capacity failure.
    /// Quiet/invalid/exhausted requests consume no ISN; no packet or key is logged.</summary>

    public static Result Next(Owner* owner, Lease lease, in TcpConnectionKey tuple, ulong now)
    {
        if (!Current(owner, lease))
            return new(Status.InvalidState);
        if (!tuple.HasValidEndpoint || tuple.LocalAddress.Octet0 is 0 or >= 224 || tuple.RemoteAddress.Octet0 is 0 or >= 224)
            return new(Status.InvalidEndpoint);
        if (now < owner->LastNow)
            return new(Status.ClockFailure);
        if (owner->Issued == ulong.MaxValue)
            return new(Status.Exhausted);
        if (now < owner->QuietUntil)
        {
            owner->LastNow = now;
            return new(Status.Quiet);
        }

        byte* input = stackalloc byte[28];
        byte* digest = stackalloc byte[32];
        const string domain = "CLROS.TCP.ISN.v1";
        for (int i = 0; i < domain.Length; i++)
            input[i] = (byte)domain[i];
        uint offset = (uint)domain.Length;
        Address(input + offset, tuple.LocalAddress);
        Port(input + offset + 4, tuple.LocalPort);
        Address(input + offset + 6, tuple.RemoteAddress);
        Port(input + offset + 10, tuple.RemotePort);
        bool computed = HmacSha256.TryCompute(owner->Key, 32, input, offset + 12, digest, 32);
        uint sequence = computed ? unchecked((uint)(now / 4000) + ((uint)digest[0] << 24 | (uint)digest[1] << 16 | (uint)digest[2] << 8 | digest[3])) : 0;
        SensitiveMemory.Clear(input, 28);
        SensitiveMemory.Clear(digest, 32);
        if (!computed)
            return new(Status.InvalidState);
        owner->LastNow = now;
        owner->Issued++;
        return new(Status.Success, sequence);
    }

    /// <summary>Release only after all borrowers and TCP records stop using this
    /// key lifetime. Network drain/abort policy belongs to the caller. A stale
    /// lease rejects unchanged; released copies cannot issue further ISNs.</summary>

    public static bool Release(Owner* owner, Lease lease)
    {
        if (!Current(owner, lease))
            return false;
        owner->Borrower = 0;
        return true;
    }

    /// <summary>Replace a drained lifetime with32 newly acquired entropy bytes and
    /// a strictly greater epoch. No clock/counter fallback or live replacement.
    /// Equal keys reject. The next Acquire restarts full quiet time, retaining
    /// monotonic clock/generation/issued overflow guards. Source freshness is the
    /// caller's contract, not established by checking unequal key bytes.</summary>

    public static bool Reseed(Owner* owner, byte* secret, ulong epoch)
    {
        if (!Live(owner) || owner->Borrower != 0 || epoch <= owner->Epoch || !Input(owner, secret))
            return false;
        uint difference = 0;
        for (uint i = 0; i < 32; i++)
            difference |= (uint)(owner->Key[i] ^ secret[i]);
        if (difference == 0)
            return false;
        SensitiveMemory.Clear(owner->Key, 32);
        for (uint i = 0; i < 32; i++)
            owner->Key[i] = secret[i];
        owner->Epoch = epoch;
        owner->ClockStarted = 0;
        owner->QuietUntil = 0;
        return true;
    }

    /// <summary>Erase exactly this private unborrowed owner; copied/live-borrowed
    /// owners reject unchanged. No registers or other copies are claimed erased.</summary>

    public static bool Erase(Owner* owner) => Live(owner) && owner->Borrower == 0 && SensitiveMemory.Clear(owner, (uint)sizeof(Owner));
    /// <summary>Read-only validation of the exact mapped owner; establishes no
    /// entropy provenance or authority to erase a borrowed lifetime.</summary>

    public static bool IsInitialized(Owner* owner) => Live(owner);
    private static bool Storage(Owner* owner) => owner != null && ((ulong)owner & 7) == 0 && Range(owner, (uint)sizeof(Owner));
    private static bool Live(Owner* owner) => Storage(owner) && owner->Self == (ulong)owner && owner->Epoch != 0 && owner->ClockStarted <= 1 && (owner->Borrower == 0 || owner->ClockStarted == 1 && owner->Generation != 0);
    private static bool Input(Owner* owner, byte* source) => Range(source, 32) && !((ulong)owner < (ulong)source + 32 && (ulong)source < (ulong)owner + (uint)sizeof(Owner));
    private static bool Range(void* pointer, uint bytes) => pointer != null && (ulong)pointer <= ulong.MaxValue - bytes;
    private static bool Zero(byte* bytes, uint count)
    {
        for (uint i = 0; i < count; i++)
            if (bytes[i] != 0)
                return false;
        return true;
    }

    private static void Port(byte* output, ushort port)
    {
        output[0] = (byte)(port >> 8);
        output[1] = (byte)port;
    }

    private static void Address(byte* output, Ipv4Address address)
    {
        output[0] = address.Octet0;
        output[1] = address.Octet1;
        output[2] = address.Octet2;
        output[3] = address.Octet3;
    }
}
