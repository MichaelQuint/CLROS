using OS.Std.Binary;

namespace OS.Drivers;
public struct XApicCpuObservation
{
    public uint FeaturesEdx, BspId;
    public ulong ApicBaseMsr, Rflags;
}
