using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class IdtrDescriptorTests
{
    private const ulong IdtBaseAddress = 0xffff800000100000UL;

    [HostTest("idtr descriptor creates 4096-byte table pointer", HostLane.Fast, 0)]
    private static bool TestCreates4096ByteTablePointer()
    {
        IdtrDescriptorResult result = IdtrDescriptor.Create(
            IdtBaseAddress,
            IdtTablePlan.EncodedByteLength);

        return HostCheck.Result(result.Succeeded
            && result.Status == IdtrDescriptorStatus.Ok
            && result.ActualBytes == IdtTablePlan.EncodedByteLength
            && result.RequiredBytes == IdtrDescriptor.RequiredTableByteLength
            && result.Descriptor.Limit == 4095
            && result.Descriptor.Limit == IdtrDescriptor.RequiredLimit
            && result.Descriptor.BaseAddress == IdtBaseAddress);
    }

    [HostTest("idtr descriptor writes packed bytes", HostLane.Fast, 1)]
    private static bool TestWritesPackedBytes()
    {
        IdtrDescriptorResult createResult = IdtrDescriptor.Create(
            IdtBaseAddress,
            IdtTablePlan.EncodedByteLength);

        byte[] bytes = new byte[IdtrDescriptor.EncodedLength];
        IdtrDescriptorResult writeResult = createResult.Descriptor.Write(bytes);

        byte[] shortBytes = new byte[IdtrDescriptor.EncodedLength - 1];
        IdtrDescriptorResult shortWriteResult = createResult.Descriptor.Write(shortBytes);

        return HostCheck.Result(createResult.Succeeded
            && writeResult.Succeeded
            && writeResult.ActualBytes == IdtrDescriptor.EncodedLength
            && writeResult.RequiredBytes == IdtrDescriptor.EncodedLength
            && bytes[0] == 0xff
            && bytes[1] == 0x0f
            && bytes[2] == 0x00
            && bytes[3] == 0x00
            && bytes[4] == 0x10
            && bytes[5] == 0x00
            && bytes[6] == 0x00
            && bytes[7] == 0x80
            && bytes[8] == 0xff
            && bytes[9] == 0xff
            && !shortWriteResult.Succeeded
            && shortWriteResult.Status == IdtrDescriptorStatus.OutputBufferTooSmall
            && shortWriteResult.ActualBytes == shortBytes.Length
            && shortWriteResult.RequiredBytes == IdtrDescriptor.EncodedLength);
    }

    [HostTest("idtr descriptor rejects invalid inputs", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidInputs()
    {
        IdtrDescriptorResult nullBase = IdtrDescriptor.Create(0, IdtTablePlan.EncodedByteLength);
        IdtrDescriptorResult tooSmall = IdtrDescriptor.Create(
            IdtBaseAddress,
            IdtTablePlan.EncodedByteLength - 1);
        IdtrDescriptorResult tooLarge = IdtrDescriptor.Create(
            IdtBaseAddress,
            IdtTablePlan.EncodedByteLength + IdtGateDescriptor.EncodedLength);

        return HostCheck.Result(!nullBase.Succeeded
            && nullBase.Status == IdtrDescriptorStatus.NullBaseAddress
            && !tooSmall.Succeeded
            && tooSmall.Status == IdtrDescriptorStatus.TableTooSmall
            && tooSmall.ActualBytes == IdtTablePlan.EncodedByteLength - 1
            && !tooLarge.Succeeded
            && tooLarge.Status == IdtrDescriptorStatus.TableTooLarge
            && tooLarge.ActualBytes == IdtTablePlan.EncodedByteLength + IdtGateDescriptor.EncodedLength);
    }
}
