using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Independent complete-capture controls and ordinary packet view.
    /// The view is an internal partition of borrowed bytes, never a replacement
    /// artifact. Process, source, serial and native ownership remain separate.</summary>

    public sealed record ServiceControlInspection(byte[] OrdinaryCapture, IReadOnlyList<int> Ports, ServiceHttpPacketInspection Http, int ControlledRecords, int PendingResets, bool BoundedFlight = false);
}
