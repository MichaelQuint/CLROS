using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Serialization;

namespace OS.Tools.QemuRunner;

public static partial class WebRaceRendezvous
{
    /// <summary>Canonical uppercase report hashes to recheck against independently held originals.</summary>

    public sealed record Digests(string SelectedSha256, string AttemptsSha256, string WireSha256);
}
