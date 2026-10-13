# App Model

## NativeAOT-first application boundary

M16-006/#684 is qualified in [step1205](../done/step1205.md).
[Step1203](../done/step1203.md) records its
host-tested sparse anonymous-memory backend and high-address checks. The user
VA policy now ends at 64 GiB; physical backing, kernel identity mappings and
table roots remain below 4 GiB. Dynamic mappings use the 4..64-GiB window,
separate virtual and physical quotas, at most 64 ownership extents, 4096 pool
pages and the existing 128-table-page/32768-leaf limits. A reservation has no
backing or leaf cost until commitment.

`LinuxMemoryServices` builds and audits candidate tables before publishing
memory state. Anonymous fixed replacement discards backing; `mprotect(NONE)`
retains contents. Partial operations split ownership, freed/reused pages are
zeroed, and failures preserve the live root and accounting. Its caller must
run on a separate kernel root while the target user root is inactive, then
reload/publish CR3 before resuming user code. Accessible `MADV_DONTNEED` ranges
are eagerly zeroed without claiming physical reclamation; inaccessible ranges
release their backing. Demand faults, file/shared mappings and SMP are unsupported.

The default-off M16-006 adapter now dispatches under the original kernel root,
copies high user pointers through audited physical aliases, then reloads the
mutated user root before checked IRET. Startup services supply observed HPET
monotonic time, retained-map memory facts, a MADT/native-validated single CPU,
bounded brk ownership and separately captured consumable entropy. Unsupported
wall-clock and entropy refill requests fail explicitly. Dump policy is retained;
in-place mremap preserves bytes and real musl stack probes receive bounded
ENOMEM/EFAULT results. No thread/signal behavior is fabricated.

[Step1204](../done/step1204.md) records the implementation, original native review,
controlled memory negatives, twelve ancestors and unchanged upstream entry
attempt. [Step1205](../done/step1205.md) qualifies 4,646 full-host checks across
608 fixtures (94 Native) and three fresh 150-record guests. The 34-call fixture
proves high-address getrandom copy-out in CPL3 as well as high cross-page copy-in.
The one-use/64-call M16-005 policy is unchanged. The upstream payload stops at
ARCH_SET_FS before Hello; these APIs do not qualify an upstream runtime.

M16-007/#685 and #707 are qualified in [step1208](../done/step1208.md). Their
default-off child retains sixteen descriptors, two 4KiB pipes and separate 8KiB
scratch in an owned supervisor RW/NX arena. The immutable /app/ view reads the
original declared package ELF, including bytes beyond 1MiB, rather than its
mutable user mapping. Proc snapshots use actual retained-map facts and VMAs.
Duplicate fds share cursors/status and final-close endpoint ownership; fd flags
and internal generation tokens remain separate. Stdin is EOF and output uses
owned serial channels. Blocking pipe calls fail ENOSYS; EPIPE requires the
explicit mechanical ignored-SIGPIPE policy. The gateway keeps its 64-call bound
and checks returns before resuming CPL3. 4,679 full-host checks across 613
fixtures (94 Native), eight original native bodies, six repeated descriptor/pipe
guests and fifteen retained ancestors pass. The descriptor witness executes three
actual openat routes: payload open, traversal rejection and CLOEXEC reopen.
Step1206 preserves the initial omission; step1207 records its review and #708's
still-open brk/fixed-map ownership defect before unchanged Hello/runtime acceptance.
This is bounded mechanical behavior; it does
not qualify an unchanged upstream runtime or a continuing workload.

#686 adds mechanical threads/TLS/futex, scheduler-backed blocking pipes
and real per-thread signal-mask inheritance/restoration. Its scheduler chain is
#183 through #188, then #191/#192; #183 is the next ready foundation.
M16-009/#687 owns first pinned NativeAOT Hello acceptance after signal
startup/delivery, before focused suspension checks; Hello itself starts runtime
threads before printing. #688 repeats Hello and qualifies the comprehensive
runtime exerciser. See the canonical [workload roadmap](workload-profiles.md).

M16-005/#683 and supplemental #699 are qualified in
[step1201](../done/step1201.md): 4,619 full-host checks across 604 groups
(94 Native), scoped original native review, three write/exit guests, four
controlled-negative guests and seven ancestors pass. The default-off profile
enters one mechanical ELF at CPL3, dispatches checked Linux write/exit/errno,
and restores its owned kernel baseline. Real NativeAOT Hello, upstream guest
runtime behavior, general processes and CLROS Kestrel remain unqualified.

The mechanical M16-005 ELF fixtures exercise privilege, syscall and restoration
mechanics only; they do not substitute for #687's real NativeAOT Hello workload.
Before capturing the kernel baseline, the owned adapter establishes NULL FS/GS
selectors and independently checks that their 64-bit MSR bases are preserved.
The excursion restores that owned baseline; inherited firmware selector caches
outside the owned GDT are not a restoration target. Negative captures keep the
ordinary smoke result failed and require a separate exact challenge/restoration
validator, including package identity and the existing parent validators.

The qualified loader/service children in [step1200](../done/step1200.md) add
host-checked copy, startup-stack and protected-root mechanics plus an EFI read-back
path. 4,603 full-host checks, native review, three real combined package guests,
standalone children, five ancestors and missing-RNG rollback pass. Those loader/service profiles do not enter the ELF,
set an application TLS thread pointer, register app GC roots in the kernel, or
prove upstream application GC/EH, syscalls or CLROS Kestrel execution.

