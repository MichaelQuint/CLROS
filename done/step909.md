# Step 909 - Execute the M02-005 owned COM1 writer in the EFI guest

## Summary

Integrated #45's allocation-free production writer into the explicit
no-stdlib NativeAOT EFI closure under separately claimed issue #450. The
default-false M02-005 profile consumes the accepted M02-004 checked binding,
calls the writer exactly once, requires seven configuration writes and 18
first-ready line-status polls, emits the standalone marker only through direct
COM1 port I/O, and emits a separate fixed evidence line through UEFI serial.

The shared closure moves from 53 to 54 explicit compile items and from 66 to 67
source-authority inputs. Supplemental tooling issue #451 raises only the QEMU
state machine's bounded marker ceiling from 256 to 320 characters so the exact
259-character detail marker is admissible; the 128-marker, line-capacity,
matching, ordering, uniqueness, terminal, and noise policies remain unchanged.

## Why

Managed #45 cannot lawfully edit `EfiEntry.cs`, `OS.csproj`, build/verifier
scripts, QEMU tooling, README, or central EFI/QEMU documentation. Real guest
evidence nevertheless requires those collision-controlled paths. Issue #450
owns that serialized integration lane, and #451 owns the two host-tool paths
disclosed only after the exact marker exceeded the previous input ceiling.

The transport distinction is deliberate: the first line is the payload proof
and must come exclusively from checked COM1 `Out8`; the second line is evidence
metadata and states that its own transport is UEFI serial. Neither line implies
firmware exit.

## Files changed

Issue #450 owns:

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `scripts/test-efi-artifact.ps1`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- current exact-count/source consumers:
  `BuildEfiShellPreflightTests.cs`,
  `EfiArtifactVerificationScriptTests.cs`,
  `M02SafeStubPageEfiEntryTests.cs`,
  `M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`,
  `M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`, and
  `M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `docs/efi-build.md`
- `docs/qemu.md`
- `README.md`
- this globally reserved `done/step909.md`

Issue #451 owns:

- `tools/OS.QemuRunner/SerialMarkerStateMachine.cs`
- `tests/OS.HostTests/SerialMarkerStateMachineTests.cs`

The combined branch also consumes exactly #45's five paths listed in
`done/step908.md`. No backlog manifest, synchronizer, `BootSequence.cs`,
`Probes.cs`, completion gate, unrelated source/test/script/doc, reference, app,
generated artifact, or stable identity is changed.

## Tests run

Focused and development evidence commands:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\artifacts\focused-m02-005\FocusedM02005.csproj --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02OwnedCom1ReadyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -SkipImageBuild -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
```

The focused integration cases passed compile ordering/count/hash, default-off
and dependency policy, call ordering/uniqueness, result gates, reconstructed
success/failure bytes, absence of a UEFI standalone-marker formatter, exact
QEMU required/forbidden markers, and the 259/320/321 marker boundary. Default
dry-run passed. The three invalid shapes exited 1 with their exact dependency,
build, and skip-image rejection reasons.

The accepted development preflight reported 67 authority inputs and 54 compile
items. The flagged publish emitted `TEST_PASS efi.artifact.verify` and
`TEST_PASS efi.shell.publish`, with PE32+ AMD64, EFI subsystem 10, no ordinary
or delay imports, exact `EfiMain`, and the linked audited serializer.

After the rejected attempts documented below, the fresh complete pre-push
HostTests run passed with 3,399 line-leading starts, 3,400 passes, zero
failures, exactly one terminal `TEST_PASS host.tests`, and process exit 0. Its
872,320-byte log is
`artifacts/logs/m02-005-hosttests-wave-r4.log` with SHA-256
`8D40BCBF2A68E57200506D21CA698FE7F827C60D08F11815F5DF4FD1634E4F20`.
It includes passing M02-005 writer, EFI integration, M02-004 predecessor, and
320-character serial-state-machine groups. The complete solution,
naming/source-firewall, final publish, real-QEMU, and a second complete
HostTests run must still be preserved at the exact pushed review head. This
ledger does not claim that final review gate prematurely.

## Evidence

The development real-QEMU/OVMF run emitted the exact sequence once after
M02-004 and before `TEST_PASS m01.stage3`:

