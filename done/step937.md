# Step 937 - Original-allocator heap-page EFI integration

## Summary

Integrate the independently reviewed supplemental #516 adapter for #75.
The disabled-default heap profile retains one page from the original M01
allocator and validates it again immediately before the terminal boot plan.
This step records source integration and focused host evidence; full host and
fresh NativeAOT/QEMU acceptance remain pending.

## Why

The host leaf alone cannot establish an EFI handoff from the original M01
allocator. Its adapter must run after every historical completion consumer,
retain the original parent, and avoid freeing or restoring the retained page
on either success or failure. Acceptance also needs exact observed addresses,
accounting, readback and lifecycle evidence from a fresh guest.

## Files changed

- `src/OS/Boot/EfiEntry.cs`: stable 216-byte heap state, original allocator/node/
  ledger handoff, initial reservation and fresh pre-plan validation, numeric
  observations, and terminal ownership-preserving failure behavior.
- `src/OS/OS.csproj`: one frozen leaf include and disabled profile with explicit
  prerequisites/exclusions; 70 Compile items and 83 source-authority inputs.
- `scripts/build-efi-shell.ps1`, `scripts/test-efi-artifact.ps1` and
  `scripts/run-qemu-smoke.ps1`: complete 23-property binding, exact-source
  firewall admission, build/profile checks and strict heap transcript checks.
- `tests/OS.HostTests/M04ManagedHeapPageRangeEfiIntegrationTests.cs`: actual
  adapter, closure, property, artifact, transcript and firewall controls.
- Eighteen existing host fixtures: build/artifact verification, twelve M02
  profiles and four earlier M04 profiles. Changes update directly affected
  closure/count/hash and profile-condition assertions only; the exact 24-file
  handoff list is preserved with its source hashes.
- `docs/nativeaot-nostdlib-limits.md`, `docs/efi-build.md`, `docs/qemu.md` and
  `docs/completion-gates.md`: current integration and pending evidence boundary.
- This step, reserved with `scripts/new-done-step.ps1 -Reserve`.

The frozen #75 leaf/test pair, runtime shim, generic QEMU runner, standalone
layout validator, BootSequence and Probes are unchanged.

## Tests run

- Author focused Release harness: actual exit 0, eight paired module starts/
  passes plus one harness terminal, zero failures. Includes actual enabled
  no-stdlib compilation with zero AssemblyReferences and NoInlining readback.
- Actual-source adapter: 50 controlled cases; all 200 original ledger bytes
  preserved. Host backing/address observations are not guest execution.
- Strict transcript: 506 controlled raw captures plus native Int32 exit and
  lifecycle controls; the complete CLROS transcript contains 52 records.
- Artifact authority: controlled schema-three PE fixtures for default, UD2
  and heap, full prerequisite rejection, and all 23 property flips/missing/
  embedded-NUL values through the actual comparator. Synthetic PE is not AOT.
- Affected legacy harness: 15 targeted closure/project methods, exact default
  preflight and raw UD2 authority pins. This is not the complete legacy suite.
- Default and enabled seven-flag preflights: actual exit 0 / EFI_PREFLIGHT_OK.
  Enabled was rerun serially after the preserved concurrent-output lock error.
- Author HostTests Release build: exit 0, zero warnings/errors; not a full run.
- Independent review verifies 24 LF source/test hashes, all 83 authority
  inputs, 70 ordered Compile items, 23 evaluated properties, 20 XML defaults,
  all 36 evidence-log hashes, focused marker pairs and actual native-exit
  validation including controlled drain/disposition negatives.
- Parent guarded integration verifies all 24 copied source/test hashes and
  all 83 authority inputs, no overlap since the author's base, and tracked
  whitespace cleanliness. Source review did not execute a full suite or guest.
- Fresh parent `dotnet build .\CLROS.slnx --nologo -m:1`: actual exit 0,
  zero warnings/errors, 21.87 seconds and empty stderr. Naming/source-firewall
  checks and default QEMU dry-run each exit 0; dry-run is not boot proof.

