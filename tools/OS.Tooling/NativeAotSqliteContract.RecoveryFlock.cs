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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct RecoveryFlock
    {
        [FieldOffset(0)]
        internal short Type;
        [FieldOffset(2)]
        internal short Whence;
        [FieldOffset(8)]
        internal long Start;
        [FieldOffset(16)]
        internal long Length;
        [FieldOffset(24)]
        internal int Pid;
    }
}
