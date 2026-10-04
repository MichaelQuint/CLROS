# Step 969 - Repair verifier fixture and reconcile stale backlog status

## What changed and why

The legacy `EfiArtifactVerificationScriptTests` synthetic post-restore record
omitted `EnableM03MemoryMapCaptureProbe=false`. The verifier correctly requires
all 93 evaluated properties; the fixture supplied 92, causing later serializer
tests to fail before reaching their intended checks. Add that one false entry
without changing the verifier or any of the original 20 test registrations.

The requested blocked-issue audit found no newly ready implementation leaf.
Of 255 blocked issues, 253 had open declared prerequisites; #57 and #69 are
unclaimed tracking epics deliberately kept blocked by the existing resolver.
Clarification comments explain their progress and #64's remaining dependency
on #63. The renderer's unchecked boxes do not independently establish state.

The audit did find stale catalog intent for M04-009/#78: its reviewed PR531
closure was already live, but the catalog still said open/claimed. Change only
its desired state and agent label, and refresh M04-EPIC's current evidence for
the accepted bounded allocation milestones. All other fields and 343 other
rows are preserved. No live issue labels, dependencies or ownership changed.

## Files changed

- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`: missing false
  property in the shared synthetic metadata builder.
- `.gitea/backlog/clros-issues.json`: three fields across two of 345 rows.
- `docs/efi-build.md` and `docs/completion-gates.md`: distinguish the accepted
  saved-v8 static inspection from the failed common run and pending guests.
- `docs/issue-labels.md`: audit findings and current checkbox/epic policy.
- `done/step969.md`: this milestone, reserved with the actual helper at
  02:55:44.161215-02:55:44.468059 UTC on 2026-10-04, native exit 0.

## Tests run

The complete original verifier initializer passed on the one-entry repair:
20 START, 21 PASS, zero FAIL, one terminal `host.tests`, native exit 0 and empty
stderr. Its isolated build passed with zero warnings/errors. Actual run:
02:56:21.461161-03:20:15.380321 UTC on 2026-10-04; input and launch-binary
identities were unchanged. This includes all formerly failing groups and the
original package/source authority, external evaluation and bounded parsing
scenarios. No registration or assertion was removed.

Catalog offline validation and policy self-tests completed with native exit 0
and empty stderr. The latter made zero API calls. A read-only preview completed
03:02:49.996334-03:06:29.802163 UTC, native exit 0: 91 closure-evidence records
verified, 416 unchanged objects, 13 proposed updates, no creates or failures.
No Apply was run. These checks leave the catalog bytes unchanged.
`scripts/check-naming.ps1` passed both naming and source-firewall groups with
native exit 0 and empty stderr; `git diff --check` passed.

## Logs and evidence

Evidence lives under ignored `artifacts/orchestration-20261001/`:

- `issue532-verifier-property-diagnosis/`: retained failing child inputs and
  exact 93-required/92-actual comparison; only the new M03 property was absent.
- `issue532-common-gate/run-1c37aa1/`: original command, raw streams, process,
  source/binary identities and failed terminal result.
- `issue532-property-inventory-independent-review/`: independent confirmation
  of the one-line repair and unchanged shared downstream consumer.
- Author worktree `artifacts/issue532/verifier-property-repair/attempt1/`:
  complete focused commands/results and raw logs. The 3,488-byte run stdout
  SHA-256 is
  `B34177032532CCE6A33D623B49C064B9070F6DB7A8920D7FDAA692247DD08AF1`.
  Parent read the full stream and imported the exact reviewed fixture bytes,
  SHA-256 `FD3701A42B1D34AE0E3FF6904DEFCBA25C023C1A116917DDBDE77C137DA8AB03`.
  The final report is
  `8D4C626A1016B6DBFB3C92EB59A651AE50D7CE9D2E6997E56BE3C9EED65527A4`;
  parent rehashed its four package and 184 retained evidence records.
- `stale-blocker-audit-1c37/` and `stale-blocker-comments/`: full live audit,
  proposed reconciliation and exact comment readbacks. Successful comments are
  #57/14641, #69/14645 and #64/14648; only comment count/update time changed.
- `issue532-repair-root-integration/`: original catalog and exact three-field
  delta; final catalog SHA-256
  `1353132536B21DA10AF8C3D3B29BA81A71B48F74439298116CE8709865917908`.
- `issue532-catalog-doc-independent-review/`: no findings in the four-file
  catalog/docs snapshot; reversing the three catalog fields reproduces the
  original bytes exactly. Its 11 outputs and 68 inputs were rehashed by parent.
- `issue532-common-failure-progress/`: comment14652 on #532 corrects the
  earlier in-progress report, records the failed run and pending repair gates,
  and leaves issue state/content version unchanged (open/claimed/version0).

Saved-v8 native inspection was independently accepted before this repair:
callback ABI, stack alignment, Capture/CopyPlan and complete copy/guard/tail
readback. Those original image identities remain tied to their own source
version. No image was executed by that inspection or this fixture repair.

## Failures

The first common run at `1c37aa18ebc317152b2743b98ca6b80235967a14`
ran 02:12:15.598692-02:51:34.510889 UTC, native exit 1. After preserving
completed failing child artifacts, parent stopped only its verified owned
process tree. Sources and five binaries were unchanged. Its 91,248-byte
stdout has SHA-256
`0473458A5787DFD0BB3796A6055AB7AFFD11AC21AFF43780FC39DFDBEC22B78C`;
stderr is empty. The partial stream contains 688 START and 676 PASS lines,
seven outer failing groups, and no terminal `host.tests`. Eleven raw FAIL
lines include four echoed child diagnostics, some expected rejections; they
are not eleven independent group failures. No complete-suite acceptance or
marker-checker result is claimed for this stopped run.

The first clarification-comment script failed parameter binding before any
API operation because PowerShell 5 wrapped a JSON array. Removing only the
extra wrapper allowed the reviewed comments to post and read back once.
The original script and failure remain preserved. A fixture packaging check's
CRLF assumption was corrected to the actual LF bytes; it changed no source.

## Deferred work and evidence boundary

Finish independent focused/final-transaction review, then run a fresh common
solution build and complete host suite on the new clean commit. Fresh default
and enabled QEMU captures, native/raw lifecycle/disk review and reviewed merge
remain required before closing #58/#532. PR535 remains open and held.

This step establishes no live capture, map-key freshness, ExitBootServices,
post-firmware ownership, collecting GC, general runtime, hosted .NET/Kestrel,
cloud or production support. The remaining blocked prerequisites stay open.
