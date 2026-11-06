using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    private void VerifyConstructorFailureBridge()
    {
        string[] names = ["RhpThrowEx", "CLROS_CctorFailure_InvokeClauses", "CLROS_CctorFailure_OuterClauses", "CLROS_CctorFailure_Select", "CLROS_CctorFailure_ValidateContinuation", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter"];
        const string objectName = "CLROS.X64.ConstructorFailure.obj";
        if (!profile.IsEnabled("M04ConstructorFailure"))
        {
            Require(!response.Contains(objectName, StringComparison.OrdinalIgnoreCase), "constructor_failure_unrequested_native_object");
            foreach (string name in names.Skip(1))
                Require(Symbols(map, name).Count == 0, "constructor_failure_unrequested_native_symbol", name);
            return;
        }

        byte[] obj = NativeObject("constructorFailure", ".ctor-failure.obj", objectName, EfiContracts.ConstructorFailureObjectBytes, EfiContracts.ConstructorFailureObjectSha256, "constructor_failure");
        ulong[] addresses = names.Select((name, index) => Address(name, index < 3 ? objectName : "OS.obj", "constructor_failure")).ToArray();
        Require(addresses[0] % 16 == 0 && addresses[1] == addresses[0] + 1024 && addresses[2] == addresses[0] + 1040, "constructor_failure_bridge_layout");
        byte[] text = BridgeText(addresses[0], 1048, obj, [330, 811, 1027, 1043], "constructor_failure");
        int[] fields = [330, 811, 1027, 1043], targets = [3, 4, 5, 6];
        for (int index = 0; index < fields.Length; index++)
            Rel32(text, fields[index], addresses[0], addresses[targets[index]], "constructor_failure_rel32_mismatch");
        foreach (int index in new[]
        {
            3,
            4
        }

        )
            CodeAddress(addresses[index], "constructor_failure_callback");
        VerifyConstructorFailureGeometry(addresses);
        emit($"EFI_ARTIFACT_CONSTRUCTOR_FAILURE_THUNK bytes={EfiContracts.ConstructorFailureObjectBytes} text=1048 frame=840 callbacks=2 tables=2 geometryReviewed=true transferAccepted=false callbackStackAccepted=false guestAccepted=false");
        // Independent first-party stack review includes both live bridges and
        // the reference-store callback: 12,216 bytes <= 16,400. Firmware
        // diagnostic internals and actual transfers require guest evidence.
    }

    private void VerifyConstructorFailureGeometry(ulong[] bridge)
    {
        ulong nonGc = Address("?__NONGCSTATICS@OS_OS_Runtime_ConstructorFailureFixture@@", "OS.obj", "constructor_failure"), constructor = Address("OS_OS_Runtime_ConstructorFailureFixture___cctor", "OS.obj", "constructor_failure");
        Require(nonGc >= image.ImageBase + 8 && (nonGc & 7) == 0, "constructor_failure_context_address");
        var context = image.Resolve(nonGc - image.ImageBase - 8, 16, "constructor_failure_context_not_backed", "constructor_failure_context_ambiguous");
        Require((context.Section.Characteristics & 0xc0000040) == 0xc0000040 && (context.Section.Characteristics & 0x22000020) == 0, "constructor_failure_context_permissions");
        byte[] initial = image.Copy(context.Offset, 16, "constructor_failure_context_truncated");
        Require(BinaryPrimitives.ReadUInt64LittleEndian(initial) == constructor && BinaryPrimitives.ReadUInt64LittleEndian(initial.AsSpan(8)) == 0, "constructor_failure_context_initializer_mismatch");
        ulong exception = Address("??_7OS_System_Exception@@6B@", "OS.obj", "constructor_failure") - image.ImageBase, wrapper = Address("??_7OS_System_TypeInitializationException@@6B@", "OS.obj", "constructor_failure") - image.ImageBase;
        for (int part = 0; part < 2; part++)
        {
            ulong table = bridge[5 + part] - image.ImageBase;
            var backing = image.Resolve(table, 8, "constructor_failure_table_not_backed", "constructor_failure_table_ambiguous");
            backing.Section.RequireReadOnlyData("constructor_failure_table_permissions");
            Require(EfiVerify.TestConstructorFailureEhTable(image.Copy(backing.Offset, 8, "constructor_failure_table_truncated"), part, exception, wrapper), "constructor_failure_clause_table_mismatch");
        }

        for (int part = 0; part < EfiVerify.ConstructorFailureShapes.Length; part++)
        {
            var shape = EfiVerify.ConstructorFailureShapes[part];
            ulong method = Address(shape.Name, "OS.obj", "constructor_failure"), rva = method - image.ImageBase;
            int length = shape.Hex.Length / 2;
            var code = image.Resolve(rva, (ulong)length, "constructor_failure_method_not_backed", "constructor_failure_method_ambiguous");
            code.Section.RequireCode("constructor_failure_method_permissions");
            byte[] compiled = image.Copy(code.Offset, length, "constructor_failure_method_truncated");
            Require(EfiVerify.TestConstructorFailureCode(compiled, part), "constructor_failure_method_bytes_mismatch", shape.Name);
            foreach (var field in shape.Fields)
            {
                ulong target = Address(field.Target, field.Owner, "constructor_failure");
                Require(field.Addend >= 0 && target <= ulong.MaxValue - (ulong)field.Addend, "constructor_failure_relocation_addend");
                Rel32(compiled, field.Field, method, target + (ulong)field.Addend, "constructor_failure_method_target_mismatch");
            }

            uint[] begins = part == 6 ? [0, 35] : part == 7 ? [0, 40] : part == 2 ? [] : [0];
            uint[] ends = part == 6 ? [35, 53] : part == 7 ? [40, 101] : part == 2 ? [] : [(uint)length];
            var rows = image.RuntimeFunctions.Where(row => row.Begin < rva + (ulong)length && row.End > rva).ToArray();
            Require(rows.Length == begins.Length, "constructor_failure_runtime_rows", shape.Name);
            for (int index = 0; index < rows.Length; index++)
            {
                Require(rows[index].Begin == rva + begins[index] && rows[index].End == rva + ends[index], "constructor_failure_runtime_row_shape", shape.Name);
                int kind = part switch
                {
                    0 or 3 => 0,
                    1 => 1,
                    4 => 2,
                    5 => 3,
                    6 => index == 0 ? 4 : 5,
                    7 => index == 0 ? 6 : 5,
                    _ => -1
                };
                if (kind < 0)
                    continue; // Wrap's full instruction/frame graph is reviewed separately.
                int size = kind switch
                {
                    0 => 11,
                    1 => 9,
                    2 or 5 => 7,
                    3 => 9,
                    4 => 13,
                    _ => 15
                };
                var unwind = image.Resolve(rows[index].Unwind, (ulong)size, "constructor_failure_unwind_not_backed", "constructor_failure_unwind_ambiguous");
                unwind.Section.RequireReadOnlyData("constructor_failure_unwind_permissions");
                Require(EfiVerify.TestConstructorFailureUnwindBytes(image.Copy(unwind.Offset, size, "constructor_failure_unwind_truncated"), kind, bridge[part == 6 ? 5 : 6] - image.ImageBase), "constructor_failure_unwind_mismatch", shape.Name);
            }
        }

        var wrapperPrefix = image.Resolve(wrapper, 24, "constructor_failure_wrapper_not_backed", "constructor_failure_wrapper_ambiguous");
        wrapperPrefix.Section.RequireReadOnlyData("constructor_failure_wrapper_permissions");
        byte[] prefix = image.Copy(wrapperPrefix.Offset, 24, "constructor_failure_wrapper_truncated");
        Require(BinaryPrimitives.ReadUInt32LittleEndian(prefix) == 0x51000000 && BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(4)) == 32 && BinaryPrimitives.ReadUInt64LittleEndian(prefix.AsSpan(8)) == image.ImageBase + exception, "constructor_failure_wrapper_prefix_mismatch");
        ulong exported = Address("RhTypeCast_IsInstanceOfException", "OS.obj", "constructor_failure"), managed = Address("OS_System_Runtime_TypeCast__IsInstanceOfException", "OS.obj", "constructor_failure"), retained = Address("OS_OS_Runtime_RhpNewFastAllocation__MatchRetainedExceptionType", "OS.obj", "constructor_failure");
        Require(exported == managed, "constructor_failure_predicate_export_alias");
        var predicate = image.Resolve(exported - image.ImageBase, 5, "constructor_failure_predicate_not_backed", "constructor_failure_predicate_ambiguous");
        byte[] tail = image.Copy(predicate.Offset, 5, "constructor_failure_predicate_truncated");
        Require(tail[0] == 0xe9, "constructor_failure_predicate_export_shape");
        Rel32(tail, 1, exported, retained, "constructor_failure_predicate_export_target");
    }
}
