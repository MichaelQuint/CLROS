# Step 979 - Executing-stack sentinel source and focused validation

## What changed and why

The step977 reference guest returned through the bounded helper route, but did
not check a sentinel on its actual executing stack. #537 therefore remained
open with #79, leaving #80 blocked. This continuation adds the missing source
path and its focused controls; it does not claim the later guest acceptance.

A conditional EfiEntry getter validates the original private M01 static state,
retained switch record/plan, backing identity and earlier continuation RSP. The
reference route supplies a real local-address witness and requires a 16 KiB
margin above two distinct words at the bottom of the 33-page usable stack.
Admission checks complete extents, aliases and original reference authority
before writing the sentinel or a separate 96-byte POD state.

The words are armed once and read through an ordinary non-inlined C# helper.
Five subsequent checks follow callback counts 18, 19, 20, 22 and 23. Corruption
is terminal and cannot rearm. The last check follows full retained verification;
one `M04_REFERENCE_STACK` record then precedes READY. Returning callbacks,
allocator, generated thunks and the original thirteen-input heap handoff are
unchanged. This adds no probe, Compile item or runtime feature outside the
existing default-off reference profile.

## Files changed

- `src/OS/Boot/EfiEntry.cs`: conditional getter and scalar forwarder only.
- `src/OS/Runtime/CheckedReferenceBarrierAdapter.cs`: sentinel state, admission,
  arm/check/read and final record.
- `scripts/run-qemu-smoke.ps1`: exact 71-record protocol, 19-record reference
  block, STACK marker vectors and strict field/geometry checks.
- `tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.cs`: complete
  mapped controls, source/default isolation and real-streamer negative cases.
- Four compatibility fixtures: M02 IDTR, owned IDT, UD2 and M04 String; only
  mechanically derived entry/source hash expectations change.
- Canonical runtime-limit, EFI-build and completion-gate docs, three M04 catalog
  evidence rows and this step. No issue state or readiness label changes here.

This step was reserved through `scripts/new-done-step.ps1 -Reserve` at
2026-10-04 14:35:00.681334-14:35:00.932811 UTC, native exit 0.

## Tests run

Author worktree: `C:/Users/Roger/.codex/worktrees/m04-reference-stack-canary/CLROS`,
branch `codex/m04-reference-stack-canary`, based on merge
`34a9fd562384a627decb35c2378f86c22b7a427e`. Paths in this section are relative
to that worktree. Final source is `artifacts/issue537-stack-sentinel/source-checkpoint6`.

The exact build/run arrays are in its `command-plan.json`. They use .NET 10
Release, single-node builds, disabled build servers/shared compilation and the
native Windows PowerShell module path. The focused project directly includes
the whole original five-group reference initializer, normal helpers and actual
Kernel/Std/QemuRunner projects. No filtered/reflected subset is substituted.

- The complete M01 source module passed on the final typed-equality EfiEntry:
  13 START, 14 PASS, zero FAIL, native 0; run at 14:24:23.367279-14:24:24.837438
  UTC. Checkpoint6 only changes two reference fixture strings, so this actual
  unchanged-source result is reused.
- Final reference build: 14:27:05.567714-14:27:08.462418 UTC, native 0.
- Complete reference run: 14:27:08.531423-14:32:36.112902 UTC, native 0,
  **5 START / 6 PASS / zero FAIL**, one host terminal and empty stderr.
- Normal HostTests compilation: 14:32:36.190632-14:32:54.821611 UTC, native 0,
  zero warnings/errors and empty stderr.

The full reference run includes its actual seven-group mapped core child,
strict transcript and real SerialMarkerStateMachine controls, all profile
prerequisites/conflicts/default isolation, and both fresh default/enabled
NativeAOT publications with zero AssemblyRefs. Mapped controls cover the full
five-check sequence, 20 admission failures, 10 provenance/phase failures,
each/both-word corruption, one-shot/retry refusal and exact margin boundary.
The new state measures 96 bytes in that mapped child. Full patterned state and
stack snapshots check rejection preservation. The three new streamer controls
reject missing, duplicate and after-READY STACK records. All original outer
registrations and old rejection checks remain.

The raw transcript child covers 552 cases (two accepted, 550 rejected). All
116 profile commands complete: 114 intended nonzero rejections and two expected
dry-run successes. These negative-case exits are expected controls, distinct
from the three failed outer attempts below.

## Logs, identities and review

- Final raw EfiEntry and independently LF-normalized hash:
  `18E22A183B2F01A6A1B6BD26322B93C44005BB37AD74B09E986CEAA18E552614`.
