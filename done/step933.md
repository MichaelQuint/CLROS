# Step 933 - Accepted layout guest and page-image host evidence

## Summary

Accept PR #511's complete host gate and fresh default/layout NativeAOT QEMU
matrix at source `5f6108ca363f03c7ebad8dafca7ae78e1d3a0db2`, tree
`1ab2491342bbade7f83379df0108f02cd33e8603`. This covers the #510 host page-image
leaf, #74 pinned scalar layouts and #512 default-off EFI adapter. Independent
reviews found no remaining findings within those evidence boundaries.

## Why

The prior full run failed a required manifest ownership sentence. Step932
preserved that failure and restored the note without weakening its predicate.
The corrected combined source needs its own complete run and fresh guest
artifacts before merge and closure. Old artifacts cannot stand in for this run.

## Files changed

- `.gitea/backlog/clros-issues.json`: M04-005 evidence and desired completion.
- `docs/efi-build.md`, `docs/qemu.md`, `docs/nativeaot-nostdlib-limits.md`,
  `docs/completion-gates.md`: accepted layout evidence and limitations.
- `docs/diagnostics.md`: complete host coverage for the #510 leaf.
- `docs/image-builder.md`: newly discovered, separately tracked #514 defect.
- This step, reserved with `scripts/new-done-step.ps1 -Reserve`.

No executable source, tests or scripts change in this evidence commit.

## Tests run

- The combined solution build at step932 passed with zero warnings/errors.
- `dotnet run --project ./tests/OS.HostTests/OS.HostTests.csproj --no-build`:
  actual exit 0; 3,527 starts, 3,528 passes, zero failures; exactly one final
  `TEST_PASS host.tests`; empty stderr. Source stayed clean and unchanged.
  Wrapper interval: 2026-10-01T22:57:38.7308365Z through
  2026-10-02T00:20:56.5493337Z. The harness finished before 00:16:43Z; idle
  reusable MSBuild/compiler servers delayed the wrapper. Graceful
  `dotnet build-server shutdown --msbuild --vbcscompiler` exited 0 and allowed
  the existing wrapper to capture its actual process exit. No test restart,
  inferred exit or forced harness termination occurred.
- Fresh default and layout commands are archived as exact argument arrays in
  each lane's `command.json`. Both use `powershell.exe -NoProfile
  -ExecutionPolicy Bypass -File scripts/run-qemu-smoke.ps1 -BuildEfi`, installed
  QEMU/OVMF, timeout 30 and unique EFI/image/serial/summary paths. The layout
  command adds all four M04 switches, including `-EnableM04LayoutContractProbe`.
  Default ran 00:22:42-00:23:44Z; layout ran 00:23:45-00:24:44Z on October 2.
  Both fresh publishes, same-build verifications, image builds and wrappers
  passed with exit 0. Layout met 18 required, six ordered, four unique and six
  forbidden constraints plus the strict whole-line transcript audit.
- Independent review rehashed 94 archived files, 82 source inputs and 900
  package files (298,827,245 bytes) per lane, checked restore/tool identities,
  69 Compile items, and only the four expected profile-flag differences.
  It reran strict serial/summary audits and independently decoded both GPT/FAT
  payloads. Default has no M04 records; layout has four in the required order.

- After the manifest edit, the actual `GiteaBacklogManifestTests` module ran
  through `artifacts/orchestration-20261001/manifest-focused/ManifestFocused.csproj`:
  all 17 checks passed, terminal `TEST_PASS manifest.focused`, exit 0 and empty
  stderr. Logs use the `step933-manifest` prefix in the orchestration directory.
- `sync-gitea-backlog.ps1 -Offline` and `-SelfTest` both passed without live
  mutation. Naming/source firewall and `git diff --check` passed. This is not
  a claim that live checked dependency bodies have zero synchronization drift.

## Evidence

Host directory: `artifacts/orchestration-20261001/layout-wave-5f6108c/`.
Guest directory:
`artifacts/orchestration-20261001/issue512-guest-5f6108c-20261002T0022419996556Z/`.
Generated artifacts remain ignored, including exact commands, images, source
snapshots, sidecars, raw logs, failed attempts and independent audit reports.

| Item | SHA-256 |
| --- | --- |
| Host result | 8D7CF8F4E8E6109A6AC76C46D9A1F1330D710B8C4B8059D5C27D6A355CF13108 |
| Host stdout, 457238 bytes | DAC63F838C324D4B29B7AE504B8255D4724B773D74BCF1BDCB14529DAD013D52 |
| Independent host review | 5250C6CFD8C503F2B6E9647E3F661D555D9A1471931DC2D422AFF7E81D9B44F5 |
| Guest matrix result | CDF8B63F5F7E8651974B65FFDE24849639A968BC4DC7F9F7FD97CFCCC4C7E58C |
| Independent guest review | E04E0A1EC40F0A65D0A7F8C992B742F3635FE3E404DDC17D0F3D1DAAFA8ECB31 |
| Default EFI, 55808 bytes | EBAC4F464E5E9E21ECB8830D57DD15BEA4FC9705B5CD1B5AD1F8F67F1FFA77FD |
| Default image, 67108864 bytes | D05C179C143C680EB54AF91D3A06093D73796C55AC421A3D687B610EEC6F1A67 |
| Default serial, 7947 bytes | 0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1 |
| Default summary, 2679 bytes | 250F6773447D873B838984E6B28E2A68B914BED3DFFC7AC058E4CC31E04697AD |
| Layout EFI, 94720 bytes | 35E8F0FD4F1DDA63057740217CC14F258C9C9B721189B5C8F1A3ACD5EF9CC88C |
| Layout image, 67108864 bytes | 7ADDF3300B62AC44103264FF49A2FD6EB947703A4974F7E8CE38D5EBC017370F |
| Layout serial, 8682 bytes | 832AE32F7EDBC61E938D1FF498ED5CE23F120E8D474B7B3FD78A8917D825C57D |
| Layout summary, 5900 bytes | 734C3756C761D1C21C54A2457CC1A17CBF798E97BC0500D959382A244C78F3A1 |
| Independent default payload report | C3934A78484896A6C716B0EE3BBB62370BDDDDC89E5E55357331124636C56690 |
| Independent layout payload report | EBA74B9E3B30B8A1181951B43BB0AEA73AB1C06AA9A3E8F4D19B5BE445D5DFDB |

Source authority: 69 Compile items / 82 raw inputs, aggregate
`1dc878bb2e22305963e1d65cf0f838bdd33c0f365756b7ef21a00d331950a28a`.
SDK 10.0.302; ILCompiler/runtime pack 10.0.8; linker 14.51.36260.0;
Windows SDK 10.0.28000.0. QEMU 11.0.50
`v11.0.0-12631-g54e84cdc7a`, SHA-256
`B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`;
OVMF SHA-256 `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

