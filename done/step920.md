# Step 920 - Materialize and audit the M02-010 normalized interrupt-entry page in EFI

## Summary

Integrated the frozen M02-010 interrupt-entry page materializer into one
default-off, no-stdlib NativeAOT EFI profile and preserved fresh host,
same-build artifact-verifier, and real QEMU/OVMF evidence.

The profile allocates one temporary 4,096-byte `EfiLoaderData` page before
memory-map capture, models the reviewed common target at the address one page
past that allocation, materializes all 256 fixed 16-byte vector slots exactly
once, validates all 4,096 bytes through the production readback path exactly
once, audits the complete result, and releases the page exactly once before it
can emit its ready/pass records. The accepted identity is nine
hardware-error-code vectors, 247 synthetic-error-code vectors, three normalized
semantic stack slots, FNV-1a64 `428D0353A96B7ACE`, and independent complete-page
SHA-256 `71325A5633E1985BB22B4F8E1581399F63FEDEEB8CBFA1E3507A3F4ED73A71AF`.

The accepted returning profile has exactly 17 required, five ordered, three
unique, and three forbidden markers. Supplemental issue #490 raises only the
host runner's bounded marker limit from 320 to 512 so the exact 370-character
M02-010 record can pass unchanged; it adds no guest capability evidence.

No generated slot or modeled common target is executed. No executable
permission, IDT/IDTR/IST installation, or live fault is claimed. No numbered
completion gate advances; Gate 2 remains the only passing gate and Gate 3
remains `Fail`.

## Why

PR #486 merged a reviewed host leaf that deterministically generated and read
back the complete interrupt-entry page, but that leaf still reported
`guestMaterialization=false` and had no EFI or real-QEMU evidence. Issue #487
owns the serialized EFI/project/build/verifier/QEMU/documentation integration
needed to cross that one boundary without conflating materialized bytes with a
working interrupt path.

The first required dry run exposed a separate bounded tooling mismatch: the
exact guest record is 370 ASCII characters while the runner accepted only 320.
Issue #490 owns exactly the two state-machine paths needed to admit that record
while preserving every other marker, count, line-capacity, ordering, uniqueness,
forbidden-marker, terminal, and byte-noise invariant.

## Files changed

- `src/OS/OS.csproj`
  - Links the frozen leaf into the explicit 60-item EFI closure, adds the
    default-false typed property and exact compile symbol, and rejects the
    nonreturning M02-009 terminal-profile collision.
- `src/OS/Boot/EfiEntry.cs`
  - Allocates one `EfiLoaderData` page, validates its real address, materializes
    once, reads back once, audits the complete result, releases once, and emits
    exact records before memory-map capture.
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
  - Carry and verify the switch, 60-item compile closure, 73-input source
    authority, collision guard, and exact 17/5/3/3 QEMU profile.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
  - Pin the new explicit closure while preserving historical accepted
    identities for earlier profiles.
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
  - Verifies frozen identities, project/tool propagation, EFI call order,
    fake Boot Services cleanup/failures, hostile arguments/transcripts, and
    exact marker lists.
- `tools/OS.QemuRunner/SerialMarkerStateMachine.cs`
- `tests/OS.HostTests/SerialMarkerStateMachineTests.cs`
  - Issue #490 only: raises 320 to 512; accepts the exact 370-character and a
    512-character marker; keeps 513 rejected.
- `README.md`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
  - Record the bounded, default-off, pre-firmware tuple without changing gates.
- `done/step920.md`
  - Preserves this evidence ledger.

The frozen production leaf and host test were verified unchanged and are not
attributed as new implementation paths in this integration.

## Tests run

At exact implementation/evidence head
`c49148df816b9d7640d25ffc29fac9080a5d9ce7`:

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit `0`; zero warnings and zero errors; final refresh 10.34 seconds.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Exit `0`; zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Exit `0` after 2,766.509 seconds.
  - 3,475 `TEST_START`, 3,476 `TEST_PASS`, zero `TEST_FAIL`.
  - Exactly one terminal `TEST_PASS host.tests`.
  - M02-010 integration, host leaf, and exact 370/512/513 state-machine
    boundary cases all passed.
