using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Storage;
using OS.Tooling;

internal static class HostFormatterSourceGuardTests
{
    [HostTest("formatter guards preserve counter widths and successful offset flow across wrapper styles")]
    private static void CounterAndTryWrapperControls()
    {
        const string source = """
            using System;
            using Digits = OS.Std.Text.AsciiDecimalFormatter;
            using Builder = OS.Std.Text.AsciiTextBuilder;
            namespace MixedFormatter;
            class Input { public int A { get; } public uint B { get; } public long C { get; } public ulong D { get; } }
            static class Candidate
            {
                static int Size(Input value) => Digits.CountInt32Digits(value.A) + Digits.CountUInt32Digits(value.B)
                    + Digits.CountInt64Digits(value.C) + Digits.CountUInt64Digits(value.D);
                static bool Append(ulong value, Span<byte> destination, ref int offset)
                {
                    var appended = Builder.AppendUInt64Decimal(destination, offset, value);
                    if (!appended.Succeeded) return false;
                    offset = appended.NextOffset;
                    return true;
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        bool Check(string candidate)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("renamed-part.cs", "synthetic", syntax)], references);
            return HostFormatterSourceGuard.CountFields(catalog, "MixedFormatter.Candidate", "Size",
                    [("A", "CountInt32Digits"), ("B", "CountUInt32Digits"), ("C", "CountInt64Digits"), ("D", "CountUInt64Digits")])
                && HostFormatterSourceGuard.Append(catalog, "MixedFormatter.Candidate", "Append", "AppendUInt64Decimal", tryStyle: true);
        }
        HostCheck.True(Check(source));
        HostCheck.True(Check(source.Replace("{ get; }", ";", StringComparison.Ordinal)), "POD fields and properties both retain bound member identity");
        foreach ((string before, string after) in new[]
        {
            ("CountInt32Digits(value.A)", "CountInt64Digits(value.A)"),
            ("CountUInt32Digits(value.B)", "CountUInt64Digits(value.B)"),
            ("CountUInt64Digits(value.D)", "CountUInt64Digits(value.B)"),
            ("CountInt64Digits(value.C)", "CountInt64Digits((short)value.C)"),
            ("static int Size(Input value) =>", "static byte Size(Input value) => (byte)("),
            ("Digits.CountInt32Digits(value.A)", "0 * Digits.CountInt32Digits(value.A)"),
            ("if (!appended.Succeeded) return false", "if (!appended.Succeeded) return true"),
            ("if (!appended.Succeeded)", "if (appended.Succeeded)"),
            ("offset = appended.NextOffset", "offset = 0"),
            ("destination, offset, value", "destination, offset, (byte)value"),
            ("return true;", "return false;")
        })
        {
            string changed = source.Replace(before, after, StringComparison.Ordinal);
            if (after.StartsWith("static byte", StringComparison.Ordinal))
                changed = changed.Replace("CountUInt64Digits(value.D);", "CountUInt64Digits(value.D));", StringComparison.Ordinal);
            HostCheck.True(!Check(changed), before);
        }
    }

    [HostTest("storage preflight formatter source guard binds fields and append results")]
    private static void FieldsAndAppendResults()
    {
        const string source = """
            using System;
            using OS.Storage;
            using Digits = OS.Std.Text.AsciiDecimalFormatter;
            using Builder = OS.Std.Text.AsciiTextBuilder;
            namespace FormatterControl;
            internal static class Candidate
            {
                private static int GetRequiredBytes(in VirtioBlockPackageFileStagingPreflightResult input)
                {
                    return Digits.CountUInt64Digits(input.ReadSessionByteCount)
                        + Digits.CountUInt64Digits(input.PackageBytesRead)
                        + Digits.CountUInt64Digits(input.PackageFileBytesStaged)
                        + Digits.CountUInt64Digits(input.StagingDestinationBytesAvailable)
                        + Digits.CountUInt64Digits(input.PackageStaging.FileLength)
                        + Digits.CountUInt64Digits(input.PackageStaging.RequiredBytes);
                }
                private static void AppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
                {
                    var append = Builder.AppendBytes(destination, offset, value);
                    if (append.Succeeded) offset = append.NextOffset;
                }
                private static void AppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
                {
                    var append = Builder.AppendUInt64Decimal(destination, offset, value);
                    if (append.Succeeded) offset = append.NextOffset;
                }
            }
            """;
        string[] fields = ["ReadSessionByteCount", "PackageBytesRead", "PackageFileBytesStaged", "StagingDestinationBytesAvailable",
            "PackageStaging.FileLength", "PackageStaging.RequiredBytes"];
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References
            .Append(MetadataReference.CreateFromFile(typeof(OsPackageParser).Assembly.Location));
        bool Check(string candidate)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("moved-preflight-formatter.cs", "synthetic", syntax)], references);
            return HostFormatterSourceGuard.Check(catalog, "FormatterControl.Candidate", fields);
        }
        HostCheck.True(Check(source), "aliases and renamed parameters retain bound sizing and append dependencies");
        HostCheck.True(!Check(source.Replace("input.PackageBytesRead", "input.ReadSessionByteCount", StringComparison.Ordinal)),
            "duplicating one field cannot conceal a missing numeric field");
        HostCheck.True(!Check(source.Replace("input.PackageStaging.RequiredBytes", "input.PackageStaging.FileLength", StringComparison.Ordinal)),
            "nested staging values remain separate sizing inputs");
        HostCheck.True(!Check(source.Replace("return Digits.CountUInt64Digits(input.ReadSessionByteCount)",
            "return 0 * Digits.CountUInt64Digits(input.ReadSessionByteCount)", StringComparison.Ordinal)),
            "a counted field must actually contribute to the returned sum");
        HostCheck.True(!Check(source.Replace("destination, offset, value", "destination, 0, value", StringComparison.Ordinal)),
            "the shared builder receives the actual current offset");
        HostCheck.True(!Check(source.Replace("offset = append.NextOffset", "offset++", StringComparison.Ordinal)),
            "success advances to the shared builder's returned offset");
        HostCheck.True(!Check(source.Replace("if (append.Succeeded)", "append = default; if (append.Succeeded)", StringComparison.Ordinal)),
            "reassignment cannot retain the append result identity");
        HostCheck.True(!Check(source.Replace("var append = Builder.AppendBytes", "value.CopyTo(destination[offset..]); var append = Builder.AppendBytes", StringComparison.Ordinal)),
            "the formatter retains its forbidden CopyTo boundary");
        HostCheck.True(!Check(source.Replace("var append = Builder.AppendUInt64Decimal", "value /= 10; var append = Builder.AppendUInt64Decimal", StringComparison.Ordinal)),
            "a shared-call decoy cannot retain manual decimal division");
        HostCheck.True(!Check(source.Replace("var append = Builder.AppendUInt64Decimal", "destination[offset] = (byte)'0'; var append = Builder.AppendUInt64Decimal", StringComparison.Ordinal)),
            "a shared-call decoy cannot retain manual output digits");
        HostCheck.True(!Check(source.Replace("private static int GetRequiredBytes(",
            "private static int GetUInt64DecimalByteCount(ulong value) => 1; private static int GetRequiredBytes(", StringComparison.Ordinal)),
            "the formatter does not regain a custom digit-count helper");
    }
}
