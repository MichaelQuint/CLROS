using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using OS.Tooling;
using OS.Std.NoRuntime;

internal static partial class DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests
{
    private const string ReadyMarkerPrefix = "CLROS_M04_001_READY declarations=";
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        HostCheck.Run("m04.001", () =>
        {
            var ordinary = HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>()));
            var strings = HostRuntimeAllocationBoundary.AllocationProfile("EnableM04SzArrayAllocationProbe", "EnableM04StringAllocationProbe");
            var exceptions = HostRuntimeAllocationBoundary.AllocationProfile("EnableM04GcStaticRootMaterializationProbe", "EnableM04SimpleThrowCatchProbe");
            var declarations = new[] { ordinary, strings, exceptions }.SelectMany(CanonicalDeclarations).Distinct(StringComparer.Ordinal).ToList();
            var runtimeExports = ExtractRuntimeExports(ordinary);
            var ledger = HostSourceCatalog.Project("src/OS.Std.NoRuntime.Shared/OS.Std.NoRuntime.Shared.csproj");
            TestSystemDeclarationSourceBinding(declarations);
            TestSystemDeclarationClassifications();
            TestRuntimeExportSourceBinding(runtimeExports);
            TestRuntimeExportClassifications(runtimeExports);
            TestUnknownAndDefaultValuesFailClosed();
            TestEntriesAreReferenceFreeAndAllocationFreeByConstruction(ledger);
            TestNoCanonicalSystemNamespaceSurface(ledger);
            int linkOnlyNoOpCount = CountRuntimeExportBehavior(runtimeExports, DotNetN10RuntimeExportBehavior.LinkOnlyNoOp);
            int failFastCount = CountRuntimeExportBehavior(runtimeExports, DotNetN10RuntimeExportBehavior.FailFast);
            int auditedStackProbeCount = CountRuntimeExportBehavior(runtimeExports, DotNetN10RuntimeExportBehavior.AuditedStackProbe);
            bool gate4Pass = AnyEntrySatisfiesGate4();
            bool canonicalSystemNamespace = ContainsCanonicalSystemNamespace(ledger);
            Console.WriteLine(ReadyMarkerPrefix + declarations.Count + " runtimeExports=" + runtimeExports.Count + " linkOnlyNoOp=" + linkOnlyNoOpCount + " failFast=" + failFastCount + " auditedStackProbe=" + auditedStackProbeCount + " gate4=" + (gate4Pass ? "Pass" : "Fail") + " canonicalSystemNamespace=" + (canonicalSystemNamespace ? "true" : "false"));
        });
    }

    private static void TestSystemDeclarationSourceBinding(List<string> sourceDeclarations)
    {
        ExpectEqual("source declaration count", DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount, sourceDeclarations.Count);
        HashSet<string> remaining = new HashSet<string>(sourceDeclarations, StringComparer.Ordinal);
        if (remaining.Count != sourceDeclarations.Count)
        {
            throw new InvalidOperationException("Canonical System declaration extraction returned duplicates.");
        }

        for (int index = 0; index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount; index++)
        {
            if (!DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.TryGetSystemDeclaration(index, out var entry))
            {
                throw new InvalidOperationException("Ledger rejected declaration index " + index + ".");
            }

            ExpectEqual("declaration append-only identity", index + 1, (int)entry.Declaration);
            string fullName = GetCanonicalSystemDeclarationName(entry.Declaration);
            if (!remaining.Remove(fullName))
            {
                throw new InvalidOperationException("Ledger declaration is absent or duplicated in the admitted runtime source family: " + fullName + ".");
            }
        }

        if (remaining.Count != 0)
        {
            throw new InvalidOperationException("the admitted runtime source family has unclassified canonical System declaration: " + First(remaining) + ".");
        }
    }

    private static void TestSystemDeclarationClassifications()
    {
        int narrowCount = 0;
        int stubbedCount = 0;
        int forbiddenCount = 0;
        for (int index = 0; index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount; index++)
        {
            DotNetN10SystemDeclarationCompatibilityEntry entry = DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.GetSystemDeclaration(index);
            if (!entry.IsInitialized || entry.Category == DotNetN10SystemDeclarationCategory.Unknown || entry.SupportState == DotNetN10HelperSupportState.Unknown)
            {
                throw new InvalidOperationException("System declaration entry is not initialized at index " + index + ".");
            }

            if (entry.SatisfiesRuntimeCompletionGate4)
            {
                throw new InvalidOperationException("System declaration unexpectedly satisfies Gate 4: " + entry.Declaration + ".");
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
                    throw new InvalidOperationException("Unknown support state at index " + index + ".");
            }
        }

        ExpectEqual("narrow declaration count", 11, narrowCount);
        ExpectEqual("classified declaration count", DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount, narrowCount + stubbedCount + forbiddenCount);
        ExpectEqual("forbidden declaration count", 2, forbiddenCount);
        ExpectSystemClassification(DotNetN10SystemDeclaration.UnmanagedType, DotNetN10SystemDeclarationCategory.InteropContract, DotNetN10HelperSupportState.Stubbed);
        ExpectSystemClassification(DotNetN10SystemDeclaration.InAttribute, DotNetN10SystemDeclarationCategory.AttributeContract, DotNetN10HelperSupportState.Stubbed);
        ExpectSystemClassification(DotNetN10SystemDeclaration.Exception, DotNetN10SystemDeclarationCategory.CoreType, DotNetN10HelperSupportState.Stubbed);
        ExpectSystemClassification(DotNetN10SystemDeclaration.RuntimeFailFast, DotNetN10SystemDeclarationCategory.RuntimeControl, DotNetN10HelperSupportState.NarrowlyImplemented);
        ExpectSystemClassification(DotNetN10SystemDeclaration.Buffer, DotNetN10SystemDeclarationCategory.MemoryHelper, DotNetN10HelperSupportState.NarrowlyImplemented);
        ExpectSystemClassification(DotNetN10SystemDeclaration.TypeCast, DotNetN10SystemDeclarationCategory.RuntimeExportInfrastructure, DotNetN10HelperSupportState.Forbidden);
        ExpectSystemClassification(DotNetN10SystemDeclaration.IDynamicInterfaceCastable, DotNetN10SystemDeclarationCategory.InteropContract, DotNetN10HelperSupportState.Forbidden);
    }

    private static void TestRuntimeExportSourceBinding(List<RuntimeExportSourceEntry> sourceEntries)
    {
        ExpectEqual("source RuntimeExport count", DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.RuntimeExportCount, sourceEntries.Count);
        ExpectEqual("audited RuntimeExport snapshot", 21, sourceEntries.Count);
        var remaining = sourceEntries.ToDictionary(entry => entry.Symbol, StringComparer.Ordinal);
        for (int index = 0; index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.RuntimeExportCount; index++)
        {
            if (!DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.TryGetRuntimeExport(index, out var ledgerEntry))
                throw new InvalidOperationException("Ledger rejected RuntimeExport index " + index + ".");
            ExpectEqual("RuntimeExport append-only identity", index + 1, (int)ledgerEntry.RuntimeExport);
            string name = ledgerEntry.RuntimeExport.ToString();
            if (!remaining.Remove(name, out var sourceEntry) || sourceEntry.Behavior != ledgerEntry.Behavior)
                throw new InvalidOperationException("RuntimeExport missing or behavior mismatch: " + name);
        }
        if (remaining.Count != 0)
            throw new InvalidOperationException("Unclassified RuntimeExport");
    }

    private static void TestRuntimeExportClassifications(List<RuntimeExportSourceEntry> sourceEntries)
    {
        int linkOnlyNoOp = CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.LinkOnlyNoOp);
        int failFast = CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.FailFast);
        int auditedStackProbe = CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.AuditedStackProbe);
        ExpectEqual("link-only no-op RuntimeExports", 4, linkOnlyNoOp);
        ExpectEqual("fail-fast RuntimeExports", 12, failFast);
        ExpectEqual("audited stack-probe RuntimeExports", 1, auditedStackProbe);
        ExpectEqual("profile-bound fixed-allocation RuntimeExports", 1, CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation));
        ExpectEqual("profile-bound String RuntimeExports", 1, CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation));
        ExpectEqual("profile-bound SZ-array RuntimeExports", 2, CountRuntimeExportBehavior(sourceEntries, DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation));
        for (int index = 0; index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.RuntimeExportCount; index++)
        {
            DotNetN10RuntimeExportCompatibilityEntry entry = DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.GetRuntimeExport(index);
            if (!entry.IsInitialized || entry.SatisfiesRuntimeCompletionGate4)
            {
                throw new InvalidOperationException("RuntimeExport entry is invalid or promoted Gate 4 at index " + index + ".");
            }

            DotNetN10HelperSupportState expectedSupport = entry.Behavior switch
            {
                DotNetN10RuntimeExportBehavior.LinkOnlyNoOp => DotNetN10HelperSupportState.Stubbed,
                DotNetN10RuntimeExportBehavior.FailFast => DotNetN10HelperSupportState.Forbidden,
                DotNetN10RuntimeExportBehavior.AuditedStackProbe => DotNetN10HelperSupportState.NarrowlyImplemented,
                DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation or DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation or DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation => DotNetN10HelperSupportState.NarrowlyImplemented,
                _ => DotNetN10HelperSupportState.Unknown
            };
            if (entry.SupportState != expectedSupport)
            {
                throw new InvalidOperationException("RuntimeExport support mismatch for " + entry.RuntimeExport + ": expected=" + expectedSupport + " actual=" + entry.SupportState + ".");
            }
        }
    }

    private static void TestUnknownAndDefaultValuesFailClosed()
    {
        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.TryGetSystemDeclaration(-1, out var negativeDeclaration) || negativeDeclaration.IsInitialized || negativeDeclaration.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException("Negative declaration index did not fail closed.");
        }

        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.TryGetSystemDeclaration(DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount, out var pastDeclaration) || pastDeclaration.IsInitialized || pastDeclaration.SupportState != DotNetN10HelperSupportState.Unknown)
        {
            throw new InvalidOperationException("Past-end declaration index did not fail closed.");
        }

        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.TryGetRuntimeExport(-1, out var negativeExport) || negativeExport.IsInitialized || negativeExport.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException("Negative RuntimeExport index did not fail closed.");
        }

        if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.TryGetRuntimeExport(DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.RuntimeExportCount, out var pastExport) || pastExport.IsInitialized || pastExport.Behavior != DotNetN10RuntimeExportBehavior.Unknown)
        {
            throw new InvalidOperationException("Past-end RuntimeExport index did not fail closed.");
        }

        var defaultDeclaration = default(DotNetN10SystemDeclarationCompatibilityEntry);
        var defaultExport = default(DotNetN10RuntimeExportCompatibilityEntry);
        if (defaultDeclaration.IsInitialized || defaultDeclaration.SatisfiesRuntimeCompletionGate4 || defaultExport.IsInitialized || defaultExport.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException("Default ledger entries did not fail closed.");
        }
    }

    private static bool AnyEntrySatisfiesGate4()
    {
        for (int index = 0; index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SystemDeclarationCount; index++)
        {
            if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.GetSystemDeclaration(index).SatisfiesRuntimeCompletionGate4)
            {
                return true;
            }
        }

        for (int index = 0; index < DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.RuntimeExportCount; index++)
        {
            if (DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.GetRuntimeExport(index).SatisfiesRuntimeCompletionGate4)
            {
                return true;
            }
        }

        return false;
    }

    private static void ExpectSystemClassification(DotNetN10SystemDeclaration declaration, DotNetN10SystemDeclarationCategory expectedCategory, DotNetN10HelperSupportState expectedSupport)
    {
        int index = (int)declaration - 1;
        DotNetN10SystemDeclarationCompatibilityEntry entry = DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.GetSystemDeclaration(index);
        if (entry.Declaration != declaration || entry.Category != expectedCategory || entry.SupportState != expectedSupport)
        {
            throw new InvalidOperationException("Unexpected classification for " + declaration + ".");
        }
    }

    private static string GetCanonicalSystemDeclarationName(DotNetN10SystemDeclaration declaration)
    {
        if (declaration >= DotNetN10SystemDeclaration.RuntimeFailFast && declaration <= DotNetN10SystemDeclaration.RuntimeFieldHandle)
        {
            return "System." + declaration;
        }

        if (declaration >= DotNetN10SystemDeclaration.RuntimeExportAttribute && declaration <= DotNetN10SystemDeclaration.TypeCast)
        {
            return "System.Runtime." + declaration;
        }

        if (declaration >= DotNetN10SystemDeclaration.RuntimeFeature && declaration <= DotNetN10SystemDeclaration.RuntimeHelpers)
        {
            return "System.Runtime.CompilerServices." + declaration;
        }

        if (declaration >= DotNetN10SystemDeclaration.UnmanagedCallersOnlyAttribute && declaration <= DotNetN10SystemDeclaration.IDynamicInterfaceCastable)
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

    private static int CountRuntimeExportBehavior(List<RuntimeExportSourceEntry> entries, DotNetN10RuntimeExportBehavior behavior)
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
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectEqual(string scenario, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }
}
