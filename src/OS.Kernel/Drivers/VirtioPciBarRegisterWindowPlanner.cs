using OS.Std.Binary;

namespace OS.Drivers;
public static class VirtioPciBarRegisterWindowPlanner
{
    public const byte RequiredMappedBarWindowCount = PciBarDecoder.BarCount;
    public const bool RequiresCallerMappedWindows = true;
    public const bool PlansMappedRegisterAddresses = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool ConfiguresQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public static VirtioPciBarRegisterWindowBindingResult BindRequiredApertures(VirtioPciBindingPlanResult bindingPlan, ReadOnlySpan<VirtioPciMappedBarWindow> windows)
    {
        if (!bindingPlan.Succeeded)
        {
            VirtioPciBarRegisterWindowPlanStatus status = bindingPlan.Status == VirtioPciBindingPlanStatus.ApertureValidationFailed ? VirtioPciBarRegisterWindowPlanStatus.ApertureValidationFailed : VirtioPciBarRegisterWindowPlanStatus.BindingPlanFailed;
            return VirtioPciBarRegisterWindowBindingResult.Fail(status, bindingPlan.Status, bindingPlan.ApertureValidation.Status, bindingPlan.ApertureValidation.FailingCapabilityType, bindingPlan.ApertureValidation.FailingBarIndex);
        }

        VirtioPciBarApertureSet apertures = bindingPlan.Binding.Apertures;
        VirtioPciBarRegisterWindowPlanStatus commonStatus = TryBindAperture(apertures.CommonConfiguration, windows, out VirtioPciBarRegisterWindowBinding commonConfiguration);
        if (commonStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, commonStatus, apertures.CommonConfiguration);
        }

