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
    public sealed class ManagedRecovery
    {
        internal ManagedRecovery(string directory, RecoveryJournal journal, ProcessResult writer, string recoveredHash)
        {
            Directory = directory;
            Journal = journal;
            Writer = writer;
            RecoveredDatabaseSha256 = recoveredHash;
        }

        public string Directory { get; }
        public RecoveryJournal Journal { get; }
        public ProcessResult Writer { get; }
        public string RecoveredDatabaseSha256 { get; }
        public bool GrantsLinuxNativeAotGuestOrPowerLossDurabilityAuthority => false;
    }
}
