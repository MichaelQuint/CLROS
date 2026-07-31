# Repository Audit

## Current Status

Audit date: 2026-07-30.

The first-party workspace began as an empty baseline under the former `OS`
solution/repository identity. The current canonical solution is `CLROS.slnx`,
the current worktree is `C:\Projects\CLROS`, and the current Gitea repository is
`admin/CLROS`. The local `references/` directory contains reference snapshots
but they are not build inputs.

All 29 supplied reference snapshots are inventoried and classified in
`docs/reference-map.md`; detailed SharpOS adaptation decisions live in
`docs/fork-diff.md`, and provenance/license/reuse limits live in
`docs/license-audit.md`. SharpOS's DOOM and PowerShell done files and media are
historical engineering evidence only. They are not independently reproduced
CLROS evidence.

The lost implementation backlog has been reconstructed in both the repository
and the reachable local Gitea tracker:

- `.gitea/backlog/clros-issues.json` is the version-controlled intent ledger.
- `scripts/sync-gitea-backlog.ps1` previews by default and requires `-Apply`
  before it can mutate `http://localhost:3000/admin/CLROS`.
- The original applied tracker contained 16 open milestones, 68 managed
  labels, 16 milestone epics, and 303 child issues. M00-023 extended both the
  canonical manifest and live tracker to 315 children and 331 keyed issues by
  recording the repair itself and the serialized M01-016 through M01-026
  one-live-boundary-at-a-time integration/evidence chain.
- The guarded repair apply created 11 issues, updated 24 existing issues, and
  reported `SYNC_VERIFY_PASS milestones=16 labels=68 issues=331`. A subsequent
  no-write preview reported all 415 managed objects unchanged and no create,
  update, skip, or failure action.
- Reviewed M01-019 evidence exposed a missing single-stream boundary between
  the separate host models and EFI linkage. Adding M01-027 produced the later
  316-child, 332-keyed-issue, 416-managed-object snapshot; those figures are
  preserved as historical evidence. M01-027 and its dependent M01-020 linkage
  issue are now evidence-reviewed and closed.
- M01-028 records the subsequent live-paging feasibility recovery and inserts
  the serialized M01-029 through M01-032 prerequisites before M01-025. Its
  321-child, 337-keyed-issue, 421-managed-object inventory is preserved as the
  preceding historical applied snapshot.
- The M01-033 through M01-035 instruction-publication expansion produced the
  now-historical 324-child, 340-keyed-issue, 424-managed-object tracker
  snapshot. Its reviewed object, final-PE/unwind, and EfiEntry state-order
  evidence remains in `done/step884.md`, `done/step886.md`, and
  `done/step887.md`.
- M01-026 issue `#333` then passed the complete host gate and one fresh
  all-switch local QEMU/OVMF r16 run from implementation commit
  `8f1edea17f2de425520e239af58d37aa3b182146` and tree
  `6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`. The reviewed
  documentation/evidence head `26219cc5327e9b32935f4e5b06fe5eb014cca93c`
  merged through PR #422 as
  `6aaf046362d0a3187f551ee06f1456d05ce5021f`. Issue comments #6472 and
  #6477 and `done/step890.md` preserve the review and merge proof. The evidence
  manifest SHA-256 is
  `94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188`;
  all 47 artifacts rehashed independently, all 42 issue guest markers were
  ordered and unique, all 25 issue forbidden markers were absent, and the
  serial SHA-256 is
  `CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`.
  M01-026 is reviewed, merged, live-closed, and `agent:done`.
- M01-036 completed the final M01 host-only repository-state repair. Live
  issue `#425` and M01-EPIC `#24` are closed with `agent:done`, and M02-017 was
  created as issue `#427`. Its 16-epic, 326-child, 342-keyed-issue,
  426-managed-object inventory is now a historical applied snapshot.
  Implementation commit `f14a26f9267fc3e1cc32fcd5e847818d7dbe252c`
  passed the complete host-only gate with 3,329 starts, 3,330 passes, zero
  failures, and exactly one M01-036 start/ready/pass envelope.
  `done/step891.md` preserves the exact source tuple, log hashes,
  wrapper-timeout recovery, and evidence boundary.
