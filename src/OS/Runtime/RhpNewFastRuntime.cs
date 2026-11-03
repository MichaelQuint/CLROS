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
    internal static unsafe class RhpNewFastRuntime
    {
        [RuntimeExport("RhpNewFast")]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static object RhpNewFast(Internal.Runtime.MethodTable* methodTable)
        {
#if M04_RHP_NEW_FAST_PROBE
            nuint reference = (nuint)OS.Runtime.RhpNewFastAllocation.Allocate((ulong)methodTable);
            return System.Runtime.CompilerServices.Unsafe.As<nuint, object>(ref reference);
#else
            // This branch must compile with MinimalRuntime alone and cannot return.
            while (true)
                RuntimeFailFast.Halt();
#endif
        }

        [RuntimeExport("RhpNewArrayFast")]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static object RhpNewArrayFast(Internal.Runtime.MethodTable* methodTable, nint length)
        {
#if M04_SZ_ARRAY_ALLOCATION_PROBE
            nuint reference = (nuint)OS.Runtime.RhpNewFastAllocation.AllocateArray((ulong)methodTable, length, false);
            return System.Runtime.CompilerServices.Unsafe.As<nuint, object>(ref reference);
#else
            while (true)
                RuntimeFailFast.Halt();
#endif
        }

        [RuntimeExport("RhpNewPtrArrayFast")]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static object RhpNewPtrArrayFast(Internal.Runtime.MethodTable* methodTable, nint length)
        {
#if M04_SZ_ARRAY_ALLOCATION_PROBE
            nuint reference = (nuint)OS.Runtime.RhpNewFastAllocation.AllocateArray((ulong)methodTable, length, true);
            return System.Runtime.CompilerServices.Unsafe.As<nuint, object>(ref reference);
#else
            while (true)
                RuntimeFailFast.Halt();
#endif
        }

        [RuntimeExport("RhNewString")]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static string RhNewString(Internal.Runtime.MethodTable* methodTable, nint length)
        {
#if M04_STRING_ALLOCATION_PROBE
            nuint reference = (nuint)OS.Runtime.RhpNewFastAllocation.AllocateString((ulong)methodTable, (long)length);
            return System.Runtime.CompilerServices.Unsafe.As<nuint, string>(ref reference);
#else
            while (true)
                RuntimeFailFast.Halt();
#endif
        }
    }
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
