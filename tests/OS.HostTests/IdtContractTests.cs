using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class IdtContractTests
{

    [HostTest("idt descriptor encodes interrupt gate", HostLane.Fast, 0)]
    private static bool TestInterruptGateEncoding()
    {
        const ulong handler = 0xffff800012345678;

        IdtGateDescriptorResult result = IdtGateDescriptor.Create(
            vector: FaultVectorCatalog.PageFault,
            handlerAddress: handler,
            codeSelector: 0x0008,
            istIndex: 2,
            gateType: IdtGateType.InterruptGate,
            descriptorPrivilegeLevel: 0,
            present: true);

        if (!result.Succeeded || result.Status != IdtGateDescriptorStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        IdtGateDescriptor descriptor = result.Descriptor;
        byte[] encoded = new byte[IdtGateDescriptor.EncodedLength];

        return HostCheck.Result(descriptor.TryWrite(encoded)
            && descriptor.Vector == FaultVectorCatalog.PageFault
            && descriptor.HandlerAddress == handler
            && descriptor.OffsetLow == 0x5678
            && descriptor.OffsetMiddle == 0x1234
            && descriptor.OffsetHigh == 0xffff8000
            && descriptor.CodeSelector == 0x0008
            && descriptor.IstIndex == 2
            && descriptor.GateType == IdtGateType.InterruptGate
            && descriptor.DescriptorPrivilegeLevel == 0
            && descriptor.Present
            && descriptor.TypeAttributes == 0x8e
            && encoded[0] == 0x78
            && encoded[1] == 0x56
            && encoded[2] == 0x08
            && encoded[3] == 0x00
            && encoded[4] == 0x02
            && encoded[5] == 0x8e
            && encoded[6] == 0x34
            && encoded[7] == 0x12
            && encoded[8] == 0x00
            && encoded[9] == 0x80
            && encoded[10] == 0xff
            && encoded[11] == 0xff
            && encoded[12] == 0x00
            && encoded[13] == 0x00
            && encoded[14] == 0x00
            && encoded[15] == 0x00);
    }

    [HostTest("idt descriptor rejects invalid ist index", HostLane.Fast, 1)]
    private static bool TestRejectsInvalidIstIndex()
    {
        IdtGateDescriptorResult result = IdtGateDescriptor.Create(
            vector: FaultVectorCatalog.InvalidOpcode,
            handlerAddress: 0x1000,
            codeSelector: 0x0008,
            istIndex: 8,
            gateType: IdtGateType.InterruptGate,
            descriptorPrivilegeLevel: 0,
            present: true);

        return HostCheck.Result(!result.Succeeded
            && result.Status == IdtGateDescriptorStatus.InvalidIstIndex);
    }

    [HostTest("idt descriptor rejects null handler address", HostLane.Fast, 2)]
    private static bool TestRejectsNullHandlerAddress()
    {
        IdtGateDescriptorResult result = IdtGateDescriptor.Create(
            vector: FaultVectorCatalog.DivideError,
            handlerAddress: 0,
            codeSelector: 0x0008,
            istIndex: 0,
            gateType: IdtGateType.InterruptGate,
            descriptorPrivilegeLevel: 0,
            present: true);

        return HostCheck.Result(!result.Succeeded
            && result.Status == IdtGateDescriptorStatus.NullHandlerAddress);
    }

    [HostTest("fault vectors expose error code policy", HostLane.Fast, 3)]
    private static bool TestFaultVectorErrorCodePolicy()
    {
        return HostCheck.Result(HasErrorCode(FaultVectorCatalog.DoubleFault, "#DF")
            && HasErrorCode(FaultVectorCatalog.InvalidTss, "#TS")
            && HasErrorCode(FaultVectorCatalog.SegmentNotPresent, "#NP")
            && HasErrorCode(FaultVectorCatalog.StackSegmentFault, "#SS")
            && HasErrorCode(FaultVectorCatalog.GeneralProtectionFault, "#GP")
            && HasErrorCode(FaultVectorCatalog.PageFault, "#PF")
            && DoesNotHaveErrorCode(FaultVectorCatalog.DivideError, "#DE")
            && DoesNotHaveErrorCode(FaultVectorCatalog.InvalidOpcode, "#UD")
            && DoesNotHaveErrorCode(FaultVectorCatalog.DeviceNotAvailable, "#NM"));
    }

    [HostTest("fault vectors include page fault metadata", HostLane.Fast, 4)]
    private static bool TestPageFaultMetadata()
    {
        return HostCheck.Result(FaultVectorCatalog.TryGet(FaultVectorCatalog.PageFault, out FaultVectorInfo pageFault)
            && pageFault.Vector == 14
            && pageFault.Mnemonic == "#PF"
            && pageFault.Name == "Page Fault"
            && pageFault.PushesErrorCode
            && pageFault.ErrorCodeMeaning == "page-fault"
            && pageFault.HasFaultAddressRegister
            && pageFault.FaultAddressRegister == "CR2");
    }

    private static bool HasErrorCode(byte vector, string mnemonic)
    {
        return FaultVectorCatalog.TryGet(vector, out FaultVectorInfo info)
            && info.Mnemonic == mnemonic
            && info.PushesErrorCode;
    }

    private static bool DoesNotHaveErrorCode(byte vector, string mnemonic)
    {
        return FaultVectorCatalog.TryGet(vector, out FaultVectorInfo info)
            && info.Mnemonic == mnemonic
            && !info.PushesErrorCode;
    }
}
