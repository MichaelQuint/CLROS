using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Tooling;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Probe = OS.Runtime.NativeAotModuleRegistrationProbe;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;
internal static unsafe class TypeManagerCaptureHost
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static void Run(string name,Action test){Console.WriteLine("TEST_START m04.typemanager.host "+name);test();Console.WriteLine("TEST_PASS m04.typemanager.host "+name);}
    internal static int ExecuteCases()
    {
        Console.WriteLine("TEST_START m04.typemanager.host.tests");
        Run("actual adapter register slot-derived consumer and tail canaries",()=>{
            using Fixture f=new();
            Check(sizeof(NativeAotTypeManagerAbi)==48&&Core.StateBytes==272&&sizeof(Probe.Buffer)==4096,"actual geometry");
            Check(Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out var o),"genuine adapter execution "+o.Failure);
            Check(o.Image==(ulong)f.Image&&o.Storage==(ulong)f.Storage&&o.Handle==(ulong)f.Storage+272&&o.Slot==f.Address(0x2000),"actual pointer lineage");
            Check(o.Short==Status.StorageRejected&&o.Repeat==Status.AlreadyRegistered&&o.Foreign==Status.RegistrationChanged,"actual rejection statuses");
            Check(Probe.ReadWord(o.Slot)==o.Handle&&Probe.ReadWord(o.Slot+8)==0&&o.Abi.OsModule==o.Image&&o.Abi.ReadyToRunHeader==f.Address(0x1000),"slot ABI readback");
            Check(o.Thread.Section==f.Address(0x2100)&&o.Thread.Length==8&&o.Indirection.Section==o.Slot&&o.Indirection.Length==8&&o.Empty.Succeeded&&o.Empty.Length==0&&o.Absent.Status==Status.SectionAbsent,"actual consumer results");
            byte[] before=Bytes(f.Image,Fixture.ImageSize);Check(!Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out _)&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"second adapter cannot reset retained bytes");
            Console.WriteLine("TYPE_MANAGER_HOST_LAYOUT abi=48 state="+Core.StateBytes+" result="+sizeof(TypeManagerModuleResult)+" observation="+sizeof(Probe.Observation)+" capacity=4096 guardStart=4064");
        });
        Run("known mapped short buffer rejects without image or slot writes",()=>{
            using Fixture f=new();var valid=Core.Validate(f.Capture,f.Mapping);Check(valid.Succeeded&&valid.RequiredStorage<=4064,"actual required capacity");
            byte[] before=Bytes(f.Image,Fixture.ImageSize);var shortResult=Core.RegisterOnce(f.Capture,f.Mapping,f.Storage,valid.RequiredStorage-1);
            Check(shortResult.Status==Status.StorageRejected&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"controlled full image includes full slot and buffer");
            foreach(int selected in new[]{0,1,2,3,4}){
                byte* storage=selected==0?null:selected==1?f.Storage+1:selected==2?(byte*)(ulong.MaxValue-7):f.Storage;
                ulong capacity=selected==3?4095UL:4096UL;var capture=selected==4?default:f.Capture;
                Check(!Probe.Execute(capture,f.Mapping,storage,capacity,out _)&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"numeric rejection unchanged "+selected);
            }
            f.Storage[4095]=1;before=Bytes(f.Image,Fixture.ImageSize);
            Check(!Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out _)&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"nonzero known tail rejects before registration");
        });
        Run("both internal tail strips and retained padding are read back",()=>{
            using Fixture f=new();Check(Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out var o),"register");
            foreach(int index in new[]{(int)o.Required,4063,4064,4079,4080,4095}){
                byte saved=f.Storage[index];f.Storage[index]^=1;Check(!Probe.Preserved(f.Storage,o.Required),"changed tail detected "+index);f.Storage[index]=saved;Check(Probe.Preserved(f.Storage,o.Required),"unchanged restored tail");
            }
            byte[] before=Bytes(f.Image,Fixture.ImageSize);Check(Core.RegisterOnce(f.Capture,f.Mapping,f.Storage,4096).Status==Status.AlreadyRegistered&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"repeat all bytes unchanged");
            Check(Core.TryGetSection(f.Capture,f.Mapping,f.Storage,4096,o.Handle+8,204).Status==Status.RegistrationChanged&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"foreign all bytes unchanged");
        });
        Run("real metadata and published slot tampering cannot yield a section",()=>{
            for(int selected=0;selected<4;selected++){
                using Fixture f=new();Check(Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out var o),"register");
                if(selected==0)W64(f.Row(0)+8,f.Address(0x2110));
                if(selected==1)W64(f.Image+0x2000,o.Handle+8);
                if(selected==2)f.Storage[272]^=1;
                if(selected==3)f.Image[0x200C]=1;
                byte[] before=Bytes(f.Image,Fixture.ImageSize);Check(!Core.TryGetSection(f.Capture,f.Mapping,f.Storage,4096,o.Handle,204).Succeeded&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"tamper no writes "+selected);
            }
        });
        Console.WriteLine("TEST_PASS m04.typemanager.host.tests");return 0;
    }
    private static EfiLoadedImageRangeCapturePlan Capture(ulong address, ulong bytes, ulong handle = 0x1000)
    {
        var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, handle, 0x2000, address, bytes);
        return EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
    }

    private sealed class Fixture : IDisposable
    {
        internal const int ImageSize = 0x8000;
        internal const ulong Capacity = 4096;
        internal readonly byte* Image;
        internal byte* Storage => Image + 0x3000;
        internal EfiLoadedImageRangeCapturePlan Capture { get; }
        internal M01LoadedImageVirtualMappingEvidence Mapping { get; }

        internal Fixture()
        {
            Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
            if (Image == null) throw new OutOfMemoryException();
            NativeMemory.Clear(Image, ImageSize);
            W16(Image, 0x5A4D); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B); W32(Image + 0xD0, ImageSize); W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452); W16(Image + 0x1004, 16); W16(Image + 0x100C, 5);
            Image[0x100E] = 24; Image[0x100F] = 1;
            Row(0, 202, 1, 0x2100, 0x2108); Row(1, 204, 0, 0x2000, 0);
            Row(2, 206, 1, 0x1100, 0x1108); Row(3, 213, 1, 0x1110, 0x1110); Row(4, 301, 1, 0x1200, 0x1218);
            Capture = TypeManagerCaptureHost.Capture((ulong)Image, ImageSize);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte* Row(int i) => Image + 0x1010 + 24 * i;
        internal byte* Section(int i) => Image + 0x188 + 40 * i;
        private void Row(int i, uint id, uint flags, int start, int end)
        {
            byte* r = Row(i); W32(r, id); W32(r + 4, flags); W64(r + 8, Address(start)); W64(r + 16, end == 0 ? 0 : Address(end));
        }
        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* s = Section(i); for (int n = 0; n < name.Length; n++) s[n] = (byte)name[n];
            W32(s + 8, bytes); W32(s + 12, start); W32(s + 36, flags);
        }
        internal TypeManagerModuleResult Register() => Core.RegisterOnce(Capture, Mapping, Storage, Capacity);
        internal TypeManagerModuleResult Lookup(ulong handle, int id) => Core.TryGetSection(Capture, Mapping, Storage, Capacity, handle, id);
        public void Dispose() { NativeMemory.AlignedFree(Image); }
    }

    private static byte[] Bytes(byte* p, int length) => new ReadOnlySpan<byte>(p, length).ToArray();
    private static void W16(byte* p, ushort v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }
    private static void W32(byte* p, uint v) { W16(p, (ushort)v); W16(p + 2, (ushort)(v >> 16)); }
    private static void W64(byte* p, ulong v) { W32(p, (uint)v); W32(p + 4, (uint)(v >> 32)); }

}
internal static class M04NativeAotModuleRegistrationEfiIntegrationTests
{

