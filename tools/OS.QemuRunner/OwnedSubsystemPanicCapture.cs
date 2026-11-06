using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

// The explicit M03 terminal control uses the common measured-quiet/cleanup path.
// The script separately validates the complete first-exit/ACPI numeric grammar.
public static class OwnedSubsystemPanicCapture
{
    private static readonly string[] Prefix = OS.Tooling.QemuContracts.M03FirstExitRequiredMarkers.ToArray();
    private static readonly string[] FaultNames = ["rip", "rsp", "rbp", "rflags", "cr2", "cr3", "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];
    private const string FaultHeading = "OS_SMOKE_FAIL subsystem=boot reason=exit-fault vector=14 errorCodePresent=true errorCode=0x0000000000000000";
    private const ulong LowEnd = 0x0000800000000000;
    public static int InvalidBytes(string text)
    {
        int count = 0;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
            if (b != 9 && b != 10 && b != 13 && (b < 32 || b > 126))
                count++;
        return count;
    }

    // -1 rejected, 0 unfinished, 1 exact terminal capture; never a normal boot pass.
    public static int Evaluate(string text, out string prerequisiteText)
    {
        prerequisiteText = string.Empty;
        if (text.Length > 32768)
            return -1;
        if (text.Length < 304)
            return 0;
        byte[] preamble = Encoding.UTF8.GetBytes(text[..304]);
        if (preamble.Length != 304 || Convert.ToHexString(SHA256.HashData(preamble)) != OS.Tooling.QemuContracts.FirmwarePrefixSha256)
            return -1;
        string body = text[304..];
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
        if (complete > 77 || (complete == 77 && lines[77].Length != 0))
            return -1;
        for (int i = 0; i < Math.Min(complete, 66); i++)
        {
            bool variable = i is 3 or 9 or 10 or >= 19 and <= 24;
            if (variable ? !lines[i].StartsWith(Prefix[i] + " ", StringComparison.Ordinal) : lines[i] != Prefix[i])
                return -1;
        }

        if (complete > 66 && lines[66] != "TEST_START m03.010")
            return -1;
        for (int i = 67; i < Math.Min(complete, 76); i++)
            if (!Row(lines[i], i - 67, out _))
                return -1;
        if (complete < 77)
            return 0;
        if (!ValidateRecords(lines[..77]))
            return -1;
        prerequisiteText = string.Join("\r\n", lines.AsSpan(0, 76).ToArray()) + "\r\n";
        return 1;
    }

    private static bool ValidateRecords(string[] lines)
    {
        var rows = new ulong[9][];
        for (int i = 0; i < 9; i++)
            if (!Row(lines[67 + i], i, out rows[i]))
                return false;
        ulong[] allocation = rows[0], descriptor = rows[6], armed = rows[7], entered = rows[8];
        if (!Extent(allocation[0], 312, 8) || !Extent(allocation[2], 8192, 4096) || allocation[1] != allocation[2] || allocation[3] != 2 || allocation[4] != 2 || allocation[5] != 8192 || allocation[6] != 0 || allocation[7] != 0)
            return false;
        ulong workspace = rows[1][0], copied = rows[1][1], offset = 0;
        if (!Extent(workspace, 1, 8) || copied < 16 || !Extent(copied - 16, 266272, 8))
            return false;
        string[] roles = ["rsdp", "root", "mcfg", "apic", "hpet"];
        string[] names = ["physical", "owned", "offset", "length", "signature", "rootIndex", "compared", "mismatches"];
        for (int i = 0; i < 5; i++)
        {
            ulong[] row = rows[i + 1];
            if (!DecimalFields(lines[19 + i], "CLROS_M03_002_TABLE role=" + roles[i], names, out ulong[] prior) || row[0] != workspace || row[1] != copied || row[2] != prior[0] || copied + offset != prior[1] || row[3] != offset || row[3] != prior[2] || row[4] != prior[3] || row[5] != prior[4] || row[6] != prior[5] || row[7] != row[4] || row[7] != prior[6] || prior[7] != 0 || row[4] == 0 || row[4] > (i == 0 ? 4096UL : 65536UL))
                return false;
            offset += row[4];
        }

        if (offset > 266240 || !Extent(descriptor[0], 105 * 4096, 4096) || !Extent(descriptor[1], 55 * 4096, 4096) || descriptor[1] >= 0x100000000UL - 55 * 4096 || !Extent(descriptor[2], 3 * 4096, 4096) || descriptor[3] != descriptor[0] + 4096 || descriptor[4] != descriptor[0] + 8192 || descriptor[5] != descriptor[0] + 12288 || descriptor[6] != 24 || descriptor[7] <= descriptor[0] + 22 * 4096 + 8 || descriptor[7] >= descriptor[0] + 86 * 4096 - 16 || (descriptor[7] & 15) != 8)
            return false;
        if (armed[0] != descriptor[0] + 21 * 4096 || armed[1] != descriptor[1] || armed[2] != descriptor[0] + 1024 || armed[3] != descriptor[2] || armed[4] != descriptor[0] || armed[5] != descriptor[0] + 22 * 4096 + 8 || armed[6] != descriptor[0] + 86 * 4096 - 16 || armed[7] != descriptor[0] + 98 * 4096 - 16 || entered[0] != armed[0] || entered[1] != armed[1] || entered[2] != armed[2] || entered[4] != descriptor[0] + 94 * 4096 + 8 || entered[5] != armed[7] || entered[3] < entered[4] || entered[3] > entered[5] - 184 || (entered[3] & 7) != 0 || entered[6] != 14 || entered[7] != 0)
            return false;
        if (!HexFields(lines[76], FaultHeading, FaultNames, out ulong[] fault))
            return false;
        return Extent(fault[0], 1, 1) && fault[1] >= armed[5] && fault[1] < armed[6] && (fault[3] & 0x202) == 2 && fault[4] == armed[0] && fault[5] == armed[1];
    }

    private static bool Extent(ulong start, ulong bytes, ulong alignment) => start != 0 && start < LowEnd && (start & (alignment - 1)) == 0 && bytes < LowEnd - start;
    private static bool Row(string line, int stage, out ulong[] values) => HexFields(line, "M03_OWNED " + stage.ToString(System.Globalization.CultureInfo.InvariantCulture), [], out values, 8);
    private static bool HexFields(string line, string heading, string[] names, out ulong[] values, int positional = 0)
    {
        values = new ulong[positional == 0 ? names.Length : positional];
        if (!line.StartsWith(heading + " ", StringComparison.Ordinal))
            return false;
        string[] fields = line[(heading.Length + 1)..].Split(' ');
        if (fields.Length != values.Length)
            return false;
        for (int i = 0; i < fields.Length; i++)
        {
            string key = positional == 0 ? names[i] + "=0x" : "0x";
            if (!fields[i].StartsWith(key, StringComparison.Ordinal) || fields[i].Length != key.Length + 16)
                return false;
            foreach (char c in fields[i].AsSpan(key.Length))
                if (!(c is >= '0' and <= '9' or >= 'A' and <= 'F'))
                    return false;
            if (!ulong.TryParse(fields[i].AsSpan(key.Length), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out values[i]))
                return false;
        }

        return true;
    }

    private static bool DecimalFields(string line, string heading, string[] names, out ulong[] values)
    {
        values = new ulong[names.Length];
        if (!line.StartsWith(heading + " ", StringComparison.Ordinal))
            return false;
        string[] fields = line[(heading.Length + 1)..].Split(' ');
        if (fields.Length != names.Length)
            return false;
        for (int i = 0; i < names.Length; i++)
        {
            string key = names[i] + "=";
            if (!fields[i].StartsWith(key, StringComparison.Ordinal))
                return false;
            ReadOnlySpan<char> digits = fields[i].AsSpan(key.Length);
            if (digits.Length == 0 || (digits.Length > 1 && digits[0] == '0'))
                return false;
            foreach (char c in digits)
                if (c < '0' || c > '9')
                    return false;
            if (!ulong.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out values[i]))
                return false;
        }

        return true;
    }
}