The executed guest record is exactly:

```text
CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

The actual loaded HostTests DLL hash is
`460A5ADAD53EE3EB54F7910166CD8F4BEA4D8DF738F79CF325F7B33A6368D0DB`;
the kernel beside it remains
`CB2BCB6EBB4FC94DFD7800D7E204C6FFD04B740B28790942DD011DEEA8780423`.
The separate kernel build output changed consistently with the suite's nested
QemuRunner build path. Independent comparison accounts for every changed byte
as commit-version and generated PE/PDB identity metadata; executable source and
loaded bytes are unchanged.
The default intermediate managed OS.dll was superseded by the layout publish;
its manifest hash is build-attested, not a post-run direct rehash claim.

## Failures

- The earlier failed complete run at 6146068 remains preserved and documented
  in step932. Its manifest ownership predicate passes in this corrected run.
- Both guest runs retain the exact 6947-byte OVMF return suffix. The generic
  parser reports 207 invalid display bytes/two lines, not zero. CLROS records
  pass strict validation. Native QEMU exit -1 is bounded runner termination
  after a 750 ms quiet drain, not an application exit or hard-timeout success.
- Independent disk inspection discovered #514: the historical and current
  64 MiB images have one declared trailing cluster beyond FAT capacity and
  stale FSInfo accounting. The independent decoder checks payload chains
  within both limits; exact boot-file identity and observed guest markers
  remain proved. Whole-filesystem conformance is not established. Initial
  decoder rejections are preserved. The fix stays in a separate worktree/wave.

## Deferred work

Close #510/#74/#512 only after reviewed merge and evidence/read-back. #54 stays
open for the terminal emitter, actual CR3 transition and real vector-14 guest.
#513 and #75 become eligible only after their prerequisites close. #514 has a
separately reviewed host fix, not part of this evidence-source commit.

## Evidence boundary

The page-image leaf is host-only; the layout guest runs scalar contracts.
Neither proves physical managed-object layouts, heap allocation, GC, metadata
discovery, dispatch/EH, a live page-fault transition, real ExitBootServices,
hosted .NET/Kestrel or cloud/production readiness. Gate 4 remains Fail and no
other numbered gate is promoted. Remote queued CI is not a passing runner.

