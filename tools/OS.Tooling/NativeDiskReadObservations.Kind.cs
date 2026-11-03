using System.Globalization;

namespace OS.Tooling;
public static partial class NativeDiskReadObservations
{
    public enum Kind : byte
    {
        Submitted,
        CompletionValidated,
        Copied,
        CopyChunk
    }
}
