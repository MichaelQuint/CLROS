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
    /// <summary>Original Linux reference handles, transferred only after both
    /// fresh publications, real HTTP, natural exit and independent verification
    /// complete. Dispose releases evidence/input handles, never a live process.
    /// Copied trace projection can remain incomplete despite successful native
    /// processes. This result grants no CLROS, device-FLUSH or persistence scope.</summary>

    public sealed class LinuxReference : IDisposable
    {
        private AuthorityFiles? held;
        private NativeAotLogUploadContract.LinuxReference? verifier;
        internal LinuxReference(string directory, NativeAotPayload.Inspection image, NativeAotLogUploadContract.LinuxReference verifier, NativeAotLogUploadContract.FileValues files, IEnumerable<ResponseFact> responses, NativeAotLogUploadContract.TraceValues writeTrace, NativeAotLogUploadContract.TraceValues verifyTrace, AuthorityFiles held, IEnumerable<NativeAotLogUploadContract.ArtifactIdentity> artifacts)
        {
            Directory = directory;
            HttpImage = image;
            VerifierImage = verifier.Image;
            this.verifier = verifier;
            Files = files;
            Responses = Array.AsReadOnly(responses.ToArray());
            WriteTrace = writeTrace;
            VerifyTrace = verifyTrace;
            this.held = held;
            Artifacts = Array.AsReadOnly(artifacts.ToArray());
        }

        public string Directory
        {
            get;
        }
        public NativeAotPayload.Inspection HttpImage
        {
            get;
        }
        public NativeAotPayload.Inspection VerifierImage
        {
            get;
        }
        public NativeAotLogUploadContract.FileValues Files
        {
            get;
        }
        public ReadOnlyCollection<ResponseFact> Responses
        {
            get;
        }
        public NativeAotLogUploadContract.TraceValues WriteTrace
        {
            get;
        }
        public NativeAotLogUploadContract.TraceValues VerifyTrace
        {
            get;
        }
        public ReadOnlyCollection<NativeAotLogUploadContract.ArtifactIdentity> Artifacts
        {
            get;
        }
        public bool CompleteTraceProjection => WriteTrace.CompleteTraceProjection && VerifyTrace.CompleteTraceProjection;
        public bool GrantsGuestDeviceFlushOrPhysicalPersistenceAuthority => false;

        public void VerifyUnchanged()
        {
            ObjectDisposedException.ThrowIf(held is null, this);
            verifier!.VerifyUnchanged();
            held.VerifyUnchanged();
        }

        public void Dispose()
        {
            held?.Dispose();
            held = null;
            verifier?.Dispose();
            verifier = null;
        }
    }
}
