internal static class HostSourceMutationTests
{
    [HostTest("source mutation anchors syntax across formatting and explicit conditional profiles")]
    private static void SyntaxAnchors()
    {
        const string source = """
            class C {
            #if SELECTED
                bool Run(int value) {
                    // value > 0 is not the anchor
                    return value
                        > /* retained expression trivia */ 0;
                }
            #else
                bool Run(int value) => value < 0;
            #endif
            }
            """;
        string changed = HostSourceMutation.ReplaceExpression(source, "value > 0", "false", ["SELECTED"]);
        HostCheck.True(changed.Contains("return false;", StringComparison.Ordinal));
        HostCheck.True(changed.Contains("// value > 0 is not the anchor", StringComparison.Ordinal));
        HostCheck.True(changed.Contains("bool Run(int value) => value < 0;", StringComparison.Ordinal));
        string condition = HostSourceMutation.ReplaceExpression("class C { bool A(int x) { if (x>0) return x>0; return false; } }",
            "x > 0", "false", ifConditionOnly: true);
        HostCheck.True(condition.Contains("if (false) return x>0;", StringComparison.Ordinal));
        Reject(() => HostSourceMutation.ReplaceExpression(source, "value > 0", "false"));
        Reject(() => HostSourceMutation.ReplaceExpression("class C { bool A(int x)=>x>0; bool B(int x)=>x>0; }", "x > 0", "false"));
        Reject(() => HostSourceMutation.ReplaceExpression("class C { /* x > 0 */ }", "x > 0", "false"));
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("source_mutation_anchor_missing_or_ambiguous", StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException("expected source mutation anchor rejection");
    }
}
