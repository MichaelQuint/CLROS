using System.Buffers.Binary;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaPthreadTests
{
    private const ulong Main = 0x100000001, Child = 0x100000002, Base = 0x100000000, Tls = Base + 0x5100, Top = Base + 0x5000, Sp = Top - 40, ClearTid = 0xc55930;
    private const ulong Mmap = 0x4705da, Protect = 0x47068a, Advice = 0x470581, Unmap = 0x4706d1, Clone = 0x479889;
    // Independent copied values only; no scheduler, owned files, Completion,
    // original native image or actual guest capture is manufactured here.
    private static string Data(Action<byte[]>? mutate = null)
    {
        byte[] data = new byte[112];
        void Word(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, 8), value);
        Word(0, Tls);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x30, 4), 2);
        Word(0x48, Base);
        Word(0x50, 0x6000);
        Word(0x58, Top);
        Word(0x60, 0x4000);
        Word(0x68, 0x1000);
        mutate?.Invoke(data);
        return Convert.ToHexString(data).ToLowerInvariant();
    }

    private static void Call(List<string> rows, ulong nr, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong result, ulong rip)
    {
        int ordinal = rows.Count(r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal)) + 1;
        rows.Add($"M16_008_CALL identity={Main} nr={nr} a0={a0} a1={a1} a2={a2} a3={a3} a4={a4} a5=0 rip={rip} rsp=1879048128 flags=514 fs=0 gs=0 clock={ordinal} valueBits={result} action=1 status=1 ordinal={ordinal}" + $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={rip} returnRsp=1879048128 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={result}");
    }

    private static string[] Rows(Mutation mutation = Mutation.None)
    {
        List<string> rows = [];
        Call(rows, 9, 0, 0x6000, 0, 0x22, ulong.MaxValue, Base, Mmap);
        Call(rows, 10, Base + 0x1000, 0x5000, 3, 0, 0, 0, Protect);
        Call(rows, 56, 0x7d0f00, Sp, Tls + 0x30, ClearTid, Tls, 2, Clone);
        rows.Add($"M16_016_PTHREAD identity={Main} ordinal=3 child={Child} childTid=2 tls={Tls} sp={Sp} parentTid={Tls + 0x30} clearTid={ClearTid} rip={Clone} bytes=112 data={Data()}");
        rows.Add($"M16_008_REAP old={Child} next={Child + (1UL << 32)} context=zero stack=zero slot=zero");
        switch (mutation)
        {
            case Mutation.RetainThenDiscard:
                Call(rows, 10, Base + 0x1000, 0x5000, 0, 0, 0, 0, Protect);
                Call(rows, 28, Base, 0x6000, 4, 0, 0, 0, Advice);
                break;
            case Mutation.Fixed:
                Call(rows, 9, Base + 0x2000, 0x1000, 3, 0x32, ulong.MaxValue, Base + 0x2000, Mmap);
                break;
            // Native mremap operates on one complete protection/backing region;
            // the PROT_NONE guard and RW suffix are separate regions.
            case Mutation.Shrink:
                Call(rows, 25, Base + 0x1000, 0x5000, 0x3000, 0, 0, Base + 0x1000, 0x400202);
                break;
            case Mutation.Grow:
                Call(rows, 25, Base + 0x1000, 0x5000, 0x7000, 0, 0, Base + 0x1000, 0x400202);
                break;
            case Mutation.Hole:
                Call(rows, 11, Base + 0x2000, 0x1000, 0, 0, 0, 0, Unmap);
                break;
        }

        rows.Add("M16_008_OWNER_END reaped=2 transfers=8 irqs=1 exit=99 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        return rows.ToArray();
    }

    [HostTest("quota pthread copied raw header joins original clone and allocation indices with immutable values")]
    private static void HeaderAndCopies()
    {
        var rows = Rows();
        var before = rows.ToArray();
        var result = QemuTranscripts.InspectNativeAotServiceQuotaPthreadsForTests(rows);
        HostCheck.Equal(1, result.Threads.Count);
        var thread = result.Threads[0];
        HostCheck.True(thread.Identity == Main && thread.Ordinal == 3 && thread.ChildIdentity == Child && thread.Tid == 2 && thread.Tls == Tls);
        HostCheck.True(thread.StackStart == Base + 0x1000 && thread.StackBytes == 0x4000 && thread.GuardBytes == 0x1000 && thread.MappingStart == Base && thread.MappingBytes == 0x6000);
        HostCheck.True(thread.AllocationRecordIndex == 0 && thread.CallRecordIndex == 2 && thread.RecordIndex == 3 && thread.ReapRecordIndex == 4);
        HostCheck.True(result.PeakStackBackedBytes == 0x4000 && result.PeakPthreadBackedBytes == 0x5000 && result.Snapshots.Any(s => s.StackBackedBytes == 0x4000 && s.GuardBackedBytes == 0 && s.TailBackedBytes == 0x1000));
        HostCheck.True(rows.SequenceEqual(before));
        rows[thread.RecordIndex] = "changed after inspection";
        HostCheck.Equal(thread, result.Threads[0]);
        bool immutable = false;
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaPthread>)result.Threads).Clear();
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
        FinalZero(result.Snapshots[^1], before.Length - 1);
        immutable = false;
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaPthreadSnapshot>)result.Snapshots).Clear();
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
    }

    [HostTest("quota pthread copied backing survives reap and PROT_NONE until real DONTNEED and owner zero")]
    private static void RetainedBacking()
    {
        var rows = Rows(Mutation.RetainThenDiscard);
        var result = QemuTranscripts.InspectNativeAotServiceQuotaPthreadsForTests(rows);
        foreach (int at in new[]
        {
            4,
            5
        }

        )
        {
            var snapshot = result.Snapshots.Single(s => s.RecordIndex == at);
            HostCheck.True(snapshot.DynamicBackedBytes == 0x5000 && snapshot.PthreadBackedBytes == 0x5000 && snapshot.StackBackedBytes == 0x4000 && snapshot.GuardBackedBytes == 0 && snapshot.TailBackedBytes == 0x1000 && snapshot.RetainedAfterReapBytes == 0x5000);
        }

        FinalZero(result.Snapshots.Single(s => s.RecordIndex == 6), 6);
        FinalZero(result.Snapshots[^1], rows.Length - 1);
    }

    [HostTest("quota pthread copied fixed replacement shrink and holes revoke only old allocation backing")]
    private static void AllocationMutations()
    {
        foreach (var (mutation, dynamicBytes, pthreadBytes, stackBytes, tailBytes) in new[]
        {
            (Mutation.Fixed, 0x5000UL, 0x4000UL, 0x3000UL, 0x1000UL),
            (Mutation.Shrink, 0x3000UL, 0x3000UL, 0x3000UL, 0UL),
            (Mutation.Hole, 0x4000UL, 0x4000UL, 0x3000UL, 0x1000UL),
            (Mutation.Grow, 0x7000UL, 0x7000UL, 0x4000UL, 0x3000UL)
        }

        )
        {
            var rows = Rows(mutation);
            var result = QemuTranscripts.InspectNativeAotServiceQuotaPthreadsForTests(rows);
            var snapshot = result.Snapshots.Single(s => s.RecordIndex == 5);
            HostCheck.True(snapshot.DynamicBackedBytes == dynamicBytes && snapshot.PthreadBackedBytes == pthreadBytes && snapshot.StackBackedBytes == stackBytes && snapshot.TailBackedBytes == tailBytes && snapshot.GuardBackedBytes == 0 && snapshot.RetainedAfterReapBytes == pthreadBytes);
            HostCheck.True(result.PeakStackBackedBytes == 0x4000 && result.PeakPthreadBackedBytes == Math.Max(0x5000UL, pthreadBytes));
            FinalZero(result.Snapshots[^1], rows.Length - 1);
        }
    }

    [HostTest("quota pthread cached allocation is counted once across generations and invalid advice preserves backing")]
    private static void CachedGenerationAndAdvice()
    {
        var rows = Rows().ToList();
        string owner = rows[^1];
        rows.RemoveAt(rows.Count - 1);
        Call(rows, 28, Base, 0x6000, 8, 0, 0, unchecked((ulong)-22L), Advice);
        int failed = rows.Count - 1;
        Call(rows, 56, 0x7d0f00, Sp, Tls + 0x30, ClearTid, Tls, 3, Clone);
        string data = Data(b => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x30, 4), 3));
        ulong next = Child + (1UL << 32);
        rows.Add($"M16_016_PTHREAD identity={Main} ordinal=5 child={next} childTid=3 tls={Tls} sp={Sp} parentTid={Tls + 0x30} clearTid={ClearTid} rip={Clone} bytes=112 data={data}");
        int reborn = rows.Count - 1;
        rows.Add($"M16_008_REAP old={next} next={next + (1UL << 32)} context=zero stack=zero slot=zero");
        rows.Add(owner.Replace("reaped=2", "reaped=3", StringComparison.Ordinal));
        var result = QemuTranscripts.InspectNativeAotServiceQuotaPthreadsForTests(rows.ToArray());
        HostCheck.True(result.Threads.Count == 2 && result.Threads[0].AllocationRecordIndex == result.Threads[1].AllocationRecordIndex && result.PeakPthreadBackedBytes == 0x5000);
        HostCheck.True(result.Snapshots.Single(s => s.RecordIndex == failed).RetainedAfterReapBytes == 0x5000 && result.Snapshots.Single(s => s.RecordIndex == reborn).PthreadBackedBytes == 0x5000 && result.Snapshots.Single(s => s.RecordIndex == reborn).RetainedAfterReapBytes == 0);
        var impossible = rows.ToArray();
        impossible[failed] = impossible[failed].Replace($"valueBits={unchecked((ulong)-22L)}", "valueBits=0", StringComparison.Ordinal).Replace($"returnValueBits={unchecked((ulong)-22L)}", "returnValueBits=0", StringComparison.Ordinal);
        Reject(impossible);
    }

    [HostTest("quota pthread copied seam rejects missing duplicate orphan malformed raw and mismatched provenance")]
    private static void Refusals()
    {
        var valid = Rows();
        Reject(valid.Where((_, i) => i != 3).ToArray());
        var duplicate = valid.ToList();
        duplicate.Insert(3, valid[3]);
        Reject(duplicate.ToArray());
        var orphan = valid.ToList();
        string tcb = orphan[3];
        orphan.RemoveAt(3);
        orphan.Insert(2, tcb);
        Reject(orphan.ToArray());
        foreach (var (oldValue, newValue) in new[]
        {
            ("bytes=112", "bytes=111"),
            ("ordinal=3", "ordinal=2"),
            ($"child={Child}", $"child={Main}"),
            ("childTid=2", "childTid=3"),
            ($"tls={Tls}", $"tls={Tls + 8}"),
            ($"sp={Sp}", $"sp={Sp + 8}"),
            ($"parentTid={Tls + 0x30}", $"parentTid={Tls + 0x38}"),
            ($"clearTid={ClearTid}", $"clearTid={ClearTid + 4}"),
            ($"rip={Clone}", $"rip={Clone + 1}"),
            ("data=", "extra=1 data=")
        }

        )
            Change(valid, 3, oldValue, newValue);
        Change(valid, 2, $"rip={Clone}", $"rip={Clone + 1}");
        Change(valid, 0, $"rip={Mmap}", $"rip={Mmap + 1}");
        Change(valid, 3, "data=" + Data(), "data=g" + Data()[1..]);
        Change(valid, 3, "data=" + Data(), "data=" + Data()[..^2]);
        Change(valid, 3, "data=" + Data(), "data=" + Data() + "00");
        foreach (Action<byte[]> corrupt in new Action<byte[]>[]
        {
            b => b[0] ^= 8,
            b => b[0x30] = 3,
            b => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0x48, 8), 0),
            b => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0x60, 8), 0x3000),
            b => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0x50, 8), ulong.MaxValue)
        }

        )
            Change(valid, 3, "data=" + Data(), "data=" + Data(corrupt));
        Change(valid, 4, $"old={Child}", $"old={Child + (1UL << 32)}");
        Change(valid, valid.Length - 1, "userBacked=0", "userBacked=4096");
        Reject([.. valid, valid[3].Replace("M16_016_PTHREAD ", "M16_016_PTHREADx ", StringComparison.Ordinal)]);
        Reject([]);
        Reject(null!);
        Reject([.. valid, ""]);
    }

    private static void FinalZero(QemuTranscripts.ServiceQuotaPthreadSnapshot value, int at) => HostCheck.True(value.RecordIndex == at && value.DynamicBackedBytes == 0 && value.PthreadBackedBytes == 0 && value.StackBackedBytes == 0 && value.GuardBackedBytes == 0 && value.TailBackedBytes == 0 && value.RetainedAfterReapBytes == 0);
    private static void Change(string[] rows, int at, string before, string after)
    {
        var changed = rows.ToArray();
        HostCheck.True(changed[at].Contains(before, StringComparison.Ordinal) && before != after);
        changed[at] = changed[at].Replace(before, after, StringComparison.Ordinal);
        Reject(changed);
    }

    private static void Reject(string[] rows)
    {
        bool refused = false;
        try
        {
            _ = QemuTranscripts.InspectNativeAotServiceQuotaPthreadsForTests(rows);
        }
        catch (ToolFailure)
        {
            refused = true;
        }

        HostCheck.True(refused);
    }
}
