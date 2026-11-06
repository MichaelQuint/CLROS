namespace OS.Drivers;

public readonly struct VirtioPciBindingDerivedBarSizeCatalogEntry
{
    public VirtioPciBindingDerivedBarSizeCatalogEntry(int sourceIndex, VirtioPciCandidate candidate, VirtioPciBindingDerivedBarSizePlanResult plan)
    {
        SourceIndex = sourceIndex;
        Candidate = candidate;
        Plan = plan;
    }

    public int SourceIndex
    {
        get;
    }
    public VirtioPciCandidate Candidate
    {
        get;
    }
    public VirtioPciBindingDerivedBarSizePlanResult Plan
    {
        get;
    }
    public VirtioPciBindingMetadata Binding => Plan.Binding;
    public int SegmentIndex => Binding.SegmentIndex;
    public ushort SegmentGroup => Binding.SegmentGroup;
    public ulong EcamBasePhysicalAddress => Binding.EcamBasePhysicalAddress;
    public byte SegmentStartBus => Binding.SegmentStartBus;
    public byte SegmentEndBus => Binding.SegmentEndBus;
    public byte Bus => Binding.Bus;
    public byte Device => Binding.Device;
    public byte Function => Binding.Function;
}
