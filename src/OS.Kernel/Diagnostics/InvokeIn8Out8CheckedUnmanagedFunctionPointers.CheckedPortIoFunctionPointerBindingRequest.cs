#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;

#endif
namespace OS.Kernel.Diagnostics;
public readonly struct CheckedPortIoFunctionPointerBindingRequest
{
    private CheckedPortIoFunctionPointerBindingRequest(bool isInitialized, bool sourcePlanSucceeded, ushort configuredBasePort, nuint payloadBaseAddress, nuint payloadEndAddress, nuint bytesMaterialized, nuint out8StubOffset, nuint in8StubOffset, nuint out8StubAddress, nuint in8StubAddress, int out8StubByteLength, int in8StubByteLength, int out8BytesCopied, int in8BytesCopied)
    {
        IsInitialized = isInitialized;
        SourcePlanSucceeded = sourcePlanSucceeded;
        ConfiguredBasePort = configuredBasePort;
        PayloadBaseAddress = payloadBaseAddress;
        PayloadEndAddress = payloadEndAddress;
        BytesMaterialized = bytesMaterialized;
        Out8StubOffset = out8StubOffset;
        In8StubOffset = in8StubOffset;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
        Out8StubByteLength = out8StubByteLength;
        In8StubByteLength = in8StubByteLength;
        Out8BytesCopied = out8BytesCopied;
        In8BytesCopied = in8BytesCopied;
    }

    public bool IsInitialized { get; }
    public bool SourcePlanSucceeded { get; }
    public ushort ConfiguredBasePort { get; }
    public nuint PayloadBaseAddress { get; }
    public nuint PayloadEndAddress { get; }
    public nuint BytesMaterialized { get; }
    public nuint Out8StubOffset { get; }
    public nuint In8StubOffset { get; }
    public nuint Out8StubAddress { get; }
    public nuint In8StubAddress { get; }
    public int Out8StubByteLength { get; }
    public int In8StubByteLength { get; }
    public int Out8BytesCopied { get; }
    public int In8BytesCopied { get; }

    public static CheckedPortIoFunctionPointerBindingRequest Create(bool sourcePlanSucceeded, ushort configuredBasePort, nuint payloadBaseAddress, nuint payloadEndAddress, nuint bytesMaterialized, nuint out8StubOffset, nuint in8StubOffset, nuint out8StubAddress, nuint in8StubAddress, int out8StubByteLength, int in8StubByteLength, int out8BytesCopied, int in8BytesCopied)
    {
        return new CheckedPortIoFunctionPointerBindingRequest(true, sourcePlanSucceeded, configuredBasePort, payloadBaseAddress, payloadEndAddress, bytesMaterialized, out8StubOffset, in8StubOffset, out8StubAddress, in8StubAddress, out8StubByteLength, in8StubByteLength, out8BytesCopied, in8BytesCopied);
    }

#if !NOSTDLIB_EFI
    public static CheckedPortIoFunctionPointerBindingRequest FromBindingPlan(DiagnosticStubCallableBindingPlanResult bindingPlan)
    {
        return Create(bindingPlan.Succeeded, bindingPlan.ConfiguredBasePort, bindingPlan.PayloadBaseAddress, bindingPlan.PayloadEndAddress, bindingPlan.BytesMaterialized, bindingPlan.Out8StubOffset, bindingPlan.In8StubOffset, bindingPlan.Out8StubAddress, bindingPlan.In8StubAddress, bindingPlan.Out8StubByteLength, bindingPlan.In8StubByteLength, bindingPlan.Out8BytesCopied, bindingPlan.In8BytesCopied);
    }
#endif
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
