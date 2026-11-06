using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static unsafe partial class ThreadContextSwitchBytesTests
{
    private const int BufferBytes = 64;
    private const ulong Value = 0x8877665544332211UL;
    private static ReadOnlySpan<byte> Encoding => [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88];

    [HostTest("thread.context-switch.bytes.count8-every-alignment-and-mismatch-lane")]
    private static void Count8EveryAlignmentAndMismatchLane()
    {
        using var method = Compile();
        byte* data = stackalloc byte[BufferBytes];
        for (uint offset = 0; offset < 16; offset++)
        {
            byte[] original = Seed(data, offset, Encoding[..8]);
            Check(method, data, offset, offset + 8, true, true, 8, offset + 8, true, "equal offset " + offset);
            Unchanged(data, original);
            for (int lane = 0; lane < 8; lane++)
            {
                data[offset + lane] ^= 0x80;
                byte[] mismatch = new ReadOnlySpan<byte>(data, BufferBytes).ToArray();
                Check(method, data, offset, offset + 8, true, true, 8, offset + 8, false, "mismatch offset/lane " + offset + "/" + lane);
                Unchanged(data, mismatch);
                data[offset + lane] ^= 0x80;
            }

            Unchanged(data, original);
        }
    }

    [HostTest("thread.context-switch.bytes.count8-short-remaining-and-past-limit")]
    private static void Count8ShortRemainingAndPastLimit()
    {
        using var method = Compile();
        byte* data = stackalloc byte[BufferBytes];
        for (uint remaining = 0; remaining < 8; remaining++)
        {
            const uint position = 12;
            byte[] original = Seed(data, position, Encoding[..(int)remaining]);
            Check(method, data, position, position + remaining, true, true, 8, position + 8, false, "compare remaining " + remaining);
            Unchanged(data, original);
            // Emission still writes the admitted prefix before latching failure;
            // the bytes after the limit are independent canaries.
            original = Seed(data, position, ReadOnlySpan<byte>.Empty);
            Encoding[..(int)remaining].CopyTo(original.AsSpan((int)position));
            Check(method, data, position, position + remaining, false, true, 8, position + 8, false, "emit remaining " + remaining);
            Unchanged(data, original);
        }

        byte[] past = Seed(data, 0, ReadOnlySpan<byte>.Empty);
        Check(method, data, 12, 7, true, true, 8, 20, false, "position beyond limit");
        Unchanged(data, past);
    }

    [HostTest("thread.context-switch.bytes.null-dry-and-eight-byte-emission")]
    private static void NullDryAndEightByteEmission()
    {
        using var method = Compile();
        for (int mode = 0; mode < 2; mode++)
        {
            bool compare = mode == 1;
            Check(method, null, 3, 11, compare, true, 8, 11, true, "null complete extent");
            Check(method, null, 3, 10, compare, true, 8, 11, false, "null short extent");
            Check(method, null, 12, 7, compare, true, 8, 20, false, "null beyond limit");
            Check(method, null, 3, 11, compare, false, 8, 11, false, "null invalid latch");
        }

        byte* data = stackalloc byte[BufferBytes];
        byte[] expected = Seed(data, 0, ReadOnlySpan<byte>.Empty);
        Encoding[..8].CopyTo(expected.AsSpan(5));
        Check(method, data, 5, 13, false, true, 8, 13, true, "eight-byte emission");
        Unchanged(data, expected);
    }

    [HostTest("thread.context-switch.bytes.non-eight-encodings-and-original-masked-shifts")]
    private static void NonEightEncodingsAndOriginalMaskedShifts()
    {
        using var method = Compile();
        byte* data = stackalloc byte[BufferBytes];
        // The independent literal repeats at byte8, preserving C# ulong shift
        // masking for the existing count16 loop rather than inventing zero fill.
        for (int count = 0; count <= 16; count++)
        {
            if (count == 8)
                continue;
            uint end = 3 + (uint)count;
            byte[] original = Seed(data, 3, Encoding[..count]);
            Check(method, data, 3, end, true, true, count, end, true, "compare count " + count);
            Unchanged(data, original);
            if (count > 0)
            {
                data[3 + count - 1] ^= 0x80;
                byte[] mismatch = new ReadOnlySpan<byte>(data, BufferBytes).ToArray();
                Check(method, data, 3, end, true, true, count, end, false, "mismatch count " + count);
                Unchanged(data, mismatch);
            }

            byte[] expected = Seed(data, 0, ReadOnlySpan<byte>.Empty);
            Encoding[..count].CopyTo(expected.AsSpan(3));
            Check(method, data, 3, end, false, true, count, end, true, "emit count " + count);
            Unchanged(data, expected);
        }
    }

    [HostTest("thread.context-switch.bytes.invalid-latch-keeps-position-and-emission")]
    private static void InvalidLatchKeepsPositionAndEmission()
    {
        using var method = Compile();
        byte* data = stackalloc byte[BufferBytes];
        byte[] original = Seed(data, 3, Encoding);
        method.Apply((nint)data, 3, 19, true, false, Value, 8, out uint position, out bool valid);
        HostCheck.True(position == 11 && !valid, "already-invalid word still advances");
        method.Apply((nint)data, position, 19, true, valid, Value, 8, out position, out valid);
        HostCheck.True(position == 19 && !valid, "next equal word cannot clear invalid latch");
        Unchanged(data, original);
        data[3] ^= 0x80;
        method.Apply((nint)data, 3, 19, true, true, Value, 8, out position, out valid);
        HostCheck.True(position == 11 && !valid, "mismatch latches failure");
        data[3] ^= 0x80;
        method.Apply((nint)data, position, 19, true, valid, Value, 8, out position, out valid);
        HostCheck.True(position == 19 && !valid, "later comparison still advances after mismatch");
        Unchanged(data, original);
        byte[] expected = Seed(data, 0, ReadOnlySpan<byte>.Empty);
        Encoding[..8].CopyTo(expected.AsSpan(3));
        Check(method, data, 3, 11, false, false, 8, 11, false, "invalid emission still writes and advances");
        Unchanged(data, expected);
    }

    private static byte[] Seed(byte* data, uint offset, ReadOnlySpan<byte> bytes)
    {
        new Span<byte>(data, BufferBytes).Fill(0xA5);
        bytes.CopyTo(new Span<byte>(data + offset, bytes.Length));
        return new ReadOnlySpan<byte>(data, BufferBytes).ToArray();
    }

    private static void Unchanged(byte* data, ReadOnlySpan<byte> expected) => HostCheck.True(new ReadOnlySpan<byte>(data, BufferBytes).SequenceEqual(expected), "exact bytes and canaries");
    private static void Check(CompiledMethod method, byte* data, uint position, uint limit, bool compare, bool valid, int count, uint expectedPosition, bool expectedValid, string reason)
    {
        method.Apply((nint)data, position, limit, compare, valid, Value, count, out uint actualPosition, out bool actualValid);
        HostCheck.True(actualPosition == expectedPosition && actualValid == expectedValid, reason);
    }

    private static CompiledMethod Compile()
    {
        HostCheck.True(BitConverter.IsLittleEndian && RuntimeInformation.ProcessArchitecture == Architecture.X64, "production word contract is little-endian x64 ordinary memory");
        string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS.Kernel/Threading/ThreadContextSwitch.StubWriter.cs");
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.CSharp14));
        var writer = tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().Single(s => s.Identifier.ValueText == "StubWriter");
        var method = writer.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Bytes");
        HostCheck.True(method.ReturnType.ToString() == "void" && method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString() == "ulong" && method.ParameterList.Parameters[1].Type!.ToString() == "int", "actual production signature");
        string[] names = ["_output", "_limit", "_compare", "_valid", "_position"];
        var fields = writer.Members.OfType<FieldDeclarationSyntax>().Where(f => f.Declaration.Variables.Any(v => names.Contains(v.Identifier.ValueText))).ToArray();
        HostCheck.True(new HashSet<string>(fields.SelectMany(f => f.Declaration.Variables).Select(v => v.Identifier.ValueText)).SetEquals(names), "only the five actual method fields");
        // Compile the production method and field declarations unchanged. The
        // fixture supplies initial state and raw observations, not an algorithm.
        string source = $$"""
            public static unsafe class BytesFixture
            {
                private struct StubWriter
                {
                    {{string.Join("\n", fields.Select(f => f.ToFullString()))}}
                    public StubWriter(nint output, uint position, uint limit, bool compare, bool valid)
                    { _output = (byte*)output; _position = position; _limit = limit; _compare = compare; _valid = valid; }
                    {{method.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()}}
                    public uint Position => _position;
                    public bool Valid => _valid;
                }
                public static void Apply(nint output, uint position, uint limit, bool compare, bool valid,
                    ulong value, int count, out uint finalPosition, out bool finalValid)
                {
                    var writer = new StubWriter(output, position, limit, compare, valid);
                    writer.Bytes(value, count);
                    finalPosition = writer.Position; finalValid = writer.Valid;
                }
            }
            """;
        var compilation = CSharpCompilation.Create("ThreadContextSwitchBytes", [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        HostCheck.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        image.Position = 0;
        var context = new AssemblyLoadContext("thread-context-switch-bytes", isCollectible: true);
        try
        {
            Type fixture = context.LoadFromStream(image).GetType("BytesFixture", throwOnError: true)!;
            return new CompiledMethod(context, fixture.GetMethod("Apply")!.CreateDelegate<ApplyBytes>());
        }
        catch
        {
            context.Unload();
            throw;
        }
    }
}
