using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Serialization;

namespace OS.Tools.QemuRunner;
public static partial class WebRaceRendezvous
{
    /// <summary>Copied runner-supplied forward and PID; neither is authenticated by IPC.</summary>

    public sealed record Ready(int Port, int QemuProcessId, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EventOwned = false);
}
