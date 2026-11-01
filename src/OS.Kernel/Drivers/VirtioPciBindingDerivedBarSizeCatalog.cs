namespace OS.Drivers;
public static class VirtioPciBindingDerivedBarSizeCatalog
{
    public const bool RequiresCallerProvidedCandidates = true;
    public const bool RequiresCallerProvidedConfigurationSources = true;
    public const bool UsesFakeDerivedBarSizeBindingPlan = true;
    public const bool UsesCallerOwnedCommandBuffer = true;
    public const bool UsesCallerOwnedReadbackBuffer = true;
    public const bool UsesCallerOwnedSizeBuffer = true;
    public const bool PerformsLiveConfigWrites = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool RestoresHardwareBars = false;
    public const bool MapsEcam = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool ConfiguresQueues = false;
    public const bool BindsLiveDriver = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;
    public static VirtioPciBindingDerivedBarSizeCatalogResult Build(ReadOnlySpan<VirtioPciCandidate> candidates, ReadOnlySpan<VirtioPciConfigurationSource> configurationSources, IPciConfigDwordAccessor? accessor, Span<PciBarSizingProbeAddressCommand> commands, Span<uint> sizingReadbacks, Span<ulong> barSizes, Span<VirtioPciBindingDerivedBarSizeCatalogEntry> destination)
    {
        int bindingCount = 0;
        int requiredBindingCapacity = 0;
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            VirtioPciCandidate candidate = candidates[candidateIndex];
            requiredBindingCapacity++;
            if (!TryFindConfigurationSource(configurationSources, candidate.SourceIndex, out ReadOnlyMemory<byte> configurationSpace))
            {
                return VirtioPciBindingDerivedBarSizeCatalogResult.Fail(VirtioPciBindingDerivedBarSizeCatalogStatus.MissingConfigurationSource, inspectedCandidateCount: candidateIndex + 1, bindingCount, requiredBindingCapacity, candidateIndex, candidate.SourceIndex);
            }

            VirtioPciBindingDerivedBarSizePlanResult plan = VirtioPciBindingDerivedBarSizePlanner.Plan(CreateScannedDevice(candidate), configurationSpace.Span, accessor, commands, sizingReadbacks, barSizes);
            if (!plan.Succeeded)
            {
                return VirtioPciBindingDerivedBarSizeCatalogResult.Fail(VirtioPciBindingDerivedBarSizeCatalogStatus.DerivedBindingFailed, inspectedCandidateCount: candidateIndex + 1, bindingCount, requiredBindingCapacity, candidateIndex, candidate.SourceIndex, plan);
            }

            if (bindingCount >= destination.Length)
            {
                continue;
            }

            destination[bindingCount] = new VirtioPciBindingDerivedBarSizeCatalogEntry(candidate.SourceIndex, candidate, plan);
            bindingCount++;
        }

        if (requiredBindingCapacity > destination.Length)
        {
            return VirtioPciBindingDerivedBarSizeCatalogResult.Fail(VirtioPciBindingDerivedBarSizeCatalogStatus.DestinationTooSmall, inspectedCandidateCount: candidates.Length, bindingCount, requiredBindingCapacity, bindingCount, bindingCount < candidates.Length ? candidates[bindingCount].SourceIndex : VirtioPciBindingDerivedBarSizeCatalogResult.NoFailingSourceIndex);
        }

        return VirtioPciBindingDerivedBarSizeCatalogResult.Success(candidates.Length, bindingCount);
    }

    private static bool TryFindConfigurationSource(ReadOnlySpan<VirtioPciConfigurationSource> sources, int sourceIndex, out ReadOnlyMemory<byte> configurationSpace)
    {
        for (int index = 0; index < sources.Length; index++)
        {
            if (sources[index].SourceIndex == sourceIndex)
            {
                configurationSpace = sources[index].ConfigurationSpace;
                return true;
            }
        }

        configurationSpace = default;
        return false;
    }

    private static PciEcamScannedDevice CreateScannedDevice(VirtioPciCandidate candidate)
    {
        return new PciEcamScannedDevice(candidate.SegmentIndex, new PciEcamSegment(candidate.EcamBasePhysicalAddress, candidate.SegmentGroup, candidate.SegmentStartBus, candidate.SegmentEndBus), new PciScannedDevice(candidate.Bus, candidate.Device, candidate.Function, candidate.VendorId, candidate.DeviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, candidate.CapabilityPointer));
    }
}
