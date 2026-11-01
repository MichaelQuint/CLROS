namespace OS.Drivers;
public readonly struct PciBarSizingProbeAddressCommand
{
    public PciBarSizingProbeAddressCommand(PciBarSizingProbeCommand probeCommand, PciEcamConfigurationAddressResult addressResult)
    {
        ProbeCommand = probeCommand;
        AddressResult = addressResult;
    }

    public PciBarSizingProbeCommand ProbeCommand { get; }
    public PciEcamConfigurationAddressResult AddressResult { get; }
    public PciBarSizingProbeCommandKind Kind => ProbeCommand.Kind;
    public ushort SequenceIndex => ProbeCommand.SequenceIndex;
    public byte BarIndex => ProbeCommand.BarIndex;
    public byte PairBarIndex => ProbeCommand.PairBarIndex;
    public ushort RegisterOffset => ProbeCommand.RegisterOffset;
    public byte AccessWidthBytes => ProbeCommand.AccessWidthBytes;
    public uint Value => ProbeCommand.Value;
    public bool IsUpperPart => ProbeCommand.IsUpperPart;
    public PciBarKind BarKind => ProbeCommand.BarKind;
    public PciEcamConfigurationSpaceStatus AddressStatus => AddressResult.Status;
    public ulong EcamOffset => AddressResult.EcamOffset;
    public ulong PhysicalAddress => AddressResult.PhysicalAddress;
}
