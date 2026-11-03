using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;
public enum EvidenceManifestWriteStatus
{
    Success = 0,
    InvalidRequest = 1,
    MissingInput = 2,
    HashMismatch = 3,
    ContradictoryMarkers = 4,
    StaleSourceState = 5,
    IoError = 6
}
