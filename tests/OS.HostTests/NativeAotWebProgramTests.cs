using System.Buffers.Binary;
using System.Text;
using OS.Manifest;
using OS.Tooling;

internal static class NativeAotWebProgramTests
{
    private static byte[] Image()
    {
        byte[] original = LinuxElfPackageTests.CapturePackage(); var view = LinuxElfPackage.Validate(original);
        byte[] elf = original.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
        elf[512] = 0x0f; elf[513] = 0x05; elf[514] = 0xc3; return elf;
    }

    [HostTest("nativeaot.web program offline report binds exact executable rows without prepared authority")]
    private static void OfflineReport()
    {
        byte[] elf = Image(); const string dump = "400200: 0f 05\tsyscall\n400202: c3\tretq\n";
        var report = NativeAotWebProgram.Inspect(elf, dump);
        HostCheck.Equal(2, report.InstructionCount); HostCheck.Equal(1, report.SyscallCount);
        HostCheck.Equal(64, report.Sha256.Length);
        string root = Path.Combine(Path.GetTempPath(), "clros-nonexistent-web-" + Guid.NewGuid().ToString("N"));
        bool rejected = false;
        try { NativeAotWebProgram.Create(root, NativeAotWebPackage.Create(elf, "{}"u8), dump); }
        catch (InvalidDataException error) { rejected = error.Message == "web_package_identity"; }
        HostCheck.True(rejected); // Even a valid small package/report cannot authorize the original web ELF.
    }

    [HostTest("nativeaot.web program rejects operand gaps data bytes and altered syscall rows")]
    private static void InvalidRows()
    {
        byte[] elf = Image();
        foreach (string dump in new[] { "400200: 0f 04\tsyscall\n", "400200: 0f 05\tretq\n",
            "400202: c3\tretq\n400201: 05\tbyte\n", "400fff: c3\tretq\n", "400200: 0f 05\tsyscall\n400200: 0f 05\tsyscall\n", "" })
        {
            bool rejected = false;
            try { NativeAotWebProgram.Inspect(elf, dump); }
            catch (InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
        }
        // File-backed read-only ELF data cannot gain executable-start authority.
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(68), 4);
        bool dataRejected = false;
        try { NativeAotWebProgram.Inspect(elf, "400200: 0f 05\tsyscall\n"); }
        catch (InvalidDataException) { dataRejected = true; }
        HostCheck.True(dataRejected);
    }
}
