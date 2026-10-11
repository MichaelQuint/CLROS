#if NOSTDLIB_EFI && M04_CONSTRUCTOR_FAILURE_PROBE
namespace System;

// The profile admits only this bounded wrapper identity, original inner object
// and compiler context. Type-name/message/stack-trace services are not supplied.
public sealed class TypeInitializationException : Exception
{
    private readonly Exception _innerException;
    private readonly ulong _constructorContext;

    public TypeInitializationException(Exception innerException, ulong constructorContext)
    {
        _innerException = innerException;
        _constructorContext = constructorContext;
    }

    public Exception InnerException => _innerException;
    public ulong ConstructorContext => _constructorContext;
}
#endif
