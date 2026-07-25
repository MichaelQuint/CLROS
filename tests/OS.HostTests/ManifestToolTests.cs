using System.Diagnostics;
using System.Runtime.CompilerServices;
using OS.Kernel.Build;

internal static class ManifestToolTests
{
    private const int OversizedManifestLength = 64 * 1024 + 1;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-manifesttool-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
#if OS_PROFILE_SINGLE_MINIMAL_API
            Run("manifesttool validates default single minimal api profile", ref failures, () =>
                ExpectValidationForProfile(tempRoot, "default-single-profile.json", ValidManifest(), 0, null, "TEST_PASS manifest.validate"));
#elif OS_PROFILE_MULTI_MINIMAL_API
            Run("manifesttool rejects default multi minimal api profile", ref failures, () =>
                ExpectValidationForProfile(
                    tempRoot,
                    "default-multi-profile.json",
                    ValidManifest(),
                    1,
                    null,
                    "multi_minimal_api_unsupported"));
#else
#error ManifestToolTests require one OS workload profile symbol.
#endif

            Run("manifesttool validates schema v0", ref failures, () =>
                ExpectValidation(tempRoot, "valid.json", ValidManifest(), 0, "TEST_PASS manifest.validate"));

            Run("manifesttool validates native-aot runtime", ref failures, () =>
                ExpectValidation(tempRoot, "native-aot-runtime.json", NativeAotRuntimeManifest(), 0, "TEST_PASS manifest.validate"));

            Run("manifesttool validates reserved dotnet10 kestrel contract", ref failures, () =>
                ExpectValidation(tempRoot, "dotnet10-runtime.json", DotNet10RuntimeManifest("apps/api/MinimalApi.dll", ValidKestrelListen()), 0, "TEST_PASS manifest.validate"));

            Run("manifesttool rejects multiple apps under single minimal api", ref failures, () =>
                ExpectValidationForProfile(
                    tempRoot,
                    "single-profile-multiple-apps.json",
                    MultiAppManifest(),
                    1,
                    WorkloadProfile.SingleMinimalApiName,
                    "single_profile_multiple_apps",
                    "SingleMinimalApi",
                    "count=2"));

            Run("manifesttool rejects multi app restart policy under single minimal api", ref failures, () =>
                ExpectValidationForProfile(
                    tempRoot,
                    "single-profile-restart-policy.json",
                    ManifestWithRootRestartPolicy(),
                    1,
                    WorkloadProfile.SingleMinimalApiName,
                    "single_profile_unsupported_restart_policy",
                    "SingleMinimalApi",
                    "restartPolicy"));

            Run("manifesttool rejects isolation resources under single minimal api", ref failures, () =>
                ExpectValidationForProfile(
                    tempRoot,
                    "single-profile-app-resources.json",
                    ManifestWithAppResources(),
                    1,
                    WorkloadProfile.SingleMinimalApiName,
                    "single_profile_unsupported_resources",
                    "app.resources")
                && ExpectValidationForProfile(
                    tempRoot,
                    "single-profile-app-isolation.json",
                    ManifestWithAppIsolation(),
                    1,
                    WorkloadProfile.SingleMinimalApiName,
                    "single_profile_unsupported_isolation",
                    "app.isolation"));

            Run("manifesttool rejects multi minimal api until process model exists", ref failures, () =>
                ExpectValidationForProfile(
                    tempRoot,
                    "multi-profile-unsupported.json",
                    ValidManifest(),
                    1,
                    WorkloadProfile.MultiMinimalApiName,
                    "multi_minimal_api_unsupported",
                    "MultiMinimalApi",
                    "process"));

            Run("manifesttool rejects workload profile mismatch", ref failures, () =>
                ExpectValidationForProfile(
                    tempRoot,
                    "profile-mismatch.json",
                    ManifestWithDeclaredProfile(WorkloadProfile.MultiMinimalApiName),
                    1,
                    WorkloadProfile.SingleMinimalApiName,
                    "workload_profile_mismatch",
                    WorkloadProfile.MultiMinimalApiName,
                    WorkloadProfile.SingleMinimalApiName));

            Run("manifesttool rejects malformed json", ref failures, () =>
                ExpectValidation(tempRoot, "malformed.json", "{", 1, "malformed_json"));

            Run("manifesttool rejects unknown schema version", ref failures, () =>
                ExpectValidation(tempRoot, "unknown-version.json", ManifestWithSchemaVersion(1), 1, "unknown_schema_version"));

            Run("manifesttool rejects unknown runtime kind", ref failures, () =>
                ExpectValidation(tempRoot, "unknown-runtime-kind.json", ManifestWithRuntimeKind("wasm"), 1, "unknown_runtime_kind"));

            Run("manifesttool rejects missing app identity fields", ref failures, () =>
                ExpectValidation(tempRoot, "missing-app-id.json", ManifestWithoutAppId(), 1, "missing_app_id")
                && ExpectValidation(tempRoot, "missing-app-entry.json", ManifestWithoutEntry(), 1, "missing_app_entry"));

