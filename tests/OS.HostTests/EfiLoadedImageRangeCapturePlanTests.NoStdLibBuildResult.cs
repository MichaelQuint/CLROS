using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class EfiLoadedImageRangeCapturePlanTests
{
    private readonly record struct NoStdLibBuildResult(int ExitCode, bool TimedOut, string Output);
}
