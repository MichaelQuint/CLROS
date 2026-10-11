using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OS.Std.NoRuntime;

internal static class DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests
{
    private const string ReadyMarkerPrefix =
        "CLROS_M04_001_READY declarations=";

    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        HostCheck.Run("m04.001", () =>
        {

            string repositoryRoot = HostToolProcess.FindRepositoryRoot();
            string minimalRuntimePath = Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "Runtime",
                "MinimalRuntime.cs");
            string ledgerPath = Path.Combine(
                repositoryRoot,
                "std",
                "no-runtime",
                "shared",
                "DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.cs");
            string minimalRuntimeSource = File.ReadAllText(minimalRuntimePath);
            string ledgerSource = File.ReadAllText(ledgerPath);

            List<string> declarations =
                ExtractCanonicalSystemDeclarations(minimalRuntimeSource);
            List<RuntimeExportSourceEntry> runtimeExports =
                ExtractRuntimeExports(minimalRuntimeSource);

            TestSystemDeclarationSourceBinding(declarations);
            TestSystemDeclarationClassifications();
            TestRuntimeExportSourceBinding(runtimeExports);
            TestRuntimeExportClassifications(runtimeExports);
            TestUnknownAndDefaultValuesFailClosed();
            TestEntriesAreReferenceFreeAndAllocationFreeByConstruction(ledgerSource);
            TestNoCanonicalSystemNamespaceSurface(ledgerSource);
            if (!ExtractCanonicalSystemDeclarations("namespace System\n{\npublic class Array {}\npublic class Array<T> {}\npublic struct Pair<T,U> {}\n}")
                    .SequenceEqual(new[] { "System.Array", "System.Array`1", "System.Pair`2" }, StringComparer.Ordinal))
                throw new InvalidOperationException("Canonical declaration arity must distinguish generic and nongeneric identities.");

            int linkOnlyNoOpCount = CountRuntimeExportBehavior(
                runtimeExports,
                DotNetN10RuntimeExportBehavior.LinkOnlyNoOp);
            int failFastCount = CountRuntimeExportBehavior(
                runtimeExports,
                DotNetN10RuntimeExportBehavior.FailFast);
            int auditedStackProbeCount = CountRuntimeExportBehavior(
                runtimeExports,
                DotNetN10RuntimeExportBehavior.AuditedStackProbe);
            bool gate4Pass = AnyEntrySatisfiesGate4();
            bool canonicalSystemNamespace = ContainsCanonicalSystemNamespace(
                ledgerSource);

            Console.WriteLine(
                ReadyMarkerPrefix + declarations.Count
                + " runtimeExports=" + runtimeExports.Count
                + " linkOnlyNoOp=" + linkOnlyNoOpCount
                + " failFast=" + failFastCount
                + " auditedStackProbe=" + auditedStackProbeCount
                + " gate4=" + (gate4Pass ? "Pass" : "Fail")
                + " canonicalSystemNamespace="
                + (canonicalSystemNamespace ? "true" : "false"));
        });
    }

    private static void TestSystemDeclarationSourceBinding(
        List<string> sourceDeclarations)
    {
        ExpectEqual(
            "source declaration count",
            DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .SystemDeclarationCount,
            sourceDeclarations.Count);

        HashSet<string> remaining = new HashSet<string>(
            sourceDeclarations,
            StringComparer.Ordinal);
        if (remaining.Count != sourceDeclarations.Count)
        {
            throw new InvalidOperationException(
                "Canonical System declaration extraction returned duplicates.");
        }

        for (int index = 0;
            index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .SystemDeclarationCount;
            index++)
        {
            if (!DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                    .TryGetSystemDeclaration(index, out var entry))
            {
                throw new InvalidOperationException(
                    "Ledger rejected declaration index " + index + ".");
            }

            ExpectEqual(
                "declaration append-only identity",
                index + 1,
                (int)entry.Declaration);
            string fullName = GetCanonicalSystemDeclarationName(entry.Declaration);
            if (!remaining.Remove(fullName))
            {
                throw new InvalidOperationException(
                    "Ledger declaration is absent or duplicated in MinimalRuntime.cs: "
                    + fullName + ".");
            }
        }

        if (remaining.Count != 0)
        {
            throw new InvalidOperationException(
                "MinimalRuntime.cs has unclassified canonical System declaration: "
                + First(remaining) + ".");
        }
    }

    private static void TestSystemDeclarationClassifications()
    {
        int narrowCount = 0;
        int stubbedCount = 0;
        int forbiddenCount = 0;

        for (int index = 0;
            index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .SystemDeclarationCount;
            index++)
        {
            DotNetN10SystemDeclarationCompatibilityEntry entry =
                DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                    .GetSystemDeclaration(index);
            if (!entry.IsInitialized ||
                entry.Category == DotNetN10SystemDeclarationCategory.Unknown ||
                entry.SupportState == DotNetN10HelperSupportState.Unknown)
            {
                throw new InvalidOperationException(
                    "System declaration entry is not initialized at index "
                    + index + ".");
            }

            if (entry.SatisfiesRuntimeCompletionGate4)
            {
                throw new InvalidOperationException(
                    "System declaration unexpectedly satisfies Gate 4: "
                    + entry.Declaration + ".");
            }

            switch (entry.SupportState)
            {
                case DotNetN10HelperSupportState.NarrowlyImplemented:
                    narrowCount++;
                    break;
                case DotNetN10HelperSupportState.Stubbed:
                    stubbedCount++;
                    break;
                case DotNetN10HelperSupportState.Forbidden:
                    forbiddenCount++;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown support state at index " + index + ".");
            }
        }

        ExpectEqual("narrow declaration count", 11, narrowCount);
        ExpectEqual("classified declaration count",
            DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount,
            narrowCount + stubbedCount + forbiddenCount);
        ExpectEqual("forbidden declaration count", 2, forbiddenCount);

        ExpectSystemClassification(
            DotNetN10SystemDeclaration.UnmanagedType,
            DotNetN10SystemDeclarationCategory.InteropContract,
            DotNetN10HelperSupportState.Stubbed);
        ExpectSystemClassification(
            DotNetN10SystemDeclaration.InAttribute,
            DotNetN10SystemDeclarationCategory.AttributeContract,
            DotNetN10HelperSupportState.Stubbed);
        ExpectSystemClassification(
            DotNetN10SystemDeclaration.Exception,
            DotNetN10SystemDeclarationCategory.CoreType,
            DotNetN10HelperSupportState.Stubbed);
        ExpectSystemClassification(
            DotNetN10SystemDeclaration.RuntimeFailFast,
            DotNetN10SystemDeclarationCategory.RuntimeControl,
            DotNetN10HelperSupportState.NarrowlyImplemented);
        ExpectSystemClassification(
            DotNetN10SystemDeclaration.Buffer,
            DotNetN10SystemDeclarationCategory.MemoryHelper,
            DotNetN10HelperSupportState.NarrowlyImplemented);
        ExpectSystemClassification(
            DotNetN10SystemDeclaration.TypeCast,
            DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure,
            DotNetN10HelperSupportState.Forbidden);
        ExpectSystemClassification(
            DotNetN10SystemDeclaration.IDynamicInterfaceCastable,
            DotNetN10SystemDeclarationCategory.InteropContract,
            DotNetN10HelperSupportState.Forbidden);
    }

    private static void TestRuntimeExportSourceBinding(
        List<RuntimeExportSourceEntry> sourceEntries)
    {
        ExpectEqual(
            "source RuntimeExport count",
            DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .RuntimeExportCount,
            sourceEntries.Count);
        ExpectEqual("audited RuntimeExport snapshot", 21, sourceEntries.Count);

        HashSet<string> symbols = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < sourceEntries.Count; index++)
        {
            RuntimeExportSourceEntry sourceEntry = sourceEntries[index];
            if (!symbols.Add(sourceEntry.Symbol))
            {
                throw new InvalidOperationException(
                    "Duplicate RuntimeExport symbol: " + sourceEntry.Symbol + ".");
            }

            if (!DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                    .TryGetRuntimeExport(index, out var ledgerEntry))
            {
                throw new InvalidOperationException(
                    "Ledger rejected RuntimeExport index " + index + ".");
            }

            ExpectEqual(
                "RuntimeExport append-only identity",
                index + 1,
                (int)ledgerEntry.RuntimeExport);
            ExpectEqual(
                "RuntimeExport source order",
                sourceEntry.Symbol,
                ledgerEntry.RuntimeExport.ToString());
            if (sourceEntry.Behavior != ledgerEntry.Behavior)
            {
                throw new InvalidOperationException(
                    "RuntimeExport behavior mismatch for " + sourceEntry.Symbol
                    + ": source=" + sourceEntry.Behavior
                    + " ledger=" + ledgerEntry.Behavior + ".");
            }
        }
    }

    private static void TestRuntimeExportClassifications(
        List<RuntimeExportSourceEntry> sourceEntries)
    {
        int linkOnlyNoOp = CountRuntimeExportBehavior(
            sourceEntries,
            DotNetN10RuntimeExportBehavior.LinkOnlyNoOp);
        int failFast = CountRuntimeExportBehavior(
            sourceEntries,
            DotNetN10RuntimeExportBehavior.FailFast);
        int auditedStackProbe = CountRuntimeExportBehavior(
            sourceEntries,
            DotNetN10RuntimeExportBehavior.AuditedStackProbe);
        ExpectEqual("link-only no-op RuntimeExports", 4, linkOnlyNoOp);
        ExpectEqual("fail-fast RuntimeExports", 12, failFast);
        ExpectEqual("audited stack-probe RuntimeExports", 1, auditedStackProbe);
        ExpectEqual("profile-bound fixed-allocation RuntimeExports", 1,
            CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation));
        ExpectEqual("profile-bound String RuntimeExports", 1,
            CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation));
        ExpectEqual("profile-bound SZ-array RuntimeExports", 2,
            CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation));

        for (int index = 0;
            index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .RuntimeExportCount;
            index++)
        {
            DotNetN10RuntimeExportCompatibilityEntry entry =
                DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                    .GetRuntimeExport(index);
            if (!entry.IsInitialized || entry.SatisfiesRuntimeCompletionGate4)
            {
                throw new InvalidOperationException(
                    "RuntimeExport entry is invalid or promoted Gate 4 at index "
                    + index + ".");
            }

            DotNetN10HelperSupportState expectedSupport = entry.Behavior switch
            {
                DotNetN10RuntimeExportBehavior.LinkOnlyNoOp =>
                    DotNetN10HelperSupportState.Stubbed,
                DotNetN10RuntimeExportBehavior.FailFast =>
                    DotNetN10HelperSupportState.Forbidden,
                DotNetN10RuntimeExportBehavior.AuditedStackProbe =>
                    DotNetN10HelperSupportState.NarrowlyImplemented,
                DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation or
                DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation or
                DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation =>
                    DotNetN10HelperSupportState.NarrowlyImplemented,
                _ => DotNetN10HelperSupportState.Unknown
            };
            if (entry.SupportState != expectedSupport)
            {
                throw new InvalidOperationException(
                    "RuntimeExport support mismatch for " + entry.RuntimeExport
                    + ": expected=" + expectedSupport
                    + " actual=" + entry.SupportState + ".");
            }
        }
    }

    private static void TestUnknownAndDefaultValuesFailClosed()
    {
        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .TryGetSystemDeclaration(-1, out var negativeDeclaration) ||
            negativeDeclaration.IsInitialized ||
            negativeDeclaration.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException(
                "Negative declaration index did not fail closed.");
        }

        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .TryGetSystemDeclaration(
                    DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                        .SystemDeclarationCount,
                    out var pastDeclaration) ||
            pastDeclaration.IsInitialized ||
            pastDeclaration.SupportState != DotNetN10HelperSupportState.Unknown)
        {
            throw new InvalidOperationException(
                "Past-end declaration index did not fail closed.");
        }

        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .TryGetRuntimeExport(-1, out var negativeExport) ||
            negativeExport.IsInitialized ||
            negativeExport.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException(
                "Negative RuntimeExport index did not fail closed.");
        }

        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .TryGetRuntimeExport(
                    DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                        .RuntimeExportCount,
                    out var pastExport) ||
            pastExport.IsInitialized ||
            pastExport.Behavior != DotNetN10RuntimeExportBehavior.Unknown)
        {
            throw new InvalidOperationException(
                "Past-end RuntimeExport index did not fail closed.");
        }

        var defaultDeclaration =
            default(DotNetN10SystemDeclarationCompatibilityEntry);
        var defaultExport = default(DotNetN10RuntimeExportCompatibilityEntry);
        if (defaultDeclaration.IsInitialized ||
            defaultDeclaration.SatisfiesRuntimeCompletionGate4 ||
            defaultExport.IsInitialized ||
            defaultExport.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException(
                "Default ledger entries did not fail closed.");
        }
    }

    private static void TestEntriesAreReferenceFreeAndAllocationFreeByConstruction(
        string ledgerSource)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<
                DotNetN10SystemDeclarationCompatibilityEntry>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<
                DotNetN10RuntimeExportCompatibilityEntry>())
        {
            throw new InvalidOperationException(
                "Ledger value entries contain managed references.");
        }

        string[] forbidden =
        [
            "static readonly",
            "new[]",
            "new []",
            "List<",
            "Dictionary<",
            "Enumerable.",
            "System.Reflection",
            "throw ",
            "namespace System"
        ];
        for (int index = 0; index < forbidden.Length; index++)
        {
            if (ledgerSource.Contains(forbidden[index], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Ledger source contains forbidden allocation/runtime surface: "
                    + forbidden[index] + ".");
            }
        }

        if (!ledgerSource.Contains(
                "return index switch",
                StringComparison.Ordinal) ||
            !ledgerSource.Contains(
                "public readonly struct DotNetN10SystemDeclarationCompatibilityEntry",
                StringComparison.Ordinal) ||
            !ledgerSource.Contains(
                "public readonly struct DotNetN10RuntimeExportCompatibilityEntry",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Ledger does not retain switch-indexed readonly value retrieval.");
        }
    }

    private static void TestNoCanonicalSystemNamespaceSurface(string ledgerSource)
    {
        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .AddsCanonicalSystemNamespace ||
            DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .SatisfiesRuntimeCompletionGate4 ||
            ContainsCanonicalSystemNamespace(ledgerSource) ||
            !ledgerSource.Contains(
                "namespace OS.Std.NoRuntime;",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Ledger added canonical System surface or promoted Gate 4.");
        }
    }

    private static bool AnyEntrySatisfiesGate4()
    {
        for (int index = 0;
            index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .SystemDeclarationCount;
            index++)
        {
            if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                    .GetSystemDeclaration(index)
                    .SatisfiesRuntimeCompletionGate4)
            {
                return true;
            }
        }

        for (int index = 0;
            index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .RuntimeExportCount;
            index++)
        {
            if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                    .GetRuntimeExport(index)
                    .SatisfiesRuntimeCompletionGate4)
            {
                return true;
            }
        }

        return false;
    }

    private static void ExpectSystemClassification(
        DotNetN10SystemDeclaration declaration,
        DotNetN10SystemDeclarationCategory expectedCategory,
        DotNetN10HelperSupportState expectedSupport)
    {
        int index = (int)declaration - 1;
        DotNetN10SystemDeclarationCompatibilityEntry entry =
            DotNetN10SystemExportCompatibilityLedgerImplementedStubbed
                .GetSystemDeclaration(index);
        if (entry.Declaration != declaration ||
            entry.Category != expectedCategory ||
            entry.SupportState != expectedSupport)
        {
            throw new InvalidOperationException(
                "Unexpected classification for " + declaration + ".");
        }
    }

    private static string GetCanonicalSystemDeclarationName(
        DotNetN10SystemDeclaration declaration)
    {
        if (declaration >= DotNetN10SystemDeclaration.RuntimeFailFast &&
            declaration <= DotNetN10SystemDeclaration.RuntimeFieldHandle)
        {
            return "System." + declaration;
        }

        if (declaration >= DotNetN10SystemDeclaration.RuntimeExportAttribute &&
            declaration <= DotNetN10SystemDeclaration.TypeCast)
        {
            return "System.Runtime." + declaration;
        }

        if (declaration >= DotNetN10SystemDeclaration.RuntimeFeature &&
            declaration <= DotNetN10SystemDeclaration.RuntimeHelpers)
        {
            return "System.Runtime.CompilerServices." + declaration;
        }

        if (declaration >=
                DotNetN10SystemDeclaration.UnmanagedCallersOnlyAttribute &&
            declaration <= DotNetN10SystemDeclaration.IDynamicInterfaceCastable)
        {
            return "System.Runtime.InteropServices." + declaration;
        }

        return declaration switch
        {
            DotNetN10SystemDeclaration.IntrinsicAttribute => "System.Runtime.CompilerServices.IntrinsicAttribute",
            DotNetN10SystemDeclaration.Unsafe => "System.Runtime.CompilerServices.Unsafe",
            DotNetN10SystemDeclaration.GenericArray => "System.Array`1",
            DotNetN10SystemDeclaration.Nullable => "System.Nullable`1",
            DotNetN10SystemDeclaration.RhpNewFastRuntime => "System.Runtime.RhpNewFastRuntime",
            DotNetN10SystemDeclaration.DefaultMemberAttribute => "System.Reflection.DefaultMemberAttribute",
            DotNetN10SystemDeclaration.IndexerNameAttribute => "System.Runtime.CompilerServices.IndexerNameAttribute",
            DotNetN10SystemDeclaration.RuntimeImports => "System.Runtime.RuntimeImports",
            DotNetN10SystemDeclaration.Exception => "System.Exception",
            DotNetN10SystemDeclaration.UnmanagedType => "System.Runtime.InteropServices.UnmanagedType",
            DotNetN10SystemDeclaration.InAttribute => "System.Runtime.InteropServices.InAttribute",
            _ => "invalid"
        };
    }

    private static List<string> ExtractCanonicalSystemDeclarations(string source)
    {
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        List<NamespaceFrame> namespaces = new List<NamespaceFrame>();
        List<string> declarations = new List<string>();
        string? pendingNamespace = null;
        int braceDepth = 0;
        Regex namespacePattern = new Regex(
            "^\\s*namespace\\s+([A-Za-z_][A-Za-z0-9_.]*)\\s*$",
            RegexOptions.CultureInvariant);
        Regex declarationPattern = new Regex(
            "^\\s*(?:public|internal|private)\\s+(?:(?:static|sealed|abstract|unsafe)\\s+)*(?:class|struct|interface|enum)\\s+([A-Za-z_][A-Za-z0-9_]*)(?:\\s*<([^>]+)>)?",
            RegexOptions.CultureInvariant);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = StripLineComment(lines[lineIndex]);
            Match namespaceMatch = namespacePattern.Match(line);
            if (namespaceMatch.Success)
            {
                pendingNamespace = namespaceMatch.Groups[1].Value;
            }

            int openCount = CountCharacter(line, '{');
            int closeCount = CountCharacter(line, '}');
            if (pendingNamespace != null && openCount > 0)
            {
                string parentNamespace = namespaces.Count == 0
                    ? string.Empty
                    : namespaces[^1].FullName;
                string fullNamespace = parentNamespace.Length == 0
                    ? pendingNamespace
                    : parentNamespace + "." + pendingNamespace;
                braceDepth += openCount - closeCount;
                namespaces.Add(new NamespaceFrame(fullNamespace, braceDepth));
                pendingNamespace = null;
                continue;
            }

            Match declarationMatch = declarationPattern.Match(line);
            if (declarationMatch.Success && namespaces.Count != 0)
            {
                string currentNamespace = namespaces[^1].FullName;
                if (currentNamespace.Equals("System", StringComparison.Ordinal) ||
                    currentNamespace.StartsWith("System.", StringComparison.Ordinal))
                {
                    string arity = declarationMatch.Groups[2].Success
                        ? "`" + declarationMatch.Groups[2].Value.Split(',').Length : "";
                    declarations.Add(currentNamespace + "." + declarationMatch.Groups[1].Value + arity);
                }
            }

            braceDepth += openCount - closeCount;
            while (namespaces.Count != 0 &&
                namespaces[^1].BraceDepth > braceDepth)
            {
                namespaces.RemoveAt(namespaces.Count - 1);
            }
        }

        return declarations;
    }

    private static List<RuntimeExportSourceEntry> ExtractRuntimeExports(
        string source)
    {
        const string Marker = "[RuntimeExport(\"";
        List<RuntimeExportSourceEntry> entries =
            new List<RuntimeExportSourceEntry>();
        int searchStart = 0;

        while (true)
        {
            int markerIndex = source.IndexOf(
                Marker,
                searchStart,
                StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                break;
            }

            int symbolStart = markerIndex + Marker.Length;
            int symbolEnd = source.IndexOf(
                "\")]",
                symbolStart,
                StringComparison.Ordinal);
            if (symbolEnd < symbolStart)
            {
                throw new InvalidOperationException(
                    "Malformed RuntimeExport near offset " + markerIndex + ".");
            }

            string symbol = source[symbolStart..symbolEnd];
            int bodyStart = source.IndexOf('{', symbolEnd);
            if (bodyStart < 0)
            {
                throw new InvalidOperationException(
                    "RuntimeExport has no method body: " + symbol + ".");
            }

            int bodyEnd = FindMatchingBrace(source, bodyStart);
            string body = source[(bodyStart + 1)..bodyEnd];
            entries.Add(new RuntimeExportSourceEntry(
                symbol,
                ClassifyRuntimeExportBody(symbol, body)));
            searchStart = bodyEnd + 1;
        }

        return entries;
    }

    private static DotNetN10RuntimeExportBehavior ClassifyRuntimeExportBody(
        string symbol,
        string body)
    {
        string compact = Regex.Replace(Regex.Replace(body, @"//[^\r\n]*", string.Empty), "\\s+", string.Empty);
        if (symbol.Equals("RhpNewFast", StringComparison.Ordinal))
        {
            const string exact = "#ifM04_RHP_NEW_FAST_PROBEnuintreference=(nuint)OS.Runtime.RhpNewFastAllocation.Allocate((ulong)methodTable);returnSystem.Runtime.CompilerServices.Unsafe.As<nuint,object>(refreference);#elsewhile(true)RuntimeFailFast.Halt();#endif";
            return compact.Equals(exact, StringComparison.Ordinal)
                ? DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation
                : DotNetN10RuntimeExportBehavior.Unknown;
        }
        if (symbol.Equals("RhpNewArrayFast", StringComparison.Ordinal) || symbol.Equals("RhpNewPtrArrayFast", StringComparison.Ordinal))
        {
            string width = symbol.Equals("RhpNewPtrArrayFast", StringComparison.Ordinal) ? "true" : "false";
            string exact = "#ifM04_SZ_ARRAY_ALLOCATION_PROBEnuintreference=(nuint)OS.Runtime.RhpNewFastAllocation.AllocateArray((ulong)methodTable,length," + width + ");returnSystem.Runtime.CompilerServices.Unsafe.As<nuint,object>(refreference);#elsewhile(true)RuntimeFailFast.Halt();#endif";
            return compact.Equals(exact, StringComparison.Ordinal)
                ? DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation : DotNetN10RuntimeExportBehavior.Unknown;
        }
        if (symbol.Equals("RhNewString", StringComparison.Ordinal))
        {
            const string exact = "#ifM04_STRING_ALLOCATION_PROBEnuintreference=(nuint)OS.Runtime.RhpNewFastAllocation.AllocateString((ulong)methodTable,(long)length);returnSystem.Runtime.CompilerServices.Unsafe.As<nuint,string>(refreference);#elsewhile(true)RuntimeFailFast.Halt();#endif";
            return compact.Equals(exact, StringComparison.Ordinal)
                ? DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation : DotNetN10RuntimeExportBehavior.Unknown;
        }
        if (compact.Length == 0)
        {
            return DotNetN10RuntimeExportBehavior.LinkOnlyNoOp;
        }

        if (compact.Contains("RuntimeFailFast.Halt();", StringComparison.Ordinal))
        {
            return DotNetN10RuntimeExportBehavior.FailFast;
        }

        if (symbol.Equals("RhpStackProbe", StringComparison.Ordinal) &&
            compact.Contains("s_probeEntry", StringComparison.Ordinal) &&
            compact.Contains("delegate*managed<void>", StringComparison.Ordinal))
        {
            return DotNetN10RuntimeExportBehavior.AuditedStackProbe;
        }

        return DotNetN10RuntimeExportBehavior.Unknown;
    }

    private static int CountRuntimeExportBehavior(
        List<RuntimeExportSourceEntry> entries,
        DotNetN10RuntimeExportBehavior behavior)
    {
        int count = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Behavior == behavior)
            {
                count++;
            }
        }

        return count;
    }

    private static int FindMatchingBrace(string source, int bodyStart)
    {
        int depth = 0;
        for (int index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        throw new InvalidOperationException(
            "Unterminated RuntimeExport body at offset " + bodyStart + ".");
    }

    private static bool ContainsCanonicalSystemNamespace(string source)
    {
        return Regex.IsMatch(
            source,
            "(?m)^\\s*namespace\\s+System(?:\\.|\\s|;|$)",
            RegexOptions.CultureInvariant);
    }

    private static string StripLineComment(string line)
    {
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment < 0 ? line : line[..comment];
    }

    private static int CountCharacter(string text, char value)
    {
        int count = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == value)
            {
                count++;
            }
        }

        return count;
    }

    private static string First(HashSet<string> values)
    {
        foreach (string value in values)
        {
            return value;
        }

        return "<none>";
    }

    private static void ExpectEqual(string scenario, int expected, int actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectEqual(
        string scenario,
        string expected,
        string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private readonly record struct NamespaceFrame(
        string FullName,
        int BraceDepth);

    private readonly record struct RuntimeExportSourceEntry(
        string Symbol,
        DotNetN10RuntimeExportBehavior Behavior);
}
