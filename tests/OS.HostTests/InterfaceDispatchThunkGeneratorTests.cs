using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class InterfaceDispatchThunkGeneratorTests
{
    private const string Hash = "228630B0AD4D6AB298414FDA0FCF07E76CB4699A63339FA59BA331AFDC3AEB61";
    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    [ModuleInitializer]
    internal static void RunAll()
    {
        const string name = "m04.016.thunk";
        Console.WriteLine("TEST_START " + name);
        try
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "issue572-thunk-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string project = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj");
            Run(root, folder, "build", 0, ["build", project, "--nologo", "-m:1"]);
            string tool = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net10.0/OS.X64TextSerializerGenerator.dll");
            string[] arguments = [tool, "--interface-dispatch", "--output-root", folder, "--output-file", "dispatch.obj"];
            string created = Run(root, folder, "create", 0, arguments);
            string unchanged = Run(root, folder, "unchanged", 0, arguments);
            string Expected(string state) => "TEST_START m04.016.thunk.generate" + Environment.NewLine +
                "M04_INTERFACE_DISPATCH_THUNK_READY bytes=828 sha256=" + Hash + " state=" + state + Environment.NewLine +
                "TEST_PASS m04.016.thunk.generate" + Environment.NewLine;
            Need(created == Expected("created") && unchanged == Expected("unchanged"), "exact CLI mode/result markers");
            Need(Run(root, folder, "duplicate", 2, [..arguments, "--interface-dispatch"]).Contains("argument_duplicate", StringComparison.Ordinal), "duplicate mode");
            Need(Run(root, folder, "conflict", 2, [..arguments, "--checked-reference-barriers"]).Contains("argument_mode_conflict", StringComparison.Ordinal), "conflicting modes");
            Need(Run(root, folder, "reverse-conflict", 2, [tool, "--checked-reference-barriers", ..arguments[1..]]).Contains("argument_mode_conflict", StringComparison.Ordinal), "mode conflict independent of order");
            byte[] bytes = File.ReadAllBytes(Path.Combine(folder, "dispatch.obj"));
            Need(bytes.Length == 828 && Convert.ToHexString(SHA256.HashData(bytes)) == Hash, "whole deterministic object");
            byte[] text = VerifyObject(bytes);
            foreach (int residue in new[] { 0, 8 })
            {
                VerifyBridge(text, residue, false, false, 0x1234ABCD);
                VerifyBridge(text, residue, false, false, 0);
                VerifyBridge(text, residue, true, false, 0x1234ABCD);
                VerifyBridge(text, residue, false, true, 0x1234ABCD);
            }
            // Existing modes must remain byte-identical after the shared writer extension.
            foreach (var mode in new[] { ("serializer.obj", Array.Empty<string>(), 336, "DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B"),
                ("references.obj", new[]{"--checked-reference-barriers"}, 1645, "E96ABF595503B9D1AE38DEE740F93AE9B3EB84F05D6395B931E4B5D74F65D808") })
            {
                Run(root, folder, mode.Item1, 0, [tool, ..mode.Item2, "--output-root", folder, "--output-file", mode.Item1]);
                byte[] prior = File.ReadAllBytes(Path.Combine(folder, mode.Item1));
                Need(prior.Length == mode.Item3 && Convert.ToHexString(SHA256.HashData(prior)) == mode.Item4, "old mode identity " + mode.Item1);
            }
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception error)
        { Console.WriteLine("TEST_FAIL " + name + " " + error.Message); throw; }
    }

    private static byte[] VerifyObject(byte[] bytes)
    {
        uint U32(int p) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p, 4));
        ushort U16(int p) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(p, 2));
        Need(U16(0) == 0x8664 && U16(2) == 1 && U32(4) == 0 && U32(12) == 5 && U32(16) == 0, "AMD64 deterministic header");
        Need(Encoding.ASCII.GetString(bytes, 20, 8) == ".text\0\0\0" && U32(36) == 472 && U32(40) == 60 &&
            U16(52) == 2 && U32(56) == 0x60500020, "one RX nonwritable section");
        int reloc = (int)U32(44), symbols = (int)U32(8), strings = symbols + 5 * 18;
        Need(reloc == 532 && symbols == 552 && U32(strings) == bytes.Length - strings, "bounded complete tables");
        Need(U32(reloc) == 0xDD && U32(reloc+4) == 1 && U16(reloc+8) == 4 &&
            U32(reloc+10) == 0x1C3 && U32(reloc+14) == 3 && U16(reloc+18) == 4, "exact function and data REL32 fixups");
        string[] names = ["RhpInitialDynamicInterfaceDispatch", "CLROS_InterfaceDispatch_Resolve", "CLROS_InterfaceDispatch_GetCellSection", "__InterfaceDispatchCellSection_Start", "CLROS_InterfaceDispatch_GetInitialStub"];
        for (int i = 0; i < 5; i++)
        {
            int symbol = symbols + i*18, start = strings + (int)U32(symbol+4);
            int end = Array.IndexOf(bytes, (byte)0, start);
            Need(U32(symbol) == 0 && Encoding.ASCII.GetString(bytes, start, end-start) == names[i] &&
                U32(symbol+8) == (i==2 ? 0x1C0u : i==4 ? 0x1D0u : 0) && U16(symbol+12) == (i%2==0 ? 1 : 0) &&
                U16(symbol+14) == (i==3 ? 0 : 0x20) && bytes[symbol+16] == 2 && bytes[symbol+17] == 0, "exact symbol " + i);
        }
        byte[] text = bytes.AsSpan(60, 472).ToArray();
        Need(text.AsSpan(0x1BA).SequenceEqual(Convert.FromHexString("FFE0EBFE9090488D0500000000C39090909090909090488D0529FEFFFFC3")), "tail jump, halt, alignment and address-only getter");
        Need(BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(0xDD,4)) == 0 &&
            BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(0x1C3,4)) == 0, "zero relocation addends");
        Need(0x1D7 + BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(0x1D3,4)) == 0,
            "same-section initial-stub getter exact entry identity without external fixup");
        // Signed REL32 link arithmetic: LEA uses the address after the four-byte fixup.
        foreach (long target in new long[] { 0x100000-0x1234, 0x100000+0x5678 })
        {
            long next = 0x100000+0x1C7; int displacement = checked((int)(target-next));
            Need(next + displacement == target, "cell-start getter signed relocation");
        }
        return text;
    }

    // Finite interpreter for this reviewed instruction stream, not a general x64 emulator.
    // Callback deliberately clobbers all GP except RBP/RSP, all XMM/MXCSR and home space.
    private static void VerifyBridge(byte[] code, int residue, bool incomingDf, bool returnedDf, ulong target)
    {
        ulong[] g = Enumerable.Range(0,16).Select(i => 0xA000000000000000UL+(ulong)i).ToArray();
        g[4] = (ulong)(2048+residue); ulong initialRsp = g[4];
        ulong[] original = (ulong[])g.Clone();
        byte[] memory = Enumerable.Range(0,4096).Select(i => (byte)(i*17+3)).ToArray();
        byte[] before = (byte[])memory.Clone(), xmm = Enumerable.Range(0,256).Select(i => (byte)(i*31+5)).ToArray();
        byte[] originalXmm = (byte[])xmm.Clone();
        ulong flags = 0x202UL | (incomingDf ? 0x400UL : 0), originalFlags = flags;
        uint mxcsr = 0x1F80; int ip = 0, calls = 0; bool zero = false;
        List<int> saves = [], restores = [];
        ulong Read(ulong address) => BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan(checked((int)address),8));
        void Write(ulong address, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan(checked((int)address),8),value);
        void Push(ulong value) { g[4]-=8; Write(g[4],value); }
        ulong Pop() { ulong value=Read(g[4]);g[4]+=8;return value; }
        bool At(params byte[] bytes) => code.AsSpan(ip,Math.Min(bytes.Length,code.Length-ip)).SequenceEqual(bytes);
        int I32(int p) => BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(p,4));
        for (int step=0; step<160; step++)
        {
            if (At(0xEB,0xFE))
            { Need(ip==0x1BC && (incomingDf||returnedDf||target==0) && calls==(incomingDf?0:1), "terminal rejection before any jump"); return; }
            if (At(0xFF,0xE0))
            {
                Need(!incomingDf&&!returnedDf&&target!=0&&calls==1&&g[0]==target&&g[4]==initialRsp, "real target tail jump with original rsp");
                for (int i=1;i<16;i++) Need(g[i]==original[i], "preserved GP "+i);
                Need(xmm.SequenceEqual(originalXmm)&&mxcsr==0x1F80&&flags==originalFlags, "SIMD/control/flags restored");
                Need(saves.SequenceEqual(Enumerable.Range(0,16))&&restores.SequenceEqual(saves), "all XMM save/restore slots");
                Need(memory.AsSpan((int)initialRsp).SequenceEqual(before.AsSpan((int)initialRsp)), "original return/home/stack arguments untouched");
                Need(memory.AsSpan(0,(int)initialRsp-448).SequenceEqual(before.AsSpan(0,(int)initialRsp-448)), "bounded bridge stack footprint"); return;
            }
            if (code[ip]==0x9C) {Push(flags);ip++;continue;}
            if (code[ip]==0x9D) {flags=Pop();ip++;continue;}
            int extended=code[ip]==0x41?8:0, op=code[ip+(extended==0?0:1)];
            if (op>=0x50&&op<=0x57) {Push(g[extended+op-0x50]);ip+=extended==0?1:2;continue;}
            if (op>=0x58&&op<=0x5F) {g[extended+op-0x58]=Pop();ip+=extended==0?1:2;continue;}
            if (At(0x48,0x89,0xE5)) {g[5]=g[4];ip+=3;continue;}
            if (At(0x48,0x81,0xEC)) {g[4]-=(uint)I32(ip+3);ip+=7;continue;}
            if (At(0x48,0x83,0xE4,0xF0)) {g[4]&=~15UL;ip+=4;continue;}
            if (At(0x48,0xF7,0x45,0x08)) {zero=(Read(g[5]+8)&(uint)I32(ip+4))==0;ip+=8;continue;}
            if (At(0x49,0xF7,0xC3)) {zero=(g[11]&(uint)I32(ip+3))==0;ip+=7;continue;}
            if (At(0x48,0x85,0xC0)) {zero=g[0]==0;ip+=3;continue;}
            if (At(0x0F,0x84)||At(0x0F,0x85)) {bool take=code[ip+1]==0x84?zero:!zero;int delta=I32(ip+2);ip+=6;if(take)ip+=delta;continue;}
            if (code[ip]==0xF3)
            {
                int extra=code[ip+1]==0x44?1:0, p=ip+1+extra;
                Need(code[p]==0x0F&&(code[p+1]==0x7F||code[p+1]==0x6F)&&code[p+3]==0x24, "exact XMM addressing");
                int register=((code[p+2]>>3)&7)+extra*8, offset=I32(p+4);
                Need(offset==32+register*16, "XMM outside shadow space");
                var slot=memory.AsSpan(checked((int)g[4])+offset,16);var value=xmm.AsSpan(register*16,16);
                if(code[p+1]==0x7F){value.CopyTo(slot);saves.Add(register);}else{slot.CopyTo(value);restores.Add(register);}
                ip=p+8;continue;
            }
            if (At(0x0F,0xAE,0x9C,0x24)||At(0x0F,0xAE,0x94,0x24))
            {
                Need(I32(ip+4)==288,"MXCSR disjoint slot");var slot=memory.AsSpan((int)g[4]+288,4);
                if(code[ip+2]==0x9C)BinaryPrimitives.WriteUInt32LittleEndian(slot,mxcsr);else mxcsr=BinaryPrimitives.ReadUInt32LittleEndian(slot);
                ip+=8;continue;
            }
            if (At(0x48,0x8B,0x4D)||At(0x48,0x8B,0x55))
            {g[code[ip+2]==0x4D?1:2]=Read((ulong)((long)g[5]+(sbyte)code[ip+3]));ip+=4;continue;}
            if (At(0x48,0x89,0x45,0xF8)) {Write(g[5]-8,g[0]);ip+=4;continue;}
            if (At(0x48,0x8D,0x65,0x90)) {g[4]=g[5]-112;ip+=4;continue;}
            if (code[ip]==0xE8)
            {
                Need(ip==0xDC&&I32(ip+1)==0&&calls++==0&&g[1]==original[1]&&g[2]==original[11], "actual receiver/cell ordinary callback registers");
                Need((g[4]&15)==0&&initialRsp-g[4]==(residue==0?432UL:440UL)&&g[4]+292<=g[5]-112, "Win64 alignment and disjoint shadow/SIMD frame");
                memory.AsSpan((int)g[4],32).Fill(0xCC);
                for(int i=0;i<16;i++)if(i!=4&&i!=5)g[i]=0xDDDD0000UL+(ulong)i;
                xmm.AsSpan().Fill(0xDD);mxcsr=0;flags=0x246UL|(returnedDf?0x400UL:0);g[0]=target;ip+=5;continue;
            }
            throw new InvalidOperationException("unreviewed opcode at "+ip);
        }
        throw new InvalidOperationException("bridge failed to terminate within finite instruction bound");
    }

    private static string Run(string root, string folder, string label, int expected, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory=root, UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach(string value in arguments)start.ArgumentList.Add(value);
        File.WriteAllText(Path.Combine(folder,label+".command.json"),JsonSerializer.Serialize(new { Executable="dotnet",Arguments=arguments,WorkingDirectory=root }));
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        bool timedOut=!process.WaitForExit(120000);if(timedOut){process.Kill(true);process.WaitForExit();}
        string stdout=output.GetAwaiter().GetResult(),stderr=error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder,label+".stdout.log"),stdout);File.WriteAllText(Path.Combine(folder,label+".stderr.log"),stderr);
        File.WriteAllText(Path.Combine(folder,label+".result.json"),JsonSerializer.Serialize(new {NativeExit=process.ExitCode,TimedOut=timedOut}));
        Need(!timedOut&&process.ExitCode==expected&&(expected!=0||stderr.Length==0),"command "+label+"; retained "+folder);
        return stdout+stderr;
    }
}
