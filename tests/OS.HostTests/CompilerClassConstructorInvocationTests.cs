using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.CompilerClassConstructorExecution;
using Status = OS.Kernel.Runtime.CompilerClassConstructorStatus;
using Ticket = OS.Kernel.Runtime.CompilerClassConstructorInvocation;
using Fixture = CompilerClassConstructorExecutionTests.Fixture;

internal static unsafe class CompilerClassConstructorInvocationTests
{
    private static void Expect(Status actual, Status expected) => HostCheck.True(actual == expected, "expected=" + expected + " actual=" + actual);
    private static void Empty(Ticket ticket) => HostCheck.True(ticket.Equals(default(Ticket)) && !ticket.RequiresInvocation, "zero noninvoking receipt");
    private static void Start(Fixture fixture, uint lazy = 4)
    {
        Expect(fixture.Bind(1, lazy), Status.Success);
        Expect(Core.RunEager(fixture.Runner, Fixture.Capacity), Status.Success);
    }
    private static ulong Context(Fixture fixture, int index) => fixture.Context + (uint)index * 16;
    private static void SetTarget(Fixture fixture, int index, int target = 0x8000) => *(ulong*)Context(fixture, index) = fixture.Address(target);
    private static Ticket Begin(Fixture fixture, int index)
    {
        Expect(Core.BeginLazy(fixture.Runner, Fixture.Capacity, Context(fixture, index), out var ticket), Status.Success);
        HostCheck.True(ticket.RequiresInvocation, "new admitted synthetic target requires a caller invocation");
        return ticket;
    }
    private static void Unchanged(Fixture fixture, byte[] before) => HostCheck.True(before.AsSpan().SequenceEqual(fixture.RunnerBytes()), "rejection changed runner");
    private static byte[] CompilerWords(Fixture fixture) => new ReadOnlySpan<byte>(fixture.Image + 0x2000, 0x1000).ToArray();

