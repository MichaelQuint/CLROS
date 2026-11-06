namespace OS.Drivers;

public static class VirtioPciBindingCatalog
{
    public static VirtioPciBindingCatalogResult Build(ReadOnlySpan<VirtioPciCandidate> candidates, ReadOnlySpan<VirtioPciConfigurationSource> configurationSources, ReadOnlySpan<VirtioPciBarSizeSource> barSizeSources, Span<VirtioPciBindingCatalogEntry> destination)
    {
        int bindingCount = 0;
        int requiredBindingCapacity = 0;
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            VirtioPciCandidate candidate = candidates[candidateIndex];
            requiredBindingCapacity++;
            if (!TryFindConfigurationSource(configurationSources, candidate.SourceIndex, out ReadOnlyMemory<byte> configurationSpace))
            {
                return VirtioPciBindingCatalogResult.Fail(VirtioPciBindingCatalogStatus.MissingConfigurationSource, inspectedCandidateCount: candidateIndex + 1, bindingCount, requiredBindingCapacity, candidateIndex, candidate.SourceIndex);
            }

            if (!TryFindBarSizeSource(barSizeSources, candidate.SourceIndex, out ReadOnlyMemory<ulong> barSizes))
            {
                return VirtioPciBindingCatalogResult.Fail(VirtioPciBindingCatalogStatus.MissingBarSizeSource, inspectedCandidateCount: candidateIndex + 1, bindingCount, requiredBindingCapacity, candidateIndex, candidate.SourceIndex);
            }

            VirtioPciBindingPlanResult plan = VirtioPciBindingPlan.Create(CreateScannedDevice(candidate), configurationSpace.Span, barSizes.Span);
            if (!plan.Succeeded)
            {
                return VirtioPciBindingCatalogResult.Fail(VirtioPciBindingCatalogStatus.BindingFailed, inspectedCandidateCount: candidateIndex + 1, bindingCount, requiredBindingCapacity, candidateIndex, candidate.SourceIndex, plan);
            }

            if (bindingCount >= destination.Length)
            {
                continue;
            }

            destination[bindingCount] = new VirtioPciBindingCatalogEntry(candidate.SourceIndex, candidate, plan);
            bindingCount++;
        }

        if (requiredBindingCapacity > destination.Length)
        {
            return VirtioPciBindingCatalogResult.Fail(VirtioPciBindingCatalogStatus.DestinationTooSmall, inspectedCandidateCount: candidates.Length, bindingCount, requiredBindingCapacity, bindingCount, bindingCount < candidates.Length ? candidates[bindingCount].SourceIndex : VirtioPciBindingCatalogResult.NoFailingSourceIndex);
        }

        return VirtioPciBindingCatalogResult.Success(candidates.Length, bindingCount);
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

    private static bool TryFindBarSizeSource(ReadOnlySpan<VirtioPciBarSizeSource> sources, int sourceIndex, out ReadOnlyMemory<ulong> barSizes)
    {
        for (int index = 0; index < sources.Length; index++)
        {
            if (sources[index].SourceIndex == sourceIndex)
            {
                barSizes = sources[index].BarSizes;
                return true;
            }
        }

        barSizes = default;
        return false;
    }

    private static PciEcamScannedDevice CreateScannedDevice(VirtioPciCandidate candidate)
    {
        return new PciEcamScannedDevice(candidate.SegmentIndex, new PciEcamSegment(candidate.EcamBasePhysicalAddress, candidate.SegmentGroup, candidate.SegmentStartBus, candidate.SegmentEndBus), new PciScannedDevice(candidate.Bus, candidate.Device, candidate.Function, candidate.VendorId, candidate.DeviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, candidate.CapabilityPointer));
    }
}
