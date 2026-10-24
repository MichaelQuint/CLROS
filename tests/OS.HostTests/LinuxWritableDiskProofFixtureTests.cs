using System.Buffers.Binary;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Block = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

internal static unsafe class LinuxWritableDiskProofFixtureTests
{
    [HostTest("writable disk fixture admits canonical bounded RX RW package without child or IRQ requests")]
    private static void Package()
    {
        byte[] package = F.CreatePackage(), elf = F.CreateElf(); var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded && view.DataFileCount == 0 && view.Manifest.Threads == 2 && view.Manifest.Descriptors == 16);
        HostCheck.True(package.SequenceEqual(F.CreatePackage()) && package.AsSpan((int)view.PayloadOffset).SequenceEqual(elf));
        string manifest = Encoding.ASCII.GetString(package, (int)view.ManifestOffset, (int)view.ManifestBytes);
        HostCheck.True(manifest.Contains("app.path=apps/m16writabledisk.elf\nargs=WritableDisk\n", StringComparison.Ordinal));
        HostCheck.True(!manifest.Contains("CLROS_USER_IRQS", StringComparison.Ordinal));
        fixed (byte* input = elf)
        {
            HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(input, (uint)elf.Length,
                view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
            HostCheck.True(plan.LoadCount == 2 && plan.Entry == 0x400200 && plan.ImageBytes == 0x4000);
        }
        HostCheck.Equal(5U, U32(elf, 68)); HostCheck.Equal(6U, U32(elf, 124));
        HostCheck.Equal(4096UL, U64(elf, 96)); HostCheck.Equal(12288UL, U64(elf, 152));
        F.RequirePackage(package);
        byte[] changed = (byte[])package.Clone(); changed[^1] ^= 1; Reject(changed);
        Reject(package.AsSpan(0, package.Length - 1)); Reject(LinuxDiskFileProofFixture.CreatePackage()); Reject([]);
        static void Reject(ReadOnlySpan<byte> bytes)
        {
            bool rejected = false;
            try { F.RequirePackage(bytes); } catch (ToolFailure failure) when (failure.Reason == "writable_disk_package_identity") { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("writable disk syscall and instruction inventories match independent finite byte decoding")]
    private static void DecodeValues()
    {
        byte[] elf = F.CreateElf(); var decoded = Decode(elf); var calls = F.Calls();
        HostCheck.True(F.InstructionRips().SequenceEqual(decoded.Starts)); HostCheck.Equal(30, decoded.Calls.Length);
        HostCheck.True(calls.Count == decoded.Calls.Length && F.ExpectedCalls == calls.Count);
        HostCheck.True(F.ExpectedReturnRips().SequenceEqual(decoded.Calls.Select(c => c.Rip)));
        for (int i = 0; i < calls.Count; i++)
        {
            var expected = calls[i]; var actual = decoded.Calls[i];
            HostCheck.True(expected.Number == actual.Number && expected.A0 == actual.A0 && expected.A1 == actual.A1 && expected.A2 == actual.A2 &&
                expected.A3 == actual.A3 && expected.A4 == actual.A4 && expected.A5 == actual.A5 && expected.ReturnRip == actual.Rip);
            if (actual.Number != 231)
            {
                int at = (int)(actual.Rip - F.ImageBase);
                HostCheck.True(elf[at] == 0x48 && elf[at + 1] == 0x3d);
                HostCheck.Equal(expected.Result, (long)BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + 2)));
            }
            HostCheck.True(decoded.Starts.Contains(actual.Rip));
        }
        HostCheck.True(decoded.Calls.All(c => c.Number is 0 or 1 or 2 or 3 or 8 or 17 or 72 or 74 or 87 or 231 or 328));
        HostCheck.True(decoded.Starts.Distinct().Count() == decoded.Starts.Length && decoded.Starts.Zip(decoded.Starts.Skip(1)).All(p => p.First < p.Second));
        HostCheck.True(decoded.Calls[^1] is { Number: 231, A0: 42 } && F.SuccessRip == decoded.Calls[^1].Rip);
        HostCheck.True(!decoded.Starts.Contains(F.FailureExitRip));
        int failed = (int)(F.FailureExitRip - F.ImageBase);
        HostCheck.True(elf[failed - 2] == 0x0f && elf[failed - 1] == 5);
        foreach (var call in decoded.Calls.Where(c => c.Number != 328)) HostCheck.True(call.A4 == 0 && call.A5 == 0);
    }

