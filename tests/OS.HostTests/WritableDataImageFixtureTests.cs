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

// Host image/extraction controls only. No native/device/guest persistence claim.
internal static unsafe class WritableDataImageFixtureTests
{
    [HostTest("writable data image two fresh outputs and canonical extraction receipts are byte identical", HostLane.Native)]
    private static void Deterministic() => WithImage((root, folder, path, receipt, disk) =>
    {
        string second = Path.Combine(folder, "second.raw");
        var repeated = Image.Create(root, second);
        HostCheck.True(receipt.Image.RawSha256 == repeated.Image.RawSha256 &&
            File.ReadAllBytes(second).AsSpan().SequenceEqual(disk) && receipt.CopyJson().AsSpan().SequenceEqual(repeated.CopyJson()));
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

    private sealed class MutationMedia : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(48 * 4096, 4096);
        internal uint Outs, Flushes;
        internal MutationMedia(byte[] image)
        {
            Device = new((ulong)Image.ImageBytes / 512); Device.UseMedium(image); HostCheck.Equal(B.Status.Success, Device.Start());
            ulong cursor = (ulong)backing;
            ulong Take(ulong count) { ulong address = (cursor + 15) & ~15UL; cursor = address + count; return address; }
            C.Admission input = new() { Device = Device.State, Owner = 701, Epoch = 702, PartitionFirst = Image.PartitionFirstLba, PartitionLast = Image.PartitionLastLba,
                ChangeCapacity = C.JournalChanges, ChainCapacity = C.MaximumClusters, InputCapacity = C.WriteBytes, WorkBudget = 100000,
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
                if (Device.Hardware->Pending)
                { if (Device.Hardware->Type == 1) Outs++; if (Device.Hardware->Type == 4) Flushes++; Device.Complete(); }
                if (status != C.Status.Pending) { HostCheck.Equal(C.Status.Ready, status); return; }
            }
            throw new InvalidOperationException("transition shared-media mutation did not terminate");
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
            Device.Dispose(); NativeMemory.AlignedFree(backing); NativeMemory.Free(State);
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
