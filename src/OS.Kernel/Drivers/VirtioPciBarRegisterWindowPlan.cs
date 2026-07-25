using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioPciRegisterAccessWidth
{
    Byte = 1,
    Word = 2,
    DWord = 4,
    QWord = 8
}

public enum VirtioPciBarRegisterWindowPlanStatus
{
    Success = 0,
    BindingPlanFailed = 1,
    ApertureValidationFailed = 2,
    WindowSourceTooSmall = 3,
    MissingWindow = 4,
    WindowNotMapped = 5,
    WindowNotMmio = 6,
    WindowBaseAddressZero = 7,
    PhysicalBaseMismatch = 8,
    WindowLengthTooSmall = 9,
    ApertureAddressOverflow = 10,
    AccessRangeOverflow = 11,
    AccessRangeOutsideAperture = 12,
    AccessUnaligned = 13,
    UnsupportedAccessWidth = 14
}

public enum VirtioPciBarRegisterWindowAccessExecutionStatus
{
    Success = 0,
    MissingAccessPlan = 1,
    WindowBufferTooSmall = 2,
    UnsupportedAccessWidth = 3
}

public enum VirtioPciBarRegisterWindowLiveAccessorStatus
{
    Success = 0,
    MissingAccessPlan = 1,
    AccessorNotReady = 2,
    UnsupportedReadWidth = 3,
    UnsupportedWriteWidth = 4,
    RegisterReadFailed = 5,
    RegisterWriteFailed = 6
}

public enum VirtioPciBarRegisterWindowLiveAccessorOperation
{
    None = 0,
    Read16 = 1,
    Write16 = 2,
    Write64 = 3
}

public readonly struct VirtioPciMappedBarWindow
{
    public VirtioPciMappedBarWindow(
        byte barIndex,
        ulong physicalBaseAddress,
        ulong virtualBaseAddress,
        ulong length,
        bool isMapped,
        bool isMmio,
        bool isDeviceMemory,
        bool isUncached)
    {
        BarIndex = barIndex;
        PhysicalBaseAddress = physicalBaseAddress;
        VirtualBaseAddress = virtualBaseAddress;
        Length = length;
        IsMapped = isMapped;
        IsMmio = isMmio;
        IsDeviceMemory = isDeviceMemory;
        IsUncached = isUncached;
    }

    public byte BarIndex { get; }

    public ulong PhysicalBaseAddress { get; }

    public ulong VirtualBaseAddress { get; }

    public ulong Length { get; }

    public bool IsMapped { get; }

    public bool IsMmio { get; }

    public bool IsDeviceMemory { get; }

    public bool IsUncached { get; }
}

public readonly struct VirtioPciBarRegisterWindowBinding
{
    internal VirtioPciBarRegisterWindowBinding(
        VirtioPciBarAperture aperture,
        VirtioPciMappedBarWindow window,
        ulong aperturePhysicalBaseAddress,
        ulong apertureVirtualBaseAddress)
    {
        Aperture = aperture;
        Window = window;
        AperturePhysicalBaseAddress = aperturePhysicalBaseAddress;
        ApertureVirtualBaseAddress = apertureVirtualBaseAddress;
        IsBound = true;
    }

    public VirtioPciBarAperture Aperture { get; }

    public VirtioPciMappedBarWindow Window { get; }

    public bool IsBound { get; }

    public VirtioPciCapabilityType CapabilityType => Aperture.CapabilityType;

    public byte BarIndex => Aperture.BarIndex;

    public PciBarKind BarKind => Aperture.BarKind;

    public ulong BarPhysicalBaseAddress => Window.PhysicalBaseAddress;

    public ulong BarVirtualBaseAddress => Window.VirtualBaseAddress;

    public uint ApertureOffset => Aperture.Offset;

    public uint ApertureLength => Aperture.Length;

    public ulong ApertureEndOffset => Aperture.EndOffset;

    public ulong AperturePhysicalBaseAddress { get; }

    public ulong ApertureVirtualBaseAddress { get; }

    public ulong WindowLength => Window.Length;

    public bool IsMapped => Window.IsMapped;

    public bool IsMmio => Window.IsMmio;

    public bool IsDeviceMemory => Window.IsDeviceMemory;

    public bool IsUncached => Window.IsUncached;
}

