using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class EfiProfileTestControls
{
    // Inspect each active diagnostic branch; strip only the union of exactly
    // owned call sites so the existing raw-source firewall still sees all others.
    internal static bool RemoveOwnedDmaCalls(ref string source, string call, out string reason)
    {
        reason = string.Empty;
        var admitted = new SortedSet<(int Start, int Length)>();
        // The cooperative profile borrows ordinary retained pages, independently
        // of the DMA profiles below. Admit its exact owner/receipt arguments and
        // ordered post-exit edge; leave every other raw occurrence visible.
        {
            if (!TryGetCooperativeSource(source, out var root, out var owner, out reason)) return false;
            var invocations = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().ToArray();
            var calls = invocations.Where(i => i.Expression.ToString() == call).ToArray();
            string expected = call.EndsWith(".AllocateOnePage", StringComparison.Ordinal)
                ? "(allocator,&node,1,1,null,0,0,1,Page)"
                : "(allocator,&node,1,1,null,0,0,receipts[count-1])";
            static string Tokens(Microsoft.CodeAnalysis.SyntaxNode node) => string.Concat(node.DescendantTokens().Select(token => token.Text));
            reason = "M09_004_PROBE:allocator-owner-receipt";
            if (calls.Length != 1 || Tokens(calls[0].ArgumentList) != expected ||
                calls[0].Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Span != owner.Span) return false;
            if (call.EndsWith(".AllocateOnePage", StringComparison.Ordinal))
            {
                var receipts = owner.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
                    .Where(d => d.Declaration.Variables.Any(v => v.Identifier.Text == "receipts")).ToArray();
                reason = "M09_004_PROBE:receipt-storage";
                if (receipts.Length != 1 || Tokens(receipts[0]) !=
                    "BootPageAllocatorOnePageResult*receipts=stackallocBootPageAllocatorOnePageResult[20];") return false;
                admitted.Add((receipts[0].SpanStart, receipts[0].Span.Length));
            }
            admitted.Add((calls[0].Expression.SpanStart, calls[0].Expression.Span.Length));
        }
        {
            if (!TryGetSchedulerTimerSource(source, out var root, out var owner, out reason)) return false;
            var calls = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString() == call).ToArray();
            reason = "M09_005_PROBE:allocator-owner-receipt";
            if (calls.Length != 2 || calls.Count(i => i.Ancestors().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault()?.Identifier.Text == "RunCooperativeSwitch") != 1) return false;
            calls = calls.Where(i => i.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Span == owner.Span).ToArray();
            string expected = call.EndsWith(".AllocateOnePage", StringComparison.Ordinal)
                ? "(allocator,&node,1,1,null,0,0,1,Page)"
                : "(allocator,&node,1,1,null,0,0,receipts[i-1])";
            if (calls.Length != 1 || Tokens(calls[0].ArgumentList) != expected) return false;
            var loop = calls[0].Ancestors().OfType<ForStatementSyntax>().FirstOrDefault();
            bool allocate = call.EndsWith(".AllocateOnePage", StringComparison.Ordinal);
            reason = "M09_005_PROBE:receipt-loop";
            if (loop is null || loop.Condition is null || Tokens(loop.Condition) != (allocate ? "borrowed<22" : "i>0") ||
                loop.Incrementors.Count != 1 || Tokens(loop.Incrementors[0]) != (allocate ? "borrowed++" : "i--") ||
                (allocate ? loop.Declaration is not null || loop.Initializers.Count != 0 :
                    loop.Declaration is null || Tokens(loop.Declaration) != "uinti=borrowed" || loop.Initializers.Count != 0)) return false;
            if (call.EndsWith(".AllocateOnePage", StringComparison.Ordinal))
            {
                var receipts = owner.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
                    .Single(d => d.Declaration.Variables.Any(v => v.Identifier.Text == "receipts"));
                admitted.Add((receipts.SpanStart, receipts.Span.Length));
            }
            admitted.Add((calls[0].Expression.SpanStart, calls[0].Expression.Span.Length));
        }
        foreach (var (symbol, entry, body) in EfiContracts.OwnedDmaAllocatorProfiles)
        {
            string[] symbols = ["M01_LOADED_IMAGE_STACK_PROBE", "M01_LIVE_ALLOCATOR_PROBE", "ACPI_PHYSICAL_DISCOVERY_PROBE",
                "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE", "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE",
                "M05_PCI_ENUMERATION_PROBE", symbol, .. symbol switch
                {
                    "M05_BAR_DMA_PROBE" or "M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE" => Array.Empty<string>(),
                    "M06_VIRTIO_BLOCK_READ_PROBE" => Array.Empty<string>(),
                    "M06_FAT_READ_PROBE" => ["M06_VIRTIO_BLOCK_READ_PROBE", "M06_LIVE_BLOCK_CONTRACT_PROBE", "M06_GPT_PARTITION_PROBE"],
                    "M06_PACKAGE_READ_PROBE" => ["M06_VIRTIO_BLOCK_READ_PROBE", "M06_LIVE_BLOCK_CONTRACT_PROBE", "M06_GPT_PARTITION_PROBE", "M06_FAT_READ_PROBE"],
                    "M07_VIRTIO_NET_PROBE" => ["M05_VIRTIO_NEGOTIATION_PROBE", "M05_VIRTQUEUE_POLLING_PROBE"],
                    "M07_ARP_RECEIVE_PROBE" => ["M05_VIRTIO_NEGOTIATION_PROBE", "M05_VIRTQUEUE_POLLING_PROBE", "M07_VIRTIO_NET_PROBE"],
                    "M06_GPT_PARTITION_PROBE" => ["M06_VIRTIO_BLOCK_READ_PROBE", "M06_LIVE_BLOCK_CONTRACT_PROBE"],
                    "M06_LIVE_BLOCK_CONTRACT_PROBE" => ["M06_VIRTIO_BLOCK_READ_PROBE"],
                    "M05_VIRTQUEUE_INTERRUPT_PROBE" => ["M05_VIRTIO_NEGOTIATION_PROBE", "M05_APIC_SETUP_PROBE"],
                    _ => ["M05_VIRTIO_NEGOTIATION_PROBE"]
                }];
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source,
                new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(preprocessorSymbols: symbols));
            reason = symbol + ":syntax " + string.Join(" | ", tree.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            if (tree.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)) return false;
            var root = tree.GetRoot();
            var methods = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>();
            reason = symbol + ":entry-body";
            if (methods.Count(m => m.Identifier.Text == entry) != 1 || methods.Count(m => m.Identifier.Text == body) != 1) return false;
            var invocations = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().ToArray();
            var calls = invocations.Where(i => i.Expression.ToString() == call).ToArray();
            var payloadCalls = calls.Where(i => i.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                .FirstOrDefault()?.Identifier.Text == "StagePackagePayload").ToArray();
            if (symbol == "M06_PACKAGE_READ_PROBE")
            {
                if (payloadCalls.Length != 1 || calls.Length != 2) return false;
                var payloadEdges = invocations.Where(i => i.Expression.ToString() == "StagePackagePayload").ToArray();
                if (payloadEdges.Length != 1 || payloadEdges[0].ArgumentList.ToString() != "(state, packageBytes, selectedPath)" ||
                    payloadEdges[0].Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                        .FirstOrDefault()?.Identifier.Text != "RunPackageReadCore") return false;
                string expectedPayload = call.EndsWith(".AllocateOnePage", StringComparison.Ordinal)
                    ? "(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096)"
                    : "(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation)";
                if (payloadCalls[0].ArgumentList.ToString() != expectedPayload) return false;
                admitted.Add((payloadCalls[0].Expression.SpanStart, payloadCalls[0].Expression.Span.Length));
                calls = calls.Except(payloadCalls).ToArray();
            }
            reason = symbol + ":call-count-owner " + calls.Length;
            if (symbol == "M07_ARP_RECEIVE_PROBE")
            {
                if (calls.Length != 3 || new[] { "RunQueueCore", "RunNetCore" }.Any(parent =>
                    calls.Count(i => i.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                        .FirstOrDefault()?.Identifier.Text == parent) != 1)) return false;
                calls = calls.Where(i => i.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.Text == body).ToArray();
            }
            if (symbol == "M07_VIRTIO_NET_PROBE")
            {
                // Its required polling parent has its own separately admitted
                // transaction. Admit only this child here, preserving that audit.
                if (calls.Length != 2 || calls.Count(i => i.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.Text == "RunQueueCore") != 1) return false;
                calls = calls.Where(i => i.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.Text == body).ToArray();
            }
            if (calls.Length != 1 || calls[0].Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                .FirstOrDefault()?.Identifier.Text != body) return false;
            if (body != entry)
            {
                var edges = invocations.Where(i => i.Expression.ToString() == body).ToArray();
                reason = symbol + ":shared-body-caller " + edges.Length;
                string[] expectedCallers = symbol == "M06_PACKAGE_READ_PROBE" ? ["RunOwnedVirtioBlockRead", "RunOwnedLiveBlockContract", "ReadGptSector", "ReadFatSector", entry] : symbol == "M06_FAT_READ_PROBE" ? ["RunOwnedVirtioBlockRead", "RunOwnedLiveBlockContract", "ReadGptSector", entry] : symbol == "M06_GPT_PARTITION_PROBE" ? ["RunOwnedVirtioBlockRead", "RunOwnedLiveBlockContract", entry] : symbol == "M06_LIVE_BLOCK_CONTRACT_PROBE" ? ["RunOwnedVirtioBlockRead", entry] : [entry];
                if (!edges.Select(e => e.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.Text).Order().SequenceEqual(expectedCallers.Order())) return false;
                if (symbol == "M06_FAT_READ_PROBE" &&
                    (edges[0].ArgumentList.ToString() != "(state)" || edges[1].ArgumentList.ToString() != "(state, true)" ||
                     edges[2].ArgumentList.ToString() != "(state, false, destination, sector, reads + 4UL)" ||
                     edges[3].ArgumentList.ToString() != "(state, false, destination, sector, reads + 70UL, true)")) return false;
                if (symbol == "M06_PACKAGE_READ_PROBE" &&
                    (edges[0].ArgumentList.ToString() != "(state)" || edges[1].ArgumentList.ToString() != "(state, true)" ||
                     edges[2].ArgumentList.ToString() != "(state, false, destination, sector, reads + 4UL)" ||
                     edges[3].ArgumentList.ToString() != "(state, false, destination, sector, reads + 70UL, true)" ||
                     edges[4].ArgumentList.ToString() != "(state, false, destination, sector, reads + 134UL, false, true)")) return false;
                if (symbol == "M06_GPT_PARTITION_PROBE" &&
                    (edges[0].ArgumentList.ToString() != "(state)" || edges[1].ArgumentList.ToString() != "(state, true)" ||
                     edges[2].ArgumentList.ToString() != "(state, false, destination, sector, reads + 4UL)")) return false;
                if (symbol == "M06_LIVE_BLOCK_CONTRACT_PROBE" &&
                    (edges[0].ArgumentList.ToString() != "(state)" || edges[1].ArgumentList.ToString() != "(state, true)")) return false;
            }
            admitted.Add((calls[0].Expression.SpanStart, calls[0].Expression.Span.Length));
        }
        foreach (var span in admitted.Reverse()) source = source.Remove(span.Start, span.Length);
        reason = string.Empty;
        return true;
    }

    // Admit only the two guarded, one-node retained pool transactions. Keep all
    // other references visible to the caller's whole-source materializer firewall.
    internal static bool RemoveOwnedCooperativeMaterializer(ref string source, out string reason)
    {
        var admitted = new SortedSet<(int Start, int Length)>();
        foreach (bool timer in new[] { false, true })
        {
            SyntaxNode root; MethodDeclarationSyntax owner;
            if (timer ? !TryGetSchedulerTimerSource(source, out root, out owner, out reason) :
                !TryGetCooperativeSource(source, out root, out owner, out reason)) return false;
            string symbol = timer ? "M09_005_PROBE" : "M09_004_PROBE";
            var calls = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString() == "BootPageAllocatorStateMaterializer.Materialize").ToArray();
            if (timer)
            {
                reason = symbol + ":materializer-call-count";
                if (calls.Length != 2 || calls.Count(i => i.Ancestors().OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.Text == "RunCooperativeSwitch") != 1) return false;
                calls = calls.Where(i => i.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Span == owner.Span).ToArray();
            }
            reason = symbol + ":materializer-owner-arguments";
            if (calls.Length != 1 || Tokens(calls[0].ArgumentList) !=
                "(&node,1,1,1,0,Page,32,32*Page,&allocator,1)" ||
                calls[0].Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Span != owner.Span) return false;
            var declarations = owner.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().ToArray();
            string[] expected = ["ulongpool=state->CooperativePool;",
                "BootPageFreeListNodenode=new(0,0,pool,pool+32*Page,32,32*Page,-1);",
                "BootPageAllocatorStateallocator=default;"];
            reason = symbol + ":materializer-pool";
            int previous = owner.SpanStart;
            foreach (string declaration in expected)
            {
                var matches = declarations.Where(d => Tokens(d) == declaration).ToArray();
                if (matches.Length != 1 || matches[0].SpanStart <= previous || matches[0].SpanStart >= calls[0].SpanStart) return false;
                previous = matches[0].SpanStart;
            }
            reason = symbol + ":materializer-failure-arm";
            if (calls[0].Parent is not MemberAccessExpressionSyntax succeeded || succeeded.Name.Identifier.Text != "Succeeded" ||
                succeeded.Parent is not PrefixUnaryExpressionSyntax condition || !condition.IsKind(SyntaxKind.LogicalNotExpression) ||
                condition.Parent is not IfStatementSyntax branch || branch.Condition != condition || branch.Else is not null ||
                Tokens(branch.Statement) != (timer ? "returnfalse;" : "{_=CooperativeMarker(state,3);returnfalse;}")) return false;
            admitted.Add((calls[0].Expression.SpanStart, calls[0].Expression.Span.Length));
        }
        foreach (var span in admitted.Reverse()) source = source.Remove(span.Start, span.Length);
        reason = string.Empty;
        return true;
    }

    private static string Tokens(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(token => token.Text));

    // The owner must exist only with M09 enabled, behind the original ordered
    // post-exit call. Sharing this admission keeps both narrow removals aligned.
    private static bool TryGetCooperativeSource(string source, out SyntaxNode root, out MethodDeclarationSyntax owner, out string reason)
    {
        string[] symbols = ["M01_LOADED_IMAGE_STACK_PROBE", "M01_LIVE_ALLOCATOR_PROBE", "ACPI_PHYSICAL_DISCOVERY_PROBE",
            "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE", "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE", "M09_004_PROBE"];
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: symbols));
        root = tree.GetRoot(); owner = null!;
        reason = "M09_004_PROBE:syntax";
        if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)) return false;
        var owners = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.Text == "RunCooperativeSwitch").ToArray();
        reason = "M09_004_PROBE:conditional-owner";
        if (owners.Length != 1) return false;
        var disabled = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: symbols[..^1]));
        if (disabled.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error) || disabled.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == "RunCooperativeSwitch")) return false;
        owner = owners[0];
        var edges = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString() == "BootSequence.RunOwnedCooperativeSwitch").ToArray();
        reason = "M09_004_PROBE:ordered-owned-edge";
        if (edges.Length != 1 || edges[0].Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text != "Diagnose" ||
            Tokens(edges[0].ArgumentList) != "(state->OwnedPhase==2,transition->Mode==FirmwareServicesMode.BootServicesExited&&transition->RetainedFirmwareCallbacksCleared&&state->Arena.Binding->RetainedPointersCleared,state,&RunCooperativeSwitch)") return false;
        reason = string.Empty;
        return true;
    }

    private static bool TryGetSchedulerTimerSource(string source, out SyntaxNode root, out MethodDeclarationSyntax owner, out string reason)
    {
        string[] symbols = ["M01_LOADED_IMAGE_STACK_PROBE", "M01_LIVE_ALLOCATOR_PROBE", "ACPI_PHYSICAL_DISCOVERY_PROBE",
            "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE", "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE",
            "M05_PCI_ENUMERATION_PROBE", "M05_APIC_SETUP_PROBE", "M09_005_PROBE"];
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: symbols));
        root = tree.GetRoot(); owner = null!;
        reason = "M09_005_PROBE:syntax";
        if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)) return false;
        var owners = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.Text == "RunSchedulerTimer").ToArray();
        reason = "M09_005_PROBE:conditional-owner";
        if (owners.Length != 1) return false;
        foreach (string[] disabledSymbols in new string[][] { symbols[..^1], [.. symbols[..^1], "M09_004_PROBE"] })
        {
            var disabled = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: disabledSymbols));
            if (disabled.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error) || disabled.GetRoot().DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == "RunSchedulerTimer")) return false;
        }
        owner = owners[0];
        var receipts = owner.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
            .Where(d => d.Declaration.Variables.Any(v => v.Identifier.Text == "receipts")).ToArray();
        reason = "M09_005_PROBE:receipt-storage";
        if (receipts.Length != 1 || Tokens(receipts[0]) !=
            "BootPageAllocatorOnePageResult*receipts=stackallocBootPageAllocatorOnePageResult[22];") return false;
        var edges = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString() == "BootSequence.RunOwnedSchedulerTimer").ToArray();
        reason = "M09_005_PROBE:ordered-owned-apic-edge";
        if (edges.Length != 1 || edges[0].Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text != "Diagnose" ||
            Tokens(edges[0].ArgumentList) != "(state->OwnedPhase==2,state->ApicPhase==2,transition->Mode==FirmwareServicesMode.BootServicesExited&&transition->RetainedFirmwareCallbacksCleared&&state->Arena.Binding->RetainedPointersCleared,state,&RunSchedulerTimer)" ||
            edges[0].Parent is not PrefixUnaryExpressionSyntax condition || !condition.IsKind(SyntaxKind.LogicalNotExpression) ||
            condition.Parent is not IfStatementSyntax branch || branch.Condition != condition || branch.Else is not null ||
            Tokens(branch.Statement) != "return;" || branch.Parent is not BlockSyntax block) return false;
        int at = block.Statements.IndexOf(branch);
        if (at <= 0 || Tokens(block.Statements[at - 1]) !=
            "if(!BootSequence.RunOwnedApicSetup(state->PciPhase==8,state,&RunOwnedApicSetup))return;" ||
            block.Parent is not IfStatementSyntax pci || pci.Condition is not InvocationExpressionSyntax enumeration ||
            enumeration.Expression.ToString() != "BootSequence.RunOwnedPciEnumeration" ||
            Tokens(enumeration.ArgumentList) != "(state->OwnedPhase==2,state,&RunOwnedPciEnumeration)") return false;
        reason = string.Empty;
        return true;
    }

    // Independent expectations shared by cross-profile rejection fixtures.
    internal static string? MissingParentReason(IReadOnlyCollection<string> selected)
    {
        if(selected.Contains("EnableM16013Probe") && new[]{"EnableM07027Probe","EnableM07028Probe","EnableM10004Probe",
            "EnableM09011Probe","EnableM09012Probe","EnableM16010Probe"}.Any(selected.Contains))return "socket_vectors_profile_conflict";
        bool containment = selected.Contains("EnableM07027Probe") || selected.Contains("EnableM07028Probe");
        if (containment && (selected.Contains("EnableM16012Probe") || selected.Contains("EnableM10004Probe")))
            return "socket_containment_profile_conflict";
        bool sockets = selected.Contains("EnableM16013Probe") || containment || selected.Contains("EnableM16011Probe") || selected.Contains("EnableM16012Probe") || selected.Contains("EnableM10004Probe");
        if (selected.Contains("EnableM07026Probe") || sockets)
        {
            string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableAcpiPhysicalDiscoveryProbe",
                "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe", "EnableM05PciEnumerationProbe",
                "EnableM05VirtioNegotiationProbe", "EnableM05VirtqueuePollingProbe", "EnableM07VirtioNetProbe",
                "EnableM07ArpReceiveProbe", "EnableM16002Probe", "EnableM16003Probe"];
            string[] descendants = ["EnableM05ApicSetupProbe", "EnableM16004Probe", "EnableM16005Probe", "EnableM16006Probe",
                "EnableM16007Probe", "EnableM16008Probe", "EnableM16009Probe", "EnableM16011Probe", "EnableM16012Probe", "EnableM16013Probe", "EnableM10004Probe", "EnableM07027Probe", "EnableM07028Probe"];
            if (selected.Any(name => name != "EnableM07026Probe" && !parents.Contains(name) && !(sockets && descendants.Contains(name)))) return "m07_026_profile_conflict";
            // The socket leaf supplies the entire declared network/runtime chain.
            if (sockets) return null;
        }
        if (selected.Contains("EnableM09010Probe"))
        {
            string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe",
                "EnableM05PciEnumerationProbe", "EnableM05ApicSetupProbe"];
            if (selected.Any(name => name != "EnableM09010Probe" && !parents.Contains(name))) return "m09_010_profile_conflict";
            if (parents.Any(name => !selected.Contains(name))) return "m09_010_requires_owned_first_exit";
        }
        if (selected.Contains("EnableM09009Probe"))
        {
            string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe",
                "EnableM05PciEnumerationProbe", "EnableM05ApicSetupProbe"];
            if (selected.Any(name => name != "EnableM09009Probe" && !parents.Contains(name))) return "m09_009_profile_conflict";
            if (parents.Any(name => !selected.Contains(name))) return "m09_009_requires_owned_first_exit";
        }
        if (selected.Contains("EnableM09006Probe"))
        {
            string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe",
                "EnableM05PciEnumerationProbe", "EnableM05ApicSetupProbe"];
            if (selected.Any(name => name != "EnableM09006Probe" && !parents.Contains(name))) return "m09_006_profile_conflict";
            if (parents.Any(name => !selected.Contains(name))) return "m09_006_requires_owned_first_exit";
        }
        if (selected.Contains("EnableM09005Probe"))
        {
            string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe",
                "EnableM05PciEnumerationProbe", "EnableM05ApicSetupProbe"];
            if (selected.Any(name => name != "EnableM09005Probe" && !parents.Contains(name))) return "m09_005_profile_conflict";
            if (parents.Any(name => !selected.Contains(name))) return "m09_005_requires_owned_first_exit";
        }
        if (selected.Contains("EnableM09004Probe"))
        {
            string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe"];
            if (selected.Any(name => name != "EnableM09004Probe" && !parents.Contains(name))) return "m09_004_profile_conflict";
            if (parents.Any(name => !selected.Contains(name))) return "m09_004_requires_owned_first_exit";
        }
        bool linuxThreads = selected.Contains("EnableM16008Probe") || selected.Contains("EnableM16009Probe") ||
            selected.Contains("EnableM09011Probe") || selected.Contains("EnableM09012Probe") || selected.Contains("EnableM16010Probe");
        if (linuxThreads || selected.Contains("EnableM16002Probe") || selected.Contains("EnableM16003Probe") || selected.Contains("EnableM16004Probe") || selected.Contains("EnableM16005Probe") || selected.Contains("EnableM16006Probe") || selected.Contains("EnableM16007Probe"))
        {
            string[] allowed = ["EnableM16002Probe", "EnableM16003Probe", "EnableM16004Probe", "EnableM16005Probe", "EnableM16006Probe", "EnableM16007Probe", "EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
                "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe"];
            if (selected.Any(name => !allowed.Contains(name) && !(linuxThreads &&
                name is "EnableM16008Probe" or "EnableM16009Probe" or "EnableM09011Probe" or "EnableM09012Probe" or
                    "EnableM16010Probe" or "EnableM05PciEnumerationProbe" or "EnableM05ApicSetupProbe"))) return "m16_002_profile_conflict";
        }
        bool Has(string name) => selected.Contains("Enable" + name + "Probe", StringComparer.Ordinal);
        foreach (var (child, parent, reason) in new (string, string, string)[]
        {
            ("M03FirmwareAdapterRevocation", "M03FirstRealExitBootServices", "m03_revocation_requires_first_exit"),
            ("M03OwnedSubsystemSurvival", "M03FirstRealExitBootServices", "m03_owned_requires_first_exit"),
            ("M03OwnedSubsystemPanic", "M03OwnedSubsystemSurvival", "m03_owned_panic_requires_survival"),
            ("M05PhysicalMapping", "M03FirstRealExitBootServices", "m05_mapping_requires_owned_first_exit"),
            ("M05PhysicalMapping", "M03OwnedSubsystemSurvival", "m05_mapping_requires_owned_first_exit"),
            ("M05OwnedAcpiSelection", "M03FirstRealExitBootServices", "m05_acpi_requires_owned_first_exit"),
            ("M05OwnedAcpiSelection", "M03OwnedSubsystemSurvival", "m05_acpi_requires_owned_first_exit"),
            ("M05MonotonicClock", "M03FirstRealExitBootServices", "m05_clock_requires_owned_first_exit"),
            ("M05MonotonicClock", "M03OwnedSubsystemSurvival", "m05_clock_requires_owned_first_exit"),
            ("M05PciEnumeration", "M03FirstRealExitBootServices", "m05_pci_requires_owned_first_exit"),
            ("M05PciEnumeration", "M03OwnedSubsystemSurvival", "m05_pci_requires_owned_first_exit"),
            ("M06BlockMsix", "M06PackageRead", "m06_block_msix_requires_package_apic"),
            ("M06BlockMsix", "M05ApicSetup", "m06_block_msix_requires_package_apic"),
            ("M06BlockMsixPollingRecovery", "M06BlockMsix", "m06_block_msix_polling_recovery_requires_main"),
            ("M07VirtioNet", "M05VirtqueuePolling", "m07_virtio_net_requires_polling"),
            ("M07ArpReceive", "M07VirtioNet", "m07_arp_receive_requires_net"),
            ("M05BarDma", "M05PciEnumeration", "m05_bar_dma_requires_pci"),
            ("M05ApicSetup", "M05PciEnumeration", "m05_apic_requires_pci"),
            ("M05VirtqueueInterrupt", "M05ApicSetup", "m05_virtqueue_interrupt_requires_apic_negotiation"),
            ("M05VirtqueueInterrupt", "M05VirtioNegotiation", "m05_virtqueue_interrupt_requires_apic_negotiation"),
            ("M05VirtqueuePolling", "M05VirtioNegotiation", "m05_virtqueue_polling_requires_negotiation"),
            ("M05VirtioNegotiation", "M05PciEnumeration", "m05_virtio_negotiation_requires_pci"),
            ("M06FatRead", "M06GptPartition", "m06_fat_read_requires_gpt"),
            ("M06PackageRead", "M06FatRead", "m06_package_read_requires_fat"),
            ("M06GptPartition", "M06LiveBlockContract", "m06_gpt_partition_requires_contract"),
            ("M06LiveBlockContract", "M06VirtioBlockRead", "m06_live_block_contract_requires_read"),
            ("M06VirtioBlockRead", "M06VirtioBlockQueueSetup", "m06_block_read_requires_queue"),
            ("M06VirtioBlockQueueSetup", "M05PciEnumeration", "m06_block_queue_requires_pci"),
            ("M04ClassConstructorExecution", "M04GcStaticRootMaterialization", "m04_cctor_requires_gc_profile"),
            ("M04InterfaceDispatch", "M04ClassConstructorExecution", "m04_dispatch_requires_cctor_profile"),
            ("M04X64UnwindMetadata", "M04TypeManagerModuleRegistration", "m04_x64_unwind_metadata_requires_original_module"),
            ("M04SimpleThrowCatch", "M04GcStaticRootMaterialization", "m04_simple_catch_requires_gc_profile"),
            ("M04SingleFinallyCatch", "M04GcStaticRootMaterialization", "m04_single_finally_requires_gc_profile"),
            ("M04Rethrow", "M04GcStaticRootMaterialization", "m04_rethrow_requires_gc_profile"),
            ("M04NestedFinallyCatch", "M04GcStaticRootMaterialization", "m04_nested_finally_requires_gc_profile"),
            ("M04UnhandledException", "M04GcStaticRootMaterialization", "m04_unhandled_requires_gc_profile"),
            ("M04EhCatalog", "M04FrameDispatch", "m04_eh_catalog_requires_frame_dispatch"),
            ("M04FrameDispatch", "M04GcStaticRootMaterialization", "m04_frames_requires_gc_profile"),
            ("M04ConstructorFailure", "M04GcStaticRootMaterialization", "m04_ctor_failure_requires_gc_profile"),
            ("M04FilteredCatch", "M04GcStaticRootMaterialization", "m04_filter_requires_gc_profile"),
            ("M04NormalFinally", "M04GcStaticRootMaterialization", "m04_normal_finally_requires_gc_profile"),
            ("M04SeededCollectorChurn", "M04GcStaticRootMaterialization", "m04_churn_requires_gc_profile")
        }) if (Has(child) && !Has(parent)) return reason;
        return null;
    }

    [HostTest("efi.profile service child resolves staged parent and preserves explicit exclusions")]
    private static void ServiceChildRouting()
    {
        string[] parents = ["EnableM16002Probe", "EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
            "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe"];
        foreach (bool build in new[] { true, false })
        {
            var profile = EfiOptions.Parse(["--EnableM16003Probe"], build).Profile();
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            HostCheck.True(profile.IsEnabled("M16003") && parents.All(profile.IsEnabled));
            HostCheck.True(!profile.IsEnabled("M05MonotonicClock") && !profile.IsEnabled("M03FirmwareAdapterRevocation"));
            foreach (string parent in parents)
                HostCheck.True(ServiceProfileRejected(build, ["--EnableM16003Probe", "--" + parent + ":false"],
                    parent == "EnableM16002Probe" ? "m16_003_explicit_false_prerequisite" : "m16_002_explicit_false_prerequisite"),
                    "explicit false: " + parent);
            foreach (string conflict in new[] { "EnableM05MonotonicClockProbe", "EnableM03FirmwareAdapterRevocationProbe" })
            {
                HostCheck.True(ServiceProfileRejected(build, ["--EnableM16003Probe", "--" + conflict], "m16_002_profile_conflict"));
                HostCheck.True(ServiceProfileRejected(build, ["--EnableM16002Probe", "--" + conflict], "m16_002_profile_conflict"),
                    "legacy parent conflict: " + conflict);
            }
        }
    }

    [HostTest("efi.profile service child defaults off and preserves ordinary VerifiedExit")]
    private static void ServiceChildDefaultPolicy()
    {
        foreach (bool build in new[] { true, false })
        {
            foreach (string[] args in new string[][] { [], ["--EnableM16003Probe:false"] })
            {
                var profile = EfiOptions.Parse(args, build).Profile();
                HostCheck.Equal("VerifiedExit", profile.BootPolicy);
                HostCheck.True(!profile.IsEnabled("M16003") && !profile.IsEnabled("M16002"));
            }
            HostCheck.True(ServiceProfileRejected(build, ["--boot-policy", "VerifiedExit", "--EnableM16003Probe"],
                "clros_boot_policy_probe_conflict"));
        }
    }

    [HostTest("efi.profile startup child resolves parent and admits either or both M16 children")]
    private static void StartupChildRouting()
    {
        string[] parents = ["EnableM16002Probe", "EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
            "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe"];
        string[] allowed = [.. parents, "EnableM16003Probe", "EnableM16004Probe", "EnableM16005Probe", "EnableM16006Probe", "EnableM16007Probe", "EnableM16008Probe", "EnableM16009Probe",
            "EnableM09011Probe", "EnableM09012Probe", "EnableM16010Probe", "EnableM16011Probe", "EnableM16012Probe", "EnableM16013Probe", "EnableM10004Probe", "EnableM07027Probe", "EnableM07028Probe"];
        foreach (bool build in new[] { true, false })
        {
            foreach (string[] args in new string[][]
            {
                ["--EnableM16004Probe"],
                ["--EnableM16002Probe", "--EnableM16004Probe"],
                ["--EnableM16003Probe", "--EnableM16004Probe"],
                ["--EnableM16004Probe", "--EnableM16003Probe:false"]
            })
            {
                var profile = EfiOptions.Parse(args, build).Profile();
                HostCheck.Equal("Diagnostic", profile.BootPolicy);
                HostCheck.True(profile.IsEnabled("M16004") && parents.All(profile.IsEnabled));
                HostCheck.Equal(args.Contains("--EnableM16003Probe"), profile.IsEnabled("M16003"));
                HostCheck.True(profile.Flags.Where(flag => flag.Value).All(flag => allowed.Contains(flag.Key)));
            }
            foreach (string parent in parents)
                HostCheck.True(ServiceProfileRejected(build, ["--EnableM16004Probe", "--" + parent + ":false"],
                    parent == "EnableM16002Probe" ? "m16_004_explicit_false_prerequisite" : "m16_002_explicit_false_prerequisite"),
                    "startup explicit false: " + parent);
            foreach (string conflict in EfiContracts.DiagnosticProbes.Except(allowed))
                HostCheck.True(ServiceProfileRejected(build, ["--EnableM16004Probe", "--" + conflict],
                    conflict switch { "EnableM07026Probe" => "m07_026_profile_conflict",
                        "EnableM09010Probe" => "m09_010_profile_conflict",
                        "EnableM09009Probe" => "m09_009_profile_conflict", "EnableM09006Probe" => "m09_006_profile_conflict",
                        "EnableM09004Probe" => "m09_004_profile_conflict",
                        "EnableM09005Probe" => "m09_005_profile_conflict", _ => "m16_002_profile_conflict" }),
                    "startup profile conflict: " + conflict);
        }
    }

    [HostTest("efi.profile startup child defaults off and preserves ordinary VerifiedExit")]
    private static void StartupChildDefaultPolicy()
    {
        foreach (bool build in new[] { true, false })
        {
            foreach (string[] args in new string[][]
            {
                [], ["--EnableM16004Probe:false"], ["--EnableM16003Probe:false", "--EnableM16004Probe:false"]
            })
            {
                var profile = EfiOptions.Parse(args, build).Profile();
                HostCheck.Equal("VerifiedExit", profile.BootPolicy);
                HostCheck.True(!profile.IsEnabled("M16002") && !profile.IsEnabled("M16003") && !profile.IsEnabled("M16004"));
            }
            HostCheck.True(ServiceProfileRejected(build, ["--boot-policy", "VerifiedExit", "--EnableM16004Probe"],
                "clros_boot_policy_probe_conflict"));
        }
    }

    [HostTest("efi.profile syscall child requires startup service and preserves default and explicit exclusions")]
    private static void SyscallChildRouting()
    {
        string[] parents = ["EnableM16002Probe", "EnableM16003Probe", "EnableM16004Probe", "EnableM01LoadedImageStackProbe",
            "EnableM01LiveAllocatorProbe", "EnableAcpiPhysicalDiscoveryProbe", "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe"];
        foreach (bool build in new[] { true, false })
        {
            var profile = EfiOptions.Parse(["--EnableM16005Probe"], build).Profile();
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            HostCheck.True(profile.IsEnabled("M16005") && parents.All(profile.IsEnabled));
            HostCheck.True(profile.Flags.Where(x => x.Value).All(x => parents.Contains(x.Key) || x.Key == "EnableM16005Probe"));
            foreach (string parent in parents)
                HostCheck.True(ServiceProfileRejected(build, ["--EnableM16005Probe", "--" + parent + ":false"],
                    parent is "EnableM16002Probe" or "EnableM16003Probe" or "EnableM16004Probe" ? "m16_005_explicit_false_prerequisite" : "m16_002_explicit_false_prerequisite"));
            HostCheck.True(ServiceProfileRejected(build, ["--EnableM16005Probe", "--EnableM05MonotonicClockProbe"], "m16_002_profile_conflict"));
            HostCheck.True(ServiceProfileRejected(build, ["--boot-policy", "VerifiedExit", "--EnableM16005Probe"], "clros_boot_policy_probe_conflict"));
            HostCheck.True(!EfiOptions.Parse(["--EnableM16005Probe:false"], build).Profile().IsEnabled("M16005"));
        }
        var smoke = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", "--EnableM16005Probe"]));
        foreach (string repeated in new[] { "M16_005_CALL", "M16_005_WRITE" })
            HostCheck.True(smoke.Required.Contains(repeated) && smoke.Ordered.Contains(repeated) && !smoke.Unique.Contains(repeated));
        HostCheck.True(smoke.Unique.Contains("M16_005_EXIT") && smoke.Unique.Contains("M16_005_RESTORE"));
    }

    private static bool ServiceProfileRejected(bool build, string[] args, string reason)
    {
        try { _ = EfiOptions.Parse(args, build).Profile(); }
        catch (ToolFailure error) { return error.Message.Contains(reason, StringComparison.Ordinal); }
        return false;
    }

    // The reviewed prerequisite order of the returning M01/M02 profiles.
    private static readonly string[] ReturningChain =
    [
        "M01LoadedImageStack", "M01LiveAllocator", "M01OwnedPaging", "M02SafeStub",
        "M02CheckedPortIoBinding", "M02Com1OwnedPortIo", "M02OwnedCom1Ready",
        "M02PanicSafeOwnedCom1", "M02OwnedX64GdtEncoding", "M02GuardedIstStackAllocation"
    ];

    internal static bool Returning(string profileName, IReadOnlyList<string> required, IReadOnlyList<string> forbidden,
        string ready, string failure)
    {
        int at = Array.IndexOf(ReturningChain, profileName);
        HostCheck.True(at >= 0, "reviewed returning profile");
        string[] flags = ReturningChain[..(at + 1)].Select(n => "Enable" + n + "Probe").ToArray();
        string[] arguments = flags.Select(n => "--" + n).ToArray();
        foreach (bool build in new[] { true, false })
        {
            var profile = EfiOptions.Parse(arguments, build).Profile();
            HostCheck.True(flags.All(profile.IsEnabled), "complete explicit profile reaches build and verifier");
            foreach (string prerequisite in arguments[..^1])
            {
                HostCheck.True(HostToolProcess.ValidateEfiProfile(build, arguments.Where(a => a != prerequisite).ToArray()).ExitCode != 0,
                    "missing prerequisite refused: " + prerequisite);
                HostCheck.True(HostToolProcess.ValidateEfiProfile(build, arguments.Select(a => a == prerequisite ? a + ":false" : a).ToArray()).ExitCode != 0,
                    "explicit false prerequisite refused: " + prerequisite);
            }
        }
        QemuSmokePlan plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", .. arguments]));
        HostCheck.True(flags.All(plan.IsEnabled), "profile reaches QEMU build request");
        string[] missingRequired = required.Where(marker => !plan.Required.Contains(marker)).ToArray();
        string[] missingForbidden = forbidden.Where(marker => !plan.Forbidden.Contains(marker)).ToArray();
        HostCheck.True(missingRequired.Length == 0 && missingForbidden.Length == 0,
            "whole selected marker inventories profile=" + profileName +
            " missingRequired=[" + string.Join(" | ", missingRequired) + "]" +
            " missingForbidden=[" + string.Join(" | ", missingForbidden) + "]");
        HostCheck.True(plan.Required.Contains(ready) && plan.Forbidden.Contains(failure), "independent ready and failure records");
        HostCheck.Equal(plan.Required.Count, plan.Required.Distinct(StringComparer.Ordinal).Count());
        HostCheck.Equal(plan.Forbidden.Count, plan.Forbidden.Distinct(StringComparer.Ordinal).Count());
        var baseline = QemuProfiles.Create(QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--dry-run"));
        HostCheck.True(!baseline.Required.Contains(ready), "default marker isolation");
        foreach (string[] rejected in new string[][] { ["--dry-run", .. arguments], ["--dry-run", "--build-efi", "--skip-image-build", .. arguments] })
        {
            bool accepted = true;
            try { QemuProfiles.Create(QemuSmokeOptions.Parse(rejected)); }
            catch (ToolFailure) { accepted = false; }
            HostCheck.True(!accepted, "profile needs fresh built image");
        }
        return true;
    }
}
