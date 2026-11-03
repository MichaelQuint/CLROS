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
    private sealed class CommandAttempt
    {
        public required string executable { get; init; }
        public required string[] arguments { get; init; }
        public required string workingDirectory { get; init; }
        public required string log { get; init; }
        public required string phase { get; init; }
        public double limitSeconds { get; init; }
        public double elapsedSeconds { get; set; }
        public string outcome { get; set; } = "started";
        public string failure { get; set; } = "";
    }
}
