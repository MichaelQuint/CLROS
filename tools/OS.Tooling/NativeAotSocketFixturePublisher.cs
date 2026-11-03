using System.Text.Json;

namespace OS.Tooling;

/// <summary>Linux-only publication of a fixed reviewed socket fixture from a
/// fresh source cache and the accepted original socket recipe. Retains original
/// source/compiler/ELF inputs. Publication grants neither Linux execution
/// qualification nor CLROS package, instruction, socket or guest acceptance.</summary>
internal sealed class NativeAotSocketFixturePublisher(string Application,string component,(string Name,string Hash)[] appSources)
{

    /// <summary>Require one original socket publish command to match the fixed
    /// pinned recipe; only app and output change. This pure plan grants no
    /// producer authority. Invalid, duplicated or changed recipes throw.</summary>
    internal string[] PublishArguments(string originalCommands, string destination)
    {
        Need(originalCommands.Length <= 2 * 1024 * 1024, "recipe_size");
        using var json = JsonDocument.Parse(originalCommands);
        var commands = json.RootElement.EnumerateArray().Where(row => row.GetProperty("executable").GetString() == "dotnet")
            .Select(row => row.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()!).ToArray())
            .Where(args => args.Length >= 2 && args[0] == "publish" && args[1] == "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj").ToArray();
        Need(commands.Length == 1, "recipe_inventory");
        string[] prior = commands[0], expected = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        int at = Array.IndexOf(prior, "-o"); Need(at >= 0 && at + 1 < prior.Length, "recipe_output");
        prior[1] = expected[1]; prior[at + 1] = Path.GetFullPath(destination);
        Need(prior.SequenceEqual(expected), "recipe_changed");
        expected[1] = "apps/" + Application + "/" + Application + ".csproj"; return expected;
    }

    /// <summary>Publish only on the pinned Linux builder. Arguments are publish,
    /// --recipe-root and --output pairs. Original evidence stays read-only;
    /// output/cache must be fresh beneath artifacts. Every failure is retained
    /// when output was created and returns nonzero. No app is executed here.</summary>
    internal async Task<int> RunAsync(string root, string[] args, TextWriter output)
    {
        string marker = "nativeaot.socket."+component+".publish";
        output.WriteLine("TEST_START " + marker);
        try
        {
            Need(args is ["publish", "--recipe-root", _, "--output", _], "arguments");
            Need(OperatingSystem.IsLinux(), "linux_builder_required");
            root = Path.GetFullPath(root); string recipe = Path.GetFullPath(args[2]), destination = Path.GetFullPath(args[4], root);
            Need(root != recipe && destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !Path.Exists(destination), "fresh_output_required");
            using var lease = NativeAotPayload.OpenQualificationLease(root);
            using var originalLease = NativeAotPayload.OpenQualificationLease(recipe);
            Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
            _ = NativeAotSocketPackage.Create(root, recipe, asynchronous: true);
            foreach (string shared in new[] { "global.json", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props" })
                Need(NativeAotWebPayload.HashFile(Path.Combine(root, shared)) == NativeAotWebPayload.HashFile(Path.Combine(recipe, shared)), "shared_source_changed");
            string recipeEvidence = Path.Combine(recipe, "artifacts/socket-qualification-r01");
            string appRoot = Path.Combine(root, "apps", Application);
            RequireCurrentSources(root, fresh:true);
            Dictionary<string, string> Sources() => ToolingBuildInputs.SourceInputs(root)
                .Concat(CurrentSources().Select(source => Path.Combine(root, source.Path))).Distinct()
                .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), NativeAotWebPayload.HashFile);
            var before = Sources(); var native = new Dictionary<string, string>(StringComparer.Ordinal); var commands = new List<object>();
            var recipeNative = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(recipeEvidence, "native-inputs.json")))!;
            foreach (string tool in new[] { "dotnet", "clang", "ld", "gcc", "readelf", "nm", "strace", "setsid" })
            {
                string path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(directory => Path.Combine(directory, tool)).First(File.Exists);
                path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
                string hash = NativeAotWebPayload.HashFile(path);
                Need(recipeNative.TryGetValue(path, out string? original) && original == hash, "original_tool_changed"); native.Add(path, hash);
            }
            EfiBuildFiles.InitializeDirectory(destination, "socket_fixture_publication_output");
            using var ownership = new FileStream(Path.Combine(destination, "publication.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            bool succeeded = false; string? failure = null; NativeAotPayload.Inspection? image = null;
            async Task Run(string name, string tool, string[] arguments, TimeSpan timeout)
            {
                string log = Path.Combine(destination, name); commands.Add(new { executable = tool, arguments, workingDirectory = root, log });
                await NativeAotWebPayload.ExecuteAsync(tool, arguments, root, log, timeout);
            }
            try
            {
                await Run("sdk", "dotnet", ["--version"], TimeSpan.FromMinutes(1));
                Need((await File.ReadAllTextAsync(Path.Combine(destination, "sdk.stdout.log"))).Trim() == NativeAotPayload.SdkVersion, "sdk_pin");
                string publish = Path.Combine(destination, Application);
                string[] arguments = PublishArguments(await File.ReadAllTextAsync(Path.Combine(recipeEvidence, "commands.json")), publish);
                await Run("publish", "dotnet", arguments, TimeSpan.FromMinutes(45));
                string executable = Path.Combine(publish, Application);
                Need(new FileInfo(executable).Length <= NativeAotPayload.MaximumImageBytes, "elf_size");
                image = NativeAotPayload.Inspect(await File.ReadAllBytesAsync(executable));
                string intermediate = Path.Combine(appRoot, "obj", "Release", NativeAotPayload.TargetFramework, "linux-musl-x64");
                Directory.CreateDirectory(Path.Combine(destination, "originals"));
                foreach (string path in new[] { executable, Path.Combine(intermediate, Application + ".dll"), Path.Combine(intermediate, "native", Application + ".o"), typeof(NativeAotSocketFixturePublisher).Assembly.Location })
                {
                    native.Add(path, NativeAotWebPayload.HashFile(path));
                    if (path != executable)
                    {
                        string retained = Path.Combine(destination, "originals", Path.GetFileName(path)); File.Copy(path, retained);
                        Need(NativeAotWebPayload.HashFile(retained) == native[path], "original_copy_changed");
                    }
                }
                Directory.CreateDirectory(Path.Combine(destination, "recipe"));
                foreach (string file in new[] { "receipt.json", "commands.json", "native-inputs.json" })
                    File.Copy(Path.Combine(recipeEvidence, file), Path.Combine(destination, "recipe", file));
                string assetsPath = Path.Combine(appRoot, "obj", "project.assets.json"); File.Copy(assetsPath, Path.Combine(destination, "assets.json"));
                using (var assets = JsonDocument.Parse(await File.ReadAllTextAsync(assetsPath)))
                    foreach (var input in NativeAotPayload.CapturePackageInputs(assets.RootElement)) native.TryAdd(input.Key, input.Value);
                foreach (string response in Directory.EnumerateFiles(Path.Combine(appRoot, "obj"), "*.rsp", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(destination, "responses", Path.GetRelativePath(Path.Combine(appRoot, "obj"), response));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(response, target);
                }
                await Run("elf", "readelf", ["-h", "-l", "-d", "-n", "-W", executable], TimeSpan.FromMinutes(2));
                await Run("symbols", "nm", ["-a", executable], TimeSpan.FromMinutes(2));
                RequireCurrentSources(root);
                Need(before.OrderBy(item => item.Key).SequenceEqual(Sources().OrderBy(item => item.Key)) && ToolingBuildInputs.Validate(root).Count == 0, "source_changed");
                Need(native.All(item => NativeAotWebPayload.HashFile(item.Key) == item.Value), "native_input_changed");
                _ = NativeAotSocketPackage.Create(root, recipe, asynchronous:true);
                succeeded = true;
            }
            catch (Exception error) { failure = error is ToolFailure ? error.Message : error.GetType().Name; throw; }
            finally
            {
                await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands));
                await File.WriteAllTextAsync(Path.Combine(destination, "native-inputs.json"), JsonSerializer.Serialize(native));
                var outputs = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) != "publication.lock")
                    .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(destination, path).Replace('\\', '/'), NativeAotWebPayload.HashFile);
                await File.WriteAllTextAsync(Path.Combine(destination, "publication.json"), JsonSerializer.Serialize(new
                {
                    schema = "clros.nativeaot.socket-"+component+".publication.v1", sdk = NativeAotPayload.SdkVersion, runtime = NativeAotPayload.RuntimeVersion,
                    application = Application, succeeded, failure, sources = before, native, outputs, image,
                    appExecuted = false, guestExecuted = false, boundary = "Original publication only; package/instruction admission and full native/host/real guest qualification remain required"
                }));
            }
            output.WriteLine("NATIVEAOT_SOCKET_"+component.ToUpperInvariant()+"_ELF sha256=" + image!.Sha256 + " entry=" + image.Entry);
            output.WriteLine("TEST_PASS " + marker); return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL " + marker + " reason=" + (error is ToolFailure ? error.Message : error.GetType().Name)); return 1;
        }
    }

    internal IReadOnlyList<(string Path, string Hash)> CurrentSources() =>
        NativeAotCurrentSources.AppSources(Application, appSources);

    internal void RequireCurrentSources(string root,bool fresh=false)
    {
        root=Path.GetFullPath(root);string directory=Path.Combine(root,"apps",Application);
        AuthorityFiles.RequirePlainPath(root,directory,"socket_fixture_source_path");
        foreach(string path in new[]{"apps/Directory.Build.props","apps/Directory.Build.targets","apps/Directory.Packages.props",
            "Directory.Packages.props","NuGet.Config","nuget.config"})Need(!Path.Exists(Path.Combine(root,path)),"source_override");
        var current = CurrentSources();
        string prefix = "apps/" + Application + "/";
        Need(Directory.GetFiles(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(current.Select(source => source.Path[prefix.Length..]).Order(StringComparer.Ordinal)),"app_inventory");
        foreach(string child in Directory.EnumerateDirectories(directory))
            Need(!fresh && (Path.GetFileName(child) is "bin" or "obj"),"fresh_app_cache_required");
        foreach(var source in current)
        {
            string path=Path.Combine(root,source.Path);AuthorityFiles.RequirePlainPath(root,path,"socket_fixture_source_path");
            Need(NativeAotWebPayload.HashFile(path)==source.Hash,"app_source_identity");
        }
    }

    private void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("socket_"+component+"_publish_" + reason); }
}
