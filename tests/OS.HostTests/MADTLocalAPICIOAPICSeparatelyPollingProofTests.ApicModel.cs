using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class MADTLocalAPICIOAPICSeparatelyPollingProofTests
{
    private struct ApicModel
    {
        public fixed uint Local[256];
        public fixed uint Io[256];
        public uint Selected, Operations, FailAt, BadAccess, Corrupt;
        public byte Master, Slave;
    }
}
