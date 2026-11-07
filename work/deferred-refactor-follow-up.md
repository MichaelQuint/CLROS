# Deferred refactor follow-up after PR814

## Status and scope

This draft preserves existing changes excluded from PR814. Its base is verified
master `398ad3b895a0a43be384ff0fbddf917b9511fb7f`. The November 6 execution request
resumes this draft as the [finite HTTP performance phase](http-performance-phase.md),
with current master incorporated in one new producer checkout. The original
packaging supplied no new host/native/guest acceptance.
[Step1790](../done/step1790.md) retains the PR814 release boundary and failures.

The two independent code commits contain:

1. **Shared host fixture decoder.** Extract identical `ElfRange` and
   `DecodeLogUploadFrame` helpers from the descriptor and thread writable-disk
   fixtures into `tests/OS.HostTests/LogUploadFixtureDecoder.cs`. This is the
   archived three-file preparation. Existing scenario assertions and
   route-specific ownership helpers remain. Failing CallerFilePath/LineNumber
   locations naturally move to the shared helper. The archived source review
   found equivalent bodies; compilation and focused validation remain pending.
2. **Adjacent disk descriptor audit reuse.** Preserve `ff96e2bb`'s private token
   check after the existing complete graph audit, its public-entry corruption
   regression, and its matching reviewed-source pin. This changes kernel-linked
   source. The original isolated candidate has host/native and partial integrated
   guest evidence, not complete qualification or measured guest speedup. Those
   results do not automatically qualify this new combined branch.

No earlier PR814 documentation or tracker state is rolled back.

## Archived unfinished snapshots

The November 6 cleanup preserved ten dirty checkouts with 138 changed/untracked
paths, all branches, and ignored/original/failed evidence. Some are earlier
versions of work since integrated; an archived dirty file is not automatically
an outstanding change to apply.

| Archived checkout | Paths | Preserved work |
| --- | ---: | --- |
| copy-clock-evidence-r01 | 7 | Block/FAT read handling and host observation tests. |
| refactor-quality-tail-r01 | 3 | Shared host decoder extraction, packaged here. |
| refactor-quiet-baseline-r01 | 89 | Earlier timer/cache, quiet-serving, source-pin, test and documentation snapshots. |
| refactor-smoke-capture-r01 | 2 | Runner and web-peer host handling. |
| refactor-storage-r01 | 12 | Earlier FAT/cache/native admission and host coverage. |
| socket-epoll-baseline | 10 | Writable-disk/thread integration and evidence/admission preparation. |
| sqlite-contract | 2 | Writable block read port and tests; separate unmerged history also retained. |
| sqlite-stat-contract-r01 | 7 | Block/DMA completion, reset and address-space controls. |
| web-throughput-benchmark-r01 | 1 | EFI benchmark entry preparation. |
| writable-thread-waits | 5 | Writable/SQLite host capture and validation. |

The committed `ff96e2bb` candidate and its `72784936` integration were clean
checkouts and are additional preserved history, not part of those 138 paths.
Other unmerged write/upload/SQLite branches remain recovery material, outside
this PR. No bulk replay of historical snapshots is proposed.

Recovery map, full trees, binary patches, original indexes and the verified
all-ref bundle are under
`C:/Projects/CLROS-archive/20261106-184108-pr814-workspace-cleanup`.
Historical evidence paths continue to resolve through links. Use that archive's
recovery guide before resuming an old checkout.

## Relationship to the PROD/DEV milestone

[Milestone21](http://192.168.4.35:3000/admin/CLROS/milestone/21) requires PR814
first, then project/build split #820, shared startup/diagnostic separation
#821/#815, configurable PROD stats #822 and combined verification #823 under
epic #819. PR814 is merged. None of this deferred refactor or the archived
write/SQLite work is a declared prerequisite for that milestone.

The split can start from verified master after explicit authorization. Keep
this optional runtime optimization separate from the split's acceptance so its
qualification cannot become an accidental blocker. Coordinate overlapping
source/test paths if either change is later adopted. The broader responsibility
review queue and further performance work are separate follow-ups.

## Validation still required before merging this draft

- Build the actual combined HostTests dependency graph and run affected decoder
  and descriptor-audit fixtures once development is resumed.
- Validate assertion behavior, test discovery, source pin and repository rules.
- Review the runtime commit independently; qualify its affected current native
  and guest consumers at a stable boundary if it is retained.
- Measure guest performance before claiming an improvement. The historical
  host timing screen is not a guest speedup.
- Preserve all old failures. CLROS TCG remains incomplete under the unchanged
  1,800-second limit; opening this draft adds no TCG rerun requirement.

This packaging performed provenance, exact-blob, source-equivalence and Git diff
checks only. No build, host test suite, native publication or VM was started.
