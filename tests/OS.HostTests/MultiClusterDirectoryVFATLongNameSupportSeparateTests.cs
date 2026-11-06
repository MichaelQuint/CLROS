using System.Buffers.Binary;
using System.Text;
using OS.Storage;
using DirectoryParser = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

internal static unsafe class MultiClusterDirectoryVFATLongNameSupportSeparateTests
{
    [HostTest("fat.directory streams independent VFAT entries across sector and cluster boundaries")]
    private static void CrossBoundary()
    {
        const string expected = "CLROS Live Storage Fixture.bin";
        byte[] shortEntry = ShortEntry();
        var entries = Encode(expected, shortEntry);
        HostCheck.Equal(3, entries.Count);
        byte[] disk = new byte[1024];
        for (int i = 0; i < 15; i++)
            disk[i * 32] = 0xe5;
        for (int i = 0; i < 3; i++)
            entries[i].CopyTo(disk, 480 + i * 32);
        shortEntry.CopyTo(disk, 576);
        ushort[] scratch = new ushort[260];
        LiveVfatState state = default;
        HostCheck.True(!default(LiveDirectoryEntry).Succeeded);
        int found = 0;
        for (int sector = 0; sector < 2; sector++)
            for (int offset = 0; offset < 512; offset += 32)
            {
                var status = DirectoryParser.Consume(disk.AsSpan(sector * 512 + offset, 32), ref state, scratch, out var result);
                if (status == LiveDirectoryStatus.End)
                    break;
                if (status != LiveDirectoryStatus.Entry)
                {
                    HostCheck.True(!result.Succeeded);
                    continue;
                }
                found++;
                HostCheck.True(result.Succeeded && result.HasLongName && result.Attributes == 0x20);
                HostCheck.Equal(expected.Length, (int)result.NameLength);
                HostCheck.Equal(expected, new string(scratch.Take(result.NameLength).Select(v => (char)v).ToArray()));
                HostCheck.Equal(44u, result.FirstCluster);
                HostCheck.Equal(1301u, result.ByteCount);
                HostCheck.Equal(0x317e45524946534cul, result.ShortNameLow);
                HostCheck.Equal(0x004e4942u, result.ShortNameHigh);
            }
        HostCheck.Equal(1, found);
        HostCheck.Equal(LiveDirectoryStatus.End, DirectoryParser.Finish(state));
    }

    [HostTest("fat.directory admits exact slot boundaries maximum length and valid UTF16 pairs")]
    private static void NameBoundaries()
    {
        foreach (string name in new[] { "x", new string('a', 13), new string('b', 26), new string('c', 255), "rocket-\U0001f680.bin" })
        {
            ushort[] scratch = new ushort[260];
            LiveVfatState state = default;
            byte[] shortEntry = ShortEntry();
            foreach (byte[] entry in Encode(name, shortEntry))
                HostCheck.Equal(LiveDirectoryStatus.LongName, DirectoryParser.Consume(entry, ref state, scratch, out _));
            HostCheck.Equal(LiveDirectoryStatus.Entry, DirectoryParser.Consume(shortEntry, ref state, scratch, out var result));
            HostCheck.Equal(name.Length, (int)result.NameLength);
            HostCheck.True(name.Select(c => (ushort)c).SequenceEqual(scratch.Take(result.NameLength)));
            HostCheck.Equal(LiveDirectoryStatus.End, DirectoryParser.Finish(state));
        }
    }

    [HostTest("fat.directory rejects VFAT ordering metadata checksum interruptions and dangling entries")]
    private static void SequenceFailures()
    {
        var entries = Encode("CLROS Live Storage Fixture.bin", ShortEntry());
        foreach (Action<byte[]> mutate in new Action<byte[]>[]
        {
            e => e[0] = 0x40, e => e[0] = 0x55, e => e[0] |= 0x80,
            e => e[0] |= 0x20, e => e[12] = 1, e => e[26] = 1, e => e[27] = 1
        })
        {
            var first = entries[0].ToArray();
            mutate(first);
            LiveVfatState state = default;
            HostCheck.Equal(LiveDirectoryStatus.InvalidSequence, DirectoryParser.Consume(first, ref state, new ushort[260], out _));
            HostCheck.Equal(LiveDirectoryStatus.End, DirectoryParser.Finish(state));
        }
        foreach (byte[] interruption in new[] { entries[0], entries[2], ShortEntry(), new byte[32], Deleted(), Volume() })
        {
            LiveVfatState state = default;
            ushort[] scratch = new ushort[260];
            HostCheck.Equal(LiveDirectoryStatus.LongName, DirectoryParser.Consume(entries[0], ref state, scratch, out _));
            HostCheck.Equal(LiveDirectoryStatus.InvalidSequence, DirectoryParser.Finish(state));
            HostCheck.Equal(LiveDirectoryStatus.InvalidSequence, DirectoryParser.Consume(interruption, ref state, scratch, out var result));
            HostCheck.True(!result.Succeeded);
            HostCheck.Equal(LiveDirectoryStatus.End, DirectoryParser.Finish(state));
        }
        LiveVfatState s = default;
        ushort[] words = new ushort[260];
        DirectoryParser.Consume(entries[0], ref s, words, out _);
        var wrongChecksum = entries[1].ToArray();
        wrongChecksum[13] ^= 1;
        HostCheck.Equal(LiveDirectoryStatus.InvalidSequence, DirectoryParser.Consume(wrongChecksum, ref s, words, out _));
        foreach (byte[] e in entries)
            DirectoryParser.Consume(e, ref s, words, out _);
        var wrongAlias = ShortEntry();
        wrongAlias[0] = (byte)'A';
        HostCheck.Equal(LiveDirectoryStatus.InvalidChecksum, DirectoryParser.Consume(wrongAlias, ref s, words, out _));
    }

