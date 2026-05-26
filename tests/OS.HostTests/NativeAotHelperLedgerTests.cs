using System.Runtime.CompilerServices;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;

internal static class NativeAotHelperLedgerTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;

        Run("runtime.nativeaot helper ledger", TestLedger, ref failures);
        Run("runtime.nativeaot helper ledger structured columns", TestStructuredColumns, ref failures);
        Run("runtime.nativeaot helper ledger allocator mutation boundary", TestAllocatorMutationBoundary, ref failures);
        Run("runtime.nativeaot helper ledger symbols", TestLedgerSymbols, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("NativeAOT helper ledger tests failed: " + failures);
        }
    }

    private static void Run(string name, Action test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            test();
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

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
            NativeAotHelperArea.EfiFailFastTypeCasts
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

        ExpectEqual("EFI-only helper count", 2, efiOnlyCount);
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
    }

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

    private static void TestLedgerSymbols()
    {
        string[] sourceSymbols = ExtractRuntimeExportSymbols();

        ExpectEqual("RuntimeExport symbol count", 16, sourceSymbols.Length);

        foreach (string symbol in sourceSymbols)
        {
            int ledgerCount = CountLedgerSymbol(symbol);
            if (ledgerCount != 1)
            {
                throw new InvalidOperationException(
                    "RuntimeExport symbol " + symbol + " appears in " + ledgerCount + " ledger entries.");
            }
        }

        ExpectEqual(
            "reverse p/invoke symbol count",
            4,
            SplitSymbols(GetEntry(NativeAotHelperArea.EfiReversePInvoke).HelperSymbols).Length);
        ExpectEqual(
            "fail-fast type-cast symbol count",
            12,
            SplitSymbols(GetEntry(NativeAotHelperArea.EfiFailFastTypeCasts).HelperSymbols).Length);
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
        string runtimeSource = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src", "OS", "Runtime", "MinimalRuntime.cs"));
        List<string> symbols = new List<string>();
        const string marker = "[RuntimeExport(\"";
        int searchStart = 0;

        while (true)
        {
            int markerIndex = runtimeSource.IndexOf(marker, searchStart, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                break;
            }

            int symbolStart = markerIndex + marker.Length;
            int symbolEnd = runtimeSource.IndexOf("\")", symbolStart, StringComparison.Ordinal);
            if (symbolEnd < symbolStart)
            {
                throw new InvalidOperationException("Malformed RuntimeExport attribute near offset " + markerIndex + ".");
            }

            symbols.Add(runtimeSource[symbolStart..symbolEnd]);
            searchStart = symbolEnd + 2;
        }

        return symbols.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing OS.slnx.");
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
