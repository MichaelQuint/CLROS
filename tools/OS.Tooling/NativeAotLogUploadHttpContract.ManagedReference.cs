using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OS.Tooling;
public static partial class NativeAotLogUploadHttpContract
{
    public sealed class ManagedReference
    {
        internal ManagedReference(string directory, NativeAotLogUploadContract.FileValues files, IEnumerable<ResponseFact> responses, IEnumerable<Control> controls)
        {
            Directory = directory;
            Files = files;
            Responses = Array.AsReadOnly(responses.ToArray());
            Controls = Array.AsReadOnly(controls.ToArray());
        }

        public string Directory { get; }
        public NativeAotLogUploadContract.FileValues Files { get; }
        public ReadOnlyCollection<ResponseFact> Responses { get; }
        public ReadOnlyCollection<Control> Controls { get; }
        public bool GrantsLinuxNativeGuestOrPhysicalPersistenceAuthority => false;
    }
}
