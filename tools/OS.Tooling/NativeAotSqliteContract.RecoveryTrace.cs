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
    public sealed record RecoveryTrace(StorageTrace Storage, int DatabaseWrites, int WritesAfterJournalAndParentSync, int JournalSyncs, int ParentSyncs)
    {
        public int ObserverLifetimeId { get; init; }
        public int SpillLine { get; init; }
        public long JournalBytes { get; init; }
        public string SpillHeaderBase64 { get; init; } = "";
        public bool GrantsOriginalTraceOrDurabilityAuthority => false;
    }
}