- `.\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- PowerShell AST parsing passed for all three changed scripts.
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
  - Exit `0`; default 12-required, zero-ordered, zero-unique profile retained.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM02NormalizedInterruptFramePageProbe`
  - Exit `0`; exact 17-required/five-ordered/three-unique/three-forbidden
    profile.
- Combined dry run with M04-002, M04-003, and M02-010 enabled:
  - Exit `0`; exact order M04-002, M04-003, M02-010 start/ready/pass, then
    `UEFI_MEMORY_MAP_READY`.
- `git diff --check`: exit `0`.
- `git status --short`: empty at the tested implementation head before
  evidence generation.

Real NativeAOT EFI/QEMU command:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02NormalizedInterruptFramePageProbe `
  -EfiArtifact .\artifacts\efi\BOOTX64-M02-010-c49148df-QEMU.EFI `
  -Image .\artifacts\images\m02-010-c49148df-qemu.raw `
  -SerialLog .\artifacts\logs\issue-487-m02-010-c49148df-qemu-serial.log `
  -SummaryLog .\artifacts\logs\issue-487-m02-010-c49148df-qemu-summary.log
```

- Outer exit `0` after 55.374 seconds.
- Same-build NativeAOT publish, schema-three verification, raw-image build,
  QEMU marker evaluation, and summary generation all emitted terminal passes.

Post-evidence validation after the canonical documentation edits completed:

- `dotnet build .\CLROS.slnx --nologo -m:1`: exit `0`; zero warnings and zero
  errors; 2.05 seconds.
- `.\scripts\check-naming.ps1`: `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- PowerShell AST parsing passed for all three changed scripts.
- Default QEMU dry run: exit `0`; unchanged 12-required, zero-ordered,
  zero-unique profile.
- M02-010 QEMU dry run with `-BuildEfi`: exit `0`; exact
  17-required/five-ordered/three-unique/three-forbidden profile.
- Combined M04-002+M04-003+M02-010 dry run: exit `0`; exact M04-002,
  M04-003, M02-010 start/ready/pass, and memory-map ordering.
- Direct M02-010 NativeAOT publish and artifact verification: exit `0` with
  `TEST_PASS efi.artifact.verify` and `TEST_PASS efi.shell.publish`; it
  reproduced EFI SHA-256
  `9F2061A0A7CADB9C0EC15F1A8F7148F0116AA56DBEAE8FD28E8ACECA4B4BA846`.
- `git diff --check` and the forbidden first-party source scan passed.

The complete HostTests and real-QEMU runs were not repeated after these
documentation-only edits. Their guest identity remains the exact tested
implementation head and tree; the later documentation commit is not
represented as a different QEMU-tested source.

## Evidence

### Source identity

- Canonical integration base:
  `473cd6d3f630cb6eb2e7bbee117f8d13cd956543`.
- Base tree: `5131a0a8648d23a454eae3d56cabe8245a65699a`.
- Tested implementation head:
  `c49148df816b9d7640d25ffc29fac9080a5d9ce7`.
- Tested tree: `b4d658518c9c7a927a66e88ec25a5dd82856a63a`.
- Stable combined patch ID:
  `d0c5fae05e5b185e7982b0a72abdb4ca74d3dbaf`.
- Three commits and exactly 20 implementation/test/tool paths:
  - `db86ecd9e0de0a3310f34d5a5e2d533b4de0711b`: main integration.
  - `636a66280c3fd6b0fd94c602cfeec6154f311cb1`: explicit `none` parsing.
  - `c49148df816b9d7640d25ffc29fac9080a5d9ce7`: fail closed on an absent
    requested summary line.

Final canonical PR #486 leaf identity:

