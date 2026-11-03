using System.Collections.ObjectModel;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class LavPending(NativeAotLogUploadWaitRow call, NativeAotLogUploadWaitRow begin)
    {
        internal readonly NativeAotLogUploadWaitRow Call = call, Begin = begin;
        internal ulong Sequence = begin["sequence"];
        internal NativeAotLogUploadWaitRow? Wake;
        internal readonly List<NativeAotLogUploadWaitAttempt> Attempts = [];
    }
}
