using System.Text.Json;

namespace OS.Tooling;

public sealed record MinimalApiPackageOptions
{
    public string OutputRoot { get; init; } = "artifacts/kestrel-smoke";
    public string Configuration { get; init; } = "Release";
    public string WorkloadProfile { get; init; } = "SingleMinimalApi";
    public bool SkipValidation { get; init; }
    public bool SkipNegativeValidation { get; init; }

    public static MinimalApiPackageOptions Parse(string[] arguments)
    {
        var options = new MinimalApiPackageOptions();
        for (int i = 0; i < arguments.Length; i++)
        {
            string Value() => ++i < arguments.Length ? arguments[i] : throw new ArgumentException("missing_value " + arguments[i - 1]);
            options = arguments[i] switch
            {
                "--output-root" => options with { OutputRoot = Value() },
                "--configuration" => options with { Configuration = Value() },
                "--workload-profile" => options with { WorkloadProfile = Value() },
                "--skip-validation" => options with { SkipValidation = true },
                "--skip-negative-validation" => options with { SkipNegativeValidation = true },
                _ => throw new ArgumentException("unknown_option " + arguments[i])
            };
        }
        return options;
    }
}

public static class MinimalApiPackage
{
    public static IReadOnlyList<string> RequiredFiles { get; } = Array.AsReadOnly<string>(
        ["MinimalApiSmoke.dll", "MinimalApiSmoke.deps.json", "MinimalApiSmoke.runtimeconfig.json", "appsettings.json"]);

    public static async Task<int> RunAsync(string root, MinimalApiPackageOptions options, TextWriter output,
        Func<ProcessRequest, Task<ProcessResult>>? invoke = null, CancellationToken cancellation = default)
    {
        output.WriteLine("TEST_START kestrel.smoke_app");
        if (options.WorkloadProfile != "SingleMinimalApi")
        {
            output.WriteLine("TEST_FAIL workload.profile.tooling invalid=" + options.WorkloadProfile);
            return 1;
        }
        if (string.IsNullOrWhiteSpace(options.OutputRoot) || string.IsNullOrWhiteSpace(options.Configuration))
            throw new ArgumentException("empty_publish_option");
        output.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
        string destination = Path.GetFullPath(options.OutputRoot, root);
        string packageRoot = Path.Combine(destination, "package-root");
        string appRoot = Path.Combine(packageRoot, "apps/minimal-api");
        string manifest = Path.Combine(destination, "package.json");
        Directory.CreateDirectory(appRoot);
        invoke ??= request => ProcessRunner.RunAsync(request, cancellation);
        var publish = await invoke(new("dotnet", ["publish", Path.Combine(root, "apps/MinimalApiSmoke/MinimalApiSmoke.csproj"),
            "--configuration", options.Configuration, "--framework", "net10.0", "--self-contained", "false", "-p:UseAppHost=false",
            "/p:OSWorkloadProfile=" + options.WorkloadProfile, "--output", appRoot], root) { Timeout = TimeSpan.FromMinutes(3) });
        output.Write(publish.StandardOutput);
        output.Write(publish.StandardError);
        if (!publish.Succeeded)
        {
            output.WriteLine("TEST_FAIL kestrel.smoke_app publish_failed exit=" + publish.ExitCode);
            return publish.ExitCode == 0 ? 1 : publish.ExitCode;
        }
        foreach (string name in RequiredFiles)
            if (!File.Exists(Path.Combine(appRoot, name)))
            {
                output.WriteLine("TEST_FAIL kestrel.smoke_app missing_publish_file file=" + name);
                return 1;
            }
        WriteManifest(manifest, includeExpectedBody: true);
        output.WriteLine($"KESTREL_SMOKE_PACKAGE manifest={manifest} packageRoot={packageRoot}");
        output.WriteLine("TEST_PASS kestrel.smoke_app publishes package");

        Task<ProcessResult> Validate(string path) => invoke(new("dotnet", [HostTools.ManagedTool(root, "OS.ManifestTool"),
            "validate-kestrel-package", "--manifest", path, "--package-root", packageRoot, "--profile", options.WorkloadProfile], root));
        if (!options.SkipValidation)
        {
            var validation = await Validate(manifest);
            output.Write(validation.StandardOutput);
            output.Write(validation.StandardError);
            if (!validation.Succeeded)
            {
                output.WriteLine("TEST_FAIL kestrel.smoke_app validation_failed exit=" + validation.ExitCode);
                return validation.ExitCode == 0 ? 1 : validation.ExitCode;
            }
            output.WriteLine("TEST_PASS kestrel.smoke_app validates package contract");
        }
        if (!options.SkipNegativeValidation)
        {
            string negative = Path.Combine(destination, "package.missing-expected-body.json");
            WriteManifest(negative, includeExpectedBody: false);
            var validation = await Validate(negative);
            if (validation.ExitCode == 0 || validation.TimedOut || validation.Cancelled || validation.OutputLimitExceeded ||
                !validation.TerminationConfirmed || validation.FailureReason.Length != 0 ||
                !(validation.StandardOutput + validation.StandardError).Contains("kestrel_package_missing_expected_env", StringComparison.Ordinal))
            {
                output.WriteLine("TEST_FAIL kestrel.smoke_app negative_validation_unexpected exit=" + validation.ExitCode);
                return 1;
            }
            output.WriteLine("TEST_PASS kestrel.smoke_app rejects missing expected body");
        }
        output.WriteLine("TEST_PASS kestrel.smoke_app");
        return 0;
    }

    public static void WriteManifest(string path, bool includeExpectedBody)
    {
        Dictionary<string, string> environment = new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production", ["ASPNETCORE_URLS"] = "http://0.0.0.0:8080"
        };
        if (includeExpectedBody) environment.Add("OS_SMOKE_EXPECTED_BODY", "OS_MINIMAL_API_OK");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            schemaVersion = 0, workloadProfile = "SingleMinimalApi",
            app = new { id = "minimal.api.smoke", entry = "apps/minimal-api/MinimalApiSmoke.dll" },
            args = Array.Empty<string>(), env = environment,
            runtime = new { kind = "dotnet10", assembly = "apps/minimal-api/MinimalApiSmoke.dll",
                kestrel = new { listen = new[] { new { scheme = "http", host = "0.0.0.0", port = 8080 } } } }
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new System.Text.UTF8Encoding(false));
    }
}
