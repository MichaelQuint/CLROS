namespace OS.Drivers;
public enum PciVirtioDescriptorCatalogPlanStatus
{
    Success = 0,
    EcamScanFailed = 1,
    CandidateSelectionFailed = 2,
    DerivedBindingCatalogFailed = 3,
    DescriptorSourceDestinationTooSmall = 4,
    MissingFeatureSource = 5,
    DescriptorCatalogFailed = 6
}
