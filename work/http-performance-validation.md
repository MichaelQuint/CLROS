# PR824 validation preparation and impact map

Status: preparation, not a frozen candidate or qualification result. The sole
producer and finite scope are in [the phase contract](http-performance-phase.md).
All five runtime slices have focused host evidence. The service packet-control
consumer is implemented and host-tested. Final launch/measurement owners and
their immutable input manifests still need preparation before Full/native/VM.

## Impact and required evidence

| Changed domain | Affected paths/profiles | Required qualification |
| --- | --- | --- |
| Private adjacent disk audit sharing | Linux disk descriptor completion/progress; readonly and borrowed/writable consumers | Public invalid-page-table, foreign-owner, stale-generation and reentrancy controls remain. Original native read/close/reuse/cancellation paths must match the final source. No persistent validation authority. |
| Capacity-aware socket copy | Scalar send/sendmsg through the Linux socket adapter | Full original ranges, including invalid tails, complete vector snapshots, error order, full/zero/partial results and callback failure remain. Exercise real HTTP and affected legacy socket/vector consumers. Copied-byte counts are a host mechanism result. |
| Continuing readonly DMA | Explicit Full/Timing16-sector layout; continuing block/filesystem owners and final ordered-byte consumers | Actual three-page allocation/mappings/domains,8192-byte descriptor, completion lengths, one active request, corruption, fragmentation, partition/EOF edges, cancellation and exact reset/fence/scrub/release. Requests and sectors have separate units. |
| Legacy DMA/layout consumers | Existing one-page block queue, package/disk and borrowed writable paths | Preserve4-sector ceiling and existing writable route. Added layout fields change structure layout even when large mode is off; review every emitted consumer and its backing/offset calculation. Source resemblance is insufficient. |
| Scoped clock observations | TCP receive/ACK/close/retry and continuing block idle polling | HPET remains required. Actual callback/device boundaries retain fresh observations; normal send pump retains independent clock health. Preserve backward/overflow/expiry rejection, original deadlines and completion-before-timeout ordering. |
| TCP retained state and bounded pumping | Every emitted TcpSendWindow/Network owner; legacy M07-026 and socket/epoll/vector/web descendants | Account for current owner/slot sizes and every caller, including legacy mode. Current metadata/ring validation, sequence wrap, pressure and exact cleanup must retain their contracts. Eight new-data frames per pump includes newly sequenced persist octets. |
| Explicit bounded flight | M16-014 continuing service, Full018 and Timing018 | Current actual native layout/stack/ABI/ownership review; guest controls.v3, healthy HTTP/Blazor, natural cleanup, affected cancellation/close/reuse and concurrent service. No historical stack margin transfer. |

Native review must preserve the current emitted tuple before another publication
overwrites shared intermediate outputs. Join source/profile/tool/input hashes,
object, PE/EFI, map, link/serializer evidence, type/field metadata and actual
disassembly. Review helper/caller frames, fixed allocations, indirect providers,
IRQ composition and complete owned call paths. A managed sizeof ceiling alone
does not qualify the1024-byte native slot or any kernel stack.

Full/Timing now reserve one4KiB control page plus8KiB data, matching DMA slot
metadata after the complete range and18 evidence records. Review those actual
allocations, all pointer arithmetic/mappings and cleanup. Keep legacy one-page
callers distinct. TCP segment metadata is embedded in the owner, not an array
allocated on a stack; extra pump passes repeat a bounded turn rather than
authorizing recursion or larger enclosing polling budgets.

## Original guest consumers

Reuse the authenticated compiler/ancestor and disk scenario recipes listed in
[the qualification record](rc3-whpx-qualification.md), selecting required
profiles from actual changed compile links and emitted ownership/call paths.
Its29/24/7 results retain their original source-specific scope; they do not
automatically accept this candidate. Deduplicate identical publications/recipes,
not distinct behavioral coverage. Do not restore old producer registrations or
build in an archived evidence tree.

Required disk/static coverage includes healthy DiskA/DiskB, truncated-file
corruption, exact API/static bodies, browser/Blazor results and natural cleanup.
Include affected M16 disk races and cancellation/close/reuse paths. Larger reads
may change request counts; retain exact bytes, corruption rejection, physical
sector totals and independently interpreted counter units.

