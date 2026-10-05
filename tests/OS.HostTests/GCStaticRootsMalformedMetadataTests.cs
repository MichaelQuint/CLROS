using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.GCStaticRootsMalformedMetadata;
using Status = OS.Kernel.Runtime.GCStaticMetadataStatus;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe class GCStaticRootsMalformedMetadataTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;
        Run("m04.012 POD defaults grant no authority", Pod, ref failures);
        Run("m04.012 original registered module authority is required", Authority, ref failures);
        Run("m04.012 absent empty malformed and unsupported entry counts differ", EntryCounts, ref failures);
        Run("m04.012 literal one-series abbreviated MT bytes decode", LiteralBytes, ref failures);
        Run("m04.012 both signed rel32 directions use their own field origins", RelativeDirections, ref failures);
        Run("m04.012 rel32 extremes and unsigned arithmetic reject before reads", RelativeLimits, ref failures);
        Run("m04.012 complete caller windows and target extents are admitted", Windows, ref failures);
        Run("m04.012 initialized preinitialized and reserved cell forms differ", CellForms, ref failures);
        Run("m04.012 finite MT flags sizes and related type are enforced", MethodTables, ref failures);
        Run("m04.012 GCDesc counts and backward extents are bounded", DescriptorCounts, ref failures);
        Run("m04.012 signed adjusted sizes and reference extents are bounded", SeriesBounds, ref failures);
        Run("m04.012 future cell aliases reject including full owner and slot", Aliases, ref failures);
        Run("m04.012 payload substitution is freshly observed not retained authority", Substitution, ref failures);
        Run("m04.012 read-only validation allocates no managed storage", Allocation, ref failures);
        Run("m04.012 actual source compiles without assembly references", NoStdLib, ref failures);
        if (failures != 0) throw new InvalidOperationException("M04-012 host failures=" + failures);
        Console.WriteLine("CLROS_M04_012_READY hostOnly=true readOnly=true materialized=false publicationAuthority=false guestIntegrated=false");
    }

    private static void Run(string name, Action test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try { test(); Console.WriteLine("TEST_PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("TEST_FAIL " + name + " " + ex); }
    }
    private static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    private static void NoObservations(GCStaticMetadataResult r) => Check(!r.Succeeded &&
        r.Entry == 0 && r.Cell == 0 && r.EncodedCell == 0 && r.MethodTable == 0 && r.RelatedType == 0 &&
        r.Descriptor == 0 && r.Flags == 0 && r.BaseSize == 0 && r.AdjustedSize == 0 &&
        r.SeriesOffset == 0 && r.SeriesBytes == 0, "failure has no usable observation");
    private static void Pod()
    {
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<GCStaticMetadataResult>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<GCStaticReadableWindow>(), "POD");
        Check(sizeof(GCStaticReadableWindow) == 16, "window16");
        NoObservations(default);
        Check(default(GCStaticMetadataResult).Status == Status.Uninitialized, "default status");
        Console.WriteLine("M04_012_LAYOUT window=" + sizeof(GCStaticReadableWindow) + " result=" + sizeof(GCStaticMetadataResult));
    }
    private static void Authority()
    {
        NoObservations(Core.ValidateOne(default, default, null, 0, 0, default, default, 0));
        using Fixture f = new(); using Fixture other = new();
        foreach (int which in new[] { 0, 1, 2, 3, 4 })
        {
            byte[] before = f.Snapshot();
            var r = Core.ValidateOne(which == 0 ? default : f.Capture,
                which == 1 ? other.Mapping : f.Mapping, f.Owner, Fixture.Capacity,
                which == 2 ? 0 : which == 3 ? other.Handle : f.Handle,
                f.Metadata, f.Cells, f.Object);
            if (which == 4)
            {
                f.Owner[0] ^= 1; before = f.Snapshot(); r = f.Validate(); f.AssertUnchanged(before); f.Owner[0] ^= 1;
            }
            else f.AssertUnchanged(before);
            Check(r.Status == Status.ModuleRejected, "module authority " + which); NoObservations(r);
        }
        f.Image[2] = 1; f.Expect(Status.ModuleRejected); // Structurally valid PE, changed retained headers.
        f.Image[2] = 0;
        f.Image[0x200C] = 1; f.Expect(Status.ModuleRejected); // Last four bytes of full slot.
    }
    private static void EntryCounts()
    {
        using (Fixture f = new(x => W32(x.Row(0), 200))) f.Expect(Status.SectionAbsent);
        using (Fixture f = new(x => W64(x.Row(0) + 16, x.Entry))) f.Expect(Status.SectionEmpty);
        foreach (int n in new[] { 1, 2, 3, 5, 7 })
        { using Fixture f = new(x => W64(x.Row(0) + 16, x.Entry + (uint)n)); f.Expect(Status.EntryRejected); }
        foreach (int n in new[] { 8, 12, 0x100 })
        { using Fixture f = new(x => W64(x.Row(0) + 16, x.Entry + (uint)n)); f.Expect(Status.UnsupportedEntryCount); }
        using (Fixture f = new(x => { W64(x.Row(0) + 8, x.Entry + 1); W64(x.Row(0) + 16, x.Entry + 5); }))
            f.Expect(Status.EntryRejected);
    }
    private static void LiteralBytes()
    {
        using Fixture f = new();
        byte[] expected = [0xF0,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF, 8,0,0,0,0,0,0,0,
            1,0,0,0,0,0,0,0, 0,0,0,1,24,0,0,0];
        Check(new ReadOnlySpan<byte>((void*)(f.Mt - 24), 32).SequenceEqual(expected), "independent primary-source bytes");
        // Deliberately non-ordinary trailing bytes. The abbreviated MT ends at+16.
        for (int i = 16; i < 24; i++) ((byte*)f.Mt)[i] = 0xD7;
        var r = f.Expect(Status.Success);
        Check(r.Entry == f.Entry && r.Cell == f.Cell && r.MethodTable == f.Mt &&
            r.Descriptor == f.Mt - 24 && r.RelatedType == f.Object && r.Flags == 0x01000000 &&
            r.BaseSize == 24 && r.AdjustedSize == -16 && r.SeriesOffset == 8 && r.SeriesBytes == 8,
            "literal decoded observation");
        Check(r.EncodedCell == 0x00000000FFFFF201UL, "negative MT rel32 is not sign extended into reserved word");
        f.Metadata = new(f.Address(0x1100), f.Mt + 16 - f.Address(0x1100));
        f.Expect(Status.Success); // Exact 16-byte boundary, no fabricated24-byte token.
    }
    private static void RelativeDirections()
    {
        using (Fixture f = new()) f.Expect(Status.Success); // Entry positive, MT negative.
        using (Fixture f = new(x => x.SetLocations(0x1600, 0x1300, 0x1800, 0x1180)))
        {
            f.Metadata = new(f.Address(0x1100), 0xF00); f.Cells = f.Metadata;
            var r = f.Expect(Status.Success); // Readable windows may overlap; no PE-write claim.
            Check(r.EncodedCell == 0x501 && r.Cell == f.Address(0x1300), "entry negative, MT positive");
        }
    }
    private delegate bool Resolve(ulong origin, uint encoded, out ulong target);
    private static void RelativeLimits()
    {
        // Pure numeric helper controls exercise true unsigned wrap without ever
        // fabricating/dereferencing a mapped pointer near address zero or MaxValue.
        Resolve resolve = typeof(Core).GetMethod("Relative32", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Resolve>();
        (ulong Origin, uint Word, bool Ok, ulong Target)[] vectors = [
            (0,0x7FFFFFFF,true,2147483647), (2147483648,0x80000000,true,0),
            (2147483647,0x80000000,false,0), (ulong.MaxValue-2147483647,0x7FFFFFFF,true,ulong.MaxValue),
            (ulong.MaxValue-2147483646,0x7FFFFFFF,false,0), (0,0xFFFFFFFF,false,0),
            (1,0xFFFFFFFF,true,0), (ulong.MaxValue,0,true,ulong.MaxValue) ];
        foreach (var v in vectors) Check(resolve(v.Origin,v.Word,out ulong target)==v.Ok && target==v.Target,"literal arithmetic");
        foreach (uint displacement in new uint[] { 0x7FFFFFFF, 0x80000000, 1, 0xFFFFFFFF })
        { using Fixture f = new(); W32((byte*)f.Entry, displacement); f.Expect(Status.CellRejected); }
        foreach (uint displacement in new uint[] { 0x7FFFFFFD, 0x80000001 })
        { using Fixture f = new(); W64((byte*)f.Cell, displacement); f.Expect(Status.MethodTableRejected); }
    }
    private static void Windows()
    {
        foreach (GCStaticReadableWindow window in new GCStaticReadableWindow[] { default, new(1,ulong.MaxValue), new(ulong.MaxValue-7,16) })
        {
            using Fixture f = new(); f.Metadata=window; f.Expect(Status.CallerBoundsRejected);
            f.Metadata=new(f.Address(0x1100),0xF00);f.Cells=window;f.Expect(Status.CallerBoundsRejected);
        }
        using (Fixture f = new()) { f.Metadata=new(f.Address(0x1100),0x102);f.Expect(Status.EntryRejected); }
        using (Fixture f = new()) { f.Cells=new(f.Cell,7);f.Expect(Status.CellRejected); }
        using (Fixture f = new()) { f.Metadata=new(f.Address(0x1100),f.Mt+15-f.Address(0x1100));f.Expect(Status.MethodTableRejected); }
        using (Fixture f = new()) { f.Object=0;f.Expect(Status.CallerBoundsRejected); }
        using (Fixture f = new()) { f.Object=f.Address(0x1181);f.Expect(Status.CallerBoundsRejected); }
        using (Fixture f = new()) { f.Object=f.Address(Fixture.ImageSize-16);f.Metadata=new((ulong)f.Image,Fixture.ImageSize);f.Expect(Status.CallerBoundsRejected); }
    }
    private static void CellForms()
    {
        foreach (uint tag in new uint[] { 0,2,3 })
        { using Fixture f = new(); W64((byte*)f.Cell,0xFFFFF200UL|tag);f.Expect(Status.UnsupportedForm); }
        foreach (ulong encoded in new ulong[] { 0x1234567800000100, 0x88888888FFFFF203 })
        { using Fixture f = new(); W64((byte*)f.Cell,encoded);f.Expect(Status.UnsupportedForm); }
        foreach (ulong high in new ulong[] { 1UL<<32, 0xFFFFFFFF00000000 })
        { using Fixture f = new();W64((byte*)f.Cell,0xFFFFF201UL|high);f.Expect(Status.CellRejected); }
        using (Fixture f = new()) { W32((byte*)f.Entry,0x1004);f.Expect(Status.CellRejected); }
        using (Fixture f = new()) { W64((byte*)f.Cell,0xFFFFF205);f.Expect(Status.MethodTableRejected); }
    }
    private static void MethodTables()
    {
        foreach(uint flags in new uint[] {0,0x01000001,0x51000000,0x81000000,uint.MaxValue})
        {using Fixture f=new();W32((byte*)f.Mt,flags);f.Expect(Status.UnsupportedForm);}
        foreach(uint size in new uint[] {0,8,16,23,25,4095,uint.MaxValue})
        {using Fixture f=new();W32((byte*)f.Mt+4,size);f.Expect(Status.MethodTableRejected);}
        foreach(uint size in new uint[] {4104,0xFFFFFFF8})
        {using Fixture f=new();W32((byte*)f.Mt+4,size);f.Expect(Status.UnsupportedForm);}
        foreach(ulong related in new ulong[] {0,ulong.MaxValue})
        {using Fixture f=new();W64((byte*)f.Mt+8,related);f.Expect(Status.RelatedTypeRejected);}
        using(Fixture f=new()){f.Object+=8;f.Expect(Status.RelatedTypeRejected);}
    }
    private static void DescriptorCounts()
    {
        using(Fixture f=new()){W64((byte*)f.Mt-8,0);f.Expect(Status.DescriptorRejected);}
        foreach(ulong count in new ulong[] {2,3,ulong.MaxValue,0x8000000000000000})
        {using Fixture f=new();W64((byte*)f.Mt-8,count);f.Expect(Status.UnsupportedForm);}
        using(Fixture f=new(x=>x.SetLocations(0x1800,0x2200,0x1400,0x1600)))
        {f.Metadata=new(f.Mt-16,0x500);f.Expect(Status.DescriptorRejected);}
        using(Fixture f=new(x=>x.SetLocations(0x1800,0x2200,0x1400,0x1600)))
        {f.Metadata=new(f.Mt,0x500);f.Expect(Status.MethodTableRejected);}
    }
    private static void SeriesBounds()
    {
        foreach(long adjusted in new long[] {long.MinValue,long.MaxValue,0,-1,-8,-15,-17,-24})
        {using Fixture f=new();W64((byte*)f.Mt-24,unchecked((ulong)adjusted));f.Expect(Status.DescriptorRejected);}
        foreach(ulong offset in new ulong[] {0,1,7,9,16,24,0x8000000000000000,ulong.MaxValue})
        {using Fixture f=new();W64((byte*)f.Mt-16,offset);f.Expect(Status.DescriptorRejected);}
        (uint B,long D,ulong O,ulong L)[] positive=[(32,-24,16,8),(32,-16,8,16),(4096,-4088,4080,8),(4096,-16,8,4080)];
        foreach(var v in positive)
        {
            using Fixture f=new();W32((byte*)f.Mt+4,v.B);W64((byte*)f.Mt-24,unchecked((ulong)v.D));W64((byte*)f.Mt-16,v.O);
            var r=f.Expect(Status.Success);Check(r.BaseSize==v.B && r.SeriesOffset==v.O && r.SeriesBytes==v.L,"bounded run");
        }
        using(Fixture f=new()){W32((byte*)f.Mt+4,32);W64((byte*)f.Mt-24,unchecked((ulong)-17L));f.Expect(Status.DescriptorRejected);}
        using(Fixture f=new()){W32((byte*)f.Mt+4,32);W64((byte*)f.Mt-16,16);f.Expect(Status.DescriptorRejected);}
    }
    private static void Aliases()
    {
        foreach(int offset in new[]{0x2000,0x2008,0x1200,0x1180,0x1188,0x1190,0x13E8,0x13F0,0x13F8,0x1400,0x1408})
        {
            using Fixture f=new();f.Cells=new((ulong)f.Image,Fixture.ImageSize);
            W32((byte*)f.Entry,unchecked((uint)(offset-0x1200)));
            // Descriptor/MT aliases must contain a valid tagged relative word to
            // reach the later alias predicate; snapshots include the mutation.
            if(offset>=0x13E8 && offset<=0x1408) W64(f.Image+offset,unchecked((uint)(0x1400-offset+1)));
            f.Expect(Status.ProtectedAlias);
        }
        using(Fixture f=new(inlineOwner:true))
        {W32((byte*)f.Entry,0x2500);f.Expect(Status.ProtectedAlias);} // Cell3700 lies in unused owner capacity.
    }
    private static void Substitution()
    {
        using Fixture f=new();var first=f.Expect(Status.Success);
        W32((byte*)f.Mt+4,32);W64((byte*)f.Mt-24,unchecked((ulong)-24L));
        var next=f.Expect(Status.Success);
        Check(first.BaseSize==24 && next.BaseSize==32 && next.SeriesBytes==8,"fresh valid payload observation");
        Check(Module.TryGetSection(f.Capture,f.Mapping,f.Owner,Fixture.Capacity,f.Handle,201).Succeeded,"Type does not snapshot payload");
        W64((byte*)f.Mt-24,0);f.Expect(Status.DescriptorRejected);
        Check(first.Succeeded && first.BaseSize==24,"retained POD is historical, no consumer/write API");
    }
    private static void Allocation()
    {
        using Fixture f=new();byte[] before=f.Snapshot();
        for(int i=0;i<100;i++) _=f.Validate();
        long start=GC.GetAllocatedBytesForCurrentThread();bool passed=true;
        for(int i=0;i<1000;i++) passed&=f.Validate().Succeeded;
        long end=GC.GetAllocatedBytesForCurrentThread();
        Check(passed && start==end,"warm zero allocation");f.AssertUnchanged(before);
    }

    private sealed class Fixture : IDisposable
    {
        internal const int ImageSize=0x8000;
        internal const ulong Capacity=0x1000;
        private readonly byte* imageAllocation;
        private readonly byte* ownerAllocation;
        internal readonly byte* Image;
        internal readonly byte* Owner;
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong Entry,Cell,Mt,Object;
        internal GCStaticReadableWindow Metadata,Cells;
        internal Fixture(Action<Fixture>? configure=null,bool inlineOwner=false)
        {
            imageAllocation=(byte*)NativeMemory.AlignedAlloc(ImageSize+8192,4096);
            ownerAllocation=(byte*)NativeMemory.Alloc((nuint)(Capacity+16));
            if(imageAllocation==null || ownerAllocation==null) throw new OutOfMemoryException();
            new Span<byte>(imageAllocation,ImageSize+8192).Fill(0xA5);
            new Span<byte>(ownerAllocation,(int)Capacity+16).Fill(0x5A);
            Image=imageAllocation+4096;NativeMemory.Clear(Image,ImageSize);
            Owner=inlineOwner?Image+0x2800:ownerAllocation+8;NativeMemory.Clear(Owner,(nuint)Capacity);
            W16(Image,0x5A4D);W32(Image+60,0x80);W32(Image+0x80,0x4550);
            W16(Image+0x84,0x8664);W16(Image+0x86,4);W16(Image+0x94,0xF0);
            W16(Image+0x98,0x20B);W32(Image+0xD0,ImageSize);W32(Image+0xD4,0x400);
            Section(0,".rdata",0x1000,0x1000,0x40000040);
            Section(1,".data",0x2000,0x2000,0xC0000040);
            Section(2,".modules",0x5000,8,0x40000040);
            Section(3,".text",0x6000,0x1000,0x60000020);
            W64(Image+0x5000,Address(0x1000));W32(Image+0x1000,0x00525452);
            W16(Image+0x1004,16);W16(Image+0x100C,4);Image[0x100E]=24;Image[0x100F]=1;
            WriteRow(1,202,1,0x2100,0x2108);WriteRow(2,204,0,0x2000,0);WriteRow(3,213,1,0x1210,0x1210);
            SetLocations(0x1200,0x2200,0x1400,0x1180);
            Metadata=new(Address(0x1100),0xF00);Cells=new(Address(0x2000),0x2000);
            configure?.Invoke(this);
            var observation=EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0,0x1000,0x2000,(ulong)Image,ImageSize);
            Capture=EfiLoadedImageRangeCapturePlan.Create(&observation,1,1,4096);
            Mapping=M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var r=Module.RegisterOnce(Capture,Mapping,Owner,Capacity);
            Check(r.Succeeded,"fixture genuine registration "+r.Status);Handle=r.Handle;
            // Pattern bytes outside the Type frozen headers and metadata payloads.
            new Span<byte>(Image+0x7000,0x1000).Fill(0xC3);
        }
        internal ulong Address(int offset)=>(ulong)Image+(uint)offset;
        internal byte* Row(int i)=>Image+0x1010+24*i;
        internal void SetLocations(int entry,int cell,int mt,int obj)
        {
            Entry=Address(entry);Cell=Address(cell);Mt=Address(mt);Object=Address(obj);
            WriteRow(0,201,1,entry,entry+4);
            W32((byte*)Entry,unchecked((uint)(cell-entry)));
            W64((byte*)Cell,unchecked((uint)(mt-cell+1)));
            byte[] literal=[0xF0,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,8,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,1,24,0,0,0];
            literal.CopyTo(new Span<byte>((void*)(Mt-24),32));W64((byte*)Mt+8,Object);
            W32((byte*)Object,0x50000000);W32((byte*)Object+4,24);W64((byte*)Object+8,0);
        }
        private void WriteRow(int i,uint id,uint flags,int start,int end)
        {byte* r=Row(i);W32(r,id);W32(r+4,flags);W64(r+8,Address(start));W64(r+16,end==0?0:Address(end));}
        private void Section(int i,string name,uint start,uint size,uint flags)
        {byte* p=Image+0x188+i*40;for(int n=0;n<name.Length;n++) p[n]=(byte)name[n];W32(p+8,size);W32(p+12,start);W32(p+36,flags);}
        internal GCStaticMetadataResult Validate()=>Core.ValidateOne(Capture,Mapping,Owner,Capacity,Handle,Metadata,Cells,Object);
        internal GCStaticMetadataResult Expect(Status status)
        {
            byte[] before=Snapshot();var result=Validate();AssertUnchanged(before);
            Check(result.Status==status,"expected="+status+" actual="+result.Status+" module="+result.ModuleStatus);
            if(status!=Status.Success) NoObservations(result);return result;
        }
        internal byte[] Snapshot()
        {
            byte[] result=new byte[ImageSize+8192+(int)Capacity+16];
            new ReadOnlySpan<byte>(imageAllocation,ImageSize+8192).CopyTo(result);
            new ReadOnlySpan<byte>(ownerAllocation,(int)Capacity+16).CopyTo(result.AsSpan(ImageSize+8192));return result;
        }
        internal void AssertUnchanged(byte[] before)=>Check(before.AsSpan().SequenceEqual(Snapshot()),"entire mapped image/owner/canaries unchanged");
        public void Dispose(){NativeMemory.AlignedFree(imageAllocation);NativeMemory.Free(ownerAllocation);}
    }
    private static void W16(byte* p,ushort v){p[0]=(byte)v;p[1]=(byte)(v>>8);}
    private static void W32(byte* p,uint v){W16(p,(ushort)v);W16(p+2,(ushort)(v>>16));}
    private static void W64(byte* p,ulong v){W32(p,(uint)v);W32(p+4,(uint)(v>>32));}

    private static void NoStdLib()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        string directory=Path.Combine(root,"artifacts","host-tests","m04-012-nostdlib-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string osProject=Path.Combine(root,"src","OS","OS.csproj");
        string core=Path.Combine(root,"src","OS.Kernel","Runtime","GCStaticRootsMalformedMetadata.cs");
        string[] sources=XDocument.Load(osProject).Descendants("Compile").Select(x=>Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(osProject)!,x.Attribute("Include")!.Value))).Append(core).ToArray();
        Check(sources.Distinct(StringComparer.OrdinalIgnoreCase).Count()==sources.Length,"explicit unique closure");
        var identities=sources.Append(osProject).Select(p=>new{Path=p,Bytes=new FileInfo(p).Length,
            Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}).ToArray();
        File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(identities,new JsonSerializerOptions{WriteIndented=true}));
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14.0"),
                new XElement("AssemblyName","M04_012_Closure"),new XElement("OutputType","Library"),
                new XElement("AllowUnsafeBlocks","true"),new XElement("EnableDefaultCompileItems","false"),
                new XElement("ImplicitUsings","disable"),new XElement("Nullable","disable"),
                new XElement("NoStdLib","true"),new XElement("NoConfig","true"),
                new XElement("DisableImplicitFrameworkReferences","true"),new XElement("GenerateAssemblyInfo","false"),
                new XElement("GenerateTargetFrameworkAttribute","false"),new XElement("ProduceReferenceAssembly","false"),
                new XElement("EnableNETAnalyzers","false"),new XElement("RunAnalyzersDuringBuild","false"),
                new XElement("RuntimeMetadataVersion","v4.0.30319"),new XElement("DefineConstants","NOSTDLIB_EFI")),
            new XElement("ItemGroup",sources.Select(p=>new XElement("Compile",new XAttribute("Include",p)))),
            new XElement("Target",new XAttribute("Name","RemoveFrameworkReferences"),
                new XAttribute("BeforeTargets","BeforeCompile"),new XAttribute("AfterTargets","FindReferenceAssembliesForReferences"),
                new XElement("ItemGroup",new XElement("ReferencePathWithRefAssemblies",new XAttribute("Remove","@(ReferencePathWithRefAssemblies)")),
                    new XElement("ReferencePath",new XAttribute("Remove","@(ReferencePath)")))));
        string projectPath=Path.Combine(directory,"Closure.csproj");File.WriteAllText(projectPath,project.ToString(),new UTF8Encoding(false));
        ProcessStartInfo start=new("dotnet"){WorkingDirectory=directory,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(string arg in new[]{"build",projectPath,"--nologo","-c","Release","-m:1","--disable-build-servers",
            "-p:ImportDirectoryBuildProps=false","-p:ImportDirectoryBuildTargets=false","-p:UseSharedCompilation=false"})start.ArgumentList.Add(arg);
        DateTime began=DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory,"command.json"),JsonSerializer.Serialize(new{start.FileName,Arguments=start.ArgumentList.ToArray(),start.WorkingDirectory,StartedUtc=began}));
        using Process process=Process.Start(start)!;
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        bool timeout=!process.WaitForExit(120_000);if(timeout){process.Kill(true);process.WaitForExit();}
        string output=stdout.GetAwaiter().GetResult(),error=stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory,"stdout.log"),output);File.WriteAllText(Path.Combine(directory,"stderr.log"),error);
        bool unchanged=identities.All(r=>new FileInfo(r.Path).Length==r.Bytes && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(r.Path)))==r.Sha256);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{NativeExit=process.ExitCode,Timeout=timeout,
            StartedUtc=began,EndedUtc=DateTime.UtcNow,InputsUnchanged=unchanged}));
        Check(!timeout && process.ExitCode==0 && error.Length==0 && unchanged,"actual no-stdlib child "+directory);
        string dll=Path.Combine(directory,"bin","Release","net10.0","M04_012_Closure.dll");
        using var stream=File.OpenRead(dll);using PEReader pe=new(stream);var md=pe.GetMetadataReader();
        Check(md.AssemblyReferences.Count==0,"AssemblyRefs0");
        var type=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Single(t=>md.GetString(t.Name)==nameof(GCStaticRootsMalformedMetadata));
        var method=type.GetMethods().Select(h=>md.GetMethodDefinition(h)).Single(m=>md.GetString(m.Name)=="ValidateOne");
        Check(method.RelativeVirtualAddress!=0 && pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!.Length>0,"actual validator body");
        File.WriteAllText(Path.Combine(directory,"metadata.json"),JsonSerializer.Serialize(new{AssemblyRefs=md.AssemblyReferences.Count,ValidateOneRva=method.RelativeVirtualAddress,
            Path=dll,Bytes=new FileInfo(dll).Length,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)))}));
        Console.WriteLine("M04_012_NOSTDLIB sources="+sources.Length+" assemblyRefs=0 artifacts="+directory);
    }
}
