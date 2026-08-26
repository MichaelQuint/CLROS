# Step 924 - Close M02-013 terminal UD2 vector-state EFI evidence

## Summary

Completes the implementation and evidence requested by Gitea issue #501,
`M02-013`, as a disabled-by-default NativeAOT EFI terminal evidence profile.

The profile preserves the ordinary pre-firmware boot transcript through
`OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`. Only then does it
allocate two LoaderCode pages and five disjoint LoaderData pages, materialize and
read back fresh normalized vector slots, owned IDT, transition context,
GDT/TSS, and three zeroed IST pages, publish the exact instruction range, and
enter the generated transition synchronously.

The transition loads fresh owned descriptor state, verifies live pre-fault
`RFLAGS=0x46`, loads 15 distinct GPR cookies, executes one `ud2`, receives vector
6 through the owned IDT, captures and compares all 23 integer/control fields,
writes the exact result through owned COM1, and remains in a terminal `cli`/`hlt`
loop. Fault delivery saves `RFLAGS=0x10046`; RF is asserted exactly rather than
masked.

All pre-entry failures are represented by one no-stdlib production contract.
EFI success status, rather than a nonzero returned address, establishes page
ownership. Hostile zero, misaligned, noncanonical, wrapping, overlapping,
materialization, readback, publication, marker, and cleanup cases fail closed.
Cleanup releases data before code and suppresses a possible double-free when
hostile ranges are not provably disjoint. No callback occurs after descriptor
activation.

The all-false/default EFI path remains unchanged. M02-013 adds no
`BootSequence` phase and is mutually exclusive with returning or independently
owned descriptor profiles. The final complete host suite and one fresh real
QEMU/OVMF run passed from the completed production/test source head.

## Why

M02-013 was the last child of parent issue #53's bounded controlled-fault
milestone. Host-only synthetic `CpuFaultFrame` comparison did not prove that a
virtual processor could execute the requested invalid opcode, deliver vector 6
through CLROS-owned descriptor state, capture the requested register state, or
report without returning to firmware. This step supplies that narrow processor
evidence while preserving the project's default-off probe policy and honest
completion-gate boundary.

The latest SharpOS reference refresh remained bounded to concepts already
mapped in `done/step917.md`. SharpOS main was reviewed through commit
`0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`; useful concepts were save-before-
scratch register handling, normalized error/no-error vector entry, vector 6,
allocation-free panic output, and terminal halt. CLROS did not copy SharpOS
source, selector values, firmware gate state, managed dispatch architecture, or
UEFI fault-output assumptions. SharpOS still supplied no networking shortcut
for later hosted-.NET work.

## Files changed

- `src/OS.Kernel/Diagnostics/DisabledDefaultUd2VectorStateTransition.cs`
  - Owns the exact no-stdlib stream, layout, byte/hash authority, RF-bearing
    frame expectation, and pure pre-terminal validation/cleanup result contract.
- `src/OS/Boot/EfiEntry.cs`
  - Adapts EFI Boot Services to the shared contract, allocates only after the
    ordinary smoke tail, materializes and readbacks every authority, handles
    repeated preparation without ownership loss, publishes before entry, and
    performs no callback after descriptor activation.
- `src/OS/OS.csproj`
  - Keeps M02-013 default-false, implies only its prerequisite chain, defines
    `M02_UD2_VECTOR_STATE_PROBE`, and rejects incompatible profiles.
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
  - Carry the typed profile, exact implications/exclusions, source and evaluated
    property firewalls, terminal marker order/uniqueness, and post-pass-noise
    rejection.
- `tests/OS.HostTests/DisabledDefaultUd2VectorStateTransitionTests.cs`
  - Freezes all 4,096 bytes, offsets, hashes, cookie/register comparisons,
    generated control flow, and exact saved RF behavior.
