using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using OS.Tooling;

internal static class FatImageFixtureTests
{
    [HostTest("fat.image_fixture creates fragmented file and cross-cluster LFN with exact mutation bounds", HostLane.Native)]
    private static void Layout() => WithFreshImage((path, efi) =>
    {
        byte[] before = File.ReadAllBytes(path);
        var receipt = FatImageFixture.Apply(path, efi);
        byte[] after = File.ReadAllBytes(path);
        int boot = checked((int)U64(before, 2 * 512 + 32) * 512);
        int fat = boot + U16(before, boot + 14) * 512, fatBytes = checked((int)U32(before, boot + 36) * 512);
        int data = fat + 2 * fatBytes, root = data, info = boot + 512;
        int Cluster(uint c) => checked(data + (int)(c - 2) * 512);
        uint Next(uint c) => U32(after, checked(fat + (int)c * 4)) & 0x0fffffff;
        uint[] added = [receipt.RootExtension, receipt.FileFirst, receipt.FileSecond, receipt.FileThird];
        HostCheck.True(added.Distinct().Count() == 4 && added.All(c => c >= 2 && U32(before, checked(fat + (int)c * 4)) == 0));
        HostCheck.True(receipt.FileFirst + 1 != receipt.FileSecond && receipt.FileSecond + 1 != receipt.FileThird);
        HostCheck.True(Next(2) == receipt.RootExtension && Next(receipt.RootExtension) >= 0x0ffffff8 &&
            Next(receipt.FileFirst) == receipt.FileSecond && Next(receipt.FileSecond) == receipt.FileThird && Next(receipt.FileThird) >= 0x0ffffff8);
        HostCheck.True(receipt.FileBytes == 1301 && receipt.FileCrc32 == 0xe194d61d && receipt.LongName == "CLROS Live Storage Fixture.bin");
        HostCheck.True(receipt.PartitionFirstLba == (ulong)boot / 512 && receipt.PartitionLastLba == U64(before, 2 * 512 + 40));
        HostCheck.True(receipt.OriginalSha256 == Hash(before) && receipt.FinalSha256 == Hash(after) && receipt.EfiSha256 == Hash(File.ReadAllBytes(efi)));
        HostCheck.True(after.AsSpan(fat, fatBytes).SequenceEqual(after.AsSpan(fat + fatBytes, fatBytes)) &&
            after.AsSpan(info, 512).SequenceEqual(after.AsSpan(boot + 7 * 512, 512)));
        uint clusters = (U32(before, boot + 32) - U16(before, boot + 14) - 2 * U32(before, boot + 36)) / before[boot + 13];
        uint free = 0;
        for (uint c = 2; c < clusters + 2; c++) if ((U32(before, checked(fat + (int)c * 4)) & 0x0fffffff) == 0) free++;
        HostCheck.Equal(free - 4, U32(after, info + 488));
        uint nextFree = U32(after, info + 492);
        HostCheck.True(nextFree >= 2 && nextFree < clusters + 2 && Next(nextFree) == 0);

        int tail = Cluster(receipt.RootExtension);
        int[] physical = [tail + 32, tail, root + 480], wordOffsets = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
        byte checksum = 0;
        foreach (byte b in "LSFIRE~1BIN"u8) checksum = unchecked((byte)((checksum >> 1) + (checksum << 7) + b));
        char[] name = new char[39];
        for (int slot = 0; slot < 3; slot++)
        {
            int entry = physical[slot];
            HostCheck.True(after[entry] == (slot == 2 ? 0x43 : slot + 1) && after[entry + 11] == 15 &&
                after[entry + 12] == 0 && after[entry + 13] == checksum && U16(after, entry + 26) == 0);
            for (int i = 0; i < 13; i++) name[slot * 13 + i] = (char)U16(after, entry + wordOffsets[i]);
        }
        int end = Array.IndexOf(name, '\0');
        HostCheck.True(end == 30 && new string(name, 0, end) == "CLROS Live Storage Fixture.bin" && name[(end + 1)..].All(c => c == '\uffff'));
        HostCheck.True(after.AsSpan(tail + 64, 11).SequenceEqual("LSFIRE~1BIN"u8) && after[tail + 75] == 0x20 &&
            EntryCluster(after, tail + 64) == receipt.FileFirst && U32(after, tail + 92) == 1301 && after[tail + 96] == 0);
        for (int slot = 2; slot < 15; slot++) HostCheck.Equal((byte)0xe5, after[root + slot * 32]);
        uint[] file = [receipt.FileFirst, receipt.FileSecond, receipt.FileThird];
        for (int i = 0; i < 1536; i++)
        {
            byte expected = i < 1301 ? unchecked((byte)(((uint)i * 73 + 19) ^ ((uint)i / 8))) : (byte)0xa5;
            HostCheck.Equal(expected, after[Cluster(file[i / 512]) + i % 512]);
        }
        // Every exception is a precise intended write extent, independently
        // derived from disk geometry. All EFI, GPT and unrelated bytes stay exact.
        List<(int Start, int Bytes)> allowed = [(info + 488, 8), (boot + 7 * 512 + 488, 8), (root + 480, 32)];
        for (int slot = 2; slot < 15; slot++) allowed.Add((root + slot * 32, 1));
        foreach (uint c in added) allowed.Add((Cluster(c), 512));
        foreach (uint c in added.Prepend(2u))
            foreach (int copy in new[] { 0, fatBytes }) allowed.Add((checked(fat + copy + (int)c * 4), 4));
        int cursor = 0;
        foreach (var range in allowed.OrderBy(r => r.Start))
        {
            HostCheck.True(range.Start >= cursor && before.AsSpan(cursor, range.Start - cursor).SequenceEqual(after.AsSpan(cursor, range.Start - cursor)));
            cursor = range.Start + range.Bytes;
        }
        HostCheck.True(before.Length == after.Length && before.AsSpan(cursor).SequenceEqual(after.AsSpan(cursor)));
        int executable = Cluster(4) + 64; uint current = EntryCluster(before, executable);
        byte[] expectedEfi = File.ReadAllBytes(efi);
        for (int offset = 0; offset < expectedEfi.Length; offset += 512)
        {
            int count = Math.Min(512, expectedEfi.Length - offset);
            HostCheck.True(after.AsSpan(Cluster(current), count).SequenceEqual(expectedEfi.AsSpan(offset, count)));
            current = Next(current);
        }
        HostCheck.True(current >= 0x0ffffff8);
    });

