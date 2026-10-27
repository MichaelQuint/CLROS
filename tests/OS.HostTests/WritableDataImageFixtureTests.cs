using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;
using Image = OS.Tooling.WritableDataImageFixture;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

// Host image/extraction controls only. No native/device/guest persistence claim.
internal static unsafe class WritableDataImageFixtureTests
{
    [HostTest("writable large SQLite-sized synthetic footprint uses bounded same-owner growth real flushes fresh reopen and explicit work-budget rejection", HostLane.Fast)]
    private static void LargeSqliteFootprint() => WithImage((_, _, _, seed, before) =>
    {
        // Sizes are the retained large reference footprint. These deterministic
        // bytes are NOT a valid SQLite database/journal or a recovery protocol.
        byte[] database = FootprintBytes(303104, 0x39), journal = FootprintBytes(310816, 0xa7);
        const string dbPath = "/data/footprint.db", journalPath = "/data/footprint.db-journal";
        HostCheck.True(seed.Image.Find("footprint.db") is null && seed.Image.Find("footprint.db-journal") is null);
        byte[] after; ulong originalDeviceEpoch; uint writes = 0, flushes;
        using (var media = new MutationMedia(before, workBudget: 1000000, epoch: 1702))
        using (var files = new DescriptorMedia(media, epoch: 1702))
        {
            originalDeviceEpoch = media.Device.Input.Epoch;
            var db = files.Open(dbPath, 2 | W.Create | W.Exclusive); var reader = files.Open(dbPath);
            var log = files.Open(journalPath, 2 | W.Create | W.Exclusive);
            HostCheck.Equal(17L, W.Seek(files.State, db, 17, 0).Value);
            HostCheck.Equal(29L, W.Seek(files.State, reader, 29, 0).Value);
            Write(db, database); files.Sync(db); Write(log, journal); files.Sync(log);
            var directory = files.Open("/data"); files.Sync(directory); HostCheck.Equal(0L, W.Close(files.State, directory));
            HostCheck.True(W.Seek(files.State, db, 0, 1).Value == 17 && W.Seek(files.State, reader, 0, 1).Value == 29 &&
                files.Read(reader, 64, 303040, positioned: true).AsSpan().SequenceEqual(database.AsSpan(303040)));
            foreach (var file in new[] { db, reader, log }) HostCheck.Equal(0L, W.Close(files.State, file));
            HostCheck.True(!files.State->RequestActive && !media.State->RequestActive && !media.State->Dirty &&
                !media.Device.State->RequestPending && !media.Device.State->Dirty && media.Device.State->Submitted == media.Device.State->Completed &&
                media.Device.State->SubmittedBytes == media.Outs * 512UL && media.Device.State->CompletedBytes == media.Outs * 512UL &&
                media.Device.State->DurableBytes == media.Outs * 512UL);
            after = media.Snapshot(); flushes = media.Flushes;
            void Write(W.DescriptorToken file, byte[] bytes)
            {
                for (int offset = 0; offset < bytes.Length; offset += 4096)
                {
                    int count = Math.Min(4096, bytes.Length - offset);
                    HostCheck.Equal((long)count, files.Run(W.Pwritev2SingleWithFreeSearch(files.State, file,
                        bytes.AsSpan(offset, count), (ulong)offset, 0x20, new W.FreeSearch(2, 4096, 8192))).Value);
                    writes++;
                }
            }
        }
        // The128-sector transition API deliberately does not cover this much
        // changed data. Independently reopen the complete clean raw image.
        var inspected = Image.Inspect(after); var dbEntry = inspected.Find("footprint.db")!; var journalEntry = inspected.Find("footprint.db-journal")!;
        HostCheck.True(writes == 150 && dbEntry.Bytes == 303104 && dbEntry.Clusters.Count == 592 &&
            journalEntry.Bytes == 310816 && journalEntry.Clusters.Count == 608 && inspected.FreeClusters == seed.Image.FreeClusters - 1200 &&
            dbEntry.CopyBytes().AsSpan().SequenceEqual(database) && journalEntry.CopyBytes().AsSpan().SequenceEqual(journal));
        foreach (var file in seed.Image.Files)
        {
            var current = inspected.Find(file.Name)!;
            HostCheck.True(current.Sha256 == file.Sha256 && current.Clusters.SequenceEqual(file.Clusters) &&
                after.AsSpan((int)file.ShortEntryOffset, 32).SequenceEqual(before.AsSpan((int)file.ShortEntryOffset, 32)));
            foreach (uint cluster in file.Clusters) HostCheck.True(after.AsSpan(ClusterOffset(cluster), 512).SequenceEqual(before.AsSpan(ClusterOffset(cluster), 512)));
        }
        foreach (uint cluster in seed.Image.RootClusters)
            for (int slot = 0; slot < 16; slot++)
            {
                int at = ClusterOffset(cluster) + slot * 32;
                if (before[at] is not (0 or 0xe5)) HostCheck.True(after.AsSpan(at, 32).SequenceEqual(before.AsSpan(at, 32)));
            }
        HostCheck.True(after.AsSpan(0, (int)Image.PartitionFirstLba * 512).SequenceEqual(before.AsSpan(0, (int)Image.PartitionFirstLba * 512)) &&
            after.AsSpan(((int)Image.PartitionLastLba + 1) * 512).SequenceEqual(before.AsSpan(((int)Image.PartitionLastLba + 1) * 512)));
        int used = journal.Length % 512; HostCheck.True(after.AsSpan(ClusterOffset(journalEntry.Clusters[^1]) + used, 512 - used).IndexOfAnyExcept((byte)0) < 0);
        using (var limited = new MutationMedia(after, workBudget: 100000, epoch: 1802))
        {
            C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(limited.State, "footprint.db", &request)); limited.Run();
            C.FileToken file = default; HostCheck.Equal(C.Status.BudgetExceeded, C.CopyOpen(limited.State, request, &file));
            HostCheck.True(file.Owner == 0 && !limited.State->RequestActive && !limited.State->Dirty && limited.Outs == 0 && limited.Flushes == 0 &&
                limited.Device.State->SubmittedBytes == 0 && limited.Snapshot().AsSpan().SequenceEqual(after));
        }
        using (var cold = new MutationMedia((byte[])after.Clone(), workBudget: 1000000, epoch: 1902))
        using (var files = new DescriptorMedia(cold, epoch: 1902))
        {
            HostCheck.True(cold.Device.Input.Epoch != originalDeviceEpoch);
            foreach (var pair in new[] { (dbPath, database), (journalPath, journal) })
            {
                var file = files.Open(pair.Item1);
                for (int offset = 0; offset < pair.Item2.Length; offset += 8192)
                {
                    uint count = (uint)Math.Min(8192, pair.Item2.Length - offset);
                    HostCheck.True(files.Read(file, count, (ulong)offset, positioned: true).AsSpan().SequenceEqual(pair.Item2.AsSpan(offset, (int)count)));
                }
                HostCheck.True(files.Read(file, 1, (ulong)pair.Item2.Length, positioned: true, expectedBytes: 0).Length == 0 && W.Seek(files.State, file, 0, 1).Value == 0);
                HostCheck.Equal(0L, W.Close(files.State, file));
            }
            HostCheck.True(cold.Outs == 0 && cold.Flushes == 0 && !files.State->RequestActive && !cold.State->RequestActive && !cold.Device.State->RequestPending &&
                cold.Snapshot().AsSpan().SequenceEqual(after));
        }
        Console.WriteLine($"WRITABLE_LARGE_FOOTPRINT syntheticBytes=true sqliteExecuted=false db=303104:592 journal=310816:608 positionedWrites={writes} workBudget=1000000 lowBudgetRejected=100000 flushes={flushes} rawSha256={Hash(after)} dbSha256={dbEntry.Sha256} journalSha256={journalEntry.Sha256}");
    });

    private static byte[] FootprintBytes(int length, byte salt)
    { byte[] bytes = new byte[length]; for (int i = 0; i < bytes.Length; i++) bytes[i] = unchecked((byte)(salt ^ i * 71 ^ i >> 9)); return bytes; }

    [HostTest("writable data image two fresh outputs and canonical extraction receipts are byte identical", HostLane.Native)]
    private static void Deterministic() => WithImage((root, folder, path, receipt, disk) =>
    {
        string second = Path.Combine(folder, "second.raw");
        var repeated = Image.Create(root, second);
        HostCheck.True(receipt.Image.RawSha256 == repeated.Image.RawSha256 &&
            File.ReadAllBytes(second).AsSpan().SequenceEqual(disk) && receipt.CopyJson().AsSpan().SequenceEqual(repeated.CopyJson()));
        // Retained original64MiB bytes/receipt, before relocated-body support.
        HostCheck.Equal("777844321C8836EA0E0BAFA8BA37708D22345F4ECAC539B5B0A3699CC459265A", receipt.Image.RawSha256);
        HostCheck.Equal("1C638F748E0A94C8291032B7AA3BAD9E8F6BA7B5CC05A7A799A64FE639902C11", Hash(receipt.CopyJson()));
        HostCheck.True(receipt.Image.CheckedGpt && receipt.Image.FirstLba == 2048 && receipt.Image.LastLba == 131038 &&
            receipt.Image.FirstFatSector == 2080 && receipt.Image.FirstDataSector == 4066);
        HostCheck.True(disk.Length == 67108864 && receipt.Image.Files.Count == 6 &&
            receipt.Image.Find(Image.ExclusiveCreateName) is null && receipt.Image.RootClusters.SequenceEqual(new uint[] { 2, 19, 37 }));
        using var json = JsonDocument.Parse(receipt.CopyJson());
        HostCheck.True(json.RootElement.GetProperty("schema").GetString() == "CLROS_WRITABLE_DATA_IMAGE_V1" &&
            json.RootElement.GetProperty("mount").GetString() == "/data" && !Encoding.UTF8.GetString(receipt.CopyJson()).Contains(folder, StringComparison.Ordinal));
        foreach (string empty in new[] { "LOG.TXT", "UPLOAD.BIN", "SQLITE.DB", Image.LongFileName })
            HostCheck.True(receipt.Image.Find(empty) is { Bytes: 0 } item && item.Clusters.Count == 0 && item.Sha256 == Hash([]));
        HostCheck.True(receipt.Image.Find("GUARD.TXT")!.Attributes == 0x21 && receipt.Image.Find("FRAG.BIN")!.Attributes == 0x20);
        foreach (var file in receipt.Image.Files)
        {
            HostCheck.True(file.FatDate == 33 && file.FatTime == 0 && file.ModifiedSeconds == 315532800 &&
                file.Inode == file.ShortEntryOffset / 32 + 256 && Hash(Extract(disk, file)) == file.Sha256);
        }
        Image.Verify(path, receipt);
        Console.WriteLine($"WRITABLE_DATA_IMAGE sha256={receipt.Image.RawSha256} receiptSha256={Hash(receipt.CopyJson())} bytes={disk.Length} files={receipt.Image.Files.Count} allocatedClusters=8 freeClusters={receipt.Image.FreeClusters}");
    });

    [HostTest("writable relocated body inspection uses actual physical bounds and extraction without claiming GPT selection", HostLane.Native)]
    private static void RelocatedBody() => WithImage((_, _, _, original, disk) =>
    {
        const uint first = 131072, last = 260062, delta = 129024;
        byte[] raw = new byte[128 * 1024 * 1024];
        disk.AsSpan(2048 * 512, (131038 - 2048 + 1) * 512).CopyTo(raw.AsSpan((int)first * 512));
        W32(raw, (int)first * 512 + 28, first); W32(raw, ((int)first + 6) * 512 + 28, first);
        var body = Image.InspectPartition(raw, first, last);
        HostCheck.True(!body.CheckedGpt && body.FirstLba == first && body.LastLba == last && body.FirstFatSector == first + 32 &&
            body.FirstDataSector == first + 32 + 2 * 993 && body.RawSha256 == Hash(raw) && body.FreeClusters == original.Image.FreeClusters &&
            body.RootClusters.SequenceEqual(original.Image.RootClusters) && body.FreeCandidates.SequenceEqual(original.Image.FreeCandidates));
        foreach (var old in original.Image.Files)
        {
            var now = body.Find(old.Name)!;
            HostCheck.True(now.ShortEntryOffset == old.ShortEntryOffset + delta * 512UL && now.Inode == now.ShortEntryOffset / 32 + 256 &&
                now.Clusters.SequenceEqual(old.Clusters) && now.Extents.SequenceEqual(old.Extents.Select(x => x with { FirstLba = x.FirstLba + delta })) &&
                now.CopyBytes().AsSpan().SequenceEqual(old.CopyBytes()));
        }
        byte[] guard = body.Find("GUARD.TXT")!.CopyBytes(); raw.AsSpan().Clear();
        HostCheck.True(body.Find("GUARD.TXT")!.CopyBytes().AsSpan().SequenceEqual(guard));
    });

    [HostTest("writable relocated body rejects invalid extents hidden-sector mismatch actual backup and mirror failures", HostLane.Native)]
    private static void RelocatedBodyRejection() => WithImage((_, _, _, _, disk) =>
    {
        foreach (var bounds in new (ulong First, ulong Last)[] { (0, 128990), (2048, 131037), (2048, 131039), (2048, ulong.MaxValue), (ulong.MaxValue, ulong.MaxValue) })
            Reject(() => Image.InspectPartition(disk, bounds.First, bounds.Last), "partition_bounds");
        byte[] hidden = (byte[])disk.Clone(); W32(hidden, 2048 * 512 + 28, 2049);
        Reject(() => Image.InspectPartition(hidden, 2048, 131038), "bpb");
        byte[] backup = (byte[])disk.Clone(); backup[(2048 + 6) * 512 + 28] ^= 1;
        Reject(() => Image.InspectPartition(backup, 2048, 131038), "boot_backup");
        byte[] mirror = (byte[])disk.Clone(); mirror[(2080 + 993) * 512 + 5 * 4] ^= 1;
        Reject(() => Image.InspectPartition(mirror, 2048, 131038), "fat_mirror");
        // A body result never repairs or authenticates a bad GPT header.
        disk[512] ^= 1; HostCheck.True(!Image.InspectPartition(disk, 2048, 131038).CheckedGpt);
        Reject(() => Image.Inspect(disk), "gpt");
    });

    [HostTest("writable data image independently extracts fragmented sectors max LFN and both FAT free candidates", HostLane.Native)]
    private static void IndependentGeometry() => WithImage((_, _, _, receipt, disk) =>
    {
        HostCheck.True(U32(disk, 512 + 80) == 128 && U32(disk, 512 + 84) == 128 &&
            U64(disk, 512 + 40) == 34 && U64(disk, 512 + 48) == 131038 &&
            new Guid(disk.AsSpan(1024, 16)) == new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7"));
        int boot = 2048 * 512;
        HostCheck.True(U16(disk, boot + 11) == 512 && disk[boot + 13] == 1 && U16(disk, boot + 14) == 32 &&
            disk[boot + 16] == 2 && U32(disk, boot + 36) == 993 && U32(disk, boot + 44) == 2 &&
            U16(disk, boot + 48) == 1 && U16(disk, boot + 50) == 6);
        int fat = 2080 * 512, fatBytes = 993 * 512;
        HostCheck.True(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)) &&
            disk.AsSpan(boot, 512).SequenceEqual(disk.AsSpan(boot + 6 * 512, 512)) &&
            disk.AsSpan(boot + 512, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)));
        var fragment = receipt.Image.Find("FRAG.BIN")!;
        byte[] bytes = Extract(disk, fragment);
        HostCheck.True(bytes.Length == 1537 && fragment.Clusters.SequenceEqual(new uint[] { 5, 41, 8, 73 }) && fragment.Extents.Count == 4);
        for (int i = 0; i < bytes.Length; i++) HostCheck.Equal(unchecked((byte)((i * 73 + 19) ^ (i / 512))), bytes[i]);
        var longFile = receipt.Image.Find(Image.LongFileName)!;
        HostCheck.True(longFile.Name.Length == 255 && longFile.ShortAlias == "LONGNA~1.TXT" && longFile.ShortEntryOffset == (ulong)ClusterOffset(37) + 11 * 32);
        HostCheck.True(disk[ClusterOffset(19) + 7 * 32] == 0x54 && disk[ClusterOffset(37) + 10 * 32] == 1);
        var owned = receipt.Image.Files.SelectMany(x => x.Clusters).Concat(receipt.Image.RootClusters).ToHashSet();
        HostCheck.True(owned.Count == 8 && receipt.Image.FreeClusters == 126973 - 8 && receipt.Image.FsInfoHint == 3 && receipt.Image.FreeCandidates.Count == 16);
        foreach (uint c in receipt.Image.FreeCandidates)
            HostCheck.True(!owned.Contains(c) && U32(disk, fat + (int)c * 4) == 0 && U32(disk, fat + fatBytes + (int)c * 4) == 0);
    });

    [HostTest("writable data image rejects corrupt and CRC valid divergent GPT identity and partition contracts", HostLane.Native)]
    private static void GptRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[512 + 16] ^= 1, "gpt");
        Mutant(disk, x => { W64(x, x.Length - 512 + 40, 35); RecrcHeader(x, x.Length - 512); }, "gpt");
        Mutant(disk, x => { x[x.Length - 512 + 56] ^= 1; RecrcHeader(x, x.Length - 512); }, "gpt");
        Mutant(disk, x =>
        {
            new Guid("C12A7328-F81F-11D2-BA4B-00A0C93EC93B").TryWriteBytes(x.AsSpan(1024, 16));
            x.AsSpan(1024, 16384).CopyTo(x.AsSpan(x.Length - 33 * 512, 16384)); RecrcArrays(x);
        }, "partition");
        Mutant(disk, x => { x[446] = 0x80; x[450] = 0; }, "mbr");
        Mutant(disk, x => { W32(x, 1024 + 128, 1); RecrcArrays(x); }, "gpt");
    });

    [HostTest("writable data image rejects divergent FAT cycles crosslinks overlong free and unclaimed clusters", HostLane.Native)]
    private static void AllocationRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[(2080 + 993) * 512 + 5 * 4] ^= 1, "fat_mirror");
        Mutant(disk, x => Fat(x, 37, 2), "chain_loop_or_crosslink");
        Mutant(disk, x => Fat(x, 73, 5), "chain_loop_or_crosslink");
        Mutant(disk, x => Fat(x, 41, 0), "chain");
        Mutant(disk, x => { Fat(x, 73, 91); Fat(x, 91, 0x0fffffff); }, "file_length");
        Mutant(disk, x => W16(x, ClusterOffset(2) + 4 * 32 + 26, 5), "chain_loop_or_crosslink");
        Mutant(disk, x => Fat(x, 91, 0x0fffffff), "unclaimed_cluster");
        Mutant(disk, x => Fat(x, 0, 0), "fat_reserved");
    });

    [HostTest("writable data image checks FSInfo signatures backups counts hints and backup boot", HostLane.Native)]
    private static void ReservedRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[(2048 + 6) * 512 + 71] ^= 1, "boot_backup");
        Mutant(disk, x => x[(2048 + 7) * 512 + 488] ^= 1, "fsinfo_backup");
        Mutant(disk, x => Info(x, 0, 0), "fsinfo");
        Mutant(disk, x => Info(x, 488, 1), "free_count");
        Mutant(disk, x => Info(x, 492, 2), "free_hint");
        Mutant(disk, x => Info(x, 492, uint.MaxValue - 1), "free_hint");
        byte[] advisory = (byte[])disk.Clone(); Info(advisory, 488, uint.MaxValue); Info(advisory, 492, uint.MaxValue);
        HostCheck.True(Image.Inspect(advisory).FreeClusters == 126965);
    });

    [HostTest("writable data image rejects namespace LFN interruption malformed timestamp and hidden root kinds", HostLane.Native)]
    private static void NamespaceRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[ClusterOffset(19) + 7 * 32 + 13] ^= 1, "directory");
        Mutant(disk, x => x[ClusterOffset(19) + 8 * 32] = 0xe5, "directory");
        Mutant(disk, x => x.AsSpan(ClusterOffset(2), 11).CopyTo(x.AsSpan(ClusterOffset(2) + 32, 11)), "name_collision");
        Mutant(disk, x => W16(x, ClusterOffset(2) + 24, 31), "timestamp");
        Mutant(disk, x => x[ClusterOffset(2) + 11] = 0x08, "root_nonfile");
        Mutant(disk, x => x[ClusterOffset(2) + 11] = 0x10, "directory");
        Mutant(disk, x => x[ClusterOffset(2) + 12] = 8, "directory");
    });

    [HostTest("writable data image independent postmutation extraction updates candidate claims without trusting seed plan", HostLane.Native)]
    private static void ChangedImage() => WithImage((_, folder, _, receipt, disk) =>
    {
        // Independently authored short entry in a scrubbed hole, not the writer.
        int entry = ClusterOffset(2) + 5 * 32;
        "NEW     BIN"u8.CopyTo(disk.AsSpan(entry, 11)); disk[entry + 11] = 0x20;
        W16(disk, entry + 24, 33); W16(disk, entry + 26, 100); W32(disk, entry + 28, 10);
        "independent"u8[..10].CopyTo(disk.AsSpan(ClusterOffset(100), 10));
        Fat(disk, 100, 0x0fffffff); Info(disk, 488, receipt.Image.FreeClusters - 1);
        var changed = Image.Inspect(disk);
        HostCheck.True(changed.Files.Count == 7 && changed.Find("new.bin") is { Bytes: 10 } file &&
            file.Clusters.SequenceEqual(new uint[] { 100 }) && file.CopyBytes().AsSpan().SequenceEqual("independen"u8) &&
            !changed.FreeCandidates.Contains(100) && changed.FreeCandidates.Contains(132));
        string altered = Path.Combine(folder, "altered.raw"); File.WriteAllBytes(altered, disk);
        Reject(() => Image.Verify(altered, receipt), "receipt_identity");
    });

    [HostTest("writable data image borrowed input payload and canonical receipt outputs are never retained mutable aliases", HostLane.Native)]
    private static void CopiedValues() => WithImage((_, _, _, receipt, disk) =>
    {
        var inspected = Image.Inspect(disk); var file = inspected.Find("FRAG.BIN")!;
        byte original = file.CopyBytes()[0]; string originalHash = inspected.RawSha256;
        byte[] payload = file.CopyBytes(); payload[0] ^= 1;
        byte[] json = receipt.CopyJson(); json.AsSpan().Clear();
        disk.AsSpan().Clear();
        HostCheck.True(file.CopyBytes()[0] == original && inspected.RawSha256 == originalHash && receipt.CopyJson()[0] == '{');
        bool readonlyList = false;
        try { ((IList<uint>)inspected.RootClusters)[0] = 100; } catch (NotSupportedException) { readonlyList = true; }
        HostCheck.True(readonlyList && inspected.RootClusters[0] == 2);
    });

    [HostTest("writable data image fresh artifacts publication rejects existing and escaping outputs without leftovers", HostLane.Native)]
    private static void OutputSafety() => WithImage((root, folder, path, receipt, _) =>
    {
        string originalHash = receipt.Image.RawSha256;
        Reject(() => Image.Create(root, path), "output_path");
        Reject(() => Image.Create(root, folder), "output_path");
        string outside = Path.Combine(root, "writable-data-should-not-exist.raw");
        HostCheck.True(!File.Exists(outside)); Reject(() => Image.Create(root, outside), "output_path");
        Reject(() => Image.Create(root, Path.Combine(folder, "..", "..", "..", "writable-data-should-not-exist.raw")), "output_path");
        HostCheck.True(Image.Inspect(path).RawSha256 == originalHash && !File.Exists(outside) &&
            !Directory.EnumerateFiles(folder, "*.tmp-*", SearchOption.AllDirectories).Any());
    });

    [HostTest("writable data image refuses a reparse output ancestor before creating a temporary image", HostLane.Native)]
    private static void ReparseOutput() => WithImage((root, folder, _, _, _) =>
    {
        string target = Path.Combine(folder, "target"), link = Path.Combine(folder, "link");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(link, target);
        try
        {
            Reject(() => Image.Create(root, Path.Combine(link, "forbidden.raw")), "output_path");
            HostCheck.True(!Directory.EnumerateFileSystemEntries(target).Any());
        }
        finally { Directory.Delete(link); }
    });

    [HostTest("writable data image rejects truncated raw images and binds actual bytes to prior extraction receipt", HostLane.Native)]
    private static void RawIdentity() => WithImage((_, folder, _, receipt, disk) =>
    {
        Reject(() => Image.Inspect(disk.AsSpan(0, disk.Length - 1)), "size");
        disk[ClusterOffset(5) + 1] ^= 1;
        var altered = Image.Inspect(disk);
        HostCheck.True(altered.Find("FRAG.BIN")!.Sha256 != receipt.Image.Find("FRAG.BIN")!.Sha256 && altered.RawSha256 != receipt.Image.RawSha256);
        string path = Path.Combine(folder, "substituted.raw"); File.WriteAllBytes(path, disk);
        Reject(() => Image.Verify(path, receipt), "receipt_identity");
    });

    [HostTest("writable data transition independently extracts actual continuing create grow overwrite shrink delete after real fake OUT FLUSH", HostLane.Native)]
    private static void ActualMutationTransition() => WithImage((_, _, _, seed, before) =>
    {
        using var media = new MutationMedia(before);
        C.RequestToken request = default; C.Result result = default;
        HostCheck.Equal(C.Status.Pending, C.BeginCreateEmpty(media.State, Image.ExclusiveCreateName, "M16WRI~1BIN"u8, &request));
        media.Run(); HostCheck.Equal(C.Status.Success, C.CopyResult(media.State, request, &result));
        C.FileToken file = media.Open(Image.ExclusiveCreateName);
        uint[] candidates = seed.Image.FreeCandidates.Take(11).ToArray();
        HostCheck.Equal(C.Status.Pending, C.BeginGrow(media.State, file, 5126, candidates, &request)); media.Run();
        C.MutationResult mutation = default; HostCheck.Equal(C.Status.Success, C.CopyMutationResult(media.State, request, &mutation)); file = mutation.NewFile;
        byte[] payload = Enumerable.Range(0, 1032).Select(i => unchecked((byte)(i * 61 + 9))).ToArray();
        HostCheck.Equal(C.Status.Pending, C.BeginOverwrite(media.State, file, 4094, payload, &request)); media.Run();
        HostCheck.Equal(C.Status.Success, C.CopyResult(media.State, request, &result));
        byte[] grown = media.Snapshot(); var receipt = Image.InspectTransition(before, grown);
        byte[] expected = new byte[5126]; payload.CopyTo(expected, 4094);
        HostCheck.True(receipt.Before.Find(Image.ExclusiveCreateName) is null && receipt.After.Find(Image.ExclusiveCreateName) is { } created &&
            created.CopyBytes().AsSpan().SequenceEqual(expected) && created.Clusters.SequenceEqual(candidates));
        HostCheck.True(receipt.Changes.Count is > 0 and <= 128 && receipt.Changes.Select(x => x.Kind).Distinct().Order().SequenceEqual(
            new[] { Image.SectorKind.Fat0, Image.SectorKind.Fat1, Image.SectorKind.FsInfo, Image.SectorKind.RootDirectory, Image.SectorKind.FileData }.Order()));
        CheckChanges(before, grown, receipt);
        HostCheck.True(receipt.After.Find("GUARD.TXT")!.Sha256 == seed.Image.Find("GUARD.TXT")!.Sha256 &&
            receipt.After.Find("FRAG.BIN")!.Sha256 == seed.Image.Find("FRAG.BIN")!.Sha256 && media.Outs > 0 && media.Flushes >= 6);
        file = media.Open(Image.ExclusiveCreateName);
        HostCheck.Equal(C.Status.Pending, C.BeginTruncate(media.State, file, 513, &request)); media.Run();
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(media.State, request, &mutation)); file = mutation.NewFile;
        byte[] shrunk = media.Snapshot(); var shrink = Image.InspectTransition(grown, shrunk);
        HostCheck.True(shrink.After.Find(Image.ExclusiveCreateName)!.CopyBytes().AsSpan().SequenceEqual(expected.AsSpan(0, 513)) &&
            shrink.After.FreeClusters == receipt.After.FreeClusters + 9); CheckChanges(grown, shrunk, shrink);
        HostCheck.Equal(C.Status.Pending, C.BeginDelete(media.State, file, &request)); media.Run();
        HostCheck.Equal(C.Status.Success, C.CopyMutationResult(media.State, request, &mutation));
        byte[] deleted = media.Snapshot(); var deletion = Image.InspectTransition(shrunk, deleted);
        HostCheck.True(mutation.Deleted && deletion.After.Find(Image.ExclusiveCreateName) is null && deletion.After.FreeClusters == seed.Image.FreeClusters);
        CheckChanges(shrunk, deleted, deletion);
        Console.WriteLine($"WRITABLE_TRANSITION actualCreateGrowWrite={receipt.Changes.Count} shrink={shrink.Changes.Count} delete={deletion.Changes.Count} outs={media.Outs} flushes={media.Flushes}");
    });

    [HostTest("writable data named log close reopen and upload preserve independent fd cursors across actual shared media growth and flush", HostLane.Native)]
    private static void LogAndUploadTransition() => WithImage((_, _, _, seed, before) =>
    {
        using var media = new MutationMedia(before); using var files = new DescriptorMedia(media);
        HostCheck.True(seed.Image.Find("LOG.TXT") is { Bytes: 0 } && seed.Image.Find("UPLOAD.BIN") is { Bytes: 0 });
        var logWriter = files.Open("/data/LOG.TXT", 2); var logReader = files.Open("/data/LOG.TXT");
        var uploadWriter = files.Open("/data/UPLOAD.BIN", 2); var uploadReader = files.Open("/data/UPLOAD.BIN");
        HostCheck.True(logWriter.Number != logReader.Number && uploadWriter.Number != uploadReader.Number);
        byte[] firstLine = "started\n"u8.ToArray(), secondLine = Enumerable.Range(0, 600).Select(i => (byte)('a' + i % 26)).ToArray();
        var search = new W.FreeSearch(2, 64, 8);
        HostCheck.Equal(0L, W.Seek(files.State, logWriter, 0, 2).Value);
        HostCheck.Equal((long)firstLine.Length, files.Run(W.BeginWriteWithFreeSearch(files.State, logWriter, firstLine, search)).Value);
        HostCheck.True(W.Seek(files.State, logWriter, 0, 1).Value == firstLine.Length && W.Seek(files.State, logReader, 0, 1).Value == 0);
        HostCheck.True(files.Read(logReader, (uint)firstLine.Length).AsSpan().SequenceEqual(firstLine));
        files.Sync(logWriter); var closedWriter = logWriter;
        HostCheck.Equal(0L, W.Close(files.State, closedWriter)); logWriter = files.Open("/data/LOG.TXT", 2);
        HostCheck.True(logWriter.Number == closedWriter.Number && logWriter.Generation > closedWriter.Generation && W.Seek(files.State, logWriter, 0, 1).Value == 0);
        ulong writes = media.Device.State->SubmittedBytes;
        HostCheck.Equal(-9L, W.BeginWrite(files.State, closedWriter, firstLine).Value); HostCheck.Equal(writes, media.Device.State->SubmittedBytes);
        // This is explicit seek-to-EOF plus sequential write. O_APPEND and a
        // BCL FileStream are deliberately not part of this host provider test.
        HostCheck.Equal((long)firstLine.Length, W.Seek(files.State, logWriter, 0, 2).Value);
        HostCheck.Equal((long)secondLine.Length, files.Run(W.BeginWriteWithFreeSearch(files.State, logWriter, secondLine, search)).Value);
        byte[] logBytes = firstLine.Concat(secondLine).ToArray();
        HostCheck.True(W.Seek(files.State, logWriter, 0, 1).Value == logBytes.Length && W.Seek(files.State, logReader, 0, 1).Value == firstLine.Length);
        HostCheck.True(files.Read(logReader, (uint)secondLine.Length).AsSpan().SequenceEqual(secondLine));
        HostCheck.Equal(0L, W.Close(files.State, logReader)); logReader = files.Open("/data/LOG.TXT");
        HostCheck.True(W.Seek(files.State, logReader, 0, 1).Value == 0 && files.Read(logReader, (uint)logBytes.Length).AsSpan().SequenceEqual(logBytes));
        byte[] upload = Enumerable.Range(0, 1032).Select(i => unchecked((byte)(i * 47 + 13))).ToArray(), expectedUpload = new byte[5126];
        upload.CopyTo(expectedUpload, 4094);
        HostCheck.Equal(1032L, files.Run(W.Pwritev2SingleWithFreeSearch(files.State, uploadWriter, upload, 4094, W.RwfNoAppend, search)).Value);
        HostCheck.True(W.Seek(files.State, uploadWriter, 0, 1).Value == 0 && W.Seek(files.State, uploadReader, 0, 1).Value == 0 &&
            files.Read(uploadReader, 5126).AsSpan().SequenceEqual(expectedUpload));
        // The unrelated upload's revisions must not retire the open log inode
        // or silently redirect either independent open description/cursor.
        HostCheck.True(files.Read(logReader, (uint)logBytes.Length, 0, true).AsSpan().SequenceEqual(logBytes) &&
            W.Seek(files.State, logReader, 0, 1).Value == logBytes.Length && W.Seek(files.State, logWriter, 0, 1).Value == logBytes.Length);
        files.Sync(logWriter); files.Sync(uploadWriter); var directory = files.Open("/data"); files.Sync(directory);
        byte[] after = media.Snapshot(); var transition = Image.InspectTransition(before, after);
        HostCheck.True(transition.Before.Find("LOG.TXT")!.Bytes == 0 && transition.Before.Find("UPLOAD.BIN")!.Bytes == 0 &&
            transition.After.Find("LOG.TXT")!.CopyBytes().AsSpan().SequenceEqual(logBytes) && transition.After.Find("UPLOAD.BIN")!.CopyBytes().AsSpan().SequenceEqual(expectedUpload) &&
            transition.After.FreeClusters == seed.Image.FreeClusters - 13 && transition.After.Files.Count == seed.Image.Files.Count && transition.After.Find(Image.ExclusiveCreateName) is null);
        foreach (var entry in seed.Image.Files.Where(x => x.Name is not ("LOG.TXT" or "UPLOAD.BIN")))
        {
            HostCheck.True(transition.After.Find(entry.Name) is { } unchanged && unchanged.Sha256 == entry.Sha256 && unchanged.ShortEntryOffset == entry.ShortEntryOffset &&
                unchanged.Clusters.SequenceEqual(entry.Clusters) && unchanged.CopyBytes().AsSpan().SequenceEqual(entry.CopyBytes()) &&
                before.AsSpan((int)entry.ShortEntryOffset, 32).SequenceEqual(after.AsSpan((int)entry.ShortEntryOffset, 32)));
            foreach (uint cluster in entry.Clusters)
                HostCheck.True(before.AsSpan(ClusterOffset(cluster), 512).SequenceEqual(after.AsSpan(ClusterOffset(cluster), 512))); // Include guard/slack bytes.
        }
        HostCheck.True(transition.After.RootClusters.SequenceEqual(seed.Image.RootClusters));
        ulong logEntry = seed.Image.Find("LOG.TXT")!.ShortEntryOffset, uploadEntry = seed.Image.Find("UPLOAD.BIN")!.ShortEntryOffset;
        HostCheck.True(transition.After.Find("LOG.TXT")!.ShortEntryOffset == logEntry && transition.After.Find("UPLOAD.BIN")!.ShortEntryOffset == uploadEntry);
        foreach (uint cluster in seed.Image.RootClusters)
        {
            for (int i = 0; i < 512; i++)
            {
                int address = ClusterOffset(cluster) + i; ulong at = (ulong)address;
                if (!(at >= logEntry && at - logEntry < 32) && !(at >= uploadEntry && at - uploadEntry < 32))
                    HostCheck.Equal(before[address], after[address]); // Exact independently extracted permitted entry intervals.
            }
        }
        CheckChanges(before, after, transition);
        HostCheck.True(media.Outs > 0 && media.Flushes > 0 && !media.Device.State->Dirty && media.Device.State->Submitted == media.Device.State->Completed &&
            media.Device.State->SubmittedBytes == media.Outs * 512UL && media.Device.State->CompletedBytes == media.Outs * 512UL &&
            media.Device.State->DurableBytes == media.Outs * 512UL);
        foreach (var file in new[] { logWriter, logReader, uploadWriter, uploadReader, directory }) HostCheck.Equal(0L, W.Close(files.State, file));
        Console.WriteLine($"WRITABLE_NAMED_MEDIA logBytes={logBytes.Length} uploadBytes={expectedUpload.Length} changedSectors={transition.Changes.Count} outs={media.Outs} flushes={media.Flushes} submittedWriteBytes={media.Device.State->SubmittedBytes} completedWriteBytes={media.Device.State->CompletedBytes} flushAcknowledgedBytes={media.Device.State->DurableBytes} logSha256={transition.After.Find("LOG.TXT")!.Sha256} uploadSha256={transition.After.Find("UPLOAD.BIN")!.Sha256} afterRawSha256={transition.After.RawSha256}");
    });

    [HostTest("writable data exact FileStream candidate bytes survive copied append upload close reopen and independent media extraction", HostLane.Native)]
    private static void LogUploadCandidateBytes() => WithImage((_, _, _, seed, before) =>
    {
        // Expected values and grouping come from the unchanged #809 candidate.
        // This drives W/C/B host APIs, not FileStream or its syscall/metadata/
        // advisory-lock implementation. No native, reboot or durability proof.
        byte[] expectedLog = NativeAotLogUploadContract.ExpectedLog(), expectedUpload = NativeAotLogUploadContract.ExpectedUpload();
        var expectedValues = NativeAotLogUploadContract.InspectFileValues(expectedLog, expectedUpload);
        HostCheck.True(expectedLog.Length == 660 && expectedUpload.Length == 5126 && !expectedValues.GrantsPersistenceOrGuestAuthority &&
            expectedLog.Count(x => x == (byte)'\n') == 12 && expectedLog.Length / 12 == 55);
        using var media = new MutationMedia(before); using var files = new DescriptorMedia(media);
        var initialLog = files.Open("/data/LOG.TXT"); var initialUpload = files.Open("/data/UPLOAD.BIN");
        HostCheck.True(seed.Image.Find("LOG.TXT") is { Bytes: 0 } && seed.Image.Find("UPLOAD.BIN") is { Bytes: 0 } &&
            files.Read(initialLog, 1, expectedBytes: 0).Length == 0 && files.Read(initialUpload, 1, expectedBytes: 0).Length == 0 && media.Device.State->SubmittedBytes == 0);
        HostCheck.Equal(0L, W.Close(files.State, initialLog)); HostCheck.Equal(0L, W.Close(files.State, initialUpload));
        var search = new W.FreeSearch(2, 128, 16); W.DescriptorToken previousLogWriter = default, reader = default;
        byte[] firstCheckpoint = [];
        for (int group = 0; group < 2; group++)
        {
            var writer = files.Open("/data/LOG.TXT", 1 | W.Create);
            if (group != 0)
            {
                ulong submitted = media.Device.State->SubmittedBytes;
                HostCheck.Equal(-9L, W.BeginWrite(files.State, previousLogWriter, expectedLog.AsSpan(0, 55)).Value);
                HostCheck.Equal(submitted, media.Device.State->SubmittedBytes);
            }
            // The pinned BCL Append policy uses its observed initial length.
            // Explicit seek-to-EOF models that position; O_APPEND is not added.
            HostCheck.Equal(group * 330L, W.Seek(files.State, writer, 0, 2).Value);
            for (int row = group * 6; row < group * 6 + 6; row++)
            {
                ReadOnlySpan<byte> record = expectedLog.AsSpan(row * 55, 55); HostCheck.Equal((byte)'\n', record[^1]);
                HostCheck.Equal(55L, files.Run(W.BeginWriteWithFreeSearch(files.State, writer, record, search)).Value);
            }
            files.Sync(writer); HostCheck.Equal((group + 1) * 330L, W.Seek(files.State, writer, 0, 1).Value);
            HostCheck.Equal(0L, W.Close(files.State, writer)); previousLogWriter = writer;
            if (group == 0)
            {
                firstCheckpoint = media.Snapshot(); var first = Image.InspectTransition(before, firstCheckpoint);
                HostCheck.True(first.After.Find("LOG.TXT")!.CopyBytes().AsSpan().SequenceEqual(expectedLog.AsSpan(0, 330)) && first.After.Find("UPLOAD.BIN")!.Bytes == 0);
                CheckChanges(before, firstCheckpoint, first); CheckNeighbors(firstCheckpoint, first.After);
                reader = files.Open("/data/LOG.TXT"); HostCheck.True(reader.Number == writer.Number && reader.Generation > writer.Generation &&
                    files.Read(reader, 330).AsSpan().SequenceEqual(expectedLog.AsSpan(0, 330)));
            }
        }
        HostCheck.True(files.Read(reader, 330).AsSpan().SequenceEqual(expectedLog.AsSpan(330)) && W.Seek(files.State, reader, 0, 1).Value == 660);
        HostCheck.True(files.Read(reader, 1, expectedBytes: 0).Length == 0); HostCheck.Equal(0L, W.Close(files.State, reader));
        var upload = files.Open("/data/UPLOAD.BIN", 1);
        HostCheck.Equal(4094L, files.Run(W.BeginWriteWithFreeSearch(files.State, upload, expectedUpload.AsSpan(0, 4094), search)).Value);
        HostCheck.Equal(4094L, W.Seek(files.State, upload, 0, 1).Value);
        HostCheck.Equal(1032L, files.Run(W.BeginWriteWithFreeSearch(files.State, upload, expectedUpload.AsSpan(4094), search)).Value);
        files.Sync(upload); HostCheck.Equal(5126L, W.Seek(files.State, upload, 0, 1).Value); HostCheck.Equal(0L, W.Close(files.State, upload));
        ulong written = media.Device.State->SubmittedBytes; uint flushes = media.Flushes;
        HostCheck.Equal(-9L, W.BeginSync(files.State, previousLogWriter).Value);
        HostCheck.True(media.Device.State->SubmittedBytes == written && media.Flushes == flushes);
        byte[] actualLog = ReopenAndRead("/data/LOG.TXT", expectedLog), actualUpload = ReopenAndRead("/data/UPLOAD.BIN", expectedUpload);
        HostCheck.Equal(expectedValues, NativeAotLogUploadContract.InspectFileValues(actualLog, actualUpload));
        byte[] after = media.Snapshot(); var transition = Image.InspectTransition(before, after);
        var logEntry = transition.After.Find("LOG.TXT")!; var uploadEntry = transition.After.Find("UPLOAD.BIN")!;
        HostCheck.Equal(expectedValues, NativeAotLogUploadContract.InspectFileValues(logEntry.CopyBytes(), uploadEntry.CopyBytes()));
        HostCheck.True(logEntry.Clusters.Count == 2 && uploadEntry.Clusters.Count == 11 && transition.After.FreeClusters == seed.Image.FreeClusters - 13 &&
            transition.After.Files.Count == seed.Image.Files.Count && transition.After.Find(Image.ExclusiveCreateName) is null && firstCheckpoint.Length == Image.ImageBytes);
        foreach (var file in new[] { logEntry, uploadEntry })
        {
            int used = file.Bytes % 512;
            HostCheck.True(after.AsSpan(ClusterOffset(file.Clusters[^1]) + used, 512 - used).IndexOfAnyExcept((byte)0) < 0);
        }
        CheckNeighbors(after, transition.After); CheckChanges(before, after, transition);
        HostCheck.True(!media.Device.State->Dirty && media.Device.State->Submitted == media.Device.State->Completed &&
            media.Device.State->SubmittedBytes == media.Outs * 512UL && media.Device.State->CompletedBytes == media.Outs * 512UL && media.Device.State->DurableBytes == media.Outs * 512UL);
        Console.WriteLine($"WRITABLE_LOG_UPLOAD_CANDIDATE copiedByteContract=true bclExecuted=false logBytes={expectedValues.LogBytes} logSha256={expectedValues.LogSha256} uploadBytes={expectedValues.UploadBytes} uploadSha256={expectedValues.UploadSha256} rows=12 appendGroups=2 uploadChunks=4094,1032 verifierChunk=521 changedSectors={transition.Changes.Count} outs={media.Outs} flushes={media.Flushes} flushAcknowledgedBytes={media.Device.State->DurableBytes} afterRawSha256={transition.After.RawSha256}");

        byte[] ReopenAndRead(string path, byte[] expected)
        {
            var file = files.Open(path); HostCheck.Equal(0L, W.Seek(files.State, file, 0, 1).Value); byte[] copied = new byte[expected.Length];
            for (int offset = 0; offset < expected.Length; offset += 521)
            {
                uint count = (uint)Math.Min(521, expected.Length - offset); byte[] part = files.Read(file, count);
                HostCheck.True(part.AsSpan().SequenceEqual(expected.AsSpan(offset, (int)count))); part.CopyTo(copied, offset);
            }
            HostCheck.True(files.Read(file, 1, expectedBytes: 0).Length == 0); HostCheck.Equal(0L, W.Close(files.State, file)); return copied;
        }
        void CheckNeighbors(byte[] changed, Image.Inspection inspection)
        {
            ulong logAt = seed.Image.Find("LOG.TXT")!.ShortEntryOffset, uploadAt = seed.Image.Find("UPLOAD.BIN")!.ShortEntryOffset;
            HostCheck.True(inspection.Find("LOG.TXT")!.ShortEntryOffset == logAt && inspection.Find("UPLOAD.BIN")!.ShortEntryOffset == uploadAt);
            foreach (var file in seed.Image.Files.Where(file => file.Name is not ("LOG.TXT" or "UPLOAD.BIN")))
            {
                HostCheck.True(inspection.Find(file.Name) is { } untouched && untouched.Sha256 == file.Sha256 && untouched.Clusters.SequenceEqual(file.Clusters) &&
                    before.AsSpan((int)file.ShortEntryOffset, 32).SequenceEqual(changed.AsSpan((int)file.ShortEntryOffset, 32)));
                foreach (uint cluster in file.Clusters) HostCheck.True(before.AsSpan(ClusterOffset(cluster), 512).SequenceEqual(changed.AsSpan(ClusterOffset(cluster), 512)));
            }
            HostCheck.True(inspection.RootClusters.SequenceEqual(seed.Image.RootClusters));
            foreach (uint cluster in seed.Image.RootClusters)
                for (int i = 0; i < 512; i++)
                {
                    int address = ClusterOffset(cluster) + i; ulong position = (ulong)address;
                    if (!(position >= logAt && position - logAt < 32) && !(position >= uploadAt && position - uploadAt < 32))
                        HostCheck.Equal(before[address], changed[address]);
                }
        }
    });

    [HostTest("writable data copied SQLite journal ordering preserves live DB cursors LFN removal guards and actual fake flush frontiers", HostLane.Native)]
    private static void CopiedSqliteJournalOrdering() => WithImage((_, _, _, seed, before) =>
    {
        // Copied protocol arguments/order: original sqlite-trace.5255 lines
        // 288-315, storage-values SHA256 436B4FE9109019F175ED2D62D332CB444C1C8410BB0B80B1F7B68EA817E8DCB0.
        // The paths translate to this fixture, fd numbers are newly allocated,
        // payloads below are test-authored, and W.Open has no POSIX mode input.
        // This executes no SQLite code and asserts neither valid journal/DB
        // format nor recovery, guest, reboot or physical-durability behavior.
        const string journalName = "SQLITE.DB-journal", journalPath = "/data/SQLITE.DB-journal";
        const uint openFlags = 2 | W.Create | W.LargeFile | W.NoFollow | W.CloseOnExec;
        const uint directoryFlags = W.LargeFile | W.CloseOnExec;
        HostCheck.True(openFlags == 0xa8042 && directoryFlags == 0x88000 && W.RwfNoAppend == 0x20 && seed.Image.Find(journalName) is null);
        using var media = new MutationMedia(before); using var files = new DescriptorMedia(media);
        var db = files.Open("/data/SQLITE.DB", openFlags); var reader = files.Open("/data/SQLITE.DB");
        HostCheck.True(files.Read(reader, 1, expectedBytes: 0).Length == 0); // Actual initial EOF, not fabricated zero data.
        var journal = files.Open(journalPath, openFlags);
        var search = new W.FreeSearch(2, 128, 16);
        byte[] journalBytes = Enumerable.Range(0, 512).Select(i => unchecked((byte)(i * 29 + 7))).ToArray();
        byte[] header = Enumerable.Range(0, 12).Select(i => unchecked((byte)(0xe1 - i * 3))).ToArray();
        byte[] firstPage = Enumerable.Range(0, 4096).Select(i => unchecked((byte)(i * 37 + 11))).ToArray();
        byte[] secondPage = Enumerable.Range(0, 4096).Select(i => unchecked((byte)(i * 43 + 17))).ToArray();
        byte[] dbBytes = firstPage.Concat(secondPage).ToArray();
        var boundaries = new List<(string Stage, uint Outs, uint Flushes, ulong Acknowledged)>();
        void Boundary(string stage)
        {
            HostCheck.True(!media.Device.State->Dirty && !media.Device.State->RequestPending &&
                media.Device.State->Submitted == media.Device.State->Completed && media.Device.State->SubmittedBytes == media.Outs * 512UL &&
                media.Device.State->CompletedBytes == media.Outs * 512UL && media.Device.State->DurableBytes == media.Outs * 512UL);
            boundaries.Add((stage, media.Outs, media.Flushes, media.Device.State->DurableBytes));
        }
        void Positioned(W.DescriptorToken file, byte[] bytes, ulong offset)
        {
            HostCheck.Equal((long)bytes.Length, files.Run(W.Pwritev2SingleWithFreeSearch(files.State, file, bytes, offset, 0x20, search)).Value);
            HostCheck.Equal(0L, W.Seek(files.State, file, 0, 1).Value); // Positioned writes never move the description cursor.
        }
        Positioned(journal, journalBytes, 0); files.Sync(journal); Boundary("journal512-fsync");
        var directory = files.Open("/data", directoryFlags); files.Sync(directory); Boundary("parent-fsync");
        HostCheck.Equal(0L, W.Close(files.State, directory));
        byte[] journalCheckpoint = media.Snapshot(); var created = Image.InspectTransition(before, journalCheckpoint);
        var entry = created.After.Find(journalName) ?? throw new InvalidOperationException("copied journal LFN missing");
        HostCheck.True(entry.Name == journalName && entry.ShortAlias != journalName && entry.Bytes == 512 &&
            entry.CopyBytes().AsSpan().SequenceEqual(journalBytes) && created.After.Find("SQLITE.DB")!.Bytes == 0 &&
            created.After.Files.Count == seed.Image.Files.Count + 1 && entry.Inode == entry.ShortEntryOffset / 32 + 256);
        // The independent parser authenticated the full LFN. Check its actual
        // two physical slots, short-entry checksum and original deleted holes.
        ulong journalStart = entry.ShortEntryOffset - 64;
        int at = checked((int)journalStart); byte checksum = 0;
        for (int i = 0; i < 11; i++) checksum = unchecked((byte)(((checksum & 1) << 7) + (checksum >> 1) + journalCheckpoint[(int)entry.ShortEntryOffset + i]));
        HostCheck.True(journalCheckpoint[at] == 0x42 && journalCheckpoint[at + 32] == 1 &&
            journalCheckpoint[at + 11] == 0x0f && journalCheckpoint[at + 43] == 0x0f &&
            journalCheckpoint[at + 13] == checksum && journalCheckpoint[at + 45] == checksum);
        CheckChanges(before, journalCheckpoint, created);
        Positioned(journal, header, 0); header.CopyTo(journalBytes, 0); files.Sync(journal); Boundary("journal12-fsync");
        byte[] headerCheckpoint = media.Snapshot(); var headerChange = Image.InspectTransition(journalCheckpoint, headerCheckpoint);
        HostCheck.True(headerChange.After.Find(journalName) is { } headerEntry && headerEntry.Inode == entry.Inode &&
            headerEntry.ShortEntryOffset == entry.ShortEntryOffset && headerEntry.Clusters.SequenceEqual(entry.Clusters) &&
            headerEntry.CopyBytes().AsSpan().SequenceEqual(journalBytes) && headerChange.After.Find("SQLITE.DB")!.Bytes == 0);
        CheckChanges(journalCheckpoint, headerCheckpoint, headerChange);
        Positioned(db, firstPage, 0); Positioned(db, secondPage, 4096); files.Sync(db); Boundary("db8192-fsync");
        HostCheck.True(files.Read(reader, 31).AsSpan().SequenceEqual(dbBytes.AsSpan(0, 31)) &&
            files.Read(reader, 4096, 4096, true).AsSpan().SequenceEqual(secondPage) && W.Seek(files.State, reader, 0, 1).Value == 31 &&
            W.Seek(files.State, db, 0, 1).Value == 0);
        byte[] databaseCheckpoint = media.Snapshot(); var databaseChange = Image.InspectTransition(headerCheckpoint, databaseCheckpoint);
        HostCheck.True(databaseChange.After.Find("SQLITE.DB") is { } dbEntry && dbEntry.CopyBytes().AsSpan().SequenceEqual(dbBytes) &&
            dbEntry.ShortEntryOffset == seed.Image.Find("SQLITE.DB")!.ShortEntryOffset &&
            databaseChange.After.Find(journalName) is { } retained && retained.Inode == entry.Inode && retained.CopyBytes().AsSpan().SequenceEqual(journalBytes));
        CheckChanges(headerCheckpoint, databaseCheckpoint, databaseChange);
        uint outs = media.Outs, flushes = media.Flushes;
        HostCheck.Equal(0L, W.Close(files.State, journal));
        HostCheck.True(media.Outs == outs && media.Flushes == flushes); // close supplies no pretend sync.
        HostCheck.Equal(0L, files.Run(W.BeginUnlink(files.State, Encoding.ASCII.GetBytes(journalPath))).Value); Boundary("closed-journal-unlink");
        // The measured interval ends after unlink. This additional requested
        // parent barrier is host policy coverage, not an observed SQLite call.
        directory = files.Open("/data", directoryFlags); files.Sync(directory); Boundary("additional-parent-fsync");
        HostCheck.Equal(0L, W.Close(files.State, directory));
        byte[] deleted = media.Snapshot(); var deletion = Image.InspectTransition(databaseCheckpoint, deleted);
        HostCheck.True(deletion.After.Find(journalName) is null && deletion.After.Find(entry.ShortAlias) is null &&
            deletion.After.Files.Count == seed.Image.Files.Count && deletion.After.Find("SQLITE.DB")!.CopyBytes().AsSpan().SequenceEqual(dbBytes) &&
            deletion.After.FreeClusters == seed.Image.FreeClusters - 16);
        for (int slot = 0; slot < 3; slot++)
        {
            HostCheck.Equal((byte)0xe5, deleted[at + slot * 32]);
            HostCheck.True(deleted.AsSpan(at + slot * 32 + 1, 31).IndexOfAnyExcept((byte)0) < 0); // No stale LFN fragment survives.
        }
        foreach (uint cluster in entry.Clusters)
        {
            HostCheck.True(U32(deleted, 2080 * 512 + (int)cluster * 4) == 0 && U32(deleted, (2080 + 993) * 512 + (int)cluster * 4) == 0);
            HostCheck.True(databaseCheckpoint.AsSpan(ClusterOffset(cluster), 512).SequenceEqual(deleted.AsSpan(ClusterOffset(cluster), 512)));
        }
        CheckChanges(databaseCheckpoint, deleted, deletion);
        foreach (byte[] checkpoint in new[] { journalCheckpoint, headerCheckpoint, databaseCheckpoint, deleted })
        {
            var inspected = Image.Inspect(checkpoint);
            foreach (var untouched in seed.Image.Files.Where(x => x.Name != "SQLITE.DB"))
            {
                HostCheck.True(inspected.Find(untouched.Name) is { } same && same.Sha256 == untouched.Sha256 && same.Clusters.SequenceEqual(untouched.Clusters) &&
                    before.AsSpan((int)untouched.ShortEntryOffset, 32).SequenceEqual(checkpoint.AsSpan((int)untouched.ShortEntryOffset, 32)));
                foreach (uint cluster in untouched.Clusters)
                    HostCheck.True(before.AsSpan(ClusterOffset(cluster), 512).SequenceEqual(checkpoint.AsSpan(ClusterOffset(cluster), 512)));
            }
            foreach (uint cluster in seed.Image.RootClusters)
                for (int i = 0; i < 512; i++)
                {
                    int address = ClusterOffset(cluster) + i; ulong position = (ulong)address, dbAt = seed.Image.Find("SQLITE.DB")!.ShortEntryOffset;
                    if (!(position >= dbAt && position - dbAt < 32) && !(position >= journalStart && position - journalStart < 96))
                        HostCheck.Equal(before[address], checkpoint[address]);
                }
        }
        // A live DB description survives journal create/delete revisions with
        // its independent cursor intact. A reused numeric fd cannot rescue the
        // old closed journal token, even for a later actual volume barrier.
        HostCheck.True(files.Read(reader, 8192, 0, true).AsSpan().SequenceEqual(dbBytes) && W.Seek(files.State, reader, 0, 1).Value == 31 &&
            W.Seek(files.State, db, 0, 1).Value == 0);
        var reopened = files.Open("/data/SQLITE.DB");
        HostCheck.True(reopened.Number == journal.Number && reopened.Generation > journal.Generation && W.Seek(files.State, reopened, 0, 1).Value == 0);
        outs = media.Outs; flushes = media.Flushes;
        HostCheck.Equal(-9L, W.BeginSync(files.State, journal).Value); HostCheck.True(media.Outs == outs && media.Flushes == flushes);
        HostCheck.True(boundaries.Count == 6 && boundaries[1].Outs == boundaries[0].Outs && boundaries[1].Flushes == boundaries[0].Flushes + 1 &&
            boundaries[2].Outs > boundaries[1].Outs && boundaries[3].Outs > boundaries[2].Outs && boundaries[4].Outs > boundaries[3].Outs &&
            boundaries[5].Outs == boundaries[4].Outs && boundaries[5].Flushes == boundaries[4].Flushes + 1);
        for (int i = 1; i < boundaries.Count; i++)
            HostCheck.True(boundaries[i].Outs >= boundaries[i - 1].Outs && boundaries[i].Flushes > boundaries[i - 1].Flushes && boundaries[i].Acknowledged >= boundaries[i - 1].Acknowledged);
        foreach (var file in new[] { db, reader, reopened }) HostCheck.Equal(0L, W.Close(files.State, file));
        Console.WriteLine($"WRITABLE_COPIED_SQLITE source=sqlite-trace.5255:288-315 copiedWrites=4 writeFlags=32 testPayloads=true extraPostUnlinkBarrier=true dbBytes={dbBytes.Length} journalBytes={journalBytes.Length} journalInode={entry.Inode} outs={media.Outs} flushes={media.Flushes} flushAcknowledgedBytes={media.Device.State->DurableBytes} dbSha256={Hash(dbBytes)} afterRawSha256={deletion.After.RawSha256} boundaries={string.Join(',', boundaries.Select(x => $"{x.Stage}:{x.Outs}:{x.Flushes}:{x.Acknowledged}"))}");
    });

    [HostTest("writable data transition rejects changes outside partition GPT padding BPB and reserved FSInfo bytes", HostLane.Native)]
    private static void TransitionIdentity() => WithImage((_, _, _, _, before) =>
    {
        TransitionMutant(before, x => x[100 * 512] ^= 1, "transition_outside_partition");
        TransitionMutant(before, x => x[512 + 128] ^= 1, "transition_outside_partition");
        TransitionMutant(before, x => x[^1] ^= 1, "transition_outside_partition");
        TransitionMutant(before, x => { x[2048 * 512 + 3] ^= 1; x[(2048 + 6) * 512 + 3] ^= 1; }, "transition_reserved");
        TransitionMutant(before, x => { x[(2048 + 1) * 512 + 200] = 3; x[(2048 + 7) * 512 + 200] = 3; }, "transition_fsinfo_reserved");
    });

    [HostTest("writable data transition rejects covering owned and free sectors while declaring allocated slack and guardfile changes without authorizing them", HostLane.Native)]
    private static void TransitionCoverage() => WithImage((_, _, _, _, before) =>
    {
        byte[] after = (byte[])before.Clone(); after[ClusterOffset(11) + 511] = 91;
        var slack = Image.InspectTransition(before, after, 1);
        HostCheck.True(slack.Changes.Count == 1 && slack.Changes[0].Kind == Image.SectorKind.FileData &&
            slack.Before.Find("GUARD.TXT")!.Sha256 == slack.After.Find("GUARD.TXT")!.Sha256);
        after[ClusterOffset(11)] ^= 1; var guard = Image.InspectTransition(before, after, 1);
        HostCheck.True(guard.After.Find("GUARD.TXT")!.Sha256 != guard.Before.Find("GUARD.TXT")!.Sha256);
        after[ClusterOffset(12)] = 91;
        // Both images remain structurally valid; a containing allocated sector
        // must not authorize the next free sector or an entire data interval.
        HostCheck.True(Image.Inspect(after).FreeClusters == Image.Inspect(before).FreeClusters);
        Reject(() => Image.InspectTransition(before, after), "transition_unallocated_data");
        TransitionMutant(before, x => x[ClusterOffset(Image.ClusterCount + 1) + 511] = 1, "transition_unallocated_data");
    });

    [HostTest("writable data transition rejects FAT padding unowned entries high bits and intermediate mirror damage", HostLane.Native)]
    private static void TransitionFatOwnership() => WithImage((_, _, _, _, before) =>
    {
        TransitionMutant(before, x => Fat(x, Image.ClusterCount + 2, 1), "transition_fat_ownership");
        TransitionMutant(before, x => Fat(x, 100, 0xa0000000), "transition_fat_ownership");
        TransitionMutant(before, x => Fat(x, 11, 0xafffffff), "transition_fat_ownership");
        TransitionMutant(before, x => x[(2080 + 993) * 512 + 5 * 4] ^= 1, "fat_mirror");
        byte[] invalidBefore = (byte[])before.Clone(); Fat(invalidBefore, 41, 0);
        Reject(() => Image.InspectTransition(invalidBefore, before), "chain");
    });

    [HostTest("writable data transition hard capacity rejects valid oversized mutations and invalid caller bounds without partial receipts", HostLane.Native)]
    private static void TransitionCapacity() => WithImage((_, _, _, seed, before) =>
    {
        byte[] after = (byte[])before.Clone(); int entry = ClusterOffset(2);
        W16(after, entry + 26, 1000); W32(after, entry + 28, 129 * 512);
        for (uint cluster = 1000; cluster < 1129; cluster++)
        { Fat(after, cluster, cluster == 1128 ? 0x0fffffff : cluster + 1); after.AsSpan(ClusterOffset(cluster), 512).Fill(0x7d); }
        Info(after, 488, seed.Image.FreeClusters - 129);
        HostCheck.True(Image.Inspect(after).Find("LOG.TXT")!.Bytes == 129 * 512);
        Reject(() => Image.InspectTransition(before, after), "transition_capacity");
        byte[] small = (byte[])before.Clone(); small[ClusterOffset(5)] ^= 1; small[ClusterOffset(41)] ^= 1;
        Reject(() => Image.InspectTransition(before, small, 1), "transition_capacity");
        foreach (int limit in new[] { int.MinValue, 0, 129, int.MaxValue })
            Reject(() => Image.InspectTransition([], [], limit), "transition_limit");
        Reject(() => Image.InspectTransition(before.AsSpan(0, before.Length - 1), before), "size");
    });

    [HostTest("writable data transition copied extractions hashes sector ordering and canonical JSON have no mutable input aliases", HostLane.Native)]
    private static void TransitionCopiedValues() => WithImage((_, folder, _, _, before) =>
    {
        byte[] after = (byte[])before.Clone(); after[ClusterOffset(5)] ^= 1;
        var receipt = Image.InspectTransition(before, after, 1);
        byte oldFirst = receipt.Before.Find("FRAG.BIN")!.CopyBytes()[0], newFirst = receipt.After.Find("FRAG.BIN")!.CopyBytes()[0];
        var repeated = Image.InspectTransition(before, after, 1); HostCheck.True(receipt.CopyJson().AsSpan().SequenceEqual(repeated.CopyJson()));
        CheckChanges(before, after, receipt);
        byte[] json = receipt.CopyJson(), file = receipt.After.Find("FRAG.BIN")!.CopyBytes(); file.AsSpan().Clear(); json.AsSpan().Clear();
        before.AsSpan().Clear(); after.AsSpan().Clear();
        HostCheck.True(oldFirst != newFirst && receipt.Before.Find("FRAG.BIN")!.CopyBytes()[0] == oldFirst && receipt.After.Find("FRAG.BIN")!.CopyBytes()[0] == newFirst);
        using var parsed = JsonDocument.Parse(receipt.CopyJson());
        HostCheck.True(parsed.RootElement.GetProperty("schema").GetString() == "CLROS_WRITABLE_DATA_TRANSITION_V1" &&
            parsed.RootElement.GetProperty("changes").GetArrayLength() == 1 && !Encoding.UTF8.GetString(receipt.CopyJson()).Contains(folder, StringComparison.Ordinal));
        bool readOnly = false;
        try { ((IList<Image.SectorChange>)receipt.Changes)[0] = new(0, Image.SectorKind.FileData, "bad", "bad"); } catch (NotSupportedException) { readOnly = true; }
        HostCheck.True(readOnly);
    });

    [HostTest("writable data transition admits identical independently valid snapshots without inventing mutation facts", HostLane.Native)]
    private static void TransitionNoChanges() => WithImage((_, _, _, _, before) =>
    {
        var receipt = Image.InspectTransition(before, before);
        HostCheck.True(receipt.Changes.Count == 0 && receipt.Before.RawSha256 == receipt.After.RawSha256 && receipt.Before.Find("FRAG.BIN")!.CopyBytes().AsSpan().SequenceEqual(receipt.After.Find("FRAG.BIN")!.CopyBytes()));
    });

    private static void CheckChanges(byte[] before, byte[] after, Image.TransitionReceipt receipt)
    {
        var expected = Enumerable.Range(0, before.Length / 512).Where(lba => !before.AsSpan(lba * 512, 512).SequenceEqual(after.AsSpan(lba * 512, 512))).Select(x => (ulong)x).ToArray();
        HostCheck.True(receipt.Changes.Select(x => x.Lba).SequenceEqual(expected));
        foreach (var change in receipt.Changes)
        { HostCheck.Equal(Hash(before.AsSpan(checked((int)change.Lba * 512), 512).ToArray()), change.OldSha256); HostCheck.Equal(Hash(after.AsSpan(checked((int)change.Lba * 512), 512).ToArray()), change.NewSha256); }
    }
    private static void TransitionMutant(byte[] before, Action<byte[]> mutate, string reason)
    { byte[] after = (byte[])before.Clone(); mutate(after); Reject(() => Image.InspectTransition(before, after), reason); }

    // Uses the original committed provider, not a test-created fd model. The
    // provider is detached before the borrowed one-queue engine/device owners.
    private sealed class DescriptorMedia : IDisposable
    {
        private readonly MutationMedia media;
        internal readonly W.State* State = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        private readonly W.Descriptor* slots = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        private readonly W.Description* descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        private readonly byte* input = (byte*)NativeMemory.AllocZeroed(W.IoBytes);
        internal DescriptorMedia(MutationMedia owner, ulong epoch = 702)
        {
            media = owner;
            HostCheck.True(W.Start(State, new() { Engine = media.State, Epoch = epoch, Descriptors = slots, Descriptions = descriptions,
                DescriptorCount = 32, DescriptionCount = 32, Input = input, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }));
        }
        private W.Result Wait(W.Result begun)
        {
            if (begun.Phase != W.Progress.Pending) return begun;
            for (int turn = 0; turn < 100000; turn++)
            {
                var progress = W.Pump(State); media.CompleteHardware();
                if (progress.Phase == W.Progress.Ready) return progress;
                HostCheck.Equal(W.Progress.Pending, progress.Phase);
            }
            throw new InvalidOperationException("named media provider did not finish");
        }
        internal W.Result Run(W.Result begun)
        {
            var ready = Wait(begun); var result = ready.Phase == W.Progress.Ready ? W.Complete(State, begun.Request) : ready;
            HostCheck.Equal(W.Progress.Immediate, result.Phase); HostCheck.True(result.Value >= 0); return result;
        }
        internal W.DescriptorToken Open(string path, uint flags = 0) => Run(W.Open(State, Encoding.ASCII.GetBytes(path), flags)).Descriptor;
        internal byte[] Read(W.DescriptorToken file, uint count, ulong offset = 0, bool positioned = false, uint? expectedBytes = null)
        {
            var begun = W.BeginRead(State, file, count, offset, positioned); HostCheck.Equal(W.Progress.Ready, Wait(begun).Phase);
            byte[] output = new byte[count]; var copied = W.CopyRead(State, begun.Request, output);
            HostCheck.True(copied.Phase == W.Progress.Immediate && copied.Value == (expectedBytes ?? count)); return output[..checked((int)copied.Value)];
        }
        internal void Sync(W.DescriptorToken file)
        {
            ulong submitted = media.Device.State->Submitted, completed = media.Device.State->Completed, writeBytes = media.Device.State->SubmittedBytes;
            uint flushes = media.Flushes, outs = media.Outs;
            HostCheck.Equal(0L, Run(W.BeginSync(State, file)).Value);
            // A clean barrier still performs one real FLUSH, rather than
            // fabricating success because write frontiers already agree.
            HostCheck.True(media.Flushes == flushes + 1 && media.Outs == outs && media.Device.State->Submitted > submitted &&
                media.Device.State->Submitted - submitted == media.Device.State->Completed - completed &&
                media.Device.State->SubmittedBytes == writeBytes && media.Device.State->CompletedBytes == writeBytes && media.Device.State->DurableBytes == writeBytes &&
                media.Device.State->LastOperation == B.Operation.Flush);
        }
        public void Dispose()
        {
            HostCheck.Equal(W.Progress.Retired, W.Detach(State).Phase);
            NativeMemory.Free(input); NativeMemory.Free(descriptions); NativeMemory.Free(slots); NativeMemory.Free(State);
        }
    }

    private sealed class MutationMedia : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(48 * 4096, 4096);
        internal uint Outs, Flushes;
        internal MutationMedia(byte[] image, uint workBudget = 100000, ulong epoch = 702)
        {
            Device = new((ulong)Image.ImageBytes / 512); Device.UseMedium(image); HostCheck.Equal(B.Status.Success, Device.Start());
            ulong cursor = (ulong)backing;
            ulong Take(ulong count) { ulong address = (cursor + 15) & ~15UL; cursor = address + count; return address; }
            C.Admission input = new() { Device = Device.State, Owner = 701, Epoch = epoch, PartitionFirst = Image.PartitionFirstLba, PartitionLast = Image.PartitionLastLba,
                ChangeCapacity = C.JournalChanges, ChainCapacity = C.MaximumClusters, InputCapacity = C.WriteBytes, WorkBudget = workBudget,
                Changes = (Fat32Mutation.Change*)Take(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)), Before = (byte*)Take(C.JournalChanges * 512UL), After = (byte*)Take(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)Take(C.MaximumClusters * 4UL), FileMap = (uint*)Take(C.MaximumClusters * 4UL), WalkMap = (uint*)Take(C.MaximumClusters * 4UL),
                Sector = (byte*)Take(512), Fat0 = (byte*)Take(512), Fat1 = (byte*)Take(512), Input = (byte*)Take(C.WriteBytes),
                Name = (ushort*)Take(C.NameWords * 2UL), LongName = (ushort*)Take(C.VfatWords * 2UL), EncodedEntries = (byte*)Take(C.EncodedEntryBytes) };
            HostCheck.True(cursor - (ulong)backing <= 48 * 4096UL); new Span<byte>(backing, 48 * 4096).Clear();
            HostCheck.Equal(C.Status.Pending, C.Start(State, input)); Run();
        }
        internal void Run()
        {
            for (int i = 0; i < 100000; i++)
            {
                var status = C.Pump(State);
                CompleteHardware();
                if (status != C.Status.Pending) { HostCheck.Equal(C.Status.Ready, status); return; }
            }
            throw new InvalidOperationException("transition shared-media mutation did not terminate");
        }
        internal void CompleteHardware()
        {
            if (!Device.Hardware->Pending) return;
            if (Device.Hardware->Type == 1) Outs++; if (Device.Hardware->Type == 4) Flushes++; Device.Complete();
        }
        internal C.FileToken Open(string name)
        {
            C.RequestToken request = default; HostCheck.Equal(C.Status.Pending, C.BeginOpenRoot(State, name, &request)); Run();
            C.FileToken file = default; HostCheck.Equal(C.Status.Success, C.CopyOpen(State, request, &file)); return file;
        }
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Device.Hardware->Persistent, Image.ImageBytes).ToArray();
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Device.State)); HostCheck.Equal(C.Status.Success, C.Stop(State));
            Device.CheckGuards(); Device.Dispose(); NativeMemory.AlignedFree(backing); NativeMemory.Free(State);
        }
    }

    private static void WithImage(Action<string, string, string, Image.Receipt, byte[]> action)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string folder = Path.Combine(root, "artifacts", "writable-data-image-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { string path = Path.Combine(folder, "initial.raw"); var receipt = Image.Create(root, path); action(root, folder, path, receipt, File.ReadAllBytes(path)); }
        finally { Directory.Delete(folder, recursive: true); }
    }
    private static void Mutant(byte[] disk, Action<byte[]> mutate, string reason)
    { byte[] copy = (byte[])disk.Clone(); mutate(copy); Reject(() => Image.Inspect(copy), reason); }
    private static void Reject(Action action, string suffix)
    {
        try { action(); throw new InvalidOperationException("expected writable_image_" + suffix); }
        catch (ToolFailure error) { HostCheck.Equal("writable_image_" + suffix, error.Reason); }
    }
    private static byte[] Extract(byte[] disk, Image.Entry file)
    {
        byte[] bytes = new byte[file.Bytes]; int position = 0;
        foreach (var extent in file.Extents)
        {
            int take = Math.Min(checked((int)extent.Clusters * 512), bytes.Length - position);
            disk.AsSpan(checked((int)extent.FirstLba * 512), take).CopyTo(bytes.AsSpan(position)); position += take;
        }
        HostCheck.Equal(bytes.Length, position); return bytes;
    }
    private static void Fat(byte[] disk, uint cluster, uint value)
    { W32(disk, 2080 * 512 + (int)cluster * 4, value); W32(disk, (2080 + 993) * 512 + (int)cluster * 4, value); }
    private static void Info(byte[] disk, int offset, uint value)
    { W32(disk, (2048 + 1) * 512 + offset, value); W32(disk, (2048 + 7) * 512 + offset, value); }
    private static void RecrcHeader(byte[] disk, int header)
    { W32(disk, header + 16, 0); W32(disk, header + 16, Crc32.Compute(disk.AsSpan(header, 92))); }
    private static void RecrcArrays(byte[] disk)
    {
        W32(disk, 512 + 88, Crc32.Compute(disk.AsSpan(1024, 16384))); RecrcHeader(disk, 512);
        int backup = disk.Length - 512;
        W32(disk, backup + 88, Crc32.Compute(disk.AsSpan(disk.Length - 33 * 512, 16384))); RecrcHeader(disk, backup);
    }
    private static int ClusterOffset(uint c) => checked((int)(4066 + c - 2) * 512);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
    private static void W16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void W32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void W64(byte[] bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
}
