# PAL Design

Current implementation order is the NativeAOT-first decision in
`docs/workload-profiles.md`: a separate Linux x64 syscall adapter over
common C# services. Preserve the OSHost_* ABI described here for existing
contracts and future ports; CoreCLR is no longer a prerequisite for Kestrel.

## Current Status

Design baseline only.

## Implemented Behavior

The future first-party PAL boundary uses `OSHost_*` names and will not allow exceptions, managed references, or C++ objects across the boundary. The naming decision is recorded in `docs/adr-pal-abi-naming.md`.

The modern SharpOS reference names this boundary `SharpOSHost_*`. Adapted reference code must rename those imports and exports to `OSHost_*` before becoming first-party OS code. A `SharpOSHost_*` symbol may be added only as a documented compatibility alias, not as the canonical ABI name.

`src/OS.Kernel/Pal/OSHostAbi.cs` contains the first declaration layer for `OSHostStatus` and POD structs for common handles/byte spans, memory, time, log, file/block, package path, config/environment query data, and Kestrel-critical socket/timer/thread/wait data. `src/OS.Kernel/Pal/OSHostSocketAbi.cs` now also carries Kestrel-oriented socket option value contracts. Host tests verify sequential layout, public readonly fields, enum values, no managed references, stable sizes, selected field offsets, supported socket options, unsupported option rejection, and mock manifest config/environment query behavior.

`src/OS.Kernel/Pal/OSHostStatusNameFormatter.cs` provides the first host-tested caller-buffer formatter for canonical `OSHOST_STATUS_*` ASCII names. It accepts an `OSHostStatus`, writes into caller-owned storage, reports required byte counts, rejects unknown status values and short buffers through `OSHostStatus`, and keeps default result values from counting as success evidence. It is diagnostic/status-contract groundwork only and does not export a PAL symbol.

`src/OS.Kernel/Pal/OSHostStatusCategory.cs` provides a host-tested caller-buffer diagnostic categorization helper for `OSHostStatus` values. It maps statuses into stable `Success`, `CallerError`, `ResourceError`, `Unsupported`, `RuntimeUnavailable`, and `Unknown` buckets, formats canonical `OSHOST_STATUS_CATEGORY_*` names into caller-owned buffers, and classifies unrecognized status values as `Unknown` without allocating return storage. It is diagnostic/status-contract groundwork only and does not export a PAL symbol.

`src/OS.Kernel/Pal/OSHostStatusContract.cs` provides a host-tested status-domain validator for config, environment, time, console/log, memory, package-file, and socket PAL surfaces. It maps each domain to a deterministic allowed `OSHostStatus` bit set, returns status-coded POD result structs for allowed, disallowed, unknown-domain, and unknown-status cases, and formats canonical `OSHOST_ABI_DOMAIN_*` names into caller-owned buffers. It is diagnostic/status-contract groundwork only and does not export a PAL symbol or allocate ABI-owned return storage.

`src/OS.Kernel/Platform/PlatformFirmwareServiceDenialMarkerAdapters.cs`
bridges guarded platform result structs for console, memory-map,
configuration-table, timer, random, physical-memory, and framebuffer calls into
the existing caller-owned `UEFI_SERVICE_DENIED operation=<token> mode=<mode>`
diagnostic formatter. It is UEFI-gone diagnostic groundwork only and does not
export PAL symbols, call firmware, or change boot marker order.

Example first-party symbols:

- `OSHost_AllocPages`
- `OSHost_FreePages`
- `OSHost_QueryMemory`
- `OSHost_GetMonotonicTicks`
- `OSHost_WriteLogBytes`
- `OSHost_ReadBlock`

## ABI Ground Rules

The PAL ABI is a hosted-runtime boundary, not a managed object boundary.