            Run("manifesttool rejects unsafe app entry paths", ref failures, () =>
                ExpectValidation(tempRoot, "absolute-entry.json", ManifestWithEntry("/bin/app"), 1, "invalid_app_entry")
                && ExpectValidation(tempRoot, "traversal-entry.json", ManifestWithEntry("../bin/app"), 1, "invalid_app_entry"));

            Run("manifesttool rejects unsafe dotnet10 assembly paths", ref failures, () =>
                ExpectValidation(tempRoot, "absolute-assembly.json", DotNet10RuntimeManifest("/apps/api/MinimalApi.dll", ValidKestrelListen()), 1, "invalid_runtime_assembly")
                && ExpectValidation(tempRoot, "traversal-assembly.json", DotNet10RuntimeManifest("../api/MinimalApi.dll", ValidKestrelListen()), 1, "invalid_runtime_assembly"));

            Run("manifesttool rejects malformed dotnet10 runtime shapes", ref failures, () =>
                ExpectValidation(tempRoot, "missing-assembly.json", DotNet10RuntimeWithoutAssembly(), 1, "missing_runtime_assembly")
                && ExpectValidation(tempRoot, "missing-kestrel.json", DotNet10RuntimeWithoutKestrel(), 1, "missing_kestrel")
                && ExpectValidation(tempRoot, "bad-kestrel-listen-shape.json", DotNet10RuntimeWithKestrel("{ \"listen\": \"http://0.0.0.0:8080\" }"), 1, "invalid_kestrel_listen"));

            Run("manifesttool rejects invalid kestrel listen endpoints", ref failures, () =>
                ExpectValidation(tempRoot, "https-listen.json", DotNet10RuntimeManifest("apps/api/MinimalApi.dll", """
[
  { "scheme": "https", "host": "0.0.0.0", "port": 8443 }
]
"""), 1, "invalid_kestrel_scheme")
                && ExpectValidation(tempRoot, "zero-port-listen.json", DotNet10RuntimeManifest("apps/api/MinimalApi.dll", """
[
  { "scheme": "http", "host": "0.0.0.0", "port": 0 }
]
"""), 1, "invalid_kestrel_port")
                && ExpectValidation(tempRoot, "duplicate-listen.json", DotNet10RuntimeManifest("apps/api/MinimalApi.dll", """
[
  { "scheme": "http", "host": "0.0.0.0", "port": 8080 },
  { "scheme": "http", "host": "0.0.0.0", "port": 8080 }
]
"""), 1, "duplicate_kestrel_listen")
                && ExpectValidation(tempRoot, "malformed-listen-entry.json", DotNet10RuntimeManifest("apps/api/MinimalApi.dll", """
[
  "http://0.0.0.0:8080"
]
"""), 1, "invalid_kestrel_endpoint"));

            Run("manifesttool rejects invalid args and env shapes", ref failures, () =>
                ExpectValidation(tempRoot, "invalid-args.json", ManifestWithInvalidArg(), 1, "invalid_arg")
                && ExpectValidation(tempRoot, "invalid-env-key.json", ManifestWithInvalidEnvKey(), 1, "invalid_env_key")
                && ExpectValidation(tempRoot, "invalid-env-value.json", ManifestWithInvalidEnvValue(), 1, "invalid_env_value"));

            Run("manifesttool rejects duplicate env keys", ref failures, () =>
                ExpectValidation(tempRoot, "duplicate-env.json", DuplicateEnvManifest(), 1, "duplicate_env_key"));

