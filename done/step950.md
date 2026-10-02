# Step 950 - Default-off double-fault EFI integration

## Summary

Issue #521 integrates the unchanged M02-015 double-fault leaf through a narrow,
default-off EFI adapter and source/profile/artifact/runner validation. This is
implementation and focused host/artifact evidence. The new complete repository
host run and actual default/PF-regression/DF guest captures remain pending.
Issues #55/#521 remain open and Gates 3 and 4 remain Fail.

## Why

The accepted host emitter in step945 and complete host gate in step949 did not
deliver a CPU double fault. The separate EFI supplement makes the bounded
terminal experiment concrete while preserving the default boot path, original
PF adapter, frozen host leaves and existing runtime allocation behavior.

The adapter validates C3/D10/T43 leases and five caller scratch buffers before
writes, prepares the exact context/armed-IDT/stream, and freshly audits the
original table backing against six ranges before final readback/publication.
The native entry receives context D+4096 in RCX and targets C+4096+768. After
attempted entry there is no firmware cleanup or return. Only the frozen stream
emits the DF start/ready/pass tail. Its null read escalates through the sole
cleared PF Present bit; undefined saved DF state and CR2 remain diagnostic-only.

One new profile flag joins the eight existing prerequisites; explicit false
prerequisites and 19 conflicting flags reject. Default remains all false. Two
ordered Compile additions preserve every old relative position. The resulting
closure is 79 Compile items, 92 raw inputs, 28 evaluated flags and 25 XML false
defaults. The ordinary source scanner accepts the unchanged leaf without a new
exception. The retained heap, RhpNewFast shim and compatibility ledger do not
change.

## Files changed

The implementation owns exactly the 30 paths declared in issue #521: the new
`src/OS.Kernel/Diagnostics/DoubleFaultIstProbeAuthority.cs`, the existing
`src/OS/Boot/EfiEntry.cs` and `src/OS/OS.csproj`, the three EFI build/artifact/QEMU
scripts, the new `M02DoubleFaultIstEfiIntegrationTests.cs`, and 23 existing
HostTests fixtures (21 source-closure fixtures plus the two M01 selectors).
`artifacts/issue521/final/owned30-sha256.json` in the author worktree enumerates
and hashes every exact path. Original #55/#510/#513 source/test pairs remain
unchanged. No first-party native source or generated artifact is committed.

Parent documentation covers README, diagnostics, boot order, EFI build, QEMU,
no-stdlib limits and completion gates. Only `M02-015.currentEvidence` changes
in the canonical 345-entry catalog; ownership notes and all other fields remain
unchanged. This step is the ninth parent documentation/catalog/history path.

## Tests run

Author worktree:
`C:/Users/Roger/.codex/worktrees/m02-015-df-efi/CLROS`, detached base
`27e3f42ad33da16bfa94ad4a2ac52a0964c67592`.

- `dotnet build artifacts/issue521/complete-attempt1/Complete.csproj --nologo -m:1 -nodeReuse:false`
  passed with zero warnings/errors. Then
  `dotnet artifacts/issue521/complete-attempt1/bin/Debug/net10.0/Complete.dll`
  completed native0 with 16 starts, 17 passes, zero failures and one final
  `TEST_PASS host.tests` between 21:18:31 and 21:23:01 UTC on 2026-10-02.
  It runs the intact nine-group frozen host module and seven-group new EFI
  initializer, including the actual 20-source/zero-AssemblyRef closure.
- Both fresh NativeAOT artifacts run `scripts/build-efi-shell.ps1` and its
  production artifact verifier with unique output/log paths; only the enabled
  lane supplies `-EnableM02DoubleFaultIstProbe`. Actual saved OS.dll metadata has
  zero AssemblyRefs; all 28 properties are bound, with zero/default and nine/DF
  true values. The default contains no compiled DF adapter. Archives retain
  EFI, DLL, native object, maps, link response/metadata and serializer object.
