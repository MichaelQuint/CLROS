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
    public sealed class UpstreamSource
    {
        private readonly byte[] source, header;
        internal UpstreamSource(byte[] source, byte[] header)
        {
            this.source = source;
            this.header = header;
        }

        public string ArchiveSha256 => SourceArchiveSha256;
        public string Csha256 => SourceCsha256;
        public string HeaderSha256 => SourceHeaderSha256;
        public string EngineSourceId => SourceId;

        public byte[] CopyC() => source.ToArray();
        public byte[] CopyHeader() => header.ToArray();
        public bool GrantsNativeBuildOrExecutionAuthority => false;
    }
}
