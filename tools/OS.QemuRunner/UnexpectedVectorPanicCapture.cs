using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

public static class UnexpectedVectorPanicCapture
{
    private const string PreambleBase64 = "G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==";
    private static readonly string Preamble = Encoding.ASCII.GetString(Convert.FromBase64String(PreambleBase64));
    private static readonly string[] Prefix = OS.Tooling.QemuContracts.M02TerminalPrefix.ToArray();
    private static readonly string[] MetadataNames = ["c", "d", "context", "callback", "captureRsp", "firmwareCs", "cs", "ss", "ds", "es", "tr", "gdtrBase", "gdtrLimit", "idtrBase", "idtrLimit", "tssAccess", "gateLow", "gateHigh", "normalizer0", "normalizer1", "trigger", "cr3", "entryCr3", "lar"];
    private static readonly string[] FaultNames = ["rip", "rsp", "rbp", "rflags", "cr2", "cr3", "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];
    private const string FaultHeading = "OS_SMOKE_FAIL subsystem=idt reason=unexpected-vector vector=32 errorCodePresent=false errorCode=0x0000000000000000";
    // -1 rejected, 0 a possible unfinished capture, 1 exact complete 76-record capture.
    public static int Evaluate(string text, out string prerequisiteText)
    {
        prerequisiteText = string.Empty;
        if (text.Length > SerialMarkerStateMachine.DefaultByteCapacity)
            return -1;
        int begin;
        if (text.StartsWith(Prefix[0], StringComparison.Ordinal))
            begin = 0;
        else if (text.StartsWith(Preamble, StringComparison.Ordinal))
            begin = Preamble.Length;
        else if (Prefix[0].StartsWith(text, StringComparison.Ordinal) || Preamble.StartsWith(text, StringComparison.Ordinal))
            return 0;
        else
            return -1;
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
        int completed = lines.Length - 1;
        int expectedCount = OS.Tooling.QemuContracts.M02UnexpectedCaptureRecordCount;
        if (completed > expectedCount || (completed == expectedCount && lines[expectedCount].Length != 0))
            return -1;
        for (int i = 0; i < Math.Min(completed, Prefix.Length); i++)
        {
            if (i == 3)
            {
                if (!DecimalRecord(lines[i], "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ", out _))
                    return -1;
            }
            else if (i == 9)
            {
                if (!DecimalRecord(lines[i], "BOOT_MEMORY_REGION_COUNT ", out _))
                    return -1;
            }
            else if (i != 10 && !string.Equals(lines[i], Prefix[i], StringComparison.Ordinal))
                return -1;
        }

        if (completed > 10 && !Map(lines))
            return -1;
        if (completed < Prefix.Length + 1)
            return 0;
        if (lines[Prefix.Length].Length != 648 || !HexFields(lines[Prefix.Length], "M02_UNEXPECTED_STATE", MetadataNames, out ulong[] m) || !Metadata(m))
            return -1;
        if (completed < expectedCount)
            return 0;
        if (!HexFields(lines[Prefix.Length + 1], FaultHeading, FaultNames, out ulong[] f))
            return -1;
        // RAX, RCX, RDX and R8 follow the closed entry bytes. Remaining GPRs and CR2
        // are observations, not fabricated constants or metadata authority.
        if (f[0] != m[20] + 2 || f[1] != m[1] + 28672 || (f[3] & 0x24702) != 2 || (f[3] & 0x8d5) != 0x44 || f[5] != m[21] || f[6] != 24 || f[8] != m[23] || f[9] != m[2] || f[12] != m[21])
            return -1;
        prerequisiteText = string.Join("\r\n", lines.AsSpan(0, Prefix.Length + 1).ToArray()) + "\r\n";
        return 1;
    }

    private static bool DecimalRecord(string text, string heading, out ulong value)
    {
        value = 0;
        if (!text.StartsWith(heading, StringComparison.Ordinal))
            return false;
        ReadOnlySpan<char> number = text.AsSpan(heading.Length);
        if (number.Length == 0 || (number.Length > 1 && number[0] == '0'))
            return false;
        foreach (char c in number)
            if (c < '0' || c > '9')
                return false;
        return ulong.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool Map(string[] lines)
    {
        if (!DecimalRecord(lines[3], "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ", out ulong descriptors) || !DecimalRecord(lines[9], "BOOT_MEMORY_REGION_COUNT ", out ulong regions))
            return false;
        string[] keys = ["regions", "conventionalPages", "conventionalBytes", "freeNodes", "freePages", "freeBytes", "pageSize", "statusCode"];
        string[] fields = lines[10].Split(' ');
        if (fields.Length != 10 || fields[0] != "BOOT_PAGE_ALLOCATOR_READY" || fields[1] != "status=Ready")
            return false;
        ulong[] v = new ulong[8];
        for (int i = 0; i < 8; i++)
            if (!DecimalRecord(fields[i + 2], keys[i] + "=", out v[i]))
                return false;
        return descriptors == regions && regions >= 1 && regions <= 4096 && v[0] == regions && v[3] >= 1 && v[3] <= regions && v[3] <= v[4] && v[4] <= ulong.MaxValue / 4096 && v[1] == v[4] && v[2] == v[4] * 4096 && v[2] == v[5] && v[6] == 4096 && v[7] == 0;
    }

    private static bool HexFields(string text, string heading, string[] names, out ulong[] values)
    {
        values = new ulong[names.Length];
        if (!text.StartsWith(heading + " ", StringComparison.Ordinal))
            return false;
        string[] fields = text[(heading.Length + 1)..].Split(' ');
        if (fields.Length != names.Length)
            return false;
        for (int i = 0; i < names.Length; i++)
        {
            string key = names[i] + "=0x";
            if (!fields[i].StartsWith(key, StringComparison.Ordinal) || fields[i].Length != key.Length + 16)
                return false;
            ReadOnlySpan<char> hex = fields[i].AsSpan(key.Length);
            foreach (char c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    return false;
            if (!ulong.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out values[i]))
                return false;
        }

        return true;
    }

    private static bool Metadata(ulong[] m)
    {
        ulong c = m[0], d = m[1];
        const ulong limit = 0x0000800000000000;
        if (c == 0 || d == 0 || (c & 4095) != 0 || (d & 4095) != 0 || c >= limit || d >= limit || 8192 > limit - c || 28672 > limit - d || (c < d + 28672 && d < c + 8192) || m[3] == 0 || m[3] >= limit || (m[3] >= c && m[3] < c + 8192) || (m[3] >= d && m[3] < d + 28672))
            return false;
        ulong target = c + 512;
        ulong gateLow = (target & 65535) | (8UL << 16) | (0x8eUL << 40) | ((target & 0xffff0000) << 32);
        return m[2] == d + 4096 && m[4] == d + 28488 && m[5] != 0 && m[5] <= 65535 && (m[5] & 7) == 0 && m[6] == 8 && m[7] == 16 && m[8] == 16 && m[9] == 16 && m[10] == 24 && m[11] == d + 4120 && m[12] == 39 && m[13] == d && m[14] == 4095 && m[15] == 0x8b && m[16] == gateLow && m[17] == target >> 32 && m[18] == 0x00002068006a006a && m[19] == 0x909000000df2e900 && m[20] == c + 5056 && m[21] != 0 && (m[21] & ~0x000fffffffffffffUL) == 0 && (m[21] & 0xfe7) == 0 && (m[21] & ~4095UL) != 0 && m[22] == m[21] && m[23] <= uint.MaxValue && (m[23] & 0x00f0fe00) == 0x00a09a00;
    }
}
