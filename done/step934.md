# Step 934 - FAT geometry correction and next host-leaf release

## Summary

Integrate the independently reviewed #514 host image-builder correction into
the next wave. FAT sizing no longer oscillates or pairs a selected FAT size
with a stale data-cluster count. Reject plans whose cluster numbers cannot be
represented without reaching FAT32's bad-cluster sentinel.

PR #511 merged as `b193f590c30e95dc1f7b3b156aafd13722889f65`; its tree
`7548e0bf10b64497fde1cf173a1322d394f2e3c8` exactly matches independently
reviewed head `155d2e342552110753c6801f6b4d12ac405f0dcb`. Issues #510, #512
and #74 received evidence, merge comments, done labels and verified closure.
Their dependent host leaves #513 and #75 are now separately claimed/released.

## Why

The independent GPT/FAT decoder found that historical 64 MiB images declare
126,975 data clusters while their 992-sector FAT has only 126,976 entries,
one fewer than needed with entries 0 and 1. The 16-iteration equality loop
oscillates between 992 and 993 sectors and returns stale count 126,973.
Historical payload/boot evidence remains bounded; those images are preserved.

Ignoring FAT overhead provides an upper bound U on data clusters. Choose F
large enough for U+2 entries, then compute actual C from that same F. Since
C<=U, the FAT has capacity for C+2. No minimum-size solver is needed. A separate
count guard ensures the last cluster C+1 stays below 0x0FFFFFF7. Microsoft FAT
v1.03 printed pages 15, 18 and 19 support these count/limit/overprovision rules:
https://www.cs.fsu.edu/~cop4610t/assignments/project3/spec/fatspec.pdf.

## Files changed

- `tools/OS.ImageBuilder/Program.cs`: conservative one-pass FAT sizing, wider
  round-up intermediate and existing `image_too_large` rejection.
- `tests/OS.HostTests/ImageBuilderTests.cs`: independent image/plan accounting,
  boundary and no-write rejection checks in the actual existing module.
- `docs/image-builder.md`: corrected host behavior and pending integration gate.
- `.gitea/backlog/clros-issues.json`: exactly M04-006's three reviewed scope
  fields and blocked-to-claimed label, following #74's verified closure.
  The canonical ownership sentence, dependencies and open desired state remain.
- This centrally reserved step. No EFI, runtime shim, allocator or kernel
  source is changed for #514; no #75/#513 implementation is included here.

## Tests run

- Author preserved failing-before evidence: 66 starts, 64 passes, two failures,
  including 126976 FAT entries versus 126977 required in the 64 MiB image.
- Corrected actual ImageBuilderTests module: 66 starts, 67 passes including
  terminal `TEST_PASS imagebuilder.issue514.focused`, zero failures, exit 0,
  empty stderr. Focused/tool and host Release builds: zero warnings/errors.
- Tests create 64, 65 and 128 MiB images and independently derive geometry
  from emitted BPB bytes; scan FAT accounting; compare backup BPB/FSInfo and
  both FATs; read exact EFI/manifest/package payloads; compare deterministic
  images. Accepted large boundaries are plan-only, never multi-TiB image writes.
- Last accepted integral-MiB plan: 133153, with 268434367 clusters and last
  cluster 0x0FFFFBC0. First rejected: 133154, whose geometry would exceed the
  limit. Both plan/create reject it and the former huge-size successes using
  exit 5/image_too_large without creating output. Existing argument/minimum/
  overflow cases retain their error behavior. All 20 rejection invocations
  check the exact command-specific marker and absent output file.
- Independent review found the initial large-plan limit omission, then accepted
  the corrected two-file version. The reviewer separately exercised the last
  accepted plan and 12 oversized plan/create failures without building images.
- Parent verified both incoming base blobs against the merged root, copied only
  the two reviewed paths and verified raw hashes. The actual focused module
  was freshly built and run under `artifacts/issue514/focused/`: build zero
  warnings/errors in 7.37 seconds; 66 starts/67 passes/zero failures, exit 0 and
  empty stderr. Stable successful group output matches the author hash.
- After the M04-006 manifest refinement, the actual focused manifest module
  passed all 17 checks plus its terminal, exit 0 and empty stderr.

The complete settled-source host gate and fresh corrected-image QEMU regression
belong to the later combined wave and have not run for this implementation.

## Evidence

Author worktree:
`C:/Users/Roger/.codex/worktrees/image-fat-sizing/CLROS`, base
`5f6108ca363f03c7ebad8dafca7ae78e1d3a0db2`, ignored `artifacts/issue514/`.
Original v1 source, logs and report are preserved and verified by archive hashes.
Parent handoff: `artifacts/orchestration-20261001/issue514-handoff/`.
Parent logs: `issue514-root-build.*`, `issue514-root-test.*`,
`heap-scope-manifest.*` in that orchestration directory.

| Item | SHA-256 |
| --- | --- |
| ImageBuilder source | E36CE2C91CDD5F61AD4308122DE16D45C6393A2D04AD8800F8D7E081A1C88D5C |
| ImageBuilder tests | 5F334EBBD2F430D19A733A24708F3F30DD0A7E782FB865395DDA944F78D3E5CF |
| Final implementation report | 77938B298DB8B9CCC9EB8EE81423C7222B2F1D0E4490801EB77EA74B0D67B953 |
| Independent final review | 2315C044A3BFDD91ED736DEDBA3BAC8EA41080C57A4BD154C104A4E340D248E5 |
| Independent boundary result | BB4B1DBF284DAE8960D18CE9E7B5554D6CFE0F9833BA52005803908130C76828 |
| Author and fresh parent focused stdout | 1229C93CD8D548B952FA45AABF270B82EE9BEA854A665FDFADB252284CA06EA6 |

Next leaf worktrees start at the reviewed merge `b193f590`:
`m04-006-heap/CLROS` and `m02-014-transition/CLROS`. Each author owns only its
new production/test pair. Parent owns central documentation, shared EFI lanes,
Git and tracker. Issue #75 body version 2 preserves the checked #74 dependency
and incorporates the reviewed original-authority/readback/retention contract.

## Failures

- The original FAT recurrence and initial giant-plan acceptance defects are
  preserved as failing/review evidence; neither is accepted as final behavior.
- The independent reviewer's first boundary checker incorrectly assumed CLI
  errors appeared on stdout; its preserved corrected result checks stderr too.
- Parent release preparation initially expected backticks around a declared
  label. Its shape guard stopped before any mutation; the exact existing list
  form was then used. Issue/body/label changes were independently read back.
- Step933's staged check found one trailing blank line. The separate commit
  `155d2e3` removed it before merge; whole-PR whitespace checks passed. No
  evidence or executable content changed.

## Deferred work

Keep #514 open until the combined integration gate, guest regression and
reviewed merge. #75 and #513 implementations are in progress in isolated
worktrees. Their host completion does not prove #75's required retained guest
page or #54's terminal CR3 transition and delivered vector-14 fault.

## Evidence boundary

This is host image geometry/accounting, deterministic output and bounded
payload readback. Large accepted size limits are plan checks only. It does not
prove general filesystem conformance, live storage, runtime allocation, GC,
firmware exit, Kestrel, cloud readiness or a new numbered completion gate.
