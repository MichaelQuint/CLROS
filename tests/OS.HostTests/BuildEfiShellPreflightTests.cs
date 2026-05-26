using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class BuildEfiShellPreflightTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-efi-preflight-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            Run("efi preflight reports missing msvc linker", () => TestMissingLinkerFails(tempRoot), ref failures);
            Run("efi preflight reports missing windows sdk", () => TestMissingWindowsSdkFails(tempRoot), ref failures);
            Run("efi preflight passes with discoverable toolchain", () => TestDiscoverableToolchainPasses(tempRoot), ref failures);
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
        string fixture = CreateFixture(tempRoot, "missing-linker");
        string emptyLinkerRoot = Path.Combine(fixture, "empty-msvc");
        Directory.CreateDirectory(emptyLinkerRoot);
        string sdkRoot = CreateWindowsSdkRoot(fixture);

        ProcessResult result = RunPreflight(fixture, emptyLinkerRoot, sdkRoot);

        return result.ExitCode == 1
            && result.Output.Contains("TEST_START efi.shell.publish", StringComparison.Ordinal)
            && result.Output.Contains("EFI_PREFLIGHT_START", StringComparison.Ordinal)
            && result.Output.Contains("TEST_FAIL efi.shell.publish platform_linker_missing", StringComparison.Ordinal)
            && !result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal);
    }

    private static bool TestMissingWindowsSdkFails(string tempRoot)
    {
        string fixture = CreateFixture(tempRoot, "missing-sdk");
        string linkerRoot = CreateMsvcLinkerRoot(fixture);
        string emptySdkRoot = Path.Combine(fixture, "empty-sdk");
        Directory.CreateDirectory(emptySdkRoot);

        ProcessResult result = RunPreflight(fixture, linkerRoot, emptySdkRoot);

        return result.ExitCode == 1
            && result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal)
            && result.Output.Contains("TEST_FAIL efi.shell.publish windows_sdk_um_x64_missing", StringComparison.Ordinal)
            && !result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal);
    }

    private static bool TestDiscoverableToolchainPasses(string tempRoot)
    {
        string fixture = CreateFixture(tempRoot, "discoverable");
        string linkerRoot = CreateMsvcLinkerRoot(fixture);
        string sdkRoot = CreateWindowsSdkRoot(fixture);

        ProcessResult result = RunPreflight(fixture, linkerRoot, sdkRoot);

        return result.ExitCode == 0
            && result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal)
            && result.Output.Contains("link.exe", StringComparison.Ordinal)
            && result.Output.Contains("EFI_PREFLIGHT_WINDOWS_SDK", StringComparison.Ordinal)
            && result.Output.Contains("kernel32.lib", StringComparison.Ordinal)
            && result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_FAIL efi.shell.publish", StringComparison.Ordinal);
    }

    private static string CreateFixture(string tempRoot, string name)
    {
        string fixture = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(fixture);
        return fixture;
    }

    private static string CreateMsvcLinkerRoot(string fixture)
    {
        string root = Path.Combine(fixture, "msvc");
        string linkerDirectory = Path.Combine(root, "14.40.33807", "bin", "Hostx64", "x64");
        Directory.CreateDirectory(linkerDirectory);
        File.WriteAllText(Path.Combine(linkerDirectory, "link.exe"), string.Empty);
        return root;
    }

    private static string CreateWindowsSdkRoot(string fixture)
    {
        string root = Path.Combine(fixture, "sdk");
        string libraryDirectory = Path.Combine(root, "10.0.26100.0", "um", "x64");
        Directory.CreateDirectory(libraryDirectory);
        File.WriteAllText(Path.Combine(libraryDirectory, "kernel32.lib"), string.Empty);
        return root;
    }

    private static ProcessResult RunPreflight(string fixture, string msvcLinkerSearchRoot, string windowsSdkLibSearchRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "build-efi-shell.ps1");
        string logPath = Path.Combine(fixture, "efi-shell-publish.log");

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-PreflightOnly");
        start.ArgumentList.Add("-LogPath");
        start.ArgumentList.Add(logPath);
        start.ArgumentList.Add("-MsvcLinkerSearchRoot");
        start.ArgumentList.Add(msvcLinkerSearchRoot);
        start.ArgumentList.Add("-WindowsSdkLibSearchRoot");
        start.ArgumentList.Add(windowsSdkLibSearchRoot);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput + standardError);
    }

    private readonly struct ProcessResult(int exitCode, string output)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;
    }
}
