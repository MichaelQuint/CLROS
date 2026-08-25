# Step 917 - Refresh the SharpOS upstream regression map through step 165

## Summary

Refreshed CLROS's canonical SharpOS reference record from the immutable local
archive state to the reviewed 2026-08-21 upstream tip. A complete content audit
identifies the local 1,263-file archive as upstream commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`, except for upstream's top-level
`LICENSE`, and records current `main` at
`0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`.

The 34-commit delta through step 165 was classified into existing CLROS backlog
work. Twenty-four source-linked regression addenda were added to matching Gitea
issues. No SharpOS file was copied, no local reference snapshot was changed,
and no SharpOS output was treated as CLROS evidence.

## Why

SharpOS has progressed beyond the local step-143 archive and the previously
reviewed step-149 state. Its newer interface dispatch, unwind/SIMD, scheduler,
GC/root, page-release, HPET, cache-policy, PCI truncation, hosted-runtime
isolation, and PE-manifest work contains useful negative and conformance cases.
Those cases improve CLROS's dependency-ordered backlog, but SharpOS still has a
different runtime/host boundary, single-core scheduler assumptions, separately
licensed dependencies, an unreproduced external CoreCLR fork, and no network
driver. The refresh records reusable lessons without importing the architecture
or distracting the first Minimal API appliance path with USB, terminal,
PowerShell, NES, or DOOM work.

## Files changed

- `docs/reference-map.md`: records the base/tip/compare anchors, 34-commit and
  step-165 delta, and maps reviewed findings to existing CLROS issues.
- `docs/fork-diff.md`: updates provenance/evidence classes, the high-value
  regression crosswalk, rejected/deferred breadth, and the no-network boundary.
- `docs/license-audit.md`: records the content-identified archive revision,
  reviewed-tip CC0 evidence, and separate XtermSharp, Terminal.Gui, TurboXml,
  dotnet/runtime, Fami/TriCNES, ManagedDoom, WAD, and CoreCLR-fork boundaries.
- `done/step917.md`: records the research, tracker, preservation, and validation
  evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`: pass in
  2.798 seconds with zero warnings and zero errors.
- `dotnet build .\CLROS.slnx --nologo -m:1`: pass in 6.695 seconds with zero
  warnings and zero errors after rebasing on the recovered host baseline.
- Complete `OS.HostTests` from the beginning: exit 0 with 3,452
  `TEST_START`, 3,453 `TEST_PASS`, zero `TEST_FAIL`, and exactly one terminal
  `TEST_PASS host.tests` in 2,982.069 seconds.
- `scripts/check-naming.ps1`: `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall` in 23.825 seconds.
- `git diff --check`, exact ownership, and reference build-firewall checks:
  pass; the committed implementation diff is exactly the three authorized docs.
- `dotnet sln .\CLROS.slnx list` and project/props/targets scans: zero reference
  snapshot projects or `references/` imports; the matching hostile source
  firewall cases also pass in the complete host log.
- `scripts/sync-gitea-backlog.ps1` preview:
  `SYNC_SUMMARY created=0 updated=0 unchanged=429` with
  `skipped=0 failed=0 wouldCreate=0 wouldUpdate=0`, and terminal
  `TEST_PASS gitea.backlog.sync`.

## Evidence

- Local archive identity: 1,263 files compared against all 1,264 blobs at
  `b80ee3a4175e1522d1a12ba7fe371a49da3207f5`; exactly one missing local path,
  `LICENSE`; zero extra paths and zero differing blob hashes.
- Reviewed upstream tip:
  `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`, commit date
  `2026-08-21T09:13:25+03:00`, latest numbered file `done/step165.md`.
- Immutable comparison:
  `https://github.com/daniilvaino/SharpOS/compare/b80ee3a4175e1522d1a12ba7fe371a49da3207f5...0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`.
- Delta: 34 commits after the local archive and 24 commits after reviewed
  step-149 commit `934b4fd8e4e8849d13d774bacfef063870a18d05`.
