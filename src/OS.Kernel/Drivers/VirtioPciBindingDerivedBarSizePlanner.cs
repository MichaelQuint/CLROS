namespace OS.Drivers;
public static class VirtioPciBindingDerivedBarSizePlanner
{
    public const bool RequiresCallerProvidedPciConfigurationSpace = true;
    public const bool RequiresDecodedBarDescriptors = true;
    public const bool RequiresFakeProbeDerivedSizes = true;
    public const bool UsesCallerOwnedCommandBuffer = true;
    public const bool UsesCallerOwnedReadbackBuffer = true;
    public const bool UsesCallerOwnedSizeBuffer = true;
    public const bool InvokesVirtioPciBindingPlan = true;
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
    public static VirtioPciBindingDerivedBarSizePlanResult Plan(PciEcamScannedDevice device, ReadOnlySpan<byte> pciConfigurationSpace, IPciConfigDwordAccessor? accessor, Span<PciBarSizingProbeAddressCommand> commands, Span<uint> sizingReadbacks, Span<ulong> barSizes)
    {
        PciConfigurationHeaderResult header = PciConfigurationHeader.ParseType0(pciConfigurationSpace);
        if (!header.Succeeded)
        {
            return VirtioPciBindingDerivedBarSizePlanResult.Create(VirtioPciBindingDerivedBarSizePlanStatus.HeaderParseFailed, header.Status, barDecode: default, derivedSize: default, bindingPlan: default);
        }

        PciBarDecodeResult barDecode = PciBarDecoder.Decode(header.Header);
        if (!barDecode.Succeeded)
        {
            return VirtioPciBindingDerivedBarSizePlanResult.Create(VirtioPciBindingDerivedBarSizePlanStatus.BarDecodeFailed, PciConfigurationHeaderStatus.Success, barDecode, derivedSize: default, bindingPlan: default);
        }

        PciBarSizingProbeDerivedSizeResult derivedSize = PciBarSizingProbeDerivedSizePlanner.Plan(device.Segment, device.Bus, device.DeviceNumber, device.Function, barDecode.Bars, accessor, commands, sizingReadbacks);
        if (!derivedSize.Succeeded)
        {
            return VirtioPciBindingDerivedBarSizePlanResult.Create(VirtioPciBindingDerivedBarSizePlanStatus.DerivedSizeFailed, PciConfigurationHeaderStatus.Success, barDecode, derivedSize, bindingPlan: default);
        }

        if (!derivedSize.TryCopySizesTo(barSizes))
        {
            return VirtioPciBindingDerivedBarSizePlanResult.Create(VirtioPciBindingDerivedBarSizePlanStatus.SizeDestinationTooSmall, PciConfigurationHeaderStatus.Success, barDecode, derivedSize, bindingPlan: default);
        }

        VirtioPciBindingPlanResult bindingPlan = VirtioPciBindingPlan.Create(device, pciConfigurationSpace, barSizes);
        if (!bindingPlan.Succeeded)
        {
            return VirtioPciBindingDerivedBarSizePlanResult.Create(VirtioPciBindingDerivedBarSizePlanStatus.BindingFailed, PciConfigurationHeaderStatus.Success, barDecode, derivedSize, bindingPlan);
        }

        return VirtioPciBindingDerivedBarSizePlanResult.Create(VirtioPciBindingDerivedBarSizePlanStatus.Success, PciConfigurationHeaderStatus.Success, barDecode, derivedSize, bindingPlan);
    }
}
