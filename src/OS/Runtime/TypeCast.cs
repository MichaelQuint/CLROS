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
        internal static unsafe class TypeCast
        {
            [RuntimeExport("RhTypeCast_IsInstanceOfClass")]
            public static object IsInstanceOfClass(Internal.Runtime.MethodTable* targetType, object value)
            {
#if NOSTDLIB_EFI && M04_FILTERED_CATCH_PROBE
                return OS.Runtime.RhpNewFastAllocation.MatchRetainedExceptionType((ulong)targetType, value) ? value : null;
#else
                RuntimeFailFast.Halt();
                return value;
#endif
            }

            [RuntimeExport("RhTypeCast_CheckCastClass")]
            public static object CheckCastClass(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastClassSpecial")]
            public static object CheckCastClassSpecial(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfArray")]
            public static object IsInstanceOfArray(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastArray")]
            public static object CheckCastArray(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfInterface")]
            public static object IsInstanceOfInterface(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastInterface")]
            public static object CheckCastInterface(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfAny")]
            public static object IsInstanceOfAny(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastAny")]
            public static object CheckCastAny(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastAny_NoCacheLookup")]
            public static object CheckCastAny_NoCacheLookup(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfException")]
            public static bool IsInstanceOfException(Internal.Runtime.MethodTable* targetType, object value)
            {
#if NOSTDLIB_EFI && (M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_NORMAL_FINALLY_PROBE)
                return OS.Runtime.RhpNewFastAllocation.MatchRetainedExceptionType((ulong)targetType, value);
#else
                RuntimeFailFast.Halt();
                return false;
#endif
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
