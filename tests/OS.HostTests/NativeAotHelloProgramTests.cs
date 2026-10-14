using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotHelloProgramTests
{
    private const string Rows = "  400200: b8 0f 05 00 00\tmovl $0x50f, %eax\n" +
        "  400205: 0f 05\tsyscall\n  400207: c3\tretq\n";

    [HostTest("nativeaot.hello-program instruction index binds ordered executable bytes and excludes raw operand syscall patterns")]
    private static void IndependentIndex()
    {
        byte[] elf = Elf();
        var report = NativeAotHelloProgram.Inspect(elf, Rows);
        HostCheck.True(report.InstructionCount == 3 && report.SyscallCount == 1);
        // Independent serialization vector: address as UInt64 LE, length, exact
        // bytes, syscall classification. A raw byte scan would count two SYSCALLs.
        using MemoryStream vector = new();
        vector.Write(Encoding.ASCII.GetBytes("CLROS_HELLO_INSTRUCTIONS_V1\n"));
        using (BinaryWriter writer = new(vector, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(0x400200UL); writer.Write((byte)5); writer.Write(new byte[] { 0xb8, 0x0f, 0x05, 0, 0 }); writer.Write((byte)0);
            writer.Write(0x400205UL); writer.Write((byte)2); writer.Write(new byte[] { 0x0f, 0x05 }); writer.Write((byte)1);
            writer.Write(0x400207UL); writer.Write((byte)1); writer.Write((byte)0xc3); writer.Write((byte)0);
        }
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(vector.ToArray())), report.Sha256);
        string formatted = "different/path/Hello: file format elf64-x86-64\nDisassembly of section .text:\n" +
            "0000000000400200 <entry>:\n" + Rows.Replace("\n", "\r\n", StringComparison.Ordinal);
        HostCheck.Equal(report, NativeAotHelloProgram.Inspect(elf, formatted));
    }

    [HostTest("nativeaot.hello-program rejects malformed overlap unknown opcode and nonexecutable backing rows")]
    private static void InvalidRows()
    {
        byte[] elf = Elf();
        Reject(() => NativeAotHelloProgram.Inspect(elf, ""), "hello_instruction_text");
        Reject(() => NativeAotHelloProgram.Inspect(elf, "headers only\n"), "hello_instruction_empty");
        Reject(() => NativeAotHelloProgram.Inspect(elf, new string('x', 1025)), "hello_instruction_line");
        Reject(() => NativeAotHelloProgram.Inspect(elf, Rows + "\0"), "hello_instruction_text");
        Reject(() => NativeAotHelloProgram.Inspect(elf, "400200: b8"), "hello_instruction_row");
        foreach (string bytes in new[] { "b", "zz", "b8f", string.Join(' ', Enumerable.Repeat("90", 16)) })
            Reject(() => NativeAotHelloProgram.Inspect(elf, "400200: " + bytes + "\tmovl\n"), "hello_instruction_bytes");
        foreach (string decoded in new[] { "<unknown>", ".byte 0xb8", "" })
            Reject(() => NativeAotHelloProgram.Inspect(elf, "400200: b8\t" + decoded + "\n"), "hello_instruction_decode");
        Reject(() => NativeAotHelloProgram.Inspect(elf, Rows.Replace("0f 05\tsyscall", "0f 05\tnop", StringComparison.Ordinal)), "hello_instruction_syscall");
        Reject(() => NativeAotHelloProgram.Inspect(elf, "400207: c3\tsyscall\n"), "hello_instruction_syscall");
        Reject(() => NativeAotHelloProgram.Inspect(elf, Rows + "400207: c3\tretq\n"), "hello_instruction_order");
        Reject(() => NativeAotHelloProgram.Inspect(elf, "400205: 0f 05\tsyscall\n" + Rows), "hello_instruction_order");
        Reject(() => NativeAotHelloProgram.Inspect(elf, "400200: 90\tnop\n"), "hello_instruction_backing");
        foreach (string row in new[] { "401000: 00\taddb\n", "400fff: 00 00\taddb\n", "500000: 00\taddb\n" })
            Reject(() => NativeAotHelloProgram.Inspect(elf, row), "hello_instruction_executable");
        elf[0] = 0;
        Reject(() => NativeAotHelloProgram.Inspect(elf, Rows), "hello_instruction_elf");
    }

    [HostTest("nativeaot.hello-program matching counts cannot replace the reviewed instruction index digest")]
    private static void CountIsNotIdentity()
    {
        byte[] elf = Elf();
        var admittedRows = NativeAotHelloProgram.Inspect(elf, Rows);
        // The parser checks row structure and actual bytes; the reviewed digest
        // is what prevents an untrusted decoder from inventing x64 boundaries.
        var forged = NativeAotHelloProgram.Inspect(elf,
            "400200: b8\tmovl\n400201: 0f 05\tsyscall\n400203: 00 00 0f 05\tnop\n");
        HostCheck.True(forged.InstructionCount == admittedRows.InstructionCount &&
            forged.SyscallCount == admittedRows.SyscallCount && forged.Sha256 != admittedRows.Sha256);
        var partial = NativeAotHelloProgram.Inspect(elf, "400205: 0f 05\tsyscall\n");
        HostCheck.True(partial.InstructionCount == 1 && partial.SyscallCount == 1 && partial.Sha256 != admittedRows.Sha256);
        string root = HostToolProcess.FindRepositoryRoot();
        Reject(() => NativeAotHelloProgram.Create(root, LinuxThreadProofFixture.CreatePackage("Threads"), Rows), "hello_package_identity");
    }

    [HostTest("nativeaot.hello-program file admission rejects oversized and invalid UTF8 inputs without a success marker")]
    private static void FileFailures()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Directory.CreateTempSubdirectory("clros-hello-instructions-").FullName;
        try
        {
            string package = Path.Combine(directory, "package"), dump = Path.Combine(directory, "dump");
            File.WriteAllBytes(package, [1]); File.WriteAllBytes(dump, [0xff]);
            Reject(() => NativeAotHelloProgram.Read(root, package, dump), "hello_instruction_utf8");
            var result = HostToolProcess.RunTool("linux", "hello-program", package, dump);
            HostCheck.True(result.ExitCode != 0 && result.Error.Contains("TEST_FAIL tooling", StringComparison.Ordinal) &&
                !result.Output.Contains("TEST_PASS linux.hello-program", StringComparison.Ordinal));
            File.WriteAllBytes(dump, []);
            Reject(() => NativeAotHelloProgram.Read(root, package, dump), "hello_instruction_file_size");
            using (FileStream file = new(dump, FileMode.Create)) file.SetLength(20 * 1024 * 1024 + 1);
            Reject(() => NativeAotHelloProgram.Read(root, package, dump), "hello_instruction_file_size");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static byte[] Elf()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        new byte[] { 0xb8, 0x0f, 0x05, 0, 0, 0x0f, 0x05, 0xc3 }.CopyTo(elf, 0x200);
        return elf;
    }

    private static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try { action(); }
        catch (InvalidDataException error) { rejected = error.Message == reason; }
        HostCheck.True(rejected, reason);
    }
}