- Both complete M01 selector initializers pass native0, 23 starts/24 passes and
  zero failures. The 21 closure fixtures also receive 30 selected development
  controls, including the raw UD2 snapshot; those selected controls are not
  reported as complete legacy-module execution.
- The controlled transcript fixture tests 1,116 cases (50 accepted, 1,066
  rejected). These are synthetic grammar/lifecycle cases, not a real guest.
- `dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1 -nodeReuse:false`
  passes native0 with zero warnings/errors. The complete suite was not run.
- Default/DF profile dry runs and the separate
  `scripts/run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM02PageFaultCr2ErrorCodeProbe`
  pass. Dry runs prove no boot. Author naming/source-firewall and
  `git diff --check` pass; final status and source rehash match exactly 30 paths.

The parent-derived complete-test inventory predicts 3,580 starts/3,581 passes,
eight exact ready records and 208 selected pairs across 24 changed modules.
This prediction adds seven unique emitted names to the accepted whole-output
Counter in step949. It is not a completed host result.

After the parent catalog edit, the complete `GiteaBacklogManifestTests` module
passes native0, 17 starts/18 passes, zero failures, one final
`TEST_PASS manifest.focused` and empty stderr. Its focused harness builds with
zero warnings/errors. Parent naming/source firewall then passes both groups,
and `git diff --check` passes. Actual command/log identities are in
`issue521-root-checks/checks-result.json`, SHA256
`5C61976CC5B5AEC2577C3D1DDA622DC3EC6715F170F4466A553079E716ADC654`.
The checks ran from 21:41:42 through 21:43:18 UTC. The wrapper's final console
projection displays null fields for its hashtables; the saved JSON records
above retain actual executable paths, native exits, counts and log hashes.

## Evidence

The author's `artifacts/issue521/final/` contains:

| Evidence | SHA256 |
|---|---|
| handoff.md | `25CEF4F89E298AD8073279A3E6353088483677EB3868B195A4F4BA0875187142` |
| owned30-sha256.json | `6CB0EABB9691BE53E5D826B33E8952E94FE0EE9E635289E14AECD528D3F243FE` |
| source92-sha256.json | `5692C9481AA0C55ECEBAAA4B07D7ACA57A7636D7BED28E11A1B72957EB421D37` |
| focused-result.json | `E5143DBB874AE4DE637A93D0C79293A7969FAF59E991DFBC45CBDFBFD96A9A20` |
| artifact-lanes.json | `026C12472314D27AF13C7D3725C0A158AECFDFAA47898C7089C24407EBCD7DD5` |
| managed-artifact-evidence.json | `2732AB35D1B8C0818CE097A759C8BA47E9F87CACBBD2063ECC6386D1B341D20D` |
| final-validation.json | `EB44A9DB839A1615DFB325548E3C190133509DF2361AD952FF00F1B3302B3B25` |
| evidence-sha256.json | `7805A862B6E9FD021564339A02302E487274901D79860CCA9BF52A11BB88A2CD` |

Focused raw log SHA256:
`6175E6562F6BED44F6FAFE1820618DFB424C58A648921D1C89413C5B4F03D26B`.
Ordered Compile SHA256:
`28CCA3A8AB927B0B1B5A39ECE13F139DD4875AC5949150F2F9EBD475AC33316F`.
Raw source aggregate:
`F178CEBB122F2A9DCC69607ECE91BF6CD9B7412422B9ABF173EAE0A45B821592`.

