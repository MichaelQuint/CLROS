using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotLogUploadContract
{
    public sealed record ArtifactIdentity(string Path, long Bytes, string Sha256);
}
