using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiTableSource
{
    public AcpiTableSource(ulong physicalAddress, ReadOnlyMemory<byte> tableBytes)
    {
        PhysicalAddress = physicalAddress;
        TableBytes = tableBytes;
    }

    public ulong PhysicalAddress { get; }
    public ReadOnlyMemory<byte> TableBytes { get; }
}