- M02-001 is reviewed production C# rather than a planned leaf. Implementation
  commit `acec50b47056e7c538ca5225d32898ff2c0bc93f`, tree
  `ac236ce5acb9a88485b15e12f309ec2457d2e758`, and evidence head
  `5a4234a5b6e0fadcd1a4db9a01374340723ba4e8` merged through PR #428 as
  `85528a5513f1cf24396ff29acc6787bc015ec9c5`. Issue #41 comment #6578,
  merge-proof #6584, and `done/step892.md` preserve native exit 0, 3,336
  starts, 3,337 passes, zero failures, the exact one-family audited-plan
  policy, and the host-only nonclaims. At the M02-018 evidence-source
  checkpoint, issue #41 remained open in `agent:review` pending catalog-owned
  post-merge convergence.
- M02-018 issue `#429` completed the M02-001 durability transaction. Its
  source-bound r4 gate at
  `artifacts/logs/m02-018-final/79f318f2-20260730T201222Z-r4` passed all ten
  commands. Summary SHA-256
  `99A39D223A9D208B0E985E6A50CA1E617F4A52F33B2954B0294595325B676D7F`
  records native HostTests exit 0, 3,337 starts, 3,338 passes, zero failures,
  one exact M02-018 envelope, one terminal host pass, and empty stderr.
  Reviewed PR #430 merged as
  `8566f44cf493d7138d500b0cdad17b75d34b3405`; guarded synchronization then
  closed issues #41 and #429 with `agent:done` and converged the historical
  16-epic, 327-child, 343-keyed-issue, 427-managed-object snapshot. Comments
  #6605, #6615, and #6630 preserve review, merge, and convergence proof.
- M02-002 is now reviewed production C#, not remaining planned work. Accepted
  base `8566f44cf493d7138d500b0cdad17b75d34b3405`,
  implementation/evidence-source commit
  `06a38803b8bf4d5bc35e29948e626dbd8fb164bd`/tree
  `09bc5e263a3ab624ea3232655957d9ae79b2d042`, and reviewed head
  `88df315bcca0bf3302d25ca88451b1d51f1ce31b`/tree
  `c272b6991a68309a7e348bd20d85819977b7a264` merged through PR #431 as
  `da856db657575d8f8d67713c7afe96e113854e54`. Issue #42 comments #6644 and
  #6651 and `done/step894.md` preserve native exit 0, 3,338 starts, 3,339
  passes, zero failures, the exact 4 KiB page identities, no-stdlib
  compile/pointer evidence, and the host-only boundary. QEMU was dry-run only,
  the EFI build was regression/preflight only, and no M02 page was linked or
  executed in a guest.
- M02-019 live issue `#432` is the claimed durability transaction for
  M02-002. Its canonical target is 16 epics, 328 children, 344 keyed issues,
  and 428 managed objects with 19 M02 children. It records M02-002 as
  closed/done and gates M02-017 on M02-019 so a partial tracker update cannot
  release safe EFI integration. Until independent review, merge, and guarded
  convergence complete, issue #42 remains open in `agent:review` and #432
  remains open in `agent:claimed`; the target counts are not live-convergence
  evidence. M02-017 remains the sole serialized real-QEMU integration leaf
  before M02-003 and may execute only the fixed safe offset-zero return-cookie
  entry, never In8 or Out8.

## Implemented Behavior

- `CLROS.slnx` now contains first-party project slots for kernel, std/no-runtime shared code, host tests, host QEMU runner, and native-tier example apps.
- `src/OS.Kernel/Boot/BootSequence.cs` is the first source-of-truth boot ordering file.
- `src/OS.Kernel/Diagnostics/Probes.cs` exposes compile-time probe toggles.
- `SerialSmokeMarkers` defines `TEST_START`, `TEST_PASS`, `TEST_FAIL`, `OS_BOOT_OK`, `OS_SMOKE_PASS`, and `OS_SMOKE_FAIL`.
- `src/OS/OS.csproj` is an explicit no-stdlib NativeAOT EFI project included in
  `CLROS.slnx` under stricter directory-local build rules.
