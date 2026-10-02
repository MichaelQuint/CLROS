# Step 932 - Layout EFI integration and manifest ownership repair

## Summary

Supplemental #512 integrates M04-005's frozen scalar layout evaluator into the
default-off EFI path. The adapter checks 13 positive and six rejection results
before memory-map capture. PR #511 now combines the reviewed #510 page-image
leaf, #74 layout leaf and this EFI integration; full acceptance is pending.

The prior full run at 614606834efb84ee3936a868d96a5d205937e81a failed on the
backlog manifest ownership sentence. Restore that required standalone note
without weakening the predicate or changing dependencies, states or criteria.

## Why

Guest execution must call the same version-pinned evaluator and bind the
selected profile to the source, build, image and exact serial transcript.
A marker-only command cannot establish this. One corrected settled-source
full gate will cover the combined changes before fresh default/layout QEMU.

## Files changed

- .gitea/backlog/clros-issues.json: one M04-005 ownership-note element.
- src/OS/Boot/EfiEntry.cs and src/OS/OS.csproj: returning 19-check adapter,
  default-false property and one explicit source include.
- scripts/build-efi-shell.ps1, scripts/test-efi-artifact.ps1 and
  scripts/run-qemu-smoke.ps1: propagated property/provenance, strict transcript
  validation and narrow exact path/content source admission.
- tests/OS.HostTests/M04LayoutContractEfiIntegrationTests.cs.
- The 17 existing fixtures listed in
  artifacts/orchestration-20261001/issue512-handoff/changed-files.txt:
  mechanical closure/default/property identities and one shifted include index.
- docs/efi-build.md, docs/qemu.md, docs/nativeaot-nostdlib-limits.md and
  docs/completion-gates.md.
- This centrally reserved step. No BootSequence, Probes, MinimalRuntime or
  frozen layout/page-image production leaf changes.

## Tests run

Author worktree: C:/Users/Roger/.codex/worktrees/m04-005-efi/CLROS, base
614606834efb84ee3936a868d96a5d205937e81a.

- Focused actual-source integration: eight groups, 152 injected adapter result
  mismatches and 226 strict transcript cases passed. Actual no-stdlib source
  compilation has zero assembly references and retains NoInlining.
- Selected regression harness: 55 checks from all 17 affected older fixtures
  passed. This was not a complete execution of those modules.
- Enabled preflight: 69 Compile items / 82 raw authority inputs; all four M04
  flags explicit. OS enabled project and Release host assembly builds passed
  with zero warnings/errors. No native publish or real QEMU was run here.
- Independent production review and separate test/fixture review found no
  remaining issues. Reviewer reran the eight focused groups, reconstructed the
  69/82 closure, and verified all 25 frozen file identities before and after.
- Parent integration verified the 23 incoming paths against matching base blobs,
  then all 25 frozen source hashes. No overlapping root changes were discarded.
- Parent actual GiteaBacklogManifestTests module, via ignored
  artifacts/orchestration-20261001/manifest-focused/ManifestFocused.csproj:
  all 17 checks passed, terminal TEST_PASS manifest.focused, exit zero and empty
  stderr. The existing ownership predicate is unchanged.
- Parent dotnet build ./CLROS.slnx --nologo -m:1: zero warnings/errors in
  25.33 seconds. Log:
  artifacts/orchestration-20261001/combined-layout-solution-build.log.
- Parent naming/source-firewall and git diff --check passed. Final independent
  integration review verified exactly 29 expected paths and semantic manifest
  equality to the prior revision except the one restored note. No findings
  remain after correcting this step's build-log path.

Complete settled-source host execution and fresh default/layout NativeAOT
publish/QEMU are still pending. The failed earlier run cannot cover this
integration or satisfy acceptance.

## Evidence

Evidence root: artifacts/orchestration-20261001/issue512-handoff/.
implementation-report.md preserves commands, source/tool identities and failures.
changed-files.txt and frozen-source-hashes.json enumerate 23 changed paths and
25 frozen identities. No generated artifacts are committed.

| Item | SHA-256 |
| --- | --- |
| Frozen layout source | 44F8A34E2F1911AAE4FD5B028ADB88BF1D8BF06CE915625E9F13D11964F579AA |
| Frozen layout host tests | D22AB6680E73713844C62E2FC66C4DC474F96955FD66D94740618DE16DFB249B |
| EFI entry | DD150FAE01DF91753B3C0161D386F9EAF45E86A3578F10101E0BA02100EA70BB |
| EFI project raw bytes | 709315ACC7EF5AE98DA79DE6F718564201D8EB21D1DEAA6927A79597CAFC91CF |
| New integration tests | A04C2759CA99CFFDC70FCE98EB21CD7256945163880D4B9996DC1325AC9CA88D |
| QEMU wrapper | 29A17320BA5445C7D89D6CB49D124768B5E38D89C44847022C4CAA3EAE1EE825 |
| Ordered Compile closure | 30dd633e38173653aa4b75b8059fa3036097f082284defc2335dbd8f9a9d7d66 |
| Raw source-input aggregate | 1dc878bb2e22305963e1d65cf0f838bdd33c0f365756b7ef21a00d331950a28a |
| Author implementation report | 92E8CA08AE5707880D5B6F596135CCBE47D2295B02844FDAC175AA8B38578E8C |
| Independent focused stdout | 17D188CA4F4BB4FEFE246D31A668FF943DF032A8C19BE2094834400A33A083D6 |

The successful guest line is a required contract, not observed proof yet:

CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail

## Failures

- Prior full gate: artifacts/orchestration-20261001/layout-wave-6146068/.
  Started 2026-10-01T21:54:29.6332010Z; ended 22:44:06.0234541Z.
  Native exit -532462766, 889 starts / 888 passes / one failure / no successful
  host terminal. TEST_FAIL gitea backlog manifest is complete UTF-8 without BOM
  assertion_failed. UTF-8 was valid; the M04-005 ownership sentence was the sole
  shape mismatch among all 345 entries. Commit 3c9ce77's note replacement dropped
  the required literal. This step restores it and preserves the detailed note.
  Raw stdout SHA-256:
  30F5C9CBCBDA4AAA0ECA61175D2B7B8A8155A93E146D4670571960FC18FAAF1C.
  Raw stderr SHA-256:
  AEF63448A4926BCFCFE3A878EDF4973F5A006ABADA1F56AF6F8932A9102E5DD3.
  result.json records Passed=false; logs and diagnostic remain preserved.
- Author first preflight rejected the frozen layout source under the generic
  kernel scanner (system_api). Reviewed admission is exact ordinal path plus
  whole-source identity, normalizing CRLF only. All other scanning stays intact;
  raw provenance remains raw. No runtime shim or generic weakening was added.
- Author test setup's StartsWith char overload compile error was corrected
  before final focused proof. Both setup failures remain in the handoff.

## Deferred work and evidence boundary

Keep #510, #74 and #512 open until accepted combined evidence and reviewed merge.
The #54 terminal fault adapter remains separate; #513's host emitter is blocked
until #510 closes and parent releases its disjoint work. #75 remains blocked on
#74. Gate 4 remains Fail: scalar models and later guest checks cannot establish
compiler-emitted physical objects, heap allocation, GC, dispatch/EH, real
ExitBootServices, hosted .NET, Kestrel, cloud readiness or production readiness.
