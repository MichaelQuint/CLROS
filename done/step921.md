# Step 921 - Populate and read back all 256 owned IDT gates in EFI

## Summary

Integrated the frozen M02-011 owned-IDT materializer into one default-off,
no-stdlib NativeAOT EFI profile and preserved fresh host, same-build artifact,
and real QEMU/OVMF evidence.

The profile allocates one contiguous three-page `EfiLoaderData` arena before
ordinary UEFI memory-map capture. Page zero receives the frozen M02-010 set of
256 normalized 16-byte entry slots, page one receives all 256 fresh M02-011 x64
IDT gate descriptors, and page two supplies the nonexecuted modeled common
target at delta 8,192. The integration validates the complete arena address,
materializes and reads back both produced pages through the frozen production
implementations, audits every result field and fingerprint, and releases the
exact returned base exactly once before it can emit ready/pass records.

The accepted M02-011 identity is 256 present DPL0 interrupt gates, selector
`0x0008`, IST3 for NMI, IST1 for double fault, IST2 for page fault, IST0 for the
other 253 vectors, 256 unique handlers inside the owned M02-010 page, zero
copied firmware gates, M02-010 FNV-1a64 `641BA73668C75E0E`, M02-011 normalized
policy FNV-1a64 `8514F42EBDFC5D55`, and independent page-zero SHA-256
`650F6D5EB96B0E625C2FF5FB852C0A67C6CE68D0CB92CD4B137E4DBC28636630`.

This is temporary byte population, readback, cleanup, and marker evidence only.
The profile does not publish executable memory, execute a generated entry,
inspect or copy a firmware IDT, execute `sidt`/`lidt`, load IDTR, install active
GDT/TSS/IST state, trigger a fault, call `ExitBootServices`, or run after the
firmware boundary. Gate 2 remains the only passing numbered gate; Gate 3 and
Gate 4 remain `Fail`.

## Why

PR #493 merged the reviewed four-path M02-011 host leaf, but its stable marker
correctly reported `guestPopulation=false`. Issue #494 owns the separate,
serialized EFI-entry integration required to cross the guest population,
readback, and release boundary without expanding the frozen leaf or conflating
owned descriptor bytes with an active interrupt subsystem.

This slice also closes the bounded dependency represented by issue #51. It is
the prerequisite for issue #52 / M02-012, which separately owns loading IDTR
and proving continued execution.

The SharpOS upstream refresh informed only the general idea of deterministic C#
descriptor/stub byte construction. No SharpOS source was copied. CLROS did not
adopt copied firmware vectors 32-255, the SharpOS selector `0x38`, or its
`sidt`/`lidt` installation approach.

## Files changed

- `src/OS/OS.csproj`
  - Adds the default-false `EnableM02OwnedIdtGatePopulationProbe` property and
    exact `M02_OWNED_IDT_GATE_POPULATION_PROBE` symbol.
  - Links the frozen M02-011 source immediately after M02-010 and before both
    M04 conformance sources.
  - Rejects the standalone M02-010 and terminal M02-009 profile collisions.
