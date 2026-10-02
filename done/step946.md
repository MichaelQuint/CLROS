# Step 946 - Reviewed RhpNewFast EFI source and native artifact integration

## Summary

Supplemental #519 connects the frozen M04-007 caller-buffer allocator to a
default-fail-closed NativeAOT `RhpNewFast` export. Its opt-in adapter retains the
original M04-006 authority and loaded-image capture, authenticates ten finite
compiler tokens, and records bounded allocation/readback evidence. Independent
review accepts exactly35 owned files and the author's focused/native artifacts.
Parent integrates those exact bytes alongside the reviewed #55 host milestone.

This is source, host-controlled and native-artifact evidence. The complete
integrated host suite and five actual QEMU profiles have not run for this source
yet. #55/#76/#519 remain open; Gates3/4 remain Fail.

## Why

Step940's allocation leaf has no compiler export or real object-reference
conversion. #519 supplies that deliberately finite EFI connection, allowing
four real compiled-new routes and six direct allocation-only routes to be
reviewed before physical guest acceptance. The original heap leaf and prior
M04 contracts remain byte-identical.

## Files changed

- `src/OS/Runtime/MinimalRuntime.cs`: the reviewed compiler declarations and
  sole NoInlining export, with a self-contained nonreturning default branch.
- `src/OS/Runtime/RhpNewFastAllocation.cs`: original-authority POD adapter,
  finite tokens, bounded PE/prefix validation, fixtures, byte observations and
  terminal rejection handling.
- `src/OS/Boot/EfiEntry.cs`, `src/OS/OS.csproj`: original-image accessor and
  after-zero integration, two appended Compile items, three default-off flags.
