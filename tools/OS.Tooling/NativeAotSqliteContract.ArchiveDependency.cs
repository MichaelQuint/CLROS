using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling;

public static partial class NativeAotSqliteContract
{
    /// <summary>Copied dependency facts only. The caller's expected receipt
    /// hash and supplied compiler facts do not themselves establish original
    /// file, native, process or execution authority. Runtime reuse separately
    /// holds every original input and pins the reviewed receipt.</summary>

    public sealed record ArchiveDependency(Archive Archive, string ReceiptSha256, string CommandsSha256, string ObjectSha256, IReadOnlyDictionary<string, CompilerInput> CompilerInputs)
    {
        public bool GrantsNativeOrExecutionAuthority => false;
    }
}
