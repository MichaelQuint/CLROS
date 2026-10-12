using System.Security.Cryptography;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Validate only the exact additive tail here; the caller still checks every
    // parent firmware/exit/owned record through its existing strict validator.
    public static string[] ValidateLinuxElfTail(string[] lines, byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded, "m16_002_expected_package_invalid");
        Need(lines.Length == QemuContracts.M03OwnedSubsystemMarkers.Count + 7, "m16_002_record_count");
        string[] tail = lines[^7..];
        Need(tail[0] == "TEST_START m16-002" && tail[4] == "M16_002_OWNED firmware=exited package=read-only-nx ledger=valid fileBytes=64 entry=false" &&
            tail[5] == "CLROS_M16_002_READY" && tail[6] == "TEST_PASS m16-002", "m16_002_tail_identity");
        var r = ReadRecord(tail[1], "M16_002_STAGED", "bytes:dec", "payload:dec", "pages:dec", "handlesClosed:dec", "reads:dec");
        Need(r["bytes"] == (ulong)package.Length && r["payload"] == view.PayloadBytes && r["pages"] == ((ulong)package.Length + 4095) / 4096 &&
            r["handlesClosed"] == 2 && r["reads"] >= ((ulong)package.Length + 65535) / 65536 && r["reads"] <= 4096, "m16_002_staging_metadata");
        string payloadHash = Convert.ToHexString(SHA256.HashData(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes))).ToLowerInvariant();
        string packageHash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        Need(tail[2] == "M16_002_DIGEST " + payloadHash && tail[3] == "M16_002_PACKAGE_DIGEST " + packageHash, "m16_002_input_identity");
        return lines[..^7];
    }
}