- Every function returns `OSHostStatus`.
- Every handle is an opaque unsigned 64-bit value; `0` is invalid.
- Every path, config key, environment key, and log payload is a UTF-8 pointer plus byte length.
- Every output value is returned through an unmanaged out pointer or caller-owned buffer.
- Every struct is POD with explicit scalar fields, offsets, lengths, flags, handles, or fixed bytes.
- No managed references, exceptions, C++ objects, WinAPI handles, POSIX file descriptors, or host-owned movable buffers cross the boundary.
- Compatibility aliases using `SharpOSHost_*` require a documented adapter and must not become first-party names.

Initial stable status values:

- `OSHostStatus.Ok = 0`
- `OSHostStatus.InvalidArgument = 1`
- `OSHostStatus.NotSupported = 2`
- `OSHostStatus.NotFound = 3`
- `OSHostStatus.AccessDenied = 4`
- `OSHostStatus.BufferTooSmall = 5`
- `OSHostStatus.OutOfMemory = 6`
- `OSHostStatus.WouldBlock = 7`
- `OSHostStatus.TimedOut = 8`
- `OSHostStatus.Interrupted = 9`
- `OSHostStatus.EndOfFile = 10`
- `OSHostStatus.ConnectionReset = 11`
- `OSHostStatus.ConnectionRefused = 12`
- `OSHostStatus.AddressInUse = 13`
- `OSHostStatus.AddressNotAvailable = 14`
- `OSHostStatus.NetworkDown = 15`
- `OSHostStatus.TryAgain = 16`
- `OSHostStatus.IoError = 17`
- `OSHostStatus.InternalError = 18`
- `OSHostStatus.Closed = 19`

Initial diagnostic categories:

- `OSHostStatusCategory.Success = 0`: `Ok`
- `OSHostStatusCategory.CallerError = 1`: invalid caller-owned pointers, lengths, handles, buffers, policy, missing requested data, address conflicts, unavailable caller-selected addresses, and closed-handle usage.
- `OSHostStatusCategory.ResourceError = 2`: memory exhaustion, EOF, connection reset/refused, and device/package I/O failure.
- `OSHostStatusCategory.Unsupported = 3`: `NotSupported`.
- `OSHostStatusCategory.RuntimeUnavailable = 4`: would-block, timeout, interruption, network-down, and transient retry conditions.
- `OSHostStatusCategory.Unknown = 5`: `InternalError` and unrecognized status values.

## Kestrel-Critical Contract

The first ASP.NET Minimal API/Kestrel proof depends on these `OSHost_*` domains. This file defines contract names and behavior targets only; no symbols are currently implemented.

Current C# structs for this design are declaration/layout only:

- `OSHostSocketAddress`
- `OSHostSocketCreateOptions`
- `OSHostSocketIoRequest`
- `OSHostSocketPollEntry`
- `OSHostSocketBindRequest`
- `OSHostSocketListenRequest`
- `OSHostSocketAcceptRequest`
- `OSHostSocketAcceptOutput`
- `OSHostSocketConnectRequest`
- `OSHostSocketShutdownRequest`
- `OSHostSocketOptionRequest`
- `OSHostSocketAddressQueryRequest`
- `OSHostRandomBytesRequest`
- `OSHostTimeout`
- `OSHostTimerOptions`
- `OSHostThreadStart`
- `OSHostThreadCreateOptions`
- `OSHostEventCreateOptions`
- `OSHostWaitOneRequest`
- `OSHostWaitManyRequest`
- `OSHostWaitResult`

These structs do not export socket, timer, thread, or wait behavior. No hosted runtime or Kestrel path consumes them yet.

