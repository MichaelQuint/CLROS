using System.Collections.ObjectModel;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One original source5 CALL/BEGIN and all its copied attempts.
    /// No instruction, syscall argument, path, copy or scheduler is admitted.</summary>

    public sealed class NativeAotLogUploadWaitOperation
    {
        internal NativeAotLogUploadWaitOperation(NativeAotLogUploadWaitRow call, NativeAotLogUploadWaitRow begin, List<NativeAotLogUploadWaitAttempt> attempts)
        {
            Call = call;
            Begin = begin;
            Attempts = Array.AsReadOnly(attempts.ToArray());
        }

        public NativeAotLogUploadWaitRow Call
        {
            get;
        }
        public NativeAotLogUploadWaitRow Begin
        {
            get;
        }
        public IReadOnlyList<NativeAotLogUploadWaitAttempt> Attempts
        {
            get;
        }
    }
}
