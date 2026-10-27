using System.Text.Json;
using OS.Tooling;

internal static class NativeAotStaticFilePublisherTests
{
    [HostTest("static-file publisher preserves exact compiler recipe except app and output")]
    private static void Recipe()
    {
        string destination = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "static-file-plan-only");
        string[] original = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        original[1] = "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj";
        string Json(params string[][] commands) => JsonSerializer.Serialize(commands.Select(arguments => new { executable = "dotnet", arguments }));
        string[] plan = NativeAotStaticFilePublisher.PublishArguments(Json(original), destination);
        HostCheck.Equal("apps/MinimalApiStaticFiles/MinimalApiStaticFiles.csproj", plan[1]);
        plan[1] = original[1]; HostCheck.True(plan.SequenceEqual(original));
        Reject(() => NativeAotStaticFilePublisher.PublishArguments("[]", destination));
        Reject(() => NativeAotStaticFilePublisher.PublishArguments(Json(original, original), destination));
        string[] changed = original.ToArray(); changed[Array.IndexOf(changed, "-p:StaticExecutable=true")] = "-p:StaticExecutable=false";
        Reject(() => NativeAotStaticFilePublisher.PublishArguments(Json(changed), destination));
        Reject(() => NativeAotStaticFilePublisher.PublishArguments(Json([.. original, "-p:ExtraPolicy=true"]), destination));
        changed = original.ToArray(); changed[Array.IndexOf(changed, "linux-musl-x64")] = "linux-x64";
        Reject(() => NativeAotStaticFilePublisher.PublishArguments(Json(changed), destination));
    }

    [HostTest("static-file publisher seals every app file and rejects extra source and restore overrides")]
    private static void Sources()
    {
        string root = HostToolProcess.FindRepositoryRoot(); NativeAotStaticFilePublisher.RequireCurrentSources(root);
        string temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "clros-static-file-publisher-" + Guid.NewGuid().ToString("N")));
        string tempBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        HostCheck.True(temporary.StartsWith(tempBase, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        Directory.CreateDirectory(temporary);
        try
        {
            string app = Path.Combine(temporary, "apps", "MinimalApiStaticFiles"); Directory.CreateDirectory(app);
            string[] files = ["MinimalApiStaticFiles.csproj", "Program.cs", "StaticFileApplication.cs", "StaticFileConfigurationResponse.cs", "StaticFileItemResponse.cs", "StaticFileItemsResponse.cs", "StaticFileJsonContext.cs", "StaticFileStatusResponse.cs"];
            foreach (string file in files) File.Copy(Path.Combine(root, "apps", "MinimalApiStaticFiles", file), Path.Combine(app, file));
            NativeAotStaticFilePublisher.RequireCurrentSources(temporary);
            foreach (string file in files)
            {
                string path = Path.Combine(app, file); byte[] original = File.ReadAllBytes(path);
                File.AppendAllText(path, "\n"); Reject(() => NativeAotStaticFilePublisher.RequireCurrentSources(temporary)); File.WriteAllBytes(path, original);
            }
            string quotaSource = Path.Combine(app, "StaticFileApplication.cs"), heldSource = Path.Combine(temporary, "held-StaticFileApplication.cs");
            File.Move(quotaSource, heldSource);
            try { Reject(() => NativeAotStaticFilePublisher.RequireCurrentSources(temporary), "socket_static-files_publish_app_inventory"); }
            finally { File.Move(heldSource, quotaSource); }
            NativeAotStaticFilePublisher.RequireCurrentSources(temporary);
            string extra = Path.Combine(app, "Extra.cs"); File.WriteAllText(extra, "class Extra {}");
            Reject(() => NativeAotStaticFilePublisher.RequireCurrentSources(temporary)); File.Delete(extra);
            string policy = Path.Combine(temporary, "apps", "Directory.Packages.props"); File.WriteAllText(policy, "<Project />");
            Reject(() => NativeAotStaticFilePublisher.RequireCurrentSources(temporary)); File.Delete(policy);
            NativeAotStaticFilePublisher.RequireCurrentSources(temporary);
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [HostTest("static-file publisher rejects invalid arguments and unsupported host before output creation")]
    private static void Admission()
    {
        string root = HostToolProcess.FindRepositoryRoot(); using var output = new StringWriter();
        HostCheck.Equal(1, NativeAotStaticFilePublisher.RunAsync(root, ["qualify"], output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("socket_static-files_publish_arguments", StringComparison.Ordinal));
        if (!OperatingSystem.IsLinux())
        {
            output.GetStringBuilder().Clear(); string destination = Path.Combine(root, "artifacts", "static-file-rejected-publication");
            HostCheck.Equal(1, NativeAotStaticFilePublisher.RunAsync(root, ["publish", "--recipe-root", root, "--output", destination], output).GetAwaiter().GetResult());
            HostCheck.True(output.ToString().Contains("linux_builder_required", StringComparison.Ordinal));
            HostCheck.True(!Path.Exists(destination));
        }
    }

    private static void Reject(Action action, string? reason = null)
    { bool failed = false; try { action(); } catch (ToolFailure error) { failed = true; if (reason is not null) HostCheck.Equal(reason, error.Reason); } HostCheck.True(failed); }
}
