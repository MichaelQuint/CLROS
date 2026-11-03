using System.Text;
using OS.Manifest;

namespace OS.Tooling;

public static partial class NativeAotServicePackage
{
    /// <summary>Numeric/identity observation only. A matching envelope does not
    /// establish that its payload is the reviewed original service executable.</summary>

    public readonly record struct Description(string ElfSha256, uint Arguments, uint Environment, uint SettingsBytes);
}
