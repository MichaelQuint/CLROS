using System.Security.Cryptography;

namespace OS.X64TextSerializerGenerator;

internal static partial class Program
{
    private readonly record struct CliParseResult(bool Success, string? OutputRoot, string? OutputFile, string? ErrorCode, bool CheckedReferences = false, bool InterfaceDispatch = false, bool SimpleThrowCatch = false, bool SingleFinallyCatch = false, int RethrowMode = 0, bool NestedFinallyCatch = false, bool FrameDispatch = false, bool ConstructorFailure = false, int FilterMode = 0, bool NormalFinally = false)
    {
        public static CliParseResult Fail(string errorCode) => new(false, null, null, errorCode);
    }
}