public readonly struct VirtioPciBarRegisterWindowBindingSet
{
    internal VirtioPciBarRegisterWindowBindingSet(
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        VirtioPciBarRegisterWindowBinding isrStatus,
        VirtioPciBarRegisterWindowBinding deviceConfiguration)
    {
        CommonConfiguration = commonConfiguration;
        NotifyConfiguration = notifyConfiguration;
        IsrStatus = isrStatus;
        DeviceConfiguration = deviceConfiguration;
    }

    public VirtioPciBarRegisterWindowBinding CommonConfiguration { get; }

    public VirtioPciBarRegisterWindowBinding NotifyConfiguration { get; }

    public VirtioPciBarRegisterWindowBinding IsrStatus { get; }

    public VirtioPciBarRegisterWindowBinding DeviceConfiguration { get; }
}

public readonly struct VirtioPciBarRegisterWindowAccessPlan
{
    internal VirtioPciBarRegisterWindowAccessPlan(
        VirtioPciBarRegisterWindowBinding binding,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width,
        uint widthBytes,
        ulong barRelativeOffset,
        ulong physicalAddress,
        ulong virtualAddress)
    {
        Binding = binding;
        RelativeOffset = relativeOffset;
        Width = width;
        WidthBytes = widthBytes;
        BarRelativeOffset = barRelativeOffset;
        PhysicalAddress = physicalAddress;
        VirtualAddress = virtualAddress;
    }

    public VirtioPciBarRegisterWindowBinding Binding { get; }

    public VirtioPciCapabilityType CapabilityType => Binding.CapabilityType;

    public byte BarIndex => Binding.BarIndex;

    public uint RelativeOffset { get; }

    public VirtioPciRegisterAccessWidth Width { get; }

    public uint WidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong PhysicalAddress { get; }

    public ulong VirtualAddress { get; }
}

