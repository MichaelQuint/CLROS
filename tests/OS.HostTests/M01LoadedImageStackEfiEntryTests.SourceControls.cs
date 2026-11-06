using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M01LoadedImageStackEfiEntryTests
{
    private static bool TestRepresentativeMutations()
    {
        var source = Enabled();
        var disabled = Selected(false);
        if (!ValidateContract(source, disabled, out string baselineFailure))
            return Failure("unmutated-contract=" + baselineFailure);
        const string ProbeSymbol = "M01_LOADED_IMAGE_STACK_PROBE";
        (string Search, string Replacement, string Name)[] mutations = [("#if (" + ProbeSymbol + " || CLROS_NORMAL_VERIFIED_EXIT)", "#if true", "unguarded-probe"), ("PrepareFromStaticScratch", "Prepare", "rich-prepare-on-firmware-stack"), ("AllocatePages(", "AllocatePagesDisabled(", "allocation-removed"), ("EfiLoaderCode", "EfiLoaderData", "allocation-type"), ("RecordTransferFromStaticState", "RecordTransfer", "host-transition-overload"), ("ObserveContinuationRspFromStaticState", "ObserveContinuationRsp", "host-observe-overload"), ("ReportUnexpectedReturnFromStaticState", "ReportUnexpectedReturn", "host-return-overload"), ("UnmanagedCallersOnly", "ManagedContinuationOnly", "managed-continuation"), ("FirmwareMemoryMapRecord.FlagOwnedCopy", "0", "unowned-map"), ("BootReservationTableValidator", "BootReservationTableValidatorDisabled", "ledger-validation"), ("StackProbeEntryOffset = 64", "StackProbeEntryOffset = 63", "stack-probe-offset"), ("25 => 0xf3", "25 => 0xf2", "stack-probe-loop-displacement"), ("codePage[index] = 0xcc;", "codePage[index] = 0x90;", "code-page-trap-fill"), ("codePage[index] != 0xcc", "false", "code-page-fill-readback"), (".TryInstall((ulong)probe)", ".TryInstallDisabled((ulong)probe)", "stack-probe-install"), ("codeFirmwareStatus == EfiSuccess;", "codeFirmwareStatus == EfiSuccess" + " && codeAddress != 0;", "code-allocation-zero-ownership"), ("dataFirmwareStatus == EfiSuccess;", "dataFirmwareStatus == EfiSuccess" + " && dataAddress != 0;", "data-allocation-zero-ownership"), ("TryClearStackProbeBytes(", "TryClearStackProbeBytesDisabled(", "stack-probe-clear-before-free"), ("probe[index] != 0", "probe[index] == 0", "stack-probe-clear-readback"), ("out ulong loadedImageFirmwareStatus", "out ulong ignoredLoadedImageFirmwareStatus", "loaded-image-status-out"), ("out ulong memoryMapFirmwareStatus", "out ulong ignoredMemoryMapFirmwareStatus", "memory-map-status-out"), ("firmwareStatus = bootServices->HandleProtocol", "ulong ignoredStatus = bootServices->HandleProtocol", "loaded-image-status-assignment"), ("firmwareStatus = bootServices->GetMemoryMap", "ulong ignoredStatus = bootServices->GetMemoryMap", "memory-map-status-assignment"), ("? loadedImageFirmwareStatus", "? NotAttemptedStatus", "loaded-image-status-selection"), ("? memoryMapFirmwareStatus", "? NotAttemptedStatus", "memory-map-status-selection")];
        foreach (var (search, replacement, name) in mutations)
        {
            if (name == "unguarded-probe")
            {
                // Preserve the disabled-mode rejection using the actual fallback,
                // rather than mutating a textual preprocessor spelling.
                if (ValidateContract(source, MutateMethod(disabled, StageOwner, "Run", "return false;", "return true;"), out _))
                    return Failure("mutation-survived=" + name);
            }
            else if (!Rejects(() => Mutate(source, StageOwner, search, replacement), changed => ValidateContract(changed, disabled, out _)))
                return Failure("mutation-survived=" + name);
        }

        if (!Rejects(() =>
        {
            var first = MutateMethod(source, StageOwner, "Run", "MaterializeAndInstallStackProbe(", "M01ProbeOrderSwapPlaceholder(");
            var second = MutateMethod(first, StageOwner, "Run", "PrepareAndTransfer(", "MaterializeAndInstallStackProbe(");
            return MutateMethod(second, StageOwner, "Run", "M01ProbeOrderSwapPlaceholder(", "PrepareAndTransfer(");
        }, changed => ValidateContract(changed, disabled, out _)))
            return Failure("mutation-survived=stack-probe-install-order");
        if (!Rejects(() => SwapMapOrder(source), changed => ValidateContract(changed, disabled, out _)))
            return Failure("mutation-survived=run-map-order");
        (string Method, string Search, string Replacement, string Name)[] methodMutations = [("TryValidateAllocation", "(address & (PageSize - 1)) != 0", "false", "allocation-alignment"), ("TryValidateAllocation", "pageCount > ~0UL / PageSize", "false", "allocation-multiply-overflow"), ("TryValidateAllocation", "address > ~0UL - byteLength", "false", "allocation-add-overflow"), ("RangesOverlap", "firstStart < secondEndExclusive", "false", "allocation-overlap-always-false"), ("MapContainsAllocation", "type != expectedType", "false", "map-allocation-type-filter"), ("WriteSuccessLine", "GetM01StageOneSuccessLineByte(", "GetM01StageOneSuccessLineByteDisabled(", "success-marker-byte-source"), ("WriteSuccessLine", "EfiEntry.WriteCrLf(console, serial);", "return;", "success-marker-crlf"), ("GetStackProbeByte", "return index switch", "return 1 ^ (index switch", "stack-probe-byte-plan-wrapper"), ("MaterializeAndInstallStackProbe", "return System.Runtime.X64StackProbeRuntime", "probe[0] ^= 1;" + "\n            return System.Runtime" + ".X64StackProbeRuntime", "stack-probe-post-readback-corruption")];
        foreach (var (method, search, replacement, name) in methodMutations)
            if (!Rejects(() => MutateMethod(source, StageOwner, method, search, replacement), changed => ValidateContract(changed, disabled, out _)))
                return Failure("mutation-survived=" + name);
        if (!ValidateRuntimeStackProbeContract(source, out string runtimeBaselineFailure))
            return Failure("unmutated-runtime-contract=" + runtimeBaselineFailure);
        (string Search, string Replacement, string Name)[] runtimeMutations = [("[RuntimeExport(\"RhpStackProbe\")]", "[RuntimeExport(\"RhpStackProbeDisabled\")]", "runtime-export"), ("(probeEntry & 15) != 0", "(probeEntry & 7) != 0", "runtime-install-alignment"), ("delegate* managed<void>", "delegate* unmanaged<void>", "runtime-managed-tail-dispatch"), ("((delegate* managed<void>)(void*)probeEntry)();", "return;", "runtime-noop-bridge")];
        foreach (var (search, replacement, name) in runtimeMutations)
            if (!Rejects(() => Mutate(source, RuntimeOwner, search, replacement), changed => ValidateRuntimeStackProbeContract(changed, out _)))
                return Failure("runtime-mutation-survived=" + name);
        return Rejects(() => MutateMethod(source, RuntimeOwner, "RhpStackProbe", "while (true)", "if (true)"), changed => ValidateRuntimeStackProbeContract(changed, out _)) || Failure("runtime-mutation-survived=runtime-null-fail-closed");
    }

    private static CSharpSourceCatalog SwapMapOrder(CSharpSourceCatalog source)
    {
        var method = source.Method(StageOwner, "Run");
        var allocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AllocatePages" }).ToArray();
        var map = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "TryRecaptureMemoryMap" });
        if (allocations.Length != 2 || allocations[1].SpanStart >= map.SpanStart)
            throw new InvalidOperationException("mutation_anchor_map_order");
        var name = ((MemberAccessExpressionSyntax)allocations[1].Expression).Name;
        var document = source.Document(method);
        string changed = document.Text.Remove(map.Expression.SpanStart, map.Expression.Span.Length).Insert(map.Expression.SpanStart, "AllocatePages");
        changed = changed.Remove(name.SpanStart, name.Span.Length).Insert(name.SpanStart, "TryRecaptureMemoryMap");
        return ReplaceSpan(source, document, 0, document.Text.Length, changed);
    }

    private static bool TestProbeScopedMutationSelector()
    {
        var source = SelectorSource();
        var changed = Mutate(source, "Selected.Owner", "value = 1", "value = 2");
        if (!Body(changed, "Selected.Owner", "Other")!.Contains("value = 2", StringComparison.Ordinal) || Body(changed, "Foreign.Owner", "Target") != Body(source, "Foreign.Owner", "Target"))
            return Failure("mutation-selector-owner");
        foreach (string missing in new[]
        {
            "",
            "value = 7",
            "ForeignOnly()"
        }

        )
            if (!SelectionRejects(() => Mutate(source, "Selected.Owner", missing, "value = 3")))
                return Failure("mutation-selector-missing-or-empty-anchor");
        return true;
    }

    private static bool TestProbeScopedMethodMutationSelector()
    {
        var source = SelectorSource();
        var changed = MutateMethod(source, "Selected.Owner", "Target", "value = 1", "value = 2");
        if (Body(changed, "Selected.Owner", "Other") != Body(source, "Selected.Owner", "Other") || Body(changed, "Foreign.Owner", "Target") != Body(source, "Foreign.Owner", "Target"))
            return Failure("method-selector-foreign-collision");
        return SelectionRejects(() => MutateMethod(source, "Selected.Owner", "Missing", "value = 1", "value = 2")) && SelectionRejects(() => MutateMethod(source, "Selected.Owner", "", "value = 1", "value = 2")) && SelectionRejects(() => MutateMethod(source, "Selected.Owner", "Target", "", "value = 2")) && SelectionRejects(() => MutateMethod(source, "Selected.Owner", "Target", "ForeignOnly()", "value = 2"));
    }

    private static bool SelectionRejects(Func<CSharpSourceCatalog> mutation)
    {
        try
        {
            _ = mutation();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static CSharpSourceCatalog SelectorSource()
    {
        const string text = """
            namespace Foreign { class Owner { static void Target() { int value = 1; ForeignOnly(); } static void ForeignOnly() {} } }
            namespace Selected { partial class Owner { static void Other() { int value = 1; } static void Target() { int value = 1; } } }
            """;
        return new([CSharpSourceDocument.FromBytes("arbitrary-selector.cs", System.Text.Encoding.UTF8.GetBytes(text), new CSharpParseOptions(LanguageVersion.CSharp14))], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
    }

    [HostTest("loaded-image contracts follow renamed sources and moved partial methods")]
    private static void SourceDiscoveryControls()
    {
        var source = Enabled();
        var disabled = Selected(false);
        HostCheck.True(ValidateContract(source, disabled, out _), "baseline");
        var renamed = new CSharpSourceCatalog(source.Documents.Select(document => CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(document.Path)!, "moved-" + Path.GetFileName(document.Path)), System.Text.Encoding.UTF8.GetBytes(document.Text), (CSharpParseOptions)document.Syntax.Options)), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(ValidateContract(renamed, disabled, out _), "arbitrary filenames");
        foreach (string method in new[]
        {
            "TryCaptureLoadedImage",
            "TryValidateAllocation",
            "WriteSuccessLine"
        }

        )
        {
            var moved = MoveStageMethod(source, method);
            RequireBound(moved);
            HostCheck.True(ValidateContract(moved, disabled, out _), "moved partial: " + method);
        }

        const string extra = """
            namespace OS.Boot;
            public static unsafe partial class EfiEntry
            {
                private static class AdditionalCaller
                {
                    internal static void Call() => M01LoadedImageStackIntegration.RecordTransferFromStaticState(default, 0);
                }
            }
            """;
        var injected = new CSharpSourceCatalog(source.Documents.Append(CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(source.Documents[0].Path)!, "AdditionalCaller.cs"), System.Text.Encoding.UTF8.GetBytes(extra), (CSharpParseOptions)source.Documents[0].Syntax.Options)), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        RequireBound(injected);
        HostCheck.True(BoundCoordinatorCalls(source) && !BoundCoordinatorCalls(injected), "extra nested bound caller");
    }

    private static CSharpSourceCatalog MoveStageMethod(CSharpSourceCatalog source, string name)
    {
        var method = source.Method(StageOwner, name);
        var document = source.Document(method);
        string imports = string.Join("\n", document.Syntax.GetCompilationUnitRoot().Usings.Select(HostSourceText.Active));
        string text = imports + "\nnamespace OS.Boot;\npublic static unsafe partial class EfiEntry { private static partial class M01StageOneAdapter {\n" + HostSourceText.Active(method) + "\n} }";
        var parse = (CSharpParseOptions)document.Syntax.Options;
        var replaced = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(document.Text.Remove(method.SpanStart, method.Span.Length)), parse);
        var added = CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(document.Path)!, "Moved-" + name + ".cs"), System.Text.Encoding.UTF8.GetBytes(text), parse);
        return new(source.Documents.Select(item => item == document ? replaced : item).Append(added), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static void RequireBound(CSharpSourceCatalog source)
    {
        var errors = source.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        HostCheck.True(!errors.Any(), "control must bind: " + string.Join("; ", errors));
    }
}
