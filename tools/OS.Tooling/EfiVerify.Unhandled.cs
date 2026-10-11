using System.Buffers.Binary;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    public static bool TestUnhandledEhTable(byte[]? table, ulong sibling) => table is { Length: 8 } &&
        sibling is > 0 and <= uint.MaxValue && table.AsSpan(0,4).SequenceEqual(new byte[]{2,20,72,58}) &&
        BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(4)) == sibling;

    // Include the NativeAOT GC/EH marker following the Windows unwind codes.
    public static bool TestUnhandledUnwindBytes(byte[]? bytes, int part, ulong table)
    {
        byte[] prefix = part switch
        {
            0 => [1,4,1,0,4,66,0], // thrower: 40 bytes
            1 => [1,5,2,0,5,82,1,48,0], // middle: 48 + RBX
            2 => [1,5,2,0,5,82,1,80,4], // catch root: 48 + RBP, EH table
            3 => [1,4,1,0,4,66,1], // catch funclet: 40, root association
            _ => []
        };
        return prefix.Length != 0 && bytes is not null && bytes.Length == prefix.Length + (part == 2 ? 4 : 0) &&
            bytes.AsSpan(0,prefix.Length).SequenceEqual(prefix) && (part != 2 || table is > 0 and <= uint.MaxValue &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(prefix.Length)) == table);
    }

    public static bool TestUnhandledCode(byte[]? bytes, int part)
    {
        string shape = part switch
        {
            0 => "4883EC28E800000000CC",
            1 => "534883EC30488BD9488B4B0848894C2428488D4C2428E800000000488BCBE800000000488B4424284883C4305BC3",
            2 => "554883EC30488D6C2430E800000000488945F8488B45F84883C4305DC34883EC28E80000000033C0488945F8488D05E0FFFFFF4883C428C3",
            _ => ""
        };
        if(shape.Length == 0 || bytes is null || bytes.Length != shape.Length / 2) return false;
        byte[] normalized=[..bytes];
        foreach(int field in part switch { 0 => new[]{5}, 1 => [23,31], _ => [11,34] }) normalized.AsSpan(field,4).Clear();
        return normalized.AsSpan().SequenceEqual(Convert.FromHexString(shape));
    }
}

