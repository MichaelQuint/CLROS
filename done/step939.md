# Step 939 - Retained heap guest and corrected-image acceptance

## Summary

Accepted the complete host gate, fresh default/layout/heap NativeAOT QEMU
matrix and separate corrected-image accounting at source commit
`4b18b4f5856f3070ba0415b9f5fa4bf48e649521`, tree
`24662149689f723c5692eaf03089228b8e4bd21c`. Independent reviews found no
remaining host, guest or image-accounting issue. This records evidence for
#75, #513, #514 and #516; reviewed PR #515 merge and tracker read-back precede
their live completion. Issue #54 remains open for real page-fault work.

## Why

Steps934-938 implemented corrected FAT geometry, the retained-page contract,
host-only terminal fault emitter, disabled-default heap EFI adapter and two
strict source-count corrections. Focused tests alone could not establish
complete integration or guest behavior. The final image checks also separate
pristine builder accounting from firmware changes to booted images.

## Files changed

- `.gitea/backlog/clros-issues.json`: only M04-006's `labels`, `desiredState`
  and `currentEvidence`; ownership/dependency/acceptance notes are preserved.
- `docs/nativeaot-nostdlib-limits.md`, `docs/efi-build.md`, `docs/qemu.md` and
  `docs/completion-gates.md`: actual bounded heap guest evidence and limits.
- `docs/image-builder.md`: pristine geometry/accounting and firmware changes.
- `docs/diagnostics.md`: complete host acceptance for #513, separate from
  real fault execution.
- `docs/issue-labels.md`: neutral issue-reference guidance following step938's
  investigation and correction of the unintended #54 closure.
- `done/step939.md`, reserved with `scripts/new-done-step.ps1 -Reserve`.

This final transaction changes no C#, project/build input, runtime shim,
probe toggle, boot order or generated artifact. Earlier implementation files
and their reviews remain recorded in steps934-938.

## Tests run

- Same-head Debug solution build: actual exit 0, zero warnings/errors. The
  complete host gate then ran `dotnet run --project
  .\tests\OS.HostTests\OS.HostTests.csproj --no-build` from 03:56:32.2357261
  through 04:57:25.0032657 UTC on 2026-10-02: native exit 0, empty stderr,
  3,541 starts, 3,542 passes, zero failures and one `TEST_PASS host.tests`.
  Both repaired complete M02 artifact modules and all eight heap EFI groups
  passed; source and binary identities remained unchanged.
- Fresh `scripts/run-qemu-smoke.ps1 -BuildEfi` default/layout/heap lanes used
  unique EFI/image/serial/summary outputs and 30-second guest deadlines. All
  wrapper exits were 0. Each captured a 750 ms quiet drain, runner-terminated
  disposition and actual native QEMU exit -1, not a guest-requested clean exit.
- All lanes authenticate 70 Compile items, 83 raw inputs, all 23 evaluated
  properties and actual zero-reference `OS` managed assemblies. They enable
  zero/four/seven properties respectively. Strict raw transcript checks pass.
- Each archived Debug ImageBuilder created a separate pristine 64 MiB image
  using `create-raw --workload-profile SingleMinimalApi --efi <archived EFI>
  --output <fresh pristine> --image-size-mib 64`, without rebuilding. Pristine
  payload, booted payload and accounting auditors then ran for each lane.
  All twelve commands exited 0; independent byte review passed.
- The final catalog harness build and execution both exit 0: 17 starts,
  18 passes, zero failures, one `TEST_PASS manifest.focused`, empty stderr.
  Its original module exercises actual-manifest Offline validation and the
  synchronizer's SelfTest. Naming/source-firewall and both working-tree and
  whole-wave `git diff --check` pass. No forbidden first-party native source
  or generated artifact is added by this transaction.

## Evidence

Paths below are under `artifacts/orchestration-20261001/`; generated artifacts
remain uncommitted.

| Evidence | SHA-256 |
| --- | --- |
| `heap-fault-wave-4b18b4f/result.json` | `5836803DA771B0D893B27B7D374502DB351639F39DD360E9FBE1B5B665E49C9B` |
| Host stdout, 459,025 bytes | `CFCCDBC31CAD2709C31F519E7D18E4C4375CCF8BBA4B2AFE8CC6AD780CA91187` |
| `heap-fault-wave-4b18b4f-independent-review/final-acceptance.md` | `F7673C918BA960A4BBE37D17DC417C8E5185655A1C533C699C9961B00F9A86C1` |
| `issue516-guest-4b18b4f-20261002T0537310243945Z/result.json` | `8E08389230DC753E2EF716434D4A9A6CE7D7D802D6652D149F5A5F64C5C99932` |
| Its sibling `-independent-review/final-acceptance.md` | `1C263064988FEA03716F56A11D97A59D436BC82222EACEC626244D446383C398` |
| `issue514-pristine-4b18b4f-20261002T0541574656509Z/result.json` | `6A5898441E51E2209B493676826DE94184B6156543EDF56EFB8BB230AC43507A` |
| `issue514-independent-accounting-review/review.md` | `BF715C078AAFD3FFCCE7B07E363167C07F287287E09C81425AC261D344FFE5DB` |
| `heap-wave-final-checks/manifest-tests.stdout.log` | `A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9` |
| `heap-wave-final-checks/result.json` | `046FFF5A9DFB9AC51F81E26B449CE9D3C4B721DB4EA6C18890B3977FE3254AB8` |

