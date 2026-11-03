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
    /// <summary>Copied fields printed after a successful measured F_GETLK.
    /// They describe its returned flock, not the unobserved input query or lock
    /// ownership. ReportedPid is retained even when F_UNLCK makes it irrelevant.</summary>

    public sealed record StorageReturnedLock(string Type, string Whence, long Start, long Length, int ReportedPid);
}