public readonly struct VirtioPciBarRegisterWindowAccessExecutionResult
{
    private VirtioPciBarRegisterWindowAccessExecutionResult(
        VirtioPciBarRegisterWindowAccessExecutionStatus status,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width,
        uint widthBytes,
        ulong barRelativeOffset,
        ulong value,
        bool isInitialized)
    {
        Status = status;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        RelativeOffset = relativeOffset;
        Width = width;
        WidthBytes = widthBytes;
        BarRelativeOffset = barRelativeOffset;
        Value = value;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarRegisterWindowAccessExecutionStatus Status { get; }

    public bool IsInitialized { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public uint RelativeOffset { get; }

    public VirtioPciRegisterAccessWidth Width { get; }

    public uint WidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong Value { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowAccessExecutionStatus.Success;

    internal static VirtioPciBarRegisterWindowAccessExecutionResult FromPlan(
        VirtioPciBarRegisterWindowAccessExecutionStatus status,
        VirtioPciBarRegisterWindowAccessPlan plan,
        ulong value)
    {
        return new VirtioPciBarRegisterWindowAccessExecutionResult(
            status,
            plan.CapabilityType,
            plan.BarIndex,
            plan.RelativeOffset,
            plan.Width,
            plan.WidthBytes,
            plan.BarRelativeOffset,
            value,
            isInitialized: true);
    }
}

public readonly struct VirtioPciBarRegisterWindowLiveAccessorResult
{
    private VirtioPciBarRegisterWindowLiveAccessorResult(
        bool isInitialized,
        VirtioPciBarRegisterWindowLiveAccessorStatus status,
        VirtioPciBarRegisterWindowLiveAccessorOperation operation,
        VirtioPciRegisterReadStatus readStatus,
        VirtioPciRegisterWriteStatus writeStatus,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        PciBarKind barKind,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width,
        uint widthBytes,
        ulong barRelativeOffset,
        ulong physicalAddress,
        ulong virtualAddress,
        ulong value)
    {
        IsInitialized = isInitialized;
        Status = status;
        Operation = operation;
        ReadStatus = readStatus;
        WriteStatus = writeStatus;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        BarKind = barKind;
        RelativeOffset = relativeOffset;
        Width = width;
        WidthBytes = widthBytes;
        BarRelativeOffset = barRelativeOffset;
        PhysicalAddress = physicalAddress;
        VirtualAddress = virtualAddress;
        Value = value;
    }

    public bool IsInitialized { get; }

    public VirtioPciBarRegisterWindowLiveAccessorStatus Status { get; }

    public VirtioPciBarRegisterWindowLiveAccessorOperation Operation { get; }

    public VirtioPciRegisterReadStatus ReadStatus { get; }

    public VirtioPciRegisterWriteStatus WriteStatus { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public PciBarKind BarKind { get; }

    public uint RelativeOffset { get; }

    public VirtioPciRegisterAccessWidth Width { get; }

    public uint WidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong PhysicalAddress { get; }

    public ulong VirtualAddress { get; }

    public ulong Value { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioPciBarRegisterWindowLiveAccessorStatus.Success;

    internal static VirtioPciBarRegisterWindowLiveAccessorResult FromPlan(
        VirtioPciBarRegisterWindowLiveAccessorStatus status,
        VirtioPciBarRegisterWindowLiveAccessorOperation operation,
        VirtioPciBarRegisterWindowAccessPlan plan,
        VirtioPciRegisterReadStatus readStatus,
        VirtioPciRegisterWriteStatus writeStatus,
        ulong value)
    {
        return new VirtioPciBarRegisterWindowLiveAccessorResult(
            isInitialized: true,
            status,
            operation,
            readStatus,
            writeStatus,
            plan.CapabilityType,
            plan.BarIndex,
            plan.Binding.BarKind,
            plan.RelativeOffset,
            plan.Width,
            plan.WidthBytes,
            plan.BarRelativeOffset,
            plan.PhysicalAddress,
            plan.VirtualAddress,
            value);
    }
}

public readonly struct VirtioPciBarRegisterWindowBindingResult
{
    private VirtioPciBarRegisterWindowBindingResult(
        VirtioPciBarRegisterWindowPlanStatus status,
        VirtioPciBindingPlanStatus bindingPlanStatus,
        VirtioPciBarApertureValidationStatus apertureValidationStatus,
        VirtioPciCapabilityType failingCapabilityType,
        byte failingBarIndex,
        VirtioPciBarRegisterWindowBindingSet bindings,
        bool isInitialized)
    {
        Status = status;
        BindingPlanStatus = bindingPlanStatus;
        ApertureValidationStatus = apertureValidationStatus;
        FailingCapabilityType = failingCapabilityType;
        FailingBarIndex = failingBarIndex;
        Bindings = bindings;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarRegisterWindowPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public VirtioPciBindingPlanStatus BindingPlanStatus { get; }

    public VirtioPciBarApertureValidationStatus ApertureValidationStatus { get; }

    public VirtioPciCapabilityType FailingCapabilityType { get; }

    public byte FailingBarIndex { get; }

    public VirtioPciBarRegisterWindowBindingSet Bindings { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowPlanStatus.Success;

    internal static VirtioPciBarRegisterWindowBindingResult Success(
        VirtioPciBindingPlanStatus bindingPlanStatus,
        VirtioPciBarApertureValidationStatus apertureValidationStatus,
        VirtioPciBarRegisterWindowBindingSet bindings)
    {
        return new VirtioPciBarRegisterWindowBindingResult(
            VirtioPciBarRegisterWindowPlanStatus.Success,
            bindingPlanStatus,
            apertureValidationStatus,
            default,
            PciBarDecoder.NoBarIndex,
            bindings,
            isInitialized: true);
    }

    internal static VirtioPciBarRegisterWindowBindingResult Fail(
        VirtioPciBarRegisterWindowPlanStatus status,
        VirtioPciBindingPlanStatus bindingPlanStatus,
        VirtioPciBarApertureValidationStatus apertureValidationStatus,
        VirtioPciCapabilityType failingCapabilityType,
        byte failingBarIndex)
    {
        return new VirtioPciBarRegisterWindowBindingResult(
            status,
            bindingPlanStatus,
            apertureValidationStatus,
            failingCapabilityType,
            failingBarIndex,
            default,
            isInitialized: true);
    }
}

public readonly struct VirtioPciBarRegisterWindowAccessPlanResult
{
    private VirtioPciBarRegisterWindowAccessPlanResult(
        VirtioPciBarRegisterWindowPlanStatus status,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width,
        VirtioPciBarRegisterWindowAccessPlan plan,
        bool isInitialized)
    {
        Status = status;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        RelativeOffset = relativeOffset;
        Width = width;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarRegisterWindowPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public uint RelativeOffset { get; }

    public VirtioPciRegisterAccessWidth Width { get; }

    public VirtioPciBarRegisterWindowAccessPlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowPlanStatus.Success;

    internal static VirtioPciBarRegisterWindowAccessPlanResult Success(
        VirtioPciBarRegisterWindowAccessPlan plan)
    {
        return new VirtioPciBarRegisterWindowAccessPlanResult(
            VirtioPciBarRegisterWindowPlanStatus.Success,
            plan.CapabilityType,
            plan.BarIndex,
            plan.RelativeOffset,
            plan.Width,
            plan,
            isInitialized: true);
    }

    internal static VirtioPciBarRegisterWindowAccessPlanResult Fail(
        VirtioPciBarRegisterWindowPlanStatus status,
        VirtioPciBarRegisterWindowBinding binding,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width)
    {
        return new VirtioPciBarRegisterWindowAccessPlanResult(
            status,
            binding.CapabilityType,
            binding.BarIndex,
            relativeOffset,
            width,
            default,
            isInitialized: true);
    }
}

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

    public static VirtioPciBarRegisterWindowBindingResult BindRequiredApertures(
        VirtioPciBindingPlanResult bindingPlan,
        ReadOnlySpan<VirtioPciMappedBarWindow> windows)
    {
        if (!bindingPlan.Succeeded)
        {
            VirtioPciBarRegisterWindowPlanStatus status =
                bindingPlan.Status == VirtioPciBindingPlanStatus.ApertureValidationFailed
                    ? VirtioPciBarRegisterWindowPlanStatus.ApertureValidationFailed
                    : VirtioPciBarRegisterWindowPlanStatus.BindingPlanFailed;

            return VirtioPciBarRegisterWindowBindingResult.Fail(
                status,
                bindingPlan.Status,
                bindingPlan.ApertureValidation.Status,
                bindingPlan.ApertureValidation.FailingCapabilityType,
                bindingPlan.ApertureValidation.FailingBarIndex);
        }

        VirtioPciBarApertureSet apertures = bindingPlan.Binding.Apertures;

        VirtioPciBarRegisterWindowPlanStatus commonStatus =
            TryBindAperture(
                apertures.CommonConfiguration,
                windows,
                out VirtioPciBarRegisterWindowBinding commonConfiguration);
        if (commonStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, commonStatus, apertures.CommonConfiguration);
        }

        VirtioPciBarRegisterWindowPlanStatus notifyStatus =
            TryBindAperture(
                apertures.NotifyConfiguration,
                windows,
                out VirtioPciBarRegisterWindowBinding notifyConfiguration);
        if (notifyStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, notifyStatus, apertures.NotifyConfiguration);
        }

        VirtioPciBarRegisterWindowPlanStatus isrStatus =
            TryBindAperture(
                apertures.IsrStatus,
                windows,
                out VirtioPciBarRegisterWindowBinding isrStatusBinding);
        if (isrStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, isrStatus, apertures.IsrStatus);
        }

        VirtioPciBarRegisterWindowPlanStatus deviceStatus =
            TryBindAperture(
                apertures.DeviceConfiguration,
                windows,
                out VirtioPciBarRegisterWindowBinding deviceConfiguration);
        if (deviceStatus != VirtioPciBarRegisterWindowPlanStatus.Success)
        {
            return FailAperture(bindingPlan, deviceStatus, apertures.DeviceConfiguration);
        }

        return VirtioPciBarRegisterWindowBindingResult.Success(
            bindingPlan.Status,
            bindingPlan.ApertureValidation.Status,
            new VirtioPciBarRegisterWindowBindingSet(
                commonConfiguration,
                notifyConfiguration,
                isrStatusBinding,
                deviceConfiguration));
    }

    public static VirtioPciBarRegisterWindowAccessPlanResult PlanAccess(
        VirtioPciBarRegisterWindowBinding binding,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width)
    {
        uint widthBytes = GetWidthBytes(width);
        if (widthBytes == 0)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                binding,
                relativeOffset,
                width);
        }

        if (!binding.IsBound)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.MissingWindow,
                binding,
                relativeOffset,
                width);
        }

        ulong accessEnd = (ulong)relativeOffset + widthBytes;
        if (accessEnd > uint.MaxValue)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow,
                binding,
                relativeOffset,
                width);
        }

        if (accessEnd > binding.ApertureLength)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessRangeOutsideAperture,
                binding,
                relativeOffset,
                width);
        }

        ulong barRelativeOffset = (ulong)binding.ApertureOffset + relativeOffset;
        if (barRelativeOffset < binding.ApertureOffset)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow,
                binding,
                relativeOffset,
                width);
        }

        if ((barRelativeOffset & (widthBytes - 1U)) != 0)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessUnaligned,
                binding,
                relativeOffset,
                width);
        }

        if (barRelativeOffset > ulong.MaxValue - widthBytes)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow,
                binding,
                relativeOffset,
                width);
        }

        ulong accessBarEnd = barRelativeOffset + widthBytes;
        if (accessBarEnd > binding.WindowLength)
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessRangeOutsideAperture,
                binding,
                relativeOffset,
                width);
        }

        if (AddOverflows(binding.BarVirtualBaseAddress, barRelativeOffset, out ulong virtualAddress) ||
            AddOverflows(binding.BarPhysicalBaseAddress, barRelativeOffset, out ulong physicalAddress))
        {
            return VirtioPciBarRegisterWindowAccessPlanResult.Fail(
                VirtioPciBarRegisterWindowPlanStatus.AccessRangeOverflow,
                binding,
                relativeOffset,
                width);
        }

        return VirtioPciBarRegisterWindowAccessPlanResult.Success(
            new VirtioPciBarRegisterWindowAccessPlan(
                binding,
                relativeOffset,
                width,
                widthBytes,
                barRelativeOffset,
                physicalAddress,
                virtualAddress));
    }

    private static VirtioPciBarRegisterWindowPlanStatus TryBindAperture(
        VirtioPciBarAperture aperture,
        ReadOnlySpan<VirtioPciMappedBarWindow> windows,
        out VirtioPciBarRegisterWindowBinding binding)
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

        if (AddOverflows(
                window.PhysicalBaseAddress,
                aperture.Offset,
                out ulong aperturePhysicalBaseAddress) ||
            AddOverflows(
                window.VirtualBaseAddress,
                aperture.Offset,
                out ulong apertureVirtualBaseAddress))
        {
            return VirtioPciBarRegisterWindowPlanStatus.ApertureAddressOverflow;
        }

        binding = new VirtioPciBarRegisterWindowBinding(
            aperture,
            window,
            aperturePhysicalBaseAddress,
            apertureVirtualBaseAddress);
        return VirtioPciBarRegisterWindowPlanStatus.Success;
    }

    private static VirtioPciBarRegisterWindowBindingResult FailAperture(
        VirtioPciBindingPlanResult bindingPlan,
        VirtioPciBarRegisterWindowPlanStatus status,
        VirtioPciBarAperture aperture)
    {
        return VirtioPciBarRegisterWindowBindingResult.Fail(
            status,
            bindingPlan.Status,
            bindingPlan.ApertureValidation.Status,
            aperture.CapabilityType,
            aperture.BarIndex);
    }

    private static uint GetWidthBytes(VirtioPciRegisterAccessWidth width)
    {
        return width == VirtioPciRegisterAccessWidth.Byte
            ? 1U
            : width == VirtioPciRegisterAccessWidth.Word
                ? 2U
                : width == VirtioPciRegisterAccessWidth.DWord
                    ? 4U
                    : width == VirtioPciRegisterAccessWidth.QWord
                        ? 8U
                        : 0U;
    }

    private static bool AddOverflows(ulong left, ulong right, out ulong value)
    {
        value = left + right;
        return value < left;
    }
}