- `src/OS/Boot/EfiEntry.cs`
  - Runs the optional profile immediately before ordinary memory-map capture.
  - Owns the one-allocation/three-page address preflight, four production
    operations, result audits, exactly-once release, and exact guest records.
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
  - Carry and verify the new typed switch, exact 61-item compile firewall,
    exact 74-input source authority, profile collisions, same-build evidence,
    and exact 17-required/five-ordered/three-unique/three-forbidden runner
    profile.
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`
  - Verifies the complete project/script/source contract.
  - Builds real default and enabled NativeAOT EFI artifacts, runs the
    same-build verifier for both, proves default exclusion and enabled map
    inclusion, and validates all ordered source-authority paths and hashes.
  - Mirrors the EFI ownership flow in page-aligned unmanaged memory while
    calling both frozen production leaves. It covers missing services,
    allocation failure, five invalid returned-address classes, independent
    page-zero/page-one result failures, independent readback corruptions,
    release failure, exact-address cleanup, and success.
  - Pins the default, standalone M02-011, legacy M02-010, and combined
    M04-002/M04-003/M02-011 dry-run profiles plus hostile transcript cases.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
  - Mechanically refresh the exact compile/source closure and deterministic
    source ordering while retaining their earlier behavioral identities.
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `docs/qemu.md`
  - Record implemented behavior, commands, evidence, inapplicable firmware-map
    statuses, SharpOS reference limits, unsupported behavior, and next steps
    without advancing a completion gate.
- `done/step921.md`
  - Preserves this evidence ledger after the tested implementation commit.

The frozen files
`src/OS.Kernel/Diagnostics/NormalizeErrorCodeNonErrorCodeInterruptFrame.cs` and
`src/OS.Kernel/Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.cs` were
verified unchanged. `BootSequence.cs`, `Probes.cs`, `README.md`,
`docs/completion-gates.md`, and `references/**` were not changed.

## Tests run

At exact tested implementation commit
`e1289c4b29ec1417e51dde71d2cc6bb524cfd4c5` and tree
`012572a324a92dc120080286272d0112af927716`:

- `dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers`
  - Exit `0`; zero warnings and zero errors; 12.73 seconds.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 --disable-build-servers`
  - Exit `0`; zero warnings and zero errors; 11.24 seconds.
- Focused M02-011 module execution under the ignored
  `artifacts/m02-011-focused` harness:
  - Exit `0`.
  - Both real default and enabled NativeAOT builds exited `0`.
  - Both same-build artifact-verifier executions passed.
  - Default exclusion, enabled inclusion, provenance, fake ownership, QEMU
    profiles, and hostile transcript groups all passed.
- Direct full-host execution of
  `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe`:
  - Direct process exit `0`.
  - 3,483 `TEST_START`, 3,484 `TEST_PASS`, zero `TEST_FAIL`, and exactly one
    terminal `TEST_PASS host.tests`.
  - 2,333.424 seconds from log creation through final write.
  - The complete accepted log is 448,738 bytes and 7,027 lines with SHA-256
    `D4831A01E7028A3E51DA2F115538DD8046129C0D2A16AEEF659816D79907C640`.
- `.\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- Windows PowerShell 5.1 AST parsing of all three changed scripts:
  - `POWERSHELL_5_1_PARSE_OK scripts=3`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
  - Exit `0`; exact default 12/0/0/2 profile retained.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM02NormalizedInterruptFramePageProbe`
  - Exit `0`; frozen standalone M02-010 profile retained.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM02OwnedIdtGatePopulationProbe`
  - Exit `0`; exact M02-011 17/5/3/3 profile.
- Combined M04-002/M04-003/M02-011 dry run:
  - Exit `0`; exact order `EFI_CONSOLE_READY`, M04-002 ready, M04-003 ready,
    M02-011 start/ready/pass, then `UEFI_MEMORY_MAP_READY`.
- `git diff --check`
  - Exit `0` before the implementation commit and after documentation edits.
- `git status --short`
  - Empty immediately after the tested implementation commit.

Real NativeAOT EFI/QEMU command:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02OwnedIdtGatePopulationProbe `
  -EfiArtifact .\artifacts\efi\BOOTX64-M02-011-FINAL-QEMU.EFI `
  -Image .\artifacts\images\m02-011-final-qemu.raw `
  -SerialLog .\artifacts\logs\m02-011-final-qemu-serial.log `
  -SummaryLog .\artifacts\logs\m02-011-final-qemu-summary.log
```

- Outer exit `0`.
- Same-build NativeAOT publish, schema-three artifact verification, raw-image
  construction, QEMU/OVMF marker evaluation, and summary generation all
  emitted terminal passes.
- Streaming parser result:
  `status=Passed terminal=Success bytes=8430 lines=29 invalidBytes=207 invalidLines=2 orderedSeen=5 outOfOrder=0 uniqueSeen=3 duplicateUnique=0`.
- QEMU was runner-terminated only after success:
  `disposition=runner-terminated exitCode=-1`.
- The ordinary boot continued through `OS_BOOT_OK`,
  `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.

## Evidence

### Source identity

- Required base/parent:
  `b5c289124a964b00c285a9b66f2b4cd4b026201f`.
- Tested implementation commit:
  `e1289c4b29ec1417e51dde71d2cc6bb524cfd4c5`.
- Tested implementation tree:
  `012572a324a92dc120080286272d0112af927716`.
- Stable base-to-implementation patch ID:
  `d7253094edf2bbbbce41f4d3b992e9a62f32b29c`.
- Implementation commit changed exactly 23 issue-owned paths: 2,822 insertions
  and 147 deletions.

Key tested file identities:

| Path | Bytes | Git blob | SHA-256 |
| --- | ---: | --- | --- |
| `src/OS/Boot/EfiEntry.cs` | 572,838 | `b1d2afb5fbe40cd56b5a7d71f112c03945701b4e` | `EBDFCFA71377045E271722EEA786900B5060137347B9322392ED465DC496C9CD` |
| `src/OS/OS.csproj` | 20,892 | `23d5fa5ef5a479aeff861d1aba505a4d1f4772d9` | `3FFB00E8BA1EBEAD05233CE2EEE860CF1AB6BAF8721B8BD29A2AB958F1C7B994` |
| `scripts/build-efi-shell.ps1` | 350,337 | `f453356a0321b307d17f574583af206d458892ab` | `3A28D702995D3C104F02832E509E57430AE6E06DC0EF82DE29973B6BDCE29EBE` |
| `scripts/test-efi-artifact.ps1` | 267,733 | `b9eb5f9d379c773c314067cce4c701ab194369c4` | `9A92C91CB5062939FCDF2F91363A278B05E541AB22078E3CFC59BE9F5A3D95C1` |
| `scripts/run-qemu-smoke.ps1` | 184,794 | `35d20d6b7741984e1315544f6d33d35c02d37d01` | `C1FFD2982232C8C0814965FE9956C27552108737E6E8573A1FAE0030805E23FE` |
| `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs` | 74,270 | `9f4e166cd3aab7e6be9eead762a9ed6e34f578fe` | `593EFDD70B8DD2579EDA8A1E367AC8241F721622FE53EE8844E00FE179257B91` |

Frozen leaf identities verified by the host test:

- M02-010 production SHA-256:
  `B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1`.
- M02-011 production SHA-256:
  `00EC277C3F6A0DB49CB54B34810202EE1D508318B29733C3F0392A7BC36CFA38`.
- M02-011 host-leaf test SHA-256:
  `F7C4C19A643FCD8010B0AE69870A9B6BF129EEC2AA32A53AF867C182C31A2C70`.

### Same-build NativeAOT closure

- Schema `3`; configuration `Release`; RID `win-x64`.
- .NET SDK `10.0.302`; NativeAOT runtime pack `10.0.8`.
- Exactly 61 compile items with compile-key SHA-256
  `413DDC044A3469F2F0AC51C1190609EDEAA8FBAC0D90BEB41AB2648559F6549E`.
- Exactly 74 ordered source-authority inputs with closure SHA-256
  `2D24A9B956642DDE73162A34DC0E84A426B92E70D39A21B064003ABDE9171240`.
- Zero project references and zero ordinary references.
- Raw `OS.csproj` SHA-256
  `3FFB00E8BA1EBEAD05233CE2EEE860CF1AB6BAF8721B8BD29A2AB958F1C7B994`.
- Canonical project-shape SHA-256
  `3F57CBB5DE5561C86A0AD1809534BD5D0E6DB8D8086BF667BA724666413F7181`.
- `EfiEntry.cs` SHA-256
  `EBDFCFA71377045E271722EEA786900B5060137347B9322392ED465DC496C9CD`.
- Artifact verifier accepted PE32+ AMD64, EFI subsystem 10, entry RVA
  `0x00005F30`, 145 exception/runtime records, same-build `EfiMain` and
  serializer bindings, and no ordinary, bound, IAT, or delay imports.

Final real-QEMU artifact tuple:

| Artifact | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| EFI | 72,192 | n/a | `433B087FB4D0ADBDE01DFF47F7DCB7DE7E061974ED7329138A5AD0B8250255ED` |
| Serializer object | 336 | n/a | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Link map | 42,436 | n/a | `28A7CD7615E654E8C68DAE24B6865B1CB211B8DE3E5275BD26E35A09B3F719FD` |
| Link response | 500 | 15 | `156EB9321ED154D124A4E4CA8403DA23EE94F01836D430DC6AA3458BACD22103` |
| Link-evidence JSON | 398,988 | 5,159 | `7463CEA0F859FF35DE5B89CD887B78D43BA6C25FEE76DA86DD54004001B93077` |
| Raw image | 67,108,864 | n/a | `92F0C6D93B66169CFA44216C4B045BA7EE7D49019E77549ED4A73F95BC9DFB26` |
| Serial log | 8,430 | 29 | `01CBB7C9179DB397FBD6C8E32D7A400667FEEE08119198F160966B661DF74600` |
| Summary log | 4,648 | 49 | `C6E14CBC5A21CC733137B382D6F6B23C0C885044CE0D3D158393A950923BACC9` |

Execution environment:

- QEMU executable: 25,272,968 bytes; SHA-256
  `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
- OVMF code: 3,653,632 bytes; SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

### Accepted guest transcript

The following three records were observed exactly once, in order, between
`EFI_CONSOLE_READY` and `UEFI_MEMORY_MAP_READY`:

```text
TEST_START m02.011
CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 tableBytes=4096 selector=8 present=256 dpl0=256 interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 nmiIst=3 doubleFaultIst=1 pageFaultIst=2 normalizedFnv1a64=8514F42EBDFC5D55 readBack=true guestPopulation=true loaderData=true allocationPages=3 allocationReleased=true stubExecution=false idtrLoaded=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.011
```

The parser found no `OS_SMOKE_FAIL`, no `TEST_FAIL`, no
`M02_OWNED_IDT_GATE_POPULATION_FAILED`, no missing marker, no out-of-order
marker, and no duplicated unique marker.

### Accepted host integration records

```text
M02_011_ARTIFACT_BUILD_AUDIT defaultExit=0 enabledExit=0 defaultExcluded=true enabledIncluded=true verifierPassed=true provenanceExact=true
M02_011_FAKE_AUDIT successReady=true successPass=true successFailure=false stubFnv=641BA73668C75E0E normalizedFnv=8514F42EBDFC5D55 stubSha256=650F6D5EB96B0E625C2FF5FB852C0A67C6CE68D0CB92CD4B137E4DBC28636630 slots=true descriptors=true handlers=true thirdPage=true prerequisites=true allocateFailure=true invalidAddresses=true page0ResultFailure=true page1ResultFailure=true stubReadBackMismatch=true idtReadBackMismatch=true releaseFailure=true
CLROS_M02_011_EFI_HOST_READY switchDefault=false sourceLinked=true allocationPages=3 cleanup=true markerOrder=true noExecution=true
TEST_PASS m02.011.efi
```

The complete host log is retained at
`artifacts/logs/m02-011-final-hosttests-r3.log`; artifacts remain ignored and
are not committed.

## Failures

- The first aggregate attempt used a managed `byte[]` as the fake EFI arena.
  The production preflight correctly rejected two cases because a managed array
  is not guaranteed to be 4,096-byte aligned. That aggregate was rejected; the
  fake now uses `NativeMemory.AlignedAlloc`/`AlignedFree`.
- A later aggregate produced all expected pass markers, but the outer
  `Start-Process -Wait` wrapper remained hung after the child exited and did not
  preserve a trustworthy numeric child exit. That run is supplemental only.
  The accepted r3 run invoked the native host-test app directly and captured
  exit `0`.
- A superseded r2 aggregate was intentionally interrupted after independent
  review found incomplete regression assertions. It is not accepted evidence.
- The first focused default/enabled NativeAOT run failed after preflight because
  Windows PowerShell launched from the .NET host could not auto-load
  `Get-FileHash`. The host test now prepends the inbox Windows PowerShell 5.1
  module directory to `PSModulePath`; the focused and complete aggregate reruns
  both passed.
- Independent review initially found incomplete exact-address cleanup checks,
  missing distinct page-zero/page-one result-failure cases, incomplete
  firmware-IDT token rejection, length-only required-marker checking, absent
  combined/host-only/legacy transcript cases, incomplete default/enabled
  artifact provenance, and two missing documentation boundaries. All were
  corrected before the accepted implementation commit and final evidence.
- No accepted real-QEMU, artifact-verifier, build, naming, source-firewall, or
  final host-aggregate failure remains.

## Deferred work

- Issue #52 / M02-012 must separately load IDTR and prove continued execution.
- Later issues must publish and protect executable entry code, install active
  GDT/TSS/IST state, route live interrupts, capture a controlled fault, and
  establish panic-safe post-firmware diagnostics.
- Real `ExitBootServices`, owned scheduler/threading, storage, live networking,
  hosted .NET 10, Kestrel/Minimal API execution, cloud-provider images,
  CloudReady status, security hardening, and production readiness remain
  unproven.
- `EFI_BUFFER_TOO_SMALL`, memory-map descriptor growth, stale map keys, and
  `ExitBootServices` retry exhaustion are inapplicable to this probe because it
  allocates, verifies, and frees before ordinary map capture and never calls
  `ExitBootServices`.

## Evidence boundary

This step proves that real guest `EfiLoaderData` held the frozen M02-010
normalization slots and all 256 freshly owned M02-011 IDT gate bytes, that both
pages passed production readback and exact result audits, that zero firmware
gates were copied, that the exact three-page allocation was successfully
released before ordinary memory-map capture, and that the ordinary smoke path
continued afterward.

It does not prove executable entry publication, an active IDT, IDTR/GDT/TSS/IST
installation, interrupt or fault delivery, firmware exit, hosted CoreCLR/.NET,
NativeAOT Minimal API execution on CLROS, cloud-provider support, cloud
readiness, security hardening, or production readiness. Gate 2 remains the only
passing numbered completion gate; Gate 3 and Gate 4 remain `Fail`.
