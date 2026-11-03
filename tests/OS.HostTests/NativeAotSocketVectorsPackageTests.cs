using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotSocketVectorsPackageTests
{
    private static string[] Sources => NativeAotSocketVectorsPackage.CurrentSources().Select(source => source.Path).ToArray();

    [HostTest("vectors package rejects changed app files extra sources and build overrides before reading publication")]
    private static void SourceInventory()
    {
        string root = HostToolProcess.FindRepositoryRoot(), temporary = Directory.CreateTempSubdirectory("clros-vectors-sources-").FullName;
        try
        {
            foreach (string relative in Sources)
            {
                string path = Path.Combine(temporary, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(root, relative), path);
            }
            NativeAotSocketVectorsPackage.RequireCurrentSources(temporary);
            foreach (var source in NativeAotSocketVectorsPublisher.CurrentSources())
            {
                string path = Path.Combine(temporary, source.Path);
                byte[] original = File.ReadAllBytes(path); File.AppendAllText(path, "\n");
                Reject(() => NativeAotSocketVectorsPackage.RequireCurrentSources(temporary), "app_source_identity");
                File.WriteAllBytes(path, original);
            }
            string extra = Path.Combine(temporary, "apps/NativeAotSocketVectors/Extra.cs"); File.WriteAllText(extra, "class Extra {}");
            Reject(() => NativeAotSocketVectorsPackage.RequireCurrentSources(temporary), "app_inventory"); File.Delete(extra);
            string policy = Path.Combine(temporary, "apps/Directory.Packages.props"); File.WriteAllText(policy, "<Project />");
            Reject(() => NativeAotSocketVectorsPackage.RequireCurrentSources(temporary), "source_override"); File.Delete(policy);
            NativeAotSocketVectorsPackage.RequireCurrentSources(temporary);
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [HostTest("vectors package policy binds original digest finite resources explicit IRQ and one passive bind")]
    private static void Policy()
    {
        byte[] text = Encoding.ASCII.GetBytes(NativeAotSocketVectorsPackage.Manifest);
        var parsed = NativeManifestParser.ParseLinuxElf(text);
        HostCheck.True(parsed.Succeeded && parsed.MemoryBytes == 67108864 && parsed.StackBytes == 1572864 &&
            parsed.Threads == 16 && parsed.Descriptors == 16 && parsed.EnvironmentCount == 1 && parsed.Args.Length == 8);
        HostCheck.True(Encoding.ASCII.GetString(text).Contains("app.path=apps/NativeAotSocketVectors\n", StringComparison.Ordinal) &&
            Encoding.ASCII.GetString(text).Contains("listen=0.0.0.0:8080\n", StringComparison.Ordinal) &&
            Encoding.ASCII.GetString(text).Contains("args=--policy\n", StringComparison.Ordinal));
        HostCheck.Equal(NativeAotSocketVectorsPackage.ElfSha256.ToLowerInvariant(), Encoding.ASCII.GetString(text, (int)parsed.Digest.Offset, (int)parsed.Digest.Length));
        fixed (byte* pointer = text) HostCheck.True(LinuxUserInterruptPolicy.TryRead(pointer, (uint)text.Length, parsed, out bool enabled) && enabled);
        Reject(() => NativeAotSocketVectorsPackage.RequirePackage([]), "identity");
    }

    [HostTest("vectors package rejects invented publication evidence without creating output")]
    private static void PublicationIdentity()
    {
        string root = HostToolProcess.FindRepositoryRoot(), temporary = Directory.CreateTempSubdirectory("clros-vectors-evidence-").FullName;
        try
        {
            string receipt = Path.Combine(temporary, "publication.json");
            File.WriteAllText(receipt, "{\"succeeded\":true,\"appExecuted\":true,\"guestExecuted\":true}");
            Reject(() => NativeAotSocketVectorsPackage.Create(root, temporary), "publication_identity");
            HostCheck.True(Directory.GetFiles(temporary).SequenceEqual(new[] { receipt }));
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    private static void Reject(Action action, string reason)
    {
        string? observed = null;
        try { action(); } catch (Exception error) when (error is InvalidDataException or ToolFailure) { observed = error.Message; }
        HostCheck.True(observed?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + observed);
    }
}
