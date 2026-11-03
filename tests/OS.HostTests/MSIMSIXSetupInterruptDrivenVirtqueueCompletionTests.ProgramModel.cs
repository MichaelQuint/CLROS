using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class MSIMSIXSetupInterruptDrivenVirtqueueCompletionTests
{
    private struct ProgramModel
    {
        public fixed uint Table[8192];
        public fixed uint Pba[64];
        public uint Calls, FailAt, Bad, Masked;
        public ulong Corrupt;
        public ushort Entries, Control, ConfigVector;
    }
}
