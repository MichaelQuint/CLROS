using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotThreadingPayload
{
    /// <summary>Owned value-only trace inventory. Synthetic files may exercise
    /// parsing; only the complete qualifier binds these hashes to its own run.</summary>

    public sealed record TraceInventory(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Syscalls);
}
