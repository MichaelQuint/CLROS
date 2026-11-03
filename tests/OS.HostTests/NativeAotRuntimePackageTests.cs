using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotRuntimePackageTests
{
    [HostTest("nativeaot.runtime-package exact relevant source inventories reject raw changes overrides and extra app input")]
    private static void Sources()
    {
        string root=HostToolProcess.FindRepositoryRoot(),temporary=Directory.CreateTempSubdirectory("clros-runtime-sources-").FullName;
        try
        {
            foreach(string app in NativeAotRuntimePayload.Applications)
            {
                string[] paths=NativeAotRuntimePackage.CurrentSources(app).Select(source=>source.Path).ToArray();
                foreach(string relative in paths)
                {string path=Path.Combine(temporary,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.Copy(Path.Combine(root,relative),path,true);}
                NativeAotRuntimePackage.RequireCurrentSources(root,app);NativeAotRuntimePackage.RequireCurrentSources(temporary,app);
                File.WriteAllText(Path.Combine(temporary,"unrelated-kernel.cs"),"unrelated");
                string extra=Path.Combine(temporary,"apps",app,"Extra.cs");File.WriteAllText(extra,"new source");
                Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(temporary,app),"source_inventory");File.Delete(extra);
                string cache=Path.Combine(temporary,"apps",app,"obj");Directory.CreateDirectory(cache);File.WriteAllText(Path.Combine(cache,"generated.cs"),"cache");
                NativeAotRuntimePackage.RequireCurrentSources(temporary,app);
                string config=Path.Combine(temporary,"NuGet.Config");File.WriteAllText(config,"override");
                Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(temporary,app),"source_override");File.Delete(config);
                foreach(string relative in paths)
                {
                    string path=Path.Combine(temporary,relative);byte[] original=File.ReadAllBytes(path);File.AppendAllText(path,"\n");
                    Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(temporary,app),"source_identity");File.WriteAllBytes(path,original);
                }
                NativeAotRuntimePackage.RequireCurrentSources(temporary,app);
            }
            Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(root,"../NativeAotHello"),"runtime_package_app");
        }
        finally{Directory.Delete(temporary,true);}
    }
    [HostTest("nativeaot.runtime-package policies bind distinct ELF digests sixteen bounded slots and explicit IRQs without widening focused selectors")]
    private static void Policy()
    {
        HashSet<string> digests=[];
        foreach(string app in NativeAotRuntimePayload.Applications)
        {
            byte[] manifest=Encoding.ASCII.GetBytes(NativeAotRuntimePackage.Manifest(app));var p=NativeManifestParser.ParseLinuxElf(manifest);
            HostCheck.True(p.Succeeded && p.Threads==16 && p.MemoryBytes==67108864 && p.StackBytes==1572864 && p.Descriptors==16 && p.EnvironmentCount==1 && p.Args.Length==0);
            HostCheck.Equal("apps/"+app,Encoding.ASCII.GetString(manifest,(int)p.Path.Offset,(int)p.Path.Length));
            HostCheck.True(digests.Add(Encoding.ASCII.GetString(manifest,(int)p.Digest.Offset,(int)p.Digest.Length)));
            fixed(byte* bytes=manifest)HostCheck.True(LinuxUserInterruptPolicy.TryRead(bytes,(uint)manifest.Length,p,out bool enabled) && enabled);
            HostCheck.True(!Encoding.ASCII.GetString(manifest).Contains("DOTNET_",StringComparison.Ordinal));
            Reject(()=>NativeAotThreadingPackage.Manifest(app),"threading_package_app");
        }
        foreach(string app in NativeAotThreadingPayload.Applications)Reject(()=>NativeAotRuntimePackage.Manifest(app),"runtime_package_app");
    }
    [HostTest("nativeaot.runtime-package structurally valid counterfeits changed policy reserved bytes and truncation grant no admission")]
    private static void Counterfeits()
    {
        foreach(string app in NativeAotRuntimePayload.Applications)
        {
            int bytes=app=="NativeAotSuspensionStress"?1576912:app=="NativeAotThreadPoolStress"?1627408:1577616;
            byte[] elf=new byte[bytes];LinuxThreadProofFixture.CreateElf("Threads").CopyTo(elf,0);
            string policy=NativeAotRuntimePackage.Manifest(app);int digest=policy.IndexOf("digest=",StringComparison.Ordinal)+7;
            policy=policy[..digest]+Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant()+policy[(digest+64)..];
            byte[] manifest=Encoding.ASCII.GetBytes(policy),package=new byte[64+manifest.Length+elf.Length];
            BinaryPrimitives.WriteUInt64LittleEndian(package,LinuxElfPackage.Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8),1);BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12),1);
            BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16),64);BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20),(uint)manifest.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24),(ulong)elf.Length);manifest.CopyTo(package,64);elf.CopyTo(package,64+manifest.Length);
            HostCheck.True(LinuxElfPackage.Validate(package).Succeeded,"structurally valid counterfeit");
            Reject(()=>NativeAotRuntimePackage.RequirePackage(app,package),"identity");
            Encoding.ASCII.GetBytes(NativeAotRuntimePackage.Manifest(app)).CopyTo(package,64);
            Reject(()=>NativeAotRuntimePackage.RequirePackage(app,package),"identity");
            package[32]=1;Reject(()=>NativeAotRuntimePackage.RequirePackage(app,package),"identity");
            Reject(()=>NativeAotRuntimePackage.RequirePackage(app,package[..^1]),"identity");Reject(()=>NativeAotRuntimePackage.RequirePackage(app,[]),"identity");
        }
    }
    [HostTest("nativeaot.runtime-package missing evidence and existing or escaping output fail without publication")]
    private static void OutputFailures()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Path.Combine(root,"artifacts","runtime-package-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);string output=Path.Combine(directory,"app.pkg");
        try
        {
            Reject(()=>NativeAotRuntimePackage.Write(root,directory,"NativeAotSuspensionStress",output),"runtime_evidence_path");
            HostCheck.True(!File.Exists(output) && !Directory.EnumerateFiles(directory).Any());
            File.WriteAllBytes(output,[1,2,3]);Reject(()=>NativeAotRuntimePackage.Write(root,directory,"NativeAotRuntimeMatrix",output),"output_exists");
            HostCheck.True(File.ReadAllBytes(output).AsSpan().SequenceEqual(new byte[]{1,2,3}));
            Reject(()=>NativeAotRuntimePackage.Write(root,directory,"NativeAotRuntimeMatrix",Path.Combine(root,"escape.pkg")),"output_path");
            var cli=HostToolProcess.RunTool("linux","runtime-package","NativeAotSuspensionSmoke",directory,output);
            HostCheck.True(cli.ExitCode!=0 && !cli.Output.Contains("TEST_PASS linux.runtime-package",StringComparison.Ordinal));
        }
        finally{Directory.Delete(directory,true);}
    }
    private static void Reject(Action action,string reason)
    {
        string? failure=null;try{action();}catch(Exception error)when(error is ArgumentException or InvalidDataException or IOException or ToolFailure){failure=error.Message;}
        HostCheck.True(failure?.Contains(reason,StringComparison.Ordinal)==true,reason+" actual="+failure);
    }
}
