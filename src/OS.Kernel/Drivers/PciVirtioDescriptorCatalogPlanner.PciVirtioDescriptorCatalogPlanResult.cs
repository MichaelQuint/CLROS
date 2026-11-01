namespace OS.Drivers;
public readonly struct PciVirtioDescriptorCatalogPlanResult
{
    private PciVirtioDescriptorCatalogPlanResult(PciVirtioDescriptorCatalogPlanStatus status, PciEcamSegmentScanResult ecamScan, VirtioPciCandidateSelectionResult candidateSelection, VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog, VirtioDeviceDescriptorCatalogResult descriptorCatalog, int descriptorSourceCount, int requiredDescriptorSourceCapacity, int availableDescriptorSourceCapacity, int failingSourceIndex, int failingInputIndex, bool isInitialized)
    {
        Status = status;
        EcamScan = ecamScan;
        CandidateSelection = candidateSelection;
        BindingCatalog = bindingCatalog;
        DescriptorCatalog = descriptorCatalog;
        DescriptorSourceCount = descriptorSourceCount;
        RequiredDescriptorSourceCapacity = requiredDescriptorSourceCapacity;
        AvailableDescriptorSourceCapacity = availableDescriptorSourceCapacity;
        FailingSourceIndex = failingSourceIndex;
        FailingInputIndex = failingInputIndex;
        IsInitialized = isInitialized;
    }

    public const int NoFailingSourceIndex = -1;
    public const int NoFailingInputIndex = -1;
    public PciVirtioDescriptorCatalogPlanStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == PciVirtioDescriptorCatalogPlanStatus.Success;
    public PciEcamSegmentScanResult EcamScan { get; }
    public VirtioPciCandidateSelectionResult CandidateSelection { get; }
    public VirtioPciBindingDerivedBarSizeCatalogResult BindingCatalog { get; }
    public VirtioDeviceDescriptorCatalogResult DescriptorCatalog { get; }
    public int DescriptorSourceCount { get; }
    public int RequiredDescriptorSourceCapacity { get; }
    public int AvailableDescriptorSourceCapacity { get; }
    public int FailingSourceIndex { get; }
    public int FailingInputIndex { get; }

    public static PciVirtioDescriptorCatalogPlanResult Success(PciEcamSegmentScanResult ecamScan, VirtioPciCandidateSelectionResult candidateSelection, VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog, VirtioDeviceDescriptorCatalogResult descriptorCatalog, int descriptorSourceCount, int availableDescriptorSourceCapacity)
    {
        return new PciVirtioDescriptorCatalogPlanResult(PciVirtioDescriptorCatalogPlanStatus.Success, ecamScan, candidateSelection, bindingCatalog, descriptorCatalog, descriptorSourceCount, descriptorSourceCount, availableDescriptorSourceCapacity, NoFailingSourceIndex, NoFailingInputIndex, isInitialized: true);
    }

    public static PciVirtioDescriptorCatalogPlanResult Fail(PciVirtioDescriptorCatalogPlanStatus status, PciEcamSegmentScanResult ecamScan = default, VirtioPciCandidateSelectionResult candidateSelection = default, VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog = default, VirtioDeviceDescriptorCatalogResult descriptorCatalog = default, int descriptorSourceCount = 0, int requiredDescriptorSourceCapacity = 0, int availableDescriptorSourceCapacity = 0, int failingSourceIndex = NoFailingSourceIndex, int failingInputIndex = NoFailingInputIndex)
    {
        return new PciVirtioDescriptorCatalogPlanResult(status, ecamScan, candidateSelection, bindingCatalog, descriptorCatalog, descriptorSourceCount, requiredDescriptorSourceCapacity, availableDescriptorSourceCapacity, failingSourceIndex, failingInputIndex, isInitialized: true);
    }
}