- `src/OS.Kernel/Boot/BootInfo.cs` defines the host-tested boot information and capability contract.
- `src/OS.Kernel/Boot/BootHandoffInfo.cs` defines the host-tested firmware handoff metadata contract for boot mode, capabilities, firmware-services mode, and memory-map metadata.
- `src/OS.Kernel/Memory` defines the host-tested first-party memory-map conversion contracts.
- `src/OS.Kernel/Diagnostics` includes host-tested diagnostic logging and panic formatting.
- `src/OS.Kernel/Diagnostics/ManagedX64StubStrategyADRUseExistingAudited.cs`
  now provides the reviewed allocation-free, status-coded M02-001 strategy
  decision over the exact audited Out8/In8 plans. It does not materialize or
  execute those bytes.
- `src/OS.Kernel/Diagnostics/ExistingIn8Out8StubBytesRWPagesFinalize.cs`
  now provides the reviewed allocation-free M02-002 fixed-page
  materialization, full-page readback, and value-only RW+NX-to-RO+X
  finalization audit. It does not change live protection or execute any entry.
- `tests/OS.HostTests` verifies marker emission, boot event ring-buffer behavior, boot info, typed handoff metadata, UEFI memory-map contracts, normalized memory-map conversion, UEFI-gone simulation behavior, diagnostics, manifest validation, libvirt XML generation, source firewall behavior, storage/network parser primitives, ACPI/PCI/virtio planning contracts, and the current `OS.Std.Ascii`, endian, and `OS.Std.MemoryExtensions` helper slices.
- `tools/OS.QemuRunner` can dry-run and run a QEMU/OVMF smoke, capture serial logs, and require `OS_SMOKE_PASS`.
- `tools/OS.ManifestTool` validates manifest v0 JSON with machine-readable markers.
- `tools/OS.LibvirtRunner` generates deterministic host-side domain XML in dry-run mode without requiring libvirt.
- `tools/OS.ImageBuilder` creates a deterministic raw GPT/FAT32 EFI System Partition image containing `\EFI\BOOT\BOOTX64.EFI`.
- `scripts/new-done-step.ps1` allocates the next `done/stepNNN.md` path with machine-readable markers and can reserve a template file for parallel-agent work without overwriting an existing path.
- `.gitea/backlog/clros-issues.json`,
  `scripts/sync-gitea-backlog.ps1`, and
  `tests/OS.HostTests/GiteaBacklogManifestTests.cs` preserve and verify the
  milestone, label, issue, parent, dependency, readiness, and additive
  synchronization contracts without storing Gitea credentials.

## Build Status

- Stable SDK installed for the current baseline: .NET SDK `10.0.302`,
  selected by `global.json` from the `10.0.300` feature band.
- Before scaffolding, `dotnet build CLROS.slnx` was a no-op because the solution had no projects.
- The current baseline build command passes with 0 warnings and 0 errors:

```powershell
dotnet build .\CLROS.slnx
```

## Boot Status