public static class VirtioPciBarRegisterWindowAccessExecutor
{
    public const bool RequiresAccessPlan = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool MapsBars = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ReadsLiveDeviceRegisters = false;
    public const bool WritesLiveDeviceRegisters = false;
    public const bool ConfiguresQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;

    public static VirtioPciBarRegisterWindowAccessExecutionResult Read(
        VirtioPciBarRegisterWindowAccessPlan plan,
        ReadOnlySpan<byte> windowBytes)
    {
        VirtioPciBarRegisterWindowAccessExecutionStatus status =
            Validate(plan, windowBytes.Length);
        if (status != VirtioPciBarRegisterWindowAccessExecutionStatus.Success)
        {
            return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(
                status,
                plan,
                value: 0);
        }

        int offset = checked((int)plan.BarRelativeOffset);
        ulong value = plan.WidthBytes == 1
            ? windowBytes[offset]
            : plan.WidthBytes == 2
                ? EndianPrimitives.ReadUInt16LittleEndian(
                    windowBytes.Slice(offset, sizeof(ushort)))
                : plan.WidthBytes == 4
                    ? EndianPrimitives.ReadUInt32LittleEndian(
                        windowBytes.Slice(offset, sizeof(uint)))
                    : EndianPrimitives.ReadUInt64LittleEndian(
                        windowBytes.Slice(offset, sizeof(ulong)));

        return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
            plan,
            value);
    }

    public static VirtioPciBarRegisterWindowAccessExecutionResult Write(
        VirtioPciBarRegisterWindowAccessPlan plan,
        ulong value,
        Span<byte> windowBytes)
    {
        VirtioPciBarRegisterWindowAccessExecutionStatus status =
            Validate(plan, windowBytes.Length);
        if (status != VirtioPciBarRegisterWindowAccessExecutionStatus.Success)
        {
            return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(
                status,
                plan,
                value: 0);
        }

        int offset = checked((int)plan.BarRelativeOffset);
        if (plan.WidthBytes == 1)
        {
            windowBytes[offset] = (byte)value;
        }
        else if (plan.WidthBytes == 2)
        {
            EndianPrimitives.WriteUInt16LittleEndian(
                windowBytes.Slice(offset, sizeof(ushort)),
                (ushort)value);
        }
        else if (plan.WidthBytes == 4)
        {
            EndianPrimitives.WriteUInt32LittleEndian(
                windowBytes.Slice(offset, sizeof(uint)),
                (uint)value);
        }
        else
        {
            EndianPrimitives.WriteUInt64LittleEndian(
                windowBytes.Slice(offset, sizeof(ulong)),
                value);
        }

        return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
            plan,
            value);
    }

    private static VirtioPciBarRegisterWindowAccessExecutionStatus Validate(
        VirtioPciBarRegisterWindowAccessPlan plan,
        int windowLength)
    {
        if (!plan.Binding.IsBound || plan.WidthBytes == 0)
        {
            return VirtioPciBarRegisterWindowAccessExecutionStatus.MissingAccessPlan;
        }

        uint widthBytes = GetWidthBytes(plan.Width);
        if (widthBytes == 0 || widthBytes != plan.WidthBytes)
        {
            return VirtioPciBarRegisterWindowAccessExecutionStatus.UnsupportedAccessWidth;
        }

        if (plan.BarRelativeOffset > int.MaxValue)
        {
            return VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall;
        }

        ulong endOffset = plan.BarRelativeOffset + plan.WidthBytes;
        if (endOffset < plan.BarRelativeOffset || endOffset > (ulong)windowLength)
        {
            return VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall;
        }

        return VirtioPciBarRegisterWindowAccessExecutionStatus.Success;
    }

    private static uint GetWidthBytes(VirtioPciRegisterAccessWidth width)
    {
        return width == VirtioPciRegisterAccessWidth.Byte
            ? 1U
            : width == VirtioPciRegisterAccessWidth.Word
                ? 2U
                : width == VirtioPciRegisterAccessWidth.DWord
                    ? 4U
                    : width == VirtioPciRegisterAccessWidth.QWord
                        ? 8U
                        : 0U;
    }
}