- `tests/OS.HostTests/M02Ud2VectorStateEfiIntegrationTests.cs`
  - Publishes fresh default/enabled NativeAOT artifacts, applies the same-build
    verifier, directly executes every shared pre-terminal contract stage with
    fake firmware, and source-audits the EFI adapter and terminal order.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - Cover implication/exclusion behavior, artifact properties, hostile
    arguments/transcripts, and terminal-noise rejection.
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
  - Explicitly removes the M02-013 conditional region from historical simulated
    profiles so legacy serializer-call cardinality remains exact.
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
  - Distinguish each default-false property from the exact M02-013 prerequisite
    override and preserve the earlier standalone-profile contracts.
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`
  - Pin the current 67-item compile order, 80-input authority closure, project
    hashes, and the separately oriented M02-013 exclusion rules without
    weakening historical profile checks.
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `docs/qemu.md`
  - Record the exact host, artifact, QEMU/OVMF, RF-correction, terminal, and
    completion-gate evidence boundaries.
- `done/step924.md`
  - Records this milestone and its immutable evidence boundary.

## Tests run

- Focused M02-013 final-head module
  - Exit `0`; all seven cases passed.
  - It built and verified fresh default and enabled NativeAOT artifacts, executed
    every fake-firmware contract stage, verified preparation/readback, proved
    pre-entry tail completeness, cleanup/marker failure behavior, and exact
    terminal entry/boot ordering.
- Focused regression modules
  - `m01.035`: all nine historical instruction-publication source cases passed.
  - M02-003 through M02-006: all checked-port/owned-COM1 dependency-profile
    cases passed.
  - M02-007, M02-008, M02-009, and M04-002: all compile-order, source,
    adapter, QEMU-profile, and documentation cases passed.
  - M02-010, M02-011, M02-012, and M04-003: all source-authority, fresh
    NativeAOT artifact, fake-firmware, QEMU-profile, and transcript cases passed.
  - M02-017 safe-stub project/source selector passed.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  - Exit `0`; zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Exit `0`; 3,507 `TEST_START`, 3,508 `TEST_PASS`, zero `TEST_FAIL`, and
    terminal `TEST_PASS host.tests`.
  - Stdout: 454,016 bytes, SHA-256
    `DC9F1B87D2D4E528B21E4CD20547009AC2090AEB9BF9F5C9B87F2E171BE3D423`.
  - Stderr: 0 bytes, SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Real QEMU/OVMF typed-profile run with `-BuildEfi`,
  `-EnableM02Ud2VectorStateProbe`, and the exact full ready marker
  - Exit `0`; `TEST_PASS efi.shell.publish`, `TEST_PASS efi.artifact.verify`,
    `TEST_PASS imagebuilder.raw`, and `TEST_PASS qemu.smoke`.
  - Marker state: `status=Passed`, `bytes=3837`, `lines=79`,
    `orderedSeen=59`, `outOfOrder=0`, `uniqueSeen=59`, and
    `duplicateUnique=0`.
  - Terminal audit: `postSmokeSequence=true` and
    `postPassMarkerNoise=false`.
- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit `0`; zero warnings and zero errors.
- `.\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- PowerShell AST parse of the three changed scripts
  - Zero parse errors for build, artifact-verifier, and QEMU runner scripts.
- `git diff --check`
  - Exit `0` after implementation and final evidence documentation.

## Evidence

The implementation branch started from `origin/master` commit
`6ac79e82`, created the initial terminal integration at `68cb856`, and
reconciled current master at `b207312`. The accepted same-build NativeAOT
evidence records:

- .NET SDK `10.0.302` and NativeAOT/ILCompiler package `10.0.8`;
- `Release`, `win-x64`, `net10.0`, `PublishAot=true`,
  `SelfContained=true`, and `PublishTrimmed=true`;
- 67 compile items with compile-key SHA-256
  `F118A012101E87ABF5D1AC1299BE82705B47942EAE06ABB669418977412D124C`;
- 80 source-authority inputs with closure SHA-256
  `559999AC08D30B1FF5629A8240D9E822774ADC215D5E43B80910297B0C1E5DE2`;
- raw project SHA-256
  `FF1EF01D1017123ECE5DC5EF81965C488382240937F088F1F35B28E57967FEEA`;
- canonical project-shape SHA-256
  `B317CC30C4DD27D61B6349686BE68C3FD6F6702626DD5637D0C6A43A9C2BEA2D`;
- `EfiEntry.cs` SHA-256
  `5BC413EE69931A454EF5F4B084106D8E3AB7DF68C18351F785D2535B992FD7EB`;
- transition-source SHA-256
  `F73C8F78FD1D5A1E0FD54C3FBE80716B40C61F1C05BC9F51CED8A505D61C407E`;
- exact transition FNV-1a64 `E5070E16F23533A3` and stream SHA-256
  `DB402C072CBD5E9C82F36D1802AAB526C347077BDD147031D83E83B563E195B7`.

The final real run used QEMU
`11.0.50 (v11.0.0-12631-g54e84cdc7a)` and the 3,653,632-byte OVMF image with
SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| Enabled EFI | 543,744 | `E532250338F50AE5EE191C6A2661612711A1DDA541662C72F6A7ADAF01C264DF` |
| Raw image | 67,108,864 | `E6D063ADEB790A2DE0B104A39827D2EDD75D16DE2CEAAF079A1353CDA9920C4D` |
| Serial log | 3,837 | `39DF159E4990CEE91574B6240ED77375B10DAC8B43338BA232FC965DF11D2E3E` |
| Passing summary | 17,000 | `75ED1E58513754E86579C6FD0B52F8BEC794EED9A7C205068BC9A5ADF9B7D99F` |
| Link map | 251,264 | `85A0F8D3BB996B1269F22885C958A3983DA510EA5B2C610318FC4D6546BDAFA7` |
| Link response | 500 | `70B1580FEC6721F70F1F9AAA71332055F1FD4BA6586B914156F8009F51A66B4D` |
| Link evidence | 400,930 | `AE8C700A34B796EE893FB91C1B82F83141C0FBC93ABF5AF288DFB4A10BF10AFB` |
| Publication object | 336 | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Runner stdout | 30,235 | `3B06EB7C8967374491D2F4FD099028CAD30AC55B5D426171BC43385CD61B4162` |
| Runner stderr | 0 | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |

