#if CHECKED_REFERENCE_THUNK_CHILD
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.X64TextSerializerGenerator;























































































































































































































































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
                "tests/OS.HostTests/CheckedReferenceBarrierThunkGeneratorTests.cs",
                "global.json"
            }

            )
                File.Copy(Path.Combine(root, source), Path.Combine(folder, Path.GetFileName(source)), false);
            File.WriteAllText(Path.Combine(folder, "Probe.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <DefineConstants>CHECKED_REFERENCE_THUNK_CHILD</DefineConstants><UseSharedCompilation>false</UseSharedCompilation>
                  </PropertyGroup>
                </Project>
                """, new UTF8Encoding(false));
            Run(folder, "build", 120000, ["build", "Probe.csproj", "--nologo", "--disable-build-servers", "-m:1", "-nodeReuse:false", "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"]);
            string output = Run(folder, "run", 30000, [Path.Combine(folder, "bin", "Debug", "net11.0", "Probe.dll"), Path.Combine(folder, "output"), OS.Tooling.EfiContracts.SerializerObjectSha256.ToUpperInvariant(), OS.Tooling.EfiContracts.SerializerObjectBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            if (output != "TEST_START m04.010.thunk.offline" + Environment.NewLine + "TEST_PASS m04.010.thunk.offline" + Environment.NewLine + "TEST_PASS host.tests" + Environment.NewLine)
                throw new InvalidOperationException("complete thunk child output");
        });
    }

    private static string Run(string folder, string label, int timeout, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        File.WriteAllText(Path.Combine(folder, label + ".command.json"), JsonSerializer.Serialize(new { Executable = "dotnet", Arguments = arguments, WorkingDirectory = folder, TimeoutMilliseconds = timeout }));
        using var process = new Process
        {
            StartInfo = start
        };
        if (!process.Start())
            throw new InvalidOperationException("child failed to start");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        bool timedOut = !process.WaitForExit(timeout);
        if (timedOut)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder, label + ".stdout.log"), stdout, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, label + ".stderr.log"), stderr, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, label + ".result.json"), JsonSerializer.Serialize(new { NativeExit = process.ExitCode, TimedOut = timedOut }));
        if (timedOut || process.ExitCode != 0 || stderr.Length != 0)
            throw new InvalidOperationException("child failed: " + label + "; retained " + folder);
        return stdout;
    }
}
#endif
