using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record StaticPeerRow(int Ordinal, string Method, string Path, int Status, Dictionary<string, string> Request, Dictionary<string, string> Response, byte[] Body, long Started, long Finished);
}
