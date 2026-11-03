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

internal static partial class ImageArtifactMetadataWriter
{
    private readonly record struct ImageArtifactMetadata(string InputPath, string Format, long Bytes, string Sha256);
}
