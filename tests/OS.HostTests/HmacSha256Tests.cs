using System.Security.Cryptography;
using OS.Std.Hash;
using OS.Std.Memory;

internal static unsafe class HmacSha256Tests
{
    [HostTest("shared HMAC SHA256 matches the independent BCL at empty block padding and maximum boundaries")]
    private static void Independent()
    {
        foreach (int keys in new[] { 0, 1, 20, 32, 64 })
            foreach (int bytes in new[] { 0, 1, 55, 56, 64, 113, 256 })
            {
                byte[] key = Enumerable.Range(0, keys).Select(i => (byte)(i * 19)).ToArray();
                byte[] input = Enumerable.Range(0, bytes).Select(i => (byte)(i * 7)).ToArray();
                byte[] actual = new byte[40];
                Array.Fill(actual, (byte)0xcc);
                fixed (byte* k = key, m = input, o = actual)
                    HostCheck.True(HmacSha256.TryCompute(k, (uint)keys, m, (uint)bytes, o, 40));
                HostCheck.True(actual.AsSpan(0, 32).SequenceEqual(HMACSHA256.HashData(key, input)));
                HostCheck.True(actual.AsSpan(32).ToArray().All(x => x == 0xcc));
            }
    }

    [HostTest("shared HMAC rejects invalid range capacity and output aliases before writing")]
    private static void Admission()
    {
        byte* storage = stackalloc byte[512];
        new Span<byte>(storage, 512).Fill(0xcc);
        byte[] before = new ReadOnlySpan<byte>(storage, 512).ToArray();
        HostCheck.True(!HmacSha256.TryCompute(storage, 65, storage + 80, 8, storage + 400, 32));
        HostCheck.True(!HmacSha256.TryCompute(storage, 32, storage + 80, 257, storage + 400, 32));
        HostCheck.True(!HmacSha256.TryCompute(null, 1, storage + 80, 8, storage + 400, 32));
        HostCheck.True(!HmacSha256.TryCompute(storage, 32, null, 1, storage + 400, 32));
        HostCheck.True(!HmacSha256.TryCompute(storage, 32, storage + 80, 8, storage + 400, 31));
        HostCheck.True(!HmacSha256.TryCompute(storage, 32, storage + 80, 8, storage + 1, 32));
        HostCheck.True(!HmacSha256.TryCompute(storage, 32, storage + 80, 8, storage + 81, 32));
        HostCheck.True(!HmacSha256.TryCompute((byte*)(ulong.MaxValue - 3), 32, null, 0, storage + 400, 32));
        HostCheck.True(new ReadOnlySpan<byte>(storage, 512).SequenceEqual(before));
    }

    [HostTest("sensitive erase clears only the admitted extent and rejects invalid storage")]
    private static void Erase()
    {
        byte* storage = stackalloc byte[96];
        new Span<byte>(storage, 96).Fill(0xa5);
        HostCheck.True(SensitiveMemory.Clear(storage + 16, 64));
        HostCheck.True(new ReadOnlySpan<byte>(storage + 16, 64).ToArray().All(x => x == 0));
        HostCheck.Equal((byte)0xa5, storage[15]);
        HostCheck.Equal((byte)0xa5, storage[80]);
        HostCheck.True(SensitiveMemory.Clear(null, 0) && !SensitiveMemory.Clear(null, 1));
        HostCheck.True(!SensitiveMemory.Clear((byte*)(ulong.MaxValue - 2), 4));
    }
}
