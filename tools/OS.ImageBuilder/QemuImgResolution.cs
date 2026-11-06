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

internal sealed class QemuImgResolution
{
    private QemuImgResolution(string source, string requestedPath, string? resolvedPath, string commandPath)
    {
        Source = source;
        RequestedPath = requestedPath;
        ResolvedPath = resolvedPath;
        CommandPath = commandPath;
    }

    public string Source
    {
        get;
    }
    public string RequestedPath
    {
        get;
    }
    public string? ResolvedPath
    {
        get;
    }
    public string CommandPath
    {
        get;
    }
    public bool IsAvailable => ResolvedPath is not null;

    public static QemuImgResolution Resolve(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            string? resolved = ResolveExplicit(explicitPath);
            return new QemuImgResolution("explicit", explicitPath, resolved, resolved ?? explicitPath);
        }

        string? pathResolved = ResolveFromPath();
        return new QemuImgResolution("PATH", "qemu-img", pathResolved, pathResolved ?? "qemu-img");
    }

    private static string? ResolveExplicit(string explicitPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(explicitPath);
            return File.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? ResolveFromPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string[] names = OperatingSystem.IsWindows() ? new[]
        {
            "qemu-img.exe",
            "qemu-img"
        }

        : new[]
        {
            "qemu-img"
        };
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string name in names)
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(Path.Combine(directory, name));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
