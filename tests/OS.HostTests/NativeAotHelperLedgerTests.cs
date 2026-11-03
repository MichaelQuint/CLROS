using OS.Tooling;
using Microsoft.CodeAnalysis;
using OS.X64TextSerializerGenerator;
using System.Runtime.CompilerServices;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;

internal static class NativeAotHelperLedgerTests
{

    [HostTest("runtime.nativeaot helper ledger", HostLane.Fast, 0)]
    private static void TestLedger()
    {
        TestRequiredAreasPresent();
        TestIncompleteRuntimeHelpersAreNotComplete();
        TestEfiOnlyHelpersDoNotSatisfyRuntimeCompletion();
        TestEntriesCarryEvidenceAndGateApplicability();
        TestInvalidIndexesAreRejected();
    }

    private static void TestRequiredAreasPresent()
    {
        NativeAotHelperArea[] required =
        [
            NativeAotHelperArea.ObjectAllocation,
            NativeAotHelperArea.ArrayAllocation,
            NativeAotHelperArea.StringAllocation,
            NativeAotHelperArea.WriteBarriers,
            NativeAotHelperArea.InterfaceDispatch,
            NativeAotHelperArea.ClassConstructors,
            NativeAotHelperArea.ThreadStatics,
            NativeAotHelperArea.ExceptionUnwind,
            NativeAotHelperArea.ModuleInitialization,
            NativeAotHelperArea.TypeManagerRtrScans,
            NativeAotHelperArea.GcStackRoots,
            NativeAotHelperArea.EfiReversePInvoke,
            NativeAotHelperArea.EfiFailFastTypeCasts,
            NativeAotHelperArea.EfiStackProbe,
            NativeAotHelperArea.EfiFixedAllocation,
            NativeAotHelperArea.EfiSzArrayAllocation,
            NativeAotHelperArea.EfiStringAllocation,
            NativeAotHelperArea.EfiCheckedReferences
        ];

        ExpectEqual("ledger entry count", required.Length, NativeAotHelperLedger.EntryCount);

        foreach (NativeAotHelperArea area in required)
        {
            if (!ContainsArea(area))
            {
                throw new InvalidOperationException("Required helper area is missing: " + area);
            }
        }
    }

