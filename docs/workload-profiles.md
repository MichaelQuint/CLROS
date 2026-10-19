# Workload Profiles ADR

## Status

Accepted for planning. This document defines workload profile names and gate
semantics only. It does not add runtime, process, network, storage, hosted
.NET applications, CoreCLR, ASP.NET Minimal API, or Kestrel evidence.

[Step1419](../done/step1419.md) separately qualifies bounded #220/#221/#222/
#759/#223: 5,676 unchanged full-host checks/757 fixtures (94 Native), six strict
web/quota prefixes, three policy/vector guests and eight affected ancestors.
Socket ladder/policy, exact health, stopped-guest refusal and plausible advancing
external HTTP Date pass with the declared finite UTC contract and original
source/native joins. Web workers remain active until runner termination; general
service, whole-stack, numbered, isolation, cloud and production remain incomplete.
Continue the six claimed service foundations and M16-022's empty-send correction
below before #224; preserve shared owner #737.

[Step1426](../done/step1426.md) adds the separate `MinimalApiService` fixture
and canonical envelope for this continuing batch. The envelope requires the
explicit v1 service argument/configuration,8080/content root,64MiB and16
threads/descriptors. [Step1427](../done/step1427.md) records its initial static-musl
publication and exact source/ELF/complete-index reader:1,356,654 instructions,
198 syscall continuations and all five executable sections are checked. A strict
status reader treats application reports as untrusted telemetry. These are
development prerequisites; the service worker has not executed. The ordinary
pinned web application stays unchanged. [Step1428](../done/step1428.md) adds the
distinct native service startup/listening observations, cumulative worker call
ordinals and a bounded external peer protocol. Its24 focused host checks include
a real peer-child refusal against a synthetic local endpoint. The service worker
and successful long-running peer protocol have not executed. The guest pipeline
retains its runner guard until complete service replay, transport controls and
lifecycle/packet checks exist. The seven owned issues remain open; no acceptance
gate advances.

[Step1429](../done/step1429.md) adds explicit service random controls: two16384-byte
requests permit the adapter's8192-byte partial result while checking the untouched
suffix; zero-length, invalid-flag8 and NULL-buffer calls check exact errors.
Separate service readers retain cumulative call/readiness ordinals, allow bounded
128MiB serial framing and check random metadata/counters/erasure against original
calls. Finite readers keep their8MiB/4096-call bounds. The revised fixture requires
its own publication and complete instruction index, retained in step1429.
These components do not complete scheduling, packet or resource-lifetime replay;
the runner guard and all seven open issue states remain.

[Step1430](../done/step1430.md) implements the combined source-sealed service
inner-leaf reader, reusing complete syscall/memory/thread/signal/wait/descriptor
replay through group exit and off-stack reaps. Dedicated startup and compact
TIME-WAIT/ISN-owner readers join original copied output, exact generations,
retained deadlines and bounded high-water snapshots. It also corrects the
native listener observer to match the original app's full fd2 message.
Forty-one focused host checks and three EFI C# profile builds pass. The combined
reader has no successful complete capture yet; parent/packet/peer/runner joins,
transport controls, native ABI review and real batch qualification remain.
The guard stays in place and all seven issues remain open/claimed.

[Step1307](../done/step1307.md) separately qualifies the bounded #191/#192 through
#688 thread/runtime batch: 5,202 full-host checks, 44 strict batch captures across 32
profiles, three retained strict GC repetitions and 24 fresh ancestors. The exact
unchanged pinned Hello and NativeAOT runtime workloads now execute in CLROS.
[Step1332](../done/step1332.md) separately qualifies finite continuing network/TCP:
5,341 full-host checks, three strict TCP guests and20 affected ancestors, with
retained queues and a real cooperative progress/cleanup witness.
[Step1351](../done/step1351.md) qualifies finite Linux sockets/epoll and the
web builder/V2 staging prerequisites: 5,433 full-host checks, three sync/three
async socket guests, three V2 staging guests and33 strict ancestors. Later
steps1405/1419 qualify the finite #219/#745/#220 ladder; full service is separate.
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
exit60; ordinary IRQ yielding and ownership guards remain. #219 startup remains
unaccepted because constructor creation precedes its failure-marker try. Continue
#219 -> #745 vectors -> #220 under #737, including #759 realtime before #223.
No general Kestrel, continuing-service, whole-stack, numbered, cloud or production
gate advances. See the step for exact commands, hashes and retained failures.

## Architecture review implementation order (2026-10-16)

