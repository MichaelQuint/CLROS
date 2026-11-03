#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
#endif
#if M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;
using TickTimer = OS.Kernel.Threading.SchedulerTimer;
using Deadlines = OS.Kernel.Threading.DeadlineQueue;
#endif
#if M04_LAYOUT_CONTRACT_PROBE
using OS.Kernel.Runtime;
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Memory;
#endif
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
#if M16_004_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
#if M05_PCI_ENUMERATION_PROBE || M16_006_PROBE
using OS.Drivers;
#endif
#if M07_ARP_RECEIVE_PROBE
using OS.Net;
#if !M07_VIRTIO_NET_PROBE
#error The ARP receive profile requires the accepted original Virtio-net profile.
#endif
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
using OS.Storage;
#endif
#if M06_FAT_READ_PROBE
using FatChain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using FatDirectory = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using FatFile = OS.Storage.KnownFileSHAN256EquivalentKnownFixtureHash;
#endif
#if !(M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#error The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.
#endif
#if M01_OWNED_PAGING_PROBE || M04_MANAGED_HEAP_PAGE_RANGE_PROBE || M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE || M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
#error The first real exit is an exclusive firmware-CR3 path, with no intervening paging, heap or controlled exit probe.
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE || M02_UD2_VECTOR_STATE_PROBE || M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE || M02_DOUBLE_FAULT_IST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE
#error The first real exit cannot be combined with another terminal exception probe.
#endif
#endif
namespace OS.Boot;
#pragma warning disable 649
public static unsafe partial class EfiEntry
{
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
    // Explicit default-off profile. BootSequence consumes the prepared firmware
    // binding only after M01, copied ACPI and the ordinary boot marker plan.
    private static partial class M03FirstExitAdapter
    {
        private const ulong Page = 4096, TablePages = 55;
#if M16_008_PROBE
        private const uint CooperativePoolPages = LinuxThreadProgram.PoolPages, CooperativeCodePages = LinuxThreadProgram.CodePages;
#elif M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M07_026_PROBE
        private const uint CooperativePoolPages = Cooperative.PoolPages, CooperativeCodePages = 2;
#endif
#if M16_007_PROBE
#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE
        private const uint LinuxDescriptorPages = 70, LinuxDescriptorSlots = 32;
        private const uint LinuxDescriptorDescriptions = 0x200, LinuxDescriptorPipeMetadata = 0xc00;
        private const uint LinuxDescriptorViews = 0xcc0, LinuxDescriptorPaths = 0xe00;
        private const uint LinuxDescriptorSnapshots = 0x2000, LinuxDescriptorPipes = 0x42000, LinuxDescriptorScratch = 0x44000;
#else
        private const uint LinuxDescriptorPages = 37;
        private const uint LinuxDescriptorSlots = 16;
        private const uint LinuxDescriptorSnapshots = 0x1000, LinuxDescriptorPipes = 0x21000, LinuxDescriptorScratch = 0x23000;
        private const uint LinuxDescriptorViews = 0x6c0, LinuxDescriptorPaths = 0x800;
        private const uint LinuxDescriptorDescriptions = 0x100, LinuxDescriptorPipeMetadata = 0x600;
#endif
#endif
#if M16_002_PROBE
        private const uint LinuxLedgerCapacity =
#if M16_025_PROBE
            19;
#elif M16_011_PROBE
            18;
#elif M07_026_PROBE
            11;
#elif M16_008_PROBE
            17;
#elif M16_007_PROBE
            16;
#else
#if M16_006_PROBE
            15;
#else
#if M16_005_PROBE
            13;
#else
#if M16_004_PROBE
            11;
#else
            9;
#endif
#endif
#endif
#endif
#endif
        // Conservative reservations, not native-stack evidence. The linked
        // callback graph and firmware path still require review before enablement.
        private const ulong EntryStackBudget = 128 * 1024;
#if M10_004_PROBE
        // Web heap startup runs on the retained supervisor before native stack
        // switching; its measured mapping/admission path exceeds32KiB. This is
        // a declaration within the existing main stack, not another allocation.
        private const ulong DiagnosticStackBudget = 64 * 1024;
#else
        private const ulong DiagnosticStackBudget = 32 * 1024;
#endif

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#if M16_014_PROBE

#endif
#endif
#if M16_007_PROBE

#endif
#if M10_004_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_OWNED_ACPI_SELECTION_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE

#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M06_VIRTIO_BLOCK_READ_PROBE

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#if M06_GPT_PARTITION_PROBE

#if M06_FAT_READ_PROBE

#if M06_PACKAGE_READ_PROBE

#endif
#endif
#endif

#endif
#endif
#endif
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE

#endif
#if M07_VIRTIO_NET_PROBE

#if M07_ARP_RECEIVE_PROBE

#endif
#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#if M06_BLOCK_MSIX_PROBE

#endif
#endif
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#if M10_004_PROBE

#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE

#endif
#endif
#endif
#if M16_003_PROBE

#endif
#if M07_026_PROBE

#endif
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE

#endif
#endif
#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE

#endif

        private static Storage s_storage;

        // Reuse the existing no-CRT EFI convention: NativeAOT reads this
        // retained RX symbol as fixed cookie storage and emits its own checks.
        // Never call it. Randomized cookie initialization remains hardening
        // work; this anchor only supplies the compiler-required storage.
        [System.Runtime.RuntimeExport("__security_cookie")]
        private static ulong SecurityCookieStorageAnchor() => 0x434c_524f_5347_5331UL;

        internal static bool CaptureOriginalEntry(void* imageHandle, EfiSystemTable* table)
        {
            if (s_storage.Phase != 0)
                return false;
            s_storage.Phase = 1;
            if (imageHandle == null || table == null || ((ulong)table & 7) != 0)
                return false;
            s_storage.ImageHandle = imageHandle;
            s_storage.SystemTable = table;
            return true;
        }

        internal static void Run(EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial, EfiBootServices* services)
        {
            // TEST_START m03.007; this is before the final map/exit window.
            for (int i = 0; i < 20; i++)
            {
                ulong word = (i / 8) switch
                {
                    0 => 0x4154535f54534554UL,
                    1 => 0x302e33306d205452UL,
                    _ => 0x000000000a0d3730UL
                };
                Write(console, serial, (byte)(word >> ((i % 8) * 8)));
            }
            fixed (Storage* state = &s_storage)
            {
                if (!Prepare(state, services))
                {
#if M16_005_PROBE
                    bool syscallReleased = ReleaseLinuxUserBoundary(state, services);
                    string syscallRejection = syscallReleased ? "M16_005_PREPARE_REJECTED owned-backing=released\r\n" : "M16_005_PREPARE_REJECTED owned-backing=reserved-release-failed\r\n";
                    for (int i = 0; i < syscallRejection.Length; i++)
                        Write(console, serial, (byte)syscallRejection[i]);
#endif
#if M16_004_PROBE
                    bool startupReleased = ReleaseLinuxStartup(state, services);
                    string startupRejection = startupReleased ? "M16_004_PREPARE_REJECTED owned-backing=released\r\n" : "M16_004_PREPARE_REJECTED owned-backing=reserved-release-failed\r\n";
                    for (int i = 0; i < startupRejection.Length; i++)
                        Write(console, serial, (byte)startupRejection[i]);
#endif
#if M16_002_PROBE
                    bool released = state->LinuxPackage == 0;
                    if (!released && services != null && services->FreePages != null && services->FreePages(state->LinuxPackage, state->LinuxPages) == 0)
                    {
                        state->LinuxPackage = 0;
                        released = true;
                    }
                    string rejection = released ? "M16_002_STAGE_REJECTED executable=false backing=released\r\n" : "M16_002_STAGE_REJECTED executable=false backing=reserved-release-failed\r\n";
                    for (int i = 0; i < rejection.Length; i++)
                        Write(console, serial, (byte)rejection[i]);
                    string handles = "M16_002_REJECTION_HANDLES closed=";
                    for (int i = 0; i < handles.Length; i++)
                        Write(console, serial, (byte)handles[i]);
                    WriteUnsignedDecimal(console, serial, state->LinuxClosedHandles);
                    WriteCrLf(console, serial);
#endif
                    // Only this pre-entry branch may use firmware output. Partial
                    // allocations remain reserved until VM teardown on failure.
                    state->Phase = 5;
#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE
                    WriteRealtimeAdmissionRejection(state, console, serial);
#endif
                    WritePreparationFailure(console, serial, state->PreparationCheckpoint, state->TableMemoryType);
                    return;
                }
#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE
                // A real empty OutputString succeeded through the admitted handle.
                ulong* admitted = stackalloc ulong[4];
                admitted[0] = 0x504144415f33304dUL;
                admitted[1] = 0x494d44415f524554UL;
                admitted[2] = 0x3020312044455454UL;
                admitted[3] = 0x0000000000000a0dUL;
                for (int i = 0; i < 26; i++)
                    Write(console, serial, ((byte*)admitted)[i]);
#endif
                state->Phase = 3;
                ((delegate* unmanaged<FirstExitOwnedStackContext*, void>)
                    (state->Memory.Code + FirstExitCodeBundle.StackEntryOffset))(state->Arena.StackEntry);
                // Native entry is terminal, even on refusal. Never return to the
                // boot plan or use firmware when that contract is violated.
                while (true)
                {
                }
            }
        }

        private static bool Prepare(Storage* state, EfiBootServices* services)
        {
            state->PreparationCheckpoint = 10;
            if (state->Phase != 1 || state->ImageHandle == null || state->SystemTable == null ||
                services == null || services != state->SystemTable->BootServices || services->AllocatePages == null ||
                !M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var capture, out var mapping) ||
                !M01StageOneAdapter.TryGetOriginalExitStackProbe(out ulong stackProbePage) ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong allocatorState, out var allocator))
                return false;
            state->Phase = 2;
            FirstExitImageReferences references = new FirstExitImageReferences
            {
                EntryCallback = (ulong)(delegate* unmanaged<FirstExitOwnedStackContext*, void>)&Enter,
                DiagnosticCallback = (ulong)(delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void>)&Diagnose,
                FaultCallback = (ulong)(delegate* unmanaged<FirstExitFaultFrame*, FirstExitFaultContext*, ulong, ulong, void>)&Fault,
                AdapterState = (ulong)state,
                AdapterStateBytes = (ulong)sizeof(Storage),
                AllocatorState = allocatorState,
                AllocatorStateBytes = M01LiveAllocatorStaticState.RequiredStorageBytes,
                RuntimeSupportCode = stackProbePage,
                RuntimeSupportCodeBytes = Page
            };
            state->PreparationCheckpoint = 20;
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (workspace->Phase != 3 || !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity))
                    return false;
                references.CopiedAcpi = (ulong)copied;
                references.CopiedAcpiBytes = (ulong)sizeof(AcpiBootstrapOwnedBuffer);
#if M05_PCI_ENUMERATION_PROBE
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(
                    copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                if (!selected.Succeeded || selected.Mcfg.Length < 60)
                    return false;
                byte* mcfg = copied->Bytes + 16 + selected.Mcfg.Offset;
                byte firstBus = mcfg[54];
                if (!ECAMPageVendorDeviceHeader.TryWindow(mcfg, selected.Mcfg.Length, 0, firstBus, firstBus, out state->PciWindow) ||
                    !ECAMPageVendorDeviceHeader.AdmitWindow(state->PciWindow, s_bootHandoff.MemoryMap))
                    return false;
                references.EcamStart = state->PciWindow.Start;
                references.EcamPages = state->PciWindow.Bytes / 4096;
#if M05_APIC_SETUP_PROBE
                // The diagnostic fixture admits only its single declared BSP 0.
                // Actual CPUID/MSR identity is required again after exit before MMIO.
                state->MadtAddress = (ulong)(copied->Bytes + 16 + selected.Madt.Offset);
                state->MadtBytes = selected.Madt.Length;
                if (MADTLocalAPICIOAPICSeparatelyPollingProof.Parse((byte*)state->MadtAddress, state->MadtBytes, 0,
                        out state->ApicTopology) != MadtPollingStatus.Success ||
                    !CommonNotifyISRDeviceConfigurationRegions.AdmitMmioPages(state->ApicTopology.LocalApicAddress, 1, s_bootHandoff.MemoryMap) ||
                    !CommonNotifyISRDeviceConfigurationRegions.AdmitMmioPages(state->ApicTopology.IoApicAddress, 1, s_bootHandoff.MemoryMap))
                    return false;
                references.LocalApicPage = state->ApicTopology.LocalApicAddress;
                references.IoApicPage = state->ApicTopology.IoApicAddress;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
                if (!PrepareVirtioMapping(state, out references.VirtioBarStart))
                    return false;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
                if (!PrepareMsixMapping(state, out references.MsixPage))
                    return false;
#endif
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
                var clockSelection = OwnedACPIDirectorySelectMADTHPETMCFG.Select(
                    copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                if (!clockSelection.Succeeded || !OwnedMonotonicClock.TryReadDescription(
                        copied->Bytes + 16 + clockSelection.Hpet.Offset, clockSelection.Hpet.Length, out state->HpetDescription) ||
                    !OwnedMonotonicClock.AdmitDescription(state->HpetDescription, s_bootHandoff.MemoryMap))
                    return false;
                references.HpetDevicePage = state->HpetDescription.Address;
#endif
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
                state->AcpiWorkspace = (ulong)workspace;
                state->AcpiWorkspaceBytes = (ulong)sizeof(AcpiBootstrapWorkspace);
#endif
            }
            state->PreparationCheckpoint = 30;
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            if (!FirstExitImageMappings.WriteRanges(capture, mapping, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count))
                return false;
            state->PreparationCheckpoint = 301;
            if (!FirstExitImageMappings.Contains(ranges, count, references.AdapterState, references.AdapterStateBytes, PageProtection.Read | PageProtection.Write))
                return false;
            state->PreparationCheckpoint = 302;
            if (!FirstExitImageMappings.Contains(ranges, count, references.CopiedAcpi, references.CopiedAcpiBytes, PageProtection.Read | PageProtection.Write))
                return false;
            state->PreparationCheckpoint = 303;
            if (!FirstExitImageMappings.Contains(ranges, count, references.AllocatorState, references.AllocatorStateBytes, PageProtection.Read | PageProtection.Write))
                return false;
            ulong image = ranges[0].Start, imageBytes = capture.Range.ImageSize;
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
            state->PreparationCheckpoint = 34;
            if (!BootSequence.PrepareCooperativeSwitch(state->Phase == 2, state, &PrepareCooperativePool))
                return false;
            references.CooperativePoolStart = state->CooperativePool;
            references.CooperativePoolPages = CooperativePoolPages;
#endif
#if M16_002_PROBE
            state->PreparationCheckpoint = 35;
            if (!BootSequence.StageLinuxElfPackage(state->Phase == 2, state, &ReadLinuxPackage))
                return false;
            references.LinuxPackageStart = state->LinuxPackage;
            references.LinuxPackagePages = state->LinuxPages;
#endif
#if M07_026_PROBE
            if (!BootSequence.PrepareNetworkService(state->Phase == 2, state, &PrepareNetworkArena))
                return false;
            references.NetworkArenaStart = state->NetworkArena;
            references.NetworkArenaPages = 8;
#endif
#if M16_004_PROBE
            state->PreparationCheckpoint = 36;
            if (!BootSequence.PrepareLinuxElfStartup(state->Phase == 2, state, &PrepareLinuxStartup))
                return false;
            references.LinuxBackingStart = state->LinuxImageBacking;
            references.LinuxBackingPages = state->LinuxImagePages;
            references.LinuxUserTablesStart = state->LinuxUserTables;
            references.LinuxUserTablePages = X64UserAddressSpace.MaximumTables;
#endif
#if M16_005_PROBE
            state->PreparationCheckpoint = 37;
            if (!BootSequence.PrepareLinuxUserBoundary(state->Phase == 2, state, &PrepareLinuxUserBoundary))
                return false;
            references.LinuxSyscallCodeStart = state->LinuxSyscallCode;
            references.LinuxSyscallDataStart = state->LinuxSyscallData;
#endif
#if M16_006_PROBE
            state->PreparationCheckpoint = 38;
            if (!BootSequence.PrepareLinuxMemory(state->Phase == 2, state, &PrepareLinuxMemory))
                return false;
            references.LinuxMemoryPoolStart = state->LinuxMemoryPool;
            references.LinuxMemoryPoolPages = LinuxMemoryPoolPages(state);
            references.LinuxMemoryCandidateStart = state->LinuxMemoryCandidate;
#endif
#if M16_007_PROBE
            state->PreparationCheckpoint = 39;
            if (!BootSequence.PrepareLinuxDescriptors(state->Phase == 2, state, &PrepareLinuxDescriptors))
                return false;
            references.LinuxDescriptorStart = state->LinuxDescriptorArena;
            references.LinuxDescriptorPages = LinuxDescriptorPages;
#endif
#if M16_027_PROBE
            if (!BootSequence.PrepareOwnedWritableDisk(state->Phase == 2, state, &PrepareNativeWritableDiskBacking) ||
                !GetNativeWritableDiskBacking(state, out references.NativeDiskArenaStart,
                    out references.NativeDiskArenaPages, out references.NativeDiskBarStart))
                return false;
#elif M16_025_PROBE
            if (!BootSequence.PrepareOwnedReadOnlyDisk(state->Phase == 2, state, &PrepareNativeDiskBacking))
                return false;
            fixed (NativeDiskOwner* disk = &s_nativeDisk)
            {
                if (disk->Boot != state || disk->Phase != 1)
                    return false;
                references.NativeDiskBarStart = disk->Bar;
                references.NativeDiskArenaStart = disk->Arena;
                references.NativeDiskArenaPages = disk->ArenaPages;
            }
#endif
            state->PreparationCheckpoint = 40;
            if (!Allocate(services, 1, 3, image, imageBytes, allocator, out ulong code) ||
                !Allocate(services, 2, FirstExitOwnedArena.PageCount, image, imageBytes, allocator, out ulong data) ||
                !Disjoint(code, FirstExitCodeBundle.Bytes, data, FirstExitOwnedArena.Bytes) ||
                !Allocate(services, 2, TablePages, image, imageBytes, allocator, out ulong tables) ||
                !Disjoint(code, FirstExitCodeBundle.Bytes, tables, TablePages * Page) ||
                !Disjoint(data, FirstExitOwnedArena.Bytes, tables, TablePages * Page) ||
                !Disjoint(code, FirstExitCodeBundle.Bytes, stackProbePage, Page) ||
                !Disjoint(data, FirstExitOwnedArena.Bytes, stackProbePage, Page) ||
                !Disjoint(tables, TablePages * Page, stackProbePage, Page))
                return false;

#if M16_002_PROBE
            if (!Disjoint(state->LinuxPackage, state->LinuxPages * Page, code, FirstExitCodeBundle.Bytes) ||
                !Disjoint(state->LinuxPackage, state->LinuxPages * Page, data, FirstExitOwnedArena.Bytes) ||
                !Disjoint(state->LinuxPackage, state->LinuxPages * Page, tables, TablePages * Page) ||
                !Disjoint(state->LinuxPackage, state->LinuxPages * Page, stackProbePage, Page) ||
                !SealLinuxLedger(state, code, data, tables))
                return false;
#endif

#if M05_PHYSICAL_MAPPING_PROBE
            if (!Allocate(services, 2, 1023, image, imageBytes, allocator, out ulong scratch) ||
                !Disjoint(scratch, 1023 * Page, code, FirstExitCodeBundle.Bytes) ||
                !Disjoint(scratch, 1023 * Page, data, FirstExitOwnedArena.Bytes) ||
                !Disjoint(scratch, 1023 * Page, tables, TablePages * Page) ||
                !Disjoint(scratch, 1023 * Page, stackProbePage, Page))
                return false;
            references.MappingScratchBacking = scratch;
            references.MappingScratchBackingPages = 1023;
            references.MappingScratchStart = (scratch + 0x1fffff) & ~0x1fffffUL;
#endif

            state->PreparationCheckpoint = 50;
            if (!Query(code, out var cpu, out ulong cs, out var conditions))
                return false;
#if M05_PHYSICAL_MAPPING_PROBE
            state->MappingCr0 = cpu.Cr0;
#endif
            state->PreparationCheckpoint = 60;
            if (!FirstExitOwnedArena.Materialize(ref state->Arena, (byte*)data, FirstExitOwnedArena.Bytes, code, (ushort)cs) ||
                !FirstExitCodeBundle.Materialize((byte*)code, FirstExitCodeBundle.Bytes,
                    ref state->Arena, capture, mapping, references.FaultCallback) ||
                PhysicalAddress.TryCreateFromUefiBoundary(tables, out var root) != AddressValueStatus.Success)
                return false;
#if M16_005_PROBE
#if M16_006_PROBE
            state->LinuxDispatchRoot = tables;
#endif
            state->LinuxSyscallPlan = LinuxUserTransitionPlan(state, 0x70000000);
            if (!X64LinuxUserTransition.Emit((byte*)state->LinuxSyscallCode, X64LinuxUserTransition.CodeBytes, state->LinuxSyscallPlan) ||
                !X64LinuxUserTransition.ValidateReadBack((byte*)state->LinuxSyscallCode, X64LinuxUserTransition.CodeBytes, state->LinuxSyscallPlan))
                return false;
#endif

            state->PreparationCheckpoint = 70;
            state->Memory = new FirstExitDiagnosticMemory
            {
                Code = (byte*)code,
                Capture = capture,
                Mapping = mapping,
                References = references,
                Allocator = allocator,
                Tables = new SeparatePageFaultPageTableRequest(root, TablePages, conditions),
                Pages = (X64PageTableImagePage*)state->PageMetadata,
                PageCapacity = TablePages,
                Entries = (ulong*)tables,
                EntryCapacity = TablePages * 512
            };
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
            if (!AdmitOwnedAcpiState(state))
                return false;
#endif
            for (ulong i = 0; i < TablePages * 512; i++)
                state->Memory.Entries[i] = 0;
            state->PreparationCheckpoint = 71;
            if (!FirstExitRetainedMappings.WriteRanges(ref state->Arena, (byte*)code, capture, mapping,
                    references, allocator, state->Memory.Tables, ranges, FirstExitRetainedMappings.MaximumRanges, out count))
                return false;
            var retained = SeparateDisabledDefaultPageFaultPageTables.BuildRetainedImage(state->Memory.Tables,
                    ranges, count, FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, TablePages,
                    state->Memory.Entries, TablePages * 512);
            state->PreparationCheckpoint = 7200 + (ulong)retained.Status;
            if (!retained.Succeeded)
                return false;
            state->PreparationCheckpoint = 73;
            if (!FirstExitOwnedStackStub.Prepare(ref *state->Arena.StackEntry, cpu.RawCr3, cpu.RawCr4,
                    state->Arena.StackBottom, state->Arena.StackTop, FirstExitOwnedArena.LowerCanary,
                    FirstExitOwnedArena.UpperCanary, EntryStackBudget, &Enter, state, (ulong)sizeof(Storage)))
                return false;
            state->PreparationCheckpoint = 74;
            if (!FirstExitPermanentCr3Stub.Prepare(ref *state->Arena.Permanent, cpu.RawCr3, tables, cpu.RawCr4,
                    state->Arena.StackBottom, state->Arena.StackTop, DiagnosticStackBudget,
                    FirstExitOwnedArena.LowerCanary, FirstExitOwnedArena.UpperCanary, &Diagnose, state, state->Arena.Descriptors))
                return false;

            // Original firmware table header sizes are admitted against the
            // already copied map before the binder reads their complete CRCs.
            FirmwareMemoryMapRecord map = s_bootHandoff.MemoryMap;
            state->PreparationCheckpoint = 81;
            if (!TableExtent((byte*)state->SystemTable, 120, map, out ulong systemBytes, out state->TableMemoryType))
                return false;
            state->PreparationCheckpoint = 82;
            if (!TableExtent((byte*)services, 240, map, out ulong bootBytes, out state->TableMemoryType))
                return false;
            FirstExitBindingStatus binding = FirstExitFirmwareBinding.Bind(ref *state->Arena.Binding,
                FirmwareServicesMode.BootServicesAvailable, state->ImageHandle,
                (byte*)state->SystemTable, systemBytes, (byte*)services, bootBytes, map);
            state->PreparationCheckpoint = 830 + (ulong)binding;
            if (binding != FirstExitBindingStatus.Ready)
                return false;
            state->PreparationCheckpoint = 90;
            if (!FirstExitCodeBundle.ValidateReadBack((byte*)code, FirstExitCodeBundle.Bytes, ref state->Arena,
                    capture, mapping, references.FaultCallback) ||
                !SeparateDisabledDefaultPageFaultPageTables.AuditRetainedImage(state->Memory.Tables,
                    ranges, count, FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, TablePages,
                    state->Memory.Entries, TablePages * 512).Succeeded ||
                X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie ||
                !state->Arena.Fault->Backend.ConfigureDefault().Succeeded)
                return false;
#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE
            state->PreparationCheckpoint = 95;
            if (!PrepareAdapters(state, services))
                return false;
#endif
#if M10_004_PROBE
            state->PreparationCheckpoint = 96;
            if (!BootSequence.PrepareOwnedRealtime(state->Phase == 2, state, &PrepareRealtime))
                return false;
#endif
            state->ImageHandle = null;
            state->SystemTable = null;
            state->PreparationCheckpoint = 100;
            return true;
        }

#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE

        private static bool PrepareAdapters(Storage* state, EfiBootServices* services)
        {
            if ((!BootSequence.IsFirmwareAdapterRevocationEnabled() && !BootSequence.IsWorkloadServiceLoopEnabled()) || state->SystemTable->ConOut == null ||
                state->SystemTable->ConOut->OutputString == null)
                return false;
            state->AdapterConsole = state->SystemTable->ConOut;
            state->AdapterServices = services;
            FirmwareAdapterBackends backends = new FirmwareAdapterBackends
            {
                Context = state,
                ContextBytes = (ulong)sizeof(Storage),
                Console = &AdapterConsole,
                MemoryMap = &AdapterMemoryMap,
                FileSystem = &AdapterFileSystem,
                Timer = &AdapterTimer,
                Random = &AdapterRandom,
                ConfigurationTable = &AdapterConfiguration,
                PhysicalMemory = &AdapterPhysicalMemory,
                Framebuffer = &AdapterFramebuffer
            };
            // The original retained owner is essential: never bind a copied arena.
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Bind(ref state->AdapterAuthority,
                &state->Arena, state->Memory.References, backends) != FirmwareAdapterStatus.Success)
                return false;
            state->OriginalHandles = new AdapterHandles
            {
                Console = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Console(ref state->AdapterAuthority),
                MemoryMap = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.MemoryMap(ref state->AdapterAuthority),
                FileSystem = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.FileSystem(ref state->AdapterAuthority),
                Timer = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Timer(ref state->AdapterAuthority),
                Random = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Random(ref state->AdapterAuthority),
                ConfigurationTable = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.ConfigurationTable(ref state->AdapterAuthority),
                PhysicalMemory = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.PhysicalMemory(ref state->AdapterAuthority),
                Framebuffer = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Framebuffer(ref state->AdapterAuthority)
            };
            state->CopiedHandles = state->OriginalHandles;
            char empty = '\0';
            FirmwareAdapterCallResult positive = state->OriginalHandles.Console.Write(&empty, 1);
            return positive.Succeeded && positive.FirmwareStatus == 0 && state->AdapterEntries[0] == 1 && AdapterEntryCount(state) == 1;
        }

        // Every typed thunk counts entry before reading its firmware provider. The
        // optional providers are explicitly unavailable; this profile does not
        // discover filesystem, RNG or GOP protocols or promise functional services.
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterConsole(void* context, char* text, ulong characters)
        {
            Storage* state = (Storage*)context;
            state->AdapterEntries[0]++;
            if (characters == 0 || text[characters - 1] != '\0')
                return 0x8000000000000002UL;
            return state->AdapterConsole->OutputString(state->AdapterConsole, (ushort*)text);
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterMemoryMap(void* context, byte* buffer, ulong capacity, FirmwareAdapterMemoryMapInfo* info)
        {
            Storage* state = (Storage*)context;
            state->AdapterEntries[1]++;
            ulong bytes = capacity, key = 0, stride = 0;
            uint version = 0;
            ulong status = state->AdapterServices->GetMemoryMap(&bytes, buffer, &key, &stride, &version);
            info->Bytes = bytes;
            info->MapKey = key;
            info->DescriptorBytes = stride;
            info->DescriptorVersion = version;
            info->Reserved = 0;
            return status;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterFileSystem(void* context, ulong handle, ulong offset, byte* buffer, ulong capacity, ulong* actual)
        {
            ((Storage*)context)->AdapterEntries[2]++;
            return 0x8000000000000003UL;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterTimer(void* context, ulong* value)
        {
            Storage* state = (Storage*)context;
            state->AdapterEntries[3]++;
            void* entry = state->AdapterServices->GetNextMonotonicCount;
            return entry == null ? 0x8000000000000003UL : ((delegate* unmanaged<ulong*, ulong>)entry)(value);
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterRandom(void* context, byte* buffer, ulong bytes)
        {
            ((Storage*)context)->AdapterEntries[4]++;
            return 0x8000000000000003UL;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterConfiguration(void* context, ulong low, ulong high, ulong* table)
        {
            ((Storage*)context)->AdapterEntries[5]++;
            return 0x8000000000000003UL;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterPhysicalMemory(void* context, ulong address, byte* buffer, ulong bytes)
        {
            ((Storage*)context)->AdapterEntries[6]++;
            return 0x8000000000000003UL;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static ulong AdapterFramebuffer(void* context, FirmwareAdapterFramebufferInfo* info)
        {
            ((Storage*)context)->AdapterEntries[7]++;
            return 0x8000000000000003UL;
        }

        private static ulong AdapterEntryCount(Storage* state)
        {
            ulong count = 0;
            for (int i = 0; i < 8; i++)
                count += state->AdapterEntries[i];
            return count;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool ReportRevokedAdapters(Storage* state)
        {
            if (state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed || AdapterEntryCount(state) != 1 ||
                !WriteAdapterMarker(state, 0, 0))
                return false;
            for (uint index = 0; index < 8; index++)
            {
                if (!DenyCopiedFirmwareAdapter(state, index))
                {
                    _ = WriteAdapterMarker(state, 4, 0);
                    return false;
                }
                if (!WriteAdapterMarker(state, 1, index))
                    return false;
            }
            return WriteAdapterMarker(state, 2, 0) && WriteAdapterMarker(state, 3, 0);
        }

        private static bool DenyCopiedFirmwareAdapter(Storage* state, uint index)
        {
            if (index >= 8 || AdapterEntryCount(state) != 1)
                return false;
            byte* output = stackalloc byte[128];
            for (int j = 0; j < 128; j++)
                output[j] = 0xa5;
            // Denial precedes output-range checks and the retained first-party
            // provider thunk. All addresses here belong to the original stack.
            FirmwareAdapterCallResult result = index switch
            {
                0 => state->CopiedHandles.Console.Write((char*)output, 1),
                1 => state->CopiedHandles.MemoryMap.Capture(output, 64, (FirmwareAdapterMemoryMapInfo*)(output + 64)),
                2 => state->CopiedHandles.FileSystem.Read(1, 0, output, 64, (ulong*)(output + 64)),
                3 => state->CopiedHandles.Timer.Read((ulong*)output),
                4 => state->CopiedHandles.Random.Fill(output, 64),
                5 => state->CopiedHandles.ConfigurationTable.Find(0, 0, (ulong*)output),
                6 => state->CopiedHandles.PhysicalMemory.Read(0, output, 64),
                _ => state->CopiedHandles.Framebuffer.Snapshot((FirmwareAdapterFramebufferInfo*)output)
            };
            bool denied = result.Status == FirmwareAdapterStatus.ServicesUnavailable && !result.Succeeded &&
                result.FirmwareStatus == 0 && AdapterEntryCount(state) == 1;
            if (!denied)
                return false;
            for (int j = 0; j < 128; j++)
                if (output[j] != 0xa5)
                    return false;
            for (int j = 0; j < sizeof(AdapterHandles); j++)
                if (((byte*)&state->OriginalHandles)[j] != ((byte*)&state->CopiedHandles)[j])
                    return false;
            return FirstExitOwnedArena.ValidateCanaries(state->Arena);
        }

        private static bool WriteAdapterMarker(Storage* state, uint marker, uint index)
        {
            if (!FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            ulong* words = stackalloc ulong[4];
            uint bytes;
            if (marker == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e33306d205452UL;
                words[2] = 0x000000000a0d3830UL;
                bytes = 20;
            }
            else if (marker == 1 && index < 8)
            {
                words[0] = 0x504144415f33304dUL;
                words[1] = 0x494e45445f524554UL;
                words[2] = 0x3020342030204445UL;
                words[3] = 0x000000000a0d3120UL;
                ((byte*)words)[19] = (byte)('0' + index);
                bytes = 28;
            }
            else if (marker == 2)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3830305f33UL;
                words[2] = 0x0000000a0d594441UL;
                bytes = 21;
            }
            else if (marker == 3)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e33306d2053UL;
                words[2] = 0x00000000000a0d38UL;
                bytes = 19;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e33306d204cUL;
                words[2] = 0x61636f7665722038UL;
                words[3] = 0x00000a0d6e6f6974UL;
                bytes = 30;
            }
            Com1OwnedPortIoBackend backend = state->Arena.Fault->Backend;
            for (uint i = 0; i < bytes; i++)
            {
                bool ready = false;
                for (int poll = 0; poll < PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit; poll++)
                {
                    Com1OwnedPortIoOperationResult read = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                    if (!read.Succeeded)
                        return false;
                    if ((read.Value & PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask) != 0)
                    {
                        ready = true;
                        break;
                    }
                }
                if (!ready || !backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, ((byte*)words)[i]).Succeeded)
                    return false;
            }
            return true;
        }
#endif

        private static bool Allocate(EfiBootServices* services, uint type, ulong pages,
            ulong image, ulong imageBytes, SeparatePageFaultMappingRange allocator, out ulong address)
        {
            ulong candidate = 0xffffffff; // AllocateMaxAddress, below the supported four-GiB root limit.
            address = 0;
            if (services->AllocatePages(1, type, pages, &candidate) != 0 ||
                candidate < Page || (candidate & (Page - 1)) != 0 || candidate >= 0x100000000UL - pages * Page ||
                !Disjoint(candidate, pages * Page, image, imageBytes) ||
                !Disjoint(candidate, pages * Page, allocator.Start, allocator.PageCount * Page))
                return false;
            address = candidate;
            return true;
        }

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
        // Pre-exit reservation only. Retain every successful firmware allocation
        // before validating its extent; this slice supplies no post-exit release.
        private static bool PrepareCooperativePool(void* context)
        {
            Storage* state = (Storage*)context;
            if (state == null || state->Phase != 2 || state->CooperativePool != 0 || state->SystemTable == null ||
                !M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var capture, out _) ||
                capture.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out _, out var allocator))
                return false;
            EfiBootServices* services = state->SystemTable->BootServices;
            if (services == null || services->AllocatePages == null)
                return false;
            ulong candidate = 0xffffffff;
            ulong pages = CooperativePoolPages, bytes = pages * Page;
            if (services->AllocatePages(1, 2, pages, &candidate) != 0)
                return false;
            state->CooperativePool = candidate;
            if (candidate < Page || (candidate & (Page - 1)) != 0 || candidate > 0x100000000UL - bytes ||
                !Disjoint(candidate, bytes, image, capture.Range.ImageSize) ||
                !Disjoint(candidate, bytes, allocator.Start, allocator.PageCount * Page))
                return false;
            for (ulong i = 0; i < bytes; i++)
                ((byte*)candidate)[i] = 0;
            return true;
        }

        // Private IRQ-off owner path. The out target is a distinct caller local.
        // Strict decoding admits supervisor WB tables with RW/NX-clear parents;
        // all targets stay in the original retained arena, never a foreign table.
        private static bool CooperativeFindLeaf(Storage* state, ulong address, out ulong* leaf, bool ownedIst = false)
        {
            leaf = null;
            if (!CooperativeAdmitPool(state, ownedIst, out var memory, out ulong root))
                return false;
            return CooperativeWalkPoolLeaf(memory, state->CooperativePool, root, address, out leaf);
        }

        // Authenticate one IRQ-off, single-BSP operation. These observations
        // are call-local: no code, descriptor, root or borrow validity survives
        // a transfer, callback, mapping change or return to the caller.
        private static bool CooperativeAdmitPool(Storage* state, bool ownedIst, out FirstExitDiagnosticMemory memory, out ulong root)
        {
            memory = default;
            root = 0;
            fixed (Storage* retained = &s_storage)
                if (state != retained)
                    return false;
            if (state->Phase != 4 || !state->Arena.IsReady ||
                state->Arena.Transition->Mode != FirmwareServicesMode.BootServicesExited ||
                !state->Arena.Transition->RetainedFirmwareCallbacksCleared || !state->Arena.Binding->RetainedPointersCleared)
                return false;
            memory = state->Memory;
            ulong pool = state->CooperativePool, poolBytes = CooperativePoolPages * Page;
            if (pool < Page || (pool & (Page - 1)) != 0 || pool > 0x100000000UL - poolBytes ||
                memory.References.CooperativePoolStart != pool || memory.References.CooperativePoolPages != CooperativePoolPages ||
                memory.Tables.ArenaPageCount != TablePages || memory.PageCapacity != TablePages || memory.EntryCapacity != TablePages * 512 ||
                memory.Pages != (X64PageTableImagePage*)state->PageMetadata ||
                memory.Tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out root) != AddressValueStatus.Success ||
                root != (ulong)memory.Entries || root < Page || root > 0x100000000UL - TablePages * Page || (root & (Page - 1)) != 0 ||
                !Disjoint(pool, poolBytes, root, TablePages * Page) ||
                !Disjoint(pool, poolBytes, (ulong)memory.Code, FirstExitCodeBundle.Bytes) ||
                !Disjoint(pool, poolBytes, (ulong)state, (ulong)sizeof(Storage)) ||
                !Disjoint(pool, poolBytes, (ulong)state->Arena.Storage, FirstExitOwnedArena.Bytes) ||
                SeparateDisabledDefaultPageFaultPageTables.ValidateRequest(memory.Tables) != SeparatePageFaultPageTableStatus.Success ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            ulong expectedRsp0 = state->Arena.StackTop, expectedIst4 = 0;
#if M16_008_PROBE
            // Derive candidate tops from retained ownership, never live TSS
            // values. Authenticate the actual callback below, after verifying
            // the code bytes that will make the CPU observations.
            bool gatewayActive = state->LinuxSyscallPhase == 4 &&
                *(ulong*)(state->LinuxSyscallData + X64LinuxUserTransition.PhaseOffset) == 1;
            if (gatewayActive)
                expectedRsp0 = state->LinuxSyscallData + X64LinuxUserTransition.DataBytes;
#if M16_009_PROBE
            if (state->CooperativePhase == 2 &&
                ((LinuxNativeControl*)(pool + LinuxThreadProgram.MetadataPage * Page))->UserTimer != 0)
                expectedIst4 = pool + LinuxThreadProgram.IrqTopPage * Page;
#endif
#endif
            if (!FirstExitCodeBundle.ValidateInstalledReadBack(memory.Code, FirstExitCodeBundle.Bytes, ref state->Arena,
                memory.Capture, memory.Mapping, memory.References.FaultCallback, expectedRsp0, expectedIst4))
                return false;
            ulong* observed = stackalloc ulong[(int)Cooperative.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(observed);
            bool ownedDataSelectors = (observed[5] == 16 || ownedIst && observed[5] == 0) && observed[17] == 16 && observed[18] == 16;
#if M16_008_PROBE
            // The active Linux gateway deliberately establishes NULL DS/ES;
            // an IST capture also has NULL SS until full gateway restoration.
            // Admit that envelope only after authenticating its actual saved
            // selectors, descriptors, MSRs, root, phase and callback CPU state.
            bool gatewayCallback = observed[17] == 0 && observed[18] == 0 && state->LinuxSyscallPhase == 4 &&
                state->LinuxSyscallPlan.UserRoot == state->LinuxUserRoot.Root && state->LinuxSyscallPlan.KernelRoot == root &&
                X64LinuxUserTransition.ValidateCallback((byte*)state->LinuxSyscallData,
                    X64LinuxUserTransition.DataBytes, root, observed[1], observed[3], observed[4], observed[5], observed[6],
                    observed[12], observed[13], observed[14], observed[15], observed[16]);
            if (gatewayActive != gatewayCallback)
                return false;
            if (gatewayCallback)
                ownedDataSelectors = true;
#if M16_009_PROBE
            if (state->CooperativePhase == 2)
            {
                LinuxNativeControl* c = LinuxNativeOwner(state);
                if (c == null)
                    return false;
                // IST4 remains borrowed across gateway restoration and the
                // ensuing CALL capture, until StopUserTimer completes.
                if (c->UserTimer != 0)
                {
                    if (c->Selected == 0 || c->UserTimer != c->Selected || !c->Timer.IsPrepared ||
                        !LinuxNativeUserWorker(c, c->Selected - 1) ||
                        !(gatewayCallback ? LinuxNativeUserGates(c, true, allowSyscall: true) : LinuxNativeTimerInstalled(c)))
                        return false;
                }
                else if (ownedIst || gatewayCallback && state->LinuxSyscallPlan.UserInterrupts)
                    return false;
            }
#endif
#endif
            if (observed[1] != root || (observed[0] & 0x8001000dUL) != 0x80010001UL || (observed[0] & 0x60000000UL) != 0 ||
                (observed[2] & ~0x7ffUL) != 0 || (observed[2] & 0x220UL) != 0x220UL || (observed[2] & 0x80UL) != 0 ||
                (observed[3] & 2) == 0 || (observed[3] & ~Cooperative.AdmittedFlags) != 0 || (observed[3] & 0x400UL) != 0 ||
                observed[4] != 8 || observed[6] != 24 ||
                (observed[7] & 0x07000000UL) != 0x07000000UL ||
                ((observed[7] & 0x10000000UL) != 0 && ((observed[8] >> 16) & 255) != 1) ||
                observed[9] < 0x80000001UL || (observed[10] & 0x20100000UL) != 0x20100000UL ||
                (observed[11] & 0xd00UL) != 0xd00UL || observed[15] != 0 || observed[16] != 0 ||
                !ownedDataSelectors)
                return false;
            return true;
        }

        // Called only inside the operation that just admitted memory/root.
        // This read-only walk executes no callbacks and preserves every live
        // table/leaf check, including guards; the caller verifies permissions.
        private static bool CooperativeWalkPoolLeaf(FirstExitDiagnosticMemory memory, ulong pool, ulong root, ulong address, out ulong* leaf,
            X64PageTableEntryResult* leafDecoded = null, X64PageTableEncodingPreconditions* leafPreconditions = null)
        {
            leaf = null;
            if (address < pool || address - pool >= CooperativePoolPages * Page || (address & (Page - 1)) != 0)
                return false;
            ulong table = root, visited = 0;
            for (int level = 4; level >= 1; level--)
            {
                ulong page = (table - root) / Page, bit = 1UL << (int)page;
                if ((visited & bit) != 0)
                    return false;
                visited |= bit;
                ulong* slot = memory.Entries + page * 512 + ((address >> (12 + 9 * (level - 1))) & 511);
                ulong raw = *slot;
                var preconditions = memory.Tables.Preconditions;
                var decoded = X64PageTableEntry.TryDecode((X64PageTableLevel)level, raw, preconditions);
                if (!decoded.Succeeded)
                    return false;
                if (level == 1)
                {
                    if (decoded.Kind != X64PageTableEntryKind.Unmapped &&
                        (decoded.Kind != X64PageTableEntryKind.Leaf || decoded.PageSizeBytes != Page || decoded.CachePolicy != PageCachePolicy.WriteBack ||
                        decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong physical) != AddressValueStatus.Success || physical != address))
                        return false;
                    leaf = slot;
                    // Optional captures are distinct caller stack locals, valid
                    // only for this successful leaf walk and immediate audit.
                    if (leafDecoded != null)
                        *leafDecoded = decoded;
                    if (leafPreconditions != null)
                        *leafPreconditions = preconditions;
                    return true;
                }
                if (decoded.Kind != X64PageTableEntryKind.TablePointer || decoded.CachePolicy != PageCachePolicy.WriteBack ||
                    decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong next) != AddressValueStatus.Success ||
                    next < root || next - root >= TablePages * Page || (next & (Page - 1)) != 0)
                    return false;
                table = next;
            }
            return false;
        }

        // This layout is distinct from the retained 32-page M09 layout.
        private static bool CooperativeGuardPage(uint page) =>
#if M16_008_PROBE
            LinuxThreadProgram.IsGuardPage(page);
#else
            page is 4 or 13 or 14 or 16 or 17 or 19;
#endif

        // Originals and out flag are distinct caller locals on the retained main
        // stack. A complete validated snapshot precedes any publication. False may
        // leave a partial layout; the caller restores whenever originalsCaptured.
        private static bool CooperativeSetPoolMappings(Storage* state, ulong* original, bool publish, out bool originalsCaptured)
        {
            originalsCaptured = false;
            fixed (Storage* retained = &s_storage)
                if (state != retained)
                    return false;
            const ulong savedBytes = CooperativePoolPages * 8UL;
            if (original == null || ((ulong)original & 7) != 0 ||
                !FirstExitOwnedArena.InMainStack(state->Arena, (ulong)original, savedBytes))
                return false;
            ulong* captured = stackalloc ulong[(int)CooperativePoolPages];
            ulong* replacement = stackalloc ulong[(int)CooperativePoolPages];
            ulong** slots = stackalloc ulong*[(int)CooperativePoolPages];
            ulong root = (ulong)state->Memory.Entries;
            for (uint page = 0; page < CooperativePoolPages; page++)
            {
                ulong address = state->CooperativePool + page * Page;
                if (!CooperativeFindLeaf(state, address, out slots[page]))
                    return false;
                ulong saved = publish ? *slots[page] : original[page];
                var baseline = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, saved, state->Memory.Tables.Preconditions);
                if (!baseline.Succeeded || baseline.Kind != X64PageTableEntryKind.Leaf ||
                    baseline.Protection != (PageProtection.Read | PageProtection.Write) || baseline.CachePolicy != PageCachePolicy.WriteBack ||
                    baseline.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong physical) != AddressValueStatus.Success || physical != address)
                    return false;
                captured[page] = saved;
                bool guard = CooperativeGuardPage(page);
                var code = X64PageTableEntry.TryReencodeLeafProtection(X64PageTableLevel.Pte, saved,
                    PageProtection.Read | PageProtection.Execute, X64HardwareStatePolicy.Preserve, state->Memory.Tables.Preconditions);
                if (page < CooperativeCodePages && !code.Succeeded)
                    return false;
                ulong published = guard ? 0 : page < CooperativeCodePages ? code.Entry.RawValue : saved;
                ulong current = *slots[page];
                if (!publish && (current & ~0x60UL) != (saved & ~0x60UL) &&
                    (guard ? current != 0 : (current & ~0x60UL) != (published & ~0x60UL)))
                    return false;
                replacement[page] = publish ? published : saved;
            }
            if (publish)
                for (uint page = 0; page < CooperativePoolPages; page++)
                    original[page] = captured[page];
            originalsCaptured = true;
            var update = (delegate* unmanaged<ulong*, ulong, ulong, ulong, uint>)(state->Memory.Code + FirstExitCodeBundle.CooperativePublishOffset);
            for (uint page = 0; page < CooperativePoolPages; page++)
            {
                ulong expected = *slots[page], desired = replacement[page];
                if (expected == desired)
                    continue;
                if (update(slots[page], expected, desired, root) != 1)
                    return false;
                ulong actual = *slots[page];
                if (desired == 0 ? actual != 0 : (actual & ~0x60UL) != (desired & ~0x60UL))
                    return false;
            }
            return CooperativeAuditPool(state, publish);
        }

        // Compare the complete unmanaged prerequisite value, including PAT
        // and reserved state. Unexpected layout forces the original decode.
        // Both pointers are distinct live audit stack locals. Captures are used
        // only for the current second read; every successful walk overwrites
        // them before the next iteration can reuse a result.
        private static bool CooperativeSameDecodePreconditions(X64PageTableEncodingPreconditions* first, X64PageTableEncodingPreconditions* second)
        {
            if (sizeof(X64PageTableEncodingPreconditions) != 24)
                return false;
            ulong* a = (ulong*)first;
            ulong* b = (ulong*)second;
            return a[0] == b[0] && a[1] == b[1] && a[2] == b[2];
        }

        // Audit actual retained PTEs and live CR3, not range declarations. WB is
        // decoded against the retained PAT token; unchanged live PAT/MTRR policy
        // and complete-operation serialization remain the trusted owner's duty.
        // ownedIst is supplied only after the drain callback authenticates its
        // exact IRQ frame/stack/gates and observed NULL SS. Ordinary callers
        // retain SS16; the separate Linux gateway authenticates its own envelope.
        private static bool CooperativeAuditPool(Storage* state, bool published, bool ownedIst = false)
        {
            if (!CooperativeAdmitPool(state, ownedIst, out var memory, out ulong root))
                return false;
            X64PageTableEntryResult decoded = default;
            X64PageTableEncodingPreconditions observedPreconditions = default;
            for (uint page = 0; page < CooperativePoolPages; page++)
            {
                ulong address = state->CooperativePool + page * Page;
                if (!CooperativeWalkPoolLeaf(memory, state->CooperativePool, root, address, out ulong* leaf, &decoded, &observedPreconditions))
                    return false;
                ulong raw = *leaf;
                bool guard = published && CooperativeGuardPage(page);
                if (guard)
                {
                    if (raw != 0)
                        return false;
                    continue;
                }
                var currentPreconditions = memory.Tables.Preconditions;
                if (raw != decoded.Entry.RawValue || !CooperativeSameDecodePreconditions(&observedPreconditions, &currentPreconditions))
                    decoded = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, raw, currentPreconditions);
                PageProtection expected = published && page < CooperativeCodePages ? PageProtection.Read | PageProtection.Execute : PageProtection.Read | PageProtection.Write;
                if (!decoded.Succeeded || decoded.Kind != X64PageTableEntryKind.Leaf || decoded.Protection != expected ||
                    decoded.CachePolicy != PageCachePolicy.WriteBack || decoded.PageSizeBytes != Page ||
                    decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong physical) != AddressValueStatus.Success || physical != address)
                    return false;
            }
            return true;
        }
#endif

#if M16_002_PROBE

        private static EfiGuid LinuxGuid(uint first, ushort second, byte fifth) => new EfiGuid
        {
            Data1 = first,
            Data2 = second,
            Data3 = 0x11d2,
            Data4_0 = 0x8e,
            Data4_1 = fifth,
            Data4_2 = 0,
            Data4_3 = 0xa0,
            Data4_4 = 0xc9,
            Data4_5 = 0x69,
            Data4_6 = 0x72,
            Data4_7 = 0x3b
        };

        private static bool ReadLinuxPackage(void* owner)
        {
            Storage* state = (Storage*)owner;
            if (state->Phase != 2 || state->LinuxPackage != 0)
                return false;
            LinuxFile* root = null;
            LinuxFile* file = null;
            bool okay = ReadLinuxFile(state, &root, &file);
            bool closed = true;
            if (file != null)
            {
                if (file->Close == null || file->Close(file) != 0)
                    closed = false;
                else
                    state->LinuxClosedHandles++;
                file = null;
            }
            if (root != null)
            {
                if (root->Close == null || root->Close(root) != 0)
                    closed = false;
                else
                    state->LinuxClosedHandles++;
                root = null;
            }
            return okay && closed && state->LinuxClosedHandles == 2;
        }

        private static bool ReadLinuxFile(Storage* state, LinuxFile** root, LinuxFile** file)
        {
            var services = state->SystemTable->BootServices;
            if (services == null || services->HandleProtocol == null || services->AllocatePages == null || services->FreePages == null)
                return false;
            EfiGuid loadedId = new EfiGuid
            {
                Data1 = 0x5b1b31a1,
                Data2 = 0x9562,
                Data3 = 0x11d2,
                Data4_0 = 0x8e,
                Data4_1 = 0x3f,
                Data4_2 = 0,
                Data4_3 = 0xa0,
                Data4_4 = 0xc9,
                Data4_5 = 0x69,
                Data4_6 = 0x72,
                Data4_7 = 0x3b
            };
            LinuxLoadedImage* loaded = null;
            if (services->HandleProtocol(state->ImageHandle, &loadedId, (void**)&loaded) != 0 || loaded == null || loaded->Device == null || loaded->Table != state->SystemTable)
                return false;
            EfiGuid fsId = LinuxGuid(0x964e5b22, 0x6459, 0x39);
            LinuxFileSystem* fs = null;
            if (services->HandleProtocol(loaded->Device, &fsId, (void**)&fs) != 0 || fs == null || fs->Revision < 0x10000 || fs->OpenVolume == null ||
                fs->OpenVolume(fs, root) != 0 || *root == null || (*root)->Open == null || (*root)->Close == null)
                return false;
            ushort* path = stackalloc ushort[12];
            string name = "\\OS\\APP.PKG";
            for (int i = 0; i < 11; i++)
                path[i] = name[i];
            path[11] = 0;
            if ((*root)->Open(*root, file, path, 1, 0) != 0 || *file == null || (*file)->Close == null || (*file)->Read == null || (*file)->GetInfo == null)
                return false;
            EfiGuid infoId = LinuxGuid(0x09576e92, 0x6d3f, 0x39);
            byte* info = stackalloc byte[512];
            ulong infoBytes = 512;
            if ((*file)->GetInfo(*file, &infoId, &infoBytes, info) != 0 || infoBytes < 82 || infoBytes > 512 ||
                *(ulong*)info < 82 || *(ulong*)info > infoBytes || (*(ulong*)(info + 72) & 0x10) != 0)
                return false;
            ulong bytes = *(ulong*)(info + 8);
            if (bytes < OS.Manifest.LinuxElfPackage.HeaderBytes || bytes > OS.Manifest.LinuxElfPackage.MaximumBytes)
                return false;
            ulong pages = (bytes + 4095) / 4096, candidate = 0xffffffff;
            if (services->AllocatePages(1, 2, pages, &candidate) != 0)
                return false;
            state->LinuxPackage = candidate;
            state->LinuxPages = (uint)pages;
            if (candidate < 4096 || (candidate & 4095) != 0 || candidate >= 0x100000000UL - pages * 4096 ||
                !M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var capture, out _) ||
                capture.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out _, out var allocator) ||
                !Disjoint(candidate, pages * 4096, image, capture.Range.ImageSize) ||
                !Disjoint(candidate, pages * 4096, allocator.Start, allocator.PageCount * 4096) ||
                !LinuxPoolDisjoint(state, candidate, pages))
                return false;
            byte* buffer = (byte*)candidate;
            for (ulong i = 0; i < pages * 4096; i++)
                buffer[i] = 0;
            ulong written = 0;
            while (written < bytes)
            {
                ulong request = bytes - written;
                if (request > 65536)
                    request = 65536;
                ulong actual = request;
                if (++state->LinuxReadCalls > 4096 || (*file)->Read(*file, &actual, buffer + written) != 0 || actual == 0 || actual > request)
                    return false;
                written += actual;
            }
            byte extra = 0;
            ulong end = 1;
            if ((*file)->Read(*file, &end, &extra) != 0 || end != 0)
                return false;
            state->LinuxBytes = (uint)bytes;
            state->LinuxView = OS.Manifest.LinuxElfPackage.Validate(buffer, (uint)bytes);
            return state->LinuxView.Succeeded && OS.Std.Hash.Sha256.TryCompute(buffer, (uint)bytes, state->LinuxPackageHash, 32);
        }

        private static bool SealLinuxLedger(Storage* state, ulong code, ulong data, ulong tables)
        {
            BootReservedRange* ledger = (BootReservedRange*)state->LinuxLedger;
            if ((ulong)sizeof(BootReservedRange) > 64 || !M01StageTwoAdapter.CopyOriginalExitReservations(ledger, LinuxLedgerCapacity, out uint count) || count != 5)
                return false;
            if (!LinuxReservation(BootReservationKind.LinuxElfPackage, state->LinuxPackage, state->LinuxPages, ledger, ref count) ||
                !LinuxReservation(BootReservationKind.Cr3TransitionStub, code, FirstExitCodeBundle.Bytes / 4096, ledger, ref count) ||
                !LinuxReservation(BootReservationKind.DiagnosticState, data, FirstExitOwnedArena.PageCount, ledger, ref count) ||
                !LinuxReservation(BootReservationKind.PageTableArena, tables, TablePages, ledger, ref count))
                return false;
#if M07_026_PROBE
            if (!LinuxReservation(BootReservationKind.DiagnosticState, state->NetworkArena, 8, ledger, ref count))
                return false;
#endif
#if M16_027_PROBE
            if (!GetNativeWritableDiskBacking(state, out ulong diskArena, out ulong diskPages, out _) ||
                !LinuxReservation(BootReservationKind.DiagnosticState, diskArena, diskPages, ledger, ref count))
                return false;
#elif M16_025_PROBE
            fixed (NativeDiskOwner* disk = &s_nativeDisk)
                if (disk->Boot != state || !LinuxReservation(BootReservationKind.DiagnosticState, disk->Arena, disk->ArenaPages, ledger, ref count))
                    return false;
#endif
#if M16_004_PROBE
            if (!LinuxReservation(BootReservationKind.LinuxElfBacking, state->LinuxImageBacking, state->LinuxImagePages, ledger, ref count) ||
                !LinuxReservation(BootReservationKind.LinuxElfUserTables, state->LinuxUserTables, X64UserAddressSpace.MaximumTables, ledger, ref count))
                return false;
#endif
#if M16_005_PROBE
            if (!LinuxReservation(BootReservationKind.LinuxSyscallCode, state->LinuxSyscallCode, 1, ledger, ref count) ||
                !LinuxReservation(BootReservationKind.LinuxSyscallData, state->LinuxSyscallData, X64LinuxUserTransition.DataPages, ledger, ref count))
                return false;
#endif
#if M16_006_PROBE
            if (!LinuxReservation(BootReservationKind.LinuxElfBacking, state->LinuxMemoryPool, LinuxMemoryPoolPages(state), ledger, ref count) ||
                !LinuxReservation(BootReservationKind.LinuxElfUserTables, state->LinuxMemoryCandidate, X64UserAddressSpace.MaximumTables, ledger, ref count))
                return false;
#endif
#if M16_007_PROBE
            if (!LinuxReservation(BootReservationKind.DiagnosticState, state->LinuxDescriptorArena, LinuxDescriptorPages, ledger, ref count))
                return false;
#endif
#if M16_008_PROBE || M07_026_PROBE
            if (!LinuxReservation(BootReservationKind.DiagnosticState, state->CooperativePool, CooperativePoolPages, ledger, ref count))
                return false;
#endif
            for (uint i = 1; i < count; i++)
            {
                var current = ledger[i];
                uint at = i;
                while (at > 0)
                {
                    if (current.PhysicalStart.TryCompareTo(ledger[at - 1].PhysicalStart, out int order) != AddressValueStatus.Success)
                        return false;
                    if (order >= 0)
                        break;
                    ledger[at] = ledger[at - 1];
                    at--;
                }
                ledger[at] = current;
            }
            if (!BootReservationTableValidator.Validate(ledger, count, LinuxLedgerCapacity).Succeeded)
                return false;
            state->LinuxLedgerCount = count;
            return true;
        }
        private static bool LinuxReservation(BootReservationKind kind, ulong address, ulong pages, BootReservedRange* ledger, ref uint count)
        {
            // Bound every append before writing, including partially prepared
            // combined profiles. A rejected entry does not advance the ledger.
            if (count >= LinuxLedgerCapacity || PhysicalAddress.TryCreateFromUefiBoundary(address, out var physical) != AddressValueStatus.Success ||
                BootReservedRange.TryCreate(kind, physical, pages, out var value) != BootReservedRangeStatus.Success)
                return false;
            ledger[count++] = value;
            return true;
        }

        // The combined profile reserves workers first. Reject an aliased Linux
        // candidate before any package read, zero, user-root build or emission.
        private static bool LinuxPoolDisjoint(Storage* state, ulong address, ulong pages) =>
#if M16_008_PROBE || M07_026_PROBE
            state->CooperativePool >= Page && Disjoint(address, pages * Page, state->CooperativePool, CooperativePoolPages * Page);
#else
            true;
#endif

#if M16_004_PROBE
        private static bool PrepareLinuxStartup(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->Phase != 2 || state->LinuxStartupPhase != 0 || !state->LinuxView.Succeeded)
                return false;
            state->LinuxStartupPhase = 1;
            var view = state->LinuxView;
            if (LinuxElfLoader.Validate((byte*)state->LinuxPackage + view.PayloadOffset, view.PayloadBytes,
                view.Manifest.MemoryBytes, view.Manifest.StackBytes, out state->LinuxImagePlan) != LinuxElfLoadStatus.Ready)
                return false;
            ulong pages = (state->LinuxImagePlan.ImageBytes + view.Manifest.StackBytes) / Page;
            // Keep the accepted identity-builder's 4096-page per-range cap.
            // All three pinned payloads fit; larger host-admitted images reject.
            if (pages == 0 || pages > 4096)
                return false;
            state->LinuxImagePages = pages;
            EfiBootServices* services = state->SystemTable->BootServices;
            if (!AllocateLinuxStartup(state, services, pages, out state->LinuxImageBacking) ||
                !AllocateLinuxStartup(state, services, X64UserAddressSpace.MaximumTables, out state->LinuxUserTables) ||
                !Disjoint(state->LinuxImageBacking, pages * Page, state->LinuxUserTables, X64UserAddressSpace.MaximumTables * Page) ||
                !Disjoint(state->LinuxImageBacking, pages * Page, state->LinuxPackage, state->LinuxPages * Page) ||
                !Disjoint(state->LinuxUserTables, X64UserAddressSpace.MaximumTables * Page, state->LinuxPackage, state->LinuxPages * Page))
                return false;
            var entropy = &state->LinuxEntropy;
            bool begun = FirmwareEntropyCapture.Begin(entropy, &state->LinuxEntropyScratch, &state->LinuxEntropyRetained, state->SystemTable);
            bool captured = begun && FirmwareEntropyCapture.Acquire(entropy);
            entropy->SystemTable = null;
            entropy->BootServices = null;
            entropy->Locate = null;
            entropy->Rng = null;
            entropy->GetRng = null;
            if (!captured || entropy->Phase != 3 || entropy->PublishedBytes != 32 ||
                !FirmwareEntropyCapture.Guards(&state->LinuxEntropyScratch) || !FirmwareEntropyCapture.Guards(&state->LinuxEntropyRetained) ||
                !FirmwareEntropyCapture.Zero(state->LinuxEntropyScratch.Data, 0, 64))
                return false;
            for (ulong i = 0; i < pages * Page; i++)
                ((byte*)state->LinuxImageBacking)[i] = 0;
            if (!X64UserAddressSpace.Clear((ulong*)state->LinuxUserTables, X64UserAddressSpace.MaximumTables))
                return false;
            state->LinuxStartupPhase = 2;
            return true;
        }

        private static bool AllocateLinuxStartup(Storage* state, EfiBootServices* services, ulong pages, out ulong address)
        {
            address = 0;
            bool bounded = pages > 0 && pages <= 4096;
#if M16_006_PROBE
            // Only the selected backing pool may exceed one retained range;
            // its mapping declarations remain split at the 4096-page limit.
            bounded |= pages == LinuxMemoryPoolPages(state);
#endif
            if (services == null || services->AllocatePages == null || services->FreePages == null || !bounded)
                return false;
            ulong candidate = 0xffffffff;
            if (services->AllocatePages(1, 2, pages, &candidate) != 0)
                return false;
            address = candidate;
            if (candidate < Page || (candidate & 4095) != 0 || candidate > 0x100000000UL - pages * Page ||
                !M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var capture, out _) ||
                capture.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out _, out var allocator))
                return false;
            return Disjoint(candidate, pages * Page, image, capture.Range.ImageSize) &&
                Disjoint(candidate, pages * Page, allocator.Start, allocator.PageCount * Page) && LinuxPoolDisjoint(state, candidate, pages);
        }

        private static bool ReleaseLinuxStartup(Storage* state, EfiBootServices* services)
        {
            ScrubLinuxEntropy(state);
            bool okay = true;
            if (state->LinuxUserTables != 0)
            {
                if (services != null && services->FreePages != null && services->FreePages(state->LinuxUserTables, X64UserAddressSpace.MaximumTables) == 0)
                    state->LinuxUserTables = 0;
                else
                    okay = false;
            }
            if (state->LinuxImageBacking != 0)
            {
                if (services != null && services->FreePages != null && services->FreePages(state->LinuxImageBacking, state->LinuxImagePages) == 0)
                    state->LinuxImageBacking = 0;
                else
                    okay = false;
            }
            state->LinuxStartupPhase = 7;
            return okay;
        }

        private static bool ScrubLinuxEntropy(Storage* state)
        {
            state->LinuxEntropy.SystemTable = null;
            state->LinuxEntropy.BootServices = null;
            state->LinuxEntropy.Locate = null;
            state->LinuxEntropy.Rng = null;
            state->LinuxEntropy.GetRng = null;
            for (int i = 0; i < 64; i++)
            {
                state->LinuxEntropyScratch.Data[i] = 0;
                state->LinuxEntropyRetained.Data[i] = 0;
            }
            return FirmwareEntropyCapture.Zero(state->LinuxEntropyScratch.Data, 0, 64) && FirmwareEntropyCapture.Zero(state->LinuxEntropyRetained.Data, 0, 64);
        }

        private static bool RunOwnedLinuxStartup(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->LinuxStartupPhase != 2 || state->LinuxOwnedPhase != 2 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed ||
                state->Arena.Transition->Mode != FirmwareServicesMode.BootServicesExited ||
                !state->Arena.Transition->RetainedFirmwareCallbacksCleared || !state->Arena.Binding->RetainedPointersCleared ||
                state->LinuxEntropy.SystemTable != null || state->LinuxEntropy.BootServices != null || state->LinuxEntropy.Locate != null ||
                state->LinuxEntropy.Rng != null || state->LinuxEntropy.GetRng != null || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->LinuxStartupPhase = 3;
            if (!LinuxText(state, "TEST_START m16-004\r\n"))
                return false;
            var view = state->LinuxView;
            byte* elf = (byte*)state->LinuxPackage + view.PayloadOffset;
            byte* manifest = (byte*)state->LinuxPackage + view.ManifestOffset;
            byte* bridge = state->Memory.Code + FirstExitCodeBundle.LinuxReadBackOffset;
            byte* cpu = state->Memory.Code + FirstExitCodeBundle.ClockAccessOffset;
            if (!X64LinuxElfReadBackBridge.Validate(bridge, X64LinuxElfReadBackBridge.Bytes))
                return FailLinuxStartup(state, 1);
            if (!X64OwnedClockAccess.Validate(cpu, X64OwnedClockAccess.Bytes))
                return FailLinuxStartup(state, 2);
            var cpuid = (delegate* unmanaged<uint, uint, uint*, void>)(cpu + X64OwnedClockAccess.CpuidOffset);
            uint* words = stackalloc uint[4];
            cpuid(0, 0, words);
            if (words[0] < 1)
                return FailLinuxStartup(state, 3);
            cpuid(1, 0, words);
            state->LinuxHwcap = words[3];
            if (LinuxElfMapping.Materialize(elf, view.PayloadBytes, view.Manifest.MemoryBytes, view.Manifest.StackBytes,
                (byte*)state->LinuxImageBacking, (uint)state->LinuxImagePlan.ImageBytes, out state->LinuxImagePlan) != LinuxElfMaterializeStatus.Ready)
                return FailLinuxStartup(state, 4);
            if (LinuxElfStartup.Build(manifest, view.ManifestBytes, elf, view.PayloadBytes, state->LinuxEntropyRetained.Data, 32,
                    (byte*)(state->LinuxImageBacking + state->LinuxImagePlan.ImageBytes), view.Manifest.StackBytes,
                    0x70000000UL - view.Manifest.StackBytes, state->LinuxHwcap, out state->LinuxInitialStack) != LinuxElfStartupStatus.Ready)
                return FailLinuxStartup(state, 5);
            if (!BuildLinuxUserRanges(state))
                return FailLinuxStartup(state, 6);
            state->LinuxUserRoot = X64UserAddressSpace.Build((X64UserMappingRange*)state->LinuxUserRanges, state->LinuxUserRangeCount,
                (ulong*)state->LinuxUserTables, state->LinuxUserTables, X64UserAddressSpace.MaximumTables, state->Memory.Tables.Preconditions);
            if (!state->LinuxUserRoot.Succeeded)
                return FailLinuxStartup(state, 7);
            var readCr3 = (delegate* unmanaged<ulong>)(bridge + X64LinuxElfReadBackBridge.ReadCr3Offset);
            ulong original = readCr3();
            if (original != (ulong)state->Memory.Entries)
                return FailLinuxStartup(state, 8);
            state->LinuxStartupPhase = 4;
            var enter = (delegate* unmanaged<ulong, void*, delegate* unmanaged<void*, byte>, byte>)bridge;
            byte readBack = enter(state->LinuxUserRoot.Root, state, &ReadLinuxUserRoot);
            if (readCr3() != original)
                return false; // No clearing while this root could still be active.
            if (readBack != 1 || state->LinuxStartupPhase != 5 || !ScrubLinuxEntropy(state) ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return FailLinuxStartup(state, 9);
            state->LinuxStartupPhase = 6;
            return LinuxText(state, "M16_004_IMAGE base=") && LinuxNumber(state, state->LinuxImagePlan.ImageStart) &&
                LinuxText(state, " bytes=") && LinuxNumber(state, state->LinuxImagePlan.ImageBytes) &&
                LinuxText(state, " loads=") && LinuxNumber(state, state->LinuxImagePlan.LoadCount) &&
                LinuxText(state, " entry=") && LinuxNumber(state, state->LinuxImagePlan.Entry) &&
                LinuxText(state, " phdr=") && LinuxNumber(state, state->LinuxImagePlan.ProgramHeaders) &&
                LinuxText(state, " tlsFile=") && LinuxNumber(state, state->LinuxImagePlan.TlsFileBytes) &&
                LinuxText(state, " tlsMemory=") && LinuxNumber(state, state->LinuxImagePlan.TlsMemoryBytes) &&
                LinuxText(state, "\r\nM16_004_STACK start=") && LinuxNumber(state, state->LinuxInitialStack.StackStart) &&
                LinuxText(state, " bytes=") && LinuxNumber(state, state->LinuxInitialStack.StackBytes) &&
                LinuxText(state, " sp=") && LinuxNumber(state, state->LinuxInitialStack.StackPointer) &&
                LinuxText(state, " argc=") && LinuxNumber(state, state->LinuxInitialStack.ArgumentCount) &&
                LinuxText(state, " envc=") && LinuxNumber(state, state->LinuxInitialStack.EnvironmentCount) &&
                LinuxText(state, " random=") && LinuxNumber(state, state->LinuxInitialStack.RandomAddress) &&
                LinuxText(state, " entropyBytes=32 seedCleared=32\r\nM16_004_TABLES root=") && LinuxNumber(state, state->LinuxUserRoot.Root) &&
                LinuxText(state, " used=") && LinuxNumber(state, state->LinuxUserRoot.UsedTables) &&
                LinuxText(state, " leaves=") && LinuxNumber(state, state->LinuxUserRoot.LeafPages) &&
                LinuxText(state, " ranges=") && LinuxNumber(state, state->LinuxUserRangeCount) &&
                LinuxText(state, "\r\nM16_004_READBACK old=") && LinuxNumber(state, original) &&
                LinuxText(state, " restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false\r\nCLROS_M16_004_READY\r\nTEST_PASS m16-004\r\n");
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadLinuxUserRoot(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->LinuxStartupPhase != 4 || (ulong)state != state->Memory.References.AdapterState ||
                !state->LinuxUserRoot.Succeeded || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return 0;
            var readCr3 = (delegate* unmanaged<ulong>)(state->Memory.Code + FirstExitCodeBundle.LinuxReadBackOffset + X64LinuxElfReadBackBridge.ReadCr3Offset);
            if (readCr3() != state->LinuxUserRoot.Root ||
                !X64UserAddressSpace.Audit((X64UserMappingRange*)state->LinuxUserRanges, state->LinuxUserRangeCount,
                    (ulong*)state->LinuxUserTables, state->LinuxUserTables, X64UserAddressSpace.MaximumTables, state->Memory.Tables.Preconditions, state->LinuxUserRoot) ||
                !X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables, X64UserAddressSpace.MaximumTables,
                    state->LinuxInitialStack.StackStart - Page, out ulong guard) || guard != 0)
                return 0;
            var view = state->LinuxView;
            byte* elf = (byte*)state->LinuxPackage + view.PayloadOffset;
            if (!LinuxElfMapping.ValidateMappedLoads(elf, view.PayloadBytes, view.Manifest.MemoryBytes, view.Manifest.StackBytes, state->LinuxImagePlan) ||
                !LinuxElfStartup.ValidateReadBack((byte*)state->LinuxPackage + view.ManifestOffset, view.ManifestBytes, elf, view.PayloadBytes,
                    state->LinuxEntropyRetained.Data, 32, (byte*)state->LinuxInitialStack.StackStart, view.Manifest.StackBytes,
                    state->LinuxInitialStack.StackStart, state->LinuxHwcap, state->LinuxInitialStack))
                return 0;
            state->LinuxStartupPhase = 5;
            return 1;
        }

        private static bool FailLinuxStartup(Storage* state, uint stage)
        {
            _ = LinuxText(state, "M16_004_REJECTED stage=") && LinuxNumber(state, stage) &&
                LinuxText(state, " rootStatus=") && LinuxNumber(state, (uint)state->LinuxUserRoot.Status) &&
                LinuxText(state, " ranges=") && LinuxNumber(state, state->LinuxUserRangeCount) && LinuxText(state, "\r\n");
            if (stage == 7)
                for (uint i = 0; i < state->LinuxUserRangeCount && i < X64UserAddressSpace.MaximumRanges; i++)
                {
                    var range = ((X64UserMappingRange*)state->LinuxUserRanges)[i];
                    _ = LinuxText(state, "M16_004_REJECT_RANGE index=") && LinuxNumber(state, i) &&
                        LinuxText(state, " virtual=") && LinuxNumber(state, range.VirtualStart) &&
                        LinuxText(state, " physical=") && LinuxNumber(state, range.PhysicalStart) &&
                        LinuxText(state, " pages=") && LinuxNumber(state, range.Pages) &&
                        LinuxText(state, " protection=") && LinuxNumber(state, (uint)range.Protection) &&
                        LinuxText(state, " cache=") && LinuxNumber(state, (uint)range.Cache) &&
                        LinuxText(state, " user=") && LinuxNumber(state, range.User ? 1UL : 0UL) && LinuxText(state, "\r\n");
                }
            // After exit these pages remain reserved by the original owner. Clear
            // contents and unpublish the candidate; do not pretend they are freed.
            if (state->LinuxImageBacking != 0)
                LinuxElfMapping.Clear((byte*)state->LinuxImageBacking, (uint)(state->LinuxImagePages * Page));
            if (state->LinuxUserTables != 0)
                X64UserAddressSpace.Clear((ulong*)state->LinuxUserTables, X64UserAddressSpace.MaximumTables);
            state->LinuxUserRoot = default;
            state->LinuxInitialStack = default;
            state->LinuxStartupPhase = 7;
            ScrubLinuxEntropy(state);
            _ = LinuxText(state, "TEST_FAIL m16-004 owned-startup-validation\r\n");
            return false;
        }

        private static bool BuildLinuxUserRanges(Storage* state)
        {
            if (sizeof(X64UserMappingRange) != 32)
                return false;
            X64UserMappingRange* target = (X64UserMappingRange*)state->LinuxUserRanges;
            for (uint i = 0; i < X64UserAddressSpace.MaximumRanges; i++)
                target[i] = default;
            SeparatePageFaultMappingRange* retained = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            if (!FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, state->Memory.Code, state->Memory.Capture,
                state->Memory.Mapping, state->Memory.References, state->Memory.Allocator, state->Memory.Tables, retained,
                FirstExitRetainedMappings.MaximumRanges, out ulong count))
                return false;
            uint written = 0;
            for (ulong i = 0; i < count; i++)
            {
                var r = retained[i];
                // No physical RW image/stack alias in the user root. Its table
                // arena is supplied as supervisor RW/NX by the builder itself.
                if (r.Start == state->LinuxImageBacking || r.Start == state->LinuxUserTables)
                    continue;
#if M16_006_PROBE
                ulong poolPages = LinuxMemoryPoolPages(state);
                if (r.Start >= state->LinuxMemoryPool && r.Start - state->LinuxMemoryPool < poolPages * Page)
                {
                    ulong offset = r.Start - state->LinuxMemoryPool, pages = poolPages - offset / Page;
                    if (offset % (4096 * Page) != 0 || r.PageCount != (pages > 4096 ? 4096 : pages) ||
                        r.Protection != (PageProtection.Read | PageProtection.Write) || r.CachePolicy != PageCachePolicy.WriteBack)
                        return false;
                    continue; // Neither physical backing span belongs in the user root.
                }
#endif
#if M16_008_PROBE
                // The independent user root must retain the published pool
                // geometry across mmap rebuilds. Kernel-root staging/publication
                // remains separate; no guard or writable code alias is admitted here.
                if (r.Start == state->CooperativePool)
                {
                    if (r.PageCount != LinuxThreadProgram.PoolPages || r.Protection != (PageProtection.Read | PageProtection.Write) ||
                        r.CachePolicy != PageCachePolicy.WriteBack)
                        return false;
                    uint page = 0;
                    do
                    {
                        if (written == X64UserAddressSpace.MaximumRanges ||
                            !LinuxThreadProgram.TrySupervisorMapping(r.Start, page, out target[written], out page))
                            return false;
                        written++;
                    } while (page < LinuxThreadProgram.PoolPages);
                    continue;
                }
#endif
                if (written == X64UserAddressSpace.MaximumRanges)
                    return false;
                target[written++] = new X64UserMappingRange
                {
                    VirtualStart = r.Start,
                    PhysicalStart = r.Start,
                    Pages = r.PageCount,
                    Protection = r.Protection,
                    Cache = r.CachePolicy,
                    User = false
                };
            }
            if (written + state->LinuxImagePlan.LoadCount + 2 > X64UserAddressSpace.MaximumRanges)
                return false;
            target[written++] = new X64UserMappingRange
            {
                VirtualStart = (ulong)state->Memory.Entries,
                PhysicalStart = (ulong)state->Memory.Entries,
                Pages = TablePages,
                Protection = PageProtection.Read | PageProtection.Write,
                Cache = PageCachePolicy.WriteBack,
                User = false
            };
            for (uint i = 0; i < state->LinuxImagePlan.LoadCount; i++)
            {
                uint row = i * 5;
                ulong address = state->LinuxImagePlan.Loads[row], memory = state->LinuxImagePlan.Loads[row + 3], flags = state->LinuxImagePlan.Loads[row + 4];
                ulong start = address & ~4095UL, end = (address + memory + 4095) & ~4095UL;
                PageProtection protection = PageProtection.Read;
                if ((flags & 2) != 0)
                    protection |= PageProtection.Write;
                if ((flags & 1) != 0)
                    protection |= PageProtection.Execute;
                target[written++] = new X64UserMappingRange
                {
                    VirtualStart = start,
                    PhysicalStart = state->LinuxImageBacking + start - state->LinuxImagePlan.ImageStart,
                    Pages = (end - start) / Page,
                    Protection = protection,
                    Cache = PageCachePolicy.WriteBack,
                    User = true
                };
            }
            var manifest = state->LinuxView.Manifest;
            target[written++] = new X64UserMappingRange
            {
                VirtualStart = 0x70000000UL - manifest.StackBytes,
                PhysicalStart = state->LinuxImageBacking + state->LinuxImagePlan.ImageBytes,
                Pages = manifest.StackBytes / Page,
                Protection = PageProtection.Read | PageProtection.Write,
                Cache = PageCachePolicy.WriteBack,
                User = true
            };
            state->LinuxUserRangeCount = written;
            return true;
        }
#endif

#if M16_005_PROBE
        private static bool PrepareLinuxUserBoundary(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->Phase != 2 || state->LinuxStartupPhase != 2 || state->LinuxSyscallPhase != 0)
                return false;
            state->LinuxSyscallPhase = 1;
            EfiBootServices* services = state->SystemTable->BootServices;
            if (!AllocateLinuxStartup(state, services, 1, out state->LinuxSyscallCode) ||
                !AllocateLinuxStartup(state, services, X64LinuxUserTransition.DataPages, out state->LinuxSyscallData) ||
                !Disjoint(state->LinuxSyscallCode, Page, state->LinuxSyscallData, X64LinuxUserTransition.DataBytes))
                return false;
            ulong* starts = stackalloc ulong[3];
            ulong* bytes = stackalloc ulong[3];
            starts[0] = state->LinuxPackage;
            bytes[0] = state->LinuxPages * Page;
            starts[1] = state->LinuxImageBacking;
            bytes[1] = state->LinuxImagePages * Page;
            starts[2] = state->LinuxUserTables;
            bytes[2] = X64UserAddressSpace.MaximumTables * Page;
            for (uint i = 0; i < 3; i++)
                if (!Disjoint(state->LinuxSyscallCode, Page, starts[i], bytes[i]) ||
                    !Disjoint(state->LinuxSyscallData, X64LinuxUserTransition.DataBytes, starts[i], bytes[i]))
                    return false;
            for (uint i = 0; i < X64LinuxUserTransition.CodeBytes; i++)
                ((byte*)state->LinuxSyscallCode)[i] = 0;
            for (uint i = 0; i < X64LinuxUserTransition.DataBytes; i++)
                ((byte*)state->LinuxSyscallData)[i] = 0;
            state->LinuxSyscallPhase = 2;
            return true;
        }

        private static bool ReleaseLinuxUserBoundary(Storage* state, EfiBootServices* services)
        {
            bool okay = true;
#if M16_007_PROBE
            okay = ReleaseLinuxDescriptors(state, services);
#endif
#if M16_006_PROBE
            okay = ReleaseLinuxMemory(state, services) && okay;
#endif
            if (state->LinuxSyscallCode != 0)
            {
                if (services != null && services->FreePages != null && services->FreePages(state->LinuxSyscallCode, 1) == 0)
                    state->LinuxSyscallCode = 0;
                else
                    okay = false;
            }
            if (state->LinuxSyscallData != 0)
            {
                if (services != null && services->FreePages != null && services->FreePages(state->LinuxSyscallData, X64LinuxUserTransition.DataPages) == 0)
                    state->LinuxSyscallData = 0;
                else
                    okay = false;
            }
            state->LinuxSyscallPhase = 7;
            return okay;
        }

        private static ulong LinuxGateHandler(byte* gate) =>
            *(ushort*)gate | ((ulong)*(ushort*)(gate + 6) << 16) | ((ulong)*(uint*)(gate + 8) << 32);

#if M16_006_PROBE
        // The declared large main stack has substantial startup/probe cost
        // even with few workers. Select finite application resources from the
        // already validated manifest, retaining the mechanical defaults.
        private static bool LinuxUsesApplicationLimits(Storage* state)
        {
#if M16_008_PROBE
            return state->LinuxView.Manifest.StackBytes >= 1024 * 1024 || state->LinuxView.Manifest.Threads > 8;
#else
            return false;
#endif
        }

        private static uint LinuxMemoryPoolPages(Storage* state)
        {
#if M16_014_PROBE
            // The immutable manifest is validated before pool allocation. Match
            // the exact GC key here; its value and the complete retained ledger
            // still have to pass ReadGcBudgetFromReservations before publication.
            if (state->LinuxPackage != 0 && state->LinuxView.Manifest.Succeeded)
                for (uint i = 0; i < state->LinuxView.Manifest.EnvironmentCount; i++)
                {
                    uint at = i * 4;
                    if (OS.Manifest.LinuxAppConfiguration.IsGcHeapHardLimitKey(
                        (byte*)state->LinuxPackage + state->LinuxView.ManifestOffset + state->LinuxView.Manifest.EnvironmentRanges[at],
                        state->LinuxView.Manifest.EnvironmentRanges[at + 1]))
                        return OS.Manifest.LinuxAppConfiguration.ConfiguredServicePoolPages;
                }
#endif
            return LinuxUsesApplicationLimits(state) ? LinuxMemoryServices.ApplicationPoolPages : LinuxMemoryServices.DefaultPoolPages;
        }

        private static bool PrepareLinuxMemory(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->Phase != 2 || state->LinuxMemoryPhase != 0 || state->LinuxSyscallPhase != 2)
                return false;
            state->LinuxMemoryPhase = 1;
            var services = state->SystemTable->BootServices;
            if (!AllocateLinuxStartup(state, services, LinuxMemoryPoolPages(state), out state->LinuxMemoryPool) ||
                !AllocateLinuxStartup(state, services, X64UserAddressSpace.MaximumTables, out state->LinuxMemoryCandidate))
                return false;
            ulong* starts = stackalloc ulong[6] { state->LinuxMemoryPool, state->LinuxMemoryCandidate, state->LinuxPackage,
                state->LinuxImageBacking, state->LinuxSyscallCode, state->LinuxSyscallData };
            ulong* lengths = stackalloc ulong[6] { LinuxMemoryPoolPages(state) * Page, X64UserAddressSpace.MaximumTables * Page,
                state->LinuxPages * Page, state->LinuxImagePages * Page, Page, X64LinuxUserTransition.DataBytes };
            for (uint i = 0; i < 6; i++)
                for (uint j = 0; j < i; j++)
                    if (!Disjoint(starts[i], lengths[i], starts[j], lengths[j]))
                        return false;
#if M16_014_PROBE
            if (!PrepareLinuxServiceRandom(state))
                return false;
#else
            if (LinuxRandomBudget(state) == FirmwareEntropyReservoir.Bytes)
            {
                if (!FirmwareEntropyReservoir.Acquire(&state->LinuxRuntimeRandom, state->SystemTable))
                    return false;
            }
            else
            {
                var capture = &state->LinuxRandomCapture;
                bool captured = FirmwareEntropyCapture.Begin(capture, &state->LinuxRandomScratch, &state->LinuxRandomRetained, state->SystemTable) &&
                    FirmwareEntropyCapture.Acquire(capture);
                capture->SystemTable = null;
                capture->BootServices = null;
                capture->Locate = null;
                capture->Rng = null;
                capture->GetRng = null;
                if (!captured || capture->Phase != 3 || capture->PublishedBytes != 32 ||
                    !FirmwareEntropyCapture.Guards(&state->LinuxRandomScratch) || !FirmwareEntropyCapture.Guards(&state->LinuxRandomRetained) ||
                    !FirmwareEntropyCapture.Zero(state->LinuxRandomScratch.Data, 0, 64))
                    return false;
            }
#endif
            for (ulong i = 0; i < LinuxMemoryPoolPages(state) * Page; i++)
                ((byte*)state->LinuxMemoryPool)[i] = 0;
            if (!X64UserAddressSpace.Clear((ulong*)state->LinuxMemoryCandidate, X64UserAddressSpace.MaximumTables))
                return false;
            state->LinuxMemoryPhase = 2;
            return true;
        }

        private static bool ReleaseLinuxMemory(Storage* state, EfiBootServices* services)
        {
            bool okay = true;
            if (state->LinuxMemoryPool != 0)
            {
                if (services != null && services->FreePages != null && services->FreePages(state->LinuxMemoryPool, LinuxMemoryPoolPages(state)) == 0)
                    state->LinuxMemoryPool = 0;
                else
                    okay = false;
            }
            if (state->LinuxMemoryCandidate != 0)
            {
                if (services != null && services->FreePages != null && services->FreePages(state->LinuxMemoryCandidate, X64UserAddressSpace.MaximumTables) == 0)
                    state->LinuxMemoryCandidate = 0;
                else
                    okay = false;
            }
            okay = ClearLinuxRandom(state) && okay;
            state->LinuxMemoryPhase = 7;
            return okay;
        }

        // Finite profiles keep their captured reservoirs. The separately sealed
        // service leaf owns a DRBG instead; no firmware callback survives EBS.
        private static uint LinuxRandomBudget(Storage* state)
        {
#if M16_014_PROBE
            return 0;
#else
#if M10_004_PROBE
            return LinuxStartupServices.WebEntropyBytes;
#else
#if M16_008_PROBE
            if (state->LinuxView.Manifest.Threads > 8)
                return LinuxStartupServices.RuntimeEntropyBytes;
#endif
            return LinuxStartupServices.EntropyBytes;
#endif
#endif
        }

#if M16_014_PROBE
        private static bool CaptureLinuxServiceSeed(Storage* state, FirmwareEntropyCapture.State* capture,
            FirmwareEntropyCapture.Buffer* scratch, FirmwareEntropyCapture.Buffer* retained)
        {
            bool acquired = FirmwareEntropyCapture.Begin(capture, scratch, retained, state->SystemTable) && FirmwareEntropyCapture.Acquire(capture);
            capture->SystemTable = null;
            capture->BootServices = null;
            capture->Locate = null;
            capture->Rng = null;
            capture->GetRng = null;
            return acquired && capture->Phase == 3 && capture->PublishedBytes == 32 &&
                FirmwareEntropyCapture.Guards(scratch) && FirmwareEntropyCapture.Guards(retained) &&
                FirmwareEntropyCapture.Zero(scratch->Data, 0, 64);
        }

        private static bool PrepareLinuxServiceRandom(Storage* state)
        {
            if (state->LinuxServiceRandomPhase != 0)
                return false;
            state->LinuxServiceRandomPhase = 1;
            bool captured = CaptureLinuxServiceSeed(state, &state->LinuxRandomCapture, &state->LinuxRandomScratch, &state->LinuxRandomRetained) &&
                CaptureLinuxServiceSeed(state, &state->LinuxServiceNonceCapture, &state->LinuxServiceNonceScratch, &state->LinuxServiceNonceRetained) &&
                CaptureLinuxServiceSeed(state, &state->LinuxServiceSequenceCapture, &state->LinuxServiceSequenceScratch, &state->LinuxServiceSequenceRetained);
            const string domain = "CLROS.getrandom.v1";
            byte* label = stackalloc byte[domain.Length];
            for (int i = 0; i < domain.Length; i++)
                label[i] = (byte)domain[i];
            bool initialized = false;
            if (captured)
                initialized = OS.Std.Hash.HmacSha256Drbg.Instantiate(&state->LinuxServiceRandom,
                state->LinuxRandomRetained.Data, state->LinuxServiceNonceRetained.Data, label, (uint)domain.Length) &&
                TcpInitialSequence.Initialize(&state->LinuxServiceSequence, state->LinuxServiceSequenceRetained.Data, 1);
            // Capture source facts only; seed bytes, keys and DRBG state never
            // enter serial. AT_RANDOM uses its independent LinuxEntropy owner.
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxRandomScratch.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxRandomRetained.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceNonceRetained.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceNonceScratch.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceSequenceScratch.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceSequenceRetained.Data, 64);
            if (!initialized)
                return false;
            state->LinuxServiceRandomPhase = 2;
            return true;
        }

        private static bool ClearLinuxServiceRandom(Storage* state)
        {
            // Phase2 may still own a live worker/IRQ continuation. A cleanup
            // rejection must retain its borrowed generator, not erase it early.
            if (state->CooperativePhase == 2 || state->LinuxServiceSequence.IsBorrowed)
                return false;
            bool erased = true;
            if (state->LinuxServices.Initialized == 1 && state->LinuxServices.Owner.ContinuingRandom)
                erased = LinuxStartupServices.DiscardEntropy(&state->LinuxServices);
            else if (OS.Std.Hash.HmacSha256Drbg.IsInitialized(&state->LinuxServiceRandom))
                erased = OS.Std.Hash.HmacSha256Drbg.Erase(&state->LinuxServiceRandom);
            ulong sequences = state->LinuxServiceSequence.SequencesIssued;
            if (TcpInitialSequence.IsInitialized(&state->LinuxServiceSequence))
                erased = TcpInitialSequence.Erase(&state->LinuxServiceSequence) && erased;
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxRandomScratch.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxRandomRetained.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceNonceScratch.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceNonceRetained.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceSequenceScratch.Data, 64);
            OS.Std.Memory.SensitiveMemory.Clear(state->LinuxServiceSequenceRetained.Data, 64);
            state->LinuxServiceNonceCapture.SystemTable = null;
            state->LinuxServiceNonceCapture.BootServices = null;
            state->LinuxServiceNonceCapture.Locate = null;
            state->LinuxServiceNonceCapture.Rng = null;
            state->LinuxServiceNonceCapture.GetRng = null;
            state->LinuxServiceSequenceCapture.SystemTable = null;
            state->LinuxServiceSequenceCapture.BootServices = null;
            state->LinuxServiceSequenceCapture.Locate = null;
            state->LinuxServiceSequenceCapture.Rng = null;
            state->LinuxServiceSequenceCapture.GetRng = null;
            if (!erased || !FirmwareEntropyCapture.Zero((byte*)&state->LinuxServiceRandom, 0, sizeof(OS.Std.Hash.HmacSha256Drbg.State)) ||
                !FirmwareEntropyCapture.Zero(state->LinuxRandomScratch.Data, 0, 64) || !FirmwareEntropyCapture.Zero(state->LinuxRandomRetained.Data, 0, 64) ||
                !FirmwareEntropyCapture.Zero(state->LinuxServiceNonceScratch.Data, 0, 64) || !FirmwareEntropyCapture.Zero(state->LinuxServiceNonceRetained.Data, 0, 64) ||
                !FirmwareEntropyCapture.Zero((byte*)&state->LinuxServiceSequence, 0, sizeof(TcpInitialSequence.Owner)) ||
                !FirmwareEntropyCapture.Zero(state->LinuxServiceSequenceScratch.Data, 0, 64) || !FirmwareEntropyCapture.Zero(state->LinuxServiceSequenceRetained.Data, 0, 64))
                return false;
            bool report = state->LinuxServiceRandomPhase != 3 && state->LinuxServices.Initialized == 1;
            state->LinuxServiceRandomPhase = 3;
            return !report || LinuxText(state, "M11_012_END requests=") && LinuxNumber(state, state->LinuxServices.RandomRequests) &&
                LinuxText(state, " generatedBytes=") && LinuxNumber(state, state->LinuxServices.RandomBytes) &&
                LinuxText(state, " generator=zero seed=zero firmwareCallbacks=none\r\n") &&
                LinuxText(state, "M07_033_END issued=") && LinuxNumber(state, sequences) &&
                LinuxText(state, " secret=zero seed=zero lease=released firmwareCallbacks=none\r\n");
        }
#endif

        private static bool ClearLinuxRandom(Storage* state)
        {
#if M16_014_PROBE
            if (!ClearLinuxServiceRandom(state))
                return false;
#endif
            bool consumed = LinuxRandomBudget(state) != FirmwareEntropyReservoir.Bytes ||
                state->LinuxServices.RemainingEntropy == 0 || LinuxStartupServices.DiscardEntropy(&state->LinuxServices);
            for (uint i = 0; i < 64; i++)
            {
                state->LinuxRandomScratch.Data[i] = 0;
                state->LinuxRandomRetained.Data[i] = 0;
            }
            state->LinuxRandomCapture.SystemTable = null;
            state->LinuxRandomCapture.BootServices = null;
            state->LinuxRandomCapture.Locate = null;
            state->LinuxRandomCapture.Rng = null;
            state->LinuxRandomCapture.GetRng = null;
            return FirmwareEntropyReservoir.Clear(&state->LinuxRuntimeRandom) && consumed &&
                FirmwareEntropyCapture.Zero(state->LinuxRandomScratch.Data, 0, 64) &&
                FirmwareEntropyCapture.Zero(state->LinuxRandomRetained.Data, 0, 64);
        }

        // Evaluated after the BootSequence child returns, including admission
        // or owner failure. Do not free any unresolved worker/hardware storage.
        private static bool CompleteLinuxRandomLifetime(Storage* state, bool completed) => ClearLinuxRandom(state) && completed;

        private static bool InitializeLinuxMemory(Storage* state, LinuxSyscallAccess access)
        {
            state->LinuxMemoryCheckpoint = 1;
            uint entropyBytes = LinuxRandomBudget(state);
#if M16_014_PROBE
            bool entropyReady = state->LinuxServiceRandomPhase == 2 && OS.Std.Hash.HmacSha256Drbg.IsInitialized(&state->LinuxServiceRandom) &&
                TcpInitialSequence.IsInitialized(&state->LinuxServiceSequence) &&
                state->LinuxServiceSequenceCapture.Rng == null && state->LinuxServiceSequenceCapture.GetRng == null &&
                state->LinuxServiceSequenceCapture.SystemTable == null && state->LinuxServiceSequenceCapture.BootServices == null &&
                state->LinuxServiceSequenceCapture.Locate == null &&
                state->LinuxServiceNonceCapture.Rng == null && state->LinuxServiceNonceCapture.GetRng == null &&
                state->LinuxServiceNonceCapture.SystemTable == null && state->LinuxServiceNonceCapture.BootServices == null &&
                state->LinuxServiceNonceCapture.Locate == null;
#else
            bool entropyReady = entropyBytes == FirmwareEntropyReservoir.Bytes ?
                state->LinuxRuntimeRandom.Phase == 2 && state->LinuxRuntimeRandom.PublishedBytes == entropyBytes :
                state->LinuxRandomCapture.PublishedBytes == entropyBytes;
#endif
            if (state->LinuxMemoryPhase != 2 || state->Clock.Source != OwnedClockSource.Hpet || state->Clock.Status != OwnedClockStatus.Ready ||
                !entropyReady || state->LinuxRandomCapture.Rng != null ||
                state->LinuxRandomCapture.GetRng != null || state->LinuxRandomCapture.SystemTable != null ||
                state->LinuxRandomCapture.BootServices != null || state->LinuxRandomCapture.Locate != null || !LinuxSingleCpu(state))
                return false;
            var gcBudget = OS.Manifest.LinuxAppConfiguration.ReadGcBudgetFromReservations(
                (byte*)state->LinuxPackage + state->LinuxView.ManifestOffset, state->LinuxView.ManifestBytes,
                state->LinuxView.Manifest, (BootReservedRange*)state->LinuxLedger, state->LinuxLedgerCount,
                LinuxLedgerCapacity, state->LinuxMemoryPool, LinuxMemoryPoolPages(state),
                LinuxMemoryPoolPages(state) == OS.Manifest.LinuxAppConfiguration.ConfiguredServicePoolPages ?
                    OS.Manifest.LinuxAppConfiguration.ConfiguredServiceNativeReserveBytes : LinuxMemoryPoolPages(state) * Page / 2);
            if (!gcBudget.Succeeded && gcBudget.Result != OS.Manifest.LinuxAppConfiguration.Status.Unconfigured)
                return false;
#if !M16_014_PROBE
            if (gcBudget.TerminateOnPhysicalBackingExhaustion)
                return false;
#endif
            state->LinuxMemoryPhase = 3;
            state->LinuxMemoryCheckpoint = 2;
            LinuxMemoryServices.Admission memory = new()
            {
                Pool = (byte*)state->LinuxMemoryPool,
                PoolPhysical = state->LinuxMemoryPool,
                PoolPages = LinuxMemoryPoolPages(state),
                RootPhysical = state->LinuxUserTables,
                Tables = (ulong*)state->LinuxUserTables,
                CandidateTables = (ulong*)state->LinuxMemoryCandidate,
                TablePages = X64UserAddressSpace.MaximumTables,
                StaticRanges = (X64UserMappingRange*)state->LinuxUserRanges,
                StaticCount = state->LinuxUserRangeCount,
#if M16_009_PROBE
                // Only the owned loaded ELF grants data-protection authority.
                // The adjacent initial stack and retained supervisor pool do not.
                ImageVirtualStart = state->LinuxImagePlan.ImageStart,
                ImagePhysicalStart = state->LinuxImageBacking,
                ImageBytes = state->LinuxImagePlan.ImageBytes,
#endif
                ReservationBytes = 0x400000000,
                Conditions = state->Memory.Tables.Preconditions
            };
            if (!LinuxMemoryServices.Adopt(&state->LinuxMemory, memory, state->LinuxUserRoot))
                return false;
            state->LinuxMemoryCheckpoint = 3;
            if (!LinuxMemoryFacts(state, out ulong physical, out ulong available))
                return false;
            state->LinuxMemoryCheckpoint = 4;
            LinuxStartupServices.Admission startup = new()
            {
                Memory = &state->LinuxMemory,
                User = access,
                ReadClock = &ReadLinuxClock,
                CopyToUser = &CopyToLinuxUser,
                MemoryBarrier = &LinuxMemoryBarrier,
                FrequencyHz = state->Clock.FrequencyHz,
                PhysicalBytes = physical,
                AvailableBytes = available,
                HeapStart = 0x400000000,
                HeapBytes = 2 * 1024 * 1024,
                StackBytes = state->LinuxInitialStack.StackBytes,
                ProcessId = 1,
                GcBudget = gcBudget,
                Entropy = entropyBytes == FirmwareEntropyReservoir.Bytes ? state->LinuxRuntimeRandom.Data : state->LinuxRandomRetained.Data,
                CapturedEntropyBytes = entropyBytes,
                SingleCpuValidated = true,
                FirmwareExited = true
            };
#if M16_014_PROBE
            startup.Entropy = null;
            startup.Generator = &state->LinuxServiceRandom;
            startup.ContinuingRandom = true;
            startup.User.UserInterrupts = state->LinuxSyscallPlan.UserInterrupts;
#endif
#if M10_004_PROBE
            startup.Realtime = &state->Realtime;
            startup.RequireRealtime = true;
#endif
            if (!LinuxStartupServices.Begin(&state->LinuxServices, startup))
                return false;
            state->LinuxMemoryCheckpoint = 5;
            state->LinuxUserRoot = state->LinuxMemory.Root;
            state->LinuxMemoryPhase = 4;
            if (gcBudget.Succeeded && (!LinuxText(state, "M16_016_BUDGET gc=") || !LinuxNumber(state, gcBudget.GcLimitBytes) ||
                !LinuxText(state, " nativeReserve=") || !LinuxNumber(state, gcBudget.NativeReserveBytes) ||
                !LinuxText(state, " static=") || !LinuxNumber(state, gcBudget.StaticBytes) ||
                !LinuxText(state, " pool=") || !LinuxNumber(state, gcBudget.PoolBytes) ||
                !LinuxText(state, " entitlement=") || !LinuxNumber(state, gcBudget.EntitlementBytes) ||
                !LinuxText(state, "\r\n")))
                return false;
#if M16_014_PROBE
            if (!LinuxText(state, "M11_012_SEED source=firmware-rng acquisitions=2 entropyBytes=32 nonceBytes=16 domain=getrandom state=ready firmwareCallbacks=none\r\nM07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none\r\n"))
                return false;
#endif
            return LinuxText(state, "M16_006_POLICY physical=") && LinuxNumber(state, physical) &&
                LinuxText(state, " available=") && LinuxNumber(state, available) &&
                LinuxText(state, " poolPages=") && LinuxNumber(state, memory.PoolPages) && LinuxText(state, " vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=") && LinuxNumber(state, entropyBytes) &&
                LinuxText(state, " cpus=1 root=inactive-kernel\r\n");
        }

        private static bool LinuxMemoryFacts(Storage* state, out ulong physical, out ulong available)
        {
            physical = 0;
            available = LinuxMemoryPoolPages(state) * Page;
            var map = state->Arena.Transition->Attempt.Last.Map;
            // The final GetMemoryMap writes directly into the retained arena,
            // so it intentionally has no earlier snapshot's FlagOwnedCopy.
            if (state->Arena.Transition->Mode != FirmwareServicesMode.BootServicesExited || !state->Arena.Transition->Attempt.Succeeded ||
                map.DescriptorVersion != 1 || !UefiMemoryMapCopyPlanResult.Create(map, map.MapSize).Succeeded ||
                map.BufferAddress != (ulong)state->Arena.Map || map.BufferSize != FirstExitOwnedArena.MapBytes || map.SourceBufferAddress != map.BufferAddress ||
                map.SourceBufferSize != map.BufferSize)
                return false;
            for (ulong i = 0; i < map.DescriptorCount; i++)
            {
                byte* descriptor = (byte*)(map.BufferAddress + i * map.DescriptorSize);
                uint type = *(uint*)descriptor;
                ulong pages = *(ulong*)(descriptor + 24);
                if ((type >= 1 && type <= 7) || type == 9 || type == 10)
                {
                    if (pages > (0x100000000UL - physical) / Page)
                        return false;
                    physical += pages * Page;
                    if (type == 7)
                        available += pages * Page;
                }
            }
            return physical != 0 && available <= physical;
        }

        private static bool LinuxSingleCpu(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.ApicObservationOffset;
            XApicCpuObservation cpu = default;
            if (!X64ApicObservation.Validate(code, X64ApicObservation.Bytes) ||
                ((delegate* unmanaged<XApicCpuObservation*, uint>)code)(&cpu) != 1 ||
                (cpu.ApicBaseMsr & 0x100) == 0 || cpu.BspId != 0)
                return false;
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (!ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity))
                    return false;
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                return selected.Succeeded && MADTLocalAPICIOAPICSeparatelyPollingProof.Parse(
                    copied->Bytes + 16 + selected.Madt.Offset, selected.Madt.Length, cpu.BspId, out _) == MadtPollingStatus.Success;
            }
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadLinuxClock(void* context, ulong* ticks)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            if (ticks == null || state->LinuxMemoryPhase < 3 || state->LinuxMemoryPhase > 4 ||
                state->AdapterConsole != null || state->AdapterServices != null ||
                !OwnedMonotonicClock.Read(ref state->Clock) || state->Clock.Source != OwnedClockSource.Hpet ||
                state->Clock.RecoveryAttempts > 1 || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return 0;
            *ticks = state->Clock.Ticks;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxMemoryBarrier(void* context)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            if (!LinuxSyscallAuthority(state) || state->LinuxMemoryPhase != 4)
                return 0;
            return ((delegate* unmanaged<byte>)(state->LinuxSyscallCode + X64LinuxUserTransition.MemoryBarrierOffset))();
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static long DispatchLinuxStartup(void* context, LinuxSyscallFrame* frame)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return -14;
#else
            Storage* state = (Storage*)context;
#endif
            var readRoot = (delegate* unmanaged<ulong>)(state->Memory.Code + FirstExitCodeBundle.LinuxReadBackOffset + X64LinuxElfReadBackBridge.ReadCr3Offset);
            if (!LinuxSyscallAuthority(state) || state->LinuxMemoryPhase != 4 || frame == null || readRoot() != state->LinuxDispatchRoot)
                return -14;
#if M16_014_PROBE
            long result = LinuxStartupServices.Dispatch(&state->LinuxServices, *frame, out bool physicalBackingExhausted);
            if (physicalBackingExhausted && LinuxQuotaPolicy(state))
            {
                LinuxNativeControl* c = LinuxNativeOwner(state);
                if (c == null || c->Phase != 3 || c->Selected == 0 || c->Selected > c->Capacity ||
                    c->Stopping != 0 || c->Failed != 0 || state->LinuxQuota != LinuxQuotaDisposition.None ||
                    c->Threads.Current.Value != c->Plan.Bindings[c->Selected - 1].Context.Identity)
                {
                    state->LinuxSyscallFailed = 1;
                    return -5;
                }
                state->LinuxQuota = LinuxQuotaDisposition.Pending;
            }
#else
            long result = LinuxStartupServices.Dispatch(&state->LinuxServices, *frame);
#endif
#if M16_014_PROBE
            if (frame->Number == 318 && !(LinuxText(state, "M11_012_RANDOM requested=") && LinuxNumber(state, frame->Argument1) &&
                LinuxText(state, " flags=") && LinuxNumber(state, frame->Argument2) &&
                LinuxText(state, " valueBits=") && LinuxNumber(state, unchecked((ulong)result)) &&
                LinuxText(state, " requests=") && LinuxNumber(state, state->LinuxServices.RandomRequests) &&
                LinuxText(state, " generatedBytes=") && LinuxNumber(state, state->LinuxServices.RandomBytes) && LinuxText(state, "\r\n")))
            {
                state->LinuxSyscallFailed = 1;
                return -5;
            }
#endif
            return result;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte CopyToLinuxUser(void* context, ulong destination, byte* source, uint bytes)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            uint maximum = Probes.EnableM16014Probe ? OS.Std.Hash.HmacSha256Drbg.MaximumRequestBytes : 4096;
            if (!LinuxSyscallAuthority(state) || bytes == 0 || bytes > maximum ||
                (ulong)source < state->LinuxSyscallData + X64LinuxUserTransition.StackOffset ||
                (ulong)source > state->LinuxSyscallData + X64LinuxUserTransition.DataBytes - bytes)
                return 0;
            return CopyLinuxAlias(state, destination, source, bytes, true);
        }

        private static byte CopyLinuxAlias(Storage* state, ulong address, byte* kernel, uint bytes, bool toUser)
        {
            // Resolve high user pages to actual owned kernel aliases while the
            // user root is inactive; admit the whole source/destination first.
            if (!LinuxSyscallGateway.ValidateRange(address, bytes, toUser ? OS.Kernel.Compatibility.LinuxX64.LinuxUserPageAccess.Write : OS.Kernel.Compatibility.LinuxX64.LinuxUserPageAccess.Read,
                new LinuxSyscallAccess { Context = state, PageAccess = &LinuxUserPageAccess }))
                return 0;
            for (ulong page = address & ~4095UL; page <= ((address + bytes - 1) & ~4095UL); page += Page)
            {
                if (!X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables,
                    X64UserAddressSpace.MaximumTables, page, out ulong leaf))
                    return 0;
                ulong physical = leaf & 0x000ffffffffff000;
                bool image = physical >= state->LinuxImageBacking && physical - state->LinuxImageBacking < state->LinuxImagePages * Page;
                bool pool = physical >= state->LinuxMemoryPool && physical - state->LinuxMemoryPool < LinuxMemoryPoolPages(state) * Page;
                if (!image && !pool)
                    return 0;
            }
            // The admitted single-owner mapping stays stable for this synchronous
            // copy. Resolve each page separately and preserve forward byte order.
            uint copied = 0;
            while (copied < bytes)
            {
                ulong current = address + copied;
                _ = X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables,
                    X64UserAddressSpace.MaximumTables, current, out ulong leaf);
                byte* alias = (byte*)((leaf & 0x000ffffffffff000) + (current & 4095));
                uint chunk = (uint)(Page - (current & 4095));
                if (chunk > bytes - copied)
                    chunk = bytes - copied;
                for (uint i = 0; i < chunk; i++)
                {
                    if (toUser)
                        alias[i] = kernel[copied + i];
                    else
                        kernel[copied + i] = alias[i];
                }
                copied += chunk;
            }
            return 1;
        }
#endif

#if M16_007_PROBE
        private static bool PrepareLinuxDescriptors(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->Phase != 2 || state->LinuxMemoryPhase != 2 || state->LinuxDescriptorPhase != 0)
                return false;
            state->LinuxDescriptorPhase = 1;
            if (!AllocateLinuxStartup(state, state->SystemTable->BootServices, LinuxDescriptorPages, out state->LinuxDescriptorArena))
                return false;
            ulong* starts = stackalloc ulong[8] { state->LinuxPackage, state->LinuxImageBacking, state->LinuxUserTables,
                state->LinuxSyscallCode, state->LinuxSyscallData, state->LinuxMemoryPool, state->LinuxMemoryCandidate, (ulong)state };
            ulong* lengths = stackalloc ulong[8] { state->LinuxPages * Page, state->LinuxImagePages * Page,
                X64UserAddressSpace.MaximumTables * Page, Page, X64LinuxUserTransition.DataBytes,
                LinuxMemoryPoolPages(state) * Page, X64UserAddressSpace.MaximumTables * Page, (ulong)sizeof(Storage) };
            for (uint i = 0; i < 8; i++)
                if (!Disjoint(state->LinuxDescriptorArena, LinuxDescriptorPages * Page, starts[i], lengths[i]))
                    return false;
            for (uint i = 0; i < LinuxDescriptorPages * Page; i++)
                ((byte*)state->LinuxDescriptorArena)[i] = 0;
            state->LinuxDescriptorPhase = 2;
            return true;
        }

        private static bool ReleaseLinuxDescriptors(Storage* state, EfiBootServices* services)
        {
            if (state->LinuxDescriptorArena == 0)
                return true;
            if (services == null || services->FreePages == null || services->FreePages(state->LinuxDescriptorArena, LinuxDescriptorPages) != 0)
                return false;
            state->LinuxDescriptorArena = 0;
            state->LinuxDescriptorPhase = 7;
            return true;
        }

        private static bool InitializeLinuxDescriptors(Storage* state)
        {
            if (state->LinuxDescriptorPhase != 2 || state->LinuxMemoryPhase != 4 ||
                state->LinuxView.Manifest.Descriptors != LinuxDescriptorSlots ||
                !LinuxMemoryFacts(state, out ulong physical, out ulong available))
                return false;
            // Retained package metadata stays below the first snapshot page;
            // views and their copied paths outlive every borrowed descriptor.
            if (sizeof(LinuxDescriptorServices.Slot) * LinuxDescriptorSlots > LinuxDescriptorDescriptions ||
                sizeof(LinuxDescriptorServices.Description) * LinuxDescriptorSlots > LinuxDescriptorPipeMetadata - LinuxDescriptorDescriptions ||
                sizeof(LinuxPipe) * 2 > LinuxDescriptorViews - LinuxDescriptorPipeMetadata ||
                sizeof(LinuxDescriptorServices.View) * LinuxDescriptorServices.MaximumViews > LinuxDescriptorPaths - LinuxDescriptorViews ||
                LinuxDescriptorPaths + LinuxDescriptorServices.MaximumViews * LinuxDescriptorServices.MaximumPath > LinuxDescriptorSnapshots)
                return false;
            byte* arena = (byte*)state->LinuxDescriptorArena;
            LinuxDescriptorServices.View* view = (LinuxDescriptorServices.View*)(arena + LinuxDescriptorViews);
            if (!LinuxDescriptorServices.CreatePackageViews((byte*)state->LinuxPackage, state->LinuxView.ByteCount,
                view, LinuxDescriptorServices.MaximumViews, arena + LinuxDescriptorPaths,
                LinuxDescriptorServices.MaximumViews * LinuxDescriptorServices.MaximumPath, out uint viewCount))
                return false;
            LinuxDescriptorServices.Admission owner = new()
            {
                Slots = (LinuxDescriptorServices.Slot*)arena,
                Descriptions = (LinuxDescriptorServices.Description*)(arena + LinuxDescriptorDescriptions),
                Pipes = (LinuxPipe*)(arena + LinuxDescriptorPipeMetadata),
                Views = view,
                ViewCount = viewCount,
                PackageProcessId = 1,
                Snapshots = arena + LinuxDescriptorSnapshots,
                PipeBuffers = arena + LinuxDescriptorPipes,
                Scratch = arena + LinuxDescriptorScratch,
                ScratchBytes = 8192,
                DescriptorCount = LinuxDescriptorSlots,
                DescriptionCount = LinuxDescriptorSlots,
                PipeCount = 2,
                PipeCapacity = 4096,
                Memory = &state->LinuxMemory,
                PhysicalBytes = physical,
                AvailableBytes = available,
                User = new()
                {
#if M16_027_PROBE
                    Context = LinuxUserCallbackContext(state),
#else
                    Context = state,
#endif
                    PageAccess = &LinuxUserPageAccess,
#if M16_008_PROBE
                    CopyFromUser = &LinuxThreadCopyInput,
#else
                    CopyFromUser = &CopyDescriptorInput,
#endif
                    Write = &WriteDescriptorOutput
                },
#if M16_008_PROBE
                CopyToUser = &LinuxThreadCopyOutput,
#else
                CopyToUser = &CopyDescriptorOutput,
#endif
                FirmwareExited = true,
                SingleOwnerValidated = true,
                SigpipeIgnored = true
            };
#if M10_004_PROBE
            if (!LinuxAppLogBudget.TryReadRecordLimit((byte*)state->LinuxPackage + state->LinuxView.ManifestOffset,
                state->LinuxView.ManifestBytes, state->LinuxView.Manifest, out uint logRecords) ||
                !state->LinuxLogBudget.Initialize(LinuxAppLogBudget.DefaultByteLimit, logRecords))
                return false;
            state->LinuxLogRecordLimit = logRecords;
#endif
#if M16_027_PROBE
            if (!LinuxUserCarrierCurrent(state, owner.User.Context, (ulong)sizeof(LinuxUserCarrier)))
                return false;
#endif
            if (!LinuxDescriptorServices.Begin(&state->LinuxDescriptors, owner))
                return false;
            state->LinuxDescriptorPhase = 4;
            return LinuxText(state,
#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE
                "M16_007_POLICY descriptors=32 pipes=2 capacity=4096 scratch=8192 packageBytes=") &&
#else
                "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=") &&
#endif
                LinuxNumber(state, view->Bytes) && LinuxText(state,
#if M16_008_PROBE
                " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler\r\n");
#else
                " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=unsupported\r\n");
#endif
        }

        private static bool DescriptorKernelRange(Storage* state, byte* pointer, uint bytes, bool output)
        {
            ulong start = (ulong)pointer;
            if (pointer == null || bytes == 0 || bytes > LinuxDescriptorServices.MaximumIo || start > ~0UL - bytes)
                return false;
            ulong stack = state->LinuxSyscallData + X64LinuxUserTransition.StackOffset;
            if (start >= stack && start + bytes <= state->LinuxSyscallData + X64LinuxUserTransition.DataBytes)
                return true;
            ulong scratch = state->LinuxDescriptorArena + LinuxDescriptorScratch;
            if (start >= scratch && start + bytes <= scratch + 8192)
                return true;
            if (!output)
                return false;
            ulong snapshots = state->LinuxDescriptorArena + LinuxDescriptorSnapshots;
            if (start >= snapshots && start + bytes <= snapshots + LinuxDescriptorSlots * LinuxDescriptorServices.SnapshotBytes)
                return true;
            return LinuxDescriptorServices.ContainsPackageData(&state->LinuxDescriptors, pointer, bytes);
        }

        private static bool LinuxDescriptorReleaseChecked(Storage* state)
        {
            var owner = state->LinuxDescriptors.Owner;
            for (uint i = 0; i < owner.DescriptorCount; i++)
                if (owner.Slots[i].Description != 0 || owner.Slots[i].Flags != 0)
                    return false;
            for (uint i = 0; i < owner.DescriptionCount * sizeof(LinuxDescriptorServices.Description); i++)
                if (((byte*)owner.Descriptions)[i] != 0)
                    return false;
            for (uint i = 0; i < owner.PipeCount; i++)
                if (owner.Pipes[i].Initialized || owner.Pipes[i].ReaderCount != 0 || owner.Pipes[i].WriterCount != 0)
                    return false;
            byte* bytes = (byte*)(state->LinuxDescriptorArena + LinuxDescriptorSnapshots);
            for (uint i = 0; i < LinuxDescriptorPages * Page - LinuxDescriptorSnapshots; i++)
                if (bytes[i] != 0)
                    return false;
            return true;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte CopyDescriptorInput(void* context, ulong address, byte* destination, uint bytes)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            return LinuxSyscallAuthority(state) && state->LinuxDescriptorPhase == 4 && DescriptorKernelRange(state, destination, bytes, false)
                ? CopyLinuxAlias(state, address, destination, bytes, false) : (byte)0;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte CopyDescriptorOutput(void* context, ulong address, byte* source, uint bytes)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            return LinuxSyscallAuthority(state) && state->LinuxDescriptorPhase == 4 && DescriptorKernelRange(state, source, bytes, true)
                ? CopyLinuxAlias(state, address, source, bytes, true) : (byte)0;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte WriteDescriptorOutput(void* context, uint channel, byte* source, uint bytes)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            if (!LinuxSyscallAuthority(state) || state->LinuxDescriptorPhase != 4 || (channel != 1 && channel != 2) ||
                source != (byte*)(state->LinuxDescriptorArena + LinuxDescriptorScratch) || bytes == 0 || bytes > 8192)
                return 0;
#if M10_004_PROBE
            // The shared syscall gate serializes all writers. Charge before any
            // serial side effect; a partial/failed sink never refunds its quota.
            var charge = state->LinuxLogBudget.Charge(channel, bytes);
            if (charge != LinuxAppLogBudget.ChargeStatus.Accepted)
            {
                if (charge == LinuxAppLogBudget.ChargeStatus.QuotaExceeded && state->LinuxLogRecordLimit == LinuxAppLogBudget.QuotaRecordLimit)
                    _ = LinuxWebObserveQuotaDenial(state, channel, bytes);
                return 0;
            }
#endif
            if (!LinuxText(state, "M16_005_WRITE fd=") || !LinuxNumber(state, channel) || !LinuxText(state, " bytes=") ||
                !LinuxNumber(state, bytes) || !LinuxText(state, " data="))
                return 0;
            for (uint i = 0; i < bytes; i++)
                if (!LinuxByte(state, source[i]))
                    return 0;
            if (!LinuxText(state, "\r\n"))
                return 0;
            state->LinuxSyscallWrites++;
#if M10_017_PROBE || M10_018_PROBE
            return LinuxStaticObserveOutput(state, channel, source, bytes) ? (byte)1 : (byte)0;
#elif M10_004_PROBE
            return LinuxWebObserveOutput(state, channel, source, bytes) ? (byte)1 : (byte)0;
#else
            return 1;
#endif
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static long DispatchLinuxDescriptors(void* context, LinuxSyscallFrame* frame)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return -14;
#else
            Storage* state = (Storage*)context;
#endif
            var readRoot = (delegate* unmanaged<ulong>)(state->Memory.Code + FirstExitCodeBundle.LinuxReadBackOffset + X64LinuxElfReadBackBridge.ReadCr3Offset);
            if (!LinuxSyscallAuthority(state) || state->LinuxDescriptorPhase != 4 || frame == null || readRoot() != state->LinuxDispatchRoot)
                return -14;
            return LinuxDescriptorServices.Dispatch(&state->LinuxDescriptors, *frame);
        }
#endif

        private static X64LinuxUserTransitionPlan LinuxUserTransitionPlan(Storage* state, ulong rsp)
        {
#if M16_009_PROBE
            if (!LinuxUserInterruptPolicy.TryRead((byte*)state->LinuxPackage + state->LinuxView.ManifestOffset,
                state->LinuxView.ManifestBytes, state->LinuxView.Manifest, out bool userIrqs))
                return default;
#endif
            return new()
            {
                CodeAddress = state->LinuxSyscallCode,
                ContextAddress = state->LinuxSyscallData,
                UserRoot = state->LinuxUserTables,
                UserRip = state->LinuxImagePlan.Entry,
                UserRsp = rsp,
#if M16_006_PROBE
                KernelRoot = state->LinuxDispatchRoot,
#endif
#if M16_008_PROBE
                ResumeUser = true,
#endif
#if M16_009_PROBE
                UserFaultEntry = state->CooperativePool + LinuxThreadProgram.UserFaultCapture,
                UserInterrupts = userIrqs,
                UserSpuriousEntry = userIrqs ? state->CooperativePool + LinuxThreadProgram.TimerHandler + OS.Kernel.Threading.X64TimerInterrupt.SpuriousOffset : 0,
#endif
                Dispatcher = (ulong)(delegate* unmanaged<void*, LinuxSyscallFrame*, byte>)&DispatchLinuxSyscall,
                FaultReporter = (ulong)(delegate* unmanaged<void*, byte*, byte>)&ReportLinuxUserFault,
                CallbackContext = (ulong)state,
                TssRsp0Address = (ulong)(state->Arena.Descriptors->Tss + 4),
                OriginalGp = LinuxGateHandler(state->Arena.Descriptors->FinalIdt + 13 * 16),
                OriginalPf = LinuxGateHandler(state->Arena.Descriptors->FinalIdt + 14 * 16)
            };
        }

        private static bool LinuxSupervisorLeaf(Storage* state, ulong address, bool executable, bool writable)
        {
            if (!X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables,
                X64UserAddressSpace.MaximumTables, address, out ulong leaf) || (leaf & 5) != 1)
                return false;
            return ((leaf & 2) != 0) == writable && ((leaf & 0x8000000000000000) == 0) == executable;
        }

        private static bool LinuxSyscallAuthority(Storage* state)
        {
            byte* bridge = state->Memory.Code + FirstExitCodeBundle.LinuxReadBackOffset;
            if (!X64LinuxElfReadBackBridge.Validate(bridge, X64LinuxElfReadBackBridge.Bytes) ||
                (
#if M16_006_PROBE
                 ((delegate* unmanaged<ulong>)(bridge + X64LinuxElfReadBackBridge.ReadCr3Offset))() != state->LinuxDispatchRoot &&
#endif
                 ((delegate* unmanaged<ulong>)(bridge + X64LinuxElfReadBackBridge.ReadCr3Offset))() != state->LinuxUserRoot.Root))
                return false;
            return state->LinuxSyscallPhase == 4 && state->LinuxSyscallPlan.UserRoot == state->LinuxUserRoot.Root &&
                state->Arena.Transition->Mode == FirmwareServicesMode.BootServicesExited &&
                state->Arena.Transition->RetainedFirmwareCallbacksCleared && state->Arena.Binding->RetainedPointersCleared;
        }

#if M09_011_PROBE || M09_012_PROBE || M16_010_PROBE
        // IRQ-off post-exit owner; the child retains the existing bounded native
        // run and cleanup. These outer records do not replace workload evidence.
        private static bool RunOwnedNativeAotRuntimeProfile(void* context)
        {
            Storage* state = (Storage*)context;
            if (state == null)
                return false;
#if M09_011_PROBE
            if (!LinuxText(state, "TEST_START m09.011\r\n"))
                return false;
#elif M09_012_PROBE
            if (!LinuxText(state, "TEST_START m09.012\r\n"))
                return false;
#else
            if (!LinuxText(state, "TEST_START m16-010\r\n"))
                return false;
#endif
            bool completed = RunOwnedLinuxSyscalls(context) && state->LinuxSyscallExitCode == 0;
            if (completed)
                return true;
#if M09_011_PROBE
            _ = LinuxText(state, "TEST_FAIL m09.011 native-runtime\r\n");
#elif M09_012_PROBE
            _ = LinuxText(state, "TEST_FAIL m09.012 native-runtime\r\n");
#else
            _ = LinuxText(state, "TEST_FAIL m16-010 native-runtime\r\n");
#endif
            return false;
        }
#endif

        private static bool RunOwnedLinuxSyscalls(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->Phase != 4 || state->LinuxSyscallPhase != 2 || state->LinuxStartupPhase != 6 ||
                !state->ServiceLoop.IsStopped || state->LinuxOwnedPhase != 2 || !state->LinuxUserRoot.Succeeded ||
                state->Arena.Transition->Mode != FirmwareServicesMode.BootServicesExited ||
                !state->Arena.Transition->RetainedFirmwareCallbacksCleared || !state->Arena.Binding->RetainedPointersCleared ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->LinuxSyscallPhase = 3;
#if M16_009_PROBE
            if (state->ApicPhase != 2 || !LinuxText(state, "TEST_START m16-009\r\n"))
                return false;
#elif M16_008_PROBE
            if (state->ApicPhase != 2 || !LinuxText(state, "TEST_START m16-008\r\n"))
                return false;
#elif M16_007_PROBE
            if (!LinuxText(state, "TEST_START m16-007\r\n"))
                return false;
#elif M16_006_PROBE
            if (!LinuxText(state, "TEST_START m16-006\r\n"))
                return false;
#else
            if (!LinuxText(state, "TEST_START m16-005\r\n"))
                return false;
#endif
            var plan = LinuxUserTransitionPlan(state, state->LinuxInitialStack.StackPointer);
            byte* code = (byte*)state->LinuxSyscallCode;
            byte* data = (byte*)state->LinuxSyscallData;
            byte* cpu = state->Memory.Code + FirstExitCodeBundle.ClockAccessOffset;
            if (!X64OwnedClockAccess.Validate(cpu, X64OwnedClockAccess.Bytes))
                return LinuxSyscallAdmissionFailure(state, "clock-code");
            uint* cpuidWords = stackalloc uint[4];
            var cpuid = (delegate* unmanaged<uint, uint, uint*, void>)(cpu + X64OwnedClockAccess.CpuidOffset);
            cpuid(1, 0, cpuidWords);
            // CPUID.1 EBX[23:16] is valid only when EDX.HTT is set.
            if (!X64LinuxUserTransition.SingleLogicalProcessor(cpuidWords[1], cpuidWords[3]))
                return LinuxSyscallAdmissionFailure(state, "logical-cpu", cpuidWords[1]);
            cpuid(0x80000000, 0, cpuidWords);
            if (cpuidWords[0] < 0x80000001)
                return LinuxSyscallAdmissionFailure(state, "extended-cpuid", cpuidWords[0]);
            cpuid(0x80000001, 0, cpuidWords);
            if ((cpuidWords[3] & 0x20100800) != 0x20100800)
                return LinuxSyscallAdmissionFailure(state, "syscall-nx-lm", cpuidWords[3]);
            if (!X64LinuxUserTransition.ValidateReadBack(code, X64LinuxUserTransition.CodeBytes, plan) ||
                !X64UserAddressSpace.Audit((X64UserMappingRange*)state->LinuxUserRanges, state->LinuxUserRangeCount,
                    (ulong*)state->LinuxUserTables, state->LinuxUserTables, X64UserAddressSpace.MaximumTables,
                    state->Memory.Tables.Preconditions, state->LinuxUserRoot) ||
                !LinuxSupervisorLeaf(state, plan.CodeAddress, true, false) ||
                !LinuxSupervisorLeaf(state, plan.Dispatcher, true, false) ||
                !LinuxSupervisorLeaf(state, plan.FaultReporter, true, false) ||
                !LinuxSupervisorLeaf(state, plan.CallbackContext, false, true))
                return LinuxSyscallAdmissionFailure(state, "code-root-callbacks");
            for (uint page = 0; page < X64LinuxUserTransition.DataPages; page++)
            {
                ulong address = plan.ContextAddress + page * Page;
                if (page == 3)
                {
                    if (!X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables,
                        X64UserAddressSpace.MaximumTables, address, out ulong guard) || guard != 0)
                        return LinuxSyscallAdmissionFailure(state, "guard", page);
                }
                else if (!LinuxSupervisorLeaf(state, address, false, true))
                    return LinuxSyscallAdmissionFailure(state, "context-stack", page);
            }
            ulong* original = state->LinuxSyscallOriginal;
            for (uint i = 0; i < X64LinuxUserTransition.ObservationWords; i++)
                original[i] = 0;
            var observe = (delegate* unmanaged<ulong*, void>)(code + X64LinuxUserTransition.ObserveOffset);
            observe(original);
            if (original[1] != (ulong)state->Memory.Entries || original[4] != 8 || original[5] != 16 || original[6] != 24 ||
                !X64LinuxUserTransition.AdmitCpu(original[0], original[2], original[10], (uint)original[7], (uint)original[9], original[3]))
            {
                if (!LinuxText(state, "M16_005_CPU cr0=") || !LinuxNumber(state, original[0]) ||
                !LinuxText(state, " cr4=") || !LinuxNumber(state, original[2]) ||
                !LinuxText(state, " efer=") || !LinuxNumber(state, original[10]) ||
                !LinuxText(state, " flags=") || !LinuxNumber(state, original[3]) ||
                !LinuxText(state, " cs=") || !LinuxNumber(state, original[4]) ||
                !LinuxText(state, " ss=") || !LinuxNumber(state, original[5]) ||
                !LinuxText(state, " tr=") || !LinuxNumber(state, original[6]) || !LinuxText(state, "\r\n"))
                    return false;
                return LinuxSyscallAdmissionFailure(state, "cpu-controls");
            }
            if (*(ushort*)(original + 17) != *(ushort*)state->Arena.Descriptors->Gdtr || *(ulong*)((byte*)(original + 17) + 2) != (ulong)state->Arena.Descriptors->Gdt ||
                *(ushort*)(original + 19) != 4095 || *(ulong*)((byte*)(original + 19) + 2) != (ulong)state->Arena.Descriptors->FinalIdt ||
                !X64LinuxUserTransition.Prepare(data, X64LinuxUserTransition.DataBytes, plan,
                    state->Arena.Descriptors->Gdt, 4096, state->Arena.Descriptors->FinalIdt, 4096))
                return LinuxSyscallAdmissionFailure(state, "descriptor-context");
            ulong fsBase = original[14], gsBase = original[15];
            ((delegate* unmanaged<void>)(code + X64LinuxUserTransition.EstablishSegmentsOffset))();
            observe(original);
            if (original[23] != 0 || original[24] != 0 || original[14] != fsBase || original[15] != gsBase)
                return LinuxSyscallAdmissionFailure(state, "owned-fs-gs");
            state->LinuxSyscallPlan = plan;
            LinuxSyscallAdmission admission = new()
            {
                UserRoot = plan.UserRoot,
                MaximumCalls = LinuxSyscallGateway.MaximumCalls,
                MaximumWrite = LinuxSyscallGateway.MaximumWrite,
                Scratch = data + X64LinuxUserTransition.ScratchOffset,
                ScratchBytes = 4096,
                FirmwareExited = true,
                OwnedBoundaryValidated = true
            };
#if M16_027_PROBE
            if (!PrepareLinuxUserCarrier(state) || LinuxUserCallbackContext(state) == null)
                return LinuxSyscallAdmissionFailure(state, "user-carrier");
#endif
            LinuxSyscallAccess access = new()
            {
#if M16_027_PROBE
                Context = LinuxUserCallbackContext(state),
#else
                Context = state,
#endif
                PageAccess = &LinuxUserPageAccess,
#if M16_008_PROBE
                CopyFromUser = &LinuxThreadCopyInput,
#else
                CopyFromUser = &CopyLinuxUserBytes,
#endif
                Write = &WriteLinuxUserBytes
            };
#if M16_006_PROBE
            if (state->LinuxDispatchRoot != (ulong)state->Memory.Entries || !InitializeLinuxMemory(state, access))
                return LinuxSyscallAdmissionFailure(state, "memory-startup", state->LinuxMemoryCheckpoint);
            admission.EnableStartup = true;
            access.Startup = &DispatchLinuxStartup;
#endif
#if M16_007_PROBE
            if (!InitializeLinuxDescriptors(state))
                return LinuxSyscallAdmissionFailure(state, "descriptors");
#if CLROS_LINUX_SOCKETS
            if (!InitializeLinuxSocketNetwork(state))
                return LinuxSyscallAdmissionFailure(state, "socket-network");
#endif
            admission.EnableDescriptors = true;
            access.Descriptors = &DispatchLinuxDescriptors;
#endif
#if !M16_008_PROBE
            if (LinuxSyscallGateway.Begin(ref state->LinuxSyscallState, admission, access) != LinuxSyscallGateStatus.Active)
                return false;
#endif
            if (!LinuxText(state, "M16_005_ARMED root=") || !LinuxNumber(state, plan.UserRoot) ||
                !LinuxText(state, " cs=59 ss=51 kernelStack=") || !LinuxNumber(state, plan.ContextAddress + X64LinuxUserTransition.DataBytes) ||
                !LinuxText(state, " guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null\r\n"))
                return false;
            state->LinuxSyscallPhase = 4;
#if M16_008_PROBE
            bool executed = RunLinuxNativeOwner(state);
            ulong* finalCpu = stackalloc ulong[(int)X64LinuxUserTransition.ObservationWords];
            observe(finalCpu);
#if M16_014_PROBE
            bool quotaNativeCleaned = !executed && LinuxQuotaPolicy(state) &&
                state->LinuxQuota == LinuxQuotaDisposition.NativeCleaned;
            bool ownerReturned = executed || quotaNativeCleaned;
#else
            bool ownerReturned = executed;
#endif
            bool restored = ownerReturned && *(ulong*)(data + X64LinuxUserTransition.PhaseOffset) == 2 &&
                *(ulong*)(state->Arena.Descriptors->Tss + 4) == *(ulong*)(data + 0xd0) &&
                (finalCpu[3] & 0x74700) == (original[3] & 0x74700) && FirstExitOwnedArena.ValidateCanaries(state->Arena);
            for (uint i = 0; i < X64LinuxUserTransition.ObservationWords; i++)
                if (i != 3 && finalCpu[i] != original[i])
                    restored = false;
            for (uint i = 0; i < 512; i++)
                if (data[X64LinuxUserTransition.KernelFxOffset + i] != data[X64LinuxUserTransition.UserFxOffset + i])
                    restored = false;
#if M16_014_PROBE
            if (quotaNativeCleaned)
            {
                if (!restored || state->LinuxQuota != LinuxQuotaDisposition.NativeCleaned ||
                    !LinuxText(state, "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true\r\n"))
                    return false;
                state->LinuxQuota = LinuxQuotaDisposition.CpuRestored;
                return false; // Expected process failure; outer NIC ownership still must retire.
            }
#endif
#if M16_009_PROBE
            if (!restored)
            {
                _ = LinuxText(state, "TEST_FAIL m16-009 native-return\r\n");
                return false;
            }
            return restored && LinuxText(state, "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true\r\n") &&
                LinuxText(state, "CLROS_M16_009_READY\r\nTEST_PASS m16-009\r\n");
#else
            return restored && LinuxText(state, "M16_008_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true\r\n") &&
                LinuxText(state, "CLROS_M16_008_READY\r\nTEST_PASS m16-008\r\n");
#endif
#else
            byte returned = ((delegate* unmanaged<byte>)code)();
            ulong* after = stackalloc ulong[(int)X64LinuxUserTransition.ObservationWords];
            for (uint i = 0; i < X64LinuxUserTransition.ObservationWords; i++)
                after[i] = 0;
            observe(after);
            bool restored = returned == 1 && *(ulong*)(data + X64LinuxUserTransition.PhaseOffset) == 2 &&
                *(ulong*)(state->Arena.Descriptors->Tss + 4) == *(ulong*)(data + 0xd0) &&
                (after[3] & 0x74700) == (original[3] & 0x74700) && FirstExitOwnedArena.ValidateCanaries(state->Arena);
            for (uint i = 0; i < X64LinuxUserTransition.ObservationWords; i++)
                if (i != 3 && after[i] != original[i])
                    restored = false;
            for (uint i = 0; i < 512; i++)
                if (data[X64LinuxUserTransition.KernelFxOffset + i] != data[X64LinuxUserTransition.UserFxOffset + i])
                    restored = false;
            if (!restored || !LinuxText(state, "M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true\r\n"))
                return false;
            state->LinuxSyscallPhase = 6;
#if M16_006_PROBE
            state->LinuxMemoryPhase = 6;
            if (!ClearLinuxRandom(state))
                return false;
            if (!LinuxMemoryServices.Audit(&state->LinuxMemory))
                return false;
#endif
#if M16_007_PROBE
            if (!LinuxDescriptorServices.Audit(&state->LinuxDescriptors) || !LinuxDescriptorServices.End(&state->LinuxDescriptors) ||
                !LinuxDescriptorReleaseChecked(state))
                return false;
            state->LinuxDescriptorPhase = 6;
#endif
            if (state->LinuxSyscallFaults != 0 || state->LinuxSyscallFailed != 0 || state->LinuxSyscallState.Status != LinuxSyscallGateStatus.Exited)
            {
#if M16_007_PROBE
                _ = LinuxText(state, "TEST_FAIL m16-007 user-boundary-terminated status=");
#elif M16_006_PROBE
                _ = LinuxText(state, "TEST_FAIL m16-006 user-boundary-terminated status=");
#else
                _ = LinuxText(state, "TEST_FAIL m16-005 user-boundary-terminated status=");
#endif
                _ = LinuxNumber(state, (uint)state->LinuxSyscallState.Status);
                _ = LinuxText(state, "\r\n");
                return false;
            }
            return LinuxText(state, "M16_005_EXIT code=") && LinuxNumber(state, state->LinuxSyscallExitCode) &&
                LinuxText(state, " calls=") && LinuxNumber(state, state->LinuxSyscallState.Calls) &&
                LinuxText(state, " bytes=") && LinuxNumber(state, state->LinuxSyscallState.BytesWritten) &&
                LinuxText(state, " writes=") && LinuxNumber(state, state->LinuxSyscallWrites) &&
#if M16_006_PROBE
                LinuxText(state, "\r\nM16_006_MEMORY reserved=") && LinuxNumber(state, state->LinuxMemory.ReservedBytes) &&
                LinuxText(state, " backed=") && LinuxNumber(state, state->LinuxMemory.BackedPages) &&
                LinuxText(state, " tables=") && LinuxNumber(state, state->LinuxMemory.Root.UsedTables) &&
                LinuxText(state, " entropyRemaining=") && LinuxNumber(state, state->LinuxServices.RemainingEntropy) &&
#if M16_007_PROBE
                LinuxText(state, "\r\nM16_007_RELEASE descriptors=0 pipes=0 scratch=zero snapshots=zero\r\nCLROS_M16_007_READY\r\nTEST_PASS m16-007\r\n");
#else
                LinuxText(state, "\r\nCLROS_M16_006_READY\r\nTEST_PASS m16-006\r\n");
#endif
#else
                LinuxText(state, "\r\nCLROS_M16_005_READY\r\nTEST_PASS m16-005\r\n");
#endif
#endif
        }

        private static bool LinuxSyscallAdmissionFailure(Storage* state, string stage, ulong detail = 0)
        {
#if M16_008_PROBE
            _ = LinuxText(state, "TEST_FAIL m16-008 admission stage=");
#elif M16_007_PROBE
            _ = LinuxText(state, "TEST_FAIL m16-007 admission stage=");
#elif M16_006_PROBE
            _ = LinuxText(state, "TEST_FAIL m16-006 admission stage=");
#else
            _ = LinuxText(state, "TEST_FAIL m16-005 admission stage=");
#endif
            _ = LinuxText(state, stage);
            _ = LinuxText(state, " detail=");
            _ = LinuxNumber(state, detail);
            _ = LinuxText(state, "\r\n");
            return false;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte DispatchLinuxSyscall(void* context, LinuxSyscallFrame* frame)
        {
#if M16_008_PROBE
            return DispatchLinuxNativeCore((Storage*)context, frame);
#else
            Storage* state = (Storage*)context;
            if ((ulong)context != state->Memory.References.AdapterState || frame != (LinuxSyscallFrame*)(state->LinuxSyscallData + X64LinuxUserTransition.FrameOffset) ||
                !LinuxSyscallAuthority(state))
                return 0;
            var result = LinuxSyscallGateway.Dispatch(ref state->LinuxSyscallState, *frame);
#if M16_006_PROBE
            state->LinuxUserRoot = state->LinuxMemory.Root;
#endif
            byte* data = (byte*)state->LinuxSyscallData;
            *(long*)(data + X64LinuxUserTransition.ValueOffset) = result.Value;
            *(ulong*)(data + X64LinuxUserTransition.OutcomeOffset) = (ulong)result.Action;
            if (result.Action == LinuxSyscallAction.Terminate && state->LinuxSyscallState.Status == LinuxSyscallGateStatus.Exited)
                state->LinuxSyscallExitCode = result.ExitCode;
            if (!LinuxText(state, "M16_005_CALL nr=") || !LinuxNumber(state, frame->Number) ||
                !LinuxText(state, " a0=") || !LinuxNumber(state, frame->Argument0) ||
                !LinuxText(state, " a1=") || !LinuxNumber(state, frame->Argument1) ||
                !LinuxText(state, " a2=") || !LinuxNumber(state, frame->Argument2) ||
                !LinuxText(state, " a3=") || !LinuxNumber(state, frame->Argument3) ||
                !LinuxText(state, " a4=") || !LinuxNumber(state, frame->Argument4) ||
                !LinuxText(state, " a5=") || !LinuxNumber(state, frame->Argument5) ||
                !LinuxText(state, " rip=") || !LinuxNumber(state, frame->Rip) ||
                !LinuxText(state, " rsp=") || !LinuxNumber(state, frame->Rsp) ||
                !LinuxText(state, " flags=") || !LinuxNumber(state, frame->Rflags) ||
                !LinuxText(state, " valueBits=") || !LinuxNumber(state, unchecked((ulong)result.Value)) ||
                !LinuxText(state, " action=") || !LinuxNumber(state, (uint)result.Action) ||
                !LinuxText(state, " status=") || !LinuxNumber(state, (uint)state->LinuxSyscallState.Status) ||
                !LinuxText(state, "\r\n"))
            {
                state->LinuxSyscallFailed = 1;
                return 0;
            }
            return result.Action == LinuxSyscallAction.Return ? (byte)1 : (byte)2;
#endif
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxUserPageAccess(void* context, ulong address)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            if (!LinuxSyscallAuthority(state) || !X64UserAddressSpace.TryReadLeaf((ulong*)state->LinuxUserTables, state->LinuxUserTables,
                X64UserAddressSpace.MaximumTables, address, out ulong leaf) || (leaf & 5) != 5)
                return 0;
            return (byte)(1 | ((leaf & 2) != 0 ? 2 : 0) | ((leaf & 0x8000000000000000) == 0 ? 4 : 0));
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte CopyLinuxUserBytes(void* context, ulong source, byte* destination, uint bytes)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            if (!LinuxSyscallAuthority(state) || destination != (byte*)(state->LinuxSyscallData + X64LinuxUserTransition.ScratchOffset) ||
                bytes == 0 || bytes > 4096)
                return 0;
            // Dispatch has preflighted every page; mappings cannot change in this
            // single-CPU, IRQ-off boundary and the scratch has no user alias.
#if M16_006_PROBE
            return CopyLinuxAlias(state, source, destination, bytes, false);
#else
            for (uint i = 0; i < bytes; i++)
                destination[i] = ((byte*)source)[i];
            return 1;
#endif
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte WriteLinuxUserBytes(void* context, uint descriptor, byte* source, uint bytes)
        {
#if M16_027_PROBE
            Storage* state = ResolveLinuxUserCallbackStorage(context);
            if (state == null)
                return 0;
#else
            Storage* state = (Storage*)context;
#endif
            if (!LinuxSyscallAuthority(state) || (descriptor != 1 && descriptor != 2) ||
                source != (byte*)(state->LinuxSyscallData + X64LinuxUserTransition.ScratchOffset) || bytes == 0 || bytes > 4096 ||
                !LinuxText(state, "M16_005_WRITE fd=") || !LinuxNumber(state, descriptor) ||
                !LinuxText(state, " bytes=") || !LinuxNumber(state, bytes) || !LinuxText(state, " data="))
                return 0;
            for (uint i = 0; i < bytes; i++)
                if (!LinuxByte(state, source[i]))
                    return 0;
            if (!LinuxText(state, "\r\n"))
                return 0;
            state->LinuxSyscallWrites++;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReportLinuxUserFault(void* context, byte* data)
        {
            Storage* state = (Storage*)context;
#if M16_009_PROBE
            if (data == (byte*)state->LinuxSyscallData && *(ulong*)(data + X64LinuxUserTransition.VectorOffset) == 0xe1)
                return LinuxNativeUserInterrupt(state, data);
            return LinuxNativeUserFault(state, data);
#else
            if (!LinuxSyscallAuthority(state) || data != (byte*)state->LinuxSyscallData || state->LinuxSyscallFaults != 0 ||
                *(ushort*)(data + X64LinuxUserTransition.FaultSelectorsOffset) != X64LinuxUserTransition.UserCs ||
                *(ushort*)(data + X64LinuxUserTransition.FaultSelectorsOffset + 2) != X64LinuxUserTransition.UserSs)
                return 0;
            state->LinuxSyscallFaults = 1;
            bool written = LinuxText(state, "M16_005_FAULT vector=") && LinuxNumber(state, *(ulong*)(data + X64LinuxUserTransition.VectorOffset)) &&
                LinuxText(state, " error=") && LinuxNumber(state, *(ulong*)(data + X64LinuxUserTransition.ErrorOffset)) &&
                LinuxText(state, " rip=") && LinuxNumber(state, *(ulong*)(data + 0x138)) &&
                LinuxText(state, " rsp=") && LinuxNumber(state, *(ulong*)(data + 0x140)) &&
                LinuxText(state, " cr2=") && LinuxNumber(state, *(ulong*)(data + X64LinuxUserTransition.FaultAddressOffset)) &&
                LinuxText(state, " cs=59 ss=51 disposition=terminate\r\n");
            return written ? (byte)1 : (byte)0;
#endif
        }
#endif

        private static bool VerifyLinuxPackage(void* owner)
        {
            Storage* state = (Storage*)owner;
            if (state->LinuxOwnedPhase != 0)
                return false;
            state->LinuxOwnedPhase = 1;
            if (!LinuxText(state, "TEST_START m16-002\r\n"))
                return false;
            bool okay = state->LinuxPackage != 0 && state->LinuxClosedHandles == 2 && state->LinuxLedgerCount == LinuxLedgerCapacity &&
                state->Memory.References.LinuxPackageStart == state->LinuxPackage && state->Memory.References.LinuxPackagePages == state->LinuxPages &&
                BootReservationTableValidator.Validate((BootReservedRange*)state->LinuxLedger, LinuxLedgerCapacity, LinuxLedgerCapacity).Succeeded;
            var view = OS.Manifest.LinuxElfPackage.Validate((byte*)state->LinuxPackage, state->LinuxBytes);
            byte* packageHash = stackalloc byte[32];
            okay = okay && OS.Std.Hash.Sha256.TryCompute((byte*)state->LinuxPackage, state->LinuxBytes, packageHash, 32);
            for (uint i = 0; i < 32; i++)
                if (okay && packageHash[i] != state->LinuxPackageHash[i])
                    okay = false;
            okay = okay && view.Succeeded && view.PayloadOffset == state->LinuxView.PayloadOffset && view.PayloadBytes == state->LinuxView.PayloadBytes;
            byte* sample = stackalloc byte[64];
            uint actual = 0;
            okay = okay && OS.Manifest.LinuxElfPackage.ReadFile((byte*)state->LinuxPackage, state->LinuxBytes,
                (byte*)state->LinuxPackage + view.ManifestOffset + view.Manifest.Path.Offset, view.Manifest.Path.Length, 0, sample, 64, out actual) == OS.Manifest.LinuxElfStatus.Success && actual == 64;
            for (uint i = 0; i < actual; i++)
                if (sample[i] != ((byte*)state->LinuxPackage)[view.PayloadOffset + i])
                    okay = false;
            if (!okay)
            {
                _ = LinuxText(state, "TEST_FAIL m16-002 owned-package-validation\r\n");
                return false;
            }
            if (!LinuxText(state, "M16_002_STAGED bytes=") || !LinuxNumber(state, state->LinuxBytes) || !LinuxText(state, " payload=") || !LinuxNumber(state, view.PayloadBytes) ||
                !LinuxText(state, " pages=") || !LinuxNumber(state, state->LinuxPages) || !LinuxText(state, " handlesClosed=2 reads=") || !LinuxNumber(state, state->LinuxReadCalls) || !LinuxText(state, "\r\nM16_002_DIGEST "))
                return false;
            byte* digest = (byte*)state->LinuxPackage + view.ManifestOffset + view.Manifest.Digest.Offset;
            for (uint i = 0; i < 64; i++)
                if (!LinuxByte(state, digest[i]))
                    return false;
            if (!LinuxText(state, "\r\nM16_002_PACKAGE_DIGEST "))
                return false;
            for (uint i = 0; i < 32; i++)
            {
                uint high = (uint)packageHash[i] >> 4, low = (uint)packageHash[i] & 15;
                if (!LinuxByte(state, (byte)(high < 10 ? '0' + high : 'a' + high - 10)) || !LinuxByte(state, (byte)(low < 10 ? '0' + low : 'a' + low - 10)))
                    return false;
            }
            bool reported = LinuxText(state, "\r\nM16_002_OWNED firmware=exited package=read-only-nx ledger=valid fileBytes=64 entry=false\r\nCLROS_M16_002_READY\r\nTEST_PASS m16-002\r\n");
            if (reported)
                state->LinuxOwnedPhase = 2;
            return reported;
        }
        private static bool LinuxText(Storage* state, string text)
        {
            for (int i = 0; i < text.Length; i++)
                if (!LinuxByte(state, (byte)text[i]))
                    return false;
            return true;
        }
        private static bool LinuxNumber(Storage* state, uint n)
        {
            if (n >= 10 && !LinuxNumber(state, n / 10))
                return false;
            return LinuxByte(state, (byte)('0' + n % 10));
        }
        private static bool LinuxNumber(Storage* state, ulong n)
        {
            if (n >= 10 && !LinuxNumber(state, n / 10))
                return false;
            return LinuxByte(state, (byte)('0' + n % 10));
        }
        private static bool LinuxByte(Storage* state, byte b)
        {
            var backend = state->Arena.Fault->Backend;
            for (int i = 0; i < PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit; i++)
            {
                var read = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                if (!read.Succeeded)
                    return false;
                if ((read.Value & PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask) != 0)
                    return backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, b).Succeeded;
            }
            return false;
        }
#endif

        private static bool Disjoint(ulong a, ulong an, ulong b, ulong bn)
            => a <= ulong.MaxValue - an && b <= ulong.MaxValue - bn && (a + an <= b || b + bn <= a);

        private static bool TableExtent(byte* table, uint minimum, FirmwareMemoryMapRecord map, out ulong bytes, out uint memoryType)
        {
            bytes = 0;
            memoryType = 0xffffffff;
            if (table == null || !map.HasOwnedCopy() || !UefiMemoryMapCopyPlanResult.Create(map, map.MapSize).Succeeded)
                return false;
            uint size = *(uint*)(table + 12); // Original EFI entry/table contract supplies the header itself.
            ulong address = (ulong)table;
            if (size < minimum || size > Page || address > ulong.MaxValue - size)
                return false;
            uint matches = 0;
            for (ulong i = 0; i < map.DescriptorCount; i++)
            {
                byte* descriptor = (byte*)(map.BufferAddress + i * map.DescriptorSize);
                ulong start = *(ulong*)(descriptor + 8), pages = *(ulong*)(descriptor + 24);
                if (pages == 0 || pages > (ulong.MaxValue - start) / Page)
                    return false;
                if (address < start + pages * Page && start < address + size)
                {
                    uint type = *(uint*)descriptor;
                    memoryType = type;
                    // Firmware may keep a service table in its image's code
                    // descriptor (OVMF's DXE core does this). Admit service
                    // code/data types for reading, without granting execution
                    // or retaining these firmware pages after exit.
                    if (address < start || address + size > start + pages * Page ||
                        type < 3 || type > 6)
                        return false;
                    matches++;
                }
            }
            if (matches != 1)
                return false;
            bytes = size;
            return true;
        }

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

        // The dedicated pool is retained backing, not reclaimed firmware pages.
        // Borrow exactly20 pages through the existing allocator and unwind every
        // successful receipt in reverse, from the original guarded entry stack.
        private static bool RunCooperativeSwitch(void* context)
        {
            Storage* state = (Storage*)context;
            if (!CooperativeAuthority(state) || state->CooperativePhase != 0 || !CooperativeMarker(state, 0))
                return false;
            if (!FirstExitCodeBundle.ValidateInstalledReadBack(state->Memory.Code, FirstExitCodeBundle.Bytes, ref state->Arena,
                state->Memory.Capture, state->Memory.Mapping, state->Memory.References.FaultCallback))
                return false;
            // Establish the profile's owned NULL selectors before the first
            // CPU/mapping audit. The helper preserves both base MSRs and flags.
            ((delegate* unmanaged<void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeSegmentsOffset))();
            if (!CooperativeAuditPool(state, false) || !CooperativeAuditOriginalMappings(state))
            {
                _ = CooperativeRow(state, 4, 10, 0, 0, 0);
                _ = CooperativeMarker(state, 3);
                return false;
            }
            state->CooperativePhase = 1;
            ulong pool = state->CooperativePool;
            BootPageFreeListNode node = new(0, 0, pool, pool + 32 * Page, 32, 32 * Page, -1);
            BootPageAllocatorState allocator = default;
            if (!BootPageAllocatorStateMaterializer.Materialize(&node, 1, 1, 1, 0, Page, 32, 32 * Page, &allocator, 1).Succeeded)
            {
                _ = CooperativeMarker(state, 3);
                return false;
            }
            BootPageAllocatorState initial = allocator;
            BootPageFreeListNode initialNode = node;
            BootPageAllocatorOnePageResult* receipts = stackalloc BootPageAllocatorOnePageResult[20];
            ulong* originalEntries = stackalloc ulong[32];
            for (int i = 0; i < 32; i++)
                originalEntries[i] = 0;
            uint borrowed = 0;
            bool addresses = true;
            for (; borrowed < 20; borrowed++)
            {
                var receipt = BootPageAllocatorMutator.AllocateOnePage(allocator, &node, 1, 1, null, 0, 0, 1, Page);
                if (!receipt.Succeeded)
                    break;
                allocator = receipt.AfterState;
                receipts[borrowed] = receipt;
                addresses &= receipt.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong address) == AddressValueStatus.Success &&
                    address == pool + borrowed * Page && receipt.PageCount == 1 && receipt.ByteLength == Page;
            }
            CooperativeControl* control = null;
            bool prepared = borrowed == 20 && addresses && CooperativePrepareProgram(state, out control) &&
                CooperativeRow(state, 1, pool, 32, borrowed, allocator.FreePageCount);
            bool captured = false, published = false, transferred = false, restoredContext = false;
            if (prepared)
            {
                published = CooperativeSetPoolMappings(state, originalEntries, true, out captured) && CooperativeAuditPool(state, true);
                if (published)
                {
                    control->Plan.Owner.GuardsVerified = true;
                    control->Plan.Owner.MappingsVerified = true;
                    state->CooperativePhase = 2;
                    bool admitted = Cooperative.Admit(control->Plan.Owner, control->Plan.Original, control->Plan.A) == Cooperative.Status.Ok;
                    transferred = admitted && ((delegate* unmanaged<uint>)pool)() == 1;
                    restoredContext = transferred && control->Phase == 3 && control->ACount == 8 && control->BCount == 8 && control->Transfers == 17 &&
                        Cooperative.AdmitResume(control->Plan.Owner, control->Plan.Original) == Cooperative.Status.Ok &&
                        CooperativeEqualContexts(control->Plan.Original.Context, control->Plan.Witness) &&
                        Cooperative.VerifyProgram((byte*)pool, Cooperative.ProgramBytes, control->Plan) && CooperativeAuditPool(state, true);
                    if (restoredContext)
                        restoredContext = CooperativeRow(state, 2, 0xffff, control->Plan.Original.Context->Rflags,
                        control->Plan.Witness->Rsp, control->Plan.Witness->Rip);
                }
            }
            // Restore live permissions/TLB before touching released guards or
            // making code writable. Never free resources after failed restoration.
            bool mappings = captured
                ? CooperativeSetPoolMappings(state, originalEntries, false, out _) && CooperativeAuditPool(state, false)
                : CooperativeAuditPool(state, false);
            bool zero = mappings;
            if (mappings)
            {
                for (ulong offset = 0; offset < borrowed * Page; offset++)
                    ((byte*)pool)[offset] = 0;
                for (ulong offset = 0; offset < borrowed * Page; offset++)
                    zero &= ((byte*)pool)[offset] == 0;
                for (uint count = borrowed; count > 0; count--)
                {
                    var freed = BootPageAllocatorMutator.FreeOnePage(allocator, &node, 1, 1, null, 0, 0, receipts[count - 1]);
                    if (!freed.Succeeded)
                    {
                        zero = false;
                        break;
                    }
                    allocator = freed.AfterState;
                }
            }
            bool released = mappings && zero && CooperativeAllocatorEqual(initial, allocator, initialNode, node) &&
                CooperativeAuditOriginalMappings(state) && FirstExitOwnedArena.ValidateCanaries(state->Arena);
            bool success = prepared && published && transferred && restoredContext && released;
            if (released)
                released = CooperativeRow(state, 3, allocator.FreePageCount, allocator.AllocatedPageCount, 1, borrowed * Page);
            state->CooperativePhase = success && released ? 4U : 5U;
            if (!success || !released)
            {
                _ = CooperativeRow(state, 4, prepared ? 1UL : 0, published ? 1UL : 0, transferred ? 1UL : 0, released ? 1UL : 0);
                _ = CooperativeMarker(state, 3);
                return false;
            }
            return CooperativeMarker(state, 1) && CooperativeMarker(state, 2);
        }

        private static bool CooperativeAuthority(Storage* state) => state != null && state->Phase == 4 && state->OwnedPhase == 2 &&
            state->Arena.IsReady && state->Diagnostic.Status == FirstExitDiagnosticStatus.Completed && state->CooperativePool != 0 &&
            state->Memory.References.CooperativePoolStart == state->CooperativePool && state->Memory.References.CooperativePoolPages == CooperativePoolPages &&
            state->Arena.Transition->Mode == FirmwareServicesMode.BootServicesExited && state->Arena.Transition->RetainedFirmwareCallbacksCleared &&
            state->Arena.Binding->RetainedPointersCleared && FirstExitOwnedArena.ValidateCanaries(state->Arena);

        // Use the original installed-range audit before publication and after
        // restoration, when the dedicated pool has its original RW/NX leaves.
        private static bool CooperativeAuditOriginalMappings(Storage* state)
        {
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            return FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, state->Memory.Code, state->Memory.Capture,
                    state->Memory.Mapping, state->Memory.References, state->Memory.Allocator, state->Memory.Tables,
                    ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) &&
                SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(state->Memory.Tables, ranges, count,
                    FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, state->Memory.PageCapacity,
                    state->Memory.Entries, state->Memory.EntryCapacity).Succeeded;
        }

        private static bool CooperativeSingleCpu(Storage* state, ulong* cpu)
        {
            if (((uint)cpu[7] & 0x10000000) != 0 && (((uint)cpu[8] >> 16) & 255) != 1)
                return false;
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (!ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity))
                    return false;
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                return selected.Succeeded && OS.Drivers.MADTLocalAPICIOAPICSeparatelyPollingProof.Parse(copied->Bytes + 16 + selected.Madt.Offset,
                    selected.Madt.Length, (uint)cpu[8] >> 24, out _) == OS.Drivers.MadtPollingStatus.Success;
            }
        }

        private static bool CooperativeObserve(CooperativeControl* control, bool initial)
        {
            Storage* state = control->State;
            if (!CooperativeAuthority(state))
                return false;
            ulong* cpu = control->Cpu;
            ((delegate* unmanaged<ulong*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[9] < 0x80000001 || !CooperativeSingleCpu(state, cpu))
                return false;
            Cooperative.Owner* owner = &control->Plan.Owner;
            owner->CurrentRoot = cpu[1];
            owner->CurrentKernelGs = cpu[14];
            owner->Cr0 = cpu[0];
            owner->Cr4 = cpu[2];
            owner->Flags = cpu[3];
            owner->Efer = cpu[11];
            owner->Cs = (ushort)cpu[4];
            owner->Ss = (ushort)cpu[5];
            owner->Tr = (ushort)cpu[6];
            owner->BasicEdx = (uint)cpu[7];
            owner->ExtendedEdx = (uint)cpu[10];
            owner->Fs = (ushort)cpu[15];
            owner->Gs = (ushort)cpu[16];
            owner->CpuCount = 1;
            if (initial)
                owner->KernelGs = cpu[14];
            return Cooperative.AdmitPlan(*owner) == Cooperative.Status.Ok;
        }

        private static bool CooperativePrepareProgram(Storage* state, out CooperativeControl* control)
        {
            ulong pool = state->CooperativePool, arena = pool + 2 * Page;
            control = (CooperativeControl*)(arena + Page);
            if (sizeof(CooperativeControl) > 0xe00)
                return false;
            *control = default;
            control->State = state;
            Cooperative.Owner owner = new()
            {
                Initialized = true,
                Key = pool ^ 0x4d30393030344f57UL,
                CurrentKey = pool ^ 0x4d30393030344f57UL,
                Root = (ulong)state->Memory.Entries,
                ContextArena = arena,
                ContextArenaBytes = 2 * Page,
                CodeStart = pool,
                CodeBytes = Cooperative.ProgramBytes,
                SwitchStackGuardStart = pool + 4 * Page,
                SwitchStackBytes = 10 * Page
            };
            control->Plan.Owner = owner;
            // Establish owned selectors before observing the restoration baseline.
            ((delegate* unmanaged<void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeSegmentsOffset))();
            if (!CooperativeObserve(control, true))
                return false;
            byte* rawFx = (byte*)arena + 0xe00;
            ((delegate* unmanaged<byte*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeFxOffset))(rawFx);
            control->Plan.Owner.ObservedMxcsrMask = *(uint*)(rawFx + 28);
            if (Cooperative.AdmitPlan(control->Plan.Owner) != Cooperative.Status.Ok)
                return false;
            uint mask = control->Plan.Owner.ObservedMxcsrMask;
            SavedContext* original = (SavedContext*)arena, first = (SavedContext*)(arena + 0x300), second = (SavedContext*)(arena + 0x600);
            if (SavedContext.Initialize(original, SavedContext.Bytes, pool + Cooperative.OriginalResumeOffset,
                    (state->Arena.StackTop - 64) & ~15UL, mask) != SavedContext.Status.Ok ||
                SavedContext.Initialize(first, SavedContext.Bytes, pool + Cooperative.WorkerAEntryOffset, pool + 16 * Page - 40, mask,
                    2, arena + 0x1f00, arena + 0x1f08) != SavedContext.Status.Ok ||
                SavedContext.Initialize(second, SavedContext.Bytes, pool + Cooperative.WorkerBEntryOffset, pool + 19 * Page - 40, mask,
                    0x402, arena + 0x1f10, arena + 0x1f18) != SavedContext.Status.Ok)
                return false;
            CooperativeSeed(first, 1);
            CooperativeSeed(second, 2);
            byte* helperFx = (byte*)arena + 0xc00;
            for (uint i = 0; i < 512; i++)
                helperFx[i] = 0;
            *(ushort*)helperFx = 0x027f;
            *(uint*)(helperFx + 24) = 0x1f80;
            *(uint*)(helperFx + 28) = mask;
            control->Plan.Original = new()
            {
                Identity = 0x100000001,
                CurrentIdentity = 0x100000001,
                Context = original,
                ContextBytes = SavedContext.Bytes,
                StackGuardStart = (ulong)state->Arena.Storage + 21 * Page,
                StackBytes = 66 * Page,
                ResumeRip = pool + Cooperative.OriginalResumeOffset,
                Kind = Cooperative.ResumeKind.CapturedCall
            };
            control->Plan.A = new()
            {
                Identity = 0x100000002,
                CurrentIdentity = 0x100000002,
                Context = first,
                ContextBytes = SavedContext.Bytes,
                StackGuardStart = pool + 14 * Page,
                StackBytes = 3 * Page,
                ResumeRip = pool + Cooperative.WorkerAEntryOffset,
                Kind = Cooperative.ResumeKind.Entry
            };
            control->Plan.B = new()
            {
                Identity = 0x100000003,
                CurrentIdentity = 0x100000003,
                Context = second,
                ContextBytes = SavedContext.Bytes,
                StackGuardStart = pool + 17 * Page,
                StackBytes = 3 * Page,
                ResumeRip = pool + Cooperative.WorkerBEntryOffset,
                Kind = Cooperative.ResumeKind.Entry
            };
            control->Plan.Witness = (SavedContext*)(arena + 0x900);
            control->Plan.BCount = (ulong)&control->BCount;
            control->Plan.FlagScratch = (ulong)&control->BranchFlags;
            control->Plan.Cycles = 8;
            control->Plan.RecoveryCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, byte>)&CooperativeRecover;
            control->Plan.TerminalCallback = (ulong)(delegate* unmanaged<void*, void>)&CooperativeTerminal;
            control->Plan.Stub = new()
            {
                AdmissionCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, SavedContext*, byte>)&CooperativeAdmit,
                CallbackContext = (ulong)control,
                CallbackBytes = (uint)sizeof(CooperativeControl),
                HelperFx = (ulong)helperFx,
                JumpSlot = arena + 0x1e00,
                RejectRip = pool + Cooperative.RecoveryOffset
            };
            var imageRange = state->Memory.Capture.Range;
            if (!imageRange.IsValid || imageRange.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
                control->Plan.Stub.AdmissionCallback < image || control->Plan.Stub.AdmissionCallback - image >= imageRange.ImageSize ||
                control->Plan.RecoveryCallback < image || control->Plan.RecoveryCallback - image >= imageRange.ImageSize ||
                control->Plan.TerminalCallback < image || control->Plan.TerminalCallback - image >= imageRange.ImageSize)
                return false;
            control->Phase = 1;
            return Cooperative.EmitProgram((byte*)pool, Cooperative.ProgramBytes, control->Plan) &&
                Cooperative.VerifyProgram((byte*)pool, Cooperative.ProgramBytes, control->Plan);
        }

        private static ulong CooperativeGpr(uint worker, uint register) => 0x434c520000000000UL | ((ulong)worker << 32) | (register * 0x10101UL + 0x1234);
        private static void CooperativeSeed(SavedContext* c, uint worker)
        {
            c->Rax = CooperativeGpr(worker, 0);
            c->Rcx = CooperativeGpr(worker, 1);
            c->Rdx = CooperativeGpr(worker, 2);
            c->Rbx = CooperativeGpr(worker, 3);
            c->Rbp = CooperativeGpr(worker, 5);
            c->Rsi = CooperativeGpr(worker, 6);
            c->Rdi = CooperativeGpr(worker, 7);
            c->R8 = CooperativeGpr(worker, 8);
            c->R9 = CooperativeGpr(worker, 9);
            c->R10 = CooperativeGpr(worker, 10);
            c->R11 = CooperativeGpr(worker, 11);
            c->R12 = CooperativeGpr(worker, 12);
            c->R13 = CooperativeGpr(worker, 13);
            c->R14 = CooperativeGpr(worker, 14);
            c->R15 = CooperativeGpr(worker, 15);
            byte* fx = c->FxSave;
            *(ushort*)fx = worker == 1 ? (ushort)0x027f : (ushort)0x067f;
            *(uint*)(fx + 24) = worker == 1 ? 0x1f80U : 0x3f80U;
            fx[4] = 0xff; // Eight valid normal x87 registers, TOP0.
            for (uint register = 0; register < 8; register++)
            {
                *(ulong*)(fx + 32 + register * 16) = CooperativeX87(worker, register);
                *(ushort*)(fx + 40 + register * 16) = (ushort)(0x3fff + register);
            }
            for (uint register = 0; register < 16; register++)
                for (uint part = 0; part < 16; part++)
                    fx[160 + register * 16 + part] = (byte)(0x10 + worker * 0x20 + register * 7 + part * 3);
        }
        private static bool CooperativeWorker(SavedContext* c, uint worker, ulong stackGuard, uint mask, ulong arena)
        {
            if (c->Rsp != stackGuard + 2 * Page - 80 || c->Rax != CooperativeGpr(worker, 0) || c->Rcx != CooperativeGpr(worker, 1) ||
                c->Rdx != CooperativeGpr(worker, 2) || c->Rbx != CooperativeGpr(worker, 3) || c->Rbp != CooperativeGpr(worker, 5) ||
                c->Rsi != CooperativeGpr(worker, 6) || c->Rdi != CooperativeGpr(worker, 7) || c->R8 != CooperativeGpr(worker, 8) ||
                c->R9 != CooperativeGpr(worker, 9) || c->R10 != CooperativeGpr(worker, 10) || c->R11 != CooperativeGpr(worker, 11) ||
                c->R12 != CooperativeGpr(worker, 12) || c->R13 != CooperativeGpr(worker, 13) || c->R14 != CooperativeGpr(worker, 14) ||
                c->R15 != CooperativeGpr(worker, 15) || c->Rflags != (worker == 1 ? 2UL : 0x402UL) ||
                c->FsBase != arena + (worker == 1 ? 0x1f00UL : 0x1f10UL) ||
                c->UserGsBase != arena + (worker == 1 ? 0x1f08UL : 0x1f18UL))
                return false;
            byte* fx = c->FxSave;
            if (*(ushort*)fx != (worker == 1 ? 0x027f : 0x067f) || *(ushort*)(fx + 2) != 0 || fx[4] != 0xff ||
                *(uint*)(fx + 24) != (worker == 1 ? 0x1f80 : 0x3f80) || *(uint*)(fx + 28) != mask)
                return false;
            for (uint register = 0; register < 8; register++)
                if (*(ulong*)(fx + 32 + register * 16) != CooperativeX87(worker, register) ||
                    *(ushort*)(fx + 40 + register * 16) != (ushort)(0x3fff + register))
                    return false;
            for (uint register = 0; register < 16; register++)
                for (uint part = 0; part < 16; part++)
                    if (fx[160 + register * 16 + part] != (byte)(0x10 + worker * 0x20 + register * 7 + part * 3))
                        return false;
            return true;
        }

        private static ulong CooperativeX87(uint worker, uint register) => 0x8000000000000000UL |
            ((ulong)worker << 48) | ((ulong)register << 32) | 0x12345678UL;

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte CooperativeAdmit(void* context, SavedContext* source, SavedContext* destination)
        {
            CooperativeControl* c = (CooperativeControl*)context;
            if (c == null || c->State == null || c->State->CooperativePhase != 2 || !CooperativeObserve(c, false) ||
                !CooperativeAuditPool(c->State, true))
                return 0;
            Cooperative.Descriptor from, to;
            uint fromId, toId;
            ulong pool = c->Plan.Owner.CodeStart;
            if (source == c->Plan.Original.Context && destination == c->Plan.A.Context && c->Phase == 1 && c->Transfers == 0)
            {
                from = c->Plan.Original;
                to = c->Plan.A;
                fromId = 0;
                toId = 1;
            }
            else if (source == c->Plan.A.Context && destination == c->Plan.B.Context && c->Phase == 2 && c->ACount == c->BCount && c->ACount < 8)
            {
                c->Plan.A.Kind = Cooperative.ResumeKind.CapturedCall;
                c->Plan.A.ResumeRip = pool + Cooperative.WorkerAResumeOffset;
                from = c->Plan.A;
                to = c->Plan.B;
                fromId = 1;
                toId = 2;
                if (!CooperativeWorker(source, 1, from.StackGuardStart, c->Plan.Owner.ObservedMxcsrMask, c->Plan.Owner.ContextArena))
                    return 0;
            }
            else if (source == c->Plan.B.Context && c->Phase == 2 && c->ACount == c->BCount + 1 && c->BCount < 8 &&
                destination == (c->BCount == 7 ? c->Plan.Original.Context : c->Plan.A.Context))
            {
                c->Plan.B.Kind = Cooperative.ResumeKind.CapturedCall;
                c->Plan.B.ResumeRip = pool + (c->BCount == 7 ? Cooperative.WorkerBFinalResumeOffset : Cooperative.WorkerBResumeOffset);
                from = c->Plan.B;
                to = c->BCount == 7 ? c->Plan.Original : c->Plan.A;
                fromId = 2;
                toId = c->BCount == 7 ? 0U : 1U;
                if (!CooperativeWorker(source, 2, from.StackGuardStart, c->Plan.Owner.ObservedMxcsrMask, c->Plan.Owner.ContextArena))
                    return 0;
            }
            else
                return 0;
            if (Cooperative.Admit(c->Plan.Owner, from, to) != Cooperative.Status.Ok ||
                !CooperativeRow(c->State, 0, c->Transfers, ((ulong)fromId << 32) | toId, source->Rip, source->Rsp))
                return 0;
            c->Transfers++;
            if (fromId == 0)
                c->Phase = 2;
            else if (fromId == 1)
                c->ACount++;
            else
            {
                c->BCount++;
                if (toId == 0)
                    c->Phase = 3;
            }
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte CooperativeRecover(void* context, SavedContext* original)
        {
            CooperativeControl* c = (CooperativeControl*)context;
            if (c == null || original != c->Plan.Original.Context || !CooperativeObserve(c, false) || !CooperativeAuditPool(c->State, true))
                return 0;
            c->Phase = 0x80;
            return Cooperative.AdmitResume(c->Plan.Owner, c->Plan.Original) == Cooperative.Status.Ok ? (byte)1 : (byte)0;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void CooperativeTerminal(void* context)
        {
            CooperativeControl* c = (CooperativeControl*)context;
            if (c != null && c->State != null)
                _ = CooperativeRow(c->State, 4, 0, 0, 0, 0);
            if (c != null && c->State != null)
                _ = CooperativeMarker(c->State, 3);
            while (true)
            {
            }
        }
        private static bool CooperativeEqualContexts(SavedContext* a, SavedContext* b)
        {
            for (uint i = 0; i < SavedContext.Bytes; i++)
                if (((byte*)a)[i] != ((byte*)b)[i])
                    return false;
            return true;
        }
        private static bool CooperativeAllocatorEqual(BootPageAllocatorState a, BootPageAllocatorState b, BootPageFreeListNode x, BootPageFreeListNode y) =>
            a.IsInitialized == b.IsInitialized && a.PageSize == b.PageSize && a.HeadNodeIndex == b.HeadNodeIndex && a.FreeNodeCount == b.FreeNodeCount &&
            a.FreePageCount == b.FreePageCount && a.FreeByteCount == b.FreeByteCount && a.AllocatedPageCount == b.AllocatedPageCount && a.AllocatedByteCount == b.AllocatedByteCount &&
            x.SourceFreeSegmentIndex == y.SourceFreeSegmentIndex && x.SourceUsableRangeIndex == y.SourceUsableRangeIndex && x.PhysicalStart == y.PhysicalStart &&
            x.PhysicalEndExclusive == y.PhysicalEndExclusive && x.PageCount == y.PageCount && x.ByteLength == y.ByteLength && x.NextNodeIndex == y.NextNodeIndex;

        private static bool CooperativeMarker(Storage* state, uint kind)
        {
            ulong* words = stackalloc ulong[4];
            uint bytes;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554;
                words[1] = 0x302e39306d205452;
                words[2] = 0x0a0d3430;
                bytes = 20;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43;
                words[1] = 0x45525f3430305f39;
                words[2] = 0x0a0d594441;
                bytes = 21;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554;
                words[1] = 0x30302e39306d2053;
                words[2] = 0x0a0d34;
                bytes = 19;
            }
            else if (kind == 3)
            {
                words[0] = 0x4941465f54534554;
                words[1] = 0x30302e39306d204c;
                words[2] = 0x7463656a65722034;
                words[3] = 0x0a0d6465;
                bytes = 28;
            }
            else
                return false;
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
        private static bool CooperativeRow(Storage* state, uint kind, ulong a, ulong b, ulong c, ulong d)
        {
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x5f3430305f39304d;
            uint length;
            if (kind == 0)
            {
                *(ulong*)(text + 8) = 0x524546534e415254;
                length = 16;
            }
            else if (kind == 1)
            {
                *(ulong*)(text + 8) = 0x434f4c4c41;
                length = 13;
            }
            else if (kind == 2)
            {
                *(ulong*)(text + 8) = 0x45524f54534552;
                length = 15;
            }
            else if (kind == 3)
            {
                *(ulong*)(text + 8) = 0x50554e41454c43;
                length = 15;
            }
            else if (kind == 4)
            {
                *(ulong*)(text + 8) = 0x5443454a4552;
                length = 14;
            }
            else
                return false;
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (uint i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint nibble = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

#if M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE

        private static bool RunSchedulerTimer(void* context)
        {
            Storage* state = (Storage*)context;
            if (!CooperativeAuthority(state) || state->ApicPhase != 2 || state->CooperativePhase != 0 || !TimerText(state, 0))
                return false;
            if (!FirstExitCodeBundle.ValidateInstalledReadBack(state->Memory.Code, FirstExitCodeBundle.Bytes, ref state->Arena,
                state->Memory.Capture, state->Memory.Mapping, state->Memory.References.FaultCallback))
                return false;
            // The existing pool audit admits owned NULL FS/GS selectors. Establish
            // that baseline before asking it to walk any timer backing page.
            ((delegate* unmanaged<void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeSegmentsOffset))();
            ulong pool = state->CooperativePool;
            if (!CooperativeAuditPool(state, false) || !CooperativeAuditOriginalMappings(state))
            {
                _ = TimerText(state, 8, 10, 0, 0, 0);
                return false;
            }
            BootPageFreeListNode node = new(0, 0, pool, pool + 32 * Page, 32, 32 * Page, -1);
            BootPageAllocatorState allocator = default;
            if (!BootPageAllocatorStateMaterializer.Materialize(&node, 1, 1, 1, 0, Page, 32, 32 * Page, &allocator, 1).Succeeded)
                return false;
            BootPageAllocatorState initial = allocator;
            BootPageFreeListNode initialNode = node;
            BootPageAllocatorOnePageResult* receipts = stackalloc BootPageAllocatorOnePageResult[22];
            ulong* originalEntries = stackalloc ulong[32];
            for (int i = 0; i < 32; i++)
                originalEntries[i] = 0;
            uint borrowed = 0;
            bool addresses = true;
            for (; borrowed < 22; borrowed++)
            {
                var receipt = BootPageAllocatorMutator.AllocateOnePage(allocator, &node, 1, 1, null, 0, 0, 1, Page);
                if (!receipt.Succeeded)
                    break;
                allocator = receipt.AfterState;
                receipts[borrowed] = receipt;
                addresses &= receipt.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong address) == AddressValueStatus.Success &&
                    address == pool + borrowed * Page && receipt.PageCount == 1 && receipt.ByteLength == Page;
            }
            TimerControl* clock = null;
            bool prepared = borrowed == 22 && addresses && TimerPrepare(state, out clock);
            bool captured = false, published = false, entered = false, contextRestored = false, hardwareRestored = false;
            ulong ticks = 0;
            if (prepared)
            {
                published = CooperativeSetPoolMappings(state, originalEntries, true, out captured) && CooperativeAuditPool(state, true);
                if (published)
                {
                    clock->Cooperative->Plan.Owner.GuardsVerified = true;
                    clock->Cooperative->Plan.Owner.MappingsVerified = true;
                    bool installed = TimerInstall(clock);
                    if (installed)
                        entered = ((delegate* unmanaged<uint>)pool)() == 1;
                    contextRestored = entered && clock->Mode == 2 && clock->Failed == 0 && clock->PhaseMask == 15 &&
                        clock->Progress != 0 && clock->Queue.IsEmpty &&
                        CooperativeEqualContexts(clock->Cooperative->Plan.Original.Context, clock->Cooperative->Plan.Witness) &&
                        TimerVerify(clock) && CooperativeObserve(clock->Cooperative, false);
                    ticks = clock->Timer.Ticks;
                    hardwareRestored = TimerRestore(clock) && CooperativeAuditPool(state, true);
                }
                else
                    hardwareRestored = TimerRestore(clock);
            }
            else
                hardwareRestored = clock == null || TimerRestore(clock);
            // Never unmap/free a still-borrowed IDT/IST or a timer with pending IRQs.
            if (!hardwareRestored)
            {
                _ = TimerText(state, 8, ticks, 1, 0, 0);
                return false;
            }
            bool mappings = captured ? CooperativeSetPoolMappings(state, originalEntries, false, out _) && CooperativeAuditPool(state, false)
                : CooperativeAuditPool(state, false);
            bool released = mappings;
            if (mappings)
            {
                for (ulong i = 0; i < borrowed * Page; i++)
                    ((byte*)pool)[i] = 0;
                for (ulong i = 0; i < borrowed * Page; i++)
                    released &= ((byte*)pool)[i] == 0;
                for (uint i = borrowed; i > 0; i--)
                {
                    var free = BootPageAllocatorMutator.FreeOnePage(allocator, &node, 1, 1, null, 0, 0, receipts[i - 1]);
                    if (!free.Succeeded)
                    {
                        released = false;
                        break;
                    }
                    allocator = free.AfterState;
                }
            }
            released &= CooperativeAllocatorEqual(initial, allocator, initialNode, node) && CooperativeAuditOriginalMappings(state) &&
                FirstExitOwnedArena.ValidateCanaries(state->Arena);
            bool success = prepared && published && entered && contextRestored && hardwareRestored && released;
            state->CooperativePhase = success ? 4U : 5U;
            if (!success)
            {
                _ = TimerText(state, 8, ticks, prepared ? 1UL : 0, entered ? 1UL : 0, released ? 1UL : 0);
                return false;
            }
            return TimerText(state, 5, ticks, borrowed, allocator.FreePageCount, 1) && TimerText(state, 6) && TimerText(state, 7);
        }
        private static bool TimerPrepare(Storage* state, out TimerControl* clock)
        {
            clock = null;
            if (!CooperativePrepareProgram(state, out CooperativeControl* c) || sizeof(CooperativeControl) > 0x800 || sizeof(TimerControl) > 0x600)
                return false;
            ulong pool = state->CooperativePool;
            clock = (TimerControl*)(pool + 3 * Page + 0x800);
            *clock = default;
            clock->Cooperative = c;
            clock->Mode = 1;
            clock->CriticalPhase = 1;
            clock->Key = pool ^ 0x4d30393030354f57UL;
            clock->OriginalIdentity = c->Plan.Original.Identity;
            clock->Worker = new()
            {
                Phase = (uint)((ulong)&clock->CriticalPhase - (ulong)clock),
                Command = (uint)((ulong)&clock->Command - (ulong)clock),
                Progress = (uint)((ulong)&clock->Progress - (ulong)clock),
                First = (uint)((ulong)&clock->First - (ulong)clock),
                Second = (uint)((ulong)&clock->Second - (ulong)clock),
                Root = (uint)((ulong)&clock->PublishedRoot - (ulong)clock),
                ControlBytes = (uint)sizeof(TimerControl)
            };
            c->Plan.A.Context->R12 = (ulong)clock;
            c->Plan.A.Context->R13 = (ulong)&clock->First;
            // Keep the original callback extent containing BCount/FlagScratch;
            // the initial capture callbacks find the adjacent timer owner below.
            c->Plan.Stub.AdmissionCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, SavedContext*, byte>)&TimerEnter;
            c->Plan.RecoveryCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, byte>)&TimerRecover;
            c->Plan.TerminalCallback = (ulong)(delegate* unmanaged<void*, void>)&TimerTerminal;
            if (!Cooperative.EmitProgram((byte*)pool, Cooperative.ProgramBytes, c->Plan) || !Cooperative.VerifyProgram((byte*)pool, Cooperative.ProgramBytes, c->Plan))
                return false;
            for (uint i = 0; i < Cooperative.ProgramBytes; i++)
                ((byte*)(pool + 20 * Page))[i] = ((byte*)pool)[i];
            clock->Handler = new()
            {
                CodeAddress = pool + 0xc00,
                Callback = (ulong)(delegate* unmanaged<void*, TimerIrq.Frame*, byte>)&TimerInterrupt,
                CallbackContext = (ulong)clock,
                CallbackBytes = (uint)sizeof(TimerControl),
                HelperFx = c->Plan.Stub.HelperFx
            };
            if (!TimerIrq.EmitWorker((byte*)pool + 0x200, 512, clock->Worker) || !TimerIrq.Emit((byte*)pool + 0xc00, 512, clock->Handler) || !TimerVerify(clock))
                return false;
            if (clock->Queue.Initialize(clock->Key, (Deadlines.Entry*)clock->Entries, 32, 2) != Deadlines.Status.Ok)
                return false;
            OS.Pal.OSHostHandle cancel = new(0x100000002), original = new(clock->OriginalIdentity);
            if (clock->Queue.Schedule(clock->Key, cancel, cancel, 0, 1) != Deadlines.Status.Ok ||
                clock->Queue.Schedule(clock->Key, original, original, 0, 1) != Deadlines.Status.Ok)
                return false;
            if (clock->Timer.Prepare(clock->Key, state->ApicTopology.LocalApicAddress, 2000000, 8, ApicAccess(state), 2) != TickTimer.Status.Ok)
                return false;
            return TimerText(state, 1, pool, 22, pool + 13 * Page, clock->Handler.CodeAddress);
        }
        private static bool TimerVerify(TimerControl* c)
        {
            if (c == null || c->Cooperative == null || !CooperativeAuthority(c->Cooperative->State))
                return false;
            ulong pool = c->Cooperative->State->CooperativePool;
            if ((ulong)c != pool + 3 * Page + 0x800 || c->Key != (pool ^ 0x4d30393030354f57UL))
                return false;
            if (!TimerIrq.Verify((byte*)pool + 0xc00, 512, c->Handler) || !TimerIrq.VerifyWorker((byte*)pool + 0x200, 512, c->Worker))
                return false;
            for (uint i = 0; i < Cooperative.ProgramBytes; i++)
                if (!(i >= 0x200 && i < 0x400) && !(i >= 0xc00 && i < 0xe00) && ((byte*)pool)[i] != ((byte*)(pool + 20 * Page))[i])
                    return false;
            return c->Installed == 0 || TimerInstalledReadBack(c);
        }
        private static bool TimerInstall(TimerControl* c)
        {
            Storage* state = c->Cooperative->State;
            ulong pool = state->CooperativePool;
            byte* idt = (byte*)(pool + 18 * Page), code = (byte*)c->Handler.CodeAddress;
            ((delegate* unmanaged<void*, void>)(code + TimerIrq.SaveIdtrOffset))(c->OriginalIdtr);
            if (*(ushort*)c->OriginalIdtr != 4095 || *(ulong*)((byte*)c->OriginalIdtr + 2) != (ulong)state->Arena.Descriptors->FinalIdt)
                return false;
            // IST1..3 belong to retained fault paths. Borrow only the unused IST4;
            // publish its stack after the last original-descriptor admission.
            c->OriginalIst = *(ulong*)(state->Arena.Descriptors->Tss + 60);
            if (c->OriginalIst != 0 || !TimerVerify(c) || !CooperativeAuditPool(state, true))
                return false;
            for (uint i = 0; i < 104; i++)
                c->OriginalTss[i] = state->Arena.Descriptors->Tss[i];
            for (uint i = 0; i < 4096; i++)
                idt[i] = state->Arena.Descriptors->FinalIdt[i];
            TimerGate(idt + 0xe1 * 16, c->Handler.CodeAddress, 4);
            TimerGate(idt + 0xff * 16, c->Handler.CodeAddress + TimerIrq.SpuriousOffset, 4);
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 0xe1 * 16 && i < 0xe2 * 16) && i < 0xff * 16 && idt[i] != state->Arena.Descriptors->FinalIdt[i])
                    return false;
            if (!TimerGateMatches(idt + 0xe1 * 16, c->Handler.CodeAddress) ||
                !TimerGateMatches(idt + 0xff * 16, c->Handler.CodeAddress + TimerIrq.SpuriousOffset))
                return false;
            *(ushort*)c->NewIdtr = 4095;
            *(ulong*)((byte*)c->NewIdtr + 2) = (ulong)idt;
            c->Installed = 1; // Retain the original descriptor before the first mutation.
            ((delegate* unmanaged<void*, void>)(code + TimerIrq.LoadIdtrOffset))(c->NewIdtr);
            ((delegate* unmanaged<void*, void>)(code + TimerIrq.SaveIdtrOffset))(c->RestoredIdtr);
            for (uint i = 0; i < 16; i++)
                if (((byte*)c->NewIdtr)[i] != ((byte*)c->RestoredIdtr)[i])
                    return false;
            return true;
        }
        // Validate the current descriptor authority without applying the original
        // zero-IST4 predicate to a live borrow. Every other TSS byte and IDT gate
        // must still equal the independently admitted original image.
        private static bool TimerInstalledReadBack(TimerControl* c)
        {
            Storage* state = c->Cooperative->State;
            ulong pool = state->CooperativePool;
            if (c->Installed != 1 || c->OriginalIst != 0)
                return false;
            ulong top = *(ulong*)(state->Arena.Descriptors->Tss + 60);
            if (top != 0 && top != pool + 13 * Page)
                return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 60 && i < 68) && state->Arena.Descriptors->Tss[i] != (i < 104 ? c->OriginalTss[i] : 0))
                    return false;
            byte* idt = (byte*)(pool + 18 * Page);
            if (!TimerGateMatches(idt + 0xe1 * 16, c->Handler.CodeAddress) ||
                !TimerGateMatches(idt + 0xff * 16, c->Handler.CodeAddress + TimerIrq.SpuriousOffset))
                return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 0xe1 * 16 && i < 0xe2 * 16) && i < 0xff * 16 && idt[i] != state->Arena.Descriptors->FinalIdt[i])
                    return false;
            ulong* observed = stackalloc ulong[2];
            observed[0] = 0;
            observed[1] = 0;
            ((delegate* unmanaged<void*, void>)(c->Handler.CodeAddress + TimerIrq.SaveIdtrOffset))(observed);
            for (uint i = 0; i < 16; i++)
                if (((byte*)observed)[i] != ((byte*)c->NewIdtr)[i])
                    return false;
            return true;
        }
        private static void TimerGate(byte* gate, ulong address, byte ist)
        {
            *(ushort*)gate = (ushort)address;
            *(ushort*)(gate + 2) = 8;
            gate[4] = ist;
            gate[5] = 0x8e;
            *(ushort*)(gate + 6) = (ushort)(address >> 16);
            *(uint*)(gate + 8) = (uint)(address >> 32);
            *(uint*)(gate + 12) = 0;
        }
        private static bool TimerGateMatches(byte* g, ulong address) => *(ushort*)g == (ushort)address && *(ushort*)(g + 2) == 8 &&
            g[4] == 4 && g[5] == 0x8e && *(ushort*)(g + 6) == (ushort)(address >> 16) &&
            *(uint*)(g + 8) == (uint)(address >> 32) && *(uint*)(g + 12) == 0;
        private static bool TimerRestore(TimerControl* c)
        {
            Storage* state = c->Cooperative->State;
            byte* code = (byte*)c->Handler.CodeAddress;
            if (c->Timer.IsPrepared && c->Timer.Restore(c->Key, 2) != TickTimer.Status.Ok)
                return false;
            // OriginalIdtr remains the independently captured hardware descriptor.
            if (c->Installed == 1)
            {
                ((delegate* unmanaged<void*, void>)(code + TimerIrq.LoadIdtrOffset))(c->OriginalIdtr);
                ((delegate* unmanaged<void*, void>)(code + TimerIrq.SaveIdtrOffset))(c->RestoredIdtr);
                for (uint i = 0; i < 16; i++)
                    if (((byte*)c->OriginalIdtr)[i] != ((byte*)c->RestoredIdtr)[i])
                        return false;
                *(ulong*)(state->Arena.Descriptors->Tss + 60) = c->OriginalIst;
                if (*(ulong*)(state->Arena.Descriptors->Tss + 60) != c->OriginalIst)
                    return false;
                c->Installed = 0;
            }
            for (uint i = 0; i < 2 && c->Queue.HasPending; i++)
            {
                var next = c->Queue.Peek(c->Key);
                if (!next.Succeeded || c->Queue.Cancel(c->Key, next.Value.Identity) != Deadlines.Status.Ok)
                    return false;
            }
            return !c->Queue.IsInitialized || c->Queue.Release(c->Key) == Deadlines.Status.Ok;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte TimerEnter(void* context, SavedContext* source, SavedContext* destination)
        {
            TimerControl* c = TimerFromCooperative(context);
            if (c == null)
                return 0;
            CooperativeControl* old = c->Cooperative;
            if (c->Mode != 1 || source != old->Plan.Original.Context || destination != old->Plan.A.Context || !TimerVerify(c) ||
                !CooperativeObserve(old, false) || !CooperativeAuditPool(old->State, true) ||
                Cooperative.Admit(old->Plan.Owner, old->Plan.Original, old->Plan.A) != Cooperative.Status.Ok)
                return 0;
            if (c->Installed != 1 || *(ulong*)(old->State->Arena.Descriptors->Tss + 60) != c->OriginalIst)
                return 0;
            *(ulong*)(old->State->Arena.Descriptors->Tss + 60) = old->State->CooperativePool + 13 * Page;
            if (*(ulong*)(old->State->Arena.Descriptors->Tss + 60) != old->State->CooperativePool + 13 * Page ||
                !TimerInstalledReadBack(c))
                return 0;
            if (!TimerText(old->State, 2, c->Key, 2000000, 0xe1, 16))
                return 0;
            var armed = c->Timer.Arm(c->Key, 2, true);
            if (armed != TickTimer.Status.Ok)
                return TimerInterruptReject(c, 20, (ulong)armed, 0, 0);
            return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte TimerRecover(void* context, SavedContext* original)
        {
            TimerControl* c = TimerFromCooperative(context);
            if (c == null || !TimerRestore(c) || original != c->Cooperative->Plan.Original.Context || !CooperativeObserve(c->Cooperative, false) ||
                Cooperative.AdmitResume(c->Cooperative->Plan.Owner, c->Cooperative->Plan.Original) != Cooperative.Status.Ok)
                return 0;
            c->Failed = 1;
            c->Mode = 3;
            return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void TimerTerminal(void* context)
        {
            TimerControl* c = TimerFromCooperative(context);
            if (c != null)
                _ = TimerText(c->Cooperative->State, 8, 0, 0, 0, 0);
            while (true)
            {
            }
        }
        private static TimerControl* TimerFromCooperative(void* context)
        {
            CooperativeControl* owner = (CooperativeControl*)context;
            if (owner == null || owner->State == null)
                return null;
            TimerControl* clock = (TimerControl*)(owner->State->CooperativePool + 3 * Page + 0x800);
            return clock->Cooperative == owner ? clock : null;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte TimerInterrupt(void* context, TimerIrq.Frame* frame)
        {
            TimerControl* c = (TimerControl*)context;
            if (c == null || c->Cooperative == null || frame == null)
                return 0;
            if (!TimerVerify(c))
                return TimerInterruptReject(c, 10, (ulong)frame, 0, 0);
            CooperativeControl* old = c->Cooperative;
            Storage* state = old->State;
            ulong pool = state->CooperativePool;
            if ((ulong)frame != pool + 13 * Page - TimerIrq.FrameBytes || c->Mode != 1 || frame->Complete != 1 ||
                c->Installed != 1 || *(ulong*)(state->Arena.Descriptors->Tss + 60) != pool + 13 * Page ||
                frame->Cs != 8 || frame->Ss != 16 || (frame->EntryFlags & 0x202) != 2 || (frame->Rflags & 0x602) != 0x602 ||
                // RF belongs to the actual interrupt frame and survives deferred
                // IRETQ unchanged. CALL-captured contexts retain their IRQ-off mask.
                (frame->Rflags & ~(Cooperative.AdmittedFlags | 0x10200UL)) != 0 || c->CriticalPhase > 3 ||
                c->Progress == 0)
                return TimerInterruptReject(c, 11, frame->EntryFlags, frame->Rflags, (ulong)frame);
            uint mismatch = TimerWorkerMismatch(c, frame);
            if (mismatch != 0)
                return TimerInterruptReject(c, 100 + mismatch, frame->Rip, frame->Rsp, frame->FxSave[4]);
            ulong* cpu = stackalloc ulong[19];
            ((delegate* unmanaged<ulong*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[1] != old->Plan.Owner.Root || cpu[4] != 8 || cpu[5] != 0 || cpu[6] != 24 ||
                (cpu[3] & 0x602) != 2 || cpu[14] != old->Plan.Owner.KernelGs || cpu[15] != 0 || cpu[16] != 0)
                return TimerInterruptReject(c, 12, cpu[3], cpu[5], cpu[1]);
            bool initialized = c->First == TimerIrq.FirstPattern && c->Second == TimerIrq.SecondPattern;
            if ((c->PublishedRoot != 0 && (!initialized || c->PublishedRoot != (ulong)&c->First)) ||
                (c->CriticalPhase == 0 && (!initialized || c->PublishedRoot == 0)))
                return TimerInterruptReject(c, 13, c->First, c->Second, c->PublishedRoot);
            c->PhaseMask |= 1U << (int)c->CriticalPhase;
            TickTimer.Result tick = c->Timer.HandleInterrupt(c->Key, frame->EntryFlags, frame->Rflags, ref c->Queue, c->Key, (TickTimer.Phase)c->CriticalPhase);
            if (!tick.Succeeded)
                return TimerInterruptReject(c, 14, (ulong)tick.Code, c->Timer.Ticks, c->CriticalPhase);
            if (!TimerText(state, 3, tick.Tick, c->CriticalPhase, c->Timer.DeferredTicks, c->Progress))
                return 0;
            if (tick.Tick == 1 && c->Queue.Cancel(c->Key, new OS.Pal.OSHostHandle(0x100000002)) != Deadlines.Status.Ok)
                return 0;
            if (tick.Next != TickTimer.Action.WakeReady)
            {
                c->Command = c->CriticalPhase + 1;
                return 1;
            }
            SavedContext* original = old->Plan.Original.Context;
            if (c->PhaseMask != 15 || c->OriginalIdentity != old->Plan.Original.Identity || c->OriginalIdentity != old->Plan.Original.CurrentIdentity ||
                tick.Due.Identity.Value != c->OriginalIdentity || tick.Due.Deadline > tick.Tick ||
                SavedContext.Validate(original, SavedContext.Bytes, old->Plan.Owner.ObservedMxcsrMask) != SavedContext.Status.Ok ||
                original->Rip != pool + Cooperative.OriginalResumeOffset || (original->Rsp & 15) != 0 ||
                !FirstExitOwnedArena.InMainStack(state->Arena, original->Rsp, 32) ||
                (original->Rflags & ~Cooperative.AdmittedFlags) != 0 || (original->Rflags & 0x400) != 0 ||
                !TimerText(state, 4, tick.Tick, tick.Due.Identity.Value, tick.Due.Deadline, frame->Rip))
                return 0;
            if (c->Queue.Cancel(c->Key, tick.Due.Identity) != Deadlines.Status.Ok || !c->Queue.IsEmpty)
                return 0;
            frame->Complete = 0;
            frame->Rax = original->Rax;
            frame->Rcx = original->Rcx;
            frame->Rdx = original->Rdx;
            frame->Rbx = original->Rbx;
            frame->Rbp = original->Rbp;
            frame->Rsi = original->Rsi;
            frame->Rdi = original->Rdi;
            frame->R8 = original->R8;
            frame->R9 = original->R9;
            frame->R10 = original->R10;
            frame->R11 = original->R11;
            frame->R12 = original->R12;
            frame->R13 = original->R13;
            frame->R14 = original->R14;
            frame->R15 = original->R15;
            frame->Rip = original->Rip;
            frame->Rsp = original->Rsp;
            frame->Rflags = original->Rflags;
            frame->Cs = 8;
            frame->Ss = 16;
            frame->FsBase = original->FsBase;
            frame->GsBase = original->UserGsBase;
            for (uint i = 0; i < 512; i++)
                frame->FxSave[i] = original->FxSave[i];
            c->Mode = 2;
            frame->Complete = 1;
            return 1;
        }
        private static byte TimerInterruptReject(TimerControl* c, uint reason, ulong a, ulong b, ulong d)
        {
            c->Failed = reason;
            _ = TimerText(c->Cooperative->State, 8, reason, a, b, d);
            return 0;
        }
        private static uint TimerWorkerMismatch(TimerControl* c, TimerIrq.Frame* f)
        {
            SavedContext* a = c->Cooperative->Plan.A.Context;
            ulong pool = c->Cooperative->State->CooperativePool;
            if (f->Rip < pool + 0x202 || f->Rip >= pool + 0x200 + TimerIrq.WorkerEnd || f->Rsp != a->Rsp)
                return 1;
            if (
                f->Rax != a->Rax || f->Rcx != a->Rcx || f->Rdx != a->Rdx || f->Rbx != a->Rbx || f->Rbp != a->Rbp ||
                f->Rsi != a->Rsi || f->Rdi != a->Rdi || f->R8 != a->R8 || f->R9 != a->R9 || f->R10 != a->R10 || f->R11 != a->R11 ||
                f->R12 != (ulong)c || f->R13 != (ulong)&c->First || f->R14 != a->R14 || f->R15 != a->R15)
                return 2;
            if (f->FsBase != a->FsBase || f->GsBase != a->UserGsBase)
                return 3;
            byte* fx = f->FxSave, expected = a->FxSave;
            if (*(ushort*)fx != *(ushort*)expected || *(ushort*)(fx + 2) != 0 || fx[4] != 0xff ||
                *(uint*)(fx + 24) != *(uint*)(expected + 24) || *(uint*)(fx + 28) != *(uint*)(expected + 28))
                return 4;
            for (uint r = 0; r < 8; r++)
                for (uint b = 0; b < 10; b++)
                    if (fx[32 + r * 16 + b] != expected[32 + r * 16 + b])
                        return 5;
            for (uint b = 160; b < 416; b++)
                if (fx[b] != expected[b])
                    return 6;
            return 0;
        }
        private static bool TimerText(Storage* state, uint kind, ulong a = 0, ulong b = 0, ulong c = 0, ulong d = 0)
        {
            byte* text = stackalloc byte[112];
            uint length = 0;
            if (kind == 0)
            {
                *(ulong*)text = 0x4154535f54534554;
                *(ulong*)(text + 8) = 0x302e39306d205452;
                *(ushort*)(text + 16) = 0x3530;
                length = 18;
            }
            else if (kind == 6)
            {
                *(ulong*)text = 0x5341505f54534554;
                *(ulong*)(text + 8) = 0x30302e39306d2053;
                text[16] = 0x35;
                length = 17;
            }
            else if (kind == 7)
            {
                *(ulong*)text = 0x304d5f534f524c43;
                *(ulong*)(text + 8) = 0x45525f3530305f39;
                *(uint*)(text + 16) = 0x594441;
                length = 19;
            }
            else if (kind == 8)
            {
                *(ulong*)text = 0x4941465f54534554;
                *(ulong*)(text + 8) = 0x30302e39306d204c;
                text[16] = 0x35;
                length = 17;
            }
            else
            {
                *(ulong*)text = 0x5f3530305f39304d;
                if (kind == 1)
                {
                    *(ulong*)(text + 8) = 0x4445524150455250;
                    length = 16;
                }
                else if (kind == 2)
                {
                    *(ulong*)(text + 8) = 0x44454d5241;
                    length = 13;
                }
                else if (kind == 3)
                {
                    *(ulong*)(text + 8) = 0x4b434954;
                    length = 12;
                }
                else if (kind == 4)
                {
                    *(ulong*)(text + 8) = 0x454b4157;
                    length = 12;
                }
                else if (kind == 5)
                {
                    *(ulong*)(text + 8) = 0x4445524f54534552;
                    length = 16;
                }
                else
                    return false;
            }
            if (kind is >= 1 and <= 5 or 8)
            {
                ulong* values = stackalloc ulong[4];
                values[0] = a;
                values[1] = b;
                values[2] = c;
                values[3] = d;
                for (uint i = 0; i < 4; i++)
                {
                    text[length++] = 32;
                    text[length++] = 48;
                    text[length++] = 120;
                    for (int shift = 60; shift >= 0; shift -= 4)
                    {
                        uint value = (uint)((values[i] >> shift) & 15);
                        text[length++] = (byte)(value < 10 ? 48 + value : 65 + value - 10);
                    }
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void Enter(FirstExitOwnedStackContext* context)
        {
            fixed (Storage* state = &s_storage)
            {
                if (state->Phase != 3 || !state->Arena.IsReady || context != state->Arena.StackEntry ||
                    context->Phase != 2 || context->EntryContext != state ||
                    !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                    return;
                state->Phase = 4;
                // Every return reaches only the owned entry stub's CLI/HLT loop.
                _ = BootSequence.RunFirstRealExitBootServicesTransition(ref state->Arena, state->Memory.Code);
            }
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void Diagnose(FirstExitTransitionState* transition, FirstExitPermanentCr3Context* context)
        {
            fixed (Storage* state = &s_storage)
            {
                if (state->Phase != 4 || !state->Arena.IsReady || transition != state->Arena.Transition ||
                    context != state->Arena.Permanent || context->DiagnosticContext != state)
                    return;
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
                if (ReportOwnedSubsystems(state))
                {
#if M09_004_PROBE
                    _ = BootSequence.RunOwnedCooperativeSwitch(state->OwnedPhase == 2,
                        transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                        state->Arena.Binding->RetainedPointersCleared, state, &RunCooperativeSwitch);
#endif
#if M16_002_PROBE
                    _ = BootSequence.RunOwnedLinuxElfPackage(state->OwnedPhase == 2,
                        transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                        state->Arena.Binding->RetainedPointersCleared, state, &VerifyLinuxPackage);
#endif

#if M16_004_PROBE
                    if (!BootSequence.RunOwnedLinuxElfStartup(state->LinuxOwnedPhase == 2,
                        transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                        state->Arena.Binding->RetainedPointersCleared, state, &RunOwnedLinuxStartup))
                        return;
#endif
#if M16_003_PROBE
                    if (!BootSequence.RunOwnedWorkloadServiceLoop(state->LinuxOwnedPhase == 2,
                        transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                        state->Arena.Binding->RetainedPointersCleared, state, &RunOwnedWorkloadServiceLoop))
                        return;
#endif
#if M16_005_PROBE && !M16_008_PROBE
#if M16_007_PROBE
                    if (!BootSequence.RunOwnedLinuxDescriptors(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#elif M16_006_PROBE
                    if (!BootSequence.RunOwnedLinuxMemory(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#else
                    if (!BootSequence.RunOwnedLinuxSyscalls(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#endif
                        transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                        state->Arena.Binding->RetainedPointersCleared, state, &RunOwnedLinuxSyscalls))
                        return;
#endif
#if M05_MONOTONIC_CLOCK_PROBE
                    _ = BootSequence.RunOwnedMonotonicClock(state->OwnedPhase == 2, state, &RunOwnedMonotonicClock);
#endif
#if M05_PCI_ENUMERATION_PROBE
                    if (BootSequence.RunOwnedPciEnumeration(state->OwnedPhase == 2, state, &RunOwnedPciEnumeration))
                    {
#if M05_APIC_SETUP_PROBE
                        if (!BootSequence.RunOwnedApicSetup(state->PciPhase == 8, state, &RunOwnedApicSetup))
                            return;
#if M16_008_PROBE && !CLROS_LINUX_SOCKETS
#if M09_011_PROBE
                        if (!CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedNativeAotSuspension(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#elif M09_012_PROBE
                        if (!CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedNativeAotThreadPool(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#elif M16_010_PROBE
                        if (!CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedNativeAotRuntime(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#elif M16_009_PROBE
                        if (!CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedLinuxSignals(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#else
                        if (!CompleteLinuxRandomLifetime(state, BootSequence.RunOwnedLinuxThreads(state->LinuxStartupPhase == 6, state->ServiceLoop.IsStopped,
#endif
                            transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                            state->Arena.Binding->RetainedPointersCleared, state->ApicPhase == 2, state,
#if M09_011_PROBE || M09_012_PROBE || M16_010_PROBE
                            &RunOwnedNativeAotRuntimeProfile)))
                            return;
#else
                            &RunOwnedLinuxSyscalls)))
                            return;
#endif
#if M09_011_PROBE
                        if (!LinuxText(state, "CLROS_M09_011_READY\r\nTEST_PASS m09.011\r\n"))
                            return;
#elif M09_012_PROBE
                        if (!LinuxText(state, "CLROS_M09_012_READY\r\nTEST_PASS m09.012\r\n"))
                            return;
#elif M16_010_PROBE
                        if (!LinuxText(state, "CLROS_M16_010_READY\r\nTEST_PASS m16-010\r\n"))
                            return;
#endif
#endif
#if M09_005_PROBE
                        if (!BootSequence.RunOwnedSchedulerTimer(state->OwnedPhase == 2, state->ApicPhase == 2,
                            transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                            state->Arena.Binding->RetainedPointersCleared, state, &RunSchedulerTimer))
                            return;
#endif
#if M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
                        if (!BootSequence.RunOwnedKernelThreads(state->OwnedPhase == 2, state->ApicPhase == 2,
                            transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                            state->Arena.Binding->RetainedPointersCleared, state, &RunKernelThreads))
                            return;
#endif
#endif
#if M05_BAR_DMA_PROBE
                        _ = BootSequence.RunOwnedBarDma(state->PciPhase == 8, state, &RunOwnedBarDma);
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
                        _ = BootSequence.RunOwnedVirtioNegotiation(state->PciPhase == 8, state, &RunOwnedVirtioNegotiation);
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
                        _ = BootSequence.RunOwnedVirtioBlockQueueSetup(state->PciPhase == 8, state, &RunOwnedVirtioBlockQueueSetup);
#if M06_VIRTIO_BLOCK_READ_PROBE
                        _ = BootSequence.RunOwnedVirtioBlockRead(state->BlockPhase == 5, state, &RunOwnedVirtioBlockRead);
#if M06_LIVE_BLOCK_CONTRACT_PROBE
                        _ = BootSequence.RunOwnedLiveBlockContract(state->BlockReadPhase == 7, state, &RunOwnedLiveBlockContract);
#if M06_GPT_PARTITION_PROBE
                        _ = BootSequence.RunOwnedGptPartition(state->BlockContractPhase == 9, state, &RunOwnedGptPartition);
#if M06_FAT_READ_PROBE
                        _ = BootSequence.RunOwnedFatRead(state->GptPhase == 12, state, &RunOwnedFatRead);
#if M06_PACKAGE_READ_PROBE
                        _ = BootSequence.RunOwnedPackageRead(state->FatPhase == 15, state, &RunOwnedPackageRead);
#if M06_BLOCK_MSIX_PROBE
                        _ = BootSequence.RunOwnedBlockMsix(state->PackagePhase == 19 && state->ApicPhase == 2, state, &RunOwnedBlockMsix);
#endif
#endif
#endif
#endif
#endif
#endif
#endif
#if M05_VIRTQUEUE_POLLING_PROBE
                        _ = BootSequence.RunOwnedVirtqueuePolling(state->VirtioPhase == 16, state, &RunOwnedVirtqueuePolling);
#endif
#if M07_VIRTIO_NET_PROBE
                        _ = BootSequence.RunOwnedVirtioNet(state->QueuePhase == 20, state, &RunOwnedVirtioNet);
#if M07_ARP_RECEIVE_PROBE
                        _ = BootSequence.RunOwnedArpReceive(state->NetPhase == 3, state, &RunOwnedArpReceive);
#if M07_026_PROBE
                        if (!BootSequence.RunOwnedNetworkService(state->ArpPhase == 3, state->ServicePhase == 2,
                            transition->Mode == FirmwareServicesMode.BootServicesExited && transition->RetainedFirmwareCallbacksCleared &&
                            state->Arena.Binding->RetainedPointersCleared, state, &RunOwnedNetworkService))
                            return;
#endif
#endif
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE
                        _ = BootSequence.RunOwnedVirtqueueInterrupt(state->ApicPhase == 2 && state->VirtioPhase == 16, state, &RunOwnedVirtqueueInterrupt);
#endif
                    }
#endif
#if M05_OWNED_ACPI_SELECTION_PROBE
                    _ = BootSequence.RunOwnedAcpiSelection(state->OwnedPhase == 2, state, &RunOwnedAcpiSelection);
#endif
#if M05_PHYSICAL_MAPPING_PROBE
                    _ = BootSequence.RunPostFirmwarePhysicalMapping(state->OwnedPhase == 2, state, &RunPhysicalMapping);
#endif
#if CLROS_NORMAL_VERIFIED_EXIT
                    _ = BootSequence.ReportVerifiedExitCompletion(state, &WriteVerifiedExitBytes);
#endif
                }
#elif M03_FIRMWARE_ADAPTER_REVOCATION_PROBE
                if (FirstExitPostExitDiagnostics.Report(ref state->Diagnostic, ref state->Arena, state->Memory))
                    _ = ReportRevokedAdapters(state);
#else
                _ = FirstExitPostExitDiagnostics.Report(ref state->Diagnostic, ref state->Arena, state->Memory);
#endif
                // All returns, including success, terminate in the native stub.
            }
        }

#if M05_PCI_ENUMERATION_PROBE
#if M05_APIC_SETUP_PROBE
        private static bool RunOwnedApicSetup(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->ApicPhase != 0 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->ApicPhase = 1;
            if (!WriteInterruptLiteral(state, 20, 0))
                return false;
            if (!RunApicCore(state))
            {
                _ = WriteInterruptLiteral(state, 20, 3);
                return false;
            }
            state->ApicPhase = 2;
            return WriteInterruptLiteral(state, 20, 1) && WriteInterruptLiteral(state, 20, 2);
        }

        private static bool RunApicCore(Storage* state)
        {
            byte* code = state->Memory.Code;
            if (!X64ApicObservation.Validate(code + FirstExitCodeBundle.ApicObservationOffset, X64ApicObservation.Bytes) ||
                !X64PciBarAccess.Validate(code + FirstExitCodeBundle.PciBarOffset, X64PciBarAccess.Bytes) ||
                !AuditInterruptMappings(state) ||
                ((delegate* unmanaged<XApicCpuObservation*, uint>)(code + FirstExitCodeBundle.ApicObservationOffset))(&state->ApicCpu) != 1)
                return false;
            var cpu = state->ApicCpu;
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if ((ulong)workspace != state->AcpiWorkspace || state->AcpiWorkspaceBytes != (ulong)sizeof(AcpiBootstrapWorkspace) ||
                    !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity))
                    return false;
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                if (!selected.Succeeded || (ulong)(copied->Bytes + 16 + selected.Madt.Offset) != state->MadtAddress ||
                    selected.Madt.Length != state->MadtBytes ||
                    MADTLocalAPICIOAPICSeparatelyPollingProof.Parse((byte*)state->MadtAddress, state->MadtBytes, cpu.BspId,
                        out var topology) != MadtPollingStatus.Success || topology.BspApicId != 0 ||
                    topology.LocalApicAddress != state->Memory.References.LocalApicPage ||
                    topology.IoApicAddress != state->Memory.References.IoApicPage)
                    return false;
                state->ApicTopology = topology;
            }
            var t = state->ApicTopology;
            if (!WriteInterruptRow(state, false, 0, t.LocalApicAddress, t.IoApicAddress, cpu.BspId, t.HeaderFlags))
                return false;
            var result = BoundedXApicSetup.Run(t, cpu.FeaturesEdx, cpu.BspId, cpu.ApicBaseMsr, cpu.Rflags, ApicAccess(state));
            if (result.Status != XApicSetupStatus.Success)
            {
                _ = WriteInterruptRow(state, false, 9, (ulong)result.Status, result.LocalVersion, result.IoVersion, cpu.ApicBaseMsr);
                return false;
            }
            return WriteInterruptRow(state, false, 1, cpu.ApicBaseMsr, cpu.FeaturesEdx, result.LocalVersion, result.IoVersion) &&
                WriteInterruptRow(state, false, 2, result.MaskedLocalEntries, result.MaskedIoEntries, result.LegacyPicMasked ? 1UL : 0, t.MaximumIsoGsi) &&
                WriteInterruptRow(state, false, 3, result.TaskPriority, result.Spurious, cpu.Rflags & 0x202, t.IoApicGsiBase) &&
                FirstExitOwnedArena.ValidateCanaries(state->Arena);
        }

        private static bool AuditInterruptMappings(Storage* state)
        {
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            return FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, state->Memory.Code, state->Memory.Capture,
                    state->Memory.Mapping, state->Memory.References, state->Memory.Allocator, state->Memory.Tables,
                    ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) &&
                SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(state->Memory.Tables, ranges, count,
                    FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, state->Memory.PageCapacity,
                    state->Memory.Entries, state->Memory.EntryCapacity).Succeeded;
        }
        private static XApicSetupAccess ApicAccess(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            return new XApicSetupAccess
            {
                Context = state,
                Read32 = (delegate* unmanaged<void*, ulong, uint*, byte>)code,
                Write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16),
                ReadPort8 = &ReadPicMask,
                WritePort8 = &WritePicMask
            };
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadPicMask(void* context, ushort port, byte* output)
        {
            Storage* state = (Storage*)context;
            if (state->ApicPhase != 1 || (port != 0x21 && port != 0xa1))
                return 0;
            *output = ((delegate* unmanaged<ushort, byte>)(state->Memory.Code + FirstExitCodeBundle.In8Offset))(port);
            return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte WritePicMask(void* context, ushort port, byte value)
        {
            Storage* state = (Storage*)context;
            if (state->ApicPhase != 1 || (port != 0x21 && port != 0xa1) || value != 255)
                return 0;
            ((delegate* unmanaged<ushort, byte, void>)(state->Memory.Code + FirstExitCodeBundle.Out8Offset))(port, value);
            return 1;
        }
        private static bool WriteInterruptRow(Storage* state, bool msix, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 9)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = msix ? 0x205152495f35304dUL : 0x434950415f35304dUL;
            uint length = 8;
            if (!msix)
                text[length++] = (byte)' ';
            text[length++] = (byte)('0' + stage);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
        private static bool WriteInterruptLiteral(Storage* state, uint issue, uint kind)
        {
            if (issue < 20 || issue > 21 || kind > 3)
                return false;
            ulong* words = stackalloc ulong[5];
            uint bytes, digit;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3030UL;
                bytes = 20;
                digit = 17;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3030305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
                digit = 12;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
                digit = 16;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x735f636970612030UL;
                words[3] = 0x0a0d65746174UL;
                bytes = 30;
                digit = 16;
            }
            ((byte*)words)[digit - 1] = (byte)('0' + issue / 10);
            ((byte*)words)[digit] = (byte)('0' + issue % 10);
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
        private static bool PrepareMsixMapping(Storage* state, out ulong page)
        {
            page = 0;
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out _) ||
                MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)words, 256, out var cap) != MsixStatus.Success ||
                cap.TableBar != cap.PbaBar || cap.TableBar >= 4)
                return false;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            for (uint i = 0; i < 6; i++)
                bars[i] = default;
            bool found = false;
            for (uint i = 0; i < 6; i++)
            {
                uint raw = words[4 + i];
                if (BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Format(raw, i, out var kind) != PciBarProbeStatus.Success)
                    return false;
                if (i == cap.TableBar)
                {
                    if ((kind != PciSizedBarKind.Memory32 && kind != PciSizedBarKind.Memory64) ||
                        BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(raw, words[5 + i],
                            0xfffff000U | (raw & 15), 0xffffffff, i, out bars[i]) != PciBarProbeStatus.Success)
                        return false;
                    if (kind == PciSizedBarKind.Memory64)
                        bars[i + 1] = new PciSizedBar(PciSizedBarKind.UpperHalf, 0, 0, false);
                    found = true;
                }
                if (kind == PciSizedBarKind.Memory64)
                    i++;
            }
            // A provisional single-page aperture grants no device-programming
            // authority. The destructive post-exit BAR probe must confirm it.
            if (!found || MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Plan(cap, bars, 6, out var regions) != MsixStatus.Success ||
                regions.Table.PageCount != 1 || regions.Pba.PageCount != 1 || regions.Table.PageStart != regions.Pba.PageStart ||
                regions.Table.PageStart != bars[cap.TableBar].Base ||
                !CommonNotifyISRDeviceConfigurationRegions.AdmitMmioPages(regions.Table.PageStart, 1, s_bootHandoff.MemoryMap))
                return false;
            state->MsixCapability = cap;
            state->MsixRegions = regions;
            page = regions.Table.PageStart;
            return true;
        }

        private static bool AdmitSizedMsix(Storage* state, uint* words, PciSizedBar* bars)
        {
            if (state->InterruptPhase != 0 ||
                MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)words, 256, out var cap) != MsixStatus.Success ||
                cap.CapabilityOffset != state->MsixCapability.CapabilityOffset || cap.Control != state->MsixCapability.Control ||
                cap.TableBar != state->MsixCapability.TableBar || cap.PbaBar != state->MsixCapability.PbaBar ||
                cap.TableOffset != state->MsixCapability.TableOffset || cap.PbaOffset != state->MsixCapability.PbaOffset ||
                MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Plan(cap, bars, 6, out var regions) != MsixStatus.Success ||
                bars[cap.TableBar].Bytes != 4096 || bars[cap.TableBar].Base != state->Memory.References.MsixPage ||
                regions.Table.PhysicalStart != state->MsixRegions.Table.PhysicalStart || regions.Table.Bytes != state->MsixRegions.Table.Bytes ||
                regions.Pba.PhysicalStart != state->MsixRegions.Pba.PhysicalStart || regions.Pba.Bytes != state->MsixRegions.Pba.Bytes ||
                regions.Table.PageCount != 1 || regions.Pba.PageCount != 1 || regions.Table.PageStart != state->Memory.References.MsixPage ||
                regions.Pba.PageStart != state->Memory.References.MsixPage || !AuditInterruptMappings(state))
                return false;
            state->InterruptPhase = 1;
            return true;
        }

        private static MsixRegisterAccess MsixAccess(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            return new MsixRegisterAccess
            {
                Context = state,
#if M06_BLOCK_MSIX_PROBE
                Read16 = &ReadBlockWord,
#else
                Read16 = &ReadQueueWord,
#endif
                Read32 = (delegate* unmanaged<void*, ulong, uint*, byte>)code,
                Write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16),
                Write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32)
            };
        }
        private static byte* TemporaryInterruptIdt(Storage* state) => (byte*)(((ulong)state->InterruptIdtStorage + 15) & ~15UL);

#if M05_VIRTQUEUE_INTERRUPT_PROBE
        private static bool PrepareInterruptTransaction(Storage* state, VirtioMappedRegion common)
        {
            if (state->InterruptPhase != 2 || state->ApicPhase != 2 || state->QueuePhase != 16 ||
                !AuditInterruptMappings(state))
                return false;
            byte* code = state->Memory.Code + FirstExitCodeBundle.MsixInterruptOffset;
            byte* temporary = TemporaryInterruptIdt(state);
            if (!X64MsixInterrupt.Validate(code, X64MsixInterrupt.Bytes) ||
                !X64MsixInterrupt.PrepareTemporaryIdt(state->Arena.Descriptors->FinalIdt, temporary, 4096, (ulong)state->Memory.Code, (ulong)code) ||
                BoundedXApicSetup.CheckQuiescent(state->ApicTopology.LocalApicAddress, ApicAccess(state)) != XApicSetupStatus.Success)
                return false;
            state->InterruptContext = default;
            state->InterruptContext.EoiAddress = state->ApicTopology.LocalApicAddress + 0xb0;
            *(ushort*)state->InterruptContext.TemporaryIdtr = 4095;
            *(ulong*)(state->InterruptContext.TemporaryIdtr + 2) = (ulong)temporary;
            state->InterruptProgramming = BoundedMsixProgramming.Prepare(state->MsixCapability, state->MsixRegions,
                state->PciWindow.Start + 0x30000, common, state->ApicTopology.BspApicId, MsixAccess(state));
            if (state->InterruptProgramming.Status != MsixStatus.Success)
                return false;
            state->InterruptPhase = 3;
            return true;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte WaitQueueInterrupt(void* context, uint limit)
        {
            Storage* state = (Storage*)context;
            byte* code = state->Memory.Code + FirstExitCodeBundle.MsixInterruptOffset;
            byte* original = state->Arena.Descriptors->FinalIdt;
            byte* temporary = TemporaryInterruptIdt(state);
            if (state->InterruptPhase != 3 || state->ApicPhase != 2 || state->QueuePhase != 18 || limit != X64MsixInterrupt.MaximumIterations ||
                !X64MsixInterrupt.Validate(code, X64MsixInterrupt.Bytes) ||
                !X64MsixInterrupt.ValidateTemporaryIdt(original, temporary, 4096, (ulong)state->Memory.Code, (ulong)code) ||
                BoundedMsixProgramming.SetFunctionMask(state->MsixCapability, state->PciWindow.Start + 0x30000, false, MsixAccess(state)) != MsixStatus.Success)
                return 0;
            state->InterruptPhase = 4;
            uint observed = ((delegate* unmanaged<MsixInterruptContext*, uint, uint>)code)(&state->InterruptContext, limit);
            if (!X64MsixInterrupt.ValidateCompletion(&state->InterruptContext, (ulong)code, (ulong)original, (ulong)temporary,
                    state->ApicTopology.LocalApicAddress, limit, observed) ||
                !X64MsixInterrupt.ValidateTemporaryIdt(original, temporary, 4096, (ulong)state->Memory.Code, (ulong)code) ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return 0;
            state->InterruptPhase = 5;
            return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte MaskQueueInterrupt(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->InterruptPhase < 3 || state->InterruptPhase > 5 || state->QueuePhase != 18 ||
                BoundedMsixProgramming.SetFunctionMask(state->MsixCapability, state->PciWindow.Start + 0x30000, true, MsixAccess(state)) != MsixStatus.Success)
                return 0;
            state->InterruptPhase = 6;
            return 1;
        }

        private static bool RunOwnedVirtqueueInterrupt(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->InterruptPhase != 1 || state->QueuePhase != 0 || state->ApicPhase != 2 ||
                state->VirtioPhase != 16 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->InterruptPhase = 2;
            state->QueuePhase = 16;
            if (!WriteInterruptLiteral(state, 21, 0))
                return false;
            if (!RunQueueCore(state) || state->InterruptPhase != 7)
            {
                _ = BoundedMsixProgramming.Disable(state->MsixCapability, state->PciWindow.Start + 0x30000, MsixAccess(state));
                _ = WriteInterruptRow(state, true, 9, state->InterruptPhase, state->InterruptContext.Count,
                    (ulong)state->InterruptProgramming.Status, (ulong)state->InterruptQueue.Status);
                _ = WriteInterruptLiteral(state, 21, 3);
                return false;
            }
            state->QueuePhase = 20;
            MsixInterruptContext* c = &state->InterruptContext;
            ulong code = (ulong)state->Memory.Code + FirstExitCodeBundle.MsixInterruptOffset;
            var p = state->InterruptProgramming;
            var r = state->InterruptQueue;
            if (!AuditInterruptMappings(state) || !FirstExitOwnedArena.ValidateInstalledDescriptors(ref state->Arena, (ulong)state->Memory.Code) ||
                BoundedXApicSetup.CheckQuiescent(state->ApicTopology.LocalApicAddress, ApicAccess(state)) != XApicSetupStatus.Success ||
                !WriteInterruptRow(state, true, 0, state->MsixRegions.Table.PhysicalStart, state->MsixRegions.Pba.PhysicalStart,
                    state->MsixCapability.TableSize, state->MsixCapability.CapabilityOffset) ||
                !WriteInterruptRow(state, true, 1, p.MessageAddress, p.MessageData, p.Control, p.ConfigVector) ||
                !WriteInterruptRow(state, true, 2, code, (ulong)state->Arena.Descriptors->FinalIdt, (ulong)TemporaryInterruptIdt(state), c->EoiAddress) ||
                !WriteInterruptRow(state, true, 3, c->Count, c->SpuriousCount, c->BeforeEoiIsr, c->AfterEoiIsr) ||
                !WriteInterruptRow(state, true, 4, c->InterruptedRip, c->InterruptedFlags, c->EntryFlags, c->ExitFlags) ||
                !WriteInterruptRow(state, true, 5, c->Iterations, X64MsixInterrupt.MaximumIterations, r.Polls, r.Fences) ||
                !WriteInterruptRow(state, true, 6, *(ushort*)c->OriginalIdtr, *(ulong*)(c->OriginalIdtr + 2),
                    *(ushort*)c->RestoredIdtr, *(ulong*)(c->RestoredIdtr + 2)) ||
                !WriteInterruptRow(state, true, 7, state->MsixCapability.Control, 1, 1, 1))
                return false;
            state->InterruptPhase = 8;
            return WriteInterruptLiteral(state, 21, 1) && WriteInterruptLiteral(state, 21, 2);
        }
#endif
#endif
        private static bool RunOwnedPciEnumeration(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->PciPhase != 0 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed)
                return false;
            state->PciPhase = 4;
            if (!WritePciLiteral(state, 4, 0))
                return false;
            if (!RunPciCore(state))
            {
                _ = WritePciLiteral(state, (uint)state->PciPhase, 3);
                return false;
            }
            state->PciPhase = 8;
            return true;
        }

        private static bool RunPciCore(Storage* state)
        {
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (!AdmitOwnedAcpiState(state) || workspace->Phase != 3 ||
                    !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity) ||
                    !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                    return false;
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(
                    copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                if (!selected.Succeeded || !ECAMPageVendorDeviceHeader.TryWindow(
                    copied->Bytes + 16 + selected.Mcfg.Offset, selected.Mcfg.Length, 0,
                    state->PciWindow.FirstBus, state->PciWindow.LastBus, out var window) ||
                    window.Start != state->PciWindow.Start || window.Bytes != 0x100000 ||
                    window.BusZeroBase != state->PciWindow.BusZeroBase || window.Segment != state->PciWindow.Segment ||
                    window.AllocationFirst != state->PciWindow.AllocationFirst || window.AllocationLast != state->PciWindow.AllocationLast ||
                    window.Start != state->Memory.References.EcamStart || state->Memory.References.EcamPages != 256)
                    return false;
            }
            byte* readCode = state->Memory.Code + FirstExitCodeBundle.PciReadOffset;
            if (!X64PciConfigurationRead.Validate(readCode, X64PciConfigurationRead.Bytes) ||
                !BoundedPCIConfigurationSpaceInterface.TryOpen(state->PciWindow, null,
                    (delegate* unmanaged<void*, ulong, uint>)readCode, out var access))
                return false;
            byte bus = state->PciWindow.FirstBus;
            var invalidRegister = BoundedPCIConfigurationSpaceInterface.Read32(ref access, bus, 0, 0, 4096, out _);
            var outside = BoundedPCIConfigurationSpaceInterface.Address(state->PciWindow, bus == 255 ? (byte)0 : (byte)(bus + 1), 0, 0, 0, out _);
            BoundedPciAccess revoked = default;
            var inactive = BoundedPCIConfigurationSpaceInterface.Read32(ref revoked, bus, 0, 0, 0, out _);
            if (invalidRegister != BoundedPciStatus.InvalidRegister || outside != BoundedPciStatus.OutsideWindow || inactive != BoundedPciStatus.InvalidAccess ||
                !WritePciRow(state, 0, state->PciWindow.BusZeroBase, state->PciWindow.Start, state->PciWindow.Bytes, state->PciWindow.Segment) ||
                !WritePciRow(state, 1, (ulong)invalidRegister, (ulong)outside, (ulong)inactive, 1) ||
                !WritePciLiteral(state, 4, 1) || !WritePciLiteral(state, 4, 2))
                return false;

            state->PciPhase = 5;
            if (!WritePciLiteral(state, 5, 0) ||
                ECAMPageVendorDeviceHeader.ReadHeader(ref access, bus, 0, 0, out var first) != BoundedPciStatus.Success ||
                BoundedPCIConfigurationSpaceInterface.Address(state->PciWindow, bus, 0, 0, 0, out ulong page) != BoundedPciStatus.Success ||
                !WritePciRow(state, 2, page, first.Identity, first.HeaderType, first.ClassRevision) ||
                !WritePciRow(state, 3, 1, 1, 1, 4096) || !WritePciLiteral(state, 5, 1) || !WritePciLiteral(state, 5, 2))
                return false;

            state->PciPhase = 6;
            if (!WritePciLiteral(state, 6, 0))
                return false;
            BoundedPciFunction* devices = stackalloc BoundedPciFunction[16];
            BoundedPciFunction* repeat = stackalloc BoundedPciFunction[16];
            var scan = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, devices, 16, 256);
            var again = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, repeat, 16, 256);
            if (!scan.Complete || !again.Complete || scan.Count < 2 || scan.Count != again.Count || scan.Visited != again.Visited ||
                !scan.HasLast || !again.HasLast || PciTuple(scan.LastSegment, scan.LastBus, scan.LastDevice, scan.LastFunction) !=
                PciTuple(again.LastSegment, again.LastBus, again.LastDevice, again.LastFunction))
                return false;
            for (uint i = 0; i < scan.Count; i++)
            {
                if (PciTuple(devices[i].Segment, devices[i].Bus, devices[i].Device, devices[i].Function) !=
                    PciTuple(repeat[i].Segment, repeat[i].Bus, repeat[i].Device, repeat[i].Function) ||
                    devices[i].Header.Identity != repeat[i].Header.Identity || devices[i].Header.HeaderType != repeat[i].Header.HeaderType ||
                    devices[i].Header.ClassRevision != repeat[i].Header.ClassRevision || devices[i].Header.CommandStatus != repeat[i].Header.CommandStatus ||
                    devices[i].Header.BusNumbers != repeat[i].Header.BusNumbers || devices[i].Header.CapabilityPointer != repeat[i].Header.CapabilityPointer)
                    return false;
            }
            var partial = PCIBusesDevicesFunctionsBoundedDiagnostics.Scan(ref access, repeat, 1, 256);
            if (partial.Status != BoundedPciStatus.Capacity || partial.Count != 1 || partial.RequiredCapacity != 2 || partial.CanEstablishAbsenceInAllocation ||
                !WritePciRow(state, 4, scan.Count, scan.Visited, PciTuple(scan.LastSegment, scan.LastBus, scan.LastDevice, scan.LastFunction), scan.CanEstablishAbsenceInAllocation ? 1UL : 0) ||
                !WritePciRow(state, 5, again.Count, again.Visited, 1, scan.HasUnscannedBridge ? 1UL : 0) ||
                !WritePciRow(state, 6, (ulong)partial.Status, partial.Count, partial.RequiredCapacity, 0) ||
                !WritePciLiteral(state, 6, 1) || !WritePciLiteral(state, 6, 2))
                return false;

            state->PciPhase = 7;
            if (!WritePciLiteral(state, 7, 0))
                return false;
            uint* snapshot = stackalloc uint[66];
            snapshot[0] = 0xabcddcba;
            snapshot[65] = 0x13579bdf;
            byte* config = (byte*)(snapshot + 1);
            bool found = false;
            for (uint i = 0; i < scan.Count; i++)
            {
                if ((devices[i].Header.CommandStatus & 0x00100000) == 0)
                    continue;
                if (ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, devices[i].Bus, devices[i].Device, devices[i].Function,
                    snapshot + 1, 64) != BoundedPciStatus.Success || snapshot[1] != devices[i].Header.Identity)
                    return false;
                var walk = PciCapabilityWalk.Validate(config, 256);
                if (!walk.Succeeded)
                    return false;
                if (walk.Count == 0)
                    continue;
                if (!WritePciRow(state, 7, devices[i].Header.Identity, PciTuple(devices[i].Segment, devices[i].Bus, devices[i].Device, devices[i].Function), walk.Count, (ulong)walk.Status))
                    return false;
                found = true;
                break;
            }
            if (!found)
                return false;
            config[0x34] = 0x40;
            config[0x41] = 0x40;
            var cycle = PciCapabilityWalk.Validate(config, 256);
            config[0x41] = 0x41;
            var alignment = PciCapabilityWalk.Validate(config, 256);
            config[0x41] = 0x3c;
            var range = PciCapabilityWalk.Validate(config, 256);
            BoundedPCIConfigurationSpaceInterface.Revoke(ref access);
            return cycle.Status == PciCapabilityWalkStatus.LoopDetected && alignment.Status == PciCapabilityWalkStatus.OffsetUnaligned &&
                range.Status == PciCapabilityWalkStatus.OffsetOutOfRange && !access.IsActive &&
                snapshot[0] == 0xabcddcba && snapshot[65] == 0x13579bdf && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                WritePciRow(state, 8, (ulong)cycle.Status, (ulong)alignment.Status, (ulong)range.Status, 1) &&
                WritePciLiteral(state, 7, 1) && WritePciLiteral(state, 7, 2);
        }

        private static ulong PciTuple(ushort segment, byte bus, byte device, byte function) =>
            ((ulong)segment << 24) | ((ulong)bus << 16) | ((ulong)device << 8) | function;

        private static bool WritePciRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 8)
                return false;
            byte* text = stackalloc byte[90];
            *(ulong*)text = 0x204943505f35304dUL;
            text[8] = (byte)('0' + stage);
            uint length = 9;
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }

        private static bool WritePciLiteral(Storage* state, uint issue, uint kind)
        {
            if (issue < 4 || issue > 7 || kind > 3)
                return false;
            ulong* words = stackalloc ulong[4];
            uint bytes, digit;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3030UL;
                bytes = 20;
                digit = 17;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3030305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
                digit = 12;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
                digit = 16;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x74735f6963702030UL;
                words[3] = 0xa0d657461UL;
                bytes = 29;
                digit = 16;
            }
            ((byte*)words)[digit] = (byte)('0' + issue);
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
#endif

#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        private static bool PrepareVirtioMapping(Storage* state, out ulong start)
        {
            start = 0;
            uint* words = stackalloc uint[64];
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out var caps))
                return false;
            for (uint i = 0; i < 6; i++)
                bars[i] = default;
            // The runner fixes a modern QEMU net or block device with a 16 KiB BAR4.
            // This provisional declaration grants only initial reset access.
            // After firmware exit/reset, destructive sizing must confirm it
            // before mapping/negotiation success is reported.
            if ((words[8] & 15) != 12 || BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(
                    words[8], words[9], 0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success ||
                PlanVirtioFixture(caps, bars, regions) != VirtioRegionStatus.Success)
                return false;
            for (uint i = 0; i < 4; i++)
                if (!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[i], s_bootHandoff.MemoryMap))
                    return false;
            start = bars[4].Base;
            return true;
        }

        private static bool ReadVirtioFixture(Storage* state, delegate* unmanaged<void*, ulong, uint> read,
            uint* words, out VirtioPciCapabilitySet caps)
        {
            caps = default;
            if (state->PciWindow.FirstBus != 0 || state->PciWindow.LastBus != 0 ||
                !BoundedPCIConfigurationSpaceInterface.TryOpen(state->PciWindow, null, read, out var access))
                return false;
            var snapshot = ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, 0, 6, 0, words, 64);
            BoundedPCIConfigurationSpaceInterface.Revoke(ref access);
            if (snapshot != BoundedPciStatus.Success || (words[1] & 2) == 0)
                return false;
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
            if (LiveVirtioBlkPCIDevice.Parse((byte*)words, 256, 0, 6, 0, out var binding) != VirtioBlkBindingStatus.Success ||
                MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)words, 256, out var msix) != MsixStatus.Success)
                return false;
            caps = binding.Capabilities;
#else
            if (words[0] != 0x10411af4 || ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse((byte*)words, 256, out caps) != VirtioCapabilityStatus.Success)
                return false;
#endif
            return VirtioFixtureCapability(caps.CommonConfiguration, 0) && VirtioFixtureCapability(caps.NotifyConfiguration, 0x3000) &&
                VirtioFixtureCapability(caps.IsrStatus, 0x1000) && VirtioFixtureCapability(caps.DeviceConfiguration, 0x2000);
        }

        private static bool VirtioFixtureCapability(VirtioPciCapabilityMetadata cap, uint offset) =>
            cap.Bar == 4 && cap.Offset == offset && cap.Length == 4096;

        private static VirtioRegionStatus PlanVirtioFixture(VirtioPciCapabilitySet caps, PciSizedBar* bars,
            VirtioConfigurationRegion* regions) =>
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
            CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4);
#else
            CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4);
#endif

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static uint ReadVirtioSnapshotWord(void* context, ulong address) => *(uint*)address;
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadVirtioStatus(void* context, ulong address, byte* output)
        {
            *output = *(byte*)address;
            return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte WriteVirtioStatus(void* context, ulong address, byte value)
        {
            *(byte*)address = value;
            return 1;
        }

#if M05_VIRTIO_NEGOTIATION_PROBE
        private static bool RunOwnedVirtioNegotiation(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->VirtioPhase != 0 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->VirtioPhase = 12;
            if (!RunVirtioCore(state))
            {
                _ = WriteVirtioLiteral(state, state->VirtioPhase, 3);
                return false;
            }
            state->VirtioPhase = 16;
            return true;
        }

        private static bool RunVirtioCore(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* snapshotCode = state->Memory.Code + FirstExitCodeBundle.PciReadOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64PciConfigurationRead.Validate(snapshotCode, X64PciConfigurationRead.Bytes) ||
                !WriteVirtioLiteral(state, 12, 0))
                return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, (delegate* unmanaged<void*, ulong, uint>)snapshotCode, words, out var caps))
                return false;
            ulong start = state->Memory.References.VirtioBarStart;
            ulong observedBase = ((ulong)words[9] << 32) | (words[8] & ~15u);
            if (observedBase != start || !WriteVirtioRow(state, 0, 0x600, words[0], start, 4) ||
                !WriteVirtioRow(state, 1, start, start + 0x3000, start + 0x1000, start + 0x2000) ||
                !WriteVirtioLiteral(state, 12, 1) || !WriteVirtioLiteral(state, 12, 2))
                return false;
            state->VirtioPhase = 13;
            if (!WriteVirtioLiteral(state, 13, 0))
                return false;
            ulong page = state->PciWindow.Start + 0x30000;
            uint originalCommand = words[1] & 0xffff, ownedCommand = (originalCommand & 0xfffb) | 0x400, current = 0;
            // Terminal ownership: disable bus mastering and INTx before reset.
            // Firmware is gone; no firmware interface or device DMA is used.
            if (write16(null, page + 4, ownedCommand) != 1 || read(null, page + 4, &current) != 1 ||
                (current & 0xffff) != ownedCommand || !ResetVirtio(start))
                return false;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            var probe = N32BitN64BitBARsWhileAlwaysRestoring.Probe(state->PciWindow, 0, 6, 0, null, read, write32, write16, bars, 6);
            if (!probe.Succeeded || bars[4].Kind != PciSizedBarKind.Memory64 || bars[4].Base != start ||
                bars[4].Bytes != 0x4000 || bars[5].Kind != PciSizedBarKind.UpperHalf ||
                !WriteVirtioRow(state, 2, originalCommand, ownedCommand, 0, bars[4].Bytes))
                return false;
#if M05_VIRTQUEUE_INTERRUPT_PROBE
            if (!AdmitSizedMsix(state, words, bars))
                return false;
#endif
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success)
                return false;
            VirtioMappedRegion common = default;
            for (uint i = 0; i < 4; i++)
            {
                if (regions[i].PageCount != 1 || regions[i].PageStart < start || regions[i].PageStart >= start + 0x4000 ||
                    CommonNotifyISRDeviceConfigurationRegions.Bind(regions[i], regions[i].PageStart, out var mapped) != VirtioRegionStatus.Success ||
                    !WriteVirtioRow(state, i + 3, mapped.PhysicalStart, mapped.VirtualStart, mapped.Bytes, regions[i].PageCount))
                    return false;
                if (i == 0)
                    common = mapped;
            }
            // Re-audit the installed, accessed image, including all four UC/RW/NX
            // leaves. Numeric region binding is never accepted as mapping proof.
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            if (!FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, state->Memory.Code, state->Memory.Capture,
                    state->Memory.Mapping, state->Memory.References, state->Memory.Allocator, state->Memory.Tables,
                    ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) ||
                !SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(state->Memory.Tables, ranges, count,
                    FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, state->Memory.PageCapacity,
                    state->Memory.Entries, state->Memory.EntryCapacity).Succeeded ||
                !WriteVirtioLiteral(state, 13, 1) || !WriteVirtioLiteral(state, 13, 2))
                return false;
            state->VirtioPhase = 14;
            if (!WriteVirtioLiteral(state, 14, 0))
                return false;
            var negotiated = ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.Run(common, BoundedVirtioFeatures.Version1, 1024,
                null, read, write32, &ReadVirtioStatus, &WriteVirtioStatus);
            if (!WriteVirtioRow(state, 7, negotiated.Offered, negotiated.Selected, negotiated.DeviceStatus, (ulong)negotiated.Status) ||
                !negotiated.Succeeded || !WriteVirtioLiteral(state, 14, 1) || !WriteVirtioLiteral(state, 14, 2))
                return false;
            state->VirtioPhase = 15;
            if (!WriteVirtioLiteral(state, 15, 0))
                return false;
            var unsupported = BoundedVirtioFeatures.Select(negotiated.Offered, BoundedVirtioFeatures.Version1 | 1, out ulong none);
            var missing = BoundedVirtioFeatures.Select(0, BoundedVirtioFeatures.Version1, out ulong empty);
            var rejected = BoundedVirtioFeatures.ValidateFeaturesOk(3);
            var failed = BoundedVirtioFeatures.ValidateFeaturesOk(0x83);
            if (unsupported != VirtioFeatureStatus.UnsupportedRequested || missing != VirtioFeatureStatus.MissingVersion1 ||
                rejected != VirtioFeatureStatus.FeaturesRejected || failed != VirtioFeatureStatus.DeviceFailed || none != 0 || empty != 0 ||
                !WriteVirtioRow(state, 8, (ulong)unsupported, (ulong)missing, (ulong)rejected, (ulong)failed) ||
                !ResetVirtio(start) || read(null, page + 4, &current) != 1 || (current & 0xffff) != ownedCommand ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena) || !WriteVirtioRow(state, 9, 0, current & 0xffff, 1, 0))
                return false;
            return WriteVirtioLiteral(state, 15, 1) && WriteVirtioLiteral(state, 15, 2);
        }

#endif
        private static bool ResetVirtio(ulong common)
        {
            var write = (delegate* unmanaged<void*, ulong, byte, byte>)&WriteVirtioStatus;
            var read = (delegate* unmanaged<void*, ulong, byte*, byte>)&ReadVirtioStatus;
            if (write(null, common + 20, 0) != 1)
                return false;
            byte value = 0;
            for (uint i = 0; i < 1024; i++)
            {
                if (read(null, common + 20, &value) != 1)
                    return false;
                if (value == 0)
                    return true;
            }
            return false;
        }

        private static bool WriteVirtioRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 9)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x545249565f35304dUL;
            *(uint*)(text + 8) = 0x00204f49;
            uint length = 11;
            text[length++] = (byte)('0' + stage);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }

        private static bool WriteVirtioLiteral(Storage* state, uint issue, uint kind)
        {
            if (issue < 12 || issue > 19 || kind > 3)
                return false;
            ulong* words = stackalloc ulong[5];
            uint bytes, digit;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3030UL;
                bytes = 20;
                digit = 17;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3030305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
                digit = 12;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
                digit = 16;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x6f69747269762030UL;
                words[3] = 0x0a0d65746174735fUL;
                bytes = 32;
                digit = 16;
            }
            ((byte*)words)[digit - 1] = (byte)('0' + issue / 10);
            ((byte*)words)[digit] = (byte)('0' + issue % 10);
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
#endif

#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        private static bool RunOwnedVirtioBlockQueueSetup(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->BlockPhase != 0 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->BlockPhase = 1;
            if (!RunBlockCore(state, out var common) || !RunBlockQueueCore(state, common))
            {
                _ = WriteBlockLiteral(state, state->BlockPhase, 3);
                return false;
            }
            state->BlockPhase = 5;
            return true;
        }

        private static bool RunBlockCore(Storage* state, out VirtioMappedRegion common)
        {
            common = default;
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* snapshotCode = state->Memory.Code + FirstExitCodeBundle.PciReadOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64PciConfigurationRead.Validate(snapshotCode, X64PciConfigurationRead.Bytes) ||
                !WriteBlockLiteral(state, 1, 0))
                return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            var read8 = (delegate* unmanaged<void*, ulong, byte*, byte>)&ReadVirtioStatus;
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, (delegate* unmanaged<void*, ulong, uint>)snapshotCode, words, out var caps) ||
                LiveVirtioBlkPCIDevice.Parse((byte*)words, 256, 0, 6, 0, out var binding) != VirtioBlkBindingStatus.Success ||
                MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)words, 256, out state->BlockMsix) != MsixStatus.Success)
                return false;
            ulong start = state->Memory.References.VirtioBarStart;
            if ((((ulong)words[9] << 32) | (words[8] & ~15u)) != start)
                return false;
            byte initialStatus = 0;
            state->BlockCommand = (uint)((binding.OriginalCommand & 0xfffb) | 0x400);
            // Firmware may have booted from this block device. Retain its memory,
            // stop bus mastering/INTx, then observe reset before destructive BAR
            // sizing. MSI-X must already be disabled in this admitted fixture.
            if (BlockDisableBusMaster(state) != 1 || read8(null, start + 20, &initialStatus) != 1 || !ResetVirtio(start))
                return false;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            var sized = N32BitN64BitBARsWhileAlwaysRestoring.Probe(state->PciWindow, 0, 6, 0, null, read, write32, write16, bars, 6);
            if (!sized.Succeeded || bars[4].Kind != PciSizedBarKind.Memory64 || bars[4].Base != start ||
                bars[4].Bytes != 0x4000 || bars[5].Kind != PciSizedBarKind.UpperHalf)
                return false;
#if M06_BLOCK_MSIX_PROBE
            if (!AdmitSizedMsix(state, words, bars))
                return false;
#endif
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success)
                return false;
            for (uint i = 0; i < 4; i++)
            {
                if (regions[i].PageCount != 1 || regions[i].PageStart < start || regions[i].PageStart >= start + 0x4000 ||
                    CommonNotifyISRDeviceConfigurationRegions.Bind(regions[i], regions[i].PageStart, out var mapped) != VirtioRegionStatus.Success)
                    return false;
                if (i == 0)
                    common = mapped;
#if M06_VIRTIO_BLOCK_READ_PROBE
                if (i == 0)
                    state->BlockCommon = mapped;
                if (i == 1 && CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[i],
                    caps.NotifyConfiguration.Length, regions[i].PageStart, out state->BlockNotify) != VirtioRegionStatus.Success)
                    return false;
                if (i == 3)
                    state->BlockDevice = mapped;
#endif
            }
#if M06_VIRTIO_BLOCK_READ_PROBE
            state->BlockNotifyMultiplier = caps.NotifyConfiguration.NotifyOffsetMultiplier;
#endif
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            if (!FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, state->Memory.Code, state->Memory.Capture,
                    state->Memory.Mapping, state->Memory.References, state->Memory.Allocator, state->Memory.Tables,
                    ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) ||
                !SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(state->Memory.Tables, ranges, count,
                    FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, state->Memory.PageCapacity,
                    state->Memory.Entries, state->Memory.EntryCapacity).Succeeded || BlockDisableBusMaster(state) != 1 ||
                !WriteBlockRow(state, 0, 0x600, binding.Identity, start, binding.OriginalCommand) ||
                !WriteBlockRow(state, 1, state->BlockCommand, initialStatus, 0, state->BlockMsix.Control) ||
                !WriteBlockRow(state, 2, bars[4].Bytes, 4, common.Bytes, regions[3].Bytes) ||
                !WriteBlockLiteral(state, 1, 1) || !WriteBlockLiteral(state, 1, 2))
                return false;
            state->BlockPhase = 2;
            if (!WriteBlockLiteral(state, 2, 0))
                return false;
            var features = MinimalVirtioBlkFeatureSet.Negotiate(common, 1024, null, read, write32, &ReadVirtioStatus, &WriteVirtioStatus);
            var missing = MinimalVirtioBlkFeatureSet.Select(0, out ulong none);
            var rejected = BoundedVirtioFeatures.ValidateFeaturesOk(3);
            var failed = BoundedVirtioFeatures.ValidateFeaturesOk(0x83);
            // The runner exposes the boot image read-only; RO must survive negotiation.
            if (!features.Succeeded || features.Selected != (BoundedVirtioFeatures.Version1 | BoundedVirtioFeatures.BlockReadOnly) ||
                missing != VirtioFeatureStatus.MissingVersion1 || rejected != VirtioFeatureStatus.FeaturesRejected ||
                failed != VirtioFeatureStatus.DeviceFailed || none != 0 ||
                !WriteBlockRow(state, 3, features.Offered, features.Selected, features.DeviceStatus, (ulong)features.Status) ||
                !WriteBlockRow(state, 4, (ulong)missing, (ulong)rejected, (ulong)failed, none) ||
                !WriteBlockLiteral(state, 2, 1) || !WriteBlockLiteral(state, 2, 2))
                return false;
            state->BlockPhase = 3;
            return true;
        }

        private static bool RunBlockQueueCore(Storage* state, VirtioMappedRegion common)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) ||
                !WriteBlockLiteral(state, 3, 0) || !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            if (allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) != AddressValueStatus.Success ||
                allocation.PageCount != 1 || allocation.ByteLength != 4096 || (page & 4095) != 0 ||
                page < backing.Start || page - backing.Start > backing.PageCount * 4096 - 4096 ||
                DmaAddressWindow.TryCreate(page, page, page, 4096, 32, out var window) != DmaAddressStatus.Success)
                return false;
            // Explicit identity domains for the no-IOMMU QEMU fixture. The original
            // arena is retained WB/RW/NX; the exact allocator receipt stays live.
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            if (PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, 1) != DmaBufferStatus.Success)
                return false;
            VirtioBlkQueueAccess access = new();
            access.Context = state;
            access.Read8 = &ReadVirtioStatus;
            access.Write8 = &WriteVirtioStatus;
            access.Read16 = &ReadBlockWord;
            access.Write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            access.Read32 = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            access.Write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            access.Fence = (delegate* unmanaged<void*, byte>)fence;
            access.BusMasterOff = &BlockBusMasterOff;
            var result = VirtioBlkQueueN0.Run(common, &pool, access, 1024);
            // A failed callback may already have written a queue address. Keep the
            // original backing on any uncertain reset, fence or metadata release.
            if (!result.Succeeded || pool.AllocatedMask != 0)
            {
                _ = WriteBlockRow(state, 13, (ulong)result.Status, (ulong)result.CleanupStatus, result.ResetReads, result.Fences);
                return false;
            }
            var layout = result.Layout;
            if (!WriteBlockRow(state, 5, layout.Range.CpuVirtualAddress, layout.Range.PhysicalAddress, layout.Range.DeviceAddress, layout.Range.ByteLength) ||
                !WriteBlockRow(state, 6, layout.Rings.DescriptorTable.Offset, layout.Rings.AvailableRing.Offset, layout.Rings.UsedRing.Offset, layout.Rings.QueueSize) ||
                !WriteBlockRow(state, 7, VirtioBlkDmaLayout.RequestOffset, VirtioBlkDmaLayout.StatusOffset, VirtioBlkDmaLayout.DataOffset, VirtioBlkDmaLayout.DataBytes) ||
                !WriteBlockLiteral(state, 3, 1) || !WriteBlockLiteral(state, 3, 2))
                return false;
            state->BlockPhase = 4;
            if (!WriteBlockLiteral(state, 4, 0))
                return false;
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            byte status = 0xff;
            var read8 = (delegate* unmanaged<void*, ulong, byte*, byte>)&ReadVirtioStatus;
            return equal && result.Fences == 2 && owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                BlockDisableBusMaster(state) == 1 && read8(null, common.VirtualStart + 20, &status) == 1 && status == 0 &&
                WriteBlockRow(state, 8, 0, result.MaximumSize, result.Size, result.Configured ? 1UL : 0) &&
                WriteBlockRow(state, 9, page + layout.Rings.DescriptorTable.Offset, page + layout.Rings.AvailableRing.Offset, page + layout.Rings.UsedRing.Offset, 0) &&
                WriteBlockRow(state, 10, result.ResetComplete ? 1UL : 0, result.BusMasterDisabled ? 1UL : 0, result.DmaReleased ? 1UL : 0, result.Fences) &&
                WriteBlockRow(state, 11, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1) &&
                WriteBlockRow(state, 12, state->BlockCommand, status, state->BlockMsix.Control, 1) &&
                WriteBlockLiteral(state, 4, 1) && WriteBlockLiteral(state, 4, 2);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadBlockWord(void* context, ulong address, ushort* output)
        {
            *output = *(ushort*)address;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte BlockBusMasterOff(void* context) => BlockDisableBusMaster((Storage*)context);

        private static byte BlockDisableBusMaster(Storage* state)
        {
            if (state->BlockPhase < 1 || state->BlockPhase > 4 || (state->BlockCommand & 0x406) != 0x402 ||
                !state->BlockMsix.IsValid)
                return 0;
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes))
                return 0;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            ulong page = state->PciWindow.Start + 0x30000;
            uint control = 0, command = 0;
            if (read(null, page + state->BlockMsix.CapabilityOffset, &control) != 1 ||
                (control >> 16) != state->BlockMsix.Control || (control & 0x80000000) != 0 ||
                write(null, page + 4, state->BlockCommand) != 1 || read(null, page + 4, &command) != 1 ||
                (command & 0xffff) != state->BlockCommand)
                return 0;
            return 1;
        }

        private static bool WriteBlockRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 13)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x204b4c425f36304dUL;
            uint length = 8;
            text[length++] = (byte)(stage < 10 ? '0' + stage : 'A' + stage - 10);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }

        private static bool WriteBlockLiteral(Storage* state, uint issue, uint kind)
        {
#if M06_BLOCK_MSIX_PROBE
            if (issue < 1 || issue > 19 || kind > 3)
                return false;
#elif M06_PACKAGE_READ_PROBE
            if (issue < 1 || issue > 18 || kind > 3)
                return false;
#else
            if (issue < 1 || issue > 14 || kind > 3)
                return false;
#endif
            ulong* words = stackalloc ulong[4];
            uint bytes, digit;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e36306d205452UL;
                words[2] = 0xa0d3030UL;
                bytes = 20;
                digit = 17;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3030305f36UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
                digit = 12;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e36306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
                digit = 16;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e36306d204cUL;
                words[2] = 0x5f6b636f6c622030UL;
                words[3] = 0xa0d6574617473UL;
                bytes = 31;
                digit = 16;
            }
            ((byte*)words)[digit - 1] = (byte)('0' + issue / 10);
            ((byte*)words)[digit] = (byte)('0' + issue % 10);
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
#endif

#if M06_VIRTIO_BLOCK_READ_PROBE
        private static bool RunOwnedVirtioBlockRead(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->BlockReadPhase != 0 || state->BlockPhase != 5 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->BlockReadPhase = 5;
            if (!RunBlockReadCore(state))
            {
                _ = WriteBlockLiteral(state, state->BlockReadPhase, 3);
                return false;
            }
            state->BlockReadPhase = 7;
            return true;
        }

#if M06_LIVE_BLOCK_CONTRACT_PROBE
        private static bool RunOwnedLiveBlockContract(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->BlockContractPhase != 0 || state->BlockReadPhase != 7 || state->BlockReadCapacity < 2 ||
                state->BlockPhase != 5 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->BlockContractPhase = 7;
            // A fresh negotiation/reset lifetime, reusing the same checked
            // original allocator core. The prerequisite read has completed.
            state->BlockReadPhase = 5;
            if (!RunBlockReadCore(state, true))
            {
                _ = WriteBlockLiteral(state, state->BlockReadPhase + 2, 3);
                return false;
            }
            state->BlockReadPhase = 7;
            state->BlockContractPhase = 9;
            return true;
        }
#endif

#if M06_GPT_PARTITION_PROBE
        private static bool RunOwnedGptPartition(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->GptPhase != 0 || state->BlockContractPhase != 9 || state->BlockReadPhase != 7 ||
                state->BlockReadCapacity < 68 || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->GptPhase = 9;
            // One fixed array outside all read loops. Backup reuses it only
            // after the primary has been rejected; no DMA survives a read.
            byte* headerBytes = stackalloc byte[512];
            byte* entries = stackalloc byte[PrimaryGPTIndependentlyBoundsCRC.ArrayBytes];
            uint reads = 0;
            if (!WriteBlockLiteral(state, 9, 0) ||
                !ReadGptTable(state, false, headerBytes, entries, ref reads, out var primary, out var primaryArray, out uint primaryCrc) ||
                !WriteGptRow(state, 0, (ulong)primary.Status, primary.ExpectedHeaderCrc, primary.ComputedHeaderCrc, primary.ArrayCrc) ||
                !WriteGptRow(state, 1, (ulong)primaryArray, primaryCrc, primary.EntryLba, reads) ||
                !WriteBlockLiteral(state, 9, 1) || !WriteBlockLiteral(state, 9, 2) || !WriteBlockLiteral(state, 10, 0))
                return false;
            LiveGptHeaderResult backup = default;
            LiveGptStatus backupArray = default;
            uint backupCrc = 0;
            if (ExerciseBackupGPTFallbackLiveDisk.NeedsBackup(primary, primaryArray) &&
                !ReadGptTable(state, true, headerBytes, entries, ref reads, out backup, out backupArray, out backupCrc))
                return false;
            var selection = ExerciseBackupGPTFallbackLiveDisk.Select(primary, primaryArray, backup, backupArray);
            if (!selection.Succeeded ||
                !WriteGptRow(state, 2, (ulong)backup.Status, backup.ExpectedHeaderCrc, backup.ComputedHeaderCrc, backupCrc) ||
                !WriteGptRow(state, 3, selection.UsedBackup ? 1UL : 0, (ulong)selection.PrimaryStatus, (ulong)selection.BackupStatus, reads) ||
                !WriteGptRow(state, 4, selection.Header.CurrentLba, selection.Header.EntryLba, selection.Header.FirstUsableLba, selection.Header.LastUsableLba) ||
                !WriteBlockLiteral(state, 10, 1) || !WriteBlockLiteral(state, 10, 2) || !WriteBlockLiteral(state, 11, 0))
                return false;
            var partition = FAT32ApplicationEFIPartition.Select(selection.Header, new System.Span<byte>(entries, PrimaryGPTIndependentlyBoundsCRC.ArrayBytes));
            if (!partition.Succeeded || !WriteGptRow(state, 5, partition.EntryIndex, partition.FirstLba, partition.LastLba, partition.BlockCount) ||
                !WriteGptRow(state, 6, partition.ByteOffset, partition.Attributes, reads, 1) ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena) ||
                !WriteBlockLiteral(state, 11, 1) || !WriteBlockLiteral(state, 11, 2))
                return false;
            state->BlockReadPhase = 7;
            state->GptPhase = 12;
#if M06_FAT_READ_PROBE
            state->FatPartitionFirst = partition.FirstLba;
            state->FatPartitionLast = partition.LastLba;
#endif
            return true;
        }

        private static bool ReadGptTable(Storage* state, bool backup, byte* headerBytes, byte* entries,
            ref uint reads, out LiveGptHeaderResult header, out LiveGptStatus arrayStatus, out uint crc)
        {
            header = default;
            arrayStatus = default;
            crc = 0;
            if (!ReadGptSector(state, backup ? state->BlockReadCapacity - 1 : 1UL, headerBytes, ref reads))
                return false;
            header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(new System.Span<byte>(headerBytes, 512),
                state->BlockReadCapacity, BlockReadResult.Success(1, 512), backup);
            if (!header.Succeeded)
                return true;
            for (uint i = 0; i < PrimaryGPTIndependentlyBoundsCRC.ArraySectors; i++)
                if (!ReadGptSector(state, header.EntryLba + i, entries + i * 512, ref reads))
                    return false;
            arrayStatus = PrimaryGPTIndependentlyBoundsCRC.ValidateArray(header,
                new System.Span<byte>(entries, PrimaryGPTIndependentlyBoundsCRC.ArrayBytes), out crc);
            return true;
        }

        private static bool ReadGptSector(Storage* state, ulong sector, byte* destination, ref uint reads)
        {
            if (reads >= 66)
                return false;
            state->BlockReadPhase = 5;
            if (!RunBlockReadCore(state, false, destination, sector, reads + 4UL))
                return false;
            uint crc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 0; i < 512; i++)
                crc = OS.Std.Hash.Crc32.UpdateByte(crc, destination[i]);
            // Receipt exists only after completion, reset, bus-master disable,
            // DMA release and exact original allocator/canary restoration.
            if (!WriteGptRow(state, 0x80 + reads, sector, OS.Std.Hash.Crc32.Finish(crc), reads + 4UL, 1))
                return false;
            reads++;
            return true;
        }

        private static bool WriteGptRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d, bool fat = false
#if M06_PACKAGE_READ_PROBE
            , bool package = false
#endif
            )
        {
            if (stage > 255)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = fat ? 0x205441465f36304dUL : 0x205450475f36304dUL;
            uint length = 8;
#if M06_PACKAGE_READ_PROBE
            if (package)
                *(ulong*)text = 0x20474b505f36304dUL;
#endif
            for (int shift = 4; shift >= 0; shift -= 4)
            {
                uint digit = (stage >> shift) & 15;
                text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
            }
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

#if M06_FAT_READ_PROBE
        private static bool RunOwnedFatRead(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->FatPhase != 0 || state->GptPhase != 12 || state->BlockContractPhase != 9 || state->BlockReadPhase != 7)
                return false;
            state->FatPhase = 12;
            if (!RunFatReadCore(state))
            {
                _ = WriteBlockLiteral(state, state->FatPhase, 3);
                return false;
            }
            state->FatPhase = 15;
            state->BlockReadPhase = 7;
            return true;
        }

        private static bool RunFatReadCore(Storage* state)
        {
            // Fixed scratch outside every read/walk loop. No metadata or DMA
            // from a prior transport lifetime is reused as device authority.
            byte* sector = stackalloc byte[512];
            byte* mirror = stackalloc byte[512];
            ushort* name = stackalloc ushort[FatDirectory.NameScratchWords];
            uint* rootHistory = stackalloc uint[8];
            uint* fileHistory = stackalloc uint[8];
            uint reads = 0, rootCount = 0;
            LiveFat32Volume volume = default;
            if (!WriteBlockLiteral(state, 12, 0) || !ReadFatSector(state, state->FatPartitionFirst, sector, ref reads) ||
                FatChain.ParseBpb(new System.Span<byte>(sector, 512), state->BlockReadCapacity,
                    state->FatPartitionFirst, state->FatPartitionLast, BlockReadResult.Success(1, 512), ref volume) != LiveFat32Status.Success ||
                volume.SectorsPerCluster != 1 ||
                !WriteFatRow(state, 0, volume.PartitionFirstLba, volume.PartitionLastLba, volume.FirstFatLba, volume.FirstDataLba) ||
                !WriteFatRow(state, 1, volume.SectorsPerCluster, volume.ClusterCount, volume.RootCluster, volume.TotalSectors))
                return false;
            uint cluster = volume.RootCluster;
            while (true)
            {
                if (FatChain.Visit(volume, cluster, new System.Span<uint>(rootHistory, 8), ref rootCount) != LiveFat32Status.Success)
                    return false;
                uint next = 0;
                var status = ReadFatLink(state, volume, cluster, sector, mirror, ref reads, ref next);
                if (status == LiveFat32Status.EndOfChain)
                    break;
                if (status != LiveFat32Status.Success)
                    return false;
                cluster = next;
            }
            if (rootCount != 2 || !WriteFatRow(state, 2, rootCount, rootHistory[0], rootHistory[1], reads) ||
                !WriteBlockLiteral(state, 12, 1) || !WriteBlockLiteral(state, 12, 2))
                return false;
            state->FatPhase = 13;
            if (!WriteBlockLiteral(state, 13, 0))
                return false;
            LiveVfatState sequence = default;
            LiveDirectoryEntry file = default;
            uint found = 0, consumed = 0, nameCrc = 0;
            bool ended = false;
            for (uint i = 0; i < rootCount; i++)
            {
                ulong lba = 0;
                if (ended || FatChain.ClusterFirstLba(volume, rootHistory[i], ref lba) != LiveFat32Status.Success ||
                    !ReadFatSector(state, lba, sector, ref reads))
                    return false;
                for (uint offset = 0; offset < 512; offset += 32)
                {
                    consumed++;
                    var status = FatDirectory.Consume(new System.Span<byte>(sector + offset, 32), ref sequence,
                        new System.Span<ushort>(name, FatDirectory.NameScratchWords), out var entry);
                    if (status == LiveDirectoryStatus.End)
                    {
                        ended = true;
                        break;
                    }
                    if (status == LiveDirectoryStatus.Skipped || status == LiveDirectoryStatus.LongName)
                        continue;
                    if (status != LiveDirectoryStatus.Entry)
                        return false;
                    if (entry.NameLength == 30 && IsFatFixtureName(name))
                    {
                        if (++found != 1 || entry.Attributes != 0x20)
                            return false;
                        file = entry;
                        uint crc = OS.Std.Hash.Crc32.InitialState;
                        for (uint n = 0; n < 60; n++)
                            crc = OS.Std.Hash.Crc32.UpdateByte(crc, ((byte*)name)[n]);
                        nameCrc = OS.Std.Hash.Crc32.Finish(crc);
                    }
                }
            }
            if (!ended || found != 1 || FatDirectory.Finish(sequence) != LiveDirectoryStatus.End ||
                !WriteFatRow(state, 3, rootCount, consumed, found, 1) ||
                !WriteFatRow(state, 4, file.FirstCluster, file.ByteCount, file.NameLength, nameCrc) ||
                !WriteFatRow(state, 5, file.ShortNameLow, file.ShortNameHigh, file.Attributes, reads) ||
                !WriteBlockLiteral(state, 13, 1) || !WriteBlockLiteral(state, 13, 2))
                return false;
            state->FatPhase = 14;
            LiveFileChecksum checksum = default;
            if (!WriteBlockLiteral(state, 14, 0) ||
                FatFile.Begin(file, FatFile.FixtureBytes, FatFile.FixtureCrc32, ref checksum) != LiveFileStatus.Success)
                return false;
            uint fileCount = 0;
            cluster = file.FirstCluster;
            while (true)
            {
                if (cluster == rootHistory[0] || cluster == rootHistory[1] ||
                    FatChain.Visit(volume, cluster, new System.Span<uint>(fileHistory, 8), ref fileCount) != LiveFat32Status.Success)
                    return false;
                ulong lba = 0;
                if (FatChain.ClusterFirstLba(volume, cluster, ref lba) != LiveFat32Status.Success ||
                    !ReadFatSector(state, lba, sector, ref reads) ||
                    FatFile.Append(new System.Span<byte>(sector, 512), BlockReadResult.Success(1, 512), checksum.BytesRead, ref checksum) != LiveFileStatus.Success)
                    return false;
                uint next = 0;
                var status = ReadFatLink(state, volume, cluster, sector, mirror, ref reads, ref next);
                if (status == LiveFat32Status.EndOfChain)
                    break;
                if (status != LiveFat32Status.Success)
                    return false;
                cluster = next;
            }
            uint required = (file.ByteCount + 511) / 512;
            return FatChain.CheckLength(fileCount, required) == LiveFat32Status.Success && fileCount == 3 &&
                FatFile.Finish(ref checksum) == LiveFileStatus.Complete && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                WriteFatRow(state, 6, checksum.BytesRead, OS.Std.Hash.Crc32.Finish(checksum.CrcState), checksum.ExpectedCrc, fileCount) &&
                WriteFatRow(state, 7, fileHistory[0], fileHistory[1], fileHistory[2], reads) &&
                WriteFatRow(state, 8, 1, required, 15, reads + 69UL) &&
                WriteBlockLiteral(state, 14, 1) && WriteBlockLiteral(state, 14, 2);
        }

        private static LiveFat32Status ReadFatLink(Storage* state, LiveFat32Volume volume, uint cluster,
            byte* sector, byte* mirror, ref uint reads, ref uint next)
        {
            ulong lba = volume.FirstFatLba + cluster / 128;
            if (!ReadFatSector(state, lba, sector, ref reads) ||
                !ReadFatSector(state, lba + volume.FatSectors, mirror, ref reads))
                return LiveFat32Status.ReadFailed;
            for (uint i = 0; i < 512; i++)
                if (sector[i] != mirror[i])
                    return LiveFat32Status.InvalidFatBounds;
            return FatChain.ReadFatEntry(volume, cluster, lba, new System.Span<byte>(sector, 512), BlockReadResult.Success(1, 512), ref next);
        }

        private static bool ReadFatSector(Storage* state, ulong sector, byte* destination, ref uint reads)
        {
            if (reads >= 64)
                return false;
            state->BlockReadPhase = 5;
            if (!RunBlockReadCore(state, false, destination, sector, reads + 70UL, true))
                return false;
            uint crc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 0; i < 512; i++)
                crc = OS.Std.Hash.Crc32.UpdateByte(crc, destination[i]);
            if (!WriteFatRow(state, 0x80 + reads, sector, OS.Std.Hash.Crc32.Finish(crc), reads + 70UL, 1))
                return false;
            reads++;
            return true;
        }

        private static bool WriteFatRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d) =>
            WriteGptRow(state, stage, a, b, c, d, true);

        private static bool IsFatFixtureName(ushort* name)
        {
            // Exact UTF-16LE "CLROS Live Storage Fixture.bin"; no strings,
            // allocation, collation or checksum-only filename matching.
            ulong* words = (ulong*)name;
            return words[0] == 0x004f0052004c0043UL && words[1] == 0x0069004c00200053UL &&
                words[2] == 0x0053002000650076UL && words[3] == 0x00610072006f0074UL &&
                words[4] == 0x0046002000650067UL && words[5] == 0x0075007400780069UL &&
                words[6] == 0x0062002e00650072UL && *(uint*)(name + 28) == 0x006e0069;
        }
#endif

#if M06_PACKAGE_READ_PROBE
        private static bool RunOwnedPackageRead(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->PackagePhase != 0 || state->FatPhase != 15 || state->GptPhase != 12 ||
                state->BlockContractPhase != 9 || state->BlockReadPhase != 7)
                return false;
            state->PackagePhase = 15;
            if (!RunPackageReadCore(state))
            {
                _ = WriteBlockLiteral(state, state->PackagePhase, 3);
                return false;
            }
            state->PackagePhase = 19;
            return true;
        }

        private static bool RunPackageReadCore(Storage* state)
        {
            byte* sector = state->PackageSector;
            byte* mirror = state->PackageMirror;
            ushort* name = state->PackageName;
            uint* history = state->PackageHistory;
            uint reads = 0;
            LiveFat32Volume volume = default;
            if (!WriteBlockLiteral(state, 15, 0) ||
                !ReadPackageSector(state, state->FatPartitionFirst, sector, ref reads) ||
                FatChain.ParseBpb(new System.Span<byte>(sector, 512), state->BlockReadCapacity,
                    state->FatPartitionFirst, state->FatPartitionLast, BlockReadResult.Success(1, 512), ref volume) != LiveFat32Status.Success ||
                volume.SectorsPerCluster != 1 ||
                !WritePackageRow(state, 0, volume.PartitionFirstLba, volume.PartitionLastLba, volume.FirstFatLba, volume.FirstDataLba))
                return false;
            // Re-read directory bytes under this profile's own transport epochs.
            // The parent's completed FAT probe is a prerequisite, not a receipt
            // authorizing bytes or a mutable directory entry.
            uint rootCount = 0, osCount = 0, cluster = volume.RootCluster, rootSecond = 0;
            LiveVfatState sequence = default;
            LiveDirectoryEntry os = default;
            bool ended = false;
            while (true)
            {
                if (FatChain.Visit(volume, cluster, new System.Span<uint>(history, 8), ref rootCount) != LiveFat32Status.Success ||
                    rootCount > 2)
                    return false;
                if (rootCount == 2)
                    rootSecond = cluster;
                ulong lba = 0;
                if (ended || FatChain.ClusterFirstLba(volume, cluster, ref lba) != LiveFat32Status.Success ||
                    !ReadPackageSector(state, lba, sector, ref reads))
                    return false;
                for (uint offset = 0; offset < 512; offset += 32)
                {
                    var status = FatDirectory.Consume(new System.Span<byte>(sector + offset, 32), ref sequence,
                        new System.Span<ushort>(name, 260), out var entry);
                    if (status == LiveDirectoryStatus.End)
                    {
                        ended = true;
                        break;
                    }
                    if (status == LiveDirectoryStatus.Skipped || status == LiveDirectoryStatus.LongName)
                        continue;
                    if (status != LiveDirectoryStatus.Entry)
                        return false;
                    if (entry.ShortNameLow == 0x202020202020534fUL && entry.ShortNameHigh == 0x202020)
                    {
                        if (++osCount != 1 || entry.HasLongName || entry.Attributes != 0x10 || entry.ByteCount != 0)
                            return false;
                        os = entry;
                    }
                }
                uint next = 0;
                var link = ReadPackageLink(state, volume, cluster, ref reads, ref next);
                if (link == LiveFat32Status.EndOfChain)
                    break;
                if (link != LiveFat32Status.Success)
                    return false;
                cluster = next;
            }
            if (rootCount != 2 || !ended || osCount != 1 || FatDirectory.Finish(sequence) != LiveDirectoryStatus.End ||
                os.FirstCluster == volume.RootCluster || os.FirstCluster == rootSecond ||
                !WritePackageRow(state, 1, rootCount, volume.RootCluster, rootSecond, os.FirstCluster))
                return false;
            ulong osLba = 0;
            uint osNext = 0;
            if (FatChain.ClusterFirstLba(volume, os.FirstCluster, ref osLba) != LiveFat32Status.Success ||
                ReadPackageLink(state, volume, os.FirstCluster, ref reads, ref osNext) != LiveFat32Status.EndOfChain ||
                !ReadPackageSector(state, osLba, sector, ref reads))
                return false;
            sequence = default;
            ended = false;
            uint manifestCount = 0, packageCount = 0;
            LiveDirectoryEntry manifestFile = default, packageFile = default;
            for (uint offset = 0; offset < 512; offset += 32)
            {
                var status = FatDirectory.Consume(new System.Span<byte>(sector + offset, 32), ref sequence,
                    new System.Span<ushort>(name, 260), out var entry);
                if (status == LiveDirectoryStatus.End)
                {
                    ended = true;
                    break;
                }
                if (status == LiveDirectoryStatus.Skipped || status == LiveDirectoryStatus.LongName)
                    continue;
                if (status != LiveDirectoryStatus.Entry)
                    return false;
                bool manifest = entry.ShortNameLow == 0x202045564954414eUL && entry.ShortNameHigh == 0x4e414d;
                bool package = entry.ShortNameLow == 0x2020202020505041UL && entry.ShortNameHigh == 0x474b50;
                if (!manifest && !package)
                    continue;
                if (entry.HasLongName || entry.Attributes != 0x20 || entry.ByteCount == 0 || entry.ByteCount > 4096 ||
                    entry.FirstCluster == volume.RootCluster || entry.FirstCluster == rootSecond || entry.FirstCluster == os.FirstCluster)
                    return false;
                if (manifest)
                {
                    if (++manifestCount != 1)
                        return false;
                    manifestFile = entry;
                }
                else
                {
                    if (++packageCount != 1)
                        return false;
                    packageFile = entry;
                }
            }
            if (!ended || manifestCount != 1 || packageCount != 1 || manifestFile.FirstCluster == packageFile.FirstCluster ||
                FatDirectory.Finish(sequence) != LiveDirectoryStatus.End ||
                !WritePackageRow(state, 2, os.FirstCluster, manifestFile.FirstCluster, packageFile.FirstCluster, reads))
                return false;
            var disk = ReadPackageFile(state, volume, manifestFile, state->ManifestBytes, packageFile.FirstCluster,
                os.FirstCluster, rootSecond, ref reads);
            if (disk != LiveFat32Status.Success)
                return ReportPackageFault(state, LiveStorageFailureKind.Disk, (byte)disk, reads);
            var manifestResult = NativeManifestDisk.Validate(new System.Span<byte>(state->ManifestBytes, (int)manifestFile.ByteCount));
            if (!manifestResult.Succeeded)
                return ReportPackageFault(state, LiveStorageFailureKind.Manifest, (byte)manifestResult.Status, reads);
            if (!WritePackageRow(state, 3, manifestResult.ByteCount, manifestResult.AppPathOffset, manifestResult.AppPathLength,
                    PackageBytesCrc(state->ManifestBytes, manifestFile.ByteCount)) ||
                !WriteBlockLiteral(state, 15, 1) || !WriteBlockLiteral(state, 15, 2))
                return false;
            state->PackagePhase = 16;
            if (!WriteBlockLiteral(state, 16, 0))
                return false;
            disk = ReadPackageFile(state, volume, packageFile, state->PackageBytes, manifestFile.FirstCluster,
                os.FirstCluster, rootSecond, ref reads);
            if (disk != LiveFat32Status.Success)
                return ReportPackageFault(state, LiveStorageFailureKind.Disk, (byte)disk, reads);
            var packageBytes = new System.Span<byte>(state->PackageBytes, (int)packageFile.ByteCount);
            var selectedPath = new System.Span<byte>(state->ManifestBytes + manifestResult.AppPathOffset, (int)manifestResult.AppPathLength);
            var selected = APPPKGDisk.Validate(packageBytes, selectedPath, M06DiskPackageFixture.PackageCrc32);
            if (!selected.Succeeded)
                return ReportPackageFault(state, LiveStorageFailureKind.Package, (byte)selected.Status, reads);
            if (!WritePackageRow(state, 4, packageFile.ByteCount, selected.ContentOffset, selected.ContentLength, selected.PackageCrc32) ||
                !WriteBlockLiteral(state, 16, 1) || !WriteBlockLiteral(state, 16, 2))
                return false;
            state->PackagePhase = 17;
            if (!WriteBlockLiteral(state, 17, 0))
                return false;
            if (!StagePackagePayload(state, packageBytes, selectedPath))
                return false;
            if (!WritePackageRow(state, 7, reads, reads + 133UL, 17, 1) ||
                !WriteBlockLiteral(state, 17, 1) || !WriteBlockLiteral(state, 17, 2))
                return false;
            state->PackagePhase = 18;
            if (!WriteBlockLiteral(state, 18, 0) || !WritePackageCleanup(state) ||
                !WriteBlockLiteral(state, 18, 1) || !WriteBlockLiteral(state, 18, 2))
                return false;
            return true;
        }

        private static LiveFat32Status ReadPackageFile(Storage* state, LiveFat32Volume volume, LiveDirectoryEntry file,
            byte* destination, uint otherFile, uint osDirectory, uint rootSecond, ref uint reads)
        {
            uint count = 0, written = 0, cluster = file.FirstCluster;
            while (true)
            {
                if (cluster == otherFile || cluster == osDirectory || cluster == volume.RootCluster || cluster == rootSecond)
                    return LiveFat32Status.InvalidCluster;
                var visit = FatChain.Visit(volume, cluster, new System.Span<uint>(state->PackageHistory, 8), ref count);
                if (visit != LiveFat32Status.Success)
                    return visit;
                ulong lba = 0;
                if (FatChain.ClusterFirstLba(volume, cluster, ref lba) != LiveFat32Status.Success ||
                    !ReadPackageSector(state, lba, state->PackageSector, ref reads))
                    return LiveFat32Status.ReadFailed;
                if (OS.Std.Memory.BoundedFileSectorCopy.Copy(new System.Span<byte>(state->PackageSector, 512), 512,
                    file.ByteCount, new System.Span<byte>(destination, 4096), ref written) != OS.Std.Memory.FileSectorCopyStatus.Success)
                    return LiveFat32Status.InvalidLength;
                uint next = 0;
                var link = ReadPackageLink(state, volume, cluster, ref reads, ref next);
                if (link == LiveFat32Status.EndOfChain)
                    return FatChain.CheckLength(count, (file.ByteCount + 511) / 512);
                if (link != LiveFat32Status.Success)
                    return link;
                // This profile admits one-cluster files only. It observes
                // truncation and self-loops without ever reading another file's
                // possible backing cluster or publishing partial file bytes.
                for (uint i = 0; i < count; i++)
                    if (state->PackageHistory[i] == next)
                        return LiveFat32Status.Loop;
                if (written == file.ByteCount)
                    return LiveFat32Status.Overlong;
                return LiveFat32Status.ScratchLimit;
            }
        }

        private static LiveFat32Status ReadPackageLink(Storage* state, LiveFat32Volume volume, uint cluster,
            ref uint reads, ref uint next)
        {
            ulong lba = volume.FirstFatLba + cluster / 128;
            if (!ReadPackageSector(state, lba, state->PackageSector, ref reads) ||
                !ReadPackageSector(state, lba + volume.FatSectors, state->PackageMirror, ref reads))
                return LiveFat32Status.ReadFailed;
            for (uint i = 0; i < 512; i++)
                if (state->PackageSector[i] != state->PackageMirror[i])
                    return LiveFat32Status.InvalidFatBounds;
            return FatChain.ReadFatEntry(volume, cluster, lba, new System.Span<byte>(state->PackageSector, 512),
                BlockReadResult.Success(1, 512), ref next);
        }

        private static bool ReadPackageSector(Storage* state, ulong sector, byte* destination, ref uint reads)
        {
            if (reads >= 64)
                return false;
            state->BlockReadPhase = 5;
            if (!RunBlockReadCore(state, false, destination, sector, reads + 134UL, false, true))
                return false;
            // The shared core returns only after reset, bus-master disable,
            // DMA release and exact allocator restoration; its row mode leaves
            // phase six for the caller to finish.
            state->BlockReadPhase = 7;
            if (!WritePackageRow(state, 0x80 + reads, sector, PackageBytesCrc(destination, 512), reads + 134UL, 1))
                return false;
            reads++;
            return true;
        }

        private static uint PackageBytesCrc(byte* bytes, uint count)
        {
            uint crc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 0; i < count; i++)
                crc = OS.Std.Hash.Crc32.UpdateByte(crc, bytes[i]);
            return OS.Std.Hash.Crc32.Finish(crc);
        }

        private static bool StagePackagePayload(Storage* state, System.Span<byte> packageBytes, System.Span<byte> selectedPath)
        {
            if (!M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            bool valid = allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) == AddressValueStatus.Success &&
                allocation.PageCount == 1 && allocation.ByteLength == 4096 && (page & 4095) == 0 &&
                page >= backing.Start && page - backing.Start <= backing.PageCount * 4096 - 4096;
            SelectedPayloadOwnedMemoryResult staged = default;
            if (valid)
                staged = SelectedPayloadOwnedMemory.Stage(packageBytes, selectedPath, M06DiskPackageFixture.PackageCrc32,
                new System.Span<byte>((void*)page, 4096));
            bool copied = valid && staged.Succeeded && staged.BytesWritten == M06DiskPackageFixture.PayloadBytes &&
                staged.EntryOffset == M06DiskPackageFixture.EntryOffset && staged.PayloadCrc32 == M06DiskPackageFixture.PayloadCrc32;
            if (copied)
            {
                var selected = APPPKGDisk.Validate(packageBytes, selectedPath, M06DiskPackageFixture.PackageCrc32);
                if (!selected.Succeeded)
                    copied = false;
                else
                    for (uint i = 0; i < staged.BytesWritten; i++)
                        if (((byte*)page)[i] != state->PackageBytes[selected.ContentOffset + i])
                            copied = false;
            }
            // Free the successful allocation on every subsequent path, including
            // malformed payloads and unexpected receipt/staging results.
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            return copied && equal && owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                WritePackageRow(state, 5, page, staged.BytesWritten, staged.EntryOffset, staged.PayloadCrc32) &&
                WritePackageRow(state, 6, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1);
        }

        private static bool ReportPackageFault(Storage* state, LiveStorageFailureKind kind, byte code, uint reads)
        {
            var failure = MalformedDiskPackageFailureProbesRemainDiagnosableEBS.Create(kind, code,
                state->BlockReadPhase == 7, FirstExitOwnedArena.ValidateCanaries(state->Arena));
            if (!failure.Succeeded)
                return false;
            uint failedPhase = state->PackagePhase;
            state->PackagePhase = 18;
            if (!WriteBlockLiteral(state, 18, 0) ||
                !WritePackageRow(state, 8, (ulong)failure.Kind, failure.Code, failedPhase, reads) ||
                !WritePackageCleanup(state) || !WriteBlockLiteral(state, 18, 1) || !WriteBlockLiteral(state, 18, 2))
                return false;
            return true;
        }

        private static bool WritePackageCleanup(Storage* state)
        {
            byte status = 0xff;
            ushort command = 0xffff;
            var read8 = (delegate* unmanaged<void*, ulong, byte*, byte>)&ReadVirtioStatus;
            var read16 = (delegate* unmanaged<void*, ulong, ushort*, byte>)&ReadBlockWord;
            // Transport cleanup already disabled bus mastering and reset the
            // device. Audit the completed state through retained PCI/MMIO reads.
            return state->BlockPhase == 5 && state->BlockReadPhase == 7 && state->PackagePhase == 18 &&
                FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                read16(null, state->PciWindow.Start + 0x30004, &command) == 1 &&
                command == state->BlockCommand && (command & 4) == 0 &&
                read8(null, state->BlockCommon.VirtualStart + 20, &status) == 1 && status == 0 &&
                WritePackageRow(state, 9, 1, status, 1, 1);
        }

        private static bool WritePackageRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d) =>
            WriteGptRow(state, stage, a, b, c, d, false, true);
#endif

#if M06_BLOCK_MSIX_PROBE
        private static bool RunOwnedBlockMsix(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->PackagePhase != 19 || state->BlockReadPhase != 7 || state->ApicPhase != 2 ||
                state->InterruptPhase != 1 || !AuditInterruptMappings(state) ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->InterruptPhase = 2;
            state->BlockReadPhase = 5;
            if (!WriteBlockLiteral(state, 19, 0) || !RunBlockReadCore(state, blockMsix: true))
            {
                _ = WriteBlockLiteral(state, 19, 3);
                return false;
            }
            state->BlockReadPhase = 7;
            var r = state->BlockMsixRead;
            var p = state->InterruptProgramming;
            MsixInterruptContext* c = &state->InterruptContext;
            BlockCompletionRoute expected =
#if M06_BLOCK_MSIX_POLLING_RECOVERY_PROBE
                BlockCompletionRoute.PollingRecovery;
#else
                BlockCompletionRoute.Interrupt;
#endif
            if (r.CompletionRoute != expected || !r.Succeeded || !AuditInterruptMappings(state) ||
                !FirstExitOwnedArena.ValidateInstalledDescriptors(ref state->Arena, (ulong)state->Memory.Code) ||
                BoundedXApicSetup.CheckQuiescent(state->ApicTopology.LocalApicAddress, ApicAccess(state)) != XApicSetupStatus.Success)
            {
                _ = WriteBlockLiteral(state, 19, 3);
                return false;
            }
            if (!WriteBlockMsixRow(state, 0, (ulong)r.CompletionRoute, state->BlockMsix.Control, state->InterruptPhase, r.Capacity) ||
                !WriteBlockMsixRow(state, 1, r.Sector, r.BytesRead, r.UsedId, r.UsedLength) ||
                !WriteBlockMsixRow(state, 2, p.MessageAddress, p.MessageData, state->MsixRegions.Table.PhysicalStart, state->MsixRegions.Pba.PhysicalStart) ||
                !WriteBlockMsixRow(state, 3, c->Count, c->SpuriousCount, c->BeforeEoiIsr, c->AfterEoiIsr) ||
                !WriteBlockMsixRow(state, 4, c->InterruptedRip, c->InterruptedFlags, c->EntryFlags, c->ExitFlags) ||
                !WriteBlockMsixRow(state, 5, *(ushort*)c->OriginalIdtr, *(ulong*)(c->OriginalIdtr + 2), *(ushort*)c->RestoredIdtr, *(ulong*)(c->RestoredIdtr + 2)) ||
                !WriteBlockMsixRow(state, 6, c->Iterations, X64MsixInterrupt.MaximumIterations, r.Polls, r.Fences) ||
                !WriteBlockMsixRow(state, 7, r.Queue.ResetComplete ? 1UL : 0, r.Queue.BusMasterDisabled ? 1UL : 0, r.Queue.DmaReleased ? 1UL : 0, r.InterruptStopped ? 1UL : 0) ||
                !WriteBlockMsixRow(state, 8, state->BlockMsixCrc, r.UsedIndex, r.DeviceStatus, r.Queue.ResetReads))
                return false;
            state->InterruptPhase = 7;
            return WriteBlockLiteral(state, 19, 1) && WriteBlockLiteral(state, 19, 2);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte PrepareBlockInterrupt(void* context, VirtioMappedRegion common)
        {
            Storage* state = (Storage*)context;
            if (state->InterruptPhase != 2 || state->BlockReadPhase != 5 || state->ApicPhase != 2 ||
                state->PackagePhase != 19 || common.VirtualStart != state->BlockCommon.VirtualStart ||
                common.PhysicalStart != state->BlockCommon.PhysicalStart || common.Bytes != state->BlockCommon.Bytes ||
                !AuditInterruptMappings(state))
                return 0;
            byte* code = state->Memory.Code + FirstExitCodeBundle.MsixInterruptOffset;
            byte* temporary = TemporaryInterruptIdt(state);
            if (!X64MsixInterrupt.Validate(code, X64MsixInterrupt.Bytes) ||
                !X64MsixInterrupt.PrepareTemporaryIdt(state->Arena.Descriptors->FinalIdt, temporary, 4096, (ulong)state->Memory.Code, (ulong)code) ||
                BoundedXApicSetup.CheckQuiescent(state->ApicTopology.LocalApicAddress, ApicAccess(state)) != XApicSetupStatus.Success)
                return 0;
            state->InterruptContext = default;
            state->InterruptContext.EoiAddress = state->ApicTopology.LocalApicAddress + 0xb0;
            *(ushort*)state->InterruptContext.TemporaryIdtr = 4095;
            *(ulong*)(state->InterruptContext.TemporaryIdtr + 2) = (ulong)temporary;
            state->InterruptProgramming = BoundedMsixProgramming.Prepare(state->MsixCapability, state->MsixRegions,
                state->PciWindow.Start + 0x30000, common, state->ApicTopology.BspApicId, MsixAccess(state));
            if (state->InterruptProgramming.Status != MsixStatus.Success)
                return 0;
            state->InterruptPhase = 3;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte WaitBlockInterrupt(void* context, uint limit)
        {
            Storage* state = (Storage*)context;
            byte* code = state->Memory.Code + FirstExitCodeBundle.MsixInterruptOffset;
            byte* original = state->Arena.Descriptors->FinalIdt;
            byte* temporary = TemporaryInterruptIdt(state);
            if (state->InterruptPhase != 3 || state->BlockReadPhase != 5 || state->ApicPhase != 2 ||
                limit != X64MsixInterrupt.MaximumIterations || !AuditInterruptMappings(state) ||
                !X64MsixInterrupt.Validate(code, X64MsixInterrupt.Bytes) ||
                !X64MsixInterrupt.ValidateTemporaryIdt(original, temporary, 4096, (ulong)state->Memory.Code, (ulong)code))
                return 0;
#if !M06_BLOCK_MSIX_POLLING_RECOVERY_PROBE
            if (BoundedMsixProgramming.SetFunctionMask(state->MsixCapability, state->PciWindow.Start + 0x30000, false, MsixAccess(state)) != MsixStatus.Success)
                return 0;
#endif
            state->InterruptPhase = 4;
            uint count = ((delegate* unmanaged<MsixInterruptContext*, uint, uint>)code)(&state->InterruptContext, limit);
            bool masked = BoundedMsixProgramming.SetFunctionMask(state->MsixCapability,
                state->PciWindow.Start + 0x30000, true, MsixAccess(state)) == MsixStatus.Success;
            var route = PollingCompletionMSIXRemovingPollingRecoveryPath.Select(&state->InterruptContext, (ulong)code,
                (ulong)original, (ulong)temporary, state->ApicTopology.LocalApicAddress, limit, count, masked);
            if (!masked || !FirstExitOwnedArena.ValidateCanaries(state->Arena) || route == BlockCompletionRoute.InvalidWait)
                return 0;
            state->InterruptPhase = 5;
            return (byte)route;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte StopBlockInterrupt(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->InterruptPhase < 2 || state->InterruptPhase > 5 || state->BlockReadPhase != 5 ||
                BoundedMsixProgramming.Disable(state->MsixCapability, state->PciWindow.Start + 0x30000, MsixAccess(state)) != MsixStatus.Success ||
                !FirstExitOwnedArena.ValidateInstalledDescriptors(ref state->Arena, (ulong)state->Memory.Code) ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena) || !AuditInterruptMappings(state) ||
                BoundedXApicSetup.CheckQuiescent(state->ApicTopology.LocalApicAddress, ApicAccess(state)) != XApicSetupStatus.Success)
                return 0;
            state->InterruptPhase = 6;
            return 1;
        }

        private static bool WriteBlockMsixRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 8)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x205152495f36304dUL;
            uint length = 8;
            text[length++] = (byte)('0' + stage);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif
        private static bool RunBlockReadCore(Storage* state, bool liveContract = false,
            byte* gptDestination = null, ulong gptSector = 0, ulong gptEpoch = 0, bool fat = false
#if M06_PACKAGE_READ_PROBE
            , bool package = false
#endif
            , bool blockMsix = false
            )
        {
            bool gpt = gptDestination != null;
#if M06_BLOCK_MSIX_PROBE
            if (blockMsix && (gpt || liveContract || fat || package || state->PackagePhase != 19 ||
                state->ApicPhase != 2 || state->InterruptPhase != 2 || state->BlockReadPhase != 5))
                return false;
#else
            if (blockMsix)
                return false;
#endif
            bool quiet = gpt || blockMsix;
#if M06_PACKAGE_READ_PROBE
            if (package && (!gpt || fat || liveContract || state->GptPhase != 12 || state->FatPhase != 15 ||
                state->PackagePhase < 15 || state->PackagePhase > 16 || state->BlockContractPhase != 9 ||
                state->FatPartitionFirst == 0 || state->FatPartitionFirst > state->FatPartitionLast ||
                state->FatPartitionLast >= state->BlockReadCapacity || gptSector < state->FatPartitionFirst ||
                gptSector > state->FatPartitionLast || gptEpoch < 134 || gptEpoch > 197))
                return false;
#endif
#if M06_FAT_READ_PROBE
            if (fat && (!gpt || liveContract || state->GptPhase != 12 || state->FatPhase < 12 || state->FatPhase > 14 ||
                state->BlockContractPhase != 9 || state->FatPartitionFirst == 0 || state->FatPartitionFirst > state->FatPartitionLast ||
                state->FatPartitionLast >= state->BlockReadCapacity || gptSector < state->FatPartitionFirst ||
                gptSector > state->FatPartitionLast || gptEpoch < 70 || gptEpoch > 133))
                return false;
#else
            if (fat)
                return false;
#endif
#if M06_GPT_PARTITION_PROBE
            if (gpt && !fat
#if M06_PACKAGE_READ_PROBE
                && !package
#endif
                && (liveContract || state->GptPhase != 9 || state->BlockContractPhase != 9 ||
                gptSector >= state->BlockReadCapacity || gptEpoch < 4 || gptEpoch > 69))
                return false;
#else
            if (gpt)
                return false;
#endif
            uint firstIssue = liveContract ? 7u : 5u, secondIssue = firstIssue + 1;
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) ||
                (!quiet && !WriteBlockLiteral(state, firstIssue, 0)) || SetBlockReadBusMaster(state, false) != 1)
                return false;
            var read32 = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            // The prerequisite queue-only proof reset the device. Negotiate a
            // fresh read lifetime; no descriptor or token survives that reset.
            var features = MinimalVirtioBlkFeatureSet.Negotiate(state->BlockCommon, 1024, null,
                read32, write32, &ReadVirtioStatus, &WriteVirtioStatus);
            if (!features.Succeeded || features.Selected != (BoundedVirtioFeatures.Version1 | BoundedVirtioFeatures.BlockReadOnly) ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            if (allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) != AddressValueStatus.Success ||
                allocation.PageCount != 1 || allocation.ByteLength != 4096 || (page & 4095) != 0 ||
                page < backing.Start || page - backing.Start > backing.PageCount * 4096 - 4096 ||
                DmaAddressWindow.TryCreate(page, page, page, 4096, 32, out var window) != DmaAddressStatus.Success)
                return false;
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            if (PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, blockMsix ? 198UL : gpt ? gptEpoch : liveContract ? 3UL : 2UL) != DmaBufferStatus.Success)
                return false;
            VirtioBlkReadAccess access = new();
            access.Queue.Context = state;
            access.Queue.Read8 = &ReadVirtioStatus;
            access.Queue.Write8 = &WriteVirtioStatus;
            access.Queue.Read16 = &ReadBlockWord;
            access.Queue.Write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            access.Queue.Read32 = read32;
            access.Queue.Write32 = write32;
            access.Queue.Fence = (delegate* unmanaged<void*, byte>)fence;
            access.Queue.BusMasterOff = &BlockReadBusMasterOff;
            access.BusMasterOn = &BlockReadBusMasterOn;
#if M06_BLOCK_MSIX_PROBE
            if (blockMsix)
            {
                access.PrepareInterrupt = &PrepareBlockInterrupt;
                access.WaitInterrupt = &WaitBlockInterrupt;
                access.StopInterrupt = &StopBlockInterrupt;
            }
#endif
            byte* localDestination = stackalloc byte[512];
            byte* destination = gpt ? gptDestination : localDestination;
            for (uint i = 0; i < 512; i++)
                destination[i] = 0xa5;
            VirtioBlkReadResult result;
#if M06_LIVE_BLOCK_CONTRACT_PROBE
            BlockDeviceInfo contractInfo = default;
            BlockReadResult contractRead = default;
            Lba0Result lba0 = default;
            ulong spanLength = 0, emptyLength = 0, emptyPointer = 1, spanMatches = 0;
            if (liveContract || gpt)
            {
                var geometry = BlockDeviceInfo.Create(512, state->BlockReadCapacity);
                if (!geometry.Succeeded)
                    return false;
                var device = new LiveDeviceExistingIReadOnlyBlockDeviceContract(geometry.Info,
                    state->BlockCommon, state->BlockNotify, state->BlockDevice, state->BlockNotifyMultiplier,
                    &pool, access, 1000000, 1024);
                var span = new System.Span<byte>(destination, 512);
                contractInfo = LBAN0Guest.Info(ref device);
                contractRead = gpt ? PrimaryGPTIndependentlyBoundsCRC.Read(ref device, gptSector, span) : LBAN0Guest.Read(ref device, span);
                result = device.LastRead;
                if (!contractRead.Succeeded || contractRead.BlocksRead != 1 || contractRead.BytesRead != 512)
                    return false;
                spanLength = (ulong)span.Length;
                fixed (byte* pointer = span)
                    spanMatches = pointer == destination ? 1UL : 0;
                var empty = default(System.Span<byte>);
                emptyLength = (ulong)empty.Length;
                fixed (byte* pointer = empty)
                    emptyPointer = (ulong)pointer;
                var zero = new System.Span<byte>(destination, 0);
                fixed (byte* pointer = zero)
                    if (pointer != null || zero.Length != 0)
                        return false;
            }
            else
#endif
            {
                // The prerequisite LBA1 is opaque transport, not GPT parsing.
                result = SubmitPollSectorRequest.Run(state->BlockCommon, state->BlockNotify, state->BlockDevice,
                    state->BlockNotifyMultiplier, &pool, access, 1, destination, 512, 1000000, 1024);
            }
            if (!result.Succeeded || pool.AllocatedMask != 0)
            {
                _ = WriteBlockReadRow(state, liveContract ? 14u : 10u, (ulong)result.Status, (ulong)result.Queue.Status, (ulong)result.Queue.CleanupStatus, result.Polls, liveContract);
                return false;
            }
#if M06_LIVE_BLOCK_CONTRACT_PROBE
            if (!liveContract && !quiet)
                state->BlockReadCapacity = result.Capacity;
#endif
            uint crc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 0; i < 512; i++)
                crc = OS.Std.Hash.Crc32.UpdateByte(crc, destination[i]);
            crc = OS.Std.Hash.Crc32.Finish(crc);
            var layout = result.Queue.Layout;
            if (!quiet && (!WriteBlockReadRow(state, 0, result.Capacity, result.Sector, result.BytesRead, result.Generation, liveContract) ||
                !WriteBlockReadRow(state, 1, result.CapacityReads, result.NotifyOffset, result.NotifyAddress, result.Polls, liveContract) ||
                !WriteBlockReadRow(state, 2, layout.Range.CpuVirtualAddress, layout.Range.PhysicalAddress, layout.Range.DeviceAddress, layout.Range.ByteLength, liveContract) ||
                !WriteBlockReadRow(state, 3, result.UsedIndex, result.UsedId, result.UsedLength, result.DeviceStatus, liveContract) ||
                !WriteBlockReadRow(state, 4, result.Published ? 1UL : 0, result.Notified ? 1UL : 0, result.Fences, result.Queue.Fences, liveContract) ||
                !WriteBlockReadRow(state, 5, crc, *(ulong*)destination, *(ulong*)(destination + 504), 512, liveContract) ||
                !WriteBlockLiteral(state, firstIssue, 1) || !WriteBlockLiteral(state, firstIssue, 2)))
                return false;
            state->BlockReadPhase = 6;
            if (!quiet && !WriteBlockLiteral(state, secondIssue, 0))
                return false;
#if M06_LIVE_BLOCK_CONTRACT_PROBE
            Lba0Status badSignature = default, badEntry = default, hybrid = default, failedRead = default;
            if (liveContract)
            {
                var sector = new System.Span<byte>(destination, 512);
                lba0 = LBAN0Guest.Validate(sector, contractInfo.BlockCount, contractRead);
                if (!lba0.Succeeded)
                    return false;
                destination[510] ^= 1;
                badSignature = LBAN0Guest.Validate(sector, contractInfo.BlockCount, contractRead).Status;
                destination[510] ^= 1;
                destination[450] ^= 1;
                badEntry = LBAN0Guest.Validate(sector, contractInfo.BlockCount, contractRead).Status;
                destination[450] ^= 1;
                destination[462] = 1;
                hybrid = LBAN0Guest.Validate(sector, contractInfo.BlockCount, contractRead).Status;
                destination[462] = 0;
                failedRead = LBAN0Guest.Validate(sector, contractInfo.BlockCount, default).Status;
                if (!LBAN0Guest.Validate(sector, contractInfo.BlockCount, contractRead).Succeeded)
                    return false;
            }
#endif
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            byte status = 0xff;
            var read8 = (delegate* unmanaged<void*, ulong, byte*, byte>)&ReadVirtioStatus;
            bool cleaned = equal && owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                SetBlockReadBusMaster(state, false) == 1 && read8(null, state->BlockCommon.VirtualStart + 20, &status) == 1 && status == 0;
#if M06_BLOCK_MSIX_PROBE
            if (blockMsix)
            {
                state->BlockMsixRead = result;
                state->BlockMsixCrc = crc;
                return cleaned && result.Capacity == state->BlockReadCapacity && result.Sector == 1 &&
                    result.InterruptAttempted && result.InterruptPrepared && result.InterruptStopped && state->InterruptPhase == 6;
            }
#endif
#if M06_GPT_PARTITION_PROBE
            if (gpt)
                return cleaned && result.Capacity == state->BlockReadCapacity && result.Sector == gptSector &&
                result.BytesRead == 512 && result.UsedIndex == 1 && result.UsedId == 0 && result.UsedLength == 513 &&
                result.DeviceStatus == 0 && result.Queue.ResetComplete && result.Queue.BusMasterDisabled && result.Queue.DmaReleased;
#endif
            cleaned = cleaned && WriteBlockReadRow(state, 6, result.Queue.ResetComplete ? 1UL : 0, result.Queue.BusMasterDisabled ? 1UL : 0,
                    result.Queue.DmaReleased ? 1UL : 0, result.Queue.ResetReads, liveContract) &&
                WriteBlockReadRow(state, 7, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1, liveContract) &&
                WriteBlockReadRow(state, 8, state->BlockCommand, status, state->BlockMsix.Control, 1, liveContract) &&
                WriteBlockReadRow(state, 9,
                    (ulong)UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(0, 0, 513, 0),
                    (ulong)UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 512, 0),
                    (ulong)UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 1, 1),
                    (ulong)UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 513, 0xff), liveContract);
#if M06_LIVE_BLOCK_CONTRACT_PROBE
            if (liveContract)
                cleaned = cleaned && WriteBlockReadRow(state, 10, contractInfo.BlockSize, contractInfo.BlockCount,
                        contractInfo.CapacityBytes, contractRead.BlocksRead, true) &&
                    WriteBlockReadRow(state, 11, (ulong)lba0.Status, lba0.FirstLba, lba0.ProtectedSectors, lba0.Read.BytesRead, true) &&
                    WriteBlockReadRow(state, 12, spanLength, emptyLength, emptyPointer, spanMatches, true) &&
                    WriteBlockReadRow(state, 13, (ulong)badSignature, (ulong)badEntry, (ulong)hybrid, (ulong)failedRead, true);
#endif
            return cleaned && WriteBlockLiteral(state, secondIssue, 1) && WriteBlockLiteral(state, secondIssue, 2);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte BlockReadBusMasterOff(void* context) => SetBlockReadBusMaster((Storage*)context, false);
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte BlockReadBusMasterOn(void* context) => SetBlockReadBusMaster((Storage*)context, true);

        private static byte SetBlockReadBusMaster(Storage* state, bool enabled)
        {
            if (state->BlockPhase != 5 || state->BlockReadPhase < 5 || state->BlockReadPhase > 6 ||
                (state->BlockCommand & 0x406) != 0x402 || !state->BlockMsix.IsValid)
                return 0;
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes))
                return 0;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            ulong page = state->PciWindow.Start + 0x30000;
            uint control = 0, command = 0, expected = enabled ? state->BlockCommand | 4u : state->BlockCommand;
#if M06_BLOCK_MSIX_PROBE
            ushort expectedControl = state->InterruptPhase == 3 || state->InterruptPhase == 5
                ? (ushort)((state->BlockMsix.Control & 0x7ff) | 0xc000) : state->BlockMsix.Control;
#else
            ushort expectedControl = state->BlockMsix.Control;
#endif
            if (read(null, page + state->BlockMsix.CapabilityOffset, &control) != 1 ||
                (control >> 16) != expectedControl ||
                write(null, page + 4, expected) != 1 || read(null, page + 4, &command) != 1 ||
                (command & 0xffff) != expected)
                return 0;
            return 1;
        }

        private static bool WriteBlockReadRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d, bool liveContract = false)
        {
            if (stage > 14)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = liveContract ? 0x564544425f36304dUL : 0x444145525f36304dUL;
            text[8] = 32;
            uint length = 9;
            text[length++] = (byte)(stage < 10 ? '0' + stage : 'A' + stage - 10);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE
#if M07_VIRTIO_NET_PROBE
#if M07_ARP_RECEIVE_PROBE
        private static bool RunOwnedArpReceive(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->ArpPhase != 0 || state->NetPhase != 3 || state->QueuePhase != 20 ||
                state->VirtioPhase != 16 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->ArpPhase = 1;
            if (!RunArpCore(state))
            {
                _ = WriteNetLiteral(state, 9, 3);
                return false;
            }
            state->ArpPhase = 3;
            return true;
        }

        private static bool RunArpCore(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes))
                return false;
            for (uint issue = 6; issue <= 9; issue++)
                if (!WriteNetLiteral(state, issue, 0))
                    return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out var priorCaps) ||
                LiveVirtioNetPCIDevice.Parse((byte*)words, 256, 0, 6, 0, out var binding) != VirtioNetBindingStatus.Success)
                return false;
            var caps = binding.Capabilities;
            ulong start = state->Memory.References.VirtioBarStart;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            for (uint i = 0; i < 6; i++)
                bars[i] = default;
            if (BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(words[8], words[9], 0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success ||
                bars[4].Base != start || bars[4].Bytes != 0x4000 || caps.CommonConfiguration.Offset != priorCaps.CommonConfiguration.Offset ||
                caps.NotifyConfiguration.Offset != priorCaps.NotifyConfiguration.Offset)
                return false;
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], start, out var common) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], caps.NotifyConfiguration.Length,
                    start + 0x3000, out var notify) != VirtioRegionStatus.Success || binding.OriginalCommand != state->QueueCommand ||
                (binding.OriginalCommand & 0x406) != 0x402 || !WriteArpRow(state, 0, binding.Identity, 0x600, binding.OriginalCommand, start))
                return false;
            var negotiated = ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.RunNetReceive(common, 1024, null, read, write32,
                &ReadVirtioStatus, &WriteVirtioStatus);
            if (!negotiated.Succeeded || negotiated.Selected != VirtioNetHeaderChecksumPolicy.AcceptedFeatures ||
                !WriteArpRow(state, 1, negotiated.Offered, negotiated.Selected, negotiated.DeviceStatus, 1) ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            if (allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) != AddressValueStatus.Success ||
                allocation.PageCount != 1 || allocation.ByteLength != 4096 || (page & 4095) != 0 ||
                page < backing.Start || page - backing.Start > backing.PageCount * 4096 - 4096 ||
                DmaAddressWindow.TryCreate(page, page, page, 4096, 32, out var window) != DmaAddressStatus.Success)
                return false;
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            if (PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, 208) != DmaBufferStatus.Success)
                return false;
            Span<byte> received = stackalloc byte[(int)VirtioNetQueue.ReceiveBufferBytes];
            received.Clear();
            Span<ArpNeighborCacheEntry> entries = stackalloc ArpNeighborCacheEntry[2];
            var cache = ArpNeighborCache.Create(entries);
            state->ArpPhase = 2;
            VirtqueuePollingAccess access = new();
            access.Context = state;
            access.Read8 = &ReadVirtioStatus;
            access.Write8 = &WriteVirtioStatus;
            access.Read16 = &ReadQueueWord;
            access.Write16 = write16;
            access.Read32 = read;
            access.Write32 = write32;
            access.Fence = (delegate* unmanaged<void*, byte>)fence;
            access.BusMaster = &QueueBusMaster;
            var result = ARPRequestReplyBoundedNeighborCache.Run(common, notify, caps.NotifyConfiguration.NotifyOffsetMultiplier,
                &pool, access, 1000000, 1024, negotiated.Selected, new MacAddress(2, 0, 0, 0, 0, 1),
                new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), ref cache, 100, received);
            var r = result.Receive.Queue;
            uint length = result.Receive.CapturedBytes;
            // QEMU's user-network ARP reply is 64 zero-padded Ethernet bytes.
            if (!result.Succeeded || pool.AllocatedMask != 0 || length < 54 || length > 76 ||
                cache.TryLookup(new Ipv4Address(10, 0, 2, 2), out var cached) != ArpNeighborCacheStatus.Success ||
                cached.Entry.MacAddress != result.Reply.ResolvedMac || cached.Entry.LastUpdatedTick != 100 || cache.Count != 1)
            {
                ulong failure = (ulong)r.Status | ((ulong)r.CleanupStatus << 8) |
                    ((ulong)result.Receive.Classification.Status << 16) | ((ulong)result.Policy.Status << 24) |
                    ((ulong)result.Request.Status << 32) | ((ulong)result.Reply.Status << 40) | ((ulong)result.Cache.Status << 48);
                byte* header = (byte*)page + VirtioNetQueue.ReceiveBufferOffset;
                _ = WriteArpRow(state, 14, failure, ((ulong)result.Receive.ReceiveUsedLength << 32) | length,
                    pool.AllocatedMask, *(ushort*)header | ((ulong)*(ushort*)(header + 10) << 16));
                return false;
            }
            for (int i = 54; i < length; i++)
                if (received[i] != 0)
                    return false;
            uint receiveCrc = OS.Std.Hash.Crc32.InitialState, transmitCrc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 12; i < length; i++)
                receiveCrc = OS.Std.Hash.Crc32.UpdateByte(receiveCrc, received[(int)i]);
            for (uint i = 0; i < 60; i++)
                transmitCrc = OS.Std.Hash.Crc32.UpdateByte(transmitCrc, ((byte*)page)[VirtioNetQueue.TransmitBufferOffset + 12 + i]);
            receiveCrc = OS.Std.Hash.Crc32.Finish(receiveCrc);
            transmitCrc = OS.Std.Hash.Crc32.Finish(transmitCrc);
            received[0] = 1;
            bool policyControls = VirtioNetHeaderChecksumPolicy.Validate(negotiated.Selected, received).Frame.Status == VirtioNetHeaderStatus.UnsupportedChecksumOffload;
            received[0] = 0;
            received[1] = 1;
            policyControls &= VirtioNetHeaderChecksumPolicy.Validate(negotiated.Selected, received).Frame.Status == VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload;
            received[1] = 0;
            received[10] = 0;
            policyControls &= VirtioNetHeaderChecksumPolicy.Validate(negotiated.Selected, received).Frame.Status == VirtioNetHeaderStatus.UnsupportedReceiveBufferCount;
            received[10] = 1;
            policyControls &= VirtioNetHeaderChecksumPolicy.Validate(0, received).Status == VirtioNetHeaderChecksumPolicyStatus.UnsupportedFeatures;
            var controls = ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTests.Run();
            if (!policyControls || !controls.Succeeded || controls.CacheCount != 2 || controls.RequestBytes != 42)
                return false;
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            uint command = 0;
            if (!equal || r.CpuAddress != page || r.DeviceAddress != page || r.Fences != 8 ||
                read(null, state->PciWindow.Start + 0x30004, &command) != 1 || (command & 0xffff) != state->QueueCommand ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) || !owner->IsCompleted ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            MacAddress mac = cached.Entry.MacAddress;
            ulong peerMac = ((ulong)mac.Octet0 << 40) | ((ulong)mac.Octet1 << 32) | ((ulong)mac.Octet2 << 24) |
                ((ulong)mac.Octet3 << 16) | ((ulong)mac.Octet4 << 8) | mac.Octet5;
            if (!WriteArpRow(state, 2, page, page, page, 4096) ||
                !WriteArpRow(state, 3, r.Receive.Descriptor, r.Receive.Available, r.Receive.Used, page + VirtioNetQueue.ReceiveBufferOffset) ||
                !WriteArpRow(state, 4, r.Transmit.Descriptor, r.Transmit.Available, r.Transmit.Used, page + VirtioNetQueue.TransmitBufferOffset) ||
                !WriteArpRow(state, 5, result.Receive.ReceiveUsedIndex, result.Receive.ReceiveUsedId, length, 1) ||
                !WriteArpRow(state, 6, 12, 1, 0, negotiated.Selected) ||
                !WriteArpRow(state, 7, (ulong)cache.Count, (ulong)cache.Capacity, peerMac, cached.Entry.LastUpdatedTick) ||
                !WriteArpRow(state, 8, receiveCrc, length - 12, transmitCrc, 60) ||
                !WriteArpRow(state, 9, controls.Checks, controls.CacheCount, controls.RequestBytes, 1) ||
                !WriteArpRow(state, 10, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1) ||
                !WriteArpRow(state, 11, r.Polls, r.Fences, r.ResetReads, 3) ||
                !WriteArpRow(state, 12, 1, 1, 1, command & 0xffff) ||
                !WriteArpRow(state, 13, 15, negotiated.Selected, 0x0a000202, 0x0a00020f))
                return false;
            for (uint issue = 6; issue <= 9; issue++)
                if (!WriteNetLiteral(state, issue, 1) || !WriteNetLiteral(state, issue, 2))
                    return false;
            return true;
        }

        private static bool WriteArpRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 14)
                return false; // E is failure-only; strict success admits 0..D.
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x205052415f37304dUL;
            uint length = 8;
            text[length++] = (byte)(stage < 10 ? '0' + stage : 'A' + stage - 10);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif
        private static bool RunOwnedVirtioNet(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->NetPhase != 0 || state->QueuePhase != 20 || state->VirtioPhase != 16 ||
                state->PciPhase != 8 || state->OwnedPhase != 2 || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->NetPhase = 1;
            if (!RunNetCore(state))
            {
                _ = WriteNetLiteral(state, 5, 3);
                return false;
            }
            state->NetPhase = 3;
            return true;
        }

        private static bool RunNetCore(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) || !WriteNetLiteral(state, 1, 0))
                return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out var priorCaps) ||
                LiveVirtioNetPCIDevice.Parse((byte*)words, 256, 0, 6, 0, out var binding) != VirtioNetBindingStatus.Success)
                return false;
            var caps = binding.Capabilities;
            ulong start = state->Memory.References.VirtioBarStart;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            for (uint i = 0; i < 6; i++)
                bars[i] = default;
            if (BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(words[8], words[9], 0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success ||
                bars[4].Base != start || bars[4].Bytes != 0x4000 || caps.CommonConfiguration.Offset != priorCaps.CommonConfiguration.Offset ||
                caps.NotifyConfiguration.Offset != priorCaps.NotifyConfiguration.Offset)
                return false;
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], start, out var common) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], caps.NotifyConfiguration.Length,
                    start + 0x3000, out var notify) != VirtioRegionStatus.Success ||
                binding.OriginalCommand != state->QueueCommand || (binding.OriginalCommand & 0x406) != 0x402 ||
                !WriteNetRow(state, 0, binding.Identity, 0x600, binding.OriginalCommand, start) ||
                !WriteNetLiteral(state, 1, 1) || !WriteNetLiteral(state, 1, 2) || !WriteNetLiteral(state, 2, 0))
                return false;
            var negotiated = MinimalNoOffloadNoGSONoMergeableBuffer.Negotiate(common, 1024, null, read, write32,
                &ReadVirtioStatus, &WriteVirtioStatus);
            if (!negotiated.Succeeded || negotiated.Selected != BoundedVirtioFeatures.Version1 ||
                !WriteNetRow(state, 1, negotiated.Offered, negotiated.Selected, negotiated.DeviceStatus, 1) ||
                !WriteNetLiteral(state, 2, 1) || !WriteNetLiteral(state, 2, 2) ||
                !WriteNetLiteral(state, 3, 0) || !WriteNetLiteral(state, 4, 0) || !WriteNetLiteral(state, 5, 0) ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            if (allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) != AddressValueStatus.Success ||
                allocation.PageCount != 1 || allocation.ByteLength != 4096 || (page & 4095) != 0 ||
                page < backing.Start || page - backing.Start > backing.PageCount * 4096 - 4096 ||
                DmaAddressWindow.TryCreate(page, page, page, 4096, 32, out var window) != DmaAddressStatus.Success)
                return false;
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            if (PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, 207) != DmaBufferStatus.Success)
                return false;
            state->NetPhase = 2;
            VirtqueuePollingAccess access = new();
            access.Context = state;
            access.Read8 = &ReadVirtioStatus;
            access.Write8 = &WriteVirtioStatus;
            access.Read16 = &ReadQueueWord;
            access.Write16 = write16;
            access.Read32 = read;
            access.Write32 = write32;
            access.Fence = (delegate* unmanaged<void*, byte>)fence;
            access.BusMaster = &QueueBusMaster;
            var r = FixedEthernetFrameReclaimDescriptors.Run(common, notify, caps.NotifyConfiguration.NotifyOffsetMultiplier,
                &pool, access, 1000000, 1024);
            if (!r.Succeeded || !r.TransmitCompleted || pool.AllocatedMask != 0)
                return false;
            uint crc = OS.Std.Hash.Crc32.InitialState;
            for (uint i = 0; i < 60; i++)
                crc = OS.Std.Hash.Crc32.UpdateByte(crc, ((byte*)page)[VirtioNetQueue.TransmitBufferOffset + 12 + i]);
            crc = OS.Std.Hash.Crc32.Finish(crc);
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            uint command = 0;
            if (!equal || r.CpuAddress != page || r.DeviceAddress != page || r.Fences != 7 ||
                read(null, state->PciWindow.Start + 0x30004, &command) != 1 || (command & 0xffff) != state->QueueCommand ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) || !owner->IsCompleted ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            return WriteNetRow(state, 2, r.Receive.Index, r.Receive.MaximumSize, r.Receive.Size, r.Receive.NotifyOffset) &&
                WriteNetRow(state, 3, r.Transmit.Index, r.Transmit.MaximumSize, r.Transmit.Size, r.Transmit.NotifyOffset) &&
                WriteNetRow(state, 4, page, page, page, 4096) &&
                WriteNetRow(state, 5, r.Receive.Descriptor, r.Receive.Available, r.Receive.Used, page + VirtioNetQueue.ReceiveBufferOffset) &&
                WriteNetRow(state, 6, r.Transmit.Descriptor, r.Transmit.Available, r.Transmit.Used, page + VirtioNetQueue.TransmitBufferOffset) &&
                WriteNetRow(state, 7, VirtioNetQueue.ReceiveBufferBytes, 2, r.Receive.AvailableIndex, r.Transmit.AvailableIndex) &&
                WriteNetRow(state, 8, r.Receive.Notify, r.Transmit.Notify, caps.NotifyConfiguration.NotifyOffsetMultiplier, VirtioNetQueue.TransmitBufferBytes) &&
                WriteNetRow(state, 9, r.UsedIndex, r.UsedId, r.UsedLength, 1) &&
                WriteNetRow(state, 10, r.Polls, r.Fences, r.ResetReads, 3) &&
                WriteNetRow(state, 11, 1, 1, 1, command & 0xffff) &&
                WriteNetRow(state, 12, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1) &&
                WriteNetRow(state, 13, crc, 60, 0, 12) &&
                WriteNetLiteral(state, 3, 1) && WriteNetLiteral(state, 3, 2) &&
                WriteNetLiteral(state, 4, 1) && WriteNetLiteral(state, 4, 2) &&
                WriteNetLiteral(state, 5, 1) && WriteNetLiteral(state, 5, 2);
        }

        private static bool WriteNetLiteral(Storage* state, uint issue, uint kind)
        {
            if (issue == 0 || issue > 9 || kind > 3)
                return false;
            ulong* words = stackalloc ulong[5];
            uint bytes, digit, phase;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3030UL;
                bytes = 20;
                digit = 17;
                phase = 13;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3030305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
                digit = 12;
                phase = 8;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
                digit = 16;
                phase = 12;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x6f69747269762030UL;
                words[3] = 0x0a0d65746174735fUL;
                bytes = 32;
                digit = 16;
                phase = 12;
            }
            ((byte*)words)[phase] = (byte)'7';
            ((byte*)words)[digit] = (byte)('0' + issue);
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }

        private static bool WriteNetRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 13)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x2054454e5f37304dUL;
            uint length = 8;
            text[length++] = (byte)(stage < 10 ? '0' + stage : 'A' + stage - 10);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif
#if M05_VIRTQUEUE_POLLING_PROBE
        private static bool RunOwnedVirtqueuePolling(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->QueuePhase != 0 || state->VirtioPhase != 16 || state->PciPhase != 8 ||
                state->OwnedPhase != 2 || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->QueuePhase = 16;
            if (!RunQueueCore(state))
            {
                _ = WriteVirtioLiteral(state, state->QueuePhase, 3);
                return false;
            }
            state->QueuePhase = 20;
            return true;
        }
#endif

        private static bool RunQueueCore(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) ||
                !WriteVirtioLiteral(state, 16, 0))
                return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            var read16 = (delegate* unmanaged<void*, ulong, ushort*, byte>)&ReadQueueWord;
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out var caps))
                return false;
            ulong start = state->Memory.References.VirtioBarStart;
            // Negotiation profile already sized this exact BAR and audited all
            // four installed UC/RW/NX pages. Rebind only that retained extent.
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            for (uint i = 0; i < 6; i++)
                bars[i] = default;
            if (BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(words[8], words[9], 0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success ||
                bars[4].Base != start || bars[4].Bytes != 0x4000)
                return false;
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], start, out var common) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], caps.NotifyConfiguration.Length,
                    start + 0x3000, out var notify) != VirtioRegionStatus.Success)
                return false;
            state->QueueCommand = words[1] & 0xffff;
            if ((state->QueueCommand & 0x406) != 0x402)
                return false;
            var negotiated = ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.Run(common, BoundedVirtioFeatures.Version1, 1024,
                null, read, write32, &ReadVirtioStatus, &WriteVirtioStatus);
            ushort maximum = 0;
            if (!negotiated.Succeeded || write16(null, start + 22, 1) != 1 || read16(null, start + 24, &maximum) != 1 ||
                BoundedVirtqueueSize.Validate(8, maximum, out ushort size) != VirtqueueSizeStatus.Success)
                return false;
#if M05_VIRTQUEUE_INTERRUPT_PROBE
            if (!PrepareInterruptTransaction(state, common))
                return false;
#endif
            var invalid = BoundedVirtqueueSize.Validate(3, maximum, out ushort empty);
            if (invalid != VirtqueueSizeStatus.InvalidRequested || empty != 0 ||
                !WriteQueueRow(state, 0, 1, maximum, size, (ulong)invalid) ||
                !WriteVirtioLiteral(state, 16, 1) || !WriteVirtioLiteral(state, 16, 2))
                return false;
            state->QueuePhase = 17;
            if (!WriteVirtioLiteral(state, 17, 0) || !QueueDescriptorControls(state) ||
                !WriteVirtioLiteral(state, 17, 1) || !WriteVirtioLiteral(state, 17, 2))
                return false;
            state->QueuePhase = 18;
            if (!WriteVirtioLiteral(state, 18, 0) || !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            if (allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) != AddressValueStatus.Success ||
                allocation.PageCount != 1 || allocation.ByteLength != 4096 || (page & 4095) != 0 ||
                page < backing.Start || page - backing.Start > backing.PageCount * 4096 - 4096 ||
                DmaAddressWindow.TryCreate(page, page, page, 4096, 32, out var window) != DmaAddressStatus.Success)
                return false;
            // This bounded QEMU fixture has no IOMMU. The original owned arena
            // is retained WB/RW/NX and identity mapped; all domains are explicit.
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            if (PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, 1) != DmaBufferStatus.Success)
                return false;
            VirtqueuePollingAccess access = new();
            access.Context = state;
            access.Read8 = &ReadVirtioStatus;
            access.Write8 = &WriteVirtioStatus;
            access.Read16 = read16;
            access.Write16 = write16;
            access.Read32 = read;
            access.Write32 = write32;
            access.Fence = (delegate* unmanaged<void*, byte>)fence;
            access.BusMaster = &QueueBusMaster;
#if M05_VIRTQUEUE_INTERRUPT_PROBE
            var result = SplitVirtqueuePollingNotificationCompletion.RunInterrupt(common, notify, caps.NotifyConfiguration.NotifyOffsetMultiplier,
                &pool, access, X64MsixInterrupt.MaximumIterations, 1024,
                new VirtqueueInterruptAccess { Context = state, Wait = &WaitQueueInterrupt, Mask = &MaskQueueInterrupt });
            state->InterruptQueue = result;
#else
            var result = SplitVirtqueuePollingNotificationCompletion.Run(common, notify, caps.NotifyConfiguration.NotifyOffsetMultiplier,
                &pool, access, 1000000, 1024);
#endif
            // Preserve allocator ownership on any uncertain device lifetime.
            if (!result.Succeeded || pool.AllocatedMask != 0)
            {
                _ = WriteQueueRow(state, 8, (ulong)result.Status, (ulong)result.CleanupStatus, result.UsedIndex, result.Polls);
                return false;
            }
#if M05_VIRTQUEUE_INTERRUPT_PROBE
            if (state->InterruptPhase != 6 || !result.InterruptObserved || !result.InterruptsMasked ||
                BoundedMsixProgramming.Disable(state->MsixCapability, state->PciWindow.Start + 0x30000, MsixAccess(state)) != MsixStatus.Success)
                return false;
            state->InterruptPhase = 7;
#endif
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            if (!equal || result.MaximumSize != maximum || result.Size != size ||
                !WriteQueueRow(state, 2, page, page, page, 4096) ||
                !WriteQueueRow(state, 3, result.NotifyAddress, result.NotifyOffset, caps.NotifyConfiguration.NotifyOffsetMultiplier, 72) ||
                !WriteQueueRow(state, 4, result.AvailableIndex, result.UsedIndex, result.UsedId, result.UsedLength) ||
                !WriteQueueRow(state, 5, result.Polls, result.Fences, 1, 1) ||
                !WriteVirtioLiteral(state, 18, 1) || !WriteVirtioLiteral(state, 18, 2))
                return false;
            state->QueuePhase = 19;
            uint command = 0;
            return WriteVirtioLiteral(state, 19, 0) &&
                X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes) &&
                read(null, state->PciWindow.Start + 0x30004, &command) == 1 && (command & 0xffff) == state->QueueCommand &&
                result.Fences == 5 && owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                WriteQueueRow(state, 6, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1) &&
                WriteQueueRow(state, 7, result.Fences, 1, 1, command & 0xffff) &&
                WriteVirtioLiteral(state, 19, 1) && WriteVirtioLiteral(state, 19, 2);
        }

        private static bool QueueDescriptorControls(Storage* state)
        {
            VirtqueueDescriptorPoolState pool = default;
            VirtqueueDescriptorSlot* slots = stackalloc VirtqueueDescriptorSlot[2];
            slots[0] = default;
            slots[1] = default;
            if (VirtqueueDescriptors.TryInitialize(&pool, slots, 2, 1) != VirtqueueDescriptorStatus.Success ||
                VirtqueueDescriptors.TryAllocate(&pool, out var first) != VirtqueueDescriptorStatus.Success ||
                VirtqueueDescriptors.TryAllocate(&pool, out var second) != VirtqueueDescriptorStatus.Success)
                return false;
            var exhausted = VirtqueueDescriptors.TryAllocate(&pool, out _);
            if (VirtqueueDescriptors.TryRelease(&pool, first) != VirtqueueDescriptorStatus.Success)
                return false;
            var twice = VirtqueueDescriptors.TryRelease(&pool, first);
            return first.Index == 0 && second.Index == 1 && exhausted == VirtqueueDescriptorStatus.Exhausted &&
                twice == VirtqueueDescriptorStatus.StaleToken &&
                VirtqueueDescriptors.TryRelease(&pool, second) == VirtqueueDescriptorStatus.Success && pool.AllocatedCount == 0 &&
                WriteQueueRow(state, 1, first.Index, second.Index, (ulong)exhausted, (ulong)twice);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadQueueWord(void* context, ulong address, ushort* output)
        {
            *output = *(ushort*)address;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte QueueBusMaster(void* context, byte enable)
        {
            Storage* state = (Storage*)context;
            bool active = state->QueuePhase == 18;
#if M07_VIRTIO_NET_PROBE
            active |= state->QueuePhase == 20 && state->NetPhase == 2;
#if M07_ARP_RECEIVE_PROBE
            active |= state->QueuePhase == 20 && state->ArpPhase == 2;
#if M07_026_PROBE
            active |= state->QueuePhase == 20 && state->NetworkPhase == 2;
#endif
#endif
#endif
            if (enable > 1 || !active || (state->QueueCommand & 0x406) != 0x402)
                return 0;
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            ulong address = state->PciWindow.Start + 0x30004;
            uint current = 0, wanted = state->QueueCommand | (enable == 1 ? 4u : 0u);
            if (read(null, address, &current) != 1 || ((current & 0xffff) != state->QueueCommand &&
                (current & 0xffff) != (state->QueueCommand | 4)))
                return 0;
            return write(null, address, wanted) == 1 && read(null, address, &current) == 1 &&
                (current & 0xffff) == wanted ? (byte)1 : (byte)0;
        }

        private static bool WriteQueueRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 8)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x554555515f35304dUL;
            *(ushort*)(text + 8) = 0x2045;
            uint length = 10;
            text[length++] = (byte)('0' + stage);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

#if M05_BAR_DMA_PROBE
        private static bool RunOwnedBarDma(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->BarDmaPhase != 0 || state->PciPhase != 8 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->BarDmaPhase = 8;
            if (!RunBarCore(state) || !RunDmaCore(state))
            {
                _ = WriteBarLiteral(state, state->BarDmaPhase, 3);
                return false;
            }
            state->BarDmaPhase = 12;
            return true;
        }

        private static bool RunBarCore(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                state->PciWindow.FirstBus != 0 || state->PciWindow.LastBus != 0 ||
                state->PciWindow.Start != state->Memory.References.EcamStart || state->Memory.References.EcamPages != 256 ||
                !WriteBarLiteral(state, 8, 0))
                return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            ulong page = state->PciWindow.Start + 0x30000;
            uint identity = 0, command = 0, after = 0;
            // The runner installs exactly this non-DMA test function. Never probe
            // an arbitrary firmware-owned storage/network function for this profile.
            if (read(null, page, &identity) != 1 || read(null, page + 4, &command) != 1 ||
                !WriteBarRow(state, 0, 0x600, identity, command & 0xffff, page) ||
                identity != 0x00051b36)
                return false;
            // OVMF may enable bus mastering even on this synthetic non-DMA device.
            // Take ownership only here: clear that bit with a WORD write and prove
            // readback before entering the general probe, which still rejects it.
            // This terminal diagnostic never re-enables bus mastering.
            command &= 0xfffb;
            if (write16(null, page + 4, command) != 1 || read(null, page + 4, &after) != 1 ||
                (after & 0xffff) != command)
                return false;
            PciSizedBar* first = stackalloc PciSizedBar[6];
            PciSizedBar* repeat = stackalloc PciSizedBar[6];
            var result = N32BitN64BitBARsWhileAlwaysRestoring.Probe(state->PciWindow, 0, 6, 0, null, read, write32, write16, first, 6);
            if (!result.Succeeded)
            {
                _ = WriteBarRow(state, 13, 1, (ulong)result.Status, (ulong)result.OperationStatus, result.Restored ? 1UL : 0);
                return false;
            }
            var again = N32BitN64BitBARsWhileAlwaysRestoring.Probe(state->PciWindow, 0, 6, 0, null, read, write32, write16, repeat, 6);
            if (!again.Succeeded)
            {
                _ = WriteBarRow(state, 13, 2, (ulong)again.Status, (ulong)again.OperationStatus, again.Restored ? 1UL : 0);
                return false;
            }
            if (read(null, page + 4, &after) != 1 || (command & 0xffff) != (after & 0xffff))
                return false;
            for (uint i = 0; i < 6; i++)
                if (first[i].Kind != repeat[i].Kind || first[i].Base != repeat[i].Base ||
                    first[i].Bytes != repeat[i].Bytes || first[i].Prefetchable != repeat[i].Prefetchable)
                    return false;
            if (first[0].Kind != PciSizedBarKind.Memory32 || first[0].Bytes != 4096 || !first[0].IsAssigned || first[0].Prefetchable ||
                first[2].Kind != PciSizedBarKind.Memory64 || first[2].Bytes != 0x200000 || !first[2].IsAssigned || !first[2].Prefetchable ||
                first[3].Kind != PciSizedBarKind.UpperHalf ||
                !WriteBarRow(state, 1, first[0].Base, first[0].Bytes, (ulong)first[0].Kind, 0) ||
                !WriteBarRow(state, 2, first[2].Base, first[2].Bytes, (ulong)first[2].Kind, 1) ||
                !WriteBarRow(state, 3, command & 0xffff, after & 0xffff, result.Restored ? 1UL : 0, again.Restored ? 1UL : 0) ||
                !WriteBarLiteral(state, 8, 1) || !WriteBarLiteral(state, 8, 2))
                return false;
            state->BarDmaPhase = 9;
            var badType = BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(2, 0, 2, 0, 0, out _);
            var badPair = BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(4, 1, 0xfffff004, 0xffffffff, 5, out _);
            var badMask = BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0x80000008, 0, 0xfffff000, 0, 0, out _);
            var badExtent = BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0x80000010, 0, 0xfffff000, 0, 0, out _);
            return badType == PciBarProbeStatus.UnsupportedType && badPair == PciBarProbeStatus.UnsupportedType &&
                badMask == PciBarProbeStatus.InvalidMask && badExtent == PciBarProbeStatus.InvalidExtent &&
                WriteBarLiteral(state, 9, 0) && WriteBarRow(state, 4, 0, 1, (ulong)first[0].Kind, (ulong)first[2].Kind) &&
                WriteBarRow(state, 5, (ulong)badType, (ulong)badPair, (ulong)badMask, (ulong)badExtent) &&
                WriteBarLiteral(state, 9, 1) && WriteBarLiteral(state, 9, 2);
        }

        private static bool RunDmaCore(Storage* state)
        {
            state->BarDmaPhase = 10;
            if (!WriteBarLiteral(state, 10, 0) || !FirstExitOwnedArena.ValidateCanaries(state->Arena) ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount || backing.PageCount == 0 ||
                backing.PageCount > M01LiveAllocatorIntegration.MaximumArenaPageCount)
                return false;
            M01LiveAllocatorStaticState* owner = (M01LiveAllocatorStaticState*)address;
            if (!owner->IsCompleted)
                return false;
            var before = owner->AllocatorStateValue;
            var node = owner->FreeNodeValue;
            var allocation = BootPageAllocatorMutator.AllocateOnePage(before, &owner->FreeNodeValue, 1, 1, null, 0, 0, 1, 4096);
            if (!allocation.Succeeded)
                return false;
            owner->AllocatorStateValue = allocation.AfterState;
            bool admitted = allocation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong page) == AddressValueStatus.Success &&
                allocation.PhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong end) == AddressValueStatus.Success &&
                allocation.PageCount == 1 && allocation.ByteLength == 4096 && (page & 4095) == 0 &&
                page >= backing.Start && page - backing.Start <= backing.PageCount * 4096 - 4096 && end == page + 4096;
            // This declared x64 QEMU adapter has identity CPU/physical mapping and
            // no IOMMU. Device address correspondence is explicit; no device uses it.
            bool exercised = admitted && RunDmaPage(state, page, before.FreePageCount, owner->AllocatorStateValue.FreePageCount);
            // No early return after allocation: return the exact receipt even if
            // address admission, a contract check, or serial reporting failed.
            var freed = BootPageAllocatorMutator.FreeOnePage(owner->AllocatorStateValue, &owner->FreeNodeValue, 1, 1, null, 0, 0, allocation);
            if (!freed.Succeeded)
                return false;
            owner->AllocatorStateValue = freed.AfterState;
            var restored = owner->AllocatorStateValue;
            var finalNode = owner->FreeNodeValue;
            bool equal = before.IsInitialized == restored.IsInitialized && before.PageSize == restored.PageSize &&
                before.HeadNodeIndex == restored.HeadNodeIndex && before.FreeNodeCount == restored.FreeNodeCount &&
                before.FreePageCount == restored.FreePageCount && before.FreeByteCount == restored.FreeByteCount &&
                before.AllocatedPageCount == restored.AllocatedPageCount && before.AllocatedByteCount == restored.AllocatedByteCount &&
                node.SourceFreeSegmentIndex == finalNode.SourceFreeSegmentIndex && node.SourceUsableRangeIndex == finalNode.SourceUsableRangeIndex &&
                node.PhysicalStart == finalNode.PhysicalStart && node.PhysicalEndExclusive == finalNode.PhysicalEndExclusive &&
                node.PageCount == finalNode.PageCount && node.ByteLength == finalNode.ByteLength && node.NextNodeIndex == finalNode.NextNodeIndex;
            return exercised && equal && owner->IsCompleted && FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                WriteBarRow(state, 12, before.FreePageCount, restored.FreePageCount, restored.AllocatedPageCount, 1) &&
                WriteBarLiteral(state, 11, 1) && WriteBarLiteral(state, 11, 2);
        }

        private static bool RunDmaPage(Storage* state, ulong page, ulong freeBefore, ulong freeDuring)
        {
            if (DmaAddressWindow.TryCreate(page, page, page, 4096, 32, out var window) != DmaAddressStatus.Success ||
                window.TrySlice(0, 4096, out var range) != DmaAddressStatus.Success ||
                DmaAddressWindow.TryCreate(0x100000, 0x200000, 0x300000, 8192, 32, out var distinct) != DmaAddressStatus.Success ||
                distinct.TrySlice(4096, 4096, out var slice) != DmaAddressStatus.Success)
                return false;
            var empty = default(DmaAddressWindow).TrySlice(0, 4096, out _);
            var width = DmaAddressWindow.TryCreate(page, page, 0x100000000, 4096, 32, out _);
            var invalid = window.TrySlice(1, 4096, out _);
            if (empty != DmaAddressStatus.Uninitialized || width != DmaAddressStatus.DeviceAddressLimit || invalid != DmaAddressStatus.InvalidAlignment ||
                !WriteBarRow(state, 6, range.CpuVirtualAddress, range.PhysicalAddress, range.DeviceAddress, range.ByteLength) ||
                !WriteBarRow(state, 7, slice.CpuVirtualAddress, slice.PhysicalAddress, slice.DeviceAddress, slice.ByteLength) ||
                !WriteBarRow(state, 8, (ulong)empty, (ulong)width, (ulong)invalid, 0) ||
                !WriteBarLiteral(state, 10, 1) || !WriteBarLiteral(state, 10, 2))
                return false;
            state->BarDmaPhase = 11;
            if (!WriteBarLiteral(state, 11, 0))
                return false;
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            if (PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, 1) != DmaBufferStatus.Success ||
                PageAlignedDmaBuffers.TryAllocate(&pool, 1, out var first) != DmaBufferStatus.Success ||
                first.Range.CpuVirtualAddress != page || first.Range.PhysicalAddress != page || first.Range.DeviceAddress != page || first.Range.ByteLength != 4096)
                return false;
            var exhausted = PageAlignedDmaBuffers.TryAllocate(&pool, 1, out _);
            ulong* firstWord = (ulong*)page;
            ulong* lastWord = (ulong*)(page + 4088);
            ulong savedFirst = ReadDmaWord(firstWord), savedLast = ReadDmaWord(lastWord);
            ulong pattern = page ^ 0xe05ab41c7298d603UL;
            WriteDmaWord(firstWord, pattern);
            WriteDmaWord(lastWord, ~pattern);
            bool written = ReadDmaWord(firstWord) == pattern && ReadDmaWord(lastWord) == ~pattern;
            WriteDmaWord(firstWord, savedFirst);
            WriteDmaWord(lastWord, savedLast);
            bool bytesRestored = ReadDmaWord(firstWord) == savedFirst && ReadDmaWord(lastWord) == savedLast;
            var gave = PageAlignedDmaBuffers.TryGiveToDevice(&pool, first.Token);
            var busy = PageAlignedDmaBuffers.TryRelease(&pool, first.Token);
            var returned = PageAlignedDmaBuffers.TryReturnToCpu(&pool, first.Token);
            var released = PageAlignedDmaBuffers.TryRelease(&pool, first.Token);
            var reused = PageAlignedDmaBuffers.TryAllocate(&pool, 1, out var second);
            var stale = PageAlignedDmaBuffers.TryRelease(&pool, first.Token);
            var secondRelease = PageAlignedDmaBuffers.TryRelease(&pool, second.Token);
            var twice = PageAlignedDmaBuffers.TryRelease(&pool, second.Token);
            return written && bytesRestored && exhausted == DmaBufferStatus.Exhausted && gave == DmaBufferStatus.Success &&
                busy == DmaBufferStatus.InvalidOwnership && returned == DmaBufferStatus.Success && released == DmaBufferStatus.Success &&
                reused == DmaBufferStatus.Success && second.Range.CpuVirtualAddress == page && stale == DmaBufferStatus.StaleToken &&
                secondRelease == DmaBufferStatus.Success && twice == DmaBufferStatus.StaleToken && pool.AllocatedMask == 0 &&
                WriteBarRow(state, 9, page, freeBefore, freeDuring, 1) &&
                WriteBarRow(state, 10, 1, (ulong)gave, (ulong)busy, (ulong)returned) &&
                WriteBarRow(state, 11, (ulong)stale, (ulong)twice, (ulong)exhausted, 1);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static ulong ReadDmaWord(ulong* address) => *address;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WriteDmaWord(ulong* address, ulong value) => *address = value;

        private static bool WriteBarRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 13)
                return false;
            byte* text = stackalloc byte[96];
            *(ulong*)text = 0x5f5241425f35304dUL;
            *(uint*)(text + 8) = 0x20414d44;
            uint length = 12;
            if (stage >= 10)
                text[length++] = (byte)'1';
            text[length++] = (byte)('0' + stage % 10);
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }

        private static bool WriteBarLiteral(Storage* state, uint issue, uint kind)
        {
            if (issue < 8 || issue > 11 || kind > 3)
                return false;
            ulong* words = stackalloc ulong[5];
            uint bytes, digit;
            if (kind == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3030UL;
                bytes = 20;
                digit = 17;
            }
            else if (kind == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3030305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
                digit = 12;
            }
            else if (kind == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
                digit = 16;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x6d645f7261622030UL;
                words[3] = 0x0d65746174735f61UL;
                words[4] = 10;
                bytes = 33;
                digit = 16;
            }
            ((byte*)words)[digit - 1] = (byte)('0' + issue / 10);
            ((byte*)words)[digit] = (byte)('0' + issue % 10);
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
#endif

#if M16_003_PROBE
        private static bool RunOwnedWorkloadServiceLoop(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->ServicePhase != 0 || state->LinuxOwnedPhase != 2 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed ||
                state->Arena.Transition->Mode != FirmwareServicesMode.BootServicesExited ||
                !state->Arena.Transition->RetainedFirmwareCallbacksCleared ||
                !state->Arena.Binding->RetainedPointersCleared || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            state->ServicePhase = 1;
            // Provider pointers are unnecessary once the original arena epoch has
            // revoked these copied handles. Keep only first-party denied thunks.
            state->AdapterConsole = null;
            state->AdapterServices = null;
            if (!LinuxText(state, "TEST_START m16-003\r\n") ||
                !StartOwnedHpet(state, out OwnedHpetAccess hpet, out OwnedTscAccess tsc) ||
                state->Clock.PeriodFemtoseconds == 0 ||
                1000000000000000UL % state->Clock.PeriodFemtoseconds != 0)
            {
                _ = LinuxText(state, "TEST_FAIL m16-003 clock_admission\r\n");
                return false;
            }
            if (!LinuxText(state, "M16_003_CLOCK frequency=") || !LinuxNumber(state, state->Clock.FrequencyHz) ||
                !LinuxText(state, " period=") || !LinuxNumber(state, state->Clock.PeriodFemtoseconds) ||
                !LinuxText(state, " recoveries=") || !LinuxNumber(state, state->Clock.RecoveryAttempts) ||
                !LinuxText(state, " source=owned-hpet\r\n"))
                return false;
            var request = new WorkloadServiceLoopRequest
            {
                FrequencyHz = state->Clock.FrequencyHz,
                IntervalMilliseconds = 1,
                MinimumIterations = 3,
                MaximumIterations = 4,
                PollLimit = 4096,
                FirmwareExited = true,
                RetainedPointersCleared = true,
                OwnedMappingsValidated = true
            };
            var callbacks = new WorkloadServiceLoopCallbacks
            {
                Context = state,
                ReadClock = &ServiceReadClock,
                Service = &ServiceIteration,
                Stop = &ServiceStop
            };
            if (WorkloadServiceLoop.Run(ref state->ServiceLoop, request, callbacks) != WorkloadServiceStatus.Stopped ||
                state->ServiceLoop.Iterations != 4 || state->ServiceLoop.LastTicks <= state->ServiceLoop.FirstTicks ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
            {
                _ = LinuxText(state, "TEST_FAIL m16-003 service_state status=") &&
                    LinuxNumber(state, (uint)state->ServiceLoop.Status) &&
                    LinuxText(state, " iterations=") && LinuxNumber(state, state->ServiceLoop.Iterations) &&
                    LinuxText(state, " reads=") && LinuxNumber(state, state->ServiceLoop.ClockReads) &&
                    LinuxText(state, " first=") && LinuxNumber(state, state->ServiceLoop.FirstTicks) &&
                    LinuxText(state, " last=") && LinuxNumber(state, state->ServiceLoop.LastTicks) &&
                    LinuxText(state, " clockStatus=") && LinuxNumber(state, (uint)state->Clock.Status) &&
                    LinuxText(state, " recoveries=") && LinuxNumber(state, state->Clock.RecoveryAttempts) &&
                    LinuxText(state, "\r\n");
                return false;
            }
            state->ServicePhase = 2;
            return LinuxText(state, "M16_003_STOP iterations=4 reads=") && LinuxNumber(state, state->ServiceLoop.ClockReads) &&
                LinuxText(state, " first=") && LinuxNumber(state, state->ServiceLoop.FirstTicks) &&
                LinuxText(state, " last=") && LinuxNumber(state, state->ServiceLoop.LastTicks) &&
                LinuxText(state, " interval=") && LinuxNumber(state, state->ServiceLoop.IntervalTicks) &&
                LinuxText(state, " firmware=revoked providers=1 shutdown=owned-controller\r\nCLROS_M16_003_READY\r\nTEST_PASS m16-003\r\n");
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ServiceReadClock(void* context, ulong* ticks)
        {
            Storage* state = (Storage*)context;
            if (state->ServicePhase != 1 || ticks == null || state->AdapterConsole != null || state->AdapterServices != null ||
                !OwnedMonotonicClock.Read(ref state->Clock) || state->Clock.Source != OwnedClockSource.Hpet ||
                state->Clock.RecoveryAttempts > 1 || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return 0;
            *ticks = state->Clock.Ticks;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ServiceIteration(void* context, WorkloadServiceLoopState* original, uint iteration, ulong ticks)
        {
            Storage* state = (Storage*)context;
            if (state->ServicePhase != 1 || original != &state->ServiceLoop || iteration == 0 || iteration > 4 ||
                ticks != state->Clock.Ticks || !LinuxText(state, "M16_003_ITERATION index=") ||
                !LinuxNumber(state, iteration) || !LinuxText(state, " ticks=") || !LinuxNumber(state, ticks) ||
                !LinuxText(state, "\r\n"))
                return 0;
            if (iteration == 4 && !WorkloadServiceLoop.RequestShutdown(ref *original))
                return 0;
            return 1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ServiceStop(void* context, uint iterations, ulong ticks)
        {
            Storage* state = (Storage*)context;
            if (state->ServicePhase != 1 || iterations != 4 || ticks != state->Clock.Ticks ||
                state->AdapterConsole != null || state->AdapterServices != null)
                return 0;
            for (uint index = 0; index < 8; index++)
                if (!DenyCopiedFirmwareAdapter(state, index) || !LinuxText(state, "M16_003_FIRMWARE_DENIED index=") ||
                    !LinuxNumber(state, index) || !LinuxText(state, " status=ServicesUnavailable providers=1 outputs=unchanged\r\n"))
                    return 0;
            return 1;
        }
#endif

#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
#if M05_MONOTONIC_CLOCK_PROBE
        private static bool RunOwnedMonotonicClock(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->ClockPhase != 0 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed)
                return false;
            state->ClockPhase = 1;
            if (!WriteClockLiteral(state, 0))
                return false;
            if (!RunClockCore(state))
            {
                _ = WriteClockLiteral(state, 3);
                return false;
            }
            state->ClockPhase = 2;
            return WriteClockLiteral(state, 1) && WriteClockLiteral(state, 2);
        }

        private static bool RunClockCore(Storage* state)
        {
            if (!StartOwnedHpet(state, out OwnedHpetAccess hpet, out OwnedTscAccess tsc) ||
                !ReadClockProgress(state, out ulong first) || !ReadClockProgress(state, out ulong second))
                return false;
            ulong capabilities = hpet.Read(hpet.Context, 0);
            bool tscAvailable = OwnedMonotonicClock.TryTscFrequency(tsc, out ulong tscFrequency) && tsc.Read != null;
            if (!tscAvailable)
                tscFrequency = 0;
            var invalidPeriod = MonotonicHpetTscClockMath.DecodeHpetCapabilities(0);
            var ambiguous = MonotonicHpetTscClockMath.Accumulate(0, 0x80000000, 32, 0);
            bool invalidTscRefused = !OwnedMonotonicClock.TryTscFrequency(default, out ulong invalidFrequency) && invalidFrequency == 0;
            if (invalidPeriod.Status != MonotonicClockMathStatus.InvalidPeriod ||
                ambiguous.Status != MonotonicClockMathStatus.AmbiguousDelta || !invalidTscRefused ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            return WriteClockRow(state, 0, state->HpetDescription.Address, capabilities, state->Clock.PeriodFemtoseconds, state->Clock.CounterWidth) &&
                WriteClockRow(state, 1, (ulong)state->Clock.Source, state->Clock.FrequencyHz, state->Clock.RecoveryAttempts, (ulong)state->Clock.HpetStatus) &&
                WriteClockRow(state, 2, first, second, tscAvailable ? 1UL : 0UL, tscFrequency) &&
                WriteClockRow(state, 3, (ulong)invalidPeriod.Status, (ulong)ambiguous.Status, 1, 1);
        }
#endif

        private static bool StartOwnedHpet(Storage* state, out OwnedHpetAccess hpet, out OwnedTscAccess tsc)
        {
            hpet = default;
            tsc = default;
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (!AdmitOwnedAcpiState(state) || workspace->Phase != 3 ||
                    !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity) ||
                    !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                    return false;
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(
                    copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                if (!selected.Succeeded || !OwnedMonotonicClock.TryReadDescription(
                        copied->Bytes + 16 + selected.Hpet.Offset, selected.Hpet.Length, out var description) ||
                    description.Address != state->HpetDescription.Address || description.HardwareId != state->HpetDescription.HardwareId ||
                    description.Address != state->Memory.References.HpetDevicePage)
                    return false;
            }
            byte* access = state->Memory.Code + FirstExitCodeBundle.ClockAccessOffset;
            if (!X64OwnedClockAccess.Validate(access, X64OwnedClockAccess.Bytes))
                return false;
            tsc = ReadClockCpu(access);
            hpet = new OwnedHpetAccess
            {
                Context = (void*)state->HpetDescription.Address,
                HardwareId = state->HpetDescription.HardwareId,
                Read = (delegate* unmanaged<void*, uint, ulong>)(access + X64OwnedClockAccess.ReadOffset),
                Write = (delegate* unmanaged<void*, uint, ulong, void>)(access + X64OwnedClockAccess.WriteOffset)
            };
#if M10_004_PROBE
            // The finite web fixture already acquired its UTC anchor before
            // exit. Preserve that HPET epoch instead of starting a second owner.
            return state->Realtime.IsAvailable && state->Clock.Source == OwnedClockSource.Hpet &&
                state->Clock.Status == OwnedClockStatus.Ready && OwnedMonotonicClock.Read(ref state->Clock);
#else
            return OwnedMonotonicClock.Start(ref state->Clock, hpet, tsc) && state->Clock.Source == OwnedClockSource.Hpet;
#endif
        }

        private static bool ReadClockProgress(Storage* state, out ulong ticks)
        {
            ulong previous = state->Clock.Ticks;
            ticks = previous;
            for (uint i = 0; i < OwnedMonotonicClock.MaximumMotionSamples; i++)
            {
                if (!OwnedMonotonicClock.Read(ref state->Clock))
                    return false;
                ticks = state->Clock.Ticks;
                if (ticks > previous)
                    return true;
            }
            return false;
        }

        private static OwnedTscAccess ReadClockCpu(byte* access)
        {
            var cpuid = (delegate* unmanaged<uint, uint, uint*, void>)(access + X64OwnedClockAccess.CpuidOffset);
            uint* words = stackalloc uint[4];
            cpuid(0, 0, words);
            OwnedTscAccess tsc = default;
            tsc.MaximumBasicLeaf = words[0];
            tsc.IntelVendor = words[1] == 0x756e6547 && words[3] == 0x49656e69 && words[2] == 0x6c65746e;
            cpuid(0x80000000, 0, words);
            tsc.MaximumExtendedLeaf = words[0];
            if (tsc.MaximumBasicLeaf >= 1)
            {
                cpuid(1, 0, words);
                tsc.Leaf1Edx = words[3];
            }
            if (tsc.MaximumExtendedLeaf >= 0x80000007)
            {
                cpuid(0x80000007, 0, words);
                tsc.Leaf80000007Edx = words[3];
            }
            if (tsc.IntelVendor && tsc.MaximumBasicLeaf >= 0x15)
            {
                cpuid(0x15, 0, words);
                tsc.Denominator = words[0];
                tsc.Numerator = words[1];
                tsc.CrystalHz = words[2];
            }
            if (tsc.IntelVendor && (tsc.Leaf1Edx & 0x04000010) == 0x04000010)
                tsc.Read = (delegate* unmanaged<ulong>)(access + X64OwnedClockAccess.TscOffset);
            return tsc;
        }

#if M05_MONOTONIC_CLOCK_PROBE
        private static bool WriteClockRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 3)
                return false;
            byte* text = stackalloc byte[90];
            *(ulong*)text = 0x434f4c435f35304dUL;
            text[8] = (byte)'K';
            text[9] = (byte)' ';
            text[10] = (byte)('0' + stage);
            uint length = 11;
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }

        private static bool WriteClockLiteral(Storage* state, uint marker)
        {
            ulong* words = stackalloc ulong[4];
            uint bytes;
            if (marker == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3330UL;
                bytes = 20;
            }
            else if (marker == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3330305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
            }
            else if (marker == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d33UL;
                bytes = 19;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x5f6b636f6c632033UL;
                words[3] = 0xa0d6574617473UL;
                bytes = 31;
            }
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }
#endif
#endif

#if M05_OWNED_ACPI_SELECTION_PROBE
        private static bool RunOwnedAcpiSelection(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->AcpiSelectionPhase != 0 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed)
                return false;
            state->AcpiSelectionPhase = 1;
            if (!WriteAcpiSelectionLiteral(state, 0))
                return false;
            if (!SelectOriginalOwnedAcpi(state))
            {
                _ = WriteAcpiSelectionLiteral(state, 3);
                return false;
            }
            state->AcpiSelectionPhase = 2;
            return WriteAcpiSelectionLiteral(state, 1) && WriteAcpiSelectionLiteral(state, 2);
        }

        private static bool SelectOriginalOwnedAcpi(Storage* state)
        {
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (!AdmitOwnedAcpiState(state) || workspace->Phase != 3 ||
                    !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity) ||
                    !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                    return false;
                ulong* originalDescriptors = stackalloc ulong[30];
                for (int i = 0; i < 30; i++)
                    originalDescriptors[i] = workspace->Descriptors[i];
                var selected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(
                    copied->Bytes + 16, workspace->TotalBytes, workspace->Descriptors, 30);
                if (!selected.Succeeded)
                    return false;
                // Change only the supplied scalar bound, never live retained bytes.
                var rejected = OwnedACPIDirectorySelectMADTHPETMCFG.Select(
                    copied->Bytes + 16, workspace->TotalBytes - 1, workspace->Descriptors, 30);
                // ReadBackAcpiBootstrap refreshes compared-byte counters. Check
                // every original descriptor first so it cannot hide a mutation.
                for (int i = 0; i < 30; i++)
                    if (originalDescriptors[i] != workspace->Descriptors[i])
                        return false;
                if (rejected.Status != OwnedAcpiDirectorySelectionStatus.InvalidDirectory ||
                    rejected.Mcfg.Length != 0 || rejected.Madt.Length != 0 || rejected.Hpet.Length != 0 ||
                    !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity) ||
                    !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                    return false;
                return WriteAcpiSelectionRow(state, 0, selected.Mcfg.PhysicalAddress, selected.Mcfg.Offset, selected.Mcfg.Length, selected.Mcfg.RootIndex) &&
                    WriteAcpiSelectionRow(state, 1, selected.Madt.PhysicalAddress, selected.Madt.Offset, selected.Madt.Length, selected.Madt.RootIndex) &&
                    WriteAcpiSelectionRow(state, 2, selected.Hpet.PhysicalAddress, selected.Hpet.Offset, selected.Hpet.Length, selected.Hpet.RootIndex) &&
                    WriteAcpiSelectionRow(state, 3, (ulong)rejected.Status, workspace->TotalBytes - 1, workspace->TotalBytes, 1);
            }
        }

        private static bool WriteAcpiSelectionLiteral(Storage* state, uint marker)
        {
            ulong* words = stackalloc ulong[4];
            uint bytes;
            if (marker == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3230UL;
                bytes = 20;
            }
            else if (marker == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3230305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
            }
            else if (marker == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d32UL;
                bytes = 19;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x735f697063612032UL;
                words[3] = 0xa0d65746174UL;
                bytes = 30;
            }
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }

        private static bool WriteAcpiSelectionRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 3)
                return false;
            byte* text = stackalloc byte[88];
            *(ulong*)text = 0x495043415f35304dUL;
            text[8] = (byte)' ';
            text[9] = (byte)('0' + stage);
            uint length = 10;
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

#if M05_PHYSICAL_MAPPING_PROBE
        // Reviewed mapping graph: 8,176 bytes below permanent continuation,
        // within its 32 KiB reserve. The dangerous profile still defaults off.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool MappingExecutionReady() => true;

        private static bool RunPhysicalMapping(void* context)
        {
            Storage* state = (Storage*)context;
            if (state->MappingPhase != 0 || state->OwnedPhase != 2 ||
                state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed)
                return false;
            state->MappingPhase = 1;
            if (!WriteMappingLiteral(state, 0))
                return false;
            if (!MappingExecutionReady() || !RunPhysicalMappingCore(state))
            {
                _ = WriteMappingRow(state, 9, state->MappingPhase, (ulong)state->Memory.Entries, 0, 0);
                _ = WriteMappingLiteral(state, 3);
                return false;
            }
            state->MappingPhase = 8;
            return WriteMappingLiteral(state, 1) && WriteMappingLiteral(state, 2);
        }

        private static bool RunPhysicalMappingCore(Storage* state)
        {
            FirstExitDiagnosticMemory memory = state->Memory;
            ulong root = (ulong)memory.Entries, start = memory.References.MappingScratchStart;
            if (!AuditOriginalMapping(state) ||
                PhysicalAddress.TryCreateFromUefiBoundary(start, out var physical) != AddressValueStatus.Success ||
                PhysicalAddress.TryCreateFromUefiBoundary(start + Page, out var selected) != AddressValueStatus.Success)
                return false;
            var readMsr = (delegate* unmanaged<uint, ulong>)(memory.Code + FirstExitCodeBundle.MappingReadMsrOffset);
            var writePat = (delegate* unmanaged<ulong, ulong, ulong, uint>)(memory.Code + FirstExitCodeBundle.MappingWritePatOffset);
            var publish = (delegate* unmanaged<ulong*, ulong, ulong, ulong, uint>)(memory.Code + FirstExitCodeBundle.MappingPublishOffset);
            ulong capabilities = ((delegate* unmanaged<ulong>)(memory.Code + FirstExitCodeBundle.MappingReadCapabilitiesOffset))();
            if ((capabilities & 0x11000) != 0x11000 || ((capabilities >> 32) & 0x2000) != 0)
                return false;
            ulong mtrrCap = readMsr(0xfe), mtrrDefault = readMsr(0x2ff), originalPat = readMsr(0x277);
            uint variableCount = (uint)(mtrrCap & 0xff);
            if (variableCount > 32 || (mtrrCap & 0xc00) != 0x400 ||
                originalPat != memory.Tables.Preconditions.PatMapping.RawIa32Pat ||
                ((originalPat >> 32) & 0xff) != 6)
                return false;
            ulong* bases = stackalloc ulong[32];
            ulong* masks = stackalloc ulong[32];
            for (uint i = 0; i < variableCount; i++)
            {
                bases[i] = readMsr(0x200 + 2 * i);
                masks[i] = readMsr(0x201 + 2 * i);
            }
            if (PostFirmwarePhysicalMappingExplicitUCWCWB.ValidateWriteBackMtrrRange(
                    physical, 0x200000, memory.Tables.Preconditions.PhysicalAddressWidth, state->MappingCr0,
                    mtrrCap, mtrrDefault, false, bases, masks, variableCount) != PhysicalMappingPreparationStatus.Prepared ||
                !WriteMappingRow(state, 0, root, start, mtrrCap, mtrrDefault))
                return false;

            // The original canonical audit admits only PAT index 0 WB leaves.
            // Slot 4 is unused, so changing it needs translation invalidation,
            // but no cache-type transition until a leaf actually selects it.
            ulong changedPat = (originalPat & ~(0xffUL << 32)) | (1UL << 32);
            if (X64PatMappingToken.TryCreate(changedPat, 0, 3, 4, out var cache) != X64PageTableEntryStatus.Success)
                return false;
            var conditions = X64PageTableEncodingPreconditions.CreateObserved(
                memory.Tables.Preconditions.PhysicalAddressWidth, true, true, false, false, cache);
            if (conditions.Validate() != X64PageTableEntryStatus.Success)
                return false;
            state->MappingPhase = 2;
            if (writePat(originalPat, changedPat, root) != 1 || readMsr(0x277) != changedPat ||
                !WriteMappingRow(state, 1, originalPat, changedPat, root, capabilities))
                return false;

            if (!FindMappingPde(memory, start, out uint parentIndex, out uint childPage))
                return false;
            ulong* parent = memory.Entries + parentIndex;
            ulong* children = memory.Entries + childPage * 512;
            for (uint i = 0; i < 512; i++)
            {
                var leaf = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, children[i], conditions);
                if (!leaf.Succeeded || leaf.Kind != X64PageTableEntryKind.Leaf ||
                    leaf.Protection != (PageProtection.Read | PageProtection.Write) || leaf.CachePolicy != PageCachePolicy.WriteBack ||
                    leaf.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong address) != AddressValueStatus.Success ||
                    address != start + i * Page)
                    return false;
                WriteMappingValue((ulong*)address, 0x434c524f53000000UL + i);
            }
            var large = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(X64PageTableLevel.Pde,
                X64PageTableEntryKind.Leaf, physical, PageProtection.Read | PageProtection.Write, PageCachePolicy.WriteBack), conditions);
            if (!large.Succeeded)
                return false;
            ulong originalParent = *parent, largeEntry = large.Entry.RawValue | 0x60UL;
            // Both refusals must occur before the native stub's first write.
            if (publish(parent, originalParent ^ 2UL, largeEntry, root) != 0 || *parent != originalParent ||
                publish(parent, originalParent, largeEntry, root + Page) != 0 || *parent != originalParent)
                return false;
            state->MappingPhase = 3;
            if (publish(parent, originalParent, largeEntry, root) != 1 || *parent != largeEntry ||
                !WriteMappingRow(state, 2, (ulong)parent, originalParent, *parent, childPage))
                return false;
            // The old PT is detached only after CR3 reload and cache flush.
            // Reuse that exact page so the final canonical audit remains valid.
            for (uint i = 0; i < 512; i++)
                children[i] = 0;
            var split = PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(
                memory.Tables.ArenaStart, memory.Entries, (uint)TablePages, selected, Page,
                PhysicalMappingUsage.Device, conditions);
            if (!split.IsPrepared || split.ParentEntryIndex != parentIndex || split.ChildTablePageIndex != childPage ||
                split.OriginalPde != *parent)
                return false;
            state->MappingPhase = 4;
            if (publish(parent, split.OriginalPde, split.ReplacementPde, root) != 1 ||
                ReadMappingValue((ulong*)(start + Page)) != 0x434c524f53000001UL ||
                !WriteMappingRow(state, 3, (ulong)parent, split.OriginalPde, *parent, children[1]))
                return false;

            ulong* selectedEntry = children + 1;
            WriteMappingValue((ulong*)(start + Page), 0x5543000000000001UL);
            if (ReadMappingValue((ulong*)(start + Page)) != 0x5543000000000001UL ||
                !EncodeMappingLeaf(selected, PageCachePolicy.WriteCombining, conditions, *selectedEntry, out ulong wc))
                return false;
            ulong uc = *selectedEntry;
            state->MappingPhase = 5;
            if (publish(selectedEntry, uc, wc, root) != 1 || *selectedEntry != wc ||
                !WriteMappingRow(state, 4, (ulong)selectedEntry, uc, wc, changedPat))
                return false;
            WriteMappingValue((ulong*)(start + Page), 0x5743000000000001UL);
            if (!EncodeMappingLeaf(selected, PageCachePolicy.WriteBack, conditions, *selectedEntry, out ulong wb))
                return false;
            state->MappingPhase = 6;
            // The native MFENCE drains the WC store before removing this leaf.
            if (publish(selectedEntry, *selectedEntry, wb, root) != 1 ||
                ReadMappingValue((ulong*)(start + Page)) != 0x5743000000000001UL ||
                !WriteMappingRow(state, 5, (ulong)selectedEntry, wc, *selectedEntry, 0x5743000000000001UL))
                return false;
            for (uint i = 0; i < 512; i++)
                if (ReadMappingValue((ulong*)(start + i * Page)) !=
                    (i == 1 ? 0x5743000000000001UL : 0x434c524f53000000UL + i))
                    return false;
            // No reachable leaf uses slot 4 before restoring its firmware value.
            if (!AuditOriginalMapping(state))
                return false;
            state->MappingPhase = 7;
            if (writePat(changedPat, originalPat, root) != 1 || readMsr(0x277) != originalPat ||
                !AuditOriginalMapping(state) || !FirstExitOwnedArena.ValidateCanaries(state->Arena) ||
                !WriteMappingRow(state, 6, root, start, originalPat, *selectedEntry))
                return false;
            return true;
        }

        private static bool AuditOriginalMapping(Storage* state)
        {
            FirstExitDiagnosticMemory memory = state->Memory;
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            return FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, memory.Code, memory.Capture, memory.Mapping,
                    memory.References, memory.Allocator, memory.Tables, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) &&
                SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(memory.Tables, ranges, count,
                    FirstExitRetainedMappings.MaximumRanges, memory.Pages, memory.PageCapacity, memory.Entries, memory.EntryCapacity).Succeeded;
        }

        private static bool FindMappingPde(FirstExitDiagnosticMemory memory, ulong start, out uint parentIndex, out uint childPage)
        {
            parentIndex = 0;
            childPage = 0;
            ulong root = (ulong)memory.Entries, table = root;
            for (int level = 4; level >= 2; level--)
            {
                uint index = (uint)((table - root) / 8 + ((start >> (12 + 9 * (level - 1))) & 511));
                var entry = X64PageTableEntry.TryDecode((X64PageTableLevel)level, memory.Entries[index], memory.Tables.Preconditions);
                if (!entry.Succeeded || entry.Kind != X64PageTableEntryKind.TablePointer ||
                    entry.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong next) != AddressValueStatus.Success ||
                    next < root || next - root >= TablePages * Page || (next & 4095) != 0)
                    return false;
                if (level == 2)
                {
                    parentIndex = index;
                    childPage = (uint)((next - root) / Page);
                    return true;
                }
                table = next;
            }
            return false;
        }

        private static bool EncodeMappingLeaf(PhysicalAddress address, PageCachePolicy cache,
            X64PageTableEncodingPreconditions conditions, ulong previous, out ulong encoded)
        {
            var result = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(X64PageTableLevel.Pte,
                X64PageTableEntryKind.Leaf, address, PageProtection.Read | PageProtection.Write, cache), conditions);
            encoded = result.Succeeded ? result.Entry.RawValue | (previous & 0x60UL) : 0;
            return result.Succeeded;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static ulong ReadMappingValue(ulong* address) => *address;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WriteMappingValue(ulong* address, ulong value) => *address = value;

        private static bool WriteMappingLiteral(Storage* state, uint marker)
        {
            ulong* words = stackalloc ulong[5];
            uint bytes;
            if (marker == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e35306d205452UL;
                words[2] = 0xa0d3130UL;
                bytes = 20;
            }
            else if (marker == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3130305f35UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
            }
            else if (marker == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x30302e35306d2053UL;
                words[2] = 0xa0d31UL;
                bytes = 19;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x30302e35306d204cUL;
                words[2] = 0x6e697070616d2031UL;
                words[3] = 0xd65746174735f67UL;
                words[4] = 0xaUL;
                bytes = 33;
            }
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }

        private static bool WriteMappingRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d)
        {
            if (stage > 9)
                return false;
            byte* text = stackalloc byte[87];
            *(ulong*)text = 0x2050414d5f35304dUL;
            text[8] = (byte)('0' + stage);
            uint length = 9;
            ulong* values = stackalloc ulong[4];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            for (int i = 0; i < 4; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'a' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }
#endif

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void Fault(FirstExitFaultFrame* frame, FirstExitFaultContext* context, ulong cr2, ulong cr3)
        {
            fixed (Storage* state = &s_storage)
            {
                if (state->Phase != 4 || !state->Arena.IsReady || context != state->Arena.Fault)
                    return;
#if M03_OWNED_SUBSYSTEM_PANIC_PROBE
                ulong bottom = (ulong)state->Arena.Storage + 94 * Page + 8;
                ulong top = (ulong)state->Arena.Storage + 98 * Page - 16;
                if (state->OwnedPhase != 3 || context->Phase != 2 || frame == null ||
                    (ulong)frame < bottom || (ulong)frame > top - FirstExitTerminalFaultStub.FrameBytes ||
                    frame->Vector != 14 || frame->HasErrorCode != 1 || frame->ErrorCode != 0 ||
                    cr2 != state->PanicGuard || cr3 != (ulong)state->Memory.Entries ||
                    !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                    return;
                state->OwnedPhase = 4;
                if (!WriteOwnedRow(state, 8, cr2, cr3, (ulong)context, (ulong)frame, bottom, top, frame->Vector, frame->ErrorCode))
                    return;
                if (FirstExitTerminalFaultStub.Report(frame, context, cr2, cr3))
                    state->OwnedPhase = 5;
#else
                _ = FirstExitTerminalFaultStub.Report(frame, context, cr2, cr3);
#endif
            }
        }

#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
        // Every mutable source and metadata owner read by AcpiSnapshotAdmission
        // is original image storage; physical addresses in the rows are identities.
        private static bool AdmitOwnedAcpiState(Storage* state)
        {
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            fixed (OwnedMemoryRegionBuffer* regions = &s_ownedMemoryRegions)
            fixed (BootMemoryRegionMaterializationResult* map = &s_memoryRegionMaterialization)
            fixed (EfiConfigurationTableScanResult* scan = &s_acpiRsdpScanResult)
            {
                if (state->AcpiWorkspace != (ulong)workspace || state->AcpiWorkspaceBytes != (ulong)sizeof(AcpiBootstrapWorkspace) ||
                    state->Memory.References.CopiedAcpi != (ulong)copied ||
                    state->Memory.References.CopiedAcpiBytes != (ulong)sizeof(AcpiBootstrapOwnedBuffer) ||
                    state->Memory.References.AdapterState != (ulong)state || state->Memory.References.AdapterStateBytes != (ulong)sizeof(Storage))
                    return false;
                SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitImageMappings.MaximumSections + 1];
                if (!FirstExitImageMappings.WriteRanges(state->Memory.Capture, state->Memory.Mapping, ranges,
                    FirstExitImageMappings.MaximumSections + 1, out ulong count))
                    return false;
                ulong* addresses = stackalloc ulong[7];
                ulong* lengths = stackalloc ulong[7];
                addresses[0] = (ulong)workspace;
                lengths[0] = (ulong)sizeof(AcpiBootstrapWorkspace);
                addresses[1] = (ulong)copied;
                lengths[1] = (ulong)sizeof(AcpiBootstrapOwnedBuffer);
                addresses[2] = (ulong)regions;
                lengths[2] = (ulong)sizeof(OwnedMemoryRegionBuffer);
                addresses[3] = (ulong)map;
                lengths[3] = (ulong)sizeof(BootMemoryRegionMaterializationResult);
                addresses[4] = (ulong)scan;
                lengths[4] = (ulong)sizeof(EfiConfigurationTableScanResult);
                addresses[5] = (ulong)state;
                lengths[5] = (ulong)sizeof(Storage);
                addresses[6] = state->Memory.References.AllocatorState;
                lengths[6] = M01LiveAllocatorStaticState.RequiredStorageBytes;
                for (int i = 0; i < 7; i++)
                {
                    if (!FirstExitImageMappings.Contains(ranges, count, addresses[i], lengths[i], PageProtection.Read | PageProtection.Write))
                        return false;
                    for (int j = 0; j < i; j++)
                        if (!Disjoint(addresses[i], lengths[i], addresses[j], lengths[j]))
                            return false;
                }
                return true;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool ReportOwnedSubsystems(Storage* state)
        {
            if (state->OwnedPhase != 0 || !AdmitOwnedAcpiState(state) ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong owner, out var backing) ||
                owner != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != state->Memory.Allocator.PageCount)
                return false;
            state->OwnedPhase = 1;
            if (BootSequence.RunOwnedSubsystemAllocatorRoundTrip(ref state->OwnedAllocator, ref state->Diagnostic,
                ref state->Arena, state->Memory, (M01LiveAllocatorStaticState*)owner) != OwnedAllocatorRoundTripStatus.Completed)
                return false;
            if (!WriteOwnedLiteral(state, 0))
                return false;
            M01LiveAllocatorStaticState* allocator = (M01LiveAllocatorStaticState*)owner;
            if (!WriteOwnedRow(state, 0, owner, state->OwnedAllocator.Page, backing.Start, backing.PageCount,
                allocator->AllocatorState.FreePageCount, allocator->AllocatorState.FreeByteCount,
                allocator->AllocatorState.AllocatedPageCount, allocator->AllocatorState.AllocatedByteCount))
                return false;
            fixed (AcpiBootstrapWorkspace* workspace = &s_acpiBootstrap)
            fixed (AcpiBootstrapOwnedBuffer* copied = &s_acpiBootstrapOwned)
            {
                if (!AdmitOwnedAcpiState(state) || workspace->Phase != 3 ||
                    !ReadBackAcpiBootstrap(workspace, copied->Bytes, AcpiOwnedPayloadCapacity))
                    return OwnedFailure(state);
                for (uint role = 0; role < 5; role++)
                {
                    ulong* row = workspace->Descriptors + role * 6;
                    if (!WriteOwnedRow(state, role + 1, (ulong)workspace, (ulong)(copied->Bytes + 16),
                        row[0], row[4], row[2], row[1], row[3], row[5]))
                        return false;
                }
            }
            if (!FirstExitOwnedArena.ValidateInstalledDescriptors(ref state->Arena, (ulong)state->Memory.Code) ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return OwnedFailure(state);
            FirstExitDescriptorContext* descriptors = state->Arena.Descriptors;
            if (!WriteOwnedRow(state, 6, (ulong)state->Arena.Storage, (ulong)state->Memory.Entries, (ulong)state->Memory.Code,
                (ulong)descriptors->Gdt, (ulong)descriptors->Tss, (ulong)descriptors->FinalIdt,
                descriptors->ObservedTr, state->Arena.Permanent->ObservedRsp))
                return false;
#if M03_OWNED_SUBSYSTEM_PANIC_PROBE
            state->PanicGuard = (ulong)state->Arena.Storage + 21 * Page;
            if (!FirstExitOwnedArena.IsGuardPage(21) || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return OwnedFailure(state);
            state->OwnedPhase = 3;
            if (!WriteOwnedRow(state, 7, state->PanicGuard, (ulong)state->Memory.Entries, (ulong)state->Arena.Fault,
                (ulong)state->Memory.Code, (ulong)state->Arena.Storage, state->Arena.StackBottom, state->Arena.StackTop,
                (ulong)state->Arena.Storage + 98 * Page - 16))
                return false;
            state->UnexpectedGuardValue = ReadOwnedGuard((ulong*)state->PanicGuard);
            return OwnedFailure(state);
#else
            if (!WriteOwnedLiteral(state, 1) || !WriteOwnedLiteral(state, 2))
                return false;
            state->OwnedPhase = 2;
            return true;
#endif
        }

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static ulong ReadOwnedGuard(ulong* address) => *address;
#endif

#if CLROS_NORMAL_VERIFIED_EXIT
        private static bool WriteVerifiedExitBytes(void* context, byte* text, uint bytes)
        {
            Storage* state = (Storage*)context;
            return state->OwnedPhase == 2 && WriteOwnedBytes(state, text, bytes);
        }
#endif

        private static bool OwnedFailure(Storage* state)
        {
            _ = WriteOwnedLiteral(state, 3);
            return false;
        }

        private static bool WriteOwnedLiteral(Storage* state, uint marker)
        {
            ulong* words = stackalloc ulong[4];
            uint bytes;
            if (marker == 0)
            {
                words[0] = 0x4154535f54534554UL;
                words[1] = 0x302e33306d205452UL;
                words[2] = 0xa0d3031UL;
                bytes = 20;
            }
            else if (marker == 1)
            {
                words[0] = 0x304d5f534f524c43UL;
                words[1] = 0x45525f3031305f33UL;
                words[2] = 0xa0d594441UL;
                bytes = 21;
            }
            else if (marker == 2)
            {
                words[0] = 0x5341505f54534554UL;
                words[1] = 0x31302e33306d2053UL;
                words[2] = 0xa0d30UL;
                bytes = 19;
            }
            else
            {
                words[0] = 0x4941465f54534554UL;
                words[1] = 0x31302e33306d204cUL;
                words[2] = 0x5f64656e776f2030UL;
                words[3] = 0xa0d6574617473UL;
                bytes = 31;
            }
            return WriteOwnedBytes(state, (byte*)words, bytes);
        }

        private static bool WriteOwnedRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d, ulong e, ulong f, ulong g, ulong h)
        {
            if (stage > 8)
                return false;
            byte* text = stackalloc byte[165];
            *(ulong*)text = 0x454e574f5f33304dUL;
            *(ushort*)(text + 8) = 0x2044;
            text[10] = (byte)('0' + stage);
            uint length = 11;
            ulong* values = stackalloc ulong[8];
            values[0] = a;
            values[1] = b;
            values[2] = c;
            values[3] = d;
            values[4] = e;
            values[5] = f;
            values[6] = g;
            values[7] = h;
            for (int i = 0; i < 8; i++)
            {
                text[length++] = (byte)' ';
                text[length++] = (byte)'0';
                text[length++] = (byte)'x';
                for (int shift = 60; shift >= 0; shift -= 4)
                {
                    uint digit = (uint)((values[i] >> shift) & 15);
                    text[length++] = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                }
            }
            text[length++] = 13;
            text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
        }

        private static bool WriteOwnedBytes(Storage* state, byte* text, uint bytes)
        {
            if (state->Diagnostic.Status != FirstExitDiagnosticStatus.Completed || !FirstExitOwnedArena.ValidateCanaries(state->Arena))
                return false;
            Com1OwnedPortIoBackend backend = state->Arena.Fault->Backend;
            for (uint i = 0; i < bytes; i++)
            {
                bool ready = false;
                for (int poll = 0; poll < PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit; poll++)
                {
                    Com1OwnedPortIoOperationResult read = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                    if (!read.Succeeded)
                        return false;
                    if ((read.Value & PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask) != 0)
                    {
                        ready = true;
                        break;
                    }
                }
                if (!ready || !backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, text[i]).Succeeded)
                    return false;
            }
            return true;
        }
#endif

        private static void WritePreparationFailure(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong checkpoint, uint memoryType)
        {
            // TEST_FAIL m03.007 preparation\r\n; no success claim before owned adoption.
            for (int i = 0; i < 29; i++)
            {
                ulong word = (i / 8) switch
                {
                    0 => 0x4941465f54534554UL,
                    1 => 0x30302e33306d204cUL,
                    2 => 0x7261706572702037UL,
                    3 => 0x000a0d6e6f697461UL,
                    _ => 0
                };
                Write(console, serial, (byte)(word >> ((i % 8) * 8)));
            }
            Write(console, serial, (byte)'=');
            WriteUnsignedDecimal(console, serial, checkpoint);
            Write(console, serial, (byte)'/');
            WriteUnsignedDecimal(console, serial, memoryType);
            WriteCrLf(console, serial);
        }

        private static bool Query(ulong code, out X64PagingModePreflightObservation cpu,
            out ulong cs, out X64PageTableEncodingPreconditions conditions)
        {
            cpu = default;
            cs = 0;
            conditions = default;
            X64PagingModePreflightObservation observation = default;
            X64PagingModePreflightResult scratch = default;
            ulong* selectors = stackalloc ulong[3];
            for (int i = 0; i < 3; i++)
                selectors[i] = 0;
            for (int i = 0; i < 46; i++)
                ((byte*)code)[128 + i] = SelectorByte(i);
            for (int i = 0; i < 21; i++)
                ((byte*)code)[192 + i] = PatByte(i);
            for (int i = 0; i < 46; i++)
                if (((byte*)code)[128 + i] != SelectorByte(i))
                    return false;
            for (int i = 0; i < 21; i++)
                if (((byte*)code)[192 + i] != PatByte(i))
                    return false;
            if (!X64PagingModePreflightStubPlan.TryMaterialize((byte*)code, Page, 0, 111, &observation, 56, &scratch, 96).Succeeded ||
                !X64PagingModePreflightStubPlan.TryValidateMaterialized((byte*)code, Page, 0, 111, &observation, 56, &scratch, 96).Succeeded ||
                X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie)
                return false;
            if (((delegate* unmanaged<ulong*, uint>)(code + 128))(selectors) != 1 ||
                selectors[0] > 4088 || (selectors[0] & 7) != 0 || (selectors[0] != 8 && selectors[0] < 40) ||
                (selectors[1] & 0x00f0fe00) != 0x00a09a00 || (selectors[2] & 0x07010000) != 0x07010000)
                return false;
            ((delegate* unmanaged<X64PagingModePreflightObservation*, void>)code)(&observation);
            if (!observation.IsInitialized || observation.CpuidMaximumExtendedLeaf < 0x80000008 ||
                observation.PhysicalAddressWidth < 36 || observation.PhysicalAddressWidth > 52 ||
                (observation.Cr0 & 0x8001000d) != 0x80010001 ||
                (observation.RawCr4 & ~0x7ffUL) != 0 || (observation.RawCr4 & 0x620) != 0x620 ||
                observation.RawCr3 >= 0x100000000 || (observation.RawCr3 & 0xfe7) != 0 || (observation.RawCr3 & ~0x18UL) == 0 ||
                (observation.Ia32Efer & 0xd00) != 0xd00 || (observation.Rflags & 0x24702) != 0x202)
                return false;
            ulong pat = 0;
            ((delegate* unmanaged<ulong*, void>)(code + 192))(&pat);
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M05_PCI_ENUMERATION_PROBE
            byte uncached = 255;
            for (byte i = 0; i < 8; i++)
                if (((pat >> (i * 8)) & 255) == 0)
                {
                    uncached = i;
                    break;
                }
            if (uncached == 255 || X64PatMappingToken.TryCreate(pat, 0, uncached, 255, out var cache) != X64PageTableEntryStatus.Success)
                return false;
#else
            if (X64PatMappingToken.TryCreate(pat, 0, 255, 255, out var cache) != X64PageTableEntryStatus.Success)
                return false;
#endif
            conditions = X64PageTableEncodingPreconditions.CreateObserved(observation.PhysicalAddressWidth, true, true, false, false, cache);
            cpu = observation;
            cs = selectors[0];
            return conditions.Validate() == X64PageTableEntryStatus.Success;
        }

        // CS/LAR/CPUID.1:EDX; preserve RBX. Same 46-byte query used by M02-016.
        private static byte SelectorByte(int i) => i switch
        {
            0 => 0xf3,
            1 => 0x0f,
            2 => 0x1e,
            3 => 0xfa,
            4 => 0x31,
            5 => 0xc0,
            6 => 0x66,
            7 => 0x8c,
            8 => 0xc8,
            9 => 0x48,
            10 => 0x89,
            11 => 0x01,
            12 => 0x0f,
            13 => 0x02,
            14 => 0xd0,
            15 => 0x75,
            16 => 0x1a,
            17 => 0x48,
            18 => 0x89,
            19 => 0x51,
            20 => 0x08,
            21 => 0x53,
            22 => 0x49,
            23 => 0x89,
            24 => 0xc8,
            25 => 0xb8,
            26 => 0x01,
            27 => 0x00,
            28 => 0x00,
            29 => 0x00,
            30 => 0x0f,
            31 => 0xa2,
            32 => 0x49,
            33 => 0x89,
            34 => 0x50,
            35 => 0x10,
            36 => 0x5b,
            37 => 0xb8,
            38 => 0x01,
            39 => 0x00,
            40 => 0x00,
            41 => 0x00,
            42 => 0xc3,
            43 => 0x31,
            44 => 0xc0,
            45 => 0xc3,
            _ => 0
        };

        // RCX points to one owned ulong. CPUID.PAT and ring zero were checked.
        // mov r8,rcx; mov ecx,277h; rdmsr; shl rdx,32; or rax,rdx; mov [r8],rax; ret.
        private static byte PatByte(int i) => i switch
        {
            0 => 0x49,
            1 => 0x89,
            2 => 0xc8,
            3 => 0xb9,
            4 => 0x77,
            5 => 0x02,
            6 => 0x00,
            7 => 0x00,
            8 => 0x0f,
            9 => 0x32,
            10 => 0x48,
            11 => 0xc1,
            12 => 0xe2,
            13 => 0x20,
            14 => 0x48,
            15 => 0x09,
            16 => 0xd0,
            17 => 0x49,
            18 => 0x89,
            19 => 0x00,
            20 => 0xc3,
            _ => 0
        };
    }
#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE

#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE

#endif
#if M03_FIRMWARE_ENTROPY_SEED_PROBE || M16_004_PROBE

#if M10_004_PROBE

#else

#endif

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE

#endif
#if M01_OWNED_PAGING_PROBE

#if M02_SAFE_STUB_PROBE

#endif
#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif

#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif

#if M02_UD2_VECTOR_STATE_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif
#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_COM1_OWNED_PORT_IO_PROBE

#endif

#if M02_CHECKED_PORT_IO_BINDING_PROBE

#if M02_COM1_OWNED_PORT_IO_PROBE

#if M02_OWNED_COM1_READY_PROBE

#if M02_PANIC_SAFE_OWNED_COM1_PROBE

#if M02_OWNED_X64_GDT_ENCODING_PROBE

#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE

#endif
#endif
#endif
#endif
#endif
#endif

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_COM1_OWNED_PORT_IO_PROBE

#endif

#if M02_OWNED_COM1_READY_PROBE

#endif

#if M02_PANIC_SAFE_OWNED_COM1_PROBE

#endif

#if M02_OWNED_X64_GDT_ENCODING_PROBE

#endif

#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE

#endif

#if M02_OWNED_X64_GDT_ENCODING_PROBE

#endif

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif
#if M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE

#endif
#if M02_DOUBLE_FAULT_IST_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE

#endif
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE

#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE

#endif
#if M04_ARITHMETIC_CONFORMANCE_PROBE

#endif
#if M04_LAYOUT_CONTRACT_PROBE

#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE

#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE

#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M03_FIRMWARE_ENTROPY_SEED_PROBE

#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE

#endif
#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#else
#endif
#if UEFI_GONE_SIMULATION_PROBE

#endif
#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE

#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE

#endif
#if M04_ARITHMETIC_CONFORMANCE_PROBE

#endif
#if M04_LAYOUT_CONTRACT_PROBE

#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE

#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE

#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if UEFI_GONE_SIMULATION_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#else
#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M04_RHP_NEW_FAST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE || M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M04_RHP_NEW_FAST_PROBE

#endif
#endif

#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE

#endif

#if (M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif
#if M04_RHP_NEW_FAST_PROBE

#endif
#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE

#endif

#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE

#endif
#if M02_DOUBLE_FAULT_IST_PROBE

#endif
#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif

#else
#endif
#if (M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M16_002_PROBE

#endif
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#if M04_RHP_NEW_FAST_PROBE

#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#else

#endif

#endif
#endif

#if M01_OWNED_PAGING_PROBE

#endif

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif

#else
#endif
#if M04_RHP_NEW_FAST_PROBE

#endif
#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE

#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif
}
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#pragma warning restore 649
