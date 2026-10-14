using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiPeImage;
using static OS.Tooling.EfiEvidence;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    private ulong Address(string symbol, string owner, string prefix)
    {
        var matches = Symbols(map, symbol);
        Require(matches.Count == 1 && Owner(matches[0], owner), prefix + "_symbol_owner", symbol);
        ulong address = Hex(matches[0].Groups[3].Value, prefix + "_symbol_address_invalid");
        Require(address > image.ImageBase && address - image.ImageBase < image.SizeOfImage, prefix + "_symbol_rva_invalid", symbol);
        return address;
    }
    private void BindProfile(bool enabled, string name, params string[] symbols)
        => BindProfile(image, map, enabled, name, symbols);

    private static void BindProfile(EfiPeImage image, string map, bool enabled, string name, params string[] symbols)
    {
        foreach (string symbol in symbols)
        {
            var binding = Symbols(map, symbol);
            if (!enabled) { Require(binding.Count == 0, name + "_unrequested_native_adapter", symbol); continue; }
            Require(binding.Count == 1 && Owner(binding[0], "OS.obj"), name + "_native_adapter_missing", symbol);
            ulong address = Hex(binding[0].Groups[3].Value, name + "_address_invalid");
            Require(address > image.ImageBase && address - image.ImageBase < image.SizeOfImage, name + "_rva_invalid", symbol);
            image.Resolve(address - image.ImageBase, 1, name + "_not_backed", name + "_ambiguous").Section.RequireCode(name + "_section_invalid");
        }
    }
    private void VerifyProfileBindings()
    {
        HashSet<ulong> sampleRvas = []; HashSet<int> sampleRows = [];
        foreach (string name in new[] { "OS_OS_Runtime_X64UnwindMetadataProbe__SampleA", "OS_OS_Runtime_X64UnwindMetadataProbe__SampleB" })
        {
            var matches = Symbols(map, name);
            if (!profile.IsEnabled("M04X64UnwindMetadata")) { Require(matches.Count == 0, "x64_unwind_unrequested_sample", name); continue; }
            Require(matches.Count == 1 && Owner(matches[0], "OS.obj"), "x64_unwind_sample_map_identity", name);
            ulong address = Hex(matches[0].Groups[3].Value, "x64_unwind_sample_address");
            Require(address > image.ImageBase && address - image.ImageBase < image.SizeOfImage, "x64_unwind_sample_image_range", name);
            ulong rva = address - image.ImageBase; Require(sampleRvas.Add(rva), "x64_unwind_sample_alias");
            var rows = image.RuntimeFunctions.Select((f, i) => (f, i)).Where(p => rva >= p.f.Begin && rva < p.f.End).ToArray();
            Require(rows.Length == 1, "x64_unwind_sample_runtime_row", name); Require(sampleRows.Add(rows[0].i), "x64_unwind_sample_runtime_row_alias");
            var row = rows[0].f; image.Resolve(row.Begin, row.End - row.Begin, "x64_unwind_sample_code_backing", "x64_unwind_sample_code_ambiguous").Section.RequireCode("x64_unwind_sample_code_permissions");
            int offset = row.UnwindOffset; int version = image.Bytes[offset] & 7, flags = image.Bytes[offset] >> 3, prolog = image.Bytes[offset + 1], slots = image.Bytes[offset + 2], frame = image.Bytes[offset + 3];
            Require(version == 1 && slots != 0 && prolog <= row.End - row.Begin, "x64_unwind_sample_metadata_header", name);
            emit($"EFI_ARTIFACT_X64_UNWIND_METHOD symbol={name} rva={rva} functionIndex={rows[0].i} beginRva={row.Begin} endRva={row.End} unwindRva={row.Unwind} version={version} flags={flags} prolog={prolog} codeSlots={slots} frameRegister={frame & 15} frameOffset={frame >> 4}");
        }
        if (profile.IsEnabled("M04X64UnwindMetadata")) emit("EFI_ARTIFACT_X64_UNWIND_METADATA methods=2 association=same-build-linker-map executionAccepted=false stackBoundAccepted=false unwindExecution=false");
        BindProfile(profile.IsEnabled("M04SeededCollectorChurn"), "churn", "OS_OS_Runtime_RhpNewFastAllocation__RunSeededCollectorChurn");
        BindProfile(profile.BootPolicy == "VerifiedExit", "normal_verified_exit", "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__WriteVerifiedExitBytes");
        BindProfile(profile.IsEnabled("M03OwnedSubsystemSurvival") || profile.BootPolicy == "VerifiedExit", "owned_subsystem", "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__ReportOwnedSubsystems");
        BindProfile(profile.IsEnabled("M03OwnedSubsystemPanic"), "owned_subsystem_panic", "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__ReadOwnedGuard");
        BindProfile(profile.IsEnabled("M06PackageRead"), "m06_package_read", "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__RunOwnedPackageRead");
        BindProfile(profile.IsEnabled("M07ArpReceive"), "m07_arp_receive", "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__RunOwnedArpReceive");
        BindProfile(profile.IsEnabled("M03FirmwareAdapterRevocation"), "firmware_revocation", "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__ReportRevokedAdapters");
        BindProfile(profile.IsEnabled("M03FirmwareAdapterRevocation") || profile.IsEnabled("M16003"), "firmware_revocation",
            new[] { "AdapterConsole", "AdapterMemoryMap", "AdapterFileSystem", "AdapterTimer", "AdapterRandom", "AdapterConfiguration", "AdapterPhysicalMemory", "AdapterFramebuffer" }
                .Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n).ToArray());
        BindProfile(profile.IsEnabled("M16003"), "m16_service", new[] { "RunOwnedWorkloadServiceLoop", "ServiceReadClock", "ServiceIteration", "ServiceStop" }
            .Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n).ToArray());
        BindProfile(profile.IsEnabled("M16004"), "m16_startup", new[] { "PrepareLinuxStartup", "RunOwnedLinuxStartup", "ReadLinuxUserRoot" }
            .Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n).ToArray());
        BindProfile(profile.IsEnabled("M16005"), "m16_syscalls", new[] { "PrepareLinuxUserBoundary", "RunOwnedLinuxSyscalls", "DispatchLinuxSyscall",
            "LinuxUserPageAccess", "WriteLinuxUserBytes", "ReportLinuxUserFault" }
            .Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n).ToArray());
        BindProfile(profile.IsEnabled("M16006"), "m16_memory", new[] { "PrepareLinuxMemory", "DispatchLinuxStartup", "CopyToLinuxUser",
            "ReadLinuxClock", "LinuxMemoryBarrier" }.Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n).ToArray());
        BindProfile(profile.IsEnabled("M16007"), "m16_descriptors", new[] { "PrepareLinuxDescriptors", "DispatchLinuxDescriptors",
            "WriteDescriptorOutput" }
            .Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n).ToArray());
        VerifyLinuxCopyBindings(profile, map, image);
        BindProfile(profile.IsEnabled("M04ClassConstructorExecution"), "compiler_cctor", "OS_OS_Runtime_RhpNewFastAllocation__BindAndRunCompilerClassConstructors", "OS_OS_Runtime_RhpNewFastAllocation__CctorDirectRead", "OS_System_Runtime_CompilerServices_ClassConstructorRunner__CheckStaticClassConstructionReturnGCStaticBase");
        BindProfile(profile.IsEnabled("M04ClassConstructorExecution") || profile.IsEnabled("M04ConstructorFailure"), "compiler_cctor", "OS_System_Runtime_CompilerServices_ClassConstructorRunner__EnsureClassConstructorRun", "OS_System_Runtime_CompilerServices_ClassConstructorRunner__CheckStaticClassConstructionReturnNonGCStaticBase");
        Require(!Regex.IsMatch(map, @"ClassConstructorRunner__[^\s]*ThreadStatic"), "compiler_cctor_thread_static_unsupported");
        foreach (string symbol in new[] { "Enter", "Diagnose", "Fault" }.Select(n => "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__" + n))
        {
            var binding = Symbols(map, symbol);
            if (!profile.IsEnabled("M03FirstRealExitBootServices") && profile.BootPolicy != "VerifiedExit") { Require(binding.Count == 0, "first_exit_unrequested_native_callback", symbol); continue; }
            Require(binding.Count == 1 && Owner(binding[0], "OS.obj"), "first_exit_native_callback_missing", symbol);
            ulong address = Hex(binding[0].Groups[3].Value, "first_exit_callback_address_invalid");
            Require(address > image.ImageBase && address - image.ImageBase < image.SizeOfImage, "first_exit_callback_rva_invalid");
            image.Resolve(address - image.ImageBase, 1, "first_exit_callback_not_backed", "first_exit_callback_ambiguous").Section.RequireCode("first_exit_callback_section_invalid");
        }
    }
    internal static void VerifyLinuxCopyBindings(EfiProfile profile, string map, EfiPeImage image)
    {
        const string prefix = "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__";
        // M16-009 implies M16-008. Its authenticated thread owner replaces the
        // single-thread callbacks; those old bodies are trimmed by NativeAOT.
        bool threads = profile.IsEnabled("M16008");
        BindProfile(image, map, profile.IsEnabled("M16005") && !threads, "m16_syscalls", prefix + "CopyLinuxUserBytes");
        BindProfile(image, map, profile.IsEnabled("M16007") && !threads, "m16_descriptors",
            prefix + "CopyDescriptorInput", prefix + "CopyDescriptorOutput");
        BindProfile(image, map, threads, "m16_threads", prefix + "LinuxThreadCopyInput", prefix + "LinuxThreadCopyOutput");
    }

    private byte[] NativeObject(string propertyPrefix, string sidecar, string objectName, int size, string hash, string reasonPrefix)
    {
        string preserved = EvidencePath(propertyPrefix + "ObjectPath", reasonPrefix + "_object_path_invalid"), linked = EvidencePath(propertyPrefix + "LinkedObjectPath", reasonPrefix + "_linked_object_path_invalid");
        Require(SamePath(preserved, artifactPath + sidecar) && SamePath(linked, Path.Combine(Path.GetDirectoryName(linkedSerializer)!, objectName)) && Number(propertyPrefix + "ObjectBytes") == size && Text(propertyPrefix + "ObjectSha256") == hash, reasonPrefix + "_object_contract_invalid");
        AuthorityFile preservedFile = Open(preserved, reasonPrefix + "_object", size, output: true), linkedFile = Open(linked, reasonPrefix + "_linked_object", size, output: true, root: Root);
        Require(preservedFile.Length == size && preservedFile.Sha256 == hash && linkedFile.Length == size && linkedFile.Sha256 == hash, reasonPrefix + "_object_identity_mismatch");
        Require(ObjectCount(linked) == 1, reasonPrefix + "_object_link_response_missing");
        return preservedFile.ReadBytes(size, reasonPrefix + "_object_read_failed");
    }
    private byte[] BridgeText(ulong address, int length, byte[] nativeObject, int[] relocations, string prefix)
    {
        var backing = image.Resolve(address - image.ImageBase, (ulong)length, prefix + "_text_not_backed", prefix + "_text_ambiguous");
        backing.Section.RequireCode(prefix + "_text_section_invalid");
        byte[] text = image.Copy(backing.Offset, length, prefix + "_text_truncated");
        Require(nativeObject.Length >= 60 + length, prefix + "_object_truncated");
        for (int i = 0; i < text.Length; i++) if (!relocations.Any(r => i >= r && i < r + 4)) Require(text[i] == nativeObject[60 + i], prefix + "_text_mismatch", "offset=" + i);
        return text;
    }
    private static void Rel32(byte[] text, int offset, ulong start, ulong target, string reason) => Require((decimal)start + offset + 4 + BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(offset, 4)) == target, reason);
    private void CodeAddress(ulong address, string prefix) => image.Resolve(address - image.ImageBase, 1, prefix + "_not_backed", prefix + "_ambiguous").Section.RequireCode(prefix + "_section_invalid");
    private void VerifyNativeBridges()
    {
        if (profile.IsEnabled("M04CheckedReferenceBarrier") || profile.IsEnabled("M04GcStaticRootMaterialization"))
        {
            byte[] obj = NativeObject("checkedReference", ".references.obj", "CLROS.X64.CheckedReferenceBarriers.obj", EfiContracts.CheckedReferenceObjectBytes, EfiContracts.CheckedReferenceObjectSha256, "reference");
            string[] names = ["RhpAssignRef", "RhpCheckedAssignRef", "RhpByRefAssignRef", "CLROS_CheckedReferenceBarrier_Apply"];
            ulong[] a = names.Select((n, i) => Address(n, i == 3 ? "OS.obj" : "CLROS.X64.CheckedReferenceBarriers.obj", "reference")).ToArray();
            Require(a[1] == a[0] + 464 && a[2] == a[0] + 928 && a[0] % 16 == 0 && !(a[3] >= a[0] && a[3] < a[0] + 1390), "reference_symbol_layout");
            CodeAddress(a[3], "reference_callback"); int[] fields = [232, 696, 1160]; byte[] text = BridgeText(a[0], 1390, obj, fields, "reference");
            foreach (int field in fields) Rel32(text, field, a[0], a[3], "reference_callback_rel32_mismatch");
            emit($"EFI_ARTIFACT_CHECKED_REFERENCE_THUNKS bytes={EfiContracts.CheckedReferenceObjectBytes} text=1390 exports=3 callbackRel32=3 callbackGraphAccepted=false unwindSupported=false");
        }
        string[] dispatch = ["RhpInterfaceDispatch", "CLROS_InterfaceDispatch_GetCellSection", "CLROS_InterfaceDispatch_GetInitialStub", "CLROS_InterfaceDispatch_Resolve", "__InterfaceDispatchCellSection_Start"];
        if (!profile.IsEnabled("M04InterfaceDispatch"))
        {
            foreach (string name in dispatch.Take(4)) Require(Symbols(map, name).Count == 0, "dispatch_unrequested_native_symbol", name);
            Require(!response.Contains("CLROS.X64.InterfaceDispatch.obj", StringComparison.OrdinalIgnoreCase), "dispatch_unrequested_native_object");
        }
        else
        {
            byte[] obj = NativeObject("interfaceDispatch", ".dispatch.obj", "CLROS.X64.InterfaceDispatch.obj", EfiContracts.InterfaceDispatchObjectBytes, EfiContracts.InterfaceDispatchObjectSha256, "dispatch");
            ulong[] a = dispatch.Select((n, i) => Address(n, i < 3 ? "CLROS.X64.InterfaceDispatch.obj" : "OS.obj", "dispatch")).ToArray();
            Require(a[1] == a[0] + 448 && a[2] == a[0] + 464 && a[0] % 16 == 0 && a[4] % 8 == 0 && !(a[3] >= a[0] && a[3] < a[0] + 472), "dispatch_symbol_layout");
            CodeAddress(a[3], "dispatch_callback");
            // .NET 11 cells are BSS. Admit their mapped extent separately from
            // raw backing, and verify any file-backed bytes initialize to zero.
            ulong cellRva = a[4] - image.ImageBase;
            Require(a[4] >= image.ImageBase && cellRva < image.SizeOfImage &&
                80 <= image.SizeOfImage - cellRva, "dispatch_cell_out_of_image");
            var cellSections = image.Sections.Where(s => cellRva >= s.VirtualAddress &&
                cellRva + 80 <= (ulong)s.VirtualAddress + s.VirtualSize).ToArray();
            Require(cellSections.Length == 1, "dispatch_cell_ambiguous_or_absent");
            var cellSection = cellSections[0];
            Require((cellSection.Characteristics & 0xc0000000) == 0xc0000000 &&
                (cellSection.Characteristics & 0x22000020) == 0 &&
                (cellSection.Characteristics & 0xc0) != 0, "dispatch_cell_section_invalid");
            for (ulong i = 0; i < 80; i++)
            {
                ulong sectionOffset = cellRva + i - cellSection.VirtualAddress;
                if (sectionOffset < cellSection.RawSize)
                    Require(image.Bytes[checked((int)(cellSection.RawOffset + sectionOffset))] == 0, "dispatch_cell_not_zero");
            }
            byte[] text = BridgeText(a[0], 472, obj, [221, 451], "dispatch");
            Rel32(text, 221, a[0], a[3], "dispatch_callback_rel32_mismatch"); Rel32(text, 451, a[0], a[4], "dispatch_cell_rel32_mismatch");
            emit($"EFI_ARTIFACT_INTERFACE_DISPATCH_THUNKS bytes={EfiContracts.InterfaceDispatchObjectBytes} text=472 exports=3 callbackRel32=1 dataRel32=1 callbackGraphAccepted=false unwindSupported=false");
        }
        VerifyCatchBridge();
        VerifyRethrowBridges();
        VerifyNestedFinallyBridge();
        VerifyFrameDispatchBridge();
        VerifyEhCatalog();
        VerifyNormalFinallyBridge();
        VerifyConstructorFailureBridge();
        VerifyFilteredCatchBridge();
    }
    private void VerifyCatchBridge()
    {
        if (profile.IsEnabled("M04UnhandledException")) { VerifyUnhandledBridge(); return; }
        bool finallyProbe = profile.IsEnabled("M04SingleFinallyCatch"), simple = profile.IsEnabled("M04SimpleThrowCatch");
        List<string> names = ["RhpThrowEx", "CLROS_Eh_GetClauseTable", "CLROS_Eh_Select", "CLROS_Eh_ValidateContinuation", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Catcher"];
        if (finallyProbe) names.AddRange(["CLROS_Eh_ValidateFinally", "OS_OS_Runtime_RhpNewFastAllocation__Catcher", "??_7OS_OS_Runtime_SimpleCatchBase@@6B@"]);
        else Require(Symbols(map, "CLROS_Eh_ValidateFinally").Count == 0, "single_finally_unrequested_native_symbol");
        if (!finallyProbe) Require(!response.Contains("CLROS.X64.SingleFinallyCatch.obj", StringComparison.OrdinalIgnoreCase), "single_finally_unrequested_native_object");
        if (!simple) Require(!response.Contains("CLROS.X64.SimpleThrowCatch.obj", StringComparison.OrdinalIgnoreCase), "simple_catch_unrequested_native_object");
        if (!finallyProbe && !simple)
        {
            foreach (string name in names)
                if (name != "RhpThrowEx" || !(profile.IsEnabled("M04Rethrow") || profile.IsEnabled("M04NestedFinallyCatch") || profile.IsEnabled("M04FrameDispatch") || profile.IsEnabled("M04ConstructorFailure") || profile.IsEnabled("M04FilteredCatch") || profile.IsEnabled("M04NormalFinally")))
                    Require(Symbols(map, name).Count == 0, "simple_catch_unrequested_native_symbol", name);
            return;
        }
        int size = finallyProbe ? EfiContracts.SingleFinallyObjectBytes : EfiContracts.SimpleCatchObjectBytes, textSize = finallyProbe ? 1336 : 1032, getterOffset = finallyProbe ? 1328 : 1024;
        string objectName = finallyProbe ? "CLROS.X64.SingleFinallyCatch.obj" : "CLROS.X64.SimpleThrowCatch.obj";
        byte[] obj = NativeObject(finallyProbe ? "singleFinally" : "simpleCatch", finallyProbe ? ".finally.obj" : ".catch.obj", objectName, size, finallyProbe ? EfiContracts.SingleFinallyObjectSha256 : EfiContracts.SimpleCatchObjectSha256, "catch");
        ulong[] a = names.Select((n, i) => Address(n, i < 2 ? objectName : "OS.obj", "simple_catch")).ToArray();
        Require(a[0] % 16 == 0 && a[1] == a[0] + (ulong)getterOffset, "simple_catch_bridge_layout");
        int[] fields = finallyProbe ? [330, 609, 1105, 1331] : [330, 811, 1027], targets = finallyProbe ? [2, 5, 3, 4] : [2, 3, 4];
        byte[] text = BridgeText(a[0], textSize, obj, fields, "simple_catch");
        for (int i = 0; i < fields.Length; i++) Rel32(text, fields[i], a[0], a[targets[i]], "simple_catch_rel32_mismatch");
        foreach (int i in finallyProbe ? new[] { 2, 3, 5 } : new[] { 2, 3 })
        {
            Require(!(a[i] >= a[0] && a[i] < a[0] + (ulong)textSize), "simple_catch_callback_alias"); CodeAddress(a[i], "simple_catch_callback");
        }
        var table = image.Resolve(a[4] - image.ImageBase, finallyProbe ? 11UL : 8UL, "simple_catch_table_not_backed", "simple_catch_table_ambiguous");
        table.Section.RequireReadOnlyData("simple_catch_table_permissions");
        if (!finallyProbe) { emit($"EFI_ARTIFACT_SIMPLE_CATCH_THUNK bytes={EfiContracts.SimpleCatchObjectBytes} text=1032 callbackRel32=2 tableRel32=1 tableBytes=8 transferAccepted=false callbackStackAccepted=false"); return; }
        ulong catcher = a[6] - image.ImageBase, type = a[7] - image.ImageBase, tableRva = a[4] - image.ImageBase;
        Require(EfiVerify.TestSingleFinallyEhTable(image.Copy(table.Offset, 11, "single_finally_table_truncated"), type), "single_finally_clause_table_mismatch");
        image.Resolve(catcher, 136, "single_finally_catcher_not_backed", "single_finally_catcher_ambiguous").Section.RequireCode("single_finally_catcher_permissions");
        image.Resolve(type, 24, "single_finally_type_not_backed", "single_finally_type_ambiguous").Section.RequireReadOnlyData("single_finally_type_permissions");
        var rows = image.RuntimeFunctions.Where(f => f.Begin < catcher + 136 && f.End > catcher).ToArray();
        Require(rows.Length == 3, "single_finally_runtime_rows");
        uint[] starts = [0, 71, 94], ends = [71, 94, 136];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i]; Require(row.Begin == catcher + starts[i] && row.End == catcher + ends[i], "single_finally_runtime_row_shape");
            int length = i == 0 ? 15 : 7; var unwind = image.Resolve(row.Unwind, (ulong)length, "single_finally_unwind_not_backed", "single_finally_unwind_ambiguous");
            Require(EfiVerify.TestSingleFinallyUnwindBytes(image.Copy(unwind.Offset, length, "single_finally_unwind_truncated"), i == 0, tableRva), "single_finally_unwind_mismatch");
        }
        emit($"EFI_ARTIFACT_SINGLE_FINALLY_THUNK bytes={EfiContracts.SingleFinallyObjectBytes} text=1336 callbackRel32=3 tableRel32=1 tableBytes=11 clauses=2 runtimeRows=3 transferAccepted=false callbackStackAccepted=false");
    }
}
