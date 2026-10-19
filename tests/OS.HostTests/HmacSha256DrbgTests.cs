using System.Runtime.InteropServices;
using OS.Std.Hash;

internal static unsafe class HmacSha256DrbgTests
{
    // NIST CAVP drbgtestvectors.zip, HMAC_DRBG.rsp, SHA-256 COUNT0,
    // AdditionalInputLen0, ReturnedBitsLen1024. Published inputs are test-only.
    // https://csrc.nist.gov/Projects/Cryptographic-Algorithm-Validation-Program/Random-Number-Generators
    [HostTest("HMAC DRBG matches the original NIST no-reseed SHA256 known answer")]
    private static void KnownAnswer() => Kat(
        "ca851911349384bffe89de1cbdc46e6831e44d34a4fb935ee285dd14b71a7488",
        "659ba96c601dc69fc902940805ec0ca8", "", "",
        "e528e9abf2dece54d47c7e75e5fe302149f817ea9fb4bee6f4199697d04d5b89d54fbb978a15b5c443c9ec21036d2460b6f73ebad0dc2aba6e624abf07745bc107694bb7547bb0995f70de25d6b29e2d3011bb19d27676c07162c8b5ccde0668961df86803482cb37ed6d5c0bb8d50cf1f50d476aa0458bdaba806f48be9dcb8");

    [HostTest("HMAC DRBG matches the original NIST explicit-reseed SHA256 known answer")]
    private static void ReseedAnswer() => Kat(
        "06032cd5eed33f39265f49ecb142c511da9aff2af71203bffaf34a9ca5bd9c0d",
        "0e66f71edc43e42a45ad3c6fc6cdc4df", "",
        "01920a4e669ed3a85ae8a33b35a74ad7fb2a6bb4cf395ce00334a9c9a5a5d552",
        "76fc79fe9b50beccc991a11b5635783a83536add03c157fb30645e611c2898bb2b1bc215000209208cd506cb28da2a51bdb03826aaf2bd2335d576d519160842e7158ad0949d1a9ec3e66ea1b1a064b005de914eac2e9d4f2d72a8616a80225422918250ff66a41bd2f864a6a38cc5b6499dc43f7f2bd09e1e0f8f5885935124");

    [HostTest("HMAC DRBG matches the original NIST personalized SHA256 known answer")]
    private static void PersonalizedAnswer() => Kat(
        "5cacc68165a2e2ee20812f35ec73a79dbf30fd475476ac0c44fc6174cdac2b55",
        "6f885496c1e63af620becd9e71ecb824",
        "e72dd8590d4ed5295515c35ed6199e9d211b8f069b3058caa6670b96ef1208d0", "",
        "f1012cf543f94533df27fedfbf58e5b79a3dc517a9c402bdbfc9a0c0f721f9d53faf4aafdc4b8f7a1b580fcaa52338d4bd95f58966a243cdcd3f446ed4bc546d9f607b190dd69954450d16cd0e2d6437067d8b44d19a6af7a7cfa8794e5fbd728e8fb2f2e8db5dd4ff1aa275f35886098e80ff844886060da8b1e7137846b23b");

