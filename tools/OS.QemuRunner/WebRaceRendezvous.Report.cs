using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Serialization;

namespace OS.Tools.QemuRunner;
public static partial class WebRaceRendezvous
{
    /// <summary>Completed coordination values, without execution or file authority.</summary>

    public sealed record Report(int Port, int QemuProcessId, Digests Digests, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EventOwned = false);
}
