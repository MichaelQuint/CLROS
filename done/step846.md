# Step 846 - M00-022 Source-Bound Gate 2 Reverification

## Summary

The EFI application and raw image were rebuilt from clean, pushed
implementation commit `32a8f3b91e17e1fc7e4b296730a5ea6565c38369`,
booted under real QEMU/OVMF, and bound to a deterministic machine-readable
evidence manifest. The real run started at
`2026-07-25T21:42:47.9237542Z`, completed at
`2026-07-25T21:42:56.7816592Z`, observed every required marker, observed no
forbidden marker, and emitted `TEST_PASS qemu.smoke`.

## Why

The earlier Gate 2 raw image had been overwritten and its source was a dirty
worktree. A new immutable source-to-artifact tuple was required.

## Files changed

- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `docs/efi-build.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `docs/repo-audit.md`
- `artifacts/logs/m00-final/32a8f3b91e17-20260725T213702Z/qemu/`
  (generated, not committed)

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\build-efi-shell.ps1 `
  -Configuration Release `
  -Runtime win-x64 `
  -LogPath "...\qemu\efi-publish.log" `
  -EfiOutputPath "...\qemu\BOOTX64.EFI" `
  -QemuPath "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -QemuImgPath "C:\Program Files\qemu\qemu-img.exe" `
  -OvmfCodePath "C:\Program Files\qemu\share\edk2-x86_64-code.fd"

powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\test-efi-artifact.ps1 `
  -EfiArtifact "...\qemu\BOOTX64.EFI"

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 `
  -EfiArtifact "...\qemu\BOOTX64.EFI" `
  -Image "...\qemu\clros.raw" `
  -SerialLog "...\qemu\serial.log" `
  -SummaryLog "...\qemu\qemu-summary.json" `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -WorkloadProfile SingleMinimalApi `
  -DiskFormat raw `
  -RequireMarker "OS_BOOT_OK,TEST_PASS boot.sequence,OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT,BOOT_PAGE_ALLOCATOR_READY,BOOT_ACPI_RSDP_HANDOFF_READY,UEFI_ACPI_RSDP_READY" `
  -ForbidMarker "OS_SMOKE_FAIL,TEST_FAIL"
```

## Evidence

The EFI verifier reported PE32+ machine `0x8664`, EFI application subsystem
`10`, entry RVA `0x00003730` in executable/file-backed `.managed`, same-build
association with `OS_OS_Boot_EfiEntry__EfiMain`, and absent ordinary,
bound-import, IAT, and delay-import directories.

The authoritative evidence manifest has schema version `1`, classification
`pass`, exit code `0`, source dirty `false`, 10 exact tool-version records, 23
hashed evidence inputs, 12 required markers, 2 forbidden markers, and 20
serial-derived observed markers. Its UTF-8 representation has no BOM.

Tool versions:

- Selected .NET SDK `10.0.302`.
- MSVC linker `14.51.36252.0`.
- Windows SDK `10.0.28000.0`.
- ILCompiler and NativeAOT runtime pack `10.0.8`.
- QEMU and qemu-img `11.0.50`
  (`v11.0.0-12631-g54e84cdc7a`).
- OVMF identifies itself as `edk2-stable202408-prebuilt.qemu.org`.

Required markers:

```text
OS_BOOT_OK
TEST_PASS boot.sequence
OS_SMOKE_PASS
UEFI_MEMORY_MAP_OWNED
BOOT_HANDOFF_READY
UEFI_MEMORY_MAP_NORMALIZED
BOOT_MEMORY_MAP_ACCEPTED
BOOT_MEMORY_REGIONS_READY
BOOT_MEMORY_REGION_COUNT
BOOT_PAGE_ALLOCATOR_READY
BOOT_ACPI_RSDP_HANDOFF_READY
UEFI_ACPI_RSDP_READY
```

Forbidden markers `OS_SMOKE_FAIL` and `TEST_FAIL` were absent. The QEMU
summary records `MISSING_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
`FAIL_REASON none`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `qemu/BOOTX64.EFI` | 54,272 | `0580A45FC10B93C7C218CAB9BA179549C278B4CA970C82420CE74684EDA6036F` |
| `qemu/BOOTX64.EFI.link.json` | 1,816 | `DA194DBDA450407CE87FC83A5BA01E07C7318D8D342255AF444DAA01C08CB1DF` |
| `qemu/BOOTX64.EFI.link.map` | 31,913 | `64375B6CC0C2A900289FE97F7C7013E3356F22003B9B7011AAC04C631A9FDFEE` |
| `qemu/BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `qemu/qemu-command.txt` | 1,003 | `E8DD5D28DD5F0C8C50D16C7CC4D76A8B32C1A536EF76CC8024910D9342BAC665` |
| `qemu/clros.raw` | 67,108,864 | `484A7B8D2C9E9C01BFF6AAA63633E3CEC3406A0FBAC487100DFB40B0A463428A` |
| `qemu/serial.log` | 7,947 | `C190355FB48902635AB4BC7EBD4BAEBD3E029619C851407A599D3BC9FB412BC7` |
| `qemu/qemu-summary.json` | 2,571 | `178B8F9204173C30B2C7D8CE24B7082AE591D3FF6C761453EB084B5F53D53D86` |
| `qemu/qemu-smoke.stdout.log` | 4,322 | `31B7E8B2B60CB18BCC95B1402F9B1DFE91BF0918E46887FF4F9F97DB3D3625DE` |
| `qemu/evidence-manifest.json` | 9,989 | `D398F73FF5051D138D5F2A77CE4EF51277BD2DE289F58BA5383FC6FC74D02E65` |

The manifest also binds the host baseline, OVMF, QEMU, qemu-img, linker,
ILCompiler, QEMU runner, EFI publish/verification logs, and the two superseded
manifest-attempt records by verified SHA-256.

## Failures

The superseded Gate 2 raw-image hash no longer identifies bytes preserved at
its historical working path. It was not silently rehashed.

The first manifest invocation used enum spelling `Passed`; the CLI correctly
rejected it because its public values are `pass`, `fail`, and `timeout`. The
next passing manifest recorded requested SDK band `10.0.300` instead of the
actually selected `10.0.302` patch. Both attempts were preserved under the
evidence root and hashed into the corrected authoritative manifest. Neither
superseded attempt is cited as accepted evidence.

## Deferred work

All post-firmware and later subsystem gates remain open.

## Evidence boundary

This step proves only that the stated OVMF/QEMU/toolchain tuple loads the EFI
application built from the stated commit and that the current
firmware-visible path emits the accepted markers. It does not prove real
`ExitBootServices`, owned COM1, live CR3/descriptors, drivers, workloads,
hosted .NET, Kestrel, cloud support, CloudReady status, or production
readiness.
