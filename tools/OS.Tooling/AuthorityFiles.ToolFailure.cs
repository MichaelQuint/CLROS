using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;

public sealed class ToolFailure(string reason, string detail = "") : Exception(reason + (detail.Length == 0 ? "" : " " + detail))
{
    public string Reason { get; } = reason;
    public string Detail { get; } = detail;
}
