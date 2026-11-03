using System.Globalization;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Structurally checked external controls from one runner summary.
    /// These borrowed values require independent original serial, packet,
    /// runtime, process and source joins before they qualify a guest.</summary>

    public sealed record WebControlInspection(WebSocketControls.Report Sockets, WebFrameControl.Report Frames);
}