    [HostTest("m04.015.invocation POD and rejected admission remain noninvoking", HostLane.Fast, 0)]
    private static void Admission()
    {
        HostCheck.True(sizeof(Ticket) == 40 && Marshal.SizeOf<Ticket>() == 40 &&
            Marshal.OffsetOf<Ticket>(nameof(Ticket.Context)).ToInt32() == 16 &&
            Marshal.OffsetOf<Ticket>(nameof(Ticket.Target)).ToInt32() == 24 &&
            Marshal.OffsetOf<Ticket>(nameof(Ticket.RecordIndex)).ToInt32() == 32 &&
            Marshal.OffsetOf<Ticket>(nameof(Ticket.ParentIndex)).ToInt32() == 36, "independent ticket POD offsets");
        HostCheck.True(Marshal.OffsetOf<CompilerClassConstructorProgress>(nameof(CompilerClassConstructorProgress.EagerFinished)).ToInt32() == 20 &&
            sizeof(CompilerClassConstructorProgress) == 24 && Core.LazyRecordBytes == 24 &&
            Core.RequiredStorage(1, 4) == Core.StateBytes + 16 + 4 * 24, "existing progress and lazy record ABI");
        using var f = new Fixture();
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out var ticket), Status.NotBound); Empty(ticket);
        Expect(Core.BeginLazy(null, 0, f.Context, out ticket), Status.StorageRejected); Empty(ticket);
        Expect(f.Bind(), Status.Success);
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out ticket), Status.EagerNotStarted); Empty(ticket);
        Expect(Core.RunEager(f.Runner, Fixture.Capacity), Status.Success);
        byte[] before = f.RunnerBytes();
        foreach (ulong context in new[] { 0UL, f.Context + 1, f.Address(0x1000), (ulong)f.Runner })
        {
            Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, context, out ticket), Status.ContextRejected); Empty(ticket); Unchanged(f, before);
        }
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out ticket), Status.ContextRejected); Empty(ticket);
        *(ulong*)f.Context = f.Address(0x1800);
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out ticket), Status.TargetRejected); Empty(ticket);
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, default, true), Status.InvocationRejected);
        Unchanged(f, before); f.Guards();
    }

    [HostTest("m04.015.invocation normal completion clears only compiler word", HostLane.Fast, 1)]
    private static void Completed()
    {
        using var f = new Fixture(inline: true); Start(f); SetTarget(f, 0);
        *(ulong*)(f.Context + 8) = 0x0123456789ABCDEF;
        byte[] compiler = CompilerWords(f); Ticket ticket = Begin(f, 0);
        HostCheck.True(ticket.Storage == (ulong)f.Runner && ticket.Capacity == Fixture.Capacity && ticket.Context == f.Context &&
            ticket.Target == f.Address(0x8000) && ticket.RecordIndex == 0 && ticket.ParentIndex == uint.MaxValue,
            "ticket binds original runner/context/target and no parent");
        // The host caller reports a return; synthetic PE code is never executed.
        HostCheck.True(compiler.AsSpan().SequenceEqual(CompilerWords(f)), "admission changed compiler bytes");
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, ticket, true), Status.Success);
        HostCheck.True(*(ulong*)f.Context == 0 && *(ulong*)(f.Context + 8) == 0x0123456789ABCDEF, "completion word and adjacent guard");
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var progress), Status.Success);
        HostCheck.True(progress.LazyCount == 1 && progress.LazyCompleted == 1 && progress.ActiveLazyCount == 0, "normal completion accounting");
        byte[] before = f.RunnerBytes();
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out var repeated), Status.Success); Empty(repeated);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.Success);
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, ticket, false), Status.InvocationRejected);
        Unchanged(f, before); f.Guards();
    }

    [HostTest("m04.015.invocation nested folded targets and active reentry do not reinvoke", HostLane.Fast, 2)]
    private static void Reentry()
    {
        using var f = new Fixture(); Start(f); SetTarget(f, 0); SetTarget(f, 1);
        Ticket a = Begin(f, 0);
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out var again), Status.Success); Empty(again);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.Success);
        Ticket b = Begin(f, 1);
        HostCheck.True(b.RecordIndex == 1 && b.ParentIndex == a.RecordIndex && a.Target == b.Target, "context identity permits folded target");
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out again), Status.Success); Empty(again);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, f.Context), Status.Success);
        byte[] before = f.RunnerBytes();
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, a, true), Status.InvocationRejected); Unchanged(f, before);
        HostCheck.True(*(ulong*)f.Context == a.Target && *(ulong*)Context(f, 1) == b.Target, "out-of-order finish retained both compiler words");
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, b, true), Status.Success);
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var progress), Status.Success);
        HostCheck.True(progress.ActiveLazyCount == 1 && progress.LazyCompleted == 1 && *(ulong*)f.Context == a.Target, "exact child pop retains parent");
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, a, true), Status.Success);
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out progress), Status.Success);
        HostCheck.True(progress.LazyCount == 2 && progress.LazyCompleted == 2 && progress.ActiveLazyCount == 0, "nested completion accounting"); f.Guards();
    }

    [HostTest("m04.015.invocation failure retains target and pops only failed child", HostLane.Fast, 3)]
    private static void Failed()
    {
        using var f = new Fixture(inline: true); Start(f, 2); SetTarget(f, 0); SetTarget(f, 1, 0x8010);
        Ticket a = Begin(f, 0), b = Begin(f, 1); byte[] compiler = CompilerWords(f);
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, b, false), Status.Success);
        HostCheck.True(compiler.AsSpan().SequenceEqual(CompilerWords(f)) && *(ulong*)Context(f, 1) == b.Target, "failure preserves original nonzero compiler bytes");
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var progress), Status.Success);
        HostCheck.True(progress.LazyCount == 2 && progress.LazyCompleted == 0 && progress.ActiveLazyCount == 1, "failed record is not active or complete");
        byte[] before = f.RunnerBytes();
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, Context(f, 1), out var repeated), Status.InitializationFailed); Empty(repeated);
        Expect(Core.EnsureLazy(f.Runner, Fixture.Capacity, Context(f, 1)), Status.InitializationFailed);
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, b, true), Status.InvocationRejected); Unchanged(f, before);
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, a, true), Status.Success);
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out progress), Status.Success);
        HostCheck.True(progress.LazyCompleted == 1 && progress.ActiveLazyCount == 0 && *(ulong*)Context(f, 1) == b.Target, "parent completion leaves failed child nonzero");
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, Context(f, 1), out repeated), Status.InitializationFailed); Empty(repeated);
        SetTarget(f, 2);
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, Context(f, 2), out repeated), Status.CapacityExceeded); Empty(repeated); f.Guards();
    }

    [HostTest("m04.015.invocation every ticket identity field and changed word reject atomically", HostLane.Fast, 4)]
    private static void ChangedTicket()
    {
        using var f = new Fixture(); Start(f); SetTarget(f, 0); Ticket original = Begin(f, 0);
        byte[] before = f.RunnerBytes();
        for (int field = 0; field < 6; field++)
        {
            Ticket changed = original;
            if (field < 4) ((ulong*)&changed)[field] ^= 8;
            else ((uint*)&changed)[field + 4] ^= 1;
            Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, changed, true), Status.InvocationRejected); Unchanged(f, before);
            HostCheck.True(*(ulong*)f.Context == original.Target, "forged ticket cleared compiler word");
        }
        foreach (ulong word in new[] { 0UL, f.Address(0x8010) })
        {
            *(ulong*)f.Context = word;
            Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, original, true), Status.StateChanged); Unchanged(f, before);
            Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out var denied), Status.StateChanged); Empty(denied);
        }
        *(ulong*)f.Context = original.Target;
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, original, false), Status.Success);
        *(ulong*)f.Context = 0;
        before = f.RunnerBytes();
        Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out var stale), Status.StateChanged); Empty(stale); Unchanged(f, before); f.Guards();
    }

    [HostTest("m04.015.invocation complete parent chain rejects corruption before writes", HostLane.Fast, 5)]
    private static void ParentChain()
    {
        using var f = new Fixture(); Start(f); SetTarget(f, 0); SetTarget(f, 1);
        Ticket a = Begin(f, 0), b = Begin(f, 1);
        // Independent existing 24-byte record layout: two ulongs, phase, parent.
        byte* record = f.Runner + Core.StateBytes + 16 + b.RecordIndex * 24;
        foreach (uint parent in new[] { b.RecordIndex, 2U, uint.MaxValue })
        {
            *(uint*)(record + 20) = parent; byte[] before = f.RunnerBytes();
            Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, b, true), Status.StateChanged); Unchanged(f, before);
            Expect(Core.BeginLazy(f.Runner, Fixture.Capacity, f.Context, out var denied), Status.StateChanged); Empty(denied);
        }
        *(uint*)(record + 20) = a.RecordIndex;
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, b, false), Status.Success);
        Expect(Core.FinishLazy(f.Runner, Fixture.Capacity, a, false), Status.Success);
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var progress), Status.Success);
        HostCheck.True(progress.ActiveLazyCount == 0 && progress.LazyCompleted == 0 && progress.LazyCount == 2, "both failed records popped"); f.Guards();
    }

    [HostTest("m04.015.invocation original runner capacity and module owner remain required", HostLane.Fast, 6)]
    private static void Ownership()
    {
        using var a = new Fixture(); using var b = new Fixture(inline: true);
        Start(a); Start(b); SetTarget(a, 0); SetTarget(b, 0);
        Ticket first = Begin(a, 0), other = Begin(b, 0); byte[] before = a.RunnerBytes();
        Expect(Core.FinishLazy(a.Runner, Fixture.Capacity, other, true), Status.InvocationRejected); Unchanged(a, before);
        Expect(Core.FinishLazy(a.Runner, Fixture.Capacity - 8, first, true), Status.StateChanged); Unchanged(a, before);
        ulong ownerWord = *(ulong*)(a.Image + 0x2000); *(ulong*)(a.Image + 0x2000) = 0;
        Expect(Core.FinishLazy(a.Runner, Fixture.Capacity, first, false), Status.ModuleRejected); Unchanged(a, before);
        *(ulong*)(a.Image + 0x2000) = ownerWord;
        Expect(Core.FinishLazy(a.Runner, Fixture.Capacity, first, true), Status.Success);
        Expect(Core.FinishLazy(b.Runner, Fixture.Capacity, other, true), Status.Success); a.Guards(); b.Guards();
    }
}
