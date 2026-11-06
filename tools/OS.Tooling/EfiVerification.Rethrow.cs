using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    // Exact retained COFF text, relocated calls, compiled clauses and unwind rows.
    // Callback stack review and guest execution remain separate admission gates.
    private void VerifyRethrowBridges()
    {
        if (!profile.IsEnabled("M04Rethrow"))
        {
            Require(!Regex.IsMatch(response, @"(?i)CLROS\.X64\.Rethrow(?:Inner|Outer)\.obj") && !Regex.IsMatch(map, @"(?m)^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(?:RhpRethrow|CLROS_Rethrow_\S+|__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Rethrow\S+)\s+"), "rethrow_unrequested_native_input");
            return;
        }

        foreach (bool inner in new[]
        {
            true,
            false
        }

        )
        {
            string role = inner ? "Inner" : "Outer", objectName = "CLROS.X64.Rethrow" + role + ".obj";
            int size = inner ? EfiContracts.RethrowInnerObjectBytes : EfiContracts.RethrowOuterObjectBytes;
            int textSize = inner ? 1336 : 1048, getterOffset = inner ? 1328 : 1040;
            byte[] obj = NativeObject("rethrow" + role, ".rethrow-" + role.ToLowerInvariant() + ".obj", objectName, size, inner ? EfiContracts.RethrowInnerObjectSha256 : EfiContracts.RethrowOuterObjectSha256, "rethrow");
            List<string> names = inner ? ["RhpThrowEx", "CLROS_Rethrow_InnerClauses", "CLROS_Rethrow_SelectInner", "CLROS_Rethrow_RejectInnerReturn", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__RethrowInner", "CLROS_Rethrow_ValidateFinally"] : ["RhpRethrow", "CLROS_Rethrow_OuterClauses", "CLROS_Rethrow_SelectOuter", "CLROS_Rethrow_ValidateOuter", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__RethrowOuter"];
            int methodIndex = names.Count;
            names.AddRange(["OS_OS_Runtime_RhpNewFastAllocation__Rethrow" + role, "??_7OS_OS_Runtime_SimpleCatchBase@@6B@"]);
            ulong[] a = names.Select((name, i) => Address(name, i < 2 ? objectName : "OS.obj", "rethrow")).ToArray();
            Require(a[0] % 16 == 0 && a[1] == a[0] + (ulong)getterOffset, "rethrow_bridge_layout", role);
            int[] fields = inner ? [330, 609, 1105, 1331] : [332, 813, 1043], targets = inner ? [2, 5, 3, 4] : [2, 3, 4];
            byte[] text = BridgeText(a[0], textSize, obj, fields, "rethrow");
            for (int i = 0; i < fields.Length; i++)
                Rel32(text, fields[i], a[0], a[targets[i]], "rethrow_rel32_mismatch");
            foreach (int i in inner ? new[]
            {
                2,
                3,
                5
            }

            : new[]
            {
                2,
                3
            }

            )
            {
                Require(!(a[i] >= a[0] && a[i] < a[0] + (ulong)textSize), "rethrow_callback_alias");
                CodeAddress(a[i], "rethrow_callback");
            }

            ulong methodRva = a[methodIndex] - image.ImageBase, typeRva = a[methodIndex + 1] - image.ImageBase, tableRva = a[4] - image.ImageBase;
            int tableSize = inner ? 11 : 8, codeSize = inner ? 88 : 89;
            var table = image.Resolve(tableRva, (ulong)tableSize, "rethrow_table_not_backed", "rethrow_table_ambiguous");
            table.Section.RequireReadOnlyData("rethrow_metadata_permissions");
            image.Resolve(typeRva, 24, "rethrow_type_not_backed", "rethrow_type_ambiguous").Section.RequireReadOnlyData("rethrow_metadata_permissions");
            Require(EfiVerify.TestRethrowEhTable(image.Copy(table.Offset, tableSize, "rethrow_table_truncated"), inner, typeRva), "rethrow_clause_table_mismatch", role);
            image.Resolve(methodRva, (ulong)codeSize, "rethrow_method_not_backed", "rethrow_method_ambiguous").Section.RequireCode("rethrow_method_permissions");
            var rows = image.RuntimeFunctions.Where(f => f.Begin < methodRva + (ulong)codeSize && f.End > methodRva).ToArray();
            uint[] starts = inner ? [0, 46, 69] : [0, 51], ends = inner ? [46, 69, 88] : [51, 89];
            Require(rows.Length == starts.Length, "rethrow_runtime_rows", role);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                Require(row.Begin == methodRva + starts[i] && row.End == methodRva + ends[i], "rethrow_runtime_row_shape", role);
                int length = i > 0 ? 7 : inner ? 13 : 15;
                var unwind = image.Resolve(row.Unwind, (ulong)length, "rethrow_unwind_not_backed", "rethrow_unwind_ambiguous");
                Require(EfiVerify.TestRethrowUnwindBytes(image.Copy(unwind.Offset, length, "rethrow_unwind_truncated"), inner, i == 0, tableRva), "rethrow_unwind_mismatch", role);
            }

            emit($"EFI_ARTIFACT_RETHROW_THUNK role={role.ToLowerInvariant()} bytes={size} text={textSize} tableBytes={tableSize} runtimeRows={rows.Length} transferAccepted=false callbackStackAccepted=false");
        }
    }
}
