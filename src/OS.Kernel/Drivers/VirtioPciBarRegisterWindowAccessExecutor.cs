using OS.Std.Binary;

namespace OS.Drivers;
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
    public static VirtioPciBarRegisterWindowAccessExecutionResult Read(VirtioPciBarRegisterWindowAccessPlan plan, ReadOnlySpan<byte> windowBytes)
    {
        VirtioPciBarRegisterWindowAccessExecutionStatus status = Validate(plan, windowBytes.Length);
        if (status != VirtioPciBarRegisterWindowAccessExecutionStatus.Success)
        {
            return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(status, plan, value: 0);
        }

        int offset = checked((int)plan.BarRelativeOffset);
        ulong value = plan.WidthBytes == 1 ? windowBytes[offset] : plan.WidthBytes == 2 ? EndianPrimitives.ReadUInt16LittleEndian(windowBytes.Slice(offset, sizeof(ushort))) : plan.WidthBytes == 4 ? EndianPrimitives.ReadUInt32LittleEndian(windowBytes.Slice(offset, sizeof(uint))) : EndianPrimitives.ReadUInt64LittleEndian(windowBytes.Slice(offset, sizeof(ulong)));
        return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(VirtioPciBarRegisterWindowAccessExecutionStatus.Success, plan, value);
    }

    public static VirtioPciBarRegisterWindowAccessExecutionResult Write(VirtioPciBarRegisterWindowAccessPlan plan, ulong value, Span<byte> windowBytes)
    {
        VirtioPciBarRegisterWindowAccessExecutionStatus status = Validate(plan, windowBytes.Length);
        if (status != VirtioPciBarRegisterWindowAccessExecutionStatus.Success)
        {
            return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(status, plan, value: 0);
        }

        int offset = checked((int)plan.BarRelativeOffset);
        if (plan.WidthBytes == 1)
        {
            windowBytes[offset] = (byte)value;
        }
        else if (plan.WidthBytes == 2)
        {
            EndianPrimitives.WriteUInt16LittleEndian(windowBytes.Slice(offset, sizeof(ushort)), (ushort)value);
        }
        else if (plan.WidthBytes == 4)
        {
            EndianPrimitives.WriteUInt32LittleEndian(windowBytes.Slice(offset, sizeof(uint)), (uint)value);
        }
        else
        {
            EndianPrimitives.WriteUInt64LittleEndian(windowBytes.Slice(offset, sizeof(ulong)), value);
        }

        return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(VirtioPciBarRegisterWindowAccessExecutionStatus.Success, plan, value);
    }

    private static VirtioPciBarRegisterWindowAccessExecutionStatus Validate(VirtioPciBarRegisterWindowAccessPlan plan, int windowLength)
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
        return width == VirtioPciRegisterAccessWidth.Byte ? 1U : width == VirtioPciRegisterAccessWidth.Word ? 2U : width == VirtioPciRegisterAccessWidth.DWord ? 4U : width == VirtioPciRegisterAccessWidth.QWord ? 8U : 0U;
    }
}
