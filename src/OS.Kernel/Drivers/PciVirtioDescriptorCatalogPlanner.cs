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

public readonly struct PciVirtioDescriptorFeatureSource
{
    public PciVirtioDescriptorFeatureSource(
        int sourceIndex,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted)
    {
        SourceIndex = sourceIndex;
        DeviceFeatures = deviceFeatures;
        AvailableQueueCount = availableQueueCount;
        FeaturesOkAccepted = featuresOkAccepted;
    }

    public int SourceIndex { get; }

    public ulong DeviceFeatures { get; }

    public ushort AvailableQueueCount { get; }

    public bool FeaturesOkAccepted { get; }
}

public readonly struct PciVirtioDescriptorCatalogPlanResult
{
    private PciVirtioDescriptorCatalogPlanResult(
        PciVirtioDescriptorCatalogPlanStatus status,
        PciEcamSegmentScanResult ecamScan,
        VirtioPciCandidateSelectionResult candidateSelection,
        VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog,
        VirtioDeviceDescriptorCatalogResult descriptorCatalog,
        int descriptorSourceCount,
        int requiredDescriptorSourceCapacity,
        int availableDescriptorSourceCapacity,
        int failingSourceIndex,
        int failingInputIndex,
        bool isInitialized)
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

    public static PciVirtioDescriptorCatalogPlanResult Success(
        PciEcamSegmentScanResult ecamScan,
        VirtioPciCandidateSelectionResult candidateSelection,
        VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog,
        VirtioDeviceDescriptorCatalogResult descriptorCatalog,
        int descriptorSourceCount,
        int availableDescriptorSourceCapacity)
    {
        return new PciVirtioDescriptorCatalogPlanResult(
            PciVirtioDescriptorCatalogPlanStatus.Success,
            ecamScan,
            candidateSelection,
            bindingCatalog,
            descriptorCatalog,
            descriptorSourceCount,
            descriptorSourceCount,
            availableDescriptorSourceCapacity,
            NoFailingSourceIndex,
            NoFailingInputIndex,
            isInitialized: true);
    }

    public static PciVirtioDescriptorCatalogPlanResult Fail(
        PciVirtioDescriptorCatalogPlanStatus status,
        PciEcamSegmentScanResult ecamScan = default,
        VirtioPciCandidateSelectionResult candidateSelection = default,
        VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog = default,
        VirtioDeviceDescriptorCatalogResult descriptorCatalog = default,
        int descriptorSourceCount = 0,
        int requiredDescriptorSourceCapacity = 0,
        int availableDescriptorSourceCapacity = 0,
        int failingSourceIndex = NoFailingSourceIndex,
        int failingInputIndex = NoFailingInputIndex)
    {
        return new PciVirtioDescriptorCatalogPlanResult(
            status,
            ecamScan,
            candidateSelection,
            bindingCatalog,
            descriptorCatalog,
            descriptorSourceCount,
            requiredDescriptorSourceCapacity,
            availableDescriptorSourceCapacity,
            failingSourceIndex,
            failingInputIndex,
            isInitialized: true);
    }
}

public static class PciVirtioDescriptorCatalogPlanner
{
    public const bool UsesInjectedEcamReader = true;
    public const bool UsesCandidateSelector = true;
    public const bool UsesFakeDerivedBarSizing = true;
    public const bool UsesDescriptorCatalog = true;
    public const bool PerformsLivePciEnumeration = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool PerformsLiveConfigWrites = false;
    public const bool MapsEcam = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool ConfiguresQueues = false;
    public const bool BindsLiveDriver = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;

