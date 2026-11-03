using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;

internal static partial class KestrelSmokePackageContractTests
{
    private const string ContractId = "kestrel.smoke.package.v0";
    private const string AppId = "minimal.api.smoke";
    private const string ExpectedBodyKey = "OS_SMOKE_EXPECTED_BODY";
    private const string ExpectedBody = "OS_MINIMAL_API_OK";
    private const int ExpectedBodyBytes = 17;
    private const string ExpectedBodySha256 = "87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d";
    private const string AssemblyPath = "apps/minimal-api/MinimalApiSmoke.dll";
    private const string DepsPath = "apps/minimal-api/MinimalApiSmoke.deps.json";
    private const string RuntimeConfigPath = "apps/minimal-api/MinimalApiSmoke.runtimeconfig.json";
    private const string AppSettingsPath = "apps/minimal-api/appsettings.json";
    private const string MinimalApiSmokeProjectPath = "apps/MinimalApiSmoke/MinimalApiSmoke.csproj";
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-kestrel-package-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        HostFixturePublish? hostFixture = null;
        try
        {
            Run("kestrel.smoke_app host fixture publishes", ref failures, () =>
            {
                hostFixture = PublishMinimalApiSmokeFixture(tempRoot);
                return hostFixture.Value.Valid;
            });
            Run("kestrel.smoke_app host healthz returns configured body", ref failures, () => hostFixture.HasValue && hostFixture.Value.Valid && TestHostHealthRoute(hostFixture.Value.PublishDirectory));
            Run("kestrel.smoke_app host config exposes contract metadata", ref failures, () => hostFixture.HasValue && hostFixture.Value.Valid && TestHostConfigRoute(hostFixture.Value.PublishDirectory));
            Run("kestrel.smoke_app host env override controls body", ref failures, () => hostFixture.HasValue && hostFixture.Value.Valid && TestHostEnvironmentOverride(hostFixture.Value.PublishDirectory));
            Run("kestrel.package.contract validates layout", ref failures, () =>
            {
                string manifestPath = WriteManifest(tempRoot, "valid-manifest.json", ValidManifest());
                string packageRoot = CreatePackageRoot(tempRoot, "valid-package");
                return ExpectKestrelPackageValidation(manifestPath, packageRoot, WorkloadProfile.SingleMinimalApiName, 0, "TEST_START kestrel.package.contract", "KESTREL_PACKAGE_CONTRACT " + ContractId, "TEST_PASS kestrel.package.contract");
            });
            Run("kestrel.package.contract rejects missing files", ref failures, () =>
            {
                string manifestPath = WriteManifest(tempRoot, "missing-files-manifest.json", ValidManifest());
                return ExpectKestrelPackageValidation(manifestPath, CreatePackageRoot(tempRoot, "missing-app", AssemblyPath), WorkloadProfile.SingleMinimalApiName, 1, "kestrel_package_missing_file", AssemblyPath) && ExpectKestrelPackageValidation(manifestPath, CreatePackageRoot(tempRoot, "missing-deps", DepsPath), WorkloadProfile.SingleMinimalApiName, 1, "kestrel_package_missing_file", DepsPath) && ExpectKestrelPackageValidation(manifestPath, CreatePackageRoot(tempRoot, "missing-runtimeconfig", RuntimeConfigPath), WorkloadProfile.SingleMinimalApiName, 1, "kestrel_package_missing_file", RuntimeConfigPath);
            });
            Run("kestrel.package.contract requires expected env", ref failures, () =>
            {
                string manifestPath = WriteManifest(tempRoot, "missing-expected-env.json", ManifestWithoutExpectedBody());
                string packageRoot = CreatePackageRoot(tempRoot, "missing-expected-env-package");
                return ExpectKestrelPackageValidation(manifestPath, packageRoot, WorkloadProfile.SingleMinimalApiName, 1, "kestrel_package_missing_expected_env", "OS_SMOKE_EXPECTED_BODY");
            });
            Run("kestrel.package.contract rejects unsupported profile", ref failures, () =>
            {
                string manifestPath = WriteManifest(tempRoot, "multi-profile-manifest.json", ValidManifest());
                string packageRoot = CreatePackageRoot(tempRoot, "multi-profile-package");
                return ExpectKestrelPackageValidation(manifestPath, packageRoot, WorkloadProfile.MultiMinimalApiName, 1, "kestrel_package_unsupported_profile", WorkloadProfile.SingleMinimalApiName);
            });
            Run("kestrel.package.contract rejects unsupported runtime", ref failures, () =>
            {
                string manifestPath = WriteManifest(tempRoot, "native-runtime-manifest.json", NativeRuntimeManifest());
                string packageRoot = CreatePackageRoot(tempRoot, "native-runtime-package");
                return ExpectKestrelPackageValidation(manifestPath, packageRoot, WorkloadProfile.SingleMinimalApiName, 1, "kestrel_package_unsupported_runtime", "dotnet10");
            });
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            // Cleanup failure should not hide contract validation failures.
            }
        }

        if (failures > 0)
        {
            throw new InvalidOperationException("Kestrel smoke package contract tests failed: " + failures);
        }
    }

    private static HostFixturePublish PublishMinimalApiSmokeFixture(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, MinimalApiSmokeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        string programPath = Path.Combine(repositoryRoot, "apps", "MinimalApiSmoke", "Program.cs");
        string appSettingsPath = Path.Combine(repositoryRoot, "apps", "MinimalApiSmoke", "appsettings.json");
        string publishDirectory = Path.Combine(tempRoot, "minimal-api-smoke-publish");
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("MinimalApiSmoke project missing.", projectPath);
        }

        if (!ValidateMinimalApiSmokeSourceContract(projectPath, programPath, appSettingsPath))
        {
            return new HostFixturePublish(publishDirectory, false);
        }

        Directory.CreateDirectory(publishDirectory);
        ProcessResult publishResult = RunDotnet(repositoryRoot, "publish", projectPath, "--configuration", "Debug", "--framework", "net11.0", "--self-contained", "false", "-p:UseAppHost=false", "/p:OSWorkloadProfile=" + WorkloadProfile.SingleMinimalApiName, "--output", publishDirectory, "--nologo");
        if (publishResult.ExitCode != 0)
        {
            Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.publish exit=" + publishResult.ExitCode);
            Console.WriteLine(publishResult.Output);
            throw new InvalidOperationException("MinimalApiSmoke publish failed.");
        }

        string[] requiredFiles = ["MinimalApiSmoke.dll", "MinimalApiSmoke.deps.json", "MinimalApiSmoke.runtimeconfig.json", "appsettings.json"];
        foreach (string requiredFile in requiredFiles)
        {
            string requiredPath = Path.Combine(publishDirectory, requiredFile);
            if (!File.Exists(requiredPath))
            {
                Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.publish missing=" + requiredFile);
                return new HostFixturePublish(publishDirectory, false);
            }
        }

        string publishedAppSettings = Path.Combine(publishDirectory, "appsettings.json");
        return new HostFixturePublish(publishDirectory, ValidateMinimalApiSmokeAppSettings(publishedAppSettings));
    }

    private static bool ValidateMinimalApiSmokeSourceContract(string projectPath, string programPath, string appSettingsPath)
    {
        string project = File.ReadAllText(projectPath);
        string program = File.ReadAllText(programPath);
        bool valid = true;
        valid &= ContainsOrReport(project, "<TargetFramework>net11.0</TargetFramework>", projectPath);
        valid &= ContainsOrReport(project, "<LangVersion>14.0</LangVersion>", projectPath);
        valid &= ContainsOrReport(project, "<Nullable>enable</Nullable>", projectPath);
        valid &= ContainsOrReport(project, "<InvariantGlobalization>true</InvariantGlobalization>", projectPath);
        valid &= ContainsOrReport(program, "WebApplication.CreateSlimBuilder(args)", programPath);
        valid &= ContainsOrReport(program, "const string AppId = \"" + AppId + "\";", programPath);
        valid &= ContainsOrReport(program, "const string ContractId = \"" + ContractId + "\";", programPath);
        valid &= ContainsOrReport(program, "const string ExpectedBodyKey = \"" + ExpectedBodyKey + "\";", programPath);
        valid &= ContainsOrReport(program, "app.MapGet(\"/\",", programPath);
        valid &= ContainsOrReport(program, "app.MapGet(\"/healthz\",", programPath);
        valid &= ContainsOrReport(program, "app.MapGet(\"/config\",", programPath);
        valid &= ContainsOrReport(program, "JsonSerializerContext", programPath);
        valid &= ContainsOrReport(program, "KestrelSmokeJsonContext.Default", programPath);
        valid &= ValidateMinimalApiSmokeAppSettings(appSettingsPath);
        return valid;
    }

    private static bool ValidateMinimalApiSmokeAppSettings(string appSettingsPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        if (!document.RootElement.TryGetProperty(ExpectedBodyKey, out JsonElement expectedBodyElement) || expectedBodyElement.GetString() != ExpectedBody)
        {
            Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.appsettings missing_key=" + ExpectedBodyKey);
            return false;
        }

        return true;
    }

    private static bool ContainsOrReport(string contents, string expected, string path)
    {
        if (contents.Contains(expected, StringComparison.Ordinal))
        {
            return true;
        }

        Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.source missing=\"" + expected + "\" path=" + path);
        return false;
    }

    private static bool TestHostHealthRoute(string publishDirectory)
    {
        using RunningMinimalApiSmoke app = StartMinimalApiSmoke(publishDirectory, ExpectedBody);
        HttpResponseSummary root = GetResponse(app.BaseUri, "/");
        HttpResponseSummary health = GetResponse(app.BaseUri, "/healthz");
        return IsExpectedSmokeBody(root, ExpectedBody, ExpectedBodyBytes, ExpectedBodySha256) && IsExpectedSmokeBody(health, ExpectedBody, ExpectedBodyBytes, ExpectedBodySha256);
    }

    private static bool TestHostConfigRoute(string publishDirectory)
    {
        using RunningMinimalApiSmoke app = StartMinimalApiSmoke(publishDirectory, ExpectedBody);
        HttpResponseSummary config = GetResponse(app.BaseUri, "/config");
        if (config.StatusCode != HttpStatusCode.OK || !string.Equals(config.ContentType, "application/json", StringComparison.Ordinal))
        {
            Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.config status=" + ((int)config.StatusCode).ToString() + " contentType=" + config.ContentType);
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(config.Body);
        JsonElement root = document.RootElement;
        return JsonStringEquals(root, "appId", AppId) && JsonStringEquals(root, "contractId", ContractId) && JsonStringEquals(root, "expectedBodyKey", ExpectedBodyKey) && JsonStringEquals(root, "expectedBody", ExpectedBody) && JsonIntEquals(root, "expectedBodyBytes", ExpectedBodyBytes) && JsonStringEquals(root, "listenEndpoint", app.BaseUri.ToString().TrimEnd('/')) && JsonStringEquals(root, "healthPath", "/healthz");
    }

    private static bool TestHostEnvironmentOverride(string publishDirectory)
    {
        const string overrideBody = "OS_MINIMAL_API_ENV_OVERRIDE";
        using RunningMinimalApiSmoke app = StartMinimalApiSmoke(publishDirectory, overrideBody);
        HttpResponseSummary health = GetResponse(app.BaseUri, "/healthz");
        HttpResponseSummary config = GetResponse(app.BaseUri, "/config");
        int overrideBytes = Encoding.UTF8.GetByteCount(overrideBody);
        string overrideHash = Sha256Utf8(overrideBody);
        if (!IsExpectedSmokeBody(health, overrideBody, overrideBytes, overrideHash))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(config.Body);
        JsonElement root = document.RootElement;
        return JsonStringEquals(root, "expectedBody", overrideBody) && JsonIntEquals(root, "expectedBodyBytes", overrideBytes);
    }

    private static RunningMinimalApiSmoke StartMinimalApiSmoke(string publishDirectory, string expectedBody)
    {
        int port = ReserveFreeLoopbackPort();
        Uri baseUri = new Uri("http://127.0.0.1:" + port.ToString() + "/");
        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = publishDirectory
        };
        startInfo.ArgumentList.Add("MinimalApiSmoke.dll");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["ASPNETCORE_URLS"] = baseUri.ToString().TrimEnd('/');
        startInfo.Environment[ExpectedBodyKey] = expectedBody;
        Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start MinimalApiSmoke.");
        RunningMinimalApiSmoke app = new RunningMinimalApiSmoke(process, process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync(), baseUri);
        if (!WaitForHealthyEndpoint(app, TimeSpan.FromSeconds(20)))
        {
            string output = app.StopAndCaptureOutput();
            throw new InvalidOperationException("MinimalApiSmoke did not become healthy. Output: " + output);
        }

        return app;
    }

    private static bool WaitForHealthyEndpoint(RunningMinimalApiSmoke app, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (app.HasExited)
            {
                return false;
            }

            try
            {
                HttpResponseSummary summary = GetResponse(app.BaseUri, "/healthz", TimeSpan.FromSeconds(2));
                if (summary.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static HttpResponseSummary GetResponse(Uri baseUri, string path)
    {
        return GetResponse(baseUri, path, TimeSpan.FromSeconds(5));
    }

    private static HttpResponseSummary GetResponse(Uri baseUri, string path, TimeSpan timeout)
    {
        if (baseUri.Host != "127.0.0.1")
        {
            throw new InvalidOperationException("Kestrel smoke fixture must use 127.0.0.1.");
        }

        if (string.IsNullOrEmpty(path) || path[0] != '/')
        {
            throw new InvalidOperationException("Kestrel smoke fixture path must be origin-form.");
        }

        using TcpClient client = new TcpClient(AddressFamily.InterNetwork);
        Task connect = client.ConnectAsync(IPAddress.Loopback, baseUri.Port);
        if (!connect.Wait(timeout))
        {
            throw new TimeoutException("Timed out connecting to MinimalApiSmoke.");
        }

        connect.GetAwaiter().GetResult();
        int timeoutMilliseconds = checked((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue));
        client.SendTimeout = timeoutMilliseconds;
        client.ReceiveTimeout = timeoutMilliseconds;
        using NetworkStream stream = client.GetStream();
        byte[] request = Encoding.ASCII.GetBytes("GET " + path + " HTTP/1.1\r\n" + "Host: 127.0.0.1\r\n" + "Connection: close\r\n" + "\r\n");
        stream.Write(request, 0, request.Length);
        using MemoryStream responseBytes = new MemoryStream();
        byte[] scratch = new byte[4096];
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        int headerTerminator = -1;
        int contentLength = -1;
        while (DateTime.UtcNow < deadline)
        {
            int remainingMilliseconds = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            stream.ReadTimeout = remainingMilliseconds;
            int read;
            try
            {
                read = stream.Read(scratch, 0, scratch.Length);
            }
            catch (IOException ex)when (ex.InnerException is SocketException)
            {
                throw new TimeoutException("Timed out reading MinimalApiSmoke response.", ex);
            }

            if (read == 0)
            {
                break;
            }

            responseBytes.Write(scratch, 0, read);
            if (responseBytes.Length > 64 * 1024)
            {
                throw new InvalidOperationException("MinimalApiSmoke response exceeded host-test limit.");
            }

            byte[] current = responseBytes.ToArray();
            if (headerTerminator < 0)
            {
                headerTerminator = FindHeaderTerminator(current);
                if (headerTerminator >= 0)
                {
                    contentLength = ParseContentLength(current, headerTerminator);
                }
            }

            if (headerTerminator >= 0 && contentLength >= 0 && current.Length >= headerTerminator + 4 + contentLength)
            {
                break;
            }
        }

        byte[] response = responseBytes.ToArray();
        headerTerminator = FindHeaderTerminator(response);
        if (headerTerminator < 0)
        {
            throw new InvalidOperationException("MinimalApiSmoke response missing HTTP headers.");
        }

        return ParseHttpResponse(response, headerTerminator);
    }

    private static int FindHeaderTerminator(byte[] response)
    {
        for (int index = 0; index <= response.Length - 4; index++)
        {
            if (response[index] == (byte)'\r' && response[index + 1] == (byte)'\n' && response[index + 2] == (byte)'\r' && response[index + 3] == (byte)'\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static int ParseContentLength(byte[] response, int headerTerminator)
    {
        string headerText = Encoding.ASCII.GetString(response, 0, headerTerminator);
        string[] lines = headerText.Split("\r\n", StringSplitOptions.None);
        for (int index = 1; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line["Content-Length:".Length..].Trim(), out int contentLength) && contentLength >= 0)
            {
                return contentLength;
            }
        }

        return -1;
    }

    private static HttpResponseSummary ParseHttpResponse(byte[] response, int headerTerminator)
    {
        string headerText = Encoding.ASCII.GetString(response, 0, headerTerminator);
        string[] lines = headerText.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || !lines[0].StartsWith("HTTP/1.1 ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("MinimalApiSmoke response status line was invalid.");
        }

        if (!int.TryParse(lines[0].AsSpan(9, 3), out int statusCode))
        {
            throw new InvalidOperationException("MinimalApiSmoke response status code was invalid.");
        }

        string contentType = string.Empty;
        bool transferEncodingChunked = false;
        int contentLength = ParseContentLength(response, headerTerminator);
        for (int index = 1; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
            {
                contentType = line["Content-Type:".Length..].Trim();
                int semicolon = contentType.IndexOf(';', StringComparison.Ordinal);
                if (semicolon >= 0)
                {
                    contentType = contentType[..semicolon].Trim();
                }
            }
            else if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) && line["Transfer-Encoding:".Length..].Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                transferEncodingChunked = true;
            }
        }

        int bodyOffset = headerTerminator + 4;
        int availableBodyBytes = Math.Max(0, response.Length - bodyOffset);
        byte[] bodyBytes = transferEncodingChunked ? DecodeChunkedBody(response, bodyOffset, availableBodyBytes) : CopyBodyBytes(response, bodyOffset, contentLength >= 0 ? Math.Min(contentLength, availableBodyBytes) : availableBodyBytes);
        string body = Encoding.UTF8.GetString(bodyBytes);
        return new HttpResponseSummary((HttpStatusCode)statusCode, contentType, body);
    }

    private static byte[] CopyBodyBytes(byte[] response, int bodyOffset, int bodyLength)
    {
        byte[] body = new byte[bodyLength];
        Array.Copy(response, bodyOffset, body, 0, bodyLength);
        return body;
    }

    private static byte[] DecodeChunkedBody(byte[] response, int bodyOffset, int availableBodyBytes)
    {
        using MemoryStream body = new MemoryStream();
        int offset = bodyOffset;
        int end = bodyOffset + availableBodyBytes;
        while (offset < end)
        {
            int lineEnd = FindLineEnd(response, offset, end);
            if (lineEnd < 0)
            {
                throw new InvalidOperationException("Chunked MinimalApiSmoke response missing chunk length terminator.");
            }

            int chunkLength = ParseHexInt(response, offset, lineEnd);
            offset = lineEnd + 2;
            if (chunkLength == 0)
            {
                return body.ToArray();
            }

            if (chunkLength < 0 || offset > end - chunkLength || offset + chunkLength + 2 > end || response[offset + chunkLength] != (byte)'\r' || response[offset + chunkLength + 1] != (byte)'\n')
            {
                throw new InvalidOperationException("Chunked MinimalApiSmoke response had invalid chunk bounds.");
            }

            body.Write(response, offset, chunkLength);
            offset += chunkLength + 2;
        }

        throw new InvalidOperationException("Chunked MinimalApiSmoke response missing terminating chunk.");
    }

    private static int FindLineEnd(byte[] response, int offset, int end)
    {
        for (int index = offset; index < end - 1; index++)
        {
            if (response[index] == (byte)'\r' && response[index + 1] == (byte)'\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static int ParseHexInt(byte[] response, int offset, int end)
    {
        int value = 0;
        for (int index = offset; index < end; index++)
        {
            int digit = response[index] switch
            {
                >= (byte)'0' and <= (byte)'9' => response[index] - (byte)'0',
                >= (byte)'a' and <= (byte)'f' => response[index] - (byte)'a' + 10,
                >= (byte)'A' and <= (byte)'F' => response[index] - (byte)'A' + 10,
                _ => -1
            };
            if (digit < 0 || value > (int.MaxValue - digit) / 16)
            {
                throw new InvalidOperationException("Chunked MinimalApiSmoke response had invalid chunk length.");
            }

            value = (value * 16) + digit;
        }

        return value;
    }

    private static bool IsExpectedSmokeBody(HttpResponseSummary response, string expectedBody, int expectedBytes, string expectedHash)
    {
        int actualBytes = Encoding.UTF8.GetByteCount(response.Body);
        string actualHash = Sha256Utf8(response.Body);
        bool passed = response.StatusCode == HttpStatusCode.OK && string.Equals(response.ContentType, "text/plain", StringComparison.Ordinal) && string.Equals(response.Body, expectedBody, StringComparison.Ordinal) && actualBytes == expectedBytes && string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.response status=" + ((int)response.StatusCode).ToString() + " contentType=" + response.ContentType + " bodyBytes=" + actualBytes.ToString() + " bodySha256=" + actualHash);
        }

        return passed;
    }

    private static bool JsonStringEquals(JsonElement root, string propertyName, string expected)
    {
        if (root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            return true;
        }

        Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.config missing_or_invalid=" + propertyName);
        return false;
    }

    private static bool JsonIntEquals(JsonElement root, string propertyName, int expected)
    {
        if (root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int actual) && actual == expected)
        {
            return true;
        }

        Console.WriteLine("TEST_FAIL_DETAIL kestrel.smoke_app.config missing_or_invalid=" + propertyName);
        return false;
    }

    private static int ReserveFreeLoopbackPort()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static ProcessResult RunDotnet(string workingDirectory, params string[] args)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet command did not exit within 120 seconds.");
        }

        return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static string Sha256Utf8(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static void Run(string name, ref int failures, Func<bool> test)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool ExpectKestrelPackageValidation(string manifestPath, string packageRoot, string profileName, int expectedExitCode, params string[] expectedOutput)
    {
        ToolResult result = RunManifestTool(manifestPath, packageRoot, profileName);
        bool passed = result.ExitCode == expectedExitCode && result.Output.Contains("TEST_START kestrel.package.contract", StringComparison.Ordinal);
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

    private static ToolResult RunManifestTool(string manifestPath, string packageRoot, string profileName)
    {
        string repoRoot = HostToolProcess.FindRepositoryRoot();
        string toolPath = HostToolProcess.GetBuiltToolPath(repoRoot, "OS.ManifestTool");
        if (!File.Exists(toolPath))
        {
            return new ToolResult(-1, "manifest tool not found: " + toolPath);
        }

        using Process process = new Process();
        process.StartInfo = HostToolProcess.CreateDotnetToolStartInfo(repoRoot, "OS.ManifestTool");
        process.StartInfo.ArgumentList.Add("validate-kestrel-package");
        process.StartInfo.ArgumentList.Add("--manifest");
        process.StartInfo.ArgumentList.Add(manifestPath);
        process.StartInfo.ArgumentList.Add("--package-root");
        process.StartInfo.ArgumentList.Add(packageRoot);
        process.StartInfo.ArgumentList.Add("--profile");
        process.StartInfo.ArgumentList.Add(profileName);
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

    private static string WriteManifest(string tempRoot, string fileName, string contents)
    {
        string manifestPath = Path.Combine(tempRoot, fileName);
        File.WriteAllText(manifestPath, contents);
        return manifestPath;
    }

    private static string CreatePackageRoot(string tempRoot, string name, params string[] omittedPackageFiles)
    {
        string packageRoot = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(packageRoot);
        HashSet<string> omitted = new HashSet<string>(omittedPackageFiles, StringComparer.Ordinal);
        WritePackageFile(packageRoot, AssemblyPath, omitted);
        WritePackageFile(packageRoot, DepsPath, omitted);
        WritePackageFile(packageRoot, RuntimeConfigPath, omitted);
        WritePackageFile(packageRoot, AppSettingsPath, omitted);
        return packageRoot;
    }

    private static void WritePackageFile(string packageRoot, string packagePath, HashSet<string> omitted)
    {
        if (omitted.Contains(packagePath))
        {
            return;
        }

        string filePath = PackagePath(packageRoot, packagePath);
        string? directory = Path.GetDirectoryName(filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, "fixture:" + packagePath);
    }

    private static string PackagePath(string packageRoot, string packagePath)
    {
        string path = packageRoot;
        foreach (string segment in packagePath.Split('/'))
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    private static string ValidManifest()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.smoke",
    "entry": "apps/minimal-api/MinimalApiSmoke.dll"
  },
  "args": [],
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Production",
    "OS_SMOKE_EXPECTED_BODY": "OS_MINIMAL_API_OK"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/minimal-api/MinimalApiSmoke.dll",
    "kestrel": {
      "listen": [
        {
          "scheme": "http",
          "host": "0.0.0.0",
          "port": 8080
        }
      ]
    }
  }
}
""";
    }

    private static string ManifestWithoutExpectedBody()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.smoke",
    "entry": "apps/minimal-api/MinimalApiSmoke.dll"
  },
  "args": [],
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Production"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/minimal-api/MinimalApiSmoke.dll",
    "kestrel": {
      "listen": [
        {
          "scheme": "http",
          "host": "0.0.0.0",
          "port": 8080
        }
      ]
    }
  }
}
""";
    }

    private static string NativeRuntimeManifest()
    {
        return """
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.smoke",
    "entry": "apps/minimal-api/MinimalApiSmoke.dll"
  },
  "args": [],
  "env": {
    "OS_SMOKE_EXPECTED_BODY": "OS_MINIMAL_API_OK"
  },
  "runtime": {
    "kind": "native-aot"
  }
}
""";
    }
}