    private static void TestIncompleteRuntimeHelpersAreNotComplete()
    {
        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            NativeAotHelperLedgerEntry entry = NativeAotHelperLedger.GetEntry(i);
            if (entry.IsEfiOnly)
            {
                continue;
            }

            if (entry.IsComplete || entry.Status == NativeAotHelperStatus.Complete)
            {
                throw new InvalidOperationException("Incomplete runtime helper is marked complete: " + entry.Area);
            }

            if (entry.SatisfiesKernelRuntimeCompletion)
            {
                throw new InvalidOperationException("Incomplete runtime helper satisfies runtime completion: " + entry.Area);
            }
        }
    }

    private static void TestEfiOnlyHelpersDoNotSatisfyRuntimeCompletion()
    {
        int efiOnlyCount = 0;

        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            NativeAotHelperLedgerEntry entry = NativeAotHelperLedger.GetEntry(i);
            if (!entry.IsEfiOnly)
            {
                continue;
            }

            efiOnlyCount++;

            if (entry.SatisfiesKernelRuntimeCompletion)
            {
                throw new InvalidOperationException("EFI-only helper satisfies kernel/runtime completion: " + entry.Area);
            }

            if (entry.AppliesTo(NativeAotCompletionGate.StdRuntime) ||
                entry.AppliesTo(NativeAotCompletionGate.HostedDotNetKestrel) ||
                entry.AppliesTo(NativeAotCompletionGate.ExtendedHostedTier))
            {
                throw new InvalidOperationException("EFI-only helper applies to runtime completion gates: " + entry.Area);
            }

            if (!entry.AppliesTo(NativeAotCompletionGate.CleanBuild) ||
                !entry.AppliesTo(NativeAotCompletionGate.Boot))
            {
                throw new InvalidOperationException("EFI-only helper lost EFI build/boot applicability: " + entry.Area);
            }
        }

        ExpectEqual("EFI-only helper count", 7, efiOnlyCount);
    }

    private static void TestEntriesCarryEvidenceAndGateApplicability()
    {
        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            NativeAotHelperLedgerEntry entry = NativeAotHelperLedger.GetEntry(i);

            if (entry.Area == NativeAotHelperArea.Unknown)
            {
                throw new InvalidOperationException("Ledger entry has unknown area at index " + i);
            }

            if (entry.Status == NativeAotHelperStatus.Unknown)
            {
                throw new InvalidOperationException("Ledger entry has unknown status: " + entry.Area);
            }

            if (entry.CompletionGates == NativeAotCompletionGate.None)
            {
                throw new InvalidOperationException("Ledger entry has no completion gate applicability: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.Evidence))
            {
                throw new InvalidOperationException("Ledger entry has no evidence text: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.HelperSymbols))
            {
                throw new InvalidOperationException("Ledger entry has no helper symbol text: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.NextStep))
            {
                throw new InvalidOperationException("Ledger entry has no next-step text: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.RequiredContract))
            {
                throw new InvalidOperationException("Ledger entry has no required-contract text: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.AllowedMilestoneStub))
            {
                throw new InvalidOperationException("Ledger entry has no allowed-stub text: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.TestCoverage))
            {
                throw new InvalidOperationException("Ledger entry has no test-coverage text: " + entry.Area);
            }

            if (string.IsNullOrWhiteSpace(entry.OwnerSubsystem))
            {
                throw new InvalidOperationException("Ledger entry has no owner-subsystem text: " + entry.Area);
            }
        }
    }

    [HostTest("runtime.nativeaot helper ledger structured columns", HostLane.Fast, 1)]
    private static void TestStructuredColumns()
    {
        NativeAotHelperLedgerEntry objectAllocation = GetEntry(NativeAotHelperArea.ObjectAllocation);
        ExpectEqual("object allocation owner", "runtime-gc", objectAllocation.OwnerSubsystem);
        ExpectContains("object allocation contract", objectAllocation.RequiredContract, "GC registration");
        ExpectContains("object allocation stub", objectAllocation.AllowedMilestoneStub, "Fail-fast");
        ExpectContains("object allocation evidence", objectAllocation.Evidence, "BootPageAllocatorStateInitializer");
        ExpectContains("object allocation evidence", objectAllocation.Evidence, "BootPageAllocatorMutation");
        ExpectContains("object allocation evidence", objectAllocation.Evidence, "NativeAotHeapBootstrapPlanner");
        ExpectContains("object allocation coverage", objectAllocation.TestCoverage, "boot page allocator mutation tests");
        ExpectContains("object allocation coverage", objectAllocation.TestCoverage, "heap bootstrap plan tests");

        NativeAotHelperLedgerEntry exceptionUnwind = GetEntry(NativeAotHelperArea.ExceptionUnwind);
        ExpectEqual("exception unwind owner", "runtime-exceptions", exceptionUnwind.OwnerSubsystem);
        ExpectContains("exception unwind contract", exceptionUnwind.RequiredContract, "catch");
        ExpectContains("exception unwind stub", exceptionUnwind.AllowedMilestoneStub, "catch/finally");
        if (!exceptionUnwind.AppliesTo(NativeAotCompletionGate.FaultDiagnostics))
        {
            throw new InvalidOperationException("Exception/unwind entry must apply to fault diagnostics.");
        }

        NativeAotHelperLedgerEntry typeManager = GetEntry(NativeAotHelperArea.TypeManagerRtrScans);
        ExpectEqual("TypeManager owner", "runtime-metadata", typeManager.OwnerSubsystem);
        ExpectContains("TypeManager contract", typeManager.RequiredContract, "TypeManager/RTR metadata");
        if (!typeManager.AppliesTo(NativeAotCompletionGate.HostedDotNetKestrel))
        {
            throw new InvalidOperationException("TypeManager/RTR entry must apply to hosted .NET/Kestrel gate.");
        }

        NativeAotHelperLedgerEntry classConstructors = GetEntry(NativeAotHelperArea.ClassConstructors);
        ExpectEqual("class constructor owner", "runtime-initialization", classConstructors.OwnerSubsystem);
        ExpectContains("class constructor evidence", classConstructors.Evidence, "NativeAotClassConstructorRunnerPreflight");
        ExpectContains("class constructor coverage", classConstructors.TestCoverage, "preflight tests");
        ExpectContains("class constructor next step", classConstructors.NextStep, "runner invocation");

        NativeAotHelperLedgerEntry reversePInvoke = GetEntry(NativeAotHelperArea.EfiReversePInvoke);
        ExpectEqual("EFI reverse P/Invoke owner", "efi-runtime-shim", reversePInvoke.OwnerSubsystem);
        ExpectContains("EFI reverse P/Invoke stub", reversePInvoke.AllowedMilestoneStub, "EFI link-only");

        NativeAotHelperLedgerEntry failFastCasts = GetEntry(NativeAotHelperArea.EfiFailFastTypeCasts);
        ExpectEqual("EFI fail-fast cast owner", "efi-runtime-shim", failFastCasts.OwnerSubsystem);
        ExpectContains("EFI fail-fast cast stub", failFastCasts.AllowedMilestoneStub, "EFI fail-fast");

        NativeAotHelperLedgerEntry stackProbe = GetEntry(NativeAotHelperArea.EfiStackProbe);
        ExpectEqual("EFI stack-probe owner", "efi-runtime-shim", stackProbe.OwnerSubsystem);
        ExpectEqual("EFI stack-probe status", (int)NativeAotHelperStatus.EfiOnlyStackProbe, (int)stackProbe.Status);
        ExpectEqual("EFI stack-probe symbol", "RhpStackProbe", stackProbe.HelperSymbols);
        ExpectContains("EFI stack-probe evidence", stackProbe.Evidence, "27-byte");
        ExpectContains("EFI stack-probe evidence boundary", stackProbe.Evidence, "no guest execution");
        ExpectContains("EFI stack-probe contract", stackProbe.RequiredContract, "R11");
        ExpectContains("EFI stack-probe contract", stackProbe.RequiredContract, "downward");
        ExpectContains("EFI stack-probe contract", stackProbe.RequiredContract, "RSP");
        ExpectContains("EFI stack-probe allowed plan", stackProbe.AllowedMilestoneStub, "read-back-verified");
        ExpectContains("EFI stack-probe prohibited approximation", stackProbe.AllowedMilestoneStub, "no-op");
        ExpectContains("EFI stack-probe coverage", stackProbe.TestCoverage, "generated-object");
        ExpectContains("EFI stack-probe next step", stackProbe.NextStep, "M01-022");
    }

    [HostTest("runtime.nativeaot helper ledger append-only values", HostLane.Fast, 2)]
    private static void TestAppendOnlyValues()
    {
        ExpectEqual("EFI reverse P/Invoke area value", 12, (int)NativeAotHelperArea.EfiReversePInvoke);
        ExpectEqual("EFI fail-fast type-cast area value", 13, (int)NativeAotHelperArea.EfiFailFastTypeCasts);
        ExpectEqual("EFI stack-probe area value", 14, (int)NativeAotHelperArea.EfiStackProbe);
        ExpectEqual("complete helper status value", 4, (int)NativeAotHelperStatus.Complete);
        ExpectEqual("EFI stack-probe status value", 5, (int)NativeAotHelperStatus.EfiOnlyStackProbe);
        ExpectEqual("EFI fixed-allocation area value", 15, (int)NativeAotHelperArea.EfiFixedAllocation);
        ExpectEqual("EFI fixed-allocation status value", 6, (int)NativeAotHelperStatus.EfiOnlyFixedAllocation);
        NativeAotHelperLedgerEntry fixedAllocation = GetEntry(NativeAotHelperArea.EfiFixedAllocation);
        ExpectEqual("EFI fixed-allocation symbol", "RhpNewFast", fixedAllocation.HelperSymbols);
        ExpectContains("EFI fixed-allocation actual leaf", fixedAllocation.Evidence, "GuestTestRhpNewFast");
        ExpectContains("EFI fixed-allocation authority", fixedAllocation.RequiredContract, "thirteen-argument");
        ExpectContains("EFI fixed-allocation no fabricated reference", fixedAllocation.AllowedMilestoneStub, "never fabricates");
        ExpectContains("EFI fixed-allocation no general GC", fixedAllocation.NextStep, "Gate 4 remains incomplete");
        ExpectEqual("EFI SZ-array area value", 16, (int)NativeAotHelperArea.EfiSzArrayAllocation);
        ExpectEqual("EFI SZ-array status value", 7, (int)NativeAotHelperStatus.EfiOnlySzArrayAllocation);
        NativeAotHelperLedgerEntry arrays = GetEntry(NativeAotHelperArea.EfiSzArrayAllocation);
        ExpectEqual("EFI SZ-array symbols", "RhpNewArrayFast;RhpNewPtrArrayFast", arrays.HelperSymbols);
        ExpectContains("EFI SZ-array actual wrapper", arrays.Evidence, "GuestTestArrayAllocationLengthOverflowChecks");
        ExpectContains("EFI SZ-array shared state", arrays.Evidence, "same80-byte state");
        ExpectContains("EFI SZ-array width route", arrays.RequiredContract, "component width, not HasPointers");
        ExpectContains("EFI SZ-array unsupported references", arrays.AllowedMilestoneStub, "managed reference store");
        ExpectContains("EFI SZ-array incomplete", arrays.NextStep, "Gate 4 remains incomplete");
        ExpectEqual("EFI String area value", 17, (int)NativeAotHelperArea.EfiStringAllocation);
        ExpectEqual("EFI String status value", 8, (int)NativeAotHelperStatus.EfiOnlyStringAllocation);
        NativeAotHelperLedgerEntry strings = GetEntry(NativeAotHelperArea.EfiStringAllocation);
        ExpectEqual("EFI String symbol", "RhNewString", strings.HelperSymbols);
        ExpectContains("EFI String actual wrapper", strings.Evidence, "GuestTestStringAllocationCharacterStorage");
        ExpectContains("EFI String shared state", strings.Evidence, "same80-byte state");
        ExpectContains("EFI String full-width length", strings.RequiredContract, "full-width signed length");
        ExpectContains("EFI String unsupported GC", strings.AllowedMilestoneStub, "barrier, GC");
        ExpectContains("EFI String incomplete", strings.NextStep, "Gate 4 remains incomplete");
        ExpectEqual("EFI checked-reference area value", 18, (int)NativeAotHelperArea.EfiCheckedReferences);
        ExpectEqual("EFI checked-reference status value", 9, (int)NativeAotHelperStatus.EfiOnlyCheckedReferences);
        NativeAotHelperLedgerEntry references = GetEntry(NativeAotHelperArea.EfiCheckedReferences);
        ExpectEqual("EFI checked-reference symbols", "RhpAssignRef;RhpCheckedAssignRef;RhpByRefAssignRef;CLROS_CheckedReferenceBarrier_Apply", references.HelperSymbols);
        ExpectContains("EFI checked-reference authority", references.Evidence, "original thirteen-argument");
        ExpectContains("EFI checked-reference special ABI", references.RequiredContract, "byref+8 after success");
        ExpectContains("EFI checked-reference no general GC", references.AllowedMilestoneStub, "GC visibility");
        ExpectContains("EFI checked-reference incomplete", references.NextStep, "Gate 4 remains incomplete");
        ExpectDeferredRuntimeHelper(GetEntry(NativeAotHelperArea.WriteBarriers));
    }

    [HostTest("runtime.nativeaot helper ledger allocator mutation boundary", HostLane.Fast, 3)]
    private static void TestAllocatorMutationBoundary()
    {
        NativeAotHelperLedgerEntry objectAllocation = GetEntry(NativeAotHelperArea.ObjectAllocation);
        NativeAotHelperLedgerEntry arrayAllocation = GetEntry(NativeAotHelperArea.ArrayAllocation);
        NativeAotHelperLedgerEntry stringAllocation = GetEntry(NativeAotHelperArea.StringAllocation);

        ExpectDeferredRuntimeHelper(objectAllocation);
        ExpectDeferredRuntimeHelper(arrayAllocation);
        ExpectDeferredRuntimeHelper(stringAllocation);

        ExpectContains("object allocation evidence", objectAllocation.Evidence, "caller-owned free-list mutation");
        ExpectContains("object allocation required contract", objectAllocation.RequiredContract, "managed objects");
        ExpectContains("object allocation allowed stub", objectAllocation.AllowedMilestoneStub, "no helper may report allocation success");

        if (!BootPageAllocatorMutator.MutatesCallerOwnedFreeList)
        {
            throw new InvalidOperationException("Boot page allocator mutation boundary must mutate caller-owned free-list metadata.");
        }

        if (BootPageAllocatorMutator.PerformsLiveAllocation ||
            BootPageAllocatorMutator.ConfiguresPaging ||
            BootPageAllocatorMutator.EnforcesNxOrWx)
        {
            throw new InvalidOperationException("Boot page allocator mutation evidence crossed into live allocation or paging policy.");
        }
    }

    private static void TestInvalidIndexesAreRejected()
    {
        if (NativeAotHelperLedger.TryGetEntry(-1, out NativeAotHelperLedgerEntry negativeEntry))
        {
            throw new InvalidOperationException("Negative index unexpectedly returned " + negativeEntry.Area);
        }

        if (negativeEntry.Area != NativeAotHelperArea.Unknown ||
            negativeEntry.Status != NativeAotHelperStatus.Unknown)
        {
            throw new InvalidOperationException("Negative index did not return the invalid entry contract.");
        }

        if (NativeAotHelperLedger.TryGetEntry(NativeAotHelperLedger.EntryCount, out NativeAotHelperLedgerEntry pastEndEntry))
        {
            throw new InvalidOperationException("Past-end index unexpectedly returned " + pastEndEntry.Area);
        }

        if (pastEndEntry.Area != NativeAotHelperArea.Unknown ||
            pastEndEntry.Status != NativeAotHelperStatus.Unknown)
        {
            throw new InvalidOperationException("Past-end index did not return the invalid entry contract.");
        }
    }

    private static void ExpectDeferredRuntimeHelper(NativeAotHelperLedgerEntry entry)
    {
        if (entry.IsEfiOnly)
        {
            throw new InvalidOperationException("Runtime helper unexpectedly marked EFI-only: " + entry.Area);
        }

        if (entry.Status != NativeAotHelperStatus.Deferred || entry.IsComplete)
        {
            throw new InvalidOperationException("Runtime helper is not deferred: " + entry.Area);
        }

        if (entry.SatisfiesKernelRuntimeCompletion)
        {
            throw new InvalidOperationException("Runtime helper satisfies completion before implementation: " + entry.Area);
        }
    }

    [HostTest("runtime.nativeaot helper ledger symbols", HostLane.Fast, 4)]
    private static void TestLedgerSymbols()
    {
        string[] sourceSymbols = ExtractRuntimeExportSymbols();

        string[] ledgerExports = Enumerable.Range(0, NativeAotHelperLedger.EntryCount)
            .Select(NativeAotHelperLedger.GetEntry)
            .Where(entry => entry.IsEfiOnly && entry.Area != NativeAotHelperArea.EfiCheckedReferences)
            .SelectMany(entry => SplitSymbols(entry.HelperSymbols)).ToArray();
        HostCheck.True(sourceSymbols.Order(StringComparer.Ordinal).SequenceEqual(ledgerExports.Order(StringComparer.Ordinal)),
            "ordinary runtime exports and independently maintained EFI ledger must agree; missing="
                + string.Join(",", ledgerExports.Except(sourceSymbols, StringComparer.Ordinal)) + "; extra="
                + string.Join(",", sourceSymbols.Except(ledgerExports, StringComparer.Ordinal)));

        foreach (string symbol in sourceSymbols)
        {
            int sourceCount = CountSymbols(sourceSymbols, symbol);
            if (sourceCount != 1)
            {
                throw new InvalidOperationException(
                    "RuntimeExport symbol " + symbol + " appears " + sourceCount + " times in the selected runtime compilation.");
            }

            int ledgerCount = CountLedgerSymbol(symbol);
            if (ledgerCount != 1)
            {
                throw new InvalidOperationException(
                    "RuntimeExport symbol " + symbol + " appears in " + ledgerCount + " ledger entries.");
            }
        }

        int efiLedgerSymbolCount = 0;
        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            NativeAotHelperLedgerEntry entry = NativeAotHelperLedger.GetEntry(i);
            if (!entry.IsEfiOnly)
            {
                continue;
            }

            string[] ledgerSymbols = SplitSymbols(entry.HelperSymbols);
            if (entry.Area == NativeAotHelperArea.EfiCheckedReferences)
            {
                // These three symbols are COFF definitions, not fabricated C#
                // RuntimeExport declarations. The fourth is the ordinary adapter.
                var definition = CheckedReferenceBarrierThunkGenerator.Define();
                string callback = CheckedReferenceBarrierThunkGenerator.Callback;
                var symbols = definition.Symbols ?? throw new InvalidOperationException("generated symbol inventory missing");
                var defined = symbols.Where(symbol => symbol.SectionNumber > 0).ToArray();
                HostCheck.True(defined.Select(symbol => symbol.Name).Order(StringComparer.Ordinal)
                    .SequenceEqual(ledgerSymbols.Where(name => name != callback).Order(StringComparer.Ordinal)),
                    "generated COFF definitions match the ledger without counting source literals");
                ExpectEqual("one undefined callback relocation target", 1,
                    symbols.Count(symbol => symbol.Name == callback && symbol.SectionNumber == 0));
                foreach (string name in ledgerSymbols)
                {
                    ExpectEqual("generated symbol absent from ordinary shim", 0, CountSymbols(sourceSymbols, name));
                    ExpectEqual("generated symbol one ledger entry", 1, CountLedgerSymbol(name));
                }
                foreach (string flag in new[] { "EnableM04CheckedReferenceBarrierProbe", "EnableM04GcStaticRootMaterializationProbe" })
                {
                    string[] heapPrerequisites = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                        "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
                        "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
                        "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe"];
                    var catalog = HostSourceCatalog.Efi(EfiProfile.Create("Diagnostic",
                        heapPrerequisites.Append(flag).ToDictionary(name => name, _ => true, StringComparer.Ordinal)));
                    var exports = HostRuntimeExports.Read(catalog);
                    HostCheck.True(exports.TryGetValue(callback, out var method), flag);
                    HostCheck.Equal("OS.Runtime.RhpNewFastAllocation", method!.ContainingType.ToDisplayString());
                    HostCheck.Equal(4, method.Parameters.Length);
                    HostCheck.True(method.Arity == 0 && method.RefKind == RefKind.None
                        && method.Parameters.All(parameter => parameter.RefKind == RefKind.None && parameter.Type.SpecialType == SpecialType.System_UInt64)
                        && method.ReturnType.SpecialType == SpecialType.System_UInt32, "callback POD ABI");
                }
                continue;
            }
            for (int symbolIndex = 0; symbolIndex < ledgerSymbols.Length; symbolIndex++)
            {
                string ledgerSymbol = ledgerSymbols[symbolIndex];
                efiLedgerSymbolCount++;

                int sourceCount = CountSymbols(sourceSymbols, ledgerSymbol);
                if (sourceCount != 1)
                {
                    throw new InvalidOperationException(
                        "EFI ledger symbol " + ledgerSymbol + " appears " + sourceCount +
                        " times in the selected runtime compilation.");
                }
            }
        }

        ExpectEqual("EFI ledger symbol count", sourceSymbols.Length, efiLedgerSymbolCount);

        ExpectEqual(
            "reverse p/invoke symbol count",
            4,
            SplitSymbols(GetEntry(NativeAotHelperArea.EfiReversePInvoke).HelperSymbols).Length);
        ExpectEqual(
            "fail-fast type-cast symbol count",
            12,
            SplitSymbols(GetEntry(NativeAotHelperArea.EfiFailFastTypeCasts).HelperSymbols).Length);
        ExpectEqual(
            "stack-probe symbol count",
            1,
            SplitSymbols(GetEntry(NativeAotHelperArea.EfiStackProbe).HelperSymbols).Length);
    }

    private static bool ContainsArea(NativeAotHelperArea area)
    {
        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            if (NativeAotHelperLedger.GetEntry(i).Area == area)
            {
                return true;
            }
        }

        return false;
    }

    private static NativeAotHelperLedgerEntry GetEntry(NativeAotHelperArea area)
    {
        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            NativeAotHelperLedgerEntry entry = NativeAotHelperLedger.GetEntry(i);
            if (entry.Area == area)
            {
                return entry;
            }
        }

        throw new InvalidOperationException("Missing helper ledger entry: " + area);
    }

    private static int CountLedgerSymbol(string symbol)
    {
        int count = 0;
        for (int i = 0; i < NativeAotHelperLedger.EntryCount; i++)
        {
            if (ContainsSymbol(NativeAotHelperLedger.GetEntry(i).HelperSymbols, symbol))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSymbols(string[] symbols, string symbol)
    {
        int count = 0;
        for (int i = 0; i < symbols.Length; i++)
        {
            if (string.Equals(symbols[i], symbol, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ContainsSymbol(string helperSymbols, string symbol)
    {
        string[] symbols = SplitSymbols(helperSymbols);
        for (int i = 0; i < symbols.Length; i++)
        {
            if (string.Equals(symbols[i], symbol, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] SplitSymbols(string helperSymbols)
    {
        return helperSymbols.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] ExtractRuntimeExportSymbols()
    {
        var catalog = HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>()));
        var exports = HostRuntimeExports.Read(catalog);
        // These two bootstrap contracts belong to the EFI image rather than the
        // runtime helper ledger. Validate their owners and ABI before removing
        // them; every other discovered export must have a ledger entry.
        var entry = exports["EfiMain"];
        HostCheck.True(SymbolEqualityComparer.Default.Equals(entry.ContainingType, catalog.Type("OS.Boot.EfiEntry"))
            && entry.Arity == 0 && entry.RefKind == RefKind.None
            && entry.ReturnType.SpecialType == SpecialType.System_Int64
            && entry.Parameters is [{ RefKind: RefKind.None, Type: IPointerTypeSymbol image },
                { RefKind: RefKind.None, Type: IPointerTypeSymbol table }]
            && image.PointedAtType.SpecialType == SpecialType.System_Void
            && SymbolEqualityComparer.Default.Equals(table.PointedAtType, catalog.Type("OS.Boot.EfiSystemTable")), "EFI entry ABI");
        var cookie = exports["__security_cookie"];
        HostCheck.True(SymbolEqualityComparer.Default.Equals(cookie.ContainingType, catalog.Type("OS.Boot.EfiEntry+M03FirstExitAdapter"))
            && cookie.Arity == 0 && cookie.RefKind == RefKind.None && cookie.Parameters.Length == 0
            && cookie.ReturnType.SpecialType == SpecialType.System_UInt64, "EFI cookie storage ABI");
        return exports.Keys.Where(name => name is not "EfiMain" and not "__security_cookie").ToArray();
    }
    private static void ExpectEqual(string scenario, int expected, int actual)
    {
        if (actual != expected)
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

    private static void ExpectContains(string scenario, string actual, string expectedSubstring)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                scenario + " expected text containing " + expectedSubstring + " but returned " + actual + ".");
        }
    }
}
