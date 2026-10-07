# Windows development-machine handoff

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

The repository's `localhost:3000` instructions refer to the **source host**.
The destination's localhost is its unrelated Gitea. Existing REST issue helpers
and `sync-gitea-backlog.ps1` enforce loopback; passing a LAN `-BaseUrl` is not a
supported workaround. Run those tools on the source host, or explicitly
configure/review scoped LAN REST tooling and secure credentials before tracker
operations from the destination. Do not rewrite the historical manifest target,
weaken endpoint guards or import a server database as part of this handoff.

## Resume

Read [AGENTS](../AGENTS.md), the final handoff manifest and the current
[completion gates](completion-gates.md). Verify [host requirements](host-requirements.md):
the SDK selected by `global.json`, Desktop PowerShell 5.1 where receipts bind it,
MSVC/Windows SDK, pinned NativeAOT packages, and QEMU/OVMF for later guest work.
Recreate local build outputs when needed; copied `bin`/`obj` files are not fresh
build evidence. Copy verification alone does not require replaying every accepted
suite. Resume only the explicitly selected work, preserving its evidence boundary.
