using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe partial class M01LiveAllocatorIntegrationTests
{
    private enum StageOneCompletion : byte
    {
        PreparedOnly = 1,
        TransferRecorded = 2,
        Final = 3
    }
}
