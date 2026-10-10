using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Tooling;

internal static class GptImageFixtureTests
{
    private const int Sector = 512, Blocks = 128, Backup = (Blocks - 1) * Sector, BackupArray = (Blocks - 33) * Sector;

    [HostTest("gpt.image_fixture mutates exactly the primary CRC byte with an auditable receipt")]
    private static void SingleFault()
    {
        HostCheck.True(Crc("123456789"u8) == 0xcbf43926);
        byte[] original = Image();
        WithImage(original, path =>
        {
            var receipt = GptImageFixture.ApplyPrimaryHeaderCrcFault(path);
            byte[] final = File.ReadAllBytes(path);
            HostCheck.True(receipt.Offset == 528 && receipt.Before == original[528] && receipt.After == (original[528] ^ 1));
            HostCheck.True(receipt.OriginalSha256 == Convert.ToHexString(SHA256.HashData(original)) &&
                receipt.FinalSha256 == Convert.ToHexString(SHA256.HashData(final)) && final.Length == original.Length);
            int changed = 0;
            for (int i = 0; i < original.Length; i++)
                if (original[i] != final[i]) { changed++; HostCheck.True(i == 528 && (original[i] ^ final[i]) == 1); }
            HostCheck.True(changed == 1);
            Reject(path);
            HostCheck.True(File.ReadAllBytes(path).AsSpan().SequenceEqual(final));
        });
    }

    [HostTest("gpt.image_fixture rejects corrupt GPT copies and disagreement without writes")]
    private static void Corruption()
    {
        foreach (int offset in new[] { Sector + 16, Backup + 16, 2 * Sector + 100, BackupArray + 100 })
        {
            byte[] image = Image(); image[offset] ^= 1; RejectUnchanged(image);
        }
        byte[] different = Image();
        different[Backup + 56] ^= 1; HeaderCrc(different, Backup); RejectUnchanged(different);
        different = Image();
        different[BackupArray + 56] = 7;
        Put32(different, Backup + 88, Crc(different.AsSpan(BackupArray, 16384)));
        HeaderCrc(different, Backup); RejectUnchanged(different);
        foreach (bool duplicate in new[] { false, true })
        {
            byte[] image = Image();
            image.AsSpan(2 * Sector, 128).CopyTo(image.AsSpan(2 * Sector + 128, 128));
            if (!duplicate) image[2 * Sector + 128 + 16] ^= 1;
            else { Put64(image, 2 * Sector + 128 + 32, 81); Put64(image, 2 * Sector + 128 + 40, 90); }
            ArrayCrc(image); RejectUnchanged(image);
        }
        byte[] unused = Image(); unused[2 * Sector + 128 + 80] = 1; ArrayCrc(unused); RejectUnchanged(unused);
    }

    [HostTest("gpt.image_fixture rejects truncation and unsupported geometry without writes")]
    private static void Geometry()
    {
        foreach (int length in new[] { 0, 67 * Sector, Blocks * Sector - 1, (Blocks - 1) * Sector })
            RejectUnchanged(Image()[..length]);
        foreach (var mutation in new (int Header, int Field, ulong Value, bool Wide)[]
        {
            (Sector, 12, 91, false), (Sector, 12, 513, false), (Sector, 20, 1, false),
            (Sector, 24, 2, true), (Sector, 32, 126, true), (Sector, 40, 33, true),
            (Sector, 48, 95, true), (Sector, 72, ulong.MaxValue, true),
            (Sector, 80, 127, false), (Sector, 84, 256, false), (Backup, 72, 94, true),
        })
        {
            byte[] image = Image();
            if (mutation.Wide) Put64(image, mutation.Header + mutation.Field, mutation.Value);
            else Put32(image, mutation.Header + mutation.Field, (uint)mutation.Value);
            if (mutation.Field != 12) HeaderCrc(image, mutation.Header);
            RejectUnchanged(image);
        }
        byte[] reserved = Image(); reserved[Sector + 511] = 1; RejectUnchanged(reserved);
    }

    private static byte[] Image()
    {
        byte[] image = new byte[Blocks * Sector];
        // Opaque workload bytes must survive; this is not a filesystem fixture.
        for (int i = 40 * Sector; i < 81 * Sector; i++) image[i] = (byte)(i * 37 + 19);
        byte[] type = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b").ToByteArray();
        type.CopyTo(image, 2 * Sector);
        new Guid("01020304-0506-0708-0910-111213141516").ToByteArray().CopyTo(image, 2 * Sector + 16);
        Put64(image, 2 * Sector + 32, 40); Put64(image, 2 * Sector + 40, 80);
        foreach (int offset in new[] { Sector, Backup })
        {
            "EFI PART"u8.CopyTo(image.AsSpan(offset));
            Put32(image, offset + 8, 0x10000); Put32(image, offset + 12, 92);
            Put64(image, offset + 24, offset == Sector ? 1UL : 127);
            Put64(image, offset + 32, offset == Sector ? 127UL : 1);
            Put64(image, offset + 40, 34); Put64(image, offset + 48, 94);
            new Guid("aabbccdd-eeff-1122-3344-556677889900").ToByteArray().CopyTo(image, offset + 56);
            Put64(image, offset + 72, offset == Sector ? 2UL : 95);
            Put32(image, offset + 80, 128); Put32(image, offset + 84, 128);
        }
        ArrayCrc(image);
        return image;
    }

    private static void ArrayCrc(byte[] image)
    {
        image.AsSpan(2 * Sector, 16384).CopyTo(image.AsSpan(BackupArray, 16384));
        uint crc = Crc(image.AsSpan(2 * Sector, 16384));
        foreach (int header in new[] { Sector, Backup }) { Put32(image, header + 88, crc); HeaderCrc(image, header); }
    }

    private static void HeaderCrc(byte[] image, int offset)
    {
        Put32(image, offset + 16, 0);
        Put32(image, offset + 16, Crc(image.AsSpan(offset, 92)));
    }

    // Independent MSB-first polynomial oracle, with explicit reflected input/output.
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= Reverse(value, 8) << 24;
            for (int i = 0; i < 8; i++) crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04c11db7U : 0);
        }
        return ~Reverse(crc, 32);
    }
    private static uint Reverse(uint value, int bits)
    {
        uint result = 0;
        for (int i = 0; i < bits; i++) { result = (result << 1) | (value & 1); value >>= 1; }
        return result;
    }
    private static void Put32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
    private static void Put64(byte[] data, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), value);
    private static void RejectUnchanged(byte[] image) => WithImage(image, path =>
    {
        Reject(path); HostCheck.True(File.ReadAllBytes(path).AsSpan().SequenceEqual(image));
    });
    private static void Reject(string path)
    {
        try { GptImageFixture.ApplyPrimaryHeaderCrcFault(path); HostCheck.True(false, "invalid image accepted"); }
        catch (ToolFailure) { }
    }
    private static void WithImage(byte[] image, Action<string> action)
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/gpt-image-fixture-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "disk.raw");
        try { File.WriteAllBytes(path, image); action(path); }
        finally { Directory.Delete(directory, true); }
    }
}
