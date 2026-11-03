using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05PhysicalMappingRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == 86 && lines[76] == "TEST_START m05.001" && lines[84] == "CLROS_M05_001_READY" && lines[85] == "TEST_PASS m05.001", "m05_mapping_record_count");
        ulong[][] rows = new ulong[7][];
        for (int stage = 0; stage < rows.Length; stage++)
        {
            var match = Regex.Match(lines[77 + stage], @"\AM05_MAP " + stage + @"((?: 0x[0-9a-f]{16}){4})\z");
            Need(match.Success, "m05_mapping_row_" + stage);
            rows[stage] = match.Groups[1].Value.TrimStart().Split(' ').Select(Number).ToArray();
        }
        ulong[] descriptors = ReadHexRecord(lines[73], "M03_OWNED 6", 8);
        ulong root = rows[0][0], start = rows[0][1], cap = rows[0][2], defaults = rows[0][3];
        Need(root == descriptors[1] && root != 0 && root % 4096 == 0 && (decimal)root + 225280 < 4294967296 &&
            start >= 1048576 && start % 2097152 == 0 && (decimal)start + 2097152 <= 4294967296 &&
            (cap & 3072) == 1024 && (cap & 255) <= 32 && (defaults & 2048) != 0 && (defaults & 0xfffffffffffff300) == 0 &&
            new ulong[] { 0, 1, 4, 5, 6 }.Contains(defaults & 255), "m05_mapping_admission");
        foreach (var (at, length) in new[] { (descriptors[0], 430080UL), (root, 225280UL), (descriptors[2], 12288UL) })
            Need((decimal)start >= (decimal)at + length || (decimal)at >= (decimal)start + 2097152, "m05_mapping_scratch_alias");
        ulong originalPat = rows[1][0], changedPat = rows[1][1], features = rows[1][3];
        const ulong patMask = 0xffffff00ffffffff, nx = 0x8000000000000000, withoutAd = 0xffffffffffffff9f;
        Need((originalPat & 255) == 6 && ((originalPat >> 24) & 255) == 0 && ((originalPat >> 32) & 255) == 6 &&
            ((changedPat >> 32) & 255) == 1 && (originalPat & patMask) == (changedPat & patMask) && rows[1][2] == root &&
            (features & 69632) == 69632 && ((features >> 32) & 8192) == 0, "m05_mapping_pat");
        for (int i = 0; i < 8; i++)
            Need(new ulong[] { 0, 1, 4, 5, 6, 7 }.Contains((originalPat >> (8 * i)) & 255), "m05_mapping_pat_reserved");
        ulong entry = rows[2][0], originalParent = rows[2][1], large = rows[2][2], child = rows[2][3];
        Need(entry >= root && (decimal)entry < (decimal)root + 225280 && entry % 8 == 0 && (entry % 4096) / 8 == ((start >> 21) & 511) &&
            child is >= 1 and < 55 && (originalParent & 64) == 0 && (rows[3][2] & 64) == 0 &&
            (originalParent & withoutAd) == ((root + child * 4096) | 3UL) && (large & withoutAd) == (nx | start | 131UL), "m05_mapping_coalesce");
        ulong uc = rows[3][3], wc = rows[4][2], wb = rows[5][2], selected = root + child * 4096 + 8;
        Need(rows[3][0] == entry && rows[3][1] == large && (rows[3][2] & withoutAd) == (originalParent & withoutAd) &&
            (uc & withoutAd) == (nx | (start + 4096) | 27UL) && rows[4][0] == selected && (rows[4][1] & withoutAd) == (uc & withoutAd) &&
            (wc & withoutAd) == (nx | (start + 4096) | 131UL) && rows[4][3] == changedPat && rows[5][0] == selected &&
            (rows[5][1] & withoutAd) == (wc & withoutAd) && (wb & withoutAd) == (nx | (start + 4096) | 3UL) &&
            rows[5][3] == 0x5743000000000001 && rows[6][0] == root && rows[6][1] == start && rows[6][2] == originalPat &&
            (rows[6][3] & withoutAd) == (wb & withoutAd), "m05_mapping_transition");
    }
}
