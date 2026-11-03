using OS.Tooling;

internal static class HostProjectOutputsTests
{
    [HostTest("host project outputs retain selected paths contexts and diamond references")]
    private static void SelectedContext()
    {
        string root = Scratch();
        string project = Path.Combine(root, "moved", "MyTool.csproj");
        string target = Path.Combine(root, "custom-output", "renamed-producer.dll");
        string row = Row(project, target, new string('A', 64), "Special", "ARM64", "net11.0", "linux-arm64");
        var outputs = Parse(root, row, row);
        HostCheck.Equal(1, outputs.Count);
        HostCheck.Equal("MyTool", outputs[0].Name);
        HostCheck.Equal(target, outputs[0].TargetPath);
        HostCheck.Equal("Special", outputs[0].Configuration);
        HostCheck.Equal("ARM64", outputs[0].Platform);
        HostCheck.Equal("linux-arm64", outputs[0].RuntimeIdentifier);
        HostCheck.True(!outputs[0].TargetPath.Contains(Path.Combine("bin", "Debug")));
    }

    [HostTest("host project outputs reject missing ambiguous malformed and escaped identities")]
    private static void RejectInvalid()
    {
        string root = Scratch();
        string project = Path.Combine(root, "one", "Tool.csproj");
        string target = Path.Combine(root, "first", "output.dll");
        string hash = new('A', 64);
        string row = Row(project, target, hash);
        Reject(() => HostProjectOutputs.Parse(root, new StringReader("unversioned")));
        HostCheck.Equal(0, Parse(root).Count);
        Reject(() => Parse(root, "missing fields"));
        Reject(() => Parse(root, Row("relative.csproj", target, hash)));
        Reject(() => Parse(root, Row(project, Path.GetFullPath("../outside.dll", root), hash)));
        Reject(() => Parse(root, Row(project, target, "not a hash")));
        Reject(() => Parse(root, row, Row(project, target, hash, "Other")));
        Reject(() => Parse(root, row, Row(project, Path.Combine(root, "second", "output.dll"), hash)));
        Reject(() => Parse(root, row, Row(Path.Combine(root, "two", "Tool.csproj"), Path.Combine(root, "second", "output.dll"), hash)));
        Reject(() => Parse(root, row, Row(Path.Combine(root, "two", "Other.csproj"), target, hash)));
        Reject(() => HostProjectOutputs.Resolve(HostToolProcess.FindRepositoryRoot(), "NotADeclaredProjectReference", typeof(HostProjectOutputsTests).Assembly));
    }

    [HostTest("host project output hashes reject changed and missing producers")]
    private static void ProducerIdentity()
    {
        string root = Scratch();
        string project = Path.Combine(root, "Tool.csproj");
        string directory = Path.Combine(root, "output");
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "producer.dll");
        File.WriteAllText(project, "<Project />");
        File.WriteAllText(target, "first producer bytes");
        var original = Parse(root, Row(project, target, HostRunCheckpoint.HashFile(target))).Single();
        HostProjectOutputs.Verify(root, original);
        HostCheck.Equal(original.Sha256, HostProjectOutputs.Snapshot(root, [original], [])[target]);
        File.WriteAllText(target, "other producer bytes");
        Reject(() => HostProjectOutputs.Snapshot(root, [original], []));
        Reject(() => HostProjectOutputs.AdmitHash(original, HostRunCheckpoint.HashFile(target)));
        Reject(() => HostProjectOutputs.Verify(root, original));
        var missing = original with
        {
            TargetPath = Path.Combine(directory, "missing.dll")
        };
        Reject(() => HostProjectOutputs.Verify(root, missing));
        Reject(() => HostProjectOutputs.Snapshot(root, [missing], []));
    }

    [HostTest("host project output inventory includes sidecars and detects new runtime members")]
    private static void DirectoryMembership()
    {
        string root = Scratch();
        string output = Path.Combine(root, "output");
        string runtime = Path.Combine(output, "runtimes", "linux-x64", "native");
        Directory.CreateDirectory(runtime);
        foreach (string name in new[] { "apphost", "tool.dll", "tool.pdb", "tool.host-compilation.xml", "tool.deps.json" })
            File.WriteAllText(Path.Combine(output, name), name);
        var before = HostProjectOutputs.FilesInDirectories(root, [output, output]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HostCheck.Equal(5, before.Count);
        string native = Path.Combine(runtime, "dependency.so");
        File.WriteAllText(native, "new runtime dependency");
        var after = HostProjectOutputs.FilesInDirectories(root, [output]);
        HostCheck.True(after.Contains(native));
        HostCheck.True(!after.All(before.Contains));
    }

    [HostTest("host project output resource binds the executing producer and transitive helper")]
    private static void CompiledGraph()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var outputs = HostProjectOutputs.Read(root, typeof(HostProjectOutputsTests).Assembly);
        HostCheck.True(outputs.Any(output => output.Name == "OS.QemuCapture"));
        foreach (var output in outputs)
        {
            HostProjectOutputs.Verify(root, output);
            HostCheck.Equal(output.TargetPath, HostToolProcess.GetBuiltToolPath(root, output.Name));
        }
        var inventory = CSharpBuiltSourceInventory.Read(root, typeof(HostProjectOutputsTests).Assembly.Location);
        HostCheck.True(inventory.InputHashes.Keys.Any(path => Path.GetFileName(path) == "host-project-outputs.txt"));
        HostCheck.True(!outputs.Any(output => output.TargetPath == inventory.Artifacts.AssemblyPath));
    }

    private static IReadOnlyList<HostProjectOutputs.Output> Parse(string root, params string[] rows) =>
        HostProjectOutputs.Parse(root, new StringReader("CLROS_HOST_PROJECT_OUTPUTS_1\n" + string.Join('\n', rows)));

    private static string Row(string project, string target, string hash, string configuration = "Debug",
        string platform = "AnyCPU", string framework = "net11.0", string runtime = "") =>
        string.Join('|', project, target, hash, configuration, platform, framework, runtime);

    private static string Scratch()
    {
        string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "host-project-output-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("project output control did not reject");
    }
}
