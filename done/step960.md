# Step 960 - Record the bounded M02 diagnostics technical exit

## What changed

Reconcile M02-EPIC's canonical status with the accepted child evidence. All
20 keyed children, prerequisite #425 and 27 related supplements are closed;
M02 milestone 4 currently contains 40 issues with only the epic #40 open.
The catalog now records closed/done intent and the bounded technical exit.
Live epic issue closure follows independent review, documentation merge,
evidence comment and readback. Milestone4 remains open until the separately
tracked synchronization repair #529 is reviewed and merged. This file does not predict a merge hash or
claim that those later tracker actions have already occurred.

The canonical diagnostics, boot-order, completion-gates and repository-audit
texts distinguish the opt-in local-QEMU M02 technical exit from general fault
handling and later runtime/firmware gates. There is no executable change.

## Why

The epic still said owned COM1, descriptor state and controlled live faults
were unproved and showed unchecked children. That historical text no longer
represented the reviewed first-party evidence accepted in step959. Closing a
tracking milestone must record the precise criterion it satisfies and keep
broader completion gates honest.

## Files changed

- `.gitea/backlog/clros-issues.json`: only M02-EPIC labels, desiredState,
  currentEvidence and evidenceBoundary. All 344 other rows, dependency edges,
  ownership and original acceptance criteria remain unchanged.
- `docs/diagnostics.md`, `docs/boot-order.md`, `docs/completion-gates.md`,
  `docs/repo-audit.md`: concise technical-exit and unchanged-gate boundaries.
- `done/step960.md`: this new reserved milestone. All older steps are immutable.

## Evidence and criterion audit

The independent GET-only audit is
`artifacts/orchestration-20261001/m02-epic-closure-eligibility-review-d4bd841/`.
Its report SHA256 is
`4C362D145F4905F7C79512119F31F50246E9FF5A9FE9BB3EB100057F34EC01BA`;
11-output manifest
`98EBFF73238CA931F5C0A322F74655BEAAFAC550A223C484E974D8E56AB44342`
and all 165 consumed input identities were read and rehashed by the parent.
The audit used Gitea 1.26.2, the literal installed REST issue helper, and 98
recorded issue/comment GETs returning 200. It made no tracker mutation.

Each keyed child is freshly closed/agent:done with exactly one correct
reviewed-evidence marker. Supporting historical correction comments remain
explicit:

| Key | Issue | Reviewed comment |
| --- | ---: | --- |
| M02-001 | 41 | 6578 |
| M02-002 | 42 | 6644 |
| M02-003 | 43 | 6882 |
| M02-004 | 44 | 7039, 7059 |
| M02-005 | 45 | 7225 |
| M02-006 | 46 | 7325, 7340, 7399 |
| M02-007 | 47 | 7564, 7460, 7603 |
| M02-008 | 48 | 9897 |
| M02-009 | 49 | 12996 |
| M02-010 | 50 | 13161 |
| M02-011 | 51 | 13210 |
| M02-012 | 52 | 13274 |
| M02-013 | 53 | 13369 |
| M02-014 | 54 | 13944 |
| M02-015 | 55 | 14121 |
| M02-016 | 56 | 14407 |
| M02-017 | 427 | 6742 |
| M02-018 | 429 | 6605 |
| M02-019 | 432 | 6664 |
| M02-020 | 436 | 6799, 6784, 6829 |

Dependency #425 is closed with reviewed comment 6496 and step891. The audit's
supplement-closure-matrix.json lists all 27 closed related supplements, including
the PF/DF supplements assigned outside M02. Host-only strategy and durability
children retain their narrower scope.

The actual pre-EBS owned-COM1/private-descriptor/controlled-fault exit is
supported by the independently accepted step959 joint record
`A512693F128EA8BF936DC76D34EC1C5393608F232CBDAB85F3C4656AD254B733`.
It combines the clean dee6df223b471a86f4bc5aea10c9747d3ac2ee29 complete host
suite (3636 starts/3637 passes/zero failures/native0) and nine fresh profiles,
with actual source/native/raw/lifecycle/disk review. The epic audit rehashed
all 13 direct joint-record inputs; it did not rerun or relabel the old evidence.

