namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public static class VirtioNetDeviceConfigRegisterWindowReader
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool UsesCallerOwnedScratchBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool ReadsCallerOwnedWindowBuffer = true;
    public const bool WritesCallerOwnedScratchBuffer = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
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
    public static VirtioNetDeviceConfigRegisterWindowReadResult Read(VirtioPciBarRegisterWindowBinding deviceConfiguration, ReadOnlySpan<byte> deviceWindowBytes, Span<byte> scratch)
    {
        if (scratch.Length < VirtioNetDeviceConfig.MinimumLength)
        {
            return VirtioNetDeviceConfigRegisterWindowReadResult.FromPlan(VirtioNetDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall, VirtioNetDeviceConfigStatus.BufferTooSmall, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, default, scratch.Length, bytesRead: 0, bytesConsumed: 0, statusFieldAttempted: false, statusFieldRead: false, default);
        }

        VirtioNetDeviceConfigRegisterWindowReadResult macPrefix = ReadDWord(deviceConfiguration, relativeOffset: VirtioNetDeviceConfig.MacAddressOffset, deviceWindowBytes, scratch, scratchOffset: VirtioNetDeviceConfig.MacAddressOffset, bytesRead: 0, statusFieldAttempted: false, statusFieldRead: false);
        if (!macPrefix.Succeeded)
        {
            return macPrefix;
        }

        VirtioNetDeviceConfigRegisterWindowReadResult macSuffix = ReadWord(deviceConfiguration, relativeOffset: VirtioNetDeviceConfig.MacAddressOffset + sizeof(uint), deviceWindowBytes, scratch, scratchOffset: VirtioNetDeviceConfig.MacAddressOffset + sizeof(uint), bytesRead: sizeof(uint), statusFieldAttempted: false, statusFieldRead: false);
        if (!macSuffix.Succeeded)
        {
            return macSuffix;
        }

        bool readStatus = deviceConfiguration.ApertureLength >= VirtioNetDeviceConfig.StatusConfigLength && scratch.Length >= VirtioNetDeviceConfig.StatusConfigLength;
        VirtioNetDeviceConfigRegisterWindowReadResult lastRead = macSuffix;
        int bytesRead = VirtioNetDeviceConfig.MinimumLength;
        if (readStatus)
        {
            lastRead = ReadWord(deviceConfiguration, relativeOffset: VirtioNetDeviceConfig.StatusOffset, deviceWindowBytes, scratch, scratchOffset: VirtioNetDeviceConfig.StatusOffset, bytesRead, statusFieldAttempted: true, statusFieldRead: false);
            if (!lastRead.Succeeded)
            {
                return lastRead;
            }

            bytesRead = VirtioNetDeviceConfig.StatusConfigLength;
        }

        VirtioNetDeviceConfigParseResult parse = VirtioNetDeviceConfig.TryParse(scratch.Slice(0, bytesRead));
        if (!parse.Succeeded)
        {
            return VirtioNetDeviceConfigRegisterWindowReadResult.FromLastRead(VirtioNetDeviceConfigRegisterWindowReadStatus.ConfigParseFailed, parse.Status, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, default, scratch.Length, bytesRead, parse.BytesConsumed, statusFieldAttempted: readStatus, statusFieldRead: readStatus, lastRead);
        }

        return VirtioNetDeviceConfigRegisterWindowReadResult.FromLastRead(VirtioNetDeviceConfigRegisterWindowReadStatus.Success, parse.Status, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, parse.Config, scratch.Length, bytesRead, parse.BytesConsumed, statusFieldAttempted: readStatus, statusFieldRead: readStatus, lastRead);
    }

    private static VirtioNetDeviceConfigRegisterWindowReadResult ReadDWord(VirtioPciBarRegisterWindowBinding deviceConfiguration, uint relativeOffset, ReadOnlySpan<byte> deviceWindowBytes, Span<byte> scratch, int scratchOffset, int bytesRead, bool statusFieldAttempted, bool statusFieldRead)
    {
        VirtioPciBarRegisterWindowAccessExecutionResult read = ReadAccess(deviceConfiguration, relativeOffset, VirtioPciRegisterAccessWidth.DWord, deviceWindowBytes, out VirtioPciBarRegisterWindowPlanStatus planStatus);
        if (planStatus != VirtioPciBarRegisterWindowPlanStatus.Success || !read.Succeeded)
        {
            return FailRead(read, planStatus, scratch.Length, bytesRead, statusFieldAttempted, statusFieldRead);
        }

        EndianPrimitives.WriteUInt32LittleEndian(scratch.Slice(scratchOffset, sizeof(uint)), (uint)read.Value);
        return VirtioNetDeviceConfigRegisterWindowReadResult.FromAccess(VirtioNetDeviceConfigRegisterWindowReadStatus.Success, VirtioNetDeviceConfigStatus.Success, planStatus, read.Status, default, scratch.Length, bytesRead + sizeof(uint), bytesConsumed: 0, statusFieldAttempted, statusFieldRead, read);
    }

    private static VirtioNetDeviceConfigRegisterWindowReadResult ReadWord(VirtioPciBarRegisterWindowBinding deviceConfiguration, uint relativeOffset, ReadOnlySpan<byte> deviceWindowBytes, Span<byte> scratch, int scratchOffset, int bytesRead, bool statusFieldAttempted, bool statusFieldRead)
    {
        VirtioPciBarRegisterWindowAccessExecutionResult read = ReadAccess(deviceConfiguration, relativeOffset, VirtioPciRegisterAccessWidth.Word, deviceWindowBytes, out VirtioPciBarRegisterWindowPlanStatus planStatus);
        if (planStatus != VirtioPciBarRegisterWindowPlanStatus.Success || !read.Succeeded)
        {
            return FailRead(read, planStatus, scratch.Length, bytesRead, statusFieldAttempted, statusFieldRead);
        }

        EndianPrimitives.WriteUInt16LittleEndian(scratch.Slice(scratchOffset, sizeof(ushort)), (ushort)read.Value);
        return VirtioNetDeviceConfigRegisterWindowReadResult.FromAccess(VirtioNetDeviceConfigRegisterWindowReadStatus.Success, VirtioNetDeviceConfigStatus.Success, planStatus, read.Status, default, scratch.Length, bytesRead + sizeof(ushort), bytesConsumed: 0, statusFieldAttempted, statusFieldRead: statusFieldAttempted || statusFieldRead, read);
    }

    private static VirtioPciBarRegisterWindowAccessExecutionResult ReadAccess(VirtioPciBarRegisterWindowBinding deviceConfiguration, uint relativeOffset, VirtioPciRegisterAccessWidth width, ReadOnlySpan<byte> deviceWindowBytes, out VirtioPciBarRegisterWindowPlanStatus planStatus)
    {
        VirtioPciBarRegisterWindowAccessPlanResult plan = VirtioPciBarRegisterWindowPlanner.PlanAccess(deviceConfiguration, relativeOffset, width);
        planStatus = plan.Status;
        if (!plan.Succeeded)
        {
            return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(VirtioPciBarRegisterWindowAccessExecutionStatus.Success, plan.Plan, value: 0);
        }

        return VirtioPciBarRegisterWindowAccessExecutor.Read(plan.Plan, deviceWindowBytes);
    }

    private static VirtioNetDeviceConfigRegisterWindowReadResult FailRead(VirtioPciBarRegisterWindowAccessExecutionResult read, VirtioPciBarRegisterWindowPlanStatus planStatus, int scratchLength, int bytesRead, bool statusFieldAttempted, bool statusFieldRead)
    {
        VirtioNetDeviceConfigRegisterWindowReadStatus status = planStatus == VirtioPciBarRegisterWindowPlanStatus.Success ? VirtioNetDeviceConfigRegisterWindowReadStatus.AccessExecutionFailed : VirtioNetDeviceConfigRegisterWindowReadStatus.RegisterWindowPlanFailed;
        return VirtioNetDeviceConfigRegisterWindowReadResult.FromAccess(status, VirtioNetDeviceConfigStatus.Success, planStatus, read.Status, default, scratchLength, bytesRead, bytesConsumed: 0, statusFieldAttempted, statusFieldRead, read);
    }
}
