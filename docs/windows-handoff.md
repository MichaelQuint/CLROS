# Windows development-machine handoff

## October 20 resumed validation

[Step1479](../done/step1479.md) completes the original v1 and five memory
controls after workspace cleanup. The six-command controller is terminal/passed;
source/native copies remain under `artifacts/service-validation-20261020-r01/`.
The host-only unsupported Task<int> repair passes its red/green focused checks.
All 16 issues remain open pending a fresh full-host gate and scoped closure review.

Keep the primary on `codex/resume-service-validation-20261020` while the frozen
full lane runs. Locate its exact process using `full-host-return-kinds-r01.state.json`
in that evidence root; verify PID and start time or the live session before
calling it stopped. A state file alone is not liveness evidence. Do not edit or
switch source during the gate, and do not restart solely after an observation
timeout. The separate quota-classification proposal remains ignored/unapplied.

## October 19 workspace cleanup

[Step1478](../done/step1478.md) consolidates development in `C:/Projects/CLROS`
on current LAN master. All sixteen audited checkouts were committed and clean:
fifteen heads were ancestors of master; the remaining runtime commit was already
present as an identical cherry-picked patch. The fifteen linked checkouts are
now unregistered, and four leftover marker-only wrappers are archived.

Preserved archive: `C:/Projects/CLROS-archive/2026-10-19-workspace-cleanup`.
Its `cleanup-state.json` maps each original checkout to
`worktrees/<name>/CLROS`, retains the head/branch and six verified evidence
hashes, and inventories 46 archived primary `bin`/`obj` directories. Complete
source snapshots, ignored captures, native inputs and copied Git registration
metadata remain there; `repository.bundle` preserves all local Git refs.
The primary's existing `artifacts` and `references` directories remain in place.

For historical paths beginning `C:/Users/AI/.codex/worktrees/<name>/CLROS`, use
the matching archive prefix. Original receipts are unchanged; their embedded
absolute paths are historical and may require explicit relocation when audited.
Archived `.git` pointer files are renamed `.git.archived-pointer`, so the
archives are evidence snapshots rather than active worktrees. Build fresh
tooling in the primary before new validation. Workspace cleanup adds no runtime,
guest, issue or gate acceptance; the pending work in step1477 remains pending.

The cleanup PR/master readback and final single-worktree state are recorded in
the archive's `cleanup-merge-readback.json` after the documentation merge.

## October 19 development checkpoint

[Step1477](../done/step1477.md) records the completed host run, successful original
guests, failed v1 consumer and unrun memory controls. PR786 merges development
only; all 16 affected issues remain open. The merge/master identity and exact
tracker readbacks are retained after merge in
`artifacts/kestrel-churn-foundations-r01/run-closeout-merge-readback-r01.json`.

Resume from current LAN master in a fresh development cycle after checking live
ownership. Step1477 originally retained the evidence worktree and paused primary.
The later user-authorized cleanup in step1478 supersedes that workspace layout;
the complete `nativeaot-logging` evidence snapshot is preserved in the archive
above, and development resumes from the primary checkout.

Step1479 completes the repaired v1 producer and five memory controls against their
exact source. Qualify the new host runner, then review the 14 conditional candidates. Keep #748/#225 open
until their full quota/failure/cleanup criteria pass. Do not repeat the completed
full-host run solely to refresh a count; the newer host-only delta needs scoped
validation, and any future kernel/runtime changes need the relevant stable gate.
Refresh all issue dependencies and retained hashes before reusing evidence.

## Earlier handoffs

The October 7 clean stop is recorded in [step1076](../done/step1076.md).
Use `artifacts/clean-stop-20261007/handoff-manifest.json` for the final master
identity, all-ref bundle, tracker snapshot and evidence inventory. That generated
manifest is finalized after the documentation merge so the bundle includes it.
The accepted throw/catch implementation and commands remain in
[step1075](../done/step1075.md); the broader backlog was paused at that stop.

