using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if RHP_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

// Compiles the actual private POD core. No CLR object address or privileged path is invoked.
internal static unsafe partial class RhpCoreTestProgram
{
    private delegate bool RejectionReadback(RhpNewFastBumpState* bump, byte* bumpBefore, byte* backing, byte* backingBefore);

}
#else
#endif
