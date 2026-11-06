using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Manifest;

namespace OS.Tooling;

public sealed record LinuxElfImageReceipt(string Variant, string OriginalSha256, string FinalSha256, string EfiSha256, string PackageSha256, int ChangedOffset, int ChangedBytes);
