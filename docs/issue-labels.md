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

M01-036 is the final host-only repository-state repair leaf. Live issue `#425`
was bootstrapped and claimed without applying the stale synchronizer. Its
repaired canonical manifest contains 16 epics, 326 children, 342 keyed issues,
and 426 managed objects: M01 gains M01-036, and M02 gains M02-017. These are
canonical target totals, not a claim that the live tracker has converged; live
adoption of `#425`, creation of only M02-017, M01 epic closure, and a final
426-object unchanged preview remain post-merge acceptance steps. Implementation
commit `f14a26f9267fc3e1cc32fcd5e847818d7dbe252c` passed the complete host-only
gate with 3,329 starts, 3,330 passes, zero failures, and exactly one M01-036
start/ready/pass envelope; `done/step891.md` preserves the exact logs, hashes,
wrapper-timeout recovery, and evidence boundary.

The repaired dependency graph exposes actual production-C# work without
confusing host evidence with guest execution. M02-001 owns an allocation-free
managed x64-stub strategy contract, M02-002 owns the fixed 4 KiB page
materializer/readback contract, and M04-001 owns the `OS.Std.*` runtime-helper
classification ledger; all three are host-only implementation leaves. M02-017
is the sole serialized EFI/QEMU integration leaf before M02-003 and may execute
only the safe offset-zero return-cookie entry, never the In8 or Out8 entries.
None of this administrative or planned work advances a completion gate or
proves real `ExitBootServices`, provider behavior, CloudReady status, hosted
.NET, Kestrel, or production readiness.

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
  M01-036 is the final host-only durability repair. It releases M02-001 and
  M04-001 only after repository/tracker convergence; M02-017 remains the sole
  safe live integration boundary for the fixed M02 page.
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

1. Review and merge M01-036, post its exact host evidence to issue `#425`, then
   run guarded preview/apply/preview synchronization from merged canonical
   source.
2. Verify hidden-key adoption of `#425`, creation of only M02-017, all 426
   managed objects unchanged on the final preview, and honest M01 epic closure.
3. Claim M02-001 as the smallest collision-safe `agent:ready` production-C#
   leaf and implement its exact host-only contract.
4. Continue M02-002 and then the serialized M02-017 safe-QEMU integration
   boundary before M02-003.
