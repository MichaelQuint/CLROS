using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M04ManagedHeapPageRangeEfiIntegrationTests
{
    private const string Flag = "EnableM04ManagedHeapPageRangeProbe";
    private const string Leaf = "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs";
    private const string LeafHash = "43cdf5af449f7963f07b200ec494da3d8538f1ceb2f01fb3dbe9c29edebcb070";
    private const string Ready = "CLROS_M04_006_READY pages=1 bytes=4096 retained=true allocatorMutated=true zeroReadback=true checkpoint=beforeBootPlan hostConformance=false guestIntegrated=true objectAllocation=false gc=false hardwareNxProved=false postExitBootServices=false gate4=Fail";
    private static readonly string[] Prerequisites = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe"];

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m04.006.efi.integration");
        Run("frozen source and explicit project", TestProject);
        Run("actual enabled no-stdlib metadata", TestBuild);
        Run("actual adapter original authority and retained failures", TestAdapter);
        Run("profile prerequisites and collisions", TestProfiles);
        Run("schema-three complete evaluated probe vector", TestArtifactAuthority);
        Run("exact terminal numeric transcript", TestTranscript);
        Run("exact frozen source firewall", TestFirewall);
        Console.WriteLine("TEST_PASS m04.006.efi.integration");
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m04.006.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m04.006.efi " + name); }
        catch (Exception e) { Console.WriteLine("TEST_FAIL m04.006.efi " + name + " " + e.Message); throw; }
    }

    private static void TestProject()
    {
        string root = Root();
        XDocument project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        string[] keys = project.Descendants("Compile").Select(x => x.Attribute("Include")!.Value + "|" + (x.Attribute("Link")?.Value ?? "")).ToArray();
        Require(keys.Length == 124 && Hash(string.Join('\n', keys) + "\n") == "901575b1a6c8a4a766ed47c60579c997596769aa8b5a51132349ae62eedc1fe0", "ordered compile closure");
        Require(Hash(File.ReadAllText(Path.Combine(root, Leaf)).Replace("\r\n", "\n", StringComparison.Ordinal)) == LeafHash, "frozen leaf");
        Require(Hash(File.ReadAllText(Path.Combine(root, "tests/OS.HostTests/ManagedHeapPageRangeLiveAllocatorTests.cs")).Replace("\r\n", "\n", StringComparison.Ordinal)) == "aed8f7c32f80c5e600ed837e37c5f6de477324e57a9f9aa1ac0aedf43adf4bf6", "frozen host tests");
        XElement flag = project.Descendants(Flag).Single();
        Require(flag.Value == "false" && flag.Attribute("Condition")?.Value == "'$(" + Flag + ")' == ''", "default false");
        foreach (string p in Prerequisites)
            Require(project.Descendants("Error").Any(x => x.Attribute("Condition")?.Value == "'$(" + Flag + ")' == 'true' and '$(" + p + ")' != 'true'"), "explicit prerequisite " + p);
        string entry = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs"));
        int handoff = entry.IndexOf("if (!M04ManagedHeapAdapter.Checkpoint(true", StringComparison.Ordinal);
        Require(handoff > entry.IndexOf("if (!M01StageThreeAdapter.RunAfterStageTwo", StringComparison.Ordinal), "handoff follows all M01 consumers");
        Require(entry.Contains("fixed (ManagedHeapPageRangeState* heap = &s_heapState)", StringComparison.Ordinal), "stable owned slot");
        int later = entry.IndexOf("if (!M01StageTwoAdapter.ValidateManagedHeapBeforeBootPlan", StringComparison.Ordinal);
        Require(later > 0 && entry.IndexOf("EfiEntry.WriteBootSequencePlan(", later, StringComparison.Ordinal) > later, "later retention before boot plan");
        string adapter = Adapter(entry);
        Require(adapter.Contains("&state->AllocatorStateValue", StringComparison.Ordinal) && adapter.Contains("&state->FreeNodeValue", StringComparison.Ordinal), "original authority fields");
        foreach (string prohibited in new[] { "FreeOnePage", "FreePages", "AllocatedPageStart", "*ledger", "ledger[index] =", "*heap =", "*allocator =", "IsCompleted =" })
            Require(!adapter.Contains(prohibited, StringComparison.Ordinal), "no free/reset/replay " + prohibited);
        Require(!entry[handoff..entry.IndexOf("private static bool ValidateStageOneReservationStorage", handoff, StringComparison.Ordinal)].Contains("return Fail(", StringComparison.Ordinal), "no stage-two cleanup after reserve");
    }

    private static void TestBuild()
    {
        string root = Root();
        Result r = Process(root, "dotnet", ["build", "src/OS/OS.csproj", "-c", "Release", "--nologo", .. Prerequisites.Append(Flag).Select(x => "-p:" + x + "=true")]);
        Save("enabled-build", r); Require(r.Code == 0, r.Output);
        using FileStream input = File.OpenRead(Path.Combine(root, "src/OS/bin/Release/net10.0/win-x64/OS.dll"));
        using PEReader reader = new(input);
        MetadataReader metadata = reader.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "no framework/runtime assembly references");
        var method = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Single(m => metadata.GetString(m.Name) == "ReadFixedPage");
        Require((method.ImplAttributes & System.Reflection.MethodImplAttributes.NoInlining) != 0, "fresh readback execution identity");
    }

    private static void TestAdapter()
    {
        string root = Root(), directory = Scratch("adapter");
        string adapter = Adapter(File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")));
        XDocument sourceProject = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        string includes = string.Join('\n', sourceProject.Descendants("Compile")
            .Select(x => x.Attribute("Include")!.Value).Where(x => x.StartsWith("..", StringComparison.Ordinal))
            .Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.GetFullPath(Path.Combine(root, "src/OS", x))) + "\"/>"));
        File.WriteAllText(Path.Combine(directory, "Adapter.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
            <ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            <DefineConstants>NOSTDLIB_EFI</DefineConstants></PropertyGroup><ItemGroup><Compile Include="Program.cs"/>
            """ + includes + "</ItemGroup></Project>");
        string program = "using OS.Boot; using OS.Kernel.Memory; using OS.Kernel.Runtime; using System.Runtime.InteropServices;\nunsafe static class Program {\n" + """
            private struct EfiSimpleTextOutputProtocol { }
            private struct EfiSerialIoProtocol { }
            private static readonly System.Text.StringBuilder Output = new();
            private static void Write(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s, byte value) => Output.Append((char)value);
            private static void WriteUnsignedDecimal(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s, ulong value) => Output.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            private static int Fault, Calls, FaultCall;
            private static ManagedHeapPageRangeResult Invoke(bool initial, BootPageAllocatorState* a, ulong ac,
                BootPageFreeListNode* n, ulong nc, ulong ncap, BootReservedRange* l, ulong lc, ulong lcap,
                ulong index, byte* b, ulong bc, ManagedHeapPageRangeState* h, ulong hc)
            {
                ManagedHeapPageRangeResult r = initial
                    ? ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(a,ac,n,nc,ncap,l,lc,lcap,index,b,bc,h,hc)
                    : ManagedHeapPageRangeLiveAllocator.ValidateRetained(a,ac,n,nc,ncap,l,lc,lcap,index,b,bc,h,hc);
                if (++Calls != FaultCall) return r;
                if (Fault == 10) h->LifecycleValue = ManagedHeapPageRangeLifecycle.ReservedNotReady;
                if (Fault == 11) h->SelfAddressValue += 8;
                return new ManagedHeapPageRangeResult(Fault == 0 ? ManagedHeapPageRangeStatus.InvalidStorage : r.Status,
                    Fault == 1 ? default : r.Lifecycle, Fault == 2 ? r.PhysicalStart + 4096 : r.PhysicalStart,
                    Fault == 3 ? r.PhysicalEndExclusive + 4096 : r.PhysicalEndExclusive,
                    Fault == 4 ? r.MappedStart + 4096 : r.MappedStart,
                    Fault == 5 ? default : r.InitialAllocatorState, Fault == 6 ? default : r.CurrentAllocatorState,
                    Fault == 7 ? 4095UL : r.ReadBytes, Fault == 8 ? false : r.ContentsAreZeroNow,
                    r.AllocationStatus);
            }
            private static ManagedHeapPageRangeResult Reserve(BootPageAllocatorState* a, ulong ac, BootPageFreeListNode* n, ulong nc, ulong ncap, BootReservedRange* l, ulong lc, ulong lcap, ulong i, byte* b, ulong bc, ManagedHeapPageRangeState* h, ulong hc) => Invoke(true,a,ac,n,nc,ncap,l,lc,lcap,i,b,bc,h,hc);
            private static ManagedHeapPageRangeResult Retain(BootPageAllocatorState* a, ulong ac, BootPageFreeListNode* n, ulong nc, ulong ncap, BootReservedRange* l, ulong lc, ulong lcap, ulong i, byte* b, ulong bc, ManagedHeapPageRangeState* h, ulong hc) => Invoke(false,a,ac,n,nc,ncap,l,lc,lcap,i,b,bc,h,hc);
            private static BootReservedRange Range(BootReservationKind kind, ulong address, ulong pages)
            {
                PhysicalAddress.TryCreateFromUefiBoundary(address, out var p);
                if (BootReservedRange.TryCreate(kind,p,pages,out var range) != BootReservedRangeStatus.Success) throw new Exception("range");
                return range;
            }
            private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
            private static int Main()
            {
                byte* memory=(byte*)NativeMemory.AlignedAlloc(65536,4096);
                try {
                    var state=(M01LiveAllocatorStaticState*)memory;
                    var ledger=(BootReservedRange*)(memory+4096);
                    var record=(BootMemoryRegionRecord*)(memory+8192);
                    var heap=(ManagedHeapPageRangeState*)(memory+12288);
                    ulong start=(ulong)(memory+32768);
                    for (int test=0; test<50; test++) {
                        new Span<byte>(memory,65536).Clear();
                        new Span<byte>((void*)start,8192).Fill(0xA5);
                        Output.Clear(); Calls=0; FaultCall=0; Fault=0;
                        typeof(M04ManagedHeapAdapter).GetField("s_reservationObserved",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,false);
                        for(int i=0;i<4;i++) ledger[i]=Range((BootReservationKind)(i+1),0x100000UL+(ulong)i*4096,1);
                        ledger[4]=Range(BootReservationKind.AllocatorArena,start,2);
                        if(test>=48) {
                            int parentIndex=test==48?0:2;
                            for(int i=0,kind=1;i<5;i++) {
                                if(i==parentIndex) ledger[i]=Range(BootReservationKind.AllocatorArena,start,2);
                                else ledger[i]=Range((BootReservationKind)kind++,i<parentIndex?start-8192+(ulong)i*4096:start+8192+(ulong)(i-parentIndex-1)*4096,1);
                            }
                        }
                        *record=new BootMemoryRegionRecord(2,start,start,2,0,8192,start+8192);
                        state->HeaderSize=312; state->HeaderVersion=1; state->CoordinatorStatus=M01LiveAllocatorStatus.Success;
                        state->CoordinatorCheckpoint=M01LiveAllocatorCheckpoint.DoubleFreeRejected;
                        state->DoubleFreeStatusValue=BootPageAllocatorMutationStatus.FreeRangeNotAllocated;
                        state->StageOneStateAddressValue=(ulong)(memory+16384); state->StageOneStateCapacityValue=M01LoadedImageStackStaticState.RequiredStorageBytes;
                        state->RefreshedMapGenerationValue=3; state->ArenaReservationValue=Range(BootReservationKind.AllocatorArena,start,2);
                        BootReservationSourceRange.TryCreate(0,state->ArenaReservationValue.PhysicalStart,2,out var range);
                        state->ArenaSourceRangeValue=range;state->FreeRangeValue=range;
                        state->FreeNodeValue=new BootPageFreeListNode(0,0,start,start+8192,2,8192,-1);
                        state->AllocatorStateValue=new BootPageAllocatorState(true,4096,0,1,2,8192,0,0);
                        state->AllocatedPageStartValue=state->ArenaReservationValue.PhysicalStart;
                        PhysicalAddress.TryCreateFromUefiBoundary(start+4096,out state->AllocatedPageEndExclusiveValue);
                        state->InitialFreePagesValue=2;state->InitialFreeBytesValue=8192;
                        Check(state->IsCompleted,"actual completed input");
                        byte[] ledgerBefore=new ReadOnlySpan<byte>(ledger,200).ToArray();
                        if(test>=1 && test<=11) { FaultCall=1;Fault=test-1; }
                        if(test>=12 && test<=22) { FaultCall=2;Fault=test-12; }
                        // Fault 9 is intentionally unused: allocationStatus is only meaningful on rejection.
                        if(Fault==9) Fault=11;
                        if(test==23) state->HeaderVersion=0;
                        if(test==24) *record=new BootMemoryRegionRecord(7,start,start,2,0,8192,start+8192);
                        if(test==25) ledger[0]=default;
                        if(test==26) ledger[0]=ledger[4];
                        if(test==27) heap->LifecycleValue=(ManagedHeapPageRangeLifecycle)255;
                        if(test==46) heap->ReservationCountValue=99;
                        byte[] callLedger=new ReadOnlySpan<byte>(ledger,200).ToArray();
                        bool first=M04ManagedHeapAdapter.Checkpoint(true,state,record,ledger,test==47?null:heap,null,null);
                        if(test>=1 && test<=11 || test>=23 && test<=27 || test==46 || test==47) {
                            Check(!first,"initial fault accepted "+test);
                            Check(!Output.ToString().Contains("TEST_PASS m04.006"),"failed initial pass");
                            if(test>=46) Check(Output.ToString().Contains("reserved=unknown"),"unknown ownership is never false");
                        } else {
                            Check(first,"initial rejected "+test+" "+Output);
                            Check(!state->IsCompleted,"M01 completion must become historical");
                            Check(state->AllocatorStateValue.AllocatedPageCount==1,"original allocator not mutated");
                            Check(heap->AllocatorAddress==(ulong)&state->AllocatorStateValue && heap->NodesAddress==(ulong)&state->FreeNodeValue,"original identities");
                            for(int j=0;j<4096;j++) Check(memory[32768+j]==0 && memory[36864+j]==0xA5,"fixed one-page write");
                            if(test==28) memory[32768+4095]=1;
                            if(test==29) state->FreeNodeValue=new BootPageFreeListNode(0,0,start,start+8192,2,8192,-1);
                            if(test==30) heap->ParentIndexValue=0;
                            if(test==31) heap->BackingAddressValue+=4096;
                            if(test==32) heap->LifecycleValue=ManagedHeapPageRangeLifecycle.ReservedNotReady;
                            if(test==33) heap->LifecycleValue=(ManagedHeapPageRangeLifecycle)255;
                            if(test==34) heap->LedgerAddressValue+=40;
                            if(test==35) state->AllocatorStateValue=default;
                            if(test==36) *record=default;
                            if(test==37) ledger[0]=default;
                            if(test==38) heap->PhysicalEndValue+=4096;
                            if(test==39) heap->NodesAddressValue+=48;
                            if(test==40) heap->SelfAddressValue+=216;
                            var retainedHeap=test==41?null:heap;
                            var retainedState=state;
                            var retainedLedger=ledger;
                            if(test==42) { retainedState=(M01LiveAllocatorStaticState*)(memory+49152); *retainedState=*state; }
                            if(test==43) { retainedHeap=(ManagedHeapPageRangeState*)(memory+45056); *retainedHeap=*heap; }
                            if(test==44) { retainedLedger=(BootReservedRange*)(memory+53248); for(int j=0;j<5;j++)retainedLedger[j]=ledger[j]; }
                            if(test==45) retainedState=null;
                            callLedger=new ReadOnlySpan<byte>(ledger,200).ToArray();
                            bool second=M04ManagedHeapAdapter.Checkpoint(false,retainedState,record,retainedLedger,retainedHeap,null,null);
                            Check(second==(test==0 || test>=48),"retained decision "+test);
                            if(test!=0 && test<48) Check(!Output.ToString().Contains("TEST_PASS m04.006") && Output.ToString().Contains("reserved=true"),"retained failure ownership "+test);
                            else { Check(Output.ToString().Split("\r\n").Length==8,"seven complete heap lines"); if(test==0)Console.Write(Output); }
                        }
                        Check(new ReadOnlySpan<byte>(ledger,200).SequenceEqual(callLedger),"all 200 ledger bytes preserved");
                        if(test>=1 && test<=22) Check(state->AllocatorStateValue.AllocatedPageCount==1,"no free after reservation");
                    }
                    Console.WriteLine("TEST_PASS m04.006.efi.adapter controlled=50 ledgerBytes=200");
                    return 0;
                } finally { NativeMemory.AlignedFree(memory); }
            }
            """ + adapter.Replace("ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(", "Reserve(", StringComparison.Ordinal).Replace("ManagedHeapPageRangeLiveAllocator.ValidateRetained(", "Retain(", StringComparison.Ordinal) + "\n}\n";
        File.WriteAllText(Path.Combine(directory, "Program.cs"), program);
        Result result = Process(root, "dotnet", "run", "--project", Path.Combine(directory, "Adapter.csproj"), "-c", "Release");
        File.WriteAllText(Path.Combine(directory, "run.log"), result.Output);
        Require(result.Code == 0 && result.Output.Contains("controlled=50 ledgerBytes=200", StringComparison.Ordinal), result.Output);
        string[] lines = result.Output.Replace("\r", "").Split('\n').Where(x => x.StartsWith("M04_HEAP_", StringComparison.Ordinal)).ToArray();
        Require(lines.Select(x => x.Length).SequenceEqual(new[] { 298,246,327,118 }), "observed numeric record lengths");
    }

    private static void TestProfiles()
    {
        string root = Root();
        string[] enabled = Prerequisites.Append(Flag).Select(x => "-" + x).ToArray();
        Result good = Qemu(["-BuildEfi", .. enabled]); Save("dryrun",good); Require(good.Code == 0,good.Output);
        string[] ordered = Markers(good.Output,"ORDERED_MARKERS ");
        Require(ordered.Length==41 && Markers(good.Output,"REQUIRED_MARKERS ").Length==50 && Markers(good.Output,"UNIQUE_MARKERS ").Length==41,"41 anchors / 50 merged required / 52 strict lines");
        foreach (string missing in Prerequisites)
        foreach (bool explicitFalse in new[] {false,true})
        foreach (string script in new[] {"scripts/run-qemu-smoke.ps1","scripts/build-efi-shell.ps1","scripts/test-efi-artifact.ps1"})
        {
            string pre = script.Contains("run-qemu",StringComparison.Ordinal) ? "-DryRun -BuildEfi" : script.Contains("build-efi",StringComparison.Ordinal) ? "-PreflightOnly -LogPath artifacts/issue516-tests/rejected-preflight.log" : "-EfiArtifact missing.EFI";
            string command = "& '"+Path.Combine(root,script)+"' "+pre+" "+string.Join(' ',Prerequisites.Append(Flag).Where(x => explicitFalse || x!=missing).Select(x=>"-"+x+(x==missing?":$false":"")));
            Result r=Process(root,"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-Command",command);
            Require(r.Code!=0 && r.Output.Contains("m04_managed_heap_page_range_requires_explicit_chain",StringComparison.Ordinal),r.Output);
        }
        string[] collisions=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj")).Descendants("Error")
            .Select(x=>x.Attribute("Condition")?.Value??"").Where(x=>x.StartsWith("'$("+Flag+")'",StringComparison.Ordinal) && x.EndsWith("== 'true'",StringComparison.Ordinal))
            .Select(x=>Regex.Matches(x,@"\$\(([^)]+)\)")[1].Groups[1].Value).ToArray();
        Require(collisions.Length==21,"bounded collision list");
        foreach(string collision in collisions) {
            Result r=Qemu(["-BuildEfi",..enabled,"-"+collision]);
            string expectedDiagnostic=string.Equals(collision,"EnableM03TypedExitBootServicesCallProbe",StringComparison.Ordinal)
                ? "m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe"
                : string.Equals(collision,"EnableM04TypeManagerModuleRegistrationProbe",StringComparison.Ordinal)
                ? "m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe"
                : string.Equals(collision,"EnableM02UnexpectedVectorPanicProbe",StringComparison.Ordinal)
                ? "m02_unexpected_vector_panic_profile_conflict property=EnableM04IndependentEndianConformanceProbe"
                : string.Equals(collision,"EnableM03FirmwareEntropySeedProbe",StringComparison.Ordinal)
                    ? "m03_firmware_entropy_profile_conflict property=EnableM01LiveAllocatorProbe"
                    : string.Equals(collision,"EnableM03MemoryMapCaptureProbe",StringComparison.Ordinal)
                    ? "m03_memory_map_capture_profile_conflict property=EnableM01LoadedImageStackProbe"
                    : "m04_managed_heap_page_range_forbids_";
            Require(r.Code!=0 && r.Output.Contains(expectedDiagnostic,StringComparison.Ordinal),r.Output);
        }
        foreach(string[] extra in new[] {new[]{"-SkipImageBuild"},new[]{"-M01RawObservationLog","artifacts/issue516-tests/forbidden.json"}}) {
            Result r=Qemu(["-BuildEfi",..enabled,..extra]);Require(r.Code!=0,r.Output);
        }
        Require(Qemu(enabled).Code!=0,"requires BuildEfi");
        Result pageFault=Qemu(["-BuildEfi",..enabled,"-EnableM02PageFaultCr2ErrorCodeProbe"]);
        Require(pageFault.Code!=0 && pageFault.Output.Contains("m02_page_fault_cr2_error_code_profile_conflict",StringComparison.Ordinal),"page-fault/heap reciprocal collision "+pageFault.Output);
        Result sibling=Qemu(["-BuildEfi",..enabled.Where(x=>x!="-"+Flag)]);Require(sibling.Code==0 && Markers(sibling.Output,"ORDERED_MARKERS ").Length==6,"standalone layout preserved");
        Require(Qemu(["-RequireMarker",Ready]).Code!=0,"marker-only spoof rejected");
    }

    private static void TestTranscript()
    {
        string root=Root(), directory=Scratch("serial");
        string source=File.ReadAllText(Path.Combine(root,"scripts/run-qemu-smoke.ps1"));
        string defs=Between(source,"$script:M04IndependentEndianConformanceReadyMarker =","function Invoke-NativeCommand {");
        string functions=Between(source,"function Assert-M04ManagedHeapPageRangeTranscript {","function Assert-M04LayoutContractTranscript {");
        string[] chain=Markers(Qemu(["-BuildEfi",..Prerequisites.Append(Flag).Select(x=>"-"+x)]).Output,"ORDERED_MARKERS ");
        List<string> lines=["TEST_START efi.main",..chain.Take(6),"UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3","UEFI_MEMORY_MAP_OWNED","BOOT_HANDOFF_READY","UEFI_MEMORY_MAP_NORMALIZED","BOOT_MEMORY_MAP_ACCEPTED","BOOT_MEMORY_REGIONS_READY","BOOT_MEMORY_REGION_COUNT 3","BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0","BOOT_ACPI_RSDP_HANDOFF_READY","UEFI_ACPI_RSDP_READY",..chain.Skip(6)];
        const string identity="allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")]+=" "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")]+=" checkpoint=beforeBootPlan "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")]+=" pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")]+=" freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        Require(lines.Count==52,"full success lines");
        string good=string.Join("\r\n",lines)+"\r\n";
        List<(string Text,bool Accept)> cases=[(good,true),("\u001b[2J\u001b[HUEFI Interactive Shell\r\n"+good,true),(good+"\r\n",true)];
        for(int i=0;i<lines.Count;i++) {
            foreach(string replacement in new[]{"",lines[i]+" "," "+lines[i],lines[i]+" diagnostic",lines[i]+"\0",lines[i]+"\t",lines[i]+"\uFEFF",lines[i].ToLowerInvariant()})
                cases.Add((good.Replace(lines[i]+"\r\n",replacement+"\r\n",StringComparison.Ordinal),false));
            cases.Add((good.Replace(lines[i]+"\r\n",lines[i]+"\r\n"+lines[i]+"\r\n",StringComparison.Ordinal),false));
        }
        foreach(string tail in new[]{"OVMF menu\r\n","partial","\r","OS_SMOKE_PASS\r\n","M04_HEAP_PAGE_RANGE_FAILED\r\n","TEST_FAIL m04.006\r\n"}) cases.Add((good+tail,false));
        foreach(string bad in new[]{"TEST_FAIL m04.006\r\n","prefixCLROS_M04_006_READY\r\n","M\0\uFEFF04_HEAP_PAGE_BOUND\r\n","TE\r\nST_FA\r\nIL firmware\r\n","OS_SMO\r\nKE_FA\r\nIL\r\n","TE\r\nST_START efi.main\r\n"}) cases.Add((bad+good,false));
        cases.Add((good.Replace("\r\n","\n",StringComparison.Ordinal),false));cases.Add((good[..^1],false));
        foreach((string old,string changed) in new[]{("parentIndex=4","parentIndex=5"),("pages=1 ","pages=01 "),("readBytes=4096","readBytes=4095"),("contentsZero=true","contentsZero=false"),("nodes=0x0000000000100040","nodes=0x0000000000100030"),("ledger=0x0000000000102000","ledger=0x0000000000901000"),("heapState=0x0000000000101000","heapState=0xFFFFFFFFFFFFFFF8"),("conventionalPages=20","conventionalPages=18446744073709551616"),("conventionalBytes=81920","conventionalBytes=81921"),("regionCount 3","regionCount 4"),("UEFI_ACPI_RSDP_READY","UEFI_ACPI_RSDP_MISSING"),("BOOT_ACPI_RSDP_HANDOFF_READY","BOOT_ACPI_RSDP_HANDOFF_INVALID")}) {
            string changedText=good.Replace(old,changed,StringComparison.Ordinal); if(changedText!=good) cases.Add((changedText,false));
        }
        foreach(Match m in Regex.Matches(identity,@"[a-zA-Z]+=(?:0x[0-9A-F]{16}|[0-9]+)")) {
            string bad=m.Value.EndsWith('0')?m.Value[..^1]+"1":m.Value[..^1]+"0";
            int at=good.IndexOf(m.Value,StringComparison.Ordinal);cases.Add((good[..at]+bad+good[(at+m.Length)..],false));
        }
        var records=cases.Select((c,i)=>new{Path=Path.Combine(directory,i+".log"),c.Accept}).ToArray();
        for(int i=0;i<cases.Count;i++) File.WriteAllText(records[i].Path,cases[i].Text,new UTF8Encoding(false));
        string manifest=Path.Combine(directory,"cases.json");File.WriteAllText(manifest,System.Text.Json.JsonSerializer.Serialize(records));
        string ps=Path.Combine(directory,"Test.ps1");
        File.WriteAllText(ps,"$ErrorActionPreference='Stop'\n"+defs+functions+"\n$cases=Get-Content -Raw -LiteralPath '"+manifest+"' | ConvertFrom-Json\n"+"""
            foreach($case in $cases) {
                $accepted=$true; $reason=''
                try { Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $case.Path } catch { $accepted=$false; $reason=$_.Exception.Message }
                if($accepted -ne $case.Accept) { throw ($case.Path+' accepted='+$accepted+' reason='+$reason) }
            }
            Assert-M04ManagedHeapRunnerResult @('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1','SERIAL_MARKER_DRAIN milliseconds=750')
            foreach($exitCode in @('0','1','-2147483648','2147483647')) {
                Assert-M04ManagedHeapRunnerResult @(('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exitCode),'SERIAL_MARKER_DRAIN milliseconds=750')
            }
            foreach($exitCode in @('none','','2147483648','-2147483649','99999999999999999999','-0','00','01','-01','+1',' 1','1 ')) {
                $rejected=$false;try { Assert-M04ManagedHeapRunnerResult @(('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exitCode),'SERIAL_MARKER_DRAIN milliseconds=750') } catch {$rejected=$true}
                if(-not $rejected){throw ('invalid native exit '+$exitCode)}
            }
            foreach($bad in @('exited','cleanup-failed','running')) {
                $rejected=$false;try { Assert-M04ManagedHeapRunnerResult @(('QEMU_PROCESS_RESULT disposition='+$bad+' exitCode=0'),'SERIAL_MARKER_DRAIN milliseconds=750') } catch {$rejected=$true}
                if(-not $rejected){throw 'terminal disposition'}
            }
            Write-Output ('TEST_PASS m04.006.efi.serial cases='+$cases.Count)
            """);
        Result result=Process(root,"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",ps);File.WriteAllText(Path.Combine(directory,"run.log"),result.Output);Require(result.Code==0,result.Output);
    }

    private static void TestArtifactAuthority()
    {
        string root=Root(), directory=Scratch("authority");
        // Reuse the repository's controlled PE/schema-three fixture builder. Strip
        // only its entry-point attribute so this runs the two selected scenarios.
        string fixture=File.ReadAllText(Path.Combine(root,"tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs"));
        File.WriteAllText(Path.Combine(directory,"Fixture.cs"),fixture.Replace("[ModuleInitializer]","",StringComparison.Ordinal));
        File.Copy(Path.Combine(root,"tests/OS.HostTests/HostToolProcess.cs"),Path.Combine(directory,"HostToolProcess.cs"));
        File.WriteAllText(Path.Combine(directory,"ProbeVector.ps1"),"""
            param([string]$Verifier,[string]$EvidencePath)
            $ErrorActionPreference='Stop'
            $tokens=$null;$errors=$null
            $ast=[System.Management.Automation.Language.Parser]::ParseFile($Verifier,[ref]$tokens,[ref]$errors)
            foreach($definition in $ast.FindAll({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true)) {
                . ([scriptblock]::Create($definition.Extent.Text))
            }
            # Controlled failure sink; the actual comparator and its validators run.
            function Write-Failure { param([string]$Reason,[string]$Detail) throw ($Reason+' '+$Detail) }
            $json=[IO.File]::ReadAllText($EvidencePath)
            $original=($json | ConvertFrom-Json).postRestoreEvaluation
            $properties=[ordered]@{}
            foreach($property in $original.properties.PSObject.Properties){$properties[$property.Name]=$property.Value}
            $snapshot=($json | ConvertFrom-Json).postRestoreEvaluation
            $snapshot.properties=$properties
            Assert-PostRestoreEvidenceMatchesSnapshot -Evidence $original -Snapshot $snapshot
            $names=@($properties.Keys | Where-Object {$_.StartsWith('Enable',[StringComparison]::Ordinal)})
            if($names.Count -ne 52){throw 'complete vector'}
            foreach($name in $names) {
                $changed=($json | ConvertFrom-Json).postRestoreEvaluation
                $changed.properties.$name=if($properties[$name] -eq 'true'){'false'}else{'true'}
                $rejected=$false
                try {Assert-PostRestoreEvidenceMatchesSnapshot -Evidence $changed -Snapshot $snapshot} catch {$rejected=$true}
                if(-not $rejected){throw ('flag flip '+$name)}
            }
            foreach($kind in @('missing','nul')) {
                $changed=($json | ConvertFrom-Json).postRestoreEvaluation
                if($kind -eq 'missing'){$changed.properties.PSObject.Properties.Remove('EnableM01LoadedImageStackProbe')}
                else {$changed.properties.EnableM01LiveAllocatorProbe='true'+[char]0}
                $rejected=$false
                try {Assert-PostRestoreEvidenceMatchesSnapshot -Evidence $changed -Snapshot $snapshot} catch {$rejected=$true}
                if(-not $rejected){throw $kind}
            }
            Write-Output 'TEST_PASS m04.006.efi.authority flags=54 flipped=54 missing=1 embeddedNul=1'
            """);
        File.WriteAllText(Path.Combine(directory,"Authority.csproj"),"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
            <LangVersion>14.0</LangVersion><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>
            """);
        File.WriteAllText(Path.Combine(directory,"Program.cs"),"""
            using System.Reflection;
            using System.Text.Json.Nodes;
            const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
            Type type=typeof(EfiArtifactVerificationScriptTests);
            object Call(string name,params object?[] values)=>type.GetMethod(name,Flags)!.Invoke(null,values)!;
            string path=Path.Combine(Environment.CurrentDirectory,"artifacts/issue516-tests/authority-fixtures",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            if(!(bool)Call("TestValidArtifactPasses",path))throw new Exception("default artifact");
            if(!(bool)Call("TestM02Ud2VectorStateSingleSwitchImplication",path))throw new Exception("legacy UD2 artifact");
            byte[] pe=(byte[])Call("CreateValidPe",(uint)0x1100);
            string artifact=(string)Call("WriteFixture",path,"heap",pe);
            object paths=Call("WriteLinkEvidence",artifact,null);
            string objectPath=(string)paths.GetType().GetProperty("ObjectPath")!.GetValue(paths)!;
            string evidencePath=artifact+".link.json";
            JsonObject evidence=JsonNode.Parse(File.ReadAllText(evidencePath))!.AsObject();
            JsonObject properties=evidence["postRestoreEvaluation"]!["properties"]!.AsObject();
            string[] enabled=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe"];
            foreach(string flag in enabled)properties[flag]="true";
            string[] vector=properties.Select(x=>x.Key).Where(x=>x.StartsWith("Enable",StringComparison.Ordinal)).ToArray();
            if(vector.Length!=54)throw new Exception("complete 54-probe vector");
            string[] verifierArgs=enabled.Select(x=>"-"+x).ToArray();
            void Verify(bool success,string context) {
                File.WriteAllText(evidencePath,evidence.ToJsonString());
                object result=Call("RunVerifier",artifact,null,null,objectPath,verifierArgs);
                int code=(int)result.GetType().GetProperty("ExitCode")!.GetValue(result)!;
                string output=(string)result.GetType().GetProperty("Output")!.GetValue(result)!;
                File.WriteAllText(Path.Combine(path,context+".log"),output);
                if((code==0)!=success)throw new Exception(context+" "+output);
                if(!success && !output.Contains("post_restore_evidence_property_",StringComparison.Ordinal))throw new Exception("wrong rejection "+context+" "+output);
            }
            Verify(true,"heap-positive");
            properties[enabled[0]]="false";Verify(false,"flipped-prerequisite");properties[enabled[0]]="true";
            File.WriteAllText(evidencePath,evidence.ToJsonString());
            // The positive cases above prove fresh complete verifier evaluation;
            // exercise each manifest edit directly against its actual comparator.
            var start=new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
            foreach(string arg in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(AppContext.BaseDirectory,"../../../ProbeVector.ps1"),"-Verifier",Path.Combine(HostToolProcess.FindRepositoryRoot(),"scripts/test-efi-artifact.ps1"),"-EvidencePath",evidencePath})start.ArgumentList.Add(arg);
            using var process=System.Diagnostics.Process.Start(start)!;
            string output=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();process.WaitForExit();
            Console.Write(output);if(process.ExitCode!=0)throw new Exception("probe vector comparator");
            """);
        Result r=Process(root,"dotnet","run","--project",Path.Combine(directory,"Authority.csproj"),"-c","Release");
        File.WriteAllText(Path.Combine(directory,"run.log"),r.Output);
        Require(r.Code==0 && r.Output.Contains("flags=54 flipped=54",StringComparison.Ordinal),r.Output);
    }

    private static void TestFirewall()
    {
        string directory=Scratch("firewall"), ps=Path.Combine(directory,"Test.ps1");
        File.WriteAllText(ps,"""
            param([string]$Root)
            $ErrorActionPreference='Stop'
            $tokens=$null;$errors=$null
            $ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $Root 'scripts/build-efi-shell.ps1'),[ref]$tokens,[ref]$errors)
            foreach($name in @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')) {
                $definition=$ast.Find({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true)
                . ([scriptblock]::Create($definition.Extent.Text))
            }
            $name='..\OS.Kernel\Runtime\ManagedHeapPageRangeLiveAllocator.cs'
            $text=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs')).Replace("`r`n","`n")
            foreach($valid in @($text,$text.Replace("`n","`r`n"))) {if(-not (Test-M01ActiveSourceSurface $valid $name).Succeeded){throw 'exact source'}}
            foreach($bad in @(($text+' '),($text+"`r"),$text.Replace('4096','4095'),($text+"`n// edit"))) {if((Test-M01ActiveSourceSurface $bad $name).Succeeded){throw 'mutated source'}}
            foreach($bad in @($name.ToLowerInvariant(),($name+' '),$name.Replace('\','/'),$name.Replace('Runtime\','Runtime\.\'),$name.Insert(5,[string][char]0),'..\OS.Kernel\Runtime\Other.cs')) {if((Test-M01ActiveSourceSurface $text $bad).Succeeded){throw 'path alias'}}
            Write-Output 'TEST_PASS m04.006.efi.firewall'
            """);
        Result r=Process(Root(),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",ps,"-Root",Root());Save("firewall",r);Require(r.Code==0,r.Output);
    }

    private static string Adapter(string text) => Between(text,"    private static class M04ManagedHeapAdapter","#endif");
    private static string Between(string text,string start,string end) {int i=text.IndexOf(start,StringComparison.Ordinal);Require(i>=0,start);return text[i..text.IndexOf(end,i,StringComparison.Ordinal)];}
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static Result Qemu(params string[] args)=>Process(Root(),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),"-BootPolicy", "Diagnostic", "-DryRun",..args]);
    private static string[] Markers(string output,string prefix)=>output.Replace("\r","").Split('\n').Single(x=>x.StartsWith(prefix,StringComparison.Ordinal))[prefix.Length..].Split(';');
    private static Result Process(string root,string exe,params string[] args)
    {
        ProcessStartInfo start=new(exe){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in args)start.ArgumentList.Add(arg);
        using System.Diagnostics.Process process=System.Diagnostics.Process.Start(start)!;
        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(180000)){process.Kill(true);throw new TimeoutException(exe);}
        Task.WaitAll(stdout,stderr);return new(process.ExitCode,stdout.Result+stderr.Result);
    }
    private static void Save(string name,Result r)=>File.WriteAllText(Path.Combine(Scratch(name),"run.log"),r.Output);
    private static string Scratch(string name){string path=Path.Combine(Root(),"artifacts/issue516-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private static string Root(){for(DirectoryInfo? d=new(Environment.CurrentDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"CLROS.slnx")))return d.FullName;throw new DirectoryNotFoundException();}
    private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private readonly record struct Result(int Code,string Output);
}
