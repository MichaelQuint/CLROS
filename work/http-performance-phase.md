# PR824 finite HTTP performance phase

## Authority and source

The November 6, 2026 execution request resumes PR824 for five changes only:
the preserved refactor, socket-copy reduction (#818), larger bounded readonly
DMA transfers (#817), scoped HPET sampling (#816), and congestion-controlled
TCP flight (#751/M07-031). PROD/DEV milestone21 remains separate.

Sole implementation and serialized build/native/guest owner:
chat `01a1b3bb-8244-7310-b3ac-bd3ceea56d6d`, producer checkout
`C:/Users/AI/.codex/worktrees/clros-performance-r01/CLROS`, branch
`codex/deferred-refactor-followup-r01`. No subagent or standing helper.

Startup readback authenticated canonical LAN repository9, open draft PR824 at
`f94cd8d1d870f43e73ed5e3263ce01f7351b20ad`, and clean master
`bc103d2f1d91fda4104f4912fb7c4611365101f2`. The producer starts from PR824 and
merges master, retaining the Visual Studio tooling and README changes. Original
startup readbacks are retained in `artifacts/performance-r01`. The primary
checkout remains on master. No old producer worktree or archived history is
restored. Historical path links provide evidence access only.

The explicit request incorporates the shared network integration lane for this
finite phase. #751 retains dependencies M15-016/#750 and M07-030/#744; the
request permits implementation preparation, not closure of these prerequisites
or broader Gate14, baseline, production or milestone scopes. Preserve canonical
issue content versions and use the compiled backlog tool for managed updates.

## Implementation boundary

1. Preserve the shared log/upload decoder. Reuse the disk descriptor audit only
   in the existing private adjacent path, before any callback, with unchanged
   owner, generation and relevant state. Public boundaries retain full audits.
2. Validate the original send ranges and complete snapshotted sendmsg vectors,
   then copy only the prefix allowed by descriptor scratch and send capacity.
   Retain flag/error order, zero/full/partial behavior and callback failures.
3. Keep the legacy one-page DMA layout. Continuing readonly reads use one control
   page plus an 8KiB data area, at most16 sectors per physical request, with
   complete owned allocation/mapping/descriptor/cleanup checks. Retain one active
   physical request, fragmentation/partition/EOF checks and fresh device reads.
4. Retain HPET. Share observations only across adjacent internal checks without
   callbacks/device/ownership transitions; skip timer-only empty work, preserve
   independent lifetime checks and sample afresh after accepted transmission.
5. Add an owned eight-segment ring within the retained32KiB send-buffer budget.
   Bound flight by peer window, cwnd, bytes and ring capacity; implement RFC5681
   congestion control and RFC6298 retransmission/Karn behavior. Preserve legacy
   single flight; explicitly enable bounded flight in the candidate serving
   profile. Round-robin new data is limited to eight frames per network pump,
   retaining retry, receive ACK, close and enclosing polling/time budgets.

No content cache, speculative readahead, asynchronous disk pipeline, TSC
replacement, SMP, scheduler rewrite, ThreadPool increase or native source import.
Record exact reference provenance/hashes before any selective adaptation;
ProtonOS is concept-only, with independently authored implementation/tests.

## Stable qualification boundary

During implementation build affected managed projects and run focused fixtures
with fresh results, unchanged-input guards and zero reuse. Prepare final harness,
expected-source changes, changed-contract docs and the native/guest impact map
alongside code. Keep the five areas in coherent commits. Do not run unfiltered
Full, native publication/qualification, QEMU or guest workloads until all five
implementations, focused tests and final recipes are ready and frozen.

Then run one unfiltered full-host original, required native publications and
actual emitted layout/stack/ownership reviews, followed by affected guest
profiles: healthy HTTP/Blazor, truncated-file corruption, cancellation/close/
reuse, delayed ACK/loss/duplicate/reorder/partial ACK/zero-window/multiconnection
controls. Bind exact package/source/EFI/serial/wire/body/browser and cleanup
identities; distinguish wrapper, runner and guest outcomes. Historical stack
approval does not cover changed DMA/TCP state. Deduplicate identical recipes,
not coverage. A failed gate needs one concrete correction or diagnostic
hypothesis before rerunning invalidated gates; preserve failed originals.

Use the existing quiet HTTP client and retained application/file inputs for
baseline and candidate: same host, WHPX, one vCPU,256MiB, three independent boots
and ten warm sequences per boot, with first sequences separate. Record exact
body hashes, per-boot medians and full-file, API, headers,64KiB range and
concurrent file/API timings. Report the combined change, variability and
regressions. Existing Alpine measurements are historical unless configuration
is demonstrably matched. No sub-second guarantee or individual attribution.
TCG remains incomplete; do not reopen it or exceed the30-minute limit.

## Delivery and stop

After qualification reserve one final evidence step, reconcile affected canonical
docs and README's collapsed progress/performance section, issue dependencies and
PR824's combined description. Post evidence before scoped reviewed closure.
Commit/push/review/merge only qualified code, verify intended commits/docs on
canonical master and remote parity, preserve originals/unfinished work before
recoverable cleanup, stop development and begin no next phase.

For a concrete bounded blocker only, consult expert chat
`01a16aa9-c50d-7c90-8639-c34a81399caf` with exact source/function, expected versus
observed result, minimal evidence/reproduction, ruled-out hypotheses and one
question. If work must continue later, retain a clean commit and exact next
command; incomplete work and focused mechanisms are not release acceptance.

## Implementation checkpoint

The preserved refactor's affected graph builds with zero warnings/errors.
Fresh focused Fast originals pass97 checks: disk descriptor21, writable disk
descriptor47 and writable disk thread29, all with zero reuse. This finishes
managed integration only; original native/guest approval does not transfer.

Socket-copy preparation builds with zero warnings/errors and passes50 fresh
focused checks: socket23 (including four new copied-byte/fault controls), large
message7 and TCP send-window20, zero reuse. The8192-byte scalar/fragmented offers
copy1460 payload bytes with1460 free, preserving the original invalid-tail
checks, full/zero/error policy and failure atomicity. Two first-run controls
failed at socket admission because their newly constructed test transports
omitted required close configuration; reusing the configured helper fixes that
fixture setup. The original failed r01 and successful r02 remain under
`artifacts/performance-r01/socket-copy-*`. No guest speedup is claimed.

The readonly DMA candidate now has separate legacy and three-page layouts,
explicit Full/Timing16-sector selection, complete allocation/domain/ownership
checks, matching arena/slot/evidence storage and strict ordered16-sector byte
consumers. The final counter consumer counts requests and sectors separately.
Affected graph builds have zero warnings/errors. Focused selections pass:
block30, filesystem53, evidence23, legacy queue5, disk descriptor21, writable
descriptor47 and profile/source-admission5 checks, zero reuse. Original per-run
receipts and the14-entry reviewed-source update map are retained under
`artifacts/performance-r01/dma-*`. The final23-check consumer original follows
the earlier22-check run and the added16-sector final-counter control; they are
distinct executions/producers, not one full-host result. No actual DMA hardware,
emitted frame/stack or guest performance acceptance follows.

Scoped clock sampling now builds with zero warnings/errors. Focused selections
pass: TCP send23, retransmission16, termination18, socket network43, socket23,
block31 and owned clock6 checks, zero reuse, retaining each actual producer.
The first network selection failed its unchanged backwards-clock control when
the initial candidate omitted every idle TCP sample. The bounded correction
retains the normal Pump's independent clock-health observation and omits only
redundant idle retry/close samples (three calls become one). Receive/ACK parsing
shares one observation, FIN retry shares its initial observation, and accepted
device copies still sample afresh for RTO. Block idle polls retain status/index
checks and defer clock failure/regression to the next actual request; the old
idle-failure test now checks that precise contract before unchanged fail/cleanup
assertions. Original failed network r01 and successful r02 remain in
`artifacts/performance-r01/clock-*`. HPET validation/source is unchanged.

Bounded flight now retains eight segment lengths in the TCP owner, preserving
the existing payload buffers. Explicit Full/Timing selection enables the mode;
legacy callers remain single-flight. New-data service is bounded to eight
accepted frames per network pump, including new persist octets, with
round-robin extra passes after every connection's retry/ACK/close turn. Host
controls cover congestion avoidance and ACK division, timeout/backoff, exact
suffix retransmission, three-duplicate fast recovery, backpressure, cumulative/
partial/stale/future ACKs, wrap, zero windows, FIN/abort/scrub, ring compaction,
idle restart at clock zero, and clean RTT sampling after the retransmission
horizon during a continuous stream. Native rings use embedded metadata, not
stack or separately allocated arrays.

The affected graph builds with zero warnings/errors. The final focused r03
batch passes151 checks with zero reuse: bounded TCP13, network46, legacy send23,
retransmission16, termination18, sockets23, large-message7 and source admission5.
The earlier r01 network and r02 socket failures identified respectively the
exact project-shape pin and the profile-specific compile-inventory selector
missing the newly reviewed Flight part. Those admission checks were preserved
and the exact inventories/pins updated; no runtime assertion was relaxed.
`flight-project-shape-r02.json` proves that removing only the new Compile line
reproduces both prior project hashes. The earlier DOM-removal diagnostic is
retained but is not the pin authority. All originals remain under
`artifacts/performance-r01/flight-*`.

The service control integration additionally selects bounded flight in
M16-014. Its v3 driver and independent full-PCAP consumer reuse the original
three reserved tuples,45-second stimulus budget,512 observed packets and64
injections. Legacy v2 host fixtures remain available; current production
inspection requires v3. The healthy32-byte-MSS flow tests initial loss/delayed
ACK, two unacknowledged segments, raw-peer reassembly reordering, three duplicate
ACKs, fast retransmit, a16-byte partial ACK, stale ACK and exact suffix RTO.
Pending resets, unread last-close, lost zero-window reopen and concurrent
healthy/blocked lifetimes retain their original controls.

The graph builds with zero warnings/errors. Successful r02 selections pass63
fresh focused checks: driver6, independent controls4, unread owner2, source
admission5 and network46. After adding the coupled-driver/PCAP early-suffix
negative, r03 passes23 checks: driver7, runner10, HTTP packet2 and lifecycle
packet4. Every selection has zero reuse. The initial r01 negative test caught
IOException, while the correctly rejected corrupt packet throws the existing
InvalidDataException. It now requires the exact intended rejection message;
the failed original is retained. Mock I/O and synthetic capture timestamps are
explicitly host-only. These are distinct executions, not an aggregated Full.
Repository/style and contracts checks pass after a fresh tooling build. The
first style original rejected an inline async-lambda block; the correction only
puts its braces/statements on separate lines. Behavioral focused originals
retain their pre-format producer identities; the final Full must rebuild all.

The [validation impact map](http-performance-validation.md) records required
coverage and unresolved owner/quiet-client preparation. All five runtime
changes and the packet-control integration have focused evidence; final
validation recipes and authenticated launch manifests are not yet complete.
Next: finish binding those consumers/recipes to one source and exact retained
inputs, then freeze. No readiness flag or acceptance has been issued.
Do not freeze or begin expensive qualification until those preparations pass.
No Full, native publication/qualification, guest or
benchmark has run. Final native review must account for the larger Full/Timing
DMA arena and18-record evidence backing, plus every legacy layout consumer
affected by the added layout fields. Retain the existing guest limits.

## Resume command and remaining preparation

All implementation and packet-control work is committed through7776210c.
The next managed command, in this producer only, refreshes the complete graph
after the final formatting-only tooling rebuild before further focused tests:

```powershell
dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1 -p:UseSharedCompilation=false
```

Then finish the quiet matrix owner and authenticated baseline/candidate launch
manifests. The exact existing integration point is the final
`observeQuietServing` argument of `QemuSmoke.RunAsync`; its selected-profile
branch is in `tools/OS.Tooling/QemuSmoke.cs`. It retains native/source/image
guards and requires a cancelled, joined runner. The retained two-curl owner
and strict eight-case first-plus-ten client routes are recorded in the impact
map; do not rediscover startup/PR ownership or restore an archived checkout.
This initial resume command has now passed. The quiet owner and retained2e
baseline adapter are implemented; details and artifact identities are in the
impact map. The quiet client passes17 focused checks, including88 exact
responses, partial-body cancellation and failure-bearing joined-runner rejection.
Retained Serving passes4 selector/mapping/binding controls, legacy retained
Timing passes2, and EFI source inventory passes2; each is a distinct focused
original with zero reuse. Graph builds have zero warnings/errors. The initial
quiet CLI guard test incorrectly expected a thrown error rather than the
existing returned failure/output; the corrected assertion preserves the guard
and the failed original. The first baseline preparation correctly refused an
archive junction; an explicit physical-artifacts read mapping fixes that route
without changing historical records or relaxing plain-path admission.

Recipe preparation r04 passes all22 managed dry-runs and13 source queries,
with explicit baseline/candidate boot commands and copied retained inputs. No
native publication or VM occurred. Final scoped checks, commit and clean-tree
recipe regeneration are the next freeze prerequisites.
The shared writable layout is included for native review without enabling
larger writable transfers or claiming new writable workload acceptance.

Final source freeze, unfiltered Full, actual native review, guest/browser/
fault controls, matched six-boot measurement, final evidence/docs/tracker and
qualified PR824 delivery/master verification remain outstanding. The durable
goal remains active; no permission, expert consultation or next phase is pending.

## November7 qualification and bounded consumer repair

Runtime candidate `cdab7536d825b82570b465d302be86cf02a08681` completed
one fresh unfiltered Full:7833 checks,999 groups, zero reuse, unchanged
inputs. Thirteen actual native publications and current emitted stack,
layout and ownership reviews are retained in
`artifacts/performance-r01/native-admissions-r01`; eleven admit their
prepared originals. Ordinary and writable-layout reviews grant no new
functional guest acceptance.

The first DiskA1 original completed HTTP, browser observations and natural
guest teardown, but its original consumer failed
`disk_static_measured_layout`: the stale Full expectation was91 pages;
the separately compiled layout and live allocation both use93 pages after
the three-page continuing-read DMA change. Legacy remains90. Correct only
that exact consumer expectation and reject90/91/92/94 for Full. Preserve
the failed original, its producer and all inputs. The repaired consumer's
focused fixture passed8 checks with zero reuse. A separate copied-value
diagnostic replay passed protocol, device bytes/counters, parent/lifetime,
full HTTP/packet and49 browser exchange joins; it cannot rescue the failed
original or grant fresh guest acceptance.

The preparation script now uses admitted flat EFI paths and retains mandatory
real EFI builds. An execution owner must compare the freshly rebuilt native
tuple and source closure to the exact reviewed publication before image
creation or VM activation. Earlier invalid recipes and failed attempts remain
preserved. Freeze the repaired tooling/tests/recipes, rerun the invalidated
Full gate, verify current native identities/reviews and rerun the affected
original with fresh outputs. The remaining guest matrix, matched six quiet
boots and release reconciliation remain outstanding. No timer, correctness
guard, resource bound or guest limit changed.

The repaired `45567777` candidate passed a fresh unfiltered Full:7833 checks,
999 groups, zero reuse, unchanged inputs. Its thirteen fresh native publications
match all six reviewed binary components per profile. The wide4006-entry source
receipt changes only the exact host disk consumer;4005 entries remain unchanged.
All174 referenced native proof files were rehashed before renewing eleven finite
original admissions. Ordinary/writable reviews still grant no new functional
guest acceptance.

Fresh DiskA1 then passed169 HTTP/browser responses, Blazor boot, natural
teardown and endpoint refusal. Socket sync, socket async, epoll stolen-readiness,
vectors and web smoke also passed at that source. A later preparation stopped
before VM launch because the mandatory real rebuild regenerated only
`generatedUtc` in a canonical EFI receipt. Preserve that failed preparation and
the successful originals. The execution helper now restores authenticated
sidecars, preserves prior receipts, and records all561 post-original native
leaves plus every changed byte. The targeted epoll original passed with only
the timestamped receipt changed. No source or correctness check was weakened.

Service-v1 completed its guest/runner cleanup but its original consumer failed
`service_packet_nic_join`: the kernel reported24 ignored packets; the aggregate
verifier counted23. The complete v3 packet proof had already validated the
deliberately stale ACK. Kernel `Tx.OldAck` correctly returns network `Ignored`,
but the shared host accounting omitted that exact control record. Carry the
original one-based ordinal from the complete injection/flight proof; independently
require a bare older ACK on the same tuple with no receive-sequence progress.
Keep all aggregate equalities and legacy/default zero-record behavior.

The bounded host-only repair passes25 focused checks with zero reuse. A separate
copied-value replay identifies original record139 and joins263 received,
239 accepted and24 ignored, including the existing compact-reuse bound. This
does not rescue the failed original. Freeze/rebuild the repaired host graph and
rerun the invalidated unfiltered Full, renew actual native identity joins, and
run a fresh affected service original before continuing. Retained successful
originals keep their exact `45567777` source/producers; do not call them new-source
guest runs. Runtime source and emitted-code identity must remain explicit.

The `4de9abe1` host repair passed7834 checks across999 groups in a fresh
unfiltered Full, with zero reuse and unchanged inputs. Independent readback
rehashes3271 compiler inputs and47 dependency records. All thirteen current
native publications preserve the six reviewed binary components per profile;
their4006-entry wide receipt changes only the four exact host consumer files.
A review-reader attempt incorrectly read per-profile component expectations
from shared intermediates overwritten by quiet. Its original failure remains;
the corrected reader hashes each exact authenticated profile archive member,
retaining202 immutable proof bindings,78 component checks and all prior
conditions. Full and actual publications were not invalidated by that
artifact-reader correction.

The next fresh service-v1 original joined its runner and cleaned up, but failed
`service_flight_timeout`. Its32-byte retry is exact and arrives1,122,717
microseconds after the first copy. The observer counted the initial SYN's
undefined ACK field of zero as an advancing ACK when the randomized server
sequence is in the high half of the sequence space. Require the ACK flag before
interpreting that field. Keep the exact one-second/two-second wire intervals,
all sequence/payload/order checks and the45-second control bound unchanged.

The bounded host-only correction builds without warnings/errors and passes27
focused checks with zero reuse: socket controls9, NIC4, legacy controls4 and
runner controls10. New controls cover high initial server sequences and early
first retransmission rejection; the existing early-suffix rejection remains.
Repository/style and contract checks pass. A copied-value diagnostic of the
failed original then passes the complete v3 packet and NIC joins: record117,
262 received,239 accepted,23 ignored and240 TCP copies. This does not rescue the
failed original. Freeze the corrected host source, rerun invalidated Full and
current native identity gates, then run a fresh service original and the
remaining matrix. Runtime implementation remains unchanged since`cdab7536`;
both failed service originals and every prior producer remain preserved.

Full on`f324725f` stopped after887 Fast groups: the unchanged unwind
fixture observed7936 allocated bytes with64 expected successes. The Native
lane correctly stayed blocked. Inputs remained unchanged, reuse was zero,
all children joined, and the failed producer's17 unique files are preserved.
The failed result is not a complete Full or a new kernel-allocation finding.

One bounded expert consultation recommended separating empty, successful,
rejected, alternating and retained-allocation windows before changing the
measurement. Two fresh single-case diagnostics passed with zero allocation,
with default tiering and tiering disabled; that does not attribute the failure.
An artifact-only packet then used the same fixture and immutable binaries,
512 fixed observations per kind/phase,64 calls per operation window,65 observed
background collections and a maintained bounded no-GC region. All operation
and empty windows reported zero; every retained37-byte array reported64 bytes.
The first unwarmed zero-stack rejection also reported zero with the expected
status. The original7936-byte event remains unexplained. No successful
diagnostic rescues it or proves a general native/no-GC property.

The host fixture now follows the existing filesystem allocation measurement
pattern: take its backing snapshot, require a scoped1MiB no-GC region, keep
the original64 success/rejection pairs and exact zero-byte assertion, and end
the region in`finally`. A retained positive control inside the region must
still report64 bytes. Admission failure or premature region exit fails the
test. There is no tolerance, subtraction, extra rejection warmup, global GC
configuration or toolchain change. Six fresh focused unwind checks and
repository/style/contracts checks pass; the graph builds without warnings or
errors. Freeze this host-test-only correction before another Full. The first
failure and its causal uncertainty remain part of the final evidence.

## Bounded legacy network corpus correction after current originals

Frozen3bdc Full5 passes7,836 checks with zero reuse. Its thirteen native
publications and reviewed component joins precede twelve successful current
originals: service-v1,quota256,quota512,positive-v2,DiskA2,DiskB,TruncatedA,
Timing,Progress,CloseReuse,ExitPending and LegacyBlock. Timing has five responses
and64KiB; its intentional stop does not qualify full/browser/natural cleanup.
Healthy/truncated Full018 originals each pass120 HTTP and49 browser responses
and their complete original cleanup consumers. Six earlier unaffected originals
retain their actual455 source and producer identities, never new-source status.

The next Network original fails. The first mismatch is corpus index39 / actual
PCAP record108: after the peer advertised zero window, the kernel sends the
retained octet`A` at SND.UNA1001 rather than the corpus's four-byte`ABCD`.
Record104's original four bytes and record108's one byte have the same sequence
and ACK10017, with a3,000,101us interval. Existing PumpPersist and focused legacy
tests specify one retained octet and no ordinary retry-budget consumption. Source
inspection confirms that policy also existed at accepted2e; this is not evidence
that an earlier Network original passed or failed. The kernel is unchanged.

The peer stopped after39 accepted corpus packets. Its InvalidDataException is
appended to inherited stderr, so the strict outer`whpx_raw_after_seal` check also
fails. The96-byte QEMU interrupt-vector warning exactly matches its sealed
prelaunch prefix; that warning is not the new post-seal content. The original
process joins with exit19, no timeout/output-limit failure, and the wrapper joins
all children with unchanged source/producers. The original remains FAILED.

The bounded host correction changes only that expected payload to`A`. A new
independent control pins literal original frames104/108, accepts the original
four-byte send and one-byte probe separately, and rejects a full-segment probe
or a valid-checksum wrong octet. Packet counts, sequence/ACK/window checks,
three-second interval, four-minute TIME-WAIT,285-second runner bound and strict
stderr/cleanup checks remain. Preserve`network-persist-pre-repair-r01`, the raw
original, packet readback and all current Full/native/guest receipts. Focused
checks and a new stable host qualification are required; no quiet measurements,
tracker writes, publication or delivery have happened yet.

The affected graph builds with zero warnings/errors. Fresh focused originals
pass43 checks with unchanged inputs and zero reuse: continuing transcript4,
legacy send/persist23 and retransmission16. Repository, source style, contracts
and diff checks pass. The new literal capture control also rejects a valid-
checksum wrong octet. Commit/freeze this three-file host-only correction before
the renewed full-host/native identity and affected-original gates. Retain all
twelve passed3bdc originals with their exact source and producer identities;
only the failed Network original needs a fresh functional replacement.

## Bounded quiet startup correction

Frozen80eff Full6 passes7,837 checks with zero reuse. Thirteen fresh native
publications retain the same runtime component bytes and their reviewed actual
layout/stack proofs. The corrected Network original passes with80 TCP packets,
including its unchanged four-minute TIME-WAIT and joined cleanup. The first
baseline quiet boot then fails before any HTTP request: the owner waits for
`M10_018_LISTENING`, but quiet serving deliberately suppresses that trace.
The original serial contains `CLROS_HTTP_BENCH_READY` followed by Kestrel's
`Now listening on: http://0.0.0.0:8080`. The240-second startup deadline expires;
the runner joins, with no matrix report or samples. This is a host readiness
contract defect, not a measured guest transfer timeout. Preserve the failed
`quiet-baseline-01-r05` original and `quiet-readiness-pre-repair-r01` copies.

The bounded host correction requires the exact quiet marker and a subsequent
listener message, rejects guest failure markers and forbidden traces after
readiness, and repeats that check on the stopped original. Full/browser mode
keeps its existing predicate. Quiet host mocks now emit the real quiet contract;
focused controls cover missing, partial, out-of-order and false-prefix markers,
wrong ports, failure markers and forbidden tracing. No kernel code, timeout,
WHPX configuration or performance acceptance rule changes. Earlier Full/native
and guest results keep their actual sources. Renew the changed host qualification
and affected quiet originals before claiming any matched comparison.

Fresh focused originals pass22 checks with zero reuse and unchanged inputs:
HTTP benchmark18, including the complete88-response quiet loopback observation,
and retained-serving4. The affected graph builds with zero warnings/errors;
repository, source style, contracts and diff checks pass. The final artifact
reader also follows the existing `requests/benchmark.json` and body-file
location while keeping `owner.json` at the owner root; prior readers remain
preserved. Freeze this five-file host-only repair, renew the complete host and
native identity gates, retain nineteen unaffected functional/Timing originals
at their recorded sources, and run three new baseline/candidate quiet boot pairs.
No failed original is reused or relabelled as a successful comparison.
