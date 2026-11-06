using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;

public static partial class LinuxWritableDiskProofFixture
{
    /// <summary>Copied six-register Linux x64 syscall contract. Positional
    /// pwritev2 uses the full 64-bit A3 offset, unused A4 = 0, and A5 = RWF_NOAPPEND.
    /// OutputBytes bounds the checked copy-out range; it does not authenticate
    /// user mappings, continuations, device requests or completion timing.</summary>

    public readonly record struct CallSpec(string Role, ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, ulong ReturnRip, long Result, uint OutputBytes);
}
