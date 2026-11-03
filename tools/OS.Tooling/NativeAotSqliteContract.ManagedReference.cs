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
    public sealed class ManagedReference
    {
        internal ManagedReference(string directory, Transcript transcript, Transcript verification, string databaseHash)
        {
            Directory = directory;
            Values = transcript;
            Verification = verification;
            DatabaseSha256 = databaseHash;
        }

        public string Directory
        {
            get;
        }
        public Transcript Values
        {
            get;
        }
        public Transcript Verification
        {
            get;
        }
        public string DatabaseSha256
        {
            get;
        }
        public bool GrantsLinuxNativeAotGuestOrDurabilityAuthority => false;
    }
}
