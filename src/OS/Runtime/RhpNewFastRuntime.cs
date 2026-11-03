namespace Internal.Runtime
{
}

namespace System
{
#pragma warning disable 169
#pragma warning restore 169
#if M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE || M04_NESTED_FINALLY_CATCH_PROBE || M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_FILTERED_CATCH_PROBE || M04_NORMAL_FINALLY_PROBE




#endif
#pragma warning disable CS0649
#pragma warning restore CS0649
#pragma warning disable CS0649
#pragma warning restore CS0649
#if M04_STRING_ALLOCATION_PROBE

#endif
#if M04_STRING_ALLOCATION_PROBE

#else
#endif
#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#elif M04_CHECKED_REFERENCE_BARRIER_PROBE

#else
#endif
    namespace Runtime
    {
#if NOSTDLIB_EFI && M04_FILTERED_CATCH_PROBE

#else
#endif
#if NOSTDLIB_EFI && (M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_NORMAL_FINALLY_PROBE)

#else
#endif
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
