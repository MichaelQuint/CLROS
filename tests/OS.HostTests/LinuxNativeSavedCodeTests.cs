using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static unsafe class LinuxNativeSavedCodeTests
{
    [HostTest("linux.native-saved-code.actual-extent-equal-null-and-canaries")]
    private static void ActualExtentEqualNullAndCanaries()
    {
        using var method = Compile(ProductionMethod(), LinuxThreadProgram.Bytes);
        using var bytes = new Buffers(checked((int)LinuxThreadProgram.Bytes));
        HostCheck.True(method.Compare((nint)bytes.Code, (nint)bytes.Copy));
        HostCheck.True(!method.Compare((nint)bytes.Code, 0), "null saved code rejects");
        HostCheck.True(!method.Compare(0, 0), "null saved code rejects before reading code");
        bytes.AssertUnchanged();
    }

    [HostTest("linux.native-saved-code.actual-extent-every-byte-on-both-sides")]
    private static void ActualExtentEveryByteOnBothSides()
    {
        using var method = Compile(ProductionMethod(), LinuxThreadProgram.Bytes);
        using var bytes = new Buffers(checked((int)LinuxThreadProgram.Bytes));
        CheckEveryPosition(method, bytes);
    }

    [HostTest("linux.native-saved-code.zero-short-words-and-all-byte-tails")]
    private static void ZeroShortWordsAndAllByteTails()
    {
        MethodDeclarationSyntax production = ProductionMethod();
        // A fixture constant supplies only the extent. The production method's
        // statements are compiled unchanged for zero, short and every tail size.
        for (uint length = 0; length < 24; length++)
        {
            using var method = Compile(production, length);
            using var bytes = new Buffers((int)length);
            HostCheck.True(method.Compare((nint)bytes.Code, (nint)bytes.Copy), "equal extent " + length);
            HostCheck.True(!method.Compare((nint)bytes.Code, 0), "null copy extent " + length);
            if (length == 0)
                HostCheck.True(method.Compare(0, (nint)bytes.Copy), "zero extent does not read code");
            CheckEveryPosition(method, bytes);
        }
    }

    private static void CheckEveryPosition(CompiledMethod method, Buffers bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes.Copy[i] ^= 0x80;
            bool savedEqual = method.Compare((nint)bytes.Code, (nint)bytes.Copy);
            bytes.Copy[i] ^= 0x80;
            HostCheck.True(!savedEqual, "saved mismatch at " + i);

            bytes.Code[i] ^= 0x80;
            bool codeEqual = method.Compare((nint)bytes.Code, (nint)bytes.Copy);
            bytes.Code[i] ^= 0x80;
            HostCheck.True(!codeEqual, "live mismatch at " + i);
        }
        HostCheck.True(method.Compare((nint)bytes.Code, (nint)bytes.Copy), "restored equal extent");
        bytes.AssertUnchanged();
    }

    private static MethodDeclarationSyntax ProductionMethod()
    {
        string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.LinuxThreadRun.cs");
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path),
            new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["M16_008_PROBE"]));
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "LinuxNativeSavedCode");
        HostCheck.True(method.ReturnType.ToString() == "bool" && method.ParameterList.Parameters.Count == 1 &&
            method.ParameterList.Parameters[0].Type!.ToString() == "LinuxNativeControl*", "actual production signature");
        return method;
    }

    private static CompiledMethod Compile(MethodDeclarationSyntax production, uint length)
    {
        // Bind only the two borrowed pointers and NativeProgram.Bytes. This is
        // host algorithm evidence, not the native control's layout or lifecycle.
        string source = $$"""
            public static unsafe class SavedCodeFixture
            {
                private struct State { public ulong CooperativePool; }
                private struct LinuxNativeControl { public State* State; public byte* CodeCopy; }
                private static class NativeProgram { public const uint Bytes = {{length}}u; }
                {{production.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()}}
                public static bool Compare(nint code, nint saved)
                {
                    State state = new State { CooperativePool = (ulong)code };
                    LinuxNativeControl control = new LinuxNativeControl { State = &state, CodeCopy = (byte*)saved };
                    return LinuxNativeSavedCode(&control);
                }
            }
            """;
        var compilation = CSharpCompilation.Create("SavedCode" + length,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        HostCheck.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        image.Position = 0;
        var context = new AssemblyLoadContext("native-saved-code-" + length, isCollectible: true);
        try
        {
            Type fixture = context.LoadFromStream(image).GetType("SavedCodeFixture", throwOnError: true)!;
            return new CompiledMethod(context, fixture.GetMethod("Compare")!.CreateDelegate<Func<nint, nint, bool>>());
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    private sealed class CompiledMethod(AssemblyLoadContext context, Func<nint, nint, bool> compare) : IDisposable
    {
        internal bool Compare(nint code, nint saved) => compare(code, saved);
        public void Dispose() => context.Unload();
    }

    private sealed class Buffers : IDisposable
    {
        private byte* codeAllocation;
        private byte* copyAllocation;
        private readonly int allocationBytes;
        private readonly byte[] originalCode;
        private readonly byte[] originalCopy;
        internal int Length { get; }
        internal byte* Code => codeAllocation + 8;
        internal byte* Copy => copyAllocation + 8;

        internal Buffers(int length)
        {
            Length = length;
            allocationBytes = checked((length + 16 + 7) & ~7);
            codeAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)allocationBytes, 8);
            try
            {
                copyAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)allocationBytes, 8);
                // Unequal guards before and after the extent expose comparisons
                // beyond it, without claiming to detect unobserved machine loads.
                new Span<byte>(codeAllocation, allocationBytes).Fill(0xA5);
                new Span<byte>(copyAllocation, allocationBytes).Fill(0x5A);
                for (int i = 0; i < length; i++) Code[i] = Copy[i] = unchecked((byte)(i * 73 + 19));
                originalCode = new ReadOnlySpan<byte>(codeAllocation, allocationBytes).ToArray();
                originalCopy = new ReadOnlySpan<byte>(copyAllocation, allocationBytes).ToArray();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void AssertUnchanged()
        {
            HostCheck.True(new ReadOnlySpan<byte>(codeAllocation, allocationBytes).SequenceEqual(originalCode), "live bytes and guards unchanged");
            HostCheck.True(new ReadOnlySpan<byte>(copyAllocation, allocationBytes).SequenceEqual(originalCopy), "saved bytes and guards unchanged");
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(codeAllocation);
            NativeMemory.AlignedFree(copyAllocation);
            codeAllocation = copyAllocation = null;
        }
    }
}
