using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inspection = OS.Tooling.NativeAotPayload.Inspection;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    public sealed record QualificationReceipt(string Schema, string Sdk, string Runtime, IReadOnlyDictionary<string, string> Sources, IReadOnlyDictionary<string, string> Outputs);
}
