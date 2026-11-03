using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Manifest;
using OS.Storage;

internal static partial class ImageBuilderTests
{
    private readonly struct DirectoryEntry
    {
        public DirectoryEntry(bool isDirectory, int firstCluster, int fileSize)
        {
            IsDirectory = isDirectory;
            FirstCluster = firstCluster;
            FileSize = fileSize;
        }

        public bool IsDirectory
        {
            get;
        }
        public int FirstCluster
        {
            get;
        }
        public int FileSize
        {
            get;
        }
    }
}
