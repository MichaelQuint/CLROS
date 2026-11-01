using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct MadtPollingTopology
{
    public readonly ulong LocalApicAddress, IoApicAddress;
    public readonly uint IoApicGsiBase, HeaderFlags, MaximumIsoGsi;
    public readonly byte BspApicId, ProcessorUid, IoApicId;
    public readonly ushort RecordCount, IsoCount, NmiCount;
    public readonly bool HasLocalApicOverride;
    internal MadtPollingTopology(ulong local, ulong io, uint gsiBase, uint flags, byte bsp, byte uid, byte ioId, ushort records, ushort isos, ushort nmis, bool addressOverride, uint maximumIsoGsi)
    {
        LocalApicAddress = local;
        IoApicAddress = io;
        IoApicGsiBase = gsiBase;
        HeaderFlags = flags;
        BspApicId = bsp;
        ProcessorUid = uid;
        IoApicId = ioId;
        RecordCount = records;
        IsoCount = isos;
        NmiCount = nmis;
        HasLocalApicOverride = addressOverride;
        MaximumIsoGsi = maximumIsoGsi;
    }
}
