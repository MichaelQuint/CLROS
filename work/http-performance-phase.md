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
