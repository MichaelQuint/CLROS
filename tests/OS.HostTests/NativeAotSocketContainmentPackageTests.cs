using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotSocketContainmentPackageTests
{
    private static readonly string[] Sources = ["global.json", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props",
        "tools/OS.Tooling/NativeAotPayload.cs", "tools/OS.Tooling/NativeAotWebPayload.cs", "tools/OS.Tooling/NativeAotSocketPayload.cs",
        "apps/NativeAotSocketSmoke/Program.cs", "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj",
        "apps/NativeAotSocketContainment/Program.cs", "apps/NativeAotSocketContainment/NativeAotSocketContainment.csproj"];
    private static void Reject(Action action, string reason)
    {
        string? observed = null;
        try { action(); } catch (Exception error) when (error is InvalidDataException or ToolFailure) { observed = error.Message; }
        HostCheck.True(observed?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + observed);
    }

    [HostTest("containment package rejects extra or changed app sources before using publication evidence")]
    private static void SourceInventory()
    {
        string root = HostToolProcess.FindRepositoryRoot(), temporary = Directory.CreateTempSubdirectory("clros-containment-sources-").FullName;
        try
        {
            foreach (string relative in Sources)
            {
                string path = Path.Combine(temporary, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(root, relative), path);
            }
            NativeAotSocketContainmentPackage.RequireCurrentSources(temporary);
            string program = Path.Combine(temporary, "apps/NativeAotSocketContainment/Program.cs");
            byte[] original = File.ReadAllBytes(program); File.AppendAllText(program, "\n");
            Reject(() => NativeAotSocketContainmentPackage.RequireCurrentSources(temporary), "source_identity");
            File.WriteAllBytes(program, original);
            string extra = Path.Combine(temporary, "apps/NativeAotSocketContainment/Extra.cs"); File.WriteAllText(extra, "class Extra {}");
            Reject(() => NativeAotSocketContainmentPackage.RequireCurrentSources(temporary), "source_inventory");
            File.Delete(extra); NativeAotSocketContainmentPackage.RequireCurrentSources(temporary);
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [HostTest("containment package policy retains finite resources original digest explicit IRQ and one passive bind")]
    private static void Policy()
    {
        byte[] text = Encoding.ASCII.GetBytes(NativeAotSocketContainmentPackage.Manifest);
        var parsed = NativeManifestParser.ParseLinuxElf(text);
        HostCheck.True(parsed.Succeeded && parsed.MemoryBytes == 67108864 && parsed.StackBytes == 1572864 &&
            parsed.Threads == 16 && parsed.Descriptors == 16 && parsed.EnvironmentCount == 1 && parsed.Args.Length == 0);
        HostCheck.True(Encoding.ASCII.GetString(text).Contains("app.path=apps/NativeAotSocketContainment\n", StringComparison.Ordinal) &&
            Encoding.ASCII.GetString(text).Contains("listen=0.0.0.0:8080\n", StringComparison.Ordinal));
        HostCheck.Equal(NativeAotSocketContainmentPackage.ElfSha256.ToLowerInvariant(), Encoding.ASCII.GetString(text, (int)parsed.Digest.Offset, (int)parsed.Digest.Length));
        fixed (byte* pointer = text) HostCheck.True(LinuxUserInterruptPolicy.TryRead(pointer, (uint)text.Length, parsed, out bool enabled) && enabled);
        Reject(() => NativeAotSocketContainmentPackage.RequirePackage([]), "identity");
    }

    [HostTest("containment package rejects invented publication evidence without creating output")]
    private static void PublicationIdentity()
    {
        string root = HostToolProcess.FindRepositoryRoot(), temporary = Directory.CreateTempSubdirectory("clros-containment-evidence-").FullName;
        try
        {
            string receipt = Path.Combine(temporary, "publication.json");
            File.WriteAllText(receipt, "{\"succeeded\":true,\"appExecuted\":true,\"guestExecuted\":true}");
            Reject(() => NativeAotSocketContainmentPackage.Create(root, temporary), "publication_identity");
            HostCheck.True(Directory.GetFiles(temporary).SequenceEqual(new[] { receipt }));
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }
}