Host-only mock tables and facades now cover early status translation for several contract slices. The status diagnostics slice includes `OSHostStatusNameFormatter`, which formats canonical status names into caller-owned buffers without allocating return storage or accepting unknown status values, `OSHostStatusCategoryFormatter`, which maps statuses to stable diagnostic categories and formats canonical category names into caller-owned buffers, and `OSHostStatusContract`, which validates config/environment/time/console-log/memory/package-file/socket status sets and formats canonical domain names through POD-only result structs. The memory slice includes `OSHostMemoryTable` plus `OSHostMemoryFacade`, which models future `OSHost_AllocPages`, `OSHost_FreePages`, `OSHost_ProtectPages`, and `OSHost_QueryMemory` calls as in-process methods only. The table validates page size, alignment, page-count arithmetic, finite mock capacity, stale handles, exact active ranges, default/zero allocation-result rejection, and W^X/protection transitions through `OSHostMemoryProtectionPlan`; it does not allocate real pages, change page tables, export unmanaged symbols, or feed a GC. The socket slice includes `OSHostSocketTable` plus `OSHostSocketFacade`, which model future socket create/bind/listen/accept/connect/send/receive/poll/shutdown/options/address-query status translation as in-process methods only. The table rejects default/zero socket-create, accept, and poll results as success evidence, records host-only outbound remote endpoint metadata only after a socket has a pre-bound local endpoint, and the operation-result ABI helper rejects default/zero I/O result success while preserving zero-byte success on a valid socket handle. These remain no-export/no-live-socket boundaries. The timer slice includes `OSHostTimerTable` plus `OSHostTimerFacade`, which models future `OSHost_TimeGetMonotonicTicks`, `OSHost_TimerCreate`, `OSHost_TimerArm`, `OSHost_TimerCancel`, `OSHost_TimerClose`, and timer-poll calls as in-process methods only. The table rejects default/zero timeout, create, and poll results as success evidence. The facade validates null-shaped output pointers, invalid handles, invalid timer options, close/cancel semantics, and capacity exhaustion through `OSHostStatus`; it does not export unmanaged symbols or schedule real wakes. The wait/event slice includes `OSHostWaitTable` plus `OSHostWaitFacade`, which models future `OSHost_EventCreate`, `OSHost_EventSet`, `OSHost_EventReset`, `OSHost_EventClose`, `OSHost_WaitOne`, and `OSHost_WaitMany` status translation over an injected table. The table exposes early single-thread capability metadata, rejects malformed timeout structs as invalid arguments, reports valid non-immediate waits as unsupported, and rejects default/zero event-create and capability results as success evidence. The facade validates event options, caller-owned wait-result outputs, wait-many handle-span metadata, null tables, invalid/stale handles, immediate no-signal timeout behavior, unsupported wait-all flags, unsupported non-immediate timeouts, and capacity exhaustion without exporting unmanaged symbols or blocking scheduler threads. The package/config slice includes `OSHostPackageFileTable` plus `OSHostPackageFileFacade` and `OSHostConfigEnvironmentTable` plus `OSHostConfigEnvironmentFacade`, which model future package open/read/info/close/enumeration and config/environment lookup/enumeration status translation over already-loaded bytes only. These facades validate caller-owned buffers, request-shape metadata, stale handles, unsafe paths, missing files/keys, buffer sizing, and deterministic table exhaustion without exporting unmanaged symbols, reading guest disks, or feeding hosted .NET configuration. The entropy slice includes `OSHostRandomTable` plus `OSHostRandomFacade`, which model future `OSHost_GetRandomBytes` status translation over caller-owned destination buffers. The table rejects empty, flagged, oversized, unavailable-entropy, and default/zero result paths without exporting unmanaged symbols, calling firmware RNG, seeding a post-firmware entropy pool, or providing cryptographic randomness.

`OSHostConfigEnvironmentTable.Create` is the fixed-capacity creation boundary for the host-only config/environment table. It accepts caller-provided key/value entries, rejects config or environment counts above the table capacity with `OSHostStatus.OutOfMemory`, and does not export PAL symbols or allocate ABI-owned return buffers. Manifest listen-port bytes in the table are formatted through the shared no-runtime `OS.Std.Text.AsciiDecimalFormatter` and `AsciiTextBuilder` helpers, guarded by `TEST_PASS pal.config table port formatter uses shared ascii text builder`.

