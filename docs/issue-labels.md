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

M01-036 subsequently completed the final M01 host-only repository-state
repair. Live issue `#425` and M01-EPIC `#24` are closed with `agent:done`, the
hidden key was adopted, and M02-017 was created as live issue `#427`. Its
16-epic, 326-child, 342-keyed-issue, 426-managed-object inventory is now a
historical applied snapshot. Implementation commit
`f14a26f9267fc3e1cc32fcd5e847818d7dbe252c` passed the complete host-only gate
with 3,329 starts, 3,330 passes, zero failures, and exactly one M01-036
start/ready/pass envelope; `done/step891.md` preserves the exact logs, hashes,
wrapper-timeout recovery, and evidence boundary.

M02-001 is now actual reviewed production C#, not planned work. Implementation
commit `acec50b47056e7c538ca5225d32898ff2c0bc93f` and evidence head
`5a4234a5b6e0fadcd1a4db9a01374340723ba4e8` merged through PR #428 as
`85528a5513f1cf24396ff29acc6787bc015ec9c5`. Issue #41 evidence comment #6578,
merge-proof comment #6584, and `done/step892.md` preserve native HostTests exit
0, 3,336 starts, 3,337 passes, zero failures, the exact one-family
ExistingAuditedBytePlans decision, and the host-only evidence boundary. At the
M02-018 evidence-source checkpoint, issue #41 remained open in `agent:review`
pending the post-merge manifest-owned closure transaction.

M02-018 live issue `#429` is that serialized host-only durability transaction.
Implementation commit `8c1f0d8d2b6dfd06433840f973de6aa7aa08e532`,
tree `4e1dfbe4bad969af26e534a0cf12b10c61401d7f`, and evidence-source
commit `79f318f25389cee411e65bc745f2cb052be0058d`, tree
`13e86bf0164bb0e52b3f4e2c2d53f341f351e3f4`, are carried by draft PR #430
and `done/step893.md`. The complete r4 gate under
`artifacts/logs/m02-018-final/79f318f2-20260730T201222Z-r4` passed all ten
commands. Summary SHA-256
`99A39D223A9D208B0E985E6A50CA1E617F4A52F33B2954B0294595325B676D7F`
records native HostTests exit 0, 3,337 starts, 3,338 passes, zero failures, one
exact M02-018 start/ready/pass envelope, one terminal host pass, and empty
stderr. Offline validation reported 427 skipped managed objects, managed-body
and state-policy self-tests passed, and no real QEMU ran.

The canonical target adds the eighteenth M02 child for 16 epics, 327 children,
343 keyed issues, and 427 managed objects; records M02-001 as
`desiredState=closed`/`agent:done`; and rewires M02-002 to depend on M02-018 so
materialization cannot become ready after a partial closure. M02-002 still owns
the fixed 4 KiB page materializer/readback production C#, M04-001 owns the
`OS.Std.*` runtime-helper classification production C#, and M02-017 remains the
sole serialized EFI/QEMU integration leaf before M02-003. M02-017 may execute
only the safe offset-zero return-cookie entry, never the In8 or Out8 entries.
None of this repository-state or M02-001 host evidence advances a completion
gate or proves real `ExitBootServices`, live port I/O, provider behavior,
CloudReady status, hosted .NET, Kestrel, or production readiness. Live closure
and 427-object convergence remain pending until the post-merge synchronizer
sequence passes.

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
  reviewed production C# merged through PR #428; M02-018 owns its durable
  catalog/tracker closure and prevents M02-002 readiness until the complete
  427-object transaction converges. M02-017 remains the sole safe live
  integration boundary for the fixed M02 page.
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

1. Independently review the source-bound M02-018 r4 evidence and final
   evidence-only delta, post exactly one reviewed marker to issue `#429` while
   it is open, and merge PR #430 only at the reviewed head.
2. From clean canonical master, run guarded preview/apply/repeated-apply/final
   preview; require issues `#41` and `#429` closed with `agent:done`, no
   duplicate hidden keys, M02-002 ready, and all 427 managed objects
   unchanged/skipped with zero proposed writes.
3. Claim and implement M02-002 as the smallest collision-safe production-C#
   leaf, then run the complete host evidence gate.
4. Continue with the serialized M02-017 safe-QEMU integration boundary before
   M02-003; do not treat its safe offset-zero cookie as live In8/Out8 evidence.
