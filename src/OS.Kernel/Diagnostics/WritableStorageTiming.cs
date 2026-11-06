using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

namespace OS.Kernel.Diagnostics;
/// <summary>One serialized diagnostic turn, independent of the normal clock.
/// The supervisor owns mapped ordinary counter storage and an already admitted
/// HPET register through End/Cancel. Address checks do not establish mapping or
/// device authority. Hooks perform direct counter loads, never callbacks, I/O
/// submission, allocation or normal-clock mutation. Sampling must remain within
/// half the counter range without reset; missed multiple wraps are unknowable.
/// Raw totals include measurement overhead and grant no execution/fit authority.</summary>

public static unsafe partial class WritableStorageTiming
{
    // Only the serialized supervisor installs this diagnostic pointer. No
    // constructor, managed object, callback or cross-turn permission is retained.
    private static State* current;
    private static ulong generation;
    /// <summary>Whether the serialized supervisor currently has an installed
    /// diagnostic area. This query performs no counter load or admission.</summary>

    public static bool IsActive => current != null;

    /// <summary>Install one zeroed caller-owned area. Invalid geometry or a
    /// second installation has no effects. The caller separately admits HPET
    /// width, mapping, lifetime and the no-reset/half-range sampling interval.</summary>

    public static bool Begin(State* s, ulong counterAddress, uint width)
    {
        if (current != null || generation == ulong.MaxValue || !Ordinary((ulong)s, (ulong)sizeof(State)) || !Ordinary(counterAddress, 8) || width is not (32 or 64) || Overlap((ulong)s, (ulong)sizeof(State), counterAddress, 8))
            return false;
        for (uint i = 0; i < sizeof(State); i++)
            if (((byte*)s)[i] != 0)
                return false;
        s->Self = (ulong)s;
        s->Generation = ++generation;
        s->Counter = counterAddress;
        s->Width = width;
        s->Phase = 1;
        current = s;
        ulong calibration = Sample(s), afterCalibration = Sample(s);
        var overhead = MonotonicHpetTscClockMath.Accumulate(calibration, afterCalibration, width, 0);
        if (!overhead.Succeeded)
        {
            current = null;
            s->Failed = true;
            s->Phase = 2;
            return false;
        }

        s->AdjacentReadTicks = overhead.DeltaTicks;
        s->Started = Sample(s);
        return !s->Failed;
    }

    /// <summary>Start one hook; same-bucket recursion poisons the measurement.
    /// Disabled/failed measurement returns an inert receipt without MMIO.</summary>

    public static Stamp Enter(Bucket kind)
    {
        State* s = current;
        if (s == null || s->Failed)
            return default;
        if (kind > Bucket.Pump)
        {
            s->Failed = true;
            return default;
        }

        if (kind == Bucket.Pair && s->PumpDepth != 0 || kind == Bucket.Pump && s->PairDepth != 0)
        {
            s->Failed = true;
            return default;
        }

        uint* depth = kind == Bucket.Audit ? &s->AuditDepth : kind == Bucket.Pair ? &s->PairDepth : &s->PumpDepth;
        ulong* calls = kind == Bucket.Audit ? &s->AuditCalls : kind == Bucket.Pair ? &s->PairCalls : &s->PumpCalls;
        if (*depth != 0 || !Increment(s, calls))
        {
            s->Failed = true;
            return default;
        }

        *depth = 1;
        return new(s, *calls, Sample(s), kind);
    }

    /// <summary>Account every hook return, including rejection. Child audit
    /// deltas remain separate from enclosing inclusive totals. A cancelled or
    /// foreign receipt causes no writes to its former owner.</summary>

    public static void Leave(Stamp stamp)
    {
        State* s = current;
        if (s == null || stamp.Owner != s || stamp.Generation != s->Generation || s->Phase != 1)
            return;
        ulong ordinal = stamp.Kind == Bucket.Audit ? s->AuditCalls : stamp.Kind == Bucket.Pair ? s->PairCalls : s->PumpCalls;
        if (stamp.Ordinal != ordinal)
            return;
        uint* depth = stamp.Kind == Bucket.Audit ? &s->AuditDepth : stamp.Kind == Bucket.Pair ? &s->PairDepth : &s->PumpDepth;
        if (*depth != 1)
        {
            s->Failed = true;
            return;
        }

        *depth = 0;
        if (s->Failed)
            return;
        ulong raw = Sample(s);
        ulong* total = stamp.Kind == Bucket.Audit ? &s->AuditTicks : stamp.Kind == Bucket.Pair ? &s->PairTicks : &s->PumpTicks;
        var delta = MonotonicHpetTscClockMath.Accumulate(stamp.Raw, raw, s->Width, *total);
        if (!delta.Succeeded || s->AuditTicks < stamp.Audit)
        {
            s->Failed = true;
            return;
        }

        *total = delta.AccumulatedTicks;
        if (stamp.Kind != Bucket.Audit)
        {
            ulong audit = s->AuditTicks - stamp.Audit;
            ulong* nested = stamp.Kind == Bucket.Pair ? &s->PairAuditTicks : &s->PumpAuditTicks;
            if (audit > delta.DeltaTicks || *nested > ulong.MaxValue - audit)
                s->Failed = true;
            else
                *nested += audit;
        }
    }

