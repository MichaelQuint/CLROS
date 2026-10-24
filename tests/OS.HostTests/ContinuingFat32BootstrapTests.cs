using System.Reflection;
using System.Runtime.InteropServices;
using OS.Storage;
using OS.Std.Hash;
using K = OS.Storage.ContinuingFat32Bootstrap;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

// Independent numeric GPT/BPB bytes reach the kernel ONLY through real fake
// queue IN/completion/copy, not a parsed image or second owner of that queue.
internal static unsafe class ContinuingFat32BootstrapTests
{
    private const int First = 2048, Reserved = 32, FatSectors = 512, Clusters = 65525;
    private const int Fat0 = First + Reserved, Data = Fat0 + 2 * FatSectors, Last = Data + Clusters - 1, Sectors = Last + 34;
    private static void W16(Span<byte> x, int at, ushort v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(x[at..], v);
    private static void W32(Span<byte> x, int at, uint v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(x[at..], v);
    private static void W64(Span<byte> x, int at, ulong v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(x[at..], v);
    private static byte[] Image()
    {
        byte[] image = new byte[Sectors * 512]; var mbr = image.AsSpan(0, 512); mbr[450] = 0xee; W32(mbr, 454, 1); W32(mbr, 458, Sectors - 1); mbr[510] = 0x55; mbr[511] = 0xaa;
        var entries = image.AsSpan(1024, 16384); new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7").TryWriteBytes(entries);
        new Guid("0B315D75-842F-4C81-BEB0-000000000022").TryWriteBytes(entries[16..]); W64(entries, 32, First); W64(entries, 40, Last);
        var h = image.AsSpan(512, 512); "EFI PART"u8.CopyTo(h); W32(h, 8, 0x10000); W32(h, 12, 92); W64(h, 24, 1); W64(h, 32, Sectors - 1);
        W64(h, 40, 34); W64(h, 48, Last); new Guid("0B315D75-842F-4C81-BEB0-000000000021").TryWriteBytes(h[56..]); W64(h, 72, 2); W32(h, 80, 128); W32(h, 84, 128); Recrc(image);
        var boot = image.AsSpan(First * 512, 512); W16(boot, 11, 512); boot[13] = 1; W16(boot, 14, Reserved); boot[16] = 2;
        W32(boot, 32, Last - First + 1); W32(boot, 36, FatSectors); W32(boot, 44, 2); boot[510] = 0x55; boot[511] = 0xaa;
        foreach (int firstFat in new[] { Fat0, Fat0 + FatSectors })
        { W32(image, firstFat * 512, 0x0ffffff8); W32(image, firstFat * 512 + 4, 0x0fffffff); W32(image, firstFat * 512 + 8, 0x0fffffff); }
        return image; // Intentionally absent backups/FSInfo: not bootstrap claims.
    }
    private static void Recrc(byte[] image)
    { W32(image, 512 + 88, Crc32.Compute(image.AsSpan(1024, 16384))); W32(image, 512 + 16, 0); W32(image, 512 + 16, Crc32.Compute(image.AsSpan(512, 92))); }

    [HostTest("continuing bootstrap selects actual BasicData and primary BPB in35 INs then mounts same writable owner without queue setup")]
    private static void Positive()
    {
        using var f = new Fixture(); HostCheck.Equal(K.Status.Pending, f.Start()); HostCheck.Equal(K.Status.Ready, f.Run());
        var result = f.Copy(); HostCheck.True(result.Reads == K.MaximumReads && result.EntryIndex == 0 && result.Volume.Succeeded && result.Volume.PartitionFirstLba == First &&
            result.Volume.PartitionLastLba == Last && result.Volume.FirstFatLba == Fat0 && result.Volume.FirstDataLba == Data && result.Volume.ClusterCount == Clusters &&
            result.DeviceOwner == (ulong)f.Device.State && result.DeviceEpoch == f.Device.Input.Epoch && result.PartitionGuidLow != 0 && result.DiskGuidLow != 0);
        HostCheck.True(f.Device.State->Submitted == 35 && f.Device.State->Completed == 35 && f.Device.State->SubmittedBytes == 0 && f.Device.Hardware->Type == 0);
        HostCheck.Equal(K.Status.Success, K.Stop(f.State)); uint setups = f.Device.Hardware->DriverOkWrites;
        using var memory = new EngineMemory(f.Device.State, result.Volume.PartitionFirstLba, result.Volume.PartitionLastLba);
        HostCheck.Equal(C.Status.Pending, C.Start(memory.State, memory.Input));
        C.Status status = C.Status.Pending;
        for (int i = 0; i < 1000 && status == C.Status.Pending; i++) { status = C.Pump(memory.State); if (f.Device.Hardware->Pending) f.Device.Complete(); }
        HostCheck.Equal(C.Status.Ready, status); HostCheck.True(memory.State->IsMounted && f.Device.State->Submitted == 38 && f.Device.Hardware->DriverOkWrites == setups);
        HostCheck.Equal(C.Status.Success, C.Stop(memory.State));
    }

    [HostTest("continuing bootstrap independently admits the fixed writable data fixture without importing its receipt as mount authority", HostLane.Native)]
    private static void FixedDataImage()
    {
        string root = HostToolProcess.FindRepositoryRoot(); string folder = Path.Combine(root, "artifacts", "bootstrap-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "initial.raw"); var receipt = OS.Tooling.WritableDataImageFixture.Create(root, path);
            using var f = new Fixture(provided: File.ReadAllBytes(path)); f.Start(); HostCheck.Equal(K.Status.Ready, f.Run()); var result = f.Copy();
            HostCheck.True(result.Reads == 35 && result.Volume.Capacity == 131072 && result.Volume.PartitionFirstLba == OS.Tooling.WritableDataImageFixture.PartitionFirstLba &&
                result.Volume.PartitionLastLba == OS.Tooling.WritableDataImageFixture.PartitionLastLba && result.Volume.FatSectors == 993 && result.Volume.FirstDataLba == 4066 &&
                receipt.Image.Find(OS.Tooling.WritableDataImageFixture.ExclusiveCreateName) is null);
            HostCheck.Equal(K.Status.Success, K.Stop(f.State));
            using var memory = new EngineMemory(f.Device.State, result.Volume.PartitionFirstLba, result.Volume.PartitionLastLba);
            C.Start(memory.State, memory.Input); C.Status status = C.Status.Pending;
            for (int i = 0; i < 1000 && status == C.Status.Pending; i++) { status = C.Pump(memory.State); if (f.Device.Hardware->Pending) f.Device.Complete(); }
            HostCheck.Equal(C.Status.Ready, status); HostCheck.True(f.Device.State->Submitted == 38 && memory.State->IsMounted); C.Stop(memory.State);
        }
        finally { Directory.Delete(folder, true); }
    }

    [HostTest("continuing bootstrap rejects bad MBR primary header and array CRC without backup fallback or output")]
    private static void InvalidPrimary()
    {
        foreach (int kind in new[] { 0, 1, 2 })
        {
            using var f = new Fixture(image => image[kind == 0 ? 510 : kind == 1 ? 512 + 16 : 1024 + 60] ^= 1);
            f.Start(); HostCheck.Equal(kind == 0 ? K.Status.InvalidMbr : K.Status.InvalidGpt, f.Run());
            K.Result result = default; HostCheck.Equal(f.State->LastFailure, K.CopyResult(f.State, f.Input.Epoch, &result)); HostCheck.True(!result.Volume.Succeeded && result.Owner == 0);
            HostCheck.True(f.Device.State->Submitted <= 34 && f.Device.State->SubmittedBytes == 0);
        }
    }

    [HostTest("continuing bootstrap rejects missing ambiguous overlapping and attributed BasicData entries after full CRC validation")]
    private static void SelectionFailures()
    {
        for (int kind = 0; kind < 4; kind++)
        {
            using var f = new Fixture(image =>
            {
                if (kind == 0) image.AsSpan(1024, 128).Clear();
                if (kind is 1 or 2)
                {
                    image.AsSpan(1024, 128).CopyTo(image.AsSpan(1152, 128)); W64(image, 1152 + 16, 1);
                    if (kind == 1) { W64(image, 1152 + 32, 34); W64(image, 1152 + 40, 35); }
                }
                if (kind == 3) W64(image, 1024 + 48, 1UL << 60);
                Recrc(image);
            });
            f.Start(); HostCheck.Equal(kind == 0 ? K.Status.MissingPartition : kind == 1 ? K.Status.AmbiguousPartition : kind == 2 ? K.Status.InvalidGpt : K.Status.UnsupportedAttributes, f.Run());
            HostCheck.Equal(34U, f.State->SubmittedReads);
        }
    }

    [HostTest("continuing bootstrap uses existing FAT BPB validator and withholds backup FSInfo or whole FAT claims")]
    private static void BpbScope()
    {
        for (int kind = 0; kind < 3; kind++)
        {
            using var f = new Fixture(image => { if (kind == 0) W16(image, First * 512 + 40, 1); if (kind == 1) W16(image, First * 512 + 42, 1); if (kind == 2) W32(image, First * 512 + 32, Last - First + 2); });
            f.Start(); HostCheck.Equal(K.Status.InvalidVolume, f.Run()); HostCheck.Equal(35U, f.State->SubmittedReads);
        }
        using var accepted = new Fixture(image => { image[(Sectors - 1) * 512] = 0xff; image[(Fat0 + FatSectors) * 512 + 200] = 0x91; });
        accepted.Start(); HostCheck.Equal(K.Status.Ready, accepted.Run()); HostCheck.True(accepted.Copy().Volume.Succeeded);
    }

    [HostTest("continuing bootstrap exact read budget succeeds and smaller budget does not masquerade as corrupt media")]
    private static void Budget()
    {
        using var f = new Fixture(); var invalid = f.Input; invalid.SectorBudget = 0;
        HostCheck.Equal(K.Status.InvalidBudget, K.Start(f.State, invalid)); HostCheck.Equal(0UL, f.Device.State->Submitted);
        f.Input.SectorBudget = 34; f.Start(); HostCheck.Equal(K.Status.BudgetExceeded, f.Run()); HostCheck.Equal(34U, f.State->SubmittedReads);
    }

    [HostTest("continuing bootstrap cancellation before and during actual IN drains without publishing geometry or copied payload")]
    private static void Cancellation()
    {
        foreach (bool pending in new[] { false, true })
        {
            using var f = new Fixture(); f.Start();
            if (pending) { K.Pump(f.State); HostCheck.True(f.Device.Hardware->Pending); }
            HostCheck.Equal(pending ? K.Status.Pending : K.Status.Cancelled, K.Cancel(f.State, f.Input.Epoch));
            if (pending) { f.Device.Complete(); HostCheck.Equal(K.Status.Cancelled, K.Pump(f.State)); HostCheck.Equal(0U, f.Device.Observation->PayloadBytes); }
            K.Result result = default; HostCheck.Equal(K.Status.Cancelled, K.CopyResult(f.State, f.Input.Epoch, &result)); HostCheck.True(result.Owner == 0);
            HostCheck.Equal(K.Status.Success, K.Stop(f.State)); HostCheck.True(f.Device.State->Running);
        }
    }

    [HostTest("continuing bootstrap IN error malformed length and timeout retain buffers until actual original retirement")]
    private static void IoFailure()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new Fixture(); f.Start(); K.Pump(f.State);
            if (mode != 2) f.Device.Complete(length: mode == 0 ? 1U : 512U, status: mode == 0 ? (byte)1 : (byte)0); else f.Device.Hardware->Now = 1000;
            HostCheck.Equal(K.Status.BlockFailure, K.Pump(f.State)); HostCheck.Equal(K.Status.Busy, K.Stop(f.State));
            HostCheck.True(f.Device.Pool->AllocatedMask == 1 && f.State->RequestPending); B.Stop(f.Device.State); HostCheck.Equal(K.Status.Success, K.Stop(f.State));
        }
    }

    [HostTest("continuing bootstrap failed notify after actual IN publication cannot retire scratch before original device Stop")]
    private static void PublicationFailure()
    {
        uint callbacks;
        using (var baseline = new Fixture())
        { baseline.Start(); uint before = baseline.Device.Hardware->Calls; HostCheck.Equal(K.Status.Pending, K.Pump(baseline.State)); callbacks = baseline.Device.Hardware->Calls - before; }
        using var f = new Fixture(); f.Start(); new Span<byte>(f.Input.Sector, 512).Fill(0x71); new Span<byte>(f.Input.Entries, (int)K.EntryBytes).Fill(0x72);
        f.Device.Hardware->FailCall = f.Device.Hardware->Calls + callbacks;
        HostCheck.Equal(K.Status.BlockFailure, K.Pump(f.State));
        HostCheck.True(f.Device.State->Submitted == 1 && f.State->SubmittedReads == 1 && !f.State->RequestPending && !f.Device.State->Running && f.Device.Pool->AllocatedMask == 1 &&
            f.Device.Observation->Flags == B.ObservationFlags.Published);
        HostCheck.Equal(K.Status.Busy, K.Stop(f.State)); HostCheck.True(!f.State->Retired && new ReadOnlySpan<byte>(f.Input.Sector, 512).IndexOfAnyExcept((byte)0x71) < 0 &&
            new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).IndexOfAnyExcept((byte)0x72) < 0);
        B.Stop(f.Device.State); HostCheck.Equal(K.Status.Success, K.Stop(f.State));
    }

