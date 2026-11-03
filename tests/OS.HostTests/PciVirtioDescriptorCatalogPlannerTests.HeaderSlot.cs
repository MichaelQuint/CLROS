using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static partial class PciVirtioDescriptorCatalogPlannerTests
{
    private readonly struct HeaderSlot
    {
        public HeaderSlot(byte bus, byte device, byte function, byte[] bytes)
        {
            Bus = bus;
            Device = device;
            Function = function;
            Bytes = bytes;
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
        public byte[] Bytes
        {
            get;
        }
    }
}
