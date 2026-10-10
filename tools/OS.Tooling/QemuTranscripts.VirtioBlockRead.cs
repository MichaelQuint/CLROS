using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM06VirtioBlockReadRecords(IReadOnlyList<string> lines)
    {
        int start = QemuContracts.M06VirtioBlockQueueSetupMarkers.Count;
        Need(lines.Count == QemuContracts.M06VirtioBlockReadMarkers.Count, "m06_read_record_count");
        for (int i = start; i < lines.Count; i++)
        {
            string expected = QemuContracts.M06VirtioBlockReadMarkers[i];
            if (!expected.StartsWith("M06_READ ", StringComparison.Ordinal)) Need(lines[i] == expected, "m06_read_marker_order");
        }
        ulong[] Row(int offset, int stage) => ReadHexRecord(lines[start + offset], "M06_READ " + stage, 4);
        ulong[] geometry = Row(1, 0), notification = Row(2, 1), dma = Row(3, 2), used = Row(4, 3), ordering = Row(5, 4), payload = Row(6, 5);
        ulong[] binding = ReadHexRecord(lines.Single(s => s.StartsWith("M06_BLK 0 ", StringComparison.Ordinal)), "M06_BLK 0", 4);
        ulong[] priorDma = ReadHexRecord(lines.Single(s => s.StartsWith("M06_BLK 5 ", StringComparison.Ordinal)), "M06_BLK 5", 4);
        Need(geometry[0] > 1 && geometry[0] <= ulong.MaxValue / 512 && geometry[1] == 1 && geometry[2] == 512 && geometry[3] <= 255, "m06_read_geometry");
        // This QEMU fixture uses queue zero, notify offset zero and multiplier 4.
        Need(notification[0] is >= 1 and <= 16 && notification[1] == 0 && notification[2] == binding[2] + 0x3000 &&
            notification[3] is >= 1 and <= 1000000, "m06_read_notification");
        Need(dma.SequenceEqual(priorDma), "m06_read_original_dma");
        Need(used.SequenceEqual(new ulong[] { 1, 0, 513, 0 }), "m06_read_completion");
        Need(ordering.SequenceEqual(new ulong[] { 1, 1, 4, 2 }), "m06_read_ordering");
        Need(payload[0] <= uint.MaxValue && payload[3] == 512, "m06_read_payload_shape");
        ulong[] cleanup = Row(10, 6), allocator = Row(11, 7), final = Row(12, 8), controls = Row(13, 9);
        ulong[] priorAllocator = ReadHexRecord(lines.Single(s => s.StartsWith("M06_BLK B ", StringComparison.Ordinal)), "M06_BLK B", 4);
        ulong[] priorFinal = ReadHexRecord(lines.Single(s => s.StartsWith("M06_BLK C ", StringComparison.Ordinal)), "M06_BLK C", 4);
        Need(cleanup[0] == 1 && cleanup[1] == 1 && cleanup[2] == 1 && cleanup[3] is >= 1 and <= 1024, "m06_read_cleanup");
        Need(allocator.SequenceEqual(priorAllocator), "m06_read_allocator_restore");
        Need(final.SequenceEqual(priorFinal), "m06_read_final_state");
        Need(controls.SequenceEqual(new ulong[] { 14, 14, 15, 14 }), "m06_read_completion_controls");
    }

    public static void AssertM06VirtioBlockReadImage(string serialPath, string imagePath)
    {
        string[] lines = File.ReadAllLines(serialPath);
        ulong[] geometry = ReadHexRecord(lines.Single(s => s.StartsWith("M06_READ 0 ", StringComparison.Ordinal)), "M06_READ 0", 4);
        ulong[] payload = ReadHexRecord(lines.Single(s => s.StartsWith("M06_READ 5 ", StringComparison.Ordinal)), "M06_READ 5", 4);
        using var disk = File.OpenRead(imagePath);
        Need(disk.Length >= 1024 && disk.Length % 512 == 0 && (ulong)disk.Length / 512 == geometry[0] &&
            geometry[1] == 1 && geometry[2] == 512 && payload[3] == 512, "m06_read_disk_geometry");
        Span<byte> sector = stackalloc byte[512]; disk.Position = 512; disk.ReadExactly(sector);
        Need(OS.Std.Hash.Crc32.Compute(sector) == payload[0] && BinaryPrimitives.ReadUInt64LittleEndian(sector) == payload[1] &&
            BinaryPrimitives.ReadUInt64LittleEndian(sector[504..]) == payload[2], "m06_read_disk_payload");
    }
}
