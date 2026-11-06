using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

// This finite parser belongs only to the explicit expected-panic experiment.
// Generic serial marker completion/failure behavior remains unchanged.
public static class UnhandledExceptionCapture
{
    public const string Failure = "TEST_FAIL m04.021.unhandled unhandled-exception";
    public const string Terminal = "OS_SMOKE_FAIL subsystem=runtime reason=unhandled-exception";
    public const int RecordCount = 73;
    public const string PrerequisiteTerminal = "M04_UNHANDLED 7";
    // Runner framing and exact terminal; OS.Tooling independently admits every
    // prerequisite numeric association before treating exit 19 as qualification.
    public static int Evaluate(string text, out string prerequisites)
    {
        prerequisites = "";
        if (text.Length > SerialMarkerStateMachine.DefaultByteCapacity)
            return -1;
        const string first = "TEST_START efi.main";
        int begin;
        if (text.StartsWith(first, StringComparison.Ordinal))
            begin = 0;
        else
        {
            if (first.StartsWith(text, StringComparison.Ordinal) || text.Length < OS.Tooling.QemuContracts.FirmwarePrefixBytes)
                return 0;
            begin = OS.Tooling.QemuContracts.FirmwarePrefixBytes;
            byte[] prefix = Encoding.ASCII.GetBytes(text[..begin]);
            if (Convert.ToHexString(SHA256.HashData(prefix)) != OS.Tooling.QemuContracts.FirmwarePrefixSha256)
                return -1;
        }

        string body = text[begin..];
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '\r')
            {
                if (i + 1 < body.Length && body[i + 1] != '\n')
                    return -1;
            }
            else if (c == '\n')
            {
                if (i == 0 || body[i - 1] != '\r')
                    return -1;
            }
            else if (c < 32 || c > 126)
                return -1;
        }

        string[] lines = body.Split("\r\n", StringSplitOptions.None);
        int complete = lines.Length - 1;
        if (complete > RecordCount || complete == RecordCount && lines[^1].Length != 0)
            return -1;
        if (complete > 0 && lines[0] != first)
            return -1;
        if (complete < RecordCount)
            return 0;
        if (lines[62] != "TEST_START m04.021.unhandled" || lines[71] != Failure || lines[72] != Terminal)
            return -1;
        for (int i = 0; i < 8; i++)
            if (!System.Text.RegularExpressions.Regex.IsMatch(lines[63 + i], "\\AM04_UNHANDLED " + i + "(?: 0x[0-9A-F]{16}){4}\\z"))
                return -1;
        for (int i = 0; i < 71; i++)
            if (lines[i].StartsWith("TEST_FAIL", StringComparison.Ordinal) || lines[i].StartsWith("OS_SMOKE_FAIL", StringComparison.Ordinal) || lines[i] == "OS_SMOKE_PASS" || lines[i] == "TEST_START boot.sequence")
                return -1;
        prerequisites = text[..^(Failure.Length + Terminal.Length + 4)];
        return 1;
    }
}