The host qualification in [step1198](../done/step1198.md) accepts separate
Hello and runtime fixtures plus static ELF inspection/publication tooling.
The runtime fixture uses upstream GC/EH, threads/TLS, ThreadPool/tasks and timers;
the Minimal API retains real `CreateSlimBuilder`/Kestrel with configuration
watching disabled before provider creation and HTTP/1.1 endpoint defaults.
Windows managed checks and all three pinned static Linux payload runs pass.
The [requirement inventory](workload-profiles.md#pinned-payload-requirements-and-trace-limits)
combines actual ELF/auxv/native/source/trace evidence and source error paths;
it does not claim exhaustive failure execution. This host qualification adds no
CLROS guest application launch.

The versioned Linux ELF package in [step1199](../done/step1199.md) now has a
default-off M16-002 staging profile. Three final-source guests retain exact
Hello/runtime/Minimal API package bytes across firmware exit as read-only/NX data,
with closed file handles, validated ledger and a bounded immutable file copy.
Full-host qualification passes all 4,565 checks. Later steps above qualify bounded
ELF mappings, startup construction and mechanical user entry; continuing runtime
services and the upstream guest runtime remain incomplete. A normal NativeAOT
executable must not be treated as raw OSAPPv0 bytes or
share the kernel collector. Existing native contracts below retain their
historical ABI and evidence.

The memory adapter must distinguish sparse virtual reservations from committed
pages and physical-memory quotas. The pinned Hello trace reserves more than
4 GiB with `PROT_NONE`; the current bounded loader range is not sufficient to
reproduce that request. Preserve the qualified payload, or record and retrace
an explicit bounded runtime configuration before changing its requirements.
See [the measured memory requirement](workload-profiles.md#pinned-payload-requirements-and-trace-limits).

Stock Linux/musl remains the primary application target. The host-only
[custom Kestrel transport comparison #702](http://192.168.4.35:3000/admin/CLROS/issues/702)
may inform the network boundary;
[WASI HTTP #703](http://192.168.4.35:3000/admin/CLROS/issues/703)
with a custom ASP.NET `IServer` is a separate optional target and does not run
Kestrel. Both are supplemental research without milestones or critical-path
dependencies; neither qualifies guest execution. The
[Kestrel ledger](kestrel.md#transport-decision-experiments) owns that distinction.

## Current Status

The legacy OSAPPv0 native-tier app model described below is in early host-tested
planning. The separate Linux ELF path above is the current first-class .NET 10
application target. Kestrel support is mandatory, but upstream runtime,
sockets, timers, threading/scheduler and continuing network prerequisites remain
incomplete. Broad workload claim boundaries are tracked in `docs/completion-gates.md`.

`docs/workload-profiles.md` defines the application workload profiles used by the backlog. `SingleMinimalApi` is the near-term default and deliberately keeps the first hosted target to one Minimal API/Kestrel workload without requiring a process table or per-app address spaces. `MultiMinimalApi` is a later profile that requires process/isolation/supervisor semantics and is not implemented today.

The build now accepts `/p:OSWorkloadProfile=SingleMinimalApi` or
`/p:OSWorkloadProfile=MultiMinimalApi`. This currently selects compile symbols
and the `OS.Kernel.Build.WorkloadProfile` metadata contract only. It is not app
launcher, process, hosted .NET, or Kestrel evidence.

`OS.ManifestTool validate` accepts a matching `--profile` argument. The current
JSON validator allows only the single primary `app` object under
`SingleMinimalApi`, rejects future multi-app fields with explicit status codes,
and returns `multi_minimal_api_unsupported` for `MultiMinimalApi` until process
and supervisor semantics exist.

`OS.Cloud.AwsImdsAppMetadataPolicyPlanner` now models the first host-tested
cloud metadata policy boundary for future app-supervisor work. Direct app
metadata access is denied by default; metadata and user-data requests are
allowed only when a supervisor-mediated policy explicitly enables them, and IAM
role credentials remain disabled by default.

Host packaging now has byte-placement primitives for future app payloads: `tools/OS.ImageBuilder build-native-payload --input <raw-bin> --output <payload-bin>` wraps a raw native app binary in the minimal `OSAPPv0` v0 header, `tools/OS.ImageBuilder build-package --input <dir> --output <pkg>` can create deterministic parser-valid `OSPKGv0` packages from a host directory, `tools/OS.ImageBuilder build-native-app-package --manifest <native-manifest> --input <raw-bin> --output <pkg>` can build a validated single-file package at the manifest-selected `app.path`, `tools/OS.ImageBuilder build-native-app-image --efi <BOOTX64.EFI> --manifest <native-manifest> --input <raw-bin> --output <raw-image>` can compose that package into a raw image containing `\EFI\BOOT\BOOTX64.EFI`, `\OS\MANIFEST.JSON`, and `\OS\APP.PKG`, `tools/OS.ImageBuilder validate-package --manifest <native-manifest> --package <pkg>` can validate that the manifest-selected `app.path` exists in the package and has a valid native payload header, and `tools/OS.ImageBuilder create-raw --package <path>` can copy a package file into the ESP at `\OS\APP.PKG`. `OS.Storage.OsPackageParser` can validate an in-memory package metadata header/file table, enumerate file entries by deterministic index, and copy file paths into caller-owned buffers. `OS.Storage.OsPackageFileSystem` can open/read/enumerate package-relative files from caller-owned package bytes, `OS.Storage.OsPackageFileStagingPlanner` can copy an already-opened package file entry into caller-owned staging storage while preserving source-range overflow metadata and without live storage or PAL calls, `OS.Pal.OSHostPackageFileTable` and `OSHostPackageFileFacade` can model deterministic PAL-facing open/read-at/info/close/enumerate status translation over already-loaded package bytes with invalid-handle rejection, and `OS.Storage.OsPackageLoader` can locate `OS/APP.PKG` through the host-tested GPT ESP + FAT32 parser stack over an `IReadOnlyBlockDevice`. `OS.Pal.OSHostConfigEnvironmentTable` and `OSHostConfigEnvironmentFacade` can model manifest config/environment lookup and enumeration status translation over caller-provided bytes only. `OS.Manifest.NativeAppLaunchPlan` can turn a parsed native manifest into launch-readiness requirements, including source-backed manifest environment metadata and optional bounded listen endpoint metadata. `OS.Kernel.Runtime.NativeAppPayloadMappingPlanner` can turn a validated selected app descriptor into deterministic payload/header/executable/entry ranges for a future executable-mapping step. `OS.Kernel.Runtime.NativeAppExecutableMemoryPlan` turns that range plan into a host-tested write-then-execute page-permission plan for future native app executable bytes. `OS.Kernel.Runtime.NativeAppInvocationPlanner` consumes executable-memory planning plus launch-plan metadata and produces a deterministic invocation metadata plan for stack requirements, entry offsets, and bounded manifest metadata counts without allocating stacks or executing code. `OS.Kernel.Runtime.NativeAppLifecyclePlanner` consumes invocation metadata plus app-log readiness and produces a non-executing lifecycle plan for future app supervisor work. `BootSequence.RunAppPackageReadiness`, `BootSequence.RunAppLaunchDescriptorReadiness`, `BootSequence.RunAppPayloadReadiness`, `BootSequence.RunAppExecutableMappingReadiness`, `BootSequence.RunAppExecutableMemoryReadiness`, `BootSequence.RunAppInvocationReadiness`, `BootSequence.RunAppLifecycleReadiness`, `BootSequence.RunAppSelectionReadiness`, `BootSequence.RunAppSelectionPreflightReadiness`, `BootSequence.RunNativeAppArgumentVectorReadiness`, `BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness`, and `BootSequence.RunAppEntryReadiness` can aggregate manifest, package, launch-plan, selected-app descriptor, payload-header, payload-range-plan, executable-memory-plan, invocation-plan, argument-vector metadata, app-log readiness, virtio-blk app-selection preflight, and FetchApp manifest config preflight results into stable readiness markers for the future boot boundary. These are not an app launcher and do not execute app code.

`BootSequence.RunNativeManifestEnvironmentReadiness` adds a narrower
environment-readiness boundary for the same future native app handoff. It
formats already-parsed manifest environment metadata into
`NATIVE_MANIFEST_ENV_READY` only when the native manifest parse and diagnostic
formatter succeeded, and emits status-coded `NATIVE_MANIFEST_ENV_NOT_READY`
metadata for uninitialized, parse-failure, invalid-source-range, or diagnostic
formatting failure inputs. It does not materialize process environment
variables, enter hosted .NET, start Kestrel, read storage, or run an app.

`OS.Manifest.NativeAppSelectionLoader` now composes the caller-buffer GPT ESP manifest loader, caller-buffer package loader, package parser, native launch-plan creation, and launch-descriptor creation into one status-preserving host-tested selection boundary. Its load result preserves nested manifest/package primary/backup GPT table-source metadata plus required and available byte counts when caller-owned scratch buffers are too small, inherits backup-GPT fallback from the nested loader path, and only reports success for an explicitly constructed success value whose nested manifest, package, launch-plan, and descriptor results also succeeded; a default result does not satisfy readiness. It only consumes caller-owned buffers and an `IReadOnlyBlockDevice`; it does not read from live virtio-blk, map payload bytes, start apps, or enter hosted .NET.

`OS.Kernel.Runtime.NativeAppArgumentHandoffPlanner` composes the existing
native app invocation metadata with the parsed native app argument-vector
metadata. The handoff result preserves nested invocation and argument-vector
statuses, argument count, parsed argument byte count, raw manifest launch
argument byte length, and explicit `ExecutesApp=false` /
`DefinesFinalAbi=false` boundary flags. It rejects default dependency results,
invocation failures, argument-vector failures, impossible argument counts,
invalid parsed argument metadata, and parsed-byte counts that exceed the raw
launch argument byte length. This is a host-tested metadata plan only; it does
not define `argc`/`argv`, allocate stacks, switch stacks, start hosted .NET, or
execute app code.

`BootSequence.RunNativeAppArgumentHandoffReadiness` exposes that already-built
handoff result as ordered `APP_ARGS_HANDOFF_READY` or
`APP_ARGS_HANDOFF_NOT_READY` markers. The marker preserves readiness status,
handoff status, invocation status, argument-vector status, argument count,
parsed/raw argument byte counts, and explicit no-stack/no-execution/no-final-ABI
boundary flags. It is not wired into the default boot path and does not invoke
apps, materialize `argc`/`argv`, allocate or switch stacks, start hosted .NET,
start ASP.NET Minimal API, or start Kestrel.

`OS.Manifest.NativeAppSelectionBootScratchPlan` defines the fixed boot-owned scratch sizing contract needed before the selection loader can be called from boot code. The default contract reserves a 512-byte GPT header sector, 128 GPT partition entries at 128 bytes each rounded to 32 sectors, one partition descriptor slot, a 64 KiB manifest buffer matching the image-builder manifest input ceiling, and a 16 MiB package buffer matching the image-builder package ceiling. It rejects sector sizes that are outside or unaligned with the storage/GPT block-size contract, reports required and available byte/count metadata for each caller-owned buffer, and performs no storage reads, parsing, allocation, or app execution.

`OS.Manifest.VirtioBlockNativeAppSelectionPreflight` now bridges a completed caller-owned virtio-blk read-session result, expected `BlockDeviceInfo`, validated app-selection scratch buffers, and the selection loader. It rejects incomplete read-session provenance before scratch validation, rejects completed read-session adapter creation failures before loader entry, preserves adapter-create, scratch-plan, and selection-load status evidence, exposes initialization state so default/zero results are not readiness, and can be consumed by `BootSequence.RunAppSelectionPreflightReadiness` to emit `APP_SELECTION_PREFLIGHT_READY` or status-coded `APP_SELECTION_PREFLIGHT_NOT_READY` markers in host tests. Host coverage now also adapts a simulated full-disk read-session destination through `OS.Storage.VirtioBlockReadSessionBlockDevice` and proves the selected manifest/package app descriptor can be read from those caller-owned bytes. It still performs no live virtio-blk I/O, boot-time package load, app execution, hosted .NET, or Kestrel work.

`OS.Pal.OSHostAppLogTable` now models the first native app stdout/stderr sink contract for future launcher and PAL work. It stores bounded caller-owned byte records, tracks stdout versus stderr stream ids, rejects invalid streams, oversized records, and default/zero write/read results with status codes, and preserves machine-readable app status marker bytes for host tests. This is host-only contract evidence; no guest app output is routed to serial yet.

`OS.Kernel.Runtime.NativeAppExitStatusFormatter` now models the first native
app exit marker contract. It formats bounded ASCII
`APP_EXIT code=<n> status=<OSHOST_STATUS_*>` records for normal exits,
nonzero exits, and launcher failures, rejects invalid exit codes and status
values, and can write the marker through `OSHostAppLogTable` as a
machine-readable stdout record. Its exit-code byte count and marker writes use
the shared `OS.Std.Text.AsciiDecimalFormatter` and
`OS.Std.Text.AsciiTextBuilder` helpers instead of app-model-local decimal
formatting loops. It also parses the same byte-span marker shape back into a
status-coded result, rejecting malformed prefixes, field order, exit codes,
status tokens, extra fields, truncated fields, and default/zero parse results.
The marker has no explicit launcher-failure field, so parsing can only infer
the formatter's launcher-failure convention (`code=255` with a non-OK status).
This is host-only marker evidence; no app has run and no live exit has been
observed.

`OS.Kernel.Runtime.NativeAppExitLogObserver` scans bounded
`OSHostAppLogTable` records using only the public entry/read APIs and a
caller-owned scratch buffer. It considers only stdout records tagged with
`OSHostAppLogConstants.FlagMachineReadable`, ignores other streams and
non-machine-readable records, and uses `NativeAppExitStatusFormatter.TryParse`
for APP_EXIT parsing. Its result preserves initialized-state, record index,
stream id, flags, sequence number, bytes read, required bytes, log/read status,
parse status, parsed exit code/status, and launcher-failure inference. Normal
malformed input is status-coded as `MalformedExitMarker`, no observed marker is
`NoExitMarker`, invalid tables/records are explicit failures, and short scratch
buffers report `RecordBufferTooSmall`. This is host-only supervisor-ingestion
evidence; it does not route live app logs or observe a running guest app.

`OS.Kernel.Runtime.NativeAppExitLogObserverDiagnosticFormatter` formats an
initialized observer result into a caller-owned ASCII buffer with the stable
`APP_EXIT_OBSERVER status=` prefix. The marker includes observer status, record
scan/index metadata, stream id, flags, sequence number, read byte counts,
`OSHostStatus` names, parse-attempt/status metadata, parsed exit code/status,
and launcher-failure inference. It supports required-byte queries, reports
short destinations without partial writes, and rejects default/uninitialized
observer results. Its decimal byte-count and write paths use the shared
`OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder` helpers
instead of app-model-local loops. This is host-only diagnostic formatting
evidence; it does not route live logs or observe a running guest app.

`BootSequence.RunNativeAppExitObserverReadiness` can emit ordered
`APP_EXIT_OBSERVER_READY` or `APP_EXIT_OBSERVER_NOT_READY` markers from the
same already-produced observer metadata. This is host-tested readiness only; it
does not run an app, ingest live serial logs, start a supervisor, or enforce a
restart policy.

`OS.Kernel.Runtime.NativeAppLifecyclePlanner` now models the first native app
lifecycle readiness contract. It consumes a successful invocation plan and a
bounded app-log sink readiness result, preserves the app identity metadata that
the future supervisor needs, and verifies that the app log can hold
machine-readable exit/status markers before a future launcher starts an app.
The log-sink readiness value exposes initialized-state metadata and rejects
zero remaining record capacity or records too small for app exit/status markers.
`BootSequence.RunAppLifecycleReadiness` emits `APP_LIFECYCLE_READY` for a ready
plan or `APP_LIFECYCLE_NOT_READY status=<status> invocation=<status>
log=<status>` for dependency failures. This is host-only readiness evidence; no
supervisor state, restart policy, live stdout/stderr routing, app execution, or
exit capture exists yet.

`tools/OS.QemuRunner` can now observe exact `APP_EXIT` serial lines in its
summary parser. Valid records are reported with `APP_EXIT_RESULT` fields, and
malformed app-exit lines are reported with `APP_EXIT_MALFORMED` reason and line
fields without changing required-marker or forbidden-marker smoke behavior.
This is runner-summary evidence only; it does not execute an app or capture a
live exit by itself.

`OS.Kernel.Runtime.NativeAppPayloadStagingPlanner` now models the first
caller-owned executable-byte staging boundary after payload range planning. It
copies only the selected executable bytes from already-loaded package bytes into
a supplied staging span, preserves payload/mapping/header status metadata, and
rejects failed mapping plans, overflowed package ranges, package ranges outside
the supplied bytes, too-small staging buffers, and default/zero results. It does
not read live storage, map pages, change permissions, relocate code, or execute
the app.

`OS.Kernel.Runtime.NativeAppPackageStagingReadinessPlanner` now composes the
caller-owned package/app staging evidence that already exists: manifest load,
package parse, launch plan, selected descriptor, package-file staging, payload
mapping, payload staging, executable-memory planning, and invocation planning.
It returns one initialized readiness value only when every dependency succeeded
and staged-byte, writable-staging, and entry-offset metadata agree. It rejects
dependency failures, byte mismatches, entry mismatches, and default/zero results
without reading storage, allocating memory, mapping pages, changing
permissions, relocating code, or executing an app.

`OS.Manifest.NativeAppEntryStagingPreflight` now wraps that already-produced
package-staging readiness into the final app-entry preflight. `APP_ENTRY_READY`
can be emitted only after the preflight proves an initialized manifest/package
selection plus caller-owned package-file staging and caller-owned payload
staging metadata. Default or failed staging readiness produces a status-coded
`APP_ENTRY_NOT_READY` marker and remains host-tested only.

`OS.Kernel.Runtime.NativeAppPackageStagingReadinessFormatter` formats the
package-staging not-ready marker into caller-owned ASCII byte buffers. It
mirrors the current `APP_PACKAGE_STAGING_NOT_READY ...` status fields, reports
required bytes for short destinations, rejects unknown status values, and does
uses the shared `OS.Std.Text.AsciiDecimalFormatter` and
`OS.Std.Text.AsciiTextBuilder` helpers for decimal byte counts and marker
writes. It does not read storage, allocate staging memory, map pages, relocate
code, or execute an app.

`OS.Net.FetchAppHttpCompletionExpectation` now derives the FetchApp completion
expectation from already-parsed native manifest environment/config entries. It
defaults to HTTP status `200` and marker `FETCH_OK`, accepts
`FETCH_HTTP_EXPECTED_STATUS`, `FETCH_HTTP_EXPECTED_MARKER`,
`FETCH_HTTP_EXPECTED_BODY`, and `OS_SMOKE_EXPECTED_BODY`, and rejects malformed
status codes, empty markers, oversized markers beyond the fixed evidence
buffer, missing required marker config when requested, and failed manifest
parses with status-coded results. `OS.Net.FetchAppHttpRequestEndpoint` derives
the FetchApp request endpoint from `FETCH_HTTP_URL` or explicit
`FETCH_HTTP_HOST`, `FETCH_HTTP_PORT`, and `FETCH_HTTP_PATH`, defaults to
`example.com:80/` when absent, copies host/path into caller-owned buffers, and
rejects HTTPS/non-HTTP schemes, missing hosts, invalid ports, empty paths, and
oversized fields without performing DNS, socket, or HTTP I/O. Its diagnostic
formatter emits a caller-owned `FETCH_HTTP_ENDPOINT` marker with status, port,
host/path byte counts, required byte counts, and config-source flags without
dumping host/path values. `BootSequence.RunFetchAppHttpRequestEndpointReadiness`
can emit ordered `FETCHAPP_HTTP_ENDPOINT_READY` or
`FETCHAPP_HTTP_ENDPOINT_NOT_READY` markers from the same already-produced
endpoint metadata, including only status, port, byte-count, and config-source
flag metadata. It is not wired into the default boot path and does not read
manifests from live storage, resolve DNS, open sockets, perform HTTP I/O, or
execute FetchApp.
`OS.Net.FetchAppHttpTcpContextPlan` adds the next FetchApp-facing network
handoff contract by combining that already-derived endpoint, an
already-successful DNS resolver transaction result, a cached IPv4 neighbor-route
result, and caller-provided local TCP identity into `HttpTcpClientGetPlannerInfo`
for the existing HTTP TCP GET planner. It preserves endpoint/DNS/route status
metadata, validates DNS host fingerprint parity, and rejects uncached routes or
invalid local identity without live DNS, ARP, TCP, HTTP, socket, or FetchApp
execution.
`OS.Net.FetchAppHttpCompletionPlan` models the first FetchApp-facing completion
predicate over the existing HTTP TCP GET planner. It requires an
already-successful `HttpTcpClientGetPlanResult`, validates a configured HTTP
status and non-empty expected ASCII body marker against caller-owned response
bytes, and returns status-coded metadata for future app/supervisor observation.
`OS.Net.FetchAppHttpCompletionExpectationDiagnosticFormatter` formats the
initialized expectation metadata into a caller-owned `FETCH_HTTP_EXPECTATION`
marker with status, initialization, expected status, marker length, and
config-source flags, without dumping configured marker/body bytes.
`OS.Net.FetchAppHttpCompletionPlanDiagnosticFormatter` formats that already
produced completion metadata into a stable caller-owned
`FETCH_HTTP_COMPLETION` marker with required-byte and short-destination
reporting. `BootSequence.RunFetchAppHttpCompletionReadiness` can emit ordered
`FETCH_APP_HTTP_COMPLETION_READY` or `FETCH_APP_HTTP_COMPLETION_NOT_READY`
markers from the same metadata. It does not execute FetchApp, open sockets,
perform DNS, transfer packets, load manifests from storage, parse JSON, or
execute hosted .NET/Kestrel configuration.
`OS.Manifest.NativeManifestFetchAppConfigPreflight` adds the manifest-side
preflight for that path by validating an already-loaded native manifest and
caller-owned manifest bytes before FetchApp completion planning consumes them.
It preserves nested status metadata and copies the configured/default marker
into caller-owned scratch only; it does not read storage, execute FetchApp, or
perform HTTP I/O.
`BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness` can aggregate
that preflight into `FETCHAPP_CONFIG_PREFLIGHT_READY` or
`FETCHAPP_CONFIG_PREFLIGHT_NOT_READY` markers while preserving nested manifest
load/parse, expectation, expected status-code, marker byte-count,
configured/default marker, and manifest byte-count metadata. It is not wired
into the default boot path.

## Implemented Behavior

Placeholder apps exist to reserve the expected surfaces:

- `HelloSharpFs`
- `FetchApp`: not executable in the guest yet; its current host-tested network
  boundary is `FetchAppHttpRequestEndpoint`,
  `FetchAppHttpCompletionExpectation`, and `FetchAppHttpCompletionPlan`, which
  derive request endpoint plus expected status/body-marker metadata from the
  parsed native manifest, can emit host-tested endpoint and completion
  BootSequence readiness markers from already-produced results, validate an
  already-produced HTTP TCP GET result plus response bytes for that marker, and
  can format that result as a `FETCH_HTTP_COMPLETION` diagnostic marker.
- `TinyHttpServer`
- `DiagnosticApp`
- `MinimalApiSmoke`: host-buildable ASP.NET Core .NET 10 Minimal API fixture. It returns the configured `OS_SMOKE_EXPECTED_BODY` value from `GET /` and `GET /healthz`, exposes non-secret smoke metadata from `GET /config`, and includes `dotnet run --project tools/OS.Tooling -- kestrel package` to publish the host package layout under `artifacts/kestrel-smoke`. The published package can now be staged into a host-built raw ESP with `tools/OS.ImageBuilder create-raw --kestrel-package` and read back from `\OS\APP.PKG` through host storage/package parsers. This is not in-guest Kestrel evidence. The first in-guest package contract is `kestrel.smoke.package.v0` in `docs/kestrel.md`; it requires the package-relative assembly `apps/minimal-api/MinimalApiSmoke.dll`, listen endpoint `http://0.0.0.0:8080`, and manifest environment/config key `OS_SMOKE_EXPECTED_BODY=OS_MINIMAL_API_OK`.
- `tools/OS.ImageBuilder` can place a caller-supplied package payload at `\OS\APP.PKG` alongside optional `\OS\MANIFEST.JSON`, and can validate/stage a `kestrel.smoke.package.v0` package root with `--kestrel-package` for `SingleMinimalApi` host-image evidence only.
- `tools/OS.ImageBuilder build-native-payload` can prepend a 32-byte `OSAPPv0\0` native payload header with entry offset `32` to caller-supplied raw app bytes.
- `tools/OS.ImageBuilder build-package` can create deterministic `OSPKGv0` packages from a host directory using ordinal package-path ordering.
- `tools/OS.ImageBuilder build-native-app-package` can parse a kernel-facing native manifest, wrap caller-supplied raw app bytes with `OSAPPv0`, build a deterministic single-file `OSPKGv0` package at the manifest-selected `app.path`, and validate the generated package before success.
- `tools/OS.ImageBuilder build-native-app-image` composes the existing native app package builder and raw image writer so one host command emits a raw image with `BOOTX64.EFI`, `OS/MANIFEST.JSON`, and `OS/APP.PKG` from caller-supplied raw app bytes.
- `tools/OS.ImageBuilder validate-package` can parse a kernel-facing native manifest, parse a package, resolve manifest `app.path`, and validate the selected `OSAPPv0` payload header without executing app code.
- `OS.Storage.OsPackageLoader` can read `OS/APP.PKG` from a primary GPT ESP through the FAT32 first-level file reader and validate package metadata from caller-owned storage.
- `OS.Storage.VirtioBlockPackageReadPreflight` bridges a caller-owned virtio-blk read-session toward package-loader inputs. It rejects incomplete read-session provenance before package parsing, validates caller-owned GPT/FAT/package buffers, preserves nested storage/package-load evidence, and performs no live device notification, hardware waiting, DMA/BAR mapping, MMIO, real disk reads, app launch, hosted .NET, or Kestrel execution.
- `OS.Storage.OsPackageFileSystem` can open, read, and enumerate package-relative files from already-loaded package bytes, including bounded range reads and bounded path copies for future app binary/config access.
- `OS.Storage.OsPackageFileStagingPlanner` can stage an already-opened package file entry from already-loaded package bytes into a caller-owned destination through `OS.Std.Memory.BufferOperations.MemoryCopy`. It rejects open failures, zero-length files, invalid package ranges including source-range overflow, short destinations, defensive shared-copy failures, and default/zero results before any future app launcher treats staged bytes as readiness evidence.
- `OS.Manifest.NativeManifestParser` parses kernel-facing native manifest v0 bytes, including package/native runtime selection, bounded environment fields, bounded listen endpoint metadata, and status-coded rejection for malformed input. Its parse result exposes initialized-state metadata so default/zero values do not satisfy readiness. `NativeManifestEnvironmentDiagnosticFormatter` formats that parsed environment metadata into a caller-owned `NATIVE_MANIFEST_ENV status=` marker with count/capacity and first key/value source-range metadata while explicitly reporting that process environment variables, hosted .NET, and Kestrel were not started. `BootSequence.RunNativeManifestEnvironmentReadiness` wraps that formatter in `NATIVE_MANIFEST_ENV_READY` / `NATIVE_MANIFEST_ENV_NOT_READY` readiness markers while preserving parse/format status and initialized state. Host tests now cover exact maximum accepted byte lengths for app name, app path, args, environment key/value fields, and listen host metadata, plus one-byte-over rejection statuses and environment diagnostic failure/readiness boundaries. This is parser/formatter/marker validation evidence only; it does not load or execute an app.
- `OS.Manifest.NativeManifestLoader` locates and reads `OS/MANIFEST.JSON` from the GPT ESP through host-tested GPT/FAT32 parser contracts, preserves caller-buffer GPT source and scratch-size metadata, and exposes initialized-state metadata so default/zero load results do not satisfy readiness.
- `OS.Manifest.NativeAppSelectionLoader` composes caller-buffer manifest/package loading from the same GPT ESP image with package parsing, launch-plan validation, and launch-descriptor validation. It returns the nested failing layer instead of throwing or collapsing diagnostics, preserves manifest/package primary/backup GPT table-source metadata plus scratch-buffer sizing from nested load failures, proves backup-GPT fallback composition over an injected image, and its default result is not treated as success.
- `OS.Manifest.NativeAppSelectionBootScratchPlan` validates the boot scratch buffers expected by the selection loader before any storage read occurs: GPT header sector geometry, GPT partition-entry sectors, partition descriptors, manifest bytes, and package bytes. It rejects non-512-aligned sector sizes with `InvalidSectorSize` before computing GPT entry-sector capacity.
- `OS.Manifest.VirtioBlockNativeAppSelectionPreflight` composes a completed caller-owned virtio-blk read-session result with expected block-device geometry validation, completed read-session block-device adapter creation, app-selection scratch validation, and `NativeAppSelectionLoader` delegation. It preserves read-session, adapter-create, scratch-plan, manifest/package, launch-plan, and descriptor provenance while still executing only host-tested in-memory selection composition. Host tests now cover a simulated full-disk read-session destination adapted as the `IReadOnlyBlockDevice` source for manifest/package app selection.
- `OS.Manifest.NativeAppLaunchPlan` records app name/path/args fields, `app.kind=package`, `runtime.kind=native-aot`, requested network/log modes, optional `listen=host:port` metadata, required package-storage runtime capability, required network runtime capability when `network=dhcp` or listen metadata is present, and required text output boot capability when `log=serial`; its result exposes initialized-state metadata so default/zero values do not satisfy readiness.
- `OS.Manifest.NativeAppLaunchPlanDiagnosticFormatter` formats already-produced launch-plan results into caller-owned `APP_LAUNCH_PLAN status=` diagnostics. The marker preserves launch status, source-backed app field byte counts, environment/listen metadata, boot/runtime capability masks, and explicit `appLoaded=false`, `appExecuted=false`, `hostedDotNetStarted=false`, and `kestrelStarted=false` boundary flags without loading package bytes or launching an app. Its decimal byte-count and marker write paths use the shared `OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder` helpers, and host source guards reject formatter-local integer byte-count/write helpers and direct digit writes.
- `NativeAppLaunchPlan` carries source-backed manifest environment entries from `env.KEY=value` fields without turning them into app-visible process state yet.
- `OS.Manifest.NativeAppArgumentVectorPlanner` converts the launch plan's source-backed `args=` field into a bounded argument vector without copying strings or executing app code. It splits unquoted space-delimited arguments, preserves quoted spans such as `"hello cloud"` as one argument, reports malformed quotes and count/length limits with status codes, and exposes initialized-state metadata so default/zero results are not readiness evidence. `OS.Manifest.NativeAppArgumentVectorDiagnosticFormatter` formats the already-produced plan result into a caller-owned `APP_ARGS_VECTOR status=` ASCII marker with planner status, initialization, argument count/limits, first-argument offset/length metadata, and explicit `appExecuted=false` / `kestrelStarted=false` boundary flags. The diagnostic formatter rejects default results and reports short destinations without modifying the caller buffer. `BootSequence.RunNativeAppArgumentVectorReadiness` emits ordered `APP_ARGS_VECTOR_READY` or status-coded `APP_ARGS_VECTOR_NOT_READY` markers from the same already-produced plan while preserving initialization state, planner/launch status, argument counts and caps, error offset, and explicit `executesApp=false` / `startsKestrel=false` boundary flags.
- `OS.Manifest.NativeAppArgumentVectorDiagnosticFormatter` routes its decimal
  byte-count and marker write paths through the shared
  `OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder`
  helpers, keeping the argument-vector diagnostic marker on the common
  no-runtime formatting surface.
- `NativeAppLaunchPlan` carries listen endpoint host bytes and a bounded numeric port into the launch plan without opening sockets, creating a listener, or claiming Kestrel support.
- `OS.Manifest.NativeAppSocketBindPolicy` checks a future PAL IPv4 bind endpoint against the launch plan's manifest listen endpoint, preserves expected/actual endpoint metadata, and exposes initialized-state metadata so default/zero results do not satisfy readiness. It opens no sockets, calls no PAL bind, exports no PAL functions, and starts no hosted runtime or Kestrel server.
- `OS.Manifest.NativeAppSocketBindBridge` consumes the manifest bind policy and host-only `OSHostSocketTable.Bind` mock, preserves policy/socket status and initialized-state metadata so default/zero results do not satisfy readiness, and remains mock-only without PAL exports, live sockets, hosted .NET, or Kestrel.
- `OS.Manifest.NativeAppSocketListenBridge` consumes the manifest bind policy and host-only `OSHostSocketTable` create/bind/listen mocks, preserves policy/socket/listen status and initialized-state metadata so default/zero results do not satisfy readiness, and remains mock-only without PAL exports, live sockets, scheduler waits, hosted .NET, or Kestrel.
- `OS.Manifest.NativeAppLaunchDescriptorBuilder` combines a successful launch plan with package file lookup and `OS.Manifest.NativeAppPayloadHeader` validation to produce a selected app descriptor containing the package entry, app binary length, payload header, argument byte length, network mode, and log mode. Its result exposes initialized-state metadata so default/zero values do not satisfy readiness.
- `OS.Manifest.NativeAppPayloadHeader` validates the v0 native payload header: `OSAPPv0\0` magic, version `0`, header size `32`, flags `0`, entry offset inside the payload after the header, and reserved field `0`. Its result exposes initialized-state metadata so default/zero values do not satisfy readiness.
- `OS.Kernel.Runtime.NativeAppPayloadMappingPlanner` computes a status-coded executable range plan from a successful selected app descriptor: payload offset/length, header length, executable offset/length, entry offset relative to payload, entry offset relative to executable content, and entry offset relative to package bytes. Its result exposes initialized-state metadata so default/zero values do not satisfy readiness.
- `OS.Kernel.Runtime.NativeAppPayloadStagingPlanner` copies the selected executable content range from already-loaded package bytes into caller-owned staging storage through `OS.Std.Memory.BufferOperations.MemoryCopy`. It rejects failed mapping plans, range overflow, out-of-bounds package ranges, too-small destinations, defensive shared-copy failures, and default/zero result values without reading live storage, mapping pages, changing page permissions, relocating code, or invoking the app.
- `OS.Kernel.Runtime.NativeAppPackageStagingReadinessPlanner` aggregates already-produced manifest, package, descriptor, staging, executable-memory, and invocation planning results into a single consistency-checked readiness value. It rejects dependency failures, staged-byte mismatches, writable-staging mismatches, entry-offset mismatches, and default/zero result values without performing storage I/O, allocation, executable mapping, page-permission changes, relocation, or app execution.
- `BootSequence.RunAppPackageStagingReadiness` consumes that readiness value and
  emits host-tested `APP_PACKAGE_STAGING_READY` or status-coded
  `APP_PACKAGE_STAGING_NOT_READY ...` markers with nested dependency metadata.
  It is not wired into the default boot path.
- `OS.Kernel.Runtime.NativeAppPackageStagingReadinessFormatter` can format the
  full not-ready marker line for package-staging failures into caller-owned
  ASCII bytes, including initialized state, nested dependency statuses, and byte
  metadata. Its numeric formatting is routed through the shared no-runtime
  ASCII text helpers. It is a diagnostic marker helper only and performs no
  storage I/O, allocation, executable mapping, permission change, relocation, or
  app launch.
- `OS.Kernel.Runtime.NativeAppExecutableMemoryPlan` consumes a successful payload mapping plan and computes the future writable staging byte count, executable byte count, page-aligned executable map offset/length, executable-content offset inside that mapping, and entry offset inside the executable mapping. It rejects failed mapping plans, zero executable content, invalid page size or executable alignment, unaligned executable offsets, accidental RWX policy, unsafe non-W^X policy, invalid entry offsets, overflow, and default/zero result values without allocating pages or changing live permissions.
- `OS.Kernel.Runtime.NativeAppInvocationPlanner` consumes `NativeAppExecutableMemoryPlanResult` and `NativeAppLaunchPlanResult`, preserves launch metadata counts, and computes the non-executing invocation prerequisites that a future launcher will need after executable-memory planning. It rejects executable-memory plan failures, launch-plan failures, inconsistent executable-memory metadata, invalid entry offsets, invalid stack sizes, bounded manifest metadata failures, range overflow, and default/zero result values without allocating stacks, switching stacks, creating threads, mapping pages, or defining a final app ABI.
- `OS.Kernel.Runtime.NativeAppArgumentHandoffPlanner` consumes a successful
  invocation plan and a successful argument-vector plan, checks that parsed
  source-backed argument byte metadata is compatible with the raw manifest
  launch argument byte length, preserves nested dependency status metadata, and
  exposes explicit no-execution/no-final-ABI flags. Its default result and
  default dependency results are not success evidence.
- `BootSequence.RunNativeAppArgumentHandoffReadiness` consumes an already-produced
  `NativeAppArgumentHandoffPlanResult` and emits `APP_ARGS_HANDOFF_READY` only
  when it is initialized and successful. Failed/default results emit
  `APP_ARGS_HANDOFF_NOT_READY` with readiness, handoff, invocation,
  argument-vector, argument-count, parsed/raw argument-byte, and stack-unavailable
  metadata. This is marker/readiness evidence only and does not define the final
  ABI, allocate stacks, switch stacks, invoke native app code, start hosted .NET,
  start ASP.NET Minimal API, or start Kestrel.
- `OS.Kernel.Runtime.NativeAppExitStatusFormatter` formats and parses bounded native app exit markers for future supervisor and runner evidence. It rejects invalid exit codes, invalid `OSHostStatus` values, malformed marker text, and default/zero create, marker, and parse results, reports destination sizing without throwing, and can write the marker through the host-tested app log table without executing app code.
- `OS.Kernel.Runtime.NativeAppExitLogObserver` scans host-tested app-log records for machine-readable stdout `APP_EXIT` markers with caller-owned scratch storage, preserves record/parse metadata in value-type results, rejects default/zero results, reports malformed markers and short scratch buffers with status codes, and ignores non-machine-readable or non-stdout records without executing app code.
- `OS.Kernel.Runtime.NativeAppExitLogObserverDiagnosticFormatter` formats initialized observer results into caller-owned `APP_EXIT_OBSERVER status=...` diagnostics, supports required-byte queries and short-destination failures, rejects default observer results, uses the shared no-runtime ASCII decimal/text formatting helpers, and preserves the no-allocation/no-exception normal-failure boundary.
- `BootSequence.RunNativeAppExitObserverReadiness` consumes `NativeAppExitLogObserverResult` and emits host-tested `APP_EXIT_OBSERVER_READY status=Ready ...` or status-coded `APP_EXIT_OBSERVER_NOT_READY ...` markers while preserving observer scan/read/parse/exit metadata. It is not wired into the default boot path.
- `OS.QemuRunner` and `dotnet run --project tools/OS.Tooling -- qemu smoke` expose an opt-in `--marker-preset app-exit` that requires `APP_EXIT_OBSERVER_READY` for future native-app smoke images. This is marker-contract tooling only; it does not prove a live app ran or exited.
- `OS.Kernel.Runtime.NativeAppLifecyclePlanner` consumes invocation metadata and app-log readiness, preserves app identity metadata, records exit-marker/log-record capacity, and rejects invocation failures, missing/undersized/exhausted/default app log sinks, and default/zero result values before any app code can run.
- Launch-plan creation is status-coded and rejects missing app fields, unsafe package-relative paths, unsupported app/runtime selections, unsupported static network mode, missing boot capabilities, and missing runtime capabilities without executing app code.
- `BootSequence.RunAppPackageReadiness` emits `APP_PACKAGE_READY` only when native manifest loading, OS package parsing, and launch-plan validation all succeeded. It emits `APP_PACKAGE_NOT_READY manifest=<status> package=<status> launch=<status>` when any prerequisite fails.
- `BootSequence.RunAppLaunchDescriptorReadiness` emits `APP_LAUNCH_READY` only when a selected app descriptor is ready. It emits `APP_LAUNCH_NOT_READY status=<status> launch=<status> packageFile=<status> package=<status>` for missing package binaries or launch-plan/package-lookup failures.
- `BootSequence.RunAppPayloadReadiness` emits `APP_PAYLOAD_READY` only when the selected app descriptor and native payload header are ready. It emits `APP_PAYLOAD_NOT_READY descriptor=<status> payload=<status>` for descriptor failures or invalid payload headers.
- `BootSequence.RunAppExecutableMappingReadiness` emits `APP_EXEC_MAPPING_READY` only when a selected native app payload range plan is ready. It emits `APP_EXEC_MAPPING_NOT_READY status=<status> descriptor=<status> payload=<status>` for range-planning failures.
- `BootSequence.RunAppExecutableMemoryReadiness` emits `APP_EXEC_MEMORY_READY` only when a selected native app executable-memory plan is ready. It emits `APP_EXEC_MEMORY_NOT_READY status=<status> mapping=<mapping-status> descriptor=<descriptor-status> payload=<payload-status>` for memory-planning failures.
- `BootSequence.RunAppInvocationReadiness` emits `APP_INVOCATION_READY` only when a selected native app invocation plan is ready. It emits `APP_INVOCATION_NOT_READY status=<status> memory=<memory-status> launch=<launch-status>` for invocation-planning failures.
- `BootSequence.RunAppLifecycleReadiness` emits `APP_LIFECYCLE_READY` only when selected invocation metadata and app-log readiness are ready. It emits `APP_LIFECYCLE_NOT_READY status=<status> invocation=<status> log=<status>` for invocation or log-sink dependency failures.
- `BootSequence.RunAppSelectionReadiness` emits `APP_SELECTION_READY` only when native manifest loading, package parsing, launch-plan validation, and selected package-file descriptor creation all succeeded. It emits `APP_SELECTION_NOT_READY status=<status> manifest=<status> package=<status> launch=<status> descriptor=<status> packageFile=<status> descriptorPackage=<status>` when any prerequisite fails.
- `BootSequence.RunAppSelectionPreflightReadiness` emits `APP_SELECTION_PREFLIGHT_READY` only when a `VirtioBlockNativeAppSelectionPreflightResult` is initialized and successful. It emits status-coded `APP_SELECTION_PREFLIGHT_NOT_READY` markers for default/uninitialized results, incomplete read sessions, invalid block devices, scratch failures, and selection-loader failures.
- `BootSequence.RunAppEntryReadiness` emits `APP_ENTRY_READY` only when manifest/package/launch/descriptor prerequisites, `NativeAppEntryStagingPreflight`, payload-mapping, executable-memory, and invocation prerequisites all succeeded. It emits `APP_ENTRY_NOT_READY status=<status> manifest=<status> package=<status> launch=<status> descriptor=<status> staging=<status> packageStaging=<status> payloadStaging=<status> mapping=<status> memory=<status> invocation=<status>` when any dependency blocks the final pre-entry boundary.

## Unsupported Behavior

Actual app execution, app ABI, live stdout/stderr routing, package reads through the live boot path, package payload execution, network access, socket listen/bind behavior, app exit capture, restart policy execution, and process/resource/isolation enforcement are pending. Hosted .NET 10, in-guest ASP.NET Minimal API, Kestrel, and workload-profile completion claims remain governed by `docs/completion-gates.md`.

The app-package, app-launch-descriptor, app-payload, app-executable-mapping, app-executable-memory, app-invocation, app-lifecycle, app-selection, app-argument-vector, and app-entry readiness helpers are not wired into the default boot path or live EFI artifact yet.

## Tests

No app-execution tests exist. Host tests cover image-builder package byte placement, package/manifest planning, app-selection composition, and app readiness markers only:

```text
TEST_PASS imagebuilder emits dry-run package placement plan without writing image
TEST_PASS imagebuilder.package-builds-directory
TEST_PASS imagebuilder.package-builds-deterministically
TEST_PASS imagebuilder.package-build rejects unsafe input
TEST_PASS imagebuilder.native-payload writes v0 header
TEST_PASS imagebuilder.native-payload preserves app bytes
TEST_PASS imagebuilder.native-payload rejects missing input
TEST_PASS imagebuilder.native-payload rejects empty input
TEST_PASS imagebuilder.package validates manifest app payload
TEST_PASS imagebuilder.package validation rejects missing manifest
TEST_PASS imagebuilder.package validation rejects malformed manifest
TEST_PASS imagebuilder.package validation rejects missing package
TEST_PASS imagebuilder.package validation rejects missing app file
TEST_PASS imagebuilder.package validation rejects invalid payload header
TEST_PASS imagebuilder.native-app-package builds validated package
TEST_PASS imagebuilder.native-app-package preserves manifest app path
TEST_PASS imagebuilder.native-app-package rejects malformed manifest
TEST_PASS imagebuilder.native-app-package rejects empty app input
TEST_PASS imagebuilder.native-app-image creates raw image with package
TEST_PASS imagebuilder.native-app-image rejects missing efi
TEST_PASS imagebuilder.native-app-image rejects malformed manifest
TEST_PASS imagebuilder.kestrel_package stages published package
TEST_PASS imagebuilder.kestrel_package readback preserves published files
TEST_PASS imagebuilder.kestrel_package readback preserves manifest json
TEST_PASS imagebuilder creates raw ESP with manifest and APP.PKG
TEST_PASS storage.package parses package header and file table
TEST_PASS storage.package locates file content
TEST_PASS storage.package enumerates file table
TEST_PASS storage.package enumeration exposes file paths
TEST_PASS storage.package enumeration rejects out of range index
TEST_PASS storage.package parser path copy uses shared buffer operations
TEST_PASS storage.package loader reads package from gpt esp
TEST_PASS storage.package loader caller-buffer gpt falls back to backup
TEST_PASS storage.package loader caller-buffer reports primary gpt source
TEST_PASS storage.package loader caller-buffer reports backup gpt source
TEST_PASS storage.package loader rejects missing package
TEST_PASS storage.package loader rejects malformed package
TEST_PASS storage.virtio package preflight validates buffers
TEST_PASS storage.virtio package preflight rejects incomplete session
TEST_PASS storage.virtio package preflight reports scratch capacity
TEST_PASS storage.virtio package preflight preserves storage boundary
TEST_PASS appmodel.virtio selection preflight validates scratch
TEST_PASS appmodel.virtio selection preflight rejects incomplete session
TEST_PASS appmodel.virtio selection preflight preserves selection provenance
TEST_PASS appmodel.virtio selection preflight reads app from session destination
TEST_PASS appmodel.virtio selection preflight adapts completed read session block device
TEST_PASS appmodel.virtio selection preflight reports adapter creation failure
TEST_PASS appmodel.virtio selection preflight preserves no-launch boundary
TEST_PASS appmodel.virtio selection preflight default result is not success
TEST_PASS boot.sequence virtio app selection preflight ready
TEST_PASS boot.sequence virtio app selection preflight blocks default result
TEST_PASS boot.sequence virtio app selection preflight reports selection failure
TEST_PASS storage.package opens file
TEST_PASS storage.package rejects missing file
TEST_PASS storage.package enforces bounds
TEST_PASS storage.package filesystem enumerates bounded paths
TEST_PASS storage.package staging copies opened file
TEST_PASS storage.package staging rejects short destination
TEST_PASS storage.package staging rejects invalid package range
TEST_PASS storage.package staging rejects source range overflow
TEST_PASS storage.package staging rejects open failure
TEST_PASS storage.package staging preserves no-live-storage boundary
TEST_PASS storage.package staging uses shared buffer operations
TEST_PASS storage.package staging default result is not success
TEST_PASS pal.file opens package file
TEST_PASS pal.file reads offset ranges
TEST_PASS pal.file rejects unsafe paths
TEST_PASS pal.file rejects invalid handles
TEST_PASS manifest kernel accepts maximum string field lengths
TEST_PASS manifest kernel rejects oversized string fields
TEST_PASS manifest kernel parse default result is not success
TEST_PASS manifest loader default result is not success
TEST_PASS appmodel launch plan accepts manifest
TEST_PASS appmodel launch plan accepts packaged native app
TEST_PASS appmodel launch plan carries environment fields
TEST_PASS boot.sequence manifest env ready
TEST_PASS boot.sequence manifest env rejects parse failure
TEST_PASS boot.sequence manifest env rejects invalid source range
TEST_PASS boot.sequence manifest env rejects default result
TEST_PASS boot.sequence manifest env formats diagnostic failure marker
TEST_PASS boot.sequence manifest env preserves default markers
TEST_PASS appmodel launch plan records listen endpoint
TEST_PASS appmodel launch plan requires network for listen endpoint
TEST_PASS appmodel launch plan rejects missing capabilities
TEST_PASS appmodel launch plan rejects malformed requirements
TEST_PASS appmodel launch plan default result is not success
TEST_PASS appmodel launch diagnostic formats selected app
TEST_PASS appmodel launch diagnostic reports short destination
TEST_PASS appmodel launch diagnostic formats missing runtime
TEST_PASS appmodel launch diagnostic rejects default result
TEST_PASS appmodel launch plan formatter uses shared ascii text builder
TEST_PASS appmodel args vector splits manifest args
TEST_PASS appmodel args vector preserves quoted spaces
TEST_PASS appmodel args vector accepts empty args
TEST_PASS appmodel args vector rejects too many args
TEST_PASS appmodel args vector rejects malformed quotes
TEST_PASS appmodel args vector rejects failed launch plan
TEST_PASS appmodel args vector default result is not success
TEST_PASS appmodel args vector formatter formats success marker
TEST_PASS appmodel args vector formatter reports short destination
TEST_PASS appmodel args vector formatter rejects default result
TEST_PASS appmodel args vector formatter uses shared ascii text builder
TEST_PASS boot.sequence app args vector ready
TEST_PASS boot.sequence app args vector rejects failed result
TEST_PASS boot.sequence app args vector rejects default result
TEST_PASS boot.sequence app args vector preserves default markers
TEST_PASS appmodel.socket.policy accepts manifest listen endpoint
TEST_PASS appmodel.socket.policy accepts wildcard manifest endpoint
TEST_PASS appmodel.socket.policy rejects missing listen endpoint
TEST_PASS appmodel.socket.policy rejects wrong port
TEST_PASS appmodel.socket.policy rejects wrong address
TEST_PASS appmodel.socket.policy preserves no-socket-io boundary
TEST_PASS appmodel.socket.policy default result is not success
TEST_PASS appmodel.socket.bindbridge binds manifest-approved endpoint
TEST_PASS appmodel.socket.bindbridge rejects policy mismatch
TEST_PASS appmodel.socket.bindbridge propagates mock bind conflict
TEST_PASS appmodel.socket.bindbridge rejects missing table
TEST_PASS appmodel.socket.bindbridge preserves mock-only boundary
TEST_PASS appmodel.socket.bindbridge default result is not success
TEST_PASS appmodel.socket.listenbridge creates manifest listener
TEST_PASS appmodel.socket.listenbridge rejects policy mismatch
TEST_PASS appmodel.socket.listenbridge propagates create failure
TEST_PASS appmodel.socket.listenbridge propagates bind failure
TEST_PASS appmodel.socket.listenbridge propagates listen failure
TEST_PASS appmodel.socket.listenbridge preserves mock-only boundary
TEST_PASS appmodel.socket.listenbridge default result is not success
TEST_PASS boot.sequence app package readiness emits ready marker
TEST_PASS boot.sequence app package readiness rejects manifest failure
TEST_PASS boot.sequence app package readiness rejects package failure
TEST_PASS boot.sequence app package readiness rejects launch-plan failure
TEST_PASS boot.sequence app launch descriptor ready
TEST_PASS boot.sequence app launch descriptor rejects missing binary
TEST_PASS boot.sequence app launch descriptor rejects launch-plan failure
TEST_PASS boot.sequence app payload ready
TEST_PASS boot.sequence app payload rejects descriptor failure
TEST_PASS boot.sequence app payload rejects invalid header
TEST_PASS boot.sequence app payload leaves default markers unchanged
TEST_PASS appmodel payload header accepts v0 native payload
TEST_PASS appmodel payload header rejects bad magic
TEST_PASS appmodel payload header rejects unsupported version
TEST_PASS appmodel payload header rejects invalid entry offset
TEST_PASS appmodel payload header default result is not success
TEST_PASS appmodel launch descriptor accepts package payload header
TEST_PASS appmodel launch descriptor rejects invalid payload header
TEST_PASS appmodel launch descriptor default result is not success
TEST_PASS appmodel selection loader composes image inputs
TEST_PASS appmodel selection loader reports primary gpt source
TEST_PASS appmodel selection loader gpt falls back to backup
TEST_PASS appmodel selection loader reports backup gpt source
TEST_PASS appmodel selection loader rejects manifest load
TEST_PASS appmodel selection loader reports manifest scratch capacity
TEST_PASS appmodel selection loader rejects package load
TEST_PASS appmodel selection loader reports package scratch capacity
TEST_PASS appmodel selection loader rejects launch capability
TEST_PASS appmodel selection loader rejects descriptor failure
TEST_PASS appmodel selection loader default result is not success
TEST_PASS appmodel.selectionscratch accepts default image buffers
TEST_PASS appmodel.selectionscratch rejects short manifest buffer
TEST_PASS appmodel.selectionscratch rejects short package buffer
TEST_PASS appmodel.selectionscratch rejects short gpt entry buffer
TEST_PASS appmodel.selectionscratch rejects invalid requested sizes
TEST_PASS appmodel.selectionscratch rejects unaligned sector size
TEST_PASS appmodel.selectionscratch default result is not success
TEST_PASS appmodel.selectionscratch preserves no-live-storage boundary
TEST_PASS appmodel payload mapping plans executable range
TEST_PASS appmodel payload mapping rejects descriptor failure
TEST_PASS appmodel payload mapping rejects zero executable bytes
TEST_PASS appmodel payload mapping rejects invalid entry range
TEST_PASS appmodel payload mapping default result is not success
TEST_PASS appmodel payload staging copies executable bytes
TEST_PASS appmodel payload staging rejects short destination
TEST_PASS appmodel payload staging rejects package range
TEST_PASS appmodel payload staging rejects mapping failure
TEST_PASS appmodel payload staging preserves no-execute boundary
TEST_PASS appmodel payload staging uses shared buffer operations
TEST_PASS appmodel payload staging default result is not success
TEST_PASS appmodel package staging readiness composes
TEST_PASS appmodel package staging readiness rejects selection failures
TEST_PASS appmodel package staging readiness rejects staging failures
TEST_PASS appmodel package staging readiness rejects memory invocation failures
TEST_PASS appmodel package staging readiness rejects byte mismatches
TEST_PASS appmodel package staging readiness preserves no-launch boundary
TEST_PASS appmodel package staging readiness default result is not success
TEST_PASS appmodel entry staging preflight accepts staged package and payload
TEST_PASS appmodel entry staging preflight rejects payload staging failure
TEST_PASS appmodel entry staging preflight rejects default staging readiness
TEST_PASS appmodel entry staging preflight default result is not success
TEST_PASS appmodel entry staging preflight preserves no-launch boundary
TEST_PASS appmodel package staging formatter formats not-ready marker
TEST_PASS appmodel package staging formatter reports short destination
TEST_PASS appmodel package staging formatter formats default readiness
TEST_PASS appmodel package staging formatter rejects invalid status
TEST_PASS appmodel package staging formatter default result is not success
TEST_PASS appmodel package staging formatter uses shared ascii text builder
TEST_PASS appmodel executable memory plan aligns payload mapping
TEST_PASS appmodel executable memory plan rejects rwx policy
TEST_PASS appmodel executable memory plan rejects invalid alignment
TEST_PASS appmodel executable memory plan rejects zero executable content
TEST_PASS appmodel executable memory plan rejects overflow
TEST_PASS appmodel executable memory plan default result is not success
TEST_PASS appmodel native invocation plan accepts executable memory
TEST_PASS appmodel native invocation plan rejects memory failure
TEST_PASS appmodel native invocation plan rejects invalid stack
TEST_PASS appmodel native invocation plan preserves manifest metadata counts
TEST_PASS appmodel native invocation plan rejects invalid entry metadata
TEST_PASS appmodel native invocation plan default result is not success
TEST_PASS appmodel invocation args handoff composes vector
TEST_PASS appmodel invocation args handoff rejects invocation failure
TEST_PASS appmodel invocation args handoff rejects argument-vector failure
TEST_PASS appmodel invocation args handoff rejects parsed-byte mismatch
TEST_PASS appmodel invocation args handoff rejects default dependency results
TEST_PASS appmodel invocation args handoff rejects impossible counts
TEST_PASS appmodel invocation args handoff default result is not success
TEST_PASS appmodel invocation args handoff layout contains no managed references
TEST_PASS boot.sequence app executable mapping ready
TEST_PASS boot.sequence app executable mapping rejects mapping failure
TEST_PASS boot.sequence app executable mapping preserves default markers
TEST_PASS boot.sequence app executable memory ready
TEST_PASS boot.sequence app executable memory rejects plan failure
TEST_PASS boot.sequence app executable memory preserves default markers
TEST_PASS boot.sequence app invocation ready
TEST_PASS boot.sequence app invocation rejects plan failure
TEST_PASS boot.sequence app invocation preserves default markers
TEST_PASS boot.sequence app selection ready
TEST_PASS boot.sequence app selection rejects manifest failure
TEST_PASS boot.sequence app selection rejects package failure
TEST_PASS boot.sequence app selection rejects descriptor failure
TEST_PASS boot.sequence app entry ready
TEST_PASS boot.sequence app entry rejects dependency failure
TEST_PASS boot.sequence app entry rejects staging preflight failure
TEST_PASS boot.sequence app entry blocks default staging preflight
TEST_PASS boot.sequence app entry preserves default markers
TEST_PASS pal.app_log stdout write captured
TEST_PASS pal.app_log stderr write captured
TEST_PASS pal.app_log invalid stream rejected
TEST_PASS pal.app_log oversized write rejected
TEST_PASS pal.app_log preserves app status marker
TEST_PASS pal.app_log reports deterministic exhaustion
TEST_PASS pal.app_log default results are not success
TEST_PASS appmodel.exit status formats success marker
TEST_PASS appmodel.exit status formats nonzero marker
TEST_PASS appmodel.exit status formats launcher failure
TEST_PASS appmodel.exit status rejects invalid input
TEST_PASS appmodel.exit status parser parses formatted marker
TEST_PASS appmodel.exit status parser rejects malformed marker
TEST_PASS appmodel.exit status parser accepts address status aliases
TEST_PASS appmodel.exit status parser uses shared memory helpers
TEST_PASS appmodel.exit status formatter uses shared ascii text builder
TEST_PASS appmodel.exit status create default result is not success
TEST_PASS appmodel.exit status marker default result is not success
TEST_PASS appmodel.exit status parser default result is not success
TEST_PASS appmodel.exit status writes marker to app log
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
TEST_PASS boot.sequence fetch http endpoint ready
TEST_PASS boot.sequence fetch http endpoint rejects failed result
TEST_PASS boot.sequence fetch http endpoint rejects default result
TEST_PASS boot.sequence fetch http endpoint preserves default markers
TEST_PASS fetch.http tcp context composes planner info
TEST_PASS fetch.http tcp context rejects endpoint failure
TEST_PASS fetch.http tcp context rejects dns failure
TEST_PASS fetch.http tcp context rejects dns host mismatch
TEST_PASS fetch.http tcp context rejects uncached route
TEST_PASS fetch.http tcp context rejects invalid local identity
TEST_PASS fetch.http tcp context default result is not success
TEST_PASS fetch.http completion expectation formatter formats success marker
TEST_PASS fetch.http completion expectation formatter reports short destination
TEST_PASS fetch.http completion expectation formatter rejects default result
TEST_PASS manifest fetchapp config preflight valid config
TEST_PASS manifest fetchapp config preflight defaults
TEST_PASS manifest fetchapp config preflight invalid status
TEST_PASS manifest fetchapp config preflight empty marker
TEST_PASS manifest fetchapp config preflight oversized marker
TEST_PASS manifest fetchapp config preflight manifest not ready
TEST_PASS boot.sequence fetchapp config preflight ready
TEST_PASS boot.sequence fetchapp config preflight rejects failed result
TEST_PASS boot.sequence fetchapp config preflight rejects default result
TEST_PASS boot.sequence fetchapp config preflight preserves default markers
TEST_PASS fetch.http completion formatter formats success marker
TEST_PASS fetch.http completion formatter reports short destination
TEST_PASS fetch.http completion formatter rejects default result
TEST_PASS boot.sequence fetch http completion ready
TEST_PASS boot.sequence fetch http completion rejects failed result
TEST_PASS boot.sequence fetch http completion preserves default markers
TEST_PASS appmodel.exit observer finds stdout marker
TEST_PASS appmodel.exit observer ignores non-machine-readable records
TEST_PASS appmodel.exit observer reports malformed marker
TEST_PASS appmodel.exit observer reports no marker
TEST_PASS appmodel.exit observer reports short record buffer
TEST_PASS appmodel.exit observer reports invalid log table
TEST_PASS appmodel.exit observer default result is not success
TEST_PASS appmodel.exit observer formatter formats success marker
TEST_PASS appmodel.exit observer formatter reports short destination
TEST_PASS appmodel.exit observer formatter rejects default result
TEST_PASS appmodel.exit observer formatter uses shared ascii text builder
TEST_PASS boot.sequence app exit observer ready
TEST_PASS boot.sequence app exit observer rejects observer failure
TEST_PASS boot.sequence app exit observer rejects default result
TEST_PASS boot.sequence app exit observer preserves default markers
TEST_PASS appmodel.lifecycle accepts invocation and log sink
TEST_PASS appmodel.lifecycle rejects invocation failure
TEST_PASS appmodel.lifecycle rejects missing log sink
TEST_PASS appmodel.lifecycle log sink default readiness is not success
TEST_PASS appmodel.lifecycle preserves app identity
TEST_PASS appmodel.lifecycle default result is not success
TEST_PASS boot.sequence app lifecycle ready
TEST_PASS boot.sequence app lifecycle rejects dependency failure
TEST_PASS boot.sequence app lifecycle preserves default markers
```

## Known Limitations

The apps are normal host projects for now, not freestanding native-tier binaries or hosted .NET 10 workloads. Manifest environment fields and listen endpoints are parsed and carried as metadata only; they are not process environment variables, ASP.NET configuration, hosted runtime state, socket binds, or TCP listeners.

The AWS metadata policy planner is a host-tested decision fixture only; it does not enforce per-app metadata permissions, proxy metadata responses, fetch IMDSv2 tokens, expose user data, or expose IAM credentials.

`build-native-payload` wraps bytes only; it does not compile native-tier binaries, validate a complete executable format, relocate code, map executable memory, or define the final app ABI. `build-package` packages an input directory exactly as supplied; it does not compile apps, create native-tier binaries, create hosted-workload archives, or validate executable formats. `build-native-app-package` only wraps one caller-supplied raw app byte file and places it at the manifest-selected package path. `build-native-app-image` is host-side composition only, and `validate-package` validates local manifest/package/payload consistency only; neither proves guest storage, boot-time package loading, executable mapping, app launch, or QEMU boot.

`NativeAppPayloadMappingPlanner` computes ranges only; it does not allocate executable memory, enforce W^X/NX at runtime, relocate code, validate a complete executable format, or jump to an app entrypoint. `NativeAppPayloadStagingPlanner` copies executable bytes only from already-loaded package memory into caller-owned staging memory; it does not read live storage, allocate pages, map executable memory, change protections, relocate code, or invoke the app. `NativeAppPackageStagingReadinessPlanner` validates consistency across already-produced staging and planning results only; it does not create boot-owned staging memory, allocate pages, map executable memory, change protections, relocate code, or invoke the app. `BootSequence.RunAppPackageStagingReadiness` only emits marker metadata from that readiness value and is not part of the default boot path. `NativeAppExecutableMemoryPlan` is host-tested permission and layout planning only; it does not allocate pages, copy executable bytes, change page protections, flush instruction caches, relocate code, or invoke an app entrypoint. `NativeAppInvocationPlanner` is host-tested metadata planning only; it does not allocate stacks, switch stacks, create threads, map pages, finalize an ABI, or execute app code.

`MinimalApiSmoke` can be published, staged into a host-built raw ESP, and read back from `\OS\APP.PKG` on the host, but it still runs only on the host Kestrel stack. The in-guest hosted .NET, Minimal API, and Kestrel claim boundary is `docs/completion-gates.md`.

The current `\OS\APP.PKG` path is readable only through the host-tested parser stack over an in-memory block-device abstraction, and package file lookup/staging is limited to already-loaded package bytes. The PAL package-file handle table is also host-tested only and does not export ABI symbols. `NativeAppSelectionLoader` composes those host-tested reads, backup-GPT fallback, nested manifest/package primary/backup GPT table-source metadata, and nested manifest/package required sizes for caller-owned scratch buffers, but still depends on caller-provided buffers and an injected block device; the default-result guard prevents accidental readiness from an uninitialized result, but it is still only host evidence. `NativeAppSelectionBootScratchPlan` only validates future boot-owned buffer sizes; it does not allocate those buffers, read storage, parse the manifest/package, or call the loader. `VirtioBlockNativeAppSelectionPreflight` adds read-session and scratch gating before the host-tested selection loader and rejects default/zero result values as not successful, and `BootSequence.RunAppSelectionPreflightReadiness` only turns that result into host-tested markers. Both still consume host-provided evidence and do not perform live virtio-blk reads or boot-time loading. `OsPackageFileStagingPlanner` copies from caller-owned package bytes only; it does not prove boot-owned staging memory, executable mapping, or app launch. The live boot path does not yet read the package from virtio-blk or pass it into `BootSequence.RunAppPackageReadiness`. The launch plan does not parse package content, load binaries, schedule threads, or bind hosted runtime services.

The selected app launch descriptor resolves metadata and validates only the small v0 native payload header. The explicit package/native runtime selection still does not map the binary into memory, validate a complete executable format, allocate stacks, enter an app ABI, start a thread, route live stdout/stderr, capture exit codes, or enter any hosted runtime. `NativeAppArgumentHandoffPlanner` only composes invocation and parsed argument-vector metadata; it does not define a final `argc`/`argv` ABI, materialize strings, allocate stacks, switch stacks, start hosted .NET, or run app code. `OSHostAppLogTable` preserves stdout/stderr bytes in host tests only; it is not connected to a launcher, serial writer, or hosted runtime console stream. `NativeAppExitStatusFormatter` formats and parses future exit marker bytes only; it does not observe a real app exit, enforce a restart policy, or expose a host runner result by itself. Its parser consumes already-present marker bytes and can infer launcher-failure-shaped formatter output only from the current `code=255` plus non-OK status convention. `NativeAppExitLogObserver` scans the host-tested app-log table only; it does not ingest live serial data, supervisor-owned ring buffers, or hosted runtime logs. `NativeAppExitLogObserverDiagnosticFormatter` formats observer metadata only; it does not make the observer a live supervisor or serial-log ingestion path. `NativeAppLifecyclePlanner` is readiness aggregation only; it does not create supervisor state, start/restart apps, route live app logs, capture exits, or enforce crash policy. QEMU runner `APP_EXIT_RESULT` summary lines only reflect serial text parsing when such lines are present; they are not live app-exit evidence.

`APP_PACKAGE_READY`, `APP_LAUNCH_READY`, `APP_PAYLOAD_READY`, `APP_EXEC_MAPPING_READY`, `APP_EXEC_MEMORY_READY`, `APP_INVOCATION_READY`, `APP_ARGS_HANDOFF_READY`, `APP_LIFECYCLE_READY`, `APP_SELECTION_READY`, `APP_SELECTION_PREFLIGHT_READY`, `NATIVE_MANIFEST_ENV_READY`, `FETCHAPP_CONFIG_PREFLIGHT_READY`, `FETCHAPP_HTTP_ENDPOINT_READY`, `APP_EXIT_OBSERVER_READY`, and `APP_ENTRY_READY` are host-tested readiness markers only. They prove that the relevant manifest, package, descriptor, payload, executable-memory, invocation, argument-handoff, lifecycle, preflight, manifest environment, FetchApp config, FetchApp request endpoint, observer, and entry status objects can be aggregated without exceptions. They do not prove live boot package reads, live virtio-blk reads, boot-owned scratch allocation, manifest/config reads from live storage, process environment materialization, executable page allocation or mapping, live W^X/NX enforcement, payload copying, relocation, stack allocation or switching, a final ABI, supervisor/log/exit handling, live app-exit observation, hosted .NET, Kestrel, live FetchApp execution, or app execution.

`kestrel.smoke.package.v0` is a hosted-workload package contract only. It defines future Minimal API package files, manifest fields, environment/config propagation, `GET /healthz` response text, and runner markers. It is not runtime evidence; the required hosted .NET 10, PAL, socket, TCP listener, and Kestrel proof remains in `docs/completion-gates.md`.

## Next Steps

1. Repair #708's reproduced brk/fixed-map ownership defect while preserving #684's sparse reservations and #685's qualified mechanical descriptors/pipes.
2. Implement #686 mechanical threads/TLS/futex and per-thread signal masks; #687 adds signal startup/delivery, qualifies the unchanged pinned Hello and then exercises suspension. #688 repeats Hello and adds the runtime exerciser. Do not move upstream application GC/EH into the kernel.
3. Connect continuing NIC/TCP and Linux socket/readiness services to the pinned Minimal API payload and qualify external Kestrel traffic under the [workload roadmap](workload-profiles.md).
4. Add private address spaces, descriptors/config, resource budgets and supervisor lifecycle for two applications, proving one survives the other's failure.
5. Preserve the legacy OSAPPv0 planners and `kestrel.smoke.package.v0` tooling as separate contracts. CoreCLR/framework-dependent launch and broader storage/package selection remain later work.
