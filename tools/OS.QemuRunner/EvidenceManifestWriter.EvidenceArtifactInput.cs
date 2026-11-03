using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;
public sealed record EvidenceArtifactInput(string Name, string Role, string Path, string ExpectedSha256);