    [HostTest("writable disk embedded paths vector flock and varying payload remain disjoint and bounded")]
    private static void MemoryValues()
    {
        byte[] elf = F.CreateElf(); var calls = Decode(elf).Calls; var write = calls.Single(c => c.Number == 328);
        HostCheck.True(write is { A0: 3, A2: 1, A3: 4094, A4: 0, A5: 0x20 });
        HostCheck.Equal(F.Payload, U64(elf, (int)(write.A1 - F.ImageBase)));
        HostCheck.Equal(1032UL, U64(elf, (int)(write.A1 - F.ImageBase + 8)));
        HostCheck.True(write.A3 % 512 == 510 && write.A3 / 4096 != (write.A3 + 1031) / 4096);
        HostCheck.True(1032 <= 8192 && 5126 == write.A3 + 1032);
        HostCheck.Equal("/data/m16-write.bin", Text(elf, F.FileName)); HostCheck.Equal("/data", Text(elf, F.RootName));
        HostCheck.Equal("/data/../private/sentinel", Text(elf, F.OutsideName));
        HostCheck.True(Text(elf, F.FileName).Length < 255 && Text(elf, F.FileName).Count(c => c == '/') == 2);
        HostCheck.True(F.Iovec + 16 <= F.WriteLock && F.Unlock + 32 <= F.Zeroes && F.Zeroes + 16 < F.Payload && F.Payload + 1032 < F.Output);
        HostCheck.True(F.Output + 4096 == F.ImageBase + (ulong)elf.Length);
        foreach (ulong address in new[] { F.WriteLock, F.QueryLock, F.Unlock })
        {
            int at = (int)(address - F.ImageBase);
            HostCheck.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(at + 2)));
            HostCheck.Equal(1073741825UL, U64(elf, at + 8)); HostCheck.Equal(1UL, U64(elf, at + 16));
            HostCheck.True(elf.AsSpan(at + 24, 8).IndexOfAnyExcept((byte)0) < 0);
        }
        HostCheck.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan((int)(F.WriteLock - F.ImageBase))));
        HostCheck.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan((int)(F.Unlock - F.ImageBase))));
        byte[] payload = F.ExpectedPayload();
        HostCheck.True(payload.SequenceEqual(elf.AsSpan((int)(F.Payload - F.ImageBase), 1032).ToArray()));
        for (int i = 0; i < payload.Length; i++) HostCheck.Equal(unchecked((byte)((i * 73 + 19) ^ (i >> 3) ^ (i >> 9))), payload[i]);
        HostCheck.True(payload.Distinct().Count() > 200 && elf.AsSpan((int)(F.Output - F.ImageBase), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.True(elf.AsSpan((int)(F.Zeroes - F.ImageBase), 16).IndexOfAnyExcept((byte)0) < 0);
        var copied = F.Calls();
        HostCheck.True(copied.Single(c => c.Role == "read.reopened").OutputBytes == 1032 && copied.Single(c => c.Role == "lock.get-own").OutputBytes == 32);
        HostCheck.True(copied.Where(c => c.OutputBytes != 0).All(c => c.OutputBytes <= 1032));
    }

    [HostTest("writable disk emitted assertions exercise finite file lifetimes offsets locks and parent synchronization")]
    private static void FiniteModel()
    {
        var result = Execute(F.CreateElf());
        HostCheck.Equal(42, result.Exit); HostCheck.Equal(30, result.Calls.Count);
        HostCheck.True(result.Calls.Select(c => c.Rip).SequenceEqual(F.ExpectedReturnRips()));
        HostCheck.True(result.FileSyncs == 1 && result.DirectorySyncs == 2 && result.Locks.SequenceEqual(new ulong[] { 6, 5, 6 }));
        HostCheck.True(result.OpenCount == 0 && result.Unlinked && result.Witness == "M16_WRITABLE_DISK_OK!");
        HostCheck.Equal(5126, result.SavedFile.Length);
        HostCheck.True(result.SavedFile.AsSpan(0, 4094).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(result.SavedFile.AsSpan(4094).SequenceEqual(F.ExpectedPayload()));
        HostCheck.True(result.ReopenedReadBytes == 1032 && result.IndependentReadCursor == 4102 && result.PositionedOwnerCursor == 0);
        // These are independent finite bytecode/model controls, not actual
        // Linux execution, device failure injection or observed flush facts.
        HostCheck.Equal(99, Execute(F.CreateElf(), corruptReopened: true).Exit);
        HostCheck.Equal(99, Execute(F.CreateElf(), failFileSync: true).Exit);
        byte[] badFlags = F.CreateElf(); int flags = (int)(F.Calls().Single(c => c.Number == 328).ReturnRip - F.ImageBase) - 6;
        BinaryPrimitives.WriteUInt32LittleEndian(badFlags.AsSpan(flags), 0);
        HostCheck.Equal(99, Execute(badFlags).Exit);
    }

    [HostTest("writable disk copied metadata payload and canonical bytes have independent ownership")]
    private static void Immutable()
    {
        var calls = F.Calls(); var rips = F.InstructionRips(); var returns = F.ExpectedReturnRips();
        bool a = false, b = false, c = false;
        try { ((IList<F.CallSpec>)calls)[0] = default; } catch (NotSupportedException) { a = true; }
        try { ((IList<ulong>)rips)[0] = 0; } catch (NotSupportedException) { b = true; }
        try { ((IList<ulong>)returns)[0] = 0; } catch (NotSupportedException) { c = true; }
        HostCheck.True(a && b && c);
        byte[] elf = F.CreateElf(), package = F.CreatePackage(), payload = F.ExpectedPayload();
        Array.Clear(elf); Array.Clear(package); Array.Clear(payload);
        HostCheck.True(calls.SequenceEqual(F.Calls()) && rips.SequenceEqual(F.InstructionRips()) && returns.SequenceEqual(F.ExpectedReturnRips()));
        HostCheck.True(!ReferenceEquals(calls, F.Calls()) && !ReferenceEquals(rips, F.InstructionRips()) && !ReferenceEquals(returns, F.ExpectedReturnRips()));
        HostCheck.True(F.ExpectedPayload().Any(v => v != 0) && F.CreateElf()[0] == 0x7f);
    }

    [HostTest("writable observation values join actual OUT FLUSH and held IN copies without granting execution authority")]
    private static void ObservedValues()
    {
        var recorded = RecordedChain(); var report = Inspect(recorded);
        HostCheck.True(report.Requests.Count == 4 && report.SampleCount == recorded.Rows.Length && report.ObservedProtocolDrained &&
            !report.Dirty && !report.HasDeviceError && !report.GrantsDeviceOrPersistenceAuthority && report.After == recorded.After);
        HostCheck.True(report.Requests.Select(r => r.Kind).SequenceEqual(new[] { Block.Operation.Write, Block.Operation.Flush, Block.Operation.Read, Block.Operation.Flush }));
        HostCheck.True(report.Requests[0].CopyPayload().AsSpan().SequenceEqual(SectorPayload()) && report.Requests[2].CopyPayload().AsSpan().SequenceEqual(SectorPayload()));
        HostCheck.True(report.Requests[1].PayloadBytes == 0 && report.Requests[1].FlushTargetBytes == 512 && report.Requests[1].FlushAcknowledgedBytes == 512 &&
            report.Requests[3].FlushTargetBytes == 512 && report.Requests[3].FlushAcknowledgedBytes == 512);
        foreach (var request in report.Requests)
        {
            HostCheck.True(request.Request.Generation == request.SubmittedCount && request.CompletionValidated && request.CompletionSampleIndex > request.FirstSampleIndex &&
                request.LastSampleIndex >= request.CompletionSampleIndex && request.AvailableIndex == unchecked((ushort)request.SubmittedCount) &&
                request.ConsumedIndex == unchecked((ushort)request.CompletedCount));
            HostCheck.True(request.ReadCopySampleIndex == (request.Kind == Block.Operation.Read ? request.LastSampleIndex : -1));
        }
        var empty = F.InspectBlockObservationValues([], recorded.Before, recorded.Before);
        HostCheck.True(empty.Requests.Count == 0 && empty.ObservedProtocolDrained && !empty.Dirty);
    }

    [HostTest("writable observation values inspect actual single engine Pump combined IN completion copy and real write flush facts")]
    private static void ObservedEnginePump()
    {
        using var f = new ContinuingFat32MutationTests.Fixture(observe: true); f.Record = false;
        var before = Boundary(f.Device); List<Block.Observation> rows = []; Dictionary<ulong, byte[]> copied = [];
        HostCheck.Equal(C.Status.Pending, C.Start(f.State, f.Input)); HostCheck.Equal(C.Status.Ready, Drive());
        C.RequestToken token = default; C.FileToken file = default;
        HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(f.State, "DATA.BIN", &token)); HostCheck.Equal(C.Status.Ready, Drive());
        HostCheck.Equal(C.Status.Success, C.CopyOpen(f.State, token, &file));
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(f.State, file, 500, "actual protocol receipt across two sectors"u8, &token));
        HostCheck.Equal(C.Status.Ready, Drive()); C.Result result = default; HostCheck.Equal(C.Status.Success, C.CopyResult(f.State, token, &result));
        var report = F.InspectBlockObservationValues(rows.ToArray(), before, Boundary(f.Device));
        HostCheck.True(report.ObservedProtocolDrained && !report.Dirty && report.Requests.Count == (int)f.Device.State->Submitted &&
            report.Requests.Any(r => r.Kind == Block.Operation.Write) && report.Requests.Any(r => r.Kind == Block.Operation.Flush) && copied.Count != 0);
        foreach (var request in report.Requests.Where(r => r.Kind == Block.Operation.Read))
            HostCheck.True(request.ReadCopySampleIndex == request.CompletionSampleIndex && request.CopyPayload().AsSpan().SequenceEqual(copied[request.Request.Generation]));
        C.Stop(f.State);
        C.Status Drive()
        {
            for (int i = 0; i < 100000; i++)
            {
                var status = f.Turn(); Block.Observation value = *f.Device.Observation;
                if (value.Flags != Block.ObservationFlags.None)
                {
                    rows.Add(value);
                    if ((value.Flags & Block.ObservationFlags.ReadCopied) != 0 && !copied.ContainsKey(value.Request.Generation))
                    {
                        byte[] bytes = new ReadOnlySpan<byte>(value.Data, 512).ToArray();
                        HostCheck.True(bytes.AsSpan().SequenceEqual(f.Volatile.Slice(checked((int)value.Sector * 512), 512))); copied.Add(value.Request.Generation, bytes);
                    }
                }
                if (status != C.Status.Pending) return status;
            }
            throw new InvalidOperationException("engine observation did not terminate");
        }
    }

    [HostTest("writable observation values retain pending publication failures without inventing completion reset retry or flush")]
    private static void ObservedPartialPublication()
    {
        uint calls;
        using (var good = new BlockSeries()) { uint first = good.Device.Hardware->Calls; good.Write(SectorPayload()); calls = good.Device.Hardware->Calls - first; }
        int unpublished = 0, published = 0;
        for (uint at = 1; at <= calls; at++) foreach (bool after in new[] { false, true })
        {
            using var series = new BlockSeries(); series.Device.Hardware->FailCall = series.Device.Hardware->Calls + at; series.Device.Hardware->FailAfterWrite = after;
            Block.RequestToken token = default;
            HostCheck.True(Block.SubmitWrite(series.Device.State, 3, SectorPayload(), &token) != Block.Status.Success); series.Sample();
            var report = series.Inspect();
            if (series.Device.State->Submitted == 0) { unpublished++; HostCheck.True(report.Requests.Count == 0 && !report.Dirty); }
            else
            {
                published++; HostCheck.True(report.Requests.Count == 1 && report.HasUncompletedPublication && !report.ObservedProtocolDrained && report.Dirty &&
                    !report.HasDeviceError && report.Requests[0].DeviceResult == Block.Status.Uninitialized && report.Requests[0].CopyPayload().AsSpan().SequenceEqual(SectorPayload()));
            }
            HostCheck.True(report.After.Completed == 0 && report.After.CompletedBytes == 0 && report.After.FlushAcknowledgedBytes == 0 && !report.GrantsDeviceOrPersistenceAuthority);
        }
        HostCheck.True(unpublished != 0 && published != 0);
    }

    [HostTest("writable observation values preserve cancelled successful flush wire errors and dirty submitted bytes independently")]
    private static void ObservedErrorsAndCancellation()
    {
        foreach (byte status in new byte[] { 1, 2 })
        {
            using var series = new BlockSeries(); series.Write(SectorPayload()); series.Device.Complete(status: status);
            series.Poll(status == 1 ? Block.Status.DeviceIoError : Block.Status.UnsupportedRequest); var report = series.Inspect();
            HostCheck.True(report.HasDeviceError && report.Dirty && report.Requests[0].CompletionValidated && report.Requests[0].CompletionBytes == 0 &&
                report.Requests[0].PayloadBytes == 512 && report.After.SubmittedBytes == 512 && report.After.CompletedBytes == 0 && report.After.FlushAcknowledgedBytes == 0);
        }
        foreach (bool fail in new[] { false, true })
        {
            using var series = new BlockSeries(); series.Write(SectorPayload()); series.Device.Complete(); series.Poll(Block.Status.Success);
            var token = series.Flush(); HostCheck.Equal(Block.Status.Success, Block.Cancel(series.Device.State, token)); series.Sample();
            series.Device.Complete(status: fail ? (byte)1 : (byte)0); series.Poll(fail ? Block.Status.DeviceIoError : Block.Status.Cancelled);
            var report = series.Inspect(); var flush = report.Requests[^1];
            HostCheck.True(flush.Cancelled && flush.CompletionValidated && flush.PayloadBytes == 0 && flush.CompletionBytes == 0 && flush.FlushTargetBytes == 512 &&
                flush.FlushAcknowledgedBytes == (fail ? 0UL : 512UL) && report.Dirty == fail && report.HasDeviceError == fail);
        }
    }

    [HostTest("writable observation values distinguish cancelled pending and held IN from copied bytes and later requests")]
    private static void ObservedReadCancellation()
    {
        foreach (bool ready in new[] { false, true })
        {
            using var series = new BlockSeries(); var read = series.Read();
            if (ready) { series.Device.Complete(); series.Poll(Block.Status.Ready); HostCheck.True(series.Inspect().HasHeldRead); }
            HostCheck.Equal(Block.Status.Success, Block.Cancel(series.Device.State, read)); series.Sample();
            if (!ready) { series.Device.Complete(); series.Poll(Block.Status.Cancelled); }
            series.Flush(); series.Device.Complete(); series.Poll(Block.Status.Success);
            var report = series.Inspect(); var request = report.Requests[0];
            HostCheck.True(report.ObservedProtocolDrained && !report.HasHeldRead && request.Cancelled && request.CompletionValidated && !request.ReadCopied &&
                request.DeviceResult == Block.Status.Success && request.CompletionBytes == 512 && request.PayloadBytes == 0);
            byte[] output = Enumerable.Repeat((byte)0xa5, 512).ToArray();
            HostCheck.Equal(Block.Status.StaleToken, Block.CopyRead(series.Device.State, read, output)); HostCheck.True(output.All(b => b == 0xa5));
        }
    }

    [HostTest("writable observation values reject missing conflicting reused or late stages and forged payload status counts")]
    private static void ObservationMutations()
    {
        var recorded = RecordedChain(); int writeComplete = Array.FindIndex(recorded.Rows, r => r.Kind == Block.Operation.Write && (r.Flags & Block.ObservationFlags.CompletionValidated) != 0);
        int readBegin = Array.FindIndex(recorded.Rows, r => r.Kind == Block.Operation.Read), readComplete = readBegin + 1, readCopy = readBegin + 2;
        int flushBegin = Array.FindIndex(recorded.Rows, r => r.Kind == Block.Operation.Flush);
        Mutate(0, (ref Block.Observation row) => row.Request = TokenField(row.Request, 0, row.Request.Owner + 8));
        Mutate(0, (ref Block.Observation row) => row.Request = TokenField(row.Request, 1, row.Request.Epoch + 1));
        Mutate(0, (ref Block.Observation row) => row.Request = TokenField(row.Request, 2, row.Request.Generation + 1));
        Mutate(0, (ref Block.Observation row) => row.AvailableIndex++);
        Mutate(writeComplete, (ref Block.Observation row) => row.ConsumedIndex++);
        Mutate(writeComplete, (ref Block.Observation row) => row.CompletedCount++);
        Mutate(0, (ref Block.Observation row) => row.PayloadBytes = 511);
        Mutate(writeComplete, (ref Block.Observation row) => DataByte(ref row, 511, (byte)(SectorPayload()[511] ^ 1)));
        Mutate(writeComplete, (ref Block.Observation row) => row.DeviceResult = Block.Status.Timeout);
        Mutate(writeComplete, (ref Block.Observation row) => row.FlushAcknowledgedBytes = 512);
        Mutate(flushBegin, (ref Block.Observation row) => row.Sector = 1);
        Mutate(flushBegin, (ref Block.Observation row) => row.FlushTargetBytes = 0);
        Mutate(flushBegin, (ref Block.Observation row) => row.FlushAcknowledgedBytes = 512);
        Mutate(readBegin, (ref Block.Observation row) => row.Sector = recorded.Before.SectorCount);
        Mutate(readBegin, (ref Block.Observation row) => row.Flags |= Block.ObservationFlags.ReadCopied);
        Mutate(readComplete, (ref Block.Observation row) => row.PayloadBytes = 512);
        Mutate(readCopy, (ref Block.Observation row) => row.Cancelled = true);
        Mutate(readCopy, (ref Block.Observation row) => { row.Flags = Block.ObservationFlags.Published; row.CompletionBytes = 0; row.DeviceResult = Block.Status.Uninitialized; row.PayloadBytes = 0; ClearData(ref row); });
        HostCheck.Equal(4, F.InspectBlockObservationValues(recorded.Rows.Skip(1).ToArray(), recorded.Before, recorded.After).Requests.Count); // Removing an unchanged repeat loses no value fact.
        Reject(recorded.Rows.Where(row => row.Kind != Block.Operation.Write || row.Flags != Block.ObservationFlags.Published).ToArray(), recorded.Before, recorded.After);
        Reject(recorded.Rows.Where((_, i) => i != writeComplete).ToArray(), recorded.Before, recorded.After);
        Reject(recorded.Rows.Where((_, i) => i != readCopy).ToArray(), recorded.Before, recorded.After); // A later request cannot replace held IN.
        Reject(recorded.Rows, recorded.Before with { Epoch = recorded.Before.Epoch + 1 }, recorded.After);
        Reject(recorded.Rows, recorded.Before, recorded.After with { Submitted = recorded.After.Submitted + 1 });
        Reject(recorded.Rows, recorded.Before, recorded.After with { FlushAcknowledgedBytes = 0 });
        void Mutate(int index, ChangeObservation change, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(change))] string vector = "")
        { var rows = (Block.Observation[])recorded.Rows.Clone(); change(ref rows[index]); Reject(rows, recorded.Before, recorded.After, vector); }
    }

    [HostTest("writable observation values reject invented postcompletion cancellation and changed wire errors")]
    private static void ObservationTerminalMutation()
    {
        using (var series = new BlockSeries())
        {
            series.Write(SectorPayload()); series.Device.Complete(); series.Poll(Block.Status.Success);
            Block.Observation row = series.Rows[^1]; row.Cancelled = true; series.Rows.Add(row); Reject(series.Rows.ToArray(), series.Before, series.After);
        }
        using (var series = new BlockSeries())
        {
            series.Write(SectorPayload()); series.Device.Complete(status: 1); series.Poll(Block.Status.DeviceIoError);
            Block.Observation row = series.Rows[^1]; row.DeviceResult = Block.Status.UnsupportedRequest; series.Rows.Add(row); Reject(series.Rows.ToArray(), series.Before, series.After);
        }
    }

    [HostTest("writable observation values bound samples handle ushort wrap and fail checked counter overflow")]
    private static void ObservationBoundsAndWrap()
    {
        var original = RecordedChain(); const ulong shift = 65535; var rows = (Block.Observation[])original.Rows.Clone();
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].SubmittedCount += shift; rows[i].CompletedCount += shift;
            rows[i].Request = TokenField(rows[i].Request, 2, rows[i].Request.Generation + shift);
            rows[i].AvailableIndex = unchecked((ushort)rows[i].SubmittedCount); rows[i].ConsumedIndex = unchecked((ushort)rows[i].CompletedCount);
        }
        // Copied counter rebasing is a value vector, not execution of65535
        // requests. Prior clean FLUSHes can supply this drained baseline.
        var before = original.Before with { Submitted = shift, Completed = shift };
        var after = original.After with { Submitted = shift + original.After.Submitted, Completed = shift + original.After.Completed };
        var report = F.InspectBlockObservationValues(rows, before, after);
        HostCheck.True(report.Requests[0].AvailableIndex == 0 && rows[0].ConsumedIndex == ushort.MaxValue && report.Requests[^1].Request.Generation == shift + 4);
        var full = new Block.Observation[F.MaximumBlockObservationSamples]; original.Rows.CopyTo(full, 0); Array.Fill(full, original.Rows[^1], original.Rows.Length, full.Length - original.Rows.Length);
        HostCheck.Equal(F.MaximumBlockObservationSamples, F.InspectBlockObservationValues(full, original.Before, original.After).SampleCount);
        Reject(new Block.Observation[F.MaximumBlockObservationSamples + 1], original.Before, original.After);
        Reject(original.Rows, original.Before with { Submitted = ulong.MaxValue, Completed = ulong.MaxValue }, original.After);
        ulong bytes = ulong.MaxValue & ~511UL, count = bytes / 512;
        var near = original.Before with { Submitted = count, Completed = count, SubmittedBytes = bytes, CompletedBytes = bytes };
        Block.Observation overflow = original.Rows[0]; overflow.Request = TokenField(overflow.Request, 2, count + 1); overflow.SubmittedCount = count + 1;
        Reject([overflow], near, near with { Submitted = count + 1 });
    }

    [HostTest("writable observation values copy payload and scalar ownership without retaining mutable caller aliases")]
    private static void ObservationCopiedOwnership()
    {
        var recorded = RecordedChain(); var report = Inspect(recorded); byte[] expected = report.Requests[0].CopyPayload();
        Block.Observation changed = recorded.Rows[0]; DataByte(ref changed, 0, (byte)(expected[0] ^ 1)); recorded.Rows[0] = changed;
        Array.Clear(recorded.Rows); byte[] published = report.Requests[0].CopyPayload(); Array.Clear(published);
        HostCheck.True(report.Requests[0].CopyPayload().AsSpan().SequenceEqual(expected) && report.Requests[0].Request.Owner == recorded.Before.Owner && report.SampleCount != 0);
        bool readOnly = false;
        try { ((IList<F.BlockRequestFacts>)report.Requests)[0] = report.Requests[1]; } catch (NotSupportedException) { readOnly = true; }
        HostCheck.True(readOnly && report.Requests.Count == 4 && !report.GrantsDeviceOrPersistenceAuthority);
    }

    private delegate void ChangeObservation(ref Block.Observation value);
    private sealed record RecordedBlock(Block.Observation[] Rows, F.BlockBoundary Before, F.BlockBoundary After);
    private static byte[] SectorPayload() => Enumerable.Range(0, 512).Select(i => unchecked((byte)(i * 19 ^ (i >> 3) ^ 0x5a))).ToArray();
    private static F.BlockObservationInspection Inspect(RecordedBlock recorded) => F.InspectBlockObservationValues(recorded.Rows, recorded.Before, recorded.After);
    private static RecordedBlock RecordedChain()
    {
        using var series = new BlockSeries(); byte[] bytes = SectorPayload(); series.Write(bytes); Array.Clear(bytes);
        series.Poll(Block.Status.Pending); series.Device.Complete(); series.Poll(Block.Status.Success);
        series.Flush(); series.Device.Complete(); series.Poll(Block.Status.Success);
        var read = series.Read(); series.Device.Complete(); series.Poll(Block.Status.Ready);
        byte[] output = new byte[512]; HostCheck.Equal(Block.Status.Success, Block.CopyRead(series.Device.State, read, output)); series.Sample(); HostCheck.True(output.AsSpan().SequenceEqual(SectorPayload()));
        series.Flush(); series.Device.Complete(); series.Poll(Block.Status.Success);
        return new(series.Rows.ToArray(), series.Before, series.After);
    }
    private static void Reject(ReadOnlySpan<Block.Observation> values, F.BlockBoundary before, F.BlockBoundary after,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(values))] string vector = "")
    {
        bool rejected = false;
        try { F.InspectBlockObservationValues(values, before, after); }
        catch (ToolFailure failure) when (failure.Reason.StartsWith("writable_observation_", StringComparison.Ordinal)) { rejected = true; }
        HostCheck.True(rejected, "copied observation vector was admitted: " + vector);
    }
    private static Block.RequestToken TokenField(Block.RequestToken token, int field, ulong value)
    { HostCheck.Equal(24, sizeof(Block.RequestToken)); ((ulong*)&token)[field] = value; return token; }
    private static void DataByte(ref Block.Observation value, int offset, byte replacement)
    { fixed (byte* data = value.Data) data[offset] = replacement; }
    private static void ClearData(ref Block.Observation value)
    { fixed (byte* data = value.Data) new Span<byte>(data, 512).Clear(); }
    private static F.BlockBoundary Boundary(WritableBlockSessionTests.Fixture device) => new((ulong)device.State, device.Input.Epoch, device.State->SectorCount,
        device.State->Submitted, device.State->Completed, device.State->SubmittedBytes, device.State->CompletedBytes, device.State->DurableBytes);
    private sealed class BlockSeries : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device = new(observe: true);
        internal readonly List<Block.Observation> Rows = [];
        internal readonly F.BlockBoundary Before;
        internal F.BlockBoundary After => Boundary(Device);
        internal BlockSeries() { HostCheck.Equal(Block.Status.Success, Device.Start()); Before = Boundary(Device); }
        internal Block.RequestToken Write(byte[] bytes)
        { Block.RequestToken token = default; HostCheck.Equal(Block.Status.Success, Block.SubmitWrite(Device.State, 3, bytes, &token)); Sample(); return token; }
        internal Block.RequestToken Read()
        { Block.RequestToken token = default; HostCheck.Equal(Block.Status.Success, Block.SubmitRead(Device.State, 3, &token)); Sample(); return token; }
        internal Block.RequestToken Flush()
        { Block.RequestToken token = default; HostCheck.Equal(Block.Status.Success, Block.SubmitFlush(Device.State, &token)); Sample(); return token; }
        internal void Poll(Block.Status expected)
        { Block.Completion value = default; HostCheck.Equal(expected, Block.Poll(Device.State, &value)); Sample(); }
        internal void Sample() { if (Device.Observation->Flags != Block.ObservationFlags.None) Rows.Add(*Device.Observation); }
        internal F.BlockObservationInspection Inspect() => F.InspectBlockObservationValues(Rows.ToArray(), Before, After);
        public void Dispose() => Device.Dispose();
    }

    private readonly record struct Call(ulong Rip, ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5);
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
    private static string Text(byte[] memory, ulong address)
    {
        int at = checked((int)(address - F.ImageBase)); int count = memory.AsSpan(at, Math.Min(256, memory.Length - at)).IndexOf((byte)0);
        if (count < 0) throw new InvalidOperationException("unterminated fixture path");
        return Encoding.ASCII.GetString(memory, at, count);
    }
    // Independent decoder recognizes only this finite byte vocabulary. Every
    // conditional branch must target failure, and the final JMP cannot loop.
    private static (ulong[] Starts, Call[] Calls) Decode(byte[] elf)
    {
        List<ulong> starts = []; List<Call> calls = []; ulong[] r = new ulong[16];
        int end = checked((int)(F.MainEndRip - F.ImageBase));
        for (int at = F.EntryOffset; at < end;)
        {
            starts.Add(F.ImageBase + (uint)at); ReadOnlySpan<byte> code = elf.AsSpan(at); int bytes;
            if (code[0] is >= 0xb8 and <= 0xbf) { r[code[0] - 0xb8] = U32(elf, at + 1); bytes = 5; }
            else if (code[0] == 0x41 && code[1] is >= 0xb8 and <= 0xbf) { r[8 + code[1] - 0xb8] = U32(elf, at + 2); bytes = 6; }
            else if (code.StartsWith(new byte[] { 0x0f, 5 }))
            { calls.Add(new(F.ImageBase + (uint)at + 2, r[0], r[7], r[6], r[2], r[10], r[8], r[9])); bytes = 2; }
            else if (code.StartsWith(new byte[] { 0x31, 0xc0 })) bytes = 2;
            else if (code.StartsWith(new byte[] { 0x66, 0x8c, 0xc8 })) bytes = 3;
            else if (code.StartsWith(new byte[] { 0x48, 0x3d })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x0f, 0xb6, 5 })) bytes = 7;
            else if (code.StartsWith(new byte[] { 0x8b, 5 })) bytes = 6;
            else if (code[0] == 0xfc) bytes = 1;
            else if (code.StartsWith(new byte[] { 0xf3, 0xa6 })) bytes = 2;
            else if (code.StartsWith(new byte[] { 0x0f, 0x85 }))
            { bytes = 6; HostCheck.Equal(F.FailureOffset, at + bytes + BinaryPrimitives.ReadInt32LittleEndian(code[2..])); }
            else if (code[0] == 0xe9)
            { bytes = 5; HostCheck.Equal(end, at + bytes); HostCheck.Equal(F.FailureOffset, at + bytes + BinaryPrimitives.ReadInt32LittleEndian(code[1..])); }
            else throw new InvalidOperationException("unsupported writable fixture opcode " + at.ToString("x"));
            HostCheck.True(at + bytes <= end); at += bytes;
        }
        HostCheck.Equal((byte)0xcc, elf[end]); return (starts.ToArray(), calls.ToArray());
    }

    private sealed class ModelResult
    {
        public int Exit, FileSyncs, DirectorySyncs, OpenCount, ReopenedReadBytes;
        public long IndependentReadCursor, PositionedOwnerCursor;
        public bool Unlinked;
        public string Witness = "";
        public byte[] SavedFile = [];
        public readonly List<Call> Calls = [];
        public readonly List<ulong> Locks = [];
    }
    private sealed class Description(bool directory, bool writable)
    { public bool Directory = directory, Writable = writable; public long Offset; }

    // This small independent interpreter checks the emitted assertions and
    // syscall register bytes against a bounded file model. No kernel service,
    // hardware, NativeAOT, persistence or generic x64 execution is represented.
    private static ModelResult Execute(byte[] memory, bool corruptReopened = false, bool failFileSync = false)
    {
        ModelResult result = new(); Dictionary<int, Description> fds = []; byte[]? file = null;
        ulong[] r = new ulong[16]; bool equal = false, locked = false; int at = F.EntryOffset;
        int Address(ulong address, int count)
        {
            if (address < F.ImageBase + 4096 || address > F.ImageBase + (ulong)memory.Length || count < 0 || (ulong)count > F.ImageBase + (ulong)memory.Length - address)
                throw new InvalidOperationException("fixture model memory bounds");
            return (int)(address - F.ImageBase);
        }
        Call Current() => new(F.ImageBase + (uint)at + 2, r[0], r[7], r[6], r[2], r[10], r[8], r[9]);
        long SystemCall(Call c)
        {
            int fd = checked((int)c.A0); fds.TryGetValue(fd, out var d);
            if (c.Number == 2)
            {
                string path = Text(memory, c.A0);
                if (path.Contains("..", StringComparison.Ordinal)) return -13;
                bool directory = path == "/data";
                if (!directory && path != "/data/m16-write.bin") throw new InvalidOperationException("unexpected fixture path");
                if (directory && (c.A1 & 3) != 0) return -21;
                if (!directory && file is null)
                { if ((c.A1 & 0x40) == 0) return -2; HostCheck.True((c.A1 & 0x80) != 0 && c.A2 == 0x180); file = []; }
                else if (!directory && (c.A1 & 0xc0) == 0xc0) return -17;
                int selected = 3; while (fds.ContainsKey(selected)) selected++;
                fds.Add(selected, new(directory, (c.A1 & 3) == 2)); return selected;
            }
            if (c.Number == 87)
            { HostCheck.True(Text(memory, c.A0) == "/data/m16-write.bin" && file is not null && fds.Values.All(v => v.Directory)); result.SavedFile = (byte[])file!.Clone(); file = null; result.Unlinked = true; return 0; }
            if (c.Number == 1)
            { HostCheck.Equal(1, fd); result.Witness = Encoding.ASCII.GetString(memory, (int)(c.A1 - F.ImageBase), (int)c.A2); return (long)c.A2; }
            if (d is null) return -9;
            if (c.Number == 3)
            {
                if (fd == 4) result.IndependentReadCursor = d.Offset;
                if (d.Writable) result.PositionedOwnerCursor = d.Offset;
                fds.Remove(fd); locked = false; return 0;
            }
            if (c.Number == 8)
            { d.Offset = c.A2 switch { 0 => checked((long)c.A1), 1 => checked(d.Offset + (long)c.A1), _ => throw new InvalidOperationException("fixture seek whence") }; return d.Offset; }
            if (c.Number == 328)
            {
                if (c.A5 != 0x20) return -95;
                HostCheck.True(d.Writable && !d.Directory && c.A2 == 1 && c.A3 <= uint.MaxValue && c.A4 <= uint.MaxValue);
                int vector = Address(c.A1, 16); ulong pointer = U64(memory, vector), count = U64(memory, vector + 8);
                HostCheck.True(count is > 0 and <= 8192);
                int offset = checked((int)(c.A3 | c.A4 << 32)), size = checked((int)count);
                HostCheck.True(offset + size <= 8192); Array.Resize(ref file, offset + size);
                memory.AsSpan(Address(pointer, size), size).CopyTo(file.AsSpan(offset)); return size;
            }
            if (c.Number is 0 or 17)
            {
                HostCheck.True(!d.Directory && file is not null && c.A2 <= 8192);
                int offset = c.Number == 17 ? checked((int)c.A3) : checked((int)d.Offset);
                int count = Math.Min((int)c.A2, Math.Max(0, file!.Length - offset)); int output = Address(c.A1, count);
                file.AsSpan(offset, count).CopyTo(memory.AsSpan(output));
                if (c.Number == 0) d.Offset += count;
                if (c.Number == 17 && count == 1032)
                { result.ReopenedReadBytes = count; if (corruptReopened) memory[output + count - 1] ^= 1; }
                return count;
            }
            if (c.Number == 72)
            {
                HostCheck.True(d.Writable && c.A1 is 5 or 6); result.Locks.Add(c.A1);
                int flock = Address(c.A2, 32); ushort type = BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan(flock));
                HostCheck.True(BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan(flock + 2)) == 0 && U64(memory, flock + 8) == 1073741825 && U64(memory, flock + 16) == 1);
                if (c.A1 == 5) { HostCheck.True(locked && type == 1); BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(flock), 2); }
                else if (type == 1) { HostCheck.True(!locked); locked = true; }
                else { HostCheck.True(type == 2 && locked); locked = false; }
                return 0;
            }
            if (c.Number == 74)
            { if (d.Directory) result.DirectorySyncs++; else { result.FileSyncs++; if (failFileSync) return -5; } return 0; }
            throw new InvalidOperationException("unexpected fixture syscall " + c.Number);
        }
        for (int steps = 0; steps < 4096; steps++)
        {
            ReadOnlySpan<byte> code = memory.AsSpan(at); int size;
            if (code[0] is >= 0xb8 and <= 0xbf) { r[code[0] - 0xb8] = U32(memory, at + 1); size = 5; }
            else if (code[0] == 0x41 && code[1] is >= 0xb8 and <= 0xbf) { r[8 + code[1] - 0xb8] = U32(memory, at + 2); size = 6; }
            else if (code.StartsWith(new byte[] { 0x31, 0xc0 })) { r[0] = 0; size = 2; }
            else if (code.StartsWith(new byte[] { 0x66, 0x8c, 0xc8 })) { r[0] = 0x3b; size = 3; }
            else if (code.StartsWith(new byte[] { 0x48, 0x3d })) { equal = r[0] == unchecked((ulong)(long)BinaryPrimitives.ReadInt32LittleEndian(code[2..])); size = 6; }
            else if (code.StartsWith(new byte[] { 0x0f, 0xb6, 5 })) { size = 7; r[0] = memory[at + size + BinaryPrimitives.ReadInt32LittleEndian(code[3..])]; }
            else if (code.StartsWith(new byte[] { 0x8b, 5 })) { size = 6; r[0] = U32(memory, at + size + BinaryPrimitives.ReadInt32LittleEndian(code[2..])); }
            else if (code.StartsWith(new byte[] { 0x0f, 0x85 })) { size = 6; if (!equal) { at += size + BinaryPrimitives.ReadInt32LittleEndian(code[2..]); continue; } }
            else if (code[0] == 0xe9) { at += 5 + BinaryPrimitives.ReadInt32LittleEndian(code[1..]); continue; }
            else if (code[0] == 0xfc) size = 1;
            else if (code.StartsWith(new byte[] { 0xf3, 0xa6 }))
            {
                size = 2; equal = true;
                while (r[1] != 0)
                { equal = memory[Address(r[7]++, 1)] == memory[Address(r[6]++, 1)]; r[1]--; if (!equal) break; }
            }
            else if (code.StartsWith(new byte[] { 0x0f, 5 }))
            {
                var call = Current(); result.Calls.Add(call);
                if (call.Number == 231) { result.Exit = (int)call.A0; result.OpenCount = fds.Count; return result; }
                r[0] = unchecked((ulong)SystemCall(call)); size = 2;
            }
            else throw new InvalidOperationException("unsupported model opcode " + at.ToString("x"));
            at += size;
        }
        throw new InvalidOperationException("fixture model step bound");
    }
}
