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
    // Flags retains syscall-specific values; flock never becomes a fcntl
    // StorageLock or supplies byte-range ownership from its file lock flags.
    public sealed record StorageEvent(TraceCall Call, int LifetimeId, int Descriptor, string Kind, long? Count, long? Offset, long Returned, string Flags, StorageLock? Lock)
    {
        /// <summary>Returned F_GETLK fields only; Lock remains the separately
        /// observed F_SETLK input. Neither value grants actual lock authority.</summary>

        public StorageReturnedLock? ReturnedLock { get; init; }
    }
}
