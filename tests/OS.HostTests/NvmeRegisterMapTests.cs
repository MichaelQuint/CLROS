using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class NvmeRegisterMapTests
{

    [HostTest("drivers.nvme.registermap rejects default aperture result", HostLane.Fast, 4)]
    private static bool TestRejectsDefaultApertureResult()
    {
        NvmeRegisterApertureValidationResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NvmeRegisterApertureValidationStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                NvmeRegisterApertureValidationResult>());
    }

    [HostTest("drivers.nvme.registermap controller offsets", HostLane.Fast, 0)]
    private static bool TestControllerOffsets()
    {
        return HostCheck.Result(NvmeRegisterMap.ControllerRegisterBaseOffset == 0x0000
            && NvmeRegisterMap.ControllerCapabilities.Offset == 0x0000
            && NvmeRegisterMap.ControllerCapabilities.Width == 8
            && NvmeRegisterMap.Version.Offset == 0x0008
            && NvmeRegisterMap.Version.Width == 4
            && NvmeRegisterMap.InterruptMaskSet.Offset == 0x000C
            && NvmeRegisterMap.InterruptMaskSet.Width == 4
            && NvmeRegisterMap.InterruptMaskClear.Offset == 0x0010
            && NvmeRegisterMap.InterruptMaskClear.Width == 4
            && NvmeRegisterMap.ControllerConfiguration.Offset == 0x0014
            && NvmeRegisterMap.ControllerConfiguration.Width == 4
            && NvmeRegisterMap.ControllerStatus.Offset == 0x001C
            && NvmeRegisterMap.ControllerStatus.Width == 4
            && NvmeRegisterMap.DoorbellBaseOffset == 0x1000
            && NvmeRegisterMap.Doorbell.RegisterWidth == 4
            && NvmeRegisterMap.Doorbell.AdminSubmissionQueueTailOffset == 0x1000);
    }

    [HostTest("drivers.nvme.registermap admin queue offsets", HostLane.Fast, 1)]
    private static bool TestAdminQueueOffsets()
    {
        return HostCheck.Result(NvmeRegisterMap.AdminQueueAttributes.Offset == 0x0024
            && NvmeRegisterMap.AdminQueueAttributes.Width == 4
            && NvmeRegisterMap.AdminQueueAttributes.SubmissionQueueSizeShift == 0
            && NvmeRegisterMap.AdminQueueAttributes.CompletionQueueSizeShift == 16
            && NvmeRegisterMap.AdminQueueAttributes.QueueSizeMask == 0x0FFF
            && NvmeRegisterMap.AdminQueueAttributes.SubmissionQueueSizeMask == 0x00000FFF
            && NvmeRegisterMap.AdminQueueAttributes.CompletionQueueSizeMask == 0x0FFF0000
            && NvmeRegisterMap.AdminSubmissionQueueBase.Offset == 0x0028
            && NvmeRegisterMap.AdminSubmissionQueueBase.Width == 8
            && NvmeRegisterMap.AdminCompletionQueueBase.Offset == 0x0030
            && NvmeRegisterMap.AdminCompletionQueueBase.Width == 8
            && NvmeRegisterMap.RequiredAdminQueueRegisterApertureLength == 0x0038
            && NvmeRegisterMap.QueueEntries.AdminQueueId == 0
            && NvmeRegisterMap.QueueEntries.SubmissionEntryBytes == 64
            && NvmeRegisterMap.QueueEntries.CompletionEntryBytes == 16
            && NvmeRegisterMap.QueueEntries.SubmissionEntrySizeExponent == 6
            && NvmeRegisterMap.QueueEntries.CompletionEntrySizeExponent == 4);
    }

    [HostTest("drivers.nvme.registermap status bits", HostLane.Fast, 2)]
    private static bool TestStatusBits()
    {
        return HostCheck.Result(NvmeRegisterMap.ControllerConfiguration.EnableBit == 0
            && NvmeRegisterMap.ControllerConfiguration.Enable == 0x00000001
            && NvmeRegisterMap.ControllerConfiguration.IoSubmissionQueueEntrySizeShift == 16
            && NvmeRegisterMap.ControllerConfiguration.IoCompletionQueueEntrySizeShift == 20
            && NvmeRegisterMap.ControllerConfiguration.IoSubmissionQueueEntrySizeMask == 0x000F0000
            && NvmeRegisterMap.ControllerConfiguration.IoCompletionQueueEntrySizeMask == 0x00F00000
            && NvmeRegisterMap.ControllerStatus.ReadyBit == 0
            && NvmeRegisterMap.ControllerStatus.Ready == 0x00000001
            && NvmeRegisterMap.ControllerStatus.FatalStatusBit == 1
            && NvmeRegisterMap.ControllerStatus.ControllerFatalStatus == 0x00000002);
    }

    [HostTest("drivers.nvme.registermap validates admin queue aperture length", HostLane.Fast, 3)]
    private static bool TestValidatesAdminQueueApertureLength()
    {
        NvmeRegisterApertureValidationResult tooShort =
            NvmeRegisterMap.ValidateAdminQueueRegisterAperture(0x0037);
        NvmeRegisterApertureValidationResult exact =
            NvmeRegisterMap.ValidateAdminQueueRegisterAperture(0x0038);
        NvmeRegisterApertureValidationResult larger =
            NvmeRegisterMap.ValidateAdminQueueRegisterAperture(0x1000);

        return HostCheck.Result(!tooShort.Succeeded
            && tooShort.Status == NvmeRegisterApertureValidationStatus.AdminQueueRegistersTruncated
            && tooShort.ApertureLength == 0x0037
            && tooShort.RequiredLength == 0x0038
            && exact.Succeeded
            && exact.Status == NvmeRegisterApertureValidationStatus.Success
            && exact.ApertureLength == 0x0038
            && exact.RequiredLength == 0x0038
            && larger.Succeeded
            && larger.ApertureLength == 0x1000
            && larger.RequiredLength == 0x0038);
    }

    [HostTest("drivers.nvme.registermap calculates doorbell stride", HostLane.Fast, 5)]
    private static bool TestCalculatesDoorbellStride()
    {
        ulong zeroStrideCapability = 0;
        ulong encodedThreeStrideCapability = 3UL << NvmeRegisterMap.ControllerCapabilities.DoorbellStrideShift;
        uint stride = NvmeRegisterMap.CalculateDoorbellStrideBytes(encodedThreeStrideCapability);

        bool adminSq = NvmeRegisterMap.TryCalculateDoorbellOffset(
            NvmeRegisterMap.QueueEntries.AdminQueueId,
            completionQueue: false,
            stride,
            out uint adminSubmissionQueueTail);
        bool adminCq = NvmeRegisterMap.TryCalculateDoorbellOffset(
            NvmeRegisterMap.QueueEntries.AdminQueueId,
            completionQueue: true,
            stride,
            out uint adminCompletionQueueHead);
        bool rejectsInvalidStride = NvmeRegisterMap.TryCalculateDoorbellOffset(
            queueId: 1,
            completionQueue: false,
            strideBytes: 2,
            out _);

        return HostCheck.Result(NvmeRegisterMap.ControllerCapabilities.DoorbellStrideShift == 32
            && NvmeRegisterMap.ControllerCapabilities.DoorbellStrideMask == 0x0F
            && NvmeRegisterMap.Doorbell.MinimumStrideBytes == 4
            && NvmeRegisterMap.CalculateDoorbellStrideBytes(zeroStrideCapability) == 4
            && stride == 32
            && adminSq
            && adminSubmissionQueueTail == 0x1000
            && adminCq
            && adminCompletionQueueHead == 0x1020
            && !rejectsInvalidStride);
    }
}
