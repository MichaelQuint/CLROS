using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.NativeAotEhClauseMetadata;
using Status = OS.Kernel.Runtime.NativeAotEhClauseStatus;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

internal static unsafe partial class NativeAotEhClauseMetadataTests
{
    internal readonly record struct Row(uint Start, uint Length, uint Kind, uint Handler, uint Extra = 0);
}
