using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class GdtDescriptorPlanTests
{
    private const ulong TssBaseAddress = 0xffff80000020f000UL;
    private const uint TssLimit = 103;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("gdt descriptor plan encodes kernel segments", TestEncodesKernelSegments, ref failures);
        Run("gdt descriptor plan encodes tss descriptor", TestEncodesTssDescriptor, ref failures);
        Run("gdt descriptor plan rejects invalid inputs", TestRejectsInvalidInputs, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("GDT descriptor plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestEncodesKernelSegments()
    {
        byte[] bytes = new byte[GdtDescriptorPlan.RequiredByteLength];

        GdtDescriptorPlanResult result = GdtDescriptorPlan.WriteKernelGdt(
            TssBaseAddress,
            TssLimit,
            bytes);

        return result.Succeeded
            && result.Status == GdtDescriptorPlanStatus.Ok
            && result.BytesWritten == GdtDescriptorPlan.RequiredByteLength
            && result.RequiredBytes == GdtDescriptorPlan.RequiredByteLength
            && GdtDescriptorPlan.NullSelector == 0
            && GdtDescriptorPlan.KernelCodeSelector == 0x0008
            && GdtDescriptorPlan.KernelDataSelector == 0x0010
            && GdtDescriptorPlan.TssSelector == 0x0018
            && IsZero(bytes, GdtDescriptorPlan.NullDescriptorOffset, GdtDescriptorPlan.SegmentDescriptorLength)
            && bytes[GdtDescriptorPlan.KernelCodeDescriptorOffset + 5] == 0x9a
            && bytes[GdtDescriptorPlan.KernelCodeDescriptorOffset + 6] == 0x20
            && bytes[GdtDescriptorPlan.KernelDataDescriptorOffset + 5] == 0x92
            && bytes[GdtDescriptorPlan.KernelDataDescriptorOffset + 6] == 0x00;
    }

    private static bool TestEncodesTssDescriptor()
    {
        byte[] bytes = new byte[GdtDescriptorPlan.RequiredByteLength];

        GdtDescriptorPlanResult result = GdtDescriptorPlan.WriteKernelGdt(
            TssBaseAddress,
            TssLimit,
            bytes);

        int offset = GdtDescriptorPlan.TssDescriptorOffset;

        return result.Succeeded
            && bytes[offset + 0] == 0x67
            && bytes[offset + 1] == 0x00
            && bytes[offset + 2] == 0x00
            && bytes[offset + 3] == 0xf0
            && bytes[offset + 4] == 0x20
            && bytes[offset + 5] == 0x89
            && bytes[offset + 6] == 0x00
            && bytes[offset + 7] == 0x00
            && bytes[offset + 8] == 0x00
            && bytes[offset + 9] == 0x80
            && bytes[offset + 10] == 0xff
            && bytes[offset + 11] == 0xff
            && bytes[offset + 12] == 0x00
            && bytes[offset + 13] == 0x00
            && bytes[offset + 14] == 0x00
            && bytes[offset + 15] == 0x00;
    }

    private static bool TestRejectsInvalidInputs()
    {
        byte[] bytes = new byte[GdtDescriptorPlan.RequiredByteLength];
        byte[] shortBytes = new byte[GdtDescriptorPlan.RequiredByteLength - 1];

        GdtDescriptorPlanResult shortBuffer = GdtDescriptorPlan.WriteKernelGdt(
            TssBaseAddress,
            TssLimit,
            shortBytes);
        GdtDescriptorPlanResult nullTssBase = GdtDescriptorPlan.WriteKernelGdt(
            0,
            TssLimit,
            bytes);
        GdtDescriptorPlanResult smallTssLimit = GdtDescriptorPlan.WriteKernelGdt(
            TssBaseAddress,
            GdtDescriptorPlan.MinimumTssLimit - 1,
            bytes);
        GdtDescriptorPlanResult largeTssLimit = GdtDescriptorPlan.WriteKernelGdt(
            TssBaseAddress,
            GdtDescriptorPlan.MaximumTssLimit + 1,
            bytes);

        return !shortBuffer.Succeeded
            && shortBuffer.Status == GdtDescriptorPlanStatus.OutputBufferTooSmall
            && shortBuffer.RequiredBytes == GdtDescriptorPlan.RequiredByteLength
            && !nullTssBase.Succeeded
            && nullTssBase.Status == GdtDescriptorPlanStatus.NullTssBaseAddress
            && !smallTssLimit.Succeeded
            && smallTssLimit.Status == GdtDescriptorPlanStatus.InvalidTssLimit
            && !largeTssLimit.Succeeded
            && largeTssLimit.Status == GdtDescriptorPlanStatus.InvalidTssLimit;
    }

    private static bool IsZero(byte[] bytes, int offset, int length)
    {
        for (int index = 0; index < length; index++)
        {
            if (bytes[offset + index] != 0)
            {
                return false;
            }
        }

        return true;
    }
}
