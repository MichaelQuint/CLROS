using OS.Tooling;

internal static class EfiProfileTestControls
{
    // Inspect each active diagnostic branch; strip only the union of exactly
    // owned call sites so the existing raw-source firewall still sees all others.
    internal static bool RemoveOwnedDmaCalls(ref string source, string call, out string reason)
    {
        reason = string.Empty;
        var admitted = new SortedSet<(int Start, int Length)>();
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

    // Independent expectations shared by cross-profile rejection fixtures.
    internal static string? MissingParentReason(IReadOnlyCollection<string> selected)
    {
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
            ("M04SeededCollectorChurn", "M04GcStaticRootMaterialization", "m04_churn_requires_gc_profile")
        }) if (Has(child) && !Has(parent)) return reason;
        return null;
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
