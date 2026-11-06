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
    public static unsafe class Buffer
    {
        public static void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy < 0 || destinationSizeInBytes < 0 || sourceBytesToCopy > destinationSizeInBytes)
            {
                Halt();
            }

            Memmove(ref *(byte*)destination, ref *(byte*)source, (nuint)sourceBytesToCopy);
        }

        public static void MemoryCopy(void* source, void* destination, ulong destinationSizeInBytes, ulong sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                Halt();
            }

            Memmove(ref *(byte*)destination, ref *(byte*)source, (nuint)sourceBytesToCopy);
        }

        internal static void Memmove(ref byte destination, ref byte source, nuint length)
        {
            fixed (byte* destinationPointer = &destination)
            fixed (byte* sourcePointer = &source)
            {
                if (destinationPointer < sourcePointer || destinationPointer >= sourcePointer + length)
                {
                    for (nuint i = 0; i < length; i++)
                    {
                        destinationPointer[i] = sourcePointer[i];
                    }
                }
                else
                {
                    for (nuint i = length; i > 0; i--)
                    {
                        destinationPointer[i - 1] = sourcePointer[i - 1];
                    }
                }
            }
        }

        internal static void BulkMoveWithWriteBarrier(ref byte destination, ref byte source, nuint length)
        {
#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
            while (true)
                RuntimeFailFast.Halt();
#elif M04_CHECKED_REFERENCE_BARRIER_PROBE
            OS.Runtime.RhpNewFastAllocation.MoveReferences(ref destination, ref source, length);
#else
            Memmove(ref destination, ref source, length);
#endif
        }

        private static void Halt()
        {
            RuntimeFailFast.Halt();
        }
    }

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
