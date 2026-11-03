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

    internal static class ThrowHelpers
    {
        private static void ThrowOverflowException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowIndexOutOfRangeException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowNullReferenceException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowDivideByZeroException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArrayTypeMismatchException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArrayMismatchException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowPlatformNotSupportedException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowTypeLoadException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowInvalidProgramException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowInvalidCastException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowBadImageFormatException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowMissingMethodException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowMissingFieldException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowMarshalDirectiveException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowNotSupportedException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowOutOfMemoryException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowRankException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowFeatureBodyRemoved()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArgumentException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArgumentNullException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArgumentOutOfRangeException()
        {
            RuntimeFailFast.Halt();
        }
    }
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
