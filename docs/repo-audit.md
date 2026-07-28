# Repository Audit

## Current Status

Audit date: 2026-07-28.

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
- The current durable manifest and live tracker add M01-033 through M01-035
  after M01-025 and before M01-026: deterministic C# AMD64 COFF serializer and
  encoded unwind intent, canonical EFI
  link/authoritative-exception-directory verification, and four-epoch
  EfiEntry checked-cookie publication ordering through stack-switch-persistent
  state. The guarded 2026-07-28 apply closed reviewed M01-025 issue `#332`,
  created issues `#411` through `#413`, and converged at 324 children, 340
  keyed issues, and 424 managed objects. M01-033 `#411` is the only open
  `agent:ready` issue; M01-034 `#412`, M01-035 `#413`, and M01-026 `#333`
  remain blocked. This changes dependency/readiness state only and does not
  change a capability or completion gate.

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
- The verified live Gitea tracker snapshot now contains 68 labels, 16 open
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
  `#413`, and made only M01-033 ready; because it owns only new generator
  files, its focused test, and `docs/security.md`, it carries no central
  collision lane and may run beside disjoint work. M01-034, M01-035, and
  M01-026 remain blocked in order. The first leaf proves object bytes and
  relocatable unwind intent, not final-image unwindability; the second proves
  the authoritative linked PE exception directory; the third requires the
  exact `0 -> 1 -> 3 -> 7 -> 15` publication state across the one-way stack
  switch and places epoch 3 inside `CaptureArchitecturalPreflight` before its
  first generated-code invocation. The post-apply no-write preview reported
  all 424 managed objects unchanged, and independent REST verification found
  exactly one ready key, M01-033. M02 and M04
  entry work still
  depends on the final M01-026 cumulative paging proof. Only `M15-013`, the
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

## Next Steps

1. Finish UEFI platform interfaces and post-firmware diagnostics before real `ExitBootServices`.
2. Convert the opt-in ACPI SDT/MCFG probe evidence into a durable platform discovery handoff, then feed parsed ECAM metadata into a live PCI reader after physical-memory/MMIO ownership rules are defined.
3. Build live virtio-blk and virtio-net drivers on top of the existing planning contracts.
4. Add libvirt real-smoke proof and cloud image validation when host tools and live device paths are ready.
