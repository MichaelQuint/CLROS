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

internal readonly struct PackagePayloadValidation
{
    public PackagePayloadValidation(string appPath, ulong appBytes, ulong entryOffset)
    {
        AppPath = appPath;
        AppBytes = appBytes;
        EntryOffset = entryOffset;
    }

    public string AppPath
    {
        get;
    }
    public ulong AppBytes
    {
        get;
    }
    public ulong EntryOffset
    {
        get;
    }
}
