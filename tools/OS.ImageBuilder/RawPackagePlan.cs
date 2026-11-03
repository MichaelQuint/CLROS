using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal readonly struct RawPackagePlan
{
    public static RawPackagePlan None => new RawPackagePlan(null, 0);

    public RawPackagePlan(string? sourcePath, long sizeBytes)
    {
        SourcePath = sourcePath;
        SizeBytes = sizeBytes;
    }

    public string? SourcePath { get; }
    public long SizeBytes { get; }
}
