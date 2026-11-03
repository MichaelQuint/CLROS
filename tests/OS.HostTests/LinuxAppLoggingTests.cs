using System.Text;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using Budget = OS.Kernel.Compatibility.LinuxX64.LinuxAppLogBudget;

internal static unsafe partial class LinuxAppLoggingTests
{
    private const ulong User = 0x100000000, Data = User + 4096, Vectors = User + 20480;
    [HostTest("m10-005 log quota rejects defaults invalid records and reinitialization without side effects")]
    private static void Admission()
    {
        Budget budget = default;
        HostCheck.Equal(Budget.ChargeStatus.Rejected, budget.Charge(1, 1));
        HostCheck.True(!budget.Initialize(0, 1) && !budget.Initialize(1, 0) && !budget.IsInitialized);
        HostCheck.True(budget.Initialize(10, 2) && !budget.Initialize(100, 100));
        foreach (var record in new[]
        {
            (0U, 1U),
            (3U, 1U),
            (1U, 0U),
            (2U, 8193U)
        }

        )
            HostCheck.Equal(Budget.ChargeStatus.Rejected, budget.Charge(record.Item1, record.Item2));
        HostCheck.Equal(0U, budget.ChargedBytes);
        HostCheck.Equal(Budget.ChargeStatus.Accepted, budget.Charge(1, 4));
        HostCheck.Equal(Budget.ChargeStatus.Accepted, budget.Charge(2, 6));
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, budget.Charge(1, 1));
        HostCheck.Equal(10U, budget.ChargedBytes);
        HostCheck.Equal(2U, budget.ChargedRecords);
        HostCheck.Equal(4U, budget.StdoutChargedBytes);
        HostCheck.Equal(6U, budget.StderrChargedBytes);
        Budget records = default;
        HostCheck.True(records.Initialize(100, 1));
        HostCheck.Equal(Budget.ChargeStatus.Accepted, records.Charge(1, 1));
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, records.Charge(2, 1));
        HostCheck.Equal(1U, records.ChargedBytes);
        HostCheck.Equal(1U, records.ChargedRecords);
    }

    [HostTest("m10-005 actual descriptor write and writev retain partial counts ordering and errno before quota")]
    private static void DescriptorWrites()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        LoggingModel model = new()
        {
            Descriptor = *owner.Context
        };
        HostCheck.True(model.Budget.Initialize(16384, 2));
        Attach(owner, &model);
        owner.Begin();
        new Span<byte>(owner.Memory.At(Data), 16384).Fill((byte)'a');
        HostCheck.Equal(8192L, owner.Call(1, 1, Data, 16384));
        HostCheck.Equal(8192U, model.Descriptor.SinkBytes);
        HostCheck.True(new ReadOnlySpan<byte>(model.Descriptor.Sink, 8192).IndexOfAnyExcept((byte)'a') == -1);
        new Span<byte>(owner.Memory.At(Data), 4000).Fill((byte)'b');
        new Span<byte>(owner.Memory.At(Data + 4000), 6000).Fill((byte)'c');
        ulong* vectors = (ulong*)owner.Memory.At(Vectors);
        vectors[0] = Data;
        vectors[1] = 4000;
        vectors[2] = Data + 4000;
        vectors[3] = 6000;
        HostCheck.Equal(8192L, owner.Call(20, 2, Vectors, 2));
        HostCheck.True(new ReadOnlySpan<byte>(model.Descriptor.Sink, 4000).IndexOfAnyExcept((byte)'b') == -1);
        HostCheck.True(new ReadOnlySpan<byte>(model.Descriptor.Sink + 4000, 4192).IndexOfAnyExcept((byte)'c') == -1);
        HostCheck.Equal(8192U, model.Budget.StdoutChargedBytes);
        HostCheck.Equal(8192U, model.Budget.StderrChargedBytes);
        uint calls = model.Descriptor.SinkCalls;
        HostCheck.Equal(-9L, owner.Call(1, 99, Data, 1));
        HostCheck.Equal(-14L, owner.Call(1, 1, User + 32768, 1));
        HostCheck.Equal(0L, owner.Call(1, 1, ulong.MaxValue, 0));
        HostCheck.Equal(calls, model.Descriptor.SinkCalls);
        HostCheck.Equal(-5L, owner.Call(1, 1, Data, 1));
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, model.LastCharge);
        HostCheck.Equal(2U, model.DeliveredRecords);
        HostCheck.Equal(16384U, model.Budget.ChargedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0) == -1);
    }

    [HostTest("m10-005 failed serial delivery consumes its charge while rejected output never reaches the sink")]
    private static void DeliveryFailure()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        LoggingModel model = new()
        {
            Descriptor = *owner.Context,
            FailDelivery = true
        };
        HostCheck.True(model.Budget.Initialize(6, 10));
        Attach(owner, &model);
        owner.Begin();
        owner.Text(Data, "abc");
        HostCheck.Equal(-5L, owner.Call(1, 1, Data, 3));
        HostCheck.Equal(3U, model.Budget.ChargedBytes);
        HostCheck.Equal(0U, model.DeliveredRecords);
        model.FailDelivery = false;
        HostCheck.Equal(3L, owner.Call(1, 2, Data, 3));
        HostCheck.Equal("abc", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(model.Descriptor.Sink, 3)));
        HostCheck.Equal(0L, owner.Call(20, 1, Vectors, 0));
        HostCheck.Equal(-5L, owner.Call(1, 1, Data, 1));
        HostCheck.Equal(6U, model.Budget.ChargedBytes);
        HostCheck.Equal(2U, model.Budget.ChargedRecords);
        HostCheck.Equal(1U, model.DeliveredRecords);
    }

    [HostTest("m10-005 externally serialized concurrent writers share one process quota")]
    private static void ConcurrentWriters()
    {
        Budget budget = default;
        HostCheck.True(budget.Initialize(1000, 1000));
        object gate = new();
        int accepted = 0, exhausted = 0;
        Task.WaitAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
                lock (gate)
                {
                    var status = budget.Charge((uint)(worker % 2 + 1), 1);
                    if (status == Budget.ChargeStatus.Accepted)
                        accepted++;
                    else if (status == Budget.ChargeStatus.QuotaExceeded)
                        exhausted++;
                    else
                        throw new InvalidOperationException("unexpected log charge rejection");
                }
        })).ToArray());
        HostCheck.Equal(1000, accepted);
        HostCheck.Equal(7000, exhausted);
        HostCheck.Equal(1000U, budget.ChargedBytes);
        HostCheck.Equal(1000U, budget.ChargedRecords);
        HostCheck.Equal(1000U, budget.StdoutChargedBytes + budget.StderrChargedBytes);
    }

    [HostTest("m10-005 log quota reaches uint byte boundary exactly without wrapping or allocations")]
    private static void ArithmeticBoundary()
    {
        Budget warm = default;
        warm.Initialize(1, 1);
        warm.Charge(1, 1);
        Budget budget = default;
        HostCheck.True(budget.Initialize(uint.MaxValue, uint.MaxValue));
        bool accepted = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint i = 0; i < uint.MaxValue / 8192; i++)
            accepted &= budget.Charge(1, 8192) == Budget.ChargeStatus.Accepted;
        accepted &= budget.Charge(2, uint.MaxValue % 8192) == Budget.ChargeStatus.Accepted;
        var exhausted = budget.Charge(1, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(accepted);
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, exhausted);
        HostCheck.Equal(uint.MaxValue, budget.ChargedBytes);
        HostCheck.Equal(524288U, budget.ChargedRecords);
        HostCheck.Equal(0L, allocated);
    }

    [HostTest("m10-005 copied log observation joins split records eight writers writev and later EBADF")]
    private static void CopiedObservation()
    {
        LinuxAppLogObservation logs = default;
        HostCheck.True(!logs.Complete && !logs.Failed);
        HostCheck.True(LogText(ref logs, 2, 10, "CLROS_APP_ENTRY minimal.api.smoke\n"));
        HostCheck.True(LogCall(ref logs, 10, 1, 2, 32));
        HostCheck.True(LogText(ref logs, 1, 10, "      Application started.\n"));
        HostCheck.True(LogCall(ref logs, 10, 1, 1, 27));
        for (int worker = 0; worker < 8; worker++)
            for (int record = 0; record < 4; record++)
            {
                ulong identity = (ulong)worker + 100;
                string row = $"CLROS_LOG fd=1 worker={worker} record={record}\n";
                HostCheck.True(LogText(ref logs, 1, identity, row[..9]));
                HostCheck.True(LogText(ref logs, 1, identity, row[9..^1]));
                HostCheck.True(LogCall(ref logs, identity, 1, 1, 32));
                HostCheck.True(LogText(ref logs, 1, identity, "\n"));
                HostCheck.True(LogCall(ref logs, identity, 1, 1, 1));
                HostCheck.True(LogText(ref logs, 2, identity, row.Replace("fd=1", "fd=2")[..^1]));
                HostCheck.True(LogText(ref logs, 2, identity, "\n"));
                HostCheck.True(LogCall(ref logs, identity, 20, 2, 33));
            }

        HostCheck.Equal(32U, logs.StdoutRecords);
        HostCheck.Equal(32U, logs.StderrRecords);
        HostCheck.Equal(32U, logs.WritevRecords);
        HostCheck.True(!logs.Complete);
        HostCheck.True(LogCall(ref logs, 10, 20, 1234, -9) && logs.Complete);
        HostCheck.True(!LogCall(ref logs, 10, 20, 1234, -9) && logs.Failed && !logs.Complete);
    }

    [HostTest("m10-005 copied log observation rejects malformed duplicate foreign and unjoined records permanently")]
    private static void InvalidCopiedObservation()
    {
        foreach (string text in new[]
        {
            "CLROS_LOG fd=2 worker=0 record=0\n",
            "CLROS_LOG fd=1 worker=8 record=0\n",
            "CLROS_LOG fd=1 worker=0 record=4\n",
            "CLROS_LOG fd=1 worker=0 record=0\r\n",
            "CLROS_LOG fd=1 worker=0 record=0 extra\n"
        }

        )
        {
            LinuxAppLogObservation logs = default;
            HostCheck.True(!LogText(ref logs, 1, 100, text) && logs.Failed && !logs.Complete);
            HostCheck.True(!LogText(ref logs, 1, 100, "CLROS_LOG fd=1 worker=0 record=0\n"));
        }

        foreach (int mode in Enumerable.Range(0, 4))
        {
            LinuxAppLogObservation logs = default;
            HostCheck.True(LogText(ref logs, 1, 100, "CLROS_LOG fd=1 worker=0 record=0\n"));
            bool accepted = mode switch
            {
                0 => LogText(ref logs, 1, 100, "CLROS_LOG fd=1 worker=0 record=0\n"),
                1 => LogText(ref logs, 1, 100, "CLROS_LOG fd=1 worker=1 record=0\n"),
                2 => LogText(ref logs, 2, 101, "CLROS_LOG fd=2 worker=0 record=0\n"),
                _ => LogCall(ref logs, 10, 20, 1234, -9)};
            HostCheck.True(!accepted && logs.Failed && !logs.Complete);
        }

        LinuxAppLogObservation changed = default;
        HostCheck.True(LogText(ref changed, 1, 100, "CLROS_LOG"));
        HostCheck.True(!LogText(ref changed, 1, 101, " fd=1 worker=0 record=0\n") && changed.Failed);
        foreach (var call in new[]
        {
            (101UL, 20UL, 2UL, 33L),
            (100UL, 1UL, 2UL, 33L),
            (100UL, 20UL, 1UL, 33L),
            (100UL, 20UL, 2UL, 32L),
            (100UL, 20UL, 2UL, -5L)
        }

        )
        {
            LinuxAppLogObservation logs = default;
            HostCheck.True(LogText(ref logs, 1, 100, "CLROS_LOG fd=1 worker=0 record=0\n"));
            HostCheck.True(LogCall(ref logs, 100, 1, 1, 33));
            HostCheck.True(LogText(ref logs, 2, 100, "CLROS_LOG fd=2 worker=0 record=0\n"));
            HostCheck.True(!LogCall(ref logs, call.Item1, call.Item2, call.Item3, call.Item4) && logs.Failed);
        }

        LinuxAppLogObservation pending = default;
        HostCheck.True(LogText(ref pending, 1, 100, "CLROS_LOG fd=1 worker=0 record=0\n"));
        HostCheck.True(LogCall(ref pending, 100, 1, 1, 33));
        HostCheck.True(LogText(ref pending, 2, 100, "CLROS_LOG fd=2 worker=0 record=0\n"));
        HostCheck.True(!LogText(ref pending, 2, 100, "CLROS_LOG fd=2 worker=0 record=1\n"));
        foreach (int mode in Enumerable.Range(0, 5))
        {
            LinuxAppLogObservation logs = default;
            byte value = 65;
            HostCheck.True(!logs.Observe(mode == 0 ? 3U : 1U, mode == 1 ? 0UL : 1UL, mode == 2 ? null : &value, mode == 3 ? 0U : mode == 4 ? 8193U : 1U) && logs.Failed);
        }

        LinuxAppLogObservation noWriter = default;
        HostCheck.True(!LogCall(ref noWriter, 0, 1, 1, 1) && noWriter.Failed);
    }

    [HostTest("m10-005 copied stderr stays in one syscall and preserves each worker source order")]
    private static void CopiedObservationCallOrder()
    {
        const string output = "CLROS_LOG fd=1 worker=0 record=0\n", error = "CLROS_LOG fd=2 worker=0 record=0\n";
        foreach (int split in new[]
        {
            1,
            8,
            9,
            32
        }

        )
        {
            LinuxAppLogObservation logs = default;
            HostCheck.True(LogText(ref logs, 1, 100, output) && LogCall(ref logs, 100, 1, 1, 33));
            HostCheck.True(LogText(ref logs, 2, 100, error[..split]));
            bool firstCall = LogCall(ref logs, 100, 20, 2, split);
            if (split < 9)
                HostCheck.True(firstCall && !LogText(ref logs, 2, 100, error[split..]));
            else
                HostCheck.True(!firstCall);
            HostCheck.True(logs.Failed && !logs.Complete && logs.WritevRecords == 0);
        }

        LinuxAppLogObservation extra = default;
        HostCheck.True(LogText(ref extra, 1, 100, output) && LogCall(ref extra, 100, 1, 1, 33));
        HostCheck.True(LogText(ref extra, 2, 100, error + new string ('x', 32)));
        HostCheck.True(!LogCall(ref extra, 100, 20, 2, 33) && extra.Failed);
        foreach (int mode in Enumerable.Range(0, 3))
        {
            LinuxAppLogObservation logs = default;
            if (mode != 0)
                HostCheck.True(LogText(ref logs, 1, 100, output) && LogCall(ref logs, 100, 1, 1, 33));
            string reordered = mode switch
            {
                0 => error,
                1 => output.Replace("record=0", "record=1"),
                _ => error.Replace("record=0", "record=1")};
            HostCheck.True(!LogText(ref logs, mode == 1 ? 1U : 2U, 100, reordered) && logs.Failed);
        }
    }

    [HostTest("m10-005 copied log observation uses no managed allocations")]
    private static void CopiedObservationAllocation()
    {
        byte[] row = Encoding.ASCII.GetBytes("CLROS_LOG fd=1 worker=0 record=0\n");
        LinuxAppLogObservation warm = default, logs = default;
        fixed (byte* source = row)
        {
            warm.Observe(1, 100, source, 33);
            LogCall(ref warm, 100, 1, 1, 33);
            bool accepted = true;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint worker = 0; worker < 8; worker++)
                for (uint record = 0; record < 4; record++)
                {
                    source[22] = (byte)('0' + worker);
                    source[31] = (byte)('0' + record);
                    source[13] = (byte)'1';
                    accepted &= logs.Observe(1, worker + 100, source, 33);
                    accepted &= LogCall(ref logs, worker + 100, 1, 1, 33);
                    source[13] = (byte)'2';
                    accepted &= logs.Observe(2, worker + 100, source, 33);
                    accepted &= LogCall(ref logs, worker + 100, 20, 2, 33);
                }

            accepted &= LogCall(ref logs, 10, 20, 1234, -9);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            HostCheck.True(accepted && logs.Complete);
            HostCheck.Equal(0L, allocated);
        }
    }

    [HostTest("m10-005 raw copied logging joins startup both streams catalog writers and actual EBADF")]
    private static void RawCopiedLogging()
    {
        byte[] raw = LoggingRaw("none"), before = (byte[])raw.Clone();
        var report = QemuTranscripts.InspectNativeAotWebLoggingForTests(raw);
        HostCheck.Equal(32U, report.StdoutRecords);
        HostCheck.Equal(32U, report.StderrRecords);
        HostCheck.Equal(8U, report.Writers);
        HostCheck.Equal(32U, report.WritevRecords);
        HostCheck.Equal(9U, report.InvalidErrno);
        HostCheck.True(raw.AsSpan().SequenceEqual(before));
        // The independent borrowed startup seam keeps its narrower contract.
        var startup = QemuTranscripts.InspectNativeAotWebStartupForTests(raw);
        HostCheck.Equal(0x100000001UL, startup.WriterIdentity);
        HostCheck.Equal(1, startup.StartupWriteRecords);
    }

    [HostTest("m10-005 raw copied logging rejects malformed missing forged cross-call and reordered evidence")]
    private static void InvalidRawCopiedLogging()
    {
        foreach (string mutation in new[]
        {
            "missing_record",
            "malformed",
            "duplicate_record",
            "foreign_writer",
            "same_writer",
            "record_order",
            "stream_order",
            "short_write",
            "scalar_count",
            "short_vector",
            "vector_count",
            "forged_return",
            "cross_call_1",
            "cross_call_8",
            "cross_call_9",
            "cross_call_32",
            "early_invalid",
            "wrong_errno",
            "missing_invalid",
            "repeated_invalid",
            "missing_start",
            "duplicate_start",
            "early_start",
            "late_start",
            "missing_ready",
            "duplicate_ready",
            "duplicate_summary",
            "early_summary",
            "late_summary",
            "pass_gap",
            "unknown",
            "failure",
            "smoke_failure",
            "bad_frame_length",
            "orphan_stdout",
            "orphan_stderr",
            "summary_stdout",
            "summary_stderr",
            "summary_writers",
            "summary_writev",
            "summary_invalidErrno"
        }

        )
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.InspectNativeAotWebLoggingForTests(LoggingRaw(mutation));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected, mutation);
        }
    }

    [HostTest("m10-005 raw quota joins coalesced callbacks EIO and later denials")]
    private static void RawQuotaLogging()
    {
        byte[] raw = LoggingRaw("quota_none"), before = (byte[])raw.Clone();
        var report = QemuTranscripts.InspectNativeAotWebLogQuotaForTests(raw);
        HostCheck.Equal(64U, report.CopiedRecords);
        HostCheck.Equal(3U, report.StdoutRecords);
        HostCheck.Equal(2U, report.StderrRecords);
        HostCheck.Equal(1U, report.DeniedCalls);
        HostCheck.Equal(1U, report.EioCalls);
        HostCheck.True(raw.AsSpan().SequenceEqual(before));
        foreach (string mutation in new[]
        {
            "counter",
            "identity",
            "result",
            "vector",
            "denial_missing",
            "pending",
            "after_copy",
            "summary",
            "early",
            "short_scalar"
        }

        )
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.InspectNativeAotWebLogQuotaForTests(LoggingRaw("quota_" + mutation));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected, mutation);
        }

        bool normalRejected = false;
        try
        {
            QemuTranscripts.InspectNativeAotWebLoggingForTests(raw);
        }
        catch (ToolFailure)
        {
            normalRejected = true;
        }

        HostCheck.True(normalRejected);
        bool quotaRejected = false;
        try
        {
            QemuTranscripts.InspectNativeAotWebLogQuotaForTests(LoggingRaw("none"));
        }
        catch (ToolFailure)
        {
            quotaRejected = true;
        }

        HostCheck.True(quotaRejected);
    }

    private static byte[] LoggingRaw(string mutation)
    {
        const ulong writer = 0x100000001, binder = 0x100000005, firstWorker = 0x100000006;
        const string start = "TEST_START m10.005\r\n", ready = "CLROS_M10_005_READY\r\n", pass = "TEST_PASS m10.005\r\n";
        const string summary = "M10_005_LOGS stdout=32 stderr=32 writers=8 writev=32 invalidErrno=9\r\n";
        string block = summary + ready + pass;
        Dictionary<ulong, int> ordinals = [];
        string Call(ulong identity, int nr, int fd, long result, int count, bool forgedReturn = false)
        {
            int ordinal = ordinals.GetValueOrDefault(identity) + 1;
            ordinals[identity] = ordinal;
            ulong bits = unchecked((ulong)result), returned = forgedReturn ? bits - 1 : bits;
            return $"M16_008_CALL identity={identity} nr={nr} a0={fd} a1=4096 a2={count} a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={bits} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={returned}\r\n";
        }

        string Frame(int fd, string text) => $"M16_005_WRITE fd={fd} bytes={Encoding.ASCII.GetByteCount(text)} data={text}\r\n";
        const string prefix = "CLROS_APP_ENTRY minimal.api.smoke\nCLROS_APP_CCTOR minimal.api.smoke\n" + "CLROS_APP_CTOR minimal.api.smoke\nCLROS_APP_BUILT minimal.api.smoke\n";
        var text = new StringBuilder("TEST_START m10.004\r\nTEST_START m16-009\r\nTEST_START m10.006\r\n");
        text.Append(Frame(2, prefix)).Append($"M10_006_STARTUP writer={writer} stages=4\r\n").Append(Call(writer, 1, 2, prefix.Length, prefix.Length));
        text.Append($"M10_006_BIND identity={binder} fd=6\r\n").Append(Call(binder, 49, 6, 0, 16));
        text.Append("M10_004_LISTEN fd=6 address=0 port=8080 owner=live\r\nCLROS_M10_004_READY\r\nTEST_PASS m10.004\r\nCLROS_M10_006_READY\r\nTEST_PASS m10.006\r\n");
        const string framework = "info: logging fixture\n";
        text.Append(Frame(1, framework)).Append(Call(writer, 1, 1, framework.Length, framework.Length));
        if (mutation.StartsWith("quota_", StringComparison.Ordinal))
        {
            for (int i = 0; i < 57; i++)
                text.Append(Frame(1, framework)).Append(Call(writer, 1, 1, framework.Length, framework.Length));
            string row = "CLROS_LOG fd=1 worker=0 record=0\n", error = row.Replace("fd=1", "fd=2", StringComparison.Ordinal);
            text.Append(Frame(1, row)).Append(start).Append(Call(firstWorker, 1, 1, 33, mutation == "quota_short_scalar" ? 34 : 33));
            text.Append(Frame(2, error)).Append(Call(firstWorker, 20, 2, 33, 2));
            text.Append(Frame(1, row.Replace("record=0", "record=1", StringComparison.Ordinal))).Append(Call(firstWorker, 1, 1, 33, 33));
            text.Append(Frame(2, error.Replace("record=0", "record=1", StringComparison.Ordinal))).Append(Call(firstWorker, 20, 2, 33, 2));
            text.Append(Frame(1, row.Replace("record=0", "record=2", StringComparison.Ordinal))).Append(Call(firstWorker, 1, 1, 33, 33));
            int copiedBytes = prefix.Length + framework.Length * 58 + 165;
            string Deny(int fd, int bytes, int copied, ulong identity = firstWorker) => $"M10_005_QUOTA_DENY identity={identity} fd={fd} bytes={bytes} chargedRecords=64 chargedBytes={copiedBytes} copiedCallBytes={copied}\r\n";
            string eio = Deny(2, 33, 0, mutation == "quota_identity" ? firstWorker + 1 : firstWorker);
            if (mutation == "quota_counter")
                eio = eio.Replace("chargedRecords=64", "chargedRecords=63", StringComparison.Ordinal);
            string quotaSummary = $"M10_005_LOGS case=records64 copiedRecords=64 copiedBytes={copiedBytes} stdout=3 stderr=2 deniedCalls=1 eioCalls=1\r\n";
            if (mutation == "quota_summary")
                quotaSummary = quotaSummary.Replace("eioCalls=1", "eioCalls=2", StringComparison.Ordinal);
            if (mutation != "quota_denial_missing")
                text.Append(eio);
            text.Append(quotaSummary).Append(ready).Append(pass).Append(Call(firstWorker, 20, 2, mutation == "quota_result" ? 32 : -5, mutation == "quota_vector" ? 0 : 2));
            text.Append(Deny(1, 33, 0));
            if (mutation != "quota_pending")
                text.Append(Call(firstWorker, 1, 1, -5, 33));
            if (mutation == "quota_after_copy")
                text.Append(Frame(1, row)).Append(Call(firstWorker, 1, 1, 33, 33));
            if (mutation == "quota_early")
                text.Replace(start, "").Insert(text.ToString().IndexOf("M10_006_BIND", StringComparison.Ordinal), start);
            return Encoding.ASCII.GetBytes(text.ToString());
        }

        if (mutation == "early_invalid")
            text.Append(Call(binder, 20, 1234, -9, 1));
        string firstBody = "", firstOutputCall = "", lastErrorCall = "";
        for (int worker = 0; worker < 8; worker++)
            for (int step = 0; step < 4; step++)
            {
                int record = mutation == "record_order" && worker == 0 && step < 2 ? 1 - step : step;
                if (mutation == "missing_record" && worker == 7 && record == 3)
                    continue;
                ulong identity = mutation == "same_writer" ? firstWorker : firstWorker + (uint)worker;
                bool first = worker == 0 && step == 0;
                string output = $"CLROS_LOG fd=1 worker={worker} record={record}\n", error = output.Replace("fd=1", "fd=2", StringComparison.Ordinal);
                if (first && mutation == "malformed")
                    output = output.Replace("record=0", "record=4", StringComparison.Ordinal);
                if (first && mutation == "stream_order")
                    text.Append(Frame(2, error)).Append(Call(identity, 20, 2, 33, 2));
                text.Append(Frame(1, output[..^1]));
                if (first)
                    firstBody = Frame(1, output[..^1]);
                text.Append(Call(identity, 1, 1, first && mutation == "short_write" ? 31 : 32, first && mutation == "scalar_count" ? 31 : 32));
                text.Append(Frame(1, "\n"));
                if (first)
                    text.Append(start);
                string outputCall = Call(identity, 1, 1, 1, 1);
                text.Append(outputCall);
                if (first)
                    firstOutputCall = outputCall;
                ulong errorWriter = first && mutation == "foreign_writer" ? identity + 1 : identity;
                if (first && mutation.StartsWith("cross_call_", StringComparison.Ordinal))
                {
                    int split = int.Parse(mutation[11..], System.Globalization.CultureInfo.InvariantCulture);
                    text.Append(Frame(2, error[..split])).Append(Call(errorWriter, 20, 2, split, 2));
                    text.Append(Frame(2, error[split..])).Append(Call(errorWriter, 20, 2, error.Length - split, 2));
                }
                else
                {
                    text.Append(Frame(2, error[..^1])).Append(Frame(2, "\n"));
                    lastErrorCall = Call(errorWriter, 20, 2, first && mutation == "short_vector" ? 32 : 33, first && mutation == "vector_count" ? 1 : 2, first && mutation == "forged_return");
                    text.Append(lastErrorCall);
                }

                if (first && mutation == "duplicate_record")
                    text.Append(Frame(1, output)).Append(Call(identity, 1, 1, 33, 33));
            }

        string invalidCall = Call(binder, 20, 1234, mutation == "wrong_errno" ? -5 : -9, 1);
        text.Append(block).Append(invalidCall);
        text.Append(Frame(1, framework)).Append(Call(writer, 1, 1, framework.Length, framework.Length));
        string raw = text.ToString();
        raw = mutation switch
        {
            "missing_invalid" => raw.Replace(invalidCall, "", StringComparison.Ordinal),
            "repeated_invalid" => raw + Call(binder, 20, 1234, -9, 1),
            "missing_start" => raw.Replace(start, "", StringComparison.Ordinal),
            "duplicate_start" => raw.Replace(start, start + start, StringComparison.Ordinal),
            "early_start" => raw.Replace(start, "", StringComparison.Ordinal).Replace(firstBody, start + firstBody, StringComparison.Ordinal),
            "late_start" => raw.Replace(start + firstOutputCall, firstOutputCall + start, StringComparison.Ordinal),
            "missing_ready" => raw.Replace(ready, "", StringComparison.Ordinal),
            "duplicate_ready" => raw.Replace(ready, ready + ready, StringComparison.Ordinal),
            "duplicate_summary" => raw.Replace(summary, summary + summary, StringComparison.Ordinal),
            "early_summary" => raw.Replace(block, "", StringComparison.Ordinal).Replace(lastErrorCall, block + lastErrorCall, StringComparison.Ordinal),
            "late_summary" => raw.Replace(block + invalidCall, invalidCall + block, StringComparison.Ordinal),
            "pass_gap" => raw.Replace(pass, pass + "M16_008_GAP\r\n", StringComparison.Ordinal),
            "unknown" => raw + "M10_005_UNKNOWN\r\n",
            "failure" => raw + "TEST_FAIL m10.005 forged\r\n",
            "smoke_failure" => raw + "OS_SMOKE_FAIL\r\n",
            "bad_frame_length" => raw.Replace(firstBody, firstBody.Replace("bytes=32", "bytes=31", StringComparison.Ordinal), StringComparison.Ordinal),
            "orphan_stdout" => raw + Frame(1, "x"),
            "orphan_stderr" => raw + Frame(2, "x"),
            _ => raw
        };
        if (mutation.StartsWith("summary_", StringComparison.Ordinal))
        {
            string field = mutation[8..], original = field is "stdout" or "stderr" or "writev" ? "32" : field == "writers" ? "8" : "9";
            raw = raw.Replace(field + "=" + original, field + "=1", StringComparison.Ordinal);
        }

        return Encoding.ASCII.GetBytes(raw);
    }

    private static bool LogText(ref LinuxAppLogObservation logs, uint fd, ulong identity, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* source = bytes)
            return logs.Observe(fd, identity, source, (uint)bytes.Length);
    }

    private static bool LogCall(ref LinuxAppLogObservation logs, ulong identity, ulong number, ulong fd, long value) => logs.RecordCall(identity, new LinuxSyscallFrame { Number = number, Argument0 = fd }, new(LinuxSyscallAction.Return, value));
    [HostTest("m10-005 real record quota preserves coalesced writev then EIO without refund or copied overflow")]
    private static void DescriptorQuotaControl()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        LoggingModel model = new()
        {
            Descriptor = *owner.Context,
            QuotaCase = true,
            Writer = 100
        };
        HostCheck.True(model.Budget.Initialize(Budget.DefaultByteLimit, Budget.QuotaRecordLimit));
        Attach(owner, &model);
        owner.Begin();
        for (int i = 0; i < 59; i++)
            HostCheck.Equal(10L, QuotaWrite(owner, ref model, "framework\n"));
        HostCheck.Equal(33L, QuotaWrite(owner, ref model, "CLROS_LOG fd=1 worker=0 record=0\n"));
        HostCheck.Equal(33L, QuotaWritev(owner, ref model, "CLROS_LOG fd=2 worker=0 record=0\n"));
        HostCheck.Equal(33L, QuotaWrite(owner, ref model, "CLROS_LOG fd=1 worker=0 record=1\n"));
        HostCheck.Equal(33L, QuotaWritev(owner, ref model, "CLROS_LOG fd=2 worker=0 record=1\n"));
        HostCheck.Equal(33L, QuotaWrite(owner, ref model, "CLROS_LOG fd=1 worker=0 record=2\n"));
        HostCheck.Equal(64U, model.Quota.CopiedRecords);
        HostCheck.Equal(755U, model.Quota.CopiedBytes);
        HostCheck.Equal(0U, model.Quota.DeniedCalls);
        HostCheck.Equal(0U, model.Quota.EioCalls);
        HostCheck.True(!model.Quota.Complete && !model.ObservationFailed);
        uint delivered = model.DeliveredRecords;
        HostCheck.Equal(-5L, QuotaWritev(owner, ref model, "CLROS_LOG fd=2 worker=0 record=2\n"));
        HostCheck.True(model.Quota.Complete && !model.ObservationFailed);
        HostCheck.Equal(1U, model.Quota.EioCalls);
        HostCheck.Equal(1U, model.Quota.DeniedCalls);
        HostCheck.Equal(-5L, QuotaWrite(owner, ref model, "CLROS_LOG fd=1 worker=0 record=3\n"));
        HostCheck.Equal(2U, model.Quota.EioCalls);
        HostCheck.Equal(2U, model.Quota.DeniedCalls);
        HostCheck.Equal(delivered, model.DeliveredRecords);
        HostCheck.Equal(64U, model.Budget.ChargedRecords);
        HostCheck.Equal(755U, model.Budget.ChargedBytes);
        HostCheck.True(model.Quota.Complete && !model.ObservationFailed);
        var unjoined = model.Quota;
        HostCheck.True(unjoined.Deny(1, 100, 33, 64, 755) && !unjoined.Complete);
        HostCheck.True(unjoined.RecordCall(100, new() { Number = 1, Argument0 = 1, Argument2 = 33 }, new(LinuxSyscallAction.Return, -5)) && unjoined.Complete);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0) == -1);
    }

    [HostTest("m10-005 quota rejects startup-only exhaustion mismatched charge identity result and copied overflow")]
    private static void InvalidQuotaControl()
    {
        LinuxAppLogQuotaObservation early = default;
        byte[] noise = "framework\n"u8.ToArray();
        fixed (byte* text = noise)
            for (int i = 0; i < 64; i++)
            {
                HostCheck.True(early.Observe(1, 100, text, 10));
                HostCheck.True(early.RecordCall(100, new() { Number = 1, Argument0 = 1, Argument2 = 10 }, new(LinuxSyscallAction.Return, 10)));
            }

        HostCheck.True(!early.Deny(1, 100, 10, 64, 640) && early.Failed && !early.Complete);
        LinuxAppLogQuotaObservation partial = default;
        fixed (byte* text = noise)
            for (int i = 0; i < 62; i++)
            {
                HostCheck.True(partial.Observe(1, 100, text, 10));
                HostCheck.True(partial.RecordCall(100, new() { Number = 1, Argument0 = 1, Argument2 = 10 }, new(LinuxSyscallAction.Return, 10)));
            }

        byte[] first = "CLROS_LOG fd=1 worker=0 record=0\n"u8.ToArray();
        byte[] second = "CLROS_LOG fd=2 worker=0 record=0\n"u8.ToArray();
        fixed (byte* row = first)
        {
            HostCheck.True(partial.Observe(1, 100, row, 33));
            HostCheck.True(partial.RecordCall(100, new() { Number = 1, Argument0 = 1, Argument2 = 33 }, new(LinuxSyscallAction.Return, 33)));
        }

        fixed (byte* row = second)
        {
            HostCheck.True(partial.Observe(2, 100, row, 32));
        }

        HostCheck.True(!partial.Deny(2, 100, 1, 64, 685) && partial.Failed);
        for (int mutation = 0; mutation < 6; mutation++)
        {
            using var owner = new LinuxDescriptorServicesTests.Owner();
            LoggingModel model = new()
            {
                Descriptor = *owner.Context,
                QuotaCase = true,
                Writer = 100
            };
            HostCheck.True(model.Budget.Initialize(Budget.DefaultByteLimit, Budget.QuotaRecordLimit));
            Attach(owner, &model);
            owner.Begin();
            for (int i = 0; i < 63; i++)
                HostCheck.Equal(10L, QuotaWrite(owner, ref model, "framework\n"));
            HostCheck.Equal(33L, QuotaWrite(owner, ref model, "CLROS_LOG fd=1 worker=0 record=0\n"));
            var quota = model.Quota;
            bool accepted;
            if (mutation < 2)
                accepted = quota.Deny(1, 100, 33, mutation == 0 ? 63U : 64U, mutation == 1 ? 662U : 663U);
            else
            {
                HostCheck.True(quota.Deny(1, 100, 33, 64, 663));
                if (mutation == 2)
                    accepted = quota.Deny(1, 100, 33, 64, 663);
                else if (mutation == 3)
                    accepted = quota.RecordCall(101, new() { Number = 1, Argument0 = 1, Argument2 = 33 }, new(LinuxSyscallAction.Return, -5));
                else if (mutation == 4)
                    accepted = quota.RecordCall(100, new() { Number = 1, Argument0 = 1, Argument2 = 33 }, new(LinuxSyscallAction.Return, 33));
                else
                    fixed (byte* text = noise)
                        accepted = quota.Observe(1, 100, text, 10);
            }

            HostCheck.True(!accepted && quota.Failed && !quota.Complete, "quota mutation " + mutation);
        }
    }

    private static long QuotaWrite(LinuxDescriptorServicesTests.Owner owner, ref LoggingModel model, string row)
    {
        owner.Text(Data, row);
        long value = owner.Call(1, 1, Data, (ulong)row.Length);
        model.ObservationFailed |= !model.Quota.RecordCall(model.Writer, new() { Number = 1, Argument0 = 1, Argument2 = (ulong)row.Length }, new(LinuxSyscallAction.Return, value));
        return value;
    }

    private static long QuotaWritev(LinuxDescriptorServicesTests.Owner owner, ref LoggingModel model, string row)
    {
        owner.Text(Data, row);
        ulong* vectors = (ulong*)owner.Memory.At(Vectors);
        vectors[0] = Data;
        vectors[1] = (ulong)row.Length - 1;
        vectors[2] = Data + (ulong)row.Length - 1;
        vectors[3] = 1;
        long value = owner.Call(20, 2, Vectors, 2);
        model.ObservationFailed |= !model.Quota.RecordCall(model.Writer, new() { Number = 20, Argument0 = 2, Argument2 = 2 }, new(LinuxSyscallAction.Return, value));
        return value;
    }

    private static void Attach(LinuxDescriptorServicesTests.Owner owner, LoggingModel* model)
    {
        model->Descriptor.RequiredOutputSource = owner.Admission.Scratch;
        owner.Admission.User.Context = model;
        owner.Admission.User.Write = &Sink;
    }

    [UnmanagedCallersOnly]
    private static byte Sink(void* context, uint channel, byte* source, uint bytes)
    {
        LoggingModel* model = (LoggingModel*)context;
        model->Descriptor.SinkCalls++;
        if (source != model->Descriptor.RequiredOutputSource)
            return 0;
        model->LastCharge = model->Budget.Charge(channel, bytes);
        if (model->QuotaCase && model->LastCharge == Budget.ChargeStatus.QuotaExceeded)
            model->ObservationFailed |= !model->Quota.Deny(channel, model->Writer, bytes, model->Budget.ChargedRecords, model->Budget.ChargedBytes);
        if (model->LastCharge != Budget.ChargeStatus.Accepted || model->FailDelivery)
            return 0;
        new ReadOnlySpan<byte>(source, (int)bytes).CopyTo(new Span<byte>(model->Descriptor.Sink, 8192));
        model->Descriptor.Channel = channel;
        model->Descriptor.SinkBytes = bytes;
        model->DeliveredRecords++;
        if (model->QuotaCase)
            model->ObservationFailed |= !model->Quota.Observe(channel, model->Writer, source, bytes);
        return model->ObservationFailed ? (byte)0 : (byte)1;
    }
}
