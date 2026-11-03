namespace Internal.Runtime
{
}

namespace System
{
#pragma warning disable 169
#pragma warning restore 169
#pragma warning disable CS0649
#pragma warning restore CS0649
#pragma warning disable CS0649
#pragma warning restore CS0649
    namespace Runtime
    {
        internal static unsafe class X64StackProbeRuntime
        {
            private static ulong s_probeEntry;
            public static bool TryInstall(ulong probeEntry)
            {
                if (probeEntry == 0 || (probeEntry & 15) != 0 || s_probeEntry != 0)
                {
                    return false;
                }

                s_probeEntry = probeEntry;
                return true;
            }

            public static bool TryClear(ulong expectedProbeEntry)
            {
                if (expectedProbeEntry == 0 || (s_probeEntry != 0 && s_probeEntry != expectedProbeEntry))
                {
                    return false;
                }

                s_probeEntry = 0;
                return true;
            }

            [RuntimeExport("RhpStackProbe")]
            private static void RhpStackProbe()
            {
                ulong probeEntry = s_probeEntry;
                if (probeEntry == 0)
                {
                    while (true)
                    {
                    }
                }

                ((delegate* managed<void>)(void*)probeEntry)();
            }
        }
    }

    namespace Runtime.CompilerServices
    {
    }
}

namespace System.Runtime.InteropServices
{
}

namespace Internal.Runtime.CompilerHelpers
{
    using System;
    using System.Runtime;
}

// Exact compiler metadata contracts; no array or nullable operations are supplied.
namespace System.Runtime.CompilerServices
{
}

namespace System
{
#pragma warning disable 169, 649
#pragma warning restore 169, 649
}

namespace System.Runtime
{
}

// Compiler metadata only, not reflection or a general String API.
namespace System.Reflection
{
}

namespace System.Runtime.CompilerServices
{
}
#if M04_STRING_ALLOCATION_PROBE
namespace System.Runtime
{

}
#endif
