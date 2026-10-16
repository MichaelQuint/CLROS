using System.Security.Cryptography;
using System.Text.Json;
using OS.Tooling;

internal static class NativeAotWebPayloadTests
{
    [HostTest("nativeaot.web receipt rejects stale source missing altered and unexpected output")]
    private static void StaleArtifacts()
    {
        foreach (string mutation in new[] { "source", "missing", "altered", "extra", "pin" })
        {
            string root = Path.Combine(Path.GetTempPath(), "clros-web-receipt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var sources = new Dictionary<string, string>();
                foreach (string app in new[] { "NativeAotHello", "NativeAotRuntimeSmoke", "MinimalApiSmoke" })
                {
                    string relative = "apps/" + app + "/Program.cs";
                    string path = Path.Combine(root, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, app);
                    sources.Add(relative, Hash(path));
                }
                string evidence = Path.Combine(root, "artifacts/evidence"); Directory.CreateDirectory(evidence);
                string output = Path.Combine(evidence, "observed.log"); File.WriteAllText(output, "original");
                var outputs = new Dictionary<string, string> { ["observed.log"] = Hash(output) };
                string sdk = mutation == "pin" ? "10.0.100" : NativeAotPayload.SdkVersion;
                var receipt = new NativeAotPayload.QualificationReceipt("clros.nativeaot.payload.v1", sdk,
                    NativeAotPayload.RuntimeVersion, sources, outputs);
                File.WriteAllText(Path.Combine(evidence, "receipt.json"), JsonSerializer.Serialize(receipt));
                if (mutation == "source") File.AppendAllText(Path.Combine(root, sources.Keys.First()), "changed");
                if (mutation == "missing") File.Delete(output);
                if (mutation == "altered")
                {
                    DateTime timestamp = File.GetLastWriteTimeUtc(output);
                    File.WriteAllText(output, "modified"); File.SetLastWriteTimeUtc(output, timestamp);
                }
                if (mutation == "extra") File.WriteAllText(Path.Combine(evidence, "unexpected.log"), "extra");
                string? failure = null;
                try { NativeAotPayload.ValidateQualification(root, evidence); }
                catch (InvalidDataException error) { failure = error.Message; }
                HostCheck.Equal(mutation switch { "pin" => "native_receipt_pin", "source" => "source_inputs_changed",
                    _ => "native_evidence_changed" }, failure!);
            }
            finally { Directory.Delete(root, recursive: true); }
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