### Memory

Required calls:

- `OSHost_AllocPages`
- `OSHost_FreePages`
- `OSHost_ProtectPages`
- `OSHost_QueryMemory`

Required first behavior:

- Page-size and page-alignment validation for every allocation and protection range.
- Opaque generation-based handles for allocated page ranges.
- Deterministic capacity exhaustion for handle slots and mock page ranges.
- Status-coded rejection for stale handles, missing ranges, partial ranges where the current host model requires exact range identity, arithmetic overflow, unsupported protection states, RWX requests, and invalid write-after-execute transitions.
- One-way write-then-execute transition planning through `OSHostMemoryProtectionPlan`.

Current host-only evidence:

- `OSHostMemoryTable` models aligned mock page allocations, frees, query-by-address, exact-range protection changes, handle reuse with generation increments, stale-handle rejection, default/zero allocation-result rejection, deterministic handle/range exhaustion, and W^X policy validation.
- `OSHostMemoryFacade` models future exported memory call status translation over that table, including null output pointer rejection and null table rejection.
- No exported `OSHost_*` memory symbol, real page allocator, page table mutation, TLB shootdown, instruction-cache flush, GC heap integration, hosted runtime allocation, or Kestrel behavior exists yet.

### Sockets

Required calls:

- `OSHost_SocketCreate`
- `OSHost_SocketBind`
- `OSHost_SocketListen`
- `OSHost_SocketAccept`
- `OSHost_SocketConnect`
- `OSHost_SocketSend`
- `OSHost_SocketReceive`
- `OSHost_SocketShutdown`
- `OSHost_SocketClose`
- `OSHost_SocketSetOption`
- `OSHost_SocketGetLocalAddress`
- `OSHost_SocketGetRemoteAddress`
- `OSHost_SocketPoll`

Required first behavior:

- IPv4 TCP stream sockets for inbound Kestrel.
- Bind and listen only on manifest-approved addresses and ports.
- Listener access is denied by default; the manifest/supervisor grants the only authority to create a listening endpoint.
- Accept, send, and receive with caller-owned buffers.
- Host-only connect metadata for already-bound outbound IPv4 TCP stream sockets; this is not required for the first inbound Kestrel smoke and is not DNS/routing/TCP handshake behavior.
- Store/query Kestrel-oriented `NoDelay`, `ReuseAddress`, receive timeout, send timeout, and keep-alive option contracts.
- Query bound local endpoints and accepted remote peer endpoints.
- Reject duplicate binds unless the new socket and every conflicting bound/listening socket have `ReuseAddress` enabled.
- Bounded blocking, nonblocking, timeout, and cancellation semantics.
- Status-coded errors for invalid arguments, unsupported operations, out-of-resources, address in use, address unavailable, would-block, timeout, interrupted waits, reset, refused, closed handles, network down, and unsupported options.

Function-level contract:

