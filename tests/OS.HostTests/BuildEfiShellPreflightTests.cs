using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

internal static class BuildEfiShellPreflightTests
{
    private const string SecretCanary = "CLROS_PREFLIGHT_SECRET_MUST_NOT_APPEAR";

    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-efi-preflight-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            Run("efi preflight reports missing msvc linker", () => TestMissingLinkerFails(tempRoot), ref failures);
            Run("efi preflight reports missing windows sdk", () => TestMissingWindowsSdkFails(tempRoot), ref failures);
            Run("efi preflight reports missing ilcompiler", () => TestMissingIlCompilerFails(tempRoot), ref failures);
            Run("efi preflight reports missing nativeaot runtime pack", () => TestMissingRuntimePackFails(tempRoot), ref failures);
            Run("efi preflight reports missing qemu", () => TestMissingQemuFails(tempRoot), ref failures);
            Run("efi preflight reports missing qemu-img", () => TestMissingQemuImgFails(tempRoot), ref failures);
            Run("efi preflight reports missing ovmf", () => TestMissingOvmfFails(tempRoot), ref failures);
            Run("efi preflight passes with injected exact paths", () => TestInjectedPathsPass(tempRoot), ref failures);
            Run("efi preflight passes with injected search roots", () => TestInjectedSearchRootsPass(tempRoot), ref failures);
            Run("efi preflight selects newest credible numeric toolsets", () => TestNewestCredibleToolsetsWin(tempRoot), ref failures);
            Run("efi preflight rejects undersized ovmf", () => TestUndersizedOvmfFails(tempRoot), ref failures);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure should not hide preflight contract results.
            }
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("Build EFI shell preflight tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestMissingLinkerFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-linker");
        tools.MsvcLinkerSearchRoot = CreateEmptyDirectory(tools.Root, "empty-msvc");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_MSVC_LINKER missing",
            "platform_linker_missing",
            unexpectedMarker: "EFI_PREFLIGHT_WINDOWS_SDK");
    }

    private static bool TestMissingWindowsSdkFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-sdk");
        tools.WindowsSdkLibSearchRoot = CreateEmptyDirectory(tools.Root, "empty-sdk");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_WINDOWS_SDK missing",
            "windows_sdk_um_x64_missing",
            unexpectedMarker: "EFI_PREFLIGHT_ILCOMPILER")
            && result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER path=", StringComparison.Ordinal);
    }

    private static bool TestMissingIlCompilerFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-ilcompiler");
        tools.IlCompilerPath = Path.Combine(tools.Root, "missing-ilc.exe");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_ILCOMPILER missing",
            "ilcompiler_missing",
            unexpectedMarker: "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK")
            && result.Output.Contains("EFI_PREFLIGHT_WINDOWS_SDK path=", StringComparison.Ordinal);
    }

    private static bool TestMissingRuntimePackFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-runtime-pack");
        tools.NativeAotRuntimePackPath = Path.Combine(tools.Root, "missing-runtime-pack");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK missing",
            "nativeaot_runtime_pack_missing",
            unexpectedMarker: "EFI_PREFLIGHT_QEMU")
            && result.Output.Contains("EFI_PREFLIGHT_ILCOMPILER path=", StringComparison.Ordinal);
    }

    private static bool TestMissingQemuFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-qemu");
        tools.QemuPath = Path.Combine(tools.Root, "missing-qemu.exe");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_QEMU missing",
            "qemu_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_QEMU_IMG")
            && result.Output.Contains("EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=", StringComparison.Ordinal);
    }

    private static bool TestMissingQemuImgFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-qemu-img");
        tools.QemuImgPath = Path.Combine(tools.Root, "missing-qemu-img.exe");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_QEMU_IMG missing",
            "qemu_img_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_OVMF")
            && result.Output.Contains("EFI_PREFLIGHT_QEMU path=", StringComparison.Ordinal);
    }

    private static bool TestMissingOvmfFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-ovmf");
        tools.OvmfCodePath = Path.Combine(tools.Root, "missing-OVMF_CODE.fd");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_OVMF missing",
            "ovmf_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_OK")
            && result.Output.Contains("EFI_PREFLIGHT_QEMU_IMG path=", StringComparison.Ordinal);
    }

    private static bool TestInjectedPathsPass(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "injected-paths");
        ProcessResult result = RunPreflight(tools);

        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestInjectedSearchRootsPass(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "injected-roots");
        ProcessResult result = RunPreflight(tools, useSearchRoots: true);

        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestNewestCredibleToolsetsWin(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "numeric-toolsets");

        string olderLinker = Path.Combine(
            tools.MsvcLinkerSearchRoot,
            "14.99.99999",
            "bin",
            "Hostx64",
            "x64",
            "link.exe");
        WriteMinimalPeX64(olderLinker);
        string newestLinker = Path.Combine(
            tools.MsvcLinkerSearchRoot,
            "14.100.10000",
            "bin",
            "Hostx64",
            "x64",
            "link.exe");
        WriteMinimalPeX64(newestLinker);
        string invalidNewestLinker = Path.Combine(
            tools.MsvcLinkerSearchRoot,
            "99.0.0",
            "bin",
            "Hostx64",
            "x64",
            "link.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidNewestLinker)!);
        File.WriteAllText(invalidNewestLinker, string.Empty);

        string olderSdk = Path.Combine(
            tools.WindowsSdkLibSearchRoot,
            "10.0.9999.0",
            "um",
            "x64",
            "kernel32.lib");
        WriteCoffArchive(olderSdk);
        string newestSdk = Path.Combine(
            tools.WindowsSdkLibSearchRoot,
            "10.0.100000.0",
            "um",
            "x64",
            "kernel32.lib");
        WriteCoffArchive(newestSdk);

        ProcessResult result = RunPreflight(tools);
        bool passed = result.ExitCode == 0
            && result.Output.Contains(
                "EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(newestLinker) + "\"",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(newestSdk) + "\"",
                StringComparison.Ordinal)
            && !result.Output.Contains(Path.GetFullPath(invalidNewestLinker), StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestUndersizedOvmfFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "undersized-ovmf");
        File.WriteAllBytes(tools.OvmfCodePath, new byte[] { 0x5f, 0x46, 0x56, 0x48 });

        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_OVMF missing",
            "ovmf_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_OK");
    }

    private static bool IsMissingFailure(
        ProcessResult result,
        string missingMarker,
        string failureReason,
        string unexpectedMarker)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        return result.ExitCode == 1
            && result.Output.Contains("TEST_START efi.shell.publish", StringComparison.Ordinal)
            && result.Output.Contains("EFI_PREFLIGHT_START", StringComparison.Ordinal)
            && result.Output.Contains(missingMarker, StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish " + failureReason,
                StringComparison.Ordinal)
            && !result.Output.Contains(unexpectedMarker, StringComparison.Ordinal)
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal)
            && log.Contains(missingMarker, StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal)
            && !log.Contains(SecretCanary, StringComparison.Ordinal);
    }

    private static bool IsSuccessfulEvidence(ProcessResult result, ToolFixture tools)
    {
        if (!File.Exists(result.LogPath))
        {
            return false;
        }

        string log = File.ReadAllText(result.LogPath);
        string[] requiredOutput =
        [
            "EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(tools.LinkerPath) + "\" version=\"14.40.33807\"",
            "EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(tools.SdkLibPath) + "\" version=\"10.0.26100.0\"",
            "EFI_PREFLIGHT_ILCOMPILER path=\"" + Path.GetFullPath(tools.IlCompilerPath) + "\" packageVersion=\"10.0.8\"",
            "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=\"" + Path.GetFullPath(tools.NativeAotRuntimePackPath) + "\" packageVersion=\"10.0.8\"",
            "EFI_PREFLIGHT_QEMU path=\"" + Path.GetFullPath(tools.QemuPath) + "\" version=\"QEMU emulator version 99.1-test\"",
            "EFI_PREFLIGHT_QEMU_IMG path=\"" + Path.GetFullPath(tools.QemuImgPath) + "\" version=\"qemu-img version 99.1-test\"",
            "EFI_PREFLIGHT_OVMF path=\"" + Path.GetFullPath(tools.OvmfCodePath) + "\" bytes=65536 sha256=",
            "EFI_PREFLIGHT_OK"
        ];

        foreach (string marker in requiredOutput)
        {
            if (!result.Output.Contains(marker, StringComparison.Ordinal)
                || !log.Contains(marker, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return result.ExitCode == 0
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_FAIL efi.shell.publish", StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal)
            && !log.Contains(SecretCanary, StringComparison.Ordinal);
    }

    private static ToolFixture CreateCompleteToolFixture(string tempRoot, string name)
    {
        string root = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(root);

        string msvcRoot = Path.Combine(root, "msvc");
        string linkerDirectory = Path.Combine(msvcRoot, "14.40.33807", "bin", "Hostx64", "x64");
        string linkerPath = Path.Combine(linkerDirectory, "link.exe");
        WriteMinimalPeX64(linkerPath);

        string sdkRoot = Path.Combine(root, "sdk");
        string libraryDirectory = Path.Combine(sdkRoot, "10.0.26100.0", "um", "x64");
        string sdkLibPath = Path.Combine(libraryDirectory, "kernel32.lib");
        WriteCoffArchive(sdkLibPath);

        string ilCompilerSearchRoot = Path.Combine(root, "ilcompiler");
        string ilCompilerDirectory = Path.Combine(ilCompilerSearchRoot, "10.0.8", "tools");
        string ilCompilerPath = Path.Combine(ilCompilerDirectory, "ilc.exe");
        WriteMinimalPeX64(ilCompilerPath);

        string runtimePackSearchRoot = Path.Combine(root, "runtime-packs");
        string runtimePackPath = Path.Combine(
            runtimePackSearchRoot,
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "10.0.8");
        string runtimePackData = Path.Combine(runtimePackPath, "data");
        Directory.CreateDirectory(runtimePackData);
        File.WriteAllText(
            Path.Combine(runtimePackData, "RuntimeList.xml"),
            "<FileList TargetFrameworkIdentifier=\".NETCoreApp\" TargetFrameworkVersion=\"10.0\" />");

        string qemuPath = Path.Combine(root, "qemu-system-x86_64.cmd");
        File.WriteAllLines(
            qemuPath,
            [
                "@echo off",
                "echo QEMU emulator version 99.1-test",
                "exit /b 0"
            ]);

        string qemuImgPath = Path.Combine(root, "qemu-img.cmd");
        File.WriteAllLines(
            qemuImgPath,
            [
                "@echo off",
                "echo qemu-img version 99.1-test",
                "exit /b 0"
            ]);

        string ovmfSearchRoot = Path.Combine(root, "firmware");
        Directory.CreateDirectory(ovmfSearchRoot);
        string ovmfCodePath = Path.Combine(ovmfSearchRoot, "edk2-x86_64-code.fd");
        byte[] ovmf = new byte[65536];
        ovmf[40] = (byte)'_';
        ovmf[41] = (byte)'F';
        ovmf[42] = (byte)'V';
        ovmf[43] = (byte)'H';
        File.WriteAllBytes(ovmfCodePath, ovmf);

        return new ToolFixture
        {
            Root = root,
            MsvcLinkerSearchRoot = msvcRoot,
            LinkerPath = linkerPath,
            WindowsSdkLibSearchRoot = sdkRoot,
            SdkLibPath = sdkLibPath,
            IlCompilerSearchRoot = ilCompilerSearchRoot,
            IlCompilerPath = ilCompilerPath,
            NativeAotRuntimePackSearchRoot = runtimePackSearchRoot,
            NativeAotRuntimePackPath = runtimePackPath,
            QemuPath = qemuPath,
            QemuImgPath = qemuImgPath,
            OvmfSearchRoot = ovmfSearchRoot,
            OvmfCodePath = ovmfCodePath
        };
    }

    private static string CreateEmptyDirectory(string root, string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteMinimalPeX64(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] bytes = new byte[256];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3c] = 0x80;
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        bytes[0x84] = 0x64;
        bytes[0x85] = 0x86;
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteCoffArchive(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] bytes = new byte[68];
        Encoding.ASCII.GetBytes("!<arch>\n").CopyTo(bytes, 0);
        File.WriteAllBytes(path, bytes);
    }

    private static ProcessResult RunPreflight(ToolFixture tools, bool useSearchRoots = false)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "build-efi-shell.ps1");
        string logPath = Path.Combine(tools.Root, "efi-shell-publish.log");

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        start.Environment["CLROS_PREFLIGHT_SECRET_CANARY"] = SecretCanary;
        AddArgument(start, "-NoProfile");
        AddArgument(start, "-ExecutionPolicy", "Bypass");
        AddArgument(start, "-File", scriptPath);
        AddArgument(start, "-PreflightOnly");
        AddArgument(start, "-LogPath", logPath);
        AddArgument(start, "-MsvcLinkerSearchRoot", tools.MsvcLinkerSearchRoot);
        AddArgument(start, "-WindowsSdkLibSearchRoot", tools.WindowsSdkLibSearchRoot);

        if (useSearchRoots)
        {
            AddArgument(start, "-IlCompilerSearchRoot", tools.IlCompilerSearchRoot);
            AddArgument(
                start,
                "-NativeAotRuntimePackSearchRoot",
                tools.NativeAotRuntimePackSearchRoot);
            AddArgument(start, "-OvmfSearchRoot", tools.OvmfSearchRoot);
        }
        else
        {
            AddArgument(start, "-IlCompilerPath", tools.IlCompilerPath);
            AddArgument(start, "-NativeAotRuntimePackPath", tools.NativeAotRuntimePackPath);
            AddArgument(start, "-OvmfCodePath", tools.OvmfCodePath);
        }

        AddArgument(start, "-QemuPath", tools.QemuPath);
        AddArgument(start, "-QemuImgPath", tools.QemuImgPath);

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("EFI preflight did not exit within 30 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult(),
            logPath);
    }

    private static void AddArgument(ProcessStartInfo start, params string[] arguments)
    {
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
    }

    private readonly struct ProcessResult(int exitCode, string output, string logPath)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;

        public string LogPath { get; } = logPath;
    }

    private sealed class ToolFixture
    {
        public required string Root { get; init; }

        public required string MsvcLinkerSearchRoot { get; set; }

        public required string LinkerPath { get; init; }

        public required string WindowsSdkLibSearchRoot { get; set; }

        public required string SdkLibPath { get; init; }

        public required string IlCompilerSearchRoot { get; init; }

        public required string IlCompilerPath { get; set; }

        public required string NativeAotRuntimePackSearchRoot { get; init; }

        public required string NativeAotRuntimePackPath { get; set; }

        public required string QemuPath { get; set; }

        public required string QemuImgPath { get; set; }

        public required string OvmfSearchRoot { get; init; }

        public required string OvmfCodePath { get; set; }
    }
}
