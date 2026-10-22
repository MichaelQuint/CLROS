# .NET 11 Minimal API and Kestrel Ledger

[Step1547](../done/step1547.md) shares one successful post-refresh descriptor
audit across consecutive not-ready retained epoll operations in the same
exclusive IRQ-off scan. Every operation identity is still checked; the scan
audits a changed owner and is discarded before thread inspection or wake.
The host build and69 focused checks pass, including null-owner and damaged
later-operation controls that preserve the original partial-wake behavior.
This source has no new native review, guest result or timing measurement yet.

The preceding pipe transaction at `8b2d6a38` has its original scoped native
review:962 source joins and12 bodies totalling4,117 bytes. Its fresh guest still
failed `web_signalrace_missed_recovery_gate`: tgkill preceded preemption, but
the waiter returned readiness before competing consumption. Both the failed
original and its native inputs are preserved. The new scan keeps the required
race order, timer and one-attempt policy; #225/#748/#737 and PR790 remain pending.

[Step1546](../done/step1546.md) combines pipe selection and descriptor dispatch
under one complete synchronous admission, removing two further duplicate audits.
All67 relevant focused checks pass, including strengthened controls that expose
premature copying, readiness changes and descriptor duplication. The same host
comparison is400.2766ms per2,000 pairs, about48.5% below the original baseline.
The first optimization's fresh guest still failed: tgkill preceded preemption,
but the waiter completed readiness before the actor read. That original and its
native review are preserved. Step1547 records the pipe transaction's completed
scoped native review and failed fresh guest. It does not complete #225/#737 or
qualify merge.

[Step1544](../done/step1544.md) consolidates three exact duplicate descriptor/
epoll audits within serialized calls. Every distinct admission predicate and
post-mutation audit remains. The unchanged application, timer, signal ordering
and one-attempt policy are retained. All62 focused host checks pass; a host-only
pipe write/read comparison improves from777.569ms to579.8792ms per2,000 pairs.
That measurement does not qualify the outstanding guest race. Fresh native
review, original guest execution and the stable acceptance batch remain required.
The three reviewed source identities are admitted in
[step1545](../done/step1545.md); the initial publication rejection is retained.

[Step1543](../done/step1543.md) records a complete original configured V2 service
pass at `3b325f4e`: 923,022ms actual QEMU lifetime, 55 accepted connections,
52 retired transport lifetimes and checked process/descriptor/mapping cleanup.
The original owned consumer passes after the host capture and persist-reader
repairs in steps1539/1540. Earlier failed captures remain failed.

The separately republished event-owned Smoke still fails its required scheduling
witness. Its explicit preparation yield executes, but a later timer preemption
lets the waiter return readiness before the actor's directed signal and read.
The four original failures remain preserved; no unchanged retry is scheduled.
The remaining #225 witness must retain readiness wake, directed signal,
competing consumption, repark, EINTR and recovery in that order during real churn.
Signaling after repark would test a different race. Preserve the original
deadlines, one-attempt admission and checked teardown. A reviewed fixture fix,
successful owned Smoke, affected ancestors and the stable unfiltered full-host
run still precede #748/#225/#737 closure and PR790 merge.

## Request churn development

[Step1497](../done/step1497.md) adds explicit diagnostics to the real
MinimalApiSmoke app: POST `/__clros/churn` and GET `/__clros/churn/status`.
An empty POST must have Content-Length0, exactly `X-CLROS-Churn: v1`, no query
and no Transfer-Encoding. Invalid input returns400; a third concurrent request
returns429. Each admitted call retains one64KiB patterned buffer across eight
finite cancellable timer awaits and actual awaited ThreadPool/full-GC/data checks.
Success is `CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8`.
Request cancellation propagates after the current awaited operation drains.
Status counters describe completed operations and retained roots; collection
observations can overlap and do not count unique global collections or backing.

Four focused host checks pass. An actual managed-host Kestrel run observes three
disconnect cancellations, three completions, seven malformed400 responses and ten
exact healthy responses. Final active/waiting/held state is zero. The owned host
process is stopped through its bounded owner with termination confirmed; this is
not graceful app-exit or CLROS guest evidence. The unchanged `/healthz` body is
`OS_MINIMAL_API_OK` under default configuration.

For local development after building, run `dotnet run --project
apps/MinimalApiSmoke --no-build -- --urls http://127.0.0.1:5081`, then send
`curl.exe --header "X-CLROS-Churn: v1" --data "" http://127.0.0.1:5081/__clros/churn`.
Focused checks use `dotnet tests/OS.HostTests/bin/Debug/net11.0/OS.HostTests.dll
--lane full --filter KestrelRequestChurnTests` and emit `host.tests.focused`.

The old prepared web ELF/source/index pins remain unchanged and must reject
this unpublished source. [Step1498](../done/step1498.md) implements the qualifier
extension: existing Linux `nativeaot web qualify --output <fresh-directory>` runs
the bounded churn in both owned app sessions and writes `web-churn.json` plus
`web-churn-default.json`. Receipt `clros.nativeaot.web.v4` hashes both reports and
revalidates their exact corresponding HTTP endpoint/body. Earlier receipts stay
historical evidence and cannot supply current churn qualification.

Each report selects42 ordered observations: initial zero state, seven malformed
requests, three pending/cancel/drain and pending/complete/health/drain cycles, then
final zero ownership. Strict bounded copied-value checks require exact schema,
fields, bodies, canonical status counters and progress; they grant no process or
file authority. All request tasks are canceled/joined on failure; partial reports
remain rejected and retained. Six focused checks pass across two original
producers. The production capture passes on managed Kestrel in both configurations;
an injected health transport fault during a real pending request retains nine
rejected observations and leaves the request drained and health responsive.

Genuine Linux NativeAOT publication and its exact new ELF/index admission remain
pending until the next app slice is stable; preserve old accepted evidence. #225 still requires
directed signal delivery with readiness consumed by a competing thread during
real churn, physical/metadata fragmentation, effective component budgets and
offending-workload cleanup. The mechanical signal race alone cannot supply that
contract. #748/#225 and broader Kestrel/Gate14 remain open.

## Signal ownership prerequisite

[Step1499](../done/step1499.md) adds a one-shot explicit POST
`/__clros/signalprobe`, requiring Content-Length0, exactly `X-CLROS-Signal: v1`
and no query/transfer encoding. Malformed requests return400; unsupported hosts
return501 before libc entry. Linux x64 reserves SIGUSR2 only if its actual
disposition is default. Repeated/concurrent attempts return409. It creates one
dedicated waiter and three descriptors; it never targets Kestrel or GC workers.

