namespace OS.Tools.QemuRunner;

/// <summary>Host-only identity of the finite diagnostic binary response. Bytes
/// are borrowed synchronously; matching allocates nothing and retains no input.
/// These values describe entity bytes only, without syscall or guest authority.</summary>
public static class WebVectorResponse
{
    public const int Length = 49152;
    public const string Sha256 = "664d1e34fe80e8713fefa2b9c30df7d7877bbffd850411ffda14f54bd1ce847c";

    /// <summary>Require exactly Length bytes with byte i equal to i%251.</summary>
    public static bool Matches(ReadOnlySpan<byte> body)
    {
        if (body.Length != Length) return false;
        for (int i = 0; i < body.Length; i++) if (body[i] != (byte)(i % 251)) return false;
        return true;
    }
}
