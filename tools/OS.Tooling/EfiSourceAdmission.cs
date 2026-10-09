using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed record EfiSourceResult(bool Succeeded, string Reason, string Detail, string ActiveText);

/// <summary>Admission for no-stdlib compile inputs; this does not replace raw build provenance.</summary>
public static class EfiSourceAdmission
{
    private static readonly RegexOptions Options = RegexOptions.CultureInvariant;
    private static EfiSourceResult Fail(string name, string reason, string detail) => new(false, reason, name + ":" + detail, "");
    private static EfiSourceResult Pass(string text) => new(true, "ok", "", text);
    private static bool Match(string text, string pattern) => Regex.IsMatch(text, pattern, Options);

    public static EfiSourceResult ActiveSource(string sourceText, string sourceName)
    {
        StringBuilder active = new();
        bool insideManagedBranch = false;
        foreach (string line in Regex.Split(sourceText, "\r\n|\n|\r"))
        {
            Match conditional = Regex.Match(line, @"^\s*#if\s+(.+?)\s*$", Options);
            if (conditional.Success)
            {
                string condition = conditional.Groups[1].Value;
                if (condition != "!NOSTDLIB_EFI" || insideManagedBranch)
                    return Fail(sourceName, "ambiguous_preprocessor_condition", condition);
                insideManagedBranch = true;
                continue;
            }
            if (Match(line, @"^\s*#(?:elif|else)\b")) return Fail(sourceName, "ambiguous_preprocessor_condition", "alternate-branch");
            if (Match(line, @"^\s*#endif\s*$"))
            {
                if (!insideManagedBranch) return Fail(sourceName, "unbalanced_preprocessor_condition", "#endif");
                insideManagedBranch = false;
                continue;
            }
            if (Match(line, @"^\s*#if\b")) return Fail(sourceName, "ambiguous_preprocessor_condition", "malformed-if");
            if (Match(line, @"^\s*#")) return Fail(sourceName, "unsupported_preprocessor_directive", line.Trim());
            if (!insideManagedBranch) active.AppendLine(line);
        }
        return insideManagedBranch ? Fail(sourceName, "unbalanced_preprocessor_condition", "missing-endif") : Pass(active.ToString());
    }

    // These two independent test pins are not special source-surface admissions.
    private static bool HasSpecialAdmission(string name) =>
        name != @"..\..\std\no-runtime\shared\Numerics\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs" &&
        name != @"..\..\std\no-runtime\shared\Memory\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs" &&
        EfiContracts.SourceIdentities.ContainsKey(name);

    public static EfiSourceResult Inspect(string sourceText, string sourceName)
    {
        if (HasSpecialAdmission(sourceName))
            return ContractAudit.HashSource(sourceText) == EfiContracts.SourceIdentities[sourceName]
                ? Pass(sourceText) : Fail(sourceName, "forbidden_active_source", EfiContracts.SourceAdmissionReasons[sourceName]);
        var active = ActiveSource(sourceText, sourceName);
        if (!active.Succeeded) return active;
        string code = Regex.Replace(active.ActiveText, @"/\*.*?\*/", "", Options | RegexOptions.Singleline);
        code = Regex.Replace(code, @"//.*$", "", Options | RegexOptions.Multiline);
        foreach (var (reason, pattern) in ForbiddenPatterns)
            if (Match(code, pattern)) return Fail(sourceName, "forbidden_active_source", reason);
        foreach (Match constant in Regex.Matches(code, @"\b[A-Za-z_][A-Za-z0-9_.<>]*\s*\.\s*(?:MaxValue|MinValue|Empty)\b", Options))
            if (Regex.Replace(constant.Value, @"\s+", "") != "ulong.MaxValue")
                return Fail(sourceName, "forbidden_active_source", "unavailable_bcl_constant");
        foreach (Match declaration in Regex.Matches(code, @"(?<![@A-Za-z0-9_])class\b", Options))
        {
            int boundary = -1;
            if (declaration.Index > 0)
                foreach (char delimiter in "\r\n;{}") boundary = Math.Max(boundary, code.LastIndexOf(delimiter, declaration.Index - 1));
            string[] modifiers = code[(boundary + 1)..declaration.Index].Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (!modifiers.Contains("static", StringComparer.Ordinal) || modifiers.Any(m => !ClassModifiers.Contains(m)))
                return Fail(sourceName, "forbidden_active_source", "non_static_class");
        }
        return Pass(code);
    }

    private static readonly HashSet<string> ClassModifiers = new(
        ["public", "internal", "private", "protected", "file", "new", "unsafe", "sealed", "abstract", "partial", "static"], StringComparer.Ordinal);
    private static readonly (string Reason, string Pattern)[] ForbiddenPatterns =
    [
        ("unicode_identifier_escape", @"\\(?:u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})"),
        ("verbatim_identifier", @"@[A-Za-z_]"),
        ("span_api", @"\b(?:ReadOnly)?Span\s*<"),
        ("flags_attribute", @"System\.FlagsAttribute|\[\s*(?:System\.)?Flags\s*\]"),
        ("system_api", @"\bSystem\s*\."),
        ("system_namespace", @"\bnamespace\s+(?:global\s*::\s*)?System\b"),
        ("using_directive", @"\busing\b"),
        ("managed_string", "\\b(?:string|String)\\b|\"|\\bnameof\\s*\\("),
        ("managed_object", @"\b(?:object|Object)\b"),
        ("managed_array", @"\[\s*(?:,\s*)*\]|\bnew\s+[A-Za-z_][A-Za-z0-9_.<>]*\s*\["),
        ("managed_root_reference", @"\b(?:Array|Type)\b"),
        ("managed_record_type", @"\brecord\s+(?:(?:class|struct)\s+)?[A-Za-z_][A-Za-z0-9_]*\s*(?:[<(:;{])"),
        ("generic_type_declaration", @"\b(?:class|struct|interface|record)\s+[A-Za-z_][A-Za-z0-9_]*\s*<"),
        ("generic_method_shape", @"\b[A-Za-z_][A-Za-z0-9_]*\s*<\s*(?:[A-Za-z_][A-Za-z0-9_]*\s*,\s*)*[A-Za-z_][A-Za-z0-9_]*\s*>\s*\("),
        ("generic_constraint", @"\bwhere\s+[A-Za-z_][A-Za-z0-9_]*\s*:"),
        ("managed_anonymous_object", @"\bnew\s*\{"),
        ("managed_type_shape", @"\b(?:interface|event|dynamic)\b"),
        ("source_attribute", @"(?m)(?:^|[;{}(,])\s*\[\s*[A-Za-z_]"),
        ("exception_control_flow", @"\b(?:Exception|throw|try|catch|finally)\b"),
        ("reflection_api", @"\b(?:typeof|GetType|Activator)\b|System\.Reflection"),
        ("managed_delegate", @"\bdelegate\b(?!\s*\*)|\b(?:Func|Action|Delegate|MulticastDelegate)\b"),
        ("managed_runtime_api", @"\b(?:Console|Task|Thread|GC|Marshal)\b")
    ];
}
