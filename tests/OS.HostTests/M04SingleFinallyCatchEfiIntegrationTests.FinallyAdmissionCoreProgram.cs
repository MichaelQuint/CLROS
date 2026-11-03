#if SINGLE_FINALLY_CORE_TEST
using System.Reflection;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe class FinallyAdmissionCoreProgram
{
    private static void Need(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
    private static int Main()
    {
        Run("complete saved contexts", Contexts);
        Run("measured throwing and parent frames", Frames);
        Run("control fields and allowed status flags", Controls);
        Run("measured nested clause pair", Clauses);
        Console.WriteLine("FINALLY_ADMISSION_CORE_PASS"); return 0;
    }
    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START finally.admission " + name);
        try { test(); Console.WriteLine("TEST_PASS finally.admission " + name); }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL finally.admission " + name + " " + ex); throw; }
    }
    private static void Contexts()
    {
        Need(sizeof(X64UnwindContext) == 240, "actual complete context ABI");
        X64UnwindContext saved = default;
        for (int i = 0; i < 30; i++) ((ulong*)&saved)[i] = 0x1000UL + (ulong)(17 * i);
        X64UnwindContext actual = saved;
        Need(SingleFinallyAdmission.ContextsMatch(&actual, &saved, false), "exact transfer context");
        for (int i = 0; i < 30; i++)
        {
            actual = saved; ((ulong*)&actual)[i] ^= 1;
            Need(!SingleFinallyAdmission.ContextsMatch(&actual, &saved, false), "changed transfer word " + i);
            Need(SingleFinallyAdmission.ContextsMatch(&actual, &saved, true) == (i is 0 or 2), "post-catch word " + i);
        }
        Need(!SingleFinallyAdmission.ContextsMatch(null, &saved, false) &&
            !SingleFinallyAdmission.ContextsMatch(&actual, null, true), "null contexts rejected");
    }
    private static void Frames()
    {
        Need(SingleFinallyAdmission.ParentFrameMatches(0x10000, 0x10030, 0x10070), "40-byte thrower plus return; parent RBP +64");
        foreach (ulong delta in new ulong[] { 0, 8, 16, 40, 56 })
            Need(!SingleFinallyAdmission.ParentFrameMatches(0x10000, 0x10000 + delta, 0x10040 + delta), "wrong throwing-frame extent");
        Need(!SingleFinallyAdmission.ParentFrameMatches(0x10000, 0x10030, 0x10060), "wrong parent RBP");
        Need(!SingleFinallyAdmission.ParentFrameMatches(0, 48, 112), "null capture");
        Need(!SingleFinallyAdmission.ParentFrameMatches(ulong.MaxValue - 47, 0, 64), "wrapped caller");
        Need(!SingleFinallyAdmission.ParentFrameMatches(ulong.MaxValue - 111, ulong.MaxValue - 63, 0), "wrapped parent RBP");
    }
    private static SingleFinallyAdmission.Control Control(ulong flags = 0x202, ulong cr4 = 0x620,
        uint mxcsr = 0x1F80, ushort x87 = 0x37F) => new(flags, cr4, mxcsr, x87);
    private static void Controls()
    {
        var entry = Control();
        Need(SingleFinallyAdmission.ControlsMatch(entry, entry, entry), "unchanged controls");
        Need(SingleFinallyAdmission.ControlsMatch(entry, Control(flags: 0x203, mxcsr: 0x1FBF), entry),
            "arithmetic flags and MXCSR status bits may differ");
        foreach (var bad in new[] { Control(flags: 0x602), Control(cr4: 0x800620), Control(cr4: 0x621),
            Control(mxcsr: 0x11F80), Control(mxcsr: 0x1FC0), Control(x87: 0x27F) })
            Need(!SingleFinallyAdmission.ControlsMatch(entry, bad, entry), "invalid or changed post controls");
        foreach (var changed in new[] { Control(flags: 0x203), Control(cr4: 0x621), Control(mxcsr: 0x1F81), Control(x87: 0x27F) })
            Need(!SingleFinallyAdmission.ControlsMatch(changed, changed, entry), "entry snapshot mutation");
        var df = Control(flags: 0x602); var cet = Control(cr4: 0x800620);
        Need(!SingleFinallyAdmission.ControlsMatch(df, df, df) && !SingleFinallyAdmission.ControlsMatch(cet, cet, cet),
            "forbidden entry state even when snapshots agree");
    }
    private static readonly ConstructorInfo ClauseConstructor = typeof(NativeAotEhClauseResult).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        [typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(NativeAotEhClauseKind),
         typeof(ulong), typeof(ulong), typeof(ulong), typeof(bool)], null)!;
    private static NativeAotEhClauseResult Clause(bool cleanup, uint? index = null, uint count = 2,
        uint start = 27, uint? end = null, NativeAotEhClauseKind? kind = null, ulong? handler = null,
        ulong filter = 0, ulong? type = null, bool boundary = false) =>
        (NativeAotEhClauseResult)ClauseConstructor.Invoke([index ?? (cleanup ? 0U : 1U), count, start,
            end ?? (cleanup ? 33U : 47U), kind ?? (cleanup ? NativeAotEhClauseKind.FaultOrFinally : NativeAotEhClauseKind.Typed),
            handler ?? (cleanup ? 0x100047UL : 0x10005EUL), filter, type ?? (cleanup ? 0UL : 0x200000UL), boundary]);
    private static void Clauses()
    {
        // Model actual parser results from the measured 11-byte table. This
        // exercises admission policy, not module authentication or the parser.
        var cleanup = Clause(true); var caught = Clause(false);
        bool Catch(NativeAotEhClauseResult c, ulong root = 0x100000, ulong type = 0x200000, uint pc = 31) =>
            SingleFinallyAdmission.AcceptCatch(c, root, type, pc);
        bool Pair(NativeAotEhClauseResult c, NativeAotEhClauseResult handler) =>
            SingleFinallyAdmission.AcceptCleanup(c, handler, 0x100000, 0x200000, 31);
        Need(Catch(caught) && Pair(cleanup, caught), "exact containing typed catch then inner cleanup");
        foreach (var bad in new[] { default, Clause(false, index: 0), Clause(false, count: 1),
            Clause(false, start: 26), Clause(false, end: 46), Clause(false, kind: NativeAotEhClauseKind.Filter),
            Clause(false, handler: 0x10005F), Clause(false, filter: 1), Clause(false, type: 0), Clause(false, boundary: true) })
            Need(!Catch(bad) && !Pair(cleanup, bad), "invalid outer record cannot admit cleanup");
        foreach (var bad in new[] { default, Clause(true, index: 1), Clause(true, count: 3), Clause(true, start: 28),
            Clause(true, end: 47), Clause(true, kind: NativeAotEhClauseKind.Typed), Clause(true, handler: 0x100048),
            Clause(true, filter: 1), Clause(true, type: 0x200000), Clause(true, boundary: true) })
            Need(!Pair(bad, caught), "invalid cleanup/order/extent/target");
        foreach (uint pc in new uint[] { 0, 27, 30, 32, 33, 47, uint.MaxValue }) Need(!Catch(caught, pc: pc), "wrong protected PC");
        Need(!Catch(caught, root: 0) && !Catch(caught, root: ulong.MaxValue - 50) && !Catch(caught, type: 0),
            "null authority or wrapped contribution rejected");
    }
}
#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;
#endif
