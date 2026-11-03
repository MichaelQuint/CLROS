using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    private void VerifyNormalFinallyBridge()
    {
        const string objectName = "CLROS.X64.NormalFinally.obj";
        string[] names = ["RhpThrowEx", "CLROS_NormalFinally_Clauses", "CLROS_NormalFinally_Select", "CLROS_NormalFinally_ValidateContinuation", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher"];
        if (!profile.IsEnabled("M04NormalFinally"))
        {
            Require(!response.Contains(objectName, StringComparison.OrdinalIgnoreCase), "normal_finally_unrequested_native_object");
            foreach (string name in names.Skip(1))
                Require(Symbols(map, name).Count == 0, "normal_finally_unrequested_native_symbol", name);
            return;
        }

        byte[] obj = NativeObject("normalFinally", ".normal-finally.obj", objectName, EfiContracts.NormalFinallyObjectBytes, EfiContracts.NormalFinallyObjectSha256, "normal_finally");
        ulong[] a = names.Select((name, index) => Address(name, index < 2 ? objectName : "OS.obj", "normal_finally")).ToArray();
        Require(a[0] % 16 == 0 && a[1] == a[0] + 1024, "normal_finally_bridge_layout");
        byte[] text = BridgeText(a[0], 1032, obj, [330, 811, 1027], "normal_finally");
        Rel32(text, 330, a[0], a[2], "normal_finally_selector_relocation");
        Rel32(text, 811, a[0], a[3], "normal_finally_continuation_relocation");
        Rel32(text, 1027, a[0], a[4], "normal_finally_table_relocation");
        foreach (int index in new[]
        {
            2,
            3
        }

        )
        {
            CodeAddress(a[index], "normal_finally_callback");
            Require(!(a[index] >= a[0] && a[index] < a[0] + 1032), "normal_finally_callback_alias");
        }

        string prefix = "OS_OS_Runtime_RhpNewFastAllocation__";
        string[] methods = ["NormalCatcher", "NormalThrower", "NormalBody", "NormalFinallyEntered", "NormalCatchEntered", "NormalContinued"];
        ulong[] m = methods.Select(name => Address(prefix + name, "OS.obj", "normal_finally")).ToArray();
        foreach (ulong method in m)
            CodeAddress(method, "normal_finally_method");
        ulong type = Address("??_7OS_OS_Runtime_SimpleCatchBase@@6B@", "OS.obj", "normal_finally") - image.ImageBase;
        image.Resolve(type, 24, "normal_finally_type_not_backed", "normal_finally_type_ambiguous").Section.RequireReadOnlyData("normal_finally_type_permissions");
        ulong tableRva = a[4] - image.ImageBase;
        var table = image.Resolve(tableRva, 11, "normal_finally_table_not_backed", "normal_finally_table_ambiguous");
        table.Section.RequireReadOnlyData("normal_finally_table_permissions");
        Require(EfiVerify.TestNormalFinallyEhTable(image.Copy(table.Offset, 11, "normal_finally_table_truncated"), type), "normal_finally_clause_table_mismatch");
        for (int part = 0; part < 2; part++)
        {
            ulong rva = m[part] - image.ImageBase;
            int length = part == 0 ? 173 : 10;
            var backing = image.Resolve(rva, (uint)length, "normal_finally_method_not_backed", "normal_finally_method_ambiguous");
            backing.Section.RequireCode("normal_finally_method_permissions");
            byte[] bytes = image.Copy(backing.Offset, length, "normal_finally_method_truncated");
            Require(EfiVerify.TestNormalFinallyCode(bytes, part == 0), "normal_finally_method_bytes_mismatch");
            int[] fields = part == 0 ? [48, 75, 102, 111, 138] : [5];
            ulong[] targets = part == 0 ? [m[2], m[5], m[3], m[1], m[4]] : [a[0]];
            for (int i = 0; i < fields.Length; i++)
                Rel32(bytes, fields[i], m[part], targets[i], "normal_finally_method_call_mismatch");
            var rows = image.RuntimeFunctions.Where(f => f.Begin < rva + (uint)length && f.End > rva).ToArray();
            Require(rows.Length == (part == 0 ? 3 : 1), "normal_finally_runtime_rows");
            uint[] starts = part == 0 ? [0, 89, 121] : [0], ends = part == 0 ? [89, 121, 173] : [10];
            for (int i = 0; i < rows.Length; i++)
            {
                Require(rows[i].Begin == rva + starts[i] && rows[i].End == rva + ends[i], "normal_finally_runtime_row_shape");
                int kind = part == 0 ? (i == 0 ? 0 : 1) : 2, size = kind == 0 ? 13 : 7;
                var unwind = image.Resolve(rows[i].Unwind, (uint)size, "normal_finally_unwind_not_backed", "normal_finally_unwind_ambiguous");
                unwind.Section.RequireReadOnlyData("normal_finally_unwind_permissions");
                Require(EfiVerify.TestNormalFinallyUnwind(image.Copy(unwind.Offset, size, "normal_finally_unwind_truncated"), kind, tableRva), "normal_finally_unwind_mismatch");
            }
        }

        ulong predicate = Address("RhTypeCast_IsInstanceOfException", "OS.obj", "normal_finally"), managed = Address("OS_System_Runtime_TypeCast__IsInstanceOfException", "OS.obj", "normal_finally"), retained = Address(prefix + "MatchRetainedExceptionType", "OS.obj", "normal_finally");
        Require(managed == predicate, "normal_finally_predicate_export_alias");
        CodeAddress(retained, "normal_finally_predicate_target");
        var p = image.Resolve(predicate - image.ImageBase, 5, "normal_finally_predicate_not_backed", "normal_finally_predicate_ambiguous");
        byte[] exported = image.Copy(p.Offset, 5, "normal_finally_predicate_truncated");
        Require(exported[0] == 0xe9, "normal_finally_predicate_export_shape");
        Rel32(exported, 1, predicate, retained, "normal_finally_predicate_export_call");
        // This verifier admits the exact instructions and original joins.
        // Separate current-image callback/stack/lifetime review precedes every
        // guest run; a verified artifact alone is not that review or boot proof.
        emit($"EFI_ARTIFACT_NORMAL_FINALLY_THUNK bytes={EfiContracts.NormalFinallyObjectBytes} text=1032 clauses=2 runtimeRows=3 normalCall=56 parentReturn=61 continuation=62 originalCatalogJoined=true transferAccepted=true callbackStackAccepted=false");
    }
}
