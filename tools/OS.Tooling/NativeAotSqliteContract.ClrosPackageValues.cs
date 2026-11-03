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
    /// <summary>Immutable copied package/ELF values for one exact app mode.
    /// The64MiB/1.5MiB/16thread/32fd envelope is selected candidate policy, not
    /// measured resource fit. Arguments name a database file, never a --root.
    /// Database/seed require absent inputs; recovery-spill intentionally waits
    /// for an external owned kill. No image, process or recovery is admitted.</summary>

    public sealed class ClrosPackageValues
    {
        internal ClrosPackageValues(string mode, string path, string manifest, int bytes, string hash, NativeAotPayload.Inspection image)
        {
            Mode = mode;
            DatabasePath = path;
            Manifest = manifest;
            PackageBytes = bytes;
            PackageSha256 = hash;
            Image = image;
            Arguments = Array.AsReadOnly(new[] { "--" + mode, path });
        }

        public string Mode { get; }
        public string DatabasePath { get; }
        public string Manifest { get; }
        public int PackageBytes { get; }
        public string PackageSha256 { get; }
        public NativeAotPayload.Inspection Image { get; }
        public ReadOnlyCollection<string> Arguments { get; }
        public bool ResourceEnvelopeMeasured => false;
        public bool InstructionIndexAdmitted => false;
        public bool ExecutionAuthority => false;
        public bool GrantsOriginalPublicationGuestRecoveryOrPersistenceAuthority => false;
    }
}
