using OS.Tooling;

internal static partial class RepositoryCodeStyleTests
{
    [HostTest("source style rejects only universally inert conditional shells")]
    private static void InertConditionalGroups()
    {
        foreach (string source in new[]
        {
            "#if A\n#endif\nclass Owner {}",
            "#if A\n#if B\n#elif C\n#else\n#endif\n#else\n#endif\nclass Owner {}",
            "class Owner {\n#if A\n#endif\n}"
        })
            HostCheck.True(RepositoryCodeStyle.CheckFile("Owner.cs", source).Any(finding => finding.Rule == "CLROSSTYLE003"));

        foreach (string source in new[]
        {
            "#if A // retain the comment\n#endif\nclass Owner {}",
            "#if A\n// retain the comment\n#endif\nclass Owner {}",
            "#if A\nclass Owner {}\n#endif",
            "#if A\n#warning retain\n#endif\nclass Owner {}",
            "#if A\n#error retain\n#endif\nclass Owner {}",
            "#if A\n#nullable disable\n#endif\nclass Owner {}",
            "#if A\n#define B\n#endif\nclass Owner {}",
            "#if A\n#undef B\n#endif\nclass Owner {}",
            "#if A\n#line 44\n#endif\nclass Owner {}",
            "#if A\n#pragma warning disable 649\n#endif\nclass Owner {}",
            "#if A\n#region retain\n#endregion\n#endif\nclass Owner {}",
            "class Owner { string value = \"\"\"\n#if A\n#endif\n\"\"\"; }",
            "#if A\nclass Owner { string value = \"\"\"\n#if B\n#endif\n\"\"\"; }\n#endif"
        })
            HostCheck.True(RepositoryCodeStyle.CheckFile("Owner.cs", source).All(finding => finding.Rule != "CLROSSTYLE003"));

        HostCheck.True(RepositoryCodeStyle.CheckFile("Generated.g.cs", "#if A\n#endif").Count == 0);
        HostCheck.True(RepositoryCodeStyle.CheckFile("Owner.cs", "#if A B\n#endif\nclass Owner {}")
            .Any(finding => finding.Rule == "CLROSSTYLE000"), "malformed source stays rejected");
    }
}
