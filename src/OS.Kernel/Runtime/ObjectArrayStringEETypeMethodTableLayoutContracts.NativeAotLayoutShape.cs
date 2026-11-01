using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum NativeAotLayoutShape : byte
{
    Uninitialized = 0,
    FixedObject = 1,
    SzArray = 2,
    String = 3
}