```text
OWNED_COM1_READY
CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 configurationWrites=7 lineStatusPolls=18 ownedWriter=true ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial guestPortIo=true guestIn8=true postExitBootServices=false
```

The serial state machine reported:

```text
SERIAL_MARKER_STREAM status=Passed terminal=Success bytes=3032 lines=74 invalidBytes=15 invalidLines=1 orderedSeen=52 outOfOrder=0 uniqueSeen=52 duplicateUnique=0
TEST_PASS qemu.smoke
```

The summary records `MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
`DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
`FAIL_REASON none`. No `TEST_FAIL` or `M02_OWNED_COM1_READY_FAILED` marker
appeared.

| Development artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `artifacts/efi/BOOTX64.EFI` | 492,032 | `8E9F1F70738158858AC319BB4C9085D3DB7005DEF3E6A080585CCB20559F9097` |
| EFI linker map | 218,960 | `092EABCF1C3F7E9561176BBAD6ACB87EFA8453271D24B584A6BF11E5B879853A` |
| EFI linker response | 492 | `5241E77AD56D1CA2CC60935AB8D70D3B006D97DBB6624FC7A46199ECE76F3515` |
| EFI link evidence JSON | 395,970 | `4F7C1E0CF5610EA46AB3C6A94E63DD7D93A35336A70406B118D0D9BA7DAC8692` |
| serializer object | 336 | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| raw image | 67,108,864 | `0C676D8F5F2F94E52AA57401D74D1295BABD240ACF6CB7196A0FD3C88056DBE0` |
| serial log | 3,032 | `F4FA71ACE78D1E900AB3E16B8273F8F58639A99754CB2AD4B5E08FD284E11424` |
| QEMU summary | 11,845 | `12EA4D15FBEE8D08AF6D663CF1D633C9C578E6D9C14C2F94B26763BFDC194A2A` |
| raw observation log | 2,755 | `04568D63A2E2C192140AEFDEDAF237F02FCE68C84D65C45D0B6A4D4CCD58B5BA` |
| QMP transcript | 9,288 | `82E01B351AB7BBB375A451ADE642B8BA87B0F777E35A442F608F0434EAADB3D4` |
| EFI publish log | 4,292 | `DE2E4EE6370A435B993F03AB874947A074027E0830510A79488B544C6C8C512F` |

All generated artifacts remain ignored under `artifacts/` and do not enter
Git. These are development hashes from the completed implementation tree; the
tracker review comment must carry fresh exact-pushed-head hashes.

## Failures

- The first source-bound transport-separation check counted the raw
  `OWNED_COM1_READY` token and also matched the required preprocessor symbol.
  The test now reconstructs fixed UEFI formatters and rejects only an actual
  standalone UEFI formatter or quoted standalone marker; production transport
  behavior did not change.
- The exact issue command initially omitted inherited M01 switches required by
  the already accepted safe-stub profile. The highest cumulative M02 profile
  now composes only those three established prerequisites when the complete
  explicit M02 tuple is present and reports that composition. Dependency
  rejection remains fail-closed for incomplete M02 tuples.
- The next exact flagged dry-run failed before QEMU because the 259-character
  evidence marker exceeded the runner's 256-character bound. Issue #451 was
  created and claimed before touching the two tooling paths; only the ceiling
  moved to 320, and tests retain a hard rejection at 321.
- The first flagged EFI publish reached post-restore verification but failed
  `post_restore_evaluation_item_count_mismatch` because the verifier's static
  expected compile list omitted the new writer. The expected exact list was
  corrected; no artifact from that attempt was accepted.
- The next flagged publish failed
  `post_restore_evidence_property_set_invalid` because the build-side evidence
  omitted the new default-false Boolean property. Both build and verifier now
  capture and compare that exact property. Only the subsequent fresh publish is
  recorded as positive evidence.
