using T = OS.Kernel.Diagnostics.WritableStorageTiming;

namespace OS.HostTests;

public static unsafe class WritableStorageTimingTests
{
    [HostTest("writable timing separates inclusive pair and pump ticks from nested retained audit ticks")]
    public static void NestedAccounting()
    {
        T.State s = default;
        ulong raw = 100;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            raw = 110;
            var pair = T.Enter(T.Bucket.Pair);
            raw = 120;
            var audit = T.Enter(T.Bucket.Audit);
            T.OutsideDevice();
            T.OutsideDevice();
            raw = 150;
            T.Leave(audit);
            raw = 170;
            T.Leave(pair);
            raw = 180;
            var pump = T.Enter(T.Bucket.Pump);
            raw = 185;
            audit = T.Enter(T.Bucket.Audit);
            T.OutsideDevice();
            raw = 195;
            T.Leave(audit);
            raw = 220;
            T.Leave(pump);
            raw = 240;
            HostCheck.True(T.End(&s));
            HostCheck.True(s.AuditCalls == 2 && s.AuditTicks == 40 && s.OutsideDeviceCalls == 3);
            HostCheck.True(s.PairCalls == 1 && s.PairTicks == 60 && s.PairAuditTicks == 30);
            HostCheck.True(s.PumpCalls == 1 && s.PumpTicks == 40 && s.PumpAuditTicks == 10);
            HostCheck.True(s.OuterTicks == 140 && s.RawReads == 12 && s.AdjacentReadTicks == 0 && s.Phase == 2 && !s.Failed);
            HostCheck.True(s.AuditOutsideEnclosingTicks == 0 && s.UnattributedTicks == 40);
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing preserves bounded modular 32 bit counter wrap")]
    public static void Wrap32()
    {
        T.State s = default;
        ulong raw = uint.MaxValue - 5UL;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 32));
        try
        {
            var stamp = T.Enter(T.Bucket.Audit);
            raw = 7;
            T.Leave(stamp);
            raw = 9;
            HostCheck.True(T.End(&s));
            HostCheck.True(s.AuditTicks == 13 && s.OuterTicks == 15 && s.Width == 32);
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing rejects ambiguous or backwards deltas without wrapping totals")]
    public static void BadDelta()
    {
        foreach (uint width in new uint[] { 32, 64 })
        {
            T.State s = default;
            ulong raw = 0;
            HostCheck.True(T.Begin(&s, (ulong)&raw, width));
            try
            {
                var stamp = T.Enter(T.Bucket.Audit);
                raw = width == 32 ? 0x80000000UL : 0x8000000000000000UL;
                T.Leave(stamp);
                HostCheck.True(s.Failed && s.AuditTicks == 0 && !T.End(&s));
            }
            finally { T.Cancel(&s); }
        }
    }

    [HostTest("writable timing invalid installation and foreign cancellation have no effects")]
    public static void InvalidInstall()
    {
        T.State s = default, other = default;
        ulong raw = 10;
        HostCheck.True(!T.Begin(null, (ulong)&raw, 64) && !T.Begin(&s, 0, 64));
        HostCheck.True(!T.Begin(&s, (ulong)&raw, 16) && !T.Begin(&s, (ulong)&s, 64));
        HostCheck.True(s.Self == 0 && s.Phase == 0);
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            HostCheck.True(!T.Begin(&other, (ulong)&raw, 64));
            T.Cancel(&other);
            HostCheck.True(other.Phase == 0 && other.Self == 0 && !T.End(&other));
            raw = 20;
            HostCheck.True(T.End(&s));
        }
        finally { T.Cancel(&s); }
        HostCheck.True(!T.Begin(&s, (ulong)&raw, 64));
    }

    [HostTest("writable timing disabled hooks never retain receipts or touch former storage")]
    public static void Disabled()
    {
        var stamp = T.Enter(T.Bucket.Audit);
        T.Leave(stamp);
        T.OutsideDevice();
        T.EngineProgress(4096, 1, 2, 3, false, true);
        T.State s = default;
        HostCheck.True(!T.End(&s));
        T.Cancel(&s);
        HostCheck.True(s.Self == 0 && s.RawReads == 0 && s.Phase == 0);
    }

