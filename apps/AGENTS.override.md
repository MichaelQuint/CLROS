# `apps` Override

This directory contains first-party workload examples for the OS application model. Root `AGENTS.md` applies; this file narrows how agents should work on app projects.

## Purpose

Apps under this tree are used to prove the single-workload CloudReady model:

- Manifest-selected app launch.
- App arguments and environment/config visibility.
- Packaged file reads.
- Network client behavior.
- HTTP server behavior.
- Future hosted .NET 10 ASP.NET Minimal API and Kestrel support.

Current apps are host-buildable `net10.0` placeholders. They are not proof that the OS can run hosted .NET 10 workloads yet.

## Project Rules

Use:

- C# 14.0.
- `net10.0`.
- Nullable annotations enabled.
- Clear console output markers for host-side smoke tests.
- Small apps with one focused behavior each.

Do not:

- Add Linux, POSIX, or SSH assumptions.
- Add app behavior that requires unimplemented kernel services without documenting the prerequisite.
- Hide missing OS capabilities behind host-only mocks without naming them as mocks.
- Claim Kestrel, Minimal API hosting, or hosted .NET support from a host-only app run.

## Example App Roles

- `HelloSharpFs`: packaged file read and manifest argument smoke.
- `FetchApp`: DNS/TCP/HTTP client smoke once OS networking exists.
- `TinyHttpServer`: native-tier or hosted HTTP response smoke.
- `DiagnosticApp`: manifest/config/environment visibility and exit-code reporting.

Keep each app focused. If a new scenario combines storage, networking, and hosted runtime behavior, create separate smaller tests first.

## ASP.NET Minimal API And Kestrel

Kestrel support is mandatory for the product goal, but it must be proven with real OS evidence.

Before app code can claim Kestrel support, the repository needs:

- A documented hosted .NET 10 runtime boundary.
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
