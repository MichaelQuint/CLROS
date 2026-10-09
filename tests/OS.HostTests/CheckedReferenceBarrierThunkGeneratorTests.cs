#if CHECKED_REFERENCE_THUNK_CHILD
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.X64TextSerializerGenerator;

internal static class ThunkControls
{
    internal static void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    static byte[] Hex(string value) => Convert.FromHexString(string.Concat(value.Where(c => !char.IsWhiteSpace(c))));
    // Independent, hand-derived scalar instruction oracle, including all sixteen explicit XMM slots.
    static readonly byte[] Scalar = Hex("""
        9C55 4889E5 505152535657 41504151415241534154415541564157
        4881EC30010000 4883E4F0 48F7450800040000 0F858E010000
        F30F7F842420000000 F30F7F8C2430000000 F30F7F942440000000 F30F7F9C2450000000
        F30F7FA42460000000 F30F7FAC2470000000 F30F7FB42480000000 F30F7FBC2490000000
        F3440F7F8424A0000000 F3440F7F8C24B0000000 F3440F7F9424C0000000 F3440F7F9C24D0000000
        F3440F7FA424E0000000 F3440F7FAC24F0000000 F3440F7FB42400010000 F3440F7FBC2410010000
        0FAE9C2420010000 B901000000 488B55F0 4C8B45E8 41B908000000
        E800000000 83F801 0F85CD000000 9C415B49F7C300040000 0F85BD000000
        0FAE942420010000
        F30F6F842420000000 F30F6F8C2430000000 F30F6F942440000000 F30F6F9C2450000000
        F30F6FA42460000000 F30F6FAC2470000000 F30F6FB42480000000 F30F6FBC2490000000
        F3440F6F8424A0000000 F3440F6F8C24B0000000 F3440F6F9424C0000000 F3440F6F9C24D0000000
        F3440F6FA424E0000000 F3440F6FAC24F0000000 F3440F6FB42400010000 F3440F6FBC2410010000
        488D6590 415F415E415D415C415B415A41594158 5F5E5B5A59585D9DC3 EBFE
        """);
    static byte[] ExpectedText()
    {
        Need(Scalar.Length == 452, "literal-scalar-size");
        byte[] second = (byte[])Scalar.Clone(); second[213] = 2;
        byte[] third = [.. Scalar[..421], .. Hex("488345D808488345D008"), .. Scalar[421..]];
        third[213] = 3; third[220] = 0xD0; third[224] = 0xD8;
        BinaryPrimitives.WriteInt32LittleEndian(third.AsSpan(48),408);
        BinaryPrimitives.WriteInt32LittleEndian(third.AsSpan(241),215);
        BinaryPrimitives.WriteInt32LittleEndian(third.AsSpan(257),199);
        return [.. Scalar, .. Enumerable.Repeat((byte)0x90,12), .. second, .. Enumerable.Repeat((byte)0x90,12), .. third];
    }
    static byte[] ExpectedObject()
    {
        byte[] text = ExpectedText(); Need(text.Length == 1390,"literal-text-size");
        // Fixed complete COFF header, section, relocation and symbol rows. No serializer helpers used.
        byte[] header = Hex("6486 0100 00000000 CA050000 04000000 0000 0000");
        byte[] section = Hex("2E74657874000000 00000000 00000000 6E050000 3C000000 AC050000 00000000 0300 0000 20005060");
        byte[] relocations = Hex("E8000000 03000000 0400 B8020000 03000000 0400 88040000 03000000 0400");
        byte[] symbols = Hex("""
          00000000 04000000 00000000 0100 2000 02 00
          00000000 11000000 D0010000 0100 2000 02 00
          00000000 25000000 A0030000 0100 2000 02 00
          00000000 37000000 00000000 0000 2000 02 00
          """);
        return [.. header,.. section,.. text,0,0,.. relocations,.. symbols,0x5B,0,0,0,
            .. Encoding.ASCII.GetBytes("RhpAssignRef\0RhpCheckedAssignRef\0RhpByRefAssignRef\0CLROS_CheckedReferenceBarrier_Apply\0")];
    }
    static ushort U16(byte[] b,int p)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p));
    static uint U32(byte[] b,int p)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
    internal static byte[] CheckObject(byte[] b)
    {
        Need(b.Length==1645 && b.AsSpan().SequenceEqual(ExpectedObject()),"complete-independent-object-literal");
        Need(U16(b,0)==0x8664 && U16(b,2)==1 && U32(b,4)==0 && U32(b,8)==1482 && U32(b,12)==4 && U32(b,16)==0,"file-header");
        Need(Encoding.ASCII.GetString(b,20,8)==".text\0\0\0" && U32(b,28)==0 && U32(b,32)==0 && U32(b,36)==1390 && U32(b,40)==60 && U32(b,44)==1452 && U32(b,48)==0 && U16(b,52)==3 && U16(b,54)==0 && U32(b,56)==0x60500020,"section-header-no-unwind");
        string[] names=["RhpAssignRef","RhpCheckedAssignRef","RhpByRefAssignRef","CLROS_CheckedReferenceBarrier_Apply"];
        uint[] values=[0,464,928,0]; int[] nameOffsets=[4,17,37,55];
        Need(U32(b,1554)==91,"string-length");
        for(int i=0;i<4;i++)
        {
            int p=1482+i*18; int start=1554+nameOffsets[i]; int end=Array.IndexOf(b,(byte)0,start);
            Need(U32(b,p)==0 && U32(b,p+4)==nameOffsets[i] && U32(b,p+8)==values[i] && U16(b,p+12)==(i==3?0:1) && U16(b,p+14)==0x20 && b[p+16]==2 && b[p+17]==0,"symbol-row-"+i);
            Need(Encoding.ASCII.GetString(b,start,end-start)==names[i],"symbol-name-"+i);
        }
        uint[] calls=[232,696,1160];
        for(int i=0;i<3;i++)
        {
            int p=1452+i*10; Need(U32(b,p)==calls[i] && U32(b,p+4)==3 && U16(b,p+8)==4,"REL32-row-"+i);
            Need(b[60+(int)calls[i]-1]==0xE8 && U32(b,60+(int)calls[i])==0,"call-addend");
            int entry=(int)values[i], terminal=entry+(i==2?460:450);
            foreach(int d in new[]{48,241,257}) Need(entry+d+4+BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(60+entry+d))==terminal,"local-reject-branch");
            Need(b[60+terminal]==0xEB && b[61+terminal]==0xFE,"terminal-loop");
        }
        return b[60..1450];
    }
    internal static object Run(string output, string expectedSerializerHash, int expectedSerializerSize)
    {
        Directory.CreateDirectory(output);
        var definition=CheckedReferenceBarrierThunkGenerator.Define();
        Need(Amd64CoffWriter.TryGetRequiredSize(in definition,true,out int size,out _) && size==1645,"size");
        byte[] guarded=Enumerable.Range(0,1709).Select(i=>(byte)(i*37+11)).ToArray(); byte[] before=(byte[])guarded.Clone();
        Need(CheckedReferenceBarrierThunkGenerator.TryGenerate(guarded.AsSpan(32,1645),out int written,out var error) && written==1645 && error==CoffWriteError.None,"generate");
        Need(guarded.AsSpan(0,32).SequenceEqual(before.AsSpan(0,32)) && guarded.AsSpan(1677).SequenceEqual(before.AsSpan(1677)),"generation-canaries");
        byte[] obj=guarded[32..1677]; byte[] text=CheckObject(obj);
        File.WriteAllBytes(Path.Combine(output,"checked-reference-thunks.obj"),obj);
        foreach(int capacity in new[]{0,1,60,1644})
        {
            byte[] destination=Enumerable.Range(0,1709).Select(i=>(byte)(i*29+71)).ToArray(); byte[] old=(byte[])destination.Clone();
            Need(!CheckedReferenceBarrierThunkGenerator.TryGenerate(destination.AsSpan(32,capacity),out written,out error) && written==0 && error==CoffWriteError.CapacityExceeded && destination.AsSpan().SequenceEqual(old),"capacity-atomic-"+capacity);
        }
        byte[] rejected=Enumerable.Repeat((byte)0xA7,1645).ToArray();
        Need(!Amd64CoffWriter.TryWrite(in definition,rejected,out written,out _) && written==0 && rejected.All(x=>x==0xA7),"default-reject-atomic");
        byte[] serializer=new byte[expectedSerializerSize]; Need(InstructionPublicationSerializerGenerator.TryGenerate(serializer,out written,out error) && written==expectedSerializerSize && Convert.ToHexString(SHA256.HashData(serializer))==expectedSerializerHash,"serializer-unchanged");
        File.WriteAllBytes(Path.Combine(output,"serializer.obj"),serializer);
        List<object> states=[];
        uint[] statuses=[0,2,0x101,0x80000001,uint.MaxValue];
        for(int operation=1;operation<=3;operation++) foreach(int residue in new[]{0,8})
        {
            states.Add(new Model(text,operation,residue,1,false,false).Verify());
            states.Add(new Model(text,operation,residue,1,true,false).Verify());
            states.Add(new Model(text,operation,residue,1,false,true).Verify());
            foreach(uint status in statuses) states.Add(new Model(text,operation,residue,status,false,false).Verify());
        }
        // Each single-byte mutation must fail complete-object validation. This checks every field,
        // displacement, padding byte and string byte; semantic state tests separately use valid bytes.
        int mutations=0;
        for(int p=0;p<obj.Length;p++)
        {
            byte[] bad=(byte[])obj.Clone(); bad[p]^=1; bool failed=false;
            try { CheckObject(bad); } catch(InvalidOperationException) { failed=true; }
            Need(failed,"object-mutation-"+p); mutations++;
        }
        return new {Passed=true,ObjectBytes=obj.Length,ObjectSha256=Convert.ToHexString(SHA256.HashData(obj)),TextBytes=text.Length,
            Functions=3,ExternalCallbacks=1,Rel32=3,LocalRejectBranches=9,StateCases=states,SingleByteObjectRejects=mutations,
            NativeCodeExecuted=false,RealCallbackPresent=false,UnwindAccepted=false,WiderStateAccepted=false,GuestAccepted=false};
    }

    // Finite interpreter for exactly the emitted instruction forms. It models bytes and a private
    // patterned stack only. The opaque callback is a test transition, never a callable export.
    sealed class Model
    {
        readonly byte[] code; readonly int op,residue; readonly uint status; readonly bool entryDf,callbackDf;
        readonly ulong[] g=new ulong[16], original=new ulong[16]; readonly byte[][] xmm=new byte[16][];
        readonly byte[][] originalXmm=new byte[16][]; readonly byte[] stack=new byte[1024], initial=new byte[1024];
        readonly List<int> stores=[], restores=[]; ulong flags; uint mxcsr=0x1F80; const int StackBase=0x100000;
        int ip,callbacks,minimum,home=-1; bool zero,returned,rejected; readonly int start,terminal,s; readonly ulong originalFlags;
        const ulong ReturnAddress=0x123456789ABCDEF0;
        internal Model(byte[] bytes,int operation,int stackResidue,uint result,bool df,bool postDf)
        {
            code=bytes;op=operation;residue=stackResidue;status=result;entryDf=df;callbackDf=postDf;
            start=operation==1?0:operation==2?464:928; terminal=start+(operation==3?460:450);ip=start;
            s=StackBase+768+residue; minimum=s;
            for(int i=0;i<16;i++) { g[i]=0xA110000000000000UL+(ulong)(i*0x101+0x31);xmm[i]=Enumerable.Range(0,16).Select(j=>(byte)(i*17+j*3+5)).ToArray();originalXmm[i]=(byte[])xmm[i].Clone(); }
            // Distinct opaque addresses, intentionally never dereferenced.
            g[6]=0x12345000; g[7]=0x23456000; g[4]=(ulong)s;
            Array.Copy(g,original,16); flags=0x246UL|(df?0x400UL:0); originalFlags=flags;
            for(int i=0;i<stack.Length;i++) stack[i]=(byte)(i*31+7);
            Put64(s,ReturnAddress);Array.Copy(stack,initial,stack.Length);
        }
        Span<byte> Mem(int address,int length) { Need(address>=StackBase && address+length<=StackBase+stack.Length,"stack-bounds");return stack.AsSpan(address-StackBase,length); }
        void Put64(int address,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(Mem(address,8),value);
        ulong Get64(int address)=>BinaryPrimitives.ReadUInt64LittleEndian(Mem(address,8));
        void Push(ulong value) {g[4]-=8;minimum=Math.Min(minimum,(int)g[4]);Put64((int)g[4],value);}
        ulong Pop() {ulong value=Get64((int)g[4]);g[4]+=8;return value;}
        int I32(int offset)=>BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset));
        bool At(params byte[] bytes)=>code.AsSpan(ip,Math.Min(bytes.Length,code.Length-ip)).SequenceEqual(bytes);
        void Callback()
        {
            callbacks++;Need(callbacks==1,"single-callback");home=(int)g[4];
            Need((home&15)==0 && s-home==(residue==0?432:440),"callback-alignment-depth");
            Need(g[1]==(ulong)op && g[2]==original[op==3?7:1] && g[8]==original[op==3?6:2] && g[9]==8,"callback-args");
            Need((flags&0x400)==0,"callback-entry-DF");
            Need(stores.SequenceEqual(Enumerable.Range(0,16)),"all-XMM-saved-before-call");
            int r=s-16; int[] saved=[0,1,2,3,6,7,8,9,10,11,12,13,14,15];
            for(int i=0;i<saved.Length;i++) Need(Get64(r-8*(i+1))==original[saved[i]],"GPR-save-slot");
            Need(Get64(r)==original[5] && Get64(r+8)==originalFlags && Get64(s)==ReturnAddress,"anchor-flags-return");
            for(int i=0;i<16;i++) Need(Mem(home+32+i*16,16).SequenceEqual(originalXmm[i]),"XMM-save-slot");
            Need(BinaryPrimitives.ReadUInt32LittleEndian(Mem(home+288,4))==0x1F80,"MXCSR-save-slot");
            Need(home+292<=r-112 && home+32>home,"home-save-separation");
            Push((ulong)(ip+5));Need((g[4]&15)==8,"ordinary-callback-entry-residue");
            Mem(home,32).Fill(0xC9); // callback owns exactly home0..31, never touches caller data.
            // Adversarial transition clobbers even more GPR/SIMD state than the normal ABI permits;
            // anchored RBP/RSP remain valid. This is model coverage, not proof of a real callback.
            for(int i=0;i<16;i++) if(i!=4 && i!=5) g[i]=0xD220000000000000UL+(ulong)i;
            foreach(byte[] row in xmm) Array.Fill(row,(byte)0xDD);
            mxcsr=0x3F80; flags=0x202UL|(callbackDf?0x400UL:0);g[0]=status;
            Need(Pop()==(ulong)(ip+5),"call-return-slot");
        }
        void Step()
        {
            if(ip==terminal){Need(At(0xEB,0xFE),"reject-loop");rejected=true;return;}
            if(At(0x9C)){Push(flags);ip++;return;}
            if(At(0x9D)){flags=Pop();ip++;return;}
            if(At(0x48,0x89,0xE5)){g[5]=g[4];ip+=3;return;}
            if(At(0x48,0x81,0xEC)){g[4]-=(uint)I32(ip+3);minimum=Math.Min(minimum,(int)g[4]);ip+=7;return;}
            if(At(0x48,0x83,0xE4,0xF0)){g[4]&=~15UL;minimum=Math.Min(minimum,(int)g[4]);ip+=4;return;}
            if(At(0x48,0xF7,0x45,0x08)){zero=(Get64((int)g[5]+8)&(uint)I32(ip+4))==0;ip+=8;return;}
            if(At(0x0F,0x85)){ip=zero?ip+6:ip+6+I32(ip+2);return;}
            if(code[ip]==0xF3)
            {
                int p=ip+1;bool high=code[p]==0x44;if(high)p++;
                Need(code[p]==0x0F && (code[p+1]==0x7F || code[p+1]==0x6F) && (code[p+2]&0xC7)==0x84 && code[p+3]==0x24,"finite-MOVDQU-form");
                int register=((code[p+2]>>3)&7)+(high?8:0), offset=I32(p+4);
                Need(offset==32+16*register,"XMM-offset");
                if(code[p+1]==0x7F){xmm[register].CopyTo(Mem((int)g[4]+offset,16));stores.Add(register);}
                else {Mem((int)g[4]+offset,16).CopyTo(xmm[register]);restores.Add(register);}
                ip=p+8;return;
            }
            if(At(0x0F,0xAE,0x9C,0x24)){BinaryPrimitives.WriteUInt32LittleEndian(Mem((int)g[4]+I32(ip+4),4),mxcsr);ip+=8;return;}
            if(At(0x0F,0xAE,0x94,0x24)){mxcsr=BinaryPrimitives.ReadUInt32LittleEndian(Mem((int)g[4]+I32(ip+4),4));ip+=8;return;}
            if(code[ip]==0xB9){g[1]=(uint)I32(ip+1);ip+=5;return;}
            if(At(0x48,0x8B,0x55)){g[2]=Get64((int)g[5]+(sbyte)code[ip+3]);ip+=4;return;}
            if(At(0x4C,0x8B,0x45)){g[8]=Get64((int)g[5]+(sbyte)code[ip+3]);ip+=4;return;}
            if(At(0x41,0xB9)){g[9]=(uint)I32(ip+2);ip+=6;return;}
            if(code[ip]==0xE8){Need(I32(ip+1)==0,"unlinked-call-addend");Callback();ip+=5;return;}
            if(At(0x83,0xF8,0x01)){zero=(uint)g[0]==1;ip+=3;return;}
            if(At(0x49,0xF7,0xC3)){zero=(g[11]&(uint)I32(ip+3))==0;ip+=7;return;}
            if(At(0x48,0x83,0x45)){int a=(int)g[5]+(sbyte)code[ip+3];Put64(a,Get64(a)+(ulong)(sbyte)code[ip+4]);ip+=5;return;}
            if(At(0x48,0x8D,0x65,0x90)){g[4]=g[5]-112;ip+=4;return;}
            bool extended=code[ip]==0x41;int opcode=code[ip+(extended?1:0)];int registerIndex=(opcode&7)+(extended?8:0);
            if(opcode>=0x50 && opcode<=0x57){Push(g[registerIndex]);ip+=extended?2:1;return;}
            if(opcode>=0x58 && opcode<=0x5F){g[registerIndex]=Pop();ip+=extended?2:1;return;}
            if(code[ip]==0xC3){Need(Pop()==ReturnAddress,"return-address");returned=true;ip++;return;}
            throw new InvalidOperationException("unsupported-instruction-at-"+ip);
        }
        internal object Verify()
        {
            for(int step=0;step<250 && !returned && !rejected;step++) Step();
            bool success=status==1 && !entryDf && !callbackDf;
            Need(returned==success && rejected!=success,"return-rejection-outcome");
            Need(callbacks==(entryDf?0:1),"callback-count");
            Need(stack.AsSpan(768+residue).SequenceEqual(initial.AsSpan(768+residue)),"caller-return-home-canary");
            Need(stack.AsSpan(0,320+residue).SequenceEqual(initial.AsSpan(0,320+residue)),"lower-stack-canary");
            if(success)
            {
                for(int i=0;i<16;i++) Need(g[i]==(i==4?(ulong)s+8:original[i]+(op==3 && (i==6 || i==7)?8UL:0)),"GPR-return-"+i);
                for(int i=0;i<16;i++) Need(xmm[i].AsSpan().SequenceEqual(originalXmm[i]),"XMM-return-"+i);
                Need(mxcsr==0x1F80 && flags==originalFlags && restores.SequenceEqual(Enumerable.Range(0,16)),"MXCSR-flags-restore");
                Need(s-minimum==(residue==0?440:448),"maximum-local-call-depth");
            }
            else
            {
                Need(restores.Count==0,"rejection-before-restores");
                Need(Get64(s-56)==original[6] && Get64(s-64)==original[7],"no-saved-byref-advance-on-reject");
            }
            return new {Operation=op,EntryResidue=residue,Status=status,EntryDf=entryDf,CallbackDf=callbackDf,Callbacks=callbacks,Returned=returned,Rejected=rejected,MeasuredModelDepth=s-minimum,ActualNativeExecution=false};
        }
    }
}

