using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
#if CHECKED_REFERENCE_COFF_CHILD
using OS.X64TextSerializerGenerator;
#endif

internal static class CheckedReferenceBarrierCoffWriterTests
{
#if CHECKED_REFERENCE_COFF_CHILD
    // Independent literal layout: 20 + 2*40 headers, 12+4 raw bytes,
    // 2*10+10 relocations, 3*18 symbols and 33 string-table bytes.
    private const string ExpectedObject =
        "6486020000000000920000000300000000000000" +
        "2E7465787400000000000000000000000C0000006400000074000000000000000200000020005060" +
        "2E706461746100000000000000000000040000007000000088000000000000000100000040003040" +
        "E800000000E904030201C39000000000" +
        "010000000100000004000600000002000000040000000000000000000300" +
        "5468756E6B00000000000000010020000200" +
        "000000000400000000000000000020000200" +
        "000000001400000000000000000020000200" +
        "21000000436865636B656443616C6C6261636B0042756C6B43616C6C6261636B00";

    private static int Main()
    {
        DefaultPolicy();
        ExternalDataPolicy();
        CompleteObject();
        RelocationBounds();
        UndefinedSymbols();
        OverlapRejections();
        CapacityAndPrecedence();
        SerializerRegression();
        Console.WriteLine("COFF_REL32_CONTROL_PASS default structure bounds symbols overlaps capacity serializer");
        return 0;
    }

    private static Amd64CoffObjectDefinition Definition() => new(0x8664, 0,
        [new(".text", [0xE8,0,0,0,0,0xE9,4,3,2,1,0xC3,0x90], 0x60500020,
            [new(1,1,4), new(6,2,4)]),
         new(".pdata", [0,0,0,0], 0x40300040, [new(0,0,3)])],
        [new("Thunk",0,1,0x20,2), new("CheckedCallback",0,0,0x20,2), new("BulkCallback",0,0,0x20,2)]);

    private static Amd64CoffObjectDefinition Relocations(params CoffRelocationDefinition[] relocations)
    {
        var d = Definition();
        d.Sections![0] = d.Sections[0] with { Relocations = relocations };
        return d;
    }

    private static void DefaultPolicy()
    {
        Reject(Definition(), false, CoffWriteError.InvalidRelocation);
        Reject(Relocations(), false, CoffWriteError.InvalidSymbol);
        var d = Definition();
        Check(!Amd64CoffWriter.TryGetRequiredSize(in d, false, out int size, out var error) &&
            size == 0 && error == CoffWriteError.InvalidRelocation, "explicit false size");
        byte[] destination = Pattern(300); byte[] before = (byte[])destination.Clone();
        Check(!Amd64CoffWriter.TryWrite(in d, destination, false, out int written, out error) &&
            written == 0 && error == CoffWriteError.InvalidRelocation && destination.SequenceEqual(before), "explicit false write");
    }

    private static void ExternalDataPolicy()
    {
        var data = new Amd64CoffObjectDefinition(0x8664, 0,
            [new(".text", [0x48,0x8D,0x05,0,0,0,0,0xC3], 0x60500020, [new(3,1,4)])],
            [new("Getter",0,1,0x20,2), new("CellData",0,0,0,2)]);
        Reject(data, false, CoffWriteError.InvalidRelocation);
        Reject(data, true, CoffWriteError.InvalidRelocation);
        Check(Amd64CoffWriter.TryGetRequiredSize(in data,false,true,out int size,out var error) &&
            Amd64CoffWriter.TryWrite(in data,new byte[size],false,true,out int count,out error) && count==size,
            "independent data opt-in without function opt-in");
        var function = data with { Symbols = [new("Getter",0,1,0x20,2),new("Callback",0,0,0x20,2)] };
        DataReject(function, CoffWriteError.InvalidRelocation);
        foreach (var symbol in new[] {
            data.Symbols![1] with { Value=1 }, data.Symbols[1] with { SectionNumber=-1 },
            data.Symbols[1] with { SectionNumber=-2 }, data.Symbols[1] with { StorageClass=3 },
            data.Symbols[1] with { Type=1 } })
            DataReject(data with { Symbols=[data.Symbols[0],symbol] }, CoffWriteError.InvalidRelocation);
        foreach (var relocation in new CoffRelocationDefinition[] {new(5,1,4),new(uint.MaxValue,1,4),new(3,2,4),new(3,1,3),new(3,1,5)})
            DataReject(data with { Sections=[data.Sections![0] with { Relocations=[relocation] }] }, CoffWriteError.InvalidRelocation);
        DataReject(data with { Sections=[data.Sections![0] with { Characteristics=0x40300040 }] }, CoffWriteError.InvalidRelocation);
        DataReject(data with { Sections=[data.Sections![0] with { Relocations=[new(3,1,4),new(4,1,4)] }] }, CoffWriteError.InvalidRelocation);
        DataReject(data, CoffWriteError.CapacityExceeded, size-1);

        static void DataReject(Amd64CoffObjectDefinition input, CoffWriteError expected, int capacity=300)
        {
            byte[] bytes=Pattern(334), before=(byte[])bytes.Clone();
            Check(!Amd64CoffWriter.TryWrite(in input,bytes.AsSpan(17,capacity),false,true,out int written,out var error) &&
                written==0 && error==expected && bytes.SequenceEqual(before), "data mode bounded atomic rejection "+expected);
        }
    }

