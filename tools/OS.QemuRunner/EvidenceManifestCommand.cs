using System.Globalization;

namespace OS.Tools.QemuRunner;

public static partial class EvidenceManifestCommand
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        if (args.Length == 0 || !string.Equals(args[0], "evidence-manifest", StringComparison.Ordinal))
        {
            exitCode = 0;
            return false;
        }

        Console.WriteLine("TEST_START evidence.manifest");
        try
        {
            EvidenceManifestCliOptions options = EvidenceManifestCliOptions.Parse(args);
            EvidenceManifestWriteResult result = EvidenceManifestWriter.Write(options.ToRequest(), options.OutputPath);
            if (!result.Succeeded)
            {
                Console.Error.WriteLine("TEST_FAIL evidence.manifest status=" + result.Status + " reason=" + result.Reason);
                exitCode = 2;
                return true;
            }

            Console.WriteLine("EVIDENCE_MANIFEST_PATH " + result.ManifestPath);
            Console.WriteLine("EVIDENCE_MANIFEST_SHA256 " + result.ManifestSha256);
            Console.WriteLine("TEST_PASS evidence.manifest");
            exitCode = 0;
            return true;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("TEST_FAIL evidence.manifest invalid_args " + ex.Message);
            exitCode = 1;
            return true;
        }
    }
}