PR527 merged reviewed documentation head0906ccbef9c31e06f05d23a131c5d4ebd566114c
as d4bd8411b2bad1dbdcc4e7687c15d8675c2abfb7, the clean base for this transaction.
The parent independently rehashed all 507 noncatalog inputs from accepted source
inventory18959C0C92584A0A278AED7390F085EFD63BF17A734B757A19406716EE9D2300
before these documentation edits. Executable source remains identical.

## Tests run

The unchanged complete GiteaBacklogManifestTests ModuleInitializer was run
through the accepted focused executable:

```powershell
dotnet artifacts/orchestration-20261001/issue523-manifest-focused/bin/Release/net10.0/ManifestFocused.dll
git diff --check
```

Actual run: 2026-10-03T16:18:41.064692+00:00 through 2026-10-03T16:19:05.682019+00:00.
It returned native exit 0 with 17 starts/18 passes/zero failures, exact original
marker names, one final `TEST_PASS manifest.focused` and empty stderr. All
17 captured inputs were unchanged during the run; only step960 and
docs/repo-audit.md were updated afterward for results, readability and the
discovered milestone-container
synchronization limitation. Result SHA256
`3C487175B53E4157202BFCD38CAF54EA118B0FE8076EEACBDACEE47EB711CD83`;
stdout `A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9`;
input manifest `50DCBFC04AD26A0E17D5C91E237849485E12AB3199D44251C68CA4DA90006BD4`.
The wrapper, exact commands, raw logs and snapshots are under
`artifacts/orchestration-20261001/issue40-closure/checks/attempt1/`.

Diff/whitespace checks passed. Only documentation/catalog/Markdown paths are
changed; no forbidden first-party source or generated artifact was added.
No new runtime/full-suite, NativeAOT publication or QEMU execution is claimed
by this administrative audit. Those accepted checks remain in step959.

## Failures

Historical #44 comment 7039 contains interpolation damage; later comment 7059
supplies exact independently approved/merged identities and evidence. The
#46 merge identities are taken from 7340 rather than malformed 7399. The audit
preserves those comments and does not mistake their text defects for new proof.
All earlier test/guest failures recorded by step959 remain preserved.
A parent read guessed the v5 inventory directory before discovering the actual
v5-final-pins path; no source/evidence was modified by that read failure.

## Deferred work and evidence boundary

The remaining live transaction is scoped: reviewed docs/catalog merge,
`<!-- clros-evidence-reviewed:M02-EPIC -->` on #40, preserved human text and
checked dependency/child list, done label/closed issue and independent GET
readbacks. Milestone4 remains open: Ensure-Milestones currently forces state
open, and Verify-RemoteState requires open. Closing the live container now
would be undone by a later sync. Separately tracked #529 owns the bounded
script/test repair and subsequent evidence-qualified milestone closure.
This step records the evidence that permits epic closure, not future tracker
outcomes or completion of that synchronization repair.

M02 is the bounded, default-off, pre-ExitBootServices local-QEMU technical exit.
Firmware/NativeAOT SSE entry is a prerequisite. UD2 uses the finite compiled
state verifier, not an exposed numeric register/C/D dump; DF saved fields
remain diagnostic-only. No ordinary persistent fault framework, asynchronous
IRQ/NMI service, nested recovery, post-report sentinel observation, general
mapping/lifetime guarantee, physical hardware/provider behavior, real EBS,
post-firmware diagnostics, hosted .NET/Kestrel, cloud or production readiness
is established. Gate 2 keeps its firmware-visible smoke boundary; Gates 3/4
remain Fail. #58 memory-map and #78/#528 native String work continue separately.
