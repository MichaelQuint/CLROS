using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

public static class ManifestValidator
{
    public const long MaxManifestBytes = 64 * 1024;
    private const int MaxAppIdLength = 128;
    private const int MaxEntryLength = 260;
    private const int MaxArgCount = 64;
    private const int MaxArgLength = 1024;
    private const int MaxEnvCount = 128;
    private const int MaxEnvKeyLength = 64;
    private const int MaxEnvValueLength = 4096;
    private const int MaxRuntimeAssemblyLength = 260;
    private const int MaxKestrelListenEndpoints = 16;
    private const int MaxKestrelHostLength = 255;
    public static ValidationResult Validate(string manifestPath, string profileName)
    {
        WorkloadProfileKind profile = ParseProfile(profileName);
        if (profile == WorkloadProfileKind.Unknown)
        {
            return ValidationResult.Fail("invalid_workload_profile", "Unknown workload profile '" + profileName + "'. Valid values: SingleMinimalApi, MultiMinimalApi.");
        }

        FileInfo manifestFile;
        try
        {
            manifestFile = new FileInfo(manifestPath);
        }
        catch (Exception ex)when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return ValidationResult.Fail("invalid_manifest_path", "Manifest path is invalid.");
        }

        if (!manifestFile.Exists)
        {
            return ValidationResult.Fail("manifest_not_found", "Manifest file was not found.");
        }

        if (manifestFile.Length > MaxManifestBytes)
        {
            return ValidationResult.Fail("manifest_too_large", "Manifest must be 65536 bytes or smaller.");
        }

