using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only service startup joins. SettingsIdentity/Ordinal name
    /// the completed configuration read, not an fd-generation capability. No
    /// source, native instruction, mapping, scheduler or guest authority follows.</summary>

    public readonly record struct ServiceStartupInspection(ulong WriterIdentity, int ListenerDescriptor, uint SettingsBytes, int SettingsDescriptor, ulong SettingsIdentity, ulong SettingsOrdinal);
}
