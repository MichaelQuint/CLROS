using System.Buffers.Binary;
using OS.Std.Hash;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM06GptPartitionRecords(IReadOnlyList<string> lines, bool primaryHeaderFault = false)
    {
        var expected = QemuContracts.M06GptPartitionMarkers(primaryHeaderFault);
        Need(lines.Count == expected.Count, "m06_gpt_record_count");
        for (int i = QemuContracts.M06LiveBlockContractMarkers.Count; i < lines.Count; i++)
            Need(expected[i].StartsWith("M06_GPT ", StringComparison.Ordinal) ?
                lines[i].StartsWith(expected[i] + " ", StringComparison.Ordinal) : lines[i] == expected[i], "m06_gpt_marker_order");
        ulong[] Row(int stage) => GptRow(lines, stage);
        ulong capacity = ReadHexRecord(lines.Single(s => s.StartsWith("M06_BDEV 0 ", StringComparison.Ordinal)), "M06_BDEV 0", 4)[0];
        ulong[] primary = Row(0), array = Row(1), backup = Row(2), choice = Row(3), table = Row(4), partition = Row(5), final = Row(6);
        ulong reads = primaryHeaderFault ? 34UL : 33;
        Need(capacity >= 68 && capacity <= ulong.MaxValue / 512, "m06_gpt_capacity");
        Need(primary[0] == (primaryHeaderFault ? 14UL : 1) && primary.Skip(1).All(v => v <= uint.MaxValue) &&
            primary[1] == (primaryHeaderFault ? primary[2] ^ 1UL : primary[2]), "m06_gpt_primary_header");
        Need(array[0] == (primaryHeaderFault ? 0UL : 1) && array[1] == (primaryHeaderFault ? 0UL : primary[3]) &&
            array[2] >= 2 && array[2] <= capacity - 34 && array[3] == (primaryHeaderFault ? 1UL : 33), "m06_gpt_primary_array");
        Need(primaryHeaderFault ? backup[0] == 1 && backup[1] == backup[2] && backup.Skip(1).All(v => v <= uint.MaxValue) :
            backup.All(v => v == 0), "m06_gpt_backup_header");
        Need(choice.SequenceEqual(new ulong[] { primaryHeaderFault ? 1UL : 0, primaryHeaderFault ? 14UL : 1, primaryHeaderFault ? 1UL : 0, reads }), "m06_gpt_selection");
        Need(table[0] == (primaryHeaderFault ? capacity - 1 : 1) && table[2] >= 34 && table[2] <= table[3] && table[3] <= capacity - 34 &&
            (primaryHeaderFault ? table[1] > table[3] && table[1] <= capacity - 33 : table[1] == array[2] && table[1] <= table[2] - 32), "m06_gpt_selected_bounds");
        Need(partition[0] < 128 && partition[1] >= table[2] && partition[1] <= partition[2] && partition[2] <= table[3] &&
            partition[3] == partition[2] - partition[1] + 1, "m06_gpt_partition_extent");
        Need(final[0] == partition[1] * 512 && final[2] == reads && final[3] == 1, "m06_gpt_final_cleanup");
        for (int i = 0; i < (int)reads; i++)
        {
            ulong sector = i == 0 ? 1UL : primaryHeaderFault && i == 1 ? capacity - 1 : table[1] + (ulong)(i - (primaryHeaderFault ? 2 : 1));
            ulong[] read = Row(0x80 + i);
            Need(read[0] == sector && read[1] <= uint.MaxValue && read[2] == (ulong)i + 4 && read[3] == 1, "m06_gpt_read_lifetime");
        }
    }

    public static void AssertM06GptPartitionImage(string serialPath, string imagePath, bool primaryHeaderFault = false)
    {
        string[] lines = File.ReadAllLines(serialPath);
        ulong[] Row(int stage) => GptRow(lines, stage);
        using var disk = File.OpenRead(imagePath);
        ulong capacity = ReadHexRecord(lines.Single(s => s.StartsWith("M06_BDEV 0 ", StringComparison.Ordinal)), "M06_BDEV 0", 4)[0];
        Need(disk.Length >= 68 * 512 && disk.Length % 512 == 0 && (ulong)disk.Length / 512 == capacity, "m06_gpt_disk_geometry");
        byte[] Read(ulong lba, int bytes)
        {
            Need(bytes > 0 && lba < capacity && (ulong)bytes <= (capacity - lba) * 512, "m06_gpt_disk_range");
            byte[] data = new byte[bytes];
            disk.Position = checked((long)lba * 512);
            disk.ReadExactly(data);
            return data;
        }
        uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset));
        ulong U64(byte[] b, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(offset));
        uint HeaderCrc(byte[] header)
        {
            uint size = U32(header, 12);
            Need(size is >= 92 and <= 512, "m06_gpt_disk_header_size");
            byte[] copy = (byte[])header.Clone();
            copy.AsSpan(16, 4).Clear();
            return Crc32.Compute(copy.AsSpan(0, (int)size));
        }
        byte[] primary = Read(1, 512);
        uint computed = HeaderCrc(primary);
        Need(Row(0).SequenceEqual(new ulong[] { primaryHeaderFault ? 14UL : 1, U32(primary, 16), computed, U32(primary, 88) }) &&
            U32(primary, 16) == (primaryHeaderFault ? computed ^ 1U : computed) && Row(1)[2] == U64(primary, 72), "m06_gpt_disk_primary");
        byte[] selected = primaryHeaderFault ? Read(capacity - 1, 512) : primary;
        ulong current = primaryHeaderFault ? capacity - 1 : 1;
        Need(U64(selected, 0) == 0x5452415020494645UL && U32(selected, 8) == 0x10000 && U32(selected, 20) == 0 &&
            U64(selected, 24) == current && U64(selected, 32) == (primaryHeaderFault ? 1UL : capacity - 1) &&
            (U64(selected, 56) | U64(selected, 64)) != 0 && U32(selected, 80) == 128 && U32(selected, 84) == 128 &&
            U32(selected, 16) == HeaderCrc(selected), "m06_gpt_disk_selected_header");
        ulong entryLba = U64(selected, 72);
        byte[] entries = Read(entryLba, 16384);
        uint arrayCrc = Crc32.Compute(entries);
        Need(arrayCrc == U32(selected, 88) && Row(4).SequenceEqual(new ulong[] { current, entryLba, U64(selected, 40), U64(selected, 48) }), "m06_gpt_disk_array");
        if (primaryHeaderFault)
            Need(Row(2).SequenceEqual(new ulong[] { 1, U32(selected, 16), HeaderCrc(selected), arrayCrc }), "m06_gpt_disk_backup");
        else
            Need(Row(1)[1] == arrayCrc, "m06_gpt_disk_primary_array");
        int found = -1;
        for (int i = 0; i < 128; i++)
            if (new Guid(entries.AsSpan(i * 128, 16)) == new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b"))
            {
                Need(found == -1, "m06_gpt_disk_ambiguous_partition");
                found = i;
            }
        Need(found >= 0, "m06_gpt_disk_missing_partition");
        int offset = found * 128;
        ulong first = U64(entries, offset + 32), last = U64(entries, offset + 40);
        Need(first <= last && first >= U64(selected, 40) && last <= U64(selected, 48) &&
            Row(5).SequenceEqual(new ulong[] { (ulong)found, first, last, last - first + 1 }) &&
            Row(6)[0] == first * 512 && Row(6)[1] == U64(entries, offset + 48), "m06_gpt_disk_partition");
        for (int i = 0; i < (primaryHeaderFault ? 34 : 33); i++)
        {
            ulong[] read = Row(0x80 + i);
            Need(Crc32.Compute(Read(read[0], 512)) == read[1], "m06_gpt_disk_sector_receipt");
        }
        if (primaryHeaderFault)
        {
            using var boot = File.OpenRead(imagePath + ".boot.raw");
            Need(boot.Length == disk.Length, "m06_gpt_boot_image_length");
            disk.Position = 0;
            byte[] intact = new byte[65536], damaged = new byte[65536];
            long position = 0;
            int differences = 0;
            while (position < disk.Length)
            {
                int count = (int)Math.Min(intact.Length, disk.Length - position);
                boot.ReadExactly(intact.AsSpan(0, count));
                disk.ReadExactly(damaged.AsSpan(0, count));
                for (int i = 0; i < count; i++)
                    if (intact[i] != damaged[i])
                    {
                        Need(position + i == GptImageFixture.FaultOffset && (intact[i] ^ damaged[i]) == 1, "m06_gpt_boot_image_changed_byte");
                        differences++;
                    }
                position += count;
            }
            Need(differences == 1, "m06_gpt_boot_image_fault_missing");
        }
    }

    private static ulong[] GptRow(IReadOnlyList<string> lines, int stage)
    {
        string key = "M06_GPT " + stage.ToString("X2");
        string[] matches = lines.Where(s => s.StartsWith(key + " ", StringComparison.Ordinal)).ToArray();
        Need(matches.Length == 1, "m06_gpt_unique_row");
        return ReadHexRecord(matches[0], key, 4);
    }
}