The verifier identified PE32+ AMD64, EFI subsystem 10, entry RVA `0x55980`, no
ordinary/bound/IAT/delay imports, 991 runtime-function records, and the exact
same-build `EfiMain` and instruction-publication serializer bindings.

The accepted serial suffix was exactly:

```text
OS_BOOT_OK
TEST_PASS boot.sequence
OS_SMOKE_PASS
TEST_START m02.013
CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 ud2RipMatched=true registerFields=23 registerStateCaptured=true registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true ownedIdtrLoaded=true ownedCom1=true faultTriggered=true terminalHalt=true markerTransport=ownedCom1 postExitBootServices=false
TEST_PASS m02.013
```

No marker-like output followed the pass record.

## Failures

- The first live vector-6 attempt reached the exact `ud2` RIP with the GPR,
  CR2/CR3, and fresh descriptor addresses correct, but emitted the generated
  failure transcript because the model expected saved `RFLAGS=0x46`. A bounded
  instruction/exception trace isolated architectural RF in the saved frame:
  `0x10046`. The production stream and focused tests now assert that exact value.
- One earlier full-host attempt was intentionally stopped after 394 starts, 392
  passes, zero failures, and no aggregate result because later source changes
  invalidated its provenance. It is diagnostic only.
- The first completed host attempt failed seven records in `m01.035` because its
  source-profile selector did not recognize the new M02-013 conditional symbol.
  It exited before an aggregate result with stdout SHA-256
  `45008C759CC848CB8DABB4F26577AE719C29A0F735DB0552A3779754B08D0D28`
  and stderr SHA-256
  `10ED88B14018D7F15E0F06B655DBF03886AF79C27C0FA12A32B3547B1D9F3EC5`.
  The selector now disables that region explicitly in historical profiles.
- The second completed host attempt passed that blocker, then failed two
  M02-003 records because older exact-count tests treated the M02-013
  prerequisite overrides and five new compile inputs as historical defaults.
  Its stdout SHA-256 was
  `76C048C74816821C6BBDA71A90BFC26037DA3803755FA994BA09E0755E5F5961`;
  stderr SHA-256 was
  `C3558D41C18F01346E529E181BAC8E3BA5D9C613376ACD8E69BB2F7B524A3EB9`.
  Focused sibling sweeps corrected every affected exact predicate before the
  accepted complete run.
- The first final-QEMU command was rejected before publishing or launching a
  guest because its requested EFI output was outside the required
  `artifacts/efi` authority directory. Its 10,133-byte diagnostic stdout has
  SHA-256
  `CB28EA86944C51460CE8FA5FCE4ACC3E7092978B3E211DED86975AFD01DD71DD`;
  stderr was empty. The accepted rerun used the required authority directory.
- Temporary ignored focused harnesses initially lacked two existing host-helper
  methods and one QEMU-runner project reference. Those harness-only compile
  errors were corrected without production changes before the focused sweeps.
- No accepted focused, host, build, naming, source-firewall, script-parse,
  artifact-verifier, image, or QEMU failure remains.

## Deferred work

- This profile deliberately never returns, so descriptor restoration, page
  release after entry, safe continuation, second-fault behavior, and persistent
  interrupt ownership remain unproved.
- `EfiLoaderCode` is an allocation type; this step does not prove live W^X/NX.
- Real `ExitBootServices`, post-firmware memory/platform ownership, ACPI/PCI,
  live storage/network drivers, scheduling/threading, and reusable panic
  diagnostics remain pending.
- Hosted .NET 10, a NativeAOT Minimal API/Kestrel workload on CLROS,
  cloud-provider images, hardening, CloudReady status, and production readiness
  remain future backlog work.
- Per the user's stop instruction, no next Gitea issue was reconciled, claimed,
  or started after M02-013.

## Evidence boundary

This step proves one exact, default-off, terminal, pre-`ExitBootServices`
NativeAOT EFI profile on the recorded QEMU/OVMF tuple. It proves that ordinary
smoke completed first; fresh owned GDT/TSS/IDT/IST state was materialized and
activated; the virtual processor delivered vector 6 from the deliberate UD2;
all requested integer/control fields matched; the exact result was emitted
through owned COM1; and no marker-like output followed the pass record.

It does not add a reusable `BootSequence` phase, safely return from a fault,
restore descriptor state, free pages after terminal entry, prove a second fault,
operate after firmware exit, or establish a general interrupt/panic subsystem.
It does not prove hosted .NET, Kestrel, networking, cloud readiness, security
hardening, or production readiness. Gate 2 remains the only passing numbered
completion gate; Gate 3 and Gate 4 remain `Fail`.