internal static class Program
{
    public static int Main(string[] args)
    {
        const string group="m04.010.thunk.offline";
        Console.WriteLine("TEST_START "+group);
        try
        {
            ThunkControls.Need(args.Length==3,"output and reviewed serializer hash and size arguments");
            object result=ThunkControls.Run(args[0],args[1],int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(args[0],"controls.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})+"\n",new UTF8Encoding(false));
            Console.WriteLine("TEST_PASS "+group);Console.WriteLine("TEST_PASS host.tests");return 0;
        }
        catch(Exception e){Console.WriteLine("TEST_FAIL "+group+" "+e.Message);Console.WriteLine("TEST_FAIL host.tests failures=1");Console.Error.WriteLine(e);return 1;}
    }
}
#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

internal static class CheckedReferenceBarrierThunkGeneratorTests
{
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        const string name = "m04.010.thunks";
        HostCheck.Run(name, () =>
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "issue537-thunk-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            foreach (string source in new[]
            {
                "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
                "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
                "tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs",
                "tests/OS.HostTests/CheckedReferenceBarrierThunkGeneratorTests.cs", "global.json"
            })
                File.Copy(Path.Combine(root, source), Path.Combine(folder, Path.GetFileName(source)), false);
            File.WriteAllText(Path.Combine(folder, "Probe.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
                    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <DefineConstants>CHECKED_REFERENCE_THUNK_CHILD</DefineConstants><UseSharedCompilation>false</UseSharedCompilation>
                  </PropertyGroup>
                </Project>
                """, new UTF8Encoding(false));
            Run(folder, "build", 120000, ["build", "Probe.csproj", "--nologo", "--disable-build-servers", "-m:1",
                "-nodeReuse:false", "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"]);
            string output = Run(folder, "run", 30000,
                [Path.Combine(folder, "bin", "Debug", "net10.0", "Probe.dll"), Path.Combine(folder, "output"), OS.Tooling.EfiContracts.SerializerObjectSha256.ToUpperInvariant(), OS.Tooling.EfiContracts.SerializerObjectBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            if (output != "TEST_START m04.010.thunk.offline" + Environment.NewLine +
                "TEST_PASS m04.010.thunk.offline" + Environment.NewLine + "TEST_PASS host.tests" + Environment.NewLine)
                throw new InvalidOperationException("complete thunk child output");
        });
    }

    private static string Run(string folder, string label, int timeout, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = folder, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        File.WriteAllText(Path.Combine(folder, label + ".command.json"), JsonSerializer.Serialize(new
            { Executable = "dotnet", Arguments = arguments, WorkingDirectory = folder, TimeoutMilliseconds = timeout }));
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("child failed to start");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        bool timedOut = !process.WaitForExit(timeout);
        if (timedOut) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder, label + ".stdout.log"), stdout, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, label + ".stderr.log"), stderr, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, label + ".result.json"), JsonSerializer.Serialize(new
            { NativeExit = process.ExitCode, TimedOut = timedOut }));
        if (timedOut || process.ExitCode != 0 || stderr.Length != 0)
            throw new InvalidOperationException("child failed: " + label + "; retained " + folder);
        return stdout;
    }
}
#endif