| Function | Input POD | Output POD or buffer | Blocking/timeout rule |
| --- | --- | --- | --- |
| `OSHost_SocketCreate` | `OSHostSocketCreateOptions` | Caller-owned `OSHostHandle*` | Never blocks. |
| `OSHost_SocketBind` | `OSHostSocketBindRequest` | None | Never blocks; manifest policy must approve the endpoint. |
| `OSHost_SocketListen` | `OSHostSocketListenRequest` | None | Never blocks; backlog is bounded. |
| `OSHost_SocketAccept` | `OSHostSocketAcceptRequest` | Caller-owned `OSHostSocketAcceptOutput*` | Immediate timeout returns `WOULD_BLOCK`; bounded timeout returns `TIMED_OUT`; close/cancel returns `INTERRUPTED` or `CLOSED`. |
| `OSHost_SocketConnect` | `OSHostSocketConnectRequest` | None | Not required for first inbound Kestrel smoke. Current host mock support requires a pre-bound local IPv4 endpoint, records remote endpoint metadata, and performs no DNS, routing, TCP handshake, packet I/O, or real timeout wait. |
| `OSHost_SocketSend` | `OSHostSocketIoRequest` | Caller-owned transferred-byte count | May return partial byte counts; nonblocking pressure returns `WOULD_BLOCK`. |
| `OSHost_SocketReceive` | `OSHostSocketIoRequest` | Caller-owned transferred-byte count | May return partial byte counts; peer graceful close returns `END_OF_FILE`; reset returns `CONNECTION_RESET`. |
| `OSHost_SocketShutdown` | `OSHostSocketShutdownRequest` | None | Wakes pending I/O; receive/send/both are explicit flags. |
| `OSHost_SocketClose` | `OSHostHandle` | None | Releases the handle and wakes waiters; stale unknown handles are invalid. |
| `OSHost_SocketSetOption` | `OSHostSocketOptionRequest` | None | Never blocks; unsupported options return `NOT_SUPPORTED` or `INVALID_ARGUMENT`. |
| `OSHost_SocketGetLocalAddress` | `OSHostSocketAddressQueryRequest` | Caller-owned `OSHostSocketAddress*` | Never blocks. |
| `OSHost_SocketGetRemoteAddress` | `OSHostSocketAddressQueryRequest` | Caller-owned `OSHostSocketAddress*` | Never blocks; unconnected sockets return `NOT_FOUND` or `INVALID_ARGUMENT`. |
| `OSHost_SocketPoll` | Caller-owned `OSHostSocketPollEntry[]`, count, `OSHostTimeout` | Mutates caller-owned poll entries only | Must not busy-wait or block forever. |

### Timers

Required calls:

- `OSHost_TimeGetMonotonicTicks`
- `OSHost_TimeGetTickFrequency`
- `OSHost_TimeGetWallClockUnixNanoseconds`
- `OSHost_TimerCreate`
- `OSHost_TimerArm`
- `OSHost_TimerCancel`
- `OSHost_TimerClose`
- `OSHost_ThreadSleepUntil`

Required first behavior:

- Stable monotonic ticks for timeout and cancellation logic.
- Explicit tick frequency.
- One-shot timers that can wake PAL wait handles.
- Wall-clock time returns `OSHOST_STATUS_NOT_SUPPORTED` until RTC or cloud metadata time is trustworthy.

Current host-only evidence:

- `OSHostTimerTable` models deterministic monotonic snapshots, one-shot and periodic timers, cancellation, close, timeout comparison, invalid/stale handles, default/zero timeout/create/poll result rejection, and capacity exhaustion.
- `OSHostTimerFacade` models future exported timer call status translation over that table, including null output pointer rejection and invalid handle translation.
- No exported `OSHost_*` timer symbol, wait-handle completion, scheduler wake, APIC/HPET/TSC integration, sleep, or guest behavior exists yet.

### Threads And Sync

Required calls:

- `OSHost_ThreadCreate`
- `OSHost_ThreadExit`
- `OSHost_ThreadJoin`
- `OSHost_ThreadYield`
- `OSHost_ThreadGetCurrentId`
- `OSHost_WaitOne`
- `OSHost_WaitMany`
- `OSHost_EventCreate`
- `OSHost_EventSet`
- `OSHost_EventReset`
- `OSHost_EventClose`
- `OSHost_SemaphoreCreate`
- `OSHost_SemaphoreRelease`
- `OSHost_SemaphoreClose`
- `OSHost_MutexCreate`
- `OSHost_MutexLock`
- `OSHost_MutexUnlock`
- `OSHost_MutexClose`
- `OSHost_MemoryBarrier`

Required first behavior:

- Hosted runtime can run worker threads, join them, and wait without deadlocking the kernel.
- Events support manual-reset and auto-reset modes.
- Semaphores detect count overflow and report status.
- Wait-many has a documented maximum handle count.
- Memory ordering is explicit enough for the hosted runtime and thread pool.

