using System.Globalization;
using System.Text.RegularExpressions;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only native send observations for the fifth, large-response
    /// connection. Full syscall/mapping/generation replay and exact packet/runner
    /// joins remain mandatory; these counts grant no guest authority.</summary>

    public readonly record struct WebVectorCallInspection(int Sends, int ScatterSends, int ShortSends, int BlockedSends, ulong Bytes);
}
