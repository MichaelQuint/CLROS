using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class WritableBlockReadPortTests
{
    private static readonly Retain CanRetain = typeof(P).GetMethod("CanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Retain>();
    private static readonly RetainRanges CanRetainRanges = typeof(P).GetMethod("CanRetainStorageRanges", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<RetainRanges>();

    [HostTest("borrowed port range batch preserves complete scalar aliases boundaries and bounded zero skipping without effects")]
    private static void RetainedBatchRanges()
    {
        using var f = new Fixture(); ulong address = (ulong)f.Bytes;
        BatchQueriesOnly(f, () =>
        {
            ulong[] valid = [address + 1, 17, address + 8, 24, address + 80, 48];
            BatchEquivalent(f, valid, true);
            BatchEquivalent(f, [address, 1, address, 1, address + 1, 1, 0, 0, ulong.MaxValue, 0], true);
            HostCheck.True(CanRetainRanges(f.State, f.Input.Epoch, [0, 0, ulong.MaxValue, 0]));
            HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, []) && !CanRetainRanges(f.State, f.Input.Epoch, [address]));
            ulong[] maximum = new ulong[177 * 2]; maximum[maximum.Length - 2] = address; maximum[maximum.Length - 1] = 512;
            BatchEquivalent(f, maximum, true);
            HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, new ulong[maximum.Length + 2]));
            foreach (ulong[] bad in new ulong[][] { [0, 1], [4095, 1], [ulong.MaxValue - 7, 16], [0x0000800000000000, 1] })
                BatchEquivalent(f, bad, false);
            foreach (var extent in f.OwnedExtents())
                foreach (var overlap in new[] { extent, (extent.Start, 1UL), (extent.Start - 1, extent.Bytes + 2),
                    (extent.Start + extent.Bytes - 1, 2UL) })
                    for (int position = 0; position < 3; position++)
                    {
                        ulong[] changed = valid.ToArray(); changed[position * 2] = overlap.Item1; changed[position * 2 + 1] = overlap.Item2;
                        BatchEquivalent(f, changed, false);
                    }
            HostCheck.True(CanRetainRanges(f.State, f.Input.Epoch, [address + 4095, 1]));
        });
    }

    [HostTest("borrowed port range batch revalidates exact original owner geometry epochs and every retained rewrite even for empty candidates")]
    private static void RetainedBatchOwners()
    {
        using var f = new Fixture(); ulong[] valid = [(ulong)f.Bytes, 512];
        HostCheck.True(!CanRetainRanges(null, f.Input.Epoch, valid) && !CanRetainRanges((P.State*)ulong.MaxValue, f.Input.Epoch, valid));
        P.State copy = *f.State;
        HostCheck.True(!CanRetainRanges(&copy, f.Input.Epoch, valid) && !CanRetainRanges(f.State, 0, valid));
        var port = PortInput(ref *f.State); var engine = EngineInput(ref *f.Engine); var block = BlockInput(ref *f.Block);
        ulong pool = PoolEpoch(ref *f.Device.Pool), self = PortSelf(ref *f.State);
        try
        {
            for (int defect = 0; defect < 14; defect++)
            {
                PortInput(ref *f.State) = port; EngineInput(ref *f.Engine) = engine; BlockInput(ref *f.Block) = block;
                PoolEpoch(ref *f.Device.Pool) = pool; PortSelf(ref *f.State) = self;
                if (defect == 0) PortInput(ref *f.State).Epoch++;
                else if (defect == 1) PortInput(ref *f.State).EngineEpoch++;
                else if (defect == 2) EngineInput(ref *f.Engine).Epoch++;
                else if (defect == 3) BlockInput(ref *f.Block).Epoch++;
                else if (defect == 4) PoolEpoch(ref *f.Device.Pool)++;
                else if (defect == 5) EngineInput(ref *f.Engine).PartitionFirst++;
                else if (defect == 6) BlockInput(ref *f.Block).Observation = (B.Observation*)engine.Before;
                else if (defect == 7) EngineInput(ref *f.Engine).Before = engine.After;
                else if (defect == 8) PortInput(ref *f.State).Device = (B.State*)((byte*)f.Block + 8);
                else if (defect == 9) PortInput(ref *f.State).Engine = (C.State*)ulong.MaxValue;
                else if (defect == 10) PortSelf(ref *f.State)++;
                else if (defect == 11) PortInput(ref *f.State).FirstLba = engine.PartitionFirst;
                else if (defect == 12) PortInput(ref *f.State).LastLba = f.Block->SectorCount;
                else PortInput(ref *f.State).DeviceEpoch++;
                BatchQueriesOnly(f, () =>
                {
                    BatchEquivalent(f, valid, false);
                    HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, [0, 0, ulong.MaxValue, 0]));
                });
            }
        }
        finally
        {
            PortInput(ref *f.State) = port; EngineInput(ref *f.Engine) = engine; BlockInput(ref *f.Block) = block;
            PoolEpoch(ref *f.Device.Pool) = pool; PortSelf(ref *f.State) = self;
        }
        BatchEquivalent(f, valid, true);
        HostCheck.Equal(P.Status.Success, P.Stop(f.State));
        try
        {
            PortInput(ref *f.State).Engine = (C.State*)ulong.MaxValue; PortInput(ref *f.State).Device = (B.State*)ulong.MaxValue;
            HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, valid) && !CanRetainRanges(f.State, f.Input.Epoch, [0, 0]));
        }
        finally { PortInput(ref *f.State) = port; }
    }

    [HostTest("borrowed port range batch keeps actual pending held cancelled and stopped read ownership structural")]
    private static void RetainedBatchReads()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Fixture(); ulong[] pairs = [(ulong)f.Bytes, 512]; var token = f.Read(1);
            if (held) { f.Device.Complete(); P.Completion ready = default; HostCheck.Equal(P.Status.Ready, P.Poll(f.State, token, &ready)); }
            BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true));
            HostCheck.Equal(held ? P.Status.Cancelled : P.Status.Pending, P.Cancel(f.State, token));
            BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true));
            if (!held) { f.Device.Complete(); P.Completion cancelled = default; HostCheck.Equal(P.Status.Cancelled, P.Poll(f.State, token, &cancelled)); }
            HostCheck.Equal(B.Status.Success, B.Stop(f.Block));
            BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true));
            HostCheck.Equal(P.Status.Success, P.Stop(f.State));
            HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, pairs) && !CanRetainRanges(f.State, f.Input.Epoch, [0, 0]));
        }
    }

    [HostTest("borrowed port range batch preserves real dirty write flush held metadata and fault cleanup lifetimes")]
    private static void RetainedBatchMutation()
    {
        using (var f = new Fixture())
        {
            var file = f.Open(out _); C.RequestToken request = default;
            HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.Engine, file, 0, new byte[37], &request));
            ulong[] pairs = [(ulong)f.Bytes, 512]; bool dirty = false, flush = false;
            for (int i = 0; i < 2000; i++)
            {
                var status = C.Pump(f.Engine);
                if (!dirty && f.Engine->Dirty && f.Block->LastOperation == B.Operation.Write)
                { BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true)); dirty = true; }
                if (!flush && f.Block->RequestPending && f.Block->LastOperation == B.Operation.Flush)
                { BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true)); flush = true; }
                if (status == C.Status.Ready) break;
                HostCheck.Equal(C.Status.Pending, status); HostCheck.True(i < 1999);
                if (f.Device.Hardware->Pending) f.Device.Complete();
            }
            HostCheck.True(dirty && flush && f.Engine->IsReady && f.Engine->RequestActive);
            BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true));
            C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.Engine, request, &result));
        }
        using (var f = new Fixture())
        {
            var token = f.Read(1); f.Device.Complete(length: 1, status: 1); P.Completion failure = default;
            HostCheck.Equal(P.Status.DeviceFailed, P.Poll(f.State, token, &failure));
            ulong[] pairs = [(ulong)f.Bytes, 512]; BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Block)); BatchQueriesOnly(f, () => BatchEquivalent(f, pairs, true));
            HostCheck.Equal(P.Status.Success, P.Stop(f.State)); HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, pairs));
        }
    }

    [HostTest("borrowed port range batch performs three complete engine audits independent of external inventory length")]
    private static void RetainedBatchAuditCount()
    {
        using var f = new Fixture(); ulong counter = 0;
        foreach (int count in new[] { 1, 25, 177 })
        {
            ulong[] pairs = new ulong[count * 2];
            for (int i = 0; i < count; i++) { pairs[i * 2] = (ulong)f.Bytes + (ulong)(i % 64); pairs[i * 2 + 1] = 1; }
            OS.Kernel.Diagnostics.WritableStorageTiming.State timing = default;
            HostCheck.True(OS.Kernel.Diagnostics.WritableStorageTiming.Begin(&timing, (ulong)&counter, 64));
            bool value = false, ended = false;
            try { value = CanRetainRanges(f.State, f.Input.Epoch, pairs); ended = OS.Kernel.Diagnostics.WritableStorageTiming.End(&timing); }
            finally { if (OS.Kernel.Diagnostics.WritableStorageTiming.IsActive) OS.Kernel.Diagnostics.WritableStorageTiming.Cancel(&timing); }
            HostCheck.True(value && ended && timing.AuditCalls == 3 && timing.OutsideDeviceCalls == 42);
        }
    }

    private static void BatchEquivalent(Fixture f, ReadOnlySpan<ulong> pairs, bool expected)
    {
        bool scalar = true;
        for (int i = 0; i < pairs.Length; i += 2) if (pairs[i + 1] != 0) scalar &= CanRetain(f.State, f.Input.Epoch, pairs[i], pairs[i + 1]);
        HostCheck.Equal(expected, scalar); HostCheck.Equal(expected, CanRetainRanges(f.State, f.Input.Epoch, pairs));
    }
    private static void BatchQueriesOnly(Fixture f, Action query)
    {
        byte[] Snapshot()
        {
            var bytes = new List<byte>(); var a = f.Device.Input;
            foreach (var extent in f.OwnedExtents())
                if (extent.Start != a.Common.VirtualStart && extent.Start != a.Notify.VirtualStart && extent.Start != a.Device.VirtualStart)
                    bytes.AddRange(new ReadOnlySpan<byte>((void*)extent.Start, (int)extent.Bytes).ToArray());
            bytes.AddRange(new ReadOnlySpan<byte>(f.Device.Hardware, sizeof(WritableBlockSessionTests.Hardware)).ToArray());
            bytes.AddRange(new ReadOnlySpan<byte>(f.Bytes, 4096).ToArray()); return bytes.ToArray();
        }
        byte[] before = Snapshot(); query(); HostCheck.True(before.AsSpan().SequenceEqual(Snapshot()));
    }
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref P.Admission PortInput(ref P.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref C.Admission EngineInput(ref C.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Input")]
    private static extern ref B.Admission BlockInput(ref B.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Epoch")]
    private static extern ref ulong PoolEpoch(ref OS.Drivers.DmaBufferPool pool);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Self")]
    private static extern ref ulong PortSelf(ref P.State state);

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
            if (restart) HostCheck.True(!CanRetain(f.State, f.Input.Epoch, (ulong)f.Bytes, 512) &&
                !CanRetainRanges(f.State, f.Input.Epoch, [(ulong)f.Bytes, 512]) && !CanRetainRanges(f.State, f.Input.Epoch, [0, 0]));
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
        HostCheck.True(!CanRetainRanges(f.State, f.Input.Epoch, [(ulong)f.Bytes, 512]) && !CanRetainRanges(f.State, f.Input.Epoch, [0, 0]));
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
}