    public static PciVirtioDescriptorCatalogPlanResult Plan(
        ReadOnlySpan<PciEcamSegment> ecamSegments,
        PciConfigurationSpaceHeaderReader? reader,
        ReadOnlySpan<VirtioPciConfigurationSource> pciConfigurationSources,
        ReadOnlySpan<VirtioDeviceConfigurationSource> deviceConfigurationSources,
        ReadOnlySpan<PciVirtioDescriptorFeatureSource> featureSources,
        IPciConfigDwordAccessor? configAccessor,
        Span<PciEcamScannedDevice> scannedDevices,
        Span<PciScannedDevice> pciScanScratch,
        Span<VirtioPciCandidate> candidateScratch,
        Span<PciBarSizingProbeAddressCommand> barSizingCommands,
        Span<uint> sizingReadbacks,
        Span<ulong> barSizes,
        Span<VirtioPciBindingDerivedBarSizeCatalogEntry> bindingScratch,
        Span<VirtioDeviceDescriptorDerivedBindingSource> descriptorSourceScratch,
        Span<VirtioDeviceDescriptor> destination)
    {
        PciEcamSegmentScanResult ecamScan = PciEcamSegmentScanner.Scan(
            ecamSegments,
            reader,
            scannedDevices,
            pciScanScratch);
        if (!ecamScan.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(
                PciVirtioDescriptorCatalogPlanStatus.EcamScanFailed,
                ecamScan);
        }

        VirtioPciCandidateSelectionResult candidateSelection = VirtioPciCandidateSelector.Select(
            scannedDevices.Slice(0, ecamScan.DeviceCount),
            candidateScratch);
        if (!candidateSelection.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(
                PciVirtioDescriptorCatalogPlanStatus.CandidateSelectionFailed,
                ecamScan,
                candidateSelection);
        }

        VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog =
            VirtioPciBindingDerivedBarSizeCatalog.Build(
                candidateScratch.Slice(0, candidateSelection.CandidateCount),
                pciConfigurationSources,
                configAccessor,
                barSizingCommands,
                sizingReadbacks,
                barSizes,
                bindingScratch);
        if (!bindingCatalog.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(
                PciVirtioDescriptorCatalogPlanStatus.DerivedBindingCatalogFailed,
                ecamScan,
                candidateSelection,
                bindingCatalog);
        }

        if (descriptorSourceScratch.Length < bindingCatalog.BindingCount)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(
                PciVirtioDescriptorCatalogPlanStatus.DescriptorSourceDestinationTooSmall,
                ecamScan,
                candidateSelection,
                bindingCatalog,
                descriptorCatalog: default,
                descriptorSourceCount: 0,
                requiredDescriptorSourceCapacity: bindingCatalog.BindingCount,
                availableDescriptorSourceCapacity: descriptorSourceScratch.Length);
        }

        for (int index = 0; index < bindingCatalog.BindingCount; index++)
        {
            VirtioPciBindingDerivedBarSizeCatalogEntry binding = bindingScratch[index];
            if (!TryFindFeatureSource(
                    featureSources,
                    binding.SourceIndex,
                    out PciVirtioDescriptorFeatureSource featureSource))
            {
                return PciVirtioDescriptorCatalogPlanResult.Fail(
                    PciVirtioDescriptorCatalogPlanStatus.MissingFeatureSource,
                    ecamScan,
                    candidateSelection,
                    bindingCatalog,
                    descriptorCatalog: default,
                    descriptorSourceCount: index,
                    requiredDescriptorSourceCapacity: bindingCatalog.BindingCount,
                    availableDescriptorSourceCapacity: descriptorSourceScratch.Length,
                    failingSourceIndex: binding.SourceIndex,
                    failingInputIndex: index);
            }

            descriptorSourceScratch[index] = new VirtioDeviceDescriptorDerivedBindingSource(
                binding.SourceIndex,
                binding.Plan,
                featureSource.DeviceFeatures,
                featureSource.AvailableQueueCount,
                featureSource.FeaturesOkAccepted);
        }

        VirtioDeviceDescriptorCatalogResult descriptorCatalog = VirtioDeviceDescriptorCatalog.Build(
            descriptorSourceScratch.Slice(0, bindingCatalog.BindingCount),
            deviceConfigurationSources,
            destination);
        if (!descriptorCatalog.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(
                PciVirtioDescriptorCatalogPlanStatus.DescriptorCatalogFailed,
                ecamScan,
                candidateSelection,
                bindingCatalog,
                descriptorCatalog,
                descriptorSourceCount: bindingCatalog.BindingCount,
                requiredDescriptorSourceCapacity: bindingCatalog.BindingCount,
                availableDescriptorSourceCapacity: descriptorSourceScratch.Length,
                descriptorCatalog.FailingSourceIndex,
                descriptorCatalog.FailingInputIndex);
        }

        return PciVirtioDescriptorCatalogPlanResult.Success(
            ecamScan,
            candidateSelection,
            bindingCatalog,
            descriptorCatalog,
            bindingCatalog.BindingCount,
            descriptorSourceScratch.Length);
    }

    private static bool TryFindFeatureSource(
        ReadOnlySpan<PciVirtioDescriptorFeatureSource> sources,
        int sourceIndex,
        out PciVirtioDescriptorFeatureSource source)
    {
        for (int index = 0; index < sources.Length; index++)
        {
            if (sources[index].SourceIndex == sourceIndex)
            {
                source = sources[index];
                return true;
            }
        }

        source = default;
        return false;
    }
}
