using System.Text;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using Budget = OS.Kernel.Compatibility.LinuxX64.LinuxAppLogBudget;

internal static unsafe class LinuxAppLoggingTests
{
    private const ulong User = 0x100000000, Data = User + 4096, Vectors = User + 20480;

    [HostTest("m10-005 log quota rejects defaults invalid records and reinitialization without side effects")]
    private static void Admission()
    {
        Budget budget = default;
        HostCheck.Equal(Budget.ChargeStatus.Rejected, budget.Charge(1, 1));
        HostCheck.True(!budget.Initialize(0, 1) && !budget.Initialize(1, 0) && !budget.IsInitialized);
        HostCheck.True(budget.Initialize(10, 2) && !budget.Initialize(100, 100));
        foreach (var record in new[] { (0U, 1U), (3U, 1U), (1U, 0U), (2U, 8193U) })
            HostCheck.Equal(Budget.ChargeStatus.Rejected, budget.Charge(record.Item1, record.Item2));
        HostCheck.Equal(0U, budget.ChargedBytes);
        HostCheck.Equal(Budget.ChargeStatus.Accepted, budget.Charge(1, 4));
        HostCheck.Equal(Budget.ChargeStatus.Accepted, budget.Charge(2, 6));
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, budget.Charge(1, 1));
        HostCheck.Equal(10U, budget.ChargedBytes); HostCheck.Equal(2U, budget.ChargedRecords);
        HostCheck.Equal(4U, budget.StdoutChargedBytes); HostCheck.Equal(6U, budget.StderrChargedBytes);
        Budget records = default; HostCheck.True(records.Initialize(100, 1));
        HostCheck.Equal(Budget.ChargeStatus.Accepted, records.Charge(1, 1));
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, records.Charge(2, 1));
        HostCheck.Equal(1U, records.ChargedBytes); HostCheck.Equal(1U, records.ChargedRecords);
    }

    [HostTest("m10-005 actual descriptor write and writev retain partial counts ordering and errno before quota")]
    private static void DescriptorWrites()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        LoggingModel model = new() { Descriptor = *owner.Context };
        HostCheck.True(model.Budget.Initialize(16384, 2));
        Attach(owner, &model); owner.Begin();
        new Span<byte>(owner.Memory.At(Data), 16384).Fill((byte)'a');
        HostCheck.Equal(8192L, owner.Call(1, 1, Data, 16384));
        HostCheck.Equal(8192U, model.Descriptor.SinkBytes);
        HostCheck.True(new ReadOnlySpan<byte>(model.Descriptor.Sink, 8192).IndexOfAnyExcept((byte)'a') == -1);
        new Span<byte>(owner.Memory.At(Data), 4000).Fill((byte)'b');
        new Span<byte>(owner.Memory.At(Data + 4000), 6000).Fill((byte)'c');
        ulong* vectors = (ulong*)owner.Memory.At(Vectors);
        vectors[0] = Data; vectors[1] = 4000; vectors[2] = Data + 4000; vectors[3] = 6000;
        HostCheck.Equal(8192L, owner.Call(20, 2, Vectors, 2));
        HostCheck.True(new ReadOnlySpan<byte>(model.Descriptor.Sink, 4000).IndexOfAnyExcept((byte)'b') == -1);
        HostCheck.True(new ReadOnlySpan<byte>(model.Descriptor.Sink + 4000, 4192).IndexOfAnyExcept((byte)'c') == -1);
        HostCheck.Equal(8192U, model.Budget.StdoutChargedBytes); HostCheck.Equal(8192U, model.Budget.StderrChargedBytes);
        uint calls = model.Descriptor.SinkCalls;
        HostCheck.Equal(-9L, owner.Call(1, 99, Data, 1));
        HostCheck.Equal(-14L, owner.Call(1, 1, User + 32768, 1));
        HostCheck.Equal(0L, owner.Call(1, 1, ulong.MaxValue, 0));
        HostCheck.Equal(calls, model.Descriptor.SinkCalls);
        HostCheck.Equal(-5L, owner.Call(1, 1, Data, 1));
        HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, model.LastCharge);
        HostCheck.Equal(2U, model.DeliveredRecords); HostCheck.Equal(16384U, model.Budget.ChargedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Admission.Scratch, 8192).IndexOfAnyExcept((byte)0) == -1);
    }

    [HostTest("m10-005 failed serial delivery consumes its charge while rejected output never reaches the sink")]
    private static void DeliveryFailure()
    {
        using var owner = new LinuxDescriptorServicesTests.Owner();
        LoggingModel model = new() { Descriptor = *owner.Context, FailDelivery = true };
        HostCheck.True(model.Budget.Initialize(6, 10));
        Attach(owner, &model); owner.Begin(); owner.Text(Data, "abc");
        HostCheck.Equal(-5L, owner.Call(1, 1, Data, 3));
        HostCheck.Equal(3U, model.Budget.ChargedBytes); HostCheck.Equal(0U, model.DeliveredRecords);
        model.FailDelivery = false;
        HostCheck.Equal(3L, owner.Call(1, 2, Data, 3));
        HostCheck.Equal("abc", Encoding.ASCII.GetString(new ReadOnlySpan<byte>(model.Descriptor.Sink, 3)));
        HostCheck.Equal(0L, owner.Call(20, 1, Vectors, 0));
        HostCheck.Equal(-5L, owner.Call(1, 1, Data, 1));
        HostCheck.Equal(6U, model.Budget.ChargedBytes); HostCheck.Equal(2U, model.Budget.ChargedRecords);
        HostCheck.Equal(1U, model.DeliveredRecords);
    }

    [HostTest("m10-005 externally serialized concurrent writers share one process quota")]
    private static void ConcurrentWriters()
    {
        Budget budget = default; HostCheck.True(budget.Initialize(1000, 1000));
        object gate = new(); int accepted = 0, exhausted = 0;
        Task.WaitAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++) lock (gate)
            {
                var status = budget.Charge((uint)(worker % 2 + 1), 1);
                if (status == Budget.ChargeStatus.Accepted) accepted++;
                else if (status == Budget.ChargeStatus.QuotaExceeded) exhausted++;
                else throw new InvalidOperationException("unexpected log charge rejection");
            }
        })).ToArray());
        HostCheck.Equal(1000, accepted); HostCheck.Equal(7000, exhausted);
        HostCheck.Equal(1000U, budget.ChargedBytes); HostCheck.Equal(1000U, budget.ChargedRecords);
        HostCheck.Equal(1000U, budget.StdoutChargedBytes + budget.StderrChargedBytes);
    }

    [HostTest("m10-005 log quota reaches uint byte boundary exactly without wrapping or allocations")]
    private static void ArithmeticBoundary()
    {
        Budget warm = default; warm.Initialize(1, 1); warm.Charge(1, 1);
        Budget budget = default; HostCheck.True(budget.Initialize(uint.MaxValue, uint.MaxValue));
        bool accepted = true; long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint i = 0; i < uint.MaxValue / 8192; i++) accepted &= budget.Charge(1, 8192) == Budget.ChargeStatus.Accepted;
        accepted &= budget.Charge(2, uint.MaxValue % 8192) == Budget.ChargeStatus.Accepted;
        var exhausted = budget.Charge(1, 1); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(accepted); HostCheck.Equal(Budget.ChargeStatus.QuotaExceeded, exhausted);
        HostCheck.Equal(uint.MaxValue, budget.ChargedBytes); HostCheck.Equal(524288U, budget.ChargedRecords);
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
        for (int worker = 0; worker < 8; worker++) for (int record = 0; record < 4; record++)
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
        HostCheck.Equal(32U, logs.StdoutRecords); HostCheck.Equal(32U, logs.StderrRecords);
        HostCheck.Equal(32U, logs.WritevRecords); HostCheck.True(!logs.Complete);
        HostCheck.True(LogCall(ref logs, 10, 20, 1234, -9) && logs.Complete);
        HostCheck.True(!LogCall(ref logs, 10, 20, 1234, -9) && logs.Failed && !logs.Complete);
    }

    [HostTest("m10-005 copied log observation rejects malformed duplicate foreign and unjoined records permanently")]
    private static void InvalidCopiedObservation()
    {
        foreach (string text in new[] { "CLROS_LOG fd=2 worker=0 record=0\n", "CLROS_LOG fd=1 worker=8 record=0\n",
            "CLROS_LOG fd=1 worker=0 record=4\n", "CLROS_LOG fd=1 worker=0 record=0\r\n",
            "CLROS_LOG fd=1 worker=0 record=0 extra\n" })
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
                _ => LogCall(ref logs, 10, 20, 1234, -9)
            };
            HostCheck.True(!accepted && logs.Failed && !logs.Complete);
        }
        LinuxAppLogObservation changed = default;
        HostCheck.True(LogText(ref changed, 1, 100, "CLROS_LOG"));
        HostCheck.True(!LogText(ref changed, 1, 101, " fd=1 worker=0 record=0\n") && changed.Failed);
        foreach (var call in new[] { (101UL, 20UL, 2UL, 33L), (100UL, 1UL, 2UL, 33L),
            (100UL, 20UL, 1UL, 33L), (100UL, 20UL, 2UL, 32L), (100UL, 20UL, 2UL, -5L) })
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
            LinuxAppLogObservation logs = default; byte value = 65;
            HostCheck.True(!logs.Observe(mode == 0 ? 3U : 1U, mode == 1 ? 0UL : 1UL,
                mode == 2 ? null : &value, mode == 3 ? 0U : mode == 4 ? 8193U : 1U) && logs.Failed);
        }
        LinuxAppLogObservation noWriter = default;
        HostCheck.True(!LogCall(ref noWriter, 0, 1, 1, 1) && noWriter.Failed);
    }

    [HostTest("m10-005 copied stderr stays in one syscall and preserves each worker source order")]
    private static void CopiedObservationCallOrder()
    {
        const string output = "CLROS_LOG fd=1 worker=0 record=0\n", error = "CLROS_LOG fd=2 worker=0 record=0\n";
        foreach (int split in new[] { 1, 8, 9, 32 })
        {
            LinuxAppLogObservation logs = default;
            HostCheck.True(LogText(ref logs, 1, 100, output) && LogCall(ref logs, 100, 1, 1, 33));
            HostCheck.True(LogText(ref logs, 2, 100, error[..split]));
            bool firstCall = LogCall(ref logs, 100, 20, 2, split);
            if (split < 9) HostCheck.True(firstCall && !LogText(ref logs, 2, 100, error[split..]));
            else HostCheck.True(!firstCall);
            HostCheck.True(logs.Failed && !logs.Complete && logs.WritevRecords == 0);
        }
        LinuxAppLogObservation extra = default;
        HostCheck.True(LogText(ref extra, 1, 100, output) && LogCall(ref extra, 100, 1, 1, 33));
        HostCheck.True(LogText(ref extra, 2, 100, error + new string('x', 32)));
        HostCheck.True(!LogCall(ref extra, 100, 20, 2, 33) && extra.Failed);
        foreach (int mode in Enumerable.Range(0, 3))
        {
            LinuxAppLogObservation logs = default;
            if (mode != 0) HostCheck.True(LogText(ref logs, 1, 100, output) && LogCall(ref logs, 100, 1, 1, 33));
            string reordered = mode switch { 0 => error, 1 => output.Replace("record=0", "record=1"),
                _ => error.Replace("record=0", "record=1") };
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
            warm.Observe(1, 100, source, 33); LogCall(ref warm, 100, 1, 1, 33);
            bool accepted = true; long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint worker = 0; worker < 8; worker++) for (uint record = 0; record < 4; record++)
            {
                source[22] = (byte)('0' + worker); source[31] = (byte)('0' + record);
                source[13] = (byte)'1'; accepted &= logs.Observe(1, worker + 100, source, 33);
                accepted &= LogCall(ref logs, worker + 100, 1, 1, 33);
                source[13] = (byte)'2'; accepted &= logs.Observe(2, worker + 100, source, 33);
                accepted &= LogCall(ref logs, worker + 100, 20, 2, 33);
            }
            accepted &= LogCall(ref logs, 10, 20, 1234, -9);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            HostCheck.True(accepted && logs.Complete); HostCheck.Equal(0L, allocated);
        }
    }

    private static bool LogText(ref LinuxAppLogObservation logs, uint fd, ulong identity, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* source = bytes) return logs.Observe(fd, identity, source, (uint)bytes.Length);
    }
    private static bool LogCall(ref LinuxAppLogObservation logs, ulong identity, ulong number, ulong fd, long value)
        => logs.RecordCall(identity, new LinuxSyscallFrame { Number = number, Argument0 = fd }, new(LinuxSyscallAction.Return, value));

    // Descriptor callbacks retain their existing model at offset0. Only the
    // output callback is replaced; real page validation/copy/dispatch still runs.
    private struct LoggingModel
    {
        public LinuxDescriptorServicesTests.Model Descriptor;
        public Budget Budget;
        public Budget.ChargeStatus LastCharge;
        public uint DeliveredRecords;
        public bool FailDelivery;
    }
    private static void Attach(LinuxDescriptorServicesTests.Owner owner, LoggingModel* model)
    {
        model->Descriptor.RequiredOutputSource = owner.Admission.Scratch;
        owner.Admission.User.Context = model; owner.Admission.User.Write = &Sink;
    }
    [UnmanagedCallersOnly]
    private static byte Sink(void* context, uint channel, byte* source, uint bytes)
    {
        LoggingModel* model = (LoggingModel*)context; model->Descriptor.SinkCalls++;
        if (source != model->Descriptor.RequiredOutputSource) return 0;
        model->LastCharge = model->Budget.Charge(channel, bytes);
        if (model->LastCharge != Budget.ChargeStatus.Accepted || model->FailDelivery) return 0;
        new ReadOnlySpan<byte>(source, (int)bytes).CopyTo(new Span<byte>(model->Descriptor.Sink, 8192));
        model->Descriptor.Channel = channel; model->Descriptor.SinkBytes = bytes; model->DeliveredRecords++; return 1;
    }
}
