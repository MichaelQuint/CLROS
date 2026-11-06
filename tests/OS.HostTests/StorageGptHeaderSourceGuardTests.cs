using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class StorageGptHeaderSourceGuardTests
{
    [HostTest("GPT header guard binds copied bytes, failure routing and CRC exclusion across partials")]
    private static void CopyAndCrcControls()
    {
        const string source = """
            using System;
            using OS.Storage;
            using Copy = OS.Std.Memory.BufferOperations;
            using Endian = OS.Std.Binary.EndianPrimitives;
            using Hash = OS.Std.Hash.Crc32;
            namespace HeaderControl;
            internal static partial class Candidate
            {
                private static bool TryComputeHeaderCrc32(ReadOnlySpan<byte> sector, uint length, out uint crc)
                {
                    crc = 0;
                    Span<byte> scratch = stackalloc byte[checked((int)length)];
                    var copied = Copy.MemoryCopy(sector.Slice(0, scratch.Length), scratch);
                    if (!copied.Succeeded) { return false; }
                    Endian.WriteUInt32LittleEndian(scratch.Slice(16, 4), 0);
                    crc = Hash.Compute(scratch);
                    return true;
                }
                private static GptPartitionTableParseResult Parse(ReadOnlySpan<byte> sector, uint length)
                {
                    if (!TryComputeHeaderCrc32(sector, length, out uint actual))
                        return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.HeaderSectorTooSmall);
                    return default;
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References
            .Append(MetadataReference.CreateFromFile(typeof(OS.Storage.GptPartitionTableParseResult).Assembly.Location));
        bool Check(string candidate, bool move = false)
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp14);
            var tree = CSharpSyntaxTree.ParseText(candidate, options, "renamed-parser.cs");
            List<CSharpSourceDocument> documents = [];
            if (move)
            {
                var root = (CompilationUnitSyntax)tree.GetRoot();
                var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Single(declaration => declaration.Identifier.ValueText == "TryComputeHeaderCrc32");
                var moved = root.ReplaceNode(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(),
                    root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method)));
                documents.Add(new("moved-copy.cs", "synthetic", CSharpSyntaxTree.ParseText(moved.ToFullString(), options, "moved-copy.cs")));
                tree = CSharpSyntaxTree.ParseText(root.RemoveNode(method, SyntaxRemoveOptions.KeepExteriorTrivia)!.ToFullString(), options, "renamed-parser.cs");
            }
            documents.Add(new("renamed-parser.cs", "synthetic", tree));
            return StorageGptHeaderSourceGuard.Check(new CSharpSourceCatalog(documents, references), "HeaderControl.Candidate");
        }
        HostCheck.True(Check(source));
        HostCheck.True(Check(source, move: true), "the copy helper can move to a separate partial file");
        HostCheck.True(Check(source.Replace("scratch", "bytes", StringComparison.Ordinal)
            .Replace("copied", "result", StringComparison.Ordinal).Replace("{ return false; }", "return false;", StringComparison.Ordinal)),
            "local names and guard brace formatting do not define the contract");
        foreach ((string before, string after) in new[]
        {
            ("checked((int)length)", "unchecked((int)length)"),
            ("checked((int)length)", "checked((short)length)"),
            ("sector.Slice(0, scratch.Length)", "sector.Slice(1, scratch.Length)"),
            ("sector.Slice(0, scratch.Length)", "sector.Slice(0, scratch.Length - 1)"),
            ("!copied.Succeeded", "copied.Succeeded"),
            ("{ return false; }", "{ return true; }"),
            ("{ return false; }", "{ if (false) return false; }"),
            ("scratch.Slice(16, 4)", "scratch.Slice(0, 4)"),
            ("scratch.Slice(16, 4)", "scratch.Slice(16, 8)"),
            ("scratch.Slice(16, 4), 0", "scratch.Slice(16, 4), 1"),
            ("Hash.Compute(scratch)", "Hash.Compute(sector)"),
            ("crc = Hash.Compute(scratch)", "crc = 0"),
            ("HeaderSectorTooSmall", "HeaderCrcMismatch"),
            ("if (!TryComputeHeaderCrc32", "if (TryComputeHeaderCrc32")
        })
            HostCheck.True(!Check(source.Replace(before, after, StringComparison.Ordinal)), before);
        const string foreign = """
            internal static class ForeignCopy
            {
                internal static OS.Std.Memory.BufferOperationResult MemoryCopy(ReadOnlySpan<byte> source, Span<byte> target) => default;
            }
            """;
        HostCheck.True(!Check(source.Replace("using Copy = OS.Std.Memory.BufferOperations;", "using Copy = HeaderControl.ForeignCopy;", StringComparison.Ordinal)
            + "\n" + foreign), "a same-named foreign helper cannot satisfy the shared-copy dependency");
        HostCheck.True(!Check(source.Replace("if (!TryComputeHeaderCrc32", "if (false) { if (!TryComputeHeaderCrc32", StringComparison.Ordinal)
            .Replace("HeaderSectorTooSmall);", "HeaderSectorTooSmall); }", StringComparison.Ordinal)),
            "a dead nested failure route cannot satisfy the parser contract");
    }
}