    [HostTest("fat.image_fixture rejects repeated malformed mismatched and wrong-EFI inputs without writes", HostLane.Native)]
    private static void Rejections() => WithFreshImage((path, efi) =>
    {
        byte[] original = File.ReadAllBytes(path);
        int boot = checked((int)U64(original, 2 * 512 + 32) * 512);
        int secondFat = boot + (U16(original, boot + 14) + checked((int)U32(original, boot + 36))) * 512;
        foreach (int offset in new[] { boot + 13, secondFat + 8 })
        {
            original[offset] ^= 2; File.WriteAllBytes(path, original); RejectUnchanged(path, efi); original[offset] ^= 2;
        }
        File.WriteAllBytes(path, original);
        byte[] expected = File.ReadAllBytes(efi); expected[0] ^= 1; File.WriteAllBytes(efi, expected);
        RejectUnchanged(path, efi); expected[0] ^= 1; File.WriteAllBytes(efi, expected);
        FatImageFixture.Apply(path, efi); RejectUnchanged(path, efi);
    });

    private static void RejectUnchanged(string path, string efi)
    {
        string before = FileHash(path); bool rejected = false;
        try { FatImageFixture.Apply(path, efi); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected && FileHash(path) == before);
    }
    private static void WithFreshImage(Action<string, string> action)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts/fat-image-fixture-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "disk.raw"), efi = Path.Combine(directory, "BOOTX64.EFI");
        try
        {
            File.WriteAllBytes(efi, Enumerable.Range(0, 1537).Select(i => (byte)(i % 251)).ToArray());
            var start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.ImageBuilder");
            foreach (string arg in new[] { "create-raw", "--efi", efi, "--output", path, "--image-size-mib", "64" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            { process.Kill(true); process.WaitForExit(5000); throw new InvalidOperationException("image builder timed out"); }
            string text = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
            HostCheck.True(process.ExitCode == 0 && text.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal), text);
            HostCheck.Equal(64L * 1024 * 1024, new FileInfo(path).Length);
            action(path, efi);
        }
        finally { Directory.Delete(directory, true); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
    private static uint EntryCluster(byte[] bytes, int offset) => ((uint)U16(bytes, offset + 20) << 16) | U16(bytes, offset + 26);
}
