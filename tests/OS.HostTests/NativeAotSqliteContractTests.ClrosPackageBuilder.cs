using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotSqliteContractTests
{
    private delegate byte[] ClrosPackageBuilder(ReadOnlySpan<byte> elf, string mode, string hash);
}
