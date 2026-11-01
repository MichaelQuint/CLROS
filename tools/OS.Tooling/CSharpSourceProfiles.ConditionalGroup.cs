using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

public static partial class CSharpSourceProfiles
{
    private sealed class ConditionalGroup(ExpressionSyntax condition)
    {
        public List<ExpressionSyntax> Alternatives { get; } = [condition];
        public List<(ExpressionSyntax Expression, bool Expected)> Conditions { get; set; } = [(condition, true)];
        public List<(ExpressionSyntax Expression, bool Expected)[]> Paths { get; } = [];
        public bool HasElse { get; set; }
        public bool StructureChanged { get; set; }
    }
}