        VirtioPciBarRegisterWindowPlanStatus notifyStatus = TryBindAperture(apertures.NotifyConfiguration, windows, out VirtioPciBarRegisterWindowBinding notifyConfiguration);
        if (notifyStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, notifyStatus, apertures.NotifyConfiguration);
        }

        VirtioPciBarRegisterWindowPlanStatus isrStatus = TryBindAperture(apertures.IsrStatus, windows, out VirtioPciBarRegisterWindowBinding isrStatusBinding);
        if (isrStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, isrStatus, apertures.IsrStatus);
        }

        VirtioPciBarRegisterWindowPlanStatus deviceStatus = TryBindAperture(apertures.DeviceConfiguration, windows, out VirtioPciBarRegisterWindowBinding deviceConfiguration);
        if (deviceStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, deviceStatus, apertures.DeviceConfiguration);
        }

        return VirtioPciBarRegisterWindowBindingResult.Success(bindingPlan.Status, bindingPlan.ApertureValidation.Status, new VirtioPciBarRegisterWindowBindingSet(commonConfiguration, notifyConfiguration, isrStatusBinding, deviceConfiguration));
    }

    public static VirtioPciBarRegisterWindowAccessPlanResult PlanAccess(VirtioPciBarRegisterWindowBinding binding, uint relativeOffset, VirtioPciRegisterAccessWidth width)
    {
        uint widthBytes = GetWidthBytes(width);
        if (widthBytes == 0)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth, binding, relativeOffset, width);
        }

        if (!binding.IsBound)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.MissingWindow, binding, relativeOffset, width);
        }

        ulong accessEnd = (ulong)relativeOffset + widthBytes;
        if (accessEnd > uint.MaxValue)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow, binding, relativeOffset, width);
        }

        if (accessEnd > binding.ApertureLength)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessRangeOutsideAperture, binding, relativeOffset, width);
        }

        ulong barRelativeOffset = (ulong)binding.ApertureOffset + relativeOffset;
        if (barRelativeOffset < binding.ApertureOffset)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow, binding, relativeOffset, width);
        }

        if ((barRelativeOffset & (widthBytes - 1U)) != 0)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessUnaligned, binding, relativeOffset, width);
        }

        if (barRelativeOffset > ulong.MaxValue - widthBytes)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow, binding, relativeOffset, width);
        }

        ulong accessBarEnd = barRelativeOffset + widthBytes;
        if (accessBarEnd > binding.WindowLength)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessRangeOutsideAperture, binding, relativeOffset, width);
        }

        if (AddOverflows(binding.BarVirtualBaseAddress, barRelativeOffset, out ulong virtualAddress) || AddOverflows(binding.BarPhysicalBaseAddress, barRelativeOffset, out ulong physicalAddress))
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow, binding, relativeOffset, width);
        }

        return VirtioPciBarRegisterWindowAccessPlanResult.Success(new VirtioPciBarRegisterWindowAccessPlan(binding, relativeOffset, width, widthBytes, barRelativeOffset, physicalAddress, virtualAddress));
    }

    private static VirtioPciBarRegisterWindowPlanStatus TryBindAperture(VirtioPciBarAperture aperture, ReadOnlySpan<VirtioPciMappedBarWindow> windows, out VirtioPciBarRegisterWindowBinding binding)
    {
        binding = default;
        if (aperture.BarIndex >= windows.Length)
        {
            return VirtioPciBarRegisterWindowPlanStatus.WindowSourceTooSmall;
        }

        VirtioPciMappedBarWindow window = windows[aperture.BarIndex];
        if (window.BarIndex != aperture.BarIndex)
        {
            return VirtioPciBarRegisterWindowPlanStatus.MissingWindow;
        }

        if (!window.IsMapped)
        {
            return VirtioPciBarRegisterWindowPlanStatus.WindowNotMapped;
        }

        if (!window.IsMmio || !window.IsDeviceMemory || !window.IsUncached)
        {
            return VirtioPciBarRegisterWindowPlanStatus.WindowNotMmio;
        }

        if (window.VirtualBaseAddress == 0 || window.PhysicalBaseAddress == 0)
        {
            return VirtioPciBarRegisterWindowPlanStatus.WindowBaseAddressZero;
        }

        if (window.PhysicalBaseAddress != aperture.BarBaseAddressBits)
        {
            return VirtioPciBarRegisterWindowPlanStatus.PhysicalBaseMismatch;
        }

        if (aperture.EndOffset > window.Length)
        {
            return VirtioPciBarRegisterWindowPlanStatus.WindowLengthTooSmall;
        }

        if (AddOverflows(window.PhysicalBaseAddress, aperture.Offset, out ulong aperturePhysicalBaseAddress) || AddOverflows(window.VirtualBaseAddress, aperture.Offset, out ulong apertureVirtualBaseAddress))
        {
            return VirtioPciBarRegisterWindowPlanStatus.ApertureAddressOverflow;
        }

        binding = new VirtioPciBarRegisterWindowBinding(aperture, window, aperturePhysicalBaseAddress, apertureVirtualBaseAddress);
        return VirtioPciBarRegisterWindowPlanStatus.Success;
    }

    private static VirtioPciBarRegisterWindowBindingResult FailAperture(VirtioPciBindingPlanResult bindingPlan, VirtioPciBarRegisterWindowPlanStatus status, VirtioPciBarAperture aperture)
    {
        return VirtioPciBarRegisterWindowBindingResult.Fail(status, bindingPlan.Status, bindingPlan.ApertureValidation.Status, aperture.CapabilityType, aperture.BarIndex);
    }

    private static uint GetWidthBytes(VirtioPciRegisterAccessWidth width)
    {
        return width == VirtioPciRegisterAccessWidth.Byte ? 1U : width == VirtioPciRegisterAccessWidth.Word ? 2U : width == VirtioPciRegisterAccessWidth.DWord ? 4U : width == VirtioPciRegisterAccessWidth.QWord ? 8U : 0U;
    }

    private static bool AddOverflows(ulong left, ulong right, out ulong value)
    {
        value = left + right;
        return value < left;
    }
}
