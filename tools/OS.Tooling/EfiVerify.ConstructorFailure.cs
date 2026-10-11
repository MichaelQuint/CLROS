using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    internal sealed record ConstructorFailureRelocation(int Field, string Target, long Addend, string Owner);
    internal sealed record ConstructorFailureShape(string Name, string Hex, ConstructorFailureRelocation[] Fields);
    // Independent closed-development compiler capture; every normalized field
    // is separately bound to its original linked symbol and exact addend.
    internal static readonly ConstructorFailureShape[] ConstructorFailureShapes =
    [
        new(@"OS_OS_Runtime_ConstructorFailureFixture___cctor", @"56534883EC28E800000000E800000000488BD8488D350000000048837EF8007405E80000000048891EE800000000488BC8E800000000CC",
            [new(22, @"?__NONGCSTATICS@OS_OS_Runtime_ConstructorFailureFixture@@", 0L, @"OS.obj"), new(34, @"__GetNonGCStaticBase_OS_OS_Runtime_ConstructorFailureFixture", 0L, @"OS.obj"), new(50, @"RhpThrowEx", 0L, @"CLROS.X64.ConstructorFailure.obj"), new(7, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureBodyEntered", 0L, @"OS.obj"), new(12, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureSeed", 0L, @"OS.obj"), new(42, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureOriginal", 0L, @"OS.obj"), ]),
        new(@"OS_OS_Runtime_ConstructorFailureFixture__Read", @"534883EC20488D1D0000000048837BF8007509488B034883C4205BC3E800000000EBF0",
            [new(8, @"?__NONGCSTATICS@OS_OS_Runtime_ConstructorFailureFixture@@", 0L, @"OS.obj"), new(29, @"__GetNonGCStaticBase_OS_OS_Runtime_ConstructorFailureFixture", 0L, @"OS.obj"), ]),
        new(@"__GetNonGCStaticBase_OS_OS_Runtime_ConstructorFailureFixture", @"488D1500000000488D4AF8E900000000",
            [new(3, @"?__NONGCSTATICS@OS_OS_Runtime_ConstructorFailureFixture@@", 0L, @"OS.obj"), new(12, @"OS_System_Runtime_CompilerServices_ClassConstructorRunner__CheckStaticClassConstructionReturnNonGCStaticBase", 0L, @"OS.obj"), ]),
        new(@"OS_System_Runtime_CompilerServices_ClassConstructorRunner__CheckStaticClassConstructionReturnNonGCStaticBase", @"56534883EC28488BD9488BF2488BCBE800000000488BCB488BD6E800000000488BC64883C4285B5EC3",
            [new(16, @"OS_System_Runtime_CompilerServices_ClassConstructorRunner__EnsureClassConstructorRun", 0L, @"OS.obj"), new(27, @"OS_OS_Runtime_RhpNewFastAllocation__ObserveCompilerNonGcBase", 0L, @"OS.obj"), ]),
        new(@"OS_System_Runtime_CompilerServices_ClassConstructorRunner__EnsureClassConstructorRun", @"4883EC28E800000000904883C428C3",
            [new(5, @"OS_OS_Runtime_RhpNewFastAllocation__EnsureCompilerClassConstructor", 0L, @"OS.obj"), ]),
        new(@"OS_OS_Runtime_RhpNewFastAllocation__EnsureCompilerClassConstructor", @"534881EC8000000033C048894424280F57E40F296424300F296424400F296424500F296424600F29642470488BD9488D0D000000004883B9E0490100050F859A000000488D0D00000000483B99787101000F8586000000488D0D000000004883B998710100007575488D0D000000004881C1104A010048894C2478488B4C24784C8D4C24504C8BC3BA00200000E80000000083F801753B48837C2468007533488BCBE800000000488D0D00000000483B8168710100751B488D0D0000000048C78198710100010000004881C4800000005BC3B91E000000E800000000CC488D0D000000004883B9E0490100037522488D0D000000004883B978710100007511488D0D000000004883B96871010000740BB908000000E800000000CC488D0D000000004881C1104A010048894C2478488B4C24784C8D4C24284C8BC3BA00200000E80000000083F801751D48837C244000741548395C2438750E837C2448007507837C244CFF740BB909000000E800000000CC4C8D0D000000000F10442428410F1181507101000F10442438410F118160710100488B54244849899170710100498999787101004C8D4B08488D15000000004C898AA871010048C782E049010004000000448B4C244C4C894C2420448B4C2448488BD34C8B442440B903000000E800000000488B4C2440E800000000B90A000000E800000000CC",
            [new(49, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(70, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(90, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(107, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(170, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(186, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(224, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(241, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(258, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(286, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(373, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(429, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(142, @"OS_OS_Kernel_Runtime_CompilerClassConstructorExecution__BeginLazy", 0L, @"OS.obj"), new(163, @"OS_OS_Runtime_RhpNewFastAllocation__GcReadWord", 0L, @"OS.obj"), new(216, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(278, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(321, @"OS_OS_Kernel_Runtime_CompilerClassConstructorExecution__BeginLazy", 0L, @"OS.obj"), new(365, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(480, @"OS_OS_Runtime_RhpNewFastAllocation__EhLine", 0L, @"OS.obj"), new(490, @"OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke", 0L, @"OS.obj"), new(500, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), ]),
        new(@"OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke", @"554883EC20488D6C2420488BC1A8027409488B4806FF50FEEB03FFD0904883C4205DC34883EC28E800000000488BC8E800000000CC",
            [new(48, @"RhpThrowEx", 0L, @"CLROS.X64.ConstructorFailure.obj"), new(40, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureWrap", 0L, @"OS.obj"), ]),
        new(@"OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter", @"55534883EC28488D6C2430E800000000488945F0488B4DF0E800000000488B45F04883C4285B5DC34883EC28488BD9488BCBE800000000488BCB3909E800000000488BC8E800000000488D4808E80000000048FFC0488945F0488D05B4FFFFFF4883C428C3",
            [new(12, @"OS_OS_Runtime_ConstructorFailureFixture__Read", 0L, @"OS.obj"), new(25, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureContinued", 0L, @"OS.obj"), new(51, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureOuterEntered", 0L, @"OS.obj"), new(61, @"OS_System_TypeInitializationException__get_InnerException", 0L, @"OS.obj"), new(69, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(78, @"OS_OS_Runtime_RhpNewFastAllocation__GcReadWord", 0L, @"OS.obj"), ]),
        new(@"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureWrap", @"5756534881EC8000000033C048894424784889442470488BD9488D0D000000004883B9E0490100060F8540030000488D0D000000004883B988710100000F852B030000488D0D000000004883B998710100010F8516030000488BCBE800000000488D0D00000000483B81B04801000F85FA020000488BCBE800000000488BC8E80000000085C00F84E2020000E80000000085C00F84D5020000488D0D0000000048C781887101000100000048C781E049010007000000488BCBE800000000488BD04C8D05000000004D8B80807101004C894424204C8D05000000004D8B80787101004C8D0D000000004D8B8988710100B908000000E800000000488D0D000000004881C1104A010048894C2478488B4C24784C8D0500000000410F1080507101000F11442430410F1080607101000F11442440498B907071010048895424504C8D442430BA002000004533C9E8000000008BF083FE010F851D020000488D0D00000000488B8978710100E800000000488D0D00000000483B81687101000F85F6010000488D0D00000000488B8978710100488BF9E8000000004C8BC84C8D05000000004D8B80807101004C894424204C8BC78BD6B909000000E80000000033C948894C2478488D0D0000000048C781E049010008000000488D0D00000000E800000000488BF0488D0D00000000488BB978710100488D4E08488BD3E80000000048897E10488BCEE800000000488D0D000000004881C1106A01004883C158483B41280F855C010000E80000000085C00F844F010000488D15000000004881C2106A01004889542470488B5424703812488D4C245841B802000000E8000000008B44245C807C2458010F852101000083F8040F8518010000837C2460000F850D010000E80000000085C00F8400010000E80000000085C00F84F3000000488BCEE800000000488BD8488B4E08E8000000004C8BC048C744242004000000488BD34C8B4E10B90A000000E80000000033C04889442470488D050000000048C780E049010009000000E800000000488BC8488BD6E8000000008BD8E800000000488BC8488BD6E80000000085DB0F848A00000085C00F8482000000488B4E10488D0500000000483B8878710100756E488BCEE800000000488BD04C8D05000000004D8B80A07101004C8944242041B80100000041B901000000B90B000000E800000000488BC64881C4800000005B5E5FC3B90E000000E800000000CCB90F000000E800000000CCB910000000E800000000CCB911000000E800000000CCB912000000E800000000CC",
            [new(28, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(49, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(70, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(99, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(156, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(196, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(215, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(229, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(253, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(277, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(351, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(370, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(390, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(415, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(456, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(474, @"??_7OS_System_TypeInitializationException@@6B@", 0L, @"OS.obj"), new(489, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(508, @"RhpAssignRef", 0L, @"CLROS.X64.CheckedReferenceBarriers.obj"), new(527, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(568, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(727, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(799, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(826, @"?__NONGCSTATICS@OS_OS_Runtime_RhpNewFastAllocation@@", 0L, @"OS.obj"), new(92, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(120, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(128, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureRootRetained", 0L, @"OS.obj"), new(141, @"OS_OS_Runtime_RhpNewFastAllocation__EhObjectIntact", 0L, @"OS.obj"), new(186, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(246, @"OS_OS_Runtime_RhpNewFastAllocation__EhLine", 0L, @"OS.obj"), new(333, @"OS_OS_Kernel_Runtime_CompilerClassConstructorExecution__FinishLazy", 0L, @"OS.obj"), new(363, @"OS_OS_Runtime_RhpNewFastAllocation__GcReadWord", 0L, @"OS.obj"), new(405, @"OS_OS_Runtime_RhpNewFastAllocation__GcReadWord", 0L, @"OS.obj"), new(442, @"OS_OS_Runtime_RhpNewFastAllocation__EhLine", 0L, @"OS.obj"), new(479, @"RhpNewFast", 0L, @"OS.obj"), new(520, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(553, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureWrapperIntact", 0L, @"OS.obj"), new(603, @"OS_OS_Runtime_RhpNewFastAllocation__CollectGcGraph", 0L, @"OS.obj"), new(643, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureWrapperIntact", 0L, @"OS.obj"), new(656, @"OS_OS_Runtime_RhpNewFastAllocation__EhObjectIntact", 0L, @"OS.obj"), new(672, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(684, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(713, @"OS_OS_Runtime_RhpNewFastAllocation__EhLine", 0L, @"OS.obj"), new(743, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureWrapperType", 0L, @"OS.obj"), new(754, @"RhTypeCast_IsInstanceOfException", 0L, @"OS.obj"), new(761, @"OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureBaseType", 0L, @"OS.obj"), new(772, @"RhTypeCast_IsInstanceOfException", 0L, @"OS.obj"), new(816, @"OS_OS_Runtime_RhpNewFastAllocation__Bits", 0L, @"OS.obj"), new(860, @"OS_OS_Runtime_RhpNewFastAllocation__EhLine", 0L, @"OS.obj"), new(884, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(895, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(906, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(917, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), new(928, @"OS_OS_Runtime_RhpNewFastAllocation__RejectEh", 0L, @"OS.obj"), ]),
    ];

    public static bool TestConstructorFailureCode(byte[]? bytes, int part)
    {
        if ((uint)part >= (uint)ConstructorFailureShapes.Length || bytes is null) return false;
        var shape = ConstructorFailureShapes[part];
        if (bytes.Length != shape.Hex.Length / 2) return false;
        byte[] normalized = [.. bytes];
        foreach (var field in shape.Fields) normalized.AsSpan(field.Field, 4).Clear();
        return normalized.AsSpan().SequenceEqual(Convert.FromHexString(shape.Hex));
    }

    public static bool TestConstructorFailureEhTable(byte[]? bytes, int part, ulong exceptionType, ulong wrapperType)
    {
        byte[] prefix = part switch { 0 => [2,26,128,70], 1 => [2,22,72,80], _ => [] };
        ulong type = part == 0 ? exceptionType : wrapperType;
        return prefix.Length == 4 && bytes is { Length: 8 } && exceptionType is > 0 and <= uint.MaxValue &&
            wrapperType is > 0 and <= uint.MaxValue && exceptionType != wrapperType &&
            bytes.AsSpan(0,4).SequenceEqual(prefix) && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) == type;
    }

    public static bool TestConstructorFailureUnwindBytes(byte[]? bytes, int part, ulong table)
    {
        byte[] prefix = part switch
        {
            0 => [1,6,3,0,6,66,2,48,1,96,0],
            1 => [1,5,2,0,5,50,1,48,0],
            2 => [1,4,1,0,4,66,0],
            3 => [1,8,2,0,8,242,1,48,0],
            4 => [1,5,2,0,5,50,1,80,4],
            5 => [1,4,1,0,4,66,1],
            6 => [1,6,3,0,6,66,2,48,1,80,4],
            _ => []
        };
        bool eh = part is 4 or 6;
        return prefix.Length != 0 && bytes is not null && bytes.Length == prefix.Length + (eh ? 4 : 0) &&
            bytes.AsSpan(0,prefix.Length).SequenceEqual(prefix) && (!eh || table is > 0 and <= uint.MaxValue &&
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(prefix.Length)) == table);
    }
}

internal sealed partial class EfiVerification
{
    private void VerifyConstructorFailureBridge()
    {
        string[] names = ["RhpThrowEx", "CLROS_CctorFailure_InvokeClauses", "CLROS_CctorFailure_OuterClauses",
            "CLROS_CctorFailure_Select", "CLROS_CctorFailure_ValidateContinuation",
            "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke",
            "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter"];
        const string objectName = "CLROS.X64.ConstructorFailure.obj";
        if (!profile.IsEnabled("M04ConstructorFailure"))
        {
            Require(!response.Contains(objectName, StringComparison.OrdinalIgnoreCase), "constructor_failure_unrequested_native_object");
            foreach (string name in names.Skip(1))
                Require(Symbols(map, name).Count == 0, "constructor_failure_unrequested_native_symbol", name);
            return;
        }
        byte[] obj = NativeObject("constructorFailure", ".ctor-failure.obj", objectName, EfiContracts.ConstructorFailureObjectBytes,
            EfiContracts.ConstructorFailureObjectSha256, "constructor_failure");
        ulong[] addresses = names.Select((name, index) => Address(name, index < 3 ? objectName : "OS.obj", "constructor_failure")).ToArray();
        Require(addresses[0] % 16 == 0 && addresses[1] == addresses[0] + 1024 &&
            addresses[2] == addresses[0] + 1040, "constructor_failure_bridge_layout");
        byte[] text = BridgeText(addresses[0], 1048, obj, [330, 811, 1027, 1043], "constructor_failure");
        int[] fields = [330, 811, 1027, 1043], targets = [3, 4, 5, 6];
        for (int index = 0; index < fields.Length; index++)
            Rel32(text, fields[index], addresses[0], addresses[targets[index]], "constructor_failure_rel32_mismatch");
        foreach (int index in new[] {3, 4}) CodeAddress(addresses[index], "constructor_failure_callback");
        VerifyConstructorFailureGeometry(addresses);
        emit($"EFI_ARTIFACT_CONSTRUCTOR_FAILURE_THUNK bytes={EfiContracts.ConstructorFailureObjectBytes} text=1048 frame=840 callbacks=2 tables=2 geometryReviewed=true transferAccepted=false callbackStackAccepted=false guestAccepted=false");
        // Independent first-party stack review includes both live bridges and
        // the reference-store callback: 12,216 bytes <= 16,400. Firmware
        // diagnostic internals and actual transfers require guest evidence.
    }

    private void VerifyConstructorFailureGeometry(ulong[] bridge)
    {
        ulong nonGc = Address("?__NONGCSTATICS@OS_OS_Runtime_ConstructorFailureFixture@@","OS.obj","constructor_failure"),
            constructor = Address("OS_OS_Runtime_ConstructorFailureFixture___cctor","OS.obj","constructor_failure");
        Require(nonGc >= image.ImageBase + 8 && (nonGc & 7) == 0,"constructor_failure_context_address");
        var context = image.Resolve(nonGc - image.ImageBase - 8,16,"constructor_failure_context_not_backed","constructor_failure_context_ambiguous");
        Require((context.Section.Characteristics & 0xc0000040) == 0xc0000040 &&
            (context.Section.Characteristics & 0x22000020) == 0,"constructor_failure_context_permissions");
        byte[] initial = image.Copy(context.Offset,16,"constructor_failure_context_truncated");
        Require(BinaryPrimitives.ReadUInt64LittleEndian(initial) == constructor &&
            BinaryPrimitives.ReadUInt64LittleEndian(initial.AsSpan(8)) == 0,"constructor_failure_context_initializer_mismatch");
        ulong exception = Address("??_7OS_System_Exception@@6B@", "OS.obj", "constructor_failure") - image.ImageBase,
            wrapper = Address("??_7OS_System_TypeInitializationException@@6B@", "OS.obj", "constructor_failure") - image.ImageBase;
        for (int part = 0; part < 2; part++)
        {
            ulong table = bridge[5 + part] - image.ImageBase;
            var backing = image.Resolve(table,8,"constructor_failure_table_not_backed","constructor_failure_table_ambiguous");
            backing.Section.RequireReadOnlyData("constructor_failure_table_permissions");
            Require(EfiVerify.TestConstructorFailureEhTable(image.Copy(backing.Offset,8,"constructor_failure_table_truncated"),part,exception,wrapper), "constructor_failure_clause_table_mismatch");
        }
        for (int part = 0; part < EfiVerify.ConstructorFailureShapes.Length; part++)
        {
            var shape = EfiVerify.ConstructorFailureShapes[part];
            ulong method = Address(shape.Name,"OS.obj","constructor_failure"), rva = method - image.ImageBase;
            int length = shape.Hex.Length / 2;
            var code = image.Resolve(rva,(ulong)length,"constructor_failure_method_not_backed","constructor_failure_method_ambiguous");
            code.Section.RequireCode("constructor_failure_method_permissions");
            byte[] compiled = image.Copy(code.Offset,length,"constructor_failure_method_truncated");
            Require(EfiVerify.TestConstructorFailureCode(compiled,part),"constructor_failure_method_bytes_mismatch",shape.Name);
            foreach (var field in shape.Fields)
            {
                ulong target = Address(field.Target,field.Owner,"constructor_failure");
                Require(field.Addend >= 0 && target <= ulong.MaxValue - (ulong)field.Addend,"constructor_failure_relocation_addend");
                Rel32(compiled,field.Field,method,target + (ulong)field.Addend,"constructor_failure_method_target_mismatch");
            }
            uint[] begins = part == 6 ? [0,35] : part == 7 ? [0,40] : part == 2 ? [] : [0];
            uint[] ends = part == 6 ? [35,53] : part == 7 ? [40,101] : part == 2 ? [] : [(uint)length];
            var rows = image.RuntimeFunctions.Where(row => row.Begin < rva + (ulong)length && row.End > rva).ToArray();
            Require(rows.Length == begins.Length,"constructor_failure_runtime_rows",shape.Name);
            for (int index = 0; index < rows.Length; index++)
            {
                Require(rows[index].Begin == rva + begins[index] && rows[index].End == rva + ends[index],"constructor_failure_runtime_row_shape",shape.Name);
                int kind = part switch { 0 or 3 => 0, 1 => 1, 4 => 2, 5 => 3, 6 => index == 0 ? 4 : 5, 7 => index == 0 ? 6 : 5, _ => -1 };
                if (kind < 0) continue; // Wrap's full instruction/frame graph is reviewed separately.
                int size = kind switch {0 => 11, 1 => 9, 2 or 5 => 7, 3 => 9, 4 => 13, _ => 15};
                var unwind = image.Resolve(rows[index].Unwind,(ulong)size,"constructor_failure_unwind_not_backed","constructor_failure_unwind_ambiguous");
                unwind.Section.RequireReadOnlyData("constructor_failure_unwind_permissions");
                Require(EfiVerify.TestConstructorFailureUnwindBytes(image.Copy(unwind.Offset,size,"constructor_failure_unwind_truncated"),kind,
                    bridge[part == 6 ? 5 : 6] - image.ImageBase),"constructor_failure_unwind_mismatch",shape.Name);
            }
        }
        var wrapperPrefix = image.Resolve(wrapper,24,"constructor_failure_wrapper_not_backed","constructor_failure_wrapper_ambiguous");
        wrapperPrefix.Section.RequireReadOnlyData("constructor_failure_wrapper_permissions");
        byte[] prefix = image.Copy(wrapperPrefix.Offset,24,"constructor_failure_wrapper_truncated");
        Require(BinaryPrimitives.ReadUInt32LittleEndian(prefix) == 0x51000000 && BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(4)) == 32 &&
            BinaryPrimitives.ReadUInt64LittleEndian(prefix.AsSpan(8)) == image.ImageBase + exception,"constructor_failure_wrapper_prefix_mismatch");
        ulong exported = Address("RhTypeCast_IsInstanceOfException","OS.obj","constructor_failure"),
            managed = Address("OS_System_Runtime_TypeCast__IsInstanceOfException","OS.obj","constructor_failure"),
            retained = Address("OS_OS_Runtime_RhpNewFastAllocation__MatchRetainedExceptionType","OS.obj","constructor_failure");
        Require(exported == managed,"constructor_failure_predicate_export_alias");
        var predicate = image.Resolve(exported - image.ImageBase,5,"constructor_failure_predicate_not_backed","constructor_failure_predicate_ambiguous");
        byte[] tail = image.Copy(predicate.Offset,5,"constructor_failure_predicate_truncated");
        Require(tail[0] == 0xe9,"constructor_failure_predicate_export_shape");
        Rel32(tail,1,exported,retained,"constructor_failure_predicate_export_target");
    }
}
