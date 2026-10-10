using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Storage;

namespace OS.Tooling;

public sealed record GptImageFaultReceipt(string OriginalSha256, string FinalSha256, long Offset, byte Before, byte After);

/// <summary>One deliberate primary-header CRC fault in a validated, canonical 512-byte-sector image.</summary>
public static class GptImageFixture
{
    public const long FaultOffset = 512 + 16;
    private const int SectorBytes = 512, EntryCount = 128, EntryBytes = 128, ArrayBytes = EntryCount * EntryBytes;

    public static GptImageFaultReceipt ApplyPrimaryHeaderCrcFault(string imagePath)
    {
        using var image = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        long length = image.Length;
        Need(length >= 68 * SectorBytes && length % SectorBytes == 0, "geometry");
        ulong capacity = (ulong)(length / SectorBytes);
        var primary = ValidateCopy(image, capacity, false);
        var backup = ValidateCopy(image, capacity, true);
        Need(primary.Header.DiskGuid == backup.Header.DiskGuid &&
            primary.Header.FirstUsableLba == backup.Header.FirstUsableLba &&
            primary.Header.LastUsableLba == backup.Header.LastUsableLba &&
            primary.Entries.AsSpan().SequenceEqual(backup.Entries), "copies_disagree");

        // Hash the original and the expected single-byte mutation before writing. This
        // bounds memory independently of image size and authenticates every other byte.
        image.Position = 0;
        using var original = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        byte before = 0;
        long position = 0;
        int read;
        while ((read = image.Read(buffer)) != 0)
        {
            original.AppendData(buffer, 0, read);
            int changed = FaultOffset >= position && FaultOffset - position < read ? (int)(FaultOffset - position) : -1;
            if (changed >= 0) { before = buffer[changed]; buffer[changed] ^= 1; }
            expected.AppendData(buffer, 0, read);
            if (changed >= 0) buffer[changed] ^= 1;
            position += read;
        }
        Need(position == length, "read_length");
        string originalSha = Convert.ToHexString(original.GetHashAndReset());
        string expectedSha = Convert.ToHexString(expected.GetHashAndReset());
        byte after = (byte)(before ^ 1);
        image.Position = FaultOffset;
        image.WriteByte(after);
        image.Flush(true);
        image.Position = FaultOffset;
        Need(image.Length == length && image.ReadByte() == after, "write_readback");
        image.Position = 0;
        string finalSha = Convert.ToHexString(SHA256.HashData(image));
        Need(finalSha == expectedSha, "image_readback");
        return new(originalSha, finalSha, FaultOffset, before, after);
    }

    private static (GptHeader Header, byte[] Entries) ValidateCopy(FileStream image, ulong capacity, bool backup)
    {
        ulong current = backup ? capacity - 1 : 1;
        byte[] sector = Read(image, current, SectorBytes);
        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(12));
        Need(headerSize is >= 92 and <= SectorBytes &&
            BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(20)) == 0, "header_reserved");
        for (int i = (int)headerSize; i < SectorBytes; i++) Need(sector[i] == 0, "sector_reserved");
        Need(BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(80)) == EntryCount &&
            BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(84)) == EntryBytes, "entry_layout");
        ulong first = BinaryPrimitives.ReadUInt64LittleEndian(sector.AsSpan(40));
        ulong last = BinaryPrimitives.ReadUInt64LittleEndian(sector.AsSpan(48));
        ulong arrayLba = BinaryPrimitives.ReadUInt64LittleEndian(sector.AsSpan(72));
        Need(BinaryPrimitives.ReadUInt64LittleEndian(sector.AsSpan(24)) == current &&
            BinaryPrimitives.ReadUInt64LittleEndian(sector.AsSpan(32)) == (backup ? 1 : capacity - 1), "header_positions");
        Need(first >= 34 && first <= last && last <= capacity - 34 && arrayLba <= capacity - 32, "usable_geometry");
        // Fresh image-builder fixtures use the canonical adjacent backup array.
        Need(backup ? arrayLba == current - 32 && arrayLba > last : arrayLba >= 2 && arrayLba + 32 <= first,
            "array_geometry");
        byte[] entries = Read(image, arrayLba, ArrayBytes);
        Span<GptPartitionDescriptor> partitions = stackalloc GptPartitionDescriptor[EntryCount];
        var parsed = backup
            ? GptPartitionTableParser.ParseBackup(sector, entries, capacity, SectorBytes, partitions)
            : GptPartitionTableParser.ParsePrimary(sector, entries, capacity, SectorBytes, partitions);
        Need(parsed.Succeeded && parsed.CrcStatus == GptPartitionTableCrcStatus.Validated, "invalid_gpt");
        Need(parsed.Header.DiskGuid != Guid.Empty, "disk_guid");
        for (int i = 0; i < parsed.PartitionsWritten; i++)
        {
            var item = partitions[i];
            Need(item.UniquePartitionGuid != Guid.Empty, "partition_guid");
            for (int j = 0; j < i; j++)
                Need(item.UniquePartitionGuid != partitions[j].UniquePartitionGuid &&
                    (item.LastLba < partitions[j].FirstLba || item.FirstLba > partitions[j].LastLba), "partition_overlap");
        }
        for (int i = 0; i < EntryCount; i++)
        {
            var entry = entries.AsSpan(i * EntryBytes, EntryBytes);
            if (new Guid(entry[..16]) != Guid.Empty) continue;
            foreach (byte value in entry) Need(value == 0, "unused_entry");
        }
        return (parsed.Header, entries);
    }

    private static byte[] Read(FileStream image, ulong lba, int bytes)
    {
        image.Position = checked((long)(lba * SectorBytes));
        byte[] result = new byte[bytes];
        image.ReadExactly(result);
        return result;
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition) throw new ToolFailure("gpt_image_fixture_" + reason);
    }
}