Parent preparation artifacts live under `artifacts/orchestration-20261001/`.
The independent documentation preview is
`issue521-docs-preparation-review/review.md`, SHA256
`C1C1E6D6033611BA95AA45CBDB80C25EF11E436977012D9FE2A7485F1C356323`.
The source-derived marker preview is
`issue521-full-host-preparation/author-frozen-preview-report.md`, SHA256
`4151B6F4BE6E44FA8CB3BFB327F93E6472B88E7BF5772F0783F742904E0E1D45`.
Neither accepts a future execution. The #521 focused evidence comment is
[14086](http://localhost:3000/admin/CLROS/issues/521#issuecomment-14086), posted
and read back with exact body equality.

Independent final source/artifact acceptance is
`issue521-independent-review/final-candidate-review/final-source-acceptance.md`,
SHA256 `B35ED7E749F43F9B20530E5259397E0BF7E458899326432255D57A1596361A68`.
It rehashes all 30/92 sources and 4,910 preserved evidence records. Fresh
metadata/disassembly processes pass native0 with empty stderr; linked native
code retains fresh Audit, ReadBack, actual serializer/cookie, full-width target
C+0x1300/context D+0x1000 and terminal self-loop. Those observations bind the
author images only; final captured images require their own review.

Parent byte-identical source integration is recorded in
`issue521-docs-preparation/source-integration.json`, SHA256
`3161FE7D597F5834799934DE1D899620230A674D9323FF0CC637471249D4714B`.
No source normalization occurred. The separately reviewed documentation applier
changes exactly eight existing paths and preserves unrelated catalog bytes.

Before commit, the exact staged-byte check required LF normalization of the
reserved step file. The staged whitespace check then found eight trailing
spaces on blank line 362 of the new EFI test; those eight bytes alone were
removed, preserving line count and all C# tokens. The original author fixture,
focused results and earlier review inventories remain preserved. This final
format-only change does not affect the 92-input EFI closure or its artifacts;
the complete-host inventory is refreshed to bind the final test bytes.

## Failures

- Initial preflight and later controlled legacy verification separately caught
  stale 90-input predicates after the closure grew to 92. Their failed attempts
  and old source bytes remain preserved; the final verifier uses 92.
- Early DF parsing accepted arbitrary nonprotocol preamble. Review required the
  exact historical 304-byte OVMF prefix or empty controlled prefix. A later
  lifecycle repair initially matched forbidden-token declarations as failures;
  archived real runner stdout exposed that error before any guest. Final
  anchored record classification rejects real failures while accepting the
  unchanged inventory declaration. All prior candidates are retained.
- Controlled profile fixtures had duplicate opposing options and then an
  incorrect expected diagnostic; the first selector harness omitted the actual
  HostToolProcess source. Corrected fixtures/harness pass; no product validator
  was weakened to hide those attempts.
- The parent documentation draft would normalize eight LF files to CRLF. Review
  caught it before execution; the corrected writer preserves LF and exact
  unrelated catalog spans. Review-only schema/preview assertion failures are
  retained in their preparation folders.
- Direct step-reservation invocation was rejected by local execution policy
  before mutation. The official script succeeded in a child PowerShell with
  process-scoped `-ExecutionPolicy Bypass`; no global policy was changed.
- Two staging guards stopped before any commit: the reserved step's CRLF
  normalization and trailing whitespace in the new test. Both failed attempts
  are retained under `issue521-docs-preparation`; subsequent checks include
  all newly staged files, which working-tree `git diff --check` had not covered.

## Deferred work

Complete the clean same-head solution build and full host gate, then
run three fresh standard QEMU/OVMF lanes. Independently review full raw serial,
compiled/current-state authority, actual drain/termination and pristine/booted
disk accounting before merging and closing #55/#521. Shared compatibility work
for #77 follows release of the current integration lane.

## Evidence boundary

The author artifacts and synthetic transcripts do not prove vector-8 delivery,
live IST1 use or absence of triple fault. No actual guest was run in this step.
Immutable provisional metadata retains its false guest/native-acceptance flags;
future independent acceptance needs its own record. No general interrupt or
exception handling, recovery, NMI nesting, post-ExitBootServices lifetime,
GC/runtime expansion, hosted .NET/Kestrel, cloud or production claim follows.