Current host-only evidence:

- `OSHostThreadTable` models deterministic thread-create option validation, opaque thread handles, thread ids, current-thread id, yield status, early single-thread capability metadata, immediate-only sleep completion, unsupported blocking sleep status, immediate join-after-exit, exit-code preservation, handle reuse, default/zero create/join/id/capability/sleep result rejection, and capacity exhaustion.
- `OSHostWaitTable` models deterministic manual-reset and auto-reset event creation, set/reset, immediate wait-one, immediate wait-any, cancellation/interruption, close, invalid/stale handles, malformed timeout rejection, unsupported wait-all, unsupported non-immediate timeouts, early single-thread capability metadata, default/zero event-create and capability-result rejection, and capacity exhaustion.
- `OSHostWaitFacade` models future exported wait/event call status translation over that table, including null table rejection, null result output rejection, ABI-shaped event option validation, wait-many handle-span metadata validation, invalid/stale handle translation, unsupported wait-all/timeout translation, and deterministic exhaustion.
- No exported `OSHost_*` thread/wait/event symbol, real stack allocation, entrypoint invocation, context switch, blocking wait, scheduler park/wake integration, timer completion integration, TLS/thread-static support, `Thread`, `Monitor`, ThreadPool, hosted runtime, or Kestrel synchronization behavior exists yet.

### Package Files And Config

Required calls:

- `OSHost_OpenPackageFile`
- `OSHost_ReadFileAt`
- `OSHost_GetFileInfo`
- `OSHost_CloseFile`
- `OSHost_EnumeratePackage`
- `OSHost_GetConfigValue`
- `OSHost_EnumerateConfig`
- `OSHost_GetEnvironmentValue`
- `OSHost_EnumerateEnvironment`
- `OSHost_WriteLogBytes`

Required first behavior:

- Read-only package files for app assemblies, dependency files, appsettings, and optional static content.
- Relative package paths only; reject absolute paths, drive prefixes, device paths, and `..` traversal.
- Offset-based reads, no shared mutable seek position.
- Manifest-provided config and environment values copied into caller buffers.
- Buffer sizing uses `OSHOST_STATUS_BUFFER_TOO_SMALL`.
- Environment enumeration returns deterministic index-based key/value metadata with required byte counts.
- Hosted stdout, stderr, and ASP.NET logs route through `OSHost_WriteLogBytes`.

### Entropy

Required calls:

- `OSHost_GetRandomBytes`

Required first behavior:

- Fill caller-owned byte buffers only; the OS must not allocate return buffers for entropy.
- Reject null/zero destinations, unsupported flags, and malformed request metadata with `OSHOST_STATUS_INVALID_ARGUMENT`.
- Report temporarily unavailable entropy with `OSHOST_STATUS_TRY_AGAIN`.
- Keep the mock deterministic for host tests while documenting that it is not cryptographic randomness.

Current host-only evidence:

- `OSHostRandomBytesRequest` is a POD declaration for future random-byte calls.
- `OSHostRandomTable` fills caller-owned spans from a deterministic seed, advances request sequence metadata, rejects oversized requests, reports unavailable entropy, and rejects default/zero random results as success evidence.
- `OSHostRandomFacade` validates request shape and null table wiring before delegating status translation to the table.
- No exported `OSHost_GetRandomBytes` symbol, firmware RNG call, post-firmware entropy pool, cryptographic RNG, hosted runtime randomization, TLS, certificate, or Kestrel behavior exists yet.

## Kestrel Evidence Boundary

`TEST_PASS kestrel.smoke` is valid only after the guest OS launches the packaged ASP.NET Minimal API app through the documented .NET 11 NativeAOT-first route or a separately qualified future hosted runtime, Kestrel binds the configured endpoint, an external request receives the expected HTTP response over the OS network stack, and serial output contains the corresponding pass marker. The NativeAOT-first route uses the restricted Linux syscall adapter; CoreCLR and exported `OSHost_*` PAL functions are not prerequisites for that route. The complete profile-specific evidence and isolation requirements remain in `docs/completion-gates.md`.

