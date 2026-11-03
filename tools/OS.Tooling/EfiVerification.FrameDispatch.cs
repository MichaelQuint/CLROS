using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
internal sealed partial class EfiVerification
{
    private void VerifyFrameDispatchBridge()
    {
        const string objectName = "CLROS.X64.FrameDispatch.obj";
        if (!profile.IsEnabled("M04FrameDispatch"))
        {
            Require(!response.Contains(objectName, StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(map, @"(?m)^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(?:CLROS_Frame_\S+|OS_OS_Runtime_RhpNewFastAllocation__Frame\S+|__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Frame\S+)\s+"), "frames_unrequested_native_input");
            return;
        }

        byte[] obj = NativeObject("frameDispatch", ".frames.obj", objectName, EfiContracts.FrameDispatchObjectBytes, EfiContracts.FrameDispatchObjectSha256, "frames");
        string[] names = ["RhpThrowEx", "CLROS_Frame_CatcherClauses", "CLROS_Frame_MiddleClauses", "CLROS_Frame_Select", "CLROS_Frame_ValidateCleanup", "CLROS_Frame_ValidateContinuation", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameCatcher", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameMiddle", "RhTypeCast_IsInstanceOfException"];
        ulong[] a = names.Select((n, i) => Address(n, i < 3 ? objectName : "OS.obj", "frames")).ToArray();
        Require(a[0] % 16 == 0 && a[1] == a[0] + 1328 && a[2] == a[0] + 1344, "frames_bridge_layout");
        int[] fields = [330, 609, 1105, 1331, 1347], targets = [3, 4, 5, 6, 7];
        byte[] text = BridgeText(a[0], 1352, obj, fields, "frames");
        for (int i = 0; i < fields.Length; i++)
            Rel32(text, fields[i], a[0], a[targets[i]], "frames_rel32_mismatch");
        foreach (int i in new[]
        {
            3,
            4,
            5,
            8
        }

        )
        {
            Require(!(a[i] >= a[0] && a[i] < a[0] + 1352), "frames_callback_alias");
            CodeAddress(a[i], "frames_callback");
        }

        ulong managed = Address("OS_System_Runtime_TypeCast__IsInstanceOfException", "OS.obj", "frames"), retained = Address("OS_OS_Runtime_RhpNewFastAllocation__MatchRetainedExceptionType", "OS.obj", "frames");
        Require(managed == a[8], "frames_predicate_export_alias");
        CodeAddress(retained, "frames_predicate_target");
        var predicate = image.Resolve(a[8] - image.ImageBase, 5, "frames_predicate_not_backed", "frames_predicate_ambiguous");
        byte[] exported = image.Copy(predicate.Offset, 5, "frames_predicate_truncated");
        Require(exported[0] == 0xe9, "frames_predicate_export_shape");
        Rel32(exported, 1, a[8], retained, "frames_predicate_export_call");
        ulong sibling = Address("??_7OS_OS_Runtime_SimpleCatchSibling@@6B@", "OS.obj", "frames") - image.ImageBase, baseType = Address("??_7OS_OS_Runtime_SimpleCatchBase@@6B@", "OS.obj", "frames") - image.ImageBase;
        foreach (ulong type in new[]
        {
            sibling,
            baseType
        }

        )
            image.Resolve(type, 24, "frames_type_not_backed", "frames_type_ambiguous").Section.RequireReadOnlyData("frames_type_permissions");
        for (int part = 0; part < 2; part++)
        {
            int size = part == 0 ? 4 : 15;
            var table = image.Resolve(a[7 - part] - image.ImageBase, (ulong)size, "frames_table_not_backed", "frames_table_ambiguous");
            table.Section.RequireReadOnlyData("frames_table_permissions");
            Require(EfiVerify.TestFrameDispatchEhTable(image.Copy(table.Offset, size, "frames_table_truncated"), part, sibling, baseType), "frames_clause_table_mismatch");
        }

        string[] methods = ["FrameThrower", "FrameMiddle", "FrameCatcher", "FrameCleanupEntered", "FrameMarkOuter", "FrameContinued", "FrameUnexpectedCatch", "FrameCatchEntered"];
        ulong[] m = methods.Select(n => Address("OS_OS_Runtime_RhpNewFastAllocation__" + n, "OS.obj", "frames")).ToArray();
        foreach (ulong method in m)
            CodeAddress(method, "frames_method");
        int[] lengths = [10, 72, 138];
        for (int part = 0; part < 3; part++)
        {
            ulong rva = m[part] - image.ImageBase;
            var code = image.Resolve(rva, (ulong)lengths[part], "frames_method_not_backed", "frames_method_ambiguous");
            code.Section.RequireCode("frames_method_permissions");
            byte[] compiled = image.Copy(code.Offset, lengths[part], "frames_method_truncated");
            Require(EfiVerify.TestFrameDispatchCode(compiled, part), "frames_method_bytes_mismatch");
            int[] callFields = part switch
            {
                0 => [5],
                1 => [21, 35, 62],
                _ => [32, 42, 60, 80, 111]
            };
            ulong[] callTargets = part switch
            {
                0 => [a[0]],
                1 => [m[0], m[3], m[3]],
                _ => [m[4], m[1], m[5], m[6], m[7]]
            };
            for (int i = 0; i < callFields.Length; i++)
                Rel32(compiled, callFields[i], m[part], callTargets[i], "frames_method_call_mismatch");
            uint[] starts = part switch
            {
                0 => [0],
                1 => [0, 49],
                _ => [0, 75, 96]
            }, ends = part switch
            {
                0 => [10],
                1 => [49, 72],
                _ => [75, 96, 138]
            };
            var rows = image.RuntimeFunctions.Where(f => f.Begin < rva + (ulong)lengths[part] && f.End > rva).ToArray();
            Require(rows.Length == starts.Length, "frames_runtime_rows");
            for (int i = 0; i < rows.Length; i++)
            {
                Require(rows[i].Begin == rva + starts[i] && rows[i].End == rva + ends[i], "frames_runtime_row_shape");
                int kind = part == 0 ? 0 : part == 1 ? 1 + i : 3 + i, size = kind == 1 ? 13 : kind == 3 ? 15 : 7;
                var unwind = image.Resolve(rows[i].Unwind, (ulong)size, "frames_unwind_not_backed", "frames_unwind_ambiguous");
                Require(EfiVerify.TestFrameDispatchUnwindBytes(image.Copy(unwind.Offset, size, "frames_unwind_truncated"), kind, a[part == 1 ? 7 : 6] - image.ImageBase), "frames_unwind_mismatch");
            }
        }

        ulong run = Address("OS_OS_Runtime_RhpNewFastAllocation__RunFrameDispatch", "OS.obj", "frames");
        var backing = image.Resolve(run - image.ImageBase + 1002, 60, "frames_predicate_calls_not_backed", "frames_predicate_calls_ambiguous");
        byte[] calls = image.Copy(backing.Offset, 60, "frames_predicate_calls_truncated");
        foreach (int at in new[]
        {
            0,
            18,
            37
        }

        )
            Require(calls.AsSpan(at, 7).SequenceEqual(new byte[] { 0x48, 0x8b, 0xc8, 0x48, 0x8b, 0xd3, 0xe8 }), "frames_predicate_argument_shape");
        Require(calls.AsSpan(51, 5).SequenceEqual(new byte[] { 0x33, 0xc9, 0x33, 0xd2, 0xe8 }), "frames_predicate_null_shape");
        foreach (int field in new[]
        {
            7,
            25,
            44,
            56
        }

        )
            Rel32(calls, field, run + 1002, a[8], "frames_predicate_import_call");
        emit($"EFI_ARTIFACT_FRAME_DISPATCH_THUNK bytes={EfiContracts.FrameDispatchObjectBytes} text=1352 callbackRel32=3 tableRel32=2 tableBytes=19 clauses=3 runtimeRows=6 predicateExportCalls=4 transferAccepted=false callbackStackAccepted=false compilerGeometryAccepted=true guestAccepted=false");
    }
}
