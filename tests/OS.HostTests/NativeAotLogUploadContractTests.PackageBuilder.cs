using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using F = OS.Tooling.NativeAotLogUploadContract;

internal static partial class NativeAotLogUploadContractTests
{
    private delegate byte[] PackageBuilder(ReadOnlySpan<byte> elf, string phase, string digest);
}
