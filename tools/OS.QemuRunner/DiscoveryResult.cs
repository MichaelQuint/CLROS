using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

readonly struct DiscoveryResult
{
    public DiscoveryResult(string? path, IReadOnlyList<string> checkedPaths)
    {
        Path = path;
        CheckedPaths = checkedPaths;
    }

    public string? Path
    {
        get;
    }
    public IReadOnlyList<string> CheckedPaths
    {
        get;
    }
}