    /// <summary>Count a real OutsideDevice invocation without timing it.
    /// Only calls nested in the active retained audit belong to this count.</summary>

    public static void OutsideDevice()
    {
        State* s = current;
        if (s != null && !s->Failed && s->AuditDepth != 0)
            Increment(s, &s->OutsideDeviceCalls);
    }

    /// <summary>Copy actual engine progress at a C.Pump boundary. Enter counts
    /// real calls; leave only updates the last copied state. Mixed engine owners
    /// invalidate evidence. Values are diagnostics, not live tokens or authority.</summary>

    public static void EngineProgress(ulong owner, uint phase, uint step, uint work, bool io, bool entering)
    {
        State* s = current;
        if (s == null || s->Failed)
            return;
        if (owner == 0 || s->HasEngineProgress && owner != s->EngineOwner)
        {
            s->Failed = true;
            return;
        }

        if (s->EngineDepth != (entering ? 0U : 1U))
        {
            s->Failed = true;
            return;
        }

        if (entering && !Increment(s, &s->EnginePumpCalls))
            return;
        s->EngineDepth = entering ? 1U : 0U;
        if (!s->HasEngineProgress)
        {
            s->EngineOwner = owner;
            s->FirstEnginePhase = phase;
            s->FirstEngineStep = step;
            s->FirstEngineWork = work;
            s->FirstEngineIo = io;
            s->HasEngineProgress = true;
        }

        s->LastEnginePhase = phase;
        s->LastEngineStep = step;
        s->LastEngineWork = work;
        s->LastEngineIo = io;
    }

    /// <summary>Remove the diagnostic pointer before returning. Open hooks,
    /// overflow or ambiguous deltas invalidate the report; original work is
    /// unaffected. The caller may serialize/release its area only after return.</summary>

    public static bool End(State* s)
    {
        if (s == null || s != current)
            return false;
        ulong raw = s->Failed ? 0 : Sample(s);
        current = null;
        s->Phase = 2;
        if (s->AuditDepth != 0 || s->PairDepth != 0 || s->PumpDepth != 0 || s->EngineDepth != 0)
            s->Failed = true;
        if (!s->Failed)
        {
            var delta = MonotonicHpetTscClockMath.Accumulate(s->Started, raw, s->Width, 0);
            if (!delta.Succeeded)
                s->Failed = true;
            else
                s->OuterTicks = delta.DeltaTicks;
        }

        if (!s->Failed)
        {
            if (s->PairAuditTicks > s->AuditTicks || s->PumpAuditTicks > s->AuditTicks - s->PairAuditTicks || s->PairAuditTicks > s->PairTicks || s->PumpAuditTicks > s->PumpTicks || s->PairTicks > ulong.MaxValue - s->PumpTicks)
                s->Failed = true;
            else
            {
                ulong outside = s->AuditTicks - s->PairAuditTicks - s->PumpAuditTicks;
                ulong enclosed = s->PairTicks + s->PumpTicks;
                if (enclosed > s->OuterTicks || outside > s->OuterTicks - enclosed)
                    s->Failed = true;
                else
                {
                    s->AuditOutsideEnclosingTicks = outside;
                    s->UnattributedTicks = s->OuterTicks - enclosed - outside;
                }
            }
        }

        return !s->Failed;
    }

    /// <summary>Uninstall without reading MMIO. Aborted measurements remain
    /// failed; late receipts cannot write after the caller releases the area.</summary>

    public static void Cancel(State* s)
    {
        if (s == null || s != current)
            return;
        current = null;
        s->Failed = true;
        s->Phase = 2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong Sample(State* s)
    {
        if (!Increment(s, &s->RawReads))
            return 0;
        // A distinct non-inlined load on every sample. Original-byte review
        // must verify this MMIO access in the selected native publication.
        return s->Width == 32 ? *(uint*)s->Counter : *(ulong*)s->Counter;
    }

    private static bool Increment(State* s, ulong* value)
    {
        if (*value == ulong.MaxValue)
        {
            s->Failed = true;
            return false;
        }

        (*value)++;
        return true;
    }

    private static bool Ordinary(ulong address, ulong bytes) => address >= 4096 && address % 8 == 0 && address < 0x0000800000000000UL && bytes <= 0x0000800000000000UL - address;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
}
