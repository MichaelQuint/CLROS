using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static class EhContributionCatalogGeneratorTests
{
    private static void Need(bool value,string why) { if(!value) throw new InvalidOperationException(why); }
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run("runtime.eh-catalog-generator.original-vectors",()=>
        {
            string root=HostToolProcess.FindRepositoryRoot();
            string folder=Path.Combine(root,"artifacts","eh-catalog-generator",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            _=ExceptionHandlingThunkGeneratorTests.Run(root,folder,"build",["build","tools/OS.X64TextSerializerGenerator","--nologo","-m:1"]);
            Assembly assembly=Assembly.LoadFile(Path.Combine(root,"tools/OS.X64TextSerializerGenerator/bin/Debug/net11.0/OS.X64TextSerializerGenerator.dll"));
            MethodInfo generate=assembly.GetType("OS.X64TextSerializerGenerator.EhContributionCatalogGenerator",true)!.GetMethod("Generate")!;
            MethodInfo selected=assembly.GetType("OS.X64TextSerializerGenerator.EhContributionCatalogGenerator",true)!.GetMethod("GenerateForMethod")!;
            MethodInfo run=assembly.GetType("OS.X64TextSerializerGenerator.EhContributionCatalogGenerator",true)!.GetMethod("Run")!;
            byte[] Generate(byte[] obj,string map)=>(byte[])generate.Invoke(null,[obj,map])!;
            byte[] Select(byte[] obj,string map,string method)=>(byte[])selected.Invoke(null,[obj,map,method])!;
            void Reject(byte[] obj,string map,string why)
            {
                try { _=Generate(obj,map); throw new InvalidOperationException("accepted "+why); }
                catch(TargetInvocationException e) when(e.InnerException is InvalidDataException or System.Xml.XmlException) { }
            }
            void RejectSelection(byte[] obj,string map,string method,string why)
            {
                try { _=Select(obj,map,method); throw new InvalidOperationException("accepted selection "+why); }
                catch(TargetInvocationException e) when(e.InnerException is InvalidDataException or System.Xml.XmlException) { }
            }
            var input=Input(); byte[] saved=[..input.Object]; byte[] output=Generate(input.Object,input.Map);
            Need(output.SequenceEqual(Generate(input.Object,input.Map)) && input.Object.SequenceEqual(saved),"deterministic without input mutation");
            Need(S16(output,0)==0x8664 && S16(output,2)==2 && U32(output,4)==0 && S16(output,16)==0,"closed COFF header");
            Need(Encoding.ASCII.GetString(output,20,5)==".text" && U32(output,36)==16 && U32(output,56)==0x60500020,"two ordinary address getters");
            Need(Encoding.ASCII.GetString(output,60,6)==".rdata" && U32(output,76)==32 && U32(output,96)==0x40300040,"readonly non-executable catalog");
            int raw=(int)U32(output,80); byte[] expected=new byte[32]; W32(expected,0,0x43454843); W32(expected,4,1); W32(expected,8,1); W32(expected,12,32); W32(expected,20,24); W32(expected,28,4);
            Need(output.AsSpan(raw,32).SequenceEqual(expected),"literal wire vector, RVAs left for native linking");
            int code=(int)U32(output,40);
            Need(output.AsSpan(code,16).SequenceEqual(new byte[]{0x48,0x8d,0x05,0,0,0,0,0xc3,0x48,0x8d,0x05,0,0,0,0,0xc3}),"returning LEA getters");
            Fixups(output,44,new[]{(3U,2U,4),(11U,3U,4)}); Fixups(output,84,new[]{(16U,4U,3),(24U,5U,3)});
            Need(U32(output,12)==6,"four local catalog symbols plus two original external contributions");
            int symbols=(int)U32(output,8);
            for(int i=0;i<6;i++) Need(S16(output,symbols+i*18+12)==(i<2?1:i<4?2:0) && output[symbols+i*18+16]==2,"explicit symbol ownership "+i);
            Need(U32(output,symbols+3*18+8)==32,"end symbol has exact table extent");
            foreach(int length in new[]{0,1,19,100,input.Object.Length-1}) Reject(input.Object.AsSpan(0,length).ToArray(),input.Map,"truncated COFF "+length);
            foreach((int at,uint value,int width) in new[]{(0,0U,2),(4,1U,4),(8,uint.MaxValue,4),(12,uint.MaxValue,4),(16,1U,2),
                (76,3U,4),(96,0xC0300040U,4),(200,23U,4),(200,25U,4),(212,23U,4),(212,25U,4),(228,1U,4),(268,99U,4),(285,255U,1)})
            {
                byte[] bad=[..input.Object]; if(width==4) W32(bad,at,value); else if(width==2) BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(at),(ushort)value); else bad[at]=(byte)value;
                Reject(bad,input.Map,"object mutation "+at);
            }
            foreach(string map in new[]{"<Wrong/>","<!DOCTYPE ObjectNodes [<!ENTITY x SYSTEM 'file:///invalid'>]><ObjectNodes>&x;</ObjectNodes>",
                input.Map.Replace("Length=\"24\"","Length=\"23\""),input.Map.Replace("Length=\"24\"","Length=\"25\""),
                input.Map.Replace("__ehinfo_OS_Test__Method","__ehinfo_missing"),input.Map.Replace(input.EhHash,new string('0',64)),
                input.Map.Replace("</ObjectNodes>",input.Map[(input.Map.IndexOf("<MethodExceptionHandlingInfo",StringComparison.Ordinal))..input.Map.IndexOf("</ObjectNodes>",StringComparison.Ordinal)]+"</ObjectNodes>")})
                Reject(input.Object,map,"map mutation");
            // Code node hashes precede local fixups; treating them as raw COFF hashes
            // would wrongly reject legal compiler output. Shape remains required.
            Need(Generate(input.Object,input.Map.Replace(input.CodeHash,new string('0',64))).SequenceEqual(output),"code digest is provenance, not post-relocation byte equality");
            foreach(bool codeContribution in new[]{true,false})
            {
                // A relocation starting before a symbol still writes into it.
                // First admit the same fixture with an interior fixup, then move
                // that fixup across either contribution boundary.
                byte[] crossing=[..input.Object]; int header=codeContribution?20:60;
                W32(crossing,268+(codeContribution?0:18)+8,2);
                string map=input.Map;
                if(!codeContribution)
                {
                    string changedHash=Convert.ToHexString(SHA256.HashData(crossing.AsSpan(174,4)));
                    map=map.Replace(input.EhHash,changedHash);
                }
                int fixup=crossing.Length; Array.Resize(ref crossing,fixup+10);
                W32(crossing,header+24,(uint)fixup); W16(crossing,header+32,1);
                W32(crossing,fixup,2); W32(crossing,fixup+4,0); W16(crossing,fixup+8,3);
                _=Generate(crossing,map);
                W32(crossing,fixup,0); Reject(crossing,map,"fixup intersects contribution start");
                W32(crossing,fixup,codeContribution?24U:4U); Reject(crossing,map,"fixup intersects contribution end");
            }
            var pair=InputPair(); byte[] pairSaved=[..pair.Object], all=Generate(pair.Object,pair.Map);
            Need(U32(all,(int)U32(all,80)+8)==2,"default retains both original contributions");
            foreach(string method in new[]{"OS_Test__First","OS_Test__Second"})
            {
                byte[] one=Select(pair.Object,pair.Map,method);
                Need(U32(one,(int)U32(one,80)+8)==1 && U32(one,12)==6,"one explicit selected contribution");
                Need(one.SequenceEqual(Select(pair.Object,pair.Map,method)) && pair.Object.SequenceEqual(pairSaved),"selected deterministic without mutation");
                Need(Encoding.ASCII.GetString(one).Contains(method+'\0',StringComparison.Ordinal),"selected exact external identity");
            }
            foreach(string method in new[]{"", "OS_Test__Missing", "OS_Test__First\n", new string('x',4097)})
                RejectSelection(pair.Object,pair.Map,method,"invalid or absent identity");
            // Select First, then corrupt only the omitted Second. Every input row
            // remains subject to the same hash, extent, relocation and alias gates.
            byte[] omittedBad=[..pair.Object]; omittedBad[208]^=1;
            RejectSelection(omittedBad,pair.Map,"OS_Test__First","omitted EH hash");
            omittedBad=[..pair.Object]; W32(omittedBad,276,25);
            RejectSelection(omittedBad,pair.Map,"OS_Test__First","omitted code extent");
            omittedBad=[..pair.Object]; W16(omittedBad,352,4);
            RejectSelection(omittedBad,pair.Map,"OS_Test__First","omitted pdata relocation");
            omittedBad=[..pair.Object]; W32(omittedBad,466,0);
            RejectSelection(omittedBad,pair.Map,"OS_Test__First","omitted root alias");
            string objPath=Path.Combine(folder,"pair.obj"), mapPath=Path.Combine(folder,"pair.map.xml"), outputPath=Path.Combine(folder,"selected.obj");
            File.WriteAllBytes(objPath,pair.Object); File.WriteAllText(mapPath,pair.Map);
            (int Exit,string Text) Run(string[] args)
            {
                TextWriter prior=Console.Out; using StringWriter output=new();
                try {Console.SetOut(output); return ((int)run.Invoke(null,[args])!,output.ToString());}
                finally {Console.SetOut(prior);}
            }
            string[] args=["--eh-contribution-catalog","--object",objPath,"--map",mapPath,"--output",outputPath];
            Need(Run(args).Exit==0&&File.ReadAllBytes(outputPath).SequenceEqual(all),"legacy CLI keeps both methods");
            Need(Run([..args,"--method","OS_Test__First"]).Exit==0,"explicit method CLI");
            byte[] selectedSaved=File.ReadAllBytes(outputPath);
            Need(selectedSaved.SequenceEqual(Select(pair.Object,pair.Map,"OS_Test__First")),"CLI/API selected bytes agree");
            foreach(var failed in new[]{Run([..args,"--method","OS_Test__Missing"]),Run([..args,"--unexpected","OS_Test__First"])})
                Need(failed.Exit==1&&failed.Text.Contains("TEST_FAIL m04.021.eh-catalog.generate",StringComparison.Ordinal)&&File.ReadAllBytes(outputPath).SequenceEqual(selectedSaved),"failed CLI leaves prior output intact");
            Console.WriteLine("EH_CATALOG_GENERATOR_VECTORS_PASS");
        });
    }
    private static void Fixups(byte[] bytes,int pointer,(uint Offset,uint Target,int Kind)[] expected)
    {
        int at=(int)U32(bytes,pointer); Need(S16(bytes,pointer+8)==expected.Length,"fixup count");
        foreach(var e in expected) { Need(U32(bytes,at)==e.Offset && U32(bytes,at+4)==e.Target && S16(bytes,at+8)==e.Kind,"exact fixup"); at+=10; }
    }
    private static (byte[] Object,string Map,string CodeHash,string EhHash) Input()
    {
        // Authored independently of the production writer: three sections, one
        // method with root/funclet frame records, and original unresolved RVAs.
        const string method="OS_Test__Method", eh="__ehinfo_OS_Test__Method";
        byte[] names=Encoding.ASCII.GetBytes(method+'\0'+eh+'\0'+"unwind"+'\0');
        byte[] b=new byte[332+4+names.Length]; W16(b,0,0x8664); W16(b,2,3); W32(b,8,260); W32(b,12,4);
        void Section(int n,string name,uint size,uint raw,uint reloc,ushort count,uint flags)
        { int at=20+n*40; Encoding.ASCII.GetBytes(name).CopyTo(b,at); W32(b,at+16,size); W32(b,at+20,raw); W32(b,at+24,reloc); W16(b,at+32,count); W32(b,at+36,flags); }
        Section(0,".text",32,140,0,0,0x60500020); Section(1,".rdata",12,172,0,0,0x40300040); Section(2,".pdata",24,184,208,6,0x40300040);
        // Adjust symbol-table start after all six relocations.
        W32(b,8,268); Array.Resize(ref b,340+4+names.Length);
        b.AsSpan(140,32).Fill(0x90); new byte[]{2,0,4,16}.CopyTo(b,172);
        W32(b,184,0); W32(b,188,8); W32(b,192,0); W32(b,196,16); W32(b,200,24); W32(b,204,4);
        for(int i=0;i<6;i++){int p=208+i*10; W32(b,p,(uint)i*4); W32(b,p+4,(uint)(i%3==2?2:0)); W16(b,p+8,3);}
        void Symbol(int n,uint name,uint value,ushort section,byte kind)
        {int p=268+n*18;W32(b,p+4,name);W32(b,p+8,value);W16(b,p+12,section);b[p+16]=kind;}
        Symbol(0,4,0,1,2); Symbol(1,(uint)(5+method.Length),0,2,2); Symbol(2,(uint)(6+method.Length+eh.Length),4,2,3); Symbol(3,(uint)(6+method.Length+eh.Length),8,2,3);
        W32(b,340,(uint)(4+names.Length)); names.CopyTo(b,344);
        string codeHash=Convert.ToHexString(SHA256.HashData(b.AsSpan(140,24))), ehHash=Convert.ToHexString(SHA256.HashData(b.AsSpan(172,4)));
        string map=$"<ObjectNodes><MethodCode Name=\"{method}\" Length=\"24\" Hash=\"{codeHash}\"/><MethodExceptionHandlingInfo Name=\"{eh}\" Length=\"4\" Hash=\"{ehHash}\"/></ObjectNodes>";
        return(b,map,codeHash,ehHash);
    }
    private static (byte[] Object,string Map) InputPair()
    {
        // Independent two-method compiler fixture; root and funclet ranges are
        // recorded separately for each method, with original unresolved RVAs.
        string[] names=["OS_Test__First","__ehinfo_OS_Test__First","unwindFirst",
            "OS_Test__Second","__ehinfo_OS_Test__Second","unwindSecond"];
        byte[] strings=Encoding.ASCII.GetBytes(string.Join('\0',names)+'\0');
        byte[] b=new byte[516+strings.Length]; W16(b,0,0x8664); W16(b,2,3); W32(b,8,404); W32(b,12,6);
        void Section(int n,string name,uint size,uint raw,uint reloc,ushort count,uint flags)
        { int at=20+n*40; Encoding.ASCII.GetBytes(name).CopyTo(b,at); W32(b,at+16,size); W32(b,at+20,raw); W32(b,at+24,reloc); W16(b,at+32,count); W32(b,at+36,flags); }
        Section(0,".text",64,140,0,0,0x60500020); Section(1,".rdata",32,204,0,0,0x40300040);
        Section(2,".pdata",48,236,284,12,0x40300040); b.AsSpan(140,64).Fill(0x90);
        new byte[]{2,0,4,16,2,0,4,16}.CopyTo(b,204);
        uint nameOffset=4;
        for(int i=0;i<6;i++)
        {
            int at=404+i*18; W32(b,at+4,nameOffset); nameOffset+=(uint)names[i].Length+1;
            W32(b,at+8,i switch{3=>32,4=>4,2=>8,5=>16,_=>0}); W16(b,at+12,(ushort)(i%3==0?1:2)); b[at+16]=(byte)(i%3==2?3:2);
        }
        for(int method=0;method<2;method++) for(int frame=0;frame<2;frame++)
        {
            int row=method*2+frame, at=236+row*12;
            W32(b,at,(uint)(frame*16)); W32(b,at+4,(uint)(frame*16+8)); W32(b,at+8,(uint)(frame*4));
            for(int word=0;word<3;word++)
            { int fixup=284+(row*3+word)*10; W32(b,fixup,(uint)(row*12+word*4)); W32(b,fixup+4,(uint)(method*3+(word==2?2:0))); W16(b,fixup+8,3); }
        }
        W32(b,512,(uint)(4+strings.Length)); strings.CopyTo(b,516);
        string map="<ObjectNodes>";
        for(int i=0;i<2;i++) map+=$"<MethodCode Name=\"{names[i*3]}\" Length=\"24\" Hash=\"{Convert.ToHexString(SHA256.HashData(b.AsSpan(140+i*32,24)))}\"/><MethodExceptionHandlingInfo Name=\"{names[i*3+1]}\" Length=\"4\" Hash=\"{Convert.ToHexString(SHA256.HashData(b.AsSpan(204+i*4,4)))}\"/>";
        return(b,map+"</ObjectNodes>");
    }
    private static ushort S16(byte[] b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
    private static uint U32(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static void W16(byte[] b,int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at),value);
    private static void W32(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
}
