# Issue Labels

## Current Status

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
421-managed-object inventory remains the last applied historical snapshot.

The current durable manifest adds M01-033 through M01-035 after M01-025 and
before M01-026. These slices respectively own deterministic C# AMD64 COFF
serializer generation, canonical EFI link/final-artifact verification, and
four-epoch EfiEntry publication ordering. The manifest therefore contains 324
children, 340 keyed issues, and 424 managed objects. All three new issues begin
`agent:blocked`. Once M01-025 closes with reviewed evidence, readiness
recomputation makes only M01-033 `agent:ready`. M01-033 owns only new generator
files, its focused host test, and `docs/security.md`, so it has no central
collision lane and may run alongside genuinely disjoint work. M01-034,
M01-035, and M01-026 remain blocked in order. This administrative branch does
not synchronize the tracker.

The guarded earlier M00-023 apply recognized existing M00-023 as issue #321,
created only M01-016 through M01-026 as issues #323 through #333, updated
exactly 24 existing managed issues, and emitted
`SYNC_VERIFY_PASS milestones=16 labels=68 issues=331`. The 24 updates included
the substantive M03-007 post-EBS permanent-CR3 handoff and its generated
M03-EPIC child-checklist update. Its 315-child/331-keyed/415-managed-object
convergence is preserved historical evidence, as are the later
316-child/332-keyed/416-managed-object and
321-child/337-keyed/421-managed-object snapshots. After a future guarded apply,
a converged preview for the current manifest must report all 424 managed
objects unchanged. None of these administrative inventory changes advances a
capability or completion gate.

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
  M01-033 generator, M01-034 canonical link/artifact-verifier, and M01-035
  four-publication-epoch EfiEntry slices then serialize the instruction
  publication boundary before M01-026 retains the later real-QEMU evidence
  boundary.
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

Reference labels describe the specific influence named in an issue, not an
entire milestone. `reference:license-blocked` is used only when direct use of
that named material still lacks permission or has an unresolved incompatible
boundary. It is not applied to unrelated work merely because the same milestone
contains an optional licensed fixture. SharpOS-authored source covered by the
recorded owner grant and upstream CC0-1.0 license uses `reference:adapt`;
SharpOS screenshots/done records used only as historical or behavioral context
use `reference:test-only`. The separately GPL ManagedDoom fixture and WAD/media
rights remain `reference:license-blocked`.

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
dependency/evidence blocking, and child-before-epic closure order.

## Known Limitations

Gitea remains mutable external state, so user-created labels and comments can
diverge from the manifest-controlled region. Agents must read the issue body,
dependencies, current labels, and evidence comments before claiming work. The
manifest is recoverable intent, not proof that any implementation gate passed.
The 16 milestone objects intentionally remain open and have no fabricated due
dates; the keyed epic and child issue states carry the durable evidence-review
closure contract.

## Next Steps

1. Claim one smallest `agent:ready` issue and change it to `agent:claimed`.
2. Use the issue key in the branch name and evidence comments.
3. Move completed work to `agent:review`; close it only after evidence review.
4. Change `desiredState` in the manifest when an evidence-reviewed closure must
   be durable, then apply and verify convergence.