    [HostTest("writable timing cancelled and reused area rejects stale generation receipts")]
    public static void ReusedArea()
    {
        T.State s = default;
        ulong raw = 10;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        var old = T.Enter(T.Bucket.Pair);
        ulong generation = s.Generation;
        T.Cancel(&s);
        s = default;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            var fresh = T.Enter(T.Bucket.Pair);
            raw = 20;
            T.Leave(old);
            HostCheck.True(s.PairDepth == 1 && s.PairTicks == 0 && !s.Failed && s.Generation > generation);
            T.Leave(fresh);
            raw = 30;
            HostCheck.True(T.End(&s));
            HostCheck.True(s.PairTicks == 10 && s.OuterTicks == 20);
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing incomplete and recursive hooks invalidate the whole report")]
    public static void InvalidDepth()
    {
        T.State s = default;
        ulong raw = 10;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            var first = T.Enter(T.Bucket.Audit);
            _ = T.Enter(T.Bucket.Audit);
            T.Leave(first);
            HostCheck.True(s.Failed && !T.End(&s));
            s = default;
            HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
            _ = T.Enter(T.Bucket.Pump);
            raw = 20;
            HostCheck.True(!T.End(&s) && s.Failed && s.Phase == 2);
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing count and accumulated tick overflow poison rather than truncate evidence")]
    public static void Overflow()
    {
        T.State s = default;
        ulong raw = 10;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            s.AuditCalls = ulong.MaxValue;
            _ = T.Enter(T.Bucket.Audit);
            HostCheck.True(s.Failed && s.AuditCalls == ulong.MaxValue && !T.End(&s));
            s = default;
            HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
            var stamp = T.Enter(T.Bucket.Audit);
            s.AuditTicks = ulong.MaxValue - 2;
            raw = 20;
            T.Leave(stamp);
            HostCheck.True(s.Failed && s.AuditTicks == ulong.MaxValue - 2 && !T.End(&s));
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing engine progress counts real entries and copies first and final state")]
    public static void EngineProgress()
    {
        T.State s = default;
        ulong raw = 10;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            T.OutsideDevice();
            T.EngineProgress(4096, 2, 3, 100, true, true);
            T.EngineProgress(4096, 2, 4, 101, false, false);
            T.EngineProgress(4096, 2, 4, 101, false, true);
            T.EngineProgress(4096, 3, 5, 102, false, false);
            raw = 20;
            HostCheck.True(T.End(&s));
            HostCheck.True(s.EnginePumpCalls == 2 && s.EngineOwner == 4096 && s.OutsideDeviceCalls == 0);
            HostCheck.True(s.FirstEnginePhase == 2 && s.FirstEngineStep == 3 && s.FirstEngineWork == 100 && s.FirstEngineIo);
            HostCheck.True(s.LastEnginePhase == 3 && s.LastEngineStep == 5 && s.LastEngineWork == 102 && !s.LastEngineIo);
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing mixed engine or unmatched engine leave invalidates copied progress")]
    public static void InvalidEngine()
    {
        T.State s = default;
        ulong raw = 10;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            T.EngineProgress(4096, 2, 3, 0, false, false);
            HostCheck.True(s.Failed && !T.End(&s));
            s = default;
            HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
            T.EngineProgress(4096, 2, 3, 0, false, true);
            T.EngineProgress(8192, 2, 4, 1, false, false);
            HostCheck.True(s.Failed && !T.End(&s));
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing old completed scope cannot close a later hook in the same turn")]
    public static void OldScope()
    {
        T.State s = default;
        ulong raw = 10;
        HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
        try
        {
            var old = T.Enter(T.Bucket.Pair);
            raw = 20;
            T.Leave(old);
            raw = 30;
            var fresh = T.Enter(T.Bucket.Pair);
            raw = 40;
            T.Leave(old);
            HostCheck.True(s.PairDepth == 1 && s.PairTicks == 10 && !s.Failed);
            T.Leave(fresh);
            raw = 50;
            HostCheck.True(T.End(&s));
            HostCheck.True(s.PairCalls == 2 && s.PairTicks == 20 && s.OuterTicks == 40);
        }
        finally { T.Cancel(&s); }
    }

    [HostTest("writable timing rejects overlapping enclosing buckets rather than double counting audits")]
    public static void OverlappingEnclosingBuckets()
    {
        foreach (var kind in new[] { T.Bucket.Pair, T.Bucket.Pump })
        {
            T.State s = default;
            ulong raw = 10;
            HostCheck.True(T.Begin(&s, (ulong)&raw, 64));
            try
            {
                var outer = T.Enter(kind);
                _ = T.Enter(kind == T.Bucket.Pair ? T.Bucket.Pump : T.Bucket.Pair);
                raw = 20;
                T.Leave(outer);
                HostCheck.True(s.Failed && !T.End(&s) && s.UnattributedTicks == 0);
            }
            finally { T.Cancel(&s); }
        }
    }
}
