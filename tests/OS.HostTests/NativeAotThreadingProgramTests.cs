using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotThreadingProgramTests
{
    private const string Rows = "400200: b8 0f 05 00 00\tmovl $0x50f, %eax\n400205: 0f 05\tsyscall\n400207: c3\tretq\n";
    private static byte[] Elf()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        new byte[] { 0xb8, 0x0f, 0x05, 0, 0, 0x0f, 0x05, 0xc3 }.CopyTo(elf, 0x200);
        return elf;
    }
    [HostTest("nativeaot.threading-program independent domain vector binds exact instruction boundaries rather than operand opcode patterns")]
    private static void IndexVector()
    {
        byte[] elf = Elf();
        var report = NativeAotThreadingProgram.Inspect(elf, Rows);
        using MemoryStream vector = new();
        vector.Write(Encoding.ASCII.GetBytes("CLROS_THREADING_INSTRUCTIONS_V1\n"));
        using (BinaryWriter writer = new(vector, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(0x400200UL);
            writer.Write((byte)5);
            writer.Write(new byte[] { 0xb8, 0x0f, 0x05, 0, 0 });
            writer.Write((byte)0);
            writer.Write(0x400205UL);
            writer.Write((byte)2);
            writer.Write(new byte[] { 0x0f, 0x05 });
            writer.Write((byte)1);
            writer.Write(0x400207UL);
            writer.Write((byte)1);
            writer.Write((byte)0xc3);
            writer.Write((byte)0);
        }
        HostCheck.True(report.InstructionCount == 3 && report.SyscallCount == 1);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(vector.ToArray())), report.Sha256);
        HostCheck.True(report.Sha256 != NativeAotHelloProgram.Inspect(elf, Rows).Sha256);
        HostCheck.Equal(report, NativeAotThreadingProgram.Inspect(elf, "file: file format elf64-x86-64\n" + Rows.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }
    [HostTest("nativeaot.threading-program partial matching-count forged and malformed reports expose no prepared-program authority")]
    private static void ObservationBoundaries()
    {
        byte[] elf = Elf();
        var original = NativeAotThreadingProgram.Inspect(elf, Rows);
        var forged = NativeAotThreadingProgram.Inspect(elf, "400200: b8\tmovl\n400201: 0f 05\tsyscall\n400203: 00 00 0f 05\tnop\n");
        HostCheck.True(forged.InstructionCount == original.InstructionCount && forged.SyscallCount == original.SyscallCount && forged.Sha256 != original.Sha256);
        var partial = NativeAotThreadingProgram.Inspect(elf, "400205: 0f 05\tsyscall\n");
        HostCheck.Equal(1, partial.InstructionCount);
        foreach (string rows in new[] { "", "headers only", Rows + Rows, Rows.Replace("syscall", "nop", StringComparison.Ordinal), "400200: 90\tnop\n", "500000: 00\taddb\n" })
            Reject(() => NativeAotThreadingProgram.Inspect(elf, rows), "threading_instruction_");
        foreach (string app in NativeAotThreadingPayload.Applications)
            Reject(() => NativeAotThreadingProgram.Create(HostToolProcess.FindRepositoryRoot(), app, LinuxThreadProofFixture.CreatePackage("Threads"), Rows), "threading_package_identity");
        HostCheck.True(typeof(NativeAotThreadingProgram).IsSealed && typeof(NativeAotThreadingProgram).GetConstructors().Length == 0);
    }
    [HostTest("nativeaot.threading-program bounded file snapshots reject aliases invalid encoding oversized dumps and missing CLI inputs")]
    private static void FileAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Directory.CreateTempSubdirectory("clros-threading-program-").FullName;
        string package = Path.Combine(directory, "app.pkg"), dump = Path.Combine(directory, "dump");
        try
        {
            File.WriteAllBytes(package, [1]);
            File.WriteAllBytes(dump, [0xff]);
            Reject(() => NativeAotThreadingProgram.Read(root, "NativeAotSuspensionSmoke", package, package), "threading_instruction_alias");
            Reject(() => NativeAotThreadingProgram.Read(root, "NativeAotSuspensionSmoke", package, dump), "threading_instruction_dump");
            using (FileStream file = new(dump, FileMode.Create))
                file.SetLength(20 * 1024 * 1024 + 1);
            Reject(() => NativeAotThreadingProgram.Read(root, "NativeAotThreadPoolSmoke", package, dump), "size_out_of_range");
            File.WriteAllBytes(dump, []);
            Reject(() => NativeAotThreadingProgram.Read(root, "NativeAotSuspensionSmoke", package, dump), "size_out_of_range");
            var result = HostToolProcess.RunTool("linux", "threading-program", "NativeAotThreadPoolSmoke", Path.Combine(directory, "absent"), dump);
            HostCheck.True(result.ExitCode != 0 && !result.Output.Contains("TEST_PASS linux.threading-program", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    private static void Reject(Action action, string reason)
    {
        string? failure = null;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or ToolFailure) { failure = error.Message; }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }
}
