using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotPayload
{
    public sealed record Inspection(ulong Entry, int ProgramHeaderCount, IReadOnlyList<Segment> Loads, Segment? Tls, string Sha256);
}
