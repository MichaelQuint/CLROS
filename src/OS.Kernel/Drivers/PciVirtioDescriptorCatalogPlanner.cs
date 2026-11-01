namespace OS.Drivers;
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
    public static PciVirtioDescriptorCatalogPlanResult Plan(ReadOnlySpan<PciEcamSegment> ecamSegments, PciConfigurationSpaceHeaderReader? reader, ReadOnlySpan<VirtioPciConfigurationSource> pciConfigurationSources, ReadOnlySpan<VirtioDeviceConfigurationSource> deviceConfigurationSources, ReadOnlySpan<PciVirtioDescriptorFeatureSource> featureSources, IPciConfigDwordAccessor? configAccessor, Span<PciEcamScannedDevice> scannedDevices, Span<PciScannedDevice> pciScanScratch, Span<VirtioPciCandidate> candidateScratch, Span<PciBarSizingProbeAddressCommand> barSizingCommands, Span<uint> sizingReadbacks, Span<ulong> barSizes, Span<VirtioPciBindingDerivedBarSizeCatalogEntry> bindingScratch, Span<VirtioDeviceDescriptorDerivedBindingSource> descriptorSourceScratch, Span<VirtioDeviceDescriptor> destination)
    {
        PciEcamSegmentScanResult ecamScan = PciEcamSegmentScanner.Scan(ecamSegments, reader, scannedDevices, pciScanScratch);
        if (!ecamScan.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(PciVirtioDescriptorCatalogPlanStatus.EcamScanFailed, ecamScan);
        }

        VirtioPciCandidateSelectionResult candidateSelection = VirtioPciCandidateSelector.Select(scannedDevices.Slice(0, ecamScan.DeviceCount), candidateScratch);
        if (!candidateSelection.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(PciVirtioDescriptorCatalogPlanStatus.CandidateSelectionFailed, ecamScan, candidateSelection);
        }

        VirtioPciBindingDerivedBarSizeCatalogResult bindingCatalog = VirtioPciBindingDerivedBarSizeCatalog.Build(candidateScratch.Slice(0, candidateSelection.CandidateCount), pciConfigurationSources, configAccessor, barSizingCommands, sizingReadbacks, barSizes, bindingScratch);
        if (!bindingCatalog.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(PciVirtioDescriptorCatalogPlanStatus.DerivedBindingCatalogFailed, ecamScan, candidateSelection, bindingCatalog);
        }

        if (descriptorSourceScratch.Length < bindingCatalog.BindingCount)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(PciVirtioDescriptorCatalogPlanStatus.DescriptorSourceDestinationTooSmall, ecamScan, candidateSelection, bindingCatalog, descriptorCatalog: default, descriptorSourceCount: 0, requiredDescriptorSourceCapacity: bindingCatalog.BindingCount, availableDescriptorSourceCapacity: descriptorSourceScratch.Length);
        }

        for (int index = 0; index < bindingCatalog.BindingCount; index++)
        {
            VirtioPciBindingDerivedBarSizeCatalogEntry binding = bindingScratch[index];
            if (!TryFindFeatureSource(featureSources, binding.SourceIndex, out PciVirtioDescriptorFeatureSource featureSource))
            {
                return PciVirtioDescriptorCatalogPlanResult.Fail(PciVirtioDescriptorCatalogPlanStatus.MissingFeatureSource, ecamScan, candidateSelection, bindingCatalog, descriptorCatalog: default, descriptorSourceCount: index, requiredDescriptorSourceCapacity: bindingCatalog.BindingCount, availableDescriptorSourceCapacity: descriptorSourceScratch.Length, failingSourceIndex: binding.SourceIndex, failingInputIndex: index);
            }

            descriptorSourceScratch[index] = new VirtioDeviceDescriptorDerivedBindingSource(binding.SourceIndex, binding.Plan, featureSource.DeviceFeatures, featureSource.AvailableQueueCount, featureSource.FeaturesOkAccepted);
        }

        VirtioDeviceDescriptorCatalogResult descriptorCatalog = VirtioDeviceDescriptorCatalog.Build(descriptorSourceScratch.Slice(0, bindingCatalog.BindingCount), deviceConfigurationSources, destination);
        if (!descriptorCatalog.Succeeded)
        {
            return PciVirtioDescriptorCatalogPlanResult.Fail(PciVirtioDescriptorCatalogPlanStatus.DescriptorCatalogFailed, ecamScan, candidateSelection, bindingCatalog, descriptorCatalog, descriptorSourceCount: bindingCatalog.BindingCount, requiredDescriptorSourceCapacity: bindingCatalog.BindingCount, availableDescriptorSourceCapacity: descriptorSourceScratch.Length, descriptorCatalog.FailingSourceIndex, descriptorCatalog.FailingInputIndex);
        }

        return PciVirtioDescriptorCatalogPlanResult.Success(ecamScan, candidateSelection, bindingCatalog, descriptorCatalog, bindingCatalog.BindingCount, descriptorSourceScratch.Length);
    }

    private static bool TryFindFeatureSource(ReadOnlySpan<PciVirtioDescriptorFeatureSource> sources, int sourceIndex, out PciVirtioDescriptorFeatureSource source)
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
