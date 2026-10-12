#if NOSTDLIB_EFI && M04_NORMAL_FINALLY_PROBE
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{
    // Inactive compiler fixture. step1190 records original ILC10.0.12
    // measurement with temporary frame-profile anchoring, now removed.
    // Dedicated default-off wiring and native admission remain #674/#675/#676
    // work; no boot or callback invokes this compiler-only address getter.
    private static ulong s_normalFinallyEntries, s_normalCatchEntries, s_normalContinuations;
    private static ulong s_normalWitness;

    [System.Runtime.RuntimeExport("CLROS_NormalFinally_CompilerAddress")]
    private static ulong NormalCompilerAddress() =>
        (ulong)(delegate* managed<SimpleCatchBase, ulong>)&NormalCatcher;

    // CLR MethodImpl NoOptimization=0x40; the minimal enum has no named member.
    // Without it ILC clones the normal finally into this root, leaving no
    // normal-call funclet frame for RecoverNormalFinallyParent to recover.
    [MethodImpl(MethodImplOptions.NoInlining | (MethodImplOptions)0x40)]
    private static ulong NormalCatcher(SimpleCatchBase value)
    {
        ulong stamp = 0, result = 0;
        try
        {
            try { stamp = NormalBody(value); }
            finally
            {
                NormalFinallyEntered(value, ref stamp);
                NormalThrower(value);
            }
        }
        catch (SimpleCatchBase caught)
        {
            NormalCatchEntered(caught, stamp);
            result = caught.Payload + 1;
        }
        NormalContinued(value, result, stamp);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong NormalBody(SimpleCatchBase value) => value.Payload + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalThrower(System.Exception value) { throw value; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalFinallyEntered(SimpleCatchBase value, ref ulong stamp)
    {
        if (s_normalFinallyEntries != 0 || stamp != value.Payload + 1) RejectEh(61);
        stamp = value.Payload + 2;
        s_normalFinallyEntries = 1; s_normalWitness = stamp;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalCatchEntered(SimpleCatchBase value, ulong stamp)
    {
        if (s_normalFinallyEntries != 1 || s_normalCatchEntries != 0 || stamp != value.Payload + 2 ||
            s_normalWitness != stamp) RejectEh(62);
        s_normalCatchEntries = 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalContinued(SimpleCatchBase value, ulong result, ulong stamp)
    {
        if (s_normalFinallyEntries != 1 || s_normalCatchEntries != 1 || s_normalContinuations != 0 ||
            result != value.Payload + 1 || stamp != value.Payload + 2 || s_normalWitness != stamp) RejectEh(63);
        s_normalContinuations = 1;
    }
}
#endif