The source pin is dotnet/dotnet `820230c386db5b0106c5430b3e5aee92a7826bf5`,
which maps runtime to `474ed963699fc29809015392738a15231aaae2de` in its
[source manifest](https://github.com/dotnet/dotnet/blob/820230c386db5b0106c5430b3e5aee92a7826bf5/src/source-manifest.json).
The [native registration](https://github.com/dotnet/runtime/blob/474ed963699fc29809015392738a15231aaae2de/src/native/libs/System.Native/pal_signal.c)
retains SA_SIGINFO/SA_RESTART; no shared flags are cleared. Its native handler
writes a signal byte to the global pipe, and the
[managed implementation](https://github.com/dotnet/runtime/blob/474ed963699fc29809015392738a15231aaae2de/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/PosixSignalRegistration.Unix.cs)
queues the USR callback separately. The callback sets Cancel first; it cannot
prove target Tid or native wait interruption. The probe copies those observations
separately and still requires original syscall/signal/handler/restorer evidence.

Unregister only after native return and callback acknowledgment; reject action
drift and verify the exact saved disposition, including libc restorer
normalization. Restore the waiter's mask, verify its alternate stack unchanged,
join it before descriptor release. The callback owns managed counters only;
unregistration does not claim to join queued framework callbacks or stop the
runtime-owned signal thread/pipe. Unsafe uncertainty exits the owned diagnostic
process70; no success is inferred. A positive body begins `CLROS_SIGNAL_PROBE_OK`
and contains copied values, not guest authority.

Four focused ABI/layout/admission/platform controls and actual Windows HTTP
400/501/health checks pass. No signal executed in those controls. Actual Linux
NativeAOT ownership/interruption/restoration remains the next experiment; only
then add the competing pipe reader during real churn. Preserve #225's full race,
fragmentation, budget and cleanup requirements and the separate mechanical scope.

[Step1500](../done/step1500.md) records one original Linux NativeAOT ownership
capture at source `9dd1e01a`. The canonical payload qualifier passes; the separate
single probe POST returns 200 with exact health before/after. Original traces
bind sender/callback Tid 2907, waiter 2911, directed SIGUSR2, entered epoll,
EINTR, native handler pipe delivery, sigreturn and exact action/mask restoration.
The alternate stack is disabled before/after. The sender and callback legitimately
share a ThreadPool Tid; neither is the waiter. All three borrowed descriptors
close after managed Join. Native pthread tail cleanup and exit occur afterward;
managed Join is not evidence of prior native exit/reap. The owned app then exits
0 and the controller confirms process-group death and joined logs.

Retain 131 originals, 908 source joins, 1,042 unchanged native/compiler inputs,
the exact ELF/debug CRC association and reviewed RX handler/restorer byte windows.
The handler window is not a claimed whole symbol/call-chain/stack extent. This
capture contains no churn or competing readiness. It adds no web-v4 churn report,
prepared-image admission, CLROS guest race or issue acceptance. The old web image
guards remain unchanged. Next add the dedicated competing reader and recovery
phase during genuinely pending churn, preserving all #225/#748 criteria.

[Step1501](../done/step1501.md) implements that explicit actor/recovery path in
the same one-shot owner. POST `/__clros/signalrace` shares admission with the
ownership probe. One dedicated actor writes byte0x5a, directs SIGUSR2 to the
actual waiter and consumes exactly that byte through a nonblocking read. After
managed actor Join and the original-result/callback checks, the waiter borrows
the read end for a fresh epoll wait. GET `/__clros/signalrace/status` exposes
copied phase/identity data. One empty versioned POST to
`/__clros/signalrace/recover` requests a fresh byte0x6b; the request owner writes
it, and the waiter must return the exact ready tag and read that byte. Recovery
admission is atomic and refuses premature, duplicate or terminal requests.

Cancellation joins the actor before inspecting its sent flag, then acknowledges
any successful signal before unregister. It releases/wakes/joins the waiter
before closing the three descriptors. Unsafe cleanup uncertainty still exits
the owned diagnostic process70. These are managed delegate joins; native tail
exit remains separate. Early ready returns, absent recovery or timeouts fail
without retrying. Phase4 only coordinates recovery; original traces must prove
the actual empty wait and all write/signal/competing-read/repark/return ordering.

Nine ABI/copied-state/platform checks and Windows HTTP refusal/status/health
controls pass. No native actor, recovery or churn-overlap capture ran in this
step. Step1500's original Linux ownership capture remains at `9dd1e01a`; it is
not new race evidence. Next integrate the owned HTTP churn/race producer and
strict original-order consumer, then finish fragmentation/effective-budget/
offender-cleanup/RAM controls before the stable native/guest batch. No current
web index admission or #225/#748 acceptance changes.

[Step1502](../done/step1502.md) adds two host-reader prerequisites to the real
web replay. Literal Smoke may query `sigaltstack` without changing it; the reader
checks the complete 24-byte writable span and scalar result. One additional
CLOEXEC/NONBLOCK pipe shares the existing descriptor namespace, retains both
descriptor generations and replays bounded counts, occupancy, atomic writes,
EAGAIN, EOF, EPIPE and explicit close/reuse independently of the runtime signal
pipe. These scalar records establish neither byte values nor alternate-stack
contents. Service and other profiles retain their prior admission. Pipe epoll
watches, signal12 replay, original race ordering and the owned HTTP orchestration
remain unfinished; current source/ELF/index guards remain closed.

[Step1503](../done/step1503.md) adds the probe read-end epoll and directed
signal12 replay prerequisites. Watches retain object/generation identity;
modeled counts bound readiness and closure removes watches before fd reuse.
Standard12 coalesces independently of activation35's four-entry queue. A prior
readiness win survives tgkill; empty recheck/repark publishes a new sequence
before pending12 can win EINTR. Delivery selection survives IRQs before the
first observed handler call. These are host scalar/scheduler contracts, with
copied event/action/frame contents still opaque. All 63 focused checks across
six replay fixtures pass with unchanged inputs and zero reuse. At that step,
original-index race projection, owned HTTP churn orchestration and new
native/guest admission remain unfinished.

[Step1504](../done/step1504.md) implements a default-off native qualification mode:
`nativeaot web qualify --signal-race --output <fresh-directory>`. Default v4
qualification retains its two sessions; v5 additionally owns a fresh third
session with directed race during pending churn, advancing timer/worker/collection
counters, actual cancellation/drain, one recovery, later progress and refusals.
Its copied selection may request an immutable original-order projection only
after complete web scheduler replay. That projection retains original normalized
indices, model identities/FS/descriptor generations, exact race/recovery order
and explicit closes; absent native EXIT/REAP remains absent. Actual native and
owned guest execution, pointed-to contents and quota-cleanup acceptance remain
required. Current ELF/source/index guards are unchanged.
The small build and80 focused checks across10 fixtures pass with unchanged
inputs/zero reuse; this is host development evidence.

[Step1505](../done/step1505.md) retains the first native attempt at `0f0e0a90`.
All three payloads publish/inspect and the base Linux qualification passes; the
combined race qualifier fails with `web_signalrace_status_callback`. The waiter
actually returns ready before SIGUSR2, so no EINTR race witness exists. A cleanup6
snapshot with callback0 exposes a separate reader diagnostic defect. All206
original files and exact source/compiler/ELF joins survive a verified builder
recovery. No success receipt or new prepared-image admission follows. Guest
integration additionally needs owned all-attempt exchange association and an
explicit packet contract; selected HTTP samples alone cannot identify repeated
wire exchanges or turn host cancellation into an HTTP response.

[Step1506](../done/step1506.md) corrects the cleanup6/failed8 diagnostic while
retaining strict acknowledged/success roles and explicit missed-gate rejection.
Every logical HTTP call now survives in a fresh `.attempts.json` companion,
including unselected/rejected polls, bounded raw partial bodies, terminal faults
and cancellations. Selected v1 rows link to their logical request IDs; the churn
cancellation remains a host event. Header receipt ends pending overlap even if
the body stalls, and joining another failed request preserves the primary error.
The strict copied inspector checks terminal outcomes, bytes, logical order and
poll windows. New explicit race qualification writes v6 and requires the
companion; historical v4/v5 validation remains supported. The small build and23
focused checks across five fixtures pass with unchanged inputs/zero reuse.
These are host fake-handler/copied-value controls. No native rerun, successful
race witness, real connection/exchange association or guest admission follows.

[Step1507](../done/step1507.md) adds an owned observed transport adapter for that
controller. Three role-specific persistent HTTP/1.1 handlers permit control,
pending churn and pending race together. The existing bounded stream recorder
supplies actual connected endpoints/stream IDs, attempted writes, successful
read bytes, unknown failed transfers and joined disposal. A separate strict
`clros.web.signalrace.wire.v1` profile binds logical attempt IDs to real request
writes; quota schemas/routes remain strict. Raw read offsets remain independent
of HTTP response framing. Five real-loopback controls and32 related checks pass
with unchanged inputs/zero reuse, including partial failure and HTTP700 retention.
The API is not yet wired into native qualification or the guest runner. Full HTTP
FIFO framing, held runner/child evidence and actual PCAP association remain next;
no NAT tuple, native race or guest acceptance is inferred from local stream IDs.

[Step1508](../done/step1508.md) frames the retained known stream bytes, binds
request writes to exchange IDs, and pairs responses FIFO on each connection.
Every response outcome needs a unique complete request/response, exact decoded
body/status/media type and read-time bounds. Failed retries, unknown attempted
writes and incomplete response suffixes remain explicit; consumed failure bodies
are checked only as prefixes. Selected samples bind complete message indices;
churn cancellation stays a host event with index-1. All23 focused checks across
six fixtures pass, including the actual loopback controls, identical-body FIFO,
ambiguous retries and existing quota readers. Quota parsing remains unchanged.
These consistency checks still require an owned guest runner/evidence bundle,
explicit race completion/packet contract and actual PCAP/native/source joins.

[Step1509](../done/step1509.md) implements the separate ownership component and
default-off runner rendezvous. After ordinary controls, the runner sends READY
over a fresh same-user named pipe and waits for DONE within its original deadline,
monitoring child/frame/serial health. The Tooling owner joins observed HTTP work,
checks creator-handle identities and exact flushed-byte hashes against held
selected/attempt/wire originals, frames HTTP, then sends their hashes and joins
the runner. Failure paths close the pipe, cancel/join started tasks and retain
available original logs and partial reports. Only explicit mode emits race fields;
ordinary dry-run output remains byte-identical. All35 focused checks/six fixtures
pass unchanged/zero reuse, including real pipes and a full loopback handoff that
deliberately rejects its failed runner result. No successful guest receipt is
fabricated. QemuSmoke wiring, a separate exact race summary/UTC/packet contract,
actual PCAP association and fresh native/source/ELF/index admission remain next.
No native or QEMU trial ran; the original native failure remains failed.

[Step1510](../done/step1510.md) adds the exact explicit race runner reader while
preserving the ordinary reader's bytes and nine UTC windows. It checks the two
gate rows at their original positions, PID/forward/hash identity and a positive
producer UTC window of at most45s after ordinary controls and inside the child.
The window is copied owner data, not independent monotonic or Date authority.
Explicit replay now permits bounded later lifetimes only after the original
eight-lifetime control events. The first extra accept requires completed ordinary
I/O and bad-peer private retirement; final accepts must equal the complete census.
Tuple/generation/TIME-WAIT guards and normal/records64 defaults remain exact.
All65 focused checks/six fixtures pass unchanged/zero reuse, including complete
copied race projection after a ninth accept. Whole-PCAP integration still needs
the ordinary VLAN/withheld-ACK controls plus concurrent race-flow byte association;
the service packet parser cannot consume that capture unchanged. QemuSmoke wiring,
race HTTP Date/packet joins and fresh native/source/ELF/index admission remain open.

[Step1511](../done/step1511.md) consumes the original whole web PCAP with two
explicit record-owning passes. The strict web reader retains its eight flows,
literal VLAN, withheld-ACK, HTTP and nine UTC checks. Added declared TCP starts
only after the complete ordinary healthy After response and EOF. Bounded concurrent
stream reconstruction reads the same PCAP and delegates only already-validated
ordinary TCP/VLAN record IDs; counts must agree, with no unknown quiet SYNs.
Original record numbers, timestamps and first-byte islands remain intact; no
filtered or renumbered capture is constructed. Service/ordinary defaults remain
unchanged. All31 focused checks/six fixtures pass unchanged/zero reuse. This is
a copied packet inventory: unique host-byte/guest-flow association, race HTTP
Date/syscall/role joins, QemuSmoke wiring and fresh native/source/image admission
remain required. No native or QEMU run occurred.

[Step1512](../done/step1512.md) binds strict race wire/attempt/selected HTTP to
the copied added packet inventory by directional bytes and unique injective
assignment. Every payload guest must match; every successful HTTP host must be
covered. Ambiguous or missing matches reject without equating host and guest
ports or clocks. Completed logical responses retain exact clipped original
request/response ranges and persistent packet order. Failed/cancelled attempts,
unknown write tails, holes, unread responses, empty flows and unassigned hosts
remain explicit. Rechecked copied bounds are504 streams,4MiB aggregate extents,
65536 ranges/edges and64MiB comparison work. All23 focused checks/six fixtures
pass unchanged/zero reuse, including whole-PCAP ordinary/VLAN controls followed
by persistent added HTTP. This supplied-value consistency check adds no private
producer or guest authority. Race Date/owned UTC, syscall/role joins, QemuSmoke
wiring and fresh exact native/source/image admission remain required. No native
or QEMU trial ran; the original failed native attempt remains failed.

[Step1513](../done/step1513.md) adds race Date and native-CALL coverage consumers.
Every complete contiguous guest response header needs canonical RFC1123 Date
within the supplied whole producer UTC window plus/minus5s, without regression
against the ordinary last Date or on a persistent stream. Concurrent streams
have no global Date order; pending headers remain explicit. Wire ticks acquire
no UTC origin. The complete web replay now projects added-lifetime accepts and
positive socket CALLs with original row indices, checked transport identity and
replay descriptor IDs/counters. Ordinary/probe-pipe calls are excluded. This uses
existing replay facts and fabricates no quota-only post-CALL diagnostic row.
The typed temporal consumer requires exact original HTTP packet ranges, consuming
receive/accepted-send coverage and persistent FIFO, excluding peeks. All30 focused
checks/six fixtures pass unchanged/zero reuse, including complete-model projection
and independent Date/temporal controls. No EFI/kernel or quota grammar changed.
HTTP/replay actor roles, owned QemuSmoke and fresh exact native/source/image joins
still precede guest acceptance; the original native failure remains failed.

[Step1514](../done/step1514.md) joins all19 selected HTTP/body roles to the
complete replay's original socket CALLs and actor/recovery order. It recomputes
strict byte assignment and temporal coverage from raw inputs and the replay's
immutable projection. Host cancellation remains metadata: a separate unique,
fully consumed churn request is required, including an unknown host-write tail,
without inventing a response. Original-row edges require actor read/resume/
callback before armed response, actual recovery wait before overlap consumption,
the recovery request before its native writer, and recovery read/three closes
before the race response. Recovery replies may precede the writer; independent
race/repeat replies have no imposed order. All27 focused checks/six fixtures pass
unchanged/zero reuse. The complete-model positive exercises replies before the
writer; race reply before repeat remains allowed but untested. Copied bytes and
complete-model replay prove no original native/guest or GC/ThreadPool cleanup.
Owned QemuSmoke integration and fresh exact native/source/image admission remain
next. No native or QEMU run occurred; the original native failure remains failed.

[Step1515](../done/step1515.md) composes those consumers behind explicit,
default-off `--web-signal-race` in QemuSmoke. Ordinary web remains unchanged.
The dedicated launch uses the private creator-owned race completion, one forward
port and a fresh rendezvous. Eight distinct original outputs stay held against
the exact four pre-run package/index/image/EFI records through complete replay,
whole-PCAP ordinary/added reconstruction, HTTP roles, Date and final source/native
checks. Existing deadlines and old source/ELF/index admission remain unchanged.
All11 focused checks/two fixtures pass unchanged/zero reuse; these cover command
construction, prelaunch refusal and existing owner failure ownership. No positive
owned guest run or native race was executed. The expert's separate proposal adds
a one-shot phase2 start gate and controlled Linux-host scheduling, with a versioned
race-first controller; it remains unapplied here. Original206-file failure stays
failed. That execution repair, fresh admission and actual guest evidence remain
required before any race acceptance.

[Step1516](../done/step1516.md) applies the one-shot phase2 start gate and explicit
v2 controller: pending race, original wait preparation, short churn/progress,
then exactly one start POST. Selected/attempts-v2 add initial-armed/start roles
for21 samples. The private guest owner rechecks a provisional serial blocked
wait (action3/status9); final immutable replay requires original initial wait
before start consumption before actor write. Phase snapshots and copied rows
cannot establish that order. Historical v1/19-sample diagnostics remain explicit.

The separate private Linux experiment binds the actual owned tracer/session,
thread generations/pidfds, one CPU, policies and sleeping232/epoll/pipe before
tracer/actor FIFO scheduling, with rechecks, tracer restoration and confirmed
session death. Native v7 requires the paired v2 reports and complete hashed
scheduling journal; v5/v6 remain historical v1 and ordinary v4 is unchanged.
All70 focused host checks/14 fixtures pass unchanged/zero reuse; tested producers
are preserved. No Linux scheduling/native qualification or guest run occurred.
The native scheduling assumptions, actual EINTR/churn/recovery and fresh exact
source/ELF/index/image admission still require original execution evidence.
#737/#225/#748 remain open; old admission pins remain closed.

[Step1517](../done/step1517.md) preserves the first controlled v7 attempt at
`de39e4db` as failed: `sched_getscheduler errno=38`, partial v2 reports, no web
receipt, and only the confirmed four-task stop journal row. All205 original
files and970 source/producer inputs match their preserved hashes. The actual
builder's musl1.2.6-r2 scheduler exports are ENOSYS stubs; a no-mutation self-query
under the pinned runtime confirms raw Linux x64 getters work on the same thread.
The private owner now invokes syscalls143/144/145 through libc, retaining errno,
affinity, task ownership, readbacks and cleanup. All16 focused checks/3 fixtures
pass unchanged/zero reuse. This repairs host interop; controlled scheduling,
actual EINTR/churn/recovery, native v7 and guest/admission remain unqualified.

[Step1518](../done/step1518.md) records the fresh single controlled native v7
qualification at `e5dadc2e`: native and production receipt validation both exit0.
All214 original files,193 receipt outputs,932 receipt sources and970 transferred
source/producer inputs match their exact hashes. Selected-v2 has21 samples;
attempts-v2 has24 entries with one intentionally canceled pending churn. The
13-row original journal and role traces retain actual initial blocking,
write/signal/consume, EINTR/callback, fresh recovery, tracer restoration and
confirmed original task/session death. Independent review finds no contradiction.
This is a bounded Linux-host experiment using the documented cooperative
numeric-TID lifetime assumption; separate thread traces are not a global kernel
chronology. Both earlier failed attempts stay failed. The fresh8,858,120-byte ELF
SHA652E2E00D49C2529BB8C2439820F8A58F7086766E01D2C08D0748CA659C9BFAE
remains unadmitted pending exact independent decoder/index/source review.
No CLROS guest, quota termination or complete Kestrel/Gate14 acceptance follows.

[Step1519](../done/step1519.md) admits that exact ELF and complete reviewed
instruction lookup after independent decoder/debug/source review:1,426,926
starts/198 syscalls, all4,575,791 executable-section bytes, entry0x408f77 and
digestBD21ABDEBDA40026C43936D111E1FC31296C7A4D73925C798ACCCC1EA3D01326.
Fourteen source pins cover the actual nine-file app; original guards remain.
All19 focused checks/3 fixtures pass unchanged/zero reuse. Actual held
Read/Create passes; altered/earlier-failed ELF, partial decode and aliases refuse.
Prepared instruction authority is host-only. Original owned CLROS guest and
quota termination/churn cleanup remain unqualified; no complete Kestrel follows.

[Step1520](../done/step1520.md) preserves three failed owned attempts at
`ec419d08`: r01 refuses mixed host producer cohorts before QEMU; r02 rejects a
selected29-byte health body in the existing fixed17-byte socket control; r03
uses the corrected default body and reaches race orchestration, but every race
status remains phase0 while POST is pending until timeout. Fresh EFI/image and
all962 source joins are saved; all owned processes are stopped. No complete
summary/owned race receipt or acceptance follows. The expert is tasked with the
phase0 stall; earlier failed attempts remain failed.

[Step1521](../done/step1521.md) resolves r03's cause from original packet/source
evidence: the race SYNs receive no SYN-ACK because seven full TIME-WAITs and the
status connection occupy all eight bulk slots. Finite web now provisions16
bulk slots with derived backing storage and strict16-slot readers; service and
mechanical profiles keep8. Full240-second TIME-WAIT and the exact ordinary
eight-lifetime controls remain. All93 focused checks/8 fixtures pass unchanged/
zero reuse. This is host implementation/model evidence; fresh native mapping,
owned guest race and stable acceptance remain pending.

[Step1522](../done/step1522.md) reviews the exact fresh native geometry and
preserves owned r05's later `web_signalrace_missed_initial_gate` failure. The
sixteen-slot bound is genuine and original PCAP202 contains the race POST after
handshake. Status0,1,1,6 never yields the required initial-armed sample; no
churn/start/recovery is requested. Exact native/source/packet/report originals
are saved and QEMU stops. Expert diagnosis is required before the next scoped
correction; no complete owned race or quota/Kestrel acceptance follows.

[Step1523](../done/step1523.md) implements explicit-header event-owned v3 in the
app, preserving the old timed diagnostic. Its exact-1 epoll waits are owned by
one120-second operation cancellation and separate15-second cleanup grace, with
joined worker retirement before fd disposal. Nineteen focused checks and11
Windows HTTP dispatch/refusal checks pass; native signal behavior is unrun.
Old prepared source admission still refuses changed app bytes. Versioned v3
native/runner budgets and fresh qualification precede another guest;
no legacy failure is reinterpreted or accepted.

[Step1524](../done/step1524.md) implements the explicit v3 copied controller and
strict paired selected/attempt schemas under one150-second capture deadline.
Borrowed client timeout and default selector headers are checked before files
or requests; exact v3 race headers/prefixes and the ordinary v1 repeat probe are
retained in the attempt contract. All36 focused checks across seven fixtures
pass with unchanged inputs and zero reuse. Legacy v1/v2 checks pass; native role
admission explicitly refuses copied v3. Native event-wait/journal integration,
owner/runner/Date windows and fresh original
qualification/admission remain prerequisites to another owned guest.

[Step1525](../done/step1525.md) binds observed v3 request selectors to complete
local HTTP stream headers without changing legacy checks or the raw recorder
schema. Real loopback capture passes with 21 selected samples and 23 attempts;
truncated-response and pre-cancelled reports retain their incomplete status.
All 22 focused checks pass, and nine original reports plus the tested producers
are preserved. This supplies local TCP/framing evidence only. Native/guest
ownership remains closed until the versioned journal, exact event-wait contract,
nested budgets and fresh qualification/admission are integrated.

[Step1526](../done/step1526.md) adds explicit native-v8 qualification dispatch
and schedule-v2 pairing for selected/attempt-v3, preserving the old modes.
Expert review resolves the native deadline conflict: one 210-second operation
lease starts before strace, with 165 seconds required before race admission;
v8 avoids an independently timed TERM helper. Joined report retention remains
uncancelled, and late success is rejected. All 23 focused host checks pass;
no new Linux native wait, signal, cleanup or guest is qualified. The exact raw
timeout expectation still needs original native observations/disassembly.
Guest role/remaining-budget/Date integration and fresh native publication,
reviewed prepared admission and original owned guest evidence remain pending.

[Step1527](../done/step1527.md) checks the exact named event-owned initial,
recovery and repark waits in complete copied replay, retaining descriptor and
causal guards. Selected-v3 role joins require the immutable checked event profile;
legacy profiles remain separate. All 41 focused checks pass after preserving
one failed negative fixture and correcting only its ordering. No new native or
guest execution follows. Explicit guest owner/pipe/runner/remaining-time/Date
composition and fresh native qualification/admission still precede an owned guest.

[Step1528](../done/step1528.md) checks the shared explicit v3 pipe protocol and
copied runner/Date windows. The new whole-IPC cap is165s; the original absolute
clock must retain at least165s immediately before READY after connection.
Legacy pipe/JSON/profile and45s bounds remain. All43 focused checks pass,
including real local pipes, insufficient-budget EOF before READY, profile
crossings and exact copied165s boundaries. This is unmeasured shared policy;
production QEMU options and owner dispatch remain legacy pending the next slice.
No fresh Linux-native qualification, guest admission or Kestrel acceptance follows.

[Step1529](../done/step1529.md) connects the explicit event selector to production
owner/runner/held replay/Date dispatch. The selected profile must agree across
pipe, original reports, private Completion, summary and complete replay; old timed
selection remains separate. The original285s runner clock supplies the pre-READY
budget callback and retains a specific insufficient-budget failure. All47 focused
checks pass, including actual local v3 ownership failures and reports held before
DONE, with no successful synthetic Completion. Fresh native-v8 publication and
original source/debug/ELF/index qualification, deliberate prepared admission and
an original owned guest remain required. This adds no Kestrel acceptance.

[Step1530](../done/step1530.md) retains the original native-v8
`web_signalrace_lost_overlap` failure and repairs its demonstrated finite-churn
race. Explicit initial churn v2 repeats actual awaited timer/ThreadPool/full-GC
work until request cancellation, with a fixed watchdog that fails on expiry.
Final churn remains finite v1/exactly8; strict attempt/header/guest joins reject
exchanged selectors. Both dedicated probe threads must also have original native
EXIT/REAP before final healthy send; callback/request threads remain live.
All80 focused checks pass, including the actual finite-helper failure and
continuing-helper correction. This is development evidence; repaired native,
guest, component-budget/termination cleanup and stable full/ancestor gates remain
required before #737/#748/#225 closure or complete Kestrel acceptance.

[Step1531](../done/step1531.md) preserves the successful original `293dafb4`
Linux native-v8 qualification, its separate validation and complete source/output
joins. Two reviewed corrections still precede fresh Smoke publication: terminal
churn counters and active/root release publish atomically, and unselected
event-owned status polls use continuous limits. The retained service publication
needs only a reviewed current-consumer source-pin refresh; its original
ELF/index/package and producer provenance stay unchanged. No new guest or quota
acceptance follows from these Linux and host results.

[Step1532](../done/step1532.md) reviews the corrected original `c31d7cf7`
Linux native-v8 publication. Exact ELF/debug/source/index joins and original
signal/churn cleanup traces pass; the prepared-program pins select this ELF.
The optional host RAM forwarder permits the required 256/512 MiB quota
comparison without changing the payload or its budgets. Owned Smoke, both quota
captures and one refreshed configured positive V2 lifecycle still precede guest
and ordinary-headroom acceptance. The shared 36 MiB pool does not enforce
independent 16 MiB GC and 20 MiB native partitions.

[Step1533](../done/step1533.md) preserves the first actual quota256 guest at
`470bc00d`: one successful mmap is followed by an observer abort before listen
or pressure, then real exit/reap and a rejected final lease audit. Expert review
finds the new stack observer enters an active-gate check that assumes interrupt
SS0, while an ordinary SYSCALL callback retains SS16. The repair explicitly
permits both only after common gateway authentication; direct IRQ validation
keeps exact SS0. The original remains failed. Corrected guest, RAM comparison,
ordinary V2 fit and final stable gates remain required; no app republish is needed.

[Step1534](../done/step1534.md) records the corrected gateway's fresh quota256
attempt at `ad7bdc36`: stack leases and listen succeed, but the host health
exchange fails before the TCP quiet deadline and before pressure. The original
seven inbound SYNs have no guest reply or HTTP payload. Its partial wire report
does not retain the exception type. Failure-only quota peer diagnostics now
retain a bounded typed exception tree; startup retry classification and all
deadlines remain unchanged pending a fresh capture. This is not quota cleanup
or ordinary native-headroom acceptance.

[Step1535](../done/step1535.md) captures the missing original exception in the
diagnostic-only quota256 run: `HttpRequestException(Unknown)` wraps
`IOException` and `SocketException(ConnectionReset, native10054)`. The EFI/image
are byte-identical to the preceding failed run. Initial health retries now admit
that exact reset wrapper within the existing400s deadline. Pressure/poll
classification remains unchanged. Fresh quota/ordinary lifecycle acceptance is
still required; both startup failures remain failed originals.

[Step1536](../done/step1536.md) records successful startup/held-connection/worker
progress after that repair, followed by the180s operation deadline before quota
pressure. The instrumented guest completes14 random requests in151.247s;
readiness still requires more than32 and another advancing sample. The bounded
host calibration allows480s operation and900s quota child/report time. Runner900s
and owner960s remain independent outer caps. Signal-race and stream-control
reports retain600s bounds, all capture capacities and the600s application
watchdog remain unchanged. A fresh original quota run is still required.

[Step1537](../done/step1537.md) preserves the next failed owned run: all17 overlap
samples and quota cleanup markers are present, but terminal control/pressure I/O
hits the captured wrapped reset. The terminal-only predicate now admits that exact
shape and labels it ConnectionError; early completion/cancellation/overlap gates
remain mandatory. Strict offline replay also exposes reader omissions: the
unchanged app emits exact Kestrel[22] heartbeat warnings on stdout and its random
controls on stderr. The reader preserves and checks those bytes rather than
requiring empty stdout. A second strict replay exposes frozen budget admission
being mistaken for the post-reap finish clock. Quota-only readers now bind the
unique exact service-end row and retain mandatory full finish/history checks;
ordinary clock checks are unchanged. Operation540s gives measured margin over the470.85s path;
900s child/report/runner and960s owner remain independent caps. The failed original
does not become accepted. Corrected-reader strict native replay and69focused host
checks pass. The developer chat's restricted context denies loopback Connect(10013);
execution transfers to the acceptance reviewer under its existing permissions,
where the unchanged producer passes all17 actual transport checks. A fresh owned
capture and final acceptance remain required.

[Step1538](../done/step1538.md) preserves r06 after successful external peer and
native cleanup, with final owned validation stopped by an overlap-reader defect.
Console envelopes can precede authenticated startup metadata and can use fd1;
writev may publish several fragments before one CALL. The corrected mapper uses
the existing full startup validator, checks both channels and exact successful
CALL byte totals, and retains stderr-only progress offsets. Unknown metadata and
unbound successful console calls reject. 26 focused checks and the original r06
public native/stream/temporal/overlap/NIC replay pass. This does not turn the failed
owned capture into acceptance; a fresh complete owned run remains required.

[Step1539](../done/step1539.md) records two complete original owned quota captures
at `a2494fd9`, with the same EFI/disk/policy at actual256MiB and512MiB. Both
terminate the offending workload and return every recorded stack/socket lease.
The independently joined pre-pressure non-GC bound is17,473,536bytes; whole-run
pressure peaks remain separate. The36MiB pool is shared, without enforced
independent16MiB/20MiB partitions. Ordinary V2's first fresh capture remains
failed: its last real TIME-WAIT deadline904.778guest seconds exceeded the900s
host capture. Only the V2 host selection now defaults to and permits1080s;
V1/quota remain900s. Guest TCP timers, app/peer watchdogs, capture capacities,
termination and full evidence validation are unchanged.51focused checks pass.
Fresh V2/event-owned Smoke, affected ancestors and final full acceptance remain
required before #748/#225/#737 closure or PR790 acceptance.

[Step1540](../done/step1540.md) preserves V2r02's failed full consumer after
successful916,877ms guest/runner cleanup. The isolation reader incorrectly
forbade a byte-identical retransmission of the unacknowledged second persist
probe while the independent healthy connection completed. It now permits only
exact raw repeats at or after the selected second probe; earliest-probe/reopen
ordering, zero windows, healthy completion and literal HTTP joins remain strict.
Eight focused controls and original public native/packet/accounting replay pass.
No guest code or timers change, and the failed original remains failed. Fresh
owned V2 and the remaining acceptance batch still precede closure.

[Step1542](../done/step1542.md) admits the fresh original `cd275855` Linux
publication after step1541's one event-owned preparation yield. The exact ELF,
debug/source/compiler/output joins and complete instruction index pass; six host
admission checks and five actual package rejection controls pass. The yield does
not make write/signal/read atomic or prevent GC and timer preemption. Fresh owned
Smoke and ordinary V2, affected ancestors and the stable full-host run remain
required. All three earlier Smoke captures remain failed originals.

## Continuing-service foundations

[Step1479](../done/step1479.md) completes the fresh original v1 service and five
memory controls at `fee0c9af`, with exact source/native joins. The repaired reader
passes the original 829,226ms service lifetime; all three expected memory faults
pass their dedicated full-parent restoration checks. Failed originals remain failed.

[Step1481](../done/step1481.md) records the corrected runner's stable unfiltered full:
5,918 checks across 792 fixtures (698 Fast, 94 Native) at `96ba8dbd`, unchanged
inputs, zero reuse and no failures. The exact declared-return whitelist rejects
unsupported generic tasks before invocation. Both earlier failed full attempts
remain failed; the original successful producer and dependencies were preserved
before documentation rebuilds.

Fourteen finite issue scopes have reviewed support for dependency-ordered
reconciliation; step1481 names their criteria, original receipts and limitations.
Tracker closure still requires evidence comments, versioned preview/apply and
readback. Fresh v1/memory evidence stays at `fee0c9af`; retained configured v2,
15 individual ancestors and native reviews keep their original `810b3eef` scopes.

#748/#225 remain open: quota-triggered offending-workload termination and cleanup
during churn are unproved. The 36MiB dynamic pool is shared; 16MiB GC plus declared
20MiB native headroom does not enforce independent partitions. Native saturation
occurs before listen and after drain. Complete Kestrel/Gate14, isolation, cloud
and production remain incomplete.

## Planned static-file acceptance

The [ordered roadmap](workload-profiles.md#disk-backed-static-file-roadmap-2026-10-18)
adds #768–#775 after current service qualification and #224. The existing `/config`
already uses source-generated JSON; the candidate adds `/api/status` and
`/api/items` DTOs. #772 qualifies a separate staged-file intermediate profile.
[#774](http://192.168.4.35:3000/admin/CLROS/issues/774) requires a real external
browser loading disk-backed HTML/CSS/JS and rendering both endpoint results.
This is planned acceptance, not implemented static-file support.

Disk proof must include cold block reads after Kestrel readiness, file/fd/device
generations, LBA and byte-hash joins to independently extracted image contents.
Two disk-content variants must use the same app ELF/package and yield different
asset responses; caches are cleared or declared and bounded. Asset bytes must
not be embedded or wholly boot-staged in the disk profile. Missing/corrupt files
and controlled read failures cannot silently return a cached index page.

Check GET/HEAD, media type and length, ranges/416, conditional requests/304,
404, concurrent offsets and continued JSON progress. Verify confinement using
raw HTTP and syscall controls as well as browser requests, since a browser can
normalize traversal. Keep directory listing and writes disabled. Preserve exact
source/native/EFI/app/disk/serial/packet/HTTP/browser evidence and negative controls.

[#775](http://192.168.4.35:3000/admin/CLROS/issues/775) adds sustained operation,
measured resource budgets and actual app exit, off-stack reap and storage/DMA
cleanup. It must address the old finite 600-second UTC validity window before
long runs. Runner termination is not graceful shutdown. The original local
Gate14 graph and all accepted finite receipts remain unchanged.

## Accepted finite baseline

[Step1419](../done/step1419.md) qualifies bounded #220/#221/#222/#759/#223 at
implementation `88b02b4d`: 5,676 unchanged unfiltered full-host checks across
757 fixtures (94 Native), three normal web and three quota prefixes, three finite
policy/vector guests and eight affected ancestors. The nine application captures
retain `02859bfd`; exact selected-source and project-policy forward joins explain
the later source labels. Original failures and repairs remain in steps1406-1418.

Real upstream Kestrel serves exact health/configuration/logging entities, the
49,152-byte vector response and later health. The same healthy connection survives
the 30-second bad-peer deadline and rejected VLAN frame. Actual socket policy
negatives refuse port8081, foreign10.0.2.2, duplicate8080 and descriptor capacity;
the original vector fixture retains short sends/EAGAIN, reset and disposal.
External receipts check status/body/hash/deadline, advancing Date windows and
stopped-guest refusal, with raw HTTP/PCAP/serial evidence retained.

Owned UTC copies valid UEFI time before exit and shares the HPET epoch, under the
declared UTC-RTC contract, five-second observation tolerance and 600-second
lifetime. Separate copied-source unavailable/invalid controls reject workload
admission before entry/readiness and release startup/package owners. They do not
establish actual firmware-read failure or arbitrary-platform clock support.

Original code/relocation joins, complete bounded bodies, typed layout changes,
ABI/frame/unwind and actual socket dispatch-table targets are reviewed. The new
ancestor audit retains6,992 source joins and72 original native inputs. Missing
bodies, uninspected referenced data and transitive/whole-stack safety gain no
authority. Web prefixes retain live workers until runner termination; guest
app-exit/reap/resource cleanup, general service, isolation, numbered, cloud and
production gates remain incomplete. Continue #743/#744/#746/#240/#757/#758 before
repeated service #224; #737 remains the shared integration owner.

[Step1405](../done/step1405.md) qualifies bounded #219 upstream startup and
#745 checked socket vectors. Frozen implementation `8ef72134` passes 5,619
unfiltered full-host checks across 751 fixtures (94 Native), six strict web/quota
prefixes and eight affected socket/runtime ancestors. Three retained strict
BufferList guests belong to `0086b219`; the intervening change only updates three
web identity literals and evidence. Their kernel/vector sources are unchanged.

The real upstream constructors/build precede bind. Normal web runs return exact
health/configuration/logging/repeat, 49,152 binary vector bytes and later health;
quota runs retain 500/409/later-health controls. The finite BufferList fixture
observes real short sends/EAGAIN across eight bounded batches totaling 512 KiB,
then reset, pending-receive disposal, later health and terminal transport cleanup.
That accepted finite scope uses16 vectors/65,536 total bytes. Development
[step1459](../done/step1459.md), under M16-026/#787, admits connected messages
up to1024 vectors/1MiB after the first stream sendmsg in a fresh v2 guest fails
the old admission bounds. Complete metadata/range validation and short copied
prefixes remain; this larger scope has50 focused checks but awaits original
native/guest and batch qualification. Scalar/file/pipe bounds are unchanged.
Names, ancillary data and unmeasured flags remain unsupported; the ignored-SIGPIPE
profile stays explicit.

Web captures are continuing prefixes with live workers and runner-owned
termination, without guest app-exit/resource-cleanup qualification. The original
native/source/packet joins and bounded ABI/frame reviews retain their exact
scope; generated read-only metadata is not called identical. Continue #220 under
#737, then #221/#222 and #759 before #223. General Kestrel/service, local Gate14,
whole-stack, isolation, cloud and production acceptance remain incomplete.

## NativeAOT-first milestone

[Step1460](../done/step1460.md) retains one original v2 lifecycle smoke/service
pass at `e0037e92`: exact32KiB echo,512KiB stream with healthy progress,
two client FIN cancellations, terminal drain and18 worker reaps. Strict
message/syscall/packet and owned-child checks pass; the836520ms capture retains
original source/object/EFI/package/serial/PCAP joins. Its16-vector/65536B maxima
do not extend the actual observed vector scope. Larger-bound host/native work,
the new pipe-race fixture and its20 focused checks remain development evidence.
Step1461 adds one strict pipe stolen-readiness guest with27 calls, actual handler,
later I/O and two reaps/eight transfers after an exact repark inventory correction.
Fixed-runner full-host, affected ancestors, equivalent epoll race, GC/native
pressure/quota recovery and repeated-service batch qualification remain open.

[Step1380](../done/step1380.md) qualifies #217 configuration and #741/#742
network containment at implementation `3ef7316d`: 5,537 full-host checks across
731 fixtures (94 Native), three web and three containment captures, three repaired
signal/partial-pipe repetitions and fifteen other ancestors. All 24 captures
retain exact original source/object/EFI/package/serial/packet joins. Selected
configuration is observed inside the upstream NativeAOT application; the healthy
old peers survive another connection's timeout and unsupported RX content.
The later application/service ladder retains its separate prerequisites under #737.
This adds no whole-stack, general Kestrel, numbered, cloud or production gate.

[Step1389](../done/step1389.md) qualifies finite #218 stdout/stderr logging at
implementation `2bfbaeee`: 5,569 full-host checks across 734 fixtures (94 Native),
three normal and three quota guests, and eight affected socket/runtime ancestors.
Actual copied write/writev, eight workers, EBADF and quota EIO join the original
source/native/serial/packet evidence. The authenticated exit tail reaches real
exit60; ordinary IRQ yielding and ownership guards remain. That receipt left #219 startup unaccepted. Step1405 separately qualifies the
constructor/failure-marker repair and vectors; #220 and the later service/time
requirements remain open under #737.

The immutable [steps1390-1404](../done/step1390.md) retain implementation,
publication, decoder/native review and failed-qualification history. Step1405
consolidates the later stable result. Step1389 retains its original logging/exit
scope; the new continuing web prefixes do not relabel it or imply new cleanup.

[Step1335](../done/step1335.md) records fresh #214/#215 builder evidence under shared
integration [#737](http://192.168.4.35:3000/admin/CLROS/issues/737). The existing
three-app publisher is retained byte-for-byte: the accepted original runtime
package pins its source. The `nativeaot web qualify --output <fresh-directory>`
wrapper invokes that publisher, then checks the exact default `/healthz` body
and a separate configured body, rejects contradictory finite-app markers, and
seals source, payload, dependency and output hashes. `nativeaot web validate <evidence>`
rechecks the original source and native inputs as well as retained outputs.
Fresh Linux qualification passes all three apps and three Kestrel HTTP runs,
including the exact17-byte default body. An independent copy audit verifies783
source inputs,135 output files and six original managed/native producer files;
1,042 native input identities remain recorded. These are Linux builder results.
[Step1351](../done/step1351.md) accepts this builder scope for #214/#215 and
the V2 image/staging scope for #216 after the complete shared qualification.
Three V2 guests preserve exact bytes without entering the application.

The [current workload decision](workload-profiles.md) makes M16 NativeAOT
services, continuing M07 TCP and M10 actual Kestrel the critical path.
M16-001/#679 through M16-007/#685 and the bounded scheduler/timer foundations
have historical qualification. The .NET11 migration, #188 lifecycle and
#191/#192 through #688 finite runtime batch are qualified in steps1225/1230/1307.
Step1332 qualifies continuing network/TCP; step1351 qualifies the original finite
socket/SocketAsyncEngine fixtures for #689/#690. Step1380 qualifies #217
configuration; step1389 qualifies finite #218 stdout/stderr and step1405
qualifies bounded startup/vectors. Continue #220 real Kestrel socket I/O
under #737. Follow the exact managed issue dependencies.
Mechanical ELF startup/write/exit does not run the upstream runtime.
M16-008/#686 uses mechanical thread/TLS/futex fixtures with real per-thread
signal-mask inheritance/restoration. M16-009/#687 qualifies the first pinned
Hello after signal startup/delivery, before focused suspension checks.
M16-010/#688 repeats Hello and qualifies the comprehensive runtime exerciser;
step1351's real System.Net.Sockets fixture follows that runtime qualification.
CoreCLR-specific #204-#212 are deferred. The legacy hosted package/TPA contract
below remains a future target, not the first-web payload format. M16-002's
qualified ELF package preserves the meaning of existing OSAPPv0 and
`kestrel.smoke.package.v0` host evidence.
First acceptance uses one upstream static NativeAOT Minimal API, HTTP/1.1
IPv4 port8080 and /healthz body `OS_MINIMAL_API_OK`, with the unchanged
Kestrel evidence markers. Three fresh post-exit boots, external probes,
request churn/async/cancellation/shutdown and a negative probe after guest
termination are required. The full Kestrel milestone remains incomplete.

## Historical development receipts

The following pending/failure statements describe their named checkpoints.
Step1380 supersedes them only for the qualified M10-004 slice.

[Step1354](../done/step1354.md) adds a default-off `EnableM10004Probe`
development profile for the unchanged original web ELF. It retains the live
network/runtime owner, observes actual entry, immutable configuration reads,
bind/listen and standard upstream startup output, and probes `/healthz` then
`/config` through the existing runner's owned QEMU process. The selected package
uses a distinct configuration body and environment endpoint8081 while CLI and
socket policy still bind8080. Measured interval, mapping, native-stack and finite
entropy limits are explicit web-profile bounds. Kestrel's one-byte `MSG_PEEK`
copies without consuming the TCP receive window.

These are development diagnostics. Step1354 deliberately returned
`web_continuing_evidence_admission_pending` while independent continuing
transcript, original native, packet and runner joins were incomplete. The later
consumer steps below enable owner admission for qualification. No finite
exit/reap or DMA teardown is invented for
a continuing process. The retained r12 diagnostic returns exact200/22-byte
selected health and200/238-byte configuration responses; an independent bounded
PCAP reassembly matches both bodies. The runner's QEMU remains alive across
both probes, then is confirmed dead with a refused endpoint. #217 and the
dependent M10 tickets remain open; the first
default-body/HTTP1.1, repeated-load, async/cancellation/shutdown and local claim
gates remain pending.

[Step1355](../done/step1355.md) adds host validation of the retained r12 complete
prefix: exact fd1/fd2 byte framing, output/call/counter bindings, contiguous call
ordinals and restored-return observations, existing epoll sequence binding, and
original sealed syscall/return/IRQ instruction lookup. It accounts for1,211
complete calls,13 writes/1,358 stdout bytes and one retained epoll wait. The final
incomplete syscall remains a separately hashed fragment.
[Step1356](../done/step1356.md) adds explicit continuing semantic replay over
the existing memory/thread/signal/FIFO/epoll/socket model: nine clones,271 IRQs,
53 drains and648 completed transfers retain ten live workers. File-generation,
output scratch, peek and shutdown/reset controls reject impossible observations.
The two accepted connections account for537 sent and159 consumed bytes; their
packet/content join remains separate. See [app-model](app-model.md) for the
replay boundary. Parent/native/source/packet/runner joins and stable-batch
qualification are still required; complete smoke admission remains closed.
[Step1357](../done/step1357.md) subsequently joins the complete original235-record
firmware/owned/package/startup/service/APIC/NIC/ARP parent, roots, clocks and
retained live DMA to the leaf. Native/source/runner/independent packet joins
and stable-batch qualification still precede acceptance.
[Step1358](../done/step1358.md) validates the original PCAP's checksums, TCP
lifetimes/sequences and exact selected HTTP streams against those parents and
leaf byte counts. The second client FIN remains unacknowledged in the capture;
no shutdown or TIME_WAIT drain is invented. Native/source/runner admission and
stable-batch qualification remain pending.
[Step1359](../done/step1359.md) joins runner selection, child lifetime,
probe deadlines/liveness and exact summary bytes to the raw serial and HTTP
expectations. Three runner checks, three profile checks and25 retained-input
mutations pass. The retained capture uses an explicitly synthetic outer process
result/window for inspection; it is not a fresh runner qualification. Current
native/source admission, complete stack-path accounting and stable-batch
qualification remain pending.
No new guest run or issue closure is part of these host parser steps.
[Step1360](../done/step1360.md) wires the existing complete EFI verifier into
the continuing runner and retains its inputs through validation. The raw image
must contain the exact selected boot EFI and package; app/source inventories
are rechecked afterward. Seven focused checks and seven retained-image
mutations pass. Old r12 provenance correctly rejects against current sources;
fresh stable native/guest qualification is still pending.
[Step1361](../done/step1361.md) enables the complete owner-admitted path for
qualification. Borrowed transcript inspection emits observations only;
`QemuSmoke` emits `TEST_PASS qemu.smoke.web` after the actual runner and final
held source/native/image/evidence checks. Original scoped stack joins measure
6440 bytes for threaded output from gateway stack top and8872 bytes for the
retained supervisor failure path. Fresh native/full-host/guest qualification
still precedes #217 acceptance.
[Step1362](../done/step1362.md) corrects a concrete pre-switch startup stack
contract: the original heap-reservation path needs more than the declared32KiB
diagnostic body budget. Only M10-004 now reserves a64KiB declaration within the
existing main stack. Fresh native verification passes; all1741 method bodies
agree with r12 except that budget immediate, with corresponding PE/debug
metadata changes. The earlier candidate is retained as rejected before any
qualification guest or full-host run. Combined qualification remains pending.
[Step1363](../done/step1363.md) retains the subsequent stopped full-host gate:
eleven legacy expectation failures, zero qualification guests. Explicit strict
inventory/profile/source-pin repairs pass75 focused checks. The next complete
fast phase finds three later descendant-matrix omissions and blocks native work;
those test-only repairs pass20 focused checks. A fresh combined full-host/current
guest gate is still required before #217 acceptance.
The r05 full-host run then passes5,482 checks across720 fixtures. Its first
qualification QEMU exits before executing guest code because the retained
read-only image handle conflicts with QEMU's writable AHCI disk open. The
continuing-web runner now uses a temporary snapshot overlay, preserving the
locked, byte-verified base image. Six focused runner checks and paused QEMU
launch controls pass; fresh combined qualification remains pending.

## Architecture review prerequisites

[PR739](http://192.168.4.35:3000/admin/CLROS/pulls/739) reports development
guest `/healthz` and `/config` responses at the October 16 review snapshot.
It is unmerged and does not supply complete continuing-service acceptance.
Preserve #737's ownership and #217 -> #218 -> #219 work. Before #220 closure,
the managed graph now requires connection-local timeout handling (M07-027),
RX content-drop containment (M07-028) and checked vectored I/O (M16-013).
Exercise the pinned Kestrel multi-segment response path with independent exact
bytes and partial-write/backpressure evidence.

External HTTP #223 now requires [M16-019 owned realtime](http://192.168.4.35:3000/admin/CLROS/issues/759).
Both retained PR739 r12 responses contain the syntactically valid date
`Thu, 01 Jan 1970 00:00:00 GMT`; its validator accepts formatting without checking
plausibility. Compare the date with an independent UTC observation interval and
declared tolerance, and demonstrate advancing guest `DateTime.UtcNow`.

Repeated-service #224 additionally needs TCP persist, compact TIME-WAIT,
explicit continuing execution policy, replenished randomness, pending-handshake
reset cleanup (M07-032) and continuing ISNs (M07-033). Async/churn #225 needs
fragmented physical backing, a guest-verified GC/native resource budget and
pending-signal checks before renewed interruptible waits (M16-020). Keep the
original finite profiles and their markers reproducible. M16-021's broader
SIGPIPE policy work follows the local gate; the ignored profile remains explicit.
The [canonical order](workload-profiles.md#architecture-review-implementation-order-2026-10-16)
links every ticket and separates later isolation, compiler tooling and measured
performance work. No new numbered gate passes from this planning update.

## Transport decision experiments

The primary route uses the pinned static `linux-musl-x64` payload and Kestrel's
stock socket transport. The host-only
[comparison #702](http://192.168.4.35:3000/admin/CLROS/issues/702)
may replace that transport via
[`IConnectionListenerFactory`](https://github.com/dotnet/dotnet/blob/820230c386db5b0106c5430b3e5aee92a7826bf5/src/aspnetcore/src/Servers/Connections.Abstractions/src/IConnectionListenerFactory.cs)
and `ConnectionContext`/`IDuplexPipe` while retaining the real Kestrel HTTP engine
and Minimal API pipeline. Compare external `/healthz`, partial I/O, cancellation,
backpressure, disconnect/shutdown and the resulting runtime/syscall inventory.
Record whether the adapter reduces the required CLROS socket/epoll surface before
changing the architecture; a host adapter is not a guest TCP implementation.

Replacing transport does not eliminate runtime threads, GC or timers. In the
pinned .NET 11 RC2 Kestrel source,
[`Heartbeat`](https://github.com/dotnet/dotnet/blob/820230c386db5b0106c5430b3e5aee92a7826bf5/src/aspnetcore/src/Servers/Kestrel/Core/src/Internal/Infrastructure/Heartbeat.cs)
starts a dedicated thread and waits on an event. A custom native runtime target
therefore needs measured runtime services as well as its transport adapter.

[WASI HTTP #703](http://192.168.4.35:3000/admin/CLROS/issues/703)
is an optional separate experiment. The previously reviewed
[`componentize-dotnet`](https://github.com/bytecodealliance/componentize-dotnet/tree/f6bfc5a9a5a25728aeec959be60c8f737278a183)
snapshot targets .NET 10+ AOT WASI 0.2 components with experimental NativeAOT-LLVM
tooling. This is historical research; .NET 11 feasibility needs a separate audit
and must not be inferred from the selected stock RC2 compiler.
A custom ASP.NET `IServer` dispatching WASI HTTP requests may preserve Minimal
API routing, but replaces Kestrel and cannot satisfy this milestone. WASI
asynchronous sockets exist, yet both the pinned
[Mono WASI build](https://github.com/dotnet/runtime/blob/v10.0.12/src/mono/CMakeLists.txt#L344)
and the reviewed
[NativeAOT-LLVM WASI thread implementation](https://github.com/dotnet/runtimelab/blob/527a565752062b20bb9693eea6c884b34ecce7bf/src/native/libs/System.Native/pal_threading_wasi.c#L63)
lack thread creation needed by unmodified Kestrel. Component publication, a host
engine run or a custom `IServer` response is not CLROS Kestrel evidence.
Both research tickets are supplemental, have no milestone, and add no
critical-path dependencies to the NativeAOT-first implementation sequence.

## Current Status

Pinned upstream NativeAOT Kestrel runs in the bounded CLROS profiles above.
Step1405 qualifies startup and vector I/O; the remaining socket/service/time and
local Gate14 requirements still govern the single-app milestone. The separate
CoreCLR/OSHost_* PAL path remains future work described in [pal.md](pal.md).

This document is the prerequisite ledger for Gitea issue #59 and the first smoke-package contract ledger for Gitea issue #149. Use the single local [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section plus [docs/completion-gates.md Gate 14](completion-gates.md#gate-14-profile-split) when classifying host-only, mock-only, dry-run, local-loopback, native-tier, or documentation-only evidence.

Completion gate 14 in `docs/completion-gates.md` remains `Fail` until the first valid in-guest Kestrel proof below exists. That gate is split by `docs/workload-profiles.md`: `SingleMinimalApi` needs one packaged Minimal API/Kestrel app with guest and external HTTP evidence, while `MultiMinimalApi` additionally needs multi-app process/isolation/supervisor and crash-isolation evidence.

## Provider Scope

The canonical provider split is [docs/completion-gates.md Gate 14](completion-gates.md#gate-14-provider-split).

`SingleMinimalApi` Kestrel evidence must be scoped to the platform where it was
captured. A local QEMU/libvirt smoke, an AWS Nitro VM smoke, and an Azure
Hyper-V VM smoke are separate claim gates.

Local QEMU/libvirt Kestrel evidence requires the `SingleMinimalApi` guest to
boot locally, load the versioned Linux ELF package, start the pinned upstream
.NET 11 NativeAOT app, bind Kestrel to the manifest endpoint, and return
the expected `GET /healthz` response through the QEMU/libvirt network path.

AWS Nitro Kestrel evidence requires the same `SingleMinimalApi` guest Kestrel
markers plus AWS-specific VM evidence: an HVM x86_64 image or documented AWS
import path, Nitro-compatible PCIe discovery, ENA network support, NVMe root
storage support, DHCP IPv4, TCP listener behavior, and serial logs collected
from EC2.

Azure VM Kestrel evidence requires the same `SingleMinimalApi` guest Kestrel
markers plus Azure-specific VM evidence: a fixed VHD or documented Azure image
path, Hyper-V detection, VMBus negotiation, NetVSC network support, StorVSC
storage support, DHCP IPv4, TCP listener behavior, and Azure serial or boot
diagnostic logs.

`MultiMinimalApi` adds process/isolation/supervisor evidence on top of each
provider gate and must not be inferred from a `SingleMinimalApi` provider pass.

Do not promote evidence across provider gates. AWS Nitro evidence includes ENA
network support and NVMe root storage support. Azure VM evidence includes VMBus
negotiation, NetVSC network support, and StorVSC storage support.

## Milestone Definition

The first acceptable Kestrel proof is:

1. Build a packaged ASP.NET Minimal API application targeting the pinned .NET 11 release.
2. Package the pinned static Linux ELF into a CLROS image with explicit app identity, arguments, environment/config, listen endpoint and logging.
3. Boot CLROS in QEMU or libvirt and leave firmware boot services.
4. Enter the protected application context through the qualified Linux ABI; the payload initializes its own upstream .NET 11 runtime, GC and EH.
5. Launch the packaged Minimal API app inside the guest.
6. Bind a Kestrel endpoint from the manifest or app configuration.
7. Serve a deterministic HTTP response over the OS network path.
8. Capture serial and runner evidence with machine-readable markers.

Required smoke markers:

```text
TEST_START kestrel.smoke
TEST_PASS kestrel.smoke
TEST_FAIL kestrel.smoke reason=<code>
```

`TEST_PASS kestrel.smoke` is valid only when the guest OS launches the packaged Minimal API app and an external client receives the expected HTTP response from Kestrel. A host-side build, host-side unit test, native-tier HTTP demo, or placeholder app does not satisfy this proof.

## First Smoke Package Contract

Contract id: `kestrel.smoke.package.v0`.

This is the retained framework-dependent hosted smoke package contract. The
NativeAOT-first milestone uses the separately versioned Linux ELF package;
do not reinterpret this contract or require its CoreCLR/TPA work first. The
sections below retain host tooling and future hosted-runtime requirements,
not guest execution evidence.

Fixture identity:

- Source fixture: `apps/MinimalApiSmoke`.
- App id: `minimal.api.smoke`.
- Runtime kind: `dotnet10`.
- Entry assembly: `apps/minimal-api/MinimalApiSmoke.dll`.
- Required listen endpoint: `http://0.0.0.0:8080`.
- Required external runner probe path: `GET /healthz`.
- Manual smoke path: `GET /`.
- Required response status: `200`.
- Required response media type: `text/plain`.
- Required response body: `OS_MINIMAL_API_OK`.
- Required response body bytes: `17`.
- Required response body SHA-256: `87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d`.
- Required manifest environment/config key: `OS_SMOKE_EXPECTED_BODY=OS_MINIMAL_API_OK`.

The in-guest fixture must derive the `GET /healthz` response body from the manifest-provided `OS_SMOKE_EXPECTED_BODY` value or from a documented PAL config value that is populated from that manifest entry. A hard-coded host-only response remains host fixture evidence under the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section.

Required package-relative file layout:

```text
apps/minimal-api/MinimalApiSmoke.dll
apps/minimal-api/MinimalApiSmoke.deps.json
apps/minimal-api/MinimalApiSmoke.runtimeconfig.json
apps/minimal-api/appsettings.json
```

Host-side contract validation now exists for this layout through:

```powershell
dotnet run --project .\tools\OS.ManifestTool\OS.ManifestTool.csproj -- validate-kestrel-package --manifest .\manifest.json --package-root .\package-root --profile SingleMinimalApi
```

The validator checks only deterministic host-visible contract shape. It requires contract id `kestrel.smoke.package.v0`, `SingleMinimalApi`, app id `minimal.api.smoke`, `runtime.kind` `dotnet10`, manifest app/runtime assembly path `apps/minimal-api/MinimalApiSmoke.dll`, one Kestrel listen endpoint at `http://0.0.0.0:8080`, manifest environment key `OS_SMOKE_EXPECTED_BODY`, and the required package files listed above. It returns deterministic failure codes such as `kestrel_package_missing_file`, `kestrel_package_missing_expected_env`, `kestrel_package_unsupported_profile`, `kestrel_package_unsupported_runtime`, and `kestrel_package_invalid_listen`. It remains host package-contract evidence under the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section.

Issue #260 adds a host-publishable template for this contract at
`apps/MinimalApiSmoke`. The app uses ASP.NET Core Minimal APIs with explicit
`GET /`, `GET /healthz`, and `GET /config` endpoints. `/` and `/healthz`
return the configured `OS_SMOKE_EXPECTED_BODY` value as `text/plain`; `/config`
returns only non-secret smoke metadata through source-generated JSON metadata.
The host packaging command is:

```powershell
dotnet run --project tools/OS.Tooling -- kestrel package
```

The command publishes the framework-dependent .NET 10 app into
`artifacts/kestrel-smoke/package-root/apps/minimal-api`, writes
`artifacts/kestrel-smoke/package.json` with the `kestrel.smoke.package.v0`
manifest shape, runs `OS.ManifestTool validate-kestrel-package --profile
SingleMinimalApi`, and performs a negative validation that rejects a manifest
missing `OS_SMOKE_EXPECTED_BODY`. The C# command emits `WORKLOAD_PROFILE
SingleMinimalApi` and passes `/p:OSWorkloadProfile=SingleMinimalApi` through its
`dotnet publish` call. Validation invokes the already-built `OS.ManifestTool`
DLL directly, so package checks do not rebuild that tool. Build `CLROS.slnx`
before using `--no-build` tooling. Expected host-only markers are:

```text
WORKLOAD_PROFILE SingleMinimalApi
TEST_PASS kestrel.smoke_app publishes package
TEST_PASS kestrel.smoke_app validates package contract
TEST_PASS kestrel.smoke_app rejects missing expected body
TEST_PASS kestrel.smoke_app
```

These markers are host publish and host package-contract validation evidence;
claim status still follows [docs/completion-gates.md Gate 14](completion-gates.md#gate-14-profile-split).

Issue #272 adds a host route/config contract test for the real published
fixture. The test publishes `apps/MinimalApiSmoke`, launches the published DLL
on a dynamic `127.0.0.1` port, verifies `GET /` and `GET /healthz` return the
configured `OS_SMOKE_EXPECTED_BODY` body as `text/plain`, verifies `/config`
exposes non-secret smoke metadata, and verifies an environment override changes
the response. The host test uses bounded raw loopback HTTP reads, connection
close, and chunked-body decoding so fixture failures do not leave the full
host-test log stream open. Expected host-only markers are:

```text
TEST_PASS kestrel.smoke_app host fixture publishes
TEST_PASS kestrel.smoke_app host healthz returns configured body
TEST_PASS kestrel.smoke_app host config exposes contract metadata
TEST_PASS kestrel.smoke_app host env override controls body
```

These markers are host fixture evidence under the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section.

`tools/OS.ImageBuilder` can stage that same host package root into a raw image
without changing the claim boundary:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- create-raw --efi .\artifacts\efi\BOOTX64.EFI --manifest .\artifacts\kestrel-smoke\package.json --kestrel-package .\artifacts\kestrel-smoke\package-root --output .\artifacts\images\kestrel-smoke.raw
```

This validates `kestrel.smoke.package.v0`, rejects `MultiMinimalApi`, converts
the package root into `OSPKGv0`, and places it at `\OS\APP.PKG` in the ESP. It
remains host artifact evidence under [docs/completion-gates.md Gate 14](completion-gates.md#gate-14-profile-split).

Optional fixture files, when the publish output contains them, must remain under the same package directory:

```text
apps/minimal-api/appsettings.Production.json
apps/minimal-api/MinimalApiSmoke.staticwebassets.endpoints.json
```

The package must not require writes, host filesystem paths, drive letters, absolute paths, traversal paths, HTTPS certificates, static-file hosting, reflection-based endpoint discovery, or host OS environment variables.

The host-side JSON manifest shape for this contract is:

```json
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api.smoke",
    "entry": "apps/minimal-api/MinimalApiSmoke.dll"
  },
  "args": [],
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Production",
    "OS_SMOKE_EXPECTED_BODY": "OS_MINIMAL_API_OK"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/minimal-api/MinimalApiSmoke.dll",
    "kestrel": {
      "listen": [
        {
          "scheme": "http",
          "host": "0.0.0.0",
          "port": 8080
        }
      ]
    }
  }
}
```

The future kernel/guest representation may be different from this JSON, but it must preserve these semantic fields: app id, hosted runtime kind, package-relative assembly path, zero or more args, manifest-provided environment/config values, HTTP listen endpoint, and serial logging policy.

Required guest serial and runner markers:

```text
TEST_START kestrel.smoke
KESTREL_PACKAGE_CONTRACT kestrel.smoke.package.v0
KESTREL_APP_START id=minimal.api.smoke assembly=apps/minimal-api/MinimalApiSmoke.dll
KESTREL_BIND_READY endpoint=http://0.0.0.0:8080
HTTP_PROBE_START endpoint=http://<guest-ip>:8080 expected_status=200 expected_body_bytes=17 timeout_seconds=10
HTTP_PROBE_PASS endpoint=http://<guest-ip>:8080 status=200 body_bytes=17 body_sha256=87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d elapsed_ms=<n>
TEST_PASS kestrel.smoke
TEST_FAIL kestrel.smoke reason=<code>
```

The runner must save the serial log, resolved guest endpoint, request path, response status, response body byte count, response body hash, elapsed time, and first failure marker. It must fail the smoke if `HTTP_PROBE_PASS`, `TEST_PASS kestrel.smoke`, or any prerequisite marker is missing.

Blocked prerequisites for this contract remain:

- Exported `OSHost_*` package file/config functions.
- Exported `OSHost_*` socket functions.
- Exported `OSHost_*` timer and thread/sync functions.
- Hosted .NET 10 runtime startup in guest.
- Scheduler/thread-pool support for hosted runtime work.
- TCP listener and socket accept/read/write/close behavior over the guest network stack.
- Manifest-to-hosted-runtime configuration propagation.
- QEMU/libvirt runner HTTP probing in real mode.

Classify host-only, mock-only, dry-run, local-loopback, native-tier, and
documentation-only results through the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals)
section and [docs/completion-gates.md Gate 14](completion-gates.md#gate-14-profile-split).

## Dependency Map

Kestrel is mandatory for the CloudReady hosted milestone, but it is a late milestone. The dependency order below prevents agents from moving ASP.NET work ahead of the boot, runtime, storage, network, and PAL work it needs.

1. Boot proof: QEMU or libvirt must boot the OS image and produce required boot markers from the guest serial path, not from a host dry run.
2. UEFI encapsulation: firmware services must be behind platform interfaces, required firmware data must be copied into OS-owned memory, and later stages must not depend on UEFI services after the boundary.
3. PAL ABI contract: `OSHost_*` names, POD structs, opaque handles, unmanaged buffers, status codes, and no managed references or exceptions across the boundary must be documented and layout-tested.
4. Runtime helpers: allocation, array and string allocation, write barriers, interface dispatch, class constructors, exception/unwind behavior, module initialization, thread statics, TypeManager/RTR scans, and GC stack roots must be implemented or explicitly blocked with fail-fast diagnostics.
5. Scheduler, threading, and synchronization: the hosted runtime must be able to create worker threads, block and wake waits, join or exit threads, run a thread pool, and avoid deadlocks during shutdown or panic.
6. Timers: monotonic ticks, tick frequency, sleep, one-shot timers, cancellation, and bounded timeout behavior must work before Kestrel request timeouts, keep-alives, and cancellation can be trusted.
7. Storage and package loading: the guest must read the hosted runtime, app assembly, dependencies, `appsettings*.json`, optional static files, and package metadata without firmware file services.
8. Manifest configuration: the guest manifest loader must select `runtime.kind: "dotnet10"`, app entry assembly, arguments, environment, listen endpoints, file/package roots, and log policy.
9. virtio-net: the OS must own the cloud NIC path with RX/TX queues, packet buffers, deterministic errors, and serial diagnostics for device or link failure.
10. DHCP, DNS, and TCP: CloudReady networking must cover address configuration, name resolution where hostnames are used, TCP listen/accept/read/write/close, retransmission, timeout, and malformed packet behavior. Current TCP work is still host-tested packet and frame-planner evidence only. A static-IP inbound-only smoke may defer DNS, but no hostname or outbound Kestrel scenario can claim support before DNS evidence exists.
11. PAL sockets: `OSHost_SocketCreate`, bind, listen, accept, send, receive, poll, shutdown, close, address, option, timeout, and cancellation contracts must be backed by guest behavior.
12. Hosted runtime boundary: a hosted .NET 10 runtime must launch a trivial managed app in guest through the PAL boundary before ASP.NET is attempted.
13. Minimal API app launch: the packaged ASP.NET Minimal API fixture must start in guest, load manifest/config/environment data, log through OS logging, and bind the configured HTTP endpoint.
14. Kestrel proof: an external HTTP client must receive and validate the deterministic response over the OS network path while serial and runner logs emit the required Kestrel markers.

No dependency may be skipped. The [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals)
section owns the local list of substitutions that cannot be reported as Kestrel
support.

## Prerequisite Ledger

| Area | Required Capability | Current status | Evidence required before Kestrel claim |
| --- | --- | --- | --- |
| Boot proof | Guest image reaches required serial boot markers under QEMU or libvirt | Partial EFI/QEMU smoke only | Real guest run emits boot markers from serial logs and fails when markers are absent |
| UEFI encapsulation | Firmware data is copied or abstracted before firmware services are unavailable | Partial guard and simulation only | Guest path proves OS-owned data and no accidental firmware dependency after the boundary |
| PAL ABI contract | Flat `OSHost_*` ABI for memory, time, console/log, file, threading, sync, exception/unwind, environment/config, and network where needed | Base POD declarations, socket ABI value contracts, a mock memory table plus memory facade, a mock socket create/bind/listen lifecycle table, mock partial I/O and peer close/reset semantics, a mock monotonic timer table plus timer facade, a mock wait/event table plus wait/event facade, a mock package-file handle table plus package facade, a mock config/environment table plus config/environment facade, and a mock stdout/stderr app-log table plus write-log facade are host-tested; functions are not exported or guest-tested | PAL symbols are implemented, status-coded, layout-tested, and exercised by hosted runtime smoke tests |
| Runtime helpers | Allocation, GC, class constructors, thread statics, exceptions/unwind, and runtime metadata support hosted app execution | Not implemented | Hosted app smoke covers allocation, static initialization, exceptions, and request lifetime churn |
| Hosted .NET 10 runtime | A hosted runtime path capable of loading and executing a .NET 10 application assembly | Not implemented | Guest launches a trivial managed `Hello.dll` through the hosted runtime |
| App package layout | Image/package format carries runtime bits, app assembly, dependencies, appsettings, static content if needed, and manifest metadata | Not implemented | Image builder packages the Minimal API fixture and manifest; guest can enumerate/read packaged files |
| File/config reads | Hosted app can read app assemblies and configuration files without firmware services | Mock package-file handle table/facade, package file-table enumeration, and mock config-value query table/facade only; no exported PAL file/config functions or guest package I/O exists | Guest-side file reads for app binary and config emit pass markers |
| Environment variables | Manifest-provided key/value data reaches the hosted runtime and app configuration | Kernel-facing native manifest parser carries bounded `env.KEY=value` metadata and a mock PAL environment query/enumeration table plus facade can be built from native launch-plan metadata; no exported PAL query or hosted propagation exists | Fixture endpoint returns a deterministic value sourced from manifest environment |
| Logging/stdout | Actual NativeAOT fd1/fd2 write/writev reaches owned copied serial delivery | Step1389 qualifies finite eight-worker output, EBADF and records64 quota EIO in real CLROS guests. Legacy OSHost/CoreCLR logging facades remain host-only; no general lifecycle or continuing-service claim | Preserve exact bytes, ownership and budget semantics as later startup/service scopes are qualified |
| virtio-net | Guest owns NIC discovery, RX/TX queues, packet buffers, and link diagnostics | Not implemented | Guest sends and receives packets through virtio-net with deterministic failure markers |
| Sockets | Hosted runtime sees socket-like APIs backed by OS networking or a documented PAL network boundary | Mock create/bind/listen/accept-queue/send/receive/poll/shutdown/options/endpoint-query table plus host-only backlog-to-mock-accept-queue and established-stream-to-mock-socket inbound bridges only; no exports, live TCP, scheduler waits, or guest behavior | Socket create/bind/listen/accept/send/receive path is guest-tested |
| TCP | OS networking provides TCP listen, accept, read, write, close, timeout, retransmission, and error behavior sufficient for Kestrel | Host-tested TCP packet, handshake, active/passive IPv4 frame-planner, established stream, close/reset, caller-owned listener backlog metadata, passive-backlog bridge, listener-backlog-to-mock-accept-queue bridge, and established-stream-to-mock-socket inbound bridge only; no complete listener, exported socket, retransmission, timer, or guest packet-I/O behavior exists | Guest accepts an inbound TCP connection and returns deterministic bytes |
| DHCP | Address configuration is available when the Kestrel smoke depends on dynamic network configuration | Not implemented | Guest obtains or rejects network configuration with explicit markers before endpoint bind |
| DNS if needed | DNS resolution exists for outbound fixture scenarios; inbound-only Kestrel proof may not require DNS | Not implemented | DNS pass marker exists before any Kestrel proof that depends on hostnames |
| Timers | Monotonic timers and sleeps support Kestrel timeouts, keep-alives, cancellation, and runtime scheduling | Host-tested mock monotonic timer table plus host-only timer facade status translation; no exported timer functions, hardware timer, interrupt, wait-handle completion, scheduler, sleep, `Task.Delay`, or guest behavior exists | Guest timer smoke proves monotonic ticks and delay/timeout behavior |
| Scheduler/threading | Managed threads, thread pool, synchronization, and safe blocking behavior exist for hosted runtime and Kestrel | Host-tested mock wait/event table plus host-only wait facade cover immediate manual-reset, auto-reset, wait-one, wait-any, cancellation, close, invalid/stale-handle, unsupported wait-all, non-immediate timeout rejection, default/zero event-create result rejection, null-table rejection, and capacity-exhaustion semantics only; no exported wait/event functions, blocking waits, scheduler, ThreadPool, `Monitor`, `Task`, or guest behavior exists | Guest thread/thread-pool smoke passes under repeated request load |
| Async/task/cancellation | `Task`, continuations, async method builders, timers, and cancellation tokens work for ASP.NET request handling | Not implemented | Async route or delayed response fixture passes without deadlock |
| Manifest integration | Manifest selects hosted .NET 10 runtime kind, app entry assembly, args, env, files, listen endpoint, and logging policy | Host-side validation exists; the kernel-facing native parser now accepts packaged native selection fields, carries bounded environment metadata, validates future PAL IPv4 bind endpoints against listen metadata in host tests, creates a mock listener only through host-only policy/table composition, and rejects reserved hosted runtime selections as unsupported | Guest manifest loader configures hosted runtime and Kestrel endpoint |
| Runner evidence | QEMU/libvirt runner can require Kestrel-specific markers and verify HTTP response content | Marker requirement support and a host-runner HTTP probe exist for QEMU; the probe is local-loopback tested, but no guest endpoint or Kestrel response evidence exists | Runner fails on missing marker, failed response, timeout, or unexpected content |

## Kestrel-Critical PAL Contract

`docs/pal.md` is the current contract authority for the first Kestrel-required `OSHost_*` ABI surface:

- Memory path: page allocation, free, protection, and query calls for hosted runtime page requests.
- Socket listen path: `OSHost_SocketCreate`, `OSHost_SocketBind`, `OSHost_SocketListen`, `OSHost_SocketAccept`, `OSHost_SocketSend`, `OSHost_SocketReceive`, `OSHost_SocketPoll`, shutdown, close, address, and option calls.
- Timer path: monotonic ticks, tick frequency, one-shot timers, timer cancellation, timer close, and sleep-until behavior.
- Thread/sync path: thread create/exit/join/yield/current-id, wait-one, wait-many, events, semaphores, mutexes, and memory barrier.
- File/config path: read-only package open/read-at/info/close/enumeration, manifest config values, environment values, and log bytes.

The ABI uses `OSHost_*` names, POD structs, opaque 64-bit handles, unmanaged pointers plus byte lengths, UTF-8 byte spans, and `OSHostStatus` return values. It does not allow managed references, exceptions, C++ objects, WinAPI handles, POSIX descriptors, or host-owned movable buffers across the boundary.

The socket slice for Gitea issue #261 defines POD request/output shapes for create, bind, listen, accept, connect, send, receive, shutdown, close, option, endpoint-query, and poll behavior. It also reserves `OSHOST_STATUS_CLOSED` for closed handle or closed connection paths. Listener access is denied by default; a hosted Minimal API app may bind only endpoints granted by manifest/supervisor policy.

The memory slice now has a host-only facade over `OSHostMemoryTable` that models future `OSHost_AllocPages`, `OSHost_FreePages`, `OSHost_ProtectPages`, and `OSHost_QueryMemory` status translation. It validates page size, alignment, capacity, stale handles, exact mock ranges, null outputs, default/zero allocation-result rejection, and W^X/protection transitions. It is not exported and does not allocate real pages, change page tables, integrate GC, or feed hosted runtime allocation.

The timer slice now has a host-only facade over `OSHostTimerTable` that models future `OSHost_TimeGetMonotonicTicks`, `OSHost_TimerCreate`, `OSHost_TimerArm`, `OSHost_TimerCancel`, `OSHost_TimerClose`, and timer-poll status translation. The host-only timer table rejects default/zero timeout, create, and poll results as success evidence. The host-only timer/wait bridge can preserve a completion wait handle and set a mock wait event when a due timer is polled. It is not exported and does not implement blocking waits, scheduler wakes, hardware timers, interrupts, `Task.Delay`, or Kestrel behavior.

The wait/event slice now has a host-only facade over `OSHostWaitTable` that models future `OSHost_EventCreate`, `OSHost_EventSet`, `OSHost_EventReset`, `OSHost_EventClose`, `OSHost_WaitOne`, and `OSHost_WaitMany` status translation for immediate waits. It validates event options, caller-owned wait-result outputs, wait-many handle-span metadata, null tables, invalid/stale handles, unsupported wait-all flags, unsupported non-immediate timeouts, default/zero event-create result rejection, and deterministic capacity exhaustion. It is not exported and does not block, park, or wake scheduler threads.

The package/config slice now has host-only facades over `OSHostPackageFileTable` and `OSHostConfigEnvironmentTable` that model future `OSHost_OpenPackageFile`, `OSHost_ReadFileAt`, `OSHost_GetFileInfo`, `OSHost_CloseFile`, `OSHost_EnumeratePackage`, `OSHost_GetConfigValue`, `OSHost_EnumerateConfig`, `OSHost_GetEnvironmentValue`, and `OSHost_EnumerateEnvironment` status translation. They validate POD request shapes, caller-owned buffers, stale handles, unsafe paths, missing files/keys, deterministic handle exhaustion, config/environment enumeration, launch-plan listen endpoint config projection, and required-byte reporting. They are not exported and do not read guest storage, feed hosted .NET configuration, or prove Kestrel.

This contract still needs fail-fast or `OSHOST_STATUS_NOT_SUPPORTED` exports where linking requires them, real memory/page behavior, scheduler-backed thread/wait behavior, socket timeout/cancellation paths, and guest behavior. Declaration-only and mock-only PAL work remains covered by the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section.

## Host-Build Fixture Evidence

Host-build fixture evidence is useful but not sufficient. It may include:

- `dotnet build` or `dotnet publish` for a .NET 10 ASP.NET Minimal API fixture.
- `dotnet run --project tools/OS.Tooling -- kestrel package` output showing the
  staged `package.json`, `apps/minimal-api` publish layout, contract validation,
  and missing-expected-body rejection markers.
- Host-side tests that verify source-generated JSON metadata, trimming/AOT warnings, fixture route shape, deterministic response text, PAL ABI layout, `OSHost` socket endpoint/status/timeout value contracts, mock memory allocation/protection/query behavior, mock socket create/bind/listen/accept-queue lifecycle behavior, mock socket partial I/O and peer close/reset behavior, mock timer behavior and timer-facade status translation, mock wait/event table behavior and wait-facade status translation, or mock package-file open/read-at/enumeration lifecycle behavior.
- `OS.ManifestTool validate-kestrel-package` checks for the `kestrel.smoke.package.v0` manifest and package-root layout, including required fixture files, hosted-runtime metadata, listen metadata, and `OS_SMOKE_EXPECTED_BODY`.
- Image-builder tests proving the fixture files and manifest are placed into the package.
- Runner dry-run output showing how the Kestrel smoke command would be launched.
- Runner dry-run output containing `HTTP_PROBE url=<url> expected_status=<code> expected_body_bytes=<bytes> timeout_seconds=<seconds>`.
- Runner local-loopback tests proving `HTTP_PROBE_PASS` and `HTTP_PROBE_FAIL` handling without printing expected or actual response bodies.
- Documentation checks that find `kestrel.smoke.package.v0`, `OS_MINIMAL_API_OK`, `OS_SMOKE_EXPECTED_BODY`, `TEST_START kestrel.smoke`, `TEST_PASS kestrel.smoke`, and `HTTP_PROBE_PASS`.

Host-build fixture evidence must be reported through the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section and [docs/completion-gates.md Gate 14](completion-gates.md#gate-14-profile-split).

## Claim Boundary And Non-Goals

The following evidence is useful but must not be described as Kestrel support:

- `dotnet build`, `dotnet test`, or `dotnet publish` for a Minimal API fixture on the host.
- A host build or host run of `apps/MinimalApiSmoke`, including a hard-coded or host-loopback response.
- Host package-contract validation, manifest validation for `runtime.kind: "dotnet10"`, kernel-facing parser tests for hosted runtime metadata, manifest socket bind-policy, mock bind-bridge tests, mock listen-bridge tests, or `tools/OS.ImageBuilder` staging of the Kestrel package into a raw image.
- A dry-run QEMU, libvirt, or HTTP probe command, including QEMU dry-run `HTTP_PROBE_*` text and local-loopback runner probe tests.
- A PAL declaration, layout test, caller-owned TCP listener backlog, passive-backlog bridge, listener-backlog-to-mock-accept-queue bridge test, mock memory-table allocation/protection test, mock socket-table lifecycle/accept-queue test, mock timer or wait/event facade test, mock package-file/config/environment table test including manifest listen config projection, mock app-log facade test, or `OSHOST_STATUS_NOT_SUPPORTED` export with no guest behavior.
- A native-tier `TinyHttpServer`, raw TCP demo, or simple HTTP demo. These can unblock packet, TCP, or HTTP work, but they do not prove hosted .NET 10 runtime startup, ASP.NET Minimal API execution, Kestrel socket integration, ASP.NET configuration/logging, or async request handling through hosted runtime primitives.
- A successful EFI/QEMU boot smoke that does not launch a hosted .NET app.
- Documentation-only `kestrel.smoke.package.v0` contract text.
- Provider-adjacent evidence outside the target gate, such as local QEMU/libvirt virtio evidence used as AWS Nitro or Azure VM Kestrel support, host-side AWS IMDSv2 request planners, cloud-init-free image docs, or Azure metadata/goal-state fixtures.

The first Kestrel smoke contract is not complete until the guest launches the
packaged app, binds `http://0.0.0.0:8080`, and the external runner receives
`OS_MINIMAL_API_OK` from `GET /healthz` with the required serial and runner
markers.

The first Kestrel milestone is plain HTTP only. TLS, HTTPS, certificates, ALPN, HTTP/2, HTTP/3, QUIC, WebSockets, and production hardening are non-goals until separate implementation issues add guest evidence.

## Runner Markers And Failure Conditions

Future QEMU/libvirt Kestrel runners must keep serial and runner output machine-readable. A valid smoke should include guest serial markers and runner-side HTTP probe diagnostics:

```text
TEST_START kestrel.smoke
HTTP_PROBE_START endpoint=http://<host>:<port> expected_status=200 expected_body_bytes=<bytes> timeout_seconds=<seconds>
HTTP_PROBE_PASS endpoint=http://<host>:<port> status=200 body_bytes=<bytes> body_sha256=<sha256> elapsed_ms=<n>
TEST_PASS kestrel.smoke
TEST_FAIL kestrel.smoke reason=<code>
```

`HTTP_PROBE_*` diagnostics are not a substitute for guest markers. In real mode the runner must fail the smoke when any of these occur:

- required serial marker is missing;
- guest emits `OS_SMOKE_FAIL`, any documented panic/fault marker, or `TEST_FAIL kestrel.smoke`;
- hosted runtime fails to start;
- packaged Minimal API app fails to launch;
- Kestrel does not bind the manifest endpoint;
- TCP connection cannot be established;
- HTTP status, required headers, body bytes, or body hash do not match the fixture contract;
- response is truncated, malformed, or arrives after timeout;
- runner cannot save serial log, endpoint details, request mode, response summary, elapsed time, and first failure marker.

Current dry-run output and local-loopback `HTTP_PROBE` tests are classified by
the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals) section.

## In-Guest Kestrel Evidence

In-guest Kestrel evidence must include all of the following:

- Serial log contains `TEST_START kestrel.smoke`.
- Guest PAL prerequisite logs contain `TEST_PASS pal.file.config`, `TEST_PASS pal.timer`, `TEST_PASS pal.thread.sync`, and `TEST_PASS pal.socket.listen`, or the Kestrel smoke proves those same paths in one documented run.
- Guest logs show hosted runtime startup and packaged Minimal API app launch.
- Guest logs show Kestrel binding the configured endpoint.
- Runner performs an HTTP request from outside the guest or from a documented peer VM path.
- Response status, headers needed by the test, and body match the deterministic fixture contract.
- Serial log contains `TEST_PASS kestrel.smoke`.

This evidence is the minimum `SingleMinimalApi` claim boundary. A
`MultiMinimalApi` claim must keep all of the above and add at least two
manifest-launched ASP.NET Minimal API apps, distinct listen endpoints,
per-app lifecycle/log/config identity, process or equivalent isolation,
supervisor restart/exit-status behavior, port-conflict handling, and proof that
one app can fail while another continues serving HTTP responses.
- Runner exits nonzero and emits `TEST_FAIL kestrel.smoke reason=<code>` if bind, request, response validation, timeout, or app launch fails.

## Socket Compatibility Test Ladder

`docs/networking.md` owns the detailed Kestrel socket compatibility checklist. The required ordering is:

1. Packet encode/decode tests for Ethernet, ARP, IPv4, UDP, DNS/DHCP where relevant, and TCP headers/options.
2. Raw TCP state-machine tests for listen, accept handshakes, sequence/ack validation, reassembly, retransmission, close, timeout, and backpressure behavior.
3. PAL socket tests for `OSHost_SocketCreate`, bind, listen, accept, send, receive, shutdown, close, poll, options, timeout, and cancellation semantics.
4. App-level Kestrel HTTP tests that launch the packaged Minimal API fixture in guest and validate HTTP response behavior through the external runner.

The Kestrel socket compatibility smoke must cover bind/listen/accept, concurrent clients, partial reads, partial writes, connection close, timeouts, cancellation, backpressure, large response bodies, split request headers, and malformed request handling. Required app-level markers are:

```text
TEST_START kestrel.socket.compat
TEST_PASS kestrel.socket.compat
TEST_FAIL kestrel.socket.compat reason=<code>
```

`TEST_PASS kestrel.socket.compat` is valid only when the guest-hosted Kestrel
path passes over the OS TCP/PAL/socket stack. Host-only, mock-only, native-tier,
and protocol-only substitutions remain covered by the [Claim Boundary And Non-Goals](#claim-boundary-and-non-goals)
section.

## Unsupported Behavior

- Hosted .NET 10 runtime execution.
- CoreCLR PAL implementation.
- Exported or guest-tested `OSHost_*` memory, socket, timer, thread/sync, and file/config behavior.
- Exported or guest-tested PAL socket accept/send/receive/poll/shutdown behavior.
- Kestrel socket binding.
- Complete TCP listener/socket support.
- In-guest ASP.NET Minimal API launch.
- Appsettings/config/environment propagation into hosted .NET. Kernel-facing environment/listen metadata and host-tested PAL config/environment mock queries exist, but no exported PAL query or hosted-runtime propagation path exists yet.
- Guest runner-level HTTP response verification. The QEMU runner can perform a real host HTTP probe, but it has only local-loopback host evidence and no OS guest or Kestrel endpoint evidence yet.
- Kestrel request concurrency, cancellation, TLS, HTTP/2, HTTP/3, WebSockets, static files, or production hardening.

Do not claim TLS, HTTPS, HTTP/2, HTTP/3, or WebSocket support until those features have separate guest evidence.

## Tests

This ledger now references host-tested PAL socket declarations, mock memory-table/facade evidence, mock socket-table lifecycle/accept-queue evidence, a mock `OSHost_*`-shaped socket facade, and mock package-file handle-table evidence, but it remains documentation plus host contract evidence only. Current mock-only PAL marker names include:

- `TEST_PASS pal.socket table creates binds and listens`
- `TEST_PASS pal.socket table rejects invalid lifecycle`
- `TEST_PASS pal.socket table reports deterministic exhaustion`
- `TEST_PASS pal.socket table create default result is not success`
- `TEST_PASS pal.socket accept queues connection`
- `TEST_PASS pal.socket accept rejects non-listener`
- `TEST_PASS pal.socket accept reports empty queue`
- `TEST_PASS pal.socket accept default result is not success`
- `TEST_PASS pal.socket send receive transfers bytes`
- `TEST_PASS pal.socket partial receive preserves unread bytes`
- `TEST_PASS pal.socket partial send reports backpressure`
- `TEST_PASS pal.socket peer close returns eof after draining`
- `TEST_PASS pal.socket peer reset reports connection reset`
- `TEST_PASS pal.socket poll reports close and reset readiness`
- `TEST_PASS pal.socket poll reports readiness`
- `TEST_PASS pal.socket poll default result is not success`
- `TEST_PASS pal.socket shutdown unblocks io`
- `TEST_PASS pal.socket send rejects invalid state`
- `TEST_PASS pal.socket options store kestrel flags`
- `TEST_PASS pal.socket options reject unsupported values`
- `TEST_PASS pal.socket endpoints report bound local address`
- `TEST_PASS pal.socket endpoints report accepted peer address`
- `TEST_PASS pal.socket reuse address policy is deterministic`
- `TEST_PASS pal.socket facade creates binds and listens`
- `TEST_PASS pal.socket facade accepts with endpoint output`
- `TEST_PASS pal.socket facade preserves partial io`
- `TEST_PASS pal.socket facade polls close and reset states`
- `TEST_PASS pal.socket facade translates invalid handles`
- `TEST_PASS pal.socket facade close invalidates handle`
- `TEST_PASS pal.socket abi default operation result is not success`

Current TCP-to-mock-socket bridge markers include `TEST_PASS net.tcp.listener.socketbridge enqueues accepted connection`, `TEST_PASS net.tcp.listener.socketbridge rejects empty backlog`, `TEST_PASS net.tcp.listener.socketbridge propagates non-listener failure`, `TEST_PASS net.tcp.listener.socketbridge preserves accepted endpoint metadata`, `TEST_PASS net.tcp.listener.socketbridge preserves mock-only boundary`, `TEST_PASS net.tcp.stream.socketbridge queues inbound payload`, `TEST_PASS net.tcp.stream.socketbridge rejects failed receive result`, `TEST_PASS net.tcp.stream.socketbridge rejects payload length mismatch`, `TEST_PASS net.tcp.stream.socketbridge propagates mock socket failure`, and `TEST_PASS net.tcp.stream.socketbridge preserves mock-only boundary`. These markers are host-only prerequisite evidence, not Kestrel support.

Current manifest-to-mock-listener markers include `TEST_PASS appmodel.socket.listenbridge creates manifest listener`, `TEST_PASS appmodel.socket.listenbridge rejects policy mismatch`, `TEST_PASS appmodel.socket.listenbridge propagates create failure`, `TEST_PASS appmodel.socket.listenbridge propagates bind failure`, `TEST_PASS appmodel.socket.listenbridge propagates listen failure`, and `TEST_PASS appmodel.socket.listenbridge preserves mock-only boundary`. These markers are host-only prerequisite evidence, not Kestrel support.
- `TEST_PASS pal.memory table allocates aligned pages`
- `TEST_PASS pal.memory table rejects invalid request shape`
- `TEST_PASS pal.memory table reports capacity exhaustion`
- `TEST_PASS pal.memory table reuses freed ranges and rejects stale handles`
- `TEST_PASS pal.memory table protects write then execute`
- `TEST_PASS pal.memory table rejects stale and partial ranges`
- `TEST_PASS pal.memory table layouts are stable`
- `TEST_PASS pal.memory table default result is not success`
- `TEST_PASS pal.memory facade translates alloc free protect query`
- `TEST_PASS pal.memory facade rejects null outputs and table`
- `TEST_PASS pal.timer monotonic snapshot is stable`
- `TEST_PASS pal.timer one shot arms and fires`
- `TEST_PASS pal.timer stores completion wait handle`
- `TEST_PASS pal.timer cancel prevents fire`
- `TEST_PASS pal.timer periodic advances deterministically`
- `TEST_PASS pal.timer invalid handles are rejected`
- `TEST_PASS pal.timer default results are not success`
- `TEST_PASS pal.timer facade returns monotonic snapshot`
- `TEST_PASS pal.timer facade creates and arms one shot`
- `TEST_PASS pal.timer facade arms completion wait handle`
- `TEST_PASS pal.timer facade cancels and closes handles`
- `TEST_PASS pal.timer facade translates invalid handles`
- `TEST_PASS pal.timer facade reports exhaustion`
- `TEST_PASS pal.timer.waitbridge signals completion event`
- `TEST_PASS pal.timer.waitbridge leaves pending timer unsignaled`
- `TEST_PASS pal.timer.waitbridge rejects missing completion handle`
- `TEST_PASS pal.timer.waitbridge propagates wait set failure`
- `TEST_PASS pal.timer.waitbridge preserves mock-only boundary`
- `TEST_PASS pal.timer.waitbridge default result is not success`
- `TEST_PASS pal.wait manual reset event stays signaled`
- `TEST_PASS pal.wait auto reset consumes signal`
- `TEST_PASS pal.wait many reports first signaled`
- `TEST_PASS pal.wait reset and close reject invalid`
- `TEST_PASS pal.wait invalid options are rejected`
- `TEST_PASS pal.wait default results are not success`
- `TEST_PASS pal.file opens package file`
- `TEST_PASS pal.file reads offset ranges`
- `TEST_PASS pal.file rejects unsafe paths`
- `TEST_PASS pal.file reports deterministic exhaustion`
- `TEST_PASS pal.file enumerates package files`
- `TEST_PASS pal.file enumeration reports buffer too small`
- `TEST_PASS pal.file default results are not success`
- `TEST_PASS pal.config returns manifest config value`
- `TEST_PASS pal.config reports buffer too small`
- `TEST_PASS pal.env returns manifest environment value`
- `TEST_PASS pal.env enumerates environment entries`
- `TEST_PASS pal.env rejects invalid queries`
- `TEST_PASS pal.config table builds from native manifest`
- `TEST_PASS pal.config table exposes launch fields`
- `TEST_PASS pal.config table exposes manifest listen endpoint`
- `TEST_PASS pal.config table exposes listen host and port`
- `TEST_PASS pal.config facade returns manifest listen endpoint`
- `TEST_PASS pal.config table omits listen endpoint when absent`
- `TEST_PASS pal.config table preserves host-only boundary`
- `TEST_PASS pal.env table exposes manifest environment`
- `TEST_PASS pal.config table rejects invalid manifest slices`
- `TEST_PASS pal.config table default results are not success`
- `TEST_PASS pal.app_log facade writes stdout`
- `TEST_PASS pal.app_log facade writes stderr`
- `TEST_PASS pal.app_log facade rejects invalid stream`
- `TEST_PASS pal.app_log facade rejects oversized write`
- `TEST_PASS pal.app_log facade reports exhaustion`
- `TEST_PASS pal.app_log facade preserves machine marker flag`

The current expected verification for this documentation slice is:

```powershell
rg -n "Gitea issue #59|Gitea issue #149|kestrel.smoke.package.v0|OS_MINIMAL_API_OK|OS_SMOKE_EXPECTED_BODY|TEST_START kestrel.smoke|TEST_PASS kestrel.smoke|HTTP_PROBE_PASS|Claim Boundary And Non-Goals|docs/completion-gates.md Gate 14|## Dependency Map|Boot proof|UEFI encapsulation|PAL ABI|Runtime helpers|scheduler|virtio-net|DHCP|DNS|TCP|dry-run|host-only" .\docs\kestrel.md .\docs\completion-gates.md
git diff --check -- .\docs\kestrel.md
```

For the socket compatibility checklist, use:

```powershell
rg -n "Kestrel Socket Compatibility Test Plan|Layer 1 - Packet Encode/Decode|Layer 2 - Raw TCP State Machine|Layer 3 - PAL Socket Tests|Layer 4 - App-Level Kestrel HTTP Tests|TEST_START kestrel.socket.compat|TLS, HTTPS" .\docs\networking.md .\docs\kestrel.md
git diff --check -- .\docs\networking.md .\docs\kestrel.md .\done\step022.md
```

The host package-contract validator is covered by `tests/OS.HostTests/KestrelSmokePackageContractTests.cs` and emits:

```text
TEST_PASS kestrel.smoke_app host fixture publishes
TEST_PASS kestrel.smoke_app host healthz returns configured body
TEST_PASS kestrel.smoke_app host config exposes contract metadata
TEST_PASS kestrel.smoke_app host env override controls body
TEST_PASS kestrel.package.contract validates layout
TEST_PASS kestrel.package.contract rejects missing files
TEST_PASS kestrel.package.contract requires expected env
TEST_PASS kestrel.package.contract rejects unsupported profile
TEST_PASS kestrel.package.contract rejects unsupported runtime
```

## Known Limitations

This document is a prerequisite ledger. The memory table/facade and package-file table are host-only PAL mocks; they do not change the Kestrel claim boundary, exported ABI state, guest memory/page-table state, guest storage state, or networking state.

The complete single-app milestone still needs the declared service, time and
resource evidence; CloudReady remains unqualified.

## Next Steps

Step1419 qualifies the bounded #220-#223 socket/health/UTC ladder after step1405
startup/vectors. General service and shutdown/reap remain separate from these
finite prefixes. Step1420 corrects the current handoff and adds #766's reproduced
empty-send boundary; no runtime fix is included in that review.

1. Preserve the accepted memory/descriptor/thread/signal/runtime scopes through [step1307](../done/step1307.md).
2. Preserve [step1380](../done/step1380.md) configuration/containment and [step1389](../done/step1389.md) finite logging under continuing integration #737.
3. Under #737, complete #743/#744/#746/#240/#757/#758 and #766 before #224 repeated service. The empty-send correction follows accepted #745; preserve genuine reset/shutdown errors and nonempty backpressure.
4. Complete #760/#747/#748 and the exact #225 async/churn prerequisites, then #226/#227. Retain fresh guest boots, external `/healthz`, cancellation/shutdown, resource reuse and stopped-guest refusal at the appropriate gate. Earlier finite receipts do not close these service requirements.
5. Preserve `kestrel.smoke.package.v0` and its host checks for later framework-dependent hosting; those contracts do not block the NativeAOT-first path.

Time-API evidence correction: step1419's `realtime/gettimeofday` layout wording
must be read as the implemented syscall228 `clock_gettime(CLOCK_REALTIME)` and
x64 `timespec`. Syscall96 `gettimeofday` is explicitly unsupported and tested as
`ENOSYS`; neither a `timeval` layout nor a fallback is qualified. The immutable
receipt is preserved, with this correction recorded in step1420 and #759.
