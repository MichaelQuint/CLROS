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
    public sealed record Segment(uint Type, uint Flags, ulong Offset, ulong Address, ulong FileSize, ulong MemorySize, ulong Alignment);
}