    [HostTest("fat.directory rejects malformed names path aliases UTF16 and padding")]
    private static void InvalidNames()
    {
        foreach (string name in new[] { ".", "..", "a/../b", "a\\b", "a:b", "a?b", "a*b", "a|b", "a<b", "a>b", "a\"b",
            "a\u001fb", "trailing.", "trailing ", "a\ud800", "a\udc00", "a\ud800x", new string('x', 256), "a\uffffb" })
            HostCheck.Equal(LiveDirectoryStatus.InvalidName, ParseName(name));
        foreach (Action<byte[][]> mutate in new Action<byte[][]>[]
        {
            a => BinaryPrimitives.WriteUInt16LittleEndian(a[0].AsSpan(1), 0),
            a => BinaryPrimitives.WriteUInt16LittleEndian(a[0].AsSpan(5), (ushort)'x'),
            a => BinaryPrimitives.WriteUInt16LittleEndian(a[0].AsSpan(3), 0xffff)
        })
        {
            byte[] alias = ShortEntry();
            var entries = Encode("x", alias).ToArray();
            mutate(entries);
            LiveVfatState state = default;
            ushort[] scratch = new ushort[260];
            foreach (byte[] e in entries)
                DirectoryParser.Consume(e, ref state, scratch, out _);
            HostCheck.Equal(LiveDirectoryStatus.InvalidName, DirectoryParser.Consume(alias, ref state, scratch, out _));
        }
        // An unnecessary extra slot may not bind stale caller scratch.
        byte[] shortEntry = ShortEntry();
        var extra = Encode(new string('x', 14), shortEntry).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(extra[0].AsSpan(1), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(extra[0].AsSpan(3), 0xffff);
        LiveVfatState st = default;
        ushort[] names = new ushort[260];
        foreach (byte[] e in extra)
            DirectoryParser.Consume(e, ref st, names, out _);
        HostCheck.Equal(LiveDirectoryStatus.InvalidName, DirectoryParser.Consume(shortEntry, ref st, names, out _));
    }

    [HostTest("fat.directory handles short names directories deletion and invalid metadata")]
    private static void ShortEntries()
    {
        ushort[] scratch = new ushort[260];
        LiveVfatState state = default;
        HostCheck.Equal(LiveDirectoryStatus.Entry, DirectoryParser.Consume(ShortEntry(), ref state, scratch, out var shortResult));
        HostCheck.True(!shortResult.HasLongName);
        foreach (byte[] e in new[] { Deleted(), Volume(), Dot(false), Dot(true) })
            HostCheck.Equal(LiveDirectoryStatus.Skipped, DirectoryParser.Consume(e, ref state, scratch, out _));
        var badDot = Dot(false);
        badDot[26] = 0;
        HostCheck.Equal(LiveDirectoryStatus.InvalidEntry, DirectoryParser.Consume(badDot, ref state, scratch, out _));
        HostCheck.Equal(LiveDirectoryStatus.End, DirectoryParser.Consume(new byte[32], ref state, scratch, out _));
        foreach (Action<byte[]> mutate in new Action<byte[]>[]
        {
            e => e[0] = (byte)' ', e => e[2] = (byte)' ', e => e[0] = (byte)'/', e => e[0] = 1,
            e => e[11] = 0x40, e => e[11] = 0x18, e => e[11] = 0x10,
            e => BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(20), 0x1000),
            e => BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(26), 1),
            e => BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(26), 0)
        })
        {
            byte[] e = ShortEntry();
            mutate(e);
            HostCheck.Equal(LiveDirectoryStatus.InvalidEntry, DirectoryParser.Consume(e, ref state, scratch, out _));
        }
        byte[] empty = ShortEntry();
        empty.AsSpan(26, 6).Clear();
        HostCheck.Equal(LiveDirectoryStatus.Entry, DirectoryParser.Consume(empty, ref state, scratch, out _));
        empty[11] = 0x10;
        empty[26] = 2;
        HostCheck.Equal(LiveDirectoryStatus.Entry, DirectoryParser.Consume(empty, ref state, scratch, out _));
        empty[0] = 5;
        HostCheck.Equal(LiveDirectoryStatus.Entry, DirectoryParser.Consume(empty, ref state, scratch, out _));
    }

    [HostTest("fat.directory rejects bad lengths pointers overlap and mutable state")]
    private static void Admission()
    {
        LiveVfatState s = default;
        ushort[] words = new ushort[260];
        foreach (int length in new[] { 0, 31, 33 })
            HostCheck.Equal(LiveDirectoryStatus.InvalidLength, DirectoryParser.Consume(new byte[length], ref s, words, out _));
        foreach (int length in new[] { 0, 259, 261 })
            HostCheck.Equal(LiveDirectoryStatus.InvalidLength, DirectoryParser.Consume(ShortEntry(), ref s, new ushort[length], out _));
        HostCheck.Equal(LiveDirectoryStatus.InvalidLength, DirectoryParser.Consume(new Span<byte>(null, 32), ref s, words, out _));
        HostCheck.Equal(LiveDirectoryStatus.InvalidLength, DirectoryParser.Consume(ShortEntry(), ref s, new Span<ushort>(null, 260), out _));
        byte* shared = stackalloc byte[520];
        HostCheck.Equal(LiveDirectoryStatus.InvalidLength, DirectoryParser.Consume(new Span<byte>(shared, 32), ref s, new Span<ushort>(shared, 260), out _));
        foreach (var bad in new[] { new LiveVfatState { Slots = 21 }, new LiveVfatState { Remaining = 1 },
            new LiveVfatState { Checksum = 1 }, new LiveVfatState { Slots = 2, Remaining = 2 } })
        {
            s = bad;
            HostCheck.Equal(LiveDirectoryStatus.InvalidState, DirectoryParser.Consume(ShortEntry(), ref s, words, out _));
        }
    }

    private static LiveDirectoryStatus ParseName(string name)
    {
        byte[] alias = ShortEntry();
        LiveVfatState state = default;
        ushort[] scratch = new ushort[260];
        foreach (byte[] entry in Encode(name, alias))
            DirectoryParser.Consume(entry, ref state, scratch, out _);
        return DirectoryParser.Consume(alias, ref state, scratch, out _);
    }

    internal static byte[] ShortEntry()
    {
        byte[] bytes = new byte[32];
        Encoding.ASCII.GetBytes("LSFIRE~1BIN").CopyTo(bytes, 0);
        bytes[11] = 0x20;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 44);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 1301);
        return bytes;
    }

    // Independent host encoder; intentionally includes no production-parser calls.
    internal static List<byte[]> Encode(string name, byte[] alias)
    {
        byte sum = 0;
        for (int i = 0; i < 11; i++)
            sum = unchecked((byte)((sum >> 1) + ((sum & 1) == 0 ? 0 : 128) + alias[i]));
        int slots = (name.Length + 12) / 13;
        List<byte[]> entries = [];
        int[] offsets = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
        for (int ordinal = slots; ordinal >= 1; ordinal--)
        {
            byte[] entry = new byte[32];
            entry[0] = (byte)(ordinal | (ordinal == slots ? 0x40 : 0));
            entry[11] = 0x0f;
            entry[13] = sum;
            for (int i = 0; i < 13; i++)
            {
                int index = (ordinal - 1) * 13 + i;
                ushort value = index < name.Length ? name[index] : index == name.Length ? (ushort)0 : (ushort)0xffff;
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(offsets[i]), value);
            }
            entries.Add(entry);
        }
        return entries;
    }

    private static byte[] Deleted()
    {
        byte[] b = new byte[32];
        b[0] = 0xe5;
        return b;
    }
    private static byte[] Volume()
    {
        byte[] b = ShortEntry();
        b[11] = 8;
        return b;
    }
    private static byte[] Dot(bool parent)
    {
        byte[] b = new byte[32];
        b.AsSpan(0, 11).Fill((byte)' ');
        b[0] = (byte)'.';
        if (parent)
            b[1] = (byte)'.';
        else
            b[26] = 2;
        b[11] = 0x10;
        return b;
    }
}