internal sealed partial class EfiVerification
{
    private void VerifyUnhandledBridge()
    {
        const string objectName = "CLROS.X64.SimpleThrowCatch.obj";
        byte[] obj = NativeObject("simpleCatch", ".catch.obj", objectName,
            EfiContracts.SimpleCatchObjectBytes, EfiContracts.SimpleCatchObjectSha256, "unhandled");
        string[] names = ["RhpThrowEx", "CLROS_Eh_GetClauseTable", "CLROS_Eh_Select", "CLROS_Eh_ValidateContinuation",
            "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Catcher", "RhTypeCast_IsInstanceOfException",
            "OS_OS_Runtime_RhpNewFastAllocation__Catcher", "OS_OS_Runtime_RhpNewFastAllocation__Thrower",
            "OS_OS_Runtime_RhpNewFastAllocation__UnhandledMiddle"];
        ulong[] addresses = names.Select((name, index) => Address(name, index < 2 ? objectName : "OS.obj", "unhandled")).ToArray();
        Require(addresses[0] % 16 == 0 && addresses[1] == addresses[0] + 1024, "unhandled_bridge_layout");
        foreach (int index in new[] { 2, 3, 5, 6, 7, 8 }) CodeAddress(addresses[index], "unhandled_code");
        byte[] text = BridgeText(addresses[0], 1032, obj, [330, 811, 1027], "unhandled");
        Rel32(text, 330, addresses[0], addresses[2], "unhandled_selector_relocation");
        Rel32(text, 811, addresses[0], addresses[3], "unhandled_continuation_relocation");
        Rel32(text, 1027, addresses[0], addresses[4], "unhandled_table_relocation");
        foreach(int index in new[]{2,3,5,6,7,8})
            Require(!(addresses[index]>=addresses[0] && addresses[index]<addresses[0]+1032),"unhandled_callback_alias");
        ulong tableRva=addresses[4]-image.ImageBase;
        ulong sibling=Address("??_7OS_OS_Runtime_SimpleCatchSibling@@6B@","OS.obj","unhandled")-image.ImageBase;
        image.Resolve(sibling,24,"unhandled_type_not_backed","unhandled_type_ambiguous").Section.RequireReadOnlyData("unhandled_type_permissions");
        var table=image.Resolve(tableRva,8,"unhandled_table_not_backed","unhandled_table_ambiguous");
        table.Section.RequireReadOnlyData("unhandled_table_permissions");
        Require(EfiVerify.TestUnhandledEhTable(image.Copy(table.Offset,8,"unhandled_table_truncated"),sibling),"unhandled_clause_table_mismatch");
        ulong mark=Address("OS_OS_Runtime_RhpNewFastAllocation__UnhandledMarkFrame","OS.obj","unhandled"),
            unexpected=Address("OS_OS_Runtime_RhpNewFastAllocation__UnhandledUnexpectedCatch","OS.obj","unhandled");
        CodeAddress(mark,"unhandled_mark"); CodeAddress(unexpected,"unhandled_unexpected");
        ulong[] methods=[addresses[7],addresses[8],addresses[6]];
        int[] lengths=[10,46,56];
        for(int part=0;part<methods.Length;part++)
        {
            ulong rva=methods[part]-image.ImageBase;
            var code=image.Resolve(rva,(ulong)lengths[part],"unhandled_method_not_backed","unhandled_method_ambiguous");
            code.Section.RequireCode("unhandled_method_permissions");
            byte[] bytes=image.Copy(code.Offset,lengths[part],"unhandled_method_truncated");
            Require(EfiVerify.TestUnhandledCode(bytes,part),"unhandled_method_bytes_mismatch");
            int[] fields=part switch{0=>[5],1=>[23,31],_=>[11,34]};
            ulong[] targets=part switch{0=>[addresses[0]],1=>[mark,methods[0]],_=>[methods[1],unexpected]};
            for(int i=0;i<fields.Length;i++) Rel32(bytes,fields[i],methods[part],targets[i],"unhandled_method_call_mismatch");
            var rows=image.RuntimeFunctions.Where(f=>f.Begin<rva+(ulong)lengths[part] && f.End>rva).ToArray();
            Require(rows.Length==(part==2?2:1),"unhandled_runtime_rows");
            for(int i=0;i<rows.Length;i++)
            {
                Require(rows[i].Begin==rva+(ulong)(i==0?0:29) && rows[i].End==rva+(ulong)(part==2 && i==0?29:lengths[part]),"unhandled_runtime_row_shape");
                int kind=part+i,size=kind switch{0=>7,1=>9,2=>13,_=>7};
                var unwind=image.Resolve(rows[i].Unwind,(ulong)size,"unhandled_unwind_not_backed","unhandled_unwind_ambiguous");
                Require(EfiVerify.TestUnhandledUnwindBytes(image.Copy(unwind.Offset,size,"unhandled_unwind_truncated"),kind,tableRva),"unhandled_unwind_mismatch");
            }
        }
        ulong managed=Address("OS_System_Runtime_TypeCast__IsInstanceOfException","OS.obj","unhandled"),
            retained=Address("OS_OS_Runtime_RhpNewFastAllocation__MatchRetainedExceptionType","OS.obj","unhandled");
        Require(managed==addresses[5],"unhandled_predicate_export_alias");
        CodeAddress(retained,"unhandled_predicate_target");
        var predicate=image.Resolve(addresses[5]-image.ImageBase,5,"unhandled_predicate_not_backed","unhandled_predicate_ambiguous");
        byte[] exported=image.Copy(predicate.Offset,5,"unhandled_predicate_truncated");
        Require(exported[0]==0xe9,"unhandled_predicate_export_shape");
        Rel32(exported,1,addresses[5],retained,"unhandled_predicate_export_call");
        // Three target/object argument pairs and one null pair reach the actual
        // RuntimeImport/export ABI before the compiler throw is entered.
        ulong run=Address("OS_OS_Runtime_RhpNewFastAllocation__RunUnhandledException","OS.obj","unhandled");
        var callBacking=image.Resolve(run-image.ImageBase+937,59,"unhandled_predicate_calls_not_backed","unhandled_predicate_calls_ambiguous");
        byte[] calls=image.Copy(callBacking.Offset,59,"unhandled_predicate_calls_truncated");
        foreach(int at in new[]{0,18,36}) Require(calls.AsSpan(at,7).SequenceEqual(new byte[]{0x48,0x8b,0xc8,0x48,0x8b,0xd3,0xe8}),"unhandled_predicate_argument_shape");
        Require(calls.AsSpan(50,5).SequenceEqual(new byte[]{0x33,0xc9,0x33,0xd2,0xe8}),"unhandled_predicate_null_shape");
        foreach(int field in new[]{7,25,43,55}) Rel32(calls,field,run+937,addresses[5],"unhandled_predicate_import_call");
        emit($"EFI_ARTIFACT_UNHANDLED_THUNK bytes={EfiContracts.SimpleCatchObjectBytes} text=1032 tableBytes=8 clauses=1 runtimeRows=4 predicateExportCalls=4 transferAccepted=false callbackStackAccepted=false compilerGeometryAccepted=true guestAccepted=false");
    }
}
