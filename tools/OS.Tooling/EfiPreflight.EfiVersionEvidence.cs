using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling.Native;

namespace OS.Tooling;
public sealed record EfiVersionEvidence(string Status, string? Version);
