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
    internal static unsafe class SpanHelpers
    {
        internal static void ClearWithoutReferences(ref byte destination, nuint byteLength)
        {
            fixed (byte* destinationPointer = &destination)
            {
                for (nuint i = 0; i < byteLength; i++)
                {
                    destinationPointer[i] = 0;
                }
            }
        }

        internal static void ClearWithReferences(ref IntPtr destination, nuint pointerLength)
        {
            fixed (IntPtr* destinationPointer = &destination)
            {
                byte* bytePointer = (byte*)destinationPointer;
                nuint byteLength = pointerLength * (nuint)sizeof(IntPtr);
                for (nuint i = 0; i < byteLength; i++)
                {
                    bytePointer[i] = 0;
                }
            }
        }

        internal static void ZeroMemory(ref byte destination, nuint byteLength)
        {
            ClearWithoutReferences(ref destination, byteLength);
        }

        internal static void Memmove(ref byte destination, ref byte source, nuint byteLength)
        {
            Buffer.Memmove(ref destination, ref source, byteLength);
        }

        internal static void Fill(ref byte destination, byte value, nuint byteLength)
        {
            fixed (byte* destinationPointer = &destination)
            {
                for (nuint i = 0; i < byteLength; i++)
                {
                    destinationPointer[i] = value;
                }
            }
        }

        internal static void Fill<T>(ref T destination, T value, nuint elementCount)
        {
            FillSingleOrHalt(ref destination, value, elementCount);
        }

        internal static void Fill<T>(ref T destination, nuint elementCount, T value)
        {
            FillSingleOrHalt(ref destination, value, elementCount);
        }

        private static void FillSingleOrHalt<T>(ref T destination, T value, nuint elementCount)
        {
            if (elementCount == 0)
            {
                return;
            }

            destination = value;
            if (elementCount != 1)
            {
                RuntimeFailFast.Halt();
            }
        }
    }

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
#if M04_RHP_NEW_FAST_PROBE

#else
#endif
#if M04_SZ_ARRAY_ALLOCATION_PROBE

#else
#endif
#if M04_SZ_ARRAY_ALLOCATION_PROBE

#else
#endif
#if M04_STRING_ALLOCATION_PROBE

#else
#endif
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