            Run("manifesttool rejects oversized manifests", ref failures, () =>
                ExpectValidation(tempRoot, "oversized.json", new string(' ', OversizedManifestLength), 1, "manifest_too_large"));
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Test cleanup failure should not hide validation results.
            }
        }

        if (failures > 0)
        {
            throw new InvalidOperationException("Manifest tool host tests failed: " + failures);
        }
    }

    private static void Run(string name, ref int failures, Func<bool> test)
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

    private static bool ExpectValidation(
        string tempRoot,
        string fileName,
        string contents,
        int expectedExitCode,
        params string[] expectedOutput)
    {
        return ExpectValidationForProfile(
            tempRoot,
            fileName,
            contents,
            expectedExitCode,
            WorkloadProfile.SingleMinimalApiName,
            expectedOutput);
    }

    private static bool ExpectValidationForProfile(
        string tempRoot,
        string fileName,
        string contents,
        int expectedExitCode,
        string? profileName,
        params string[] expectedOutput)
    {
        string manifestPath = Path.Combine(tempRoot, fileName);
        File.WriteAllText(manifestPath, contents);

        ToolResult result = RunManifestTool(manifestPath, profileName);
        bool passed = result.ExitCode == expectedExitCode
            && result.Output.Contains("TEST_START manifest.validate", StringComparison.Ordinal);

        foreach (string expected in expectedOutput)
        {
            passed = passed && result.Output.Contains(expected, StringComparison.Ordinal);
        }

        if (!passed)
        {
            Console.WriteLine("Expected exit=" + expectedExitCode + " output=" + string.Join(",", expectedOutput));
            Console.WriteLine("Actual exit=" + result.ExitCode);
            Console.WriteLine("Actual output=" + result.Output.Replace(Environment.NewLine, "\\n", StringComparison.Ordinal));
        }

        return passed;
    }

    private static ToolResult RunManifestTool(string manifestPath, string? profileName)
    {
        string repoRoot = HostToolProcess.FindRepositoryRoot();
        string toolPath = HostToolProcess.GetBuiltToolPath(repoRoot, "OS.ManifestTool");
        if (!File.Exists(toolPath))
        {
            return new ToolResult(-1, "manifest tool not found: " + toolPath);
        }

        using Process process = new Process();
        process.StartInfo = HostToolProcess.CreateDotnetToolStartInfo(repoRoot, "OS.ManifestTool");
        process.StartInfo.ArgumentList.Add("validate");
        process.StartInfo.ArgumentList.Add("--manifest");
        process.StartInfo.ArgumentList.Add(manifestPath);
        if (profileName is not null)
        {
            process.StartInfo.ArgumentList.Add("--profile");
            process.StartInfo.ArgumentList.Add(profileName);
        }

        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(30000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Timeout is already the actionable failure.
            }

            return new ToolResult(-1, "manifest tool timed out");
        }

        string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        return new ToolResult(process.ExitCode, output);
    }

    private static string ValidManifest()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "args": [
    "--mode",
    "smoke"
  ],
  "env": {
    "APP_MODE": "smoke"
  }
}
""";
    }

    private static string NativeAotRuntimeManifest()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "runtime": {
    "kind": "native-aot"
  }
}
""";
    }

    private static string MultiAppManifest()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.one",
    "entry": "apps/api-one/host.bin"
  },
  "apps": [
    {
      "id": "minimal.api.one",
      "entry": "apps/api-one/host.bin"
    },
    {
      "id": "minimal.api.two",
      "entry": "apps/api-two/host.bin"
    }
  ]
}
""";
    }

    private static string ManifestWithRootRestartPolicy()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  },
  "restartPolicy": {
    "mode": "always"
  }
}
""";
    }

    private static string ManifestWithAppResources()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin",
    "resources": {
      "memoryMiB": 128
    }
  }
}
""";
    }

    private static string ManifestWithAppIsolation()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin",
    "isolation": {
      "addressSpace": "private"
    }
  }
}
""";
    }

    private static string ManifestWithDeclaredProfile(string profileName)
    {
        return """
{
  "schemaVersion": 0,
  "workloadProfile": "PROFILE",
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  }
}
""".Replace("PROFILE", profileName, StringComparison.Ordinal);
    }

    private static string ManifestWithRuntimeKind(string kind)
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "runtime": {
    "kind": "KIND"
  }
}
""".Replace("KIND", kind, StringComparison.Ordinal);
    }

    private static string DotNet10RuntimeManifest(string assembly, string listen)
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "ASSEMBLY",
    "kestrel": {
      "listen": LISTEN
    }
  }
}
""".Replace("ASSEMBLY", assembly, StringComparison.Ordinal)
   .Replace("LISTEN", listen, StringComparison.Ordinal);
    }

    private static string DotNet10RuntimeWithoutAssembly()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  },
  "runtime": {
    "kind": "dotnet10",
    "kestrel": {
      "listen": [
        { "scheme": "http", "host": "0.0.0.0", "port": 8080 }
      ]
    }
  }
}
""";
    }

    private static string DotNet10RuntimeWithoutKestrel()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/api/MinimalApi.dll"
  }
}
""";
    }

    private static string DotNet10RuntimeWithKestrel(string kestrel)
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/api/MinimalApi.dll",
    "kestrel": KESTREL
  }
}
""".Replace("KESTREL", kestrel, StringComparison.Ordinal);
    }

    private static string ValidKestrelListen()
    {
        return """
[
  { "scheme": "http", "host": "0.0.0.0", "port": 8080 },
  { "scheme": "http", "host": "127.0.0.1", "port": 8081 }
]
""";
    }

    private static string ManifestWithSchemaVersion(int version)
    {
        return """
{
  "schemaVersion": VERSION,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  }
}
""".Replace("VERSION", version.ToString(), StringComparison.Ordinal);
    }

    private static string ManifestWithEntry(string entry)
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "ENTRY"
  }
}
""".Replace("ENTRY", entry, StringComparison.Ordinal);
    }

    private static string ManifestWithoutAppId()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "entry": "apps/hello.bin"
  }
}
""";
    }

    private static string ManifestWithoutEntry()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app"
  }
}
""";
    }

    private static string ManifestWithInvalidArg()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "args": [
    "--mode",
    1
  ]
}
""";
    }

    private static string ManifestWithInvalidEnvKey()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "env": {
    "1BAD": "value"
  }
}
""";
    }

    private static string ManifestWithInvalidEnvValue()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "env": {
    "GOOD": 1
  }
}
""";
    }

    private static string DuplicateEnvManifest()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "env": {
    "DUP": "one",
    "DUP": "two"
  }
}
""";
    }

    private sealed class ToolResult
    {
        public ToolResult(int exitCode, string output)
        {
            this.ExitCode = exitCode;
            this.Output = output;
        }

        public int ExitCode { get; }

        public string Output { get; }
    }
}
