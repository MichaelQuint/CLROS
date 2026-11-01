using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public enum Com1PortIoBackendContractStatus
{
    Success = 0,
    BackendReadinessFailed = 1,
    StubBundlePlanFailed = 2,
    MissingOut8StubSlot = 3,
    MissingIn8StubSlot = 4,
    InvalidPayloadBaseAddress = 5,
    AddressOverflow = 6,
    InvalidCom1BasePort = 7
}
