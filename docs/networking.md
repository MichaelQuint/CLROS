# Networking

The [October19 review](repo-audit.md#october-19-whole-project-review) reproduces
last-close unread-data retention: [#778](http://192.168.4.35:3000/admin/CLROS/issues/778)
must release inaccessible RX backing with explicit reset/discard semantics
before #224 acceptance. Preserve readable duplicate fds, SHUT_WR, tuple lifetime
and scrubbing. One unread byte pins the sole bulk slot in the host fixture;
no live guest attack or fix is claimed. [#779](http://192.168.4.35:3000/admin/CLROS/issues/779)
separately requires resource peaks reconstructed from ownership events.
Step1437 remains accepted within its original scope.

[Step1405](../done/step1405.md) qualifies #745 checked connected-stream vectors
and #219 startup: 5,619 full-host checks/751 fixtures (94 Native), three finite
BufferList guests, six real Kestrel web/quota prefixes and eight strict ancestors.
Vector captures stay at `0086b219`; full-host/web/ancestors stay at `8ef72134`.
Kernel/vector sources are unchanged across the final web-pin/evidence-only delta.
Limits remain 16 vectors/65,536 total bytes. The finite fixture proves short sends,
send EAGAIN, reset, pending disposal/later health and terminal transport cleanup;
Kestrel proves the exact 49,152-byte entity and a native short send crossing an
iovec. Web prefixes retain live workers until runner termination. General
networking/Kestrel, whole-stack and broader gates remain incomplete; #220 follows.

Current vector publication uses the retained original socket recipe and a fresh
output directory:

```text
dotnet run --project tools/OS.Tooling -- nativeaot socket-vectors publish --recipe-root <original-linux-socket-root> --output <fresh-directory>
dotnet run --project tools/OS.Tooling -- linux socket-vectors-package <original-publication> <fresh-package>
dotnet run --project tools/OS.Tooling -- linux socket-vectors-program <package> <complete-original-llvm-disassembly>
```

The explicit guest profile is `--EnableM16013Probe` with the sealed package,
complete decoder, owned peer and 600-second bound. It requires observed sendmsg
EAGAIN before guest peer reads, exact remaining vectors, a genuinely pending
receive before disposal and all four streams/cleanup. The standalone Linux peer
retains its separate 250-ms gate. Publisher/package/index admission alone grants
no guest authority. Step1405 records the exact implemented commands and hashes;
[steps1390-1404](../done/step1390.md) retain development and failed attempts.

[Step1380](../done/step1380.md) qualifies #217 configuration and #741/#742
network containment at implementation `3ef7316d`: 5,537 full-host checks across
731 fixtures (94 Native), three web and three containment captures, three repaired
signal/partial-pipe repetitions and fifteen other ancestors. All 24 captures
retain exact original source/object/EFI/package/serial/packet joins. Selected
configuration is observed inside the upstream NativeAOT application; the healthy
old peers survive another connection's timeout and unsupported RX content.
Step1405 adds bounded startup/vector qualification; the #220 service ladder
remains open under #737.
This adds no whole-stack, general Kestrel, numbered, cloud or production gate.

[Step1351](../done/step1351.md) qualifies the default-off bounded #689/#690
socket/epoll batch at implementation `3726e31`: 5,433 full-host checks across713
fixtures (94 Native), scoped original native review, three complete sync and
three async upstream socket guests and33 fresh strict ancestors. Four connections
per socket run exchange61 request/65,572 response bytes, with partial I/O, EOF,
reset, cancellation and concurrent pending-accept close. Actual TCP retirement,
worker reap, descriptor/buffer cleanup, NIC reset and allocator restoration precede
success; normal240-second TIME-WAIT is retained. The original payload is unchanged.
Only IPv4 passive TCP and the explicitly measured epoll surface are qualified.
The shared native-body joins do not prove whole-stack closure or arbitrary
workloads. The full Kestrel ladder and new numbered/cloud/production gates remain
incomplete. Continue the owned #220 service ladder under #737.

## Architecture review follow-ups

Step1380 qualifies M07-027/M07-028 after the development work in
[steps1369-1379](../done/step1369.md). Three fresh original containment guests
each exchange the exact three-peer streams, survive one local ETIMEDOUT and a
1518-byte VLAN drop/recycle, then reap five workers and scrub/release owned
resources. Healthy old connections remain usable beyond 30 seconds. Packet
delivery, a live scheduler witness, descriptor generations, timeout errno and
subsequent EOF/EPIPE are independently joined. Corrupt completion/ownership
controls remain terminal host negatives, not guest content-policy successes.
Each run records 83 RX, 71 accepted and 12 ignored frames. TX counts are
104/105/104, with each run matched to its own captured transmissions; no
individual RX outcome or cause for the TX difference is inferred.

The retained pre-repair host reproductions and failed strict startup-SYN
aggregate remain defect evidence in the named steps and issue attachments.
Their earlier pending claims are superseded only for these two bounded repairs.
Whole-stack safety, general Linux compatibility and the full Kestrel ladder
remain unqualified. The [ordered roadmap](workload-profiles.md#architecture-review-implementation-order-2026-10-16)
keeps the remaining work at its required acceptance stage:

[Step1437](../done/step1437.md) qualifies the #743/#744/#757/#758 transport
prerequisites within the seven-ticket service batch. The complete original
capture joins a lost reopen and two persist probes, two pending resets, 32
server-active closes and all32 compact retirements. Bulk high-water is4 and
compact high-water24; the full240-second policy and tuple/generation protection
remain. The same frozen checkpoint passes5,809 full-host checks and ten fresh
affected ancestors, with scoped original native/ABI/unwind/table review.

[Step1424](../done/step1424.md) defines continuing ISNs as
`M + HMAC-SHA256(secret, domain || tuple)[0..4]` modulo2^32, with real monotonic
nanoseconds/4000. A separately acquired32-byte firmware secret and exact network
lease remain distinct from AT_RANDOM/getrandom. New key lifetimes observe240s
quiet time; replacement waits for complete pending/stream/compact drain.
Step1437 joins34 actual sequence generations and final lease release/erasure.
Near-wrap and96 complete lifetimes are separate host controls. Finite profiles
retain their diagnostic counter. Repeated service #224 and general congestion,
arbitrary-stack, cloud and production claims remain open.

- [M07-029](http://192.168.4.35:3000/admin/CLROS/issues/743) supplies
  zero-window persist recovery, including a lost window-reopening update.
  [M07-030](http://192.168.4.35:3000/admin/CLROS/issues/744) retires
  active-close bulk buffers into bounded compact TIME-WAIT records while keeping
  the 240-second policy and tuple protection. Both precede repeated-service #224.
- [M16-013](http://192.168.4.35:3000/admin/CLROS/issues/745) adds
  checked `msghdr`/`iovec` semantics for the actual pinned socket/Kestrel vector
  path before #220. Small successful HTTP responses do not cover that path.
- [M07-032](http://192.168.4.35:3000/admin/CLROS/issues/757) handles validated peer RST in SYN-RECEIVED.
  The pre-repair host reproduction retained the pending slot and retransmitted
  SYN-ACK until its deadline, blocking another client when capacity is full.
- [M07-033](http://192.168.4.35:3000/admin/CLROS/issues/758) replaces the finite diagnostic ISN allocator
  for the qualified continuing profile. Step1437 preserves secret/tuple/time,
  modular arithmetic, generation and TIME-WAIT protections. The earlier finite
  exhaustion reproduction remains historical evidence; no sequence-injection
  exploit is claimed. These prerequisites feed repeated-service #224.
- After a matched workload baseline, [M07-031](http://192.168.4.35:3000/admin/CLROS/issues/751)
  replaces one-segment timed flight with bounded congestion-controlled flight.
  At 1460-byte MSS and 10ms RTT, one segment per RTT gives about 146KB/s before
  overhead; that is an analytical bound, not a measured CLROS benchmark.

These issues require independent loss/reorder/backpressure controls, another
connection's progress, a scheduler witness and complete retirement/reset
accounting in real guests. They are not implemented by this roadmap update.

## Development history

The following entries preserve their original pending/failure boundaries;
step1351 supersedes them only for the exact accepted finite batch above.

[Step1348](../done/step1348.md) repairs direct MSBuild boot-policy selection
and ordinary-policy conflict rejection for both socket leaves. The preceding
5,433-check full-host run has one native policy failure and remains failed;
all18 focused repair checks and source/admission audits now pass. No queued
guest started. Fresh full-host and socket/ancestor qualification remain required.

[Step1347](../done/step1347.md) adds the complete sync runner pass, three
byte-identical V2 package-staging guests, and six bounded original-native body
reviews. The first stable full-host attempt fails nine profile/control checks
before the native lane; all49 focused repair checks then pass. Direct MSBuild
now explicitly excludes both socket leaves from the standalone cooperative
profile. Full-host qualification, remaining call/stack closure, socket repetitions
and affected ancestors still precede acceptance. No current issue closes.

[Step1346](../done/step1346.md) records one complete live M16-012 runner pass
with the original async socket ELF. Pending accept/receive/send are each1;
four connections exchange61 request/65,572 response bytes, with two EOFs,
one reset, a cancelled pending accept and a closed pending accept. Nine workers
reap and all four transport lifetimes retire after actual240-second TIME-WAIT.
The external peer holds its first request until the accepted guest fd returns
EAGAIN and its caller parks. This only controls stimulus; the sealed program,
strict parent/scheduler/packet records and original process results still supply
the evidence. The unchanged publisher and guest ELF are preserved. Full-host,
remaining scoped native review, repetitions and affected ancestors
remain pending before #689/#690 acceptance. No Kestrel gate advances.

[Step1345](../done/step1345.md) wires the original sync/async socket packages,
sealed instruction index, independent peer child, exact forward-port identity,
full firmware/APIC/NIC parents and packet/transport cleanup checks. The original
sync guest and peer complete four connections: 61 request bytes,65,572 reply bytes,
two EOF cases, one reset and15 partial sends. Actual240-second TIME-WAIT retention,
four transport retirements, four worker reaps, descriptor/buffer cleanup, NIC reset
and allocator restoration are captured. The live outer run still fails its host
checker; corrected report-only replay passes the captured leaf, parents, peer
literals and packet checks. This is development evidence, not accepted #689/#690
completion. Full-host, original native review, fresh complete runner validation,
async and affected ancestor qualification remain pending. Kestrel remains incomplete.

The combined profile now fits its47 retained mapping declarations and18-entry
reservation ledger; every ledger append checks capacity before writing. The user
root permits only borrowed, single-page, NX supervisor UC identity device mappings
above4GiB within its64GiB virtual policy. They must remain outside the declared
dynamic reservation; owned user backing and page tables stay below4GiB. Receive
copies through the already admitted descriptor scratch extent and delivers queued
bytes before a later reset error. It preserves unread TCP bytes on copy failure.
The packet checker anchors after original firmware traffic and counts only the
checked QEMU IPv6 router advertisement as ignored background traffic; this adds
no application IPv6 support. Step1344's source3 deadline/FIFO, wake/repark,
instruction/return and generation checks remain in force.

[Step1343](../done/step1343.md) seals the original socket ELF's LLVM instruction
lookup: 398,041 starts and186 syscall continuations, independently bound to all
five executable sections. Both unchanged package modes pass; a partial index and
wrong package mode reject. The EFI owner now emits actual source3 wake, repark
and completion observations, including generation, sequence and table clock.
The socket decoder binds those observations to framed original stdout and
recorded waits/resumes. Its reports grant no complete lifecycle or guest authority;
full scheduler/packet/parent validation and QEMU integration remain pending.

[Step1342](../done/step1342.md) wires the default-off M16-011/M16-012 EFI
profile through the retained live NIC lifetime. Linux workers exclusively own
the cooperative pool; real NIC packets and hardware-clock deadlines are pumped
at restored syscall/idle boundaries. Descriptor close, real transport drain,
table retirement and NIC reset precede socket completion markers. The fixed
diagnostic requires manifest Any:8080 and retains unresolved ownership on failure.
The enabled no-stdlib C# profile compiles; native publication and guest execution
are pending. Sealed sync/async packages now reuse the same unchanged Linux ELF
after exact original receipt/output/source admission. QEMU rejects these leaves
until their original instruction index, application peer and strict transcript
are wired; the older scripted TCP transcript cannot authorize them. No current
socket/application issue or additional gate is accepted.

[Step1341](../done/step1341.md) adds the development packet/timer owner connecting
real TCP frames to the Linux socket adapter. Exact tuple/generation lookup selects
retained passive handshakes and stream windows. Valid final-ACK payload enters the
stream before socket transfer; socket accept pressure retains private established
windows until capacity returns. Listener dup follows its original description,
while close/reuse cannot redirect it. Pending retry exhaustion retains the original
hard deadline and still permits a final ACK. Full normal240-second TIME-WAIT,
reset/fd-close and buffer/table scrub have focused host evidence. Separate explicit
abort cleanup requires detached threads and every fd closed; it claims no graceful
wire completion. Descriptor/thread alias guards include network ownership. Native
source3 wait completion/commit branches are feature guarded. Step1342 adds the
combined NIC/profile source and C# compilation; enabled native publication and
actual guest exchange remain pending. No current issue or gate is accepted by
this development slice.

[Step1340](../done/step1340.md) implements development-only bounded epoll over
the shared socket descriptions. Packed Linux x64 events preserve all64 opaque
data bits; level delivery, edge drain/rearm, MOD, dup/last-close, fd reuse and
copy-before-consume use actual TCP state. Infinite waits genuinely park, including
an empty port. Original epoll descriptions remain borrowed across concurrent fd
close/reuse until completed wait or captured exit. Real clock deadlines, directed
signals without restart, early cancellation and competing waiters have focused
host evidence. Normal closed TCP reports HUP/retained FIN without a fabricated
ERR; consuming reset clears ERR. Tables are bounded to4 ports/64 registrations;
only IPv4 passive TCP sockets, IN/OUT/RDHUP/ERR/HUP and EPOLLET are supported.
Nested epoll, other pollable files, EPOLLONESHOT and temporary signal masks are
unsupported. The serialized network owner must refresh and wake after every
transport transition. EFI wait-source3 capture/completion, live NIC integration,
the actual guest socket fixture and stable batch validation remain pending.
No current issue closes and no completion gate advances.

[Step1339](../done/step1339.md) records fresh Linux NativeAOT qualification of
both modes added in step1338, from one unchanged published ELF at source3afb03eb.
Each exchanges61 request/65,572 response bytes across four connections, reset1
and EOF2. Sync observes five partial sends/78 blocked sends. Async observes one
pending accept, receive and send, then successfully cancels and closes pending
accepts. Its own traces use edge-triggered read/write registrations and infinite
`epoll_pwait` with maxevents1024. Later review of the exact pinned
[SafeSocketHandle close path](https://github.com/dotnet/dotnet/blob/820230c386db5b0106c5430b3e5aee92a7826bf5/src/runtime/src/libraries/System.Net.Sockets/src/System/Net/Sockets/SafeSocketHandle.Unix.cs#L262-L283)
corrects the initial linger requirement: abortive close explicitly continues
after EINVAL/ENOPROTOOPT/ENOTSOCK from its8-byte `SO_LINGER` request. Current
bounded rejection therefore needs no fabricated linger success. Both modes
still use `sendto`/`recvfrom`; no message-syscall
expansion is justified by this fixture. Independent copied evidence checks779
source inputs,32 outputs, four producer files and the original recipe receipt.
The earlier managed Windows run had zero pending sends and remains separate.
These are Linux builder results; step1340 adds the host epoll slice above and
actual guest proof remains pending.

[Step1337](../done/step1337.md) adds the development-only IPv4 passive TCP
socket adapter in the shared Linux descriptor namespace. Host checks exercise
genuine TCP-owner handshakes/frames, partial I/O, copy-failure retention, endpoint
denial, dup/last-close, graceful EOF, reset and closed buffer/table cleanup.
Only the manifest address/port can bind. Last close retains TCP ownership for
the continuing packet/timer owner; a failed transport is explicitly aborted
before closed reap. `CLROS_LINUX_SOCKETS` keeps the TCP dependencies outside older
EFI descriptor/threading profiles. This is host contract evidence: the live
NIC/scheduler socket profile and guest payload execution are still pending;
step1340 adds retained epoll waits. No issue in the current socket/application
batch is accepted.

[Step1336](../done/step1336.md) records the finite System.Net.Sockets fixture's
real pinned Linux NativeAOT run: four connections,61 request bytes,65,572 reply
bytes, four partial sends,70 blocked sends, reset1 and orderly EOF2. Independent
copy audit verifies777 source inputs,21 outputs and four original producer files.
The earlier managed Windows run observes no partial sends. Both boundaries
remain distinct; the CLROS socket adapter now has the bounded host slice above,
with step1340's host epoll evidence separate from pending guest proof.

The measured native calls include `sendto`/`recvfrom`, `fcntl` nonblocking flags,
`accept4`, socket options and an empty reactor's `epoll_create1`/`epoll_pwait`.
Capability probes also try IPv6, IPv4 and Unix datagram sockets. The exact
shipping runtime source pin is verified from its package/host metadata.
Its [protocol support check](https://github.com/dotnet/dotnet/blob/820230c386db5b0106c5430b3e5aee92a7826bf5/src/runtime/src/libraries/Common/src/System/Net/SocketProtocolSupportPal.Unix.cs)
treats family/protocol denial as unsupported family. Preserve the distinction
between supported IPv4 family and unsupported datagram type in the adapter;
these probes do not justify fake UDP or Unix descriptors.

[Step1332](../done/step1332.md) qualifies #691 -> #148 -> #149 -> #150 -> #151 ->
#152 -> #153 and shared #734 at implementation `fd296378`. The complete unfiltered
host gate passes5,341 checks across700 fixtures (94 Native), with unchanged
inputs, no failures/timeouts and no reuse. Three fresh strict TCP commands and20
fresh affected ancestors pass with exact source/native/producer joins.

Each TCP capture passes260 ordered/unique markers,80 independent wire steps and
an exact150-packet corpus, including the original queue warmup. Five connection
lifetimes, four buffer-pair scrubs, real240-second TIME-WAIT and original NIC
reset/bus-master disable/fence/DMA-pool cleanup complete. A guarded cooperative
worker advances2,223/2,229/2,242 times; maximum observed gap154,676,900ns stays
within the admitted one-second bound. Normal exit17, one reap, mapping restoration
and32 scrubbed retained worker pages complete in every capture.

All813 raw source inputs match between isolated trees and the independently
checked source archive. Original native reviews retain their bounded ABI/stack
scope; complete object/EFI/method-map equality carries the unchanged native-code
review across the final project-policy repair. All53 producer/input identities
match before/after the guest sequence;500 original files are hash-verified in
retained evidence. Earlier failures and interrupted gates remain in steps1309-1331.

This is a finite default-off service on one QEMU/OVMF vCPU. Malformed used-batch
and full16-bit ring-index wrap controls are host evidence; the live captures prove
actual queue reuse/fullness and independent TCP traffic. Separate8-page network
and32-page worker firmware reservations remain retained; the original two-page
allocator is preserved. No Linux application is entered by this network profile.
The separate unchanged Hello/runtime acceptance remains in step1307. Next are
#689 Linux sockets, #690 epoll and the M10 actual Kestrel ladder. No numbered,
cloud or production gate advances.

The default-off `EnableM07026Probe` retains size-eight Virtio RX/TX rings and
image-backed owners across20 ARP and12 tagged ICMP exchanges, then the complete
TCP scenario and a final cleanup echo. Actual owned HPET callbacks drive bounded
polling, SYN/data retries and lifetimes. Sends copy into retained NIC backing.
Fresh reset, bus-master disable and fence precede DMA scrub/pool release. The
separate eight-page firmware reservation remains owned/mapped; the original
two-page allocator is preserved. See [steps1309](../done/step1309.md),
[1310](../done/step1310.md) and [1311](../done/step1311.md) for ownership details.

The TCP scenario covers table/backlog pressure, stale generations and wrapped
reuse; SYN loss/duplicate/invalid ACK; receive overlap/right clipping/gaps/window
reopening; segmented short writes, partial/old/future ACK and original-byte
retries; half-close, simultaneous FIN, exact/challenged RST, send/handshake timeout
and TIME-WAIT restart/reap. [Step1321](../done/step1321.md) binds these retained
owners to the guest and independent host wire corpus. The peer uses literal wire
expectations, not guest TCP codecs. Independent PCAP admission checks every
packet, tuple, checksum, payload and timer, including the exact original queue
warmup prefix and all original serial/package parents. Only packet-observation
retry comparisons allow two milliseconds of skew; owned lifetimes retain their
exact bounds. `qemu validate-network <serial-log> <pcap> <package>` replays retained
inputs read-only without launching a guest or granting batch acceptance.

The profile defaults to285 seconds and accepts260..295. On fresh artifacts,
prepare the mechanical Threads package once, then run:

```powershell
dotnet run --project tools/OS.Tooling -- linux thread-fixture artifacts/packages/tcp-30644a3.pkg Threads
dotnet run --project tools/OS.Tooling -- qemu smoke --BuildEfi --EnableM07026Probe --LinuxElfPackage artifacts/packages/tcp-30644a3.pkg --Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" --OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" --TimeoutSeconds 285
```

This package is staged only; the network profile does not enter a Linux app.
[Steps1322](../done/step1322.md) and [1323](../done/step1323.md) preserve the
code-slot collision and repeated-FIN assertion failures and their scoped repairs.
[Step1326](../done/step1326.md) preserves the failed partial full-host attempt,
repairs cross-profile/authority admission and records189 focused passing checks
across13 unchanged-input receipts without reuse. A fresh unfiltered full-host
run, complete original native ABI/transitive stack review, strict fresh repeated
guests/packets and affected ancestors remain required before review/merge and
evidence-based closure. Sockets/epoll, guest Kestrel, isolation and new numbered,
cloud or production gates remain incomplete.

The #141–#144 / #635 receive-and-ARP batch is qualified in
[step1164](../done/step1164.md): 4,441 full-host checks, linked native review,
a 194-record guest receipt, independent request/reply libpcap and thirteen
ancestor regressions pass. Default-off `EnableM07ArpReceiveProbe` follows
the original fixed-frame profile with one fresh TX/RX lifetime. It validates a
modern 12-byte no-offload header, matches one ARP reply, and updates a caller-owned
two-entry neighbor cache. Expiry, unresolved timeout and replacement remain
explicit bounded controls. This adds no continuing receive loop or timers.
Its separate receive negotiation selects VERSION_1+MRG_RXBUF solely to obtain
the device-written buffer count on this QEMU backend. The driver posts one
buffer and requires num_buffers=1; no multi-buffer assembly is implemented.

The bounded #136â€“#140 batch is qualified in [step1162](../done/step1162.md):
4,422 full-host checks, linked native ABI/stack review, a 168-record guest receipt
and an independent exact 60-byte frame in QEMU libpcap pass. The default-off
`EnableM07VirtioNetProbe` binds modern PCI, negotiates VERSION_1 only, uses one
owned page for disjoint size-eight RX/TX rings, primes one 1526-byte RX buffer,
then transmits and reclaims one fixed experimental Ethernet frame. It resets,
disables bus mastering, fences, releases descriptors/DMA and restores the original
allocator/command. Eleven ancestor regressions pass. Continuing RX/I/O and live
DHCP/DNS/TCP/HTTP remain incomplete; M07 and later gates stay open.
Development history is in steps1158â€“1161.

## Current Status

The default-off finite continuing service is qualified in [step1332](../done/step1332.md). The following inventory records the individual host-tested helper contracts. The repository now has host-tested Ethernet + ARP + ARP neighbor resolution transaction + bounded ARP neighbor cache + ARP reply cache updater + ARP receive-dispatch cache updater + IPv4 neighbor route planner and caller-owned IPv4 neighbor route diagnostic marker formatting + network receive dispatch metadata + ICMP receive dispatch metadata + UDP receive dispatch metadata + UDP transmit frame planning with checksum generation + IPv4 + ICMPv4 echo packet + IPv4 ICMP echo frame + UDP encode/decode helpers, an IPv4 transport pseudo-header checksum helper, DHCPv4 message/client/UDP-frame primitives, DNS message primitives, mock-driven DHCP and DNS client transaction state machines, DHCP lease configuration metadata, DHCP/DNS UDP transaction planners, a TCP header primitive, mock-driven TCP active-open and passive-open handshake state machines, a caller-driven active TCP connect transaction, TCP-over-Ethernet/IPv4 active-connect frame planning with outbound SYN/final-ACK checksum generation and passive-open frame planning with outbound SYN-ACK checksum generation, a passive-open-to-listener-backlog bridge, a listener-backlog-to-mock-accept-queue bridge, a caller-owned established TCP payload stream primitive, an established-stream-to-mock-socket inbound bridge, a TCP close/reset transition planner, an HTTP/1.1 GET request builder, an HTTP/1.1 request parser, an HTTP/1.1 response writer, an HTTP/1.1 static response transaction, an HTTP/1.1 response parser, a mock-stream HTTP GET client transaction, an HTTP GET over TCP/IPv4 frame planner with outbound request checksum generation, inbound response checksum validation, and caller-owned plan diagnostic marker formatting under `src/OS.Kernel/Net`, host-tested Azure WireServer and AWS IMDSv2 request metadata planners under `src/OS.Kernel/Cloud`, and a host-only mock PAL socket table that covers connected-handle partial send/receive/poll/shutdown/peer-close/peer-reset behavior plus Kestrel-oriented socket options and endpoint queries. The earlier fixed-TX/ARP profiles retain their historical scope; step1332 adds finite retained RX/TX and TCP lifetimes. Application sockets and general workload networking remain incomplete.

The kernel-facing native manifest can now record one bounded listen endpoint as metadata for future hosted-workload binding. That metadata is host-tested through manifest parsing, launch-plan creation, bind-policy validation against a future PAL IPv4 bind endpoint, mock socket-table bind composition, and mock socket-table listener creation; it does not export PAL functions, open a live socket, enter real TCP listen state, wait on a scheduler, or prove Kestrel behavior.

Drivers now also include a host-tested modern virtio PCI capability-list parser over caller-provided config-space bytes plus a virtio PCI discovery contract that can identify caller-provided modern virtio-net PCI identity and required capability metadata. `PciVirtioDescriptorCatalogPlanner` can compose injected ECAM scan metadata through modern virtio candidate selection, fake-derived BAR-size binding, feature/queue metadata, and descriptor cataloging to produce host-tested virtio-net descriptor metadata. This is a metadata contract only; it is not live NIC discovery or packet I/O.

Generic virtio initialization now also has a host-tested status/feature negotiation planning contract for the future point where a discovered virtio-net device exposes feature bits and queue metadata.

The virtio-net path can now consume host-tested virtio PCI BAR register-window planning metadata as a prerequisite for future live queue setup. This validates caller-provided mapped-BAR window records against common, notify, ISR, and device-configuration apertures and plans aligned register addresses. `VirtioPciBarRegisterWindowLiveAccessorBoundary` now exposes the narrow handoff from those access plans to an injected register accessor for planned `Read16`, `Write16`, and `Write64` operations, preserving BAR/capability/address metadata and accessor status without containing a live MMIO or port-I/O implementation. These register-window helpers remain metadata/fake-accessor contracts. The separate bounded profile above has live owned MMIO queues and one fixed TX receipt; continuing I/O remains incomplete.

Virtio-net queue planning now has a host-tested RX/TX queue contract for caller-owned Ethernet frame buffers. It records RX buffers as device-writable, TX virtio-net header and Ethernet frame descriptors as device-readable, validates two available queues plus split-ring queue sizes, and computes required buffer bytes without allocating buffers or publishing descriptors. The virtio-net device configuration slice can parse a caller-provided MAC address and optional little-endian status bits, rejecting truncated config bytes, all-zero MAC addresses, and broadcast MAC addresses before a future driver accepts device identity. The register-window device-configuration reader can now fake-read those MAC/status bytes from caller-owned device BAR-window bytes into caller-owned scratch storage before parsing. The virtio-net header slice can now parse the baseline 10-byte no-mergeable-buffers header, reject checksum/offload/GSO state for the current no-offload baseline, expose received Ethernet frame offsets and lengths without copying packet bytes, and write a zeroed no-offload TX header. The RX classifier now combines that no-offload virtio metadata with `EthernetHeader.TryDispatch` to classify caller-provided receive buffers as ARP, IPv4, unsupported EtherType, or malformed/truncated input. The RX lifecycle slice can publish caller-owned receive buffers into a split-ring descriptor table and map one used-ring completion back to the published descriptor/buffer index before invoking the classifier. Its publish and completion results reject default/zero values as successful evidence, but it still does not notify a device, process interrupts, poll a live queue, or own DMA memory. The queue setup register-window planner can now translate the planned RX/TX common-configuration setup command sequence into caller-owned register-window access plans for a future accessor without touching registers. The queue setup register-window executor can fake-execute those planned accesses over caller-owned common BAR-window bytes, staging expected read values because a byte span does not emulate queue-select-dependent hardware registers. The queue setup executor can consume the same command sequence and execute `Read16`, `Write16`, and `Write64` operations through an injected fake virtio PCI register accessor, preserving setup-plan, command-source, missing-accessor, read/write, and read-mismatch failures without live MMIO. The queue notification coordinator can now combine successful RX/TX descriptor publication with successful queue MMIO setup metadata and calculate the RX/TX notify offset/value plan without performing MMIO. The queue notification register-window bridge can now translate successful RX/TX notify plans into checked notify-aperture access metadata for a future accessor without touching registers. The queue notification register-window executor can fake-execute those checked notify plans over caller-owned notify BAR-window bytes while preserving BAR-relative metadata and nested statuses. The barrier-gated queue notification register-window executor now requires explicit descriptor-write, available-ring-write, and ordered-before-notify metadata before that fake byte-span write. The queue notification executor can now consume that plan plus explicit barrier-readiness metadata and write one 16-bit notification through an injected fake sink or fake virtio PCI register-accessor adapter, preserving failed-plan, missing-barrier, sink/accessor not-ready, range, and write failures without live MMIO or packet transfer. The TX completion planner can now consume successful TX publication plus successful fake-sink or register-window TX notification execution provenance and map one caller-owned used-ring entry back to the published TX descriptor chain for reclaim metadata. The TX descriptor ownership slice now marks published header/frame descriptors `InFlight` in a caller-owned state span and frees only those descriptors after a successful completion plan, rejecting failed inputs, undersized state spans, in-flight reuse, not-in-flight completion, and duplicate completion. The TX free-list planner reads the same caller-owned state span to select the first contiguous two-descriptor free pair, skip in-flight descriptors, reuse descriptors reclaimed to `Free`, report capacity exhaustion, and reject invalid state values without mutating the table. The TX publish-plan composer selects that reusable pair, publishes no-offload TX header/frame descriptors into caller-owned queue memory through `VirtioNetTransmitLifecycle.Publish`, writes the caller-owned TX header, and marks only the selected descriptors `InFlight` after successful publication. The TX register-window reclaim composer can now consume successful register-window TX readiness, validate one caller-owned used-ring completion, and apply descriptor-state reclaim. These TX slices still do not wait for hardware, process interrupts, own a full runtime descriptor allocator, perform DMA mapping/allocation, notify a live device, poll used rings, or prove packet transmission. `NetworkReceiveDispatcher` consumes that classifier result and parses ARP or IPv4 header metadata from the payload. `ArpReceiveDispatchCacheUpdater` can consume successful ARP dispatch metadata and update caller-owned ARP cache storage after validation, but it still does not read from a NIC, poll a queue, own timers, route packets, or dispatch sockets. `IcmpReceiveDispatcher` adds an IPv4/ICMP echo receive metadata step over the same caller-owned receive buffer, including echo checksum validation and payload offsets, without copying payload bytes or integrating diagnostic ping state. `UdpReceiveDispatcher` adds one narrower IPv4/UDP receive metadata step over the same caller-owned receive buffer without copying payload bytes or integrating sockets, DHCP, DNS, or application I/O.

`VirtioNetTransmitReadinessPlanner` now composes successful fake-accessor queue setup execution, caller-owned TX descriptor publication, explicit barrier-readiness metadata, notification planning, and fake-accessor-backed notification execution into one host-tested readiness boundary. This is still fake-accessor and caller-owned-buffer evidence only; it does not map BARs, execute real memory barriers, notify live hardware, transfer packets, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`VirtioNetTransmitRegisterWindowReadinessPlanner` now composes successful queue setup register-window fake execution, caller-owned TX descriptor publication/state marking, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution into one host-tested readiness boundary. This is still caller-owned common/notify BAR-window, queue-memory, TX-header, and descriptor-state evidence only; it does not map BARs, execute real memory barriers, notify live hardware, wait for completion, transfer packets, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`VirtioNetTransmitRegisterWindowReclaimPlanner` now composes successful TX register-window readiness with one caller-owned used-ring completion and descriptor-state reclaim. This is still caller-owned queue-memory and descriptor-state evidence only; it does not wait for live hardware, process interrupts, own a runtime descriptor allocator, transfer packets, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`VirtioNetReceiveReadinessPlanner` now composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution into one host-tested readiness boundary. This is still caller-owned common/notify BAR-window and queue-memory evidence only; it does not map BARs, execute real memory barriers, notify live hardware, complete receives, transfer packets, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`VirtioNetReceiveRegisterWindowDispatchPlanner` now composes successful RX register-window readiness with one caller-owned used-ring completion, selects the completed buffer from a caller-owned receive-buffer slab, runs receive-frame classification through the existing completion planner, and then feeds the selected caller-owned buffer into network dispatch metadata. This is still caller-owned queue-memory and receive-buffer-slab evidence only; it does not poll hardware, process interrupts, transfer packets, notify live hardware, mutate network state, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`VirtioNetReceiveRegisterWindowTcpDispatchPlanner` now composes that completed
caller-owned RX slab dispatch path into `TcpReceiveDispatcher`. It preserves RX
dispatch, slab-completion, network-dispatch, and TCP receive-dispatch status
metadata plus descriptor head, buffer index, selected-buffer offset/length, used
byte count, payload bytes, and passive ACK bytes. The slice rejects failed RX
dispatch, selected-buffer range failures, non-TCP paths, bad TCP checksums, TCP
dispatch failures, failed established-stream paths, and default/zero result
values while keeping an explicit no-live-MMIO/no-packet-I/O/no-socket/no-Kestrel
boundary. `VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter`
serializes those already-produced results into a caller-owned
`VIRTIO_NET_RX_TCP_DISPATCH` marker with RX/slab/network/TCP status names,
network protocol, selected-buffer metadata, payload/passive-ACK byte counts,
and explicit boundary flags through the shared ASCII text builder.
`BootSequence.RunVirtioNetRxTcpDispatchReadiness` exposes that same formatter
through opt-in boot markers: successful inputs emit
`VIRTIO_NET_RX_TCP_DISPATCH_READY` followed by the formatted diagnostic
payload, while failed/default/formatting failures emit
`VIRTIO_NET_RX_TCP_DISPATCH_NOT_READY` with status-coded metadata. The default
boot path still does not emit these markers. The QEMU runner and smoke scripts
now have an opt-in `virtio-rx-tcp-dispatch` marker preset that requires
`VIRTIO_NET_RX_TCP_DISPATCH_READY` plus `VIRTIO_NET_RX_TCP_DISPATCH` for future
probe images, but the preset itself is only tooling metadata and does not prove
live virtio-net RX or TCP delivery.

The QEMU runner and smoke scripts also have an opt-in
`http-static-response` marker preset that requires
`HTTP_STATIC_RESPONSE_READY` plus `HTTP_STATIC_RESPONSE` for future tiny HTTP
static-response probe images. The preset is marker-contract tooling only; it
does not prove a live HTTP server, sockets, Kestrel, hosted .NET, or cloud
readiness.

The QEMU runner and smoke scripts also have an opt-in
`http-static-tcp-response` marker preset that requires
`HTTP_STATIC_TCP_RESPONSE_READY` plus `HTTP_STATIC_TCP_RESPONSE` for future
tiny HTTP static TCP-response probe images. The preset is marker-contract
tooling only; it does not prove live HTTP server behavior, sockets, hosted
.NET, Minimal API, Kestrel, cloud readiness, or production readiness.
`BootSequence.RunHttpStaticTcpResponseReadiness` now exposes the host-tested
plan formatter through the same markers for future probe images: successful
inputs emit `HTTP_STATIC_TCP_RESPONSE` followed by
`HTTP_STATIC_TCP_RESPONSE_READY`, and blocked/default/formatting failures emit
`HTTP_STATIC_TCP_RESPONSE_NOT_READY` with nested TCP/HTTP/formatter metadata.
The default boot path still does not emit these markers.

`TcpReceiveDispatcher` now adds a host-tested TCP receive-dispatch boundary over caller-owned virtio-net receive buffers and already-parsed `NetworkReceiveDispatchResult` metadata. It validates IPv4 TCP pseudo-header checksums, rejects malformed or out-of-window ACK/FIN/RST control metadata before stream or close mutation, routes in-order established payload segments into `TcpEstablishedStream`, routes passive FIN/ACK and RST segments into `TcpConnectionClose`, writes a caller-owned passive-close ACK segment with a computed TCP checksum, and rejects malformed, non-TCP, bad-checksum, out-of-order, and default/zero result paths with status-coded metadata. `TcpReceiveDispatchDiagnosticFormatter` formats those results into a stable caller-owned `TCP_RX_DISPATCH` marker with dispatch, action, network, TCP packet, stream, close, checksum, byte-count, and sequence metadata. These paths do not allocate packet buffers, own TCP control blocks, dispatch sockets, poll a NIC, publish virtio-net descriptors, retransmit, run timers, implement TLS, or prove Kestrel behavior.

`TcpConnectionTable` now adds the first caller-owned TCP connection table for established stream metadata. It stores value-type endpoint tuples, local/remote next-sequence metadata, and lifecycle state in caller-provided entries; supports add-established, lookup, sequence-metadata update, closing mark, remove-to-closed, and closed-slot reuse; and rejects invalid endpoints, duplicate tuples, capacity exhaustion, missing entries, malformed entry state, invalid sequence metadata, and default result success. This is TCP control-block metadata only and does not add timers, retransmission, sockets, HTTP handling, BootSequence markers, QEMU presets, or virtio-net packet I/O.

`DnsUdpFrame` now adds a host-tested DNS-over-UDP frame metadata layer over caller-owned buffers only. It writes DNS A-record query payloads through `DnsResolverTransaction`, wraps them with Ethernet/IPv4/UDP headers, parses received virtio-net UDP metadata through `UdpReceiveDispatcher`, validates DNS/client UDP ports, rejects default/zero query and receive results as successful evidence, and forwards only the DNS UDP payload back into the resolver transaction.

`DhcpClientTransactionPlanner` and `DnsResolverTransactionPlanner` compose those packet primitives into caller-driven client transaction boundaries. They preserve nested packet/UDP/protocol statuses and diagnostic metadata, reject default/zero DHCP and DNS planner results as successful evidence, and still avoid timers, sockets, queue ownership, route selection, and live virtio-net I/O.

`FetchAppHttpTcpExchangePlan` now composes an already-prepared FetchApp HTTP TCP context with caller-owned SYN, SYN-ACK, final ACK, request, response, completion, and active-close FIN/ACK frame buffers. It preserves nested context, HTTP TCP GET, completion, and close-plan status metadata and rejects failed context, short endpoint buffers, bad SYN-ACK checksums, undersized request/close frames, and missing expected body markers. This is still a caller-owned frame composition contract only; it does not open sockets, touch PAL, perform DNS/ARP, publish NIC descriptors, run timers, retransmit, execute FetchApp, perform TLS, or prove live network I/O.

`FetchAppHttpTcpExchangePlanDiagnosticFormatter` now serializes initialized exchange results into a stable caller-owned `FETCH_HTTP_TCP_EXCHANGE` ASCII marker. The marker records exchange, context, handshake/request/response/completion/close statuses, host/path byte counts, expected HTTP status and marker length, frame byte counts, and final local/remote sequence numbers without dumping configured host/path/marker values. The formatter reports required/available byte counts for short destinations and rejects default/uninitialized exchange results before formatting. This is diagnostic formatting evidence only; it does not open sockets, touch PAL, publish NIC descriptors, execute FetchApp, or prove live network I/O.

`BootSequence.RunFetchAppHttpTcpExchangeReadiness` now converts the same exchange metadata into opt-in boot markers. Successful exchange results emit `FETCH_APP_HTTP_TCP_EXCHANGE_READY` followed by the existing `FETCH_HTTP_TCP_EXCHANGE` diagnostic payload, and failed/default/formatting failures emit `FETCH_APP_HTTP_TCP_EXCHANGE_NOT_READY` with status-coded metadata. The default boot sequence still does not emit these markers; callers must invoke this readiness gate only after the FetchApp exchange preconditions have been staged.

`Probes.EnableFetchAppHttpTcpExchangeProbe` records the disabled-by-default network-smoke toggle for that future boot wiring. It is a probe-catalog contract only; enabling it is not yet wired to any default boot path and does not execute DNS, ARP, TCP, HTTP, virtio-net, PAL socket, or FetchApp code.

## Implemented Behavior

- `OS.Net.MacAddress` and `OS.Net.Ipv4Address` provide fixed-size value contracts without array-backed instance state. `MacAddress` exposes canonical zero and broadcast checks used by network and driver validation paths so ARP, IPv4 routing, UDP/DNS, DHCP, TCP frame planners, virtio-net, and ENA reject invalid link identities consistently.
- `OS.Net.EthernetHeader` reads and writes 14-byte Ethernet II headers using explicit offsets and big-endian EtherType encoding, and dispatches supported payload kinds for IPv4 and ARP.
- `OS.Net.ArpPacket` reads and writes fixed Ethernet/IPv4 ARP request and reply packets, including sender/target MAC and IPv4 addresses.
- `OS.Net.ArpNeighborResolutionTransaction` models deterministic one-target ARP neighbor resolution over caller-owned Ethernet frames. It writes a broadcast Ethernet + ARP request frame, accepts only matching ARP replies for the requested IPv4 address and local IPv4/MAC identity, records the resolved MAC address, and exposes status-coded rejection for malformed frames, wrong operation, mismatched sender/target fields, invalid state transitions, too-small output buffers, caller-driven timeout, and default/zero result values.
- `OS.Net.ArpNeighborCache` models a bounded resolved-peer table over caller-provided entry storage. It stores IPv4 address, MAC address, entry state, and last-updated monotonic tick metadata; supports lookup, insert/update, remove, clear, and caller-driven stale purge; updates duplicate inserts without consuming capacity; rejects default/zero lookup and mutation results as not successful; and reports deterministic status values for invalid address/MAC input, capacity exhaustion, missing entries, invalid TTL, and non-monotonic purge ticks.
- `OS.Net.ArpNeighborCacheUpdater` validates a caller-provided ARP reply frame through `ArpNeighborResolutionTransaction.ReceiveReply` for the local MAC/IP and requested target IP, then mutates `ArpNeighborCache` only after ARP validation succeeds. It returns nested ARP/cache statuses, packet parse status, resolved MAC, cache count/capacity, update-existing metadata, and default/zero result rejection without sending packets, blocking for replies, or owning NIC receive state.
- `OS.Net.ArpReceiveDispatchCacheUpdater` consumes a successful `NetworkReceiveDispatchResult` for ARP, validates the reply against local MAC/IP and the requested target IP, checks the payload range against the caller-provided receive buffer, and mutates caller-owned `ArpNeighborCache` storage only after validation succeeds. It preserves nested dispatch/ARP/cache statuses and update-existing metadata without reading a NIC, polling queues, routing packets, allocating cache storage, or dispatching sockets.
- `OS.Net.Ipv4NeighborRoutePlanner` resolves a target IPv4 peer from the bounded ARP cache when present, otherwise writes one broadcast ARP request frame through `ArpNeighborResolutionTransaction` into caller-provided storage. It preserves the cache on request writes, reports cache count/capacity metadata, rejects invalid target/local inputs, and returns status-coded undersized-buffer/write failures without enqueuing packets or blocking for replies.
- `OS.Net.Ipv4NeighborRoutePlanDiagnosticFormatter` formats initialized IPv4 neighbor route plan results into a stable caller-owned `IPV4_NEIGHBOR_ROUTE status=` ASCII marker through the shared `OS.Std.Text.AsciiTextBuilder` append path and shared decimal byte counts. The marker records route/action/cache/ARP/packet statuses, bytes written, cache count/capacity, and cached target MAC metadata when a cached route is used. It reports required/available destination bytes for short buffers without partial writes, rejects default/uninitialized route results, rejects unknown enum values before formatting, and contains no managed-reference result metadata.
- `OS.Net.Ipv4Header` reads and writes IPv4 headers without options, computes the IPv4 header checksum when writing, and validates the checksum when parsing.
- `OS.Net.IcmpEchoPacket` writes and parses ICMPv4 echo request/reply packets with identifier, sequence number, checksum validation, caller-provided payload slices, and status-coded failures for short packets, unsupported type/code, bad checksum, and too-small destinations.
- `OS.Net.Ipv4IcmpEchoFrame` composes Ethernet, minimum-header IPv4, and ICMPv4 echo helpers for deterministic caller-buffer echo request/reply frames, including IPv4 protocol dispatch, IPv4 checksum rejection, ICMP checksum validation, and truncated payload rejection.
- `OS.Net.IcmpReceiveDispatcher` consumes `NetworkReceiveDispatcher`, requires an IPv4 payload with protocol `1`, parses ICMPv4 echo request/reply metadata, validates ICMP checksums, and exposes source/destination IPv4 addresses, identifier, sequence number, payload offset, and payload length into the original caller-owned virtio-net RX buffer. It preserves nested classifier/network/ICMP parse statuses and does not copy payload bytes, allocate buffers, own NIC state, route packets, retry echo requests, schedule timers, or feed sockets/Kestrel.
- `OS.Net.UdpHeader` reads and writes UDP headers and payload slices. `EthernetIpv4UdpPacket.TryWrite` writes IPv4 UDP pseudo-header checksums into outbound datagrams, and `UdpReceiveDispatcher` accepts zero IPv4 UDP checksums while validating non-zero checksums.
- `OS.Net.EthernetIpv4UdpPacket` composes the helpers for deterministic Ethernet + IPv4 + UDP frames, emits IPv4 UDP pseudo-header checksums on writes, and rejects unsupported EtherType/protocol values.
- `OS.Net.UdpTransmitPlanner` validates caller-provided local/destination MAC and IPv4 identity plus UDP ports, then delegates Ethernet/IPv4/UDP frame encoding and checksum generation to `EthernetIpv4UdpPacket.TryWrite`. It returns status, nested `PacketParseStatus`, bytes written, and addressing metadata without resolving ARP, owning NIC TX descriptors, or creating sockets.
- `OS.Net.DhcpMessage` writes DHCP discover/request messages into caller-owned buffers and parses received offer/ack options for transaction ID, client MAC, offered address, server identifier, lease time, subnet mask, router, DNS server, and DHCP message type.
- `OS.Net.DhcpClientTransaction` models a deterministic DHCPv4 discover-offer-request-ack transaction over caller-provided message buffers. It validates transaction ID, client MAC, server identifier, offered address, lease time, timeout transitions, and default/zero transition results with status-coded results.
- `OS.Net.DhcpLeaseConfiguration` materializes a validated `DhcpClientLease` into immutable interface configuration metadata. It records address, optional subnet mask, optional router, optional DNS server, server identifier, lease seconds, and explicit presence flags, and rejects missing address, missing server identifier, zero lease time, and non-contiguous/zero subnet masks.
- `OS.Net.DhcpUdpFrame` wraps DHCPDISCOVER and DHCPREQUEST payloads into broadcast Ethernet/IPv4/UDP frames using client port `68`, server port `67`, broadcast destination MAC, and broadcast destination IPv4. It can unwrap either raw Ethernet/IPv4/UDP frames or virtio-net receive buffers through the existing UDP dispatcher, accepts only server-to-client UDP ports `67 -> 68`, parses the DHCP server payload, and returns nested DHCP, packet, and UDP statuses without owning timers, retry policy, queues, lease application, or NIC state.
- `OS.Net.DhcpClientTransactionPlanner` composes the DHCP payload transaction, DHCP-over-UDP frame wrapper, and lease configuration materializer into a host-tested discover/offer/request/ack planning boundary. It writes broadcast DHCPDISCOVER/DHCPREQUEST frames into caller-owned buffers, accepts OFFER/ACK from raw Ethernet or virtio-net receive buffers, preserves nested DHCP/UDP/packet statuses, rejects default/zero planner results as successful evidence, and reports explicit transition failures such as wrong transaction ID, invalid state, missing server identifier, malformed packet, and invalid lease configuration without timers, retries, sockets, live virtio-net I/O, route selection, address assignment, or lease renewal.
- `OS.Net.DnsMessage` writes a single-question A-record query and parses a matching compressed A-record response, including bounds checks for labels and compression pointers. DNS compression pointers must now target earlier bytes in the message; self, forward, and out-of-range pointers fail with status-coded parser results.
- `OS.Net.DnsResolverTransaction` models a deterministic single-question A-record DNS query/response transaction over caller-provided UDP payload buffers. It validates transaction ID, question bytes, answer owner name, unsupported answer types, malformed responses, timeout transitions, and default/zero transition results with status-coded results.
- `OS.Net.DnsUdpFrame` writes a DNS A-record query into an Ethernet + IPv4 + UDP frame using caller-provided local MAC/IP, destination MAC, DNS server IP, and source UDP port. It also consumes caller-provided virtio-net RX buffers through `UdpReceiveDispatcher`, validates DNS/client UDP ports, rejects default/zero query and receive results as successful evidence, and forwards only the UDP payload to `DnsResolverTransaction.ReceiveResponse`. Results preserve nested DNS resolver/message, UDP dispatch, and packet parse statuses without sockets, retransmission, route selection, random transaction ID generation, or live NIC I/O.
- `OS.Net.DnsResolverTransactionPlanner` composes the DNS UDP frame and resolver transaction helpers into a caller-driven DNS client transaction plan. It validates local/server identity, UDP ports, transaction ID, DNS response code, answer presence, malformed DNS payloads, unsupported answer types, and default/zero planner-result rejection while preserving answer IPv4, TTL, response size, and source server metadata without sockets, retries, timers, route selection, DNS caching, DHCP-applied configuration, or live virtio-net DNS I/O.
- `OS.Net.TcpHeader` writes and parses minimum TCP headers, flags, sequence/acknowledgement numbers, window/checksum/urgent fields, aligned options, and payload slices.
- `OS.Net.TcpClientHandshake` models a deterministic client-side SYN, SYN-ACK validation, and final ACK sequence over caller-provided TCP segment buffers, and rejects default/zero transition results as not successful before callers trust handshake state metadata.
- `OS.Net.TcpActiveConnectTransaction` models a deterministic caller-driven `CLOSED -> SYN_SENT -> ESTABLISHED` active connect over caller-provided TCP segment buffers. It writes SYN/final ACK segments, validates SYN-ACK ports, flags, acknowledgement number, optional expected remote sequence number, parse failures, insufficient output buffers, invalid state transitions, explicit timeout transitions, and default/zero transition results with status-coded results.
- `OS.Net.TcpIpv4FramePlanner` composes `TcpActiveConnectTransaction`, `EthernetHeader`, `Ipv4Header`, and `TcpHeader` into a caller-owned Ethernet/IPv4 active-connect frame boundary. It writes SYN and final ACK frames with outbound IPv4 TCP pseudo-header checksums, parses SYN-ACK frames back into the active-connect transaction, validates local/remote MAC addresses, IPv4 addresses, TCP ports, IPv4 protocol, bounds, inbound SYN-ACK IPv4 TCP pseudo-header checksum, flags, acknowledgement number, and optional remote initial sequence number, and preserves diagnostic metadata without timers, retransmission, sockets, NIC queues, established-payload checksum enforcement, or Kestrel behavior.
- `OS.Net.TcpPassiveIpv4FramePlanner` composes `TcpPassiveOpenHandshake`, `EthernetHeader`, `Ipv4Header`, and `TcpHeader` into a caller-owned Ethernet/IPv4 passive-open frame boundary. It accepts a listener SYN frame, writes a SYN-ACK frame with an outbound IPv4 TCP pseudo-header checksum, accepts the final ACK frame, validates local/remote MAC addresses, IPv4 addresses, TCP ports, IPv4 protocol, EtherType, frame bounds, inbound SYN/final ACK IPv4 TCP pseudo-header checksums, SYN/final ACK flags, acknowledgement numbers, and sequence numbers, and exposes endpoint metadata plus local/remote next sequence numbers after the final ACK. It does not implement sockets, listener tables, retransmission, timers, ARP, route selection, established-payload checksum enforcement, virtio-net, or Kestrel behavior.
- `OS.Net.TcpListenerBacklog` models a bounded listener accept backlog over caller-owned `TcpListenerBacklogEntry` storage. It enqueues already-established passive-open endpoint metadata, rejects invalid endpoints, duplicate endpoint tuples, deterministic capacity exhaustion, and default/zero result values, dequeues accepted connections in FIFO order, and marks slots reusable without parsing packets, allocating entries, exporting sockets, owning timers, retransmitting, or touching virtio-net.
- `OS.Net.TcpPassiveListenerBacklogBridge` consumes successful `TcpPassiveIpv4FramePlanResult` metadata only when the passive-open state is established, converts endpoint and next-sequence metadata into `TcpListenerBacklogConnection`, and enqueues it into caller-owned listener backlog storage while preserving passive-frame/TCP/packet/backlog statuses and rejecting default/zero bridge results. It does not parse packets, allocate entries, export sockets, own timers, retransmit, or touch virtio-net.
- `OS.Net.TcpListenerBacklogSocketBridge` moves the oldest caller-owned listener backlog entry into `OSHostSocketTable.EnqueueAcceptedConnection`, preserving local/remote endpoint metadata, mock socket-table failures, empty-backlog rejection, and boundary flags. It drains the backlog only after mock accept-queue enqueue succeeds, and it does not export PAL functions, open live sockets, schedule waits, or start Kestrel.
- `OS.Net.TcpConnectionTable` models caller-owned established TCP connection metadata. It stores endpoint tuple, local/remote next-sequence metadata, and `Established`/`Closing`/`Closed` lifecycle state in caller-supplied value entries, reuses closed slots, rejects invalid/default metadata and malformed states with status-coded results, and does not allocate entries, own timers, dispatch sockets, retransmit, or touch virtio-net.
- `OS.Manifest.NativeAppSocketBindPolicy` validates a future PAL IPv4 socket-bind endpoint against the launch plan's manifest listen endpoint. It accepts numeric IPv4 listen hosts including `0.0.0.0` and rejects missing listen metadata, unsupported host text, invalid endpoints, wrong ports, and wrong addresses without calling PAL socket bind/listen or opening sockets.
- `OS.Manifest.NativeAppSocketBindBridge` composes that policy with `OSHostSocketTable.Bind` only after policy success. It reports mock table bind failures such as endpoint conflicts while keeping policy failures from mutating socket state, and it does not export PAL socket calls or perform live network I/O.
- `OS.Manifest.NativeAppSocketListenBridge` composes the same policy with `OSHostSocketTable` create/bind/listen calls. It reports create, bind, and listen failures separately, keeps policy failures from mutating socket-table state, and remains host-only mock socket composition with no exported PAL calls, live TCP, scheduler waits, or Kestrel behavior.
- `OS.Std.Net.InternetChecksum` provides the shared no-runtime 16-bit ones-complement checksum primitive needed by IPv4, ICMP, UDP, and TCP packet work. It supports one-shot checksums, segmented checksum overloads, validity checks, and an accumulator that carries odd bytes across segment boundaries for pseudo-header/header/payload composition. The IPv4 header and ICMP echo helpers use this shared primitive.
- `OS.Net.Ipv4TransportChecksum` computes and validates IPv4 UDP/TCP pseudo-header checksums over caller-owned transport segment spans, including segmented payload input, while preserving status-coded invalid protocol/length failures and rejecting default/zero result values as not successful. It is enforced for outbound UDP checksum generation through `EthernetIpv4UdpPacket.TryWrite`, `UdpReceiveDispatcher` non-zero IPv4 UDP checksum validation, `TcpIpv4FramePlanner` outbound active-connect SYN/final ACK generation plus inbound SYN-ACK validation, `TcpPassiveIpv4FramePlanner` outbound passive SYN-ACK generation plus inbound SYN/final ACK validation, `TcpConnectionCloseIpv4FramePlanner` outbound active-close FIN/ACK generation, `TcpEstablishedStreamIpv4FramePlanner` established-payload send/receive frame checksums, `TcpReceiveDispatcher` inbound established payload/passive FIN/RST checksum validation plus passive-close ACK checksum writing, and `HttpTcpClientGetPlanner` outbound HTTP GET request checksum generation plus inbound response checksum validation; it is not yet enforced by sockets or live NIC paths.
- `OS.Net.TcpPassiveOpenHandshake` models a deterministic listener-side `LISTEN -> SYN_RECEIVED -> ESTABLISHED` handshake over caller-provided TCP segment buffers. It validates ports, SYN/final ACK flags, sequence and acknowledgement numbers, output buffer size, parse/write failures, and default/zero transition results with status-coded results.
- `OS.Net.TcpEstablishedStream` models established TCP payload state over caller-owned receive storage. It accepts only in-order ACK/PSH payload segments for the expected receive sequence and expected local acknowledgement number, preserves unread bytes across bounded reads, rejects duplicate, out-of-order, and wrong-acknowledgement payload with explicit status codes, rejects default/zero receive, read, and send results as not successful, writes one outgoing payload segment at a time bounded by a caller-provided maximum payload length, and advances local/remote sequence metadata deterministically.
- `OS.Net.TcpEstablishedStreamIpv4FramePlanner` wraps that established-stream primitive in caller-owned Ethernet/IPv4/TCP frames. It writes PSH/ACK payload frames with computed IPv4 TCP pseudo-header checksums, validates inbound frame identity and TCP checksums before stream mutation, preserves stream/packet/frame status metadata, rejects default/zero results as success evidence, and still performs no ARP, routing, sockets, retransmission, timers, NIC queue work, or Kestrel behavior.
- `OS.Net.TcpEstablishedStreamSocketBridge` queues a successful established-stream receive payload into the host-only `OSHostSocketTable` inbound buffer for an already-connected mock socket handle. It rejects failed/default receive results and default/zero bridge results before mock socket mutation, validates payload length against accepted bytes, preserves receive and socket status provenance, and does not export PAL functions, open live sockets, wait on a scheduler, touch NIC I/O, or start Kestrel.
- `OS.Net.TcpConnectionClose` models deterministic established-connection close/reset transitions over caller-provided TCP segment buffers. It writes active and passive FIN/ACK segments, validates ACK and FIN/ACK responses, advances local and remote sequence numbers for FIN consumption, records `FIN_WAIT_1`, `FIN_WAIT_2`, `CLOSE_WAIT`, `LAST_ACK`, `TIME_WAIT`, `CLOSED`, and `RESET` states, and rejects invalid transitions and default/zero transition results with status-coded results.
- `OS.Net.TcpConnectionCloseIpv4FramePlanner` wraps active-close FIN/ACK generation in caller-owned Ethernet/IPv4/TCP frames, delegates close state to `TcpConnectionClose`, emits a non-zero IPv4 TCP pseudo-header checksum, and rejects invalid identity or undersized output without advancing close state. `OS.Net.TcpConnectionCloseIpv4FramePlanDiagnosticFormatter` formats initialized frame-plan results into a stable caller-owned `TCP_CLOSE_IPV4_FRAME` marker with frame-plan status, close state, nested TCP close status, packet status, byte counts, TCP segment range, port, TTL, identification, and sequence metadata, using shared `OS.Std.Text` ASCII builder/decimal helpers for byte accounting and writing.
- `OS.Net.TcpReceiveDispatcher` consumes caller-owned receive buffers plus already-parsed network dispatch metadata, validates IPv4/TCP identity and checksums, rejects malformed or out-of-window ACK/FIN/RST control metadata before mutation, routes established PSH/ACK payloads into `TcpEstablishedStream`, routes passive FIN/ACK and RST segments into `TcpConnectionClose`, and writes the passive-close ACK into caller-owned storage. It preserves nested network, TCP parse/checksum, stream, and close status metadata while avoiding sockets, live NIC I/O, TLS, timers, retransmission, and full TCP state-machine claims.
- `OS.Net.TcpReceiveDispatchDiagnosticFormatter` formats initialized receive-dispatch results into a caller-owned ASCII marker beginning with `TCP_RX_DISPATCH` through the shared `OS.Std.Text.AsciiTextBuilder` append path. It reports required and available destination bytes, rejects short destinations without partial success, rejects default/uninitialized or unknown enum values, and includes dispatch/action/network/TCP packet/stream/close/checksum status names plus payload bytes, passive-ACK bytes, TCP segment range, and local/remote sequence metadata without allocating or touching socket/NIC state.
- `OS.Net.HttpRequestBuilder` writes deterministic HTTP/1.1 GET requests with `Host`, `User-Agent`, `Accept`, and `Connection: close` headers into caller-owned buffers, rejects unsafe host/path bytes, and rejects default/zero result values as not successful before HTTP callers trust request metadata.
- `OS.Net.HttpRequestParser` parses HTTP/1.x request lines, origin-form targets, host/content-length/connection/transfer-encoding headers, and fixed-length bodies from caller-owned byte spans. It rejects unsupported methods, malformed targets/protocol, invalid headers, duplicate content-length metadata, unsupported transfer encodings, truncated bodies, and default/zero result values before future server-side HTTP paths trust request metadata. Header field-values are raw-validated first, then semantic handling trims ASCII space/tab OWS through `OS.Std.Text.AsciiString.TrimSpaceOrTab`; the leading OWS scan and shared string trimming both use `OS.Std.Ascii.IsSpaceOrTab`.
- `OS.Net.HttpResponseWriter` writes deterministic HTTP/1.1 response bytes with status line, `Content-Length`, `Content-Type`, `Connection`, CRLF header terminator, and caller-provided body into caller-owned buffers. It rejects invalid status codes, unsafe reason phrases, unsafe content types, undersized destinations, and default/zero result values before future server-side HTTP paths trust response metadata.
- `OS.Net.HttpStaticResponseTransaction` composes the request parser and response writer into a bounded caller-buffer server-side transaction for a single static route. It emits `200 OK` for a matching GET target, `404 Not Found` for a different GET target, and `405 Method Not Allowed` for parsed non-GET methods; it preserves request/response status metadata, reports short response buffers without claiming live socket behavior, and rejects default/zero result values as successful evidence. `OS.Net.HttpStaticResponseTransactionDiagnosticFormatter` formats initialized transaction metadata into a caller-owned `HTTP_STATIC_RESPONSE` marker without dumping request body or configured response body bytes. It records transaction/request/response statuses, method, HTTP status code, target/method/connection-close booleans, request-target length, byte counts, body range, and explicit `liveSocket=false` / `startsKestrel=false` boundary flags.
- `OS.Net.HttpStaticTcpResponsePlan` consumes an already-successful `TcpReceiveDispatchResult` with accepted established payload metadata, slices the caller-owned receive buffer at the TCP payload range, and delegates to `HttpStaticResponseTransaction` to write a static HTTP response into a caller-owned destination. It preserves TCP dispatch status/action, request payload range, HTTP transaction status, request/response statuses, HTTP status code, and response byte/body metadata while rejecting default dispatch results, failed TCP dispatch, non-payload dispatches, invalid payload ranges, and short response buffers. `OS.Net.HttpStaticTcpResponsePlanDiagnosticFormatter` formats initialized plan metadata into a caller-owned `HTTP_STATIC_TCP_RESPONSE` marker without dumping request bytes or configured/response body bytes. It records plan/TCP/HTTP statuses, method, HTTP status code, request payload offset/bytes, response byte counts, response body range metadata, and explicit `opensSocket=false`, `writesTcpFrame=false`, `performsNicIo=false`, and `startsKestrel=false` boundary flags. `BootSequence.RunHttpStaticTcpResponseReadiness` wraps that already-produced plan metadata into opt-in `HTTP_STATIC_TCP_RESPONSE_READY` / `HTTP_STATIC_TCP_RESPONSE_NOT_READY` boot markers without changing the default boot path. It allocates no buffers, opens no sockets, writes no TCP response frame, performs no NIC I/O, and does not start hosted .NET, ASP.NET Minimal API, or Kestrel.
- `OS.Net.HttpStaticTcpResponseIpv4FramePlan` composes the static TCP response plan with `TcpEstablishedStreamIpv4FramePlanner` to write one outbound Ethernet/IPv4/TCP PSH/ACK response frame into a caller-owned frame buffer. It reconstructs established-send metadata from the accepted TCP dispatch ports and sequence numbers, rejects failed static response planning before frame writes, rejects complete HTTP responses larger than the configured single-frame payload limit before calling the frame planner, preserves nested response/frame status metadata, emits a non-zero IPv4 TCP pseudo-header checksum through the existing frame planner, and still performs no socket I/O, live NIC I/O, descriptor publication, interrupts, timers, retransmission, TLS, hosted .NET, ASP.NET Minimal API, or Kestrel work.
- `OS.Net.HttpResponseParser` parses HTTP/1.0 and HTTP/1.1 response status lines, selected headers, required `Content-Length`, connection metadata, and fixed-length bodies from caller-owned byte spans, and rejects missing/invalid/duplicate content length, incomplete headers, short bodies, unsupported transfer encodings, and default/zero result values before HTTP callers trust response metadata. Header field-values are raw-validated first, then semantic handling trims ASCII space/tab OWS through `OS.Std.Text.AsciiString.TrimSpaceOrTab`; HTTP client diagnostic formatters now serialize duplicate response `Content-Length` as `DuplicateContentLength` instead of an unknown status.
- `OS.Net.HttpClientGetTransaction` combines the request writer and response parser into a deterministic state machine over caller-owned byte buffers that represent an already-established TCP stream. It records request bytes, response status/body metadata, rejects default/zero result values, and reports status-coded malformed/unsupported/truncated response failures. `OS.Net.HttpClientGetTransactionDiagnosticFormatter` formats those result fields into a stable `HTTP_CLIENT_GET_TRANSACTION` ASCII marker in caller-owned storage using shared `OS.Std.Text.AsciiDecimalFormatter` byte counts and `AsciiTextBuilder` appends, reports required/available byte counts for short destinations without partial success, and rejects default/zero transaction results before formatting.
- `OS.Net.HttpTcpClientGetPlanner` composes `TcpIpv4FramePlanner`, `TcpEstablishedStream`, and `HttpClientGetTransaction` into a caller-owned-buffer HTTP GET transaction plan. It writes SYN and final ACK frames through the TCP IPv4 frame planner, accepts a checksum-validated SYN-ACK frame, writes one complete HTTP GET request as a PSH/ACK TCP payload frame with a computed IPv4 TCP pseudo-header checksum, validates the inbound response TCP pseudo-header checksum before stream acceptance, accepts one in-order response payload frame, parses the HTTP response, and rejects default/zero planner results as successful evidence while preserving nested TCP frame, TCP stream, HTTP request, HTTP response, and packet statuses. `OS.Net.HttpTcpClientGetPlanDiagnosticFormatter` formats those existing result fields into a stable `HTTP_TCP_GET_PLAN` ASCII marker in caller-owned storage using shared `OS.Std.Text.AsciiDecimalFormatter` byte counts and `AsciiTextBuilder` appends, reports required/available byte counts for short destinations without partial success, and rejects default/zero planner results before formatting. `OS.Net.FetchAppHttpRequestEndpoint` derives caller-owned FetchApp HTTP request endpoint bytes from already-parsed manifest environment/config entries. It accepts `FETCH_HTTP_URL` or explicit `FETCH_HTTP_HOST`, `FETCH_HTTP_PORT`, and `FETCH_HTTP_PATH`, defaults to `example.com:80/` when no request endpoint is configured, copies host/path into caller-provided buffers, rejects `https`/non-HTTP schemes, missing hosts, invalid ports, empty paths, oversized fields, short destinations, and failed manifest parses, and does not perform DNS, TCP, or HTTP I/O. `OS.Net.FetchAppHttpRequestEndpointDiagnosticFormatter` formats initialized endpoint metadata into a stable `FETCH_HTTP_ENDPOINT` ASCII marker in caller-owned storage using shared `OS.Std.Text.AsciiDecimalFormatter` byte counts and `AsciiTextBuilder` appends, includes status, initialization, port, host/path byte counts, required byte counts, and config-source flags without dumping host/path values, reports required/available bytes for short destinations without partial success, and rejects default/uninitialized endpoint results. `BootSequence.RunFetchAppHttpRequestEndpointReadiness` turns the same endpoint metadata into host-tested `FETCHAPP_HTTP_ENDPOINT_READY` / `FETCHAPP_HTTP_ENDPOINT_NOT_READY` boot markers without dumping host/path values and without changing the default boot path. `OS.Net.FetchAppHttpTcpContextPlan` composes an already-successful FetchApp endpoint, already-successful DNS resolver transaction result, cached IPv4 neighbor-route result, and caller-provided local TCP identity into `HttpTcpClientGetPlannerInfo` for the existing HTTP GET TCP planner. It validates endpoint/DNS host fingerprint parity, requires a cached ARP route before TCP planning, rejects invalid local identity, and preserves nested endpoint/DNS/route statuses without opening sockets or transferring packets. `OS.Net.FetchAppHttpCompletionExpectation` derives the expected HTTP status and expected body/content marker from parsed native manifest environment/config entries before completion evaluation, with deterministic defaults of status `200` and marker `FETCH_OK`. It accepts `FETCH_HTTP_EXPECTED_STATUS`, `FETCH_HTTP_EXPECTED_MARKER`, `FETCH_HTTP_EXPECTED_BODY`, and `OS_SMOKE_EXPECTED_BODY`, rejects malformed status codes, empty markers, oversized markers beyond the fixed evidence buffer, missing required marker config when requested, and failed manifest parses with status-coded results. `OS.Net.FetchAppHttpCompletionExpectationDiagnosticFormatter` formats initialized expectation metadata into a stable `FETCH_HTTP_EXPECTATION` ASCII marker in caller-owned storage, includes expectation status, initialization state, expected HTTP status, marker length, and config-source flags without dumping marker/body bytes, reports required/available byte counts for short destinations without partial success, and rejects default/uninitialized expectation results. `OS.Manifest.NativeManifestFetchAppConfigPreflight` wraps that expectation for already-loaded native manifests, copies the selected marker into caller-owned scratch storage, and preserves nested manifest/expectation status metadata before later FetchApp completion checks consume the config. `OS.Net.FetchAppHttpCompletionPlan` consumes an already-successful HTTP TCP GET plan plus caller-owned response bytes, validates the expected HTTP status and non-empty expected ASCII body marker, bounds-checks body offset/length before scanning, and returns status-coded FetchApp completion metadata without allocating or performing I/O. `OS.Net.FetchAppHttpCompletionPlanDiagnosticFormatter` formats those completion fields into a stable `FETCH_HTTP_COMPLETION` ASCII marker in caller-owned storage, reports required/available byte counts for short destinations without partial success, and rejects default/uninitialized completion results before formatting. `BootSequence.RunFetchAppHttpCompletionReadiness` turns the same completion metadata into host-tested ordered boot readiness markers for future FetchApp handoff diagnostics. `OS.Net.FetchAppHttpTcpExchangePlan` composes the endpoint, TCP GET, completion, and active-close pieces into one caller-owned frame exchange, `OS.Net.FetchAppHttpTcpExchangePlanDiagnosticFormatter` emits the stable `FETCH_HTTP_TCP_EXCHANGE` diagnostic marker, and `BootSequence.RunFetchAppHttpTcpExchangeReadiness` wraps that diagnostic into opt-in `FETCH_APP_HTTP_TCP_EXCHANGE_READY` / `FETCH_APP_HTTP_TCP_EXCHANGE_NOT_READY` boot markers without dumping configured host/path/marker bytes or changing default boot markers. These helpers do not implement sockets, live NIC I/O, retransmission, timers, TLS, redirects, chunked transfer, live FetchApp execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.
- `OS.Pal.OSHostSocketTable` now models host-only connected socket handles after accept. Connected handles have deterministic inbound and outbound byte buffers, partial receives that preserve unread bytes, partial sends that report exact transferred byte counts and `WouldBlock` on full outbound buffers, peer close that drains unread inbound bytes before `EndOfFile`, peer reset that reports `ConnectionReset`, poll readiness for readable/writable/accepted/hangup/error states, Kestrel-oriented NoDelay/ReuseAddress/timeout/keep-alive option storage, local/remote endpoint queries, deterministic ReuseAddress bind policy, and shutdown behavior that interrupts the matching I/O direction.
- `OS.Pal.OSHostSocketFacade` is a host-tested `OSHost_*`-shaped adapter over the mock socket table for create/bind/listen/accept/send/receive/poll/shutdown/options/address-query/close status translation. It does not export unmanaged symbols or perform network I/O.
- `OS.Drivers.VirtioPciCapabilityListParser` decodes modern virtio PCI capability metadata from caller-provided PCI config-space bytes and a capability-list start offset. It recognizes vendor-specific capability ID `0x09`, records common/notify/ISR/device configuration BAR, offset, length, and notify multiplier metadata, and rejects malformed capability lists with status codes without live config reads, BAR mapping, feature negotiation, or packet I/O.
- `OS.Drivers.VirtioPciDeviceDiscovery` identifies modern virtio network PCI functions from vendor `0x1AF4`, device ID `0x1041`, and required common/notify/ISR/device-configuration capability metadata. It is shared with storage for virtio-blk identification and does not perform ECAM scans, BAR mapping, feature negotiation, RX/TX queue setup, interrupts, or packet transfer.
- `OS.Drivers.PciVirtioDescriptorCatalogPlanner` composes injected ECAM scan metadata, modern virtio candidate selection, fake-derived BAR-size binding, caller-provided network feature/queue metadata, and descriptor cataloging into bounded virtio-net descriptor metadata. It preserves segment/BDF and fake BAR-size provenance and reports nested failures without live PCI enumeration, live config access, BAR mapping, MMIO, DMA, queue setup, interrupts, or packet transfer.
- `OS.Drivers.VirtioPciBarRegisterWindowPlanner` validates caller-provided mapped-BAR window records against successful virtio PCI binding/aperture metadata for common, notify, ISR, and device-configuration regions. It plans aligned register-access addresses for a future accessor without mapping BARs, performing volatile MMIO, reading or writing registers, configuring queues, notifying devices, processing interrupts, allocating/mapping DMA memory, or moving packets.
- `OS.Drivers.VirtioPciBarRegisterWindowLiveAccessorBoundary` consumes those checked access plans and delegates only planned `Read16`, `Write16`, or `Write64` operations to an injected `IVirtioPciRegisterAccessor`. It preserves capability, BAR index, BAR kind, relative offset, width, BAR-relative offset, physical address, virtual address, value, and accessor status metadata while rejecting missing plans, missing accessors, unsupported widths, and propagated accessor failures. The boundary contains no live MMIO implementation, no port-I/O implementation, no BAR mapping, no queue configuration by itself, no DMA allocation/mapping, no interrupts, no polling, and no packet I/O.
- `OS.Drivers.VirtioDeviceInitializationPlan` records generic virtio status/feature negotiation intent for future network drivers: `ACKNOWLEDGE`, `DRIVER`, `FEATURES_OK`, `DRIVER_OK`, `FAILED`, selected feature masks, missing required features, `FEATURES_OK` rejection, and queue-count validation over caller-provided metadata only.
- `OS.Drivers.VirtioNetQueuePlan` records virtio-net RX/TX queue requirements over caller-provided metadata. It requires at least two available queues, validates RX/TX split-ring queue sizes through `VirtqueueSplitRingPlan`, bounds frame buffers to a virtio-net header plus Ethernet frame without FCS, records RX buffers as device-writable, records TX header/frame descriptors as device-readable, rejects default/zero result values as not successful, and reports required RX buffer bytes plus one TX frame plan without DMA mapping, descriptor publication, device notification, or virtio-net header parsing.
- `OS.Drivers.VirtioNetQueueMmioSetupPlanner` converts successful queue bring-up metadata plus caller-provided RX/TX physical split-ring addresses and device queue metadata into deterministic common-configuration command plans. `OS.Drivers.VirtioNetQueueMmioSetupExecutor` executes those plans only through an injected fake `IVirtioPciRegisterAccessor`, preserving setup-plan, short-command-source, missing-accessor, read/write, and mismatch failures without BAR mapping, live MMIO, descriptor publication, notification, DMA allocation/mapping, interrupts, or packet transfer.
- `OS.Drivers.VirtioNetQueueMmioSetupRegisterWindowPlanner` maps each queue setup command into a checked common-configuration `VirtioPciBarRegisterWindowAccessPlan` over caller-owned destination storage. It preserves setup and register-window failure status, rejects failed setup plans, short command sources, undersized destinations, unsupported command widths, and propagated register-window failures, and performs no BAR mapping, volatile MMIO, fake or live register reads/writes, hardware queue configuration, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueMmioSetupRegisterWindowExecutor` fake-executes those same setup commands through checked common-configuration register-window access plans over caller-owned common BAR-window bytes. It stages expected queue-size and notify-offset read values for byte-span tests, preserves nested register-window/access-execution statuses, rejects failed setup plans and short command sources before touching bytes, and performs no BAR mapping, volatile/live MMIO, physical-address access, memory barriers, live queue configuration, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationPlanner` composes successful virtio-net queue MMIO setup metadata with successful RX or TX descriptor publication and the generic `VirtioPciQueueNotificationPlanner`. It returns queue index, `queue_notify_off`, notify relative offset, 16-bit notify value, write width, published descriptor count, and available-ring index provenance, rejects default/zero result values as not successful, and preserves setup, publish, disabled-queue, unsupported notification-data, invalid-aperture, and notify-range failures without MMIO, memory barriers, or packet transfer.
- `OS.Drivers.VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess` maps a successful RX/TX notification plan into a checked notify-aperture `VirtioPciBarRegisterWindowAccessPlan` while preserving notification-plan, PCI notification, queue, offset, write-value, descriptor-count, available-ring, and register-window status metadata. It performs no BAR mapping, volatile MMIO, fake or live register writes, memory barriers, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationRegisterWindowExecutor` fake-executes a successful RX/TX notification plan through the generic checked register-window access executor over caller-owned notify BAR-window bytes. It preserves queue, notify offset, write value, BAR-relative offset, access width, and nested notification/register-window/access-execution status metadata while rejecting failed notification plans, propagated register-window failures, and propagated byte-span access failures without BAR mapping, volatile/live MMIO, memory barriers, live device notification, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationRegisterWindowBarrierExecutor` adds the explicit publish-before-notify readiness gate to the fake register-window notification path. It preserves barrier metadata plus nested notification/register-window/access-execution statuses, rejects missing barrier readiness before touching caller-owned notify bytes, and performs no real memory barrier, BAR mapping, volatile/live MMIO, live device notification, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetReceiveReadinessPlanner` composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, RX notification planning, and barrier-gated notify register-window fake execution. It preserves nested setup, register-window, publish, barrier, notification-plan, and access-execution status provenance while rejecting failed setup execution before publication, failed publication before notification, notification-plan failures before fake notification writes, missing barriers, and notify window access failures. It performs no live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, receive completion, or live device notification.
- `OS.Drivers.VirtioNetReceiveRegisterWindowCompletionPlanner` composes successful RX register-window readiness, one caller-owned used-ring completion, and receive-frame classification over a caller-provided receive buffer. It preserves readiness, notification-window, completion, classifier, packet, descriptor-head, buffer-index, used-ring, and payload metadata while rejecting failed readiness before queue-memory reads, no-completion before classification, classifier failures, and default/zero result values without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetReceiveRegisterWindowSlabCompletionPlanner` selects the completed caller-owned receive buffer from a caller-owned RX slab before classification. It preserves readiness, slab-selection, completion, classifier, packet, used-ring, selected-buffer, descriptor-head, and payload metadata while rejecting failed readiness, no completion, unexpected used indexes, unexpected descriptor heads, short slabs, and classifier failures without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetReceiveRegisterWindowDispatchPlanner` feeds the completed caller-owned RX slab buffer into `NetworkReceiveDispatcher` after slab selection and receive-frame classification succeed. It preserves slab-completion, classifier, packet, ARP/IPv4 dispatch, selected-buffer, descriptor-head, and used-ring metadata while rejecting failed slab completion, dispatcher failures, and default/zero result values without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, live device notification, or network-state mutation.
- `OS.Drivers.VirtioNetReceiveRegisterWindowTcpDispatchPlanner` feeds the
  selected caller-owned RX slab buffer plus already-produced network dispatch
  metadata into `TcpReceiveDispatcher`. It preserves nested RX dispatch,
  slab-completion, network-dispatch, and TCP receive metadata while rejecting
  failed RX dispatch, selected-buffer range failures, unsupported protocol,
  bad TCP checksum, TCP dispatch failure, failed stream mutation, and default
  result paths without live MMIO, packet I/O, sockets, or Kestrel behavior.
  `OS.Drivers.VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter`
  formats initialized results into caller-provided byte buffers, reports
  required and available byte counts for short destinations, rejects default
  results, and uses `OS.Std.Text.AsciiTextBuilder` plus shared decimal byte
  counts instead of allocating or adding formatter-local integer loops.
- `OS.Drivers.VirtioNetQueueNotificationExecutor` consumes a successful queue notification plan plus explicit descriptor-write, available-ring-write, and ordered-before-notify readiness metadata before writing the planned 16-bit queue notification through an injected fake sink. `VirtioPciNotifyRegisterAccessorSink` adapts that sink contract to a fake `IVirtioPciRegisterAccessor.Write16` path and maps accessor not-ready, range, unsupported-width, and write failures into the existing notification execution statuses. These paths reject default/zero result values as not successful, preserve nested planner, barrier, queue, offset, write-value, and sink/accessor status provenance, and perform no live MMIO, BAR mapping, interrupts, DMA mapping/allocation, queue-memory reads, descriptor-state mutation, or packet I/O.
- `OS.Drivers.VirtioNetTransmitLifecycle` publishes a caller-owned no-offload TX header/frame pair into caller-owned split-ring queue memory and rejects default/zero result values as not successful. It preserves queue-plan, descriptor, available-ring, header-write, frame-size, and physical-address metadata without DMA allocation, MMIO, device notification, completion waiting, or packet transmission.
- `OS.Drivers.VirtioNetTransmitCompletionPlanner` consumes a successful TX descriptor publication result and successful fake-sink or register-window TX queue-notification execution provenance, reads one caller-owned split-ring used entry, validates the used index advanced exactly once, requires the completed descriptor head to match the published TX chain, requires `used.len == 0` for the current no-offload TX baseline, and returns descriptor reclaim metadata without MMIO, polling, interrupts, DMA mapping, descriptor-allocator mutation, or packet I/O.
- `OS.Drivers.VirtioNetTransmitDescriptorOwnershipPlan` defines the current TX descriptor ownership states, `Free` and `InFlight`. `MarkPublished` consumes only a successful `VirtioNetTransmitLifecycleResult` plus a caller-owned descriptor state span and marks the TX header/frame descriptors in-flight. `ApplyCompletion` consumes only a successful `VirtioNetTransmitCompletionPlanResult` and frees descriptors that are currently in-flight. The result rejects default/zero values as successful evidence, both paths are status-coded, and they avoid MMIO, live device access, queue-memory reads, packet transfer, and hidden allocation.
- `OS.Drivers.VirtioNetTransmitDescriptorFreeListPlanner` selects the first contiguous two-descriptor TX pair from a read-only caller-owned descriptor state span. It rejects default/zero result values as success, requires a successful queue plan, validates TX queue state-table capacity, rejects invalid descriptor state values, skips `InFlight` descriptors, reports `NoFreeDescriptorPair` when capacity is exhausted, and performs no MMIO, DMA mapping, packet I/O, queue-memory reads, interrupts, device polling, or state-table mutation.
- `OS.Drivers.VirtioNetTransmitDescriptorPublishPlanner` composes free-list selection, `VirtioNetTransmitLifecycle.Publish`, and `VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished`. It rejects default/zero result values as success, selects the first reusable contiguous TX header/frame pair, writes caller-owned no-offload TX header and queue descriptor/available-ring memory, marks only the selected descriptors `InFlight` after successful publication, preserves state on publish failure, and performs no MMIO, notification, polling, interrupts, DMA mapping/allocation, packet I/O, used-ring waiting, or queue-memory reads.
- `OS.Drivers.VirtioNetTransmitReadinessPlanner` composes successful fake queue setup execution, caller-owned TX descriptor publication, explicit barrier-readiness metadata, TX notification planning, and fake register-accessor-backed notification execution. It preserves nested setup, publish, barrier, notification-plan, notification-execution, and accessor status provenance while rejecting failed setup execution before publication, failed publication before notification, missing barriers before fake writes, and fake notification failures before completion planning. It performs no live MMIO, BAR mapping, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetTransmitRegisterWindowReadinessPlanner` composes successful queue setup register-window fake execution, caller-owned TX descriptor publication/state marking, explicit barrier-readiness metadata, TX notification planning, and barrier-gated notify register-window fake execution. It preserves nested setup, register-window, publish, ownership, barrier, notification-plan, and access-execution status provenance while rejecting failed setup execution before publication, failed publication before notification, notification-plan failures before fake notification writes, missing barriers, and notify window access failures. It performs no live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, completion waiting, or live device notification.
- `OS.Drivers.VirtioNetTransmitRegisterWindowReclaimPlanner` composes successful TX register-window readiness, one caller-owned used-ring completion validation, and descriptor-state reclaim. It preserves readiness, register-window notification, completion, and ownership status provenance while rejecting failed readiness, missing completion, and ownership-state mismatches without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetDeviceConfig` parses caller-provided virtio-net device configuration bytes. It reads the first six bytes as a MAC address, optionally reads the little-endian 16-bit status field at offset 6 when a complete field is present, exposes `HasStatus`, raw status bits, link-up, and announce helpers, and rejects short input, all-zero MAC addresses, and broadcast MAC addresses without reading MMIO or touching device state.
- `OS.Drivers.VirtioNetDeviceConfigRegisterWindowReader` fake-reads the same MAC/status bytes through checked device-configuration register-window access plans over caller-owned device BAR-window bytes, writes caller-owned scratch storage, and parses that scratch through `VirtioNetDeviceConfig.TryParse`. It preserves scratch-buffer, register-window, access-execution, and parse failures without BAR mapping, volatile/live MMIO, feature negotiation, queue setup, DMA, interrupts, or packet I/O.
- `OS.Drivers.VirtioNetHeader` parses the baseline 10-byte virtio-net header used when mergeable buffers are not negotiated, records flags, GSO type, header length, GSO size, checksum start, and checksum offset, rejects unsupported checksum/offload/GSO state for the current no-offload baseline, validates that received buffers contain a complete Ethernet header after the virtio header, exposes Ethernet frame and payload offset/length metadata without copying packet bytes, and writes a zeroed no-offload TX header into caller-owned storage.
- `OS.Drivers.VirtioNetReceiveFrameClassifier` combines the no-offload virtio-net RX metadata with Ethernet dispatch. It returns classifier status, `PacketParseStatus`, virtio metadata, Ethernet header, payload kind, payload offset, and payload length for caller-owned receive buffers, rejects default/zero result values as not successful, and leaves ARP/IPv4 body parsing to later protocol layers.
- `OS.Net.NetworkReceiveDispatcher` consumes `VirtioNetReceiveFrameClassifier.Classify`, preserves nested classifier and packet parse statuses, and parses ARP packet or IPv4 header metadata from caller-owned RX buffers. It does not allocate, update the ARP cache, route packets, or dispatch TCP/UDP/DHCP payloads.
- `OS.Net.UdpReceiveDispatcher` consumes `NetworkReceiveDispatcher`, requires an IPv4 payload with protocol `17`, parses UDP header metadata, accepts IPv4 UDP checksum zero, validates non-zero UDP checksums, and exposes source/destination IPv4 addresses, ports, payload offset, and payload length into the original caller-owned virtio-net RX buffer. It preserves nested classifier/network/UDP parse statuses and does not copy payload bytes, allocate buffers, own NIC state, or route payloads into DHCP, DNS, sockets, or Kestrel.
- Packet APIs use `Span<byte>`/`ReadOnlySpan<byte>`, explicit length checks, and `PacketParseStatus` values instead of exceptions for normal malformed-input handling.
- Packet integer fields now use `OS.Std.Binary.EndianPrimitives` from `std/no-runtime/shared`; the local `NetworkByteOrder` helper was removed.

## Parser Primitive Rule

Network packet parsers and encoders for Ethernet, ARP, IPv4, ICMP, UDP, DHCP, DNS, TCP, HTTP framing, virtio-net descriptors, and future socket/PAL packet boundaries must use `OS.Std.Binary.EndianPrimitives` from `std/no-runtime/shared` for fixed-width integer fields before adding parser-local byte shifting.

Kernel-facing packet code should prefer the non-throwing `TryRead` and `TryWrite` variants so truncated frames and malformed packets return parse/status values instead of relying on exceptions. Direct read/write variants are allowed only where a previous explicit length check proves the span is large enough.

## Unsupported Behavior

Beyond the qualified finite M07-026 profile, the following remain incomplete:

- General continuing workload/device APIs, multi-buffer receive assembly,
  checksum offload/GSO, dynamic ARP/routing, IPv4 fragmentation/options and
  general ICMP/UDP delivery.
- Live DHCP lease/retry behavior, DNS packet I/O/cache/CNAME/AAAA, and a general
  TCP stack including unsupported congestion/transport behavior.
- Application socket/epoll integration, exported manifest-approved/PAL socket
  operations, real HTTP client/server socket workloads and guest Kestrel.

Legacy packet-buffer helpers and the host-only `OSHost_*` mock tables retain
those individual scopes. Step1332 qualifies the explicit bounded retained
queue/TCP owners; it does not qualify the mock tables as live application sockets.

## Tests

`tests/OS.HostTests/NetworkPacketTests.cs` covers:

1. deterministic Ethernet + IPv4 + UDP byte encoding and parse round-trip,
2. invalid Ethernet frame length,
3. unsupported EtherType,
4. unsupported IPv4 protocol,
5. unsupported IPv4 options,
6. bad IPv4 header checksum,
7. truncated IPv4 payload,
8. truncated UDP payload,
9. source-guarded UDP and ICMP payload copies through
   `OS.Std.Memory.BufferOperations.MemoryCopy`.

`tests/OS.HostTests/ArpPacketTests.cs` covers:

1. deterministic ARP request byte encoding and parse round-trip,
2. deterministic ARP reply byte encoding and parse round-trip,
3. unsupported hardware type,
4. unsupported protocol type,
5. invalid hardware and protocol address lengths,
6. unsupported opcode,
7. truncated ARP packet,
8. Ethernet dispatch recognition for ARP and IPv4.

`tests/OS.HostTests/ArpNeighborResolutionTransactionTests.cs` covers:

1. deterministic broadcast Ethernet + ARP request frame writing,
2. matching ARP reply acceptance and resolved MAC recording,
3. wrong sender IP, wrong target IP, wrong operation, malformed frame, wrong local MAC, wrong Ethernet destination/source, unsupported EtherType, and invalid state rejection,
4. caller-driven timeout from `RequestSent`,
5. default/zero result rejection.

`tests/OS.HostTests/ArpNeighborCacheTests.cs` covers:

1. insert and lookup of resolved IPv4 peers,
2. duplicate insert updating an existing entry without consuming capacity,
3. remove and clear behavior with count metadata,
4. capacity exhaustion for full and zero-capacity caller storage,
5. stale purge using caller-provided current ticks and TTL,
6. invalid address, MAC, TTL, and non-monotonic tick inputs,
7. default/zero lookup and mutation result rejection.

`tests/OS.HostTests/ArpNeighborCacheUpdaterTests.cs` covers:

1. inserting a cache entry from a validated ARP reply,
2. refreshing an existing cache entry from a validated ARP reply,
3. rejecting wrong sender IPv4 address and wrong target MAC replies through the nested ARP transaction status,
4. reporting cache capacity failure after ARP validation succeeds,
5. leaving the cache unchanged when ARP validation fails,
6. default/zero updater result rejection.

`tests/OS.HostTests/ArpReceiveDispatchCacheUpdaterTests.cs` covers:

1. inserting a cache entry from a successfully dispatched ARP reply,
2. refreshing an existing cache entry from a successfully dispatched ARP reply,
3. rejecting non-ARP dispatch metadata without mutating the cache,
4. rejecting ARP replies for the wrong local target,
5. preserving existing cache entries when ARP validation fails,
6. rejecting receive buffers whose payload range does not contain the dispatched ARP payload,
7. no-NIC-I/O boundary flags and result layout,
8. default/zero receive-dispatch cache-update result rejection.

`tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs` covers:

1. cached IPv4 peer lookup returning the resolved MAC without needing a request buffer,
2. cache-miss ARP request frame writing through the existing ARP transaction helper,
3. cache preservation when writing an ARP request for an unresolved peer,
4. undersized ARP request buffer rejection,
5. unchanged cache count/capacity metadata when no cache mutation occurs,
6. invalid target input rejection without touching the output frame,
7. default/zero result values rejected as not successful,
8. cached-route diagnostic marker formatting with target MAC metadata,
9. ARP request and ARP request-failure diagnostic marker formatting,
10. short diagnostic destinations reporting required/available byte counts without partial writes,
11. default/uninitialized and unknown-enum diagnostic input rejection,
12. no-managed-reference diagnostic result metadata.

`tests/OS.HostTests/DhcpMessageTests.cs` covers:

1. DHCP discover message writing,
2. DHCP offer option parsing,
3. DHCP request message writing,
4. malformed packet rejection.

`tests/OS.HostTests/DhcpClientTests.cs` covers:

1. discover-offer-request-ack transition flow,
2. mismatched transaction ID rejection,
3. discover timeout transition to failed state,
4. default/zero transition-result rejection.

`tests/OS.HostTests/DhcpLeaseConfigurationTests.cs` covers:

1. materializing full DHCP lease metadata,
2. materializing partial lease metadata with optional subnet/router/DNS omitted,
3. rejecting missing address,
4. rejecting missing server identifier,
5. rejecting zero lease time,
6. rejecting invalid subnet mask metadata.

`tests/OS.HostTests/DhcpUdpFrameTests.cs` covers:

1. DHCPDISCOVER broadcast Ethernet/IPv4/UDP frame writing,
2. DHCPREQUEST broadcast Ethernet/IPv4/UDP frame writing,
3. DHCP server reply payload extraction from both virtio-net receive buffers and raw Ethernet frames,
4. wrong UDP port rejection,
5. short output frame rejection,
6. malformed DHCP reply payload rejection with nested DHCP status,
7. default/zero write-result rejection,
8. default/zero read-result rejection.

`tests/OS.HostTests/DhcpClientTransactionPlannerTests.cs` covers:

1. DHCP discover/offer/request/ack planning through Ethernet frames,
2. receive-buffer OFFER/ACK handling through the virtio-net UDP dispatch path,
3. wrong transaction ID and wrong UDP port rejection,
4. malformed server reply rejection,
5. invalid state transitions,
6. lease configuration failure propagation,
7. default/zero planner-result rejection.

`tests/OS.HostTests/DnsMessageTests.cs` covers:

1. A-record query writing,
2. compressed A-record response parsing,
3. malformed compression rejection,
4. forward compression pointer rejection,
5. out-of-range compression pointer rejection,
6. unsupported response rejection.

`tests/OS.HostTests/DnsResolverTests.cs` covers:

1. A-record query write and response resolution through a resolver transaction,
2. mismatched transaction ID rejection,
3. timeout transition to failed state,
4. default/zero transition-result rejection.

`tests/OS.HostTests/DnsUdpFrameTests.cs` covers:

1. DNS A-record query frame writing over Ethernet + IPv4 + UDP,
2. DNS response UDP payload extraction and resolver completion,
3. wrong UDP source/destination port rejection,
4. resolver parse-failure propagation,
5. short query frame buffer rejection,
6. default/zero query-result rejection,
7. default/zero receive-result rejection.

`tests/OS.HostTests/DnsResolverTransactionPlannerTests.cs` covers:

1. DNS query frame creation,
2. successful A response validation and TTL metadata capture,
3. wrong transaction and wrong server rejection,
4. NXDOMAIN and no-answer classification,
5. malformed response rejection,
6. unsupported answer type rejection,
7. default/zero planner-result rejection.

`tests/OS.HostTests/IcmpEchoPacketTests.cs` covers:

1. ICMPv4 echo request byte encoding and parse round-trip,
2. ICMPv4 echo reply byte encoding and parse round-trip,
3. bad checksum rejection,
4. short packet rejection,
5. unsupported type/code rejection,
6. too-small destination rejection.

`tests/OS.HostTests/Ipv4IcmpEchoFrameTests.cs` covers:

1. deterministic Ethernet + IPv4 + ICMPv4 echo request frame writing,
2. Ethernet + IPv4 + ICMPv4 echo reply parsing,
3. bad IPv4 header checksum rejection,
4. truncated IPv4 payload rejection.

`tests/OS.HostTests/IcmpReceiveDispatcherTests.cs` covers the ICMP receive dispatch boundary over caller-owned virtio-net RX buffers:

1. IPv4/ICMP echo request metadata parsing after virtio-net, Ethernet, and IPv4 classification,
2. IPv4/ICMP echo reply metadata parsing,
3. nested classifier failure propagation,
4. non-IPv4 payload rejection,
5. non-ICMP IPv4 protocol rejection,
6. truncated ICMP packet rejection,
7. bad ICMP checksum rejection,
8. default/zero result rejection.

`tests/OS.HostTests/Ipv4TransportChecksumTests.cs` covers:

1. deterministic UDP IPv4 pseudo-header checksum computation,
2. deterministic TCP IPv4 pseudo-header checksum computation,
3. segmented payload validation matching one-shot checksum behavior,
4. corrupted checksum rejection,
5. invalid protocol, empty input, and oversized input rejection,
6. default/zero checksum result rejection,
7. primitive boundary flags and no-managed-reference result metadata.

`tests/OS.HostTests/HttpResponseParserTests.cs` covers:

1. fixed-length `200` response parsing,
2. zero-length fixed-body response parsing,
3. malformed status-line rejection,
4. incomplete header rejection,
5. invalid, missing, and short `Content-Length` rejection,
6. unsupported transfer-encoding rejection,
7. default/zero result rejection.

`tests/OS.HostTests/TcpHeaderTests.cs` covers:

1. TCP header write/parse round-trip with payload,
2. invalid data-offset rejection,
3. truncated options rejection,
4. source-guarded TCP options and payload copies through
   `OS.Std.Memory.BufferOperations.MemoryCopy`.

`tests/OS.HostTests/TcpClientHandshakeTests.cs` covers:

1. client-side SYN, SYN-ACK, final ACK transition;
2. wrong acknowledgement-number rejection;
3. missing SYN-ACK flag rejection;
4. default/zero transition-result rejection.

`tests/OS.HostTests/TcpActiveConnectTransactionTests.cs` covers:

1. active connect SYN, SYN-ACK, final ACK transition to `ESTABLISHED`;
2. bad SYN-ACK rejection for wrong acknowledgement number, missing flags, wrong expected remote sequence number, unexpected ports, and truncated segments;
3. insufficient SYN output buffer and invalid final-ACK-before-SYN-ACK rejection;
4. explicit caller-driven timeout from `SYN_SENT`;
5. default/zero transition-result rejection.

`tests/OS.HostTests/TcpIpv4FramePlannerTests.cs` covers:

1. Ethernet/IPv4/TCP SYN frame writing into caller-owned buffers;
2. non-zero SYN checksum writing and validation against the IPv4 TCP pseudo-header;
3. matching SYN-ACK frame validation, including inbound IPv4 TCP pseudo-header checksum validation, and active-connect transaction update;
4. final ACK frame writing after SYN-ACK acceptance;
5. non-zero final ACK checksum writing and validation against the IPv4 TCP pseudo-header;
6. wrong peer MAC/IP rejection without poisoning the active transaction;
7. unsupported EtherType, unsupported IPv4 protocol, truncated frame, bad TCP acknowledgement, wrong TCP ports, invalid local identity, and short output-buffer rejection;
8. default/zero result values rejected as not successful.

`tests/OS.HostTests/TcpPassiveIpv4FramePlannerTests.cs` covers:

1. Ethernet/IPv4/TCP SYN-ACK frame writing after accepting a listener SYN frame;
2. non-zero SYN-ACK checksum writing and validation against the IPv4 TCP pseudo-header;
3. final ACK frame validation and passive-open transition to `ESTABLISHED`;
4. wrong listener MAC/IP/port rejection;
5. malformed protocol frame rejection for short frames, unsupported EtherType, unsupported IPv4 protocol, and invalid SYN flags;
6. bad TCP checksum rejection for inbound SYN and final ACK frames before listener state advances;
7. bad final ACK rejection for wrong acknowledgement number, wrong sequence number, and invalid flags;
8. short output-buffer rejection before mutating listener state;
9. default/zero result values rejected as not successful.

`tests/OS.HostTests/TcpListenerBacklogTests.cs` covers:

1. caller-owned backlog enqueue of already-established passive-open endpoint metadata;
2. FIFO accept/dequeue behavior with reusable slots;
3. duplicate endpoint rejection;
4. deterministic capacity reporting for a full backlog;
5. no-managed-reference result, entry, and connection metadata;
6. default/zero result rejection.

`tests/OS.HostTests/TcpPassiveListenerBacklogBridgeTests.cs` covers:

1. enqueueing established passive-open frame-planner metadata into caller-owned listener backlog storage;
2. rejecting failed passive-open handshakes without mutating existing backlog entries;
3. rejecting successful-but-not-established passive-open results;
4. propagating backlog-full and duplicate-endpoint failures from `TcpListenerBacklog`;
5. no-socket/no-timer/no-virtio-net boundary flags and no-managed-reference result metadata;
6. default/zero bridge-result rejection.

`tests/OS.HostTests/TcpListenerBacklogSocketBridgeTests.cs` covers:

1. enqueueing the oldest caller-owned backlog connection into the mock PAL socket accept queue;
2. rejecting an empty backlog without mutating the socket table;
3. propagating mock socket-table enqueue failures without draining the backlog;
4. preserving accepted local/remote endpoint and sequence metadata;
5. mock-only/no-export/no-live-socket/no-Kestrel boundary flags and no-managed-reference result metadata.

`tests/OS.HostTests/TcpConnectionTableTests.cs` covers:

1. adding established connection metadata into caller-owned table entries;
2. duplicate tuple and capacity-exhaustion rejection;
3. endpoint tuple lookup;
4. local/remote sequence metadata update;
5. marking closing, removing to a closed slot, and reusing that slot;
6. invalid endpoint/default sequence/default result rejection;
7. no-managed-reference layout checks for table key, sequence, entry, and result types;
8. owned half-open/closed lifetimes, exhausted64-slot capacity, generation exhaustion,
   stale/foreign/copied/previous-epoch handles, corrupt/aliased metadata,
   initialized zero sequences, release scrub and allocation-free warmed operations.

`tests/OS.HostTests/TcpPassiveOpenHandshakeTests.cs` covers:

1. listener-side SYN receive, SYN-ACK write, and final ACK transition;
2. wrong final acknowledgement-number rejection;
3. invalid SYN rejection;
4. default/zero transition-result rejection.

`tests/OS.HostTests/TcpEstablishedStreamTests.cs` covers:

1. in-order established payload receive and acknowledgement-sequence advancement;
2. unread byte retention across bounded reads and later in-order payload appends;
3. out-of-order and duplicate payload rejection without consuming buffer or advancing acknowledgement metadata;
4. wrong acknowledgement-number rejection without consuming buffer or advancing local/remote sequence metadata;
5. outgoing payload segmentation bounded by a caller-provided maximum payload length;
6. default/zero receive result rejection;
7. default/zero read result rejection;
8. default/zero send result rejection.

`tests/OS.HostTests/TcpEstablishedStreamIpv4FramePlannerTests.cs` covers:

1. established payload frame writes with computed IPv4 TCP pseudo-header checksum;
2. checksum-valid inbound established payload frame acceptance before stream readback;
3. corrupted inbound TCP checksum rejection before stream sequence/buffer mutation;
4. invalid identity, short output, and invalid maximum payload-length rejection;
5. default/zero frame-plan result rejection and no-managed-reference result metadata.

`tests/OS.HostTests/TcpEstablishedStreamSocketBridgeTests.cs` covers:

1. queueing successful established-stream payload into a connected mock PAL socket;
2. rejecting failed receive results before mock socket mutation;
3. rejecting default receive results before mock socket mutation;
4. rejecting caller payload length mismatches;
5. propagating mock socket-table queue failures;
6. mock-only/no-export/no-live-socket/no-scheduler/no-Kestrel boundary flags and no-managed-reference result metadata;
7. default/zero bridge-result rejection.

`tests/OS.HostTests/TcpConnectionCloseTests.cs` covers:

1. active FIN/ACK close planning through `FIN_WAIT_1`, `FIN_WAIT_2`, and `TIME_WAIT`;
2. passive FIN/ACK close planning through `CLOSE_WAIT`, `LAST_ACK`, and `CLOSED`;
3. inbound reset transition handling;
4. invalid close transition rejection without advancing state unexpectedly;
5. default/zero transition-result rejection.

`tests/OS.HostTests/TcpConnectionCloseIpv4FramePlannerTests.cs` covers:

1. active-close FIN/ACK Ethernet/IPv4/TCP frame generation with a non-zero TCP checksum;
2. checksum validation against the IPv4 TCP pseudo-header through `Ipv4TransportChecksum`;
3. invalid local/remote identity and undersized output rejection without advancing close state;
4. default/zero result values rejected as not successful;
5. `TCP_CLOSE_IPV4_FRAME` diagnostic marker formatting, short-destination reporting without partial writes, default-result rejection, and source-guarded use of shared no-runtime ASCII text helpers.

`tests/OS.HostTests/TcpReceiveDispatcherTests.cs` covers:

1. established payload dispatch from caller-owned receive buffers and parsed network metadata into `TcpEstablishedStream`;
2. passive FIN/ACK dispatch into `TcpConnectionClose` and caller-owned ACK segment checksum writing;
3. inbound RST dispatch into `TcpConnectionClose`;
4. valid empty ACK control dispatch without state mutation;
5. malformed or out-of-window ACK/FIN/RST control metadata rejection before stream or close mutation;
6. corrupted TCP checksum rejection before stream or close mutation;
7. out-of-order payload failure propagation from `TcpEstablishedStream`;
8. non-TCP IPv4 rejection with status-coded packet metadata;
9. default/zero result rejection, no-managed-reference result metadata, and no-socket/no-NIC/no-TLS/no-Kestrel boundary flags;
10. caller-owned diagnostic marker formatting for successful payload dispatch and checksum failure;
11. diagnostic short-destination reporting without partial success; and
12. diagnostic default-result rejection with no-managed-reference formatter metadata; and
13. source-guarded use of shared no-runtime ASCII text helpers for the receive-dispatch diagnostic formatter.

Current TCP receive-dispatch diagnostic markers:

```text
TEST_PASS net.tcp.rx.dispatch diagnostic formats payload result
TEST_PASS net.tcp.rx.dispatch diagnostic formats failure result
TEST_PASS net.tcp.rx.dispatch diagnostic reports short destination
TEST_PASS net.tcp.rx.dispatch diagnostic rejects default result
```

`tests/OS.HostTests/HttpRequestBuilderTests.cs` covers:

1. deterministic HTTP GET request writing,
2. unsafe request-path rejection,
3. destination-too-small rejection,
4. default/zero request-builder result rejection.

`tests/OS.HostTests/HttpRequestParserTests.cs` covers:

1. HTTP/1.x GET request-line, target, host, and connection metadata parsing;
2. single content-length acceptance and fixed-length request body metadata preservation;
3. duplicate content-length rejection before body metadata is trusted;
4. unsupported method, invalid target, invalid protocol, and missing request-line terminator rejection;
5. invalid header, invalid content-length, truncated body, and unsupported transfer-encoding rejection;
6. ASCII space/tab OWS trimming for semantic header values while preserving raw host-value metadata;
7. source-guarded use of shared memory and ASCII helpers, including `OS.Std.Ascii.IsSpaceOrTab`;
8. default/zero request-parse result rejection.

`tests/OS.HostTests/HttpResponseWriterTests.cs` covers:

1. deterministic `200 OK` health response writing with `Content-Length: 17`;
2. empty-body keep-alive response metadata writing;
3. invalid status-code, reason-phrase, content-type, and empty content-type rejection;
4. undersized destination rejection without partial writes;
5. default/zero response-writer result rejection.

`tests/OS.HostTests/HttpStaticResponseTransactionTests.cs` covers:

1. matched `GET` static response metadata for `200 OK`,
2. `404 Not Found` and `405 Method Not Allowed` response metadata without dumping configured body content,
3. malformed request and invalid route rejection,
4. short response-buffer reporting,
5. default/zero transaction-result rejection,
6. caller-owned `HTTP_STATIC_RESPONSE` diagnostic marker formatting,
7. short diagnostic-destination reporting without partial success,
8. default/uninitialized and invalid-enum diagnostic input rejection,
9. source-guarded use of shared no-runtime ASCII text builder and decimal helpers.

`tests/OS.HostTests/HttpStaticTcpResponsePlanTests.cs` covers:

1. accepted established TCP payload metadata serving a matching `GET` as `200 OK`,
2. route misses and non-GET requests mapping to `404 Not Found` and `405 Method Not Allowed`,
3. default, failed, non-payload, and invalid-range TCP dispatch rejection before response writes,
4. short response-buffer reporting through the nested HTTP transaction result,
5. default/zero response-plan result rejection and no-managed-reference result metadata,
6. boundary flags proving the plan uses caller-owned buffers only and does not allocate, open sockets, write TCP frames, perform NIC I/O, start hosted .NET, or start Kestrel,
7. caller-owned `HTTP_STATIC_TCP_RESPONSE` diagnostic marker formatting for `200`, `404`, and `405` metadata,
8. short diagnostic-destination reporting without partial writes,
9. default/uninitialized and invalid-enum diagnostic input rejection plus source-guarded use of shared no-runtime ASCII text builder and decimal helpers,
10. BootSequence readiness aggregation for `HTTP_STATIC_TCP_RESPONSE` plus `HTTP_STATIC_TCP_RESPONSE_READY`, blocked/default-plan markers, diagnostic-format failure metadata, and unchanged default boot markers,
11. opt-in QEMU/script marker-preset propagation for `HTTP_STATIC_TCP_RESPONSE_READY` and `HTTP_STATIC_TCP_RESPONSE` without changing default smoke behavior.

`tests/OS.HostTests/HttpStaticTcpResponseIpv4FramePlanTests.cs` covers:

1. static `200 OK` response frame generation from accepted TCP payload metadata,
2. `404 Not Found` and `405 Method Not Allowed` response frame generation,
3. emitted Ethernet/IPv4/TCP source/destination MAC, IP, port, sequence, and acknowledgement metadata,
4. non-zero IPv4 TCP pseudo-header checksum validation on emitted response frames,
5. response scratch-buffer failure before frame writes,
6. complete-response size rejection before partial TCP segmentation can be mistaken for success,
7. frame-buffer failure propagation from `TcpEstablishedStreamIpv4FramePlanner`,
8. default/zero frame-plan result rejection and no-managed-reference result metadata.

`tests/OS.HostTests/HttpClientGetTransactionTests.cs` covers:

1. HTTP GET request writing followed by fixed-length response parsing,
2. malformed, unsupported transfer-encoding, and truncated response rejection,
3. invalid state-transition rejection,
4. invalid request input rejection,
5. default/zero result rejection,
6. caller-owned diagnostic marker formatting, short-destination reporting without partial writes, and default formatter-result rejection,
7. source-guarded use of shared no-runtime ASCII text helpers for the transaction diagnostic formatter.

`tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs` covers:

1. SYN/SYN-ACK/final-ACK planning followed by one HTTP GET payload frame and one fixed-length HTTP response payload frame,
2. non-zero HTTP GET request TCP checksum writing and validation against the IPv4 TCP pseudo-header,
3. inbound HTTP response TCP checksum validation and corrupted-checksum rejection before stream state advances,
4. nested TCP handshake and bad SYN-ACK checksum failure preservation,
5. invalid request state, unsafe request input, and request scratch-buffer rejection,
6. response peer/address and acknowledgement-number rejection,
7. unsupported HTTP transfer-encoding propagation from response parsing,
8. response destination buffer-size rejection without claiming live socket behavior,
9. default/zero planner-result rejection,
10. caller-owned plan diagnostic marker formatting, short-destination reporting without partial writes, and default formatter-result rejection.

`tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs` covers:

1. expected HTTP status plus expected body-marker acceptance over caller-owned response bytes,
2. failed HTTP TCP GET plan rejection,
3. missing body-marker rejection,
4. invalid body-range and short response-buffer rejection before scanning,
5. empty expected-marker rejection,
6. manifest/config-derived expectations with deterministic defaults,
7. configured status/body-marker flow into `FetchAppHttpCompletionPlan`,
8. invalid status, empty marker, oversized marker, and missing required marker rejection,
9. manifest-side preflight over already-loaded native manifests with valid config, deterministic defaults, malformed status, empty marker, oversized marker, and manifest-not-ready coverage,
10. default/zero completion-result rejection,
11. caller-owned completion diagnostic marker formatting, short-destination reporting without partial writes, and default formatter-result rejection,
12. ordered completion boot-readiness marker generation for successful, failed, and default boot-sequence paths,
13. request endpoint derivation with deterministic defaults, explicit config, invalid scheme, invalid port, missing host, oversized-field rejection, endpoint diagnostic marker formatting, short-destination reporting, and default formatter-result rejection,
14. opt-in QEMU/script marker-preset propagation for `FETCH_APP_HTTP_COMPLETION_READY` without changing default smoke behavior.

`tests/OS.HostTests/FetchAppHttpRequestEndpointBootSequenceTests.cs` covers
ordered endpoint boot-readiness marker generation for successful, failed, and
default endpoint results while preserving the default boot marker order.

`tests/OS.HostTests/FetchAppHttpTcpContextPlanTests.cs` covers composing
FetchApp endpoint, DNS answer, cached ARP route, and local TCP identity into
`HttpTcpClientGetPlannerInfo`, plus endpoint failure, DNS failure, DNS host
fingerprint mismatch, uncached route, invalid local identity, and default-result
rejection.

`tests/OS.HostTests/FetchAppHttpTcpExchangePlanTests.cs` covers composing the
FetchApp TCP context through caller-owned SYN/SYN-ACK/final-ACK/request/
response/completion/active-close FIN-ACK buffers, plus failed context, short
endpoint buffer, bad SYN-ACK checksum, short request frame, missing expected
marker, short close frame, default-result rejection, and ordered boot-readiness
marker generation for ready, failed, and default exchange results.

Current FetchApp HTTP completion markers:

```text
TEST_PASS fetch.http tcp context composes planner info
TEST_PASS fetch.http tcp context rejects endpoint failure
TEST_PASS fetch.http tcp context rejects dns failure
TEST_PASS fetch.http tcp context rejects dns host mismatch
TEST_PASS fetch.http tcp context rejects uncached route
TEST_PASS fetch.http tcp context rejects invalid local identity
TEST_PASS fetch.http tcp context default result is not success
TEST_PASS fetch.http tcp exchange completes completion and close
TEST_PASS fetch.http tcp exchange rejects context failure
TEST_PASS fetch.http tcp exchange rejects short endpoint buffers
TEST_PASS fetch.http tcp exchange rejects bad synack checksum
TEST_PASS fetch.http tcp exchange rejects request frame too small
TEST_PASS fetch.http tcp exchange rejects missing expected marker
TEST_PASS fetch.http tcp exchange rejects close frame too small
TEST_PASS fetch.http tcp exchange default result is not success
TEST_PASS fetch.http tcp exchange formatter formats success marker
TEST_PASS fetch.http tcp exchange formatter reports short destination
TEST_PASS fetch.http tcp exchange formatter rejects default result
TEST_PASS fetch.http tcp exchange formatter uses shared ascii text builder
TEST_PASS boot.sequence fetch http tcp exchange ready
TEST_PASS boot.sequence fetch http tcp exchange rejects failed result
TEST_PASS boot.sequence fetch http tcp exchange rejects default result
TEST_PASS boot.sequence fetch http tcp exchange preserves default markers
TEST_PASS fetch.http completion accepts expected body marker
TEST_PASS fetch.http completion rejects failed http plan
TEST_PASS fetch.http completion rejects missing body marker
TEST_PASS fetch.http completion rejects invalid body range
TEST_PASS fetch.http completion rejects empty expected marker
TEST_PASS fetch.http completion rejects short response buffer
TEST_PASS fetch.http completion default result is not success
TEST_PASS fetch.http completion config uses deterministic defaults
TEST_PASS fetch.http completion config flows from manifest
TEST_PASS fetch.http completion config rejects invalid status
TEST_PASS fetch.http completion config rejects empty marker
TEST_PASS fetch.http completion config rejects oversized marker
TEST_PASS fetch.http completion config requires marker when requested
TEST_PASS fetch.http request endpoint defaults
TEST_PASS fetch.http request endpoint explicit config
TEST_PASS fetch.http request endpoint invalid scheme
TEST_PASS fetch.http request endpoint invalid port
TEST_PASS fetch.http request endpoint missing host
TEST_PASS fetch.http request endpoint oversized fields
TEST_PASS boot.sequence fetch http endpoint ready
TEST_PASS boot.sequence fetch http endpoint rejects failed result
TEST_PASS boot.sequence fetch http endpoint rejects default result
TEST_PASS boot.sequence fetch http endpoint preserves default markers
TEST_PASS fetch.http completion expectation formatter formats success marker
TEST_PASS fetch.http completion expectation formatter reports short destination
TEST_PASS fetch.http completion expectation formatter rejects default result
TEST_PASS fetch.http completion formatters use shared ascii text builder
TEST_PASS manifest fetchapp config preflight valid config
TEST_PASS manifest fetchapp config preflight defaults
TEST_PASS manifest fetchapp config preflight invalid status
TEST_PASS manifest fetchapp config preflight empty marker
TEST_PASS manifest fetchapp config preflight oversized marker
TEST_PASS manifest fetchapp config preflight manifest not ready
TEST_PASS fetch.http completion formatter formats success marker
TEST_PASS fetch.http completion formatter reports short destination
TEST_PASS fetch.http completion formatter rejects default result
TEST_PASS boot.sequence fetch http completion ready
TEST_PASS boot.sequence fetch http completion rejects failed result
TEST_PASS boot.sequence fetch http completion preserves default markers
TEST_PASS qemu.smoke.preset fetch http adds FetchApp completion marker
TEST_PASS qemu.smoke.preset script dry-run includes fetch http preset
TEST_PASS qemu.smoke.preset http static response adds marker requirements
TEST_PASS qemu.smoke.preset http static response aliases normalize
TEST_PASS qemu.smoke.preset script dry-run includes http static response preset
TEST_PASS qemu.smoke.preset http static tcp response includes marker requirements
TEST_PASS qemu.smoke.preset http static tcp response accepts aliases
TEST_PASS qemu.smoke.preset script dry-run includes http static tcp response preset
```

`tests/OS.HostTests/PalSocketTableTests.cs` covers the current host-only PAL socket table slice:

1. socket create/bind/listen lifecycle,
2. deterministic capacity exhaustion and handle reuse,
3. accept-queue behavior and empty/non-listener failures,
4. connected-handle send/receive byte transfer,
5. partial receive unread-byte preservation and partial send backpressure byte counts,
6. peer close draining to `EndOfFile` and peer reset reporting `ConnectionReset`,
7. poll readiness for readable, writable, accepted, close hangup, reset error/hangup, and invalid event states,
8. shutdown interrupting the matching I/O direction,
9. invalid-state rejection for send/receive/shutdown,
10. Kestrel-oriented option set/get for `NoDelay`, `ReuseAddress`, receive timeout, send timeout, and keep-alive,
11. local endpoint query for a bound socket and remote endpoint query for an accepted socket,
12. deterministic `ReuseAddress` duplicate-bind policy.

`tests/OS.HostTests/VirtioPciCapabilityListParserTests.cs` and `tests/OS.HostTests/VirtioPciDeviceDiscoveryTests.cs` cover the shared virtio PCI parser/discovery contracts:

1. standard PCI capability-list walking over caller-provided config bytes,
2. modern virtio vendor-specific capability record decoding,
3. loop, truncation, unaligned-offset, unsupported capability, and missing-required-capability rejection,
4. feeding decoded capability metadata into the virtio PCI discovery contract,
5. modern virtio-blk identity recognition,
6. modern virtio-net identity recognition,
7. unsupported vendor, legacy device ID, and unsupported modern device kind rejection,
8. required common/notify/ISR/device-configuration capability recording and missing/invalid capability rejection.

Current virtio PCI discovery markers:

```text
TEST_PASS drivers.virtiopci.capability parser records required capabilities
TEST_PASS drivers.virtiopci.capability parser rejects truncated capability
TEST_PASS drivers.virtiopci.capability parser rejects capability loop
TEST_PASS drivers.virtiopci.capability parser feeds discovery contract
TEST_PASS drivers.virtiopci identifies virtio block device
TEST_PASS drivers.virtiopci identifies virtio net device
TEST_PASS drivers.virtiopci rejects unsupported device
TEST_PASS drivers.virtiopci records required capabilities
TEST_PASS drivers.virtio init plans status sequence
TEST_PASS drivers.virtio init rejects missing features
TEST_PASS drivers.virtio init rejects features-ok failure
TEST_PASS drivers.virtio init records selected features
```

`tests/OS.HostTests/VirtioNetQueuePlanTests.cs` covers the virtio-net RX/TX queue planning contract:

1. successful planning with separate RX and TX split-ring queues,
2. RX descriptor direction as device-writable and TX header/frame descriptor directions as device-readable,
3. insufficient available queue rejection before queue publication,
4. invalid Ethernet frame buffer sizing rejection,
5. default/zero result rejection before queue-plan success is trusted.

Current virtio-net queue-plan markers:

```text
TEST_PASS drivers.virtionet queue plan accepts rx tx queues
TEST_PASS drivers.virtionet queue plan marks rx writable and tx readable
TEST_PASS drivers.virtionet queue plan rejects insufficient queues
TEST_PASS drivers.virtionet queue plan rejects invalid frame size
TEST_PASS drivers.virtionet queue plan default result is not success
```

`tests/OS.HostTests/VirtioNetReceiveLifecycleTests.cs` covers the host-only
virtio-net RX lifecycle coordinator:

1. caller-owned receive-buffer descriptor publication into split-ring memory,
2. available-ring wraparound metadata,
3. one caller-owned used-ring completion mapped back to the published buffer
   before receive-frame classification,
4. short queue memory, descriptor exhaustion, and completion-failure
   propagation,
5. default/zero publish and completion result rejection before RX lifecycle
   metadata is trusted.

Current virtio-net RX lifecycle markers:

```text
TEST_PASS drivers.virtionet.rx.lifecycle publishes receive buffers
TEST_PASS drivers.virtionet.rx.lifecycle wraps available ring slots
TEST_PASS drivers.virtionet.rx.lifecycle completes and classifies buffer
TEST_PASS drivers.virtionet.rx.lifecycle rejects short queue memory
TEST_PASS drivers.virtionet.rx.lifecycle rejects descriptor exhaustion
TEST_PASS drivers.virtionet.rx.lifecycle reports completion failures
TEST_PASS drivers.virtionet.rx.lifecycle publish default result is not success
TEST_PASS drivers.virtionet.rx.lifecycle completion default result is not success
```

`tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs` covers virtio-net queue setup planning, register-window access planning, fake register-window byte-span execution, and fake register-accessor execution:

1. successful RX/TX common-configuration command planning,
2. failed command destination, queue metadata, physical address, and bring-up rejection,
3. register-window access-plan creation for every planned setup command,
4. fake register-window byte-span execution of planned RX/TX queue-select, queue-size, descriptor/driver/device address, notify-offset, and queue-enable commands with staged read values,
5. fake-accessor execution of planned RX/TX queue-select, queue-size, descriptor/driver/device address, notify-offset, and queue-enable commands,
6. failed setup-plan, short command source, short access-plan destination, unsupported command width, missing accessor, read mismatch, accessor not-ready, read failure, write failure, propagated register-window failure, and propagated byte-span access failure rejection,
7. no-live-MMIO/no-BAR-mapping/no-DMA/no-packet-I/O boundary flags.

Current virtio-net queue-MMIO setup markers:

```text
TEST_PASS drivers.virtionet.queue.mmio creates setup command plan
TEST_PASS drivers.virtionet.queue.mmio rejects command destination
TEST_PASS drivers.virtionet.queue.mmio rejects queue metadata
TEST_PASS drivers.virtionet.queue.mmio rejects physical addresses
TEST_PASS drivers.virtionet.queue.mmio rejects failed bringup
TEST_PASS drivers.virtionet.queue.mmio.exec writes planned commands
TEST_PASS drivers.virtionet.queue.mmio.exec writes planned rx tx setup
TEST_PASS drivers.virtionet.queue.mmio.exec rejects failed setup plan
TEST_PASS drivers.virtionet.queue.mmio.exec rejects short command source
TEST_PASS drivers.virtionet.queue.mmio.exec rejects read mismatch
TEST_PASS drivers.virtionet.queue.mmio.exec propagates accessor failure
TEST_PASS drivers.virtionet.queue.mmio.exec propagates read failure
TEST_PASS drivers.virtionet.queue.mmio.exec propagates write failure
TEST_PASS drivers.virtionet.queue.mmio.exec rejects missing accessor
TEST_PASS drivers.virtionet.queue.mmio.exec preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.mmio.window plans setup command accesses
TEST_PASS drivers.virtionet.queue.mmio.window rejects failed setup plan
TEST_PASS drivers.virtionet.queue.mmio.window rejects short command source
TEST_PASS drivers.virtionet.queue.mmio.window rejects short destination
TEST_PASS drivers.virtionet.queue.mmio.window rejects unsupported command width
TEST_PASS drivers.virtionet.queue.mmio.window propagates register window failure
TEST_PASS drivers.virtionet.queue.mmio.window preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.mmio.windowexec writes planned setup accesses
TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects failed setup plan
TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects short command source
TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects read mismatch
TEST_PASS drivers.virtionet.queue.mmio.windowexec propagates access failure
TEST_PASS drivers.virtionet.queue.mmio.windowexec preserves no-live-mmio boundary
```

`tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs` covers the virtio-net queue notification coordinator:

1. RX notification plan composition from successful queue setup and receive-buffer publication,
2. TX notification plan composition from successful queue setup and transmit-frame publication,
3. failed queue setup propagation before notify planning,
4. failed RX/TX descriptor publication propagation before notify planning,
5. nested virtio PCI notify planner failures for disabled queues, unsupported notification-data features, and invalid notify apertures,
6. register-window access-plan creation for successful RX/TX notify plans,
7. failed notification-plan and propagated register-window failure rejection,
8. fake register-window byte-span execution of planned RX/TX 16-bit notifications,
9. fake-sink execution of planned RX/TX 16-bit notifications after explicit barrier readiness,
10. fake register-accessor adapter execution for planned TX notification writes,
11. execution rejection for missing barrier readiness, failed notification plans, register-window/access/sink/accessor not-ready/range/write failures, and no-live-MMIO boundary flags,
12. default/zero result rejection for notification planning, register-window planning/execution, barrier-gated execution, fake-sink execution, and fake register reads.

Current virtio-net queue-notification markers:

```text
TEST_PASS drivers.virtionet.queue.notify plans rx notification
TEST_PASS drivers.virtionet.queue.notify plans tx notification
TEST_PASS drivers.virtionet.queue.notify rejects failed setup
TEST_PASS drivers.virtionet.queue.notify rejects failed publish
TEST_PASS drivers.virtionet.queue.notify propagates notify failure
TEST_PASS drivers.virtionet.queue.notify preserves no-mmio boundary
TEST_PASS drivers.virtionet.queue.notify.window plans tx notification access
TEST_PASS drivers.virtionet.queue.notify.window plans rx notification access
TEST_PASS drivers.virtionet.queue.notify.window rejects failed notification plan
TEST_PASS drivers.virtionet.queue.notify.window propagates register window failure
TEST_PASS drivers.virtionet.queue.notify.window preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned tx notification
TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned rx notification
TEST_PASS drivers.virtionet.queue.notify.windowexec rejects failed notification plan
TEST_PASS drivers.virtionet.queue.notify.windowexec propagates register window failure
TEST_PASS drivers.virtionet.queue.notify.windowexec propagates access failure
TEST_PASS drivers.virtionet.queue.notify.windowexec preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.notify.windowbarrier writes planned tx notification
TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects missing barrier
TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects failed notification plan
TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates register window failure
TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates access failure
TEST_PASS drivers.virtionet.queue.notify.windowbarrier preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.notify.exec writes planned tx notification
TEST_PASS drivers.virtionet.queue.notify.exec writes planned rx notification
TEST_PASS drivers.virtionet.queue.notify.exec rejects missing barrier
TEST_PASS drivers.virtionet.queue.notify.exec rejects failed notification plan
TEST_PASS drivers.virtionet.queue.notify.exec propagates sink failure
TEST_PASS drivers.virtionet.queue.notify.exec preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.notify.accessor writes planned tx notification
TEST_PASS drivers.virtionet.queue.notify.accessor propagates range failure
TEST_PASS drivers.virtionet.queue.notify.accessor propagates write failure
TEST_PASS drivers.virtionet.queue.notify.accessor preserves no-live-mmio boundary
TEST_PASS drivers.virtionet.queue.notify rejects default results
```

`tests/OS.HostTests/VirtioNetReceiveReadinessPlanTests.cs` covers RX
readiness composition over fake register-window execution:

1. successful setup-window execution, RX descriptor publication, barrier
   readiness, notification planning, and notify-window fake execution,
2. failed setup-window execution rejection before descriptor publication,
3. failed RX publication rejection before notification planning,
4. notification-plan failure rejection before touching notify bytes,
5. missing-barrier rejection before touching notify bytes,
6. notify-window access failure propagation,
7. default/zero result rejection,
8. no-live-MMIO/no-live-device/no-packet-I/O boundary flags.

Current virtio-net RX readiness markers:

```text
TEST_PASS drivers.virtionet.rx.readiness composes setup publish notify windowexec
TEST_PASS drivers.virtionet.rx.readiness rejects failed setup window execution
TEST_PASS drivers.virtionet.rx.readiness rejects failed publish
TEST_PASS drivers.virtionet.rx.readiness rejects missing barrier
TEST_PASS drivers.virtionet.rx.readiness propagates notification plan failure
TEST_PASS drivers.virtionet.rx.readiness propagates notification window failure
TEST_PASS drivers.virtionet.rx.readiness default result is not success
TEST_PASS drivers.virtionet.rx.readiness preserves no-live-mmio boundary
```

Current virtio-net RX register-window completion/classification markers:

```text
TEST_PASS drivers.virtionet.rx.completion.windowexec composes readiness completion classification
TEST_PASS drivers.virtionet.rx.completion.windowexec rejects failed readiness
TEST_PASS drivers.virtionet.rx.completion.windowexec rejects no completion
TEST_PASS drivers.virtionet.rx.completion.windowexec propagates classifier failure
TEST_PASS drivers.virtionet.rx.completion.windowexec default result is not success
TEST_PASS drivers.virtionet.rx.completion.windowexec preserves no-live-mmio boundary
```

Current virtio-net RX register-window slab-selection markers:

```text
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec selects completed buffer
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects failed readiness
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects no completion
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects short slab
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec propagates classifier failure
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec preserves no-live-mmio boundary
```

Current virtio-net RX register-window network-dispatch markers:

```text
TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches arp slab completion
TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches ipv4 slab completion
TEST_PASS drivers.virtionet.rx.dispatch.windowexec rejects failed slab completion
TEST_PASS drivers.virtionet.rx.dispatch.windowexec propagates dispatcher failure
TEST_PASS drivers.virtionet.rx.dispatch.windowexec default result is not success
TEST_PASS drivers.virtionet.rx.dispatch.windowexec preserves no-live-mmio boundary
```

`tests/OS.HostTests/VirtioNetTransmitCompletionPlanTests.cs` covers the virtio-net TX used-ring completion/reclaim planner:

1. matching used-ring entry acceptance after successful TX publication and fake-sink or register-window notification execution,
2. wrapped used-index advancement,
3. no-completion and unexpected-used-index rejection,
4. wrong descriptor-head and unexpected-byte-count rejection,
5. short queue-memory rejection,
6. failed TX publish, failed notification planning, failed notification execution, wrong executed-notification kind, queue mismatch, and mismatched publication index rejection for fake-sink and register-window provenance,
7. no-MMIO/no-packet-I/O/no-live-notification boundary flags.

Current virtio-net TX completion markers:

```text
TEST_PASS drivers.virtionet.tx.completion accepts matching used entry
TEST_PASS drivers.virtionet.tx.completion accepts executed notification
TEST_PASS drivers.virtionet.tx.completion advances wrapped used index
TEST_PASS drivers.virtionet.tx.completion reports no completion
TEST_PASS drivers.virtionet.tx.completion rejects unexpected used index
TEST_PASS drivers.virtionet.tx.completion rejects wrong descriptor head
TEST_PASS drivers.virtionet.tx.completion rejects unexpected byte count
TEST_PASS drivers.virtionet.tx.completion rejects short queue memory
TEST_PASS drivers.virtionet.tx.completion rejects failed publish
TEST_PASS drivers.virtionet.tx.completion rejects failed notification
TEST_PASS drivers.virtionet.tx.completion rejects failed notification execution
TEST_PASS drivers.virtionet.tx.completion rejects wrong executed notification kind
TEST_PASS drivers.virtionet.tx.completion rejects executed notification queue mismatch
TEST_PASS drivers.virtionet.tx.completion rejects executed notification index mismatch
TEST_PASS drivers.virtionet.tx.completion accepts register-window notification
TEST_PASS drivers.virtionet.tx.completion rejects failed register-window notification
TEST_PASS drivers.virtionet.tx.completion rejects wrong register-window notification kind
TEST_PASS drivers.virtionet.tx.completion rejects register-window notification queue mismatch
TEST_PASS drivers.virtionet.tx.completion rejects register-window notification index mismatch
TEST_PASS drivers.virtionet.tx.completion preserves register-window notification boundary
TEST_PASS drivers.virtionet.tx.completion preserves no-mmio boundary
TEST_PASS drivers.virtionet.tx.completion preserves executed-notification boundary
```

`tests/OS.HostTests/VirtioNetTransmitDescriptorOwnershipPlanTests.cs` covers the caller-owned TX descriptor ownership state table:

1. successful publication marks the virtio-net header and Ethernet frame descriptors in-flight,
2. successful completion frees the completed descriptors and leaves unrelated descriptor states unchanged,
3. failed publish and failed completion inputs are rejected without mutating state,
4. short state tables, already-in-flight descriptors, not-in-flight descriptors, and duplicate completions are rejected,
5. no-MMIO/no-live-device boundary flags remain false while only the caller-owned state table is mutated.

Current virtio-net TX ownership markers:

```text
TEST_PASS drivers.virtionet.tx.ownership marks published descriptors in flight
TEST_PASS drivers.virtionet.tx.ownership reclaims completed descriptors
TEST_PASS drivers.virtionet.tx.ownership rejects failed publish
TEST_PASS drivers.virtionet.tx.ownership rejects failed completion
TEST_PASS drivers.virtionet.tx.ownership rejects short state table
TEST_PASS drivers.virtionet.tx.ownership rejects descriptor already in flight
TEST_PASS drivers.virtionet.tx.ownership rejects descriptor not in flight
TEST_PASS drivers.virtionet.tx.ownership rejects duplicate completion
TEST_PASS drivers.virtionet.tx.ownership preserves no-mmio boundary
```

Current virtio-net TX free-list markers:

```text
TEST_PASS drivers.virtionet.tx.freelist selects first free pair
TEST_PASS drivers.virtionet.tx.freelist skips in-flight descriptors
TEST_PASS drivers.virtionet.tx.freelist reuses reclaimed descriptors
TEST_PASS drivers.virtionet.tx.freelist reports capacity exhaustion
TEST_PASS drivers.virtionet.tx.freelist rejects invalid queue state
TEST_PASS drivers.virtionet.tx.freelist preserves no-mmio boundary
```

Current virtio-net TX publish-plan markers:

```text
TEST_PASS drivers.virtionet.tx.publishplan publishes selected free pair
TEST_PASS drivers.virtionet.tx.publishplan skips in-flight descriptors
TEST_PASS drivers.virtionet.tx.publishplan rejects free-list exhaustion
TEST_PASS drivers.virtionet.tx.publishplan preserves state on publish failure
TEST_PASS drivers.virtionet.tx.publishplan rejects invalid ownership state
TEST_PASS drivers.virtionet.tx.publishplan preserves no-mmio boundary
TEST_PASS drivers.virtionet.tx.descriptor default results are not success
```

Current virtio-net TX readiness markers:

```text
TEST_PASS drivers.virtionet.tx.readiness composes setup publish notify
TEST_PASS drivers.virtionet.tx.readiness default result is not success
TEST_PASS drivers.virtionet.tx.readiness rejects failed setup execution
TEST_PASS drivers.virtionet.tx.readiness rejects failed publish
TEST_PASS drivers.virtionet.tx.readiness rejects missing barrier
TEST_PASS drivers.virtionet.tx.readiness propagates notification failure
TEST_PASS drivers.virtionet.tx.readiness preserves no-live-mmio boundary
```

Current virtio-net TX register-window readiness markers:

```text
TEST_PASS drivers.virtionet.tx.readiness.windowexec composes setup publish notify
TEST_PASS drivers.virtionet.tx.readiness.windowexec default result is not success
TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed setup window execution
TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed publish
TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects missing barrier
TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification plan failure
TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification window failure
TEST_PASS drivers.virtionet.tx.readiness.windowexec preserves no-live-mmio boundary
```

Current virtio-net TX register-window completion/reclaim markers:

```text
TEST_PASS drivers.virtionet.tx.reclaim.windowexec composes readiness completion ownership
TEST_PASS drivers.virtionet.tx.reclaim.windowexec default result is not success
TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects failed readiness
TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects no completion
TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects ownership mismatch
TEST_PASS drivers.virtionet.tx.reclaim.windowexec preserves no-live-mmio boundary
```

`tests/OS.HostTests/VirtioNetDeviceConfigTests.cs` covers the virtio-net device configuration parser and register-window fake reader:

1. MAC address parsing from the first six config bytes,
2. optional little-endian status-field parsing at offset 6,
3. minimum six-byte config acceptance when the status field is not present,
4. truncated config rejection,
5. all-zero MAC rejection,
6. broadcast MAC rejection,
7. fake device BAR-window MAC/status reads into caller-owned scratch,
8. short scratch-buffer, register-window, access-execution, and parse-failure propagation.

Current virtio-net device-config markers:

```text
TEST_PASS drivers.virtionet.config parses mac address
TEST_PASS drivers.virtionet.config parses optional status
TEST_PASS drivers.virtionet.config accepts minimum config
TEST_PASS drivers.virtionet.config rejects short config
TEST_PASS drivers.virtionet.config rejects zero mac
TEST_PASS drivers.virtionet.config rejects broadcast mac
TEST_PASS drivers.virtionet.config.windowexec reads mac address
TEST_PASS drivers.virtionet.config.windowexec reads optional status
TEST_PASS drivers.virtionet.config.windowexec rejects short scratch buffer
TEST_PASS drivers.virtionet.config.windowexec propagates register window failure
TEST_PASS drivers.virtionet.config.windowexec propagates access failure
TEST_PASS drivers.virtionet.config.windowexec propagates config parse failure
TEST_PASS drivers.virtionet.config.windowexec preserves no-live-mmio boundary
```

`tests/OS.HostTests/VirtioNetHeaderTests.cs` covers the no-offload virtio-net header boundary:

1. zeroed TX header writing,
2. no-offload RX header parsing plus Ethernet frame offset/length metadata,
3. truncated virtio header rejection,
4. missing complete Ethernet header rejection,
5. unsupported checksum/offload/GSO rejection.

Current virtio-net header markers:

```text
TEST_PASS drivers.virtionet.header writes zero tx header
TEST_PASS drivers.virtionet.header parses rx frame metadata
TEST_PASS drivers.virtionet.header rejects truncated header
TEST_PASS drivers.virtionet.header rejects missing ethernet header
TEST_PASS drivers.virtionet.header rejects offload and gso
```

`tests/OS.HostTests/VirtioNetReceiveFrameClassifierTests.cs` covers the no-offload virtio-net RX classifier:

1. ARP Ethernet payload classification,
2. IPv4 Ethernet payload classification,
3. truncated virtio header rejection,
4. unsupported checksum/offload rejection,
5. missing complete Ethernet header rejection,
6. unsupported EtherType reporting with payload offset/length metadata,
7. default/zero result rejection.

Current virtio-net RX classifier markers:

```text
TEST_PASS drivers.virtionet.rx classifies arp frame
TEST_PASS drivers.virtionet.rx classifies ipv4 frame
TEST_PASS drivers.virtionet.rx rejects truncated virtio header
TEST_PASS drivers.virtionet.rx rejects unsupported offload
TEST_PASS drivers.virtionet.rx rejects missing ethernet header
TEST_PASS drivers.virtionet.rx reports unsupported payload
TEST_PASS drivers.virtionet.rx classifier default result is not success
```

`tests/OS.HostTests/NetworkReceiveDispatcherTests.cs` covers the receive dispatch boundary over caller-owned virtio-net RX buffers:

1. ARP payload parsing after virtio-net and Ethernet classification,
2. IPv4 header metadata parsing after virtio-net and Ethernet classification,
3. nested classifier failure propagation,
4. ARP parser failure propagation,
5. IPv4 parser failure propagation,
6. unsupported payload reporting without ARP/IPv4 parsing,
7. default/zero dispatch result rejection.

Current network receive dispatcher markers:

```text
TEST_PASS net.rx.dispatch parses arp payload
TEST_PASS net.rx.dispatch parses ipv4 payload
TEST_PASS net.rx.dispatch reports classifier failure
TEST_PASS net.rx.dispatch reports arp parse failure
TEST_PASS net.rx.dispatch reports ipv4 parse failure
TEST_PASS net.rx.dispatch reports unsupported payload
TEST_PASS net.rx.dispatch default result is not success
```

`tests/OS.HostTests/UdpReceiveDispatcherTests.cs` covers the UDP receive dispatch boundary over caller-owned virtio-net RX buffers:

1. IPv4/UDP metadata parsing after virtio-net, Ethernet, and IPv4 classification,
2. zero IPv4 UDP checksum acceptance,
3. non-zero IPv4 UDP checksum validation,
4. bad non-zero IPv4 UDP checksum rejection,
5. nested classifier failure propagation,
6. non-IPv4 payload rejection,
7. non-UDP IPv4 protocol rejection,
8. truncated UDP header rejection,
9. truncated UDP payload rejection,
10. default/zero receive-dispatch result rejection.

Current UDP receive dispatcher markers:

```text
TEST_PASS net.udp.rx parses udp payload
TEST_PASS net.udp.rx accepts zero checksum
TEST_PASS net.udp.rx validates nonzero checksum
TEST_PASS net.udp.rx rejects bad checksum
TEST_PASS net.udp.rx reports classifier failure
TEST_PASS net.udp.rx rejects non-ipv4 payload
TEST_PASS net.udp.rx rejects non-udp protocol
TEST_PASS net.udp.rx reports truncated udp header
TEST_PASS net.udp.rx reports truncated udp payload
TEST_PASS net.udp.rx default result is not success
```

`tests/OS.HostTests/UdpTransmitPlannerTests.cs` covers the UDP transmit planning boundary over caller-owned Ethernet frame buffers:

1. Ethernet/IPv4/UDP frame writing through `EthernetIpv4UdpPacket.TryWrite`,
2. non-zero IPv4 UDP checksum writing,
3. checksum validation against the IPv4 pseudo-header,
4. local and destination addressing metadata preservation,
5. invalid local identity rejection,
6. invalid destination identity rejection,
7. short frame buffer rejection,
8. oversized payload rejection,
9. default/zero result values rejected as not successful.

Current UDP transmit planner markers:

```text
TEST_PASS net.udp.tx writes ethernet ipv4 udp frame
TEST_PASS net.udp.tx writes nonzero checksum
TEST_PASS net.udp.tx checksum validates with pseudo header
TEST_PASS net.udp.tx preserves addressing metadata
TEST_PASS net.udp.tx rejects invalid local identity
TEST_PASS net.udp.tx rejects invalid destination identity
TEST_PASS net.udp.tx rejects short frame buffer
TEST_PASS net.udp.tx rejects oversized payload
TEST_PASS net.udp.tx default result is not success
```

Evidence from the current local run:

```text
dotnet build .\CLROS.slnx --nologo -m:1
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
TEST_PASS net.packet.arp.request-round-trip
TEST_PASS net.packet.arp.reply-round-trip
TEST_PASS net.packet.arp.reject-unsupported-hardware-type
TEST_PASS net.packet.arp.reject-unsupported-protocol-type
TEST_PASS net.packet.arp.reject-invalid-address-lengths
TEST_PASS net.packet.arp.reject-unsupported-opcode
TEST_PASS net.packet.arp.reject-truncated-packet
TEST_PASS net.packet.ethernet.dispatch-arp-and-ipv4
TEST_PASS net.arp resolver writes request frame
TEST_PASS net.arp resolver accepts matching reply
TEST_PASS net.arp resolver rejects mismatched reply
TEST_PASS net.arp resolver times out request
TEST_PASS net.arp resolver default result is not success
TEST_PASS net.ipv4.neighbor returns cached mac
TEST_PASS net.ipv4.neighbor writes arp request on cache miss
TEST_PASS net.ipv4.neighbor preserves cache on request write
TEST_PASS net.ipv4.neighbor rejects undersized request buffer
TEST_PASS net.ipv4.neighbor handles cache capacity unchanged
TEST_PASS net.ipv4.neighbor rejects invalid target input
TEST_PASS net.ipv4.neighbor default result is not success
TEST_PASS network packets round-trip ethernet ipv4 udp bytes
TEST_PASS network packets round-trip ethernet ipv4 udp checksum
TEST_PASS network packets reject invalid ethernet frame length
TEST_PASS network packets reject unsupported ethertype
TEST_PASS network packets reject unsupported ipv4 protocol
TEST_PASS network packets reject ipv4 options
TEST_PASS network packets reject bad ipv4 checksum
TEST_PASS network packets reject truncated ipv4 payload
TEST_PASS network packets reject truncated udp payload
TEST_PASS net.dhcp writes discover message
TEST_PASS net.dhcp parses offer options
TEST_PASS net.dhcp writes request message
TEST_PASS net.dhcp rejects malformed packets
TEST_PASS net.dhcp client transitions discover-offer-request-ack
TEST_PASS net.dhcp client rejects mismatched xid
TEST_PASS net.dhcp client times out discover
TEST_PASS net.dhcp client default result is not success
TEST_PASS net.dns writes a-record query
TEST_PASS net.dns parses compressed a-record response
TEST_PASS net.dns rejects malformed compression
TEST_PASS net.dns rejects forward compression pointer
TEST_PASS net.dns rejects out-of-range compression pointer
TEST_PASS net.dns rejects unsupported response
TEST_PASS net.dns resolver writes query and resolves response
TEST_PASS net.dns resolver rejects mismatched transaction id
TEST_PASS net.dns resolver times out query
TEST_PASS net.dns resolver default result is not success
TEST_PASS net.dns.udp writes query frame
TEST_PASS net.dns.udp query default result is not success
TEST_PASS net.dns.udp resolves response payload
TEST_PASS net.dns.udp receive default result is not success
TEST_PASS net.dns.udp rejects wrong udp ports
TEST_PASS net.dns.udp reports resolver parse failure
TEST_PASS net.dns.udp reports short frame buffer
TEST_PASS net.dns.client.transaction writes query frame
TEST_PASS net.dns.client.transaction resolves matching response
TEST_PASS net.dns.client.transaction default result is not success
TEST_PASS net.dns.client.transaction rejects wrong transaction
TEST_PASS net.dns.client.transaction rejects wrong server
TEST_PASS net.dns.client.transaction rejects wrong udp ports
TEST_PASS net.dns.client.transaction reports nxdomain and no answer
TEST_PASS net.dns.client.transaction reports malformed response
TEST_PASS net.dns.client.transaction reports unsupported answer type
TEST_PASS net.packet.tcp.header-round-trip
TEST_PASS net.packet.tcp.rejects-invalid-data-offset
TEST_PASS net.packet.tcp.rejects-truncated-options
TEST_PASS net.tcp header payloads use shared buffer operations
TEST_PASS net.tcp handshake transitions syn-synack-ack
TEST_PASS net.tcp handshake rejects wrong ack number
TEST_PASS net.tcp handshake rejects missing synack flags
TEST_PASS net.tcp handshake default result is not success
TEST_PASS net.tcp active connect establishes
TEST_PASS net.tcp active connect rejects bad synack
TEST_PASS net.tcp active connect times out
TEST_PASS net.tcp active connect default result is not success
TEST_PASS net.tcp passive handshake transitions listen-synack-established
TEST_PASS net.tcp passive handshake rejects wrong final ack
TEST_PASS net.tcp passive handshake rejects invalid syn
TEST_PASS net.tcp passive handshake default result is not success
TEST_PASS net.tcp.listener enqueues passive connection
TEST_PASS net.tcp.listener dequeues accepted connection
TEST_PASS net.tcp.listener rejects duplicate endpoint
TEST_PASS net.tcp.listener reports backlog capacity
TEST_PASS net.tcp.listener result contains no managed references
TEST_PASS net.tcp.listener default result is not success
TEST_PASS net.tcp.listener.socketbridge default result is not success
TEST_PASS net.http writes get request
TEST_PASS net.http rejects unsafe request path
TEST_PASS net.http rejects request buffer too small
TEST_PASS net.http request default result is not success
TEST_PASS net.http client writes request and parses response
TEST_PASS net.http client rejects malformed response
TEST_PASS net.http client rejects invalid state transitions
TEST_PASS net.http client rejects invalid request inputs
TEST_PASS net.http client default result is not success
TEST_PASS net.http parses fixed-length 200 response
TEST_PASS net.http parses no-body response headers
TEST_PASS net.http rejects malformed status line
TEST_PASS net.http rejects truncated body or unsupported transfer
TEST_PASS net.http response parse default result is not success
TEST_PASS std.endian primitives
TEST_PASS host.tests
```

Issue #139 adds these host-test acceptance markers for parent verification:

```text
TEST_PASS pal.socket send receive transfers bytes
TEST_PASS pal.socket poll reports readiness
TEST_PASS pal.socket shutdown unblocks io
TEST_PASS pal.socket send rejects invalid state
```

Issue #144 adds these host-test acceptance markers for parent verification:

```text
TEST_PASS pal.socket options store kestrel flags
TEST_PASS pal.socket options reject unsupported values
TEST_PASS pal.socket endpoints report bound local address
TEST_PASS pal.socket endpoints report accepted peer address
TEST_PASS pal.socket reuse address policy is deterministic
```

Issue #271 adds these host-test acceptance markers for parent verification:

```text
TEST_PASS pal.socket partial receive preserves unread bytes
TEST_PASS pal.socket partial send reports backpressure
TEST_PASS pal.socket peer close returns eof after draining
TEST_PASS pal.socket peer reset reports connection reset
TEST_PASS pal.socket poll reports close and reset readiness
```

Issue #159 adds these host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp stream receives in-order payload
TEST_PASS net.tcp stream preserves unread bytes
TEST_PASS net.tcp stream rejects out-of-order payload
TEST_PASS net.tcp stream writes segmented payload
TEST_PASS net.tcp stream receive default result is not success
TEST_PASS net.tcp stream read default result is not success
TEST_PASS net.tcp stream send default result is not success
```

Issue #161 adds these host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp close active fin ack
TEST_PASS net.tcp close passive fin ack
TEST_PASS net.tcp close handles reset
TEST_PASS net.tcp close rejects invalid transition
TEST_PASS net.tcp close default result is not success
```

Issue #398 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp.close.ipv4 writes active fin checksum
TEST_PASS net.tcp.close.ipv4 rejects identity and short output
TEST_PASS net.tcp.close.ipv4 default result is not success
```

Issue #717 adds this focused source-guard marker for the TCP close IPv4
diagnostic formatter migration:

```text
TEST_PASS net.tcp.close.ipv4.formatter uses shared ascii text builder
```

Issue #169 adds these host-test acceptance markers for parent verification:

```text
TEST_PASS net.icmp echo request round trip
TEST_PASS net.icmp echo reply round trip
TEST_PASS net.icmp rejects bad checksum
TEST_PASS net.icmp rejects short packet
```

Issue #174 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.icmp.ipv4 frame writes echo request
TEST_PASS net.icmp.ipv4 frame parses echo reply
TEST_PASS net.icmp.ipv4 frame rejects bad ip checksum
TEST_PASS net.icmp.ipv4 frame rejects truncated payload
```

Issue #181 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.arp resolver writes request frame
TEST_PASS net.arp resolver accepts matching reply
TEST_PASS net.arp resolver rejects mismatched reply
TEST_PASS net.arp resolver times out request
TEST_PASS net.arp resolver default result is not success
```

Issue #189 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.arp.cache insert lookup
TEST_PASS net.arp.cache update existing
TEST_PASS net.arp.cache remove clear
TEST_PASS net.arp.cache capacity exhaustion
TEST_PASS net.arp.cache purge stale
TEST_PASS net.arp.cache rejects invalid inputs
TEST_PASS net.arp.cache default results are not success
TEST_PASS net.arp.update inserts validated reply
TEST_PASS net.arp.update refreshes existing entry
TEST_PASS net.arp.update rejects wrong sender address
TEST_PASS net.arp.update rejects wrong target mac
TEST_PASS net.arp.update reports cache capacity failure
TEST_PASS net.arp.update leaves cache unchanged on invalid reply
TEST_PASS net.arp.update default result is not success
```

Issue #196 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.ipv4.neighbor returns cached mac
TEST_PASS net.ipv4.neighbor writes arp request on cache miss
TEST_PASS net.ipv4.neighbor preserves cache on request write
TEST_PASS net.ipv4.neighbor rejects undersized request buffer
TEST_PASS net.ipv4.neighbor handles cache capacity unchanged
TEST_PASS net.ipv4.neighbor rejects invalid target input
TEST_PASS net.ipv4.neighbor default result is not success
```

Issue #202 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.rx.dispatch parses arp payload
TEST_PASS net.rx.dispatch parses ipv4 payload
TEST_PASS net.rx.dispatch reports classifier failure
TEST_PASS net.rx.dispatch reports arp parse failure
TEST_PASS net.rx.dispatch reports ipv4 parse failure
TEST_PASS net.rx.dispatch reports unsupported payload
```

Issue #207 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.udp.rx parses udp payload
TEST_PASS net.udp.rx reports classifier failure
TEST_PASS net.udp.rx rejects non-ipv4 payload
TEST_PASS net.udp.rx rejects non-udp protocol
TEST_PASS net.udp.rx reports truncated udp header
TEST_PASS net.udp.rx reports truncated udp payload
```

Issue #211 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.dhcp.udp writes discover broadcast frame
TEST_PASS net.dhcp.udp writes request broadcast frame
TEST_PASS net.dhcp.udp extracts server reply payload
TEST_PASS net.dhcp.udp rejects wrong udp ports
TEST_PASS net.dhcp.udp reports short frame buffer
TEST_PASS net.dhcp.udp reports malformed reply frame
TEST_PASS net.dhcp.udp write default result is not success
TEST_PASS net.dhcp.udp read default result is not success
```

Issue #220 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp.ipv4.frame writes syn frame
TEST_PASS net.tcp.ipv4.frame writes syn checksum
TEST_PASS net.tcp.ipv4.frame accepts syn ack frame
TEST_PASS net.tcp.ipv4.frame writes final ack frame
TEST_PASS net.tcp.ipv4.frame writes final ack checksum
TEST_PASS net.tcp.ipv4.frame rejects wrong peer
TEST_PASS net.tcp.ipv4.frame rejects protocol and bounds
TEST_PASS net.tcp.ipv4.frame rejects bad tcp checksum
TEST_PASS net.tcp.ipv4.frame rejects bad tcp syn ack
TEST_PASS net.tcp.ipv4.frame rejects identity and short output
TEST_PASS net.tcp.ipv4.frame default result is not success
```

Issue #270 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp.passive.ipv4 writes syn ack frame
TEST_PASS net.tcp.passive.ipv4 writes syn ack checksum
TEST_PASS net.tcp.passive.ipv4 accepts final ack frame
TEST_PASS net.tcp.passive.ipv4 rejects wrong listener identity
TEST_PASS net.tcp.passive.ipv4 rejects malformed protocol frames
TEST_PASS net.tcp.passive.ipv4 rejects bad tcp checksum
TEST_PASS net.tcp.passive.ipv4 rejects bad final ack
TEST_PASS net.tcp.passive.ipv4 rejects short output
TEST_PASS net.tcp.passive.ipv4 default result is not success
```

Issue #396 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.http.tcp.get writes request checksum
TEST_PASS net.http.tcp.get default result is not success
TEST_PASS net.http.tcp.get completes request response
```

Issue #397 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.http.tcp.get rejects bad response checksum
TEST_PASS net.http.tcp.get default result is not success
TEST_PASS net.http.tcp.get completes request response
```

Issue #721 adds this focused source-guard marker for the HTTP TCP GET
diagnostic formatter migration:

```text
TEST_PASS net.http.tcp.get formatter uses shared ascii text builder
```

Issue #726 adds this focused source-guard marker for the HTTP client
transaction diagnostic formatter migration:

```text
TEST_PASS network http client transaction formatter uses shared ascii text builder
```

Issue #771 adds these host-test acceptance markers for caller-owned HTTP static
response transaction diagnostic formatting:

```text
TEST_PASS net.http.static.formatter formats 200 marker
TEST_PASS net.http.static.formatter formats 404 and 405 metadata
TEST_PASS net.http.static.formatter reports destination capacity
TEST_PASS net.http.static.formatter rejects default and invalid results
TEST_PASS net.http.static.formatter uses shared ascii text builder
```

Issue #773 adds these host-test acceptance markers for caller-owned HTTP static
TCP response plan diagnostic formatting:

```text
TEST_PASS net.http.static.tcp.formatter formats 200 marker
TEST_PASS net.http.static.tcp.formatter formats 404 and 405 metadata
TEST_PASS net.http.static.tcp.formatter reports destination capacity
TEST_PASS net.http.static.tcp.formatter rejects default and invalid results
TEST_PASS net.http.static.tcp.formatter uses shared ascii text builder
```

Issue #6 step 599 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp.stream.ipv4 writes payload checksum
TEST_PASS net.tcp.stream.ipv4 receives checksum-valid payload
TEST_PASS net.tcp.stream.ipv4 rejects bad checksum
TEST_PASS net.tcp.stream.ipv4 rejects identity and short output
TEST_PASS net.tcp.stream.ipv4 default result is not success
```

Issue #6 step 605 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.http.request parses get healthz
TEST_PASS net.http.request preserves body metadata
TEST_PASS net.http.request rejects malformed request line
TEST_PASS net.http.request rejects invalid headers and body
TEST_PASS net.http.request default result is not success
```

Issue #6 step 652 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.http.request accepts single content length
TEST_PASS net.http.request rejects duplicate content length
```

Issue #6 step 611 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.http.response writes healthz text response
TEST_PASS net.http.response writes empty keepalive response
TEST_PASS net.http.response rejects invalid metadata
TEST_PASS net.http.response rejects short destination without write
TEST_PASS net.http.response default result is not success
```

Issue #6 step 630 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.http parses fixed-length 200 response
TEST_PASS net.http parses zero-length response body
TEST_PASS net.http rejects malformed status line
TEST_PASS net.http rejects incomplete headers
TEST_PASS net.http rejects invalid content length
TEST_PASS net.http rejects missing content length
TEST_PASS net.http rejects short body
TEST_PASS net.http rejects unsupported transfer encoding
TEST_PASS net.http response parse default result is not success
```

Issue #6 step 647 adds these focused host-test acceptance markers for parent verification:

```text
TEST_PASS net.tcp.rx dispatch accepts empty ack
TEST_PASS net.tcp.rx dispatch rejects out-of-window control
```

## Known Limitations

`FetchApp` and `TinyHttpServer` are placeholders and do not perform network I/O. No Kestrel compatibility claim can be made until TCP, timers, scheduler/threading, the selected application socket boundary, and the .NET 11 application runtime have real guest evidence. The [NativeAOT-first route](workload-profiles.md) supplies the restricted Linux socket/syscall boundary; a future hosted-runtime route instead requires its exported `OSHost_*` PAL. Both require actual guest Kestrel traffic and the applicable [completion-gate evidence](completion-gates.md#gate-14-profile-split).

The IPv4 helper currently supports only minimum 20-byte headers. UDP frame writers now emit IPv4 pseudo-header checksums for caller-owned outbound datagrams. The UDP receive dispatcher accepts zero checksums and validates non-zero checksums over caller-owned IPv4 UDP datagrams. The parser treats extra bytes beyond UDP length as invalid to keep early cloud packet behavior deterministic.

The ARP helper currently supports only fixed Ethernet hardware type, IPv4 protocol type, 6-byte hardware addresses, 4-byte protocol addresses, and request/reply opcodes. `ArpNeighborResolutionTransaction` adds a one-target caller-driven request/reply boundary and explicit timeout transition. `ArpNeighborCache` adds a bounded resolved-peer table over caller-owned storage. `ArpReceiveDispatchCacheUpdater` can update that table from already-dispatched ARP reply metadata, but the ARP path does not start timers, retransmit requests, send packets, wait for replies, process gratuitous ARP, or perform NIC-backed ARP resolution.

`Ipv4NeighborRoutePlanner` and `Ipv4NeighborRoutePlanDiagnosticFormatter` are host-tested cache lookup, ARP-request frame, and marker-formatting contracts only. The formatter serializes already-produced metadata into caller-owned bytes; it does not resolve routes over a NIC, update cache entries from live replies, publish virtio-net TX descriptors, retry ARP requests, route IPv4 packets, open sockets, execute FetchApp, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

The ICMP echo helper currently supports only echo request and echo reply packets with code zero. It validates the ICMP checksum and exposes payload slices from caller-owned buffers, but it does not perform IPv4 protocol dispatch, rate limiting, timestamp/address-mask ICMP messages, error message parsing, routing, retries, timers, or NIC-backed packet I/O.

`Ipv4IcmpEchoFrame` adds Ethernet + IPv4 protocol framing for ICMP echo over caller-owned buffers only. It does not allocate packet memory, read from a NIC, write to a NIC, perform ARP resolution, retry echo requests, track echo sessions, or schedule timeouts.

`IcmpReceiveDispatcher` adds caller-owned virtio-net receive-buffer dispatch for IPv4 ICMP echo request/reply packets only. It does not allocate packet memory, read from a NIC, update routing or ping state, generate replies, retry echo requests, track echo sessions, schedule timeouts, or expose a socket/PAL API.

The helper layer assumes the caller owns packet buffer lifetime and alignment. It can interpret caller-provided virtio-net receive-buffer bytes for header, Ethernet frame metadata, unsupported EtherType dispatch metadata, ARP packet metadata, IPv4 header metadata, and IPv4/UDP payload offset/length metadata. It can update caller-owned ARP cache storage from a successful ARP dispatch result after validation, and it can write a UDP Ethernet frame when the caller already supplies local and destination link/IP/port identity. It still does not read from a live virtio-net queue, publish a live TX descriptor, route IPv4 packets, or dispatch UDP payloads into DHCP, DNS, sockets, hosted .NET, or Kestrel.

`VirtioPciCapabilityListParser` and `VirtioPciDeviceDiscovery` prove only caller-provided config-byte parsing plus identity/capability metadata handling for modern virtio PCI devices. For networking, they can recognize a caller-provided virtio-net identity and required capability set, but they do not enumerate PCI config space, map BARs, validate BAR apertures, negotiate virtio-net features, create RX/TX queues, process interrupts, or move Ethernet frames.

`VirtioPciBarRegisterWindowPlanner` is host-tested register-window metadata only. It validates caller-provided mapped-BAR window records and computes future register-access addresses for virtio PCI apertures, but it does not map BARs, perform volatile/live MMIO, execute memory barriers, configure queues, notify hardware, process interrupts, allocate or map DMA memory, or prove live virtio-net packet I/O.

`VirtioNetQueueMmioSetupRegisterWindowPlanner` is host-tested metadata-only bridge evidence from queue setup commands to register-window access plans. `VirtioNetQueueMmioSetupRegisterWindowExecutor` adds fake byte-span execution over caller-owned common BAR-window bytes with staged read values only. These paths do not map BARs, perform volatile/live MMIO, execute live register reads/writes, configure a live queue, publish descriptors, execute real memory barriers, notify a device, process interrupts, allocate or map DMA memory, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioDeviceInitializationPlan` is status/feature planning only. It does not read device feature registers, write device status registers, observe live `FEATURES_OK`, select live queues, allocate DMA memory, create RX/TX queues, or move Ethernet frames.

`VirtioNetQueuePlan` is RX/TX queue planning only. It does not allocate RX/TX buffers, publish descriptors, write queue registers, notify a device, process interrupts, or move Ethernet frames. `VirtioNetQueueMmioSetupPlanner` is setup-command planning only, and `VirtioNetQueueMmioSetupExecutor` executes that plan only through an injected fake register accessor over caller-owned test storage. They do not map BARs, perform volatile/live MMIO, publish descriptors, notify hardware, allocate/map DMA memory, handle interrupts, or prove live queue setup. `VirtioNetQueueNotificationPlanner` is notify metadata planning only. It consumes already-successful queue setup and descriptor-publication results, calculates a future 16-bit notify write plan, maps that plan to caller-owned notify-aperture register-window access metadata, and preserves failure statuses, but it does not execute memory barriers, write MMIO, notify hardware, or prove live packet I/O. `VirtioNetQueueNotificationRegisterWindowExecutor` fake-executes the checked notify access plan over caller-owned notify BAR-window bytes only; it does not map BARs, perform volatile/live MMIO, execute memory barriers, or notify a live device. `VirtioNetQueueNotificationRegisterWindowBarrierExecutor` adds explicit metadata gating before that fake byte-span write, but still does not execute a real memory barrier or notify a live device. `VirtioNetQueueNotificationExecutor` is fake-sink notification execution only after explicit barrier-readiness metadata, with an optional fake register-accessor adapter over caller-owned test storage; it does not map BARs or notify a live device. `VirtioNetReceiveRegisterWindowCompletionPlanner` is caller-owned RX used-ring and single receive-buffer classification planning only after successful register-window readiness; `VirtioNetReceiveRegisterWindowSlabCompletionPlanner` adds caller-owned slab selection before that classification; `VirtioNetReceiveRegisterWindowDispatchPlanner` adds caller-owned ARP/IPv4 dispatch metadata after slab completion. They do not wait for a live device, process interrupts, mutate network state, or prove packet reception. `VirtioNetTransmitCompletionPlanner` is caller-owned used-ring metadata planning only. It validates one TX completion entry only after successful fake-sink or register-window notification execution provenance and returns reclaim metadata, but it does not wait for a live device, process interrupts, recycle buffers by itself, or prove packet transmission. `VirtioNetTransmitDescriptorOwnershipPlan` mutates only a caller-owned descriptor state span between `Free` and `InFlight`; `VirtioNetTransmitDescriptorFreeListPlanner` reads that span to select the first contiguous free TX pair and report capacity exhaustion without mutation; `VirtioNetTransmitDescriptorPublishPlanner` composes that selection with caller-owned queue-memory/header writes and ownership marking after successful publication; and `VirtioNetTransmitRegisterWindowReclaimPlanner` composes already-successful register-window TX readiness with one caller-owned completion and descriptor-state reclaim. Together they are not live virtio-net TX, not a full runtime descriptor allocator, do not coordinate concurrent producers, do not execute real memory barriers, do not notify or poll a live device, and do not prove live descriptor reuse or packet transmission. `VirtioNetDeviceConfig` parses caller-provided configuration bytes only, and `VirtioNetDeviceConfigRegisterWindowReader` fake-reads caller-owned device BAR-window bytes only; neither reads live MMIO config space, validates live feature negotiation, assigns an interface address, or proves live virtio-net discovery. `VirtioNetHeader`, `VirtioNetReceiveFrameClassifier`, `NetworkReceiveDispatcher`, `ArpReceiveDispatchCacheUpdater`, `UdpReceiveDispatcher`, and `UdpTransmitPlanner` are caller-buffer parser/classifier/cache-update/writer helpers only; they do not negotiate offload features, support mergeable buffers, publish descriptors to a live device, process interrupts, own sockets, or prove live virtio-net packet I/O.

`VirtioNetReceiveReadinessPlanner` is host-tested register-window RX readiness composition evidence only. It composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution, but it does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify a live device, allocate or map DMA memory, process interrupts, poll hardware, complete receives, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetReceiveRegisterWindowDispatchPlanner` is host-tested register-window RX completion/slab-selection/classification/dispatch composition evidence only. It composes successful readiness with caller-owned used-ring and receive-buffer-slab snapshots, selects the completed buffer before classification, and invokes network payload dispatch metadata, but it does not poll hardware, process interrupts, transfer packets, notify live hardware, allocate or map DMA memory, mutate network state, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetReceiveRegisterWindowTcpDispatchPlanner` is host-tested
register-window RX-to-TCP receive-dispatch composition evidence only. It
consumes the completed caller-owned slab dispatch result and selected buffer
slice before invoking `TcpReceiveDispatcher`.
`VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter` only formats
already-produced result metadata into caller-owned marker bytes. These paths do
not poll hardware, process interrupts, transfer packets, notify live hardware,
allocate or map DMA memory, expose sockets, deliver bytes to PAL socket receive,
own TCP control blocks, retransmit, run timers, or prove hosted .NET 10,
ASP.NET Minimal API, or Kestrel readiness.
`BootSequence.RunVirtioNetRxTcpDispatchReadiness` is likewise a host-tested
marker wrapper only and is not emitted by the default firmware-visible boot
path.

`VirtioNetTransmitReadinessPlanner` is host-tested fake-accessor TX composition evidence only. It composes successful queue setup execution, caller-owned descriptor publication, explicit barrier-readiness metadata, notification planning, and fake-accessor notification execution, but it does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify a live device, allocate or map DMA memory, process interrupts, poll hardware, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetTransmitRegisterWindowReadinessPlanner` is host-tested register-window TX composition evidence only. It composes successful queue setup register-window fake execution, caller-owned descriptor publication/state marking, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution, but it does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify a live device, allocate or map DMA memory, process interrupts, poll hardware, wait for completion, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetTransmitRegisterWindowReclaimPlanner` is host-tested register-window TX completion/reclaim composition evidence only. It composes successful readiness with caller-owned used-ring and descriptor-state data, but it does not wait for a live device, process interrupts, recycle buffers through a runtime allocator, perform volatile/live MMIO, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`DhcpMessage` is an encode/decode primitive only. `DhcpClientTransaction` is a mock-driven state machine only. `DhcpLeaseConfiguration` is immutable metadata only. `DhcpUdpFrame` wraps and unwraps caller-provided packet buffers only. `DhcpClientTransactionPlanner` composes those helpers into a deterministic caller-driven flow, but it does not implement retransmission timers, lease renewal/rebind scheduling, address assignment to an interface, ARP conflict detection, socket creation, packet transmission, lease application, or NIC-backed packet I/O.

`DnsMessage` is an encode/decode primitive only. `DnsResolverTransaction` is a mock-driven state machine only. `DnsUdpFrame` composes those primitives with caller-owned Ethernet/IPv4/UDP frame buffers and parser metadata only. `DnsResolverTransactionPlanner` validates a caller-driven query/response transaction over those helpers, but it does not open sockets, choose routes, generate random transaction IDs, retransmit queries, maintain timers beyond explicit caller-driven timeout transitions, cache answers, perform live DNS over a NIC, apply DHCP DNS configuration, or support CNAME/AAAA/TXT/multiple-question resolution.

`HttpRequestParser` and `HttpResponseParser` are parser-only helpers over caller-owned byte spans. The current response parser requires `Content-Length` and fixed-length bodies; it does not implement close-delimited bodies or chunked transfer. `HttpResponseWriter` is a response byte writer over caller-owned buffers only. `HttpClientGetTransaction` is a mock-stream state machine only, and its diagnostic formatter only serializes already-produced metadata into caller-owned ASCII storage. `HttpTcpClientGetPlanner` composes those helpers with caller-owned Ethernet/IPv4/TCP frame buffers, inherits active-connect outbound SYN/final ACK checksum generation plus SYN-ACK checksum validation from `TcpIpv4FramePlanner`, computes one outbound HTTP GET request TCP pseudo-header checksum, and validates the inbound HTTP response TCP pseudo-header checksum before stream acceptance. `FetchAppHttpRequestEndpoint` derives endpoint metadata from already-parsed native manifest environment/config entries only; its diagnostic formatter and BootSequence readiness marker serialize only status/config-source metadata and host/path byte counts, never host/path bytes. `FetchAppHttpCompletionExpectation` derives expectation metadata from already-parsed native manifest environment/config entries only; its diagnostic formatter serializes only status/config metadata and never marker/body bytes. It does not load manifests from storage or parse JSON. `FetchAppHttpCompletionPlan` only evaluates an existing successful HTTP TCP GET plan plus caller-owned response bytes for a configured status/body marker, its diagnostic formatter only serializes that already-produced completion metadata into caller-owned ASCII storage, and `BootSequence.RunFetchAppHttpCompletionReadiness` only emits ordered readiness markers from that metadata. `FetchAppHttpTcpExchangePlan` composes those already-prepared endpoint/TCP/completion pieces across caller-owned frame buffers and an active-close FIN/ACK frame, its diagnostic formatter only serializes already-produced exchange metadata into caller-owned ASCII storage without dumping configured host/path/marker bytes, and `BootSequence.RunFetchAppHttpTcpExchangeReadiness` only emits opt-in ordered readiness markers from that metadata. It still assumes DNS and ARP already succeeded and never opens sockets or talks to a NIC. These helpers still do not open sockets, publish NIC descriptors, retransmit packets, own timers, route through the generic established-stream frame planner as a socket path, handle chunked transfer, perform TLS, follow redirects, parse a live Kestrel request stream, write a live HTTP server response, execute FetchApp, or prove FetchApp/Kestrel network behavior.

`TcpHeader` is a header primitive only. Its write path routes already-validated
options and payload copies through `OS.Std.Memory.BufferOperations.MemoryCopy`
and maps unexpected post-validation copy failures to the existing
buffer-too-small status. It does not implement sequence tracking,
acknowledgements, retransmission, connection state, checksums over
pseudo-headers, timers, sockets, or NIC-backed packet I/O.

`TcpClientHandshake` is a mock-packet handshake state machine only. It does not implement retransmission, timers, receive windows, checksum validation, data transfer, close states, sockets, or NIC-backed packet I/O.

`TcpActiveConnectTransaction` is a mock-packet active connect transaction only. Its `Timeout` method is caller-driven and does not own a timer wheel, retransmit SYN segments, allocate a TCP control block, validate pseudo-header checksums, transfer data, close the connection, expose socket handles, or perform NIC-backed packet I/O.

`TcpIpv4FramePlanner` is an active-connect frame wrapper over caller-owned Ethernet buffers only. Its TCP pseudo-header checksum evidence is limited to outbound SYN/final ACK generation and inbound SYN-ACK validation on the active-connect path. It does not resolve ARP, select routes, publish virtio-net TX/RX descriptors, enforce checksums on established payload frames, allocate a TCP control block, retransmit SYN frames, own timers, expose sockets/PAL handles, transfer payload data, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpPassiveIpv4FramePlanner` is a passive-open frame wrapper over caller-owned Ethernet buffers only. Its TCP pseudo-header checksum evidence is limited to outbound SYN-ACK generation plus inbound listener SYN and final ACK validation on the passive-open path. It does not resolve ARP, select routes, publish virtio-net TX/RX descriptors, enforce checksums on established payload frames, allocate a listener or TCP control block, maintain a listener table/backlog/accept queue, retransmit SYN-ACK frames, own timers, expose sockets/PAL handles, transfer payload data, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpListenerBacklog` is a caller-owned metadata table only. It does not parse packets, validate TCP checksums, allocate TCP control blocks, retransmit SYN-ACK frames, own timers, expose sockets/PAL handles, coordinate concurrent producers, integrate manifest bind policy, transfer payload data, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpPassiveListenerBacklogBridge` is a metadata bridge from an already-successful passive-open frame result into a caller-owned backlog table only. It does not run the handshake, parse packets, validate TCP checksums, allocate TCP control blocks, own timers, retransmit SYN-ACK frames, expose sockets/PAL handles, accept sockets, transfer payload data, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpListenerBacklogSocketBridge` is a host-only metadata bridge from a caller-owned backlog table into the in-process `OSHostSocketTable` mock accept queue. It does not export `OSHost_SocketAccept`, create a live TCP listener, own listener connection tables, block or wake scheduler waits, transfer payload data, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpConnectionTable` retains its span metadata API and adds an exclusive owned
API over1..64 mapped caller-owned entries and generation words. Opaque handles
join owner address, unique lifetime epoch and nonwrapping slot generation. A
half-open or closed unreleased connection holds its tuple; only explicit release
clears the entry and permits reuse. One serialized kernel caller must retain all
backing, validate actual protocol events and release external packet/timer leases
before metadata release. The table performs no NIC I/O, socket acceptance, timer,
retransmission, receive-window or send-queue processing. Its new ownership API has
host development evidence only. Sequence metadata has an explicit initialization
flag so zero remains a valid sequence number, as required by
[RFC9293 section3.4](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.4).
Default metadata remains invalid; this internal layout change still needs the
new native compile/ABI/guest qualification. No hosted .NET, Kestrel, cloud or
production support is established.

`OS.Std.Net.InternetChecksum` and `Ipv4TransportChecksum` are checksum primitives. Current enforcement is limited to outbound IPv4 UDP checksum generation in `EthernetIpv4UdpPacket.TryWrite`, non-zero IPv4 UDP checksum validation in `UdpReceiveDispatcher`, active-connect outbound SYN/final ACK generation plus inbound SYN-ACK validation in `TcpIpv4FramePlanner`, passive-open outbound SYN-ACK generation plus inbound SYN/final ACK validation in `TcpPassiveIpv4FramePlanner`, outbound active-close FIN/ACK checksum generation in `TcpConnectionCloseIpv4FramePlanner`, established payload send/receive frame checksums in `TcpEstablishedStreamIpv4FramePlanner`, inbound established payload/passive FIN/RST receive dispatch plus passive-close ACK checksum writing in `TcpReceiveDispatcher`, and HTTP GET request checksum generation plus response checksum validation in `HttpTcpClientGetPlanner`; they do not provide live NIC receive checksums, do not validate live NIC packets, and do not provide socket, hosted .NET 10, ASP.NET Minimal API, or Kestrel proof.

`NativeAppSocketBindPolicy` is a manifest/PAL-endpoint metadata validator only. `NativeAppSocketBindBridge` composes that policy with the in-process `OSHostSocketTable.Bind` mock only after policy success. `NativeAppSocketListenBridge` composes the same policy with in-process mock socket create/bind/listen calls. These bridges do not export `OSHost_SocketBind` or `OSHost_SocketListen`, open live sockets, create real TCP listeners, allocate real listener state, enter a scheduler wait, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`TcpPassiveOpenHandshake` is a mock-packet listener handshake state machine only. It does not allocate or manage a TCP control block, listen backlog, accepted socket, retransmission timer, receive window, checksum validation, data transfer, close state, socket/PAL handle, or NIC-backed packet I/O.

`TcpEstablishedStream` is an established-payload primitive over caller-owned buffers only. `TcpEstablishedStreamIpv4FramePlanner` adds caller-owned Ethernet/IPv4/TCP frame checksum wrapping for that primitive, but neither path allocates or manages TCP control blocks, performs retransmission, owns timers, reorders out-of-order segments, implements receive-window negotiation, exposes socket/PAL handles, publishes NIC descriptors, or performs NIC-backed packet I/O.

`TcpEstablishedStreamSocketBridge` is a host-only inbound bridge from a successful established-stream receive result and caller-owned payload bytes into the in-process `OSHostSocketTable` mock connected-socket buffer. It does not export `OSHost_SocketReceive`, create a live socket, own receive windows, validate live TCP checksums, block or wake scheduler waits, route packets from a NIC, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpConnectionClose` is an established-connection close/reset transition primitive over caller-owned segment buffers only. It does not allocate or manage TCP control blocks, perform retransmission, own timers, implement `TIME_WAIT` cleanup, handle simultaneous close, calculate pseudo-header checksums, expose socket/PAL handles, or perform NIC-backed packet I/O.

`TcpConnectionCloseIpv4FramePlanner` is an active-close frame wrapper over caller-owned Ethernet buffers only. Its TCP pseudo-header checksum evidence is limited to outbound active FIN/ACK frame generation, and its default/zero frame-plan result is rejected as not successful. `TcpConnectionCloseIpv4FramePlanDiagnosticFormatter` only serializes already-produced planner metadata into caller-owned marker bytes. It does not validate inbound close ACK/FIN/RST checksums, wrap passive close, resolve ARP, select routes, publish virtio-net TX/RX descriptors, allocate TCP control blocks, retransmit, own timers, expose sockets/PAL handles, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`TcpReceiveDispatcher` is a receive-side metadata router over caller-owned virtio-net buffers and already-parsed network dispatch results only. It validates the inbound IPv4/TCP checksum before mutation, rejects malformed or out-of-window ACK/FIN/RST control metadata before mutation, feeds only established payload into `TcpEstablishedStream`, feeds passive FIN/ACK and RST into `TcpConnectionClose`, and writes one passive-close ACK segment into caller-owned storage. `TcpReceiveDispatchDiagnosticFormatter` only formats already-produced result metadata into caller-owned marker bytes. These paths do not allocate or own TCP control blocks, expose sockets/PAL handles, select routes, resolve ARP, publish virtio-net descriptors, process interrupts, retransmit, own timers, perform TLS, or prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

`HttpRequestBuilder` is a request byte writer only. `HttpRequestParser` is a server-side request metadata parser only. `HttpResponseWriter` is a server-side response byte writer only. None of these opens TCP connections, parses full URLs, manages redirects, sends or receives bytes over a socket, performs TLS, handles chunked bodies, applies Kestrel middleware semantics, or proves FetchApp/Kestrel network behavior.

`HttpStaticResponseTransactionDiagnosticFormatter` only serializes already-produced `HttpStaticResponseTransactionResult` metadata into caller-owned marker bytes. It does not parse live request streams, accept connections, bind/listen sockets, start a TinyHttpServer loop, start hosted .NET, start ASP.NET Minimal API, start Kestrel, or prove cloud readiness.

`HttpStaticTcpResponsePlan` only composes already-produced TCP receive-dispatch metadata with caller-owned HTTP request/response buffers, and `HttpStaticTcpResponsePlanDiagnosticFormatter` only serializes already-produced plan metadata into caller-owned marker bytes. Neither path publishes a response segment, enqueues socket bytes, binds or listens on a port, polls virtio-net, processes interrupts, owns timers, retransmits, performs TLS, starts a TinyHttpServer loop, starts hosted .NET, starts ASP.NET Minimal API, starts Kestrel, or proves cloud readiness.

`HttpStaticTcpResponseIpv4FramePlan` only writes a synthetic outbound response frame into caller-owned memory after already-accepted TCP payload metadata exists. It does not publish descriptors, notify virtio-net, poll a NIC, process interrupts, enqueue PAL socket bytes, own a listener table, retransmit, segment multi-frame responses, own timers, perform TLS, start a TinyHttpServer loop, start hosted .NET, start ASP.NET Minimal API, start Kestrel, or prove cloud readiness.

`AwsImdsV2RequestPlanner` and `AwsImdsAppMetadataPolicyPlanner` are request
metadata and policy fixtures only. They do not open a socket, fetch a token,
fetch metadata or user data, cache credentials, expose IAM credentials, enforce
app-supervisor policy, or prove EC2 IMDS reachability.

`OSHostSocketTable` and `OSHostSocketFacade` connected-handle partial send/receive/poll/shutdown/options/endpoint-query/peer-close/peer-reset behavior is a host-only mock contract. The facade is shaped like future `OSHost_Socket*` calls, but it does not export `OSHost_SocketSend`, `OSHost_SocketReceive`, `OSHost_SocketPoll`, `OSHost_SocketShutdown`, `OSHost_SocketSetOption`, `OSHost_SocketGetLocalAddress`, or `OSHost_SocketGetRemoteAddress`, does not perform TCP FIN/RST packet behavior, does not unblock real scheduler waits, and does not prove hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Kestrel Socket Compatibility Test Plan

This section defines the networking test ladder required before ASP.NET Minimal API/Kestrel support can be claimed. It is a plan only; it does not implement TCP, sockets, PAL exports, hosted .NET, Kestrel, or runner HTTP verification.

The tests must stay layered so agents can implement packet encode/decode, the TCP state machine, the PAL socket boundary, and the app-level Kestrel smoke independently.

### Layer 1 - Packet Encode/Decode

Scope: deterministic byte-level host tests for packets. These tests do not require a NIC, scheduler, PAL, hosted runtime, or Kestrel.

Required tests:

- Ethernet, ARP, IPv4, ICMP if used for diagnostics, UDP, DHCP, DNS, and TCP header encode/decode round trips.
- Big-endian field encoding for ports, sequence numbers, acknowledgement numbers, window, urgent pointer, EtherType, IPv4 total length, and checksums.
- Invalid length, unsupported protocol, invalid checksum, unsupported options, and truncated payload rejection.
- TCP flag combinations, MSS/window-scale/SACK-permitted option parsing if those options are implemented.
- Zero-copy or caller-owned-buffer behavior where APIs accept `Span<byte>` or `ReadOnlySpan<byte>`.

Required markers:

```text
TEST_START net.packet.<name>
TEST_PASS net.packet.<name>
TEST_FAIL net.packet.<name> reason=<code>
```

Expected failure diagnostics must include packet layer, parse status, expected length, actual length, protocol number, and checksum status where applicable. Packet tests must not require serial output unless they are promoted into a guest smoke path.

### Layer 2 - Raw TCP State Machine

Scope: host tests and later guest tests for TCP behavior without PAL sockets or Kestrel. The state machine can run against mock packet I/O before virtio-net is complete.

Required tests:

- Passive open: `LISTEN -> SYN_RECEIVED -> ESTABLISHED`.
- Active close and passive close: FIN/ACK transitions through `FIN_WAIT_*`, `CLOSE_WAIT`, `LAST_ACK`, and `TIME_WAIT` where implemented.
- RST handling for unopened ports, invalid sequence numbers, and reset by peer.
- Sequence and acknowledgement number validation.
- Retransmission timeout, delayed ACK policy if implemented, duplicate ACK handling, and out-of-order segment handling.
- Receive stream reassembly for split request headers and fragmented application payloads.
- Send segmentation for response bodies larger than one MSS.
- Receive window updates and send-side backpressure.
- Connection table limits, listen backlog limits, and deterministic exhaustion errors.
- Timer-driven cleanup of half-open, closed, and `TIME_WAIT` entries.

Required markers:

```text
TEST_START net.tcp.<name>
TEST_PASS net.tcp.<name>
TEST_FAIL net.tcp.<name> reason=<code> state=<state> local=<addr:port> remote=<addr:port>
```

Expected failure diagnostics must include TCP state, local endpoint, remote endpoint, sequence number, acknowledgement number, advertised window, timer state, and packet status. Guest serial diagnostics should also include `TEST_FAIL net.tcp.<name>` before panic or shutdown if the TCP stack detects internal corruption.

Current #126 evidence covers only the passive-open handshake slice with mock TCP segment buffers. It proves deterministic `LISTEN -> SYN_RECEIVED -> ESTABLISHED` state decisions and rejection paths, but it is not evidence of a real listener, backlog management, accept queue, retransmission, timers, data transfer, socket handles, virtio-net packet I/O, hosted .NET, or Kestrel.

Current #391 evidence covers only passive-open TCP/IPv4 inbound SYN and final ACK pseudo-header checksum validation over caller-owned Ethernet frame buffers. It proves corrupted checksums are rejected with `PacketParseStatus.BadChecksum` before `LISTEN` or `SYN_RECEIVED` state advances, but it is not evidence of outbound SYN-ACK checksum generation, established payload checksum enforcement, a real listener, retransmission, timers, socket handles, virtio-net packet I/O, hosted .NET, or Kestrel.

Current #392 evidence covers only caller-owned UDP receive dispatch for IPv4 UDP datagrams. It proves checksum zero remains accepted, non-zero checksums are validated, and corrupted non-zero checksums return `PacketParseStatus.BadChecksum`, but it is not evidence of outbound UDP checksum generation, UDP sockets, DHCP/DNS live packet I/O, virtio-net packet I/O, hosted .NET, or Kestrel.

Current #393 evidence covers only caller-owned outbound Ethernet/IPv4/UDP frame generation. It proves UDP datagrams written through `EthernetIpv4UdpPacket.TryWrite` and `UdpTransmitPlanner` carry non-zero IPv4 pseudo-header checksums that validate, but it is not evidence of live NIC TX, descriptor publication, ARP routing, DHCP/DNS live packet I/O, UDP sockets, hosted .NET, or Kestrel.

Current #394/#455 evidence covers only caller-owned active-connect Ethernet/IPv4/TCP frame generation. It proves SYN and final ACK frames written through `TcpIpv4FramePlanner` carry non-zero IPv4 TCP pseudo-header checksums that validate, existing SYN-ACK receive checksum validation still passes, and default/zero frame-plan results are rejected as not successful. It is not evidence of passive SYN-ACK checksum generation, established payload checksum enforcement, live NIC TX/RX, retransmission, timers, sockets/PAL exports, hosted .NET, or Kestrel.

Current #395/#456 evidence covers only caller-owned passive-open Ethernet/IPv4/TCP SYN-ACK frame generation. It proves SYN-ACK frames written through `TcpPassiveIpv4FramePlanner` carry non-zero IPv4 TCP pseudo-header checksums that validate, existing inbound SYN/final ACK checksum validation still passes, and default/zero frame-plan results are rejected as not successful. It is not evidence of established payload checksum enforcement, a live listener, retransmission, timers, socket/PAL exports, virtio-net packet I/O, hosted .NET, or Kestrel.

Current #396 evidence covers only caller-owned HTTP GET outbound Ethernet/IPv4/TCP request frame generation. It proves the PSH/ACK request frame written through `HttpTcpClientGetPlanner` carries a non-zero IPv4 TCP pseudo-header checksum that validates, while the existing request/response completion fixture still passes. It is not evidence of inbound established response checksum validation, generic stream-level checksum enforcement, sockets/PAL exports, live virtio-net I/O, retransmission, timers, hosted .NET, or Kestrel.

Current #397 evidence covers only caller-owned HTTP GET inbound Ethernet/IPv4/TCP response frame validation. It proves a corrupted inbound response TCP checksum is rejected with `PacketParseStatus.BadChecksum` and nested `TcpIpv4FramePlanStatus.BadTcpChecksum` before stream or HTTP state advances, while the existing request/response completion fixture still passes. It is not evidence of generic stream-level checksum enforcement, sockets/PAL exports, live virtio-net I/O, retransmission, timers, hosted .NET, or Kestrel.

Current #721 evidence covers only the caller-owned HTTP TCP GET diagnostic
formatter migration to shared no-runtime ASCII helper calls. It proves
`HTTP_TCP_GET_PLAN` marker byte accounting and writes use
`AsciiDecimalFormatter` and `AsciiTextBuilder` instead of formatter-local
decimal loops. It is not evidence of live HTTP GET, sockets/PAL exports,
virtio-net packet I/O, retransmission, timers, hosted .NET, ASP.NET Minimal
API, or Kestrel.

Current #726 evidence covers only the caller-owned HTTP client transaction
diagnostic formatter migration to shared no-runtime ASCII helper calls. It
proves `HTTP_CLIENT_GET_TRANSACTION` marker byte accounting and writes use
`AsciiDecimalFormatter` and `AsciiTextBuilder` instead of formatter-local
decimal loops while preserving short-destination and default-result behavior.
It is not evidence of live virtio-net, DHCP, DNS, TCP, HTTP over a NIC,
sockets/PAL exports, retransmission, timers, hosted .NET, ASP.NET Minimal API,
or Kestrel.

Current #733 evidence covers only the caller-owned FetchApp HTTP request
endpoint diagnostic formatter migration to shared no-runtime ASCII helper
calls. It proves `FETCH_HTTP_ENDPOINT` marker byte accounting and writes use
`AsciiDecimalFormatter` and `AsciiTextBuilder` instead of formatter-local
decimal loops while preserving required-byte queries, short-destination
behavior, invalid/default result rejection, and exact marker text. It is not
evidence of live virtio-net, DNS, TCP, HTTP over a NIC, FetchApp execution,
FetchApp QEMU output, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #737 evidence covers only the caller-owned TCP receive-dispatch
diagnostic formatter migration to shared no-runtime ASCII helper calls. It
proves `TCP_RX_DISPATCH` marker byte accounting and writes use
`AsciiDecimalFormatter` and `AsciiTextBuilder` instead of formatter-local
destination-slice copy/format calls while preserving required-byte queries,
short-destination behavior, default-result rejection, and exact marker text. It
is not evidence of live virtio-net RX, socket delivery, exported PAL socket
receive, retransmission, receive windows, timers, hosted .NET, ASP.NET Minimal
API, or Kestrel.

Current #738 evidence covers only FetchApp HTTP completion,
completion-expectation, and TCP exchange diagnostic formatter internals. It
proves those stable caller-owned ASCII markers route byte and decimal appends
through `OS.Std.Text.AsciiTextBuilder` while preserving exact marker text,
short-destination no-write behavior, and default-result rejection. It is not
evidence of live virtio-net, DNS, TCP, HTTP over a NIC, FetchApp execution,
hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #743 evidence covers only the caller-owned FetchApp expected-marker
copy path. It proves `FetchAppHttpCompletionExpectation.CopyExpectedMarker`
routes selected default or manifest-configured marker bytes through
`OS.Std.Memory.BufferOperations.MemoryCopy` after the existing status-coded
length checks. It is not evidence of live FetchApp execution, virtio-net, DNS,
TCP, HTTP over a NIC, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #744 evidence covers only the caller-owned FetchApp request endpoint
host/path copy path. It proves `FetchAppHttpRequestEndpoint.ValidateAndCopy`
routes selected default, explicit, or URL-derived host and path bytes through
`OS.Std.Memory.BufferOperations.MemoryCopy` after the existing status-coded
field and destination checks. It is not evidence of live FetchApp execution,
DNS, TCP, HTTP over a NIC, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #748 evidence covers only the caller-owned HTTP GET request builder
dynamic host/path copy path. It proves `HttpRequestBuilder.TryWriteGet` routes
validated host and path bytes through `OS.Std.Memory.BufferOperations.MemoryCopy`
while preserving the existing byte-for-byte request output, validation statuses,
and destination-too-small behavior. It is not evidence of live TCP, HTTP over a
NIC, FetchApp execution, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #749 evidence covers only the caller-owned HTTP response writer dynamic
reason phrase, content type, and body copy path. It proves
`HttpResponseWriter.TryWrite` routes validated response bytes through
`OS.Std.Memory.BufferOperations.MemoryCopy` while preserving existing
byte-for-byte response output, validation statuses, and destination-too-small
behavior. It is not evidence of live sockets, TCP, HTTP over a NIC, hosted
.NET, ASP.NET Minimal API, or Kestrel.

Current #750 evidence covers only the caller-owned UDP and ICMP payload copy
paths. It proves `UdpHeader.TryWrite` and `IcmpEchoPacket.TryWrite` route
payload bytes through `OS.Std.Memory.BufferOperations.MemoryCopy` after the
existing length checks while preserving UDP frame bytes and ICMP checksum
behavior. The focused guard marker is `TEST_PASS net.packet udp icmp payloads
use shared buffer operations`. It is not evidence of live virtio-net, packet
I/O, sockets, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #754 evidence covers only the caller-owned TCP header options and
payload copy path. It proves `TcpHeader.TryWrite` routes validated options and
payload bytes through `OS.Std.Memory.BufferOperations.MemoryCopy` after the
existing option-alignment and segment-capacity checks while preserving current
`PacketParseStatus` behavior. The focused guard marker is `TEST_PASS net.tcp
header payloads use shared buffer operations`. It is not evidence of live
virtio-net, TCP state, sockets, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #755 evidence covers only caller-owned TCP-over-IPv4 frame planner
segment copy paths. It proves `TcpIpv4FramePlanner`,
`TcpPassiveIpv4FramePlanner`, `TcpEstablishedStreamIpv4FramePlanner`, and
`TcpConnectionCloseIpv4FramePlanner` route validated TCP segment copies through
`OS.Std.Memory.BufferOperations.MemoryCopy` after existing frame-size checks
while preserving the existing frame-too-small/non-success status behavior. The
focused guard marker is `TEST_PASS net.tcp ipv4 frame planners use shared
buffer operations`. It is not evidence of live virtio-net, TCP state,
sockets/PAL exports, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #398/#457/#717 evidence covers only caller-owned active-close Ethernet/IPv4/TCP FIN/ACK frame generation and the corresponding diagnostic formatting boundary. It proves the active close FIN/ACK frame written through `TcpConnectionCloseIpv4FramePlanner` carries a non-zero IPv4 TCP pseudo-header checksum that validates, invalid identity and undersized-output failures leave close state unchanged, default/zero frame-plan results are rejected as not successful, and `TcpConnectionCloseIpv4FramePlanDiagnosticFormatter` uses shared no-runtime ASCII text helpers while preserving short-destination no-partial-write behavior. It is not evidence of inbound close ACK/FIN/RST checksum validation, passive-close frame wrapping, generic stream-level checksum enforcement, sockets/PAL exports, live virtio-net I/O, retransmission, timers, hosted .NET, or Kestrel.

Current #6 TCP receive-dispatch evidence covers only caller-owned receive buffers and synthetic parsed network metadata. It proves `TcpReceiveDispatcher` can accept an established payload, accept a valid empty ACK without state mutation, acknowledge passive FIN/ACK with a checksummed caller-owned ACK segment, handle RST, reject malformed or out-of-window ACK/FIN/RST control metadata before mutation, reject bad checksums before mutation, propagate out-of-order stream failures, reject non-TCP IPv4, and reject default/zero results. It is not evidence of live virtio-net RX, socket delivery, exported PAL socket receive, retransmission, receive windows, timers, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #761 evidence covers only the caller-owned virtio-net RX slab
completion to TCP receive-dispatch composition. It proves a completed
register-window RX slab buffer can carry established TCP payload metadata into
`TcpReceiveDispatcher`, can write a passive-close ACK segment, and preserves
nested RX dispatch, slab completion, network dispatch, TCP status/action,
descriptor head, buffer index, selected-buffer range, used byte count, payload
bytes, and passive ACK byte metadata. It also proves failed RX dispatch,
unsupported protocol, bad TCP checksum, TCP dispatch/stream failure, and
default-result rejection. It is not evidence of live virtio-net RX, packet I/O,
socket delivery, exported PAL socket receive, retransmission, receive windows,
timers, hosted .NET, ASP.NET Minimal API, or Kestrel.

Current #377 evidence covers only the caller-owned TCP listener backlog table. It proves enqueue/dequeue, FIFO accept ordering, duplicate endpoint rejection, reusable slots, capacity reporting, and no-managed-reference result metadata over synthetic endpoint inputs. It is not evidence of a live TCP listener, socket/PAL handles, manifest-approved bind/listen enforcement, retransmission, timers, data transfer, virtio-net packet I/O, hosted .NET, or Kestrel.

Current #379 evidence covers only the passive-open-to-listener-backlog bridge. It proves successful final-ACK metadata can enqueue one caller-owned backlog entry and that failed/non-established passive-open results plus backlog-full/duplicate failures preserve caller-owned entries. It is not evidence of a live TCP listener, socket/PAL handles, manifest-approved bind/listen enforcement, retransmission, timers, data transfer, virtio-net packet I/O, hosted .NET, or Kestrel.

Current #384/#452 evidence covers only the listener-backlog-to-mock-accept-queue bridge. It proves a caller-owned backlog entry can be copied into the host-only `OSHostSocketTable` accept queue, empty backlogs and mock socket-table failures are status-coded, endpoint metadata is preserved, and default/zero bridge results are rejected as not successful. It is not evidence of exported `OSHost_SocketAccept`, a live TCP listener, scheduler waits, payload transfer, hosted .NET, or Kestrel.

Current #388/#415 evidence covers only the established-stream-to-mock-socket inbound bridge. It proves a successful established-stream receive result plus caller-owned payload bytes can queue bytes into a connected host-only mock socket, failed or default receive results and payload length mismatches do not mutate socket state, mock queue failures are propagated, and the no-export/no-live-socket/no-scheduler/no-Kestrel boundary is preserved. It is not evidence of exported `OSHost_SocketReceive`, live TCP packet delivery, scheduler waits, hosted .NET, or Kestrel.

Current #381 evidence covers only the manifest listen-endpoint bind policy. It proves a future PAL IPv4 bind endpoint can be checked against source-backed launch metadata and rejected for missing listen data, unsupported host text, invalid endpoint values, wrong ports, or wrong addresses. It is not evidence of a socket bind/listen call, accepted connections, TCP listener state, PAL exports, hosted .NET, or Kestrel.

Current #383 evidence covers only manifest-approved binding into the host-only mock socket table. It proves policy failure prevents mock socket-state mutation and policy success can call `OSHostSocketTable.Bind` while preserving mock bind conflicts. It is not evidence of exported `OSHost_SocketBind`, real sockets, TCP listen state, accepted connections, hosted .NET, or Kestrel.

Current #385 evidence covers only manifest-approved listener creation in the host-only mock socket table. It proves policy success can create, bind, and listen through `OSHostSocketTable`, policy failures avoid table mutation, and mock create/bind/listen failures remain status-coded. It is not evidence of exported `OSHost_SocketBind` or `OSHost_SocketListen`, real sockets, live TCP listener state, accepted connections, scheduler waits, hosted .NET, or Kestrel.

Current #132 evidence covers only the active-connect transaction slice with mock TCP segment buffers. It proves deterministic `CLOSED -> SYN_SENT -> ESTABLISHED` state decisions plus bad SYN-ACK, invalid state, insufficient buffer, and caller-driven timeout failures, but it is not evidence of retransmission, a live TCP connection, sockets/PAL exports, virtio-net packet I/O, hosted .NET 10, ASP.NET Minimal API, or Kestrel.

### Layer 3 - PAL Socket Tests

Scope: tests for the `OSHost_*` socket behavior documented in `docs/pal.md`, backed by mock TCP first and by the real TCP stack once available. These tests prove the hosted runtime boundary, not HTTP semantics.

Required tests:

- `OSHost_SocketCreate` creates IPv4 TCP stream sockets and rejects unsupported domains, types, protocols, and malformed options.
- `OSHost_SocketBind` accepts manifest-approved local endpoints and rejects duplicate binds, unavailable addresses, invalid ports, and endpoints blocked by manifest policy.
- `OSHost_SocketListen` applies a documented backlog maximum and rejects listen calls on unbound or already-connected sockets.
- `OSHost_SocketAccept` returns accepted socket handles, peer/local addresses, bounded wait behavior, and `OSHOST_STATUS_WOULD_BLOCK` or `OSHOST_STATUS_TIMED_OUT` when appropriate.
- Concurrent clients can connect through the same listener without corrupting accepted handles, peer addresses, or receive buffers.
- `OSHost_SocketReceive` supports partial reads, split request headers, peer close, timeout, cancellation by close, and bounded waits without losing unread bytes.
- `OSHost_SocketSend` supports partial writes, backpressure, timeout, cancellation by close, large response bodies, and deterministic byte counts.
- `OSHost_SocketShutdown` and `OSHost_SocketClose` produce deterministic FIN/RST behavior and unblock pending accept/read/write calls.
- `OSHost_SocketPoll` reports readable, writable, hangup, error, and accepted-connection events without busy waiting.
- Socket options required by Kestrel v1, including `NoDelay`, `ReuseAddress`, receive timeout, send timeout, and keep-alive, either work or return `OSHOST_STATUS_NOT_SUPPORTED` with documented impact.

Required markers:

```text
TEST_START pal.socket.<name>
TEST_PASS pal.socket.<name>
TEST_FAIL pal.socket.<name> reason=<code> status=<OSHostStatus>
TEST_START pal.socket.listen
TEST_PASS pal.socket.listen
```

Expected failure diagnostics must include `OSHostStatus`, PAL function name, handle id if available, local endpoint, remote endpoint, timeout mode, requested byte count, transferred byte count, and whether cancellation or peer close was observed. PAL tests must never throw exceptions across the ABI and must not pass managed references across the boundary.

### Layer 4 - App-Level Kestrel HTTP Tests

Scope: in-guest hosted .NET 10 and ASP.NET Minimal API/Kestrel tests. These are valid only after the hosted runtime, package/config file access, timers, thread/sync, PAL sockets, TCP, and runner HTTP verification exist.

Required tests:

- Bind/listen/accept: Kestrel binds the manifest-configured HTTP endpoint and an external runner connects to it.
- Basic request: the `kestrel.smoke.package.v0` fixture binds `http://0.0.0.0:8080`, and an external runner request to `GET /healthz` returns status `200`, expected `text/plain` response metadata, body `OS_MINIMAL_API_OK`, body byte count `17`, and body SHA-256 `87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d`.
- Config propagation: the `GET /healthz` body must come from the manifest environment/config key `OS_SMOKE_EXPECTED_BODY=OS_MINIMAL_API_OK` or an equivalent documented PAL config value populated from that key.
- Concurrent clients: at least two simultaneous clients receive valid responses without handle reuse, corrupted response bodies, or scheduler deadlock.
- Partial request headers: the runner sends the request line and headers across multiple TCP writes; Kestrel still returns the expected response.
- Partial response writes: a response requiring multiple sends is delivered in order with the expected content length or deterministic body hash.
- Large response body: a response larger than one TCP MSS and larger than one PAL send buffer is delivered correctly.
- Connection close: client-close, server-close, and graceful shutdown paths do not leak accepted sockets or block the listener.
- Timeout: incomplete request headers eventually fail with a deterministic timeout diagnostic rather than hanging forever.
- Cancellation: an app endpoint that observes cancellation can terminate when the client disconnects or the runner cancels the request.
- Backpressure: a slow-reading client causes bounded send waits or partial writes without corrupting other connections.
- Malformed request handling: malformed HTTP bytes produce Kestrel-level `400` response or close behavior; the kernel, TCP stack, and PAL must not panic.

Required serial markers:

```text
KESTREL_PACKAGE_CONTRACT kestrel.smoke.package.v0
TEST_START kestrel.socket.compat
TEST_PASS kestrel.socket.compat
TEST_FAIL kestrel.socket.compat reason=<code>
TEST_START kestrel.smoke
HTTP_PROBE_PASS status=200 body_bytes=17 body_sha256=87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d
TEST_PASS kestrel.smoke
TEST_FAIL kestrel.smoke reason=<code>
```

The runner must fail the smoke if any required serial marker is missing, if the guest panics, if the TCP connection cannot be established, if HTTP status/body/header validation fails, if the response is truncated, or if the timeout expires. Runner diagnostics must save the serial log, endpoint attempted, request bytes mode, response status, response byte count, body hash for large responses, elapsed time, and the first failure marker. Host loopback requests against the fixture and dry-run `HTTP_PROBE_*` text are not networking evidence for this layer.

### Behavior-to-Component Checklist

| Kestrel behavior | Packet/TCP requirement | PAL requirement | Runtime/app requirement | Runner evidence |
| --- | --- | --- | --- | --- |
| Bind/listen/accept | Passive open, listener table, backlog, SYN/SYN-ACK/ACK | `Create`, `Bind`, `Listen`, `Accept`, address queries | Kestrel binds manifest endpoint | External connection succeeds and `pal.socket.listen` passes |
| Concurrent clients | Multiple TCP control blocks, backlog draining, independent sequence spaces | Accepted handles are unique and waitable | Thread pool can process overlapping requests | Two clients receive independent valid responses |
| Partial reads | Stream reassembly and unread byte retention | `Receive` returns byte counts and preserves remaining data | Kestrel parser handles fragmented headers/body | Split-header test returns expected response |
| Partial writes | Segmentation, retransmission, send queue accounting | `Send` returns transferred byte count and `WOULD_BLOCK`/timeout | Kestrel flush handles short writes | Large response body validates by length/hash |
| Connection close | FIN/RST state transitions and cleanup timers | `Shutdown`, `Close`, hangup/error poll events | Request lifetime ends without leaked work | Close tests complete and listener remains usable |
| Timeouts | TCP retransmission and idle timers | Timeout structs, bounded waits, poll timeout | Kestrel request/header timeout can fire | Runner sees deterministic timeout marker |
| Cancellation | Wakes pending I/O on close/interruption | `INTERRUPTED`, close-unblocks-wait behavior | Request cancellation token is observed | Cancel test returns expected diagnostic |
| Backpressure | Receive window and send queue limits | Partial send or `WOULD_BLOCK` without busy wait | Kestrel does not block all clients | Slow-client test does not starve healthy client |
| Malformed request | TCP delivers bytes without kernel interpretation | Socket API surfaces bytes/status only | Kestrel returns 400 or closes | Runner records HTTP 400/close, no kernel panic |

### Out Of Scope

TLS, HTTPS, certificates, ALPN, HTTP/2, HTTP/3, QUIC, WebSockets, and TLS offload are out of scope until implemented and tested by separate issues. Do not claim TLS or HTTPS support from plain HTTP Kestrel socket compatibility evidence.

## Next Steps

1. Wire `TcpReceiveDispatcher` into future TCP control-block/socket I/O paths after live virtio-net RX and connection-table ownership exist, and continue checksum integration only when each surrounding packet path is ready to enforce checksums.
2. Wire `IcmpReceiveDispatcher` into a future diagnostic ping state machine after live virtio-net RX/TX, ARP routing, timers, and packet reply generation exist.
3. Wire future PCI enumeration to snapshot config bytes, feed them through `VirtioPciCapabilityListParser` and `VirtioPciDeviceDiscovery`, then feed live feature bits and queue counts into `VirtioDeviceInitializationPlan`.
4. Wire live virtio-net feature negotiation into `VirtioDeviceInitializationPlan`, parse device configuration with `VirtioNetDeviceConfig`, then feed queue metadata into `VirtioNetQueuePlan`.
5. Replace the host-tested `VirtioPciBarRegisterWindowLiveAccessorBoundary` fake accessor with an owned live BAR-backed volatile register accessor, plus MMIO ownership rules, real memory-ordering primitive, and DMA ownership prerequisites needed to replace the current fake RX/TX readiness and completion/slab-selection/classification/dispatch boundaries.
6. Feed future used RX buffers through `VirtioNetReceiveRegisterWindowDispatchPlanner` before ARP/IPv4 body parsers, keeping offload and mergeable-buffer feature negotiation explicit.
7. Keep TX completion/reclaim gated on executed notification provenance while replacing fake sink/register-window notification with live BAR-backed notification, completion, and reclaim evidence.
8. Wire ARP neighbor resolution and cache lookups over future virtio-net packet I/O.
9. Wire the host-tested DHCP client state machine to future UDP receive/send dispatch over virtio-net packet I/O with retry timers.
10. Wire the host-tested DNS resolver transaction to future UDP receive/send dispatch over virtio-net packet I/O with retry timers and cache policy.
11. Add the Layer 1 packet tests for TCP header encode/decode.
12. Expand the host-only TCP state-machine harness beyond active/passive handshake frame planners, caller-owned listener backlog, established stream, and close/reset primitives into TCP control blocks, retransmission, timers, windows, and backpressure before wiring real virtio-net RX/TX.
13. Wire the host-tested HTTP TCP GET planner to future TCP/socket I/O once connection lifecycle, segmentation, retransmission, timers, and stream ownership are represented by a broader TCP control-block contract.
14. Add PAL socket mock tests that exercise the `OSHost_*` behavior without Kestrel.
15. Add in-guest Kestrel socket compatibility smoke only after hosted .NET, PAL, TCP, timers, threading, and runner HTTP verification exist.
