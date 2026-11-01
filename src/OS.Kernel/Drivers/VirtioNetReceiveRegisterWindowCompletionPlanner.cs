namespace OS.Drivers;
using OS.Net;

public static class VirtioNetReceiveRegisterWindowCompletionPlanner
{
    public const bool RequiresSuccessfulRegisterWindowReadiness = true;
    public const bool ConsumesRegisterWindowNotificationProvenance = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedReceiveBuffer = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsCallerOwnedReceiveBuffer = true;
    public const bool ClassifiesCallerOwnedFrame = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool CopiesPayload = false;
    public const bool WritesQueueMemory = false;
    public const bool InvokesRegisterWindowAccessExecutor = false;
    public const bool WritesCallerOwnedNotifyWindowBuffer = false;
    public static VirtioNetReceiveRegisterWindowCompletionResult CompleteReceive(VirtioNetReceiveReadinessResult readiness, ReadOnlySpan<byte> queueMemory, ReadOnlySpan<byte> receiveBuffer)
    {
        if (!readiness.Succeeded)
        {
            return VirtioNetReceiveRegisterWindowCompletionResult.Create(VirtioNetReceiveRegisterWindowCompletionStatus.ReadinessFailed, readiness, default, completionAttempted: false);
        }

        VirtioNetReceiveLifecycleCompletionResult completion = VirtioNetReceiveLifecycle.CompleteReceiveBuffer(readiness.ReceivePublish, queueMemory, receiveBuffer);
        if (!completion.Succeeded)
        {
            VirtioNetReceiveRegisterWindowCompletionStatus status = completion.Status == VirtioNetReceiveLifecycleCompletionStatus.FrameClassificationFailed ? VirtioNetReceiveRegisterWindowCompletionStatus.ClassificationFailed : VirtioNetReceiveRegisterWindowCompletionStatus.CompletionFailed;
            return VirtioNetReceiveRegisterWindowCompletionResult.Create(status, readiness, completion, completionAttempted: true);
        }

        return VirtioNetReceiveRegisterWindowCompletionResult.Create(VirtioNetReceiveRegisterWindowCompletionStatus.Success, readiness, completion, completionAttempted: true);
    }
}
