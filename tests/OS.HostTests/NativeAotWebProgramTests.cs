using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using OS.Manifest;
using OS.Tooling;

internal static partial class NativeAotWebProgramTests
{
    private static byte[] Image()
    {
        byte[] original = LinuxElfPackageTests.CapturePackage();
        var view = LinuxElfPackage.Validate(original);
        byte[] elf = original.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
        elf[512] = 0x0f;
        elf[513] = 0x05;
        elf[514] = 0xc3;
        return elf;
    }

    [HostTest("nativeaot.web program offline report binds exact executable rows without prepared authority")]
    private static void OfflineReport()
    {
        byte[] elf = Image();
        const string dump = "400200: 0f 05\tsyscall\n400202: c3\tretq\n";
        var report = NativeAotWebProgram.Inspect(elf, dump);
        HostCheck.Equal(2, report.InstructionCount);
        HostCheck.Equal(1, report.SyscallCount);
        HostCheck.Equal(64, report.Sha256.Length);
        string root = Path.Combine(Path.GetTempPath(), "clros-nonexistent-web-" + Guid.NewGuid().ToString("N"));
        bool rejected = false;
        try
        {
            NativeAotWebProgram.Create(root, NativeAotWebPackage.Create(elf, "{}"u8), dump);
        }
        catch (InvalidDataException error)
        {
            rejected = error.Message == "web_package_identity";
        }

        HostCheck.True(rejected); // Even a valid small package/report cannot authorize the original web ELF.
    }

    [HostTest("nativeaot.web program rejects operand gaps data bytes and altered syscall rows")]
    private static void InvalidRows()
    {
        byte[] elf = Image();
        foreach (string dump in new[]
        {
            "400200: 0f 04\tsyscall\n",
            "400200: 0f 05\tretq\n",
            "400202: c3\tretq\n400201: 05\tbyte\n",
            "400fff: c3\tretq\n",
            "400200: 0f 05\tsyscall\n400200: 0f 05\tsyscall\n",
            ""
        }

        )
        {
            bool rejected = false;
            try
            {
                NativeAotWebProgram.Inspect(elf, dump);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        // File-backed read-only ELF data cannot gain executable-start authority.
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(68), 4);
        bool dataRejected = false;
        try
        {
            NativeAotWebProgram.Inspect(elf, "400200: 0f 05\tsyscall\n");
        }
        catch (InvalidDataException)
        {
            dataRejected = true;
        }

        HostCheck.True(dataRejected);
    }

    [HostTest("nativeaot.web source gate holds the complete current app and build inputs")]
    private static void SourceInventory()
    {
        using var fixture = new SourceFixture();
        using var files = new AuthorityFiles();
        Hold(fixture.Root, files);
        Hold(fixture.Root, files);
        files.VerifyUnchanged(); // Source admission alone never prepares an ELF.
    }

    [HostTest("nativeaot.web source gate rejects missing extra and recursive app files")]
    private static void SourceInventoryRefusals()
    {
        foreach (Action<SourceFixture> change in new Action<SourceFixture>[]
        {
            f => File.Delete(f.PathFor("apps/MinimalApiSmoke/KestrelSignalProbeAbi.cs")),
            f => f.Write("apps/MinimalApiSmoke/Extra.cs", "// extra app source"),
            f => f.Write("apps/MinimalApiSmoke/nested/Extra.cs", "// recursive extra app source")
        }

        )
        {
            using var fixture = new SourceFixture();
            change(fixture);
            using var files = new AuthorityFiles();
            Refuse(() => Hold(fixture.Root, files), "web_source_inventory");
        }
    }

    [HostTest("nativeaot.web source gate rejects altered signal source and root or app overrides")]
    private static void SourceIdentityAndOverrides()
    {
        using (var fixture = new SourceFixture())
        using (var files = new AuthorityFiles())
        {
            const string changed = "apps/MinimalApiSmoke/KestrelSignalProbe.cs";
            File.AppendAllText(fixture.PathFor(changed), "\n// altered copied source\n");
            Refuse(() => Hold(fixture.Root, files), "web_source_identity " + changed);
        }

        foreach (string relative in new[]
        {
            "NuGet.Config",
            "Directory.Packages.props",
            "apps/Directory.Build.props",
            "apps/Directory.Build.targets",
            "apps/Directory.Packages.props"
        }

        )
        {
            using var fixture = new SourceFixture();
            fixture.Write(relative, "<Project />");
            using var files = new AuthorityFiles();
            Refuse(() => Hold(fixture.Root, files), "web_source_override " + relative);
        }
    }

    [HostTest("nativeaot.web held source refuses mutation or detects changed contents")]
    private static void HeldSourceMutation()
    {
        using var fixture = new SourceFixture();
        using var files = new AuthorityFiles();
        Hold(fixture.Root, files);
        bool refused = false;
        try
        {
            using var writer = new FileStream(fixture.PathFor("apps/MinimalApiSmoke/KestrelSignalRaceState.cs"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            writer.WriteByte(0);
            writer.Flush(true);
        }
        catch (IOException)when (OperatingSystem.IsWindows())
        {
            refused = true;
        }

        if (refused)
            files.VerifyUnchanged();
        else
        {
            bool detected = false;
            try
            {
                files.VerifyUnchanged();
            }
            catch (ToolFailure error)
            {
                detected = error.Reason == "authority_input_changed";
            }

            HostCheck.True(detected);
        }
    }

    private static void Hold(string root, AuthorityFiles files)
    {
        MethodInfo method = typeof(NativeAotWebProgram).GetMethod("HoldSources", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("missing source gate");
        try
        {
            method.Invoke(null, [root, files]);
        }
        catch (TargetInvocationException error)when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static void Refuse(Action action, string reason)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidDataException error)
        {
            rejected = error.Message == reason;
        }

        HostCheck.True(rejected);
    }
}
