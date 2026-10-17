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
