using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One copied bounded HTTP/1.1 prefix. Offsets are zero-based in
    /// the supplied byte sequence; End is exclusive. HeaderEnd=-1 means the
    /// header is unfinished. State=complete alone denotes a framed message.
    /// BodyBase64 contains only decoded entity bytes actually supplied, including
    /// an unfinished chunk's bytes. DeclaredLength=-1 means chunked or unknown.
    /// Headers are copied/read-only. Values grant no I/O, flow or guest authority.</summary>

    public sealed record ServiceQuotaHttpPrefix(int Start, int HeaderEnd, int End, string First, IReadOnlyDictionary<string, string> Headers, string Framing, int DeclaredLength, string State, string BodyBase64);
}
