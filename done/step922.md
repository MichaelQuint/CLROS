# Step 922 - Load, read back, restore IDTR, and continue in EFI

## Summary

Integrated the frozen M02-012 reversible-IDTR leaf into one default-off,
no-stdlib NativeAOT EFI profile and preserved host, same-build artifact, and
real QEMU/OVMF processor evidence.

The returning profile runs after any returning M04 conformance profile and
before ordinary UEFI memory-map capture. It allocates exactly two contiguous
`EfiLoaderCode` pages and one disjoint `EfiLoaderData` page. Code page zero
receives the frozen M02-010 normalized interrupt-entry slots, code page one
receives the common return target plus the frozen 52-byte M02-012 transition,
and the data page receives the frozen M02-011 owned IDT. An 80-byte,
16-byte-aligned transition context remains in caller-owned stack storage.

The transition captures entry IF and the firmware IDTR, executes `cli`, loads
and reads back the temporary owned IDTR, restores and reads back the firmware
IDTR, restores IF when required, returns cookie `0x4F53494454523132`, and then
continues through the ordinary boot path. Before any C# or firmware callback,
the adapter independently requires the exact loaded descriptor, byte-exact raw
restoration, a previous IDTR distinct from the owned IDT, and entry/return IF
equality. An uncertain restoration takes a callback-free spin. A verified
return then passes the full production audit, releases data before code, emits
the exact M02-012 ready/pass records, and continues through `OS_SMOKE_PASS`.

The accepted real QEMU/OVMF run observed all 17 ordered records, all six unique
records exactly once, no forbidden record, no reordering, and ordinary-smoke
continuation. This is bounded pre-`ExitBootServices` evidence on the recorded
virtual platform. It is not interrupt or fault-delivery evidence, does not
establish a persistent IDT, active GDT/TSS/IST routing, W^X/NX, real firmware
exit, hosted .NET 10, Kestrel, CloudReady status, or production readiness. Gate
2 remains the only passing numbered gate; Gate 3 and Gate 4 remain `Fail`.

## Why

PR #498 merged the reviewed M02-012 host leaf, but its marker correctly
reported `guestExecution=false`, `idtrLoaded=false`, `idtrRestored=false`, and
`continuedExecution=false`. Gitea issue #497 owns the separate serialized EFI
integration required to cross that guest execution boundary without expanding
the frozen leaf or conflating a temporary reversible transition with a
functional interrupt subsystem. Parent issue #52 remains open until this guest
evidence, merge identity, and backlog reconciliation are posted and verified.

The 2026-08-26 SharpOS refresh reviewed upstream main at
`0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`, 34 commits beyond the archived
reference through its step 165. Its deterministic C# `sidt`/`lidt` byte-stream
work reinforced the usefulness of a small reversible descriptor-table proof.
No SharpOS source was copied, and CLROS did not adopt SharpOS as its identity,
architecture, permanent IDTR policy, selector layout, runtime, or boot model.

## Files changed

- `src/OS/OS.csproj`
  - Adds default-false `EnableM02IdtrContinuedExecutionProbe` and exact symbol
    `M02_IDTR_CONTINUED_EXECUTION_PROBE`.
  - Links the frozen M02-012 source after M02-010/M02-011.
  - Excludes the nonreturning M02-009 and standalone M02-010/M02-011 profiles
    while retaining returning M04 composition.