- `scripts/build-efi-shell.ps1`, `scripts/test-efi-artifact.ps1`,
  `scripts/run-qemu-smoke.ps1`: exact source/profile/artifact authority and
  strict positive transcript; ordinary failure semantics remain unchanged.
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs` and
  `std/no-runtime/shared/DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.cs`:
  narrow EFI classifications while general runtime gates remain deferred.
- `tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs`: core corruption,
  metadata/source firewall, grammar/profile and actual native-artifact checks.
- The two ledger tests, `NoStdLibShimBoundaryTests`, both M01 loaded-image and
  instruction-publication fixtures, and20 existing EFI closure fixtures:
  declaration/export accounting, preserved history, updated pins/properties
  and the exact new checked-port conflict edge. The full35-path freeze and
  20-fixture mapping are pinned below.
- `README.md`, `docs/nativeaot-nostdlib-limits.md`, `docs/efi-build.md`,
  `docs/boot-order.md`, `docs/qemu.md`, `docs/completion-gates.md`: implemented
  behavior, source/artifact proof and remaining actual guest boundaries.
- `.gitea/backlog/clros-issues.json`: only M04-007 currentEvidence is extended;
  all other fields/entries and open intent remain unchanged.
- This step was allocated with `scripts/new-done-step.ps1 -Reserve`.

## Tests run

Author worktree:
`C:\Users\Roger\.codex\worktrees\m04-007-rhpnewfast-efi\CLROS`, detached
base `c7995bf1cc900da007d13ebe82c72a4b069545da`.

```powershell
dotnet run --project artifacts/issue519/focused-ledgers-and-m01/Focused.csproj -c Release --nologo
dotnet run --project artifacts/issue519/focused-core-attempt1/Focused.csproj -c Release --nologo
dotnet run --project artifacts/issue519/focused-final-assertions/Checked.csproj -c Release --nologo
dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1 -c Release -nodeReuse:false
git diff --check
```

The five complete ledger/shim/M01 modules pass32 starts/33 passes, zero failures,
native0. Their historical source/publication mutation groups remain included.
The complete new module attempt7 passes six starts/seven passes, zero failures,
native0, from 2026-10-02 15:46:44.9582813 through15:53:00.4622068 UTC. It runs
four actual default/positive/before-bind/exhaustion native publishes/verifiers,
each with77 Compile items,90 source inputs,27 exact flags and zero AssemblyRefs.
No QEMU runs inside these author tests.

Attempt7 used archived test source `C9DD9F1F0A3EBA2A20D3DBB8D43952B07FE10D22BC53369984FD605BDD74A87A`.
Subsequent assertion-only changes repair the checked-port count and require
actual decoded caller-token equality. The complete repaired checked-port module
passes five starts/six passes/native0. The revised actual native-test method and
helpers were copied verbatim into an isolated driver and run against exact
copies of the unchanged four artifact tuples; all native0 with0/10/1/10 route
rows. Those later checks are not attributed to the earlier test snapshot.
Current test SHA256 is `3F63309D9D492271D7A2F92EB4C643FBBB7591F50BEC75C6149DB91F3A67F28F`.
The final normal HostTests Release compile passes with zero warnings/errors.
Parent's complete integrated suite must still execute this revised whole
module and all20 legacy modules; static mapping is not an execution pass.

Parent integration at16:13:34.8324282 UTC verifies all35 copied files and all90
root source inputs against the final author inventories. `git diff --check`
passes. Parent full solution build/run and actual guest checks remain pending.

## Evidence

Author evidence is retained under `artifacts/issue519/`; parent acceptance and
integration evidence are under `artifacts/orchestration-20261001/`.

| Evidence | SHA256 |
| --- | --- |
| Final author handoff | `BE6113EF8EDB6A123ABD3139ABB0895E68172E5304121A7B51E94DC7202695A0` |
| Exact35 owned files | `9DE6E4B39197032CE851D5D5A8B3922A9AC826A7949C7A29476BB4D8D5E9F930` |
| Raw90-source inventory | `7E982CCA13B7939E934AF73991CB229734B8D3B32AC8639F7E0BECBDC2E3614B` |
| Exact20-fixture mapping | `37AC6C91955E1497A0AFB284DAAE8D8F727B332D8896D5F589D7B6D67192A087` |
| Author evidence inventory | `3B085CAECF680B3033E6043F6C1C5FA493FB721019B59750D999EAB50D41A52E` |
| Attempt7 raw focused log | `DF94DC6F0FEADD777F2EEEA1287C1AA51EFB75665FC5B4368E43E1D4560D54AF` |
| Attempt7 command/result | `8C854E23F0928ACB816AD8B4765AAD7CD96780FB39B75DF8D082CC18A654D5E7` |
| Four final artifact lanes | `6A1A1525BCE3538C5FBDBFC76677153E678107997268674365345FD175A8B2F5` |
| Decoded positive native table | `5701493614500EBA70892DF6EA0AE2E85C403CD7F3D563AA24BCB42A01134FED` |
| Independent source/evidence review | `F4FF8D9CFAE49ABBDD676D32C2F42022687D049D3E6DD0731EAAE131FA873C2B` |

The90 raw-input aggregate is
`6DF0C8AD12D841B26B71EB80435BEED6A22E891B52BF8A9CAE94AD702171DFF8`;
ordered Compile hash is
`37B56FEAAE9F232DD547A34D4DB872C5E998B2EB7D501EAF422C0C5F97034A79`.
The independent review rehashes all35 source files,90 authority inputs,
26 final evidence entries and each saved native tuple, checks all27 named
profile values, and independently reads each archived managed DLL as metadata
only with zero AssemblyRefs. It accepts the scoped source; it does not accept
any guest. The final Runtime SHA256 is
`E8776789A35666DFB5916ECF1DEFB3F45EDABAF787F8670611EEF7C27B2AB5D4`.

Separate ignored acceptance helpers were reviewed before actual execution:
the transcript auditor has799 synthetic controls plus16 CLI controls, and
parent100 additional probes/110 package-file rehashes. Its review SHA256 is
`446E79AAAE78B3DD724278FD48089A125399654835EA24AA7841657C84B826FA`.
The repaired five-lane capture workflow passes21 mocked/inert controls;
review SHA256 `8C890C028E6B9C4DD8701C9F485E0AB007E8E08522F9B7CF97F45A926248BBBA`.
The previously accepted negative observer and COFF/PE prefix auditor remain
separate inputs. Every preparation result keeps GuestAccepted false. Actual
final profile/source/helper pins and complete same-head host acceptance must
precede the guest workflow.

## Failures

All author failures and canceled attempts remain preserved. They include the
original frozen-leaf scanner rejection before exact-path/hash admission;
missing test using/synthetic nullable fixture errors; stale75/77 verifier count;
PowerShell test-array and UInt64 mask issues; missing built-in module path in a
child shell; ledger wording obligations; and a mistaken native-driver argument.
No failed publish or partial run counts as accepted evidence.

Attempt5 was deliberately canceled before artifacts to repair rejection
observation from16 to all80 bump bytes. Attempt6 was canceled for the PREP
record exposing actual image/control/records extents; its completed default and
positive artifacts remain superseded. PREP's stale77-line diagnostic was fixed
to78. Review also repaired the checked-port five-condition assertion and
decoded caller-to-token association before final freeze.

The capture helper's first review found culture-sensitive Boolean matching,
incorrect approved-host file archival, incomplete partial-publish preservation,
missing cleanup after exceptional waits, and a wrong observer schema key.
All were repaired and independently retested before any guest invocation.
Parent/reviewer fixture mistakes are recorded separately from product failures.
The first parent done-step allocation invocation was blocked by the shell's
script-signing policy; the documented explicit process-scoped ExecutionPolicy
Bypass invocation reserved step946 successfully. No persistent policy changed.

## Deferred work

Run the clean integrated solution build and complete host suite. Freeze the
five-lane source/profile and acceptance-helper inputs; then run actual default,
standalone heap, positive, before-bind and exhaustion guests. Independently
bind each native caller/token/constructor/reader and every observed24-byte
prefix, including COFF-required DIR64 fixups with signed load delta. Review the
whole raw transcript, measured quiet drain/native exit and pristine/booted disk
accounting before issue closure or PR520 merge.

The #55 host milestone still needs its separately scoped vector8/IST1 EFI
integration and guest proof after this shared lane is accepted and released.

## Evidence boundary

No actual allocation guest, real ExitBootServices, general allocator, GC,
reference writes/barriers, boxing payload copy, nullable substitution,
arrays/strings, cctors/dispatch/exceptions, post-firmware lifetime, hosted .NET,
Kestrel, cloud-provider or production readiness is established. Source has
three false-by-default flags and fails closed on internal or leaf rejection.
All generated artifacts stay ignored. The original frozen leaves, BootSequence
and Probes remain unchanged; dangerous probes stay disabled by default.
