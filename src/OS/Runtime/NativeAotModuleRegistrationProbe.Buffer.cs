#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || TYPE_MANAGER_CAPTURE_HOST
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// One caller-owned static buffer, one original M01 authority, one core decoder.
// The numeric entry is also exercised on genuinely mapped controlled host storage.
internal static unsafe partial class NativeAotModuleRegistrationProbe
{

    internal struct Buffer
    {
        internal fixed ulong Words[512];
    }

}
#endif
