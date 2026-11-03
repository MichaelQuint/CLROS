using System.Reflection;
using System.Runtime.ExceptionServices;
using OS.Manifest;
using OS.Tooling;

internal static class NativeAotStaticFileProgramTests
{
    [HostTest("static program reports bind their own domain but cannot authorize a synthetic ELF")]
    private static void ReportOnly()
    {
        byte[] package = LinuxElfPackageTests.CapturePackage();
        var view = LinuxElfPackage.Validate(package);
        byte[] elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
        elf[512] = 0x0f;
        elf[513] = 0x05;
        elf[514] = 0xc3;
        const string dump = "400200: 0f 05\tsyscall\n400202: c3\tret\n";
        var report = NativeAotStaticFileProgram.Inspect(elf, dump);
        HostCheck.Equal(2, report.InstructionCount);
        HostCheck.Equal(1, report.SyscallCount);
        HostCheck.True(report.Sha256 != NativeAotWebProgram.Inspect(elf, dump).Sha256);
        foreach (bool staged in new[] { false, true })
        {
            bool rejected = false;
            try
            {
                NativeAotStaticFileProgram.Create("missing-root", NativeAotStaticFilePackage.Create(elf, staged), dump, staged);
            }
            catch (InvalidDataException e)
            {
                rejected = e.Message == "static_package_identity";
            }
            HostCheck.True(rejected);
        }
    }

    [HostTest("static program holds original shared producer and separate app inputs")]
    private static void CurrentInputs()
    {
        using var files = new AuthorityFiles();
        string root = HostToolProcess.FindRepositoryRoot();
        Hold(root, files);
        Hold(root, files);
        files.VerifyUnchanged();
        // The original accepted application gate stays independently usable.
        MethodInfo old = typeof(NativeAotWebProgram).GetMethod("HoldSources", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var original = new AuthorityFiles();
        old.Invoke(null, [root, original]);
        original.VerifyUnchanged();
    }

    [HostTest("static program rejects altered shared producer before app or instruction lookup")]
    private static void ChangedProducer()
    {
        string parent = Path.GetFullPath(Path.GetTempPath());
        string root = Path.Combine(parent, "clros-static-program-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // The first producer source has an independently pinned raw identity;
            // rejecting it must not require a native ELF or the rest of the app.
            File.WriteAllText(Path.Combine(root, "global.json"), "{}");
            using var files = new AuthorityFiles();
            bool rejected = false;
            try
            {
                Hold(root, files);
            }
            catch (InvalidDataException e)
            {
                rejected = e.Message == "static_source_identity global.json";
            }
            HostCheck.True(rejected);
        }
        finally
        {
            HostCheck.Equal(Path.TrimEndingDirectorySeparator(parent), Path.GetDirectoryName(Path.GetFullPath(root))!);
            HostCheck.True(Path.GetFileName(root).StartsWith("clros-static-program-", StringComparison.Ordinal));
            Directory.Delete(root, true);
        }
    }

    private static void Hold(string root, AuthorityFiles files)
    {
        var method = typeof(NativeAotStaticFileProgram).GetMethod("HoldSources", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            method.Invoke(null, [root, files]);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
