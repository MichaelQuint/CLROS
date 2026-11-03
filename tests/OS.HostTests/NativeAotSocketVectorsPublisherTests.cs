using System.Text.Json;
using OS.Tooling;

internal static class NativeAotSocketVectorsPublisherTests
{
    [HostTest("socket vectors publisher changes only application and destination in the exact original recipe")]
    private static void Recipe()
    {
        string destination = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "vectors-plan-only");
        string[] original = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        original[1] = "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj";
        string Json(params string[][] commands) => JsonSerializer.Serialize(commands.Select(args => new { executable = "dotnet", arguments = args }));
        string[] plan = NativeAotSocketVectorsPublisher.PublishArguments(Json(original), destination);
        HostCheck.Equal("apps/NativeAotSocketVectors/NativeAotSocketVectors.csproj", plan[1]);
        plan[1] = original[1];
        HostCheck.True(plan.SequenceEqual(original));
        foreach (string[] changed in new[]{original.Concat(["-p:NoWarn=IL2026"]).ToArray(),
            original.Select(value=>value=="linux-musl-x64"?"linux-x64":value).ToArray(),
            original.Select(value=>value=="-p:StaticExecutable=true"?"-p:StaticExecutable=false":value).ToArray()})
            Reject(() => NativeAotSocketVectorsPublisher.PublishArguments(Json(changed), destination));
        Reject(() => NativeAotSocketVectorsPublisher.PublishArguments(Json(original, original), destination));
        Reject(() => NativeAotSocketVectorsPublisher.PublishArguments("[]", destination));
        HostCheck.True(!Directory.Exists(destination));
    }

    [HostTest("socket vectors publisher binds the complete app family and rejects extra sources and build overrides")]
    private static void Sources()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string temporary = Directory.CreateTempSubdirectory("clros-vector-publisher-").FullName;
        try
        {
            string app = Path.Combine(temporary, "apps", "NativeAotSocketVectors");
            Directory.CreateDirectory(app);
            foreach (var file in NativeAotSocketVectorsPublisher.CurrentSources())
                File.Copy(Path.Combine(root, file.Path), Path.Combine(temporary, file.Path));
            NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary);
            string source = Path.Combine(app, "SocketVectorsFixture.cs");
            byte[] original = File.ReadAllBytes(source);
            File.AppendAllText(source, "\n");
            Reject(() => NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary));
            File.WriteAllBytes(source, original);
            string policySource = Path.Combine(app, "SocketPolicyFixture.cs");
            byte[] policyOriginal = File.ReadAllBytes(policySource);
            File.Delete(policySource);
            Reject(() => NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary));
            File.WriteAllBytes(policySource, policyOriginal);
            File.AppendAllText(policySource, "\n");
            Reject(() => NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary));
            File.WriteAllBytes(policySource, policyOriginal);
            string extra = Path.Combine(app, "Extra.cs");
            File.WriteAllText(extra, "class Extra {}");
            Reject(() => NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary));
            File.Delete(extra);
            string policy = Path.Combine(temporary, "apps", "Directory.Packages.props");
            File.WriteAllText(policy, "<Project />");
            Reject(() => NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary));
            File.Delete(policy);
            NativeAotSocketVectorsPublisher.RequireCurrentSources(temporary);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [HostTest("socket vectors publication rejects arguments and unsupported hosts without creating output")]
    private static void Admission()
    {
        using var output = new StringWriter();
        string root = HostToolProcess.FindRepositoryRoot();
        HostCheck.Equal(1, NativeAotSocketVectorsPublisher.RunAsync(root, ["qualify"], output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("socket_vectors_publish_arguments", StringComparison.Ordinal));
        if (!OperatingSystem.IsLinux())
        {
            output.GetStringBuilder().Clear();
            string destination = Path.Combine(root, "artifacts", "vectors-rejected-publication");
            HostCheck.Equal(1, NativeAotSocketVectorsPublisher.RunAsync(root, ["publish", "--recipe-root", root, "--output", destination], output).GetAwaiter().GetResult());
            HostCheck.True(output.ToString().Contains("linux_builder_required", StringComparison.Ordinal));
            HostCheck.True(!Directory.Exists(destination));
        }
    }

    private static void Reject(Action action)
    {
        bool failed = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            failed = true;
        }
        HostCheck.True(failed);
    }
}
