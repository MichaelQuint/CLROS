namespace OS.Kernel.Runtime;

public enum NativeAotHelperStatus
{
    Unknown = 0,
    Deferred = 1,
    EfiOnlyLinkStub = 2,
    EfiOnlyFailFastStub = 3,
    Complete = 4,
    EfiOnlyStackProbe = 5,
    EfiOnlyFixedAllocation = 6,
    EfiOnlySzArrayAllocation = 7,
    EfiOnlyStringAllocation = 8,
    EfiOnlyCheckedReferences = 9
}
