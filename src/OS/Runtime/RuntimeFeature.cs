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
        public static class RuntimeFeature
        {
            public const string UnmanagedSignatureCallingConvention = nameof(UnmanagedSignatureCallingConvention);
            // Compiler admission for the pointer-backed shared Span subset.
            // This does not supply managed-heap interior-reference services.
            public const string ByRefFields = nameof(ByRefFields);
        }
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
