using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// Ordinary BCL file workload. Scalar-write/metadata/advisory-lock requirements
// need a fresh pinned Linux NativeAOT trace and later CLROS source5 admission.
// Flush(true) requests policy only; this app cannot attest physical durability.
internal static class Program
{
    private const string LogName = "LOG.TXT", UploadName = "UPLOAD.BIN";
    private const int UploadBytes = 5126, FirstChunk = 4094;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START m10.027");
        try
        {
            Require(args is ["--write" or "--verify", "--root", _], "arguments");
            string phase = args[0] == "--write" ? "write" : "verify", supplied = args[2];
            Require(Path.IsPathFullyQualified(supplied), "absolute_root_required");
            string root = Path.GetFullPath(supplied), log = Path.Combine(root, LogName), upload = Path.Combine(root, UploadName);
            Console.WriteLine("LOG_UPLOAD_PHASE " + phase);
            if (phase == "write") Write(log, upload);
            byte[] logBytes = LogBytes(), uploadBytes = UploadPayload();
            Verify(log, logBytes); Verify(upload, uploadBytes);
            Console.WriteLine("LOG_UPLOAD_POLICY append=managed-reopen io=synchronous buffering=0 flushRequested=" + (phase == "write" ? "true" : "false"));
            Console.WriteLine("LOG_UPLOAD_FILES logBytes=" + logBytes.Length.ToString(CultureInfo.InvariantCulture) + " logSha256=" + Hash(logBytes) +
                " uploadBytes=" + uploadBytes.Length.ToString(CultureInfo.InvariantCulture) + " uploadSha256=" + Hash(uploadBytes));
            Console.WriteLine("TEST_PASS m10.027"); return 0;
        }
        catch (Exception error)
        { Console.WriteLine("TEST_FAIL m10.027 " + error.GetType().Name); return 1; }
    }

    private static FileStream Open(string path, FileMode mode, FileAccess access)
    {
        var options = new FileStreamOptions { Mode = mode, Access = access, Share = FileShare.ReadWrite, BufferSize = 0, Options = FileOptions.None };
        if (OperatingSystem.IsLinux() && mode == FileMode.Append) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static void Write(string log, string upload)
    {
        // Both files must already exist empty. These read-only opens occur
        // before any payload write; nonempty files are never truncated.
        using (var originalLog = Open(log, FileMode.Open, FileAccess.Read))
        using (var originalUpload = Open(upload, FileMode.Open, FileAccess.Read))
            Require(originalLog.Length == 0 && originalUpload.Length == 0, "empty_inputs_required");
        for (int group = 0; group < 2; group++)
        {
            using var output = Open(log, FileMode.Append, FileAccess.Write);
            for (int row = group * 6 + 1; row <= group * 6 + 6; row++) output.Write(LogRecord(row));
            output.Flush(flushToDisk: true);
        }
        using (var output = Open(upload, FileMode.Open, FileAccess.Write))
        {
            byte[] bytes = UploadPayload(); output.Write(bytes.AsSpan(0, FirstChunk)); output.Write(bytes.AsSpan(FirstChunk));
            output.Flush(flushToDisk: true);
        }
    }

    private static void Verify(string path, byte[] expected)
    {
        using var input = Open(path, FileMode.Open, FileAccess.Read); Require(input.Length == expected.Length, "file_length");
        byte[] buffer = new byte[521]; int offset = 0;
        while (offset < expected.Length)
        {
            int read = input.Read(buffer.AsSpan(0, Math.Min(buffer.Length, expected.Length - offset)));
            Require(read > 0 && buffer.AsSpan(0, read).SequenceEqual(expected.AsSpan(offset, read)), "file_bytes"); offset += read;
        }
        Require(input.ReadByte() == -1, "file_eof");
    }

    private static byte[] LogRecord(int row) => Utf8.GetBytes("CLROS_LOG v1 sequence=" + row.ToString("D2", CultureInfo.InvariantCulture) + " message=CLROS ready — café\n");
    private static byte[] LogBytes()
    {
        using var bytes = new MemoryStream(); for (int row = 1; row <= 12; row++) bytes.Write(LogRecord(row)); return bytes.ToArray();
    }
    private static byte[] UploadPayload()
    {
        byte[] bytes = new byte[UploadBytes]; for (int i = 0; i < bytes.Length; i++) bytes[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 3) ^ (i >> 9))); return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