The first firmware-visible boot smoke exists and its default path was rebuilt
and reverified on 2026-07-25 from clean, pushed implementation commit
`32a8f3b91e17e1fc7e4b296730a5ea6565c38369`.
`scripts/build-efi-shell.ps1` published the source-bound
`BOOTX64.EFI`; `OS.ImageBuilder` wrapped it into a 64 MiB raw image; and
`scripts/run-qemu-smoke.ps1` booted that image through QEMU/OVMF. The serial
log reaches `TEST_START efi.main`,
`EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`,
`UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `UEFI_MEMORY_MAP_OWNED`,
`BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`,
`BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`,
`BOOT_MEMORY_REGION_COUNT <count>`, `BOOT_PAGE_ALLOCATOR_READY`,
`BOOT_ACPI_RSDP_HANDOFF_READY`, `UEFI_ACPI_RSDP_READY`, ordered
`boot.sequence` phase markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and
`OS_SMOKE_PASS`. The opt-in ACPI physical-discovery and UEFI-gone variant
records are historical May evidence; those variants were not rerun on
2026-07-25.

This is not a full kernel boot. `EfiMain` emits the shared ordered boot marker plan, captures raw UEFI memory-map metadata, copies descriptor bytes into OS-owned static storage, normalizes a compact owned-map summary for the boot boundary, materializes compact boot memory-region records into static storage, carries ACPI RSDP provenance into a no-stdlib-safe `BootHandoffRecord`, and reaches `BOOT_MEMORY_REGIONS_READY` plus `BOOT_ACPI_RSDP_HANDOFF_READY`. The ACPI physical discovery probe proves only pre-`ExitBootServices` table bytes on the tested QEMU/OVMF host. It does not call `ExitBootServices`, run owned drivers, map ECAM, enumerate PCI, read storage through virtio-blk, move packets through virtio-net, or launch an app.

## Runtime Status

No functional no-stdlib NativeAOT runtime helper implementation exists yet. `src/OS/Runtime/MinimalRuntime.cs` contains only minimal compiler-required shell stubs and placeholders for the EFI publish boundary, while `std/no-runtime/shared` contains small host-tested `OS.Std.Ascii`, byte-span `OS.Std.MemoryExtensions`, and endian helper slices that must be expanded before broader kernel or app usage.

## App Status

The example app projects exist and currently emit deferred-status markers only:

- `HelloSharpFs`
- `FetchApp`
- `TinyHttpServer`
- `DiagnosticApp`

## Driver Status

No first-party APIC, serial port hardware backend, framebuffer, live PCI, virtio-blk, or virtio-net driver implementation exists yet. Host-tested ACPI RSDP discovery/validation, RSDT/XSDT pointer-list parsing, MCFG-to-ECAM parsing, PCI ECAM address planning, PCI type-0 header parsing, PCI BAR metadata decoding, injected-reader PCI scanning, virtio PCI capability parsing/discovery, virtio initialization planning, virtio-blk split-ring descriptor materialization, and virtio-net queue/header metadata contracts exist. The opt-in ACPI physical discovery probe has live QEMU table-discovery markers, but these are not live hardware enumeration or driver evidence.

## Networking Status

Host-tested Ethernet, ARP, ARP neighbor resolution, bounded ARP neighbor cache, IPv4, ICMP echo, UDP, DHCP message/client transaction, DNS message/resolver transaction, TCP header, active/passive TCP handshake, active-connect, established payload stream, close/reset, HTTP request/response parsing, mock HTTP GET transaction, and host-only PAL socket/config/file contracts exist under `src/OS.Kernel`. No virtio-net live RX/TX, NIC-backed DHCP/DNS/TCP/HTTP, exported socket PAL, hosted .NET 10 networking, or Kestrel behavior exists yet.

## Storage Status

Host-tested read-only block-device contracts, GPT ESP parsing/selection, partition-relative devices, FAT32 BPB/root and first-level directory parsing, VFAT long-name lookup, FAT-chain-aware content reads, package parser/filesystem APIs, `OS/APP.PKG` loader over an `IReadOnlyBlockDevice`, virtio-blk request/read transaction planning, split-ring layout, descriptor-chain planning, and caller-owned descriptor-chain writer exist. No runtime virtio-blk driver, live disk read, boot-time package load from hardware, executable app mapping, or app loader execution exists yet.

## Packaging Status

`OS.ImageBuilder` generates `artifacts/images/os.raw` as a raw GPT/FAT32 image, can place `\OS\MANIFEST.JSON` and `\OS\APP.PKG` payloads, can build deterministic `OSPKGv0` packages, can validate manifest-selected native payload packages, and can wrap raw native app bytes into the current `OSAPPv0` payload. qcow2 conversion depends on host `qemu-img`; ISO and cloud import validation are not implemented yet.

## Tests

Current baseline tests passed:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Evidence:

- Host tests emitted `TEST_PASS host.tests`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- Source firewall emitted `TEST_PASS source.firewall`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- Real QEMU smoke emitted `TEST_PASS qemu.smoke`.
- UEFI-gone simulation QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring `OS_SMOKE_PASS`, `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- ACPI physical discovery probe QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- `artifacts/logs/qemu-smoke.log` contains `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `UEFI_MEMORY_MAP_OWNED`, `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT <count>`, `BOOT_ACPI_RSDP_HANDOFF_READY`, `UEFI_ACPI_RSDP_READY`, ordered `boot.sequence` phase markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `artifacts/logs/qemu-smoke-acpi.log` additionally contains the five ordered ACPI physical discovery markers.
- `dumpbin /headers src/OS/bin/Release/net10.0/win-x64/publish/OS.exe` reports `machine (x64)`, `entry point`, and `subsystem (EFI Application)`.

Current M00 recovery evidence from 2026-07-25:

- The implementation source was committed and pushed as
  `32a8f3b91e17e1fc7e4b296730a5ea6565c38369`; Git porcelain was empty
  before and after source-bound capture.
- `scripts/run-baseline-evidence.ps1` executed 11 steps from that source:
  solution build, host build/run, naming/source firewall, two QEMU dry-run
  lanes, qcow2/libvirt intent, `git diff --check`, and `git status --short`.
  Every step exited zero. The host log has exactly one
  `TEST_PASS host.tests` and no `TEST_FAIL host.tests`; summary SHA-256 is
  `C6835020F4DF780806C8DEFBDE43F91853299E03E08FBBEE46E5AAC523F26783`.
  `done/step845.md` records the full host tuple.
- `scripts/test-efi-artifact.ps1` accepted the emitted PE32+ x64 EFI
  application, associated entry RVA `0x00003730` with the same-build
  `EfiMain` contract, and found the import, bound-import, IAT, and
  delay-import directories absent. EFI SHA-256 is
  `0580A45FC10B93C7C218CAB9BA179549C278B4CA970C82420CE74684EDA6036F`.
- Real QEMU/OVMF boot observed all 12 required markers, including
  `BOOT_PAGE_ALLOCATOR_READY`, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and
  `OS_SMOKE_PASS`; neither `TEST_FAIL` nor `OS_SMOKE_FAIL` appeared. The raw
  image SHA-256 is
  `484A7B8D2C9E9C01BFF6AAA63633E3CEC3406A0FBAC487100DFB40B0A463428A`
  and serial SHA-256 is
  `C190355FB48902635AB4BC7EBD4BAEBD3E029619C851407A599D3BC9FB412BC7`.
- The authoritative schema-version-1 evidence manifest has source dirty
  `false`, classification `pass`, exit code `0`, 10 tool records, and 23
  hashed inputs. Its SHA-256 is
  `D398F73FF5051D138D5F2A77CE4EF51277BD2DE289F58BA5383FC6FC74D02E65`.
  `done/step846.md` records the command, timestamps, markers, artifacts,
  superseded manifest attempts, and exact non-claims.
- Generated evidence is under
  `artifacts/logs/m00-final/32a8f3b91e17-20260725T213702Z/` and is not
  committed as first-party source.
- The verified pre-M01-036 live Gitea snapshot contained 68 labels, 16 open
  milestones, 324 children, 340 uniquely keyed issues, and 424 total managed
  objects. The earlier guarded M00-023 apply recognized issue
  #321, created only M01-016 through M01-026 as issues #323 through #333,
  updated exactly 24 existing managed issues, and emitted
  `SYNC_VERIFY_PASS milestones=16 labels=68 issues=331`; those counts are
  preserved historical evidence. The subsequent M01-027 insertion produced
  316 children, 332 keyed issues, and 416 managed objects; that inventory is
  also preserved as historical evidence rather than reported as current.
  M01-001 through M01-015 are host-evidence leaves. M01-019 is closed with
  reviewed `done/step866.md` evidence, M01-027 is closed with reviewed
  `done/step867.md` evidence, and M01-020 is closed with reviewed
  `done/step868.md` linkage/build evidence. M01-028 now serializes M01-029
  through M01-032 before M01-025. The new durable chain is
  `M01-025 -> M01-033 -> M01-034 -> M01-035 -> M01-026`. The guarded apply
  closed M01-025 `#332`, created M01-033 `#411`, M01-034 `#412`, and M01-035
  `#413`, and initially made only M01-033 ready. M01-033's reviewed
  implementation and closure are now merged, with the object-only evidence
  boundary preserved in `done/step884.md` and issue comment #6336. M01-034 is
  likewise merged and closed with static final-artifact evidence in
  `done/step886.md` and issue comment #6366. M01-035 is merged and closed with
  source-order, host, and fresh-build evidence in `done/step887.md` and issue
  comment #6388. Guarded synchronization verified all three closures and
  computed M01-026 as the sole ready issue. The first leaf proves object bytes
  and relocatable unwind intent; the second proves the authoritative linked PE
  exception directory statically; the third proves the exact
  `0 -> 1 -> 3 -> 7 -> 15` publication state across the one-way stack switch
  and places epoch 3 inside `CaptureArchitecturalPreflight` before its first
  generated-code invocation, all without guest execution. M01-026 subsequently
  closed with the bounded r16 cumulative local-QEMU/OVMF evidence described
  above. M01-036 then closed the final M01 repository/tracker durability
  boundary, closed M01-EPIC, and produced the applied 326-child,
  342-keyed-issue, 426-managed-object snapshot. M02-001 subsequently merged
  reviewed host-only production C# through PR #428. M02-018 then converged
  issues #41 and #429 at the historical 327-child, 343-keyed-issue,
  427-managed-object checkpoint. M02-002 subsequently merged reviewed
  host-only production C# through PR #431. M02-019 now owns its 328-child,
  344-keyed-issue, 428-managed-object durability transaction before M02-017
  alone may perform safe guest execution ahead of M02-003. Only `M15-013`, the
  isolated optional DOOM/WAD boundary, retains
  `reference:license-blocked`. This is administrative tracker evidence, not
  additional kernel capability evidence.

