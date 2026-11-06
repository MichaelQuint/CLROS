using System.Collections.ObjectModel;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One copied wake/completion and optional immediate repark. A
    /// completion's outputBytes is the captured prefix, not a returned count.</summary>

    public sealed record NativeAotLogUploadWaitAttempt(NativeAotLogUploadWaitRow Wake, NativeAotLogUploadWaitRow Complete, NativeAotLogUploadWaitRow? Repark);
}