    private const string Flag="EnableM04TypeManagerModuleRegistrationProbe";

    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m04-type-manager-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private sealed record Result(int Exit,bool Timeout,string Output,string Error);
    private static Result Invoke(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, false, result.Output, result.Error);
    }
    private static void Passed(Result r)=>Need(r.Exit==0&&!r.Timeout&&r.Error.Length==0,r.Output+r.Error);
    [HostTest("m04.011.efi conditional source and original M01 image authority", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var defaults=project.Descendants(Flag).ToArray();Need(defaults.Length==1&&defaults[0].Value=="false"&&(string?)defaults[0].Attribute("Condition")=="'$("+Flag+")' == ''","one false default");
        var define=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE",StringComparison.Ordinal));
        Need((string?)define.Parent?.Attribute("Condition")=="'$("+Flag+")' == 'true'"&&define.Value=="$(DefineConstants);M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE","exact symbol");
        var compile=project.Descendants("Compile").ToArray();Need(compile.Length==OS.Tooling.EfiContracts.CompileItems.Count,"actual complete source closure");
        foreach(string name in new[]{"..\\OS.Kernel\\Runtime\\TypeManagerModuleMetadataLiveNativeAOTImage.cs","Runtime\\NativeAotModuleRegistrationProbe.cs","..\\OS.Kernel\\Diagnostics\\TypeManagerModuleRegistrationProbeAuthority.cs"})Need(compile.Count(x=>(string?)x.Attribute("Include")==name)==1,"one real source "+name);
        Need(EfiSourceAdmission.Inspect(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Runtime/TypeManagerModuleMetadataLiveNativeAOTImage.cs")), @"..\OS.Kernel\Runtime\TypeManagerModuleMetadataLiveNativeAOTImage.cs").Succeeded,"reviewed registered-range core");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));
        foreach(string text in new[]{"TryGetOriginalTypeManagerImage","originalCapture = s_evidenceScratch.LoadedImageCapture","originalMapping = s_evidenceScratch.LoadedImageVirtualMapping","OS.Runtime.NativeAotModuleRegistrationProbe.Run(typeManagerCapture, typeManagerMapping"})Need(entry.Contains(text,StringComparison.Ordinal),"actual original authority "+text);
        Need(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/TypeManagerModuleRegistrationProbeAuthority.cs")).Contains("public const bool EnableTypeManagerModuleRegistrationProbe = false;",StringComparison.Ordinal),"disabled compile-time authority");
        string adapter=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/NativeAotModuleRegistrationProbe.cs"));
        foreach(string forbidden in new[]{"new byte[","throw ","string ","Marshal.","Reflection","delegate*","AllocatePool","AllocatePages","RhCreateTypeManager","RhGetModuleSection"})Need(!adapter.Contains(forbidden,StringComparison.Ordinal),"bounded adapter "+forbidden);
        foreach(string text in new[]{"valid.RequiredStorage - 1","storage, capacity","ulong consumerHandle = ReadWord(slot.Section)","consumerHandle, 202","consumerHandle, 204","consumerHandle, 213","consumerHandle, 0","MethodImplOptions.NoInlining"})Need(adapter.Contains(text,StringComparison.Ordinal),"genuine consumer "+text);
        Need(adapter.IndexOf("if (!registered.Succeeded",StringComparison.Ordinal)<adapter.IndexOf("storage[i] = Canary(i)",StringComparison.Ordinal),"only successful registration admits canary writes");
        var errors=project.Descendants("Error").Select(x=>(string)x.Attribute("Condition")!).ToArray();
        string[] flags=Regex.Matches(File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj")),@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"fifty-two actual flags");
        Need(errors.Count(x=>x=="'$("+Flag+")' != '' and '$("+Flag+")' != 'true' and '$("+Flag+")' != 'false'")==1,"exact invalid-value guard");
        Need(errors.Count(x=>x=="'$("+Flag+")' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'")==1,"exact original M01 prerequisite");
        Need(errors.Count(x=>x=="'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "exact unwind child original-module prerequisite");
        foreach(string other in flags.Where(x=>x!=Flag&&x!="EnableM01LoadedImageStackProbe"&&x!="EnableM04X64UnwindMetadataProbe")){
            if(other=="EnableM03FirstRealExitBootServicesProbe"||other=="EnableM03FirmwareAdapterRevocationProbe"||other=="EnableM03OwnedSubsystemSurvivalProbe"||other=="EnableM03OwnedSubsystemPanicProbe"||other=="EnableM05PhysicalMappingProbe"||other=="EnableM05OwnedAcpiSelectionProbe"||other=="EnableM05MonotonicClockProbe"){
                if(other=="EnableM03FirmwareAdapterRevocationProbe")
                    Need(errors.Count(x=>x=="'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'")==1,"exact inherited revocation prerequisite");
                if(other=="EnableM03OwnedSubsystemPanicProbe")
                    Need(errors.Count(x=>x=="'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'")==1,"exact inherited owned panic prerequisite");
                if(other=="EnableM03OwnedSubsystemSurvivalProbe"||other=="EnableM03OwnedSubsystemPanicProbe")
                    Need(errors.Count(x=>x=="'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'")==1,"exact inherited owned survival prerequisite");
                if(other=="EnableM05PhysicalMappingProbe") Need(errors.Count(x=>x=="'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')")==1,"exact inherited mapping prerequisite");
                if(other=="EnableM05OwnedAcpiSelectionProbe") Need(errors.Count(x=>x=="'$(EnableM05OwnedAcpiSelectionProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')")==1,"exact inherited ACPI prerequisite");
                Need(errors.Count(x=>x=="'$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '$("+Flag+")' == 'true'")==1,"exact incoming first-exit conflict");
                continue;
            }
            if(other=="EnableM04SeededCollectorChurnProbe"||other=="EnableM04ClassConstructorExecutionProbe"||other=="EnableM04InterfaceDispatchProbe"||other=="EnableM04SimpleThrowCatchProbe"||other=="EnableM04SingleFinallyCatchProbe"||other=="EnableM04RethrowProbe"){
                string gcChild=other;
                if(other=="EnableM04InterfaceDispatchProbe"){
                    Need(errors.Count(x=>x=="'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'")==1,"exact inherited dispatch prerequisite");
                    gcChild="EnableM04ClassConstructorExecutionProbe";
                }
                Need(errors.Count(x=>x=="'$("+gcChild+")' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'")==1
                    &&errors.Count(x=>x=="'$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '$("+Flag+")' == 'true'")==1
                    &&errors.Count(x=>x=="'$("+Flag+")' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'")==1,"exact inherited GC conflicts "+other);
                continue;
            }
            Need(errors.Contains("'$("+Flag+")' == 'true' and '$("+other+")' == 'true'",StringComparer.Ordinal)&&errors.Contains("'$("+other+")' == 'true' and '$("+Flag+")' == 'true'",StringComparer.Ordinal),"reciprocal conflict "+other);
        }
    }
    [HostTest("m04.011.efi actual mapped registration and slot-derived consumer", HostLane.Fast, 1)]
    private static void TestCapture() => Need(TypeManagerCaptureHost.ExecuteCases() == 0, "actual mapped adapter controls");

    [HostTest("m04.011.efi literal raw grammar and lifecycle controls", HostLane.Fast, 2)]
    private static void TestTranscript() => EfiTypeManagerTranscriptControls.Run(Scratch("transcript"), ProtocolLines);
    private static readonly string[] ProtocolLines = [
        "TEST_START efi.main",
        "EFI_CONSOLE_READY",
        "UEFI_MEMORY_MAP_READY",
        "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4",
        "UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED",
        "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY",
        "BOOT_MEMORY_REGION_COUNT 4",
        "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0",
        "BOOT_ACPI_RSDP_HANDOFF_READY",
        "UEFI_ACPI_RSDP_READY",
        "TEST_START m01.stage1",
        "M01_LOADED_IMAGE_CAPTURED",
        "M01_STACK_SWITCH_STUB_READY",
        "M01_STACK_ARENA_ALLOCATED",
        "M01_STACK_GUARD_RESERVED",
        "M01_STAGE1_MAP_RECAPTURED",
        "M01_STAGE1_RESERVATIONS_VALIDATED",
        "M01_OWNED_STACK_READY",
        "M01_OWNED_STACK_SWITCHED",
        "M01_RSP_CONTAINED",
        "TEST_PASS m01.stage1",
        "TEST_START m04.011",
        "CLROS_M04_011_IMAGE base=1048576 bytes=32768",
        "CLROS_M04_011_VALIDATE required=1024 storage=1060864 capacity=4096",
        "CLROS_M04_011_REJECT kind=capacity status=7 unchanged=true",
        "CLROS_M04_011_REGISTER slot=1056768 handle=1061136 osModule=1048576 header=1052672 gc=0 tls=1057024 slotBytes=16 index=0 padding=0 abiBytes=48 classlib=0 classlibCount=0",
        "CLROS_M04_011_SECTION id=202 status=1 start=1057024 length=8",
        "CLROS_M04_011_SECTION id=204 status=1 start=1056768 length=8",
        "CLROS_M04_011_SECTION id=213 status=1 start=1052944 length=0",
        "CLROS_M04_011_SECTION id=0 status=11 start=0 length=0",
        "CLROS_M04_011_REJECT kind=repeat status=9 unchanged=true",
        "CLROS_M04_011_REJECT kind=foreign status=10 unchanged=true",
        "CLROS_M04_011_READY modules=1 abiBytes=48 slotBytes=16 registered=true consumer=true tailCanaries=true",
        "TEST_PASS m04.011",
        "TEST_START boot.sequence",
        "BOOT_PHASE PhaseA.Audit START",
        "BOOT_PHASE PhaseA.Audit PASS",
        "BOOT_PHASE PhaseA.Diagnostics START",
        "BOOT_PHASE PhaseA.Diagnostics PASS",
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS"];
    [HostTest("m04.011.efi profile options and actual complete marker streamer", HostLane.Fast, 3)]
    private static void TestProfile()
    {
        string root=Root();
        Result Dry(params string[] args)=>Invoke(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", ..args]);
        string[] positive=["-BuildEfi","-"+Flag,"-EnableM01LoadedImageStackProbe"];
        Result result=Dry(positive);Passed(result);Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal),"actual dry-run terminal");
        string[] Vector(string prefix)=>result.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        var required=Vector("REQUIRED_MARKERS");var ordered=Vector("ORDERED_MARKERS");var unique=Vector("UNIQUE_MARKERS");var forbidden=Vector("FORBIDDEN_MARKERS");
        foreach(var vector in new[]{required,ordered,unique})Need(vector.Length==QemuContracts.M04TypeManagerRequiredMarkers.Count&&vector.Distinct(StringComparer.Ordinal).Count()==vector.Length,"canonical unique marker inventories");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal),StringComparer.Ordinal)&&ordered.SequenceEqual(unique,StringComparer.Ordinal),"actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines){var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",lines)+"\r\n")).Succeeded,"actual streamer Feed");return state.Complete();}
        var accepted=Stream(ProtocolLines);Need(accepted.Passed&&accepted.LinesProcessed==ProtocolLines.Length&&accepted.OrderedSeenCount==ordered.Length&&accepted.UniqueSeenCount==unique.Length,"genuine full-token streamer success");
        foreach(int index in Enumerable.Range(24,13)){
            var missing=Stream(ProtocolLines.Where((_,i)=>i!=index).ToArray());Need(!missing.Passed&&missing.MissingRequiredMarkers!.Count>0,"missing registration row");
            var duplicate=Stream(ProtocolLines.Take(index+1).Concat(ProtocolLines.Skip(index)).ToArray());Need(!duplicate.Passed&&duplicate.DuplicateUniqueOccurrenceCount==1,"duplicate registration row");
            string[] swapped=[..ProtocolLines];(swapped[index],swapped[index+1])=(swapped[index+1],swapped[index]);Need(Stream(swapped).OutOfOrderOccurrenceCount>0,"reordered registration row");
        }
        void Reject(string[] args,string reason){Result failure=Dry(args);Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"profile rejection "+reason);}
        string project=File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj"));string[] flags=Regex.Matches(project,@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"complete profile inventory");
        foreach(string other in flags.Where(x=>x!=Flag&&x!="EnableM01LoadedImageStackProbe"&&x!="EnableM04X64UnwindMetadataProbe"))
        {
            Result failure=Dry([..positive,"-"+other]);
            string diagnostic=failure.Output+failure.Error;
            Need(failure.Exit!=0&&!failure.Timeout&&diagnostic.Contains("TEST_FAIL qemu.smoke",StringComparison.Ordinal)&&
                (diagnostic.Contains("_profile_conflict",StringComparison.Ordinal)||diagnostic.Contains("_requires",StringComparison.Ordinal)||diagnostic.Contains("_profile_invalid",StringComparison.Ordinal)),
                "mutually exclusive profile rejected "+other+": "+diagnostic);
        }
        Reject(["-"+Flag,"-EnableM01LoadedImageStackProbe"],"m04_type_manager_requires_build_and_original_capture");
        Reject(["-BuildEfi","-"+Flag],"m04_type_manager_requires_original_loaded_image");
        Reject([..positive,"-SkipImageBuild"],"m04_type_manager_requires_build_and_original_capture");
        Reject([..positive,"-M01RawObservationLog",""],"m04_type_manager_requires_build_and_original_capture");
        Reject([..positive,"-RequireNativeAppMarkers"],"m04_type_manager_requires_build_and_original_capture");
        foreach(string option in new[]{"RequireMarker","RequireOrderedMarker","RequireUniqueMarker","ForbidMarker","MarkerPreset"})Reject([..positive,"-"+option,"CLROS_M04_011_READY"],"m04_type_manager_forbids_override");
        Reject(["-RequireMarker","CLROS_M04_011_READY"],"m04_type_manager_requires_explicit_profile");
        foreach(bool build in new[]{true,false})foreach(bool missingM01 in new[]{true,false})
        {
            string[] args=["-BootPolicy", "Diagnostic", "-"+Flag,..(missingM01?Array.Empty<string>():new[]{"-EnableM01LoadedImageStackProbe","-EnableM01LiveAllocatorProbe"})];
            var failure=HostToolProcess.ValidateEfiProfile(build,args);
            string reason=missingM01?"m04_type_manager_requires_original_loaded_image":"m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe";
            Need(failure.ExitCode!=0&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"closed producer/verifier "+build+" "+reason);
        }
        Passed(Dry());
    }
    [HostTest("m04.011.efi default and enabled no-stdlib artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue544-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Invoke(["efi", "build", "-BootPolicy", "Diagnostic","-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableM04TypeManagerModuleRegistrationProbe","-EnableM01LoadedImageStackProbe"}:Array.Empty<string>())]);Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&result.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"actual producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var evaluation=json.RootElement.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32()==compile,"actual evaluated Compile membership");
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())Need(string.Equals(Hash(Path.Combine(root,input.GetProperty("path").GetString()!)),input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"actual raw source identity");
            foreach(var property in evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)))
                Need(property.Value.GetString()==(enabled&&(property.Name==Flag||property.Name=="EnableM01LoadedImageStackProbe")?"true":"false"),"actual profile "+property.Name);
            using var stream=File.OpenRead(Path.Combine(directory,"OS.dll"));using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();Need(metadata.AssemblyReferences.Count==0,"zero AssemblyRefs");
            var types=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t=>metadata.GetString(t.Name)=="NativeAotModuleRegistrationProbe").ToArray();
            Need(types.Length==(enabled?1:0),"conditional registration type absent/default and present/enabled");
            if(enabled)foreach(string name in new[]{"Execute","ReadWord","ReadAbi","Zero","Preserved","Run"}){
                var methods=types[0].GetMethods().Where(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)==name).ToArray();Need(methods.Length==1&&metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress!=0,"actual registration method "+name);
            }
            string sourceDirectory=Path.Combine(directory,"source-inputs");Directory.CreateDirectory(sourceDirectory);
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray()){
                string relative=input.GetProperty("path").GetString()!,destination=Path.Combine(sourceDirectory,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(Path.Combine(root,relative),destination);
            }
            File.WriteAllText(Path.Combine(directory,"artifact-identities.json"),JsonSerializer.Serialize(Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray()));
        }
    }

}
