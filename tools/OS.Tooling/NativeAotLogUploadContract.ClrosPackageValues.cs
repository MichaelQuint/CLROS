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
    /// <summary>Immutable copied package/ELF geometry, never a prepared program.
    /// The caller supplies stable bytes for one synchronous inspection; no input
    /// buffer, file, source or process lease is retained. Exact identity does not
    /// admit instructions, execution, original publication or persistence.</summary>

    public sealed class ClrosPackageValues
    {
        internal ClrosPackageValues(string phase, string manifest, int bytes, string hash, NativeAotPayload.Inspection image)
        {
            Phase = phase;
            Manifest = manifest;
            PackageBytes = bytes;
            PackageSha256 = hash;
            Image = image;
            Arguments = Array.AsReadOnly(new[] { "--" + phase, "--root", "/data" });
        }

        public string Phase
        {
            get;
        }
        public string Manifest
        {
            get;
        }
        public int PackageBytes
        {
            get;
        }
        public string PackageSha256
        {
            get;
        }
        public NativeAotPayload.Inspection Image
        {
            get;
        }
        /// <summary>The three manifest arguments, excluding argv0.</summary>

        public ReadOnlyCollection<string> Arguments
        {
            get;
        }
        public bool InstructionIndexAdmitted => false;
        public bool ExecutionAuthority => false;
        public bool GrantsOriginalPublicationGuestOrPersistenceAuthority => false;
    }
}
