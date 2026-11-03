using System.Globalization;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>A selected HTTP role retains original request/response CALL
    /// coverage. HostCancellation has request-only coverage and Response=null;
    /// it is event metadata, never a native cancellation, EOF or HTTP reply.</summary>

    public sealed record WebSignalRaceRoleSample(int Sample, string Kind, int ExchangeId, bool HostCancellation, WebSignalRaceHttpSyscallInterval Request, WebSignalRaceHttpSyscallInterval? Response);
}