On October 7, development resumed on the destination at `C:/Projects/CLROS`.
The source Gitea at `192.168.4.35:3000` was verified as repository `admin/CLROS`,
ID 9, with `master` at `69f9c53085a3a218c1547f2199a7a1c464703058` before
changing `origin` from localhost to the LAN address. The destination's localhost
Gitea is unrelated. Use the explicit source endpoint for tracker operations;
the loopback-only plugin remains a source-host tool. The compiled backlog
synchronizer verifies and uses the canonical LAN repository from this host.

The October 8 migration selected SDK `10.0.401` with Visual Studio 2026 Insiders
MSVC and the new QEMU installation. See [host requirements](host-requirements.md)
for the current SDK policy. Pinned ILCompiler and selected NativeAOT runtime
are qualified at `10.0.12` under #590; SDK-selected and selected-runtime roots
retain separate ownership identities even when their package versions agree.
Copied evidence retains its historical scope. Fresh host/native/guest validation
of the finite migration matrix passed on October 8 in
[step1078](../done/step1078.md). Other historical optional-profile acceptance
is not repeated by copying build outputs or changing SDK selection.

On October 13, the user selected .NET 11 for the OS, tooling and guest target
after Visual Studio Insiders updated to `18.12.12308.306` and MSVC linker
`14.52.37025.0`. The repository now pins SDK `11.0.100-rc.2.26504.105` and
runtime/ILCompiler `11.0.0-rc.2.26504.105`. See
[host requirements](host-requirements.md#october-13-2026-host-toolchain-refresh)
for exact selection, package and CPU requirements. Migration
[#727](http://192.168.4.35:3000/admin/CLROS/issues/727) is qualified in
[step1225](../done/step1225.md) for its finite host/native/guest matrix and
separate fresh Linux-host payloads. No CLROS guest application is accepted.
The private .NET 10 recovery is for historical checkouts only. Retain earlier
receipts unchanged; a compiler or linker update does not requalify them.

The destination copy audit and recovery are recorded in [step1077](../done/step1077.md).
It verified the original manifest references, bundle refs and 190,179 of 190,181
artifact files on the initial pass. Both differences were subsequently recovered
from exact retained duplicates, with independent hash readback: a missing archived
synthetic fixture and a generic publish log overwritten by the initial destination
preflight. The failed command and initial audit remain preserved. These records
verify historical file recovery, not fresh execution on the destination.
CLROS source history, tracker state and generated evidence are separate parts of
the handoff. The destination already has Windows, Codex, .NET 10, `C:/Projects`
and its own Gitea. The default is to keep using the source computer's Gitea over
the LAN at `192.168.4.35`. No server migration or replacement of the destination's
Gitea has been selected.

## Freeze and preserve

Finish the current acceptance and integration work before creating the final
package. Record the final accepted master commit/tree, remaining branches and
issue/PR states, accepted evidence paths, and any unfinished result in one
handoff manifest. The final result of #582 replaces earlier preparation notes;
do not carry forward an obsolete permanent hold or mark development evidence as
accepted. A running process cannot be moved by copying its output directory.

Preserve these components together:

- A clean final source checkout plus an **all-ref Git bundle**, with the exact
  ref-name/object inventory before and after export. Include local and remote
  branches, tags, `refs/codex/snapshots/*` and `refs/codex/turn-diffs/*`. Check that
  each retained worktree HEAD is reachable; preserve an otherwise unreferenced
  HEAD separately before cleanup. A master-only clone does not preserve all of
  this local history.
- The primary `artifacts` evidence tree: successful and failed commands,
  receipts, source/native binaries, raw logs, independent reviews and acceptance
  records. Keep original path-bearing receipts unchanged; add an old-to-new path
  map. The main Git bundle does not preserve the ordinary reference snapshots.
  The completed `artifacts/clean-stop-20261007/reference-retention/` package has
  `reference-snapshots.zip` with its `.files.json` and `.verified.json` companions.
  Carry all three and restore relative entries under the destination
  `references/` directory, using the retained path map. The October 7 audit found
  no embedded Git repositories in those 39 snapshots; no nested bundle is needed
  for that set.
- Verified retained-worktree ZIPs and their `.files.json`/`.verified.json`
  companions, retention plans and reviews. These preserve ordinary file bytes,
  not NTFS permissions, alternate streams or every empty directory.
- A final read-only CLROS public tracker export and concise resume state. Keep
  raw responses and the explicit incomplete/error manifest. Gitea 1.26.2 filters
  timeline pages after pagination, so this exporter cannot prove exhaustion and
  must retain its timeline gaps (`Complete=false`); it is not a complete tracker
  backup or restore image. The source Gitea remains authoritative.

Use a new package directory outside the trees being included. Record relative
paths, sizes and SHA-256 values, then verify every copied file at the destination.
Create the transfer inventory only after the source tree is quiescent; it is not
an atomic filesystem snapshot. Reparse links are recorded as metadata, not
followed as ordinary payload, and any required destination links need separate
review. Verify the actual ZIP/bundle bytes, not only their JSON receipts. Do not include
credentials, credential stores, Gitea `app.ini`/databases, account tokens or
unrelated user profiles. Git snapshot refs and saved evidence do not promise
migration of Codex chat sessions.

An all-ref export can use `git bundle create <new-bundle-path> --all` from the
source repository. Preserve a ref inventory and use `git bundle verify` plus
`git bundle list-heads` to check the export. For recovery of every custom ref,
clone the bundle into a new bare mirror with
`git clone --mirror <bundle-path> <new-mirror-path>` and compare its refs with
the inventory. Keep that recovery mirror separate from the normal working clone.
Never copy a linked worktree's `.git` pointer and expect it to work on the new
computer; recreate needed worktrees from the preserved repository/commits.

## Verify the destination's server and clone

Confirm the source computer still owns the recorded LAN address. Set the final
commit below from the reviewed handoff, then run these read-only checks on the
destination. Stop on a mismatch; do not configure credentials against an
unverified endpoint.

```powershell
$sourceServer = 'http://192.168.4.35:3000'
$remote = "$sourceServer/admin/CLROS.git"
$expectedCommit = '<final accepted 40-character master commit from handoff>'
if ($expectedCommit -notmatch '^[0-9a-fA-F]{40}$') { throw 'Set the handoff commit first.' }
Invoke-RestMethod "$sourceServer/api/v1/version"
$repo = Invoke-RestMethod "$sourceServer/api/v1/repos/admin/CLROS"
if ($repo.full_name -ne 'admin/CLROS' -or $repo.default_branch -ne 'master') {
    throw 'Unexpected Gitea repository identity.'
}
$masterRef = git ls-remote --heads $remote refs/heads/master
if ($LASTEXITCODE -ne 0 -or (($masterRef -split '\s+')[0] -ne $expectedCommit)) {
    throw 'Remote master differs from the final handoff.'
}
```

Clone only into an absent directory; preserve any existing checkout:

```powershell
if (Test-Path -LiteralPath 'C:\Projects\CLROS') { throw 'Preserve the existing directory first.' }
git clone $remote 'C:\Projects\CLROS'
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
if ((git -C 'C:\Projects\CLROS' remote get-url origin) -ne $remote) { throw 'Unexpected origin.' }
if ((git -C 'C:\Projects\CLROS' rev-parse HEAD) -ne $expectedCommit) { throw 'Unexpected HEAD.' }
git -C 'C:\Projects\CLROS' status --short
```

Historical `localhost:3000` instructions refer to the source host. This host's
localhost is an unrelated Gitea. Use the compiled `OS.Tooling backlog sync`
against its guarded LAN endpoint, repository ID9, with secure environment
credentials. Preview a scoped `--issuekeys` selection before `--apply`, then
read back the issue and repeat the preview. Do not use the loopback-only plugin
or weaken endpoint guards. See [issue workflow](issue-labels.md).

## Resume

Read [AGENTS](../AGENTS.md), the final handoff manifest and the current
[completion gates](completion-gates.md). Verify [host requirements](host-requirements.md):
the SDK selected by `global.json`, Desktop PowerShell 5.1 where receipts bind it,
MSVC/Windows SDK, pinned NativeAOT packages, and QEMU/OVMF for later guest work.
Recreate local build outputs when needed; copied `bin`/`obj` files are not fresh
build evidence. Copy verification alone does not require replaying every accepted
suite. Resume only the explicitly selected work, preserving its evidence boundary.
