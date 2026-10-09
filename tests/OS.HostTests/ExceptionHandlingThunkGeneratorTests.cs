using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static class ExceptionHandlingThunkGeneratorTests
{
    private const string Hash = "DD32DC30808C3B6DD42981DF652B95F62236BDC8C45C1DAA3DDE32BAB1C981AB";
    private static void Need(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }

    [ModuleInitializer]
    internal static void RunAll()
    {
        const string name = "m04.021.thunk";
        Console.WriteLine("TEST_START " + name);
        try
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "simple-catch-thunk", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Run(root, folder, "build", ["build", "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj", "--nologo", "-m:1"]);
            string tool = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net10.0/OS.X64TextSerializerGenerator.dll");
            Run(root, folder, "emit", [tool, "--simple-throw-catch", "--output-root", folder, "--output-file", "catch.obj"]);
            byte[] obj = File.ReadAllBytes(Path.Combine(folder, "catch.obj"));
            Need(obj.Length == 1350 && Convert.ToHexString(SHA256.HashData(obj)) == Hash, "fixed complete object");
            Need(BinaryPrimitives.ReadUInt16LittleEndian(obj) == 0x8664 && BinaryPrimitives.ReadUInt16LittleEndian(obj.AsSpan(2)) == 1, "one AMD64 text section");
            Need(BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(36)) == 1032 && BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(40)) == 60, "finite text extent");
            int relocation = (int)BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(44));
            Need(BinaryPrimitives.ReadUInt16LittleEndian(obj.AsSpan(52)) == 3, "two callbacks and one EH address");
            foreach (var pair in new[] { (330, 1), (811, 2), (1027, 4) })
            {
                Need(BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(relocation)) == pair.Item1 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(relocation + 4)) == pair.Item2 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(obj.AsSpan(relocation + 8)) == 4, "exact REL32 ownership");
                relocation += 10;
            }
            byte[] code = obj.AsSpan(60, 1032).ToArray();
            Need(code.AsSpan(1024).SequenceEqual(new byte[] { 0x48,0x8d,0x05,0,0,0,0,0xc3 }), "ordinary exact table-address getter");
            for (int failure = 0; failure <= 6; failure++) VerifyTransfer(code, failure);
            VerifyFinallyDefinition(tool, folder, code);
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL " + name + " " + ex); throw; }
    }

    // Inspect the actual emitted definition without requiring the integration
    // owner's new CLI mode or adding a host-tool reference to this project.
    private static void VerifyFinallyDefinition(string tool, string folder, byte[] legacyCode)
    {
        Assembly assembly = Assembly.LoadFile(Path.GetFullPath(tool));
        Type generator = assembly.GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator", true)!;
        MethodInfo define = generator.GetMethod("Define", [typeof(bool)])!;
        object original = define.Invoke(null, [false])!;
        object definition = define.Invoke(null, [true])!;
        static T Read<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
        object originalSection = Read<Array>(original, "Sections").GetValue(0)!;
        Need(Read<byte[]>(originalSection, "RawData").SequenceEqual(legacyCode), "explicit false preserves original text");
        Need(Read<ushort>(definition, "Machine") == 0x8664 && Read<uint>(definition, "TimeDateStamp") == 0, "deterministic finally AMD64 definition");
        Array sections = Read<Array>(definition, "Sections"), symbols = Read<Array>(definition, "Symbols");
        Need(sections.Length == 1 && symbols.Length == 6, "one text section and one additional validator symbol");
        string[] expectedNames = ["RhpThrowEx", "CLROS_Eh_Select", "CLROS_Eh_ValidateContinuation",
            "CLROS_Eh_GetClauseTable", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Catcher"];
        for (int i = 0; i < expectedNames.Length; i++)
            Need(Read<string>(symbols.GetValue(i)!, "Name") == expectedNames[i], "original symbol identity " + i);
        object section = sections.GetValue(0)!;
        byte[] code = Read<byte[]>(section, "RawData");
        Need(Read<string>(section, "Name") == ".text" && Read<uint>(section, "Characteristics") == 0x60500020, "same executable read-only section");
        Need(Read<string>(symbols.GetValue(5)!, "Name") == "CLROS_Eh_ValidateFinally" &&
            Read<short>(symbols.GetValue(5)!, "SectionNumber") == 0 &&
            Read<uint>(symbols.GetValue(5)!, "Value") == 0 &&
            Read<ushort>(symbols.GetValue(5)!, "Type") == 0x20 &&
            Read<byte>(symbols.GetValue(5)!, "StorageClass") == 2, "separate undefined external finally validator");
        uint getter = Read<uint>(symbols.GetValue(3)!, "Value");
        Need(getter % 16 == 0 && getter + 8 == code.Length &&
            code.AsSpan((int)getter).SequenceEqual(new byte[] { 0x48,0x8d,0x05,0,0,0,0,0xc3 }), "bounded original EH getter");
        Array relocations = Read<Array>(section, "Relocations");
        Need(relocations.Length == 4, "selector, cleanup validator, catch validator and EH data relocations");
        uint[] expectedSymbols = [1,5,2,4];
        int[] offsets = new int[4];
        for (int i = 0; i < offsets.Length; i++)
        {
            object relocation = relocations.GetValue(i)!;
            offsets[i] = checked((int)Read<uint>(relocation, "Offset"));
            Need(Read<uint>(relocation, "SymbolIndex") == expectedSymbols[i] && Read<ushort>(relocation, "Type") == 4 &&
                BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offsets[i],4)) == 0, "exact finally REL32 identity");
            Need(i == 0 || offsets[i] > offsets[i-1] + 4, "disjoint ordered relocations");
            if (i < 3) Need(code[offsets[i]-1] == 0xe8, "ordinary validation call relocation");
        }
        Need(offsets[0] == 330 && offsets[3] == getter + 3, "unchanged capture/selector and table relocation");
        File.WriteAllBytes(Path.Combine(folder, "finally.text.bin"), code);
        File.WriteAllLines(Path.Combine(folder, "finally-definition.txt"),
            [$"TextBytes={code.Length}", $"TextSha256={Convert.ToHexString(SHA256.HashData(code))}", $"Relocations={string.Join(',',offsets)}"]);
        for (int failure = 0; failure <= 9; failure++)
            VerifyTransfer(code, failure, true, offsets[1]-1, offsets[2]-1);
    }

    // Finite instruction interpreter for this bridge's emitted vocabulary. It
    // observes real saved/loaded bytes; callbacks deliberately clobber scratch
    // state and the catch changes nonvolatile GP/XMM values that must survive
    // resume. Cleanup and validation scratch sets must not replace selection.
    private static void VerifyTransfer(byte[] code, int failure, bool singleFinally = false, int finallyValidator = -1, int continuationValidator = 810)
    {
        ulong[] g = Enumerable.Range(0,16).Select(i => 0x100000UL+(ulong)i).ToArray();
        byte[][] xmm = Enumerable.Range(0,16).Select(i => Enumerable.Repeat((byte)(i+1),16).ToArray()).ToArray();
        byte[] memory = new byte[8192];
        const ulong entry = 6008, rawRip = 0x180010009, continuation = 0x18002001b, exception = 0x700000;
        ulong frame = entry-840, selectedSp = entry+8+48;
        g[4]=entry;g[1]=exception;
        ulong[] original=(ulong[])g.Clone(), selected=(ulong[])g.Clone(), post=(ulong[])g.Clone();
        int[] nv=[3,5,6,7,12,13,14,15];
        byte[][] originalXmm=xmm.Select(x=>(byte[])x.Clone()).ToArray(), selectedXmm=xmm.Select(x=>(byte[])x.Clone()).ToArray();
        ulong flags=failure==1?0x400UL:0UL,cr4=failure==2?1UL<<23:0UL;
        uint mxcsr=0x1F80;ushort x87=0x37F;bool zero=false;int ip=0,selectCalls=0,catchCalls=0,validateCalls=0,cleanupCalls=0,cleanupValidations=0;
        const ulong cleanupTarget=0x180020080, cleanupStamp=0xF1A11;
        ulong parentLocal=selectedSp+24;
        byte[]? selectedBytes=null;
        void Put(ulong at,ulong v)=>BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan(checked((int)at),8),v);
        ulong Get(ulong at)=>BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan(checked((int)at),8));
        int I32(int at)=>BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(at,4));
        bool At(params byte[] b)=>code.AsSpan(ip,b.Length).SequenceEqual(b);
        Put(entry,rawRip);
        for(int step=0;step<400;step++)
        {
            if(At(0xEB,0xFE))
            {
                Need(failure!=0,"successful path cannot halt");
                Need(catchCalls==(failure>=4&&failure<=6?1:0) && validateCalls==(failure==6?1:0),"reject before next transfer boundary");
                Need(cleanupCalls==(singleFinally&&failure>=4?1:0) &&
                    cleanupValidations==(singleFinally&&(failure>=4&&failure<=6||failure==9)?1:0),"cleanup rejection never reaches catch");return;
            }
            if(At(0xFF,0xE0))
            {
                Need(failure==0 && g[0]==continuation && g[4]==selectedSp && selectCalls==1 && catchCalls==1 && validateCalls==1,"actual continuation/SP jump");
                foreach(int r in nv)Need(g[r]==post[r],"postcatch register retained "+r);
                for(int r=6;r<16;r++)Need(xmm[r].SequenceEqual(selectedXmm[r]),"postcatch XMM retained "+r);
                Need(mxcsr==0x1F80 && x87==0x37F && Get(entry)==rawRip&&
                    Get(parentLocal)==(singleFinally?cleanupStamp:0),"controls, parent local and original return word");return;
            }
            if(code[ip]==0x9c){g[4]-=8;Put(g[4],flags);ip++;continue;}
            if(code[ip]==0x58){g[0]=Get(g[4]);g[4]+=8;ip++;continue;}
            if(At(0x48,0x81,0xec)){g[4]-=(uint)I32(ip+3);ip+=7;continue;}
            if(At(0x0f,0x20,0xe0)){g[0]=cr4;ip+=3;continue;}
            if(At(0x4c,0x89,0xdc)){g[4]=g[11];ip+=3;continue;}
            if(At(0x48,0xf7,0x84,0x24)){zero=(Get(g[4]+(uint)I32(ip+4))&(uint)I32(ip+8))==0;ip+=12;continue;}
            if(At(0x0f,0x85)){int next=ip+6;ip=zero?next:next+I32(ip+2);continue;}
            if(At(0x83,0xf8,0x01)){zero=(uint)g[0]==1;ip+=3;continue;}
            if(At(0x0f,0xae,0x9c,0x24)||At(0x0f,0xae,0x94,0x24))
            {int at=checked((int)(g[4]+(uint)I32(ip+4)));if(code[ip+2]==0x9c)BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(at),mxcsr);else mxcsr=BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan(at));ip+=8;continue;}
            if(At(0xd9,0xbc,0x24)||At(0xd9,0xac,0x24))
            {int at=checked((int)(g[4]+(uint)I32(ip+3)));if(code[ip+1]==0xbc)BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(at),x87);else x87=BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan(at));ip+=7;continue;}
            if(code[ip]==0xf3)
            {
                int extra=code[ip+1]==0x44?1:0,p=ip+1+extra,r=((code[p+2]>>3)&7)+8*extra;
                Need(code[p]==0x0f && code[p+3]==0x24 && (code[p+1]==0x7f||code[p+1]==0x6f),"XMM instruction");
                int at=checked((int)(g[4]+(uint)I32(p+4)));
                if(code[p+1]==0x7f)xmm[r].CopyTo(memory,at);else xmm[r]=memory.AsSpan(at,16).ToArray();
                ip=p+8;continue;
            }
            if((code[ip]==0x48||code[ip]==0x4c) && (code[ip+1]==0x89||code[ip+1]==0x8b||code[ip+1]==0x8d) && code[ip+3]==0x24)
            {
                int r=((code[ip+2]>>3)&7)+(code[ip]==0x4c?8:0);ulong at=g[4]+(uint)I32(ip+4);
                if(code[ip+1]==0x89)Put(at,g[r]);else g[r]=code[ip+1]==0x8d?at:Get(at);ip+=8;continue;
            }
            if(code[ip]==0xe8)
            {
                Need(g[4]==frame && g[4]%16==0,"ordinary callback alignment");
                memory.AsSpan((int)frame,32).Fill(0xCC); // each callback owns its home area
                if(ip==329)
                {
                    selectCalls++;Need(g[1]==exception&&g[2]==frame+32&&g[8]==frame+272&&g[9]==frame+776,"selector args");
                    Need(Get(frame+32)==rawRip&&Get(frame+40)==entry+8,"raw preprolog input");
                    for(int i=0;i<nv.Length;i++)Need(Get(frame+48+(ulong)i*8)==original[nv[i]],"captured nonvolatile");
                    for(int r=6;r<16;r++)Need(memory.AsSpan((int)frame+112+(r-6)*16,16).SequenceEqual(originalXmm[r]),"captured XMM");
                    Put(frame+272,0x180020033);Put(frame+280,singleFinally?cleanupTarget:0);Put(frame+288,0x18002001a);Put(frame+296,selectedSp);
                    for(int i=0;i<nv.Length;i++){selected[nv[i]]+=0x10000;Put(frame+304+(ulong)i*8,selected[nv[i]]);}
                    selected[5]=selectedSp+48;Put(frame+312,selected[5]);
                    for(int r=6;r<16;r++){selectedXmm[r]=Enumerable.Repeat((byte)(0x40+r),16).ToArray();selectedXmm[r].CopyTo(memory,(int)frame+368+(r-6)*16);}
                    selectedBytes=memory.AsSpan((int)frame+288,240).ToArray();
                    g[0]=failure==3?0UL:1UL;
                }
                else if(singleFinally && ip==finallyValidator)
                {
                    cleanupValidations++;
                    Need(cleanupCalls==1&&cleanupValidations==1&&catchCalls==0&&validateCalls==0,"cleanup validated before catch");
                    Need(g[1]==frame+272&&g[2]==frame+776&&g[8]==frame+808,"finally validator arguments");
                    Need(Get(parentLocal)==cleanupStamp&&Get(frame+280)==cleanupTarget&&
                        memory.AsSpan((int)frame+288,240).SequenceEqual(selectedBytes),"parent local changed without changing selected context");
                    Need(Get(frame+808)==0&&Get(frame+816)==0&&
                        BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan((int)frame+824))==0x1F81&&
                        BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan((int)frame+828))==0x37F,"actual cleanup controls captured");
                    // Deliberately clobber callback state independently of the
                    // cleanup. The bridge must reload the original selection.
                    foreach(int r in nv)g[r]=0xBADF0000UL+(ulong)r;
                    for(int r=6;r<16;r++)xmm[r]=Enumerable.Repeat((byte)(0xE0+r),16).ToArray();
                    mxcsr=0x5F80;x87=0x77F;
                    g[0]=failure==9?0UL:1UL;
                }
                else
                {
                    Need(ip==continuationValidator,"only admitted ordinary callbacks");validateCalls++;
                    Need(g[1]==continuation&&g[2]==frame+528&&g[8]==frame+272&&g[9]==frame+776,"validator args");
                    Need(Get(frame+528)==continuation&&Get(frame+536)==selectedSp,"actual returned continuation snapshot");
                    for(int i=0;i<nv.Length;i++)Need(Get(frame+544+(ulong)i*8)==post[nv[i]],"postcatch snapshot");
                    foreach(int r in nv)g[r]=0xBAD;for(int r=6;r<16;r++)xmm[r]=new byte[16];
                    g[0]=failure==6?0UL:1UL;
                }
                ip+=5;continue;
            }
            if(At(0xff,0x94,0x24))
            {
                if(singleFinally && I32(ip+3)==280)
                {
                    Need(g[4]==frame&&Get(frame+280)==cleanupTarget&&selectCalls==1&&cleanupCalls==0&&catchCalls==0,"one selected cleanup call");
                    foreach(int r in nv)Need(g[r]==selected[r],"cleanup parent context restored");
                    for(int r=6;r<16;r++)Need(xmm[r].SequenceEqual(selectedXmm[r]),"cleanup parent XMM restored");
                    cleanupCalls++;Put(parentLocal,cleanupStamp);
                    foreach(int r in nv)g[r]=0xF1000000UL+(ulong)r;
                    for(int r=6;r<16;r++)xmm[r]=Enumerable.Repeat((byte)(0xA0+r),16).ToArray();
                    g[0]=0xDEADF100;mxcsr=0x1F81;
                    if(failure==7)flags=0x400;if(failure==8)cr4=1UL<<23;
                    ip+=7;continue;
                }
                Need(I32(ip+3)==272&&g[1]==exception&&g[4]==frame,"special catch call");
                Need(cleanupCalls==(singleFinally?1:0)&&cleanupValidations==(singleFinally?1:0)&&
                    Get(parentLocal)==(singleFinally?cleanupStamp:0)&&mxcsr==0x1F80&&x87==0x37F,"cleanup side effect and entry FP controls before catch");
                foreach(int r in nv)Need(g[r]==selected[r],"selected context restored");
                for(int r=6;r<16;r++)Need(xmm[r].SequenceEqual(selectedXmm[r]),"selected XMM restored");
                catchCalls++;
                foreach(int r in nv)if(r!=5)g[r]=0xCA7C0000UL+(ulong)r;
                for(int r=6;r<16;r++){xmm[r][0]=(byte)(0x70+r);selectedXmm[r][0]=(byte)(0x70+r);}
                post=(ulong[])g.Clone();g[0]=continuation;
                if(failure==4)flags=0x400;if(failure==5)cr4=1UL<<23;
                ip+=7;continue;
            }
            throw new InvalidOperationException("unexpected bridge opcode at "+ip);
        }
        throw new InvalidOperationException("bridge did not terminate within finite instruction budget");
    }

    private static void Run(string root,string folder,string name,string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true } };
        foreach(string arg in args)process.StartInfo.ArgumentList.Add(arg);
        Need(process.Start(),"process start");var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(120000)){process.Kill(true);throw new InvalidOperationException("bounded generator timeout");}
        Task.WaitAll(stdout,stderr);File.WriteAllText(Path.Combine(folder,name+".out"),stdout.Result);File.WriteAllText(Path.Combine(folder,name+".err"),stderr.Result);
        Need(process.ExitCode==0&&stderr.Result.Length==0,"generator command "+name);
    }
}
