using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct MsixCapability
{
    public readonly byte CapabilityOffset, TableBar, PbaBar;
    public readonly ushort Control, TableSize;
    public readonly uint TableOffset, PbaOffset;
    internal MsixCapability(byte offset, ushort control, uint table, uint pba)
    {
        CapabilityOffset = offset;
        Control = control;
        TableSize = (ushort)((control & 0x7ff) + 1);
        TableBar = (byte)(table & 7);
        TableOffset = table & ~7U;
        PbaBar = (byte)(pba & 7);
        PbaOffset = pba & ~7U;
    }

    public bool IsValid => CapabilityOffset >= 0x40 && CapabilityOffset <= 0xf4 && (CapabilityOffset & 3) == 0 && (Control & 0xb800) == 0 && TableSize == (Control & 0x7ff) + 1 && TableBar < 6 && PbaBar < 6 && (TableOffset & 7) == 0 && (PbaOffset & 7) == 0;
}
