# Step 938 - Correct two exact artifact-provenance input counts

## Summary

Correct the two existing M02-011/M02-012 artifact predicates from 82 to 83
source-authority inputs after supplemental #516 adds the retained heap leaf.
The complete source path order, raw source hashes, artifact identities and
property predicates remain unchanged. No production source changes in this step.

## Why

The first complete wave gate at `b5784a278664bf44cd53711a73b36e93e178dddf`
failed the M02-012 actual-artifact provenance case. Its default and enabled
NativeAOT publications both succeeded, but the old count rejected the valid
83-input manifest. Independent reconstruction of all 26 predicates found only
that count mismatch in both preserved tuples. The matching M02-011 predicate
was also stale; that later module was not reached by the failed run.

The earlier selected-method harness did not exercise these complete artifact
cases. This repair therefore uses both full modules with their original
ModuleInitializers and actual artifact publications intact, before requiring
a new complete host gate on a newly committed head.

## Files changed

- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs` and
  `M02OwnedIdtGatePopulationEfiIntegrationTests.cs`: one exact count each.
- `docs/efi-build.md` and `docs/completion-gates.md`: failed-gate boundary and
  strict repair evidence.
- This step, reserved using `scripts/new-done-step.ps1 -Reserve`.

## Tests run

- Independent static audits of all eighteen affected existing fixtures:
  twenty-four live authority hash pins match; the two input counts are the
  only stale count/hash/property expectations found. All 83 paths and raw
  hashes match both preserved M02-012 manifests.
- Complete IDTR/IDT focused harness built with Release/net10.0/C#14, actual
  kernel and QemuRunner dependencies: exit 0, zero warnings/errors, empty stderr.
- Complete focused execution: actual exit 0, 14 starts, 15 passes, zero
  failures, one final `TEST_PASS issue516.idtr-idt.full-modules`, empty stderr.
  Both complete module pass/host-ready records and both exact artifact audits
  are present; every default/enabled build exits 0 with provenanceExact=true.
  The test ran from 03:44:03 to 03:48:08 UTC on 2026-10-02.
- `git diff --check` passes for the two test corrections.
- Independent focused review verifies every marker pair, native completion,
  input identity, all four new artifact/verifier tuples, reconstructed ordered
  70/83 authority, all 23 evaluated properties, and recorded tool identities.
  The final five-path diff/doc review reports no substantive findings.

## Evidence

Preserved failed run:
`artifacts/orchestration-20261001/heap-fault-wave-b5784a2/`.
The native process ended at 2026-10-02 03:30:52 UTC with exit `-532462766`,
1,359 starts, 1,357 passes, two failure markers, no terminal host pass and
275 stderr bytes. The two failures are one artifact case and its enclosing
module, not two independently observed failing modules. Root head/tree stayed
unchanged and the working tree was clean throughout that run.

| Preserved evidence | SHA-256 |
| --- | --- |
| Failed result.json | 1BE9040531196E9D8B462DA6F6BB638962522D3527187734931C10F71B340C5F |
| Failed host stdout | 802AE78EFFDC088986C12C82553EE4427656209E5FD5645D6D6260AA793A8414 |
| Failed host stderr | 5F3B22CDBFBA5DEC6BAD16508CA6C8DB34901FDF10461D83EAA4271443949F98 |
| Author eighteen-fixture audit.json | 84787DCAFF688F5830FFA71C3A146EAB4C281CA5E777D8328B3F0F908C9B6889 |
| Independent failed-predicate-check.json | D7556D45E1B5343D068D1926477D8BCD39604C24715DF92FB9689BC61A11EF5B |
| Independent diagnosis.md | 7076EDAAEFC2965A4C6A18D6BA7AD47A70D6253E1426DA93857A1237CF31B40A |
| Independent harness-review.md | 4513BD04DB52E331E130860E77DDCA90C973808F62D72899AD899C17CEB91A06 |
| Focused build stdout | F60C52F259D12335C4B2E1596A2F26BCFB6420BC4DA73D7FD440506FFB340A01 |
| Complete focused test stdout | B96F4D7C524E1A6A072A7D810AC18EDA02DB10CEF3CC5DF0909F2C4BF5A2E0EC |
| Independent focused-evidence-check.json | 127124B119894F2BCAEB26ED8FD9DB6BA9F9908A2DD28F990A6BA37361D89BB4 |

Investigation reports are under `issue516-failed-gate-investigation/author/`
and `review/`; focused execution records are under `issue516-repair-focused/`,
all beneath the same orchestration directory. The independent diagnosis
explicitly records the literal comparison missed by the previous review.
The 70-Compile identity remains
`06D136C83FD42C3979662ECF7BAC6CD1A7654113C5F2392CC6BF3B16DEC04561`;
the 83-input raw closure remains
`48151B6C038674F0C9B7FCD43A2DA87CE9766CA2480AF9E4FAAF5BC546111BBC`.

Fresh actual artifact tuples use child process 13464:
`artifacts/efi/BOOTX64-M02-011-HOSTTEST-13464-{DEFAULT,ENABLED}.EFI` and
`BOOTX64-M02-012-HOSTTEST-13464-{DEFAULT,ENABLED}.EFI`, with link sidecars and
matching `artifacts/logs/m02-01{1,2}-hosttest-13464-{default,enabled}.log`.
These are four new publications, not reuses of the failed gate's PID33348
artifacts. The focused harness links both original complete test modules,
HostToolProcess and the real kernel/QemuRunner projects without source copies,
attribute stripping or selective reflection calls.

## Failures

The failed b5784a2 full run remains failed and preserved. No count check was
removed, no expected value was derived from untrusted sidecar output, and no
ordered-path or raw-hash predicate was weakened. Earlier step937 and all its
successful/failed preparation evidence remain historical records.

The parallel read-only backlog audit also identified an accidental #54
closure from PR #511's closing reference (timeline events 13650/13654/13659),
without its required guest fault evidence. Comment 13763 records the exact
chronology and evidence gap; the parent reopened #54 and verified its body,
title, labels and assignee were preserved. The existing docs/manifest already
retain this outstanding work, and #55 remains blocked on it.

## Deferred work

Complete and independently accept a new settled-source full host gate, then
fresh default/layout/heap NativeAOT/image/QEMU tuples and independent corrected
FAT accounting. Finish manifest/docs/tracker evidence and merge PR #515 before
closing #75, #513, #514 or #516.

## Evidence boundary

Focused actual NativeAOT artifact builds and controlled host tests are not
guest boot evidence. No real fault, installed paging, object allocation, GC,
hardware NX, post-ExitBootServices lifetime, hosted .NET/Kestrel or cloud
readiness is established. Gates 3 and 4 remain Fail.
