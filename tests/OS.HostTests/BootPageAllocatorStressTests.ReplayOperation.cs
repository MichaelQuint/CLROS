using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorStressTests
{
    private enum ReplayOperation : byte
    {
        Initial = 0,
        Allocate = 1,
        AllocateEmptyFallback = 2,
        Free = 3,
        FreeDepthFallback = 4,
        DrainFree = 5,
        ExhaustionAllocate = 6,
        ExhaustionProbe = 7,
        ExhaustionDrainFree = 8,
        Final = 9
    }
}
