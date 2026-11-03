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
    public sealed record TraceRow(string Source, int Line, string Raw, TraceKind Kind, string Syscall, string Arguments, string Result);
}
