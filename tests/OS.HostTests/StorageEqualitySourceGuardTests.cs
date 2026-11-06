using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class StorageEqualitySourceGuardTests
{
    [HostTest("storage equality source guards bind inputs and format constants")]
    private static void BoundInputsAndConstants()
    {
        const string source = """
            using System;
            using Bytes = OS.Std.MemoryExtensions;
            namespace EqualControl;
            internal static class Candidate
            {
                internal static bool Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
                    Bytes.SequenceEqual(left, right);
                internal static bool Signature(ReadOnlySpan<byte> input)
                {
                    return input.Length >= 8 && Bytes.SequenceEqual(input[..8], "EFI PART"u8);
                }
                internal static bool Empty(ReadOnlySpan<byte> input)
                {
                    ReadOnlySpan<byte> zeroes = stackalloc byte[16]
                    {
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                    };
                    return Bytes.SequenceEqual(input, zeroes);
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        bool Check(string candidate, string method)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("moved-equality.cs", "synthetic", syntax)], references);
            return method switch
            {
                "Compare" => StorageEqualitySourceGuard.Parameters(catalog, "EqualControl.Candidate", method),
                "Signature" => StorageEqualitySourceGuard.Literal(catalog, "EqualControl.Candidate", method, "EFI PART", 8),
                "Empty" => StorageEqualitySourceGuard.ZeroBuffer(catalog, "EqualControl.Candidate", method, 16),
                _ => throw new InvalidOperationException("unknown equality control")
            };
        }
        HostCheck.True(Check(source, "Compare") && Check(source, "Signature") && Check(source, "Empty"),
            "aliases, renamed inputs and an expression body retain the shared dependency");
        HostCheck.True(!Check(source.Replace("SequenceEqual(left, right)", "SequenceEqual(left, left)", StringComparison.Ordinal), "Compare"),
            "a self-comparison does not compare both caller inputs");
        HostCheck.True(!Check(source.Replace("=>\n        Bytes.SequenceEqual(left, right);",
            "{ if (false) return Bytes.SequenceEqual(left, right); return true; }", StringComparison.Ordinal), "Compare"),
            "a dead nested return cannot conceal another result");
        HostCheck.True(!Check(source.Replace("=>\n        Bytes.SequenceEqual(left, right);",
            "{ for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false; return Bytes.SequenceEqual(left, right); }",
            StringComparison.Ordinal), "Compare"), "a shared-call decoy cannot retain a manual equality loop");
        HostCheck.True(!Check(source.Replace("input.Length >= 8 &&", "input.Length >= 8 && input[0] == (byte)'E' &&",
            StringComparison.Ordinal), "Signature"), "a shared-call decoy cannot retain manual signature byte checks");
        HostCheck.True(!Check(source.Replace("EFI PART", "EFI PArT", StringComparison.Ordinal), "Signature"),
            "the GPT signature stays an independent byte-format expectation");
        HostCheck.True(!Check(source.Replace("input[..8]", "input[..7]", StringComparison.Ordinal), "Signature"),
            "the GPT signature consumes its complete width");
        HostCheck.True(Check(source.Replace("input[..8]", "input.Slice(0, 8)", StringComparison.Ordinal), "Signature"),
            "equivalent prefix slice spelling is accepted");
        HostCheck.True(Check(source.Replace("\"EFI PART\"u8", "\"\"\"EFI PART\"\"\"u8", StringComparison.Ordinal), "Signature"),
            "equivalent raw UTF-8 literal spelling is accepted");
        string local = source.Replace("return input.Length >= 8 && Bytes.SequenceEqual(input[..8], \"EFI PART\"u8);",
            "ReadOnlySpan<byte> marker = \"EFI PART\"u8; return Bytes.SequenceEqual(input.Slice(0, marker.Length), marker);", StringComparison.Ordinal);
        HostCheck.True(Check(local, "Signature"), "a renamed immutable local retains the exact UTF-8 signature and prefix width");
        HostCheck.True(!Check(local.Replace("marker.Length", "input.Length", StringComparison.Ordinal), "Signature"),
            "the input length cannot substitute for the fixed signature width");
        HostCheck.True(!Check(local.Replace("return Bytes.SequenceEqual(input.Slice", "marker = input; return Bytes.SequenceEqual(input.Slice", StringComparison.Ordinal), "Signature"),
            "a reassigned local cannot make the signature a self-comparison");
        HostCheck.True(!Check(local.Replace("Slice(0, marker.Length)", "Slice(1, marker.Length)", StringComparison.Ordinal), "Signature"),
            "the GPT signature starts at byte zero");
        HostCheck.True(!Check(source.Replace("0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0",
            "1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0", StringComparison.Ordinal), "Empty"),
            "all GUID comparison bytes are zero");
        HostCheck.True(!Check(source.Replace("return Bytes.SequenceEqual(input, zeroes);",
            "zeroes = input; return Bytes.SequenceEqual(input, zeroes);", StringComparison.Ordinal), "Empty"),
            "reassignment cannot turn the zero buffer into a self-comparison");
        const string foreign = """
            internal static class Foreign
            {
                internal static bool SequenceEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) => false;
            }
            """;
        HostCheck.True(!Check(source.Replace("using Bytes = OS.Std.MemoryExtensions;",
            "using Bytes = EqualControl.Foreign;", StringComparison.Ordinal) + "\n" + foreign, "Compare"),
            "an unrelated same-named method cannot satisfy the shared dependency");
        const string conversion = """
            internal readonly ref struct Redirect
            {
                public static implicit operator Redirect(ReadOnlySpan<byte> value) => default;
                public static implicit operator ReadOnlySpan<byte>(Redirect value) => default;
            }
            """;
        HostCheck.True(!Check(source.Replace("SequenceEqual(left, right)",
            "SequenceEqual((ReadOnlySpan<byte>)(Redirect)left, right)", StringComparison.Ordinal) + "\n" + conversion, "Compare"),
            "a user-defined conversion cannot retain caller parameter identity");
    }
}
