using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM06LiveBlockContractRecords(IReadOnlyList<string> lines)
    {
        int start = QemuContracts.M06VirtioBlockReadMarkers.Count;
        Need(lines.Count == QemuContracts.M06LiveBlockContractMarkers.Count, "m06_bdev_record_count");
        for (int i = start; i < lines.Count; i++)
        {
            string expected = QemuContracts.M06LiveBlockContractMarkers[i];
            if (!expected.StartsWith("M06_BDEV ", StringComparison.Ordinal)) Need(lines[i] == expected, "m06_bdev_marker_order");
        }
        ulong[] Row(int offset, int stage) => ReadHexRecord(lines[start + offset], "M06_BDEV " + stage.ToString("X"), 4);
        ulong[] Prior(int stage) => ReadHexRecord(lines.Single(s => s.StartsWith("M06_READ " + stage + " ", StringComparison.Ordinal)), "M06_READ " + stage, 4);
        ulong[] geometry = Row(1, 0), notification = Row(2, 1), dma = Row(3, 2), used = Row(4, 3), ordering = Row(5, 4), payload = Row(6, 5);
        ulong[] priorGeometry = Prior(0), priorNotification = Prior(1);
        Need(geometry[0] > 1 && geometry[0] <= ulong.MaxValue / 512 && geometry[0] == priorGeometry[0] &&
            geometry[1] == 0 && geometry[2] == 512 && geometry[3] <= 255, "m06_bdev_geometry");
        Need(notification[0] is >= 1 and <= 16 && notification[1] == priorNotification[1] &&
            notification[2] == priorNotification[2] && notification[3] is >= 1 and <= 1000000, "m06_bdev_notification");
        Need(dma.SequenceEqual(Prior(2)), "m06_bdev_original_dma");
        Need(used.SequenceEqual(new ulong[] { 1, 0, 513, 0 }), "m06_bdev_completion");
        Need(ordering.SequenceEqual(new ulong[] { 1, 1, 4, 2 }), "m06_bdev_ordering");
        Need(payload[0] <= uint.MaxValue && payload[3] == 512, "m06_bdev_payload_shape");
        ulong[] cleanup = Row(10, 6), allocator = Row(11, 7), final = Row(12, 8), controls = Row(13, 9);
        Need(cleanup[0] == 1 && cleanup[1] == 1 && cleanup[2] == 1 && cleanup[3] is >= 1 and <= 1024, "m06_bdev_cleanup");
        Need(allocator.SequenceEqual(Prior(7)), "m06_bdev_allocator_restore");
        Need(final.SequenceEqual(Prior(8)), "m06_bdev_final_state");
        Need(controls.SequenceEqual(new ulong[] { 14, 14, 15, 14 }), "m06_bdev_completion_controls");
        Need(Row(14, 10).SequenceEqual(new ulong[] { 512, geometry[0], geometry[0] * 512, 1 }), "m06_bdev_info");
        Need(Row(15, 11).SequenceEqual(new ulong[] { 1, 1, Math.Min(geometry[0] - 1, uint.MaxValue), 512 }), "m06_bdev_protective_mbr");
        Need(Row(16, 12).SequenceEqual(new ulong[] { 512, 0, 0, 1 }), "m06_bdev_span");
        Need(Row(17, 13).SequenceEqual(new ulong[] { 4, 5, 6, 7 }), "m06_bdev_rejection_controls");
    }

    public static void AssertM06LiveBlockContractImage(string serialPath, string imagePath)
    {
        string[] lines = File.ReadAllLines(serialPath);
        ulong[] Row(string stage) => ReadHexRecord(lines.Single(s => s.StartsWith("M06_BDEV " + stage + " ", StringComparison.Ordinal)), "M06_BDEV " + stage, 4);
        ulong[] geometry = Row("0"), payload = Row("5"), info = Row("A"), mbr = Row("B");
        using var disk = File.OpenRead(imagePath);
        Need(disk.Length >= 1024 && disk.Length % 512 == 0 && (ulong)disk.Length / 512 == geometry[0] &&
            geometry[1] == 0 && geometry[2] == 512 && payload[3] == 512 &&
            info.SequenceEqual(new ulong[] { 512, geometry[0], (ulong)disk.Length, 1 }), "m06_bdev_disk_geometry");
        Span<byte> sector = stackalloc byte[512]; disk.ReadExactly(sector);
        Need(OS.Std.Hash.Crc32.Compute(sector) == payload[0] && BinaryPrimitives.ReadUInt64LittleEndian(sector) == payload[1] &&
            BinaryPrimitives.ReadUInt64LittleEndian(sector[504..]) == payload[2], "m06_bdev_disk_payload");
        // Independent host parsing associates the receipt with the actual image;
        // no guest parser or GPT interpretation is used here.
        uint firstLba = BinaryPrimitives.ReadUInt32LittleEndian(sector[454..]);
        uint protectedSectors = BinaryPrimitives.ReadUInt32LittleEndian(sector[458..]);
        Need(sector[510] == 0x55 && sector[511] == 0xaa && sector[446] == 0 && sector[450] == 0xee &&
            firstLba == 1 && protectedSectors == Math.Min(geometry[0] - 1, uint.MaxValue), "m06_bdev_disk_protective_mbr");
        for (int i = 462; i < 510; i++) Need(sector[i] == 0, "m06_bdev_disk_hybrid_mbr");
        Need(mbr.SequenceEqual(new ulong[] { 1, firstLba, protectedSectors, 512 }), "m06_bdev_disk_mbr_receipt");
    }
}