| Fresh lane | EFI bytes / SHA-256 | Serial bytes / SHA-256 |
| --- | --- | --- |
| Default | 55,808 / `EBAC4F464E5E9E21ECB8830D57DD15BEA4FC9705B5CD1B5AD1F8F67F1FFA77FD` | 7,947 / `0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1` |
| Layout | 94,720 / `35E8F0FD4F1DDA63057740217CC14F258C9C9B721189B5C8F1A3ACD5EF9CC88C` | 8,682 / `832AE32F7EDBC61E938D1FF498ED5CE23F120E8D474B7B3FD78A8917D825C57D` |
| Heap | 269,824 / `59F249CC0F69BEC788C79FB22D2FEC089BEA5DC765ADA49B98A69D9E19DD8C50` | 3,538 / `F81E9F6482471E9515840B82992194E86F296521DCC45D089A92A76921B443C7` |

The matrix completed at 05:41:11.4142828 UTC with parent native exit 0. The
heap has 52 CLROS records and 41 owned anchors. Its original parent is
`0x000000000DC44000..0x000000000DC46000`; the retained page ends at
`0x000000000DC45000`. Both checkpoints read 4096 zero bytes. Final accounting
is one free and one allocated page, with original metadata identities retained.
The ready record explicitly reports `objectAllocation=false`, `gc=false`,
`hardwareNxProved=false`, `postExitBootServices=false` and `gate4=Fail`.
The heap terminal tail has no firmware return; default/layout retain their
returning tails.

Image accounting completed at 05:42:08.1685774 UTC. Each image's ESP spans
LBA2048..131038 with two 1,008-sector FATs, data at LBA4096, 126,943 data
clusters and 129,024 FAT entries. Independent review rechecked all six images,
three EFIs, source/tool identities and command/log bindings. Pristine
allocated/free counts are 114/126829, 190/126753 and 532/126411. Both FSInfo
copies match the census and first free cluster; no data cluster is
unaddressable and no pristine allocation is unexplained.

Post-boot differences total 887/887/892 bytes. Exactly three new clusters per
image belong to the 1,393-byte NVVARS file: 116-118, 192-194 and 534-536.
Changes are confined to primary FSInfo, those links in both FATs, the root
NVVARS entry and its data. Existing chains/payloads and clean/no-hard-error
FAT status remain unchanged. Backup FSInfo retains pristine hints. This
validates bounded accounting/chain placement, not NVVARS's internal format
or complete FAT conformance.

## Failures

- The earlier `b5784a2` full gate failed stale 82-input assertions despite
  successful artifact publications. Step938 preserves the failed run and
  exact two-count repair; the old run remains failed.
- Guest attempt1 (`20261002T0502352501911Z`) failed before any lane because
  Windows PowerShell nested a JSON array in `@(...|ConvertFrom-Json)`.
  Direct assignment and a flat-record guard corrected the ignored helper.
  Review: `issue516-acceptance/independent-entrypoint-review/final-review.md`,
  SHA `C882E23EBA1C542183C9E5C7B087DE680354340194C5BECF2801A54416593124`.
- Attempt2 (`20261002T0521246253594Z`) had default/layout wrapper exits 0,
  but layout metadata inspection failed loading a second same-named assembly
  into one reflection-only AppDomain. Heap did not run. Result SHA:
  `7280A8D8BDD98F88A00FA6AD5352B5EB06C933EF2B4A9408A40596435F980F5A`.
  Fresh hidden metadata-only children preserve exact OS/zero-ref checks,
  native exit/output validation and a bounded wait. Seven author controls
  and an independent default/layout/default sequence pass. Independent repair
  review SHA: `6463AFE5056EBEF07FF6FC2D3EEF5827BDD8D9BAB21C99DAD0A9FB37F7A57311`.
- Both failed matrices and auxiliary failed control/observer attempts remain
  preserved. Neither helper repair changed tracked product inputs. The final
  five-helper manifest SHA is
  `1ECF538C75BF4D8B50089CC9CE0F9F2C299A43A5705EC054C18079C9C86EFAE5`.

## Deferred work

After reviewed merge and issue reconciliation, release M04-007's bounded
`RhpNewFast` host contract and the separately owned M02-014 page-fault EFI
adapter. The #513 emitter remains host/compiler/synthetic-execution evidence;
the heap guest does not execute it or satisfy #54.

## Evidence boundary

One retained pre-firmware page is not a managed object heap or runtime helper.
No object/array/string allocation, GC, hardware NX, real ExitBootServices,
post-firmware lifetime, live storage/networking, hosted .NET/Kestrel, cloud
support or production readiness is established. Gates 3 and 4 remain Fail.
No repository CI run or physical-machine boot is claimed.
