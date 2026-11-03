using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotSocketContainmentPackageTests
{
    private static string[] Sources => NativeAotSocketContainmentPackage.CurrentSources().Select(source => source.Path).ToArray();
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
            foreach (string source in Sources.Where(path => path.StartsWith("apps/NativeAotSocketContainment/", StringComparison.Ordinal)))
            {
                string path = Path.Combine(temporary, source);
                byte[] original = File.ReadAllBytes(path);
                File.AppendAllText(path, "\n");
                Reject(() => NativeAotSocketContainmentPackage.RequireCurrentSources(temporary), "source_identity");
                File.WriteAllBytes(path, original);
            }
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
