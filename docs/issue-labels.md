# Issue Labels

## Current Status

The October 8 M03 reconciliation in [step1095](../done/step1095.md) aligns
epic #57 and milestone 5 with the accepted #68/#594 closure. The canonical
manifest records M03 closed, M03-EPIC closed/agent:done, and M05-001 (#94)
open/agent:ready. M05-EPIC (#93) remains open/agent:blocked by its incomplete
children, with its #68 prerequisite checked. Use the verified source Gitea at
`192.168.4.35:3000` from this destination host as documented in
[Windows handoff](windows-handoff.md); loopback-only tools target another server.

The canonical backlog taxonomy and desired issue state are version-controlled in
`.gitea/backlog/clros-issues.json`. The local Gitea repository at
`http://localhost:3000/admin/CLROS` is synchronized additively by
`scripts/sync-gitea-backlog.ps1`.

The 2026-07-25 reconstruction initially created 68 managed labels, 16
milestones, 16 epics, and 303 child issues. The additive M00-023 evidence-model
repair keeps those labels and milestones, adds M00-023, and replaces the
impossible per-leaf QEMU closure model with the serialized M01-016 through
M01-026 one-live-boundary-at-a-time chain. M01-019 review subsequently exposed
the need for M01-027, a host-audited single composite owned-CR3 transition leaf
that closed before M01-020 linked the reviewed source. The resulting 316
children, 332 keyed issues, and 416 managed objects are preserved as a
historical inventory snapshot; M01-027 and M01-020 are now evidence-reviewed
and closed, not the next or blocked leaves.

The M01-028 recovery added itself and inserted M01-029 through M01-032 as four
serialized prerequisites before M01-025. Its 321-child, 337-keyed-issue,
421-managed-object inventory remains a historical applied snapshot.

The 324-child, 340-keyed-issue, 424-managed-object manifest and tracker state
that added M01-033 through M01-035 after M01-025 is now a historical snapshot.
Those slices respectively owned the deterministic C# AMD64 COFF serializer and
encoded unwind intent, canonical EFI link/authoritative-exception-directory
verification, and four-epoch EfiEntry checked-cookie publication ordering.
Their reviewed evidence remains in `done/step884.md`, `done/step886.md`, and
`done/step887.md`.

M01-026 issue `#333` subsequently passed the complete host gate and one fresh
all-switch local QEMU/OVMF r16 run from implementation commit
`8f1edea17f2de425520e239af58d37aa3b182146`. The reviewed documentation/evidence
head `26219cc5327e9b32935f4e5b06fe5eb014cca93c` merged through PR #422 as
`6aaf046362d0a3187f551ee06f1456d05ce5021f`; issue comments #6472 and #6477 and
`done/step890.md` preserve the reviewed and merge evidence. The manifest SHA-256
is `94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188`, and
the accepted serial SHA-256 is
`CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`.
M01-026 is reviewed, merged, live-closed, and labeled `agent:done`.

M01-036 subsequently completed the final M01 host-only repository-state
repair. Live issue `#425` and M01-EPIC `#24` are closed with `agent:done`, the
hidden key was adopted, and M02-017 was created as live issue `#427`. Its
16-epic, 326-child, 342-keyed-issue, 426-managed-object inventory is now a
historical applied snapshot. Implementation commit
`f14a26f9267fc3e1cc32fcd5e847818d7dbe252c` passed the complete host-only gate
with 3,329 starts, 3,330 passes, zero failures, and exactly one M01-036
start/ready/pass envelope; `done/step891.md` preserves the exact logs, hashes,
wrapper-timeout recovery, and evidence boundary.

M02-001 is actual reviewed production C#, not planned work. Implementation
commit `acec50b47056e7c538ca5225d32898ff2c0bc93f` and evidence head
`5a4234a5b6e0fadcd1a4db9a01374340723ba4e8` merged through PR #428 as
`85528a5513f1cf24396ff29acc6787bc015ec9c5`. Issue #41 evidence comment #6578,
merge-proof comment #6584, and `done/step892.md` preserve native HostTests exit
0, 3,336 starts, 3,337 passes, zero failures, the exact one-family
ExistingAuditedBytePlans decision, and the host-only evidence boundary.

M02-018 issue `#429` completed M02-001's serialized host-only durability
transaction. Its r4 source-bound evidence at
`artifacts/logs/m02-018-final/79f318f2-20260730T201222Z-r4` passed all ten
commands; summary SHA-256
`99A39D223A9D208B0E985E6A50CA1E617F4A52F33B2954B0294595325B676D7F`
records native HostTests exit 0, 3,337 starts, 3,338 passes, zero failures, one
exact M02-018 envelope, one terminal host pass, and empty stderr. Reviewed PR
#430 merged as `8566f44cf493d7138d500b0cdad17b75d34b3405`; guarded synchronization
closed issues #41 and #429 with `agent:done` and converged the historical
16-epic, 327-child, 343-keyed-issue, 427-managed-object snapshot. Comments
#6605, #6615, and #6630 preserve review, merge, and convergence proof.

M02-002 is now the second actual reviewed M02 production-C# leaf.
Implementation/evidence-source commit
`06a38803b8bf4d5bc35e29948e626dbd8fb164bd`, tree
`09bc5e263a3ab624ea3232655957d9ae79b2d042`, and reviewed evidence head
`88df315bcca0bf3302d25ca88451b1d51f1ce31b`, tree
`c272b6991a68309a7e348bd20d85819977b7a264`, merged through PR #431 as
`da856db657575d8f8d67713c7afe96e113854e54`. Issue #42 comments #6644 and
#6651 and `done/step894.md` preserve the exact fixed 4 KiB page, FNV/SHA
identities, no-stdlib pointer boundary, 3,338 starts, 3,339 passes, zero
failures, and host-only evidence. QEMU was dry-run only, and the EFI build was
regression/preflight only; no M02 guest execution occurred.

M02-019 issue `#432` completed M02-002's durability transaction through
reviewed PR #433 and guarded convergence. Issues #42 and #432 are closed with
`agent:done`; the resulting 16-epic, 328-child, 344-keyed-issue,
428-managed-object inventory with 19 M02 children is now a historical applied
snapshot.

M02-017 is now reviewed and merged production C#/EFI integration rather than
planned work. Accepted source commit
`cf8ce63c37a9c62a0f8f736adda14b6bd3e19a9a`, tree
`d18f520cee19aa0c89dfb41f326c92afd0284c02`, and reviewed head
`abb82e4cd5ccb8751fabbf0d3095796f626bb6e7` merged through PR #435 as
`d2872746059a7200431b6d3bc89c6e02cb42fa8b`. Issue #427 comment #6742 and
`done/step896.md`/`done/step897.md` preserve complete HostTests with 3,346
starts, 3,347 passes, zero failures, and fresh local QEMU/OVMF evidence with
all 48 required markers once/in order and all 34 forbidden markers absent.
That tuple proves only the offset-zero return-cookie entry and records
`portIoExecuted=false`; offsets 16 and 32 were not targeted.

M02-020 issue `#436` is the claimed durability transaction for M02-017. Its
canonical target is 16 epics, 329 children, 345 keyed issues, and 429 managed
objects with 20 M02 children. It records M02-017 as
`desiredState=closed`/`agent:done`, inserts M02-020 after M02-017, and rewires
M02-003 to depend on M02-020 so privileged I/O cannot become ready after a
partial closure. PR #435 auto-closed issue #427, but #427 still carries
`agent:review`; issue #436 remains open with `agent:claimed`. Until M02-020 is
independently reviewed, merged, and converged, neither issue has reached the
manifest's exact `agent:done` target and the 429-object target is not
live-convergence evidence. This repository-state transaction proves no new
guest behavior, In8/Out8, COM1, descriptors/faults, real
`ExitBootServices`, hardware/provider behavior, CloudReady status, hosted
.NET, Kestrel, or production readiness.

The reviewed dependency chain has subsequently advanced through M02-003,
M02-004, and M02-005. Live issues `#43`, `#44`, and `#45` are closed with
`agent:done`. Their implementation PRs `#439`, `#444`, and `#452` merged as
`126bf112e2f645555329acb64241d596d30bc5fa`,
`c002d01dab1392c0768559e7a8eef84b384627f1`, and
`399aaa123dcac75bdd20780dc91d91cd87586552`; the separately reviewed M02-004
and M02-005 closure PRs `#445` and `#454` then merged as
`de160ad749533e40c20ccb7a028ae2c61614d76c` and
`b78b23e7a561ecf111c6e77533102a5f2c247652`. M02-006 implementation and EFI
evidence head `f0c07821f8dc9ed65ff6d99311c189fdd9c267da`, tree
`485f1e3398966c49c8c1b810f36e30dfd54f8094`, received undismissed review
`#11` from independent `admin` at that exact head and merged through PR `#456`
as `d0376d3fe061b9e63a14b083561897a624a25bc5`. Managed issue `#46` remains
open with `agent:review` while supplemental issue `#457` owns this exact
manifest/documentation/evidence-ledger transaction. This source records only
the reviewed intent to close M02-006; it is not live convergence evidence until
the independently reviewed closure PR merges and guarded apply, repeated
apply, and final preview prove #46 closed with `agent:done` and recompute
M02-007 readiness. These administrative transitions add no live panic or fault
route, descriptor-table installation, post-firmware serial ownership, real
`ExitBootServices`, hosted .NET, Kestrel, provider, CloudReady, production, or
completion-gate evidence.

The guarded earlier M00-023 apply recognized existing M00-023 as issue #321,
created only M01-016 through M01-026 as issues #323 through #333, updated
exactly 24 existing managed issues, and emitted
`SYNC_VERIFY_PASS milestones=16 labels=68 issues=331`. The 24 updates included
the substantive M03-007 post-EBS permanent-CR3 handoff and its generated
M03-EPIC child-checklist update. Its 315-child/331-keyed/415-managed-object
convergence is preserved historical evidence, as are the later
316-child/332-keyed/416-managed-object and
321-child/337-keyed/421-managed-object snapshots. The 2026-07-28 guarded apply
created three issues, recorded four update events including the newly created
M01-033 readiness recomputation, preserved 420 unchanged events, and emitted
`SYNC_VERIFY_PASS milestones=16 labels=68 issues=340`. Its following no-write
preview reported all 424 managed objects unchanged with zero create, update,
skip, or failure action. None of these administrative inventory changes
advances a capability or completion gate.

## Implemented Behavior

- Agent state: `agent:ready`, `agent:claimed`, `agent:blocked`,
  `agent:review`, and `agent:done`.
- Priority: `priority:p0` through `priority:p3`.
- Work type: `type:epic`, `type:implementation`, `type:test`,
  `type:tooling`, `type:docs`, and `type:research`.
- Evidence: `evidence:host`, `evidence:qemu`, `evidence:libvirt`,
  `evidence:aws`, and `evidence:azure`.
- Reference use: `reference:adapt`, `reference:test-only`, and
  `reference:license-blocked`.
- Components: repository, references, boot, memory, diagnostics, std, runtime,
  PAL, platform, PCI, virtio, storage, network, app model, images, QEMU,
  libvirt, CI, security, AWS, and Azure.
- Collision lanes serialize the shared solution baseline, EFI entry,
  freestanding project, boot sequence, probes, central docs, common virtio, and
  PAL ABI integration files.
- M01-001 through M01-015 are host-only leaf contracts. Their
  `CLROS_M01_NNN_READY` markers are host-harness markers, not guest evidence.
  M01-016 through M01-027 serialize probe policy, three BootSequence
  coordinator stages, the reviewed composite transition leaf, one reviewed
  OS-project/source-link integration, and alternating
  EfiEntry/fresh-`-BuildEfi` QEMU boundaries so each issue owns exactly one
  collision-lane surface. M01-027 and M01-020 are closed. M01-028 inserts the
  serialized M01-029 preparation, M01-030 identity-binding, M01-031 physical
  table-materialization, and M01-032 call-gate leaves before M01-025. The new
  M01-033 generator/unwind, M01-034 canonical link/artifact/unwind-verifier, and
  M01-035 four-publication-epoch checked-cookie EfiEntry slices then serialize
  the instruction-publication boundary before M01-026 retains the later
  runtime-observable real-QEMU evidence boundary. M01-033 proves object bytes
  and relocation intent only; M01-034 proves the linked PE exception
  directory statically; M01-035 proves host-tested exact state transitions
  across the one-way owned-stack switch without guest execution. M01-026 is
  now reviewed, merged, and closed from the bounded r16 local-QEMU evidence.
  M01-036 is the completed final M01 host-only durability repair. M02-001 is
  reviewed production C# merged through PR #428, and M02-018 completed its
  durable catalog/tracker closure. M02-002 is reviewed production C# merged
  through PR #431, and M02-019 completed its durable closure. M02-017 is
  reviewed safe-cookie production C#/EFI integration merged through PR #435;
  M02-020 owns its durable closure and prevents M02-003 readiness until the
  complete 429-object transaction converges.
- One `phase:mxx-*` label identifies each milestone from M00 through M15.

Only issues whose complete transitive dependency tree is manifest-closed,
tracker-closed, and backed by exact evidence-review comments, and which have no
claimed collision-scope peer, are `agent:ready`. Epics are never implementation
assignments. Closed manifest-controlled issues carry `agent:done`; an open
issue is not considered complete merely because somebody applied that label
manually.

Desired closures are applied in dependency order, with child issues before
their epic. A dependency or child that remains open after a partial API failure
blocks every later dependent and blocks epic closure. Preview may model
evidence-reviewed pending closures, but apply rechecks the live tracker before
each state transition; rerunning converges without promoting work past the
failed boundary.

Keep references to unfinished work neutral (`Refs #<number>` or
`Issue #<number> remains open`). Gitea can interpret a closing keyword before
an issue reference even in negated prose. Inspect the final PR title/body and
commit messages before merging, then read back referenced issue states.
Automatic closure is a tracker event; it does not supply the required evidence
review or dependency proof. Step938 records an observed unintended closure
and its evidence-based correction.

Reference labels describe the specific influence named in an issue, not an
entire milestone. `reference:license-blocked` is used only when direct use of
that named material still lacks permission or has an unresolved incompatible
boundary. It is not applied to unrelated work merely because the same milestone
contains an optional licensed fixture. SharpOS-authored source covered by the
recorded owner grant and upstream CC0-1.0 license uses `reference:adapt`;
SharpOS screenshots/done records used only as historical or behavioral context
use `reference:test-only`. The separately GPL ManagedDoom fixture and WAD/media
rights remain `reference:license-blocked`.

## Stale blocker and progress audit

[Step969](../done/step969.md) records the 2026-10-04 audit of all 255 live
blocked issues: 253 had open declared prerequisites; the other two were the
unclaimed M03/M04 tracking epics. The current resolver deliberately keeps
those epics blocked. No implementation leaf became ready from this audit.

The audit found one catalog state mismatch: M04-009/#78 was already reviewed,
merged through PR531 and closed with `agent:done`, while the catalog still
said open/claimed. Its desired state and agent label now match that accepted
closure; live #78 is unchanged. M04-EPIC's current evidence now distinguishes
the accepted bounded allocation milestones from unfinished runtime work.

The current renderer emits unchecked dependency and child boxes independently
of actual issue state. Read live state, dependencies and reviewed evidence
instead of treating those boxes as blockers. Durable clarification comments
on #57, #69 and #64 record their current progress and remaining prerequisites;
no label, dependency or ownership change was made. A body-only checkbox edit
would be overwritten by the current renderer. This audit does not change that
renderer or apply a broad tracker synchronization.

## Constructor dependency repair

The October 6 review identified a dependency cycle that would arise if full
M04-015 constructor failure handling simply gained M04-021 unwinding as a
prerequisite. M04-021 already followed M04-015 through dispatch, casts,
delegates, thread-static metadata and unwind metadata.

M04-024 now owns the real constructor execution prerequisite: discovery,
bounded original-module authority, once semantics and same-thread reentry.
The order is M04-014 -> M04-024 -> M04-016 through M04-021 -> full M04-015.
M04-023 also waits for M04-015, preserving the complete helper audit. The
M04 epic now tracks 24 children. Full M04-015 retains actual exception caching
and repeated-access rethrow; terminal failure is not substituted for completion.
Generated repository/network acceptance text in M04-015/016/021 is replaced by
the corresponding runtime requirements. This is a dependency/acceptance repair,
not constructor implementation or a completion-gate change.

## Milestone state policy

[Step962](../done/step962.md) adds explicit milestone intent to the existing
synchronizer. Values must be exact strings, compared ordinally:

- `open` creates a missing container open and preserves an existing closed
  container during ordinary metadata maintenance.
- `closed` requests closure after current issue membership and evidence checks.
- `reopen` explicitly requests an open container.

Preview, apply and convergence use the same decision. A missing container is
always created open; closed intent is deferred until membership can be checked.
The milestone pass still runs before issue updates, so a planned issue closure
later in the same run cannot satisfy the current milestone check.

Any resolved closed container requires the expected canonical epic with exact
`type:epic`, the existing recursive epic/child/dependency evidence and actual
closed states, and each canonical member's presence in that actual milestone.
Every actual non-PR milestone member, including unkeyed supplements, must also
be closed. The epic's reviewed evidence attests to the supplemental review;
historical supplements do not have one uniform machine-readable evidence
marker. An inconsistent closed container reports a blocker rather than being
silently reopened. The existing generic issue closure policy is unchanged.

The M02 catalog records closed intent after PR530's reviewed epic closure.
Milestone4 remains open until this repair's reviewed merge and a narrow live
update/readback; the intent alone is not live closure or a capability claim.
No due date is manufactured and no broad live apply is needed for this repair.

## Unsupported Behavior

The synchronizer does not assign agents, create branches, review evidence, or
decide that an implementation is complete. It never deletes issues, milestones,
labels, comments, or user-authored text outside its managed body region. It
never closes or reopens an issue unless the manifest explicitly changes
`desiredState`.

## Tests

Preview and validation:

```powershell
.\scripts\sync-gitea-backlog.ps1 -Offline
.\scripts\sync-gitea-backlog.ps1 -SelfTest
.\scripts\sync-gitea-backlog.ps1
```

Apply only after reviewing the preview:

```powershell
.\scripts\sync-gitea-backlog.ps1 -Apply
.\scripts\sync-gitea-backlog.ps1
```

The final command must converge with no `wouldCreate`, `wouldUpdate`, or failed
keys. Host tests cover UTF-8-without-BOM parsing, manifest shape, offline
no-write behavior, managed-body preservation, canonical-target refusal,
duplicate keys, missing fields, unknown dependencies, transitive
dependency/evidence blocking, and child-before-epic closure order. Milestone
controls also cover 28 scenarios in both preview/apply modes, exact API
payloads and convergence, 17 invalid and three valid intent values, current
membership/evidence blockers, and explicit reopening.

## Known Limitations

Gitea remains mutable external state, so user-created labels and comments can
diverge from the manifest-controlled region. Agents must read the issue body,
dependencies, current labels, and evidence comments before claiming work. The
manifest is recoverable intent, not proof that any implementation gate passed.
Milestone state follows the explicit policy above and carries no fabricated
due dates. Keyed epic and child reviewed evidence remains the closure authority;
container state alone never proves a technical milestone.

## Required Post-M02-017 Sequence

The accepted M02-017 merge requires this ordered durability sequence. Later
tracker comments and synchronizer logs prove which steps completed.

1. Implement and independently review M02-020 from the exact six owned
   catalog/synchronizer/test/document paths and one newly reserved done step.
2. Post exactly one `<!-- clros-evidence-reviewed:M02-020 -->` marker to issue
   `#436` while it remains open, then merge only the reviewed source head.
3. From clean canonical master, verify the unique M02-017 and M02-020 hidden
   keys and both reviewed markers, then run guarded
   preview/apply/repeated-apply/final-preview.
4. Require issues `#427` and `#436` closed with exactly `agent:done`, no
   duplicate hidden keys, M02-003 as the smallest collision-safe ready issue,
   and all 429 managed objects unchanged/skipped with zero proposed writes.
5. Claim M02-003 only after that convergence; its privileged In8/Out8 evidence
   must remain distinct from M02-017's safe offset-zero cookie tuple.
