using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum NativeAotLayoutProfile : byte
{
    Uninitialized = 0,
    // Historical identity retained only for explicit rejection by the current profile.
    Ilc10_0_12WinX64LittleEndian = 1,
    Ilc11_0_0Rc2WinX64LittleEndian = 2,
    Ilc11_0_0Rc3WinX64LittleEndian = 3
}