## Evidence

Author worktree: `C:/Users/Roger/.codex/worktrees/m04-006-efi/CLROS`, base
`5ce03907f176ad83960c3c3675718ad2b7cd6169`. Parent integration begins at
`346d7fef3bd8df5537f944974b1fb652f0846b2b`. The frozen report, inventory,
independent review and 36 preserved logs are copied under
`artifacts/orchestration-20261001/issue516-handoff/`. Full source/test and
authority identities are in `final-identities.json`; commands and exact log
paths are in `implementation-report.md`. Original successful and failed
controlled fixtures remain in the author worktree's ignored artifacts.

| Item | SHA-256 |
| --- | --- |
| Author report | 9B2A13552A39492CD92DF689C33357AF98B7ADD781C9BD39195B8D672AA16422 |
| Final source inventory | 9ABD744586121D84B1782D0DF8C7ED2BBC481351BDAC36303F599BBCBDEDA7A6 |
| Independent source review | 3B0EFF079781851FA93688EC36583311D4EE3CF4F35EEDF5C7CC128F39402468 |
| EfiEntry raw bytes | B575A610862F24F2B71529D70210A4042258546D972CD3F46093729CBA9FBF0C |
| OS.csproj raw bytes | FFBF4B681939C97C5F0B9EAE2AFCCFE40D1F13332767ED0CFF05086D62B9E2ED |
| Ordered 70-Compile identity | 06D136C83FD42C3979662ECF7BAC6CD1A7654113C5F2392CC6BF3B16DEC04561 |
| Raw 83-input authority | 48151B6C038674F0C9B7FCD43A2DA87CE9766CA2480AF9E4FAAF5BC546111BBC |
| Parent solution build stdout | A110B0B450B3BBF1AD883399A91F2C82E1ADF6D859EBBA6474F35F285AE00C1F |
| Parent naming/source-firewall stdout | F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651 |
| Parent default dry-run stdout | 7DD7378240F824F7EE2F8A4971BDD9A2FB7769E76279259CAF6AB82CD0B1F4BD |

Parent logs use the `issue516-root-solution-build`, `issue516-root-naming` and
`issue516-root-dryrun` prefixes under `artifacts/orchestration-20261001/`.
Independent parent integration review is `issue516-handoff/parent-review/`;
its source/doc review reports no findings. Full-gate results are not included
in these focused build and preparation records.

## Failures

Preserved author attempts include canonical project identity/source-firewall
preflight failures, an AddressValueStatus comparison and inaccessible nested
Spin compile failure, a fixture hash typo and nonadjacent reservation, missing
verifier arguments, Windows PowerShell UInt64/array handling, authority-harness
attribute stripping/name collision/property-count mistakes, excessive repeated
verifier timeout, and stale legacy counts/selectors/raw pins. The settled
source and passing focused runs supersede these attempts without removing them.
Parallel default/enabled preflights collided on generated output; default
finished and the enabled preflight passed when rerun alone. No process was
killed to bypass that lock. The source firewall exception is limited to the
exact reviewed leaf path and whole LF-normalized source hash.

## Deferred work

Run and independently accept one complete settled-source host gate. Then build
fresh default/layout/heap source-bound EFI/image/QEMU tuples, independently
audit raw transcripts, original allocator observations, actual runner lifecycle
and corrected FAT geometry/payload/accounting. Complete docs/manifest/tracker
evidence, review and merge before closing #75/#516. #514 and the separate #513
host emitter also await the combined wave's acceptance and merge.

## Evidence boundary

No NativeAOT publish, live guest address, boot, hardware fault or FAT acceptance
is claimed by this step. The adapter's ledger validation is current bounded
state, not a historical snapshot or unforgeable ownership registry. Pointer
identity/free-list checks do not provide clone/ABA or SMP protection. No object
allocation, GC, hardware NX, installed paging, post-ExitBootServices lifetime,
hosted .NET/Kestrel or cloud readiness follows. Gates 3 and 4 remain Fail.
