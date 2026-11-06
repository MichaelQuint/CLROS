using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using F = OS.Tooling.NativeAotLogUploadContract;

internal static partial class NativeAotLogUploadContractTests
{
    private delegate F.ClrosPackageValues PackageInspector(ReadOnlySpan<byte> package, string phase, string digest);
}
