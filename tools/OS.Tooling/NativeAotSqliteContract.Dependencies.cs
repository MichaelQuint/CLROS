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
    public sealed record Dependencies(IReadOnlyList<Package> Packages, IReadOnlyList<string> MuslNativeAssets)
    {
        public bool HasStaticArchive => MuslNativeAssets.Any(p => p.EndsWith(".a", StringComparison.Ordinal));
        public bool GrantsNativeLinkageOrGuestAuthority => false;
    }
}