- `src/OS/Boot/EfiEntry.cs`
  - Owns the exact two-call code/data allocation layout, range preflight,
    materialization, production readback, publication, unmanaged invocation,
    raw restoration gate, production audit, reverse cleanup, and exact guest
    records.
  - Rejects zero, misaligned, noncanonical, wrapping, top-ending,
    canonical-hole, equal, and partially overlapping allocation ranges before
    dereference.
  - Spins without callbacks when post-invocation safety cannot be established
    and suppresses double-free-capable cleanup on hostile overlap.
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
  - Carry and verify the typed switch across build, schema-three artifact
    evidence, dry-run, and real-QEMU paths.
  - Enforce the exact 62-item compile firewall, 75-input source authority,
    profile collisions, full 17-record order, six unique records, and five
    forbidden records.
  - Normalize user marker inputs and reject owned M02-012 marker prefixes unless
    the typed profile is active.
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`
  - Builds real default and enabled NativeAOT artifacts and applies the
    same-build verifier.
  - Source-binds allocation, materialization, result fingerprints,
    publication/invocation order, raw safety, audit, cleanup, and callback-free
    unsafe-return behavior.
  - Exercises hostile fake firmware without invoking privileged bytes.
  - Executes build/verifier collision cases, returning-M04 composition, hostile
    argument cases, and missing/reordered/duplicate/forbidden transcripts.
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
  - Teaches the historical profile selector to model the new M02-012 switch as
    independently disabled, preserving the four-call M01 publication contract.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
  - Mechanically refresh exact project/source authority and deterministic source
    ordering while preserving earlier behavioral identities.
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `docs/qemu.md`
  - Record the implemented flow, exact commands and artifacts, accepted guest
    tuple, SharpOS reference boundary, unsupported behavior, and unchanged
    completion gates.
- `done/step922.md`
  - Preserves this evidence ledger after the tested implementation commit.

The frozen files
`src/OS.Kernel/Diagnostics/IDTRContinuedExecution.cs`,
`src/OS.Kernel/Diagnostics/NormalizeErrorCodeNonErrorCodeInterruptFrame.cs`,
and `src/OS.Kernel/Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.cs`
were verified unchanged. `BootSequence.cs`, `Probes.cs`, `README.md`,
`docs/completion-gates.md`, `.gitea/backlog/**`, and `references/**` were not
changed by the implementation commit.

## Tests run

At exact tested implementation commit
`39c2e216c87708b5a4726950809b473739b2d068` and tree
`a154c0a2d02f5df16fbfb1f76c0e98e332dbc4e8`:

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit `0`; zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  followed by `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj
  --no-build`
  - Build exit `0`; zero warnings and zero errors.
  - Final full execution exit `0` after `2425.887` seconds; terminal
    `TEST_PASS host.tests` occurred exactly once.
  - The final log is `450,913` bytes across `7,047` lines with `3,491`
    `TEST_START` records, `3,492` `TEST_PASS` records, zero `TEST_FAIL`
    records, and SHA-256
    `9D1E375E0CB672B4767F810D092CEE442560913AB743D691B830248E108C3EE9`.
- Focused M01.035 plus M02-012 execution under ignored
  `artifacts/m02-012-focused`:
  - Exit `0` after the final documentation update.
  - All M01.035 historical profiles and all six M02-012 integration groups
    passed.
  - Both real default/enabled NativeAOT builds and same-build verifier runs
    exited `0`.
- `.\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- PowerShell AST parsing of all three changed scripts and XML parsing of
  `src/OS/OS.csproj`:
  - `SCRIPT_PARSE_PASS files=3` and `XML_PARSE_PASS src/OS/OS.csproj`.
- `.\scripts\build-efi-shell.ps1`
  - Exit `0`; default NativeAOT publish and artifact verifier passed.
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
  - Exit `0`; unchanged default marker profile.
- M02-012 typed dry run with explicit exact ready marker:
  - Exit `0`; `profileRequired=17`, `profileOrdered=17`,
    `profileUnique=6`, and `profileForbidden=5`.
- Combined M04-002/M02-012 typed dry run:
  - Exit `0`; M04-002 remains between `EFI_CONSOLE_READY` and the M02-012
    start record while the full M02-012 acceptance profile remains intact.
- `git diff --check`
  - Exit `0` before the implementation commit and after evidence-documentation
    edits.

Real NativeAOT EFI/QEMU command:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02IdtrContinuedExecutionProbe `
  -EfiArtifact .\artifacts\efi\BOOTX64-M02-012-IDTR-QEMU.EFI `
  -Image .\artifacts\images\m02-012-idtr-qemu.raw `
  -SerialLog .\artifacts\logs\m02-012-idtr-qemu-serial.log `
  -SummaryLog .\artifacts\logs\m02-012-idtr-qemu-summary.log `
  -RequireMarker "CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 idtrLimit=4095 policyFnv=8514F42EBDFC5D55 firmwareGatesCopied=0 firmwareIdtrCaptured=true ownedIdtrLoaded=true ownedIdtrReadBack=true firmwareIdtrRestored=true restoreReadBack=true ownedIdtrActive=false continuedExecution=true cliWindow=true ifRestored=true instructionPublished=true allocationsReleased=true wxEnforced=false interruptTriggered=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false"
```

- Outer exit `0`.
- NativeAOT publish, schema-three same-build verification, raw-image creation,
  QEMU/OVMF marker evaluation, and summary generation all passed.
- Streaming parser result:
  `status=Passed terminal=Success bytes=8462 lines=29 invalidBytes=207 invalidLines=2 orderedSeen=17 outOfOrder=0 uniqueSeen=6 duplicateUnique=0`.
- QEMU was runner-terminated only after success:
  `disposition=runner-terminated exitCode=-1`.
- The ordinary boot continued through `OS_BOOT_OK`,
  `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.

## Evidence

### Source identity

- Required base/parent:
  `799719dc5a398e646d6c224c9bf6fd8793145069`.
- Tested implementation commit:
  `39c2e216c87708b5a4726950809b473739b2d068`.
- Tested implementation tree:
  `a154c0a2d02f5df16fbfb1f76c0e98e332dbc4e8`.
- Stable base-to-implementation patch ID:
  `c74f47e6b5ad5ac4b560eb41b4690bee246b2576`.
- The implementation commit changed exactly 25 issue-owned paths: 4,150
  insertions and 130 deletions.

Key tested file identities:

| Path | Bytes | Git blob | SHA-256 |
| --- | ---: | --- | --- |
| `src/OS/Boot/EfiEntry.cs` | 614,116 | `81a54e41df4e2ae4a858025757a57adc00691477` | `FA3F7896078F024B6F5D5300E806B24AE6B739184C39A7292E24382F46E082B8` |
| `src/OS/OS.csproj` | 22,101 | `d1e047c55f4a6cb4def9cad4a71bbce5f597e020` | `19831A8A437D6003B51D1E89B6B26325C17417EB38B869CE01C18C04C5952BAC` |
| `scripts/build-efi-shell.ps1` | 354,950 | `ecab0bb02c4c5c3f00372a76955098abc568131d` | `6B39EBE37FE7B8F1FD88698C4A55427688BB1C78643EFE0772CCB3A517D9320F` |
| `scripts/run-qemu-smoke.ps1` | 191,664 | `ef1c700be128874584bcf8c71d7443f7b1459dd6` | `209ABFCF660FC08B11497971AA0A523B04264CD45F3E6D8B12C824081CDC9A81` |
| `scripts/test-efi-artifact.ps1` | 268,777 | `4dc20efedee9dce7ccce2fc6ba7fb7e76959b9bc` | `989A22CBEA367DABCD03EDCB096D2F5A91A987A726AB909CE5CAABF8EE31047B` |
| `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs` | 100,278 | `0479af575b98ec332320480aa932c7a1adee059b` | `0ED888620F8EE6A51C2381A1F4E35F1B55773BFCDE33353DEFE612104C3A9DBD` |
| `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs` | 157,282 | `02114a958e4e9504785271820a5cc380dc939a56` | `3BE1C8B6C44AD6D8FE51686DEAE9C0232997ED67376B7A1BF14E7816FF7E6E0E` |

Frozen identities verified by the focused test:

- M02-012 production leaf SHA-256:
  `7CBF5DD9FB17D8D9608237FC78D06FE00B40D7AFE8669E1196C72B9F1B6848EB`.
- M02-012 host-leaf test SHA-256:
  `E9F296DE66CB3FC5ED29C565DDBC99A596EC9D3188019F6E810795821C6A2CD1`.
- Integrated transition FNV-1a64: `AFF59D6480DE7F3C`.
- M02-011 normalized policy FNV-1a64: `8514F42EBDFC5D55`.

### Same-build NativeAOT closure

- Schema `3`; configuration `Release`; RID `win-x64`.
- `PublishAot=true`; `SelfContained=true`; `PublishTrimmed=true`.
- .NET SDK `10.0.302`; NativeAOT runtime pack `10.0.8`.
- Exactly 62 compile items with compile-key SHA-256
  `9B243B135C05442631A0323654FDBE07C827F043703330D8BAA9E83A78A21757`.
- Exactly 75 ordered source-authority inputs with closure SHA-256
  `F1543BCDE0DF38127068CF692188A8510DF9E41A61E49FA5CC5A90C785C37A26`.
- Zero project references and zero ordinary references.
- Raw `OS.csproj` SHA-256
  `19831A8A437D6003B51D1E89B6B26325C17417EB38B869CE01C18C04C5952BAC`.
- Canonical project-shape SHA-256
  `DE242129BD59B0358211B606F666C0D99F65AB2E23096DA47EC9B182E7D2748E`.
- `EfiEntry.cs` SHA-256
  `FA3F7896078F024B6F5D5300E806B24AE6B739184C39A7292E24382F46E082B8`.
- Enabled evidence records
  `EnableM02IdtrContinuedExecutionProbe=true` and all mutually exclusive M02
  switches `false`.
- Artifact verifier accepted PE32+ AMD64, EFI subsystem 10, entry RVA
  `0x00007E70`, 187 exception/runtime records, same-build `EfiMain` and
  serializer bindings, and no ordinary, bound, IAT, or delay imports.

Final real-QEMU artifact tuple:

| Artifact | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| EFI | 87,040 | n/a | `D7428017EB461F8678C5C6793DA9C8F6A9E6CFD2456DDACB23D1161D1759A69C` |
| Serializer object | 336 | n/a | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Link map | 53,077 | n/a | `EFCC25EF19688C2F497763D902F6279FF1B665094A044A02B9E6FD3AB2E6ED00` |
| Link response | 500 | 15 | `759617E9619E340C4016B730A32D98D5F4CABED9F61BBE0938B1169F2170BDBC` |
| Link-evidence JSON | 399,378 | 5,164 | `AAABD5CACF4123F10B273CB80899371C429EDC7216A81FEBAEDA942DF40513DD` |
| Raw image | 67,108,864 | n/a | `1757D1D647103212CAC8CAD7082D64BBC30C6632DE559EA39683D90521A236FC` |
| Serial log | 8,462 | 29 | `4E97D1D61EAF5FCB5371F05889734D8A8103C1C3CD0126B5E0A6A5519A8947C6` |
| Summary log | 5,844 | 49 | `6302E3FC51D4869F017E54836BE41F8C99A2F2CAAE3F000E61854D7947C69669` |

Default artifact control:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| EFI | 55,808 | `A70718FFA723808966005005A366340A7A7B7F02FF43B6952B7D1B51ACA94B5A` |
| Link map | 32,604 | `12A1432BA0B9A679A8234B02331A7B25CBA488FD4FF6E3241F36698B618AFF3E` |
| Link response | 500 | `759617E9619E340C4016B730A32D98D5F4CABED9F61BBE0938B1169F2170BDBC` |
| Link evidence | 229,244 | `EBA5E48AD7F6F67FF2E7AF2685B02FF8F40C492C5C54668CA89F621BD6E5A7C4` |

Execution environment:

- QEMU `11.0.50 (v11.0.0-12631-g54e84cdc7a)` executable: 25,272,968
  bytes; SHA-256
  `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
- OVMF code: 3,653,632 bytes; SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

### Accepted guest transcript

The following three M02-012 records were observed exactly once and in order
between `EFI_CONSOLE_READY` and `UEFI_MEMORY_MAP_READY`:

```text
TEST_START m02.012
CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 idtrLimit=4095 policyFnv=8514F42EBDFC5D55 firmwareGatesCopied=0 firmwareIdtrCaptured=true ownedIdtrLoaded=true ownedIdtrReadBack=true firmwareIdtrRestored=true restoreReadBack=true ownedIdtrActive=false continuedExecution=true cliWindow=true ifRestored=true instructionPublished=true allocationsReleased=true wxEnforced=false interruptTriggered=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.012
```

The parser then observed the complete memory-map/handoff/normalization/allocator/
ACPI sequence followed by `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and
`OS_SMOKE_PASS`. It found no `OS_SMOKE_FAIL`, `TEST_FAIL`,
`M02_IDTR_CONTINUED_EXECUTION_FAILED`, host-only M02-012 record, inherited
standalone-M02-011 record, missing record, out-of-order record, or duplicated
unique record.

### Accepted host integration records

```text
M02_012_ARTIFACT_BUILD_AUDIT defaultExit=0 enabledExit=0 defaultExcluded=true enabledIncluded=true verifierPassed=true provenanceExact=true
M02_012_TRANSCRIPT_AUDIT success=Passed missing=OutOfOrderMarker reordered=OutOfOrderMarker duplicateStart=DuplicateUniqueMarker duplicateReady=DuplicateUniqueMarker duplicatePass=DuplicateUniqueMarker duplicateOsBoot=DuplicateUniqueMarker duplicateBootPass=DuplicateUniqueMarker duplicateSmokePass=DuplicateTerminalMarker reorderedOrdinary=OutOfOrderMarker nonExact=OutOfOrderMarker hostOnly=ForbiddenMarker standalone=ForbiddenMarker failureSentinel=ForbiddenMarker failureTest=ForbiddenMarker smokeFailure=ForbiddenMarker
CLROS_M02_012_EFI_HOST_READY switchDefault=false sourceLinked=true codePages=2 dataPages=1 contextBytes=80 cleanup=true markerOrder=true guestExecution=false
TEST_PASS m02.012.efi
```

Generated artifacts remain ignored and are not committed.

## Failures

- The implementation agent's first broad host run was rejected when source
  authority changed before its lock. It exposed 61/74-era hash pins that needed
  a coordinated 62/75 refresh.
- A subsequent broad run was intentionally interrupted after independent review
  found stale project hashes and a runner contract that retained only five
  ordered/three unique records. That attempt is not accepted evidence.
- The first corrected focused run rejected the terminal-profile collision with
  an older prerequisite error. The M02-012 collision checks now run before the
  older dependency DAG checks and return their exact owned reasons.
- The next focused artifact audit rejected `provenanceExact=false` after the
  build-script remediation changed the source closure. All pins were refreshed
  to the final 75-input closure, and the accepted reruns pass.
- The first complete post-remediation host run found three M01.035 failures:
  two direct-call-cardinality checks and one marker/fail-closed check. The old
  profile selector did not recognize the new M02-012 preprocessor symbol and
  therefore left its block in simulated historical profiles. The selector now
  models the new flag independently as disabled by default; focused and full
  reruns pass.
- Final independent tooling review found one stale pre-remediation link-evidence
  row. A stable named current-source publish produced the final 399,378-byte
  link-evidence file and corrected SHA-256 recorded above.
- Independent core review previously found that an all-zero context could pass
  a raw restoration check, partial allocation overlap could double-free, and a
  range ending at the address-space top could bypass overflow classification.
  All three were corrected before the accepted commit and guest run.
- No accepted real-QEMU, artifact-verifier, build, focused-test, naming,
  source-firewall, script-parse, or final full-host failure remains.

## Deferred work

- Issue #52 and integration issue #497 still require Gitea evidence comments,
  merge identity, verified closure, and backlog reconciliation before closing.
- Later issues must publish/protect a real common interrupt entry path, install
  persistent owned descriptor state, activate GDT/TSS/IST routing, inject a
  controlled interrupt/fault, and preserve panic-safe diagnostics.
- Real `ExitBootServices`, post-firmware owned serial diagnostics, scheduler and
  threading, live storage/network drivers, hosted .NET 10, Kestrel/Minimal API
  execution on CLROS, provider image support, CloudReady status, security
  hardening, and production readiness remain unproved.
- `EFI_BUFFER_TOO_SMALL`, memory-map descriptor growth, stale map keys, and
  `ExitBootServices` retry exhaustion are inapplicable to this profile because
  it allocates, verifies, and frees before ordinary map capture and never calls
  `ExitBootServices`.

## Evidence boundary

This step proves that the exact source-bound NativeAOT EFI image executed the
frozen reversible-IDTR transition on the recorded QEMU/OVMF virtual CPU; loaded
and read back a complete temporary owned IDTR; restored and read back the raw
firmware IDTR; preserved IF across the transition; returned the exact cookie;
released the disjoint LoaderData allocation before the LoaderCode allocation;
and continued through ordinary pre-`ExitBootServices` smoke.

It does not prove interrupt or fault delivery, an executable common dispatch
path, active or persistent GDT/TSS/IST/IDTR state after the adapter returns,
W^X/NX enforcement, NMI/SMI/machine-check safety, real firmware exit,
post-firmware diagnostics, hosted CoreCLR/.NET, NativeAOT Minimal API execution
on CLROS, cloud-provider support, cloud readiness, security hardening, or
production readiness. Gate 2 remains the only passing numbered completion
gate; Gate 3 and Gate 4 remain `Fail`.
