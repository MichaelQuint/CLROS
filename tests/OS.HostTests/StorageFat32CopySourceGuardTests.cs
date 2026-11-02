using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Storage;
using OS.Tooling;

internal static class StorageFat32CopySourceGuardTests
{
    [HostTest("storage FAT final-copy guard binds scratch and destination range")]
    private static void FinalCopyBoundsAndFailure()
    {
        const string source = """
            using System;
            using OS.Storage;
            using Copier = OS.Std.Memory.BufferOperations;
            namespace FinalCopyControl;
            internal static class Candidate
            {
                internal static Fat32FileReadResult Copy(IReadOnlyBlockDevice device, Fat32Bpb bpb,
                    Fat32DirectoryEntry entry, Span<byte> destination, ulong lba, int length, int offset, bool contiguous)
                {
                    int remaining = length;
                    Span<byte> scratch = stackalloc byte[512];
                    device.ReadBlocks(lba, 1, scratch);
                    var copied = Copier.MemoryCopy(scratch[..remaining], destination.Slice(offset, remaining));
                    if (!copied.Succeeded)
                        return Fat32FileReadResult.Fail(Fat32Status.FileDestinationTooSmall, BlockDeviceStatus.Success,
                            bpb, entry, (uint)offset, contiguous);
                    return Fat32FileReadResult.Success(bpb, entry, (uint)(offset + remaining), contiguous);
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References
            .Append(MetadataReference.CreateFromFile(typeof(Fat32Parser).Assembly.Location));
        bool Check(string candidate)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("relocated-final-copy.cs", "synthetic", syntax)], references);
            return StorageFat32CopySourceGuard.Check(catalog, "FinalCopyControl.Candidate", "Copy");
        }
        HostCheck.True(Check(source), "renamed scratch and offset locals preserve the shared final-block copy");
        HostCheck.True(!Check(source.Replace("destination.Slice(offset, remaining)", "destination.Slice(0, remaining)", StringComparison.Ordinal)),
            "the copy starts at the caller's advancing destination offset");
        HostCheck.True(!Check(source.Replace("destination.Slice(offset, remaining)", "destination.Slice(offset, length)", StringComparison.Ordinal)),
            "source and destination use the same remaining count");
        HostCheck.True(!Check(source.Replace("scratch[..remaining]", "destination[..remaining]", StringComparison.Ordinal)),
            "the copied bytes come from block scratch rather than the destination itself");
        HostCheck.True(!Check(source.Replace("ReadBlocks(lba, 1, scratch)", "ReadBlocks(lba, 1, destination)", StringComparison.Ordinal)),
            "the final block read populates the actual copied scratch");
        HostCheck.True(!Check(source.Replace("ReadBlocks(lba, 1, scratch)", "ReadBlocks(lba, 2, scratch)", StringComparison.Ordinal)),
            "the scratch read remains one block");
        HostCheck.True(!Check(source.Replace("Fat32Status.FileDestinationTooSmall", "Fat32Status.Success", StringComparison.Ordinal)),
            "copy failure keeps the exact file destination status");
        HostCheck.True(!Check(source.Replace("var copied =", "scratch[..remaining].CopyTo(destination.Slice(offset, remaining)); var copied =", StringComparison.Ordinal)),
            "a shared-call decoy cannot conceal the forbidden CopyTo destination slice");
    }
}