- Leaf head: `2267db3563bd7eafe2543192b7ee6fbedc6d7d3b`.
- Leaf merge: `d41a2d39b0e63e893f542a673c6b8eb6a59f18f7`.
- Leaf tree: `6fdea3603aea50f7c1177410bda39a315fb87694`.
- Leaf stable patch ID:
  `a90153e01f969b138a2b557406de49ea049b3e9f`.
- Production blob `abe063a1b1d5eb34682a530edace279fb12209dc`,
  SHA-256
  `B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1`.
- Host-test blob `884970a3640d18237858e830f8d1dba1dfa4ed00`,
  SHA-256
  `DEEA918C5AB7DB971AA39682F595204465D9D75DE254E99C034A12440AC46FCC`.

Issue #490 exact tested identities:

- `tools/OS.QemuRunner/SerialMarkerStateMachine.cs`
  - Blob `dd7b2ea46e48652641b426856875cddbb4f2262c`, 31,390 bytes.
  - SHA-256
    `2BB9C585D961DD68E73CC1165176F1A7FC0D8C06A962F3B560A124FC42DA5381`.
- `tests/OS.HostTests/SerialMarkerStateMachineTests.cs`
  - Blob `178e7927f827636c13076e8eea65fe40fbe88e99`, 27,684 bytes.
  - SHA-256
    `84E3A0E6D21092B86155983F6965E97E881E3337CF59466223CF015397DF6EF5`.

Documentation and this step are layered after the tested implementation
commit. They are not claimed as a different QEMU-tested source identity.

### Complete host run

- Log:
  `artifacts/logs/issue-487-m02-010-c49148df816b9d7640d25ffc29fac9080a5d9ce7-hosttests.log`.
- 446,609 bytes; 7,007 lines.
- SHA-256:
  `101C5C7FCA0D521161ED9F7CD32FE154E7D241705E3740E006B4328063CB4FFD`.
- Wrapper interval: `2026-08-25T23:45:14.5185991Z` through
  `2026-08-26T00:31:21.0277334Z`.
- Exit `0`; 3,475 starts; 3,476 passes; zero failures; exactly one terminal
  `TEST_PASS host.tests`.

Accepted M02-010 host records:

```text
TEST_START m02.010.efi.integration
TEST_PASS m02.010.efi.integration
TEST_START m02.010
CLROS_M02_010_HOST_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 readBack=true guestMaterialization=false stubExecution=false idtInstalled=false faultTriggered=false
TEST_PASS m02.010
```

### Same-build EFI closure

Schema-three evidence generated at `2026-08-26T00:35:00.3125869Z` records:

- Configuration `Release`; RID `win-x64`; .NET SDK `10.0.302`.
- Exactly 60 compile items; compile-key SHA-256
  `805E39822E162FD57BE8421D6D19282267D8B71222602BB359C81ACE9E7BA2E1`.
- Exactly 73 source inputs; source-closure SHA-256
  `D9B71EC4E399B6F21E27C6CF14E5DD9AB143288FE63BFA71A78F31440B43E0E3`.
- Raw `src/OS/OS.csproj` SHA-256
  `90A9243917910A9516388ABB301D9BEC92F661C6A839DBFA25E24497288C2769`.
- Canonical project-shape SHA-256
  `05C6372ADA146E1CF78DFF3FB1D2C63601F788CF650172292F4D8724E15DBF45`.
- Zero project references, zero ordinary references, and two pinned analyzers.
- M02-010 `true`; M02-009, M04-002, and M04-003 `false`.
- MSVC linker `14.51.36256.0`; Windows SDK `10.0.28000.0`;
  ILCompiler/runtime pack `10.0.8`.
- PE32+ x64, EFI subsystem 10, entry RVA `0x00005330`, same-build
  entry/serializer bindings, and no ordinary/bound/IAT/delay imports.

