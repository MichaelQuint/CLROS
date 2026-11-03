using OS.Manifest;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Explicit M16-009 workload opt-in for asynchronous user interrupts.
/// Reads only an already validated immutable manifest owned by the boot/package
/// owner; it grants no CPU, mapping, timer or execution authority. Absent/0 stays
/// disabled. A malformed or duplicate CLROS_USER_IRQS request fails closed.
/// No strings, allocation, firmware access or general environment lookup.</summary>
public static unsafe class LinuxUserInterruptPolicy
{
    /// <summary>Inspect caller-owned readable manifest bytes and their retained
    /// parsed offsets. The caller proves validation/provenance and keeps both
    /// exclusive and unchanged. Invalid extents/values return false/disabled;
    /// success only proposes a policy for a separately admitted native owner.</summary>
    public static bool TryRead(byte* bytes, uint length, LinuxElfManifest manifest, out bool enabled)
    {
        enabled = false;
        if (bytes == null || length == 0 || length > LinuxElfPackage.MaximumManifestBytes ||
            (ulong)bytes > 0x0000800000000000UL - length || !manifest.Succeeded ||
            manifest.ByteCount != length || manifest.EnvironmentCount > 16)
            return false;
        bool found = false, requested = false;
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
        {
            uint row = i * 4, key = manifest.EnvironmentRanges[row], keyBytes = manifest.EnvironmentRanges[row + 1];
            uint value = manifest.EnvironmentRanges[row + 2], valueBytes = manifest.EnvironmentRanges[row + 3];
            if (keyBytes == 0 || key > length || keyBytes > length - key || value > length || valueBytes > length - value)
                return false;
            // CLROS_USER_IRQS, packed independently of UTF16/string support.
            if (!AsciiPointerMatcher.Matches(bytes + key, keyBytes, 15, 0x53555f534f524c43, 0x00535152495f5245))
                continue;
            if (found || valueBytes != 1 || bytes[value] is not ((byte)'0' or (byte)'1'))
                return false;
            found = true;
            requested = bytes[value] == '1';
        }
        enabled = requested;
        return true;
    }
}