    private static void Kat(string entropy, string nonce, string personalization, string reseed, string expected)
    {
        byte[] e = Convert.FromHexString(entropy), n = Convert.FromHexString(nonce),
            p = Convert.FromHexString(personalization), r = Convert.FromHexString(reseed);
        HmacSha256Drbg.State state = default; byte* output = stackalloc byte[128];
        fixed (byte* ep = e, np = n, pp = p, rp = r)
        {
            HostCheck.True(HmacSha256Drbg.Instantiate(&state, ep, np, pp, (uint)p.Length));
            if (r.Length != 0) HostCheck.True(HmacSha256Drbg.Reseed(&state, rp));
            HostCheck.True(HmacSha256Drbg.Generate(&state, output, 128));
            HostCheck.True(HmacSha256Drbg.Generate(&state, output, 128));
            HostCheck.True(new ReadOnlySpan<byte>(output, 128).SequenceEqual(Convert.FromHexString(expected)));
            HostCheck.Equal(2UL, state.RequestsSinceReseed);
            HostCheck.True(HmacSha256Drbg.Erase(&state));
            HostCheck.True(new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).ToArray().All(x => x == 0));
        }
    }

    [HostTest("HMAC DRBG rejects copied state invalid admission overlaps and corrupt counters unchanged")]
    private static void Admission()
    {
        HmacSha256Drbg.State state = default;
        byte* seed = stackalloc byte[48]; new Span<byte>(seed, 48).Fill(0xa5);
        byte* output = stackalloc byte[64]; new Span<byte>(output, 64).Fill(0xcc);
        HostCheck.True(!HmacSha256Drbg.Generate(&state, output, 1));
        HostCheck.True(!HmacSha256Drbg.Instantiate(&state, null, seed + 32, null, 0));
        HostCheck.True(!HmacSha256Drbg.Instantiate(&state, seed, seed + 32, null, 1));
        HostCheck.True(!HmacSha256Drbg.Instantiate(&state, seed, seed + 32, seed, 33));
        HostCheck.True(HmacSha256Drbg.Instantiate(&state, seed, seed + 32, null, 0));
        byte[] original = new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).ToArray();
        var copy = state;
        HostCheck.True(!HmacSha256Drbg.Generate(&copy, output, 1) && !HmacSha256Drbg.Erase(&copy));
        HostCheck.True(!HmacSha256Drbg.Instantiate(&state, seed, seed + 32, null, 0));
        HostCheck.True(!HmacSha256Drbg.Generate(&state, output, 0) && !HmacSha256Drbg.Generate(&state, output, 8193));
        HostCheck.True(!HmacSha256Drbg.Generate(&state, (byte*)&state, 1));
        HostCheck.True(!HmacSha256Drbg.Reseed(&state, (byte*)&state) && !HmacSha256Drbg.Reseed(&state, null));
        HostCheck.True(!HmacSha256Drbg.Generate(&state, (byte*)(ulong.MaxValue - 1), 4));
        HostCheck.True(new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).SequenceEqual(original));
        HostCheck.True(new ReadOnlySpan<byte>(output, 64).ToArray().All(x => x == 0xcc));
        HostCheck.True(HmacSha256Drbg.Erase(&state));
    }

    [HostTest("HMAC DRBG fails closed at the standard reseed interval and explicit fresh reseed restores availability")]
    private static void Exhaustion()
    {
        HmacSha256Drbg.State state = default; byte* seed = stackalloc byte[48]; new Span<byte>(seed, 48).Fill(0x23);
        byte* output = stackalloc byte[32];
        HostCheck.True(HmacSha256Drbg.Instantiate(&state, seed, seed + 32, null, 0));
        // Deliberately corrupt private metadata to reach an otherwise infeasible boundary.
        ulong* counter = (ulong*)((byte*)&state + Marshal.OffsetOf<HmacSha256Drbg.State>("Counter").ToInt32());
        *counter = HmacSha256Drbg.ReseedInterval;
        HostCheck.True(HmacSha256Drbg.Generate(&state, output, 32) && state.RequiresReseed);
        byte[] before = new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).ToArray();
        new Span<byte>(output, 32).Fill(0xcc);
        HostCheck.True(!HmacSha256Drbg.Generate(&state, output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).SequenceEqual(before));
        HostCheck.True(new ReadOnlySpan<byte>(output, 32).ToArray().All(x => x == 0xcc));
        seed[0] ^= 1; HostCheck.True(HmacSha256Drbg.Reseed(&state, seed) && !state.RequiresReseed);
        HostCheck.True(HmacSha256Drbg.Generate(&state, output, 32));
        *counter = ulong.MaxValue; before = new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).ToArray();
        HostCheck.True(!HmacSha256Drbg.Generate(&state, output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(&state, sizeof(HmacSha256Drbg.State)).SequenceEqual(before));
        *counter = 1; HostCheck.True(HmacSha256Drbg.Erase(&state));
    }

    [HostTest("HMAC DRBG supports repeated bounded requests beyond the old entropy pool without allocation or repeated output")]
    private static void Continuing()
    {
        HmacSha256Drbg.State state = default; byte* seed = stackalloc byte[48]; new Span<byte>(seed, 48).Fill(0x45);
        byte* output = stackalloc byte[8192];
        HostCheck.True(HmacSha256Drbg.Instantiate(&state, seed, seed + 32, null, 0));
        HostCheck.True(HmacSha256Drbg.Generate(&state, output, 8192));
        byte[] first = new ReadOnlySpan<byte>(output, 8192).ToArray();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        bool success = HmacSha256Drbg.Generate(&state, output, 8192);
        HostCheck.Equal(allocated, GC.GetAllocatedBytesForCurrentThread()); HostCheck.True(success);
        HostCheck.True(!new ReadOnlySpan<byte>(output, 8192).SequenceEqual(first));
        HostCheck.True(HmacSha256Drbg.Generate(&state, output, 33));
        HostCheck.True(HmacSha256Drbg.Generate(&state, output, 1));
        HostCheck.Equal(4UL, state.RequestsSinceReseed); HostCheck.True(HmacSha256Drbg.Erase(&state));
    }
}
