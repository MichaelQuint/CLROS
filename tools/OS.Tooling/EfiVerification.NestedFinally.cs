using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
internal sealed partial class EfiVerification
{
    // Exact retained bridge and compiler method/table/funclet contributions.
    // Callback stack admission and actual guest execution remain separate gates.
    private void VerifyNestedFinallyBridge()
    {
        const string objectName = "CLROS.X64.NestedFinallyCatch.obj";
        if (!profile.IsEnabled("M04NestedFinallyCatch"))
        {
            Require(!response.Contains(objectName, StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(map, @"(?m)^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(?:CLROS_Nested_\S+|OS_OS_Runtime_RhpNewFastAllocation__Nested\S+|__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NestedCatcher)\s+") && (profile.IsEnabled("M04UnhandledException") || profile.IsEnabled("M04FrameDispatch") || Symbols(map, "??_7OS_OS_Runtime_SimpleCatchSibling@@6B@").Count == 0), "nested_finally_unrequested_native_input");
            return;
        }

        byte[] obj = NativeObject("nestedFinally", ".nested-finally.obj", objectName, EfiContracts.NestedFinallyObjectBytes, EfiContracts.NestedFinallyObjectSha256, "nested_finally");
        string[] names = ["RhpThrowEx", "CLROS_Nested_Clauses", "CLROS_Nested_Select", "CLROS_Nested_ValidateCleanup", "CLROS_Nested_ValidateContinuation", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NestedCatcher"];
        ulong[] a = names.Select((n, i) => Address(n, i < 2 ? objectName : "OS.obj", "nested_finally")).ToArray();
        Require(a[0] % 16 == 0 && a[1] == a[0] + 1616, "nested_finally_bridge_layout");
        int[] fields = [330, 609, 903, 1399, 1619], targets = [2, 3, 3, 4, 5];
        byte[] text = BridgeText(a[0], 1624, obj, fields, "nested_finally");
        for (int i = 0; i < fields.Length; i++)
            Rel32(text, fields[i], a[0], a[targets[i]], "nested_finally_rel32_mismatch");
        foreach (int i in new[]
        {
            2,
            3,
            4
        }

        )
        {
            Require(!(a[i] >= a[0] && a[i] < a[0] + 1624), "nested_finally_callback_alias");
            CodeAddress(a[i], "nested_finally_callback");
        }

        ulong tableRva = a[5] - image.ImageBase;
        var table = image.Resolve(tableRva, 26, "nested_finally_table_not_backed", "nested_finally_table_ambiguous");
        table.Section.RequireReadOnlyData("nested_finally_table_permissions");
        string[] methods = ["NestedCatcher", "NestedThrower", "NestedCleanupEntered", "NestedContinued", "NestedUnexpectedCatch", "NestedCatchEntered"];
        ulong[] m = methods.Select(n => Address("OS_OS_Runtime_RhpNewFastAllocation__" + n, "OS.obj", "nested_finally") - image.ImageBase).ToArray();
        foreach (ulong method in m)
            CodeAddress(method + image.ImageBase, "nested_finally_method");
        ulong sibling = Address("??_7OS_OS_Runtime_SimpleCatchSibling@@6B@", "OS.obj", "nested_finally") - image.ImageBase, baseType = Address("??_7OS_OS_Runtime_SimpleCatchBase@@6B@", "OS.obj", "nested_finally") - image.ImageBase;
        foreach (ulong type in new[]
        {
            sibling,
            baseType
        }

        )
            image.Resolve(type, 24, "nested_finally_type_not_backed", "nested_finally_type_ambiguous").Section.RequireReadOnlyData("nested_finally_type_permissions");
        Require(EfiVerify.TestNestedFinallyEhTable(image.Copy(table.Offset, 26, "nested_finally_table_truncated"), sibling, baseType), "nested_finally_clause_table_mismatch");
        var code = image.Resolve(m[0], 222, "nested_finally_method_not_backed", "nested_finally_method_ambiguous");
        code.Section.RequireCode("nested_finally_method_permissions");
        byte[] compiled = image.Copy(code.Offset, 222, "nested_finally_method_truncated");
        Require(EfiVerify.TestNestedFinallyCode(compiled), "nested_finally_method_bytes_mismatch");
        int[] calls = [28, 48, 68, 86, 120, 149, 164, 195], callees = [1, 2, 2, 3, 2, 2, 4, 5];
        for (int i = 0; i < calls.Length; i++)
            Rel32(compiled, calls[i], m[0] + image.ImageBase, m[callees[i]] + image.ImageBase, "nested_finally_method_call_mismatch");
        var throwing = image.Resolve(m[1], 10, "nested_finally_thrower_not_backed", "nested_finally_thrower_ambiguous");
        throwing.Section.RequireCode("nested_finally_thrower_permissions");
        byte[] throwBytes = image.Copy(throwing.Offset, 10, "nested_finally_thrower_truncated"), throwShape = [..throwBytes];
        throwShape.AsSpan(5, 4).Clear();
        Require(throwShape.AsSpan().SequenceEqual(new byte[] { 0x48, 0x83, 0xec, 0x28, 0xe8, 0, 0, 0, 0, 0xcc }), "nested_finally_thrower_bytes_mismatch");
        Rel32(throwBytes, 5, m[1] + image.ImageBase, a[0], "nested_finally_thrower_call_mismatch");
        var rows = image.RuntimeFunctions.Where(f => f.Begin < m[0] + 222 && f.End > m[0]).ToArray();
        uint[] starts = [0, 101, 130, 159, 180], ends = [101, 130, 159, 180, 222];
        Require(rows.Length == 5, "nested_finally_runtime_rows");
        for (int i = 0; i < rows.Length; i++)
        {
            Require(rows[i].Begin == m[0] + starts[i] && rows[i].End == m[0] + ends[i], "nested_finally_runtime_row_shape");
            int length = i == 0 ? 15 : 7;
            var unwind = image.Resolve(rows[i].Unwind, (ulong)length, "nested_finally_unwind_not_backed", "nested_finally_unwind_ambiguous");
            Require(EfiVerify.TestNestedFinallyUnwindBytes(image.Copy(unwind.Offset, length, "nested_finally_unwind_truncated"), i == 0, tableRva), "nested_finally_unwind_mismatch");
        }

        emit($"EFI_ARTIFACT_NESTED_FINALLY_THUNK bytes={EfiContracts.NestedFinallyObjectBytes} text=1624 callbackRel32=4 tableRel32=1 tableBytes=26 clauses=4 runtimeRows=5 transferAccepted=false callbackStackAccepted=false compilerGeometryAccepted=true");
    }
}