## Known Limitations

- This audit cannot claim runtime, live driver, live storage, live network, app execution, or hosted-workload readiness.
- The default solution projects are plain `net10.0` scaffolds. The explicit `src/OS` EFI shell is no-stdlib and now has first-marker QEMU proof, but it is not a functional kernel artifact.
- SharpOS-authored source in `references/SharpOS-main` is eligible for
  selective adaptation under the recorded owner permission and public CC0
  evidence, but each adaptation still requires exact local path/hash,
  provenance, boundary, first-party review, and CLROS tests. File-local vendor,
  GPL, CoreCLR, WAD, and media rights remain separate.

## Required Post-M02-002 Sequence

The accepted M02-002 merge requires this ordered durability sequence. Later
tracker comments and synchronizer logs prove which steps completed.

1. Implement and independently review M02-019 from the exact six owned
   catalog/synchronizer/test/document paths and one newly reserved done step.
2. Post the exact `<!-- clros-evidence-reviewed:M02-019 -->` marker to issue
   `#432` while it remains open, and merge only the reviewed source head.
3. From clean canonical master, verify unique M02-002 and M02-019 hidden keys
   and both reviewed markers, then run guarded
   preview/apply/repeated-apply/final-preview.
4. Require issues `#42` and `#432` closed with exactly `agent:done`, no
   duplicate hidden keys, M02-017 ready, and final 428-object convergence with
   zero failures or proposed writes.
5. Claim and implement the serialized M02-017 safe-return-cookie EFI/QEMU slice
   before any M02-003 attempt to bind privileged In8/Out8 entries.
