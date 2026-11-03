using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class AcpiMcfgDiscoveryTests
{
    private readonly struct McfgEntrySpec
    {
        public McfgEntrySpec(ulong basePhysicalAddress, ushort segmentGroup, byte startBus, byte endBus)
        {
            BasePhysicalAddress = basePhysicalAddress;
            SegmentGroup = segmentGroup;
            StartBus = startBus;
            EndBus = endBus;
        }

        public ulong BasePhysicalAddress { get; }
        public ushort SegmentGroup { get; }
        public byte StartBus { get; }
        public byte EndBus { get; }
    }
}
