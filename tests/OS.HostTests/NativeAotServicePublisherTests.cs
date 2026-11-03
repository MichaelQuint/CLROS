using System.Text.Json;
using OS.Tooling;

internal static class NativeAotServicePublisherTests
{
    [HostTest("service publisher preserves exact compiler recipe except app and output")]
    private static void Recipe()
    {
        string destination = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "service-plan-only");
        string[] original = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        original[1] = "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj";
        string Json(params string[][] commands) => JsonSerializer.Serialize(commands.Select(arguments => new { executable = "dotnet", arguments }));
        string[] plan = NativeAotServicePublisher.PublishArguments(Json(original), destination);
        HostCheck.Equal("apps/MinimalApiService/MinimalApiService.csproj", plan[1]);
        plan[1] = original[1];
        HostCheck.True(plan.SequenceEqual(original));
        Reject(() => NativeAotServicePublisher.PublishArguments("[]", destination));
        Reject(() => NativeAotServicePublisher.PublishArguments(Json(original, original), destination));
        string[] changed = original.ToArray();
        changed[Array.IndexOf(changed, "-p:StaticExecutable=true")] = "-p:StaticExecutable=false";
        Reject(() => NativeAotServicePublisher.PublishArguments(Json(changed), destination));
        Reject(() => NativeAotServicePublisher.PublishArguments(Json([.. original, "-p:ExtraPolicy=true"]), destination));
        changed = original.ToArray();
        changed[Array.IndexOf(changed, "linux-musl-x64")] = "linux-x64";
        Reject(() => NativeAotServicePublisher.PublishArguments(Json(changed), destination));
    }

    [HostTest("service publisher seals every app file and rejects extra source and restore overrides")]
    private static void Sources()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        NativeAotServicePublisher.RequireCurrentSources(root);
        string temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "clros-service-publisher-" + Guid.NewGuid().ToString("N")));
        string tempBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        HostCheck.True(temporary.StartsWith(tempBase, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        Directory.CreateDirectory(temporary);
        try
        {
            string app = Path.Combine(temporary, "apps", "MinimalApiService");
            Directory.CreateDirectory(app);
            const string prefix = "apps/MinimalApiService/";
            string[] files = NativeAotServiceProgram.CurrentSources().Where(source => source.Path.StartsWith(prefix, StringComparison.Ordinal))
                .Select(source => source.Path[prefix.Length..]).ToArray();
            foreach (string file in files)
                File.Copy(Path.Combine(root, "apps", "MinimalApiService", file), Path.Combine(app, file));
            NativeAotServicePublisher.RequireCurrentSources(temporary);
            foreach (string file in files)
            {
                string path = Path.Combine(app, file);
                byte[] original = File.ReadAllBytes(path);
                File.AppendAllText(path, "\n");
                Reject(() => NativeAotServicePublisher.RequireCurrentSources(temporary));
                File.WriteAllBytes(path, original);
            }
            string quotaSource = Path.Combine(app, "ServiceQuotaExercise.cs"), heldSource = Path.Combine(temporary, "held-ServiceQuotaExercise.cs");
            File.Move(quotaSource, heldSource);
            try
            {
                Reject(() => NativeAotServicePublisher.RequireCurrentSources(temporary), "socket_service_publish_app_inventory");
            }
            finally
            {
                File.Move(heldSource, quotaSource);
            }
            NativeAotServicePublisher.RequireCurrentSources(temporary);
            string extra = Path.Combine(app, "Extra.cs");
            File.WriteAllText(extra, "class Extra {}");
            Reject(() => NativeAotServicePublisher.RequireCurrentSources(temporary));
            File.Delete(extra);
            string policy = Path.Combine(temporary, "apps", "Directory.Packages.props");
            File.WriteAllText(policy, "<Project />");
            Reject(() => NativeAotServicePublisher.RequireCurrentSources(temporary));
            File.Delete(policy);
            NativeAotServicePublisher.RequireCurrentSources(temporary);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [HostTest("service publisher rejects invalid arguments and unsupported host before output creation")]
    private static void Admission()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        using var output = new StringWriter();
        HostCheck.Equal(1, NativeAotServicePublisher.RunAsync(root, ["qualify"], output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("socket_service_publish_arguments", StringComparison.Ordinal));
        if (!OperatingSystem.IsLinux())
        {
            output.GetStringBuilder().Clear();
            string destination = Path.Combine(root, "artifacts", "service-rejected-publication");
            HostCheck.Equal(1, NativeAotServicePublisher.RunAsync(root, ["publish", "--recipe-root", root, "--output", destination], output).GetAwaiter().GetResult());
            HostCheck.True(output.ToString().Contains("linux_builder_required", StringComparison.Ordinal));
            HostCheck.True(!Path.Exists(destination));
        }
    }

    private static void Reject(Action action, string? reason = null)
    {
        bool failed = false;
        try
        {
            action();
        }
        catch (ToolFailure error)
        {
            failed = true;
            if (reason is not null)
                HostCheck.Equal(reason, error.Reason);
        }
        HostCheck.True(failed);
    }
}
