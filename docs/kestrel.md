# .NET 11 Minimal API and Kestrel Ledger

## NativeAOT-first milestone

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
Stable batch acceptance remains pending; neither issue is closed by this evidence.

The [current workload decision](workload-profiles.md) makes M16 NativeAOT
services, continuing M07 TCP and M10 actual Kestrel the critical path.
M16-001/#679 through M16-007/#685 and the bounded scheduler/timer foundations
have historical qualification. Finish [#727's toolchain qualification](http://192.168.4.35:3000/admin/CLROS/issues/727),
then [#188 lifecycle](http://192.168.4.35:3000/admin/CLROS/issues/188), #191 waits
and #192 FS/TLS before #686. Follow the exact managed issue dependencies.
Mechanical ELF startup/write/exit does not run the upstream runtime.
M16-008/#686 uses mechanical thread/TLS/futex fixtures with real per-thread
signal-mask inheritance/restoration. M16-009/#687 qualifies the first pinned
Hello after signal startup/delivery, before focused suspension checks.
M16-010/#688 repeats Hello and qualifies the comprehensive runtime exerciser;
#689's real System.Net.Sockets fixture follows that runtime qualification.
CoreCLR-specific #204-#212 are deferred. The legacy hosted package/TPA contract
below remains a future target, not the first-web payload format. M16-002's
qualified ELF package preserves the meaning of existing OSAPPv0 and
`kestrel.smoke.package.v0` host evidence.
First acceptance uses one upstream static NativeAOT Minimal API, HTTP/1.1
IPv4 port8080 and /healthz body `OS_MINIMAL_API_OK`, with the unchanged
Kestrel evidence markers. Three fresh post-exit boots, external probes,
request churn/async/cancellation/shutdown and a negative probe after guest
termination are required. No guest Kestrel execution is currently qualified.

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

Kestrel support is a required CloudReady milestone, but it is not implemented.

The repository currently has EFI/QEMU smoke evidence, host-side manifest validation, kernel-facing source-backed manifest environment metadata, a host-tested manifest socket bind-policy helper for future PAL IPv4 endpoint validation, a host-tested manifest-approved mock socket bind bridge, a host-tested manifest-approved mock listener bridge, early storage and packet/TCP frame-planning primitives, a host-tested caller-owned TCP listener backlog metadata table, a host-tested passive-open-to-listener-backlog bridge, a host-tested listener-backlog-to-mock-accept-queue bridge, a host-tested established-stream-to-mock-socket inbound payload bridge, host-tested PAL socket ABI value contracts, a host-tested mock socket create/bind/listen lifecycle table with accept-queue semantics, Kestrel-oriented mock socket option and endpoint-query contracts, host-tested mock socket partial I/O plus peer close/reset semantics, a host-tested mock package-file handle table over loaded package bytes with file-table enumeration, an `OSHost_*`-shaped package-file facade, a host-tested mock config/environment query table that can be built from native launch-plan metadata including optional listen endpoint config keys, an `OSHost_*`-shaped config/environment facade, a host-tested mock memory table plus an `OSHost_*`-shaped memory facade for allocation/free/protect/query status translation, a host-tested mock timer table plus an `OSHost_*`-shaped timer facade, a host-tested mock wait/event table plus an `OSHost_*`-shaped wait/event facade for immediate manual-reset and auto-reset semantics, a host-tested mock stdout/stderr app-log table plus an `OSHost_WriteLogBytes`-shaped facade, a host-tested native app exit marker formatter, a host-buildable Minimal API fixture, a host-runner HTTP probe proven against local loopback, and documentation for the future hosted tier. The Kestrel-critical PAL contract is documented in `docs/pal.md` and `work/PAL/pal-design.md`, but no `OSHost_*` memory, socket, timer, wait/event, thread/sync, file/config, or log functions are exported or guest-tested. The OS does not have a hosted .NET 10 runtime, CoreCLR PAL integration, real sockets, complete TCP, scheduler/threading, async runtime support, or an in-guest ASP.NET Minimal API application.

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
| Logging/stdout | Console/log writes from hosted runtime and app route through OS serial/logging APIs | Mock stdout/stderr app-log table, `OSHost_WriteLogBytes`-shaped in-process facade, and native app exit marker formatter only; no exported `OSHost_WriteLogBytes`, guest serial routing, hosted runtime console binding, real app output, or observed app exit exists | Fixture startup, request logs, and app lifecycle/exit markers appear in serial output without host-only interception |
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

The first Kestrel proof depends on several major unfinished areas. Until those areas have evidence, the CloudReady status remains blocked at the hosted workload gate.

## Next Steps

1. Implement #684 memory/startup services, then #685 descriptors with mechanical fixtures; preserve sparse virtual reservation and physical-commit accounting as described in [the workload roadmap](workload-profiles.md#pinned-payload-requirements-and-trace-limits).
2. Qualify #686 mechanical threads/TLS/futex and per-thread signal masks; #687 implements signal startup/delivery, runs the first pinned Hello and then checks suspension. #688 repeats Hello and adds full runtime exercise. Keep application runtime ownership separate from kernel helpers.
3. Integrate continuing NIC/TCP (#691 and its dependencies), then M16 socket/epoll services and the unchanged upstream Minimal API payload. The optional transport comparison informs this integration without replacing its acceptance.
4. Capture three fresh guest boots, external `/healthz` traffic, request churn/cancellation/shutdown and a failed probe after termination before claiming Kestrel. Follow the exact tracker dependencies and evidence markers.
5. Preserve `kestrel.smoke.package.v0` and its host checks for later framework-dependent hosting; those contracts do not block the NativeAOT-first path.
