# `apps` Override

This directory contains first-party CLROS workload fixtures and examples. Root `AGENTS.md` applies; this file narrows how agents should work on app projects.

## Purpose

Apps under this tree are used to prove the single-workload CloudReady model:

- Manifest-selected app launch.
- App arguments and environment/config visibility.
- Packaged file reads.
- Network client behavior.
- HTTP server behavior.
- Pinned Linux NativeAOT runtime, socket and Kestrel qualification.

Projects target `net11.0`; their execution evidence differs. Hello, focused
GC/ThreadPool, RuntimeSmoke/RuntimeMatrix, socket and finite Kestrel fixtures
have CLROS guest qualification in steps1307 through1556. Other samples remain
host-only. Check `docs/kestrel.md` and `docs/completion-gates.md` before claiming
a profile; complete Gate14 and arbitrary hosted .NET support remain open.

## Project Rules

Use:

- C# 14.0.
- `net11.0`.
- Nullable annotations enabled.
- Clear console output markers for host-side smoke tests.
- Small apps with one focused behavior each.

Do not:

- Add unmeasured Linux/POSIX requirements or an SSH/admin surface. The selected
  `linux-musl-x64` NativeAOT route may use the explicitly supported Linux adapter;
  new syscalls require measured upstream need and their own qualification.
- Add app behavior that requires unimplemented kernel services without documenting the prerequisite.
- Hide missing OS capabilities behind host-only mocks without naming them as mocks.
- Claim Kestrel, Minimal API hosting, or hosted .NET support from a host-only app run.

## Example App Roles

- `NativeAotHello`: unchanged pinned Linux NativeAOT Hello.
- `NativeAotRuntimeSmoke` / `NativeAotRuntimeMatrix`: bounded upstream runtime behavior.
- `NativeAotSocketSmoke` (sync/async modes) and `NativeAotSocketVectors`: measured socket/epoll fixtures.
- `MinimalApiSmoke` / `MinimalApiService`: finite Kestrel health, service and churn profiles.
- `MinimalApiStaticFiles`: separate physical-webroot/JSON candidate; original smoke
  source/ELF/index admission stays unchanged. See step1559; guest acceptance pending.
- `BlazorStaticFilesSmoke`: real Standalone browser WebAssembly client; generated
  host HTML/CSS and raw SDK outputs belong under artifacts. Its host publish or
  Linux asset fetch is separate from an external browser boot against CLROS.

The following older examples retain their separate host/planning boundaries:

- `HelloSharpFs`: packaged file read and manifest argument smoke.
- `FetchApp`: DNS/TCP/HTTP client smoke once OS networking exists.
- `TinyHttpServer`: native-tier or hosted HTTP response smoke.
- `DiagnosticApp`: manifest/config/environment visibility and exit-code reporting.

Keep each app focused. If a new scenario combines storage, networking, and hosted runtime behavior, create separate smaller tests first.

## ASP.NET Minimal API And Kestrel

Kestrel support is mandatory for the product goal, but it must be proven with real OS evidence.

For each new Kestrel scope, preserve or extend evidence for:

- The pinned upstream .NET 11 NativeAOT/Linux boundary. CoreCLR is a later route.
- Socket/timer/threading behavior sufficient for Kestrel.
- Network stack evidence through TCP.
- An image/package path that launches the Minimal API app.
- QEMU or libvirt serial evidence for HTTP request/response.

Minimal API samples should prefer AOT- and trimming-aware patterns:

- Explicit endpoint mapping.
- Source-generated JSON metadata where needed.
- No reflection-based endpoint discovery unless documented and tested.
- No dynamic code generation assumptions.

## Testing

For every app behavior change, add or update a host test or runner check first.

Expected markers should stay machine-readable:

- `TEST_START <name>`
- `TEST_PASS <name>`
- `TEST_FAIL <name> <reason>`
- `APP_EXIT <name> <code>`

When the app behavior depends on future OS services, document the missing service in `docs/app-model.md`, `docs/manifest.md`, `docs/networking.md`, or the relevant subsystem doc.