Before that point, these are not Kestrel support evidence:

- Host-only Minimal API build or health check.
- Manifest schema validation.
- Native-tier TinyHttpServer or HTTP client demos.
- PAL declarations without guest behavior.
- Socket or TCP tests that do not launch Kestrel, including host-only connect metadata tests.

## Unsupported Behavior

No exported PAL functions are implemented yet. Memory, socket, timer, thread/sync, package file/config, entropy, hosted .NET 11, and Kestrel behavior are contract-only. The status-name, status-category, and status-domain contract helpers are host-tested caller-buffer helpers only and are not exported PAL diagnostic functions. The memory, socket, timer, wait, package, config/environment, app-log, and random facades are host-only in-process adapters over mock tables; they are not unmanaged exports, do not mutate page tables, do not perform DNS/routing/TCP handshakes/packet I/O, do not block or wake scheduler threads, do not read guest disks, do not provide cryptographic entropy, and are not consumed by a hosted runtime.

## Tests

No exported PAL implementation tests exist. Host-only mock tests cover early memory, socket, package-file, and entropy contract behavior before the exported boundary exists.

Host layout tests now cover the first declaration layer, including the socket/timer/thread/wait POD structs:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Relevant markers:

- `TEST_PASS pal abi status code values are stable`
- `TEST_PASS pal abi structs use sequential layout`
- `TEST_PASS pal abi fields are public readonly`
- `TEST_PASS pal abi structs contain no managed references`
- `TEST_PASS pal abi struct sizes are stable`
- `TEST_PASS pal abi field offsets are stable`
- `TEST_PASS pal.status_names formats all canonical statuses`
- `TEST_PASS pal.status_names reports required bytes`
- `TEST_PASS pal.status_names rejects short buffer`
- `TEST_PASS pal.status_names rejects unknown status`
- `TEST_PASS pal.status_names preserves caller buffer on failure`
- `TEST_PASS pal.status_names default result is not success`
- `TEST_PASS pal.status_categories maps canonical statuses`
- `TEST_PASS pal.status_categories formats category names`
- `TEST_PASS pal.status_categories reports required bytes`
- `TEST_PASS pal.status_categories rejects short buffer`
- `TEST_PASS pal.status_categories maps unknown status`
- `TEST_PASS pal.status_categories default result is not success`
- `TEST_PASS pal.status_contract validates config and environment statuses`
- `TEST_PASS pal.status_contract validates time and console statuses`
- `TEST_PASS pal.status_contract formats domain names`
- `TEST_PASS pal.status_contract rejects malformed inputs without throwing`
- `TEST_PASS pal.status_contract result layouts contain no managed references`
- `TEST_PASS pal.status_contract default results are not success`
- `TEST_PASS platform.firmware denial adapters format guarded results`
- `TEST_PASS platform.firmware denial adapters query required bytes`
- `TEST_PASS platform.firmware denial adapters reject non-denial results`
- `TEST_PASS platform.firmware denial adapters result layout contains no managed references`
- `TEST_PASS pal.socket options reject unsupported values`
- `TEST_PASS pal.socket options store kestrel flags`
- `TEST_PASS pal.socket endpoints report bound local address`
- `TEST_PASS pal.socket endpoints report accepted peer address`
- `TEST_PASS pal.socket reuse address policy is deterministic`
- `TEST_PASS pal.socket table create default result is not success`
- `TEST_PASS pal.socket accept default result is not success`
- `TEST_PASS pal.socket connect establishes outbound endpoint`
- `TEST_PASS pal.socket connect rejects invalid state`
- `TEST_PASS pal.socket facade connects with remote endpoint`
- `TEST_PASS pal.socket abi default operation result is not success`
- `TEST_PASS pal.socket poll default result is not success`
- `TEST_PASS pal.timer facade returns monotonic snapshot`
- `TEST_PASS pal.timer facade creates and arms one shot`
- `TEST_PASS pal.timer facade cancels and closes handles`
- `TEST_PASS pal.timer facade translates invalid handles`
- `TEST_PASS pal.timer facade reports exhaustion`
- `TEST_PASS pal.timer default results are not success`
- `TEST_PASS pal.thread table creates joinable thread`
- `TEST_PASS pal.thread table rejects invalid create options`
- `TEST_PASS pal.thread table reports exhaustion and reuse`
- `TEST_PASS pal.thread table reports early mode capabilities`
- `TEST_PASS pal.thread table current id and yield are stable`
- `TEST_PASS pal.thread table sleep reports unsupported blocking waits`
- `TEST_PASS pal.thread table rejects invalid timeout and handles`
- `TEST_PASS pal.thread table default results are not success`
- `TEST_PASS pal.wait reports early mode capabilities`
- `TEST_PASS pal.wait rejects invalid timeout and unsupported waits`
- `TEST_PASS pal.wait facade creates manual and auto events`
- `TEST_PASS pal.wait facade set reset close translate statuses`
- `TEST_PASS pal.wait facade wait one reports signal and timeout`
- `TEST_PASS pal.wait facade wait many reports first signaled`
- `TEST_PASS pal.wait facade translates invalid and stale handles`
- `TEST_PASS pal.wait facade rejects unsupported wait all and timeout`
- `TEST_PASS pal.wait facade rejects null table`
- `TEST_PASS pal.wait facade reports exhaustion`
- `TEST_PASS pal.wait facade layouts are stable`
- `TEST_PASS pal.wait default results are not success`
- `TEST_PASS pal.config returns manifest config value`
- `TEST_PASS pal.config reports buffer too small`
- `TEST_PASS pal.env returns manifest environment value`
- `TEST_PASS pal.env enumerates environment entries`
- `TEST_PASS pal.env rejects invalid queries`
- `TEST_PASS pal.config table reports deterministic capacity`
- `TEST_PASS pal.config table port formatter uses shared ascii text builder`
- `TEST_PASS pal.random table fills caller buffer`
- `TEST_PASS pal.random table advances deterministic stream`
- `TEST_PASS pal.random table rejects invalid requests`
- `TEST_PASS pal.random table reports unavailable entropy`
- `TEST_PASS pal.random table default result is not success`
- `TEST_PASS pal.random facade fills caller buffer`
- `TEST_PASS pal.random facade rejects malformed request`
- `TEST_PASS pal.random facade reports unavailable table`
- `TEST_PASS pal.random facade propagates entropy status`

Documentation-only verification for the Kestrel-critical contract:

```powershell
rg -n "OSHost_SocketCreate|OSHost_TimerCreate|OSHost_ThreadCreate|OSHost_OpenPackageFile|TEST_PASS kestrel.smoke" .\docs\pal.md .\work\PAL\pal-design.md .\docs\kestrel.md
git diff --check -- .\docs\pal.md .\work\PAL\pal-design.md .\docs\kestrel.md .\docs\completion-gates.md .\done\step020.md
```

## Known Limitations

Hosted CoreCLR is not part of the native-tier OS milestone. Kestrel is a required CloudReady product goal, but this contract does not implement hosted runtime, sockets, timers, scheduler, file/package reads, or ASP.NET execution.

## Next Steps

1. Add constants for socket domains/types/protocols, poll event flags, timeout kinds, timer flags, and wait result kinds before any exported implementation uses the declaration structs.
2. Add `OSHOST_STATUS_NOT_SUPPORTED` fail-fast exports only where needed to link hosted-runtime experiments.
3. Implement package file/config reads before hosted runtime launch.
4. Implement timer and thread/sync primitives before Kestrel.
5. Implement socket listen/accept/send/receive and external HTTP verification before Kestrel smoke.