    private static void CompleteObject()
    {
        var d = Definition();
        Check(Amd64CoffWriter.TryGetRequiredSize(in d, true, out int size, out var error) &&
            error == CoffWriteError.None && size == 233, "literal required size");
        byte[] expected = Convert.FromHexString(ExpectedObject);
        Check(expected.Length == 233, "literal oracle length");
        foreach (int capacity in new[] { 233, 251 })
        {
            byte[] destination = Pattern(capacity + 34); byte[] before = (byte[])destination.Clone();
            Check(Amd64CoffWriter.TryWrite(in d, destination.AsSpan(17, capacity), true, out int written, out error) &&
                written == 233 && error == CoffWriteError.None, "accepted write");
            Check(destination.AsSpan(17, 233).SequenceEqual(expected), "entire literal COFF");
            Check(destination.AsSpan(0,17).SequenceEqual(before.AsSpan(0,17)) &&
                destination.AsSpan(250).SequenceEqual(before.AsSpan(250)), "full guards and unused capacity");
        }
        // Decode every header/relocation/symbol field from the actual object too.
        byte[] actual = new byte[233];
        Check(Amd64CoffWriter.TryWrite(in d, actual, true, out _, out _), "decode write");
        Check(U16(actual,0)==0x8664 && U16(actual,2)==2 && U32(actual,4)==0 && U32(actual,8)==146 &&
            U32(actual,12)==3 && U16(actual,16)==0 && U16(actual,18)==0, "file header");
        for (int i=0;i<2;i++)
        {
            int h=20+i*40;
            Check(U32(actual,h+8)==0 && U32(actual,h+12)==0 && U32(actual,h+28)==0 && U16(actual,h+34)==0, "section zero fields");
            Check(U32(actual,h+16)==(i==0?12u:4u) && U32(actual,h+20)==(i==0?100u:112u) &&
                U32(actual,h+24)==(i==0?116u:136u) && U16(actual,h+32)==(i==0?2:1) &&
                U32(actual,h+36)==(i==0?0x60500020u:0x40300040u), "section table");
        }
        for (int i=0;i<3;i++)
        {
            int r=116+i*10;
            Check(U32(actual,r)==new uint[]{1,6,0}[i] && U32(actual,r+4)==new uint[]{1,2,0}[i] &&
                U16(actual,r+8)==(i==2?3:4), "relocation fields");
            int s=146+i*18;
            Check(U32(actual,s+8)==0 && U16(actual,s+12)==(i==0?1:0) && U16(actual,s+14)==0x20 &&
                actual[s+16]==2 && actual[s+17]==0, "symbol fields and no aux");
            if(i>0) Check(U32(actual,s)==0 && U32(actual,s+4)==(i==1?4u:20u), "long symbol offsets");
        }
        Check(U32(actual,200)==33 && Encoding.ASCII.GetString(actual,204,29)=="CheckedCallback\0BulkCallback\0", "complete string table");
        Check(d.Sections![0].RawData!.SequenceEqual(Convert.FromHexString("E800000000E904030201C390")), "input addends unchanged");
        foreach (uint offset in new uint[]{0,8})
        {
            var edge=Relocations(new CoffRelocationDefinition(offset,1,4));
            Check(Amd64CoffWriter.TryGetRequiredSize(in edge,true,out int edgeSize,out _), "inclusive fixup edge");
            Check(Amd64CoffWriter.TryWrite(in edge,new byte[edgeSize],true,out _,out _), "edge write");
        }
    }