| Artifact | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| EFI | 67,072 | n/a | `9F2061A0A7CADB9C0EC15F1A8F7148F0116AA56DBEAE8FD28E8ACECA4B4BA846` |
| Serializer object | 336 | n/a | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Link map | 38,683 | 404 | `57C21B84F6C16FFE489C7598EAA57EF420CE8E8D1D4C4BAE8A1DD4C77A55E4FC` |
| Link response | 500 | 15 | `F88A91FBB859939851CCEBB865B680940F9ECE1193DDB8F8A1452BAF31B1A96C` |
| Link-evidence JSON | 398,582 | 5,154 | `CC0ECC0AF83B48AAB1FE0DA1757BC82345D808E68077F98CBD0A86C99DE5BD0F` |
| Archived EFI build log | 4,624 | 39 | `4C60FBF044A2B91A18D5CC021BCF9237898DA612E8766CBD4EA99062FCF798A8` |

### Real QEMU/OVMF tuple

- Wrapper: `2026-08-26T00:34:29.6833668Z` through
  `2026-08-26T00:35:25.0573607Z`; 55.374 seconds; exit `0`.
- QEMU `11.0.50` (`v11.0.0-12631-g54e84cdc7a`):
  - 25,272,968 bytes; SHA-256
    `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
- OVMF:
  - 3,653,632 bytes; SHA-256
    `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- Raw image:
  - 67,108,864 bytes; SHA-256
    `C4FE364F0E46005EF06F13B0D0A65E8013C781BAFFB4FEE1D705FBC4B15BBB5D`.
- Serial log:
  - 8,358 bytes; 29 lines; SHA-256
    `4E03A7CFCF8BD78018E3ABF35897EE3371B6E299FED70F32E56D4CEDE6EEB678`.
- Summary log:
  - 4,382 bytes; 49 lines; SHA-256
    `8FCD4EE9B1883D99A1D57068A840888CDCD49CDDD0D631A14CDC6B275957EC8F`.
- Stdout:
  - 13,706 bytes; 107 lines; SHA-256
    `53E5A581B0F487469B3C58D96291E38613CCFB617F92E2E1608BE5DE52269A2D`.
- Stderr:
  - Zero bytes and lines; SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

The state machine reported:

```text
SERIAL_MARKER_STREAM status=Passed terminal=Success bytes=8358 lines=29 invalidBytes=207 invalidLines=2 orderedSeen=5 outOfOrder=0 uniqueSeen=3 duplicateUnique=0
SERIAL_MARKER_DRAIN milliseconds=750
QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1
```

The required subsequence appeared exactly once and in order:

```text
EFI_CONSOLE_READY
TEST_START m02.010
CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 commonTargetDelta=4096 fnv1a64=428D0353A96B7ACE readBack=true guestMaterialization=true loaderData=true pageReleased=true stubExecution=false idtInstalled=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.010
UEFI_MEMORY_MAP_READY
```

Exact-line parsing found one start, one 370-character ready record, one pass,
and zero failure records. The summary records:

```text
QEMU_SMOKE_STATUS pass
MISSING_MARKERS none
OUT_OF_ORDER_MARKERS none
DUPLICATE_UNIQUE_MARKERS none
FORBIDDEN_MARKER_HIT none
FAIL_REASON none
```

Complete-page sentinels independently preserved by the host audit:

```text
vector 0   6A006A006800000000E9F20F00009090
vector 8   6A016808000000E9740F000090909090
vector 255 6A006A0068FF000000E9020000009090
```

## Failures

- The initial required M02-010 dry run reached `OS.QemuRunner` but was rejected
  before marker evaluation because the 370-character ready record exceeded
  `MaximumMarkerLength=320`: `Marker length exceeds the configured capacity`.
  No QEMU child or guest claim was accepted. Issue #490 raised only the ceiling
  to 512, kept 513 rejected, and the unchanged record then passed 17/5/3/3.
- A pre-review partial run was stopped after stale historical 59-item/72-input
  pins were found. It is not acceptance evidence.
