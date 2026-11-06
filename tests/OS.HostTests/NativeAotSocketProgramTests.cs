using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotSocketProgramTests
{
    private const string Rows = "400200: b8 0f 05 00 00\tmovl $0x50f, %eax\n400205: 0f 05\tsyscall\n400207: c3\tretq\n";
    private static byte[] Elf()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        new byte[] { 0xb8, 0x0f, 0x05, 0, 0, 0x0f, 0x05, 0xc3 }.CopyTo(elf, 0x200);
        return elf;
    }

    [HostTest("socket instruction index independent domain vector excludes syscall bytes inside operands")]
    private static void Domain()
    {
        byte[] elf = Elf();
        var report = NativeAotSocketProgram.Inspect(elf, Rows);
        using MemoryStream vector = new();
        vector.Write(Encoding.ASCII.GetBytes("CLROS_SOCKET_INSTRUCTIONS_V1\n"));
        using (BinaryWriter writer = new(vector, Encoding.ASCII, true))
        {
            writer.Write(0x400200UL);
            writer.Write((byte)5);
            writer.Write(new byte[] { 0xb8, 0x0f, 0x05, 0, 0 });
            writer.Write((byte)0);
            writer.Write(0x400205UL);
            writer.Write((byte)2);
            writer.Write(new byte[] { 0x0f, 5 });
            writer.Write((byte)1);
            writer.Write(0x400207UL);
            writer.Write((byte)1);
            writer.Write((byte)0xc3);
            writer.Write((byte)0);
        }
        HostCheck.True(report.InstructionCount == 3 && report.SyscallCount == 1);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(vector.ToArray())), report.Sha256);
        HostCheck.True(report.Sha256 != NativeAotRuntimeProgram.Inspect(elf, Rows).Sha256 && report.Sha256 != NativeAotThreadingProgram.Inspect(elf, Rows).Sha256);
        HostCheck.Equal(report, NativeAotSocketProgram.Inspect(elf, "file: file format elf64-x86-64\n" + Rows.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    [HostTest("socket instruction partial forged equal-count and malformed reports grant no prepared lookup")]
    private static void Authority()
    {
        byte[] elf = Elf();
        var original = NativeAotSocketProgram.Inspect(elf, Rows);
        var forged = NativeAotSocketProgram.Inspect(elf, "400200: b8\tmovl\n400201: 0f 05\tsyscall\n400203: 00 00 0f 05\tnop\n");
        HostCheck.True(forged.InstructionCount == original.InstructionCount && forged.SyscallCount == original.SyscallCount && forged.Sha256 != original.Sha256);
        HostCheck.Equal(1, NativeAotSocketProgram.Inspect(elf, "400205: 0f 05\tsyscall\n").InstructionCount);
        foreach (string rows in new[] { "", "headers only", Rows + Rows, Rows.Replace("syscall", "nop", StringComparison.Ordinal), "400200: 90\tnop\n", "500000: 00\taddb\n" })
            Reject(() => NativeAotSocketProgram.Inspect(elf, rows), "socket_instruction_");
        foreach (bool asynchronous in new[] { false, true })
            Reject(() => NativeAotSocketProgram.Create(HostToolProcess.FindRepositoryRoot(), asynchronous, LinuxThreadProofFixture.CreatePackage("Threads"), Rows), "socket_package_identity");
        HostCheck.True(typeof(NativeAotSocketProgram).IsSealed && typeof(NativeAotSocketProgram).GetConstructors().Length == 0);
    }

    [HostTest("socket instruction file snapshots reject aliases invalid UTF8 oversized dumps and absent CLI inputs")]
    private static void Files()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Directory.CreateTempSubdirectory("clros-socket-program-").FullName;
        string package = Path.Combine(directory, "app.pkg"), dump = Path.Combine(directory, "dump");
        try
        {
            File.WriteAllBytes(package, [1]);
            File.WriteAllBytes(dump, [0xff]);
            Reject(() => NativeAotSocketProgram.Read(root, false, package, package), "socket_instruction_alias");
            Reject(() => NativeAotSocketProgram.Read(root, false, package, dump), "socket_instruction_dump");
            using (FileStream file = new(dump, FileMode.Create))
                file.SetLength(32 * 1024 * 1024 + 1);
            Reject(() => NativeAotSocketProgram.Read(root, true, package, dump), "size_out_of_range");
            File.WriteAllBytes(dump, []);
            Reject(() => NativeAotSocketProgram.Read(root, true, package, dump), "size_out_of_range");
            var result = HostToolProcess.RunTool("linux", "socket-program", "sync", Path.Combine(directory, "absent"), dump);
            HostCheck.True(result.ExitCode != 0 && !result.Output.Contains("TEST_PASS linux.socket-program", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Reject(Action action, string reason)
    {
        string? failure = null;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or ToolFailure)
        {
            failure = error.Message;
        }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }
}