    private static void RelocationBounds()
    {
        foreach(uint offset in new uint[]{9,12,13,uint.MaxValue}) Reject(Relocations(new CoffRelocationDefinition(offset,1,4)),true,CoffWriteError.InvalidRelocation);
        foreach(uint index in new uint[]{0,3,uint.MaxValue}) Reject(Relocations(new CoffRelocationDefinition(1,index,4)),true,CoffWriteError.InvalidRelocation);
        foreach(ushort type in new ushort[]{0,1,2,5,6,7,8,9,10,ushort.MaxValue}) Reject(Relocations(new CoffRelocationDefinition(1,1,type)),true,CoffWriteError.InvalidRelocation);
        Reject(Relocations(new CoffRelocationDefinition(1,1,3)),true,CoffWriteError.InvalidRelocation); // No RVA of an undefined symbol.
        var d=Relocations(new CoffRelocationDefinition(1,1,4)); d.Sections![0]=d.Sections[0] with { Characteristics=0x40300040 };
        Reject(d,true,CoffWriteError.InvalidRelocation); // REL32 is admitted only in code.
        d=Relocations(); d.Sections![0]=d.Sections[0] with { Relocations=null };
        Reject(d,true,CoffWriteError.InvalidConfiguration);
        d=Relocations(); d.Sections![0]=d.Sections[0] with { Relocations=new CoffRelocationDefinition[65536] };
        Reject(d,true,CoffWriteError.InvalidRelocation);
    }

    private static void UndefinedSymbols()
    {
        CoffSymbolDefinition good=new("CheckedCallback",0,0,0x20,2);
        foreach(var bad in new[]{good with {Value=1},good with {Value=uint.MaxValue},good with {Type=0},
            good with {Type=0x21},good with {StorageClass=3},good with {StorageClass=0},
            good with {SectionNumber=-1},good with {SectionNumber=-2},good with {SectionNumber=3}})
        {
            var d=Relocations();d.Symbols![1]=bad;Reject(d,true,CoffWriteError.InvalidSymbol);
            d=Relocations(new CoffRelocationDefinition(1,1,4));d.Symbols![1]=bad;Reject(d,true,CoffWriteError.InvalidRelocation);
        }
        var invalidName=Relocations();invalidName.Symbols![1]=good with { Name="bad name" };Reject(invalidName,true,CoffWriteError.InvalidName);
        var duplicate=Relocations();duplicate.Symbols![2]=good;Reject(duplicate,true,CoffWriteError.DuplicateSymbol);
    }

    private static void OverlapRejections()
    {
        Reject(Relocations(new CoffRelocationDefinition(1,1,4),new(1,2,4)),true,CoffWriteError.InvalidRelocation);
        foreach(uint other in new uint[]{2,3,4})
        {
            Reject(Relocations(new CoffRelocationDefinition(1,1,4),new(other,2,4)),true,CoffWriteError.InvalidRelocation);
            Reject(Relocations(new CoffRelocationDefinition(other,2,4),new(1,1,4)),true,CoffWriteError.InvalidRelocation);
            Reject(Relocations(new CoffRelocationDefinition(1,1,4),new(other,0,3)),true,CoffWriteError.InvalidRelocation);
            Reject(Relocations(new CoffRelocationDefinition(other,0,3),new(1,1,4)),true,CoffWriteError.InvalidRelocation);
        }
        var adjacent=Relocations(new CoffRelocationDefinition(0,1,4),new(4,2,4),new(8,0,3));
        Check(Amd64CoffWriter.TryGetRequiredSize(in adjacent,true,out int size,out _), "adjacent fixups");
        Check(Amd64CoffWriter.TryWrite(in adjacent,new byte[size],true,out _,out _), "adjacent emit");
    }

    private static void CapacityAndPrecedence()
    {
        foreach(int capacity in new[]{0,1,232})
        {
            var d=Definition(); byte[] bytes=Pattern(300);byte[] before=(byte[])bytes.Clone();
            Check(!Amd64CoffWriter.TryWrite(in d,bytes.AsSpan(17,capacity),true,out int written,out var error) &&
                written==0 && error==CoffWriteError.CapacityExceeded && bytes.SequenceEqual(before), "capacity atomicity");
        }
        Reject(Definition() with {Machine=0},true,CoffWriteError.UnsupportedMachine);
        Reject(Definition() with {TimeDateStamp=1},true,CoffWriteError.NonDeterministicTimestamp);
        Reject(Definition() with {Sections=null},true,CoffWriteError.InvalidSectionCount);
        var bad=Relocations(new CoffRelocationDefinition(uint.MaxValue,1,4));
        Check(!Amd64CoffWriter.TryWrite(in bad,Span<byte>.Empty,true,out int count,out var failure) &&
            count==0 && failure==CoffWriteError.InvalidRelocation,"validation precedes capacity");
    }

