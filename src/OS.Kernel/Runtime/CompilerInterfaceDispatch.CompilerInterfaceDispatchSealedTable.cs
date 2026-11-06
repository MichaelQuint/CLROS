using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>
/// Caller-authenticated native symbol extent. The pinned sealed table has no
/// in-band length. Bytes must be the exact original COFF contribution length,
/// including folded-table identity, not the surrounding PE section length.
/// Bind copies this short list; the input buffer need not remain alive.
/// </summary>

public struct CompilerInterfaceDispatchSealedTable
{
    public ulong DeclaringType, Table, Bytes;
}
