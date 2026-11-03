using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public static class GdtDescriptorPlan
{
    public const int SegmentDescriptorLength = 8;
    public const int SystemDescriptorLength = 16;
    public const int NullDescriptorOffset = 0;
    public const int KernelCodeDescriptorOffset = 8;
    public const int KernelDataDescriptorOffset = 16;
    public const int TssDescriptorOffset = 24;
    public const int RequiredByteLength = TssDescriptorOffset + SystemDescriptorLength;
    public const ushort NullSelector = 0x0000;
    public const ushort KernelCodeSelector = 0x0008;
    public const ushort KernelDataSelector = 0x0010;
    public const ushort TssSelector = 0x0018;
    public const byte KernelCodeAccess = 0x9a;
    public const byte KernelDataAccess = 0x92;
    public const byte AvailableTssAccess = 0x89;
    public const byte LongModeCodeFlags = 0x20;
    public const uint MinimumTssLimit = 103;
    public const uint MaximumTssLimit = 0x000f_ffff;
    public static GdtDescriptorPlanResult WriteKernelGdt(ulong tssBaseAddress, uint tssLimit, Span<byte> destination)
    {
        if (destination.Length < RequiredByteLength)
        {
            return GdtDescriptorPlanResult.Fail(GdtDescriptorPlanStatus.OutputBufferTooSmall, 0);
        }

        GdtDescriptorPlanStatus tssStatus = ValidateTss(tssBaseAddress, tssLimit);
        if (tssStatus != GdtDescriptorPlanStatus.Ok)
        {
            return GdtDescriptorPlanResult.Fail(tssStatus, 0);
        }

        destination.Slice(0, RequiredByteLength).Clear();
        WriteLongModeSegmentDescriptor(destination.Slice(KernelCodeDescriptorOffset, SegmentDescriptorLength), KernelCodeAccess, LongModeCodeFlags);
        WriteLongModeSegmentDescriptor(destination.Slice(KernelDataDescriptorOffset, SegmentDescriptorLength), KernelDataAccess, flags: 0);
        WriteTssDescriptor(destination.Slice(TssDescriptorOffset, SystemDescriptorLength), tssBaseAddress, tssLimit);
        return GdtDescriptorPlanResult.Success(RequiredByteLength);
    }

    public static GdtDescriptorPlanStatus ValidateTss(ulong tssBaseAddress, uint tssLimit)
    {
        if (tssBaseAddress == 0)
        {
            return GdtDescriptorPlanStatus.NullTssBaseAddress;
        }

        if (tssLimit < MinimumTssLimit || tssLimit > MaximumTssLimit)
        {
            return GdtDescriptorPlanStatus.InvalidTssLimit;
        }

        return GdtDescriptorPlanStatus.Ok;
    }

    private static void WriteLongModeSegmentDescriptor(Span<byte> destination, byte access, byte flags)
    {
        destination.Clear();
        destination[5] = access;
        destination[6] = flags;
    }

    private static void WriteTssDescriptor(Span<byte> destination, ulong baseAddress, uint limit)
    {
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(0, sizeof(ushort)), (ushort)limit);
        destination[2] = (byte)baseAddress;
        destination[3] = (byte)(baseAddress >> 8);
        destination[4] = (byte)(baseAddress >> 16);
        destination[5] = AvailableTssAccess;
        destination[6] = (byte)((limit >> 16) & 0x0f);
        destination[7] = (byte)(baseAddress >> 24);
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(8, sizeof(uint)), (uint)(baseAddress >> 32));
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(12, sizeof(uint)), 0);
    }
}
