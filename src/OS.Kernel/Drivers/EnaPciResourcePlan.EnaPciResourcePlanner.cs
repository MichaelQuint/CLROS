using OS.Std.Binary;

namespace OS.Drivers;
public static class EnaPciResourcePlanner
{
    public const byte DefaultRegisterBarIndex = 0;
    public const ulong MinimumRegisterBarApertureLength = EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength;
    public static EnaPciResourcePlanResult Plan(EnaPciResourceSnapshot snapshot)
    {
        EnaPciCandidateValidationResult candidateValidation = EnaPciCandidateSelector.Validate(snapshot.Device);
        if (!candidateValidation.Succeeded)
        {
            return EnaPciResourcePlanResult.Fail(EnaPciResourcePlanStatus.CandidateValidationFailed, candidateValidation.Status, PciBarDecoder.NoBarIndex);
        }

        EnaPciResourcePlanStatus registerStatus = ValidateRegisterBar(snapshot, out PciBarResourcePlan registerBar);
        if (registerStatus != EnaPciResourcePlanStatus.Success)
        {
            return EnaPciResourcePlanResult.Fail(registerStatus, EnaPciCandidateValidationStatus.Success, snapshot.RegisterBarIndex);
        }

        if (!snapshot.Msix.IsPresent)
        {
            return EnaPciResourcePlanResult.Fail(EnaPciResourcePlanStatus.MissingMsixCapability, EnaPciCandidateValidationStatus.Success, PciBarDecoder.NoBarIndex);
        }

        if (!TryCreateMsixRegion(snapshot, isTable: true, snapshot.Msix.TableBarIndicator, snapshot.Msix.TableOffset, snapshot.Msix.TableLengthBytes, out PciMsixRegionPlan msixTable, out EnaPciResourcePlanStatus msixStatus, out byte failingBarIndex))
        {
            return EnaPciResourcePlanResult.Fail(msixStatus, EnaPciCandidateValidationStatus.Success, failingBarIndex);
        }

        if (!TryCreateMsixRegion(snapshot, isTable: false, snapshot.Msix.PendingBitArrayBarIndicator, snapshot.Msix.PendingBitArrayOffset, snapshot.Msix.PendingBitArrayLengthBytes, out PciMsixRegionPlan msixPendingBitArray, out msixStatus, out failingBarIndex))
        {
            return EnaPciResourcePlanResult.Fail(msixStatus, EnaPciCandidateValidationStatus.Success, failingBarIndex);
        }

        if (msixTable.BarIndex == msixPendingBitArray.BarIndex && RegionsOverlap(msixTable, msixPendingBitArray))
        {
            return EnaPciResourcePlanResult.Fail(EnaPciResourcePlanStatus.MsixRegionsOverlap, EnaPciCandidateValidationStatus.Success, msixTable.BarIndex);
        }

        EnaPciResourcePlan plan = new EnaPciResourcePlan(snapshot.SourceIndex, candidateValidation.DeviceKind, snapshot.Device, registerBar, snapshot.Msix, msixTable, msixPendingBitArray);
        return EnaPciResourcePlanResult.Success(plan);
    }

    private static EnaPciResourcePlanStatus ValidateRegisterBar(EnaPciResourceSnapshot snapshot, out PciBarResourcePlan registerBar)
    {
        registerBar = default;
        if (!snapshot.Bars.TryGet(snapshot.RegisterBarIndex, out PciBarDescriptor descriptor))
        {
            return EnaPciResourcePlanStatus.InvalidRegisterBarIndex;
        }

        if (descriptor.IsMalformed)
        {
            return EnaPciResourcePlanStatus.RegisterBarIsMalformed;
        }

        if (descriptor.IsIo)
        {
            return EnaPciResourcePlanStatus.RegisterBarIsIo;
        }

        if (!descriptor.IsMemory)
        {
            return EnaPciResourcePlanStatus.RegisterBarUnavailable;
        }

        if (descriptor.BaseAddressBits == 0)
        {
            return EnaPciResourcePlanStatus.RegisterBarBaseAddressZero;
        }

        if (!snapshot.Apertures.TryGet(snapshot.RegisterBarIndex, out ulong apertureLength) || apertureLength == 0)
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureMissing;
        }

        if (!IsPowerOfTwo(apertureLength))
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureSizeNotPowerOfTwo;
        }

        if ((descriptor.BaseAddressBits % apertureLength) != 0)
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureMisaligned;
        }

        if (apertureLength < MinimumRegisterBarApertureLength)
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureTooSmall;
        }

        registerBar = new PciBarResourcePlan(descriptor.Index, descriptor.Kind, descriptor.BaseAddressBits, apertureLength, descriptor.IsPrefetchable);
        return EnaPciResourcePlanStatus.Success;
    }

    private static bool TryCreateMsixRegion(EnaPciResourceSnapshot snapshot, bool isTable, byte barIndex, uint offset, ulong length, out PciMsixRegionPlan region, out EnaPciResourcePlanStatus status, out byte failingBarIndex)
    {
        region = default;
        failingBarIndex = barIndex;
        if (!snapshot.Bars.TryGet(barIndex, out PciBarDescriptor descriptor))
        {
            status = isTable ? EnaPciResourcePlanStatus.MsixTableBarInvalid : EnaPciResourcePlanStatus.MsixPbaBarInvalid;
            return false;
        }

        if (!descriptor.IsMemory)
        {
            status = isTable ? EnaPciResourcePlanStatus.MsixTableBarNotMemory : EnaPciResourcePlanStatus.MsixPbaBarNotMemory;
            return false;
        }

        if (!snapshot.Apertures.TryGet(barIndex, out ulong apertureLength) || apertureLength == 0)
        {
            status = isTable ? EnaPciResourcePlanStatus.MsixTableApertureMissing : EnaPciResourcePlanStatus.MsixPbaApertureMissing;
            return false;
        }

        if ((offset & (PciMsixCapabilityParser.PendingBitArrayWordSize - 1)) != 0)
        {
            status = EnaPciResourcePlanStatus.MsixOffsetMisaligned;
            return false;
        }

        if (!TryAdd(offset, length, out ulong endOffset) || endOffset > apertureLength)
        {
            status = EnaPciResourcePlanStatus.MsixRegionOutOfRange;
            return false;
        }

        region = new PciMsixRegionPlan(barIndex, offset, length);
        status = EnaPciResourcePlanStatus.Success;
        failingBarIndex = PciBarDecoder.NoBarIndex;
        return true;
    }

    private static bool RegionsOverlap(PciMsixRegionPlan left, PciMsixRegionPlan right)
    {
        return left.Offset < right.EndOffsetExclusive && right.Offset < left.EndOffsetExclusive;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong result)
    {
        if (left > ulong.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}
