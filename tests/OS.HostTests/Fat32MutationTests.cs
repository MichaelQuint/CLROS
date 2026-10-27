using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using M = OS.Storage.Fat32Mutation;
using Block = OS.Storage.WritableBlockSession;

// Actual host block-session OUT/FLUSH protocol plus independent volatile/durable
// media model. Neither an in-memory FLUSH nor recovery proves physical storage.
internal static unsafe class Fat32MutationTests
{
    private const int First = 8, Reserved = 32, FatSectors = 512, Clusters = 65525;
    private const int Data = First + Reserved + FatSectors * 2, Sectors = Data + Clusters;
    private static byte[] Payload(int count, int seed = 17) => Enumerable.Range(0, count).Select(i => unchecked((byte)(i * 73 + seed))).ToArray();
    private static byte[] Alias(int i) => System.Text.Encoding.ASCII.GetBytes("F" + i.ToString("D7", System.Globalization.CultureInfo.InvariantCulture) + "DAT");
    private static void Put16(Span<byte> b, int p, ushort n) => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(b[p..], n);
    private static void Put32(Span<byte> b, int p, uint n) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b[p..], n);
    private static uint U32(ReadOnlySpan<byte> b, int p) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b[p..]);

    [HostTest("fat32 mutation maximum LFN encoding uses21 entries and strict existing decoder")]
    private static void Encoding()
    {
        byte[] shortEntry = new byte[32]; Alias(1).CopyTo(shortEntry, 0); shortEntry[11] = 0x20;
        Span<byte> encoded = stackalloc byte[672]; string name = new('x', 255);
        HostCheck.Equal(M.Status.Success, M.EncodeEntryRun(name, shortEntry, encoded, out int count));
        HostCheck.Equal(21, count); HostCheck.True(encoded[0] == 0x54 && encoded[19 * 32] == 1);
        int checksum = 0; foreach (byte c in shortEntry.AsSpan(0, 11)) checksum = ((checksum & 1) * 128 + (checksum >> 1) + c) & 255;
        HostCheck.Equal((byte)checksum, encoded[13]);
        Span<ushort> words = stackalloc ushort[260]; LiveVfatState state = default;
        for (int i = 0; i < 21; i++)
        {
            var result = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(encoded.Slice(i * 32, 32), ref state, words, out var entry);
            HostCheck.Equal(i == 20 ? LiveDirectoryStatus.Entry : LiveDirectoryStatus.LongName, result);
            if (i == 20) HostCheck.Equal((ushort)255, entry.NameLength);
        }
        foreach (string invalid in new[] { "", ".", "..", "a/../b", "a.", "a ", "a?", "\ud800", new('x', 256) })
            HostCheck.Equal(M.Status.InvalidName, M.EncodeEntryRun(invalid, shortEntry, encoded, out _));
        HostCheck.Equal(M.Status.InvalidStorage, M.EncodeEntryRun(name, shortEntry, encoded[..640], out _));
        HostCheck.Equal(M.Status.InvalidStorage, M.EncodeEntryRun("x", shortEntry, shortEntry, out _));
        HostCheck.Equal(M.Status.Success, M.EncodeEntryRun("valid\ud83d\ude00.dat", shortEntry, encoded, out _));
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        bool good = true; for (int i = 0; i < 128; i++) good &= M.EncodeEntryRun(name, shortEntry, encoded, out _) == M.Status.Success;
        HostCheck.True(good && GC.GetAllocatedBytesForCurrentThread() == allocation);
    }

    [HostTest("fat32 mutation entry encoding preserves slot boundaries UTF16 words metadata and unused bytes")]
    private static void EncodingBoundaries()
    {
        byte[] shortEntry = new byte[32]; Alias(1).CopyTo(shortEntry, 0); shortEntry[11] = 0x21;
        Put16(shortEntry, 22, 0x4321); Put16(shortEntry, 24, 0x5678); Put16(shortEntry, 26, 2); Put32(shortEntry, 28, 17);
        string[] names = ["x", new('a', 12), new('b', 13), new('c', 14), new('d', 26), new('e', 27), new('f', 255), new string('g', 12) + "\ud83d\ude00.dat"];
        int[] longSlots = [1, 1, 1, 2, 2, 3, 20, 2];
        int[] wordOffsets = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
        byte[] output = new byte[704];
        for (int n = 0; n < names.Length; n++)
        {
            Array.Fill(output, (byte)0xcc);
            HostCheck.Equal(M.Status.Success, M.EncodeEntryRun(names[n], shortEntry, output.AsSpan(16, 672), out int count));
            HostCheck.Equal(longSlots[n] + 1, count);
            HostCheck.True(output.AsSpan(16 + longSlots[n] * 32, 32).SequenceEqual(shortEntry));
            for (int i = 0; i < 16; i++) HostCheck.Equal((byte)0xcc, output[i]);
            for (int i = 16 + count * 32; i < output.Length; i++) HostCheck.Equal((byte)0xcc, output[i]);
            for (int physical = 0; physical < longSlots[n]; physical++)
            {
                int ordinal = longSlots[n] - physical, start = 16 + physical * 32;
                HostCheck.Equal((byte)(ordinal | (physical == 0 ? 0x40 : 0)), output[start]);
                HostCheck.Equal((byte)0x0f, output[start + 11]);
                HostCheck.Equal((byte)0, output[start + 12]);
                HostCheck.Equal((ushort)0, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(start + 26)));
                for (int word = 0; word < wordOffsets.Length; word++)
                {
                    int position = (ordinal - 1) * 13 + word;
                    ushort expected = position < names[n].Length ? names[n][position] : position == names[n].Length ? (ushort)0 : ushort.MaxValue;
                    HostCheck.Equal(expected, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(start + wordOffsets[word])));
                }
            }
        }
    }

    [HostTest("fat32 mutation entry encoding rejects full partial and trailing input aliases before writing")]
    private static void EncodingAliases()
    {
        byte[] entry = new byte[32]; Alias(1).CopyTo(entry, 0); entry[11] = 0x20;
        byte[] backing = new byte[256];
        foreach (int offset in new[] { 0, 16, 32, 95, 96, 128 })
        {
            Array.Fill(backing, (byte)0xcc); entry.CopyTo(backing, offset);
            byte[] before = (byte[])backing.Clone();
            int destinationBytes = offset == 128 ? 160 : 64;
            bool alias = offset is 16 or 32 or 95 or 128;
            HostCheck.Equal(alias ? M.Status.InvalidStorage : M.Status.Success,
                M.EncodeEntryRun("test", backing.AsSpan(offset, 32), backing.AsSpan(32, destinationBytes), out int count));
            HostCheck.Equal(alias ? 0 : 2, count);
            if (alias) HostCheck.True(backing.AsSpan().SequenceEqual(before));
        }
        foreach (int offset in new[] { 0, 24, 26, 32, 94, 96, 180 })
        {
            Array.Fill(backing, (byte)0xcc);
            Span<char> name = MemoryMarshal.Cast<byte, char>(backing.AsSpan(offset, 8)); "test".AsSpan().CopyTo(name);
            byte[] before = (byte[])backing.Clone();
            int destinationBytes = offset == 180 ? 160 : 64;
            bool alias = offset is 26 or 32 or 94 or 180;
            HostCheck.Equal(alias ? M.Status.InvalidStorage : M.Status.Success,
                M.EncodeEntryRun(name, entry, backing.AsSpan(32, destinationBytes), out int count));
            HostCheck.Equal(alias ? 0 : 2, count);
            if (alias) HostCheck.True(backing.AsSpan().SequenceEqual(before));
        }
    }

    [HostTest("fat32 mutation entry encoding rejects traversal malformed UTF16 and invalid short framing")]
    private static void EncodingInvalidInputs()
    {
        byte[] entry = new byte[32]; Alias(1).CopyTo(entry, 0); entry[11] = 0x20;
        byte[] output = new byte[672]; Array.Fill(output, (byte)0x5a); byte[] before = (byte[])output.Clone();
        foreach (string invalid in new[] { ".", "..", "a.", "a ", "\udc00", "a\ud800b", "\ud800\ud800", "\ud800\udc00\udc00", "\uffff", new('x', 256) })
        {
            HostCheck.Equal(M.Status.InvalidName, M.EncodeEntryRun(invalid, entry, output, out int count));
            HostCheck.Equal(0, count); HostCheck.True(output.AsSpan().SequenceEqual(before));
        }
        HostCheck.Equal(M.Status.InvalidName, M.EncodeEntryRun("valid", entry.AsSpan(0, 31), output, out int shortCount));
        HostCheck.Equal(0, shortCount); HostCheck.True(output.AsSpan().SequenceEqual(before));
        entry[11] = 0x0f;
        HostCheck.Equal(M.Status.InvalidName, M.EncodeEntryRun("valid", entry, output, out int lfnCount));
        HostCheck.Equal(0, lfnCount); HostCheck.True(output.AsSpan().SequenceEqual(before));
        entry[11] = 0x20; entry[0] = (byte)' ';
        HostCheck.Equal(M.Status.InvalidAlias, M.EncodeEntryRun("valid", entry, output, out int invalidAliasCount));
        HostCheck.Equal(0, invalidAliasCount);
    }

    [HostTest("fat32 mutation create grow gap overwrite truncate regrow rename delete independently reopen")]
    private static void Lifecycle()
    {
        using var f = new Fixture(); var s = f.Session(); byte[] initial = Payload(700), next = Payload(31, 91);
        HostCheck.Equal(M.Status.Planned, s.Create("/first long file.dat", Alias(1), initial));
        HostCheck.True(!s.Dirty && f.H->Notifies == 0); Run(f, ref s); Check(f, "F0000001.DAT", initial);
        HostCheck.Equal(M.Status.Success, s.Open("/first long file.dat", out var a));
        HostCheck.Equal(M.Status.Planned, s.Write(a, 1400, next)); Run(f, ref s);
        byte[] expected = new byte[1431]; initial.CopyTo(expected, 0); next.CopyTo(expected, 1400); Check(f, "F0000001.DAT", expected);
        HostCheck.Equal(M.Status.StaleToken, s.Flush(a)); HostCheck.Equal(M.Status.Success, s.Open("/first long file.dat", out a));
        HostCheck.Equal(M.Status.Planned, s.Write(a, 9, next)); Run(f, ref s); next.CopyTo(expected, 9); Check(f, "F0000001.DAT", expected);
        HostCheck.Equal(M.Status.Success, s.Open("/F0000001.DAT", out a));
        HostCheck.Equal(M.Status.Planned, s.Truncate(a, 10)); Run(f, ref s); Check(f, "F0000001.DAT", expected[..10]);
        HostCheck.Equal(M.Status.Success, s.Open("/first long file.dat", out a));
        HostCheck.Equal(M.Status.Planned, s.Truncate(a, 1000)); Run(f, ref s);
        byte[] regrown = new byte[1000]; expected[..10].CopyTo(regrown, 0); Check(f, "F0000001.DAT", regrown);
        HostCheck.Equal(M.Status.Success, s.Open("/first long file.dat", out a));
        HostCheck.Equal(M.Status.Planned, s.Rename(a, "/second long file.dat", Alias(2))); Run(f, ref s); Check(f, "F0000002.DAT", regrown);
        HostCheck.Equal(M.Status.NotFound, s.Open("/first long file.dat", out _));
        HostCheck.Equal(M.Status.Success, s.Open("/second long file.dat", out a));
        HostCheck.Equal(M.Status.Planned, s.Delete(a)); Run(f, ref s);
        HostCheck.Equal(M.Status.NotFound, s.Open("/second long file.dat", out _)); Check(f, "KEEP.TXT", [4, 5, 6]);
        HostCheck.True(f.Snapshot.AsSpan().SequenceEqual(f.Durable) && !s.Dirty);
    }

    [HostTest("fat32 mutation full directory grows two clusters for maximum LFN and restores consumed terminator")]
    private static void DirectoryGrowth()
    {
        using var f = new Fixture();
        for (int i = 1; i < 16; i++)
        {
            var entry = f.Snapshot.AsSpan(Data * 512 + i * 32, 32); Alias(i + 20).CopyTo(entry); entry[11] = 0x20;
        }
        f.Snapshot.AsSpan().CopyTo(f.Volatile); f.Snapshot.AsSpan().CopyTo(f.Durable);
        var s = f.Session(); string name = new('q', 255);
        HostCheck.Equal(M.Status.Planned, s.Create("/" + name, Alias(1), [])); Run(f, ref s);
        HostCheck.Equal(M.Status.Success, s.Open("/" + name, out var a)); HostCheck.Equal(0U, a.Length);
        uint first = U32(f.Durable, (First + Reserved) * 512 + 2 * 4) & 0xfffffff;
        uint second = U32(f.Durable, (First + Reserved) * 512 + (int)first * 4) & 0xfffffff;
        HostCheck.True(first >= 4 && second >= 4 && second != first);
        HostCheck.Equal((byte)0, f.Durable[(Data + (int)second - 2) * 512 + 5 * 32]);
        // Bytes following the old terminator are stale and must stay hidden by
        // a new end marker after insertion, including across a chain boundary.
        HostCheck.Equal(M.Status.Planned, s.Delete(a)); Run(f, ref s);
        Check(f, "KEEP.TXT", [4, 5, 6]);
        for (int i = 1; i < 16; i++) HostCheck.True(Fat32Parser.FindRootDirectoryEntry(new Device(f.H->Durable), First, "F" + (i + 20).ToString("D7") + ".DAT").Succeeded);
    }

    [HostTest("fat32 mutation delete then reused short slot rejects stale flush without device publication")]
    private static void Stale()
    {
        using var f = new Fixture(); var s = f.Session();
        HostCheck.Equal(M.Status.Planned, s.Create("/a", Alias(1), Payload(10))); Run(f, ref s);
        HostCheck.Equal(M.Status.Success, s.Open("/a", out var a));
        HostCheck.Equal(M.Status.Planned, s.Delete(a)); Run(f, ref s);
        byte[] b = Payload(17, 33); HostCheck.Equal(M.Status.Planned, s.Create("/b", Alias(2), b)); Run(f, ref s);
        HostCheck.Equal(M.Status.Success, s.Open("/b", out var current)); HostCheck.Equal(a.EntryOffset, current.EntryOffset);
        uint notifies = f.H->Notifies; ulong durable = f.State->DurableBytes;
        HostCheck.Equal(M.Status.StaleToken, s.Flush(a)); HostCheck.Equal(M.Status.StaleToken, s.Write(a, 0, [7]));
        HostCheck.True(notifies == f.H->Notifies && durable == f.State->DurableBytes); Check(f, "F0000002.DAT", b);
        HostCheck.Equal(M.Status.Planned, s.Flush(current)); Run(f, ref s); HostCheck.True(f.H->Notifies == notifies + 1);
    }

    [HostTest("fat32 mutation every create data FAT mirror directory FSInfo and flush failure retains recoverable journal")]
    private static void Failures()
    {
        int requests;
        using (var baseline = new Fixture())
        {
            var s = baseline.Session(); HostCheck.Equal(M.Status.Planned, s.Create("/new item", Alias(1), Payload(700))); Run(baseline, ref s);
            requests = (int)baseline.H->Notifies;
            HostCheck.True(baseline.Trace.Contains((1, (ulong)(First + Reserved))) && baseline.Trace.Contains((1, (ulong)(First + Reserved + FatSectors))) &&
                baseline.Trace.Contains((1, (ulong)(First + 1))) && baseline.Trace.Contains((1, (ulong)(First + 7))) && baseline.Trace.Count(x => x.Type == 4) == 4);
        }
        for (int at = 1; at <= requests; at++)
        {
            using var f = new Fixture(); var s = f.Session();
            byte[] original = f.Snapshot.ToArray(); HostCheck.Equal(M.Status.Planned, s.Create("/new item", Alias(1), Payload(700)));
            f.FailAt = at;
            HostCheck.Equal(M.Status.RecoveryRequired, Run(f, ref s, false));
            HostCheck.True(s.Dirty && s.RecoveryNeeded && f.State->RetryRequired && f.State->LastFailure == Block.Status.DeviceIoError);
            HostCheck.Equal(M.Status.Success, s.InspectJournal(out var receipt));
            // An error is allowed to have changed any submitted prefix; persist
            // that partial cache and reopen before recovering retained bytes.
            f.Volatile.CopyTo(f.Durable);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.True(s.Dirty && s.LastBlockFailure == Block.Status.DeviceIoError);
            f.Durable.CopyTo(f.Snapshot); // Reopen actual partial media, not the old clean cache.
            f.Restart(); var reopened = f.Session();
            HostCheck.Equal(M.Status.RecoveryRequired, reopened.ResumeRecovery(receipt));
            HostCheck.Equal(M.Status.Planned, reopened.Recover(f.State)); Run(f, ref reopened);
            HostCheck.True(!reopened.Dirty && original.AsSpan().SequenceEqual(f.Durable)); Check(f, "KEEP.TXT", [4, 5, 6]);
        }
    }

    [HostTest("fat32 mutation cancellation before issue discards and after issue requires drained rollback")]
    private static void Cancellation()
    {
        using var f = new Fixture(); var s = f.Session();
        HostCheck.Equal(M.Status.Planned, s.Create("/cancelled", Alias(1), Payload(600)));
        HostCheck.Equal(M.Status.Cancelled, s.Cancel()); HostCheck.True(!s.Dirty && f.H->Notifies == 0);
        HostCheck.Equal(M.Status.Planned, s.Create("/cancelled", Alias(1), Payload(600)));
        HostCheck.Equal(M.Status.Pending, s.Pump()); HostCheck.Equal(M.Status.Pending, s.Cancel());
        HostCheck.Equal(M.Status.InvalidJournal, s.Recover(f.State));
        f.Complete(); HostCheck.Equal(M.Status.RecoveryRequired, s.Pump());
        HostCheck.True(s.Dirty && f.State->CompletedBytes == 512);
        HostCheck.Equal(M.Status.Planned, s.Recover(f.State)); Run(f, ref s); HostCheck.True(!s.Dirty);
        HostCheck.Equal(M.Status.NotFound, s.Open("/cancelled", out _));
    }

    [HostTest("fat32 mutation bounded capacity budget alias malformed mirror and journal substitutions fail closed")]
    private static void Rejections()
    {
        using var f = new Fixture(); var tiny = f.Session(2);
        HostCheck.Equal(M.Status.CapacityExceeded, tiny.Create("/file", Alias(1), Payload(700))); HostCheck.True(!tiny.Dirty && f.H->Notifies == 0);
        var s = f.Session();
        HostCheck.Equal(M.Status.InvalidName, s.Create("/../escape", Alias(1), []));
        HostCheck.Equal(M.Status.InvalidAlias, s.Create("/file", "bad"u8, []));
        HostCheck.Equal(M.Status.Planned, s.Create("/file", Alias(1), [])); Run(f, ref s);
        HostCheck.Equal(M.Status.AlreadyExists, s.Create("/FILE", Alias(2), []));
        HostCheck.Equal(M.Status.AlreadyExists, s.Create("/other", Alias(1), []));
        HostCheck.Equal(M.Status.InvalidStorage, s.Create("/buffer alias", Alias(2), f.Snapshot.AsSpan(0, 1)));
        var budget = f.Session(64, 1); HostCheck.Equal(M.Status.TraversalBudgetExceeded, budget.Open("/file", out _));
        // Mirror0 and1 diverge before planning: no half allocation is published.
        f.Snapshot[(First + Reserved + FatSectors) * 512 + 4 * 4] ^= 1;
        HostCheck.Equal(M.Status.MirrorMismatch, s.Create("/second", Alias(2), [1]));
        f.Snapshot[(First + Reserved + FatSectors) * 512 + 4 * 4] ^= 1;
        HostCheck.Equal(M.Status.Planned, s.Create("/second", Alias(2), [1])); HostCheck.Equal(M.Status.Success, s.InspectJournal(out var receipt));
        HostCheck.Equal(M.Status.Cancelled, s.Cancel());
        f.After[0] ^= 1; var reopened = f.Session(); HostCheck.Equal(M.Status.InvalidJournal, reopened.ResumeRecovery(receipt)); f.After[0] ^= 1;
        byte[] frame = new byte[sizeof(M.JournalReceipt)]; MemoryMarshal.Write(frame, in receipt); frame[16] ^= 128;
        HostCheck.Equal(M.Status.InvalidJournal, reopened.ResumeRecovery(MemoryMarshal.Read<M.JournalReceipt>(frame)));
        f.Changes[0].Sector = 0; HostCheck.Equal(M.Status.InvalidJournal, reopened.ResumeRecovery(receipt));
    }

    [HostTest("fat32 mutation seeded model operations preserve bytes mirrors and unrelated entries after each flush")]
    private static void Model()
    {
        using var f = new Fixture(); var s = f.Session(); var random = new Random(0x216799);
        byte[] expected = []; bool exists = false;
        for (int step = 0; step < 48; step++)
        {
            if (!exists)
            {
                expected = Payload(random.Next(0, 1100), step);
                HostCheck.Equal(M.Status.Planned, s.Create("/model.dat", Alias(1), expected)); exists = true;
            }
            else
            {
                HostCheck.Equal(M.Status.Success, s.Open("/model.dat", out var file)); int operation = random.Next(4);
                if (operation == 0)
                { HostCheck.Equal(M.Status.Planned, s.Delete(file)); exists = false; expected = []; }
                else if (operation == 1)
                {
                    int length = random.Next(0, 1600); var next = new byte[length]; expected.AsSpan(0, Math.Min(length, expected.Length)).CopyTo(next); expected = next;
                    HostCheck.True(s.Truncate(file, (uint)length) is M.Status.Planned or M.Status.Success);
                }
                else
                {
                    int offset = random.Next(0, 1600); byte[] bytes = Payload(random.Next(1, 250), step);
                    Array.Resize(ref expected, Math.Max(expected.Length, offset + bytes.Length)); bytes.CopyTo(expected, offset);
                    HostCheck.Equal(M.Status.Planned, s.Write(file, (uint)offset, bytes));
                }
            }
            Run(f, ref s); if (exists) Check(f, "F0000001.DAT", expected);
            else HostCheck.Equal(M.Status.NotFound, s.Open("/model.dat", out _));
            Check(f, "KEEP.TXT", [4, 5, 6]); HostCheck.True(f.Snapshot.AsSpan().SequenceEqual(f.Durable));
        }
    }

    [HostTest("fat32 mutation stages keep dirty through both mirrors directory FSInfo and actual final flush")]
    private static void Ordering()
    {
        using var f = new Fixture(); var s = f.Session();
        HostCheck.Equal(M.Status.Planned, s.Create("/ordered", Alias(1), Payload(700)));
        int count = s.ChangeCount; var expected = new List<(uint Type, ulong Sector)>();
        for (int phase = 1; phase <= 6; phase++)
        {
            bool wrote = false;
            for (int i = 0; i < count; i++) if ((int)f.Changes[i].Phase == phase) { expected.Add((1, f.Changes[i].Sector)); wrote = true; }
            if (wrote) expected.Add((4, 0));
        }
        uint observed = 0;
        while (true)
        {
            var result = s.Pump();
            if (result == M.Status.Success) break;
            HostCheck.Equal(M.Status.Pending, result); HostCheck.True(s.Dirty);
            if (!f.H->Pending) continue;
            HostCheck.True(f.Snapshot[Data * 512 + 32] == 0); // Cache publication is final-flush-only.
            if (f.H->Notifies != observed)
            {
                observed = f.H->Notifies; HostCheck.Equal(expected[(int)observed - 1], (f.H->Type, f.H->Sector));
                f.Complete();
            }
        }
        HostCheck.Equal(expected.Count, f.Trace.Count); HostCheck.True(!s.Dirty && f.State->CompletedBytes == f.State->DurableBytes);
        HostCheck.Equal(uint.MaxValue, U32(f.Durable, (First + 1) * 512 + 488));
        HostCheck.Equal(uint.MaxValue, U32(f.Durable, (First + 7) * 512 + 492)); Check(f, "F0000001.DAT", Payload(700));
    }

    [HostTest("fat32 mutation deleted holes preserve later entries and stale end marker bytes stay hidden")]
    private static void Terminators()
    {
        using var f = new Fixture();
        f.Snapshot[Data * 512 + 32] = 0xe5; f.Snapshot[Data * 512 + 64] = 0xe5;
        var later = f.Snapshot.AsSpan(Data * 512 + 96, 32); "LATER   TXT"u8.CopyTo(later); later[11] = 0x20;
        // A valid-looking stale short entry after the real end is not live.
        var stale = f.Snapshot.AsSpan(Data * 512 + 9 * 32, 32); "STALE   TXT"u8.CopyTo(stale); stale[11] = 0x20;
        f.Snapshot.AsSpan().CopyTo(f.Volatile); f.Snapshot.AsSpan().CopyTo(f.Durable);
        var s = f.Session(); HostCheck.Equal(M.Status.Planned, s.Create("/x", Alias(1), [])); Run(f, ref s);
        HostCheck.True(Fat32Parser.FindRootDirectoryEntry(new Device(f.H->Durable), First, "LATER.TXT").Succeeded);
        HostCheck.Equal(M.Status.Success, s.Open("/x", out var file)); HostCheck.Equal(M.Status.Planned, s.Delete(file)); Run(f, ref s);
        HostCheck.True(Fat32Parser.FindRootDirectoryEntry(new Device(f.H->Durable), First, "LATER.TXT").Succeeded);
        HostCheck.Equal(M.Status.NotFound, s.Open("/STALE.TXT", out _));
        // Force insertion through the terminator, and leave a new terminator.
        HostCheck.Equal(M.Status.Planned, s.Create("/a filename requiring three long entries.dat", Alias(2), [])); Run(f, ref s);
        HostCheck.Equal(M.Status.NotFound, s.Open("/STALE.TXT", out _)); Check(f, "KEEP.TXT", [4, 5, 6]);
    }

    [HostTest("fat32 mutation nested path and cross-directory rename preserve original bytes and confinement")]
    private static void Nested()
    {
        using var f = new Fixture();
        foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors }) Put32(f.Snapshot, lba * 512 + 4 * 4, 0xfffffff);
        var sub = f.Snapshot.AsSpan(Data * 512 + 32, 32); "SUB        "u8.CopyTo(sub); sub[11] = 0x10; Put16(sub, 26, 4);
        var dot = f.Snapshot.AsSpan((Data + 2) * 512, 32); ".          "u8.CopyTo(dot); dot[11] = 0x10; Put16(dot, 26, 4);
        dot = f.Snapshot.AsSpan((Data + 2) * 512 + 32, 32); "..         "u8.CopyTo(dot); dot[11] = 0x10;
        f.Snapshot.AsSpan().CopyTo(f.Volatile); f.Snapshot.AsSpan().CopyTo(f.Durable);
        var s = f.Session(); byte[] bytes = Payload(900);
        HostCheck.Equal(M.Status.Planned, s.Create("/source", Alias(1), bytes)); Run(f, ref s);
        HostCheck.Equal(M.Status.Success, s.Open("/source", out var file));
        HostCheck.Equal(M.Status.Planned, s.Rename(file, "/SUB/new name.dat", Alias(2))); Run(f, ref s);
        HostCheck.Equal(M.Status.NotFound, s.Open("/source", out _));
        HostCheck.Equal(M.Status.Success, s.Open("/sub/new name.dat", out file));
        byte[] read = new byte[900]; var result = Fat32Parser.ReadFirstLevelFile(new Device(f.H->Durable), First, "SUB/F0000002.DAT", read);
        HostCheck.True(result.Succeeded && read.AsSpan().SequenceEqual(bytes));
        HostCheck.Equal(M.Status.InvalidName, s.Rename(file, "/SUB/../escape", Alias(3)));
        HostCheck.Equal(M.Status.Planned, s.Delete(file)); Run(f, ref s); Check(f, "KEEP.TXT", [4, 5, 6]);
    }

    [HostTest("fat32 mutation grow truncate rename delete every torn write and flush can reopen and rollback")]
    private static void MutationFailures()
    {
        for (int variant = 0; variant < 5; variant++)
        {
            int requests;
            using (var f = new Fixture())
            {
                var s = f.Session(); Setup(f, ref s); uint start = f.H->Notifies;
                HostCheck.Equal(M.Status.Planned, PlanVariant(ref s, variant)); Run(f, ref s); requests = (int)(f.H->Notifies - start);
            }
            for (int at = 1; at <= requests; at++)
            {
                using var f = new Fixture(); var s = f.Session(); Setup(f, ref s); byte[] original = f.Snapshot.ToArray();
                HostCheck.Equal(M.Status.Planned, PlanVariant(ref s, variant)); f.FailAt = (int)f.H->Notifies + at;
                HostCheck.Equal(M.Status.RecoveryRequired, Run(f, ref s, false));
                HostCheck.Equal(M.Status.Success, s.InspectJournal(out var receipt));
                f.Volatile.CopyTo(f.Durable); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.Restart();
                f.Durable.CopyTo(f.Snapshot);
                var reopened = f.Session(); HostCheck.Equal(M.Status.RecoveryRequired, reopened.ResumeRecovery(receipt));
                HostCheck.Equal(M.Status.Planned, reopened.Recover(f.State)); Run(f, ref reopened);
                HostCheck.True(original.AsSpan().SequenceEqual(f.Durable), "rollback variant=" + variant + " request=" + at);
                Check(f, "F0000001.DAT", Payload(900)); Check(f, "KEEP.TXT", [4, 5, 6]);
            }
        }
    }
    private static void Setup(Fixture f, ref M.Session s)
    { HostCheck.Equal(M.Status.Planned, s.Create("/source", Alias(1), Payload(900))); Run(f, ref s); }
    private static M.Status PlanVariant(ref M.Session s, int variant)
    {
        HostCheck.Equal(M.Status.Success, s.Open("/source", out var file));
        return variant switch { 0 => s.Write(file, 1800, Payload(700)), 1 => s.Truncate(file, 3), 2 => s.Rename(file, "/renamed source.dat", Alias(2)),
            3 => s.Delete(file), _ => s.Write(file, 0, Payload(300, 99)) };
    }

    [HostTest("fat32 mutation timeout requires old DMA retirement and failed rollback retains original journal")]
    private static void RecoveryRetry()
    {
        using var f = new Fixture(); var s = f.Session(); byte[] original = f.Snapshot.ToArray();
        HostCheck.Equal(M.Status.Planned, s.Create("/uncertain", Alias(1), Payload(600))); HostCheck.Equal(M.Status.Pending, s.Pump());
        f.H->Now = 1001; HostCheck.Equal(M.Status.RecoveryRequired, s.Pump()); HostCheck.Equal(Block.Status.Timeout, s.LastBlockFailure);
        HostCheck.Equal(M.Status.Success, s.InspectJournal(out var receipt));
        using var replacement = new Fixture(); HostCheck.Equal(M.Status.BlockFailure, s.Recover(replacement.State));
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); HostCheck.Equal(M.Status.Planned, s.Recover(replacement.State));
        replacement.FailAt = (int)replacement.H->Notifies + 1; HostCheck.Equal(M.Status.RecoveryRequired, Run(replacement, ref s, false));
        HostCheck.True(s.Dirty && s.InspectJournal(out _) == M.Status.Success);
        HostCheck.Equal(Block.Status.Success, Block.Stop(replacement.State)); replacement.Restart();
        HostCheck.Equal(M.Status.Planned, s.Recover(replacement.State)); Run(replacement, ref s);
        HostCheck.True(original.AsSpan().SequenceEqual(replacement.Durable) && !s.Dirty && receipt.Count > 0);
    }

    [HostTest("fat32 mutation journal scratch input and DMA aliases reject before changing active owner")]
    private static void Aliases()
    {
        using var f = new Fixture(); byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).ToArray();
        byte[] dma = new ReadOnlySpan<byte>(f.H->Data, 4096).ToArray();
        M.Session bad = default;
        HostCheck.Equal(M.Status.InvalidStorage, bad.Start(f.Snapshot, First, Sectors - 1, 1234, 4321, f.State,
            f.Changes.AsSpan(0, 1), f.Before.AsSpan(0, 512), new Span<byte>(f.H->Data, 512), f.Scratch));
        HostCheck.Equal(M.Status.InvalidStorage, bad.Start(f.Snapshot, First, Sectors - 1, 1234, 4321, f.State,
            new Span<M.Change>(f.State, 1), f.Before.AsSpan(0, 512), f.After.AsSpan(0, 512), f.Scratch));
        HostCheck.Equal(M.Status.InvalidStorage, bad.Start(f.Snapshot, First, Sectors - 1, 1234, 4321, (Block.State*)8,
            f.Changes.AsSpan(0, 1), f.Before.AsSpan(0, 512), f.After.AsSpan(0, 512), f.Scratch));
        var s = f.Session();
        HostCheck.Equal(M.Status.InvalidStorage, s.Create("/dma", Alias(1), new ReadOnlySpan<byte>(f.H->Data + 512, 1)));
        HostCheck.Equal(M.Status.InvalidStorage, s.Create("/scratch", Alias(1), MemoryMarshal.AsBytes(f.Scratch.AsSpan())[..1]));
        Alias(1).CopyTo(f.After, 0);
        HostCheck.Equal(M.Status.InvalidStorage, s.Create("/alias", f.After.AsSpan(0, 11), []));
        HostCheck.Equal(M.Status.InvalidStorage, s.Create(MemoryMarshal.Cast<byte, char>(f.After.AsSpan(0, 4)), Alias(1), []));
        HostCheck.True(state.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.State, sizeof(Block.State))) &&
            dma.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.H->Data, 4096)) && f.H->Notifies == 0);
    }

    [HostTest("fat32 mutation repeated bounded planning uses caller buffers without managed allocation")]
    private static void Allocation()
    {
        using var f = new Fixture(); var s = f.Session(); byte[] alias = Alias(1), data = Payload(600);
        HostCheck.Equal(M.Status.Planned, s.Create("/allocation", alias, data)); HostCheck.Equal(M.Status.Cancelled, s.Cancel());
        long before = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 64; i++) good &= s.Create("/allocation", alias, data) == M.Status.Planned && s.Cancel() == M.Status.Cancelled;
        HostCheck.True(good && GC.GetAllocatedBytesForCurrentThread() == before && !s.Dirty && f.H->Notifies == 0);
    }

    [HostTest("fat32 mutation cycles malformed VFAT readonly files and allocation budgets publish nothing")]
    private static void Corruption()
    {
        using var f = new Fixture(); var s = f.Session();
        foreach (uint next in new uint[] { 2, 0, 0x0ffffff7, 0x0ffffff0 })
        {
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors }) Put32(f.Snapshot, lba * 512 + 8, next);
            HostCheck.Equal(M.Status.CorruptChain, s.Create("/file", Alias(1), []));
        }
        foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors }) Put32(f.Snapshot, lba * 512 + 8, 0xfffffff);
        var broken = f.Snapshot.AsSpan(Data * 512 + 32, 32); broken[0] = 0x41; broken[11] = 15;
        HostCheck.Equal(M.Status.CorruptDirectory, s.Create("/file", Alias(1), [])); broken.Clear();
        f.Snapshot[Data * 512 + 11] |= 1;
        HostCheck.Equal(M.Status.Success, s.Open("/KEEP.TXT", out var keep));
        HostCheck.Equal(M.Status.ReadOnly, s.Truncate(keep, 0)); HostCheck.Equal(M.Status.ReadOnly, s.Delete(keep));
        var bounded = f.Session(128, 3); HostCheck.Equal(M.Status.TraversalBudgetExceeded, bounded.Create("/file", Alias(1), Payload(600)));
        HostCheck.True(!s.Dirty && !bounded.Dirty && f.H->Notifies == 0);
    }

    [HostTest("fat32 mutation shared live cluster chains and dangling free-cluster references reject before freeing or allocation")]
    private static void CrossLinks()
    {
        using var f = new Fixture(); var s = f.Session();
        var duplicate = f.Snapshot.AsSpan(Data * 512 + 32, 32); "ALIAS   TXT"u8.CopyTo(duplicate); duplicate[11] = 0x20;
        Put16(duplicate, 26, 3); Put32(duplicate, 28, 3);
        HostCheck.Equal(M.Status.Success, s.Open("/KEEP.TXT", out var keep));
        HostCheck.Equal(M.Status.CorruptChain, s.Delete(keep)); HostCheck.Equal(M.Status.CorruptChain, s.Truncate(keep, 0));
        HostCheck.Equal(M.Status.CorruptChain, s.Write(keep, 0, [9])); HostCheck.Equal(M.Status.CorruptChain, s.Rename(keep, "/elsewhere", Alias(1)));
        // The same live entry points into FAT-free space. Allocation must not
        // treat that malformed dangling reference as a safe new data cluster.
        Put16(duplicate, 26, 4);
        HostCheck.Equal(M.Status.CorruptChain, s.Create("/new", Alias(1), [1]));
        HostCheck.True(!s.Dirty && f.H->Notifies == 0);
    }

    [HostTest("fat32 mutation FAT exhaustion is distinct from bounded free-cluster scan exhaustion")]
    private static void DiskFull()
    {
        using var f = new Fixture();
        foreach (int fat in new[] { First + Reserved, First + Reserved + FatSectors })
            for (int cluster = 4; cluster < Clusters + 2; cluster++) Put32(f.Snapshot, fat * 512 + cluster * 4, 0xfffffff);
        var limited = f.Session(128, 100); HostCheck.Equal(M.Status.TraversalBudgetExceeded, limited.Create("/full", Alias(1), [1]));
        var s = f.Session(); HostCheck.Equal(M.Status.DiskFull, s.Create("/full", Alias(1), [1]));
        HostCheck.True(!s.Dirty && f.H->Notifies == 0);
    }

    private static M.Status Run(Fixture f, ref M.Session session, bool expectSuccess = true)
    {
        for (int turn = 0; turn < 20000; turn++)
        {
            M.Status status = session.Pump();
            if (status != M.Status.Pending)
            { if (expectSuccess) HostCheck.Equal(M.Status.Success, status); return status; }
            if (f.H->Pending) f.Complete();
        }
        throw new InvalidOperationException("bounded host mutation did not terminate");
    }
    private static void Check(Fixture f, string alias, ReadOnlySpan<byte> expected)
    {
        var device = new Device(f.H->Durable); var geometry = Fat32Parser.ReadBpb(device, First); HostCheck.True(geometry.Succeeded);
        byte[] read = new byte[expected.Length]; var result = Fat32Parser.ReadRootFile(device, First, alias, read);
        HostCheck.True(result.Succeeded && result.FileSize == expected.Length && read.AsSpan().SequenceEqual(expected), "independent Fat32Parser " + alias +
            " status=" + result.Status + " length=" + result.FileSize + " first=" + result.Entry.FirstCluster);
        HostCheck.True(f.Durable.Slice((First + Reserved) * 512, FatSectors * 512).SequenceEqual(f.Durable.Slice((First + Reserved + FatSectors) * 512, FatSectors * 512)));
    }
    private sealed class Device(byte* bytes) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, Sectors).Info;
        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> destination)
        {
            if (first >= Sectors || count > Sectors - first || destination.Length != count * 512) return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            new ReadOnlySpan<byte>(bytes + first * 512, checked((int)count * 512)).CopyTo(destination); return BlockReadResult.Success(count, count * 512UL);
        }
    }

    private const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000, Physical = 0x220000, Dma = 0x123456780000;
    private struct Hardware
    {
        internal byte* Data, Volatile, Durable;
        internal ulong Descriptor, Available, Used, Chosen, Sector, Now;
        internal uint FeatureSelect, DriverSelect, Type, Produced, Notifies;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector;
        internal byte Status;
        internal bool Pending;
    }
    [UnmanagedCallersOnly] private static byte R8(void* c, ulong p, byte* v)
    { var h = (Hardware*)c; if (p == Common + 20) *v = h->Status; else if (p == Common + 21) *v = 7; else if (p == (ulong)h->Data + 272) *v = h->Data[272]; else return 0; return 1; }
    [UnmanagedCallersOnly] private static byte W8(void* c, ulong p, byte v)
    { if (p != Common + 20) return 0; var h = (Hardware*)c; h->Status = v; if (v == 0) { h->Enabled = 0; h->Pending = false; } return 1; }
    [UnmanagedCallersOnly] private static byte R16(void* c, ulong p, ushort* v)
    {
        var h = (Hardware*)c; if (p == (ulong)h->Data + 150) { *v = *(ushort*)p; return 1; }
        switch (p - Common) { case 16: *v = h->ConfigVector; break; case 18: *v = 1; break; case 22: *v = h->Selected; break;
            case 24: *v = h->Size == 0 ? (ushort)8 : h->Size; break; case 26: *v = h->QueueVector; break; case 28: *v = h->Enabled; break; case 30: *v = 0; break; default: return 0; } return 1;
    }
    [UnmanagedCallersOnly] private static byte W16(void* c, ulong p, uint v)
    {
        var h = (Hardware*)c;
        if (p == Notify) { h->Type = *(uint*)(h->Data + 256); h->Sector = *(ulong*)(h->Data + 264); h->Pending = true; h->Notifies++; return 1; }
        switch (p - Common) { case 16: h->ConfigVector = (ushort)v; break; case 22: h->Selected = (ushort)v; break; case 24: h->Size = (ushort)v; break;
            case 26: h->QueueVector = (ushort)v; break; case 28: h->Enabled = (ushort)v; break; default: return 0; } return 1;
    }
    [UnmanagedCallersOnly] private static byte R32(void* c, ulong p, uint* v)
    {
        var h = (Hardware*)c;
        if (p == Common + 4) { *v = (uint)(Block.Features >> (int)(h->FeatureSelect * 32)); return 1; }
        if (p == Config) { *v = Sectors; return 1; } if (p == Config + 4) { *v = 0; return 1; }
        if (p >= (ulong)h->Data + 152 && p < (ulong)h->Data + 216) { *v = *(uint*)p; return 1; }
        ulong offset = p - Common; if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        ulong value = offset < 40 ? h->Descriptor : offset < 48 ? h->Available : h->Used; *v = (uint)(value >> ((offset & 4) == 0 ? 0 : 32)); return 1;
    }
    [UnmanagedCallersOnly] private static byte W32(void* c, ulong p, uint v)
    {
        var h = (Hardware*)c; ulong offset = p - Common;
        if (offset == 0) { h->FeatureSelect = v; return 1; } if (offset == 8) { h->DriverSelect = v; return 1; }
        if (offset == 12) { int bit = (int)(h->DriverSelect * 32); h->Chosen = (h->Chosen & ~(0xffffffffUL << bit)) | ((ulong)v << bit); return 1; }
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        ulong* target = offset < 40 ? &h->Descriptor : offset < 48 ? &h->Available : &h->Used; int shift = (offset & 4) == 0 ? 0 : 32;
        *target = (*target & ~(0xffffffffUL << shift)) | ((ulong)v << shift); return 1;
    }
    [UnmanagedCallersOnly] private static byte Yes(void* c) => 1;
    [UnmanagedCallersOnly] private static byte Clock(void* c, ulong* value) { *value = ((Hardware*)c)->Now; return 1; }

    private sealed class Fixture : IDisposable
    {
        internal readonly byte[] Snapshot = new byte[Sectors * 512];
        internal readonly M.Change[] Changes = new M.Change[128];
        internal readonly byte[] Before = new byte[128 * 512], After = new byte[128 * 512];
        internal readonly uint[] Scratch = new uint[M.ScratchClusters];
        internal readonly Hardware* H = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private readonly byte* dma = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        private readonly DmaBufferPool* pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        private readonly List<nint> states = [];
        internal Block.State* State;
        internal readonly List<(uint Type, ulong Sector)> Trace = [];
        internal int FailAt;
        internal Span<byte> Volatile => new(H->Volatile, Snapshot.Length);
        internal Span<byte> Durable => new(H->Durable, Snapshot.Length);
        private VirtioMappedRegion common, notify, config;
        internal Fixture()
        {
            H->Data = dma; H->Now = 1; H->Volatile = (byte*)NativeMemory.AllocZeroed((nuint)Snapshot.Length); H->Durable = (byte*)NativeMemory.AllocZeroed((nuint)Snapshot.Length);
            Span<byte> boot = Snapshot.AsSpan(First * 512, 512); Put16(boot, 11, 512); boot[13] = 1; Put16(boot, 14, Reserved); boot[16] = 2;
            Put32(boot, 32, Sectors - First); Put32(boot, 36, FatSectors); Put32(boot, 44, 2); Put16(boot, 48, 1); Put16(boot, 50, 6); boot[510] = 0x55; boot[511] = 0xaa;
            boot.CopyTo(Snapshot.AsSpan((First + 6) * 512, 512));
            foreach (int lba in new[] { First + 1, First + 7 })
            { var fs = Snapshot.AsSpan(lba * 512, 512); Put32(fs, 0, 0x41615252); Put32(fs, 484, 0x61417272); Put32(fs, 488, Clusters - 2); Put32(fs, 492, 4); Put32(fs, 508, 0xaa550000); }
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors })
            { var fat = Snapshot.AsSpan(lba * 512); Put32(fat, 0, 0xffffff8); Put32(fat, 4, 0xfffffff); Put32(fat, 8, 0xfffffff); Put32(fat, 12, 0xfffffff); }
            var keep = Snapshot.AsSpan(Data * 512, 32); "KEEP    TXT"u8.CopyTo(keep); keep[11] = 0x20; Put16(keep, 26, 3); Put32(keep, 28, 3);
            new byte[] { 4, 5, 6 }.CopyTo(Snapshot, (Data + 1) * 512);
            Snapshot.CopyTo(Volatile); Snapshot.CopyTo(Durable);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)dma, Physical, Dma, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(pool, window, slot, 1, 75));
            PciSizedBar* bars = stackalloc PciSizedBar[6]; new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0), new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out config)); Restart();
        }
        internal void Restart()
        {
            FailAt = 0; H->Produced = 0;
            State = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State)); states.Add((nint)State);
            var queue = new VirtioBlkQueueAccess { Context = H, Read8 = &R8, Write8 = &W8, Read16 = &R16, Write16 = &W16, Read32 = &R32, Write32 = &W32, Fence = &Yes, BusMasterOff = &Yes };
            var negotiation = Block.Negotiate(common, 3, queue); HostCheck.True(negotiation.Succeeded);
            HostCheck.Equal(Block.Status.Success, Block.Start(State, new() { Common = common, Notify = notify, Device = config, NotifyMultiplier = 4, ResetLimit = 3,
                Pool = pool, Epoch = (ulong)states.Count, ExpectedCapacity = Sectors, CompletionTimeoutNanos = 1000, Negotiation = negotiation,
                Access = new() { Queue = queue, BusMasterOn = &Yes }, Clock = new() { Context = H, Read = &Clock } }));
        }
        internal M.Session Session(int capacity = 128, uint budget = 100000)
        {
            M.Session value = default; HostCheck.Equal(M.Status.Success, value.Start(Snapshot, First, Sectors - 1, 1234, 4321, State,
                Changes.AsSpan(0, capacity), Before.AsSpan(0, capacity * 512), After.AsSpan(0, capacity * 512), Scratch, budget)); return value;
        }
        internal void Complete()
        {
            HostCheck.True(H->Pending && H->Sector < Sectors); bool fail = H->Notifies == FailAt;
            Trace.Add((H->Type, H->Sector));
            if (H->Type == 1) new ReadOnlySpan<byte>(dma + 512, fail ? 257 : 512).CopyTo(Volatile.Slice((int)H->Sector * 512));
            else { HostCheck.Equal(4U, H->Type); if (!fail) Volatile.CopyTo(Durable); }
            dma[272] = fail ? (byte)1 : (byte)0; byte* item = dma + 152 + (H->Produced & 7) * 8; *(uint*)item = 0; *(uint*)(item + 4) = 1;
            H->Produced++; *(ushort*)(dma + 150) = unchecked((ushort)H->Produced); H->Pending = false;
        }
        public void Dispose()
        {
            if (State != null && !State->Retired) HostCheck.Equal(Block.Status.Success, Block.Stop(State));
            foreach (nint state in states) NativeMemory.Free((void*)state);
            NativeMemory.Free(H->Volatile); NativeMemory.Free(H->Durable); NativeMemory.Free(H); NativeMemory.Free(pool); NativeMemory.Free(slot); NativeMemory.AlignedFree(dma);
        }
    }
}
