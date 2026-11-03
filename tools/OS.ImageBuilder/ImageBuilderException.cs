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

internal sealed class ImageBuilderException : Exception
{
    public ImageBuilderException(int exitCode, string reason, string message) : base(message)
    {
        ExitCode = exitCode;
        Reason = reason;
    }

    public int ExitCode { get; }
    public string Reason { get; }
}
