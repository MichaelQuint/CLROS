using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public static class VirtioBlockDeviceConfigRegisterWindowReader
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
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;

    public static VirtioBlockDeviceConfigRegisterWindowReadResult Read(
        VirtioPciBarRegisterWindowBinding deviceConfiguration,
        ReadOnlySpan<byte> deviceWindowBytes,
        Span<byte> scratch)
    {
        if (scratch.Length < VirtioBlockDeviceConfig.CapacityLength)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromPlan(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall,
                VirtioBlockDeviceConfigStatus.ConfigTooSmall,
                BlockDeviceStatus.Success,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes,
                default,
                scratch.Length,
                bytesRead: 0,
                default,
                value: 0);
        }

        VirtioPciBarRegisterWindowAccessPlanResult plan =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                deviceConfiguration,
                VirtioBlockDeviceConfig.CapacityOffset,
                VirtioPciRegisterAccessWidth.QWord);
        if (!plan.Succeeded)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromPlan(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.RegisterWindowPlanFailed,
                VirtioBlockDeviceConfigStatus.Success,
                BlockDeviceStatus.Success,
                plan.Status,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes,
                default,
                scratch.Length,
                bytesRead: 0,
                plan.Plan,
                value: 0);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult access =
            VirtioPciBarRegisterWindowAccessExecutor.Read(
                plan.Plan,
                deviceWindowBytes);
        if (!access.Succeeded)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromAccess(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.AccessExecutionFailed,
                VirtioBlockDeviceConfigStatus.Success,
                BlockDeviceStatus.Success,
                plan.Status,
                access.Status,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes,
                default,
                scratch.Length,
                bytesRead: 0,
                access);
        }

        EndianPrimitives.WriteUInt64LittleEndian(
            scratch.Slice(VirtioBlockDeviceConfig.CapacityOffset, VirtioBlockDeviceConfig.CapacityLength),
            access.Value);

        VirtioBlockDeviceConfigResult config =
            VirtioBlockDeviceConfig.Parse(
                scratch.Slice(VirtioBlockDeviceConfig.CapacityOffset, VirtioBlockDeviceConfig.CapacityLength));
        if (!config.Succeeded)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromAccess(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.ConfigParseFailed,
                config.Status,
                config.BlockDeviceStatus,
                plan.Status,
                access.Status,
                config.RawCapacitySectors,
                config.SectorSizeBytes,
                config.BlockDeviceInfo,
                scratch.Length,
                VirtioBlockDeviceConfig.CapacityLength,
                access);
        }

        return VirtioBlockDeviceConfigRegisterWindowReadResult.FromAccess(
            VirtioBlockDeviceConfigRegisterWindowReadStatus.Success,
            config.Status,
            config.BlockDeviceStatus,
            plan.Status,
            access.Status,
            config.RawCapacitySectors,
            config.SectorSizeBytes,
            config.BlockDeviceInfo,
            scratch.Length,
            VirtioBlockDeviceConfig.CapacityLength,
            access);
    }
}
