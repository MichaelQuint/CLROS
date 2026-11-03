using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Manifest;
using OS.Storage;

internal static partial class ImageBuilderTests
{
    private sealed class KestrelPublishedImageFixture
    {
        public KestrelPublishedImageFixture(string publishOutput, string imageBuilderOutput, string imagePath, string packageRootPath, byte[] manifestBytesFromPublish, byte[] manifestBytesFromImage, bool manifestReadbackSucceeded, byte[] packageBytes, bool packageLoadSucceeded, string packageLoadStatus, string packageStatus)
        {
            PublishOutput = publishOutput;
            ImageBuilderOutput = imageBuilderOutput;
            ImagePath = imagePath;
            PackageRootPath = packageRootPath;
            ManifestBytesFromPublish = manifestBytesFromPublish;
            ManifestBytesFromImage = manifestBytesFromImage;
            ManifestReadbackSucceeded = manifestReadbackSucceeded;
            PackageBytes = packageBytes;
            PackageLoadSucceeded = packageLoadSucceeded;
            PackageLoadStatus = packageLoadStatus;
            PackageStatus = packageStatus;
        }

        public string PublishOutput
        {
            get;
        }
        public string ImageBuilderOutput
        {
            get;
        }
        public string ImagePath
        {
            get;
        }
        public string PackageRootPath
        {
            get;
        }
        public byte[] ManifestBytesFromPublish
        {
            get;
        }
        public byte[] ManifestBytesFromImage
        {
            get;
        }
        public bool ManifestReadbackSucceeded
        {
            get;
        }
        public byte[] PackageBytes
        {
            get;
        }
        public bool PackageLoadSucceeded
        {
            get;
        }
        public string PackageLoadStatus
        {
            get;
        }
        public string PackageStatus
        {
            get;
        }

        public static KestrelPublishedImageFixture CreateFailed(string publishOutput, string imageBuilderOutput, string imagePath, string packageRootPath)
        {
            return new KestrelPublishedImageFixture(publishOutput, imageBuilderOutput, imagePath, packageRootPath, Array.Empty<byte>(), Array.Empty<byte>(), manifestReadbackSucceeded: false, Array.Empty<byte>(), packageLoadSucceeded: false, packageLoadStatus: "not-run", packageStatus: "not-run");
        }
    }
}
