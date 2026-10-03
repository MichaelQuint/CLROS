# Step 953 - Source scanning, probe-default repair and callback stack findings

## What changed

The #525 naming/source-firewall script builds one admitted file list using
immediate-child traversal. It rejects excluded names before entering directories
or admitting files and reuses that list for all three original checks. Directory
reparse points remain untraversed, provider hidden-file visibility is retained,
and untracked files remain covered. Existing roots, exclusions, naming-only
exceptions, source rules, exit codes and stable markers remain unchanged.

Four focused test groups verify actual directory visits, pruning, untracked
files and related provider behavior. The twelve original source-firewall groups
and sixteen repository-recovery groups remain intact.

The M02-015 EFI source fixture also corrects its explicit XML false-default count
and diagnostic changed from 25 to 28. Its exact predicate is retained; the three
added M04-008 array probe selectors already default to false in the project.
Its complete real initializer passed, including both NativeAOT artifact lanes.

Canonical diagnostics now records the independently reviewed compiler finding
that the #56 host-only one-page callback stack is insufficient. Its product
layout remains unchanged pending the separately owned native integration.

## Why

The old source scanner recursively enumerated excluded trees before filtering
them, three times. Early pruning avoids that unnecessary traversal while
preserving admission rules. No timing-based speedup is claimed.

The complete host run at b5376c0 exposed the separate stale default count.
Earlier review updated the fixture's Compile/raw/evaluated cardinalities but
missed its 25-default assertion. This step retains that failed evidence.

## Files changed

- `scripts/check-naming.ps1` and `tests/OS.HostTests/SourceFirewallTests.cs`.
- `tests/OS.HostTests/M02DoubleFaultIstEfiIntegrationTests.cs`.
- `docs/repo-audit.md`, `docs/diagnostics.md` and this reserved step.

## Tests run

- #525 complete focused SourceFirewallTests and RepositoryRecoveryTests:
  Release harness build exited 0 with zero warnings/errors; the run from
  2026-10-03T03:33:45.848236Z to 03:34:57.119631Z exited 0 with 32 starts,
  33 passes, zero failures, one host.tests terminal and empty stderr.
- #525 direct naming script: exit 0, both stable guards passed, empty stderr.
- Integrated root naming script: exit 0, both stable guards passed, empty
  stderr at 2026-10-03T04:25:11.0474808Z to 04:25:50.6431672Z. Raw stdout SHA256
  `F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651` is retained in
  `artifacts/orchestration-20261001/issue523-host-repair/naming-check/`.
- Complete integrated host run at b5376c0: failed; details below.
- Corrected complete M02-015 module: build exit 0, zero warnings/errors;
  run 2026-10-03T04:14:37.454122Z to 04:19:06.120268Z exited 0 with 7 starts,
  8 passes, zero failures, one host-only readiness record, one host.tests
  terminal and empty outer stderr. Both actual default/DF artifacts passed
  publish and verification with 80 Compile/93 raw inputs/31 flags and true
  counts 0/9; the complete module requires zero AssemblyRefs. Nested tool output
  uses the existing combined stdout/stderr capture; no separate-empty claim is
  made for those nested streams. The images were not executed.
- `git diff --check` passed after root integration. The subsequent clean
  full-suite retry remains pending; no new full-suite or guest success is
  established here.

## Evidence

- Parent #525 review: `artifacts/orchestration-20261001/issue525-parent-review/`
  `review.md`, SHA256
  `87835E988285F8EAD291A00ED6D8573C199EB815FEFE649B4B7982515048627D`.
  All 29 author evidence identities and exact focused marker pairs were checked.
- #525 focused stdout SHA256
  `93D45A1D77E39795199294C8FD62EA475DB095CFFF297E2D0C23FBA922950D20`.
- Guarded root integration of the two reviewed #525 files is recorded in
  `artifacts/orchestration-20261001/issue523-host-repair/naming-integration.json`.
- Independent failed-host diagnosis:
  `artifacts/orchestration-20261001/issue523-full-host-failure-review/review.md`,
  SHA256 `7C6883FAE90E6AE971020849548F858543D51F45BE865EFE6334687CA814184B`.
- Complete repaired-module commands and raw logs are retained in the author
  worktree's `artifacts/issue523-default-count-repair/`. Parent read the report,
  rehashed all 1,297 evidence identities, checked exact complete marker pairs,
  and verified the sole source substitution before copying it into root.
  Result SHA256 `C7E845318CB61347E1357C1E2B94BDB49BB74AF35510A4DEC26083013F632469`;
  stdout SHA256 `026D5897B3935B6186046157F3F22C7637FDE39FD0AE72345D3E954357828904`;
  final source SHA256 `C17B2A9C4273A47251C27276D6DDDACCCF3778EF5BE6E888FB67212626582111`.
  The 93 EFI inputs, other 40 frozen author paths and 112 previous artifact
  identities remained unchanged. Parent integration is recorded in
  `artifacts/orchestration-20261001/issue523-host-repair/df-repair-integration.json`.
- #56 provisional callback compile succeeded with zero AssemblyRefs; independent
  COFF/map/PE analysis proved 80 bytes of writes below the proposed 4 KiB stack
  and a depth of 4,240 bytes. The full conditional bound is 5,096 bytes,
  including the 232-byte entry, for two exact assumed zero-frame IN/OUT targets.
  Parent read and rehashed the independent lower-bound and whole-chain reviews.
  The latter is
  `artifacts/orchestration-20261001/issue56-conditional-stack-independent-review/review.md`,
  SHA256 `4F54E4E888AC1611BAF987F87261D9F09F2F8FBB03D6B41F16856714098E579A`.
  This does not approve an 8 KiB replacement or the future real adapter.

## Failures

- The full host run started 2026-10-03T03:16:03.4108002Z at clean commit
  b5376c024872c042d3af07041a3a91221f29f50d and ended 04:05:44.2261584Z with
  native exit -532462766, 1,377 starts, 1,375 passes, one failure and no
  host.tests terminal. The failure was `m02.015.efi source closure and terminal
  ordering 25 explicit XML false defaults`. Remaining groups did not execute.
  Source stayed clean. The completed-gate checker rejected this result.
- Preserved stdout SHA256
  `2A6E2AA73559DF60E584593A7811DD05DA891540C062A8C3DADCA65EAC6E0A3E`;
  stderr SHA256 `532E9D3564EF55B6B012495A71196E3FB4D0EC139D2CA3B558738C56D7272FC5`.
- The #525 pre-fix focused run intentionally demonstrated two failed traversal
  assertions against the original script. It was not a complete successful
  two-module run; the later same-binary run above passed after the script fix.
- Earlier implementation and compiler-review failures remain in step952 and
  their original artifacts. No failed evidence was overwritten.

## Deferred work

The fresh clean committed full host gate and eight array-wave guest lanes remain
required before PR #524 and #77/#523/#525 closure. #56 also needs its separately
owned native panic integration, including a bounded transition-stack repair;
the current host-only layout has not been approved for native execution.

## Evidence boundary

This step records repository traversal and test-fixture work. It establishes no
new guest execution, general runtime/GC, ExitBootServices, hosted .NET/Kestrel,
cloud or production readiness. Gate status is unchanged. Historical step952 is
immutable; the failed suite and this corrective work are recorded separately.
