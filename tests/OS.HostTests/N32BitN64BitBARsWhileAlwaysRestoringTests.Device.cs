using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class N32BitN64BitBARsWhileAlwaysRestoringTests
{
    private struct Device
    {
        public fixed uint Registers[64];
        public fixed uint Masks[6];
        public uint Calls, FailAt, Writes, Violation;
        public bool RefuseDisable, RefuseRestore;
    }
}