- The first complete merge-wave HostTests run was stopped and rejected after
  the artifact verifier's synthetic valid fixture failed
  `post_restore_evidence_hash_mismatch property=compileSha256`. Its fixture count
  had moved to 54 but its separately pinned compile-closure hash still described
  the 53-item tree. The constant now matches the independently evaluated
  54-item hash
  `af4f0558712e2d1d4ab61104b0505ce9ca8256be61adad98f74ebb0c703020de`.
  The rejected partial log is 166,342 bytes with SHA-256
  `6E739A3522B30924376B29DA0C2130A2C678667D021CDDF0210848B3FCBF69FE`;
  it has a line-leading failure and no terminal host pass, so it is not
  acceptance evidence. A complete fresh run is required.
- The second complete run was also stopped and rejected at the same synthetic
  valid-fixture boundary after the corrected hash advanced verification to
  `post_restore_evidence_property_set_invalid`. The fixture dictionary still
  omitted the new `EnableM02OwnedCom1ReadyProbe=false` property even though the
  real build/verifier evidence already carried it. The valid fixture now
  includes the default-false property and adds explicit missing-property and
  enabled-without-switch negative mutations. The rejected partial log is
  166,000 bytes with SHA-256
  `C1F709BF415E94C472C2E7B7EA3D880ECF441C2D440DE997A08D409C2B934462`;
  it also has a line-leading failure and no terminal host pass. A complete
  fresh run remains required.
- The third complete run cleared the repaired artifact-verifier valid fixture,
  its complete schema-three mutation matrix, and every earlier group, then
  stopped at the pre-existing M02-004 EFI integration test. That test selected
  every MSBuild error condition mentioning
  `EnableM02Com1OwnedPortIoProbe`; the new M02-005 dependency legitimately
  mentions its M02-004 predecessor, so the old selector found two conditions
  instead of the one condition owned by M02-004. The selector now identifies
  only the condition whose enabled left-hand switch is M02-004, and the focused
  four-case M02-004 integration group passes. The rejected partial log is
  338,572 bytes with SHA-256
  `DD6B5725DF98550C869BAA54B1DC8C2B129FDE02C015556FDBF4567650593B73`;
  it records 1,319 line-leading starts, 1,317 passes, two failures, a nonzero
  process exit, and no terminal host pass. It is not acceptance evidence, and
  a complete fresh run remains required.
- The first literal pushed-head HostTests repetition at commit
  `71a88a2a2791d24b9f7b56f5aa0fd8476d68a9a3` was stopped immediately when the
  pre-existing 42-iteration stage-three missing-marker runner test failed after
  passing in the complete pre-push run. Its rejected 582,440-byte partial log
  has SHA-256
  `413686B855478EA1F45B77C1789D0598540BD1C2227D4A961C1E3843EE0F4765`
  and records 2,289 line-leading starts, 2,287 passes, one failure, no terminal
  host pass, and an intentional interrupt. The original runner group then
  passed all 155 cases in isolation; its 40,920-byte focused log has SHA-256
  `1DD458BD3DEA754E364C649A83F6C35071AC26F0F6FC8C21CB00B3699EB815F4`.
  This demonstrated a load-sensitive five-second subprocess allowance rather
  than marker-contract drift. The affected stage-three loop now uses the same
  eight-second budget as its complete stage-three success case and emits
  failure-only marker, exit, output, summary, and unexpected-pass diagnostics.
  The hardened 155-case repeat also passed with zero failures and no diagnostic
  line; because success output is unchanged, its log is byte-identical to the
  focused reproduction above. Stage-two timing and all runner classification
  semantics remain unchanged. The rejected pushed head is not acceptance
  evidence; a new commit and full exact-head gate are required.

## Deferred work

- Run one complete uncompromised final-head solution/HostTests/policy/EFI/QEMU
  gate, capture hashes, and push a clean branch.
- Post exact reviewed evidence to #45, #450, and #451, move them to
  `agent:review`, and obtain independent `admin` approval whose commit ID equals
  the current combined PR head before merge.
- Close supplemental #450/#451 only after approved merge. Managed #45 then
  requires its separately reviewed manifest-closure PR, canonical synchronizer
  apply, and idempotent preview before done/closure.
- This proves only pre-`ExitBootServices` checked COM1 reads and bounded marker
  writes on the recorded QEMU/OVMF platform. It proves no real firmware exit,
  post-firmware diagnostics, panic routing, physical/provider behavior, hosted
  .NET, Minimal API, Kestrel, CloudReady status, production readiness, or
  numbered completion-gate advancement.