public static class VirtioPciBarRegisterWindowLiveAccessorBoundary
{
    public const bool RequiresAccessPlan = true;
    public const bool RequiresInjectedAccessor = true;
    public const bool ConsumesMappedRegisterWindowAccessPlan = true;
    public const bool DelegatesOnlyPlannedAccessorOperations = true;
    public const bool ContainsLiveMmioImplementation = false;
    public const bool ContainsPortIoImplementation = false;
    public const bool MapsBars = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool ConfiguresQueuesByItself = false;
    public const bool NotifiesDeviceByItself = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;

    public static VirtioPciBarRegisterWindowLiveAccessorResult Read16(
        VirtioPciBarRegisterWindowAccessPlan plan,
        IVirtioPciRegisterAccessor? accessor)
    {
        VirtioPciBarRegisterWindowLiveAccessorStatus status = ValidatePlan(
            plan,
            VirtioPciRegisterAccessWidth.Word,
            isRead: true);
        if (status != VirtioPciBarRegisterWindowLiveAccessorStatus.Success)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
                status,
                VirtioPciBarRegisterWindowLiveAccessorOperation.Read16,
                plan,
                VirtioPciRegisterReadStatus.NotReady,
                VirtioPciRegisterWriteStatus.Success,
                value: 0);
        }

        if (accessor is null)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
                VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady,
                VirtioPciBarRegisterWindowLiveAccessorOperation.Read16,
                plan,
                VirtioPciRegisterReadStatus.NotReady,
                VirtioPciRegisterWriteStatus.Success,
                value: 0);
        }

        VirtioPciRegisterRead16Result read =
            accessor.Read16(plan.Binding.Aperture, plan.RelativeOffset);
        if (read.Succeeded)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
                VirtioPciBarRegisterWindowLiveAccessorStatus.Success,
                VirtioPciBarRegisterWindowLiveAccessorOperation.Read16,
                plan,
                read.Status,
                VirtioPciRegisterWriteStatus.Success,
                read.Value);
        }

        return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
            read.Status == VirtioPciRegisterReadStatus.NotReady
                ? VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady
                : VirtioPciBarRegisterWindowLiveAccessorStatus.RegisterReadFailed,
            VirtioPciBarRegisterWindowLiveAccessorOperation.Read16,
            plan,
            read.Status,
            VirtioPciRegisterWriteStatus.Success,
            read.Value);
    }

    public static VirtioPciBarRegisterWindowLiveAccessorResult Write16(
        VirtioPciBarRegisterWindowAccessPlan plan,
        ushort value,
        IVirtioPciRegisterAccessor? accessor)
    {
        return Write(
            plan,
            VirtioPciBarRegisterWindowLiveAccessorOperation.Write16,
            VirtioPciRegisterAccessWidth.Word,
            value,
            accessor);
    }

    public static VirtioPciBarRegisterWindowLiveAccessorResult Write64(
        VirtioPciBarRegisterWindowAccessPlan plan,
        ulong value,
        IVirtioPciRegisterAccessor? accessor)
    {
        return Write(
            plan,
            VirtioPciBarRegisterWindowLiveAccessorOperation.Write64,
            VirtioPciRegisterAccessWidth.QWord,
            value,
            accessor);
    }

    private static VirtioPciBarRegisterWindowLiveAccessorResult Write(
        VirtioPciBarRegisterWindowAccessPlan plan,
        VirtioPciBarRegisterWindowLiveAccessorOperation operation,
        VirtioPciRegisterAccessWidth expectedWidth,
        ulong value,
        IVirtioPciRegisterAccessor? accessor)
    {
        VirtioPciBarRegisterWindowLiveAccessorStatus status = ValidatePlan(
            plan,
            expectedWidth,
            isRead: false);
        if (status != VirtioPciBarRegisterWindowLiveAccessorStatus.Success)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
                status,
                operation,
                plan,
                VirtioPciRegisterReadStatus.Success,
                VirtioPciRegisterWriteStatus.NotReady,
                value: 0);
        }

        if (accessor is null)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
                VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady,
                operation,
                plan,
                VirtioPciRegisterReadStatus.Success,
                VirtioPciRegisterWriteStatus.NotReady,
                value: 0);
        }

        VirtioPciRegisterWriteStatus writeStatus =
            operation == VirtioPciBarRegisterWindowLiveAccessorOperation.Write16
                ? accessor.Write16(plan.Binding.Aperture, plan.RelativeOffset, (ushort)value)
                : accessor.Write64(plan.Binding.Aperture, plan.RelativeOffset, value);
        if (writeStatus == VirtioPciRegisterWriteStatus.Success)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
                VirtioPciBarRegisterWindowLiveAccessorStatus.Success,
                operation,
                plan,
                VirtioPciRegisterReadStatus.Success,
                writeStatus,
                value);
        }

        return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(
            writeStatus == VirtioPciRegisterWriteStatus.NotReady
                ? VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady
                : VirtioPciBarRegisterWindowLiveAccessorStatus.RegisterWriteFailed,
            operation,
            plan,
            VirtioPciRegisterReadStatus.Success,
            writeStatus,
            value: 0);
    }

    private static VirtioPciBarRegisterWindowLiveAccessorStatus ValidatePlan(
        VirtioPciBarRegisterWindowAccessPlan plan,
        VirtioPciRegisterAccessWidth expectedWidth,
        bool isRead)
    {
        if (!plan.Binding.IsBound || plan.WidthBytes == 0)
        {
            return VirtioPciBarRegisterWindowLiveAccessorStatus.MissingAccessPlan;
        }

        if (plan.Width != expectedWidth || plan.WidthBytes != GetWidthBytes(expectedWidth))
        {
            return isRead
                ? VirtioPciBarRegisterWindowLiveAccessorStatus.UnsupportedReadWidth
                : VirtioPciBarRegisterWindowLiveAccessorStatus.UnsupportedWriteWidth;
        }

        return VirtioPciBarRegisterWindowLiveAccessorStatus.Success;
    }

    private static uint GetWidthBytes(VirtioPciRegisterAccessWidth width)
    {
        return width == VirtioPciRegisterAccessWidth.Word
            ? 2U
            : width == VirtioPciRegisterAccessWidth.QWord
                ? 8U
                : 0U;
    }
}
