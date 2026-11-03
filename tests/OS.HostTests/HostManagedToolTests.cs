using System.Security.Cryptography;
using OS.Tooling;

internal static class HostManagedToolTests
{
    [HostTest("managed tools resolve selected custom targets and self independently of the loaded-copy directory")]
    private static void CustomTargets()
    {
        string root = Directory.CreateTempSubdirectory("clros-managed-targets-").FullName;
        try
        {
            var self = Output(root, "projects/Tool.csproj", "custom/arm64/renamed-root.dll", [1, 2, 3]);
            var child = Output(root, "helpers/Child.csproj", "elsewhere/renamed-child.dll", [4, 5, 6]);
            string loaded = Path.Combine(root, "consumer-copy", "root.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(loaded)!);
            File.Copy(self.TargetPath, loaded);
            string metadata = "CLROS_HOST_PROJECT_SELF_1\n" + string.Join('|', self.Project, self.TargetPath,
                self.Configuration, self.Platform, self.Framework, self.RuntimeIdentifier) + "\n";
            var parsed = HostProjectOutputs.ParseSelf(root, new StringReader(metadata), Hash(loaded));
            HostCheck.Equal(self, parsed);
            HostCheck.Equal(self.TargetPath, HostProjectOutputs.ResolveTool(root, "Tool", parsed, [child]));
            HostCheck.Equal(self.TargetPath, HostProjectOutputs.ResolveTool(root, "Tool", parsed, []));
            HostCheck.Equal(child.TargetPath, HostProjectOutputs.ResolveTool(root, "Child", parsed, [child]));
            HostCheck.True(self.TargetPath != loaded && !self.TargetPath.Contains("net11.0", StringComparison.Ordinal));
            Reject(() => HostProjectOutputs.ResolveTool(root, "renamed-root", parsed, [child]));
            Reject(() => HostProjectOutputs.ResolveTool(root, "../Child", parsed, [child]));
            Reject(() => HostProjectOutputs.ResolveTool(root, "Child", parsed, []));
            Reject(() => HostProjectOutputs.ReadSelf(root, typeof(string).Assembly));
            Reject(() => HostProjectOutputs.Read(root, typeof(string).Assembly));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [HostTest("managed tools reject changed missing and conflicting targets without falling back")]
    private static void TargetIdentity()
    {
        string root = Directory.CreateTempSubdirectory("clros-managed-identity-").FullName;
        try
        {
            var self = Output(root, "Tool.csproj", "selected/root.dll", [1]);
            var child = Output(root, "Child.csproj", "selected/child.dll", [2]);
            File.WriteAllBytes(self.TargetPath, [3]);
            Reject(() => HostProjectOutputs.ResolveTool(root, "Tool", self, [child]));
            File.WriteAllBytes(self.TargetPath, [1]);
            File.WriteAllBytes(child.TargetPath, [4]);
            Reject(() => HostProjectOutputs.ResolveTool(root, "Child", self, [child]));
            File.Delete(child.TargetPath);
            _ = Output(root, "decoy/Child.csproj", "tools/Child/bin/Debug/net11.0/Child.dll", [2]);
            Reject(() => HostProjectOutputs.ResolveTool(root, "Child", self, [child]));
            File.Delete(self.TargetPath);
            Reject(() => HostProjectOutputs.ResolveTool(root, "Tool", self, [child]));
            File.WriteAllBytes(self.TargetPath, [1]);

            foreach (var conflict in new[]
            {
                child with { Project = Path.Combine(root, "other", "Tool.csproj") },
                child with { Project = self.Project, Configuration = "Another" },
                child with { TargetPath = self.TargetPath },
                self
            })
                Reject(() => HostProjectOutputs.ResolveTool(root, "Tool", self, [conflict]));

            string row = string.Join('|', self.Project, self.TargetPath, "Special", "ARM64", "net11.0", "linux-arm64");
            foreach (string text in new[] { "bad", "CLROS_HOST_PROJECT_SELF_1\n", "CLROS_HOST_PROJECT_SELF_1\n" + row + "\nextra" })
                Reject(() => HostProjectOutputs.ParseSelf(root, new StringReader(text), self.Sha256));
            Reject(() => HostProjectOutputs.ParseSelf(root, new StringReader("CLROS_HOST_PROJECT_SELF_1\n" + row), "bad hash"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [HostTest("managed tooling and host runner use their own captured graphs and retain self provenance")]
    private static void CompiledGraphs()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var host = HostProjectOutputs.Read(root, typeof(HostManagedToolTests).Assembly);
        var tools = HostProjectOutputs.Read(root, typeof(HostTools).Assembly);
        var self = HostProjectOutputs.ReadSelf(root, typeof(HostTools).Assembly);
        HostCheck.Equal(host.Single(output => output.Name == self.Name), self);
        HostCheck.Equal(self.TargetPath, HostTools.ManagedTool(root, self.Name));
        HostCheck.True(tools.Any(output => output.Name == "OS.QemuCapture"));
        foreach (var child in tools)
            HostCheck.Equal(child.TargetPath, HostTools.ManagedTool(root, child.Name));
        Reject(() => HostTools.ManagedTool(root, "NotInTheSelectedGraph"));
        var receipt = CSharpBuiltSourceInventory.Read(root, self.TargetPath);
        foreach (string resource in new[] { "host-project-outputs.txt", "host-project-self.txt" })
            HostCheck.True(receipt.InputHashes.Keys.Any(path => Path.GetFileName(path) == resource));
    }

    private static HostProjectOutputs.Output Output(string root, string project, string target, byte[] bytes)
    {
        project = Path.GetFullPath(project, root);
        target = Path.GetFullPath(target, root);
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(project, "<Project />");
        File.WriteAllBytes(target, bytes);
        return new(project, target, Hash(target), "Special", "ARM64", "net11.0", "linux-arm64");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