- Ordered 88 Compile items and 38 flags are unchanged. The complete 102-input
  raw aggregate is `341ce1b74d47028a9cf7ce96fa5f70fc1ac51893938673a45237268e3c712ed6`.
- Final eight-file manifest:
  `F509933487DCB81C76497BBD55DE209E28F203DA24D7C854643650259D050144`.
- `focused-attempt4/result.json` records all three successful final stages,
  exact commands/UTC/native exits, raw streams and unchanged recorded inputs
  and launched binaries. Reference stdout SHA256:
  `8C5C86F5A17204DDAAD1E2FFE2355FCAE0492227235DBD4705565D8BE1717B78`.
- `final-focused/native-artifacts.json` binds the two terminal archived
  publications: `76FAEB998A316473D2970E4FABAB246DB2EE6AC64E6190F1503B34C37101C57E`.
  Enabled EFI SHA256:
  `5CCA886E4676FC4763B561E2F3ABF951F4475D936819E0732B793CAD9DBF4450`.
- Parent read the complete source delta, controls, actual final streams and
  reports. Independent source review and additive correction reviews are under
  root `artifacts/orchestration-20261001/issue537-stack-sentinel-source-independent-review`;
  final checkpoint6 addendum SHA256:
  `C905712F42E52E212C4B55665E410C36B8003B80CE4AC2654CA0121A7D0338A7`.
- Parent imported exactly the eight immutable source byte sequences after
  clean-root and successful-result checks. Root `issue537-stack-integration`
  records original/imported hashes and explicitly leaves native/common/guest
  acceptance false. `git diff --check` passed after source import.

The final author report is `final-focused/report.md`, SHA256
`AE8471BDF4E4B623D337A32F8C884279738063D2867B3103FB0D0BA903757AB9`.
Its 11-file manifest is
`9ED4BAAB8A84DEC130CBFE72314335664341911FBDF92A8DB50C45B15DA6A09F`;
the package binds 1,040 recorded inputs and 2,384 final-attempt evidence records,
alongside the preserved failures.

Root integration checks on 2026-10-04 all returned native 0 with empty stderr:
naming/source firewall (14:39:24.119378-14:40:19.688458 UTC), ordinary QEMU dry-run
(14:39:24.116474-14:39:38.606543), offline validation of all 345 catalog issues
(14:39:24.138983-14:39:42.276399), and catalog SelfTest with 28 policy cases and
zero actual API calls (14:39:24.123541-14:39:43.355703). Exact records are the four
`issue537-stack-*` results under root
`artifacts/orchestration-20261001/issue531-final-transaction`. The dry-run is
command/preflight evidence only. Source import and documentation changes remain
separate from the pending committed-head build, complete suite and guest checks.

## Failures

Three failed attempts remain preserved with their source, child archives and
separate logs; none is represented as passing:

1. Attempt1 passed M01 and the mapped core, then rejected the valid transcript:
   the existing numeric record reader did not support the new `true` schema.
   The repair requires the exact ordinal final ` preserved=true` suffix and
   parses the remaining fourteen numeric fields with the unchanged reader.
2. Attempt2 passed four reference groups and the default publication, then the
   enabled compile failed CS0019 on four unsupported typed-address `!=`
   expressions. Existing typed `Equals` methods replace those comparisons;
   they compare scalar values without boxing. Four measured fixture pins were
   updated; the default artifact alone was not accepted as a successful batch.
3. Attempt3 passed M01 on the corrected entry, then stopped because two source
   fixture strings still expected `!=`. Only those expected strings changed;
   ordinal matching and every other assertion remain. Attempt4 reran the
   complete reference initializer successfully.

All three reference process failures returned signed native status -532462766.
The original source reviews missed these defects and their addenda acknowledge
them. Passing tests were required; no predicate, group or acceptance criterion
was removed to obtain success.

## Deferred work and evidence boundary

Fresh native review must establish actual word stores/loads, the static state
layout, original authority, witness frame offset and complete changed call/access
ancestry within the proposed 16 KiB margin. NoInlining and mapped controls alone
are insufficient. Historical 9,456/9,072-byte bounds apply to the step977 image,
not automatically to this changed image. Serial/firmware calls are outside the
bounded returning-helper proof; no arbitrary firmware stack bound is claimed.

The current-image complete common gate, fresh QEMU/OVMF captures and independent
raw/lifecycle/disk acceptance remain pending. #537/#79 remain open and claimed;
#80 stays blocked. A local witness is not a sampled RSP, and durable sentinel
readback will not prove transient peak usage or interrupt safety. No general
WriteBarriers/collecting GC, wider-vector preservation, unwind, real
ExitBootServices, hosted .NET/Kestrel, cloud or production gate is promoted.
The separately committed #60 host entropy work remains host-only with #61 blocked.
