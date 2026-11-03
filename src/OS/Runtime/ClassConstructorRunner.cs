#if NOSTDLIB_EFI && (M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE)
namespace System.Runtime.CompilerServices;

// These are managed compiler helpers, not unmanaged runtime exports. Merely
// defining this type changes ILC's lazy-initialization policy, so the entire
// type is absent from every other profile. It has no bootstrap constructor.
internal static unsafe class ClassConstructorRunner
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static nint CheckStaticClassConstructionReturnNonGCStaticBase(
        StaticClassConstructionContext* context, nint nonGcStaticBase)
    {
        EnsureClassConstructorRun(context);
        OS.Runtime.RhpNewFastAllocation.ObserveCompilerNonGcBase((ulong)context, (ulong)nonGcStaticBase);
        return nonGcStaticBase;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object CheckStaticClassConstructionReturnGCStaticBase(
        StaticClassConstructionContext* context, object gcStaticBase)
    {
        EnsureClassConstructorRun(context);
        OS.Runtime.RhpNewFastAllocation.ObserveCompilerGcBase((ulong)context, gcStaticBase);
        return gcStaticBase;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EnsureClassConstructorRun(StaticClassConstructionContext* context) =>
        OS.Runtime.RhpNewFastAllocation.EnsureCompilerClassConstructor((ulong)context);
}
#endif
