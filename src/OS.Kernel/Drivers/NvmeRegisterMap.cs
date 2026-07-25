namespace OS.Drivers;

public enum NvmeRegisterApertureValidationStatus
{
    Success = 0,
    AdminQueueRegistersTruncated = 1
}

public readonly struct NvmeRegisterApertureValidationResult
{
    private NvmeRegisterApertureValidationResult(
        bool isInitialized,
        NvmeRegisterApertureValidationStatus status,
        ulong apertureLength,
        ulong requiredLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        ApertureLength = apertureLength;
        RequiredLength = requiredLength;
    }

    public NvmeRegisterApertureValidationStatus Status { get; }

    public ulong ApertureLength { get; }

    public ulong RequiredLength { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == NvmeRegisterApertureValidationStatus.Success;

    public static NvmeRegisterApertureValidationResult Success(
        ulong apertureLength,
        ulong requiredLength)
    {
        return new NvmeRegisterApertureValidationResult(
            isInitialized: true,
            NvmeRegisterApertureValidationStatus.Success,
            apertureLength,
            requiredLength);
    }

    public static NvmeRegisterApertureValidationResult Fail(
        NvmeRegisterApertureValidationStatus status,
        ulong apertureLength,
        ulong requiredLength)
    {
        return new NvmeRegisterApertureValidationResult(
            isInitialized: true,
            status,
            apertureLength,
            requiredLength);
    }
}

public static class NvmeRegisterMap
{
    public const uint ControllerRegisterBaseOffset = 0x0000;
    public const uint DoorbellBaseOffset = 0x1000;
    public const uint RequiredAdminQueueRegisterApertureLength =
        AdminCompletionQueueBase.Offset + AdminCompletionQueueBase.Width;

    public static class ControllerCapabilities
    {
        public const uint Offset = 0x0000;
        public const uint Width = sizeof(ulong);
        public const byte DoorbellStrideShift = 32;
        public const ulong DoorbellStrideMask = 0x0F;
    }

    public static class Version
    {
        public const uint Offset = 0x0008;
        public const uint Width = sizeof(uint);
    }

    public static class InterruptMaskSet
    {
        public const uint Offset = 0x000C;
        public const uint Width = sizeof(uint);
    }

    public static class InterruptMaskClear
    {
        public const uint Offset = 0x0010;
        public const uint Width = sizeof(uint);
    }

    public static class ControllerConfiguration
    {
        public const uint Offset = 0x0014;
        public const uint Width = sizeof(uint);
        public const byte EnableBit = 0;
        public const uint Enable = 1u << EnableBit;
        public const byte IoSubmissionQueueEntrySizeShift = 16;
        public const byte IoCompletionQueueEntrySizeShift = 20;
        public const uint QueueEntrySizeMask = 0x0F;
        public const uint IoSubmissionQueueEntrySizeMask =
            QueueEntrySizeMask << IoSubmissionQueueEntrySizeShift;
        public const uint IoCompletionQueueEntrySizeMask =
            QueueEntrySizeMask << IoCompletionQueueEntrySizeShift;
    }

    public static class ControllerStatus
    {
        public const uint Offset = 0x001C;
        public const uint Width = sizeof(uint);
        public const byte ReadyBit = 0;
        public const byte FatalStatusBit = 1;
        public const uint Ready = 1u << ReadyBit;
        public const uint ControllerFatalStatus = 1u << FatalStatusBit;
    }

    public static class AdminQueueAttributes
    {
        public const uint Offset = 0x0024;
        public const uint Width = sizeof(uint);
        public const byte SubmissionQueueSizeShift = 0;
        public const byte CompletionQueueSizeShift = 16;
        public const uint QueueSizeMask = 0x0FFF;
        public const uint SubmissionQueueSizeMask =
            QueueSizeMask << SubmissionQueueSizeShift;
        public const uint CompletionQueueSizeMask =
            QueueSizeMask << CompletionQueueSizeShift;
    }

    public static class AdminSubmissionQueueBase
    {
        public const uint Offset = 0x0028;
        public const uint Width = sizeof(ulong);
    }

    public static class AdminCompletionQueueBase
    {
        public const uint Offset = 0x0030;
        public const uint Width = sizeof(ulong);
    }

    public static class QueueEntries
    {
        public const ushort AdminQueueId = 0;
        public const uint SubmissionEntryBytes = 64;
        public const uint CompletionEntryBytes = 16;
        public const byte SubmissionEntrySizeExponent = 6;
        public const byte CompletionEntrySizeExponent = 4;
    }

    public static class Doorbell
    {
        public const uint RegisterWidth = sizeof(uint);
        public const uint MinimumStrideBytes = sizeof(uint);
        public const uint AdminSubmissionQueueTailOffset = DoorbellBaseOffset;
    }

    public static NvmeRegisterApertureValidationResult ValidateAdminQueueRegisterAperture(
        ulong apertureLength)
    {
        const ulong requiredLength = RequiredAdminQueueRegisterApertureLength;
        if (apertureLength < requiredLength)
        {
            return NvmeRegisterApertureValidationResult.Fail(
                NvmeRegisterApertureValidationStatus.AdminQueueRegistersTruncated,
                apertureLength,
                requiredLength);
        }

        return NvmeRegisterApertureValidationResult.Success(apertureLength, requiredLength);
    }

    public static uint CalculateDoorbellStrideBytes(ulong controllerCapabilities)
    {
        uint encodedStride = (uint)((controllerCapabilities >> ControllerCapabilities.DoorbellStrideShift)
            & ControllerCapabilities.DoorbellStrideMask);
        return Doorbell.MinimumStrideBytes << (int)encodedStride;
    }

    public static bool TryCalculateDoorbellOffset(
        ushort queueId,
        bool completionQueue,
        uint strideBytes,
        out uint offset)
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
