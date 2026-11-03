using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling;
using P = OS.Tooling.NativeAotSqliteProgram;

internal static partial class NativeAotSqliteProgramTests
{
    private static readonly string[] Modes = ["database", "verify-database", "recovery-seed", "recovery-spill", "recovery-verify"];
    private const string Rows = "400200: b8 0f 05 00 00\tmovl $0x50f, %eax\n400205: 0f 05\tsyscall\n400207: c3\tretq\n";
    private static byte[] Elf()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        new byte[]
        {
            0xb8,
            0x0f,
            0x05,
            0,
            0,
            0x0f,
            0x05,
            0xc3
        }.CopyTo(elf, 0x200);
        return elf;
    }

    [HostTest("nativeaot.sqlite-program independent domain vector binds starts and syscall continuations rather than operand bytes")]
    private static void IndependentVector()
    {
        byte[] elf = Elf(), before = elf.ToArray();
        var report = P.Inspect(elf, Rows);
        using MemoryStream vector = new();
        vector.Write(Encoding.ASCII.GetBytes("CLROS_SQLITE_INSTRUCTIONS_V1\n"));
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

        HostCheck.Equal(3, report.InstructionCount);
        HostCheck.Equal(1, report.SyscallCount);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(vector.ToArray())), report.Sha256);
        HostCheck.True(report.Sha256 != NativeAotThreadingProgram.Inspect(elf, Rows).Sha256 && report.Sha256 != NativeAotStaticFileProgram.Inspect(elf, Rows).Sha256 && report.Sha256 != NativeAotLogUploadProgram.Inspect(elf, Rows).Sha256 && elf.SequenceEqual(before));
        HostCheck.Equal(report, P.Inspect(elf, "C:\\original\\app: file format elf64-x86-64\n" + Rows.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    [HostTest("nativeaot.sqlite-program partial and forged equal-count rows remain reports and cannot prepare counterfeit packages")]
    private static void ReportOnly()
    {
        byte[] elf = Elf();
        var original = P.Inspect(elf, Rows);
        const string forged = "400200: b8\tmovl\n400201: 0f 05\tsyscall\n400203: 00 00 0f 05\tnop\n";
        var counterfeit = P.Inspect(elf, forged);
        var partial = P.Inspect(elf, "400205: 0f 05\tsyscall\n");
        HostCheck.True(counterfeit.InstructionCount == original.InstructionCount && counterfeit.SyscallCount == original.SyscallCount && counterfeit.Sha256 != original.Sha256 && partial.InstructionCount == 1);
        byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        foreach (string mode in Modes)
            foreach (string rows in new[]
            {
                Rows,
                forged,
                "400205: 0f 05\tsyscall\n"
            }

            )
                Reject(() => P.Create("missing-root", package, rows, mode), "sqlite_contract_clros_package");
        HostCheck.True(typeof(P).IsSealed && typeof(P).GetConstructors().Length == 0 && typeof(P).GetProperty("Mode")!.SetMethod is null);
    }

    [HostTest("nativeaot.sqlite-program malformed mismatched overlapping nonexecutable and bounded rows reject")]
    private static void MalformedRows()
    {
        byte[] elf = Elf();
        foreach (string rows in new[]
        {
            "",
            "headers only",
            Rows + Rows,
            Rows.Replace("syscall", "nop", StringComparison.Ordinal),
            "400200: 90\tnop\n",
            "500000: 00\taddb\n",
            "400200: b8\t<unknown>\n",
            "400200: b8\t.byte\n",
            "400200: b8 mov\n",
            "400200: b8\tmov\0\n",
            "400200: " + new string ('a', 1025) + "\n"
        }

        )
            Reject(() => P.Inspect(elf, rows), "sqlite_instruction_");
        Reject(() => P.Inspect(elf, new string ('x', P.MaximumDisassemblyBytes + 1)), "sqlite_instruction_text");
        Reject(() => P.Inspect(elf, null!), "sqlite_instruction_text");
        Reject(() => P.Inspect([], Rows), "sqlite_instruction_elf");
    }

    [HostTest("nativeaot.sqlite-program mode is explicit and invalid modes reject before package or filesystem admission")]
    private static void ExplicitMode()
    {
        foreach (string mode in new[]
        {
            "",
            "Database",
            "VERIFY-DATABASE",
            "default",
            "--database",
            "recovery",
            "write",
            "database\n",
            null!
        }

        )
        {
            Reject(() => P.Create("missing-root", [], Rows, mode), "sqlite_instruction_mode");
            Reject(() => P.Read("missing-root", "missing-package", "missing-dump", mode), "sqlite_instruction_mode");
        }

        foreach (string mode in Modes)
            Reject(() => P.Create("missing-root", [], Rows, mode), "sqlite_contract_clros_package");
    }

    [HostTest("nativeaot.sqlite-program reviewed producer and original app source identities remain held and usable")]
    private static void CurrentSources()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        using var sources = new AuthorityFiles();
        Hold(root, sources);
        Hold(root, sources);
        sources.VerifyUnchanged();
    }

    [HostTest("nativeaot.sqlite-program changed producer source rejects without a native ELF or instruction lookup")]
    private static void ChangedProducer()
    {
        using SourceCopy copy = new(complete: false);
        File.WriteAllText(Path.Combine(copy.Root, "global.json"), "{}");
        using var sources = new AuthorityFiles();
        Reject(() => Hold(copy.Root, sources), "sqlite_source_identity global.json");
    }

    [HostTest("nativeaot.sqlite-program source inventory and override mutations reject after exact declared source hashes")]
    private static void SourceInventory()
    {
        using SourceCopy copy = new();
        string app = Path.Combine(copy.Root, "apps", NativeAotSqliteContract.Application);
        using (var valid = new AuthorityFiles())
        {
            Hold(copy.Root, valid);
            valid.VerifyUnchanged();
        }

        string added = Path.Combine(app, "Unreviewed.cs");
        File.WriteAllText(added, "// not admitted");
        using (var sources = new AuthorityFiles())
            Reject(() => Hold(copy.Root, sources), "socket_sqlite-program_publish_app_inventory");
        File.Delete(added);
        File.WriteAllText(Path.Combine(copy.Root, "apps", "Directory.Build.props"), "<Project />");
        using (var sources = new AuthorityFiles())
            Reject(() => Hold(copy.Root, sources), "socket_sqlite-program_publish_source_override");
    }

    [HostTest("nativeaot.sqlite-program source holds deny or detect mutations through the real preparation helper")]
    private static void HeldSources()
    {
        using SourceCopy copy = new();
        string path = Path.Combine(copy.Root, "apps", NativeAotSqliteContract.Application, "Program.cs");
        using (var sources = new AuthorityFiles())
        {
            Hold(copy.Root, sources);
            bool denied = false;
            try
            {
                File.WriteAllText(path, "// changed");
            }
            catch (IOException)
            {
                denied = true;
            }

            if (denied)
                sources.VerifyUnchanged();
            else
                Reject(sources.VerifyUnchanged, "authority_input_changed");
        }

        File.WriteAllText(path, "// changed after preparation lease ended");
        using var later = new AuthorityFiles();
        Reject(() => Hold(copy.Root, later), "sqlite_source_identity apps/NativeAotSqliteContract/Program.cs");
    }

    [HostTest("nativeaot.sqlite-program bounded file admission rejects aliases bad UTF8 empty oversized and missing inputs")]
    private static void FileAdmission()
    {
        using SourceCopy copy = new(complete: false);
        string package = Path.Combine(copy.Root, "input.pkg"), dump = Path.Combine(copy.Root, "input.llvm");
        File.WriteAllBytes(package, [1]);
        File.WriteAllBytes(dump, [0xff]);
        Reject(() => P.Read(copy.Root, package, package, "database"), "sqlite_instruction_alias");
        Reject(() => P.Read(copy.Root, package, dump, "database"), "sqlite_instruction_dump");
        File.WriteAllBytes(dump, []);
        Reject(() => P.Read(copy.Root, package, dump, "recovery-verify"), "size_out_of_range");
        using (var stream = new FileStream(dump, FileMode.Create))
            stream.SetLength(P.MaximumDisassemblyBytes + 1);
        Reject(() => P.Read(copy.Root, package, dump, "database"), "size_out_of_range");
        File.WriteAllText(dump, Rows);
        using (var stream = new FileStream(package, FileMode.Create))
            stream.SetLength(NativeAotSqliteContract.ClrosElfBytes + 4097);
        Reject(() => P.Read(copy.Root, package, dump, "database"), "sqlite_instruction_package_size_out_of_range");
        File.Delete(package);
        Reject(() => P.Read(copy.Root, package, dump, "database"), "sqlite_instruction_package_");
    }

    private static void Hold(string root, AuthorityFiles sources)
    {
        var method = typeof(P).GetMethod("HoldSources", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            method.Invoke(null, [root, sources]);
        }
        catch (TargetInvocationException error)when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static void Reject(Action action, string expected)
    {
        string? actual = null;
        try
        {
            action();
        }
        catch (Exception error)when (error is InvalidDataException or ArgumentException or ToolFailure)
        {
            actual = error.Message;
        }

        HostCheck.True(actual?.Contains(expected, StringComparison.Ordinal) == true, expected + " actual=" + actual);
    }
}
