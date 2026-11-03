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
    public interface IDynamicInterfaceCastable
    {
        bool IsInterfaceImplemented(System.RuntimeTypeHandle interfaceType, bool throwIfNotImplemented);
        System.RuntimeTypeHandle GetInterfaceImplementation(System.RuntimeTypeHandle interfaceType);
        System.RuntimeTypeHandle GetDynamicInterfaceImplementation(System.RuntimeTypeHandle interfaceType);
    }
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
