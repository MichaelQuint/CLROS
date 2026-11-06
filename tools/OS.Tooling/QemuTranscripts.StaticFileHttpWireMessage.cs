using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied FIFO message coordinates and original first-byte records.
    /// RemotePort is the guest TCP tuple, never the peer's host socket identity.
    /// Raw response headers and decoded entity bytes are copied as Base64.</summary>

    public sealed record StaticFileHttpWireMessage(uint RemotePort, int MessageIndex, int RequestFirstRecord, int ResponseLastRecord, string RequestBase64, string ResponseHeaderBase64, string BodyBase64, string ResponseSha256);
}
