using System.Security.Cryptography;

namespace OS.X64TextSerializerGenerator;

internal static partial class Program
{
    private readonly record struct OutputWriteResult(bool Success, string? State, string? ErrorCode)
    {
        public static OutputWriteResult Created => new(true, "created", null);
        public static OutputWriteResult Unchanged => new(true, "unchanged", null);

        public static OutputWriteResult Fail(string errorCode) => new(false, null, errorCode);
    }
}