- `issue-487-host-tests-rejected-redundant-hash-assertion.log`:
  - 49,259 bytes; 785 lines; SHA-256
    `044EBBB43D22D46C4EA6519B75D9F7DD1D8454861B84F0A7F4F1751554606A81`.
  - 390 starts, 388 passes, zero failures, no terminal host pass.
  - Stopped after review found a redundant source-text assertion for the
    computed compile hash.
- `issue-487-host-tests-precommit-partial.log`:
  - 49,494 bytes; 789 lines; SHA-256
    `446D9DA15637B8894BC2FB88B46ED60256640ED2D93B1CAAB225B40F61C37039`.
  - 392 starts, 390 passes, zero failures, no terminal host pass.
  - Stopped before the candidate had a stable evidence commit.
- Exact run at `db86ecd9e0de0a3310f34d5a5e2d533b4de0711b`:
  - Exit `-532462766` after 2,513.958 seconds.
  - 172,368 bytes; 2,733 lines; SHA-256
    `D5070B165AE7BA60C61B8AE04E078E8A385238F6CA917D54295F5FE7E79C100E`.
  - 1,347 starts, 1,345 passes, two failures, no terminal host pass.
  - The test helper parsed explicit `ORDERED_MARKERS none` as one marker. The
    production lists were independently correct; this was a test-parser defect.
- Partial run at `636a66280c3fd6b0fd94c602cfeec6154f311cb1`:
  - 49,259 bytes; 785 lines; SHA-256
    `044EBBB43D22D46C4EA6519B75D9F7DD1D8454861B84F0A7F4F1751554606A81`.
  - 390 starts, 388 passes, zero failures, no terminal host pass.
  - Stopped when independent review found that an entirely absent requested
    summary line could still masquerade as an empty list.
- Final head `c49148df816b9d7640d25ffc29fac9080a5d9ce7` fails closed on an absent
  requested summary line, passed focused independent review, and produced the
  only accepted full HostTests and QEMU tuples.
- The accepted real-QEMU run had no failed attempt and was not retried.

## Deferred work

- Publish generated slot/common-entry pages under an owned active W^X/NX
  policy; no permanent RWX mapping is permitted.
- Implement and execute the common interrupt-entry thunk.
- Save/restore registers and bind normalized data to a real hardware frame.
- Populate/install the owned IDT and load IDTR.
- Integrate the owned GDT/TSS and guarded IST stacks into the live path.
- Execute a generated vector slot and common target.
- Trigger controlled `#PF`, `#UD`, and later double-fault/NMI probes without a
  triple fault, silent reboot, or firmware dependency.
- Route real fault state through the owned panic-safe serial path.
- Complete real successful `ExitBootServices` and owned post-firmware
  diagnostics.
- Complete runtime correctness, allocation/GC, exceptions, metadata, dispatch,
  cctors, scheduler/threading, storage, networking, app model, and PAL work.
- Run a real NativeAOT application as an OS-owned workload.
- Support hosted .NET 10, ASP.NET Minimal API/Kestrel, physical/cloud-provider
  targets, CloudReady requirements, security hardening, and production gates.
- Obtain exact-head independent PR approval, merge this evidence wave, close
  #487/#490, reconcile parent #50, and only then release M02-011.

## Evidence boundary

The accepted tuple proves only that the frozen C# materializer and production
readback validator ran against one temporary firmware-allocated
`EfiLoaderData` page on the recorded QEMU/OVMF platform before
`ExitBootServices`, produced the deterministic 4,096-byte identity, and
released the page. The common target was modeled address metadata only. No
generated slot or target was executed, no executable permission was
established, no IDT/IDTR/IST was installed, and no live interrupt or fault was
produced or handled.

This step does not prove a working kernel, real successful
`ExitBootServices`, post-firmware execution, scheduler/threading, storage,
networking, a runtime-complete NativeAOT application, hosted .NET 10,
ASP.NET Minimal API or Kestrel, physical/provider support, CloudReady status,
security hardening, production readiness, or advancement of Gate 3, Gate 4,
Gate 14, or any other numbered completion gate.
