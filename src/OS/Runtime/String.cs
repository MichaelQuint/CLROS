namespace Internal.Runtime
{
}

namespace System
{
#pragma warning disable 169
#pragma warning restore 169
#pragma warning disable CS0649
#pragma warning restore CS0649
    // Bounded compiler contract: the retained-page helper initializes this header.
    public sealed unsafe class String
    {
#pragma warning disable CS0649
        private readonly int _stringLength;
        private char _firstChar;
#pragma warning restore CS0649
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public extern String(char value, int count);
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string Ctor(char value, int count)
        {
            if (count < 0)
                while (true)
                    RuntimeFailFast.Halt();
            string result = FastAllocateString((nint)count);
#if M04_STRING_ALLOCATION_PROBE
            OS.Runtime.RhpNewFastAllocation.ConstructorEntered();
#endif
            fixed (char* first = &result._firstChar)
                for (int i = 0; i < count; i++)
                    first[i] = value;
            return result;
        }

        internal static string FastAllocateString(nint length)
        {
#if M04_STRING_ALLOCATION_PROBE
            return Runtime.RuntimeImports.RhNewString(Internal.Runtime.MethodTable.Of<string>(), length);
#else
            // Standalone shim closures do not import an unsupported allocator.
            while (true)
                RuntimeFailFast.Halt();
#endif
        }

        public int Length
        {
            [Runtime.CompilerServices.Intrinsic]
            get => _stringLength;
        }

        [Runtime.CompilerServices.IndexerName("Chars")]
        public char this[int index]
        {
            [Runtime.CompilerServices.Intrinsic]
            get
            {
                if ((uint)index >= (uint)_stringLength)
                    while (true)
                        RuntimeFailFast.Halt();
                fixed (char* first = &_firstChar)
                    return first[index];
            }
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
