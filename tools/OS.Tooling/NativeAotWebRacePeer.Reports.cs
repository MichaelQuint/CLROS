using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class NativeAotWebRacePeer
{
    private sealed record Reports(AuthorityFile Selected, AuthorityFile Attempts, AuthorityFile Wire, WebRaceRendezvous.Digests Digests, QemuTranscripts.WebSignalRaceSelection Selection, QemuTranscripts.WebSignalRaceHostHttpInspection Http);
}
