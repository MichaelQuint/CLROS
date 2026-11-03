using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Value-only post-dispatch observation before pending user-return
    /// delivery. Signal nesting/top-frame come from the actual retained owner;
    /// the return envelope is the gateway's admitted output, including sigreturn
    /// user edits. This is neither frame access nor prepared-program authority.</summary>

    public readonly record struct LinuxCallReturnObservation(int RecordIndex, ulong Identity, ulong Ordinal, ulong SignalDepth, ulong SignalFrame, ulong BlockedSignals, ulong Rip, ulong Rsp, ulong Flags, ulong Fs, ulong Gs, ulong ValueBits);
}
