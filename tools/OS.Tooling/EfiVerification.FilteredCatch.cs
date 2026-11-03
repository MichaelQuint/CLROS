using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    private void VerifyFilteredCatchBridge()
    {
        string[] names = ["RhpThrowEx", "CLROS_Filter_Clauses", "CLROS_Filter_Select", "CLROS_Filter_ValidateContinuation", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher"];
        const string bridgeName = "CLROS.X64.FilteredCatch.obj", invokeName = "CLROS.X64.FilterInvoke.obj";
        if (!profile.IsEnabled("M04FilteredCatch"))
        {
            Require(!response.Contains(bridgeName, StringComparison.OrdinalIgnoreCase) && !response.Contains(invokeName, StringComparison.OrdinalIgnoreCase), "filters_unrequested_native_object");
            foreach (string name in names.Skip(1).Append("CLROS_Filter_Invoke"))
                Require(Symbols(map, name).Count == 0, "filters_unrequested_native_symbol", name);
            return;
        }

        byte[] bridge = NativeObject("filteredCatch", ".filter-catch.obj", bridgeName, EfiContracts.FilteredCatchObjectBytes, EfiContracts.FilteredCatchObjectSha256, "filters");
        ulong[] addresses = names.Select((name, index) => Address(name, index < 2 ? bridgeName : "OS.obj", "filters")).ToArray();
        Require(addresses[0] % 16 == 0 && addresses[1] == addresses[0] + 1024, "filters_bridge_layout");
        byte[] text = BridgeText(addresses[0], 1032, bridge, [330, 811, 1027], "filters");
        int[] fields = [330, 811, 1027], targets = [2, 3, 4];
        for (int index = 0; index < fields.Length; index++)
            Rel32(text, fields[index], addresses[0], addresses[targets[index]], "filters_rel32_mismatch");
        foreach (int index in new[]
        {
            2,
            3
        }

        )
            CodeAddress(addresses[index], "filters_callback");
        byte[] invoke = NativeObject("filterInvoke", ".filter-invoke.obj", invokeName, EfiContracts.FilterInvokeObjectBytes, EfiContracts.FilterInvokeObjectSha256, "filter_invoke");
        ulong entry = Address("CLROS_Filter_Invoke", invokeName, "filter_invoke");
        CodeAddress(entry, "filter_invoke_entry");
        _ = BridgeText(entry, 669, invoke, [], "filter_invoke");
        VerifyFilteredCatchGeometry(addresses);
        emit("EFI_ARTIFACT_FILTERED_CATCH_THUNKS bridgeText=1032 bridgeFrame=840 invokeText=669 invokeFrame=328 geometryReviewed=true transferAccepted=false callbackStackAccepted=false guestAccepted=false");
    }

    private void VerifyFilteredCatchGeometry(ulong[] bridge)
    {
        ulong table = bridge[4] - image.ImageBase;
        var eh = image.Resolve(table, 11, "filters_table_not_backed", "filters_table_ambiguous");
        eh.Section.RequireReadOnlyData("filters_table_permissions");
        Require(EfiVerify.TestFilteredCatchEhTable(image.Copy(eh.Offset, 11, "filters_table_truncated")), "filters_table_mismatch");
        for (int part = 0; part < EfiVerify.FilteredCatchShapes.Length; part++)
        {
            var shape = EfiVerify.FilteredCatchShapes[part];
            ulong method = Address(shape.Name, "OS.obj", "filters"), rva = method - image.ImageBase;
            int length = shape.Hex.Length / 2;
            var backing = image.Resolve(rva, (ulong)length, "filters_method_not_backed", "filters_method_ambiguous");
            backing.Section.RequireCode("filters_method_permissions");
            byte[] compiled = image.Copy(backing.Offset, length, "filters_method_truncated");
            Require(EfiVerify.TestFilteredCatchCode(compiled, part), "filters_method_bytes_mismatch", shape.Name);
            if (part == 1)
            {
                string source = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/FilteredCatchProbe.cs"));
                var offsets = System.Text.RegularExpressions.Regex.Matches(source, @"private const uint FilterReturnOffset = ([0-9]+), FilterContinuationOffset = ([0-9]+);");
                Require(offsets.Count == 1, "filters_offset_source_shape");
                Require(EfiVerify.TestFilteredCatchOffsets(compiled, uint.Parse(offsets[0].Groups[1].Value), uint.Parse(offsets[0].Groups[2].Value)), "filters_compiled_return_offsets");
            }

            foreach (var field in shape.Fields)
            {
                ulong target = Address(field.Target, field.Owner, "filters");
                Require(field.Addend >= 0 && target <= ulong.MaxValue - (ulong)field.Addend, "filters_relocation_addend");
                Rel32(compiled, field.Field, method, target + (ulong)field.Addend, "filters_method_target_mismatch");
            }

            if (part > 1)
                continue; // Complete callback code/stack is reviewed separately; no general callback unwinding claim.
            uint[] begins = part == 0 ? [0] : [0, 60, 127, 178, 245], ends = part == 0 ? [10] : [60, 127, 178, 245, 295];
            var rows = image.RuntimeFunctions.Where(row => row.Begin < rva + (ulong)length && row.End > rva).OrderBy(row => row.Begin).ToArray();
            Require(rows.Length == begins.Length, "filters_runtime_rows", shape.Name);
            for (int index = 0; index < rows.Length; index++)
            {
                Require(rows[index].Begin == rva + begins[index] && rows[index].End == rva + ends[index], "filters_runtime_row_shape", shape.Name);
                int kind = part == 0 ? 0 : index == 0 ? 1 : index is 1 or 3 ? 2 : 3, size = kind == 1 ? 13 : 7;
                var unwind = image.Resolve(rows[index].Unwind, (ulong)size, "filters_unwind_not_backed", "filters_unwind_ambiguous");
                unwind.Section.RequireReadOnlyData("filters_unwind_permissions");
                Require(EfiVerify.TestFilteredCatchUnwind(image.Copy(unwind.Offset, size, "filters_unwind_truncated"), kind, table), "filters_unwind_mismatch");
            }
        }

        ulong exported = Address("RhTypeCast_IsInstanceOfClass", "OS.obj", "filters"), managed = Address("OS_System_Runtime_TypeCast__IsInstanceOfClass", "OS.obj", "filters");
        Require(exported == managed, "filters_class_predicate_export_alias");
    }
}
