using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

public static class KestrelSmokePackageContractValidator
{
    public const string ContractId = "kestrel.smoke.package.v0";
    private const string RequiredAppId = "minimal.api.smoke";
    private const string RequiredAssemblyPath = "apps/minimal-api/MinimalApiSmoke.dll";
    private const string RequiredDepsPath = "apps/minimal-api/MinimalApiSmoke.deps.json";
    private const string RequiredRuntimeConfigPath = "apps/minimal-api/MinimalApiSmoke.runtimeconfig.json";
    private const string RequiredAppSettingsPath = "apps/minimal-api/appsettings.json";
    private const string RequiredExpectedBodyKey = "OS_SMOKE_EXPECTED_BODY";
    private const string RequiredExpectedBodyValue = "OS_MINIMAL_API_OK";
    private const string RequiredRuntimeKind = "dotnet10";
    private const string RequiredKestrelScheme = "http";
    private const string RequiredKestrelHost = "0.0.0.0";
    private const int RequiredKestrelPort = 8080;
    private static readonly string[] RequiredPackageFiles = [RequiredAssemblyPath, RequiredDepsPath, RequiredRuntimeConfigPath, RequiredAppSettingsPath];
    public static ValidationResult Validate(string manifestPath, string packageRootPath, string profileName, string contractId)
    {
        WorkloadProfileKind profile = ParseProfile(profileName);
        if (profile == WorkloadProfileKind.Unknown)
        {
            return ValidationResult.Fail("invalid_workload_profile", "Unknown workload profile. Valid values: SingleMinimalApi, MultiMinimalApi.");
        }

        if (profile != WorkloadProfileKind.SingleMinimalApi)
        {
            return ValidationResult.Fail("kestrel_package_unsupported_profile", ContractId + " supports SingleMinimalApi only until process and multi-app supervision exist.");
        }

        if (!string.Equals(contractId, ContractId, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_unsupported_contract", "Only " + ContractId + " is supported.");
        }

        ValidationResult manifestResult = ManifestValidator.Validate(manifestPath, profileName);
        if (!manifestResult.Succeeded)
        {
            return manifestResult;
        }

        DirectoryInfo packageRoot;
        try
        {
            packageRoot = new DirectoryInfo(packageRootPath);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return ValidationResult.Fail("kestrel_package_invalid_root", "Package root path is invalid.");
        }

        if (!packageRoot.Exists)
        {
            return ValidationResult.Fail("kestrel_package_root_not_found", "Package root directory was not found.");
        }

        foreach (string requiredFile in RequiredPackageFiles)
        {
            FileInfo file = new FileInfo(JoinPackagePath(packageRoot.FullName, requiredFile));
            if (!file.Exists)
            {
                return ValidationResult.Fail("kestrel_package_missing_file", "Required package file is missing: " + requiredFile + ".");
            }
        }

        return ValidateManifestContract(manifestPath);
    }

    private static ValidationResult ValidateManifestContract(string manifestPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(manifestPath);
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            JsonElement root = document.RootElement;
            ValidationResult appResult = ValidateApp(root);
            if (!appResult.Succeeded)
            {
                return appResult;
            }

            ValidationResult environmentResult = ValidateEnvironment(root);
            if (!environmentResult.Succeeded)
            {
                return environmentResult;
            }

            return ValidateRuntime(root);
        }
        catch (JsonException)
        {
            return ValidationResult.Fail("malformed_json", "Manifest is not valid JSON.");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return ValidationResult.Fail("manifest_read_failed", "Manifest could not be read.");
        }
    }

