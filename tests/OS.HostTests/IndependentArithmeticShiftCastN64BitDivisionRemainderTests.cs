using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Std.Numerics;
using Catalog = OS.Std.Numerics.IndependentArithmeticShiftCastN64BitDivisionRemainder;

internal static class IndependentArithmeticShiftCastN64BitDivisionRemainderTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.004");
        try
        {
            CheckCatalog();
            CheckBoundaryMatrix();
            CheckFailuresAndAllocation();
            CheckNoStdLibCompilation();
            Console.WriteLine("CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28"
                + " referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.004");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.004 " + error.GetType().Name + ": " + error.Message);
            throw;
        }
    }

    private static void CheckCatalog()
    {
        Require(Catalog.VectorCount == 57 && Catalog.ExpectedFingerprint == 0x45FDFA24DD72EA28UL,
            "frozen catalog identity");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<ArithmeticConformanceVector>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<ArithmeticEvaluation>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<ArithmeticConformanceResult>(),
            "POD results");
        HashSet<ArithmeticOperation> seen = [];
        HashSet<(ArithmeticOperation, ulong, ulong)> inputs = [];
        int rejected = 0;
        for (int i = 0; i < Catalog.VectorCount; i++)
        {
            Require(Catalog.TryGetVector(i, out var vector), "missing vector " + i);
            Require(inputs.Add((vector.Operation, vector.Left, vector.Right)), "duplicate vector " + i);
            seen.Add(vector.Operation);
            var expected = Model(vector.Operation, vector.Left, vector.Right);
            Require(vector.ExpectedStatus == expected.Status && vector.Expected == expected.Value,
                "independent literal oracle " + i);
            var actual = Catalog.Evaluate(vector.Operation, vector.Left, vector.Right);
            Require(actual.Status == expected.Status && actual.Value == expected.Value,
                "executed catalog vector " + i);
            if (expected.Status != ArithmeticEvaluationStatus.Success) rejected++;
        }
        Require(seen.Count == 30 && rejected == 6, "operation/rejection coverage");
        foreach (int index in new[] { int.MinValue, -1, 57, int.MaxValue })
        {
            Require(!Catalog.TryGetVector(index, out var invalid) &&
                invalid.Operation == ArithmeticOperation.Uninitialized &&
                invalid.ExpectedStatus == ArithmeticEvaluationStatus.Uninitialized &&
                invalid.Expected == 0 && invalid.Left == 0 && invalid.Right == 0,
                "out of range clears output");
        }

        var run = Catalog.RunCatalog();
        Require(run.Succeeded && run.Status == ArithmeticConformanceStatus.Passed &&
            run.CompletedCount == 57 && run.FailedIndex == -1 &&
            run.Fingerprint == 0x45FDFA24DD72EA28UL, "exact execution result");
    }

    private static void CheckBoundaryMatrix()
    {
        ulong[] values = [0, 1, 2, 0x7F, 0x80, 0xFF, 0x8000, 0xFFFF,
            0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0x100000001,
            0x7FFFFFFFFFFFFFFF, 0x8000000000000000, 0xFFFFFFFFFFFFFFFF];
        ulong[] rightValues = [0, 1, 2, 3, 7, 31, 32, 33, 63, 64, 65,
            0xFFFFFFFF, 0x8000000000000000, 0xFFFFFFFFFFFFFFFF];
        int comparisons = 0;
        foreach (ArithmeticOperation operation in Enum.GetValues<ArithmeticOperation>())
        {
            if (operation == ArithmeticOperation.Uninitialized) continue;
            foreach (ulong left in values)
            foreach (ulong right in rightValues)
            {
                var expected = Model(operation, left, right);
                var actual = Catalog.Evaluate(operation, left, right);
                Require(actual.Status == expected.Status && actual.Value == expected.Value,
                    $"matrix {operation} left={left:X16} right={right:X16}");
                comparisons++;
            }
        }
        Require(comparisons == 6300, "boundary matrix count");
    }

    // An arbitrary-precision oracle, independent of the fixed-width operators.
    // Convert by congruence modulo 2^width, then decode the sign bit explicitly.
    private static BigInteger Unsigned(ulong value, int width) =>
        new BigInteger(value) % BigInteger.Pow(2, width);
    private static BigInteger Signed(ulong value, int width)
    {
        BigInteger raw = Unsigned(value, width);
        BigInteger modulus = BigInteger.Pow(2, width);
        return raw >= modulus / 2 ? raw - modulus : raw;
    }
    private static ulong Bits(BigInteger value)
    {
        BigInteger modulus = BigInteger.Pow(2, 64);
        return (ulong)((value % modulus + modulus) % modulus);
    }

    private static (ArithmeticEvaluationStatus Status, ulong Value) Model(
        ArithmeticOperation operation, ulong left, ulong right)
    {
        BigInteger value;
        BigInteger u32 = Unsigned(left, 32);
        BigInteger v32 = Unsigned(right, 32);
        BigInteger u64 = new(left);
        BigInteger v64 = new(right);
        BigInteger signed = Signed(left, 64);
        BigInteger divisor = Signed(right, 64);
        bool signedDivision = operation is ArithmeticOperation.DivideI64 or ArithmeticOperation.RemainderI64;
        bool unsignedDivision = operation is ArithmeticOperation.DivideU64 or ArithmeticOperation.RemainderU64;
        if (signedDivision || unsignedDivision)
        {
            if (right == 0) return (ArithmeticEvaluationStatus.DivideByZero, 0);
            if (signedDivision && signed == -BigInteger.Pow(2, 63) && divisor == -1)
                return (ArithmeticEvaluationStatus.SignedOverflow, 0);
        }
        switch (operation)
        {
            case ArithmeticOperation.Add32: value = (u32 + v32) % BigInteger.Pow(2, 32); break;
            case ArithmeticOperation.Subtract32:
                value = (u32 - v32 + BigInteger.Pow(2, 32)) % BigInteger.Pow(2, 32); break;
            case ArithmeticOperation.Multiply32: value = (u32 * v32) % BigInteger.Pow(2, 32); break;
            case ArithmeticOperation.Add64: value = u64 + v64; break;
            case ArithmeticOperation.Subtract64: value = u64 - v64; break;
            case ArithmeticOperation.Multiply64: value = u64 * v64; break;
            case ArithmeticOperation.ShiftLeft32:
                value = u32 * BigInteger.Pow(2, (int)(right % 32)) % BigInteger.Pow(2, 32); break;
            case ArithmeticOperation.ShiftLeft64:
                value = u64 * BigInteger.Pow(2, (int)(right % 64)); break;
            case ArithmeticOperation.ShiftRightUnsigned32:
                value = u32 / BigInteger.Pow(2, (int)(right % 32)); break;
            case ArithmeticOperation.ShiftRightUnsigned64:
                value = u64 / BigInteger.Pow(2, (int)(right % 64)); break;
            case ArithmeticOperation.ShiftRightSigned32:
            case ArithmeticOperation.ShiftRightSigned64:
                int width = operation == ArithmeticOperation.ShiftRightSigned32 ? 32 : 64;
                BigInteger number = Signed(left, width);
                BigInteger power = BigInteger.Pow(2, (int)(right % (ulong)width));
                value = (number < 0 ? number - power + 1 : number) / power; break;
            case ArithmeticOperation.CastI8:
            case ArithmeticOperation.EnumI8: value = Signed(left, 8); break;
            case ArithmeticOperation.CastU8:
            case ArithmeticOperation.EnumU8: value = Unsigned(left, 8); break;
            case ArithmeticOperation.CastI16:
            case ArithmeticOperation.EnumI16: value = Signed(left, 16); break;
            case ArithmeticOperation.CastU16:
            case ArithmeticOperation.EnumU16: value = Unsigned(left, 16); break;
            case ArithmeticOperation.CastI32:
            case ArithmeticOperation.EnumI32: value = Signed(left, 32); break;
            case ArithmeticOperation.CastU32:
            case ArithmeticOperation.EnumU32: value = Unsigned(left, 32); break;
            case ArithmeticOperation.EnumI64: value = signed; break;
            case ArithmeticOperation.EnumU64: value = u64; break;
            case ArithmeticOperation.DivideI64: value = signed / divisor; break;
            case ArithmeticOperation.RemainderI64: value = signed - (signed / divisor) * divisor; break;
            case ArithmeticOperation.DivideU64: value = u64 / v64; break;
            case ArithmeticOperation.RemainderU64: value = u64 - (u64 / v64) * v64; break;
            default: return (ArithmeticEvaluationStatus.UnknownOperation, 0);
        }
        return (ArithmeticEvaluationStatus.Success, Bits(value));
    }

    private static void CheckFailuresAndAllocation()
    {
        Require(!default(ArithmeticEvaluation).Succeeded &&
            !default(ArithmeticConformanceResult).Succeeded, "default result fails closed");
        for (int code = 0; code <= byte.MaxValue; code++)
        {
            if (code >= 1 && code <= 30) continue;
            var result = Catalog.Evaluate((ArithmeticOperation)code, ulong.MaxValue, 0);
            Require(result.Status == ArithmeticEvaluationStatus.UnknownOperation &&
                result.Value == 0 && !result.Succeeded, "unknown operation " + code);
        }
        for (int i = 0; i < 100; i++) _ = Catalog.RunCatalog();
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool passed = true;
        for (int i = 0; i < 1000; i++) passed &= Catalog.RunCatalog().Succeeded;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(passed && allocated == 0, "allocation-free repeated execution");
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "m04-004", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        File.WriteAllText(Path.Combine(directory, "Shim.cs"), """
            namespace System
            {
                public class Object { }
                public abstract class ValueType : Object { }
                public abstract class Enum : ValueType { }
                public abstract class Delegate : Object { }
                public abstract class MulticastDelegate : Delegate { }
                public class Attribute : Object { }
                public struct Void { }
                public struct Boolean { }
                public struct Byte { }
                public struct SByte { }
                public struct Int16 { }
                public struct UInt16 { }
                public struct Int32 { }
                public struct UInt32 { }
                public struct Int64 { }
                public struct UInt64 { }
                public struct IntPtr { }
                public struct UIntPtr { }
                public enum AttributeTargets { }
                public sealed class AttributeUsageAttribute : Attribute
                {
                    public AttributeUsageAttribute(AttributeTargets validOn) { }
                    public bool AllowMultiple { get; set; }
                    public bool Inherited { get; set; }
                }
            }
            namespace System.Runtime.CompilerServices
            {
                public sealed class IsReadOnlyAttribute : System.Attribute { }
                public sealed class CompilerGeneratedAttribute : System.Attribute { }
                public enum MethodImplOptions { NoInlining = 8 }
                public sealed class MethodImplAttribute : System.Attribute
                {
                    public MethodImplAttribute(MethodImplOptions options) { }
                }
            }
            """, new UTF8Encoding(false));
        string production = System.Security.SecurityElement.Escape(Path.Combine(root, "std",
            "no-runtime", "shared", "IndependentArithmeticShiftCastN64BitDivisionRemainder.cs"))!;
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType>
                <AssemblyName>CLROS.M04.Arithmetic.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <NoStdLib>true</NoStdLib>
                <NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType>
                <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Shim.cs" />
                <Compile Include="{{production}}" Link="Arithmetic.cs" />
              </ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile"
                      AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup>
                  <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release" })
            start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("compile start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        Require(exited && process.ExitCode == 0, "no-stdlib compile; logs=" + directory
            + " " + stdout.Result + stderr.Result);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net10.0",
            "CLROS.M04.Arithmetic.NoStdLib.dll"));
        using PEReader image = new(stream);
        Require(image.GetMetadataReader().AssemblyReferences.Count == 0, "no framework references");
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("CLROS root not found");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
