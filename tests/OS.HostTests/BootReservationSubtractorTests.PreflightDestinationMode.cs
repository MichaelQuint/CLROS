using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootReservationSubtractorTests
{
    private enum PreflightDestinationMode : byte
    {
        Valid = 0,
        Null = 1,
        Misaligned = 2,
        Poison = 3
    }
}