This order incorporates the review of accepted source
`e5bd99e6c8202988810cc479b92f1c51ef695e2d`, plus a second pass at master
`7c297c3f` with unchanged runtime source. Both passes' host reproductions and
characterizations identify follow-up work; they do not revoke step1351's finite
qualification or qualify a repair.
[PR739](http://192.168.4.35:3000/admin/CLROS/pulls/739), observed at
`e759827ce349bcb19895cbe269f08feac3fc72c9`, reports development guest HTTP
responses. Reinspection of retained r12 packets finds Unix-epoch Date headers;
owned realtime is an external-HTTP acceptance prerequisite. Step1380 now
qualifies the selected configuration/network slice; step1419 adds bounded UTC/HTTP, while the remaining
continuing-service ladder stays open. Refresh current master and the tracker
before implementation. Exact dependencies, owned
paths, commands and acceptance are in `.gitea/backlog/clros-issues.json`.

| Acceptance stage | Implementation order and required issue work |
| --- | --- |
| Qualified application startup | Steps1380/1389/1405 retain #217/#218/#219/#745 and containment prerequisites; step1419 adds bounded #220/#221/#222/#759/#223. Continue the six service foundations under #737 before #224. |
| Qualified socket ladder #220-#222 | Step1419 adds actual multi-segment Kestrel responses and same-connection progress across timeout/rejected-frame controls, manifest endpoint policy negatives, exact health and stopped-guest refusal. Preserve the accepted vector/epoll and bounded-prefix restrictions. |
| Qualified external HTTP #223 | Step1419 qualifies [M16-019 owned realtime](http://192.168.4.35:3000/admin/CLROS/issues/759), observed Date intervals/tolerance, exact status/body/hash/deadline and stale/epoch rejection. The UTC-RTC/HPET source and finite lifetime remain explicit; repeated service follows separately. |
| Repeated service #224 | After #223, qualify [M16-014 continuing execution policy](http://192.168.4.35:3000/admin/CLROS/issues/746) and [#240 replenished randomness](http://192.168.4.35:3000/admin/CLROS/issues/240). Require [M07-032 pending reset cleanup](http://192.168.4.35:3000/admin/CLROS/issues/757) -> [M07-033 continuing ISNs](http://192.168.4.35:3000/admin/CLROS/issues/758), plus [M07-029 zero-window persist](http://192.168.4.35:3000/admin/CLROS/issues/743) -> [M07-030 compact TIME-WAIT](http://192.168.4.35:3000/admin/CLROS/issues/744). Limits remain explicit; raising finite counters is insufficient. |
| Async/memory churn #225 and local Gate 14 | [M16-020 pending signals before renewed waits](http://192.168.4.35:3000/admin/CLROS/issues/760) closes the consumed-readiness race before churn. [M16-015 fragmented physical backing](http://192.168.4.35:3000/admin/CLROS/issues/747) -> [M16-016 GC/native budgets](http://192.168.4.35:3000/admin/CLROS/issues/748) also feed #225. Then preserve #226 evidence and close #227's local gate only against its complete criteria. |
| Two protected local applications | After local Kestrel, [M16-017 CPL3 #UD/#DE containment](http://192.168.4.35:3000/admin/CLROS/issues/749) and [M16-021 SIGPIPE disposition consistency](http://192.168.4.35:3000/admin/CLROS/issues/761) precede #292's general terminate/reap acceptance. Retain the explicit ignored-SIGPIPE restriction until qualified. #291 extends M16-016's resource model; #298 still needs a second protected app surviving failure. Keep the existing M14 process/address-space/descriptor/fairness ladder. |
| Measured performance | After the local gate, [M15-016 matched workload baselines](http://192.168.4.35:3000/admin/CLROS/issues/750) feeds [M07-031 congestion-controlled flight](http://192.168.4.35:3000/admin/CLROS/issues/751), [M16-018 mutation-scoped audits](http://192.168.4.35:3000/admin/CLROS/issues/752), and existing [#242 bounded release logging](http://192.168.4.35:3000/admin/CLROS/issues/242). Existing #308 retains sustained production load qualification. |
| C# build-time assurance and specialization | After the local gate, [M11-019 Roslyn effects/context/ownership checks](http://192.168.4.35:3000/admin/CLROS/issues/753) -> [M11-020 independently checked ABI generation](http://192.168.4.35:3000/admin/CLROS/issues/754). ABI generation also waits for M16-017. [M11-021 capability/resource generation](http://192.168.4.35:3000/admin/CLROS/issues/755) then consumes the per-process service boundary #294 and M16-016 budgets. These tools do not block first web or two-app qualification. |

The local Gate 14 leaf is **M10-014/#227**, following M10-013/#226;
milestone and issue numbers are different namespaces. The manifest dependency
graph is authoritative. Do not choose a later optional ticket by numeric order.
The October18 review adds [M16-022/#766](http://192.168.4.35:3000/admin/CLROS/issues/766)
after accepted M16-013 and before #224: valid empty messages must not wait for
send capacity. Integrate it under #737 without displacing the six claimed
continuing-service foundations. This is a reproduced host defect, not a fix.
Existing #160 packet corpus, #240 entropy, #242 logging, #291 budgets, #298
crash isolation and #308 sustained load are reused rather than duplicated.

Claimed shared ownership remains with #737 and the live application tickets.
New issue readiness does not authorize concurrent edits to that lane. Preserve
unrelated body addenda and refreshed content versions during tracker sync.
Implement focused slices, then qualify one stable related guest batch with
original native/source joins and affected ancestors. Host checks, source
analysis, documentation or a longer diagnostic timeout cannot close a guest
issue. See [review evidence](repo-audit.md#october-16-architecture-review) and
[completion gates](completion-gates.md#architecture-review-acceptance-boundaries).

## Context

CLROS has two related but different workload targets:

- A near-term cloud appliance target that runs one configured ASP.NET Minimal
  API workload.
- A later provider platform target that can run multiple isolated ASP.NET
  Minimal API workloads on one guest.

The current implementation includes host-tested contracts and accepted bounded
EFI/QEMU firmware-exit, storage and network diagnostics. Continuing services,
an upstream .NET application runtime, Kestrel, isolated application processes,
general application scheduling and an app supervisor remain unqualified. See completion-gates.md
for the accepted boundaries; bounded diagnostics do not establish app hosting.

[Step1201](../done/step1201.md) accepts one default-off mechanical ELF CPL3
write/exit excursion and controlled faults with full owned-state restoration.
It does not qualify the upstream NativeAOT Hello milestone, thread/TLS/futex
services, application GC/EH, a continuing workload or CLROS Kestrel.

## Decision

**Toolchain and workload version (2026-10-13):** .NET 11 is the selected OS,
tooling and guest-app target. [Step1225](../done/step1225.md) qualifies the exact
RC2 toolchain/ABI migration and fresh Linux-host payloads at implementation
source `d728d2c`; [host requirements](host-requirements.md) owns the pins and
CPU contract. C# remains 14. The architecture and issue order below remain
NativeAOT-first. Preserve .NET 10 measurements as historical inputs; only
explicitly rerun profiles gain current qualification. Subsequent finite guest
Hello/runtime qualification is linked above; guest Kestrel remains unqualified.

**Superseded ordering (2026-10-12):** the NativeAOT-first decision below is
authoritative. Older hosted-runtime descriptions preserve historical contracts,
not a requirement to finish CoreCLR before Kestrel or cloud providers before
local process isolation.

Define two workload profiles:

- `SingleMinimalApi`: the near-term default CloudReady workload profile. It
  targets one configured ASP.NET Minimal API/Kestrel workload and intentionally
  excludes multi-process requirements such as a process table, per-process
  address spaces, per-app isolation, and a multi-app supervisor. It still
  requires the Kestrel prerequisites: NativeAOT runtime boundary, package/config
  access, sockets, timers, threading/synchronization, and live network I/O.
- `MultiMinimalApi`: the later multi-app platform profile. It requires real
  process, isolation, scheduler, and supervisor semantics before it can be
  claimed. It is not a microkernel claim, and it is not implemented today.

`SingleMinimalApi` is the default implementation path until a specific issue
selects `MultiMinimalApi`.

## NativeAOT-first execution roadmap (2026-10-12)

Accepted architecture direction with separate bounded qualification receipts:
step1307 qualifies unchanged upstream Hello/runtime workloads and step1332
qualifies finite network/TCP. M16-001/#679 through M16-007/#685 are qualified within the bounded
evidence below. [Step1208](../done/step1208.md) closes the live openat coverage gap
identified in [step1207](../done/step1207.md). #708/#711's reproduced
`brk`/fixed-mapping ownership defect is repaired and qualified in step1209.
The host-only saved-context layout #183 is qualified in
[step1210](../done/step1210.md) with nine focused checks. #184's caller-owned
FIFO queue is qualified in [step1211](../done/step1211.md) with nine focused
checks. #185's injected-tick deadline queue is qualified in
[step1212](../done/step1212.md) with ten focused checks. #186/#719's default-off
cooperative transfer is qualified in [step1218](../done/step1218.md): 4,742
full-host checks, current original native review, three fresh 99-record guests
with seventeen transfers/restoration/cleanup and seven ancestors pass.
#187 and its children qualify four bounded hardware timer interrupts in
[step1224](../done/step1224.md): 4,776 full-host checks, six original native
bodies, three fresh 115-record guests and ten ancestors pass. That diagnostic
restores its original context after one safe wake; it is not a continuing
lifecycle owner. [Step1230](../done/step1230.md) qualifies
[#188](http://192.168.4.35:3000/admin/CLROS/issues/188): bounded cooperative workers
with repeated hardware timer wakes, serialized waits and off-stack reclamation.
Its 4,806 full-host checks, scoped original native review, three byte-identical
177-application-record guests and 15 fresh ancestors pass at source `e0dac79`.
See [the lifecycle contract](pal.md#qualified-bounded-lifecycle-188). Seven real
ticks, 38 transfers and four reaps return all 23 internal page leases; the
32-page firmware reservation remains owned. This does not advance the upstream
application-runtime milestone.
#191 wait-on-address and #192 application FS/TLS are prerequisites of #686
alongside accepted #685; step1307 qualifies that complete later runtime batch.
Exact managed dependencies control the continuing network/TCP/socket sequence.
These accepted .NET 10 checkpoints retain their historical toolchain identity.
[Step1225](../done/step1225.md) adds a finite .NET 11 qualification: 29 strict
QEMU captures, including three timer reruns and the expected unhandled negative,
plus original native review and the full-host gate. It does not renew every
historical profile. Step1230's later .NET 11 matrix qualifies only the bounded
lifecycle and named ancestors. Step1307 adds the later wait/TLS/Linux/runtime
qualification, retaining the historical scope of both earlier receipts.
The #708/#711 qualification in [step1209](../done/step1209.md) adds explicit
heap reservation ownership. General fixed mappings and unmaps still operate;
they relinquish heap ownership, so brk cannot silently reclaim a replacement
or hole. Changes require the entire old/new active heap prefix to remain owned.
Shrink decommits complete pages while retaining sparse ownership and dump policy;
same-page changes retain existing protection/backing. Failed growth/shrink leaves
break, live metadata, tables, backing and unrelated bytes unchanged. 4,688 fresh
full-host checks across 613 fixtures (94 Native), scoped original ownership/ABI
review, three fresh mixed-operation guests, the exact foreign-readonly fault
control and five fresh memory/descriptor ancestors pass. This qualifies the
bounded ownership repair; broad Linux compatibility and upstream acceptance remain
incomplete. Historical memory evidence and its original failure remain recorded.
The [foundation epic #678](http://192.168.4.35:3000/admin/CLROS/issues/678)
links #679-#690; [#691](http://192.168.4.35:3000/admin/CLROS/issues/691)
and #148-#153 have finite network/TCP qualification in step1332. #689 exposes
actual Linux sockets and #690 the real socket engine's epoll readiness. Refresh
the tracker before implementation.
Tracker migration and cleanup evidence are in
[step1196](../done/step1196.md).

Run one pinned upstream .NET 11 NativeAOT Minimal API through a restricted
Linux x64 syscall ABI implemented in the C# kernel, then two separately
packaged applications in hardware-protected processes. Preserve the upstream
runtime, GC, EH and musl inside each application payload. Kernel and application
heaps, roots, metadata and exception machinery remain separate.

Use the existing `linux-musl-x64` target and controlled publish recipe before
considering a custom RID/runtime fork. First-party kernel/std/apps remain
C#14/.NET11; attributed upstream native dependencies may be built externally
and packaged with apps. Reference snapshots remain read-only, outside build
inputs. Preserve `OSHost_*` and OSHostStatus; Linux register/errno/fd semantics
are a distinct adapter over common kernel services, not changes to that ABI.

| Stage | Managed keys / existing issues | Required result |
| --- | --- | --- |
| Publish first | M16-001 | Linux builder; static Hello, runtime exerciser and real Minimal API; ELF/native dependency and syscall/source review. |
| Load and continue | M16-002 through M16-007 | Immutable staging, continuing post-exit loop, mechanical ELF64 startup/user boundary, genuine memory and descriptor services; no upstream runtime acceptance. |
| Threads and runtime | #183-#194, M16-008 through M16-010 | Scheduler/TLS/futex, signal ABI and first real pinned Hello in M16-009/#687; focused suspension and comprehensive app GC/EH/ThreadPool/timer qualification follow. |
| Continuing network | M07-026, #148-#153 | Persistent Virtio RX/TX ownership, ARP and passive TCP with timing/retransmission/partial I/O/close. |
| Socket integration | M16-011, M16-012 | Linux sockets and actual epoll readiness used by System.Net.Sockets. |
| First web acceptance | #214-#227 | Real guest Kestrel /healthz with independent external HTTP evidence. |
| Two applications | #286-#301 | Separate page tables/private resources/quotas; one app fails while the other keeps serving. |
| Later targets | M12/M13, CoreCLR-specific #204-#212 | Provider devices/qualification and framework-dependent workloads; Roslyn/PowerShell remain later. |

Numeric milestone order is not execution order. Scheduling no longer waits
for HTTP/custom application EH; local multi-process work no longer waits for
AWS/Azure. The durable dependency source is
`.gitea/backlog/clros-issues.json`. Accepted bounded diagnostics remain valid;
their existence does not prove continuing services.

M16-007/#685 qualifies descriptor and immutable-file behavior with mechanical
fixtures. [Step1208](../done/step1208.md) records 4,679 fresh full-host checks,
unchanged original native review, six repeated guests with actual openat routes,
and fifteen revalidated retained ancestors. Step1206 preserves the initial
openat omission. It must not require real NativeAOT Hello before M16-008/#686 threads
and M16-009/#687 signals exist: the pinned Hello trace starts finalizer and
signal-handler threads before writing its greeting. #686 uses mechanical
clone/TLS/futex fixtures and preserves each thread's signal mask; it does not
require a .NET thread fixture. After implementing signal startup/delivery,
#687 owns the unchanged pinned Hello's first guest execution before focused
suspension qualification. M16-010/#688 repeats Hello as a regression and adds
the comprehensive runtime exerciser. Mechanical ELF entry/write/exit remains
separate evidence; #689's real System.Net.Sockets fixture follows #688.
Basic pipe descriptors/nonblocking behavior belong to #685; blocking wait/wake
belongs to #686 and signal interruption/restart to #687. Futex work follows the
M09-006 wait core; general event/mutex tickets M09-007/M09-008 are not prerequisites.

Initial profile: x86-64 QEMU/OVMF, one vCPU, static non-PIE Linux executable,
invariant globalization, workstation GC, HTTP/1.1 IPv4, explicit static IP,
port8080 and immutable package/config. Start one user-mode context with
protected kernel mappings; cross-process isolation is the mandatory next
milestone. General writable storage, DHCP/DNS/outbound fetch, TLS, SMP,
ASLR and provider drivers are not first-web prerequisites.

M16-001 is a feasibility gate: publish/inspect ET_EXEC, PT_LOAD/PT_TLS,
startup/auxv and native dependencies; prove no PT_INTERP or DT_NEEDED.
Exercise startup, forced GC, threads, requests and shutdown under tracing,
then inspect upstream error paths because tracing is not an exhaustive syscall
specification. If the static closure cannot be built, preserve the exact
blocker and review a changed strategy before speculative compatibility work.
Linux-host success is not CLROS guest evidence. Never implement successful
no-op threads, mappings, protection or entropy.

The active guest payload policy is .NET 11 RC2: SDK
`11.0.100-rc.2.26504.105`, runtime/ILC/ILLink
`11.0.0-rc.2.26504.105`, `net11.0`, and C#14. The qualification command
`dotnet run --project tools/OS.Tooling -- nativeaot payload qualify --output <fresh-directory>`
requires that exact SDK on Linux musl, publishes all three apps with explicit
runtime and target-framework pins, and rejects missing or mismatched restored
ILCompiler/NativeAOT/ILLink packages. The native input receipt includes both
ordinary assets libraries and downloaded runtime packs, including their static
archives; NuGet's `downloadDependencies` cannot be omitted from that inventory.
Microsoft publishes the matching Linux musl SDK
and checksum in the [official .NET11 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json).
[Step1225](../done/step1225.md) records the fresh Linux r04 qualification:
all three .NET 11 static payloads publish without warnings and pass dependency
inspection, runtime exercise and HTTP/configuration/shutdown checks. Produced
managed/native inputs and package closure are retained by hash, with the six
original DLL/object files copied into the migration evidence archive. This is
Linux-host acceptance; CLROS guest Hello and Kestrel remain unqualified.
The .NET11 x64 JIT/AOT baseline is
[x86-64-v2](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime#x86x64-requirements):
the builder and eventual guest CPU must supply CX16, POPCNT, SSSE3 and SSE4.1/4.2
in addition to the prior SSE baseline. This does not require AVX/XSAVE. The old
`qemu64` evidence below does not establish this new CPU contract.

[Step1198](../done/step1198.md) qualified M16-001's earlier .NET10 static Hello, runtime
exerciser and real Minimal API on Alpine3.24.2, SDK10.0.401/runtime10.0.12.
All three are x64 ET_EXEC with four disjoint load ranges, TLS, no interpreter
or dynamic section; upstream application GC/EH remains separate from the kernel.
Fifty-four observed syscall names across22 thread/process trace files, 675 source
and258 native input hashes, headers/symbols/compiler responses and actual auxv
are retained. External Windows HTTP returns exact `OS_MINIMAL_API_OK`, followed
by clean app exit and a failed post-shutdown probe. This qualifies the Linux
host feasibility gate only. [Step1199](../done/step1199.md) implements #680's
versioned immutable package/staging profile; three final-source guests pass
data retention and digest checks after firmware exit, together with two rejection
controls, two boot regressions and 4,565 full-host checks. ELF entry and guest application/runtime/
Kestrel gates remain open. M16-003 owns the bounded service loop; M16-004 owns
ELF mappings, TLS and Linux initial stack construction.

[Step1200](../done/step1200.md) qualifies both children with 4,603 full-host checks,
native review, three combined package guests, standalone children, five affected
ancestors and missing-RNG rollback.
The loader separates kernel and user
leaves, omits the physical RW image alias from the user root, zeroes BSS/padding,
and constructs argv/envp/auxv from the versioned manifest, actual PHDR and borrowed
firmware entropy. Its current live backing cap is 4096 pages, preserving the
existing kernel identity-map limits. TLS template geometry is admitted; the later
step1201 profile qualifies mechanical application entry and write/exit syscalls.
Thread TLS and upstream application startup remain incomplete. No guest runtime
or Kestrel evidence follows from loading, read-back or mechanical entry alone.

### Prior .NET10 payload requirements and trace limits

The step1198 CPU was QEMU `qemu64`, one CPU, with SSE/SSE2/SSE3, FXSAVE, NX and
long mode; no AVX is advertised. Preserve this measured target and upstream
CPUID dispatch before broadening CPU claims. GNU notes record a 1.5 MiB stack;
they are not an exhaustive CPU specification. The runtime/ILC/package inventories
in step1198 are the exact .NET10 build input evidence. The following measured
values remain historical compatibility inputs. Step1225 records the separate
.NET 11 audit: startup reserves 8,234,242,048 bytes; PT_TLS has 24 file bytes,
264 memory bytes and alignment 8; 53 syscall names appear across 22 threads in
three processes. Changed stdin/CPU settings prevent attributing every trace
difference to the runtime version. The new trace still does not prove all error
paths, `fstat`/`pread64`, or GC activation-signal delivery; those contracts and
separate suspension acceptance remain required.

| Contract family | Observed and source-backed requirement |
| --- | --- |
| ELF/startup | Actual auxv supplies PHDR0x400040, PHENT56, PHNUM11, ENTRY0x4014EE, PAGESZ4096, identity/security, RANDOM, platform/exec-name and vDSO values. musl1.2.6 reads argv/envp/auxv, TLS and stack-canary entropy before main; missing TLS/thread-pointer initialization is fatal. Mechanical startup is qualified; upstream NativeAOT guest startup is not. |
| Memory/order | mmap/mprotect/munmap/mremap/madvise/brk and resource queries appear. GC queries/registers expedited membarrier. Its source has a protection/dirty-page barrier fallback; success no-ops cannot satisfy it. Trace failures include real ENOMEM/EFAULT/EINVAL and permitted discovery ENOENT. |
| Threads/TLS/signals | clone, arch_prctl, set_tid_address, futex and realtime signal masks/handlers/return appear. musl thread setup owns stack/TLS and translates clone failure to EAGAIN with cleanup. NativeAOT thread attachment/key failures may fail fast; GC suspension must preserve the full signal context and errno. |
| Files/descriptors | Immutable config plus proc/sys CPU/memory discovery use open/read/stat/readlink/getdents/lseek/pread, pipe/fcntl/ioctl and close. Missing optional discovery paths and ENOTTY are observed; descriptor identities, bounds and errno semantics remain necessary. |
| Sockets/readiness | socket/bind/listen/accept4, sendto/recvfrom, setsockopt/shutdown and epoll create/control/wait appear. System.Native uses edge-triggered readiness, retries EINTR, maps HUP to read/write progress and returns actual socket errors. A successful trace covers neither every error nor a CLROS TCP implementation. |
| Time/termination | pthread timed waits need monotonic deadlines, spurious-wakeup handling and genuine timeout behavior; syscall tracing misses uncontended user-space operations and vDSO time reads. exit/exit_group and SIGTERM shutdown are observed. |

Source/error review is a requirement inventory, not execution of those failure
branches. Optional TLS/crypto/dynamic loading, wider APIs/CPU shapes and hostile
long-running load remain outside this fixed HTTP/1.1 qualification. Unknown
syscalls must fail honestly; tracing never becomes a complete successful-no-op ABI.

M16-006 separates reserved virtual address space from committed physical
pages and their quotas. The pinned Hello's step1198 trace reserves
8,234,266,624 bytes with `PROT_NONE`, beyond the earlier loader's low-4-GiB
user range. Sparse reservation must not allocate one backing page per reserved
page. Preserve the qualified payload and model its measured reservation,
commit/protect/decommit and failure behavior, or explicitly change the bounded
runtime configuration and repeat publish/trace/source qualification. The trace
alone does not prove which smaller-reservation fallback the runtime can use;
silent payload/configuration changes or successful no-op mappings are invalid.

The in-progress backend in [step1203](../done/step1203.md) admits a bounded
64-GiB user VA layout while retaining low-4-GiB physical/root constraints.
Focused host checks replay the exact large reservation with no new tables or
backing, then commit distant islands, trim/release ranges and replace committed
pages with anonymous `PROT_NONE`. The pinned payload/configuration is unchanged.
The default-off EFI adapter and genuine bounded startup services are implemented
in [step1204](../done/step1204.md). A 34-call mechanical guest passes the large
reservation, partial commit/protection/replacement/release, high copy, startup
queries, live monotonic time, fresh entropy and real single-context barriers.
Original scoped native review, two controlled memory negatives, twelve ancestors
and the unchanged upstream entry attempt are recorded in step1204. The follow-up
in [step1205](../done/step1205.md) qualifies 4,646 full-host checks across 608
fixtures (94 Native) and three fresh 150-record guests. It exercises high-address
copy-out through getrandom and verifies it in CPL3, preserving the 34-call
fixture and one-use/64-call gateway policy. The unchanged payload stops at
unsupported ARCH_SET_FS before Hello; no upstream runtime acceptance is claimed.

### Transport decision experiments

Stock `linux-musl-x64` with real Kestrel remains the primary route. The host-only
[custom transport comparison #702](http://192.168.4.35:3000/admin/CLROS/issues/702)
may retain Kestrel's HTTP engine while replacing its socket transport
through `IConnectionListenerFactory`; compare the pinned application's behavior,
dependency inventory and required CLROS services before adopting it. This can
change networking integration, but does not remove upstream GC/EH, memory,
threading or timer requirements. The experiment is not guest acceptance and
does not block the NativeAOT implementation sequence. See [the Kestrel ledger](kestrel.md#transport-decision-experiments).

[WASI HTTP research #703](http://192.168.4.35:3000/admin/CLROS/issues/703)
is a separate optional route using a component host and a custom ASP.NET
`IServer`. It must not be reported as Kestrel or satisfy the
existing first-web gate. The previous .NET 10 WASI research remains historical;
.NET 11 feasibility needs a fresh audit of that separate experimental toolchain.
Publishing a component and invoking it on a host do not prove a freestanding
CLROS engine, runtime or network boundary.
Both tickets are supplemental research with no milestone and no critical-path
dependency; their results inform a later explicit architecture decision.

### Guest acceptance

First web requires three fresh post-firmware-exit boots, external HTTP200
with exact body `OS_MINIMAL_API_OK`, repeated/concurrent connections,
async/cancellation and clean shutdown. Preserve source/compiler/kernel/app/
package/image/serial/packet/HTTP hashes. After terminating the guest the probe
must fail. Require `TEST_START kestrel.smoke`, `KESTREL_APP_START`,
`KESTREL_BIND_READY`, `HTTP_PROBE_PASS`, `TEST_PASS kestrel.smoke`;
missing/failure markers invalidate the run. Kernel-authored HTTP is not Kestrel.

The next gate requires distinct address spaces and private descriptors/config,
budgets, endpoint authorization and supervisor lifecycle. Fault, terminate and
exhaust one app while probing the other; verify cross-process pointer/handle
rejection, cleanup and bounded restart. Two servers sharing one process do not
qualify. No shell/SSH reduces exposed functionality, but is not proof of
resistance to application exploits or superiority over hardened Linux/Windows.

## Build Selector

The build selector itself remains unchanged. See the current roadmap above
for the implementation and evidence required before either profile is usable.

Issue #249 adds a build-time selector named `OSWorkloadProfile`. It is a
compile-time contract only. It selects constants and host-testable capability
metadata; it does not start Kestrel, create a process table, enable a supervisor,
or change runtime behavior.

Default build:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

The default is `SingleMinimalApi` and emits the compile symbol
`OS_PROFILE_SINGLE_MINIMAL_API`.

Explicit multi-profile build:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1 /p:OSWorkloadProfile=MultiMinimalApi
```

This emits `OS_PROFILE_MULTI_MINIMAL_API`, sets the kernel-facing profile
metadata to require process-table and multi-app manifest support, and still does
not implement those features.

Invalid values fail during build with a readable MSBuild error:

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1 /p:OSWorkloadProfile=BadProfile
```

The kernel-facing metadata is exposed through
`OS.Kernel.Build.WorkloadProfile`:

- `SelectedName`
- `SelectedKind`
- `IsSingleMinimalApi`
- `IsMultiMinimalApi`
- `RequiresProcessTable`
- `RequiresMultiAppManifest`
- `SupportsMultipleMinimalApiApps`
- `ExactlyOneProfileSelected`

`tools/OS.ManifestTool` also accepts a matching profile selector:

```powershell
dotnet .\tools\OS.ManifestTool\bin\Debug\net11.0\OS.ManifestTool.dll validate --manifest .\manifest.json --profile SingleMinimalApi
```

When `--profile` is omitted, the tool uses the build-selected
`OSWorkloadProfile`. `SingleMinimalApi` rejects future multi-app manifest
fields such as `apps`, `restartPolicy`, `resources`, `isolation`, and
`supervisor`. `MultiMinimalApi` returns `multi_minimal_api_unsupported` until
process, address-space isolation, and supervisor semantics exist.

## Tooling Propagation

Issue #252 propagates the profile selector into evidence-producing host
tooling. This is still a host evidence contract only; it does not implement
processes, Kestrel, hosted .NET, networking, storage, or supervisor behavior.

QEMU smoke wrapper default:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
```

Expected evidence includes:

```text
WORKLOAD_PROFILE SingleMinimalApi
TEST_PASS workload.profile.tooling default single
```

Explicit multi-profile dry run:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -WorkloadProfile MultiMinimalApi
```

Expected evidence includes:

```text
WORKLOAD_PROFILE MultiMinimalApi
TEST_PASS workload.profile.tooling propagates multi
```

CI and baseline evidence scripts accept the same `-WorkloadProfile` argument,
record `workloadProfile` in their JSON summaries, and pass
`/p:OSWorkloadProfile=<value>` to MSBuild-backed commands.

`tools/OS.ImageBuilder` accepts `--workload-profile` and emits
`WORKLOAD_PROFILE <value>`. The raw image dry-run plan includes
`"workloadProfile":"<value>"` in the `IMAGE_PLAN` JSON payload.

`tools/OS.QemuRunner` accepts `--workload-profile` and emits
`WORKLOAD_PROFILE <value>` in dry-run, real-run console output, and real-run
summary logs.

Invalid values fail early with a nonzero exit and a machine-readable failure:

```text
TEST_FAIL workload.profile.tooling rejects invalid profile=BadProfile valid=SingleMinimalApi,MultiMinimalApi
```

## Feature Matrix

| Feature | `SingleMinimalApi` | `MultiMinimalApi` |
| --- | --- | --- |
| Default workload count | Exactly one configured ASP.NET Minimal API workload. | Two or more configured ASP.NET Minimal API workloads. Acceptance starts with at least two apps. |
| Manifest shape | One workload entry selected by the OS manifest. Current v0 manifest contracts may reserve the profile name later, but existing validators do not enforce it yet. | A bounded app list with per-app name, package or assembly path, args, environment/config, listen endpoint, log settings, resource limits, and restart policy. Not implemented. |
| Process table | Explicitly not required for the near-term profile. No `pid`, `spawn`, `wait`, `kill`, or process enumeration requirement is added by this profile. | Required. Needs process IDs, lifecycle state, exit status, terminate/kill, supervisor ownership, and resource accounting. |
| Per-app address space | One guarded user address space for the selected NativeAOT payload, with validated kernel/user access. Switching among multiple app address spaces is a later requirement. This proves no containment between apps. | Required. Each app needs its own virtual address space, handle table, memory limits, guard pages, and copy-in/copy-out validation at kernel or PAL boundaries. |
| Supervisor/restart policy | Boot selects and starts one workload. Restart policy can stay absent or be a single-workload policy after app exit capture exists. No multi-app dependency ordering or port arbitration is required. | Required. The supervisor must start, stop, monitor, restart, and log multiple apps, detect port conflicts, preserve per-app exit codes, and avoid one app crash taking down the others. |
| Scheduler/threading | Requires the threads, timers and synchronization used by the pinned .NET 11 NativeAOT payload, including musl pthreads and upstream GC/ThreadPool lifecycles. Scheduling multiple processes follows the first service. | Requires preemptive scheduling across processes and threads, thread creation/join/exit, sleep/yield, timer waits, futex-style wait/wake, and fair handling of multiple active Kestrel apps. |
| Application runtime boundary | The selected path is the restricted Linux x64 syscall ABI for the pinned NativeAOT payload. One runtime/workload instance is enough. Flat `OSHost_*` PAL/CoreCLR hosting remains a future route. | Requires a process-aware boundary: per-app descriptors/handles, configuration, socket ownership, isolated runtime state, and safe cleanup after app crash or restart. |
| Kestrel network/runtime prerequisites | Required before claiming the profile: app package/config is available in guest, PAL or ABI calls are exported, TCP listener works over live network, Kestrel binds the manifest endpoint, and an external `GET /healthz` reaches the app response. | Requires all single-app Kestrel prerequisites plus distinct ports for multiple apps, concurrent accepts, app-to-app isolation, and crash isolation while another app continues responding. |
| Storage/config packaging | One selected app package plus one config/environment set. The current package, manifest, and `kestrel.smoke.package.v0` docs are contracts only until live guest reads exist. | Multiple app packages or one package with multiple app entries, per-app config/environment, per-app log paths, package enumeration, and update semantics if added later. |
| Serial/panic diagnostics | Required boot, hosted-runtime, Kestrel bind, health-probe, app exit, and panic markers. Serial remains the required early evidence channel. | Required boot plus per-app lifecycle/crash markers. One app crash must be diagnosable without corrupting another app or losing supervisor state. |
| Cloud artifacts | One bootable image with one configured workload. Local QEMU proof is not Azure/AWS proof; provider artifacts still require provider-specific validation. | Cloud image contains multiple workloads and supervisor config. Provider validation must prove the same multi-app behavior on the selected substrate, not only local QEMU. |
| Security posture | No users, no SSH, no shell, no remote admin by default. The only network service is the configured workload or explicit manifest endpoint. No per-app containment claim is made. | Adds per-app containment requirements: address-space isolation, resource limits, bind permissions, metadata mediation, filesystem/config permissions, W^X/NX enforcement, and crash isolation. |

## Profile Claim Rules

`SingleMinimalApi` can be claimed only when evidence shows:

1. A guest image boots to stable serial markers.
2. The selected app package/config is available to the guest.
3. The hosted runtime or app target starts in the guest.
4. Kestrel binds the configured HTTP endpoint.
5. An external runner receives the expected response from `GET /healthz`.
6. Serial logs include workload startup, bind, health-probe, exit/failure, and
   panic markers where applicable.

Provider scope is part of the claim; the canonical provider-scoped Kestrel
marker and evidence lists are in
[`docs/completion-gates.md`](completion-gates.md#gate-14-provider-split) and
[`docs/kestrel.md`](kestrel.md#provider-scope). Local warning:
a `SingleMinimalApi` QEMU/libvirt Kestrel pass proves only local virtualization
behavior, not AWS Nitro or Azure VM support. AWS still needs
Nitro/ENA/NVMe/DHCP/TCP/EC2 serial evidence, and Azure still needs
Hyper-V/VMBus/NetVSC/StorVSC/DHCP/TCP/Azure serial or boot diagnostic evidence.
AWS IMDSv2 and Azure WireServer/goal-state are supporting integrations, not
substitutes for provider Kestrel traffic.

Summary: QEMU/libvirt Kestrel does not prove AWS Nitro support and does not prove Azure VM support.

The following are not required for `SingleMinimalApi`:

- Process table.
- Process IDs.
- Per-process address spaces.
- Multi-app supervisor.
- Per-app resource limits.
- Per-app metadata mediation.
- Running two apps at once.

`MultiMinimalApi` can be claimed only when evidence shows:

1. At least two ASP.NET Minimal API workloads start from manifests.
2. Each app has a distinct lifecycle identity, listen endpoint, config, and log
   stream.
3. The OS or hosted boundary enforces per-app address spaces or an equivalent
   documented isolation model.
4. One app can crash or be terminated without corrupting the OS or another app.
5. The supervisor observes exit status and applies the configured restart
   policy.
6. External HTTP probes can reach both apps after startup, and can still reach
   the surviving app after the other app fails.

## Non-Goals

- This ADR does not require general POSIX/Linux compatibility beyond the
  measured restricted Linux application ABI described above.
- This ADR does not require `fork()`.
- This ADR does not claim microkernel behavior.
- This ADR does not claim Azure, AWS, or provider boot support.
- This ADR does not turn host-side Kestrel, manifest, PAL mock, package, or
  parser tests into in-guest workload evidence.

## Current Evidence Boundary

Today the repository has host-tested manifest, package, PAL mock socket/config
and readiness contracts plus local EFI/QEMU boot markers. That evidence supports
planning for both profiles, but it does not satisfy either profile.

The full hosted-workload evidence boundary remains in
[`docs/completion-gates.md`](completion-gates.md#gate-14-profile-split) and
[`docs/kestrel.md`](kestrel.md#in-guest-kestrel-evidence).

The near-term backlog should continue to implement `SingleMinimalApi` first:
live storage/package reads, hosted runtime or app-target boundary, socket/timer/
thread prerequisites, live TCP listener behavior, Kestrel bind, external HTTP
probe, and serial evidence. `MultiMinimalApi` should remain blocked until
process, address-space isolation, scheduler, supervisor, and per-app security
issues are represented and tested.
