namespace OS.Drivers;
public static partial class NvmeRegisterMap
{
    public const uint ControllerRegisterBaseOffset = 0x0000;
    public const uint DoorbellBaseOffset = 0x1000;
    public const uint RequiredAdminQueueRegisterApertureLength = AdminCompletionQueueBase.Offset + AdminCompletionQueueBase.Width;
    public static NvmeRegisterApertureValidationResult ValidateAdminQueueRegisterAperture(ulong apertureLength)
    {
        const ulong requiredLength = RequiredAdminQueueRegisterApertureLength;
        if (apertureLength < requiredLength)
        {
            return NvmeRegisterApertureValidationResult.Fail(NvmeRegisterApertureValidationStatus.AdminQueueRegistersTruncated, apertureLength, requiredLength);
        }

        return NvmeRegisterApertureValidationResult.Success(apertureLength, requiredLength);
    }

    public static uint CalculateDoorbellStrideBytes(ulong controllerCapabilities)
    {
        uint encodedStride = (uint)((controllerCapabilities >> ControllerCapabilities.DoorbellStrideShift) & ControllerCapabilities.DoorbellStrideMask);
        return Doorbell.MinimumStrideBytes << (int)encodedStride;
    }

    public static bool TryCalculateDoorbellOffset(ushort queueId, bool completionQueue, uint strideBytes, out uint offset)
    {
        if (strideBytes < Doorbell.MinimumStrideBytes)
        {
            offset = 0;
            return false;
        }

        uint doorbellIndex = ((uint)queueId * 2u) + (completionQueue ? 1u : 0u);
        ulong calculatedOffset = (ulong)DoorbellBaseOffset + ((ulong)doorbellIndex * strideBytes);
        if (calculatedOffset > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)calculatedOffset;
        return true;
    }
}
