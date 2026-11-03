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
    /// <summary>Original joined Linux reference result. Owns retained input and
    /// evidence handles until disposed. Root must serialize its builder lease;
    /// this does not admit an instruction index, package or CLROS workload.</summary>

    public sealed class LinuxReference : IDisposable
    {
        private AuthorityFiles? held;
        internal LinuxReference(string directory, NativeAotPayload.Inspection image, Transcript write, Transcript verify, TraceValues writeTrace, TraceValues verifyTrace, AuthorityFiles held, IEnumerable<AuthorityFile> artifacts)
        {
            Directory = directory;
            Image = image;
            Write = write;
            Verify = verify;
            WriteTrace = writeTrace;
            VerifyTrace = verifyTrace;
            this.held = held;
            Artifacts = Array.AsReadOnly(artifacts.Select(file => new ArtifactIdentity(file.Path, file.Length, file.Sha256)).ToArray());
            ArtifactPaths = Array.AsReadOnly(Artifacts.Select(file => file.Path).ToArray());
        }

        public string Directory { get; }
        public NativeAotPayload.Inspection Image { get; }
        public Transcript Write { get; }
        public Transcript Verify { get; }
        public TraceValues WriteTrace { get; }
        public TraceValues VerifyTrace { get; }
        public ReadOnlyCollection<ArtifactIdentity> Artifacts { get; }
        public ReadOnlyCollection<string> ArtifactPaths { get; }
        public bool CompleteTraceProjection => WriteTrace.CompleteTraceProjection && VerifyTrace.CompleteTraceProjection;
        public bool GrantsGuestHttpLockFlushOrPersistenceAuthority => false;

        public void VerifyUnchanged()
        {
            ObjectDisposedException.ThrowIf(held is null, this);
            held.VerifyUnchanged();
        }

        public void Dispose()
        {
            held?.Dispose();
            held = null;
        }
    }
}
