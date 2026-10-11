using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static class ExceptionHandlingThunkGeneratorTests
{
    private static readonly string Hash = OS.Tooling.EfiContracts.SimpleCatchObjectSha256.ToUpperInvariant();
    private static void Need(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        const string name = "m04.021.thunk";
        HostCheck.Run(name, () =>
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "simple-catch-thunk", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Run(root, folder, "build", ["build", "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj", "--nologo", "-m:1"]);
            string tool = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net10.0/OS.X64TextSerializerGenerator.dll");
            Run(root, folder, "emit", [tool, "--simple-throw-catch", "--output-root", folder, "--output-file", "catch.obj"]);
            byte[] obj = File.ReadAllBytes(Path.Combine(folder, "catch.obj"));
            Need(obj.Length == OS.Tooling.EfiContracts.SimpleCatchObjectBytes && Convert.ToHexString(SHA256.HashData(obj)) == Hash, "fixed complete object");
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
            VerifyFinallyCli(root, tool, folder);
            VerifyRethrow(root, tool, folder);
        });
    }

    // Inspect the emitted definition without adding a host-tool project reference.
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

    private static void VerifyFinallyCli(string root, string tool, string folder)
    {
        string finallyHash = OS.Tooling.EfiContracts.SingleFinallyObjectSha256.ToUpperInvariant();
        const string name = "m04.021.finally.thunk.generate";
        string output = Run(root, folder, "finally-emit",
            [tool, "--single-finally-catch", "--output-root", folder, "--output-file", "finally.obj"]);
        byte[] original = File.ReadAllBytes(Path.Combine(folder, "finally.obj"));
        Need(original.Length == OS.Tooling.EfiContracts.SingleFinallyObjectBytes && Convert.ToHexString(SHA256.HashData(original)) == finallyHash,
            "finally CLI fixed complete object");
        byte[] modeledCode = File.ReadAllBytes(Path.Combine(folder, "finally.text.bin"));
        Need(original.AsSpan(60, modeledCode.Length).SequenceEqual(modeledCode),
            "finally CLI emits the instruction-model bytes");
        Need(output.Contains("TEST_PASS " + name, StringComparison.Ordinal) &&
            output.Contains("M04_SINGLE_FINALLY_THUNK_READY bytes=" + OS.Tooling.EfiContracts.SingleFinallyObjectBytes + " sha256=" + finallyHash + " state=", StringComparison.Ordinal),
            "finally CLI exact success identity");
        Run(root, folder, "finally-repeat",
            [tool, "--single-finally-catch", "--output-root", folder, "--output-file", "finally.obj"]);
        Need(File.ReadAllBytes(Path.Combine(folder, "finally.obj")).SequenceEqual(original),
            "finally CLI deterministic repeat");

        string[][] rejectedModes =
        [
            ["--single-finally-catch", "--single-finally-catch"],
            ["--single-finally-catch", "--simple-throw-catch"],
            ["--simple-throw-catch", "--single-finally-catch"],
            ["--single-finally-catch", "--interface-dispatch"],
            ["--interface-dispatch", "--single-finally-catch"],
            ["--single-finally-catch", "--checked-reference-barriers"],
            ["--checked-reference-barriers", "--single-finally-catch"]
        ];
        for (int i = 0; i < rejectedModes.Length; i++)
        {
            string rejected = Run(root, folder, "finally-reject-" + i,
                [tool, .. rejectedModes[i], "--output-root", folder, "--output-file", "finally.obj"], expectedExit: 2);
            string reason = i == 0 ? "argument_duplicate" : "argument_mode_conflict";
            Need(rejected.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') ==
                "TEST_START " + name + "\nTEST_FAIL " + name + " " + reason &&
                File.ReadAllBytes(Path.Combine(folder, "finally.obj")).SequenceEqual(original),
                "finally CLI rejects mode before replacing output " + i);
        }
    }

    // Finite instruction interpreter for this bridge's emitted vocabulary. It
    // observes real saved/loaded bytes; callbacks deliberately clobber scratch
    // state and the catch changes nonvolatile GP/XMM values that must survive
    // resume. Cleanup and validation scratch sets must not replace selection.
    private static void VerifyRethrow(string root, string tool, string folder)
    {
        const string name = "m04.021.rethrow.thunk";
        Console.WriteLine("TEST_START " + name);
        try
        {
            Type generator = Assembly.LoadFile(Path.GetFullPath(tool))
                .GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator", true)!;
            MethodInfo define = generator.GetMethod("DefineRethrow", [typeof(bool)])!;
            static T Read<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
            foreach (bool outer in new[] { false, true })
            {
                string role = outer ? "outer" : "inner";
                object definition = define.Invoke(null, [outer])!;
                Array sections = Read<Array>(definition, "Sections"), symbols = Read<Array>(definition, "Symbols");
                Need(Read<ushort>(definition, "Machine") == 0x8664 && Read<uint>(definition, "TimeDateStamp") == 0,
                    "deterministic rethrow AMD64 definition");
                Need(sections.Length == 1, "one executable section");
                string[] names = outer
                    ? ["RhpRethrow", "CLROS_Rethrow_SelectOuter", "CLROS_Rethrow_ValidateOuter", "CLROS_Rethrow_OuterClauses",
                       "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__RethrowOuter"]
                    : ["RhpThrowEx", "CLROS_Rethrow_SelectInner", "CLROS_Rethrow_RejectInnerReturn", "CLROS_Rethrow_InnerClauses",
                       "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__RethrowInner", "CLROS_Rethrow_ValidateFinally"];
                Need(symbols.Cast<object>().Select(s => Read<string>(s, "Name")).SequenceEqual(names), "role-specific symbols");
                object section = sections.GetValue(0)!;
                byte[] code = Read<byte[]>(section, "RawData");
                Array relocs = Read<Array>(section, "Relocations");
                Need(relocs.Length == (outer ? 3 : 4), "all callbacks and one clause table");
                uint[] targets = outer ? [1U, 2U, 4U] : [1U, 5U, 2U, 4U];
                int[] offsets = new int[relocs.Length];
                for (int i = 0; i < offsets.Length; i++)
                {
                    object rel = relocs.GetValue(i)!;
                    offsets[i] = checked((int)Read<uint>(rel, "Offset"));
                    Need(Read<uint>(rel, "SymbolIndex") == targets[i] && Read<ushort>(rel, "Type") == 4,
                        "relocation target and REL32 type");
                    Need(code.AsSpan(offsets[i], 4).SequenceEqual(new byte[4]), "unbound displacement remains zero");
                }
                if (!outer) Need(code.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "finally.text.bin"))),
                    "inner bridge preserves the already-modeled finally instruction stream");
                for (int failure = 0; failure <= (outer ? 6 : 9); failure++)
                    VerifyTransfer(code, failure, !outer, outer ? -1 : offsets[1] - 1,
                        offsets[outer ? 1 : 2] - 1, outer, offsets[0] - 1);
                string[] args = [tool, "--rethrow-" + role, "--output-root", folder, "--output-file", role + ".obj"];
                string output = Run(root, folder, role + "-emit", args);
                byte[] obj = File.ReadAllBytes(Path.Combine(folder, role + ".obj"));
                string expectedHash = outer ? "635C903FF77EE3C7DE3D1EDCE3E6F015DDEF204454A50432DFCF9692686E337B"
                    : "D30D37F5AD93A70A66D2ED2E69A184A75DB903C5A1D69C4E31571569016E7A10";
                Need(obj.Length == (outer ? 1382 : 1732) && Convert.ToHexString(SHA256.HashData(obj)) == expectedHash,
                    "fixed complete rethrow object");
                Need(obj.AsSpan(60, code.Length).SequenceEqual(code), "modeled bytes are exactly serialized");
                Need(output.Contains("TEST_PASS m04.021.rethrow.thunk.generate", StringComparison.Ordinal) &&
                    output.Contains("M04_RETHROW_THUNK_READY role=" + role + " bytes=" + obj.Length + " sha256=" +
                        Convert.ToHexString(SHA256.HashData(obj)), StringComparison.Ordinal), "complete object receipt");
                Run(root, folder, role + "-repeat", args);
                Need(obj.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, role + ".obj"))), "deterministic repeat");
            }
            string[] modes = ["--rethrow-inner", "--rethrow-outer", "--single-finally-catch", "--simple-throw-catch",
                "--interface-dispatch", "--checked-reference-barriers"];
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (string first in modes.Take(2))
            foreach (string second in modes)
            foreach (var pair in new[] { (first, second), (second, first) })
            {
                if (!pairs.Add(pair.Item1 + " " + pair.Item2)) continue;
                string file = "rejected-rethrow-" + pairs.Count + ".obj";
                string output = Run(root, folder, "rethrow-conflict-" + pairs.Count,
                    [tool, pair.Item1, pair.Item2, "--output-root", folder, "--output-file", file], 2);
                Need(output.Contains(pair.Item1 == pair.Item2 ? "argument_duplicate" : "argument_mode_conflict", StringComparison.Ordinal)
                    && !File.Exists(Path.Combine(folder, file)), "invalid mode leaves no artifact");
            }
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL " + name + " " + ex); throw; }
    }

    internal static void VerifyTransfer(byte[] code, int failure, bool singleFinally = false, int finallyValidator = -1,
        int continuationValidator = 810, bool rethrow = false, int selector = 329,
        bool nestedFinally = false, int secondFinallyValidator = -1, bool frameDispatch = false)
    {
        ulong[] g = Enumerable.Range(0,16).Select(i => 0x100000UL+(ulong)i).ToArray();
        byte[][] xmm = Enumerable.Range(0,16).Select(i => Enumerable.Repeat((byte)(i+1),16).ToArray()).ToArray();
        byte[] memory = new byte[8192];
        const ulong entry = 6008, rawRip = 0x180010009, continuation = 0x18002001b, exception = 0x700000;
        ulong frame = entry-(frameDispatch ? 1080UL : nestedFinally ? 856UL : 840UL);
        ulong selectedSp = entry+8+(frameDispatch ? 128UL : 48UL), cleanupSp = entry+8+48;
        ulong selectedOffset = nestedFinally ? 304UL : 288UL;
        ulong cleanupOffset = frameDispatch ? 528UL : selectedOffset;
        ulong postOffset = frameDispatch ? 768UL : nestedFinally ? 544UL : 528UL;
        ulong exceptionOffset = frameDispatch ? 1008UL : nestedFinally ? 784UL : 768UL;
        ulong entryControl = frameDispatch ? 1016UL : nestedFinally ? 792UL : 776UL;
        ulong postControl = frameDispatch ? 1048UL : nestedFinally ? 824UL : 808UL;
        int expectedCleanups = nestedFinally ? 2 : singleFinally ? 1 : 0;
        g[4]=entry;g[1]=rethrow ? 0xDEADC0DEUL : exception;
        ulong[] original=(ulong[])g.Clone(), selected=(ulong[])g.Clone(), post=(ulong[])g.Clone();
        int[] nv=[3,5,6,7,12,13,14,15];
        byte[][] originalXmm=xmm.Select(x=>(byte[])x.Clone()).ToArray(), selectedXmm=xmm.Select(x=>(byte[])x.Clone()).ToArray();
        ulong[] cleanupRegisters = (ulong[])selected.Clone();
        byte[][] cleanupXmm = xmm.Select(x=>(byte[])x.Clone()).ToArray();
        ulong flags=failure==1?0x400UL:0UL,cr4=failure==2?1UL<<23:0UL;
        uint mxcsr=0x1F80;ushort x87=0x37F;bool zero=false;int ip=0,selectCalls=0,catchCalls=0,validateCalls=0,cleanupCalls=0,cleanupValidations=0;
        const ulong cleanupTarget=0x180020080, cleanupStamp=0xF1A11;
        ulong parentLocal=(frameDispatch ? cleanupSp : selectedSp)+24;
        byte[]? selectedBytes=null, cleanupBytes=null;
        void Put(ulong at,ulong v)=>BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan(checked((int)at),8),v);
        ulong Get(ulong at)=>BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan(checked((int)at),8));
        int I32(int at)=>BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(at,4));
        bool At(params byte[] b)=>code.AsSpan(ip,b.Length).SequenceEqual(b);
        Put(entry,rawRip);
        for(int step=0;step<600;step++)
        {
            if(At(0xEB,0xFE))
            {
                Need(failure!=0,"successful path cannot halt");
                Need(catchCalls==(failure>=4&&failure<=6?1:0) && validateCalls==(failure==6?1:0),"reject before next transfer boundary");
                int expectedCalls = failure <= 3 ? 0 : nestedFinally && (failure <= 6 || failure >= 10) ? 2 : singleFinally ? 1 : 0;
                int expectedValidations = failure <= 3 || failure is 7 or 8 ? 0 : failure == 9 ? 1 :
                    nestedFinally && failure is 10 or 11 ? 1 : nestedFinally ? 2 : singleFinally ? 1 : 0;
                Need(cleanupCalls==expectedCalls && cleanupValidations==expectedValidations,"cleanup rejection never reaches next transfer");return;
            }
            if(At(0xFF,0xE0))
            {
                Need(failure==0 && g[0]==continuation && g[4]==selectedSp && selectCalls==1 && catchCalls==1 && validateCalls==1,"actual continuation/SP jump");
                foreach(int r in nv)Need(g[r]==post[r],"postcatch register retained "+r);
                for(int r=6;r<16;r++)Need(xmm[r].SequenceEqual(selectedXmm[r]),"postcatch XMM retained "+r);
                Need(mxcsr==0x1F80 && x87==0x37F && Get(entry)==rawRip&&
                    cleanupCalls==expectedCleanups && cleanupValidations==expectedCleanups &&
                    Get(parentLocal)==(singleFinally?cleanupStamp+(ulong)expectedCleanups-1:0),"controls, parent local and original return word");return;
            }
            if(code[ip]==0x9c){g[4]-=8;Put(g[4],flags);ip++;continue;}
            if(code[ip]==0x58){g[0]=Get(g[4]);g[4]+=8;ip++;continue;}
            if(At(0x48,0x81,0xec)){g[4]-=(uint)I32(ip+3);ip+=7;continue;}
            if(At(0x31,0xc9)){Need(rethrow,"zero argument only at no-argument rethrow entry");g[1]=0;zero=true;ip+=2;continue;}
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
                if(ip==selector)
                {
                    selectCalls++;Need(g[1]==(rethrow?0:exception)&&g[2]==frame+32&&g[8]==frame+272&&g[9]==frame+entryControl,"selector args");
                    Need(Get(frame+exceptionOffset)==(rethrow?0:exception),"original exception argument retained");
                    Need(Get(frame+32)==rawRip&&Get(frame+40)==entry+8,"raw preprolog input");
                    for(int i=0;i<nv.Length;i++)Need(Get(frame+48+(ulong)i*8)==original[nv[i]],"captured nonvolatile");
                    for(int r=6;r<16;r++)Need(memory.AsSpan((int)frame+112+(r-6)*16,16).SequenceEqual(originalXmm[r]),"captured XMM");
                    Put(frame+272,0x180020033);Put(frame+280,rethrow?exception:singleFinally?cleanupTarget:0);
                    if(nestedFinally){Put(frame+288,cleanupTarget+0x100);Put(frame+296,0);}
                    Put(frame+selectedOffset,0x18002001a);Put(frame+selectedOffset+8,selectedSp);
                    for(int i=0;i<nv.Length;i++){selected[nv[i]]+=0x10000;Put(frame+selectedOffset+16+(ulong)i*8,selected[nv[i]]);}
                    selected[5]=selectedSp+48;Put(frame+selectedOffset+24,selected[5]);
                    for(int r=6;r<16;r++){selectedXmm[r]=Enumerable.Repeat((byte)(0x40+r),16).ToArray();selectedXmm[r].CopyTo(memory,(int)(frame+selectedOffset+80)+(r-6)*16);}
                    selectedBytes=memory.AsSpan((int)(frame+selectedOffset),240).ToArray();
                    if(frameDispatch)
                    {
                        Put(frame+cleanupOffset,0x18003002b);Put(frame+cleanupOffset+8,cleanupSp);
                        for(int i=0;i<nv.Length;i++)
                        {cleanupRegisters[nv[i]]=0xC1EA0000UL+(ulong)nv[i];Put(frame+cleanupOffset+16+(ulong)i*8,cleanupRegisters[nv[i]]);}
                        cleanupRegisters[5]=cleanupSp+64;Put(frame+cleanupOffset+24,cleanupRegisters[5]);
                        for(int r=6;r<16;r++)
                        {cleanupXmm[r]=Enumerable.Range(0,16).Select(b=>(byte)(0x80+r+b)).ToArray();cleanupXmm[r].CopyTo(memory,(int)(frame+cleanupOffset+80)+(r-6)*16);}
                        cleanupBytes=memory.AsSpan((int)(frame+cleanupOffset),240).ToArray();
                        Need(selectedSp>cleanupSp && !selectedBytes.SequenceEqual(cleanupBytes),"distinct physical catch/cleanup contexts");
                        foreach(int r in nv)g[r]=0xBAD50000UL+(ulong)r;
                        for(int r=6;r<16;r++)xmm[r]=new byte[16];
                    }
                    g[0]=failure==3?(frameDispatch?2UL:0UL):1UL;
                }
                else if(singleFinally && (ip==finallyValidator || nestedFinally && ip==secondFinallyValidator))
                {
                    cleanupValidations++;
                    Need(cleanupCalls==cleanupValidations&&cleanupValidations<=expectedCleanups&&catchCalls==0&&validateCalls==0 &&
                        ip==(cleanupValidations==1?finallyValidator:secondFinallyValidator),"each ordered cleanup validated before next transfer");
                    Need(g[1]==frame+272&&g[2]==frame+entryControl&&g[8]==frame+postControl,"finally validator arguments");
                    Need(Get(parentLocal)==cleanupStamp+(ulong)cleanupCalls-1&&Get(frame+280)==cleanupTarget&&
                        memory.AsSpan((int)(frame+selectedOffset),240).SequenceEqual(selectedBytes),"parent local changed without changing selected context");
                    if(frameDispatch)Need(memory.AsSpan((int)(frame+cleanupOffset),240).SequenceEqual(cleanupBytes),"cleanup context remains intact after funclet");
                    Need(Get(frame+postControl)==0&&Get(frame+postControl+8)==0&&
                        BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan((int)(frame+postControl+16)))==0x1F81&&
                        BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan((int)(frame+postControl+20)))==0x37F,"actual cleanup controls captured");
                    // Deliberately clobber callback state independently of the
                    // cleanup. The bridge must reload the original selection.
                    foreach(int r in nv)g[r]=0xBADF0000UL+(ulong)r;
                    for(int r=6;r<16;r++)xmm[r]=Enumerable.Repeat((byte)(0xE0+r),16).ToArray();
                    mxcsr=0x5F80;x87=0x77F;
                    g[0]=cleanupValidations==1&&failure==9 || cleanupValidations==2&&failure==12 ?0UL:1UL;
                }
                else
                {
                    Need(ip==continuationValidator,"only admitted ordinary callbacks");validateCalls++;
                    Need(g[1]==continuation&&g[2]==frame+postOffset&&g[8]==frame+272&&g[9]==frame+entryControl,"validator args");
                    Need(Get(frame+postOffset)==continuation&&Get(frame+postOffset+8)==selectedSp,"actual returned continuation snapshot");
                    for(int i=0;i<nv.Length;i++)Need(Get(frame+postOffset+16+(ulong)i*8)==post[nv[i]],"postcatch snapshot");
                    if(frameDispatch)for(int r=6;r<16;r++)Need(memory.AsSpan((int)(frame+postOffset+80)+(r-6)*16,16).SequenceEqual(selectedXmm[r]),"postcatch vector snapshot");
                    foreach(int r in nv)g[r]=0xBAD;for(int r=6;r<16;r++)xmm[r]=new byte[16];
                    g[0]=failure==6?0UL:1UL;
                }
                ip+=5;continue;
            }
            if(At(0xff,0x94,0x24))
            {
                if(singleFinally && (I32(ip+3)==280 || nestedFinally && I32(ip+3)==288))
                {
                    Need(g[4]==frame && I32(ip+3)==280+cleanupCalls*8 &&
                        Get(frame+(ulong)I32(ip+3))==cleanupTarget+(ulong)cleanupCalls*0x100 &&
                        selectCalls==1&&cleanupCalls<expectedCleanups&&cleanupValidations==cleanupCalls&&catchCalls==0,
                        "ordered selected cleanup call after prior validation");
                    Need(mxcsr==0x1F80&&x87==0x37F,"original FP controls restored before each cleanup");
                    foreach(int r in nv)Need(g[r]==(frameDispatch?cleanupRegisters[r]:selected[r]),"cleanup parent context restored");
                    for(int r=6;r<16;r++)Need(xmm[r].SequenceEqual(frameDispatch?cleanupXmm[r]:selectedXmm[r]),"cleanup parent XMM restored");
                    cleanupCalls++;Put(parentLocal,cleanupStamp+(ulong)cleanupCalls-1);
                    foreach(int r in nv)g[r]=0xF1000000UL+(ulong)r;
                    for(int r=6;r<16;r++)xmm[r]=Enumerable.Repeat((byte)(0xA0+r),16).ToArray();
                    g[0]=0xDEADF100;mxcsr=0x1F81;
                    if(cleanupCalls==1&&failure==7 || cleanupCalls==2&&failure==10)flags=0x400;
                    if(cleanupCalls==1&&failure==8 || cleanupCalls==2&&failure==11)cr4=1UL<<23;
                    ip+=7;continue;
                }
                Need(I32(ip+3)==272&&g[1]==exception&&g[4]==frame,"special catch call");
                Need(cleanupCalls==expectedCleanups&&cleanupValidations==expectedCleanups&&
                    Get(parentLocal)==(singleFinally?cleanupStamp+(ulong)expectedCleanups-1:0)&&mxcsr==0x1F80&&x87==0x37F,"cleanup side effects and entry FP controls before catch");
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

    internal static string Run(string root,string folder,string name,string[] args, int expectedExit = 0)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true } };
        foreach(string arg in args)process.StartInfo.ArgumentList.Add(arg);
        Need(process.Start(),"process start");var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(120000)){process.Kill(true);throw new InvalidOperationException("bounded generator timeout");}
        Task.WaitAll(stdout,stderr);File.WriteAllText(Path.Combine(folder,name+".out"),stdout.Result);File.WriteAllText(Path.Combine(folder,name+".err"),stderr.Result);
        Need(process.ExitCode==expectedExit&&(expectedExit!=0||stderr.Result.Length==0),"generator command "+name);
        return stdout.Result + stderr.Result;
    }
}
