using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotLogUploadContract
{
    public sealed class ManagedReference
    {
        internal ManagedReference(string directory, Transcript write, Transcript verify, IEnumerable<string> artifacts)
        {
            Directory = directory;
            Write = write;
            Verify = verify;
            Artifacts = Array.AsReadOnly(artifacts.ToArray());
        }

        public string Directory { get; }
        public Transcript Write { get; }
        public Transcript Verify { get; }
        public ReadOnlyCollection<string> Artifacts { get; }
        public bool GrantsLinuxNativeAotGuestOrPersistenceAuthority => false;
    }
}
