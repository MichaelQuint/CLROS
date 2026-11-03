using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class PciEcamSegmentScannerTests
{
    private readonly struct ReadRequest
    {
        public ReadRequest(byte bus, byte device, byte function)
        {
            Bus = bus;
            Device = device;
            Function = function;
        }

        public byte Bus
        {
            get;
        }
        public byte Device
        {
            get;
        }
        public byte Function
        {
            get;
        }
    }
}
