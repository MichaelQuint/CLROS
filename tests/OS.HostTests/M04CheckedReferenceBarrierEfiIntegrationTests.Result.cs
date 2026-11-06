using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;

#if CHECKED_REFERENCE_CORE_TEST
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

#else
internal static partial class M04CheckedReferenceBarrierEfiIntegrationTests
{
    private readonly record struct Result(int Code, string Output, string Error, bool TimedOut);
}
#endif
