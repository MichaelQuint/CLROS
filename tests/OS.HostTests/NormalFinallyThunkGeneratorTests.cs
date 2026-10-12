using System.Reflection;
using System.Security.Cryptography;

internal static class NormalFinallyThunkGeneratorTests
{
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run("runtime.normal-finally.thunk-instructions-and-cli-controls", () =>
        {
            string root=HostToolProcess.FindRepositoryRoot(),folder=Path.Combine(root,"artifacts/normal-finally-thunk",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _=ExceptionHandlingThunkGeneratorTests.Run(root,folder,"build",["build","tools/OS.X64TextSerializerGenerator","--nologo","-m:1"]);
            var assembly=Assembly.LoadFile(Path.Combine(root,"tools/OS.X64TextSerializerGenerator/bin/Debug/net10.0/OS.X64TextSerializerGenerator.dll"));
            object normal=assembly.GetType("OS.X64TextSerializerGenerator.NormalFinallyThunkGenerator",true)!.GetMethod("Define")!.Invoke(null,null)!;
            object original=assembly.GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator",true)!.GetMethod("Define",Type.EmptyTypes)!.Invoke(null,null)!;
            Array Sections(object value)=>(Array)value.GetType().GetProperty("Sections")!.GetValue(value)!;
            byte[] Text(object value)
            {
                object section=Sections(value).GetValue(0)!;
                return (byte[])section.GetType().GetProperties().Single(p=>p.PropertyType==typeof(byte[])).GetValue(section)!;
            }
            HostCheck.True(Sections(normal).Length==1&&Text(normal).SequenceEqual(Text(original))&&Text(normal).Length==1032,"accepted full capture/GP/XMM/control/transfer instructions retained");
            var symbols=(Array)normal.GetType().GetProperty("Symbols")!.GetValue(normal)!;
            string[] names=[.. symbols.Cast<object>().Select(symbol=>(string)symbol.GetType().GetProperty("Name")!.GetValue(symbol)!)];
            HostCheck.True(names.SequenceEqual(new[]{"RhpThrowEx","CLROS_NormalFinally_Select","CLROS_NormalFinally_ValidateContinuation","CLROS_NormalFinally_Clauses","__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher"}),"distinct original callback/getter/table identities");
            var main=assembly.GetType("OS.X64TextSerializerGenerator.Program",true)!.GetMethod("Main",BindingFlags.Static|BindingFlags.NonPublic)!;
            (int Exit,string Output) Invoke(string[] args)
            {
                TextWriter priorOut=Console.Out,priorError=Console.Error;using StringWriter output=new();
                try {Console.SetOut(output);Console.SetError(output);return((int)main.Invoke(null,[args])!,output.ToString());}
                finally {Console.SetOut(priorOut);Console.SetError(priorError);}
            }
            string[] outputArgs=["--output-root",folder,"--output-file","normal.obj"];
            HostCheck.True(Invoke(["--normal-finally",..outputArgs]).Exit==0,"normal mode emits reviewed object");
            byte[] bytes=File.ReadAllBytes(Path.Combine(folder,"normal.obj"));
            HostCheck.True(bytes.Length==1382&&Convert.ToHexString(SHA256.HashData(bytes))=="B2D9EF13E71861812B082000698E772B83C3BCDC6A4E38695C6EF2DF437A56C8","complete literal object identity");
            foreach(string other in new[]{"--filtered-catch","--filter-invoke","--constructor-failure","--frame-dispatch","--nested-finally-catch","--rethrow-inner","--rethrow-outer","--single-finally-catch","--simple-throw-catch","--interface-dispatch","--checked-reference-barriers"})
            foreach(bool reverse in new[]{false,true})
            {
                var failed=Invoke(reverse?[other,"--normal-finally",..outputArgs]:["--normal-finally",other,..outputArgs]);
                HostCheck.True(failed.Exit==2&&failed.Output.Contains("argument_mode_conflict",StringComparison.Ordinal)&&File.ReadAllBytes(Path.Combine(folder,"normal.obj")).SequenceEqual(bytes),"mode collision leaves output intact");
            }
            HostCheck.True(Invoke(["--normal-finally","--normal-finally",..outputArgs]).Output.Contains("argument_duplicate",StringComparison.Ordinal),"duplicate normal selection rejected");
        });
    }
}
