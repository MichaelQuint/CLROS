using System.Buffers.Binary;
using OS.Storage;

internal static class FAT32ApplicationEFIPartitionTests
{
    private static LiveGptHeaderResult Header(byte[] bytes, bool backup = false) =>
        PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(bytes, GptTestData.Capacity, BlockReadResult.Success(1, 512), backup);

    [HostTest("gpt.efi locates the same bounded partition in primary and backup tables")]
    private static void Locate()
    {
        foreach (bool backup in new[] { false, true })
        {
            var (header, entries) = GptTestData.Create(backup);
            var before = entries.ToArray();
            var result = FAT32ApplicationEFIPartition.Select(Header(header, backup), entries);
            HostCheck.True(result.Succeeded);
            HostCheck.Equal(0u, result.EntryIndex);
            HostCheck.Equal(2048UL, result.FirstLba);
            HostCheck.Equal(131038UL, result.LastLba);
            HostCheck.Equal(128991UL, result.BlockCount);
            HostCheck.Equal(1048576UL, result.ByteOffset);
            HostCheck.True(entries.AsSpan().SequenceEqual(before));
        }
    }

    [HostTest("gpt.efi rejects missing and ambiguous EFI partition identities")]
    private static void MissingOrAmbiguous()
    {
        var (header, entries) = GptTestData.Create();
        entries[0] ^= 1;
        GptTestData.RehashArray(header, entries);
        var result = FAT32ApplicationEFIPartition.Select(Header(header), entries);
        HostCheck.Equal(LiveEfiPartitionStatus.Missing, result.Status);
        HostCheck.True(!result.Succeeded && result.BlockCount == 0);
        (header, entries) = GptTestData.Create();
        entries.AsSpan(0, 128).CopyTo(entries.AsSpan(128));
        entries[144] ^= 1;
        BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(40), 4095);
        BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(160), 8192);
        BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(168), 12287);
        GptTestData.RehashArray(header, entries);
        result = FAT32ApplicationEFIPartition.Select(Header(header), entries);
        HostCheck.Equal(LiveEfiPartitionStatus.Ambiguous, result.Status);
        HostCheck.True(!result.Succeeded && result.FirstLba == 0 && result.BlockCount == 0);
    }

    [HostTest("gpt.efi revalidates bytes and rejects malformed or default metadata")]
    private static void InvalidTable()
    {
        var (header, entries) = GptTestData.Create();
        var parsed = Header(header);
        entries[60] ^= 1;
        var result = FAT32ApplicationEFIPartition.Select(parsed, entries);
        HostCheck.Equal(LiveGptStatus.ArrayCrcMismatch, result.TableStatus);
        HostCheck.True(!result.Succeeded && result.BlockCount == 0);
        HostCheck.True(!FAT32ApplicationEFIPartition.Select(default, entries).Succeeded);
        HostCheck.True(!FAT32ApplicationEFIPartition.Select(parsed, entries.AsSpan(0, 16383)).Succeeded);
        HostCheck.True(!default(LiveEfiPartitionResult).Succeeded);
    }
}
