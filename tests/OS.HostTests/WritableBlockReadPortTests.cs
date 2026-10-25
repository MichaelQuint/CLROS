using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe class WritableBlockReadPortTests
{
    private delegate bool Retain(P.State* state, ulong epoch, ulong address, ulong bytes);
    private static readonly Retain CanRetain = typeof(P).GetMethod("CanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Retain>();

    [HostTest("borrowed writable read port uses one real queue and preserves physical versus client read facts")]
    private static void OriginalRead()
    {
        using var f = new Fixture();
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<P.State>() && !RuntimeHelpers.IsReferenceOrContainsReferences<P.Completion>());
        ulong submitted = f.Block->Submitted, writes = f.Block->SubmittedBytes, flushed = f.Block->DurableBytes;
        var token = f.Read(1); var published = f.Sample();
        HostCheck.True(published.Kind == B.Operation.Read && published.Sector == 1 && published.Request.Generation == submitted + 1 &&
            published.Flags == B.ObservationFlags.Published && published.PayloadBytes == 0);
        P.Completion result = default;
        HostCheck.Equal(P.Status.Pending, P.Poll(f.State, token, &result)); f.Sample();
        f.Device.Complete(); HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &result)); var ready = f.Sample();
        HostCheck.True(result.AvailableBytes == 512 && result.Physical.Bytes == 512 && result.Physical.Kind == B.Operation.Read &&
            result.Physical.Token.Generation == published.Request.Generation && ready.Flags.HasFlag(B.ObservationFlags.CompletionValidated));
        uint calls = f.Device.Hardware->Calls;
        HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &result)); HostCheck.Equal(calls, f.Device.Hardware->Calls);
        HostCheck.Equal(P.Status.Success, P.Copy(f.State, token, f.Output)); var copied = f.Sample();
        HostCheck.True(f.Output.SequenceEqual(f.Media.Volatile.Slice(512, 512)) && copied.Flags.HasFlag(B.ObservationFlags.ReadCopied) &&
            copied.PayloadBytes == 512 && !f.State->RequestActive && f.Block->SubmittedBytes == writes && f.Block->DurableBytes == flushed);
        HostCheck.Equal(P.Status.StaleToken, P.Copy(f.State, token, f.Output));
        var next = f.Read(2); calls = f.Device.Hardware->Calls;
        HostCheck.True(next.Generation > token.Generation); HostCheck.Equal(P.Status.StaleToken, P.Poll(f.State, token, &result));
        HostCheck.Equal(calls, f.Device.Hardware->Calls); f.FinishRead(next);
        HostCheck.Equal(P.Status.Success, P.Stop(f.State));
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512));
        Console.WriteLine($"WRITABLE_READ_PORT_LAYOUT state={sizeof(P.State)} admission={sizeof(P.Admission)} token={sizeof(P.Token)} completion={sizeof(P.Completion)}");
    }

    [HostTest("borrowed writable read pending and held leases exclude every engine entry copy cancel pump and stop path")]
    private static void ReciprocalEngineExclusion()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(); var file = f.Open(out var consumed);
            C.FreeCursor cursor = default; HostCheck.Equal(C.Status.Success, C.CreateFreeCursor(f.Engine, 8, &cursor));
            var token = f.Read(2);
            if (held) { f.Device.Complete(); P.Completion r = default; HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &r)); f.Sample(); }
            uint calls = f.Device.Hardware->Calls; ulong submitted = f.Block->Submitted;
            C.RequestToken request = default; C.FileToken value = default; C.OpenFacts facts = default; C.RootFacts root = default;
            C.Result result = default; C.MutationResult mutation = default; C.FreeResult free = default; uint growth = 99;
            HostCheck.Equal(C.Status.InvalidStorage, C.Start(f.Engine, f.Media.Input));
            HostCheck.Equal(C.Status.Busy, C.BeginOpenRoot(f.Engine, "DATA.BIN", &request));
            HostCheck.Equal(C.Status.Busy, C.BeginInspectRoot(f.Engine, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginCreateEmpty(f.Engine, "NEW.TXT", "NEW     TXT"u8, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginOverwrite(f.Engine, file, 0, new byte[] { 4 }, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginGrow(f.Engine, file, 1500, new uint[] { 8 }, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginTruncate(f.Engine, file, 512, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginDelete(f.Engine, file, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginRead(f.Engine, file, 0, 1, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginFlush(f.Engine, &request));
            HostCheck.Equal(C.Status.Busy, C.BeginFindFree(f.Engine, cursor, 1, 1, 2, &request));
            HostCheck.Equal(C.Status.Busy, C.GrowthCandidatesRequired(f.Engine, file, 1500, &growth));
            HostCheck.Equal(C.Status.Busy, C.CreateFreeCursor(f.Engine, 8, &cursor));
            HostCheck.Equal(C.Status.StaleToken, C.CopyOpen(f.Engine, consumed, &value));
            HostCheck.Equal(C.Status.StaleToken, C.CopyOpenFacts(f.Engine, consumed, &facts, (uint)sizeof(C.OpenFacts)));
            HostCheck.Equal(C.Status.StaleToken, C.CopyRootFacts(f.Engine, consumed, &root, (uint)sizeof(C.RootFacts)));
            HostCheck.Equal(C.Status.StaleToken, C.CopyRead(f.Engine, consumed, f.Output, &result));
            HostCheck.Equal(C.Status.StaleToken, C.CopyResult(f.Engine, consumed, &result));
            HostCheck.Equal(C.Status.StaleToken, C.CopyMutationResult(f.Engine, consumed, &mutation));
            HostCheck.Equal(C.Status.StaleToken, C.CopyFree(f.Engine, consumed, Span<uint>.Empty, &free));
            HostCheck.Equal(C.Status.StaleToken, C.Cancel(f.Engine, consumed));
            HostCheck.Equal(C.Status.Idle, C.Pump(f.Engine)); HostCheck.Equal(C.Status.Busy, C.Stop(f.Engine));
            HostCheck.True(f.Block->RequestPending && !f.Engine->RequestActive && !f.Engine->Dirty && f.Block->Submitted == submitted && f.Device.Hardware->Calls == calls);
            if (!held) { f.Device.Complete(); P.Completion r = default; HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &r)); f.Sample(); }
            HostCheck.Equal(P.Status.Success, P.Copy(f.State, token, f.Output)); f.Sample();
            HostCheck.Equal(C.Status.Pending, C.BeginFlush(f.Engine, &request)); f.RunEngine();
            HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &result));
            HostCheck.Equal(P.Status.Success, P.Stop(f.State));
        }
    }

    [HostTest("borrowed read submission excludes whole engine metadata write flush and held result lifetimes")]
    private static void WholeEngineRequest()
    {
        using var f = new Fixture(); var file = f.Open(out _); byte[] changed = Enumerable.Repeat((byte)0x59, 700).ToArray();
        C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, file, 100, changed, &request));
        var kinds = new HashSet<B.Operation>();
        for (int turn = 0; turn < 2000; turn++)
        {
            ulong submitted = f.Block->Submitted; uint calls = f.Device.Hardware->Calls; P.Token none = default;
            HostCheck.Equal(P.Status.Busy, P.Submit(f.State, 1, &none));
            HostCheck.True(!none.IsInitialized && f.Block->Submitted == submitted && f.Device.Hardware->Calls == calls);
            var status = C.Pump(f.Engine); var observation = f.Sample();
            if (f.Block->Submitted != submitted) kinds.Add(observation.Kind);
            if (f.Device.Hardware->Pending) f.Device.Complete();
            if (status == C.Status.Ready) break;
            HostCheck.Equal(C.Status.Pending, status); HostCheck.True(turn < 1999);
        }
        HostCheck.True(kinds.SetEquals(new[] { B.Operation.Read, B.Operation.Write, B.Operation.Flush }) && f.Engine->IsReady && !f.Engine->Dirty);
        P.Token blocked = default; HostCheck.Equal(P.Status.Busy, P.Submit(f.State, 2, &blocked));
        C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &result)); HostCheck.Equal(700U, result.Bytes);
        var token = f.Read(2); f.FinishRead(token);
        HostCheck.True(f.Output.SequenceEqual(f.Media.Volatile.Slice(1024, 512)) && f.Block->SubmittedBytes > 0 && f.Block->DurableBytes == f.Block->CompletedBytes);
        HostCheck.Equal(P.Status.Success, P.Stop(f.State));
    }

    [HostTest("borrowed read wrong token or foreign OUT and FLUSH never invokes tokenless physical poll")]
    private static void WrongClientPoll()
    {
        foreach (bool flush in new[] { false, true })
        {
            using var f = new Fixture(); var token = f.Read(1); B.RequestToken original = f.Sample().Request;
            P.Completion result = default; uint calls = f.Device.Hardware->Calls;
            HostCheck.Equal(P.Status.StaleToken, P.Poll(f.State, default, &result)); HostCheck.Equal(calls, f.Device.Hardware->Calls);
            // Deliberately violate the caller contract through the ORIGINAL B
            // owner, to prove the port cannot consume that client's next request.
            HostCheck.Equal(B.Status.Success, B.Cancel(f.Block, original)); f.Sample(); f.Device.Complete();
            B.Completion drained = default; HostCheck.Equal(B.Status.Cancelled, B.Poll(f.Block, &drained)); f.Sample();
            B.RequestToken foreign = default;
            HostCheck.Equal(B.Status.Success, flush ? B.SubmitFlush(f.Block, &foreign) : B.SubmitWrite(f.Block, 7, new byte[512], &foreign)); f.Sample();
            f.Device.Complete(); calls = f.Device.Hardware->Calls; ulong completed = f.Block->Completed;
            HostCheck.Equal(P.Status.DeviceFailed, P.Poll(f.State, token, &result));
            HostCheck.True(f.Device.Hardware->Calls == calls && f.Block->Completed == completed && f.Block->RequestPending && f.State->RequestActive);
            HostCheck.Equal(P.Status.DeviceFailed, P.Copy(f.State, token, f.Output));
            HostCheck.Equal(B.Status.Success, B.Poll(f.Block, &drained)); f.Sample();
            HostCheck.True(drained.Token.Generation == foreign.Generation && drained.Kind == (flush ? B.Operation.Flush : B.Operation.Write));
            HostCheck.Equal(P.Status.DeviceFailed, P.Stop(f.State));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Block)); HostCheck.Equal(P.Status.Success, P.Stop(f.State));
        }
    }

    [HostTest("borrowed pending and held cancellation revoke copies without erasing successful physical IN evidence")]
    private static void Cancellation()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(); var token = f.Read(3); P.Completion result = default;
            if (held) { f.Device.Complete(); HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &result)); f.Sample(); }
            f.Output.Fill(0xa6); ulong writes = f.Block->CompletedBytes, flushed = f.Block->DurableBytes;
            HostCheck.Equal(held ? P.Status.Cancelled : P.Status.Pending, P.Cancel(f.State, token)); f.Sample();
            if (!held)
            {
                HostCheck.Equal(P.Status.Pending, P.Copy(f.State, token, f.Output)); HostCheck.Equal(C.Status.Busy, C.Stop(f.Engine));
                f.Device.Complete(); HostCheck.Equal(P.Status.Cancelled, P.Poll(f.State, token, &result)); f.Sample();
                HostCheck.True(result.AvailableBytes == 0 && result.Physical.Bytes == 512 && result.Physical.Cancelled && result.Physical.DeviceResult == B.Status.Success);
            }
            HostCheck.Equal(P.Status.StaleToken, P.Copy(f.State, token, f.Output));
            HostCheck.True(f.Output.IndexOfAnyExcept((byte)0xa6) < 0 && !f.State->RequestActive && f.Block->CompletedBytes == writes && f.Block->DurableBytes == flushed);
            HostCheck.Equal(P.Status.Success, P.Stop(f.State)); HostCheck.Equal(C.Status.Success, C.Stop(f.Engine));
        }
    }

    [HostTest("borrowed local stop drains pending or revokes held IN without resetting the physical owner")]
    private static void LocalStop()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(); var token = f.Read(1); P.Completion result = default;
            if (held) { f.Device.Complete(); HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &result)); f.Sample(); }
            uint resets = f.Device.Hardware->Resets; ulong submitted = f.Block->Submitted;
            HostCheck.Equal(held ? P.Status.Success : P.Status.Pending, P.Stop(f.State)); f.Sample();
            if (!held)
            {
                HostCheck.True(f.State->RequestActive && !f.State->Retired); f.Device.Complete();
                HostCheck.Equal(P.Status.Cancelled, P.Poll(f.State, token, &result)); f.Sample();
                HostCheck.Equal(P.Status.Success, P.Stop(f.State));
            }
            HostCheck.True(f.State->Retired && f.Block->Running && f.Device.Hardware->Resets == resets && f.Block->Submitted == submitted);
            HostCheck.Equal(P.Status.InvalidState, P.Copy(f.State, token, f.Output));
        }
    }

    [HostTest("borrowed port bounds and original partition authority reject false overlap and altered retained geometry")]
    private static void RangeAuthority()
    {
        using var f = new Fixture(start: false);
        foreach (var bounds in new[] { (0UL, 1UL), (8UL, 8UL), (7UL, 8UL), (9UL, 1UL), (1UL, f.Block->SectorCount), (ulong.MaxValue, ulong.MaxValue) })
        { var a = f.Input; a.FirstLba = bounds.Item1; a.LastLba = bounds.Item2; HostCheck.Equal(P.Status.InvalidStorage, P.Start(f.State, a)); }
        var wrong = f.Input; wrong.DeviceEpoch++; HostCheck.Equal(P.Status.InvalidStorage, P.Start(f.State, wrong));
        wrong = f.Input; wrong.EngineEpoch++; HostCheck.Equal(P.Status.InvalidStorage, P.Start(f.State, wrong));
        HostCheck.Equal(P.Status.Success, P.Start(f.State, f.Input)); P.Token token = default;
        HostCheck.Equal(P.Status.InvalidRange, P.Submit(f.State, 0, &token)); HostCheck.Equal(P.Status.InvalidRange, P.Submit(f.State, 8, &token));
        foreach (ulong lba in new ulong[] { 1, 7 }) { token = f.Read(lba); f.FinishRead(token); }
        C.Admission* retained = (C.Admission*)((byte*)f.Engine + Offset<C.State>("Input")); var saved = *retained;
        retained->PartitionFirst = 16; uint calls = f.Device.Hardware->Calls;
        HostCheck.Equal(P.Status.InvalidState, P.Submit(f.State, 1, &token));
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512) && f.Device.Hardware->Calls == calls);
        *retained = saved; HostCheck.Equal(P.Status.Success, P.Stop(f.State));
    }

    [HostTest("borrowed port complete partial covering overflow and observer aliases reject before output or IO")]
    private static void Aliases()
    {
        using var f = new Fixture(); ulong calls = f.Device.Hardware->Calls;
        foreach (var extent in f.OwnedExtents())
        {
            HostCheck.True(!CanRetain(f.State, f.Input.Epoch, extent.Start, extent.Bytes) &&
                !CanRetain(f.State, f.Input.Epoch, extent.Start - 8, extent.Bytes + 16) &&
                !CanRetain(f.State, f.Input.Epoch, extent.Start + extent.Bytes - 1, 2));
        }
        foreach (ulong address in f.Owned())
        {
            HostCheck.True(!CanRetain(f.State, f.Input.Epoch, address, 1) && !CanRetain(f.State, f.Input.Epoch, address - 8, 1024));
            HostCheck.Equal(P.Status.InvalidStorage, P.Submit(f.State, 1, (P.Token*)address));
        }
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch, ulong.MaxValue - 7, 64) && !CanRetain(f.State, f.Input.Epoch, 0x7ffffffffff8, 16));
        HostCheck.Equal(calls, (ulong)f.Device.Hardware->Calls);
        var token = f.Read(1); f.Device.Complete(); P.Completion result = default;
        foreach (ulong address in f.Owned()) HostCheck.Equal(P.Status.InvalidStorage, P.Poll(f.State, token, (P.Completion*)address));
        HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &result)); f.Sample(); f.Output.Fill(0x41);
        foreach (ulong address in f.Owned()) HostCheck.Equal(P.Status.InvalidStorage, P.Copy(f.State, token, new Span<byte>((void*)address, 512)));
        HostCheck.Equal(P.Status.InvalidStorage, P.Copy(f.State, token, f.Output[..511]));
        HostCheck.True(f.State->Ready && f.Output.IndexOfAnyExcept((byte)0x41) < 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        HostCheck.True(CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512));
        HostCheck.Equal(allocated, GC.GetAllocatedBytesForCurrentThread());
        HostCheck.Equal(P.Status.Success, P.Copy(f.State, token, f.Output)); f.Sample(); HostCheck.Equal(P.Status.Success, P.Stop(f.State));
    }

    [HostTest("borrowed admission aliases preserve original engine and block bytes with no callbacks")]
    private static void AdmissionAliases()
    {
        using var f = new Fixture(start: false);
        byte[] engine = new ReadOnlySpan<byte>(f.Engine, sizeof(C.State)).ToArray(), block = new ReadOnlySpan<byte>(f.Block, sizeof(B.State)).ToArray();
        uint calls = f.Device.Hardware->Calls;
        foreach (var extent in f.OwnedExtents().Skip(1))
            HostCheck.Equal(P.Status.InvalidStorage, P.Start((P.State*)extent.Start, f.Input));
        HostCheck.True(new ReadOnlySpan<byte>(f.Engine, sizeof(C.State)).SequenceEqual(engine) &&
            new ReadOnlySpan<byte>(f.Block, sizeof(B.State)).SequenceEqual(block) && f.Device.Hardware->Calls == calls);
        HostCheck.Equal(P.Status.Success, P.Start(f.State, f.Input)); HostCheck.Equal(P.Status.Success, P.Stop(f.State));
    }

    [HostTest("borrowed port retired DMA and same-address original block ABA cannot publish or adopt a replacement")]
    private static void RetiredAndAba()
    {
        foreach (bool restart in new[] { false, true })
        {
            using var f = new Fixture(); var token = f.Read(1); f.Device.Complete(); P.Completion result = default;
            HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &result)); f.Sample(); f.Output.Fill(0x84);
            HostCheck.Equal(B.Status.Success, B.Stop(f.Block));
            if (restart)
            {
                new Span<byte>(f.Block, sizeof(B.State)).Clear(); var a = f.Device.Input; a.Epoch += 0x10000;
                a.Negotiation = f.Device.Negotiate(); HostCheck.Equal(B.Status.Success, B.Start(f.Block, a)); f.AllowInvalidStop = true;
            }
            uint calls = f.Device.Hardware->Calls;
            HostCheck.Equal(restart ? P.Status.InvalidState : P.Status.DeviceFailed, P.Copy(f.State, token, f.Output));
            HostCheck.True(f.Output.IndexOfAnyExcept((byte)0x84) < 0 && f.Device.Hardware->Calls == calls && f.State->RequestActive);
            HostCheck.Equal(restart ? P.Status.InvalidState : P.Status.Success, P.Stop(f.State));
            if (restart) HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512));
        }
    }

    [HostTest("borrowed original engine stop and same-address new epoch never transfers read authority to its replacement")]
    private static void EngineAba()
    {
        using var f = new Fixture(); HostCheck.Equal(C.Status.Success, C.Stop(f.Engine));
        new Span<byte>(f.Engine, sizeof(C.State)).Clear(); var input = f.Media.Input; input.Epoch++;
        HostCheck.Equal(C.Status.Pending, C.Start(f.Engine, input)); HostCheck.Equal(C.Status.Ready, f.RunEngine());
        uint calls = f.Device.Hardware->Calls; P.Token token = default; f.AllowInvalidStop = true;
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512));
        HostCheck.Equal(P.Status.InvalidState, P.Submit(f.State, 1, &token)); HostCheck.Equal(P.Status.InvalidState, P.Stop(f.State));
        HostCheck.True(!token.IsInitialized && f.Device.Hardware->Calls == calls && f.Block->Running && f.Engine->IsMounted);
    }

    [HostTest("borrowed inspection authenticates exact geometry and reports writer Busy without publishing facts or consuming Ready")]
    private static void InspectionAvailability()
    {
        using var f = new Fixture(); P.Inspection facts = default;
        HostCheck.Equal(P.Status.Success, P.Inspect(f.State, f.Input.Epoch, &facts));
        HostCheck.True(facts.Owner == (ulong)f.State && facts.Epoch == f.Input.Epoch && facts.EngineOwner == (ulong)f.Engine &&
            facts.EngineEpoch == f.Media.Input.Epoch && facts.DeviceOwner == (ulong)f.Block && facts.DeviceEpoch == f.Device.Input.Epoch &&
            facts.FirstLba == 1 && facts.LastLba == 7 && facts.SectorCount == f.Block->SectorCount && !facts.RequestActive);
        C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginFlush(f.Engine, &request));
        new Span<byte>(&facts, sizeof(P.Inspection)).Fill(0x8d); uint calls = f.Device.Hardware->Calls;
        HostCheck.Equal(P.Status.Busy, P.Inspect(f.State, f.Input.Epoch, &facts));
        HostCheck.True(new ReadOnlySpan<byte>(&facts, sizeof(P.Inspection)).IndexOfAnyExcept((byte)0x8d) < 0 && f.Device.Hardware->Calls == calls);
        HostCheck.Equal(C.Status.Ready, f.RunEngine()); HostCheck.Equal(P.Status.Busy, P.Inspect(f.State, f.Input.Epoch, &facts));
        HostCheck.True(f.Engine->IsReady && new ReadOnlySpan<byte>(&facts, sizeof(P.Inspection)).IndexOfAnyExcept((byte)0x8d) < 0);
        C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &result));
        var token = f.Read(1); HostCheck.Equal(P.Status.Success, P.Inspect(f.State, f.Input.Epoch, &facts)); HostCheck.True(facts.RequestActive && !facts.ReadReady);
        f.Device.Complete(); P.Completion completion = default; HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &completion)); f.Sample();
        HostCheck.Equal(P.Status.Success, P.Inspect(f.State, f.Input.Epoch, &facts)); HostCheck.True(facts.ReadReady);
        foreach (ulong address in f.Owned()) HostCheck.Equal(P.Status.InvalidStorage, P.Inspect(f.State, f.Input.Epoch, (P.Inspection*)address));
        HostCheck.Equal(P.Status.Success, P.Copy(f.State, token, f.Output)); f.Sample(); HostCheck.Equal(P.Status.Success, P.Stop(f.State));
    }

    [HostTest("borrowed retired inspection is exact local epoch only and never dereferences released backend or writes output")]
    private static void RetiredInspection()
    {
        using var f = new Fixture(); HostCheck.Equal(P.Status.Success, P.Stop(f.State)); HostCheck.Equal(C.Status.Success, C.Stop(f.Engine));
        new Span<byte>(f.Bytes, 4096).Clear(); P.Admission* input = (P.Admission*)((byte*)f.State + Offset<P.State>("Input"));
        // Substitute mapped zero RAM to detect any attempted C/B validation;
        // exact local retirement must not depend on released backend contents.
        input->Engine = (C.State*)f.Bytes; input->Device = (B.State*)(f.Bytes + 1024);
        HostCheck.Equal(P.Status.Retired, P.Inspect(f.State, f.Input.Epoch, null));
        HostCheck.Equal(P.Status.Retired, P.Inspect(f.State, f.Input.Epoch, (P.Inspection*)f.State));
        HostCheck.Equal(P.Status.InvalidState, P.Inspect(f.State, f.Input.Epoch + 1, null));
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512) && f.Output.IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("borrowed device failures retain cleanup and dirty engine blocks reads without losing its journal")]
    private static void FailureRetention()
    {
        using (var f = new Fixture())
        {
            var token = f.Read(1); f.Device.Complete(length: 1, status: 1); P.Completion result = default;
            HostCheck.Equal(P.Status.DeviceFailed, P.Poll(f.State, token, &result)); f.Sample();
            HostCheck.True(result.BlockFailure == B.Status.DeviceIoError && result.Physical.Bytes == 0 && result.Physical.Kind == B.Operation.Read && f.State->RequestActive);
            HostCheck.True(CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512)); HostCheck.Equal(P.Status.DeviceFailed, P.Stop(f.State));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Block)); HostCheck.Equal(P.Status.Success, P.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            var file = f.Open(out _); f.Media.FailPublication = 1; C.RequestToken request = default;
            HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, file, 0, new byte[] { 6 }, &request));
            HostCheck.Equal(C.Status.RecoveryRequired, f.RunEngine()); HostCheck.True(f.Engine->Dirty);
            byte[] before = new ReadOnlySpan<byte>(f.Media.Input.Before, 65536).ToArray(); P.Token token = default; uint calls = f.Device.Hardware->Calls;
            HostCheck.Equal(P.Status.DeviceFailed, P.Submit(f.State, 1, &token));
            HostCheck.True(CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512) && f.Device.Hardware->Calls == calls &&
                new ReadOnlySpan<byte>(f.Media.Input.Before, 65536).SequenceEqual(before));
            HostCheck.Equal(P.Status.Success, P.Stop(f.State)); HostCheck.Equal(C.Status.RecoveryRequired, C.Stop(f.Engine));
        }
    }

    [HostTest("borrowed port self epoch generation and premature engine retirement fail closed")]
    private static void IdentityAndExhaustion()
    {
        using var f = new Fixture();
        P.State* copy = (P.State*)NativeMemory.AllocZeroed((nuint)sizeof(P.State));
        try { *copy = *f.State; HostCheck.True(!CanRetain(copy, f.Input.Epoch, (ulong)f.Bytes, 512)); HostCheck.Equal(P.Status.InvalidState, P.Stop(copy)); }
        finally { NativeMemory.Free(copy); }
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch + 1, (ulong)f.Bytes, 512));
        ulong* generation = (ulong*)((byte*)f.State + Offset<P.State>("Generation")); *generation = ulong.MaxValue;
        uint calls = f.Device.Hardware->Calls; P.Token token = default;
        HostCheck.Equal(P.Status.GenerationExhausted, P.Submit(f.State, 1, &token)); HostCheck.Equal(calls, f.Device.Hardware->Calls);
        HostCheck.Equal(C.Status.Success, C.Stop(f.Engine)); f.AllowInvalidStop = true;
        HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512)); HostCheck.Equal(P.Status.InvalidState, P.Stop(f.State));
    }

    private static int Offset<T>(string field) where T : struct => checked((int)Marshal.OffsetOf<T>(field));
    private sealed class Fixture : IDisposable
    {
        private static long epoch = 0x8090;
        internal readonly ContinuingFat32MutationTests.Fixture Media = new(observe: true);
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal readonly byte* Bytes = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        internal P.State* State => (P.State*)(backing + 4096);
        internal C.State* Engine => Media.State;
        internal B.State* Block => Media.Device.State;
        internal WritableBlockSessionTests.Fixture Device => Media.Device;
        internal Span<byte> Output => new(Bytes, 512);
        internal P.Admission Input;
        internal bool AllowInvalidStop;
        internal Fixture(bool start = true)
        {
            new Span<byte>(backing, 12288).Fill(0xa9); new Span<byte>(State, sizeof(P.State)).Clear(); Output.Fill(0x63);
            for (int i = 512; i < 4096; i++) Media.Volatile[i] = unchecked((byte)(i * 37 + 9));
            Media.Mount(); Input = new() { Engine = Engine, Device = Block, Epoch = (ulong)Interlocked.Increment(ref epoch), EngineEpoch = Media.Input.Epoch,
                DeviceEpoch = Device.Input.Epoch, FirstLba = 1, LastLba = 7 };
            if (start) HostCheck.Equal(P.Status.Success, P.Start(State, Input));
        }
        internal B.Observation Sample() => *Device.Observation;
        internal P.Token Read(ulong lba) { P.Token token = default; HostCheck.Equal(P.Status.Success, P.Submit(State, lba, &token)); Sample(); return token; }
        internal void FinishRead(P.Token token)
        { Device.Complete(); P.Completion result = default; HostCheck.Equal(P.Status.Ready, P.Poll(State, token, &result)); Sample(); HostCheck.Equal(P.Status.Success, P.Copy(State, token, Output)); Sample(); }
        internal C.FileToken Open(out C.RequestToken consumed)
        {
            C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(Engine, "DATA.BIN", &request));
            HostCheck.Equal(C.Status.Ready, RunEngine()); C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(Engine, request, &file)); consumed = request; return file;
        }
        internal C.Status RunEngine()
        {
            for (int i = 0; i < 100000; i++) { var status = Media.Turn(); Sample(); if (status != C.Status.Pending) return status; }
            throw new InvalidOperationException("borrowed port engine did not terminate");
        }
        internal ulong[] Owned() => new[] { (ulong)State, (ulong)State + (ulong)sizeof(P.State) - 8, (ulong)Engine,
            (ulong)Media.Input.Changes, (ulong)Media.Input.Before, (ulong)Media.Input.After, (ulong)Media.Input.DirectoryMap,
            (ulong)Media.Input.FileMap, (ulong)Media.Input.WalkMap, (ulong)Media.Input.Sector, (ulong)Media.Input.Fat0, (ulong)Media.Input.Fat1,
            (ulong)Media.Input.Input, (ulong)Media.Input.Name, (ulong)Media.Input.LongName, (ulong)Media.Input.EncodedEntries,
            (ulong)Block, (ulong)Device.Pool, (ulong)Device.Slot, (ulong)Device.Data, (ulong)Device.Observation, 0x400000UL, 0x402000UL, 0x403000UL };
        internal (ulong Start, ulong Bytes)[] OwnedExtents() => new (ulong, ulong)[]
        {
            ((ulong)State, (ulong)sizeof(P.State)), ((ulong)Engine, (ulong)sizeof(C.State)),
            ((ulong)Media.Input.Changes, C.JournalChanges * (ulong)sizeof(OS.Storage.Fat32Mutation.Change)),
            ((ulong)Media.Input.Before, C.JournalChanges * 512UL), ((ulong)Media.Input.After, C.JournalChanges * 512UL),
            ((ulong)Media.Input.DirectoryMap, C.MaximumClusters * 4UL), ((ulong)Media.Input.FileMap, C.MaximumClusters * 4UL),
            ((ulong)Media.Input.WalkMap, C.MaximumClusters * 4UL), ((ulong)Media.Input.Sector, 512),
            ((ulong)Media.Input.Fat0, 512), ((ulong)Media.Input.Fat1, 512), ((ulong)Media.Input.Input, C.WriteBytes),
            ((ulong)Media.Input.Name, C.NameWords * 2UL), ((ulong)Media.Input.LongName, C.VfatWords * 2UL),
            ((ulong)Media.Input.EncodedEntries, C.EncodedEntryBytes), ((ulong)Block, (ulong)sizeof(B.State)),
            ((ulong)Device.Pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)), ((ulong)Device.Slot, (ulong)sizeof(OS.Drivers.DmaBufferSlot)),
            ((ulong)Device.Data, 4096), ((ulong)Device.Observation, (ulong)sizeof(B.Observation)),
            (Device.Input.Common.VirtualStart, Device.Input.Common.Bytes), (Device.Input.Notify.VirtualStart, Device.Input.Notify.Bytes),
            (Device.Input.Device.VirtualStart, Device.Input.Device.Bytes)
        };
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Block)); // External fixture owner, never the port.
            var stopped = P.Stop(State); if (!AllowInvalidStop) HostCheck.Equal(P.Status.Success, stopped);
            HostCheck.True(new ReadOnlySpan<byte>(backing, 4096).IndexOfAnyExcept((byte)0xa9) < 0 &&
                new ReadOnlySpan<byte>(backing + 4096 + sizeof(P.State), 8192 - sizeof(P.State)).IndexOfAnyExcept((byte)0xa9) < 0);
            Media.Dispose(); NativeMemory.AlignedFree(backing); NativeMemory.AlignedFree(Bytes);
        }
    }
}