    [HostTest("continuing bootstrap later queued array and BPB transport faults never parse or publish uncopied bytes")]
    private static void LateQueuedFailures()
    {
        for (int mode = 0; mode < 5; mode++)
        {
            using var f = new Fixture(); f.Start(); QueueAt(f, mode is 0 or 4 ? 33UL : First);
            byte[] sector = new ReadOnlySpan<byte>(f.Input.Sector, 512).ToArray(), entries = new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).ToArray();
            ulong* lease = (ulong*)((byte*)f.Device.Slot + WritableBlockSessionTests.ManagedOffset(f.Device.Slot, "Generation")); ulong originalLease = *lease;
            B.Status failure = mode == 1 ? B.Status.DeviceIoError : mode == 3 ? B.Status.ConfigurationChanged : B.Status.InvalidCompletion;
            B.Status priorBlockFailure = f.State->LastBlockFailure;
            try
            {
                if (mode == 4) (*lease)++;
                f.Device.Complete(id: mode == 0 ? 1U : 0, length: mode == 1 ? 1U : uint.MaxValue, status: mode == 1 ? (byte)1 : (byte)0, delta: mode == 2 ? (ushort)2 : (ushort)1);
                if (mode == 3) f.Device.Hardware->Generation++;
                uint calls = f.Device.Hardware->Calls;
                K.Status expected = mode == 4 ? K.Status.InvalidDevice : K.Status.BlockFailure;
                HostCheck.Equal(expected, K.Pump(f.State)); HostCheck.Equal(mode == 4 ? priorBlockFailure : failure, f.State->LastBlockFailure);
                if (mode == 4) HostCheck.True(calls == f.Device.Hardware->Calls && f.Device.State->LastFailure == B.Status.Success &&
                    f.Device.Observation->Flags == B.ObservationFlags.Published); // Structural lease rejection precedes Poll.
                HostCheck.True(new ReadOnlySpan<byte>(f.Input.Sector, 512).SequenceEqual(sector) &&
                    new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).SequenceEqual(entries) &&
                    f.State->RequestPending && f.Device.Pool->AllocatedMask == 1 && f.Device.Observation->PayloadBytes == 0 &&
                    (f.Device.Observation->Flags & B.ObservationFlags.ReadCopied) == 0);
                K.Result result = default; HostCheck.Equal(expected, K.CopyResult(f.State, f.Input.Epoch, &result)); HostCheck.True(result.Owner == 0 && !result.Volume.Succeeded);
                HostCheck.Equal(K.Status.Busy, K.Stop(f.State));
            }
            finally { *lease = originalLease; }
            HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.Equal(K.Status.Success, K.Stop(f.State));
        }
    }

    [HostTest("continuing bootstrap cancellation revokes held block and final geometry Ready without publishing geometry")]
    private static void LateReadyCancellation()
    {
        foreach (bool geometryReady in new[] { false, true })
        {
            using var f = new Fixture(); f.Start(); QueueAt(f, First); f.Device.Complete();
            if (geometryReady) HostCheck.Equal(K.Status.Ready, K.Pump(f.State));
            else { B.Completion held = default; HostCheck.Equal(B.Status.Ready, B.Poll(f.Device.State, &held)); }
            byte[] sector = new ReadOnlySpan<byte>(f.Input.Sector, 512).ToArray(), entries = new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).ToArray();
            ulong completed = f.Device.State->Completed; uint calls = f.Device.Hardware->Calls;
            HostCheck.Equal(K.Status.StaleEpoch, K.Cancel(f.State, f.Input.Epoch + 1)); HostCheck.Equal(calls, f.Device.Hardware->Calls);
            HostCheck.Equal(K.Status.Cancelled, K.Cancel(f.State, f.Input.Epoch)); HostCheck.Equal(K.Status.Cancelled, K.Pump(f.State));
            K.Result result = default; HostCheck.Equal(K.Status.Cancelled, K.CopyResult(f.State, f.Input.Epoch, &result));
            HostCheck.True(result.Owner == 0 && !result.Volume.Succeeded && !f.State->RequestPending && f.Device.State->Running &&
                f.Device.State->Completed == completed && f.Device.State->Submitted == 35 &&
                new ReadOnlySpan<byte>(f.Input.Sector, 512).SequenceEqual(sector) && new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).SequenceEqual(entries));
            HostCheck.True(geometryReady ? (f.Device.Observation->Flags & B.ObservationFlags.ReadCopied) != 0 :
                f.Device.Observation->PayloadBytes == 0 && (f.Device.Observation->Flags & B.ObservationFlags.ReadCopied) == 0);
            HostCheck.Equal(K.Status.Success, K.Stop(f.State)); HostCheck.True(f.Device.State->Running && f.Device.Pool->AllocatedMask == 1);
        }
    }

    [HostTest("continuing bootstrap Stop retains queued scratch through failed actual reset bus disable and fence until original retirement")]
    private static void FailedStopRetention()
    {
        using var f = new Fixture(); f.Start(); QueueAt(f, 1);
        new Span<byte>(f.Input.Sector, 512).Fill(0x69); new Span<byte>(f.Input.Entries, (int)K.EntryBytes).Fill(0x96);
        HostCheck.Equal(K.Status.Pending, K.Stop(f.State));
        byte[] dma = new ReadOnlySpan<byte>(f.Device.Data, 4096).ToArray(), observed = new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).ToArray();
        for (int failure = 0; failure < 4; failure++)
        {
            f.Device.Hardware->ResetStuck = failure == 0; f.Device.Hardware->DisableFailure = failure == 1;
            f.Device.Hardware->ResetFenceFailure = failure == 2; f.Device.Hardware->ResetWriteFailure = failure == 3;
            HostCheck.Equal(B.Status.ResetFailed, B.Stop(f.Device.State)); HostCheck.Equal(K.Status.Busy, K.Stop(f.State));
            HostCheck.True(!f.State->Retired && f.State->RequestPending && f.Device.Pool->AllocatedMask == 1 &&
                new ReadOnlySpan<byte>(f.Device.Data, 4096).SequenceEqual(dma) && new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).SequenceEqual(observed) &&
                new ReadOnlySpan<byte>(f.Input.Sector, 512).IndexOfAnyExcept((byte)0x69) < 0 && new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).IndexOfAnyExcept((byte)0x96) < 0);
        }
        f.Device.Hardware->ResetWriteFailure = false;
        HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.Equal(K.Status.Success, K.Stop(f.State));
        HostCheck.True(f.Device.Pool->AllocatedMask == 0 && f.State->Retired && new ReadOnlySpan<byte>(f.Input.Sector, 512).IndexOfAnyExcept((byte)0) < 0 &&
            new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).IndexOfAnyExcept((byte)0) < 0 && new ReadOnlySpan<byte>(f.Device.Observation, sizeof(B.Observation)).SequenceEqual(observed));
    }

    [HostTest("continuing bootstrap publication boundary failures distinguish unpublished fence published fence and side effecting notify")]
    private static void PublicationBoundaries()
    {
        uint callbacks;
        using (var baseline = new Fixture()) { baseline.Start(); uint before = baseline.Device.Hardware->Calls; K.Pump(baseline.State); callbacks = baseline.Device.Hardware->Calls - before; }
        for (uint boundary = 0; boundary < 3; boundary++)
        {
            using var f = new Fixture(); f.Start(); new Span<byte>(f.Input.Sector, 512).Fill(0x37); new Span<byte>(f.Input.Entries, (int)K.EntryBytes).Fill(0x73);
            f.Device.Hardware->FailCall = f.Device.Hardware->Calls + callbacks - 2 + boundary; f.Device.Hardware->FailAfterWrite = boundary == 2;
            HostCheck.Equal(K.Status.BlockFailure, K.Pump(f.State)); uint published = boundary == 0 ? 0U : 1U;
            HostCheck.True(f.Device.Hardware->FailureFired && f.Device.State->Submitted == published && f.State->SubmittedReads == published &&
                !f.State->RequestPending && f.Device.Pool->AllocatedMask == 1 && f.Device.State->Completed == 0 &&
                f.Device.Observation->Flags == (published == 0 ? 0 : B.ObservationFlags.Published) && f.Device.Hardware->Pending == (boundary == 2));
            HostCheck.Equal(boundary == 2 ? B.Status.WriteFailed : B.Status.OrderingFailed, f.State->LastBlockFailure);
            HostCheck.Equal(K.Status.Busy, K.Stop(f.State));
            HostCheck.True(new ReadOnlySpan<byte>(f.Input.Sector, 512).IndexOfAnyExcept((byte)0x37) < 0 && new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).IndexOfAnyExcept((byte)0x73) < 0);
            HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.Equal(K.Status.Success, K.Stop(f.State));
        }
    }

    [HostTest("continuing bootstrap pending held and geometry Ready cannot adopt a real restarted block at the same address")]
    private static void OriginalDeviceAba()
    {
        for (int phase = 0; phase < 3; phase++)
        {
            using var f = new Fixture(); f.Start(); QueueAt(f, First);
            if (phase != 0) f.Device.Complete();
            if (phase == 1) { B.Completion held = default; HostCheck.Equal(B.Status.Ready, B.Poll(f.Device.State, &held)); }
            if (phase == 2) HostCheck.Equal(K.Status.Ready, K.Pump(f.State));
            byte[] scratch = new ReadOnlySpan<byte>(f.Input.Sector, 512).ToArray(), entries = new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).ToArray();
            HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State)); HostCheck.Equal(0UL, f.Device.Pool->AllocatedMask);
            new Span<byte>(f.Device.State, sizeof(B.State)).Clear(); f.Device.Negotiate(); var replacement = f.Device.Input; replacement.Epoch = f.Input.DeviceEpoch + 1;
            HostCheck.Equal(B.Status.Success, B.Start(f.Device.State, replacement)); f.ReplacedOriginal = true;
            uint calls = f.Device.Hardware->Calls; byte[] block = new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).ToArray();
            // Ready is held metadata only. CopyResult still authenticates the
            // original owner and must never publish it through the new epoch.
            HostCheck.Equal(phase == 2 ? K.Status.Ready : K.Status.InvalidStorage, K.Pump(f.State));
            K.Result result = default; new Span<byte>(&result, sizeof(K.Result)).Fill(0x53);
            HostCheck.Equal(K.Status.InvalidStorage, K.CopyResult(f.State, f.Input.Epoch, &result));
            HostCheck.Equal(K.Status.InvalidStorage, K.Cancel(f.State, f.Input.Epoch)); HostCheck.Equal(K.Status.InvalidStorage, K.Stop(f.State));
            HostCheck.True(calls == f.Device.Hardware->Calls && new ReadOnlySpan<byte>(f.Device.State, sizeof(B.State)).SequenceEqual(block) &&
                new ReadOnlySpan<byte>(&result, sizeof(K.Result)).IndexOfAnyExcept((byte)0x53) < 0 && !f.State->Retired &&
                new ReadOnlySpan<byte>(f.Input.Sector, 512).SequenceEqual(scratch) && new ReadOnlySpan<byte>(f.Input.Entries, (int)K.EntryBytes).SequenceEqual(entries));
            HostCheck.Equal(B.Status.Success, B.Stop(f.Device.State));
            // Original and replacement hardware are actually quiesced. This
            // simulated machine ends; the old bootstrap remains quarantined,
            // never adopts the replacement or fabricates a local Stop receipt.
        }
    }

    private static void QueueAt(Fixture f, ulong sector)
    {
        for (int turn = 0; turn < 1000; turn++)
        {
            HostCheck.Equal(K.Status.Pending, K.Pump(f.State));
            if (!f.Device.Hardware->Pending) continue;
            if (f.Device.Hardware->Sector == sector) return;
            f.Device.Complete();
        }
        throw new InvalidOperationException("bootstrap did not queue sector " + sector);
    }

    [HostTest("continuing bootstrap admission output and changed observer aliases reject before state scratch or receipt writes")]
    private static void Aliases()
    {
        using var f = new Fixture(); var wrong = f.Input; wrong.Sector = (byte*)f.Device.Observation;
        HostCheck.Equal(K.Status.InvalidStorage, K.Start(f.State, wrong));
        wrong = f.Input; wrong.Entries = wrong.Sector; HostCheck.Equal(K.Status.InvalidStorage, K.Start(f.State, wrong));
        f.Start(); f.Run(); byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(K.State)).ToArray();
        HostCheck.Equal(K.Status.InvalidStorage, K.CopyResult(f.State, f.Input.Epoch, (K.Result*)f.Input.Sector));
        B.Admission* input = (B.Admission*)((byte*)f.Device.State + WritableBlockSessionTests.ManagedOffset(f.Device.State, "Input")); var observation = input->Observation;
        try
        {
            foreach (ulong address in new[] { (ulong)f.State, (ulong)f.Input.Sector + 8, (ulong)f.Input.Entries + K.EntryBytes - 8 })
            { input->Observation = (B.Observation*)address; K.Result result = default; HostCheck.Equal(K.Status.InvalidStorage, K.CopyResult(f.State, f.Input.Epoch, &result)); }
        }
        finally { input->Observation = observation; }
        HostCheck.True(new ReadOnlySpan<byte>(f.State, sizeof(K.State)).SequenceEqual(state));
        K.Result copied = default; HostCheck.Equal(K.Status.StaleEpoch, K.CopyResult(f.State, f.Input.Epoch + 1, &copied)); f.Copy();
    }

    private delegate bool Borrow(B.State* state, ulong epoch, ulong address, ulong bytes);
    [HostTest("continuing bootstrap block structural borrowing survives original Stop but cannot grant live IO or alias any retained extent")]
    private static void StructuralBorrow()
    {
        using var f = new Fixture(); var borrow = typeof(B).GetMethod("CanBorrowStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Borrow>();
        HostCheck.True(borrow(f.Device.State, f.Device.Input.Epoch, (ulong)f.State, (ulong)sizeof(K.State)));
        foreach (ulong address in new[] { 8UL, (ulong)f.Device.State, (ulong)f.Device.Pool, (ulong)f.Device.Slot, (ulong)f.Device.Data, (ulong)f.Device.Observation, 0x400000UL, 0x402000UL, 0x403000UL })
            HostCheck.True(!borrow(f.Device.State, f.Device.Input.Epoch, address, 8));
        f.Start(); f.Run(); f.Copy(); B.Stop(f.Device.State);
        HostCheck.True(borrow(f.Device.State, f.Device.Input.Epoch, (ulong)f.State, (ulong)sizeof(K.State))); HostCheck.Equal(K.Status.Success, K.Stop(f.State));
    }

    [HostTest("continuing bootstrap repeated bounded Pump has no managed allocations and exposes explicit POD sizes")]
    private static void AllocationAndLayout()
    {
        using var f = new Fixture(); f.Start(); long before = GC.GetAllocatedBytesForCurrentThread(); var status = f.Run(); long after = GC.GetAllocatedBytesForCurrentThread();
        HostCheck.True(status == K.Status.Ready && before == after);
        HostCheck.True(!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<K.State>() && sizeof(K.State) <= 1024 && K.SectorBytes + K.EntryBytes <= 5 * 4096);
        Console.WriteLine($"WRITABLE_BOOTSTRAP_LAYOUT state={sizeof(K.State)} admission={sizeof(K.Admission)} result={sizeof(K.Result)} scratch={K.SectorBytes + K.EntryBytes}"); f.Copy();
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly K.State* State = (K.State*)NativeMemory.AllocZeroed((nuint)sizeof(K.State));
        private readonly byte* scratch = (byte*)NativeMemory.AlignedAlloc(5 * 4096, 4096);
        internal K.Admission Input;
        internal bool ReplacedOriginal;
        private bool started;
        internal Fixture(Action<byte[]>? mutate = null, byte[]? provided = null)
        {
            byte[] image = provided ?? Image(); mutate?.Invoke(image); Device = new((ulong)image.Length / 512, observe: true); Device.UseMedium(image); HostCheck.Equal(B.Status.Success, Device.Start());
            Input = new() { Device = Device.State, Epoch = 91, DeviceEpoch = Device.Input.Epoch, Sector = scratch, Entries = scratch + 512, SectorBudget = K.MaximumReads };
        }
        internal K.Status Start() { var status = K.Start(State, Input); started = status == K.Status.Pending; return status; }
        internal K.Status Run()
        {
            for (int i = 0; i < 1000; i++) { var result = K.Pump(State); if (Device.Hardware->Pending) Device.Complete(); if (result != K.Status.Pending) return result; }
            throw new InvalidOperationException("bootstrap did not terminate");
        }
        internal K.Result Copy() { K.Result result = default; HostCheck.Equal(K.Status.Success, K.CopyResult(State, Input.Epoch, &result)); return result; }
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Device.State));
            if (started && !ReplacedOriginal) HostCheck.Equal(K.Status.Success, K.Stop(State));
            if (ReplacedOriginal) HostCheck.True(Device.State->Retired && !State->Retired);
            Device.CheckGuards();
            Device.Dispose(); NativeMemory.AlignedFree(scratch); NativeMemory.Free(State);
        }
    }
    private sealed class EngineMemory : IDisposable
    {
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* bytes = (byte*)NativeMemory.AlignedAlloc(48 * 4096, 4096);
        internal C.Admission Input;
        internal EngineMemory(B.State* device, ulong first, ulong last)
        {
            ulong cursor = (ulong)bytes;
            ulong Take(ulong count) { ulong at = (cursor + 15) & ~15UL; cursor = at + count; return at; }
            Input = new() { Device = device, Owner = 17, Epoch = 18, PartitionFirst = first, PartitionLast = last, ChangeCapacity = C.JournalChanges,
                ChainCapacity = C.MaximumClusters, InputCapacity = C.WriteBytes, WorkBudget = 10000,
                Changes = (Fat32Mutation.Change*)Take(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)), Before = (byte*)Take(C.JournalChanges * 512UL), After = (byte*)Take(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)Take(C.MaximumClusters * 4UL), FileMap = (uint*)Take(C.MaximumClusters * 4UL), WalkMap = (uint*)Take(C.MaximumClusters * 4UL),
                Sector = (byte*)Take(512), Fat0 = (byte*)Take(512), Fat1 = (byte*)Take(512), Input = (byte*)Take(C.WriteBytes),
                Name = (ushort*)Take(C.NameWords * 2UL), LongName = (ushort*)Take(C.VfatWords * 2UL), EncodedEntries = (byte*)Take(C.EncodedEntryBytes) };
            HostCheck.True(cursor - (ulong)bytes <= 48 * 4096UL); new Span<byte>(bytes, 48 * 4096).Clear();
        }
        public void Dispose() { NativeMemory.AlignedFree(bytes); NativeMemory.Free(State); }
    }
}