    private static void SerializerRegression()
    {
        byte[] bytes=Pattern(370);byte[] before=(byte[])bytes.Clone();
        Check(InstructionPublicationSerializerGenerator.TryGenerate(bytes.AsSpan(17,336),out int n,out var error) &&
            n==336 && error==CoffWriteError.None,"original serializer");
        Check(Convert.ToHexString(SHA256.HashData(bytes.AsSpan(17,336)))=="DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B","original 336-byte hash");
        Check(bytes.AsSpan(0,17).SequenceEqual(before.AsSpan(0,17)) && bytes.AsSpan(353).SequenceEqual(before.AsSpan(353)),"serializer guards");
    }

    private static void Reject(Amd64CoffObjectDefinition d,bool opt,CoffWriteError expected)
    {
        int size;CoffWriteError error;
        bool sized=opt?Amd64CoffWriter.TryGetRequiredSize(in d,true,out size,out error):Amd64CoffWriter.TryGetRequiredSize(in d,out size,out error);
        Check(!sized && size==0 && error==expected,"size rejection "+expected);
        byte[] bytes=Pattern(600);byte[] before=(byte[])bytes.Clone();int written;
        bool ok=opt?Amd64CoffWriter.TryWrite(in d,bytes.AsSpan(17,560),true,out written,out error):Amd64CoffWriter.TryWrite(in d,bytes.AsSpan(17,560),out written,out error);
        Check(!ok && written==0 && error==expected && bytes.SequenceEqual(before),"full patterned rejection "+expected);
    }

    private static byte[] Pattern(int count)=>Enumerable.Range(0,count).Select(i=>(byte)(i*37+11)).ToArray();
    private static ushort U16(byte[] b,int p)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p,2));
    private static uint U32(byte[] b,int p)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p,4));
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
#else
    [ModuleInitializer]
    internal static void RunAll()
    {
        const string name="m04.010.coff.writer";
        Console.WriteLine("TEST_START "+name);
        try
        {
            string root=HostToolProcess.FindRepositoryRoot();
            string folder=Path.Combine(root,"artifacts","issue79-coff-writer-tests",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            foreach(string source in new[]{"tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
                "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
                "tests/OS.HostTests/CheckedReferenceBarrierCoffWriterTests.cs","global.json"})
                File.Copy(Path.Combine(root,source),Path.Combine(folder,Path.GetFileName(source)),false);
            File.WriteAllText(Path.Combine(folder,"Probe.csproj"),"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
                    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <DefineConstants>CHECKED_REFERENCE_COFF_CHILD</DefineConstants><UseSharedCompilation>false</UseSharedCompilation>
                  </PropertyGroup>
                </Project>
                """,new UTF8Encoding(false));
            Run(folder,"build",120000,["build","Probe.csproj","--nologo","--disable-build-servers","-m:1","-nodeReuse:false",
                "-p:ImportDirectoryBuildProps=false","-p:ImportDirectoryBuildTargets=false"]);
            string output=Run(folder,"run",30000,[Path.Combine(folder,"bin","Debug","net10.0","Probe.dll")]);
            if(output!="COFF_REL32_CONTROL_PASS default structure bounds symbols overlaps capacity serializer"+Environment.NewLine)
                throw new InvalidOperationException("child control marker mismatch");
            Console.WriteLine("TEST_PASS "+name);
        }
        catch
        {
            Console.WriteLine("TEST_FAIL "+name+" assertion_failed");
            throw;
        }
    }

    private static string Run(string folder,string label,int timeout,string[] arguments)
    {
        var start=new ProcessStartInfo("dotnet") {WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in arguments)start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"]="0";
        File.WriteAllText(Path.Combine(folder,label+".command.json"),JsonSerializer.Serialize(new {Executable="dotnet",Arguments=arguments,WorkingDirectory=folder,TimeoutMilliseconds=timeout}));
        using var process=new Process {StartInfo=start};
        if(!process.Start())throw new InvalidOperationException("child failed to start");
        Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
        bool timedOut=!process.WaitForExit(timeout);
        if(timedOut){process.Kill(entireProcessTree:true);process.WaitForExit();}
        string stdout=output.GetAwaiter().GetResult(),stderr=error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder,label+".stdout.log"),stdout,new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder,label+".stderr.log"),stderr,new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder,label+".result.json"),JsonSerializer.Serialize(new {NativeExit=process.ExitCode,TimedOut=timedOut}));
        if(timedOut || process.ExitCode!=0 || stderr.Length!=0)throw new InvalidOperationException("child failed: "+label+"; retained "+folder);
        return stdout;
    }
#endif
}
