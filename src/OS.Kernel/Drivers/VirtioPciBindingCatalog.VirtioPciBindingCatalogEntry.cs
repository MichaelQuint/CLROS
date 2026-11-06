namespace OS.Drivers;

public readonly struct VirtioPciBindingCatalogEntry
{
    public VirtioPciBindingCatalogEntry(int sourceIndex, VirtioPciCandidate candidate, VirtioPciBindingPlanResult plan)
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
    public VirtioPciBindingPlanResult Plan
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
