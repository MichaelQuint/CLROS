using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum NativeAotLayoutStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UnsupportedProfile = 2,
    InvalidFlags = 3,
    UnsupportedShape = 4,
    UnsupportedTypeFeatures = 5,
    InvalidBaseSize = 6,
    InvalidComponentSize = 7,
    InvalidLength = 8,
    CapacityTooSmall = 9
}