The existing M16-014 service runner now requests controls.v3. It preserves
pending handshake resets, unread last-close/reset, two zero-window probes with
one lost reopen and a concurrent healthy response. The healthy connection
advertises32-byte MSS, withholds its first ACK until an exact timeout copy,
requires two segments before advancing ACK, delivers the later segment to
bounded host reassembly first, injects three duplicate ACKs, checks fast
retransmit, then injects partial and stale ACKs and checks the exact16-byte
suffix. The full original PCAP/injection verifier checks ordering, checksums,
literal bodies/FINs and1s/2s minimum RTO intervals, independently of driver
completion and timestamp claims. Existing64-injection/512-packet/45s limits
remain. Its host mock timestamps and responses are expressly synthetic controls.
Actual guest execution and process/source/native joins remain required.

## Matched quiet measurement

Baseline source is accepted runtime
`2e81ee3abd1efbcfe6fa92233cd1449a4729ce70`; candidate is the eventual frozen
combined phase. Run three independent WHPX boots for each on the same host,
one vCPU and256MiB. Use retained identical package/assets and exact2,009,369-byte
CoreLib target. Verify every body/hash and source/tool/native/process join.
Keep first sequences separate from ten warm sequences and report per-boot
medians for full file, API, headers,64KiB range and concurrent file/API. Keep
normal browser/natural cleanup qualification separate from intentional-stop
measurement owners. Report combined improvement and variability, with no
individual causal attribution or sub-second guarantee.

Known evidence routes, for read-only reuse rather than development:

- `C:/Users/AI/.codex/worktrees/refactor-combined-r01/CLROS/artifacts/refactor-r01`
  maps to retained evidence. `compiler-compat-acceptance-r01.json`, SHA256
  `829ABD366646A4E86082585901D25CA667D22453659721979B7974A0BFFF0B4A`,
  records the accepted2e compiler contexts.
- `matched-current2e-whpx-boot01-r01` through boot03 contain historical matched
  bodies/reports. Fresh baseline boots are still required for this comparison.
- `quiet-serving-client-owner-r01/Program.cs` preserves authenticated source,
  native, input, child and joined-client ownership. Its current contract is
  specifically two full-file curl requests; it is not the required final
  first-plus-ten full measurement matrix.
- That quiet owner selects `EnableM10018ServingBenchmarkProbe`, implying Full,
  with Trace/Summary/Timing disabled. Do not silently substitute another native
  profile. Its earlier curl timeout is not authority to exceed this phase's
  existing180-second per-request ceiling.
- `HttpWorkloadBenchmark.RunAsync` already provides the strict first-plus-ten
  API/health/HEAD/range/full/concurrent body consumers and180-second ceiling.
  Final preparation must connect the existing quiet-client/owner workflow to
  the complete required sample set, preserving exact request order, joined
  clients, original bodies, quiet profile and unchanged outer budgets. No such
  final adapter/manifest is claimed ready here.

Reuse the existing Alpine results as historical context unless current matching
is demonstrated. Do not reopen TCG qualification or extend its1800-second limit.
If combined results fail to improve beyond variability, preserve originals and
make one bounded expert consultation; do not start another optimization phase.

## Freeze and execution order

1. Complete the remaining launch/quiet-client adapter, consumer controls and
   authenticated immutable manifests. Record the exact scoped profile list,
   baseline/candidate authorities and executable command vectors. Run focused
   checks and repository/contracts checks; commit source and preparation.
2. Freeze one clean candidate. Build the complete HostTests graph, capture exact
   producer/dependency/input revisions/hashes and run one unfiltered Full with
   zero reuse and before/after guards. No documentation/source/tool mutation
   during its ownership window.
3. Publish the required native profiles serially and review actual tuples,
   layouts, stack and ownership before guest activation.
4. Execute affected original guest/browser/control profiles and the six matched
   quiet boots, retaining wrappers, runner, guest outcomes and every failure.
   A failed gate requires a concrete targeted correction before invalidated
   gates rerun; never perform an unchanged retry loop.
5. Reserve the final evidence step, reconcile canonical docs/README and scoped
   tracker/PR824 dependencies, push/review/merge qualified work, read back
   canonical master parity, preserve evidence/unfinished work and stop.

Neither this plan nor a successful host selection grants Full, native, guest,
browser, performance, Gate14, production or broader issue acceptance.
