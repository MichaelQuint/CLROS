namespace OS.Kernel.Runtime;
public enum NativeAotHelperArea
{
    Unknown = 0,
    ObjectAllocation = 1,
    ArrayAllocation = 2,
    StringAllocation = 3,
    WriteBarriers = 4,
    InterfaceDispatch = 5,
    ClassConstructors = 6,
    ThreadStatics = 7,
    ExceptionUnwind = 8,
    ModuleInitialization = 9,
    TypeManagerRtrScans = 10,
    GcStackRoots = 11,
    EfiReversePInvoke = 12,
    EfiFailFastTypeCasts = 13,
    EfiStackProbe = 14,
    EfiFixedAllocation = 15,
    EfiSzArrayAllocation = 16,
    EfiStringAllocation = 17,
    EfiCheckedReferences = 18
}