        try
        {
            using FileStream stream = manifestFile.OpenRead();
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            return ValidateDocument(document.RootElement, profile);
        }
        catch (JsonException)
        {
            return ValidationResult.Fail("malformed_json", "Manifest is not valid JSON.");
        }
        catch (Exception ex)when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return ValidationResult.Fail("manifest_read_failed", "Manifest could not be read.");
        }
    }

    private static ValidationResult ValidateDocument(JsonElement root, WorkloadProfileKind selectedProfile)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("invalid_root", "Manifest root must be an object.");
        }

        if (!TryGetProperty(root, "schemaVersion", out JsonElement schemaVersion))
        {
            return ValidationResult.Fail("missing_schema_version", "schemaVersion is required.");
        }

        if (schemaVersion.ValueKind != JsonValueKind.Number || !schemaVersion.TryGetInt32(out int version))
        {
            return ValidationResult.Fail("invalid_schema_version", "schemaVersion must be integer 0.");
        }

        if (version != 0)
        {
            return ValidationResult.Fail("unknown_schema_version", "Only schemaVersion 0 is supported.");
        }

        ValidationResult profileResult = ValidateDeclaredWorkloadProfile(root, selectedProfile);
        if (!profileResult.Succeeded)
        {
            return profileResult;
        }

        if (selectedProfile == WorkloadProfileKind.MultiMinimalApi)
        {
            return ValidationResult.Fail("multi_minimal_api_unsupported", "MultiMinimalApi manifest validation is unsupported until process, address-space isolation, and supervisor semantics exist.");
        }

        ValidationResult singleProfileResult = ValidateSingleProfileRoot(root);
        if (!singleProfileResult.Succeeded)
        {
            return singleProfileResult;
        }

        if (!TryGetProperty(root, "app", out JsonElement app) || app.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("missing_app", "app object is required.");
        }

        ValidationResult appProfileResult = ValidateSingleProfileApp(app);
        if (!appProfileResult.Succeeded)
        {
            return appProfileResult;
        }

        ValidationResult appIdResult = ValidateAppId(app);
        if (!appIdResult.Succeeded)
        {
            return appIdResult;
        }

        ValidationResult entryResult = ValidateEntry(app);
        if (!entryResult.Succeeded)
        {
            return entryResult;
        }

        if (TryGetProperty(root, "args", out JsonElement args))
        {
            ValidationResult argsResult = ValidateArgs(args);
            if (!argsResult.Succeeded)
            {
                return argsResult;
            }
        }

        if (TryGetProperty(root, "env", out JsonElement env))
        {
            ValidationResult envResult = ValidateEnv(env);
            if (!envResult.Succeeded)
            {
                return envResult;
            }
        }

        if (TryGetProperty(root, "runtime", out JsonElement runtime))
        {
            ValidationResult runtimeResult = ValidateRuntime(runtime);
            if (!runtimeResult.Succeeded)
            {
                return runtimeResult;
            }
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateDeclaredWorkloadProfile(JsonElement root, WorkloadProfileKind selectedProfile)
    {
        if (!TryGetProperty(root, "workloadProfile", out JsonElement declaredProfileElement))
        {
            return ValidationResult.Pass();
        }

        if (declaredProfileElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("invalid_workload_profile", "workloadProfile must be SingleMinimalApi or MultiMinimalApi.");
        }

        string declaredProfileName = declaredProfileElement.GetString() ?? string.Empty;
        WorkloadProfileKind declaredProfile = ParseProfile(declaredProfileName);
        if (declaredProfile == WorkloadProfileKind.Unknown)
        {
            return ValidationResult.Fail("invalid_workload_profile", "Unknown workloadProfile '" + declaredProfileName + "'. Valid values: SingleMinimalApi, MultiMinimalApi.");
        }

        if (declaredProfile != selectedProfile)
        {
            return ValidationResult.Fail("workload_profile_mismatch", "Manifest workloadProfile '" + declaredProfileName + "' does not match selected profile '" + ProfileName(selectedProfile) + "'.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateSingleProfileRoot(JsonElement root)
    {
        if (TryGetProperty(root, "apps", out JsonElement apps))
        {
            if (apps.ValueKind != JsonValueKind.Array)
            {
                return ValidationResult.Fail("single_profile_multiple_apps", "SingleMinimalApi rejects field apps because only one primary app object is supported.");
            }

            int appCount = CountArrayElements(apps);
            return ValidationResult.Fail("single_profile_multiple_apps", "SingleMinimalApi rejects apps count=" + appCount.ToString(CultureInfo.InvariantCulture) + ".");
        }

        ValidationResult unsupportedResult = RejectUnsupportedSingleProfileField(root, "restartPolicy", "single_profile_unsupported_restart_policy");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(root, "resources", "single_profile_unsupported_resources");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(root, "isolation", "single_profile_unsupported_isolation");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(root, "supervisor", "single_profile_unsupported_supervisor");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateSingleProfileApp(JsonElement app)
    {
        ValidationResult unsupportedResult = RejectUnsupportedSingleProfileField(app, "restartPolicy", "single_profile_unsupported_restart_policy", "app.");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(app, "resources", "single_profile_unsupported_resources", "app.");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(app, "isolation", "single_profile_unsupported_isolation", "app.");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(app, "process", "single_profile_unsupported_isolation", "app.");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(app, "addressSpace", "single_profile_unsupported_isolation", "app.");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        unsupportedResult = RejectUnsupportedSingleProfileField(app, "supervisor", "single_profile_unsupported_supervisor", "app.");
        if (!unsupportedResult.Succeeded)
        {
            return unsupportedResult;
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult RejectUnsupportedSingleProfileField(JsonElement element, string fieldName, string code, string prefix = "")
    {
        if (TryGetProperty(element, fieldName, out _))
        {
            return ValidationResult.Fail(code, "SingleMinimalApi rejects unsupported field " + prefix + fieldName + ".");
        }

        return ValidationResult.Pass();
    }

    private static int CountArrayElements(JsonElement array)
    {
        int count = 0;
        foreach (JsonElement _ in array.EnumerateArray())
        {
            count++;
        }

        return count;
    }

    private static ValidationResult ValidateAppId(JsonElement app)
    {
        if (!TryGetProperty(app, "id", out JsonElement idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("missing_app_id", "app.id is required.");
        }

        string? id = idElement.GetString();
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxAppIdLength || !IsValidAppId(id))
        {
            return ValidationResult.Fail("invalid_app_id", "app.id must be a stable ASCII identifier.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateEntry(JsonElement app)
    {
        if (!TryGetProperty(app, "entry", out JsonElement entryElement) || entryElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("missing_app_entry", "app.entry is required.");
        }

        string? entry = entryElement.GetString();
        if (string.IsNullOrWhiteSpace(entry) || entry.Length > MaxEntryLength || !IsSafeRelativePath(entry))
        {
            return ValidationResult.Fail("invalid_app_entry", "app.entry must be a safe relative path.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateArgs(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Array)
        {
            return ValidationResult.Fail("invalid_args", "args must be an array of strings.");
        }

        int count = 0;
        foreach (JsonElement arg in args.EnumerateArray())
        {
            count++;
            if (count > MaxArgCount)
            {
                return ValidationResult.Fail("too_many_args", "args contains too many entries.");
            }

            if (arg.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Fail("invalid_arg", "args entries must be strings.");
            }

            string? value = arg.GetString();
            if (value is null || value.Length > MaxArgLength)
            {
                return ValidationResult.Fail("invalid_arg", "args entries are too long.");
            }
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateEnv(JsonElement env)
    {
        if (env.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("invalid_env", "env must be an object.");
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        foreach (JsonProperty property in env.EnumerateObject())
        {
            count++;
            if (count > MaxEnvCount)
            {
                return ValidationResult.Fail("too_many_env_entries", "env contains too many entries.");
            }

            if (!keys.Add(property.Name))
            {
                return ValidationResult.Fail("duplicate_env_key", "env keys must be unique.");
            }

            if (!IsValidEnvKey(property.Name))
            {
                return ValidationResult.Fail("invalid_env_key", "env keys must be stable ASCII identifiers.");
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Fail("invalid_env_value", "env values must be strings.");
            }

            string? value = property.Value.GetString();
            if (value is null || value.Length > MaxEnvValueLength)
            {
                return ValidationResult.Fail("invalid_env_value", "env values are too long.");
            }
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateRuntime(JsonElement runtime)
    {
        if (runtime.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("invalid_runtime", "runtime must be an object.");
        }

        if (!TryGetProperty(runtime, "kind", out JsonElement kindElement) || kindElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("missing_runtime_kind", "runtime.kind is required when runtime is present.");
        }

        string? kind = kindElement.GetString();
        if (string.Equals(kind, "native-aot", StringComparison.Ordinal))
        {
            return ValidateNativeAotRuntime(runtime);
        }

        if (string.Equals(kind, "dotnet10", StringComparison.Ordinal))
        {
            return ValidateDotNet10Runtime(runtime);
        }

        return ValidationResult.Fail("unknown_runtime_kind", "runtime.kind must be native-aot or dotnet10.");
    }

    private static ValidationResult ValidateNativeAotRuntime(JsonElement runtime)
    {
        if (TryGetProperty(runtime, "assembly", out _) || TryGetProperty(runtime, "kestrel", out _))
        {
            return ValidationResult.Fail("invalid_native_aot_runtime", "runtime.kind native-aot must not declare hosted assembly or Kestrel settings.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateDotNet10Runtime(JsonElement runtime)
    {
        if (!TryGetProperty(runtime, "assembly", out JsonElement assemblyElement) || assemblyElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("missing_runtime_assembly", "runtime.assembly is required for dotnet10.");
        }

        string? assembly = assemblyElement.GetString();
        if (string.IsNullOrWhiteSpace(assembly) || assembly.Length > MaxRuntimeAssemblyLength || !IsSafeRelativePath(assembly))
        {
            return ValidationResult.Fail("invalid_runtime_assembly", "runtime.assembly must be a safe relative path.");
        }

        if (!TryGetProperty(runtime, "kestrel", out JsonElement kestrel) || kestrel.ValueKind != JsonValueKind.Object)
        {
            return ValidationResult.Fail("missing_kestrel", "runtime.kestrel is required for dotnet10.");
        }

        return ValidateKestrel(kestrel);
    }

    private static ValidationResult ValidateKestrel(JsonElement kestrel)
    {
        if (!TryGetProperty(kestrel, "listen", out JsonElement listen) || listen.ValueKind != JsonValueKind.Array)
        {
            return ValidationResult.Fail("invalid_kestrel_listen", "runtime.kestrel.listen must be an array.");
        }

        int count = 0;
        HashSet<string> endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement endpoint in listen.EnumerateArray())
        {
            count++;
            if (count > MaxKestrelListenEndpoints)
            {
                return ValidationResult.Fail("too_many_kestrel_listen_endpoints", "runtime.kestrel.listen contains too many entries.");
            }

            if (endpoint.ValueKind != JsonValueKind.Object)
            {
                return ValidationResult.Fail("invalid_kestrel_endpoint", "runtime.kestrel.listen entries must be objects.");
            }

            ValidationResult endpointResult = ValidateKestrelEndpoint(endpoint, endpoints);
            if (!endpointResult.Succeeded)
            {
                return endpointResult;
            }
        }

        if (count == 0)
        {
            return ValidationResult.Fail("missing_kestrel_listen", "runtime.kestrel.listen must contain at least one endpoint.");
        }

        return ValidationResult.Pass();
    }

    private static ValidationResult ValidateKestrelEndpoint(JsonElement endpoint, HashSet<string> endpoints)
    {
        if (!TryGetProperty(endpoint, "scheme", out JsonElement schemeElement) || schemeElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("invalid_kestrel_scheme", "runtime.kestrel.listen[].scheme must be http.");
        }

        string? scheme = schemeElement.GetString();
        if (!string.Equals(scheme, "http", StringComparison.Ordinal))
        {
            return ValidationResult.Fail("invalid_kestrel_scheme", "Only http Kestrel listen endpoints are reserved; TLS is not claimed.");
        }

        if (!TryGetProperty(endpoint, "host", out JsonElement hostElement) || hostElement.ValueKind != JsonValueKind.String)
        {
            return ValidationResult.Fail("invalid_kestrel_host", "runtime.kestrel.listen[].host is required.");
        }

        string? host = hostElement.GetString();
        if (string.IsNullOrWhiteSpace(host) || host.Length > MaxKestrelHostLength || !IsValidListenHost(host))
        {
            return ValidationResult.Fail("invalid_kestrel_host", "runtime.kestrel.listen[].host must be a stable ASCII host token.");
        }

        if (!TryGetProperty(endpoint, "port", out JsonElement portElement) || portElement.ValueKind != JsonValueKind.Number || !portElement.TryGetInt32(out int port) || port < 1 || port > 65535)
        {
            return ValidationResult.Fail("invalid_kestrel_port", "runtime.kestrel.listen[].port must be in range 1..65535.");
        }

        string key = scheme + "://" + host + ":" + port.ToString(CultureInfo.InvariantCulture);
        if (!endpoints.Add(key))
        {
            return ValidationResult.Fail("duplicate_kestrel_listen", "runtime.kestrel.listen endpoints must be unique.");
        }

        return ValidationResult.Pass();
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

    private static string ProfileName(WorkloadProfileKind profile)
    {
        return profile switch
        {
            WorkloadProfileKind.SingleMinimalApi => WorkloadProfile.SingleMinimalApiName,
            WorkloadProfileKind.MultiMinimalApi => WorkloadProfile.MultiMinimalApiName,
            _ => "Unknown"
        };
    }

    private static bool IsValidAppId(string value)
    {
        if (!IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!IsAsciiLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidEnvKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxEnvKeyLength)
        {
            return false;
        }

        if (!IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeRelativePath(string value)
    {
        if (value[0] == '/' || value[0] == '\\' || value.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        string normalized = value.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (char character in segment)
            {
                if (!IsAsciiLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValidListenHost(string value)
    {
        foreach (char character in value)
        {
            if (!IsAsciiLetterOrDigit(character) && character != '.' && character != '-' && character != '*')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return IsAsciiLetter(character) || character >= '0' && character <= '9';
    }

    private static bool IsAsciiLetter(char character)
    {
        return character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
    }
}
