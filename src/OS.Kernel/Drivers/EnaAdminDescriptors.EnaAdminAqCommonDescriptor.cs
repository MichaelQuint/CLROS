namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct EnaAdminAqCommonDescriptor
{
    public EnaAdminAqCommonDescriptor(ushort commandId, byte opcode, byte flags)
    {
        CommandId = commandId;
        Opcode = opcode;
        Flags = flags;
    }

    public ushort CommandId
    {
        get;
    }
    public byte Opcode
    {
        get;
    }
    public byte Flags
    {
        get;
    }
    public bool Phase => (Flags & EnaAdminDescriptors.PhaseFlag) != 0;
    public bool HasControlBuffer => (Flags & EnaAdminDescriptors.ControlDataFlag) != 0;
    public bool HasIndirectControlBuffer => (Flags & EnaAdminDescriptors.ControlDataIndirectFlag) != 0;
}