- Documentation implementation commit after baseline rebase:
  `9fd231822ef0bfa2e43e6f00b6422aa85b9bccf8`.
- Complete HostTests log: 442,997 bytes; SHA-256
  `9FDCE62AA4D941D5DC2C7B71B86E703A97B787D417E3174FEE5F557E2D3A5AED`;
  final line exactly `TEST_PASS host.tests`.
- Solution-build log SHA-256:
  `BD8F050AA55F7F9995B85CDC63716B4C2C8BC2E71EE2285A29FAA1CF6D6749A9`;
  zero reference-path mentions.
- Final documentation SHA-256 values:
  `docs/reference-map.md` =
  `1E0436687E9FB6949AD6C799B1ECC60F8255D9AE0E4E6062796C8C28DA79D508`;
  `docs/fork-diff.md` =
  `AC923A20835A33D467F55C0BC751BF63C18C6F5494924868E8A53F2231CB8440`;
  `docs/license-audit.md` =
  `F00201B37CA6B905EDD0ED38F074D65FF5A6FF93D6DEC77D8E40DDC662EE0476`.
- Gitea addenda GET verification: 24 issues checked; each body contains exactly
  one `<!-- clros-reference-addendum:sharpos-0c00476f -->` marker and an
  immutable SharpOS commit link; all 24 remain open with their managed metadata,
  labels, milestones, dependencies, and desired states preserved.
- All 37 SharpOS URLs across those addenda are immutable commit/blob links; the
  addendum in every body follows, rather than mutates, its single managed block.
- Addendum targets: #85, #89, #91, #94, #96, #99, #163, #164, #177,
  #183-#194, #211, #212, and #216.
- The three-doc committed diff contains no `references/**`, first-party source,
  project, build/QEMU script, application, managed-backlog, or existing done-step
  change.

## Failures

- The first addendum pass used non-immutable source links in 13 bodies. Before
  acceptance, #94, #99, #177, #184-#186, #188-#189, #191-#193, #211, and #212
  were corrected to exact commit URLs and independently GET-verified. No managed
  key, dependency, label, milestone, desired state, or existing body content was
  removed.
- A first independent raw checkout inherited host `core.autocrlf=true` and
  reported 1,167 line-ending-only differences. A fresh ignored checkout with
  `core.autocrlf=false` proved zero SHA-256 content differences across all 1,263
  common files. Only that raw comparison is accepted.
- A first naming-check wrapper inspected an unset `$LASTEXITCODE` after both
  pass markers had printed. Rerunning the unchanged script and checking `$?`
  produced the accepted naming/source-firewall pass.
- A first Gitea read audit assumed managed backlog issues should be assigned to
  `codex`; the manifest and synchronizer intentionally do not manage assignees.
  Rechecking canonical unassigned metadata produced the accepted 24/24 audit.
- `references/SharpOS-main` has no `.git` metadata and omits the upstream
  `LICENSE`. The audit therefore retains exact local-path/hash requirements even
  though complete blob comparison identifies the corresponding commit.
- SharpOS's newest breadth does not include a live network driver. No issue was
  created to pretend that its progress shortens CLROS's M07 networking or M10
  Minimal API/Kestrel prerequisites.

## Deferred work

- Newer SharpOS source remains upstream research only. Any future adaptation
  must record the exact immutable commit, source path, file hash, license, and
  copied-versus-reimplemented boundary in its implementing issue and done step.
- USB/xHCI, interactive terminals, Terminal.Gui, PowerShell, NES, and DOOM are
  optional/deferred rather than prerequisites for the single-workload cloud
  appliance demonstration.
- This step proves provenance research and backlog-test refinement only. It
  proves no CLROS boot/runtime/device/network/application capability and does
  not advance real `ExitBootServices`, hosted .NET, Minimal API/Kestrel,
  CloudReady/provider, or production completion gates.
