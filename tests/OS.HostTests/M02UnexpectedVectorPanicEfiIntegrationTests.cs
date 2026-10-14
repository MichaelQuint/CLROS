using OS.Tooling;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;

internal static class M02UnexpectedVectorPanicEfiIntegrationTests
{
    private const string Flag = "EnableM02UnexpectedVectorPanicProbe";
    private static readonly string[] Prerequisites=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM01OwnedPagingProbe","EnableM02SafeStubProbe","EnableM02CheckedPortIoBindingProbe","EnableM02Com1OwnedPortIoProbe","EnableM02OwnedCom1ReadyProbe","EnableM02PanicSafeOwnedCom1Probe"];

    [HostTest("m02.016.efi admitted cleanup authority", HostLane.Native, 1)]
    private static void TestCleanup()
    {
        string root = Root();
        string directory = Path.Combine(root, "artifacts/issue526-tests/cleanup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs"));
        int start = source.IndexOf("    private static class M02UnexpectedVectorPanicAdapter", StringComparison.Ordinal);
        Require(start >= 0, "actual panic adapter");
        string adapter = source[start..source.IndexOf("#endif", start, StringComparison.Ordinal)];
        string[] selected = ["private struct Authority", "internal static bool Extent(",
            "internal static bool Disjoint(", "private static bool Temporary(", "internal static bool TemporaryStorage(",
            "internal static bool Lease(", "internal static byte CleanupPlan(",
            "private static void FailBeforeEntry(", "private static void Failure("];
        string run = Member(adapter, "internal static void Run(");
        bool TemporaryAdmissionFirst(string body) =>
            body.IndexOf("ulong* selectors = stackalloc", StringComparison.Ordinal) >= 0 &&
            body.IndexOf("ulong* selectors = stackalloc", StringComparison.Ordinal) < body.IndexOf("!TemporaryStorage(", StringComparison.Ordinal) &&
            body.IndexOf("!TemporaryStorage(", StringComparison.Ordinal) < body.IndexOf("services->AllocatePages(", StringComparison.Ordinal) &&
            body.Split("FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);", StringSplitOptions.None).Length == 7;
        Require(TemporaryAdmissionFirst(run), "same temporary exclusions admitted before allocations and retained by six cleanup calls");
        Require(!TemporaryAdmissionFirst(run.Replace("!TemporaryStorage(", "!OtherStorage(", StringComparison.Ordinal)), "missing preallocation admission rejected");
        Require(!TemporaryAdmissionFirst(run.Replace(", observationAddress, scratchAddress, selectorsAddress);", ", 0, scratchAddress, selectorsAddress);", StringComparison.Ordinal)), "changed cleanup temporary authority rejected");
        string members = string.Join("\n", selected.Select(name => Member(adapter, name)))
            .Replace("private struct Authority", "internal struct Authority", StringComparison.Ordinal)
            .Replace("private static void FailBeforeEntry(", "internal static void FailBeforeEntry(", StringComparison.Ordinal);
        string constants = string.Join("\n", adapter.Split('\n').Where(line =>
            line.Contains("private const ulong Page =", StringComparison.Ordinal) ||
            line.Contains("private const ulong LowEnd =", StringComparison.Ordinal)));
        Require(constants.Contains("DataBytes = 28672;", StringComparison.Ordinal), "actual C2/D7 capacities");
        // Compile complete actual cleanup methods, not Run, Query, or native entry.
        // The sole replacement boundary records FreePages arguments and returns a
        // controlled EFI status. It never dereferences or releases these integers.
        File.WriteAllText(Path.Combine(directory, "Adapter.cs"), """
            namespace OS.Boot;
            public static unsafe class EfiEntry
            {
                public struct EfiSimpleTextOutputProtocol { }
                public struct EfiSerialIoProtocol { }
                public struct EfiBootServices { public delegate* unmanaged<ulong, ulong, ulong> FreePages; }
                internal static readonly System.Text.StringBuilder Captured = new();
                private static void Write(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s, byte b) => Captured.Append((char)b);
                internal static class M02UnexpectedVectorPanicAdapter
                {
                    internal static Authority s_authority;
            """ + constants + "\n" + members + "\n}\n}\n");
        File.WriteAllText(Path.Combine(directory, "Program.cs"), """
            using System.Runtime.InteropServices;
            using OS.Boot;
            using Adapter = OS.Boot.EfiEntry.M02UnexpectedVectorPanicAdapter;
            unsafe static class Program
            {
                private static readonly List<(ulong Address, ulong Pages)> Calls = [];
                private static ulong CodeStatus, DataStatus;
                [UnmanagedCallersOnly]
                private static ulong Free(ulong address, ulong pages)
                {
                    Calls.Add((address, pages));
                    return pages == 2 ? CodeStatus : DataStatus;
                }
                private static void Check(bool value, string reason)
                { if (!value) throw new InvalidOperationException(reason); }
                private static int Main()
                {
                    const ulong c = 0x100000, d = 0x200000, image = 0x400000, imageBytes = 0x10000, io = 0x500000;
                    const ulong observation = 0x600000, scratch = 0x700000, selectors = 0x800000;
                    var rejected = new List<(ulong C, bool CSuccess, ulong D, bool DSuccess)>
                    {
                        (c, false, d, true), (c, true, c, true),
                        (c, true, c + 4096, true), (c, true, c - 4096, true),
                        (c, true, c - 6 * 4096, true)
                    };
                    foreach (ulong invalid in new[] { 0UL, ulong.MaxValue - 4095, 0x0000800000000000UL,
                        c + 1, image, image - 4096, io, io - 4096, observation, scratch, selectors,
                        observation - 4096, scratch - 4096, selectors - 4096 })
                    {
                        rejected.Add((invalid, true, 0, false));
                        rejected.Add((c, true, invalid, true));
                    }
                    foreach (var row in rejected)
                        Check(Adapter.CleanupPlan(row.C, row.CSuccess, row.D, row.DSuccess, image, imageBytes, io, observation, scratch, selectors) == 0,
                            "invalid successful response grants no release authority");
                    Check(Adapter.CleanupPlan(c, false, d, false, image, imageBytes, io, observation, scratch, selectors) == 0, "no successful allocation");
                    Check(Adapter.CleanupPlan(c, true, ulong.MaxValue, false, image, imageBytes, io, observation, scratch, selectors) == 1,
                        "failed second allocation cannot invalidate admitted C");
                    Check(Adapter.CleanupPlan(c, true, c + 8192, true, image, imageBytes, io, observation, scratch, selectors) == 3,
                        "adjacent admitted leases are disjoint");
                    foreach (var exclusions in new[] { (0UL, imageBytes, io), (image, 0UL, io),
                        (image, ulong.MaxValue, io), (image, imageBytes, 0UL), (image, imageBytes, io + 1) })
                        Check(Adapter.CleanupPlan(c, true, d, true, exclusions.Item1, exclusions.Item2, exclusions.Item3, observation, scratch, selectors) == 0,
                            "invalid original exclusion authority fails closed");

                    EfiEntry.EfiBootServices services = default; services.FreePages = &Free;
                    foreach (var row in rejected)
                    {
                        Reset();
                        Adapter.FailBeforeEntry(null, null, null, row.C, row.CSuccess, row.D, row.DSuccess, image, imageBytes, io, observation, scratch, selectors);
                        Check(Calls.Count == 0 && Adapter.s_authority.FreeAttemptMask == 0, "no invalid/foreign/overlapping free");
                        byte retained = (byte)((row.CSuccess ? 1 : 0) | (row.DSuccess ? 2 : 0));
                        Check(Adapter.s_authority.RetainedMask == retained, "every ambiguous raw claim retained");
                        Check(Adapter.s_authority.FreeCodeStatus == ulong.MaxValue && Adapter.s_authority.FreeDataStatus == ulong.MaxValue,
                            "no invented free status");
                        Check(EfiEntry.Captured.ToString() == "TEST_FAIL m02.016 preparation_failed\r\n", "no successful cleanup claim");
                    }
                    ulong[] temporary = [observation, scratch, selectors];
                    ulong[] sizes = [56, 96, 24];
                    for (int i = 0; i < temporary.Length; i++)
                    {
                        ulong original = temporary[i];
                        foreach (ulong invalid in new[] { 0UL, ulong.MaxValue - 7, original + 1, image, io,
                            temporary[(i + 1) % 3], c + 8192 - 8, d + 28672 - 8 })
                        {
                            temporary[i] = invalid;
                            foreach (bool secondSuccess in new[] { false, true })
                            {
                                if (!secondSuccess && invalid == d + 28672 - 8) continue;
                                Reset();
                                Check(Adapter.CleanupPlan(c, true, d, secondSuccess, image, imageBytes, io,
                                    temporary[0], temporary[1], temporary[2]) == 0, "invalid/partial temporary overlap is retained");
                                Adapter.FailBeforeEntry(null, null, null, c, true, d, secondSuccess, image, imageBytes, io,
                                    temporary[0], temporary[1], temporary[2]);
                                Check(Calls.Count == 0 && Adapter.s_authority.FreeAttemptMask == 0 &&
                                    Adapter.s_authority.RetainedMask == (secondSuccess ? 3 : 1), "temporary rejection never calls FreePages");
                            }
                        }
                        temporary[i] = c + 8192;
                        Check(Adapter.CleanupPlan(c, true, d, true, image, imageBytes, io,
                            temporary[0], temporary[1], temporary[2]) == 3, "exact C end adjacency allowed");
                        temporary[i] = d - sizes[i];
                        Check(Adapter.CleanupPlan(c, true, d, true, image, imageBytes, io,
                            temporary[0], temporary[1], temporary[2]) == 3, "exact D start adjacency allowed");
                        temporary[i] = original;
                    }
                    foreach (ulong cStatus in new[] { 0UL, 1UL, 0x8000000000000007UL })
                    foreach (ulong dStatus in new[] { 0UL, 2UL, 0x8000000000000009UL })
                    foreach (bool secondSuccess in new[] { false, true })
                    {
                        Reset(); CodeStatus = cStatus; DataStatus = dStatus;
                        Adapter.FailBeforeEntry(null, null, &services, c, true, d, secondSuccess, image, imageBytes, io, observation, scratch, selectors);
                        var expected = secondSuccess ? new[] { (d, 7UL), (c, 2UL) } : new[] { (c, 2UL) };
                        Check(Calls.SequenceEqual(expected), "actual reverse admitted frees exactly once");
                        Check(Adapter.s_authority.State == 4 && Adapter.s_authority.FreeAttemptMask == (secondSuccess ? 3 : 1),
                            "actual attempt mask and terminal preparation state");
                        Check(Adapter.s_authority.FreeCodeStatus == cStatus &&
                            Adapter.s_authority.FreeDataStatus == (secondSuccess ? dStatus : ulong.MaxValue), "exact returned statuses");
                        byte retained = (byte)((cStatus != 0 ? 1 : 0) | (secondSuccess && dStatus != 0 ? 2 : 0));
                        Check(Adapter.s_authority.RetainedMask == retained, "failed release remains retained");
                        Adapter.FailBeforeEntry(null, null, &services, c, true, d, secondSuccess, image, imageBytes, io, observation, scratch, selectors);
                        Check(Calls.SequenceEqual(expected), "reentry never frees again");
                    }
                    Console.WriteLine("M02_UNEXPECTED_CLEANUP_CONTROL status=passed integerOnly=true nativeEntry=false realEfiFree=false");
                    return 0;
                }
                private static void Reset()
                {
                    Calls.Clear(); EfiEntry.Captured.Clear(); CodeStatus = DataStatus = 0;
                    Adapter.s_authority = default; Adapter.s_authority.State = 1;
                    Adapter.s_authority.FreeCodeStatus = Adapter.s_authority.FreeDataStatus = ulong.MaxValue;
                }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Cleanup.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework>" +
            "<LangVersion>14.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>" +
            "<ItemGroup><ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/>" +
            "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS/Boot/M02CheckedPortIoFunctionPointerIntegration.cs")) + "\"/>" +
            "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS/Boot/M02SafeStubPageIntegration.cs")) + "\"/></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(directory, "source-selection.json"), JsonSerializer.Serialize(new
        {
            Source = "src/OS/Boot/EfiEntry.cs",
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")))) ,
            CompleteMembers = selected, WholeAdapterExecution = false, GuestExecution = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        ProcessStartInfo info = new("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "run", "--project", Path.Combine(directory, "Cleanup.csproj"), "-c", "Release" })
            info.ArgumentList.Add(argument);
        DateTime started = DateTime.UtcNow;
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(info)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        bool timedOut = !process.WaitForExit(120000);
        if (timedOut) { process.Kill(true); process.WaitForExit(); }
        Task.WaitAll(output, error);
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output.Result);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), error.Result);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
        { StartedUtc = started, EndedUtc = DateTime.UtcNow, ExitCode = process.ExitCode, TimedOut = timedOut, Arguments = info.ArgumentList.ToArray() }));
        if (timedOut) throw new TimeoutException("cleanup controlled harness; output/result preserved in " + directory);
        Require(process.ExitCode == 0 && error.Result.Length == 0, output.Result + error.Result);
        Require(output.Result.Contains("M02_UNEXPECTED_CLEANUP_CONTROL status=passed integerOnly=true nativeEntry=false realEfiFree=false", StringComparison.Ordinal),
            "complete cleanup controls terminal");
    }

    private static string Transcript()
    {
        const ulong c=0x100000, d=0x200000, target=c+512;
        string[] names=["c","d","context","callback","captureRsp","firmwareCs","cs","ss","ds","es","tr","gdtrBase","gdtrLimit","idtrBase","idtrLimit","tssAccess","gateLow","gateHigh","normalizer0","normalizer1","trigger","cr3","entryCr3","lar"];
        ulong[] values=[c,d,d+4096,0x400000,d+28488,0x38,8,16,16,16,24,d+4120,39,d,4095,0x8b,
            (target&65535)|(8UL<<16)|(0x8eUL<<40)|((target&0xffff0000)<<32),target>>32,
            0x00002068006a006a,0x909000000df2e900,c+5056,0x300000,0x300000,0xa09b00];
        string metadata="M02_UNEXPECTED_STATE "+string.Join(' ',names.Select((name,i)=>name+"=0x"+values[i].ToString("X16")))+"\r\n";
        Require(Encoding.ASCII.GetByteCount(metadata)==650,"independent 650-byte metadata record");
        string[] registers=["rip","rsp","rbp","rflags","cr2","cr3","rax","rbx","rcx","rdx","rsi","rdi","r8","r9","r10","r11","r12","r13","r14","r15"];
        ulong[] captured=[c+5058,d+28672,0x10203040,0x46,0,0x300000,24,0x1234,0xa09b00,d+4096,0x5678,0x9abc,0x300000,0x3456,0x789a,0xbcde,0x1357,0x2468,0x369c,0x48ad];
        string fault="OS_SMOKE_FAIL subsystem=idt reason=unexpected-vector vector=32 errorCodePresent=false errorCode=0x0000000000000000 "+string.Join(' ',registers.Select((name,i)=>name+"=0x"+captured[i].ToString("X16")))+"\r\n";
        return QemuTerminalControls.Prefix()+metadata+fault;
    }

    [HostTest("m02.016.efi source profile and original authority", HostLane.Fast, 0)]
    private static void TestProject()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var items=project.Descendants("Compile").ToArray();Require(items.Length==OS.Tooling.EfiContracts.CompileItems.Count,"complete exact compiler inputs");
        foreach(string name in new[]{"UnexpectedVectorPanicProbeAuthority.cs","UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.cs"})
            Require(items.Count(x=>x.Attribute("Include")?.Value=="..\\OS.Kernel\\Diagnostics\\"+name && x.Attribute("Link")?.Value=="Diagnostics\\"+name)==1,"one linked declaration "+name);
        var defaults=project.Descendants().Where(x=>x.Attribute("Condition")?.Value=="'$("+x.Name.LocalName+")' == ''" && x.Name.LocalName.StartsWith("Enable",StringComparison.Ordinal)).ToArray();
        Require(defaults.Select(x=>x.Name.LocalName).Order(StringComparer.Ordinal).SequenceEqual(EfiContracts.ProjectDefaultProbes.Order(StringComparer.Ordinal)) && defaults.All(x=>x.Value=="false") && defaults.Count(x=>x.Name.LocalName==Flag)==1,"complete literal false defaults");
        foreach(string addedFlag in new[]{"EnableM03FirstRealExitBootServicesProbe","EnableM04SeededCollectorChurnProbe","EnableM03FirmwareAdapterRevocationProbe","EnableM04ClassConstructorExecutionProbe","EnableM04InterfaceDispatchProbe","EnableM04X64UnwindMetadataProbe","EnableM03OwnedSubsystemSurvivalProbe","EnableM03OwnedSubsystemPanicProbe","EnableM04SimpleThrowCatchProbe","EnableM04SingleFinallyCatchProbe"})
            Require(defaults.Count(x=>x.Name.LocalName==addedFlag && x.Value=="false")==1,"one literal false default "+addedFlag);
        // The M09 profiles own separate aggregate exclusion categories;
        // retain every original pairwise condition and check M09 in its fixtures.
        var errors=project.Descendants("Error").Where(x=>
            !(x.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_004_",StringComparison.Ordinal) &&
            !(x.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_005_",StringComparison.Ordinal) &&
            !(x.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_006_",StringComparison.Ordinal) &&
            !(x.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_009_",StringComparison.Ordinal) &&
            !(x.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_010_",StringComparison.Ordinal) &&
            x.Attribute("Condition")!.Value.Contains("$("+Flag+")",StringComparison.Ordinal)).ToArray();
        Require(errors.Length==76,"eight prerequisites, both directions of 33 conflicts, one first-exit conflict and normal-policy conflict");
        Require(errors.Count(x=>x.Attribute("Condition")?.Value=="'$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '$(EnableM02UnexpectedVectorPanicProbe)' == 'true'")==1,"exact incoming first-exit conflict");
        foreach(string prerequisite in Prerequisites)Require(errors.Count(x=>x.Attribute("Condition")?.Value=="'$("+Flag+")' == 'true' and '$("+prerequisite+")' != 'true'")==1,"exact prerequisite "+prerequisite);
        string[] conflicts=Conflicts(project);Require(conflicts.Length==33,"exact conflict vector");
        foreach(string conflict in conflicts)foreach((string a,string b) in new[]{(Flag,conflict),(conflict,Flag)})
            Require(errors.Count(x=>x.Attribute("Condition")?.Value=="'$("+a+")' == 'true' and '$("+b+")' == 'true'")==1,"reciprocal conflict "+a+"/"+b);
        HostCheck.Registered(typeof(UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassificationTests));
        foreach(var pair in new[]{("src/OS.Kernel/Diagnostics/UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.cs",EfiContracts.UnexpectedPanicSourceSha256),
            ("src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs",OS.Tooling.EfiContracts.AllocationSourceSha256)})
            Require(Hash(File.ReadAllText(Path.Combine(root,pair.Item1)).Replace("\r\n","\n",StringComparison.Ordinal)).Equals(pair.Item2,StringComparison.OrdinalIgnoreCase),"reviewed normalized source "+pair.Item1);
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs")),adapter=Adapter(entry);
        string run=Member(adapter,"internal static void Run("),callback=Member(adapter,"private static uint Callback("),prepare=Member(adapter,"internal static bool Prepare(");
        int original=run.IndexOf("TryGetUnexpectedPanicBinding",StringComparison.Ordinal),temporary=run.IndexOf("!TemporaryStorage(",StringComparison.Ordinal),allocation=run.IndexOf("services->AllocatePages(",StringComparison.Ordinal),scratch=run.IndexOf("!ScratchBuffers(",StringComparison.Ordinal),query=run.IndexOf("!Query(",StringComparison.Ordinal),make=run.IndexOf("!Prepare(",StringComparison.Ordinal),sentinel=run.IndexOf("PlaceSentinel(",StringComparison.Ordinal),publish=run.IndexOf("X64InstructionPublication.Serialize()",StringComparison.Ordinal),entered=run.IndexOf("s_authority.State = 2;",StringComparison.Ordinal);
        Require(original>=0&&temporary>original&&allocation>temporary&&scratch>allocation&&query>scratch&&make>query&&sentinel>make&&publish>sentinel&&entered>publish,"actual authority/admission/query/preparation/sentinel/publication order");
        Require(!run[entered..].Contains("FreePages",StringComparison.Ordinal)&&!run[entered..].Contains("FailBeforeEntry",StringComparison.Ordinal)&&run[entered..].Contains("Spin();",StringComparison.Ordinal),"terminal ownership");
        Require(callback.IndexOf("!CallbackArguments(",StringComparison.Ordinal)<callback.IndexOf("!CheckSentinel(",StringComparison.Ordinal)&&callback.Contains("s_authority.Request",StringComparison.Ordinal)&&!callback.Contains("(byte*)capture",StringComparison.Ordinal)&&!callback.Contains("(byte*)context",StringComparison.Ordinal),"original callback authority before dereference");
        Require(prepare.IndexOf(".PrepareContext(",StringComparison.Ordinal)<prepare.IndexOf(".EmitStream(",StringComparison.Ordinal)&&prepare.IndexOf(".EmitStream(",StringComparison.Ordinal)<prepare.IndexOf(".ValidateStreamReadBack(",StringComparison.Ordinal),"unchanged leaf preparation sequence");
        Require(adapter.Contains("[System.Runtime.InteropServices.UnmanagedCallersOnly]",StringComparison.Ordinal)&&adapter.Contains("MethodImplOptions.NoInlining",StringComparison.Ordinal)&&!adapter.Contains("TEST_PASS",StringComparison.Ordinal),"scalar callback no premature guest pass");
        string authority=File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/UnexpectedVectorPanicProbeAuthority.cs"));
        Require(authority.Contains("public const bool EnableUnexpectedVectorPanicProbe = false;",StringComparison.Ordinal),"probe defaults false");
    }
    private static string[] Conflicts(XDocument project)=>project.Descendants("Error").Select(x=>x.Attribute("Condition")!.Value)
        .Where(x=>x.StartsWith("'$("+Flag+")' == 'true' and '",StringComparison.Ordinal)&&x.EndsWith(" == 'true'",StringComparison.Ordinal))
        .Select(x=>Regex.Matches(x,@"\$\(([^)]+)\)")[1].Groups[1].Value).ToArray();

    [HostTest("m02.016.efi actual adapter data-only preparation", HostLane.Native, 2)]
    private static void TestAdapter()
    {
        string root=Root(),dir=Scratch("adapter");string source=Adapter(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs")));
        string[] names=["internal static bool CallbackArguments(","internal static bool Prepare(","internal static bool Extent(","internal static bool Disjoint(","private static bool Temporary(","internal static bool TemporaryStorage(","internal static bool ScratchBuffers(","internal static bool Mode(","internal static byte SelectorQueryByte(","private static void PlaceSentinel(","internal static bool CheckSentinel("];
        string constants=string.Join('\n',source.Split('\n').Where(x=>x.Contains("private const",StringComparison.Ordinal)));
        string actual=string.Join('\n',names.Select(x=>Member(source,x))).Replace("private static void PlaceSentinel(","internal static void PlaceSentinel(",StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(dir,"Adapter.cs"),"unsafe static class Adapter {\n"+constants+"\n"+actual+"\n}");
        File.WriteAllText(Path.Combine(dir,"Program.cs"),"""
            using System.Runtime.InteropServices;
            using OS.Kernel.Memory;
            using OS.Kernel.Diagnostics;
            unsafe static class Program
            {
                static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
                static X64PagingModePreflightObservation Mode(ulong cr0=0x80010033,ulong cr4=0x620,ulong efer=0xd00,ulong flags=2,uint width=48,ulong cr3=0x400000)
                    =>X64PagingModePreflightObservation.CreateObserved(cr0,cr3,cr4,efer,0x80000008,width,flags);
                static int Main()
                {
                    byte* bytes=(byte*)NativeMemory.AlignedAlloc(9*4096,4096);
                    try {
                        ulong c=(ulong)bytes,d=c+8192;Check(c>uint.MaxValue,"full-width actual backing");
                        var request=new UnexpectedVectorPanicRequest(c,d,0x400000,0x10000,0x400100,0x38,0x300000,true);
                        Check(Adapter.Prepare(c,8192,d,28672,request),"actual Prepare/Emit/full ReadBack");
                        Check(UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.ValidateStreamReadBack(bytes,8192,(byte*)d,28672,request).Succeeded,"unchanged actual leaf");
                        foreach(int index in new[]{0,4095,4096,4608,5056,8191,8192,12288,20480,36863}) {
                            bytes[index]^=1;Check(!UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.ValidateStreamReadBack(bytes,8192,(byte*)d,28672,request).Succeeded,"each backing role readback "+index);bytes[index]^=1;
                        }
                        byte[] pristine=new ReadOnlySpan<byte>(bytes,36864).ToArray();
                        foreach(ulong capacity in new ulong[]{0UL,8191,8193,ulong.MaxValue})Check(!Adapter.Prepare(c,capacity,d,28672,request),"exact code capacity");
                        foreach(ulong capacity in new ulong[]{0UL,28671,28673,ulong.MaxValue})Check(!Adapter.Prepare(c,8192,d,capacity,request),"exact data capacity");
                        Check(!Adapter.Prepare(c+4096,8192,d,28672,request)&&!Adapter.Prepare(c,8192,d+4096,28672,request),"original address equality");
                        Check(new ReadOnlySpan<byte>(bytes,36864).SequenceEqual(pristine),"capacity rejection writes no bytes");
                        Adapter.PlaceSentinel(d);Check(Adapter.CheckSentinel(d),"16-byte bottom sentinel");
                        for(int i=0;i<16;i++){((byte*)d)[20480+i]^=1;Check(!Adapter.CheckSentinel(d),"all sentinel bytes checked");((byte*)d)[20480+i]^=1;}
                        Check(Adapter.CallbackArguments(2,d,28672,d+28488,d+4096),"only retained capture and context");
                        foreach(byte state in new byte[]{0,1,3,4,255})Check(!Adapter.CallbackArguments(state,d,28672,d+28488,d+4096),"one-shot callback state");
                        Check(!Adapter.CallbackArguments(2,d,28671,d+28488,d+4096)&&!Adapter.CallbackArguments(2,d,28672,d+28480,d+4096)&&!Adapter.CallbackArguments(2,d,28672,d+28488,d+4097),"exact callback capacity/arguments");
                        Check(Adapter.Mode(Mode(),0x38,0xa09b00,0x07000000),"FXSR SSE SSE2 admitted mode");
                        Check(Adapter.Mode(Mode(flags:0x202),0x38,0xa09b00,0x07000000),"pre-CLI IF is admitted");
                        foreach(var bad in new[]{Mode(cr0:0x80010037),Mode(cr0:0x8001003b),Mode(cr4:0x220),Mode(cr4:0x420),Mode(cr4:0x20620),Mode(cr4:0x1620),Mode(efer:0x500),Mode(flags:0x102),Mode(flags:0x402),Mode(width:35),Mode(width:53),Mode(cr3:0),Mode(cr3:0x400001),default})Check(!Adapter.Mode(bad,0x38,0xa09b00,0x07000000),"unsupported observed mode");
                        foreach(ulong cs in new[]{0UL,3UL,0x3cUL,0x10008UL})Check(!Adapter.Mode(Mode(),cs,0xa09b00,0x07000000),"CPL/TI selector");
                        foreach(ulong lar in new[]{0UL,0x809b00UL,0xa0fb00UL,0x100a09b00UL})Check(!Adapter.Mode(Mode(),8,lar,0x07000000),"long present ring-zero code");
                        foreach(ulong features in new[]{0UL,0x06000000UL,0x05000000UL,0x03000000UL,0x107000000UL})Check(!Adapter.Mode(Mode(),8,0xa09b00,features),"actual SIMD feature support");
                        byte[] oracle=Convert.FromHexString("F30F1EFA31C0668CC84889010F02D0751A48895108534989C8B8010000000FA2498950105BB801000000C331C0C3");
                        Check(oracle.Length==46&&oracle.Select((b,i)=>b==Adapter.SelectorQueryByte(i)).All(x=>x)&&Adapter.SelectorQueryByte(-1)==0&&Adapter.SelectorQueryByte(46)==0,"unexecuted exact selector/CPUID bytes");
                        Console.WriteLine("M02_UNEXPECTED_ADAPTER_CONTROL status=passed emittedBytesNeverExecuted=true callbackNeverInvoked=true");return 0;
                    } finally{NativeMemory.AlignedFree(bytes);}
                }
            }
            """);
        File.WriteAllText(Path.Combine(dir,"Adapter.csproj"),"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(dir,"selected-members.json"),JsonSerializer.Serialize(new{SourceSha256=Hash(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"))),Members=names,RunOrQueryExecuted=false}));
        Result result=Process(root,"dotnet","run","--project",Path.Combine(dir,"Adapter.csproj"),"-c","Release");Require(result.Code==0&&result.Error.Length==0,result.Output+result.Error);
    }

    [HostTest("m02.016.efi strict complete panic transcript", HostLane.Fast, 3)]
    private static void TestTranscript()
    {
        string dir=Scratch("transcript"),valid=Transcript();var cases=new List<(string Text,bool Accept)> { (valid,true) };
        byte[] preamble=Convert.FromBase64String("G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==");
        Require(Convert.ToHexString(SHA256.HashData(preamble))==QemuTranscripts.FirmwarePrefixSha256,"fixed OVMF prefix identity");
        cases.Add((Encoding.ASCII.GetString(preamble)+valid,true));
        preamble[0]^=1;cases.Add((Encoding.ASCII.GetString(preamble)+valid,false));
        int recordCount=QemuContracts.M02UnexpectedCaptureRecordCount,prefixCount=QemuContracts.M02TerminalPrefix.Count;
        string[] lines=valid.Split("\r\n");Require(lines.Length==recordCount+1,"complete owned record inventory");
        for(int i=0;i<recordCount;i++) {
            var copy=lines.ToList();copy.RemoveAt(i);cases.Add((string.Join("\r\n",copy),false));
            copy=lines.ToList();copy.Insert(i,lines[i]);cases.Add((string.Join("\r\n",copy),false));
            foreach(string suffix in new[]{" ","\0","\u200b"}){copy=lines.ToList();copy[i]+=suffix;cases.Add((string.Join("\r\n",copy),false));}
        }
        foreach(string suffix in new[]{"\r\n","\0","OS_SMOKE_PASS\r\n","TEST_FAIL late\r\n","firmware return\r\n"})cases.Add((valid+suffix,false));
        foreach(string prefix in new[]{"arbitrary\r\n","\0","TE\r\nST_FAIL\r\n","\ufeff"})cases.Add((prefix+valid,false));
        foreach(string ending in new[]{"\n","\r"})cases.Add((valid.Replace("\r\n",ending,StringComparison.Ordinal),false));
        for(int row=prefixCount;row<recordCount;row++)foreach(Match field in Regex.Matches(lines[row],@" ([A-Za-z0-9]+)=0x([0-9A-F]{16})")) {
            string key=field.Groups[1].Value;bool diagnostic=row==prefixCount+1&&new[]{"rbp","cr2","rbx","rsi","rdi","r9","r10","r11","r12","r13","r14","r15"}.Contains(key,StringComparer.Ordinal);
            var copy=(string[])lines.Clone();copy[row]=copy[row][..field.Index]+" "+key+"=0xFFFFFFFFFFFFFFFF"+copy[row][(field.Index+field.Length)..];cases.Add((string.Join("\r\n",copy),diagnostic));
            copy=(string[])lines.Clone();copy[row]=copy[row][..field.Index]+field.Value+"0"+copy[row][(field.Index+field.Length)..];cases.Add((string.Join("\r\n",copy),false));
        }
        foreach(ulong bit in new[]{1UL,4UL,16UL,64UL,128UL,2048UL})cases.Add((valid.Replace("rflags=0x0000000000000046","rflags=0x"+(0x46UL^bit).ToString("X16"),StringComparison.Ordinal),false));
        foreach(var change in new[]{("vector=32","vector=33"),("errorCodePresent=false","errorCodePresent=true"),("DESCRIPTOR_COUNT 2","DESCRIPTOR_COUNT 3"),("freeBytes=40960","freeBytes=40961")})cases.Add((valid.Replace(change.Item1,change.Item2,StringComparison.Ordinal),false));
        var records=cases.Select((x,i)=>new{Path=Path.Combine(dir,i+".log"),x.Accept}).ToArray();for(int i=0;i<cases.Count;i++)File.WriteAllText(records[i].Path,cases[i].Text,new UTF8Encoding(false));
        string malformed=Path.Combine(dir,"malformed-utf8.log");File.WriteAllBytes(malformed,[0xc0,0xaf,..Encoding.ASCII.GetBytes(valid)]);
        records=[..records,new{Path=malformed,Accept=false}];
        File.WriteAllText(Path.Combine(dir,"cases.json"),JsonSerializer.Serialize(records));
        foreach(var record in records) {
            bool accepted=true;string reason=string.Empty;
            try { QemuTranscripts.AssertM02UnexpectedVectorPanicTranscript(record.Path); }
            catch(ToolFailure error) { accepted=false;reason=error.Message; }
            Require(accepted==record.Accept,record.Path+" accepted="+accepted+" "+reason);
            string text=Encoding.UTF8.GetString(File.ReadAllBytes(record.Path));
            int status=UnexpectedVectorPanicCapture.Evaluate(text,out _);
            Require((status==1)==record.Accept,"runner parser "+record.Path+" status="+status);
            if(status==1)for(int n=0;n<text.Length;n++)Require(UnexpectedVectorPanicCapture.Evaluate(text[..n],out _)>=0,"valid partial "+n);
        }
    }

    [HostTest("m02.016.efi profile propagation and rejection", HostLane.Fast, 4)]
    private static void TestProfiles()
    {
        string root=Root();Result baseline=Qemu();Require(baseline.Code==0&&!baseline.Output.Contains("--expect-unexpected-vector-panic",StringComparison.Ordinal),baseline.Output);
        Result enabled=Qemu("-BuildEfi","-"+Flag);Require(enabled.Code==0&&enabled.Output.Contains("M02_UNEXPECTED_STATE",StringComparison.Ordinal),enabled.Output);
        IReadOnlyList<string>? forwarded=null;
        int dryResult=QemuSmoke.RunAsync(root,QemuSmokeOptions.Parse(["--dry-run","--build-efi","--boot-policy","Diagnostic","-"+Flag]),TextWriter.Null,
            (name,args)=> { Require(name=="OS.QemuRunner","dry-run invokes the runner");forwarded=args;return Task.FromResult(new OS.Tooling.ProcessResult(0,"","",false,false,false,TimeSpan.Zero)); }).GetAwaiter().GetResult();
        Require(dryResult==0&&forwarded is not null&&forwarded.Count(x=>x=="--expect-unexpected-vector-panic")==1,"exact typed runner option forwarding");
        foreach(string[] args in new[]{new[]{"-"+Flag},new[]{"-BuildEfi","-"+Flag,"-SkipImageBuild"},new[]{"-BuildEfi","-"+Flag,"-M01RawObservationLog","artifacts/forbidden.log"},new[]{"-RequireMarker","M02_UNEXPECTED_STATE"},new[]{"-BuildEfi","-"+Flag,"-MarkerPreset","acpi"}})Require(Qemu(args).Code!=0,"invalid typed panic profile");
        foreach(string endpoint in new[]{"qemu","build","verify"}) {
            foreach(string prerequisite in Prerequisites){Result r=Profile(endpoint,["-"+Flag,"-"+prerequisite+"=false"]);Require(r.Code!=0&&r.Output.Contains("explicit_false_prerequisite",StringComparison.Ordinal),r.Output+r.Error);}
            foreach(string conflict in Conflicts(XDocument.Load(Path.Combine(root,"src/OS/OS.csproj")))) {
                string[] chain=["EnableM04IndependentEndianConformanceProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe","EnableM04SzArrayAllocationProbe"];
                int index=Array.IndexOf(chain,conflict);
                if(conflict is "EnableM04RhpNewFastBeforeBindFailureProbe" or "EnableM04RhpNewFastExhaustionFailureProbe")index=5;
                if(conflict is "EnableM04SzArrayAllocationBeforeBindProbe" or "EnableM04SzArrayAllocationExhaustionProbe")index=6;
                if(conflict=="EnableM04CheckedReferenceBarrierProbe")index=5;
                // Supply both profiles' prerequisites so missing-chain validation cannot mask the collision.
                string companion=string.Concat(Prerequisites.Concat(chain.Take(index+1)).Where(x=>x!=conflict).Distinct(StringComparer.Ordinal).Select(x=>" -"+x));
                Result r=Profile(endpoint,["-"+Flag,"-"+conflict,..companion.Split(' ',StringSplitOptions.RemoveEmptyEntries)]);
                Require(r.Code!=0&&(r.Output+r.Error).Contains("profile",StringComparison.Ordinal),"conflict "+conflict+" "+r.Output+r.Error);
            }
        }
    }
    [HostTest("m02.016.efi ordinary source firewall", HostLane.Fast, 5)]
    private static void TestFirewall()
    {
        foreach(string leaf in new[]{"UnexpectedVectorPanicProbeAuthority","UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification"}) {
            string name=@"..\OS.Kernel\Diagnostics\"+leaf+".cs",text=File.ReadAllText(Path.Combine(Root(),"src/OS.Kernel/Diagnostics",leaf+".cs"));
            Require(EfiSourceAdmission.Inspect(text,name).Succeeded,"ordinary reviewed source rejected");
            foreach(string bad in new[]{"using System.Text;","class Bad { object M()=>new object(); }","class Bad { void M(){throw null;} }"})Require(!EfiSourceAdmission.Inspect(text+"\n"+bad,name).Succeeded,"generic firewall bypass");
        }
        string path=Path.Combine(Scratch("stderr"),"preserved.log");
        QemuTranscripts.RequireEmptyNativeError(string.Empty,path);
        foreach(char value in new[]{' ','\r','\n','\t'}) {
            QemuTerminalControls.AssertRejected(()=>QemuTranscripts.RequireEmptyNativeError(value.ToString(),path),"whitespace stderr accepted");
            Require(File.ReadAllText(path)==value.ToString(),"raw stderr preserved");
        }
    }

    [HostTest("m02.016.efi actual default and enabled NativeAOT artifacts", HostLane.Native, 6)]
    private static void TestArtifacts()
    {
        string root=Root();foreach(bool enabled in new[]{false,true}) {
            string dir=Scratch(enabled?"artifact-enabled":"artifact-default"),artifact="artifacts/efi/issue526-tests-"+Guid.NewGuid().ToString("N")+".EFI";
            Result r=Tool(["efi","build","--boot-policy","Diagnostic","-EfiOutputPath",artifact,"-LogPath",Path.Combine(dir,"publish.log"),..(enabled?new[]{"-"+Flag}:Array.Empty<string>())]);
            File.WriteAllText(Path.Combine(dir,"run.log"),r.Output+r.Error);Require(r.Code==0&&r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),r.Output+r.Error);
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(Path.Combine(root,artifact)+suffix,Path.Combine(dir,"image.EFI"+suffix));
            foreach(var file in new[]{(OS.Tooling.EfiContracts.ManagedAssemblyRelativePath("Release"),"OS.dll"),("src/OS/obj/Release/net11.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net11.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,file.Item1),Path.Combine(dir,file.Item2));
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,artifact)+".link.json"));var evidence=document.RootElement;
            var inputs=evidence.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Require(inputs.Length==EfiEvidence.SourceClosurePaths(root).Count,"complete raw source inputs");
            foreach(var input in inputs)Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,input.GetProperty("path").GetString()!)))).Equals(input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"same raw source");
            string[] expectedCompileItems = EfiProjectAdmission.ExpectedCompileItems(
                EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { [Flag] = enabled }));
            var evaluation=evidence.GetProperty("postRestoreEvaluation");
            Require(evaluation.GetProperty("compileCount").GetInt32()==expectedCompileItems.Length,"complete evaluated compile inventory");
            Require(evaluation.GetProperty("compileSha256").GetString()==EfiEvidence.HashLines(expectedCompileItems),"ordered evaluated compile inventory");
            var flags=evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();Require(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"complete evaluated diagnostic switches");
            foreach(var flag in flags)Require(flag.Value.GetString()==(enabled&&(flag.Name==Flag||Prerequisites.Contains(flag.Name,StringComparer.Ordinal))?"true":"false"),"evaluated "+flag.Name);
            using var pe=new PEReader(File.OpenRead(Path.Combine(dir,"OS.dll")));var md=pe.GetMetadataReader();Require(md.AssemblyReferences.Count==0&&md.GetString(md.GetAssemblyDefinition().Name)=="OS","actual no-stdlib OS");
            var types=md.TypeDefinitions.Where(h=>md.GetString(md.GetTypeDefinition(h).Name)=="M02UnexpectedVectorPanicAdapter").ToArray();Require(types.Length==(enabled?1:0),"compiled default adapter exclusion");
            if(enabled) {
                var methods=md.GetTypeDefinition(types[0]).GetMethods().ToDictionary(h=>md.GetString(md.GetMethodDefinition(h).Name),StringComparer.Ordinal);
                string[] run=Calls(pe,md,methods["Run"]),prepare=Calls(pe,md,methods["Prepare"]),callback=Calls(pe,md,methods["Callback"]),query=Calls(pe,md,methods["Query"]);
                int original=Array.FindIndex(run,x=>x=="M01StageThreeAdapter.TryGetUnexpectedPanicBinding"),make=Array.FindIndex(run,x=>x=="M02UnexpectedVectorPanicAdapter.Prepare"),publish=Array.FindLastIndex(run,x=>x=="X64InstructionPublication.Serialize"),entry=Array.FindLastIndex(run,x=>x=="calli");
                Require(original>=0&&make>original&&publish>make&&entry>publish,"actual linked authority/preparation/publication/entry IL");
                int next=0;foreach(string operation in new[]{"PrepareContext","EmitStream","ValidateStreamReadBack"}){int at=Array.FindIndex(prepare,next,x=>x=="UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification."+operation);Require(at>=next,"actual leaf "+operation);next=at+1;}
                Require(callback.Contains("UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.ReportCaptured",StringComparer.Ordinal)&&query.Count(x=>x=="X64InstructionPublication.Serialize")==1,"actual callback chain and query publication");
                var cb=md.GetMethodDefinition(methods["Callback"]);Require((cb.ImplAttributes&System.Reflection.MethodImplAttributes.NoInlining)!=0,"callback NoInlining");
                Require(Convert.ToHexString(md.GetBlobBytes(cb.Signature))=="0002090B0B","static uint callback(ulong,ulong) exact metadata signature");
                File.WriteAllText(Path.Combine(dir,"managed-adapter.json"),JsonSerializer.Serialize(new{GuestExecution=false,NativeStackAccepted=false,RunCalls=run,PrepareCalls=prepare,CallbackCalls=callback,QueryCalls=query,Methods=methods.Select(x=>new{Name=x.Key,Rva=md.GetMethodDefinition(x.Value).RelativeVirtualAddress})},new JsonSerializerOptions{WriteIndented=true}));
            }
            File.WriteAllText(Path.Combine(dir,"identities.json"),JsonSerializer.Serialize(Directory.GetFiles(dir).Select(path=>new{Path=Path.GetFileName(path),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}),new JsonSerializerOptions{WriteIndented=true}));
        }
    }

    // Decode only method-body instruction boundaries. Host metadata inspection never invokes this assembly.
    private static string[] Calls(PEReader pe,MetadataReader md,MethodDefinitionHandle method)
    {
        var opcodes=typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)
            .Where(f=>f.FieldType==typeof(System.Reflection.Emit.OpCode)).Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
        byte[] il=pe.GetMethodBody(md.GetMethodDefinition(method).RelativeVirtualAddress).GetILBytes()!;List<string> calls=[];
        string TypeName(EntityHandle h)=>h.Kind==HandleKind.TypeDefinition?md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)h).Name):h.Kind==HandleKind.TypeReference?md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name):h.Kind.ToString();
        for(int at=0;at<il.Length;) {
            ushort value=il[at++];if(value==0xfe)value=(ushort)(0xfe00|il[at++]);Require(opcodes.TryGetValue(value,out var op),"known IL opcode");
            int bytes=op.OperandType switch {
                System.Reflection.Emit.OperandType.InlineNone=>0,
                System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar=>1,
                System.Reflection.Emit.OperandType.InlineVar=>2,
                System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR=>8,
                System.Reflection.Emit.OperandType.InlineSwitch=>checked(4+4*BitConverter.ToInt32(il,at)),_=>4};
            Require(bytes<=il.Length-at,"bounded IL operand");
            if(op==System.Reflection.Emit.OpCodes.Calli)calls.Add("calli");
            if(op==System.Reflection.Emit.OpCodes.Call || op==System.Reflection.Emit.OpCodes.Callvirt || op==System.Reflection.Emit.OpCodes.Newobj) {
                var h=System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(BitConverter.ToInt32(il,at));
                if(h.Kind==HandleKind.MethodSpecification)h=md.GetMethodSpecification((MethodSpecificationHandle)h).Method;
                if(h.Kind==HandleKind.MethodDefinition){var m=md.GetMethodDefinition((MethodDefinitionHandle)h);calls.Add(TypeName(m.GetDeclaringType())+"."+md.GetString(m.Name));}
                else {Require(h.Kind==HandleKind.MemberReference,"method operand");var m=md.GetMemberReference((MemberReferenceHandle)h);calls.Add(TypeName(m.Parent)+"."+md.GetString(m.Name));}
            }
            at+=bytes;
        }
        return calls.ToArray();
    }

    private static string Member(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Require(start >= 0 && source.IndexOf(signature, start + signature.Length, StringComparison.Ordinal) < 0, "unique " + signature);
        int open = source.IndexOf('{', start), semicolon = source.IndexOf(';', start);
        if (semicolon >= 0 && (open < 0 || semicolon < open)) return source[start..(semicolon + 1)];
        Require(open >= 0, "member body " + signature);
        int depth = 1, cursor = open + 1;
        while (cursor < source.Length && depth != 0)
        {
            if (source[cursor] == '{') depth++;
            else if (source[cursor] == '}') depth--;
            cursor++;
        }
        Require(depth == 0, "balanced actual member " + signature);
        return source[start..cursor];
    }

    private static string Adapter(string source){int start=source.IndexOf("    private static class M02UnexpectedVectorPanicAdapter",StringComparison.Ordinal);Require(start>=0,"adapter");return source[start..source.IndexOf("#endif",start,StringComparison.Ordinal)];}
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Scratch(string name){string p=Path.Combine(Root(),"artifacts/issue526-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    private static Result Qemu(params string[] args)=>Tool(["qemu","smoke","--boot-policy","Diagnostic","--dry-run",..args]);
    private static Result Tool(params string[] args) { var r=HostToolProcess.RunTool(args);return new(r.ExitCode,r.Output,r.Error); }
    private static Result Profile(string endpoint,string[] args) {
        var r=endpoint=="qemu"?HostToolProcess.RunTool(["qemu","smoke","--boot-policy","Diagnostic","--dry-run","--build-efi",..args]):HostToolProcess.ValidateEfiProfile(endpoint=="build",["--boot-policy","Diagnostic",..args]);
        return new(r.ExitCode,r.Output,r.Error);
    }
    private readonly record struct Result(int Code,string Output,string Error);
    private static Result Process(string root,string executable,params string[] args)
    {
        string dir=Scratch("process");var start=new ProcessStartInfo(executable){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in args)start.ArgumentList.Add(arg);DateTime began=DateTime.UtcNow;
        using var process=System.Diagnostics.Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();bool timedOut=!process.WaitForExit(600000);if(timedOut){process.Kill(true);process.WaitForExit();}Task.WaitAll(output,error);
        File.WriteAllText(Path.Combine(dir,"stdout.log"),output.Result);File.WriteAllText(Path.Combine(dir,"stderr.log"),error.Result);
        File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{Executable=executable,Arguments=args,StartedUtc=began,EndedUtc=DateTime.UtcNow,ExitCode=process.ExitCode,TimedOut=timedOut}));
        if(timedOut)throw new TimeoutException("captured in "+dir);return new(process.ExitCode,output.Result,error.Result);
    }

    private static string Root()
    {
        for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CLROS.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
}
