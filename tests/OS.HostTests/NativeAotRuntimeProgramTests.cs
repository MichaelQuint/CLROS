using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotRuntimeProgramTests
{
    private const string Rows = "400200: b8 0f 05 00 00\tmovl $0x50f, %eax\n400205: 0f 05\tsyscall\n400207: c3\tretq\n";
    private static byte[] Elf()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        new byte[] { 0xb8, 0x0f, 0x05, 0, 0, 0x0f, 0x05, 0xc3 }.CopyTo(elf, 0x200);
        return elf;
    }
    [HostTest("nativeaot.runtime-program independent domain vector binds starts and actual syscalls excluding opcode bytes inside operands")]
    private static void Domain()
    {
        byte[] elf = Elf();
        var report = NativeAotRuntimeProgram.Inspect(elf, Rows);
        using MemoryStream vector = new();
        vector.Write(Encoding.ASCII.GetBytes("CLROS_RUNTIME_INSTRUCTIONS_V1\n"));
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
        HostCheck.True(report.Sha256 != NativeAotThreadingProgram.Inspect(elf, Rows).Sha256 && report.Sha256 != NativeAotHelloProgram.Inspect(elf, Rows).Sha256);
        HostCheck.Equal(report, NativeAotRuntimeProgram.Inspect(elf, "file: file format elf64-x86-64\n" + Rows.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }
    [HostTest("nativeaot.runtime-program partial forged equal-count malformed and counterfeit inputs cannot construct prepared lookup")]
    private static void Authority()
    {
        byte[] elf = Elf();
        var original = NativeAotRuntimeProgram.Inspect(elf, Rows);
        var forged = NativeAotRuntimeProgram.Inspect(elf, "400200: b8\tmovl\n400201: 0f 05\tsyscall\n400203: 00 00 0f 05\tnop\n");
        HostCheck.True(forged.InstructionCount == original.InstructionCount && forged.SyscallCount == original.SyscallCount && forged.Sha256 != original.Sha256);
        HostCheck.Equal(1, NativeAotRuntimeProgram.Inspect(elf, "400205: 0f 05\tsyscall\n").InstructionCount);
        foreach (string rows in new[] { "", "headers only", Rows + Rows, Rows.Replace("syscall", "nop", StringComparison.Ordinal), "400200: 90\tnop\n", "500000: 00\taddb\n" })
            Reject(() => NativeAotRuntimeProgram.Inspect(elf, rows), "runtime_instruction_");
        foreach (string app in NativeAotRuntimePayload.Applications)
            Reject(() => NativeAotRuntimeProgram.Create(HostToolProcess.FindRepositoryRoot(), app, LinuxThreadProofFixture.CreatePackage("Threads"), Rows), "runtime_package_identity");
        HostCheck.True(typeof(NativeAotRuntimeProgram).IsSealed && typeof(NativeAotRuntimeProgram).GetConstructors().Length == 0);
    }
    [HostTest("nativeaot.runtime-program file snapshots reject aliases invalid UTF8 oversized dumps and missing CLI inputs")]
    private static void Files()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Directory.CreateTempSubdirectory("clros-runtime-program-").FullName;
        string package = Path.Combine(directory, "app.pkg"), dump = Path.Combine(directory, "dump");
        try
        {
            File.WriteAllBytes(package, [1]);
            File.WriteAllBytes(dump, [0xff]);
            Reject(() => NativeAotRuntimeProgram.Read(root, "NativeAotRuntimeMatrix", package, package), "runtime_instruction_alias");
            Reject(() => NativeAotRuntimeProgram.Read(root, "NativeAotRuntimeMatrix", package, dump), "runtime_instruction_dump");
            using (FileStream file = new(dump, FileMode.Create))
                file.SetLength(20 * 1024 * 1024 + 1);
            Reject(() => NativeAotRuntimeProgram.Read(root, "NativeAotThreadPoolStress", package, dump), "size_out_of_range");
            File.WriteAllBytes(dump, []);
            Reject(() => NativeAotRuntimeProgram.Read(root, "NativeAotSuspensionStress", package, dump), "size_out_of_range");
            var result = HostToolProcess.RunTool("linux", "runtime-program", "NativeAotRuntimeMatrix", Path.Combine(directory, "absent"), dump);
            HostCheck.True(result.ExitCode != 0 && !result.Output.Contains("TEST_PASS linux.runtime-program", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, true);
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
