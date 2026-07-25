# Issue Labels

## Current Status

The canonical backlog taxonomy and desired issue state are version-controlled in
`.gitea/backlog/clros-issues.json`. The local Gitea repository at
`http://localhost:3000/admin/CLROS` is synchronized additively by
`scripts/sync-gitea-backlog.ps1`.

The 2026-07-25 reconstruction created 68 managed labels, 16 milestones, 16
epics, and 303 child issues. The synchronizer verified all 319 hidden issue keys,
milestone assignments, managed labels, parent links, and dependency links.

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
- One `phase:mxx-*` label identifies each milestone from M00 through M15.

Only issues whose complete transitive dependency tree is manifest-closed,
tracker-closed, and backed by exact evidence-review comments—and which have no
claimed collision-scope peer—are `agent:ready`. Epics are never implementation
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
