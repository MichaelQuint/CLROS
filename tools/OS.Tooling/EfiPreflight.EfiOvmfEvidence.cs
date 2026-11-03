using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling.Native;

namespace OS.Tooling;
public sealed record EfiOvmfEvidence(string Status, string Path, long Length, string Sha256);
