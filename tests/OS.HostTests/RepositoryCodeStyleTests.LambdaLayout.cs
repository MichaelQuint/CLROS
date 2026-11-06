using OS.Tooling;

internal static partial class RepositoryCodeStyleTests
{
    [HostTest("source layout rejects lambda braces escaping their containing statement")]
    private static void LambdaBraceOutdents()
    {
        foreach (var (open, close, expected) in new[] { (0, 0, 2), (0, 8, 1), (8, 0, 1) })
        {
            string source = "class Owner\n{\n    void Run()\n    {\n        Use(x =>\n" +
                new string(' ', open) + "{\n            First();\n" + new string(' ', close) + "});\n    }\n}";
            var findings = RepositoryCodeStyle.CheckFile("Owner.cs", source);
            HostCheck.True(findings.All(finding => finding.Rule != "CLROSSTYLE000"));
            HostCheck.Equal(expected, findings.Count(finding => finding.Rule == "CLROSSTYLE005"));
        }
    }

    [HostTest("source layout retains valid lambda continuation and field styles")]
    private static void LambdaContinuationStyles()
    {
        foreach (int indentation in new[] { 8, 12 })
        {
            string source = "class Owner\n{\n    void Run()\n    {\n        Use(\n            x =>\n" +
                new string(' ', indentation) + "{\n" + new string(' ', indentation + 4) +
                "First();\n" + new string(' ', indentation) + "});\n    }\n}";
            HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", source).Count);
        }

        const string field = "class Owner\n{\n    System.Action callback = () =>\n    {\n        First();\n    };\n}";
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", field).Count);
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Program.cs", "Use(x =>\n{\n    First();\n});\n").Count);
        const string tabs = "class Owner\n{\n\tvoid Run()\n\t{\n\t \tUse(x =>\n        {\n            First();\n        });\n\t}\n}";
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", tabs)
            .Count(finding => finding.Rule == "CLROSSTYLE005"));
        HostCheck.Equal(2, RepositoryCodeStyle.CheckFile("Owner.cs", tabs.Replace("        {", "    {").Replace("        });", "    });"))
            .Count(finding => finding.Rule == "CLROSSTYLE005"));
    }

    [HostTest("source layout covers nested and conditional lambda bodies")]
    private static void NestedLambdaContainment()
    {
        const string nested = """
            class Owner
            {
                void Run()
                {
                    Use(x =>
                    {
                        Use(y =>
            {
                            First();
            });
                    });
                }
            }
            """;
        HostCheck.Equal(2, RepositoryCodeStyle.CheckFile("Owner.cs", nested)
            .Count(finding => finding.Rule == "CLROSSTYLE005"));
        const string conditional = "class Owner\n{\n    void Run()\n    {\n#if HIDDEN\n        Use(x =>\n{\n            First();\n});\n#endif\n    }\n}";
        HostCheck.Equal(2, RepositoryCodeStyle.CheckFile("Owner.cs", conditional)
            .Count(finding => finding.Rule == "CLROSSTYLE005"));
        const string loop = "class Owner\n{\n    void Run()\n    {\n        foreach (var item in items)\n            Use(x =>\n        {\n                First();\n        });\n    }\n}";
        HostCheck.Equal(2, RepositoryCodeStyle.CheckFile("Owner.cs", loop)
            .Count(finding => finding.Rule == "CLROSSTYLE005"));
    }

    [HostTest("source layout ignores lambda text in literals comments and generated output")]
    private static void LambdaLiteralContents()
    {
        const string source = "class Owner\n{\n    string text = @\"Use(x =>\n{\nFirst();\n});\";\n    // Use(x => { First(); });\n}";
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", source).Count);
        const string raw = "class Owner\n{\n    string text = \"\"\"\nUse(x =>\n{\nFirst();\n});\n\"\"\";\n/* Use(x =>\n{\nFirst();\n}); */\n}";
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", raw).Count);
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.g.cs", "class Owner { void Run() { Use(x => {}); } }").Count);
    }
}
