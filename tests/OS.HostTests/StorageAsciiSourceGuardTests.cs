using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class StorageAsciiSourceGuardTests
{
    [HostTest("storage ASCII source guards bind comparison inputs and FAT fields")]
    private static void InputsAndFields()
    {
        const string source = """
            using System;
            using Case = OS.Std.Ascii;
            using Text = OS.Std.Text.AsciiString;
            namespace AsciiControl;
            internal static class Candidate
            {
                internal static bool Compare(ReadOnlySpan<char> left, string right)
                {
                    if (left.Length != right.Length) return false;
                    for (int index = 0; index < left.Length; index++)
                    {
                        if (Case.ToUpperInvariant(left[index]) != Case.ToUpperInvariant(right[index])) return false;
                    }
                    return true;
                }
                internal static string Decode(ReadOnlySpan<byte> input)
                {
                    int first = Text.TrimTrailingSpace(input[..8]).Length;
                    int second = Text.TrimTrailingSpace(input.Slice(8, 3)).Length;
                    Span<char> output = stackalloc char[12];
                    for (int index = 0; index < first; index++) output[index] = (char)input[index];
                    for (int index = 0; index < second; index++) output[8 + index] = (char)input[8 + index];
                    return new string(output);
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        bool Check(string candidate, string name)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("relocated-ascii.cs", "synthetic", syntax)], references);
            return name == "Compare" ? StorageAsciiSourceGuard.LongNames(catalog, "AsciiControl.Candidate", name)
                : StorageAsciiSourceGuard.ShortNameFields(catalog, "AsciiControl.Candidate", name);
        }
        HostCheck.True(Check(source, "Compare"), "aliases and renamed locals preserve the ASCII comparison");
        HostCheck.True(Check(source, "Decode"), "aliases and renamed locals preserve the FAT field trimming");
        HostCheck.True(!Check(source.Replace("ToUpperInvariant(right[index])", "ToUpperInvariant(left[index])", StringComparison.Ordinal), "Compare"),
            "both caller inputs must reach the case comparison");
        HostCheck.True(!Check(source.Replace("right[index]", "right[0]", StringComparison.Ordinal), "Compare"),
            "the compared characters use the same index symbol");
        HostCheck.True(!Check(source.Replace("right[index])) return false", "right[index])) return true", StringComparison.Ordinal), "Compare"),
            "a differing normalized character rejects the name");
        HostCheck.True(!Check(source.Replace("input[..8]", "input[..7]", StringComparison.Ordinal), "Decode"), "the FAT base-name field keeps its eight-byte width");
        HostCheck.True(!Check(source.Replace("input.Slice(8, 3)", "input.Slice(7, 3)", StringComparison.Ordinal), "Decode"), "the extension starts after the base-name field");
        HostCheck.True(!Check(source.Replace("input.Slice(8, 3)", "input.Slice(8, 2)", StringComparison.Ordinal), "Decode"), "the extension keeps its three-byte width");
        HostCheck.True(Check(source.Replace("input.Slice(8, 3)", "input[8..11]", StringComparison.Ordinal), "Decode"), "equivalent range spelling preserves FAT fields");
        HostCheck.True(!Check(source.Replace("Span<char> output =", "first = 0; Span<char> output =", StringComparison.Ordinal), "Decode"),
            "reassigning the trimmed length cannot keep its source identity");
        HostCheck.True(!Check(source.Replace("index < first", "index < 8", StringComparison.Ordinal), "Decode"), "a discarded trimming result cannot satisfy the dependency");
        HostCheck.True(!Check(source.Replace("internal static bool Compare(",
            "private static char ToUpperAscii(char value) => value; internal static bool Compare(", StringComparison.Ordinal), "Compare"),
            "the parser does not regain its private case-conversion helper");
        HostCheck.True(!Check(source.Replace("internal static string Decode(",
            "private static int TrimTrailingSpaces(ReadOnlySpan<byte> value) => value.Length; internal static string Decode(",
            StringComparison.Ordinal), "Decode"), "the parser does not regain its private trailing-space helper");
        const string foreign = """
            internal static class Foreign
            {
                internal static char ToUpperInvariant(char value) => value;
            }
            """;
        HostCheck.True(!Check(source.Replace("using Case = OS.Std.Ascii;", "using Case = AsciiControl.Foreign;",
            StringComparison.Ordinal) + "\n" + foreign, "Compare"), "a foreign same-named helper cannot satisfy the shared ASCII dependency");
    }
}