    private static ValidationResult ValidateApp(JsonElement root)
    {
        if (!TryGetProperty(root, "app", out JsonElement app) || app.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("missing_app", "app object is required.");
        }

        if (!TryString(app, "id", out string id) || !string.Equals(id, RequiredAppId, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_invalid_app_id", "app.id must be " + RequiredAppId + ".");
        }

        if (!TryString(app, "entry", out string entry) || !string.Equals(entry, RequiredAssemblyPath, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_invalid_app_entry", "app.entry must match the hosted assembly path.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateEnvironment(JsonElement root)
    {
        if (!TryGetProperty(root, "env", out JsonElement env) || env.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("kestrel_package_missing_expected_env", RequiredExpectedBodyKey + " is required in env.");
        }

        if (!TryString(env, RequiredExpectedBodyKey, out string expectedBody))
        {
            return ValidationResult.Fail("kestrel_package_missing_expected_env", RequiredExpectedBodyKey + " is required in env.");
        }

        if (!string.Equals(expectedBody, RequiredExpectedBodyValue, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_invalid_expected_env", RequiredExpectedBodyKey + " must match the package contract.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateRuntime(JsonElement root)
    {
        if (!TryGetProperty(root, "runtime", out JsonElement runtime) || runtime.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("missing_runtime_kind", "runtime object is required for " + ContractId + ".");
        }

        if (!TryString(runtime, "kind", out string kind) || !string.Equals(kind, RequiredRuntimeKind, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_unsupported_runtime", "runtime.kind must be dotnet10.");
        }

        if (!TryString(runtime, "assembly", out string assembly) || !string.Equals(assembly, RequiredAssemblyPath, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_invalid_runtime_assembly", "runtime.assembly must match the hosted assembly path.");
        }

        if (!TryGetProperty(runtime, "kestrel", out JsonElement kestrel) || kestrel.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("missing_kestrel", "runtime.kestrel is required for " + ContractId + ".");
        }

        return ValidateKestrelListen(kestrel);
    }

    private static ValidationResult ValidateKestrelListen(JsonElement kestrel)
    {
        if (!TryGetProperty(kestrel, "listen", out JsonElement listen) || listen.ValueKind != JsonValueKind.Array)
        {
            return ValidationResult.Fail("invalid_kestrel_listen", "runtime.kestrel.listen must be an array.");
        }

        int count = 0;
        JsonElement endpoint = default;
        foreach (JsonElement item in listen.EnumerateArray())
        {
            count++;
            endpoint = item;
        }

        if (count != 1 || endpoint.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("kestrel_package_invalid_listen", ContractId + " requires exactly one Kestrel listen endpoint.");
        }

        if (!TryString(endpoint, "scheme", out string scheme) || !string.Equals(scheme, RequiredKestrelScheme, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_invalid_listen", ContractId + " requires the http listen scheme.");
        }

        if (!TryString(endpoint, "host", out string host) || !string.Equals(host, RequiredKestrelHost, StringComparison.Ordinal))
        {
            return ValidationResult.Fail("kestrel_package_invalid_listen", ContractId + " requires host 0.0.0.0.");
        }

        if (!TryGetProperty(endpoint, "port", out JsonElement portElement) || portElement.ValueKind != JsonValueKind.Number || !portElement.TryGetInt32(out int port) || port != RequiredKestrelPort)
        {
            return ValidationResult.Fail("kestrel_package_invalid_listen", ContractId + " requires port " + RequiredKestrelPort.ToString(CultureInfo.InvariantCulture) + ".");
        }

        return ValidationResult.Pass();
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        if (TryGetProperty(element, name, out JsonElement property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static WorkloadProfileKind ParseProfile(string profileName)
    {
        if (string.Equals(profileName, WorkloadProfile.SingleMinimalApiName, StringComparison.Ordinal))
        {
            return WorkloadProfileKind.SingleMinimalApi;
        }

        if (string.Equals(profileName, WorkloadProfile.MultiMinimalApiName, StringComparison.Ordinal))
        {
            return WorkloadProfileKind.MultiMinimalApi;
        }

        return WorkloadProfileKind.Unknown;
    }

    private static string JoinPackagePath(string packageRootPath, string packageRelativePath)
    {
        string[] segments = packageRelativePath.Split('/');
        string path = packageRootPath;
        foreach (string segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }
}
