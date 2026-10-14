# `tests` Override

This directory contains host-side tests. Root `AGENTS.md` applies, with these local rules.

## Purpose

Host tests provide fast evidence for contracts before QEMU/libvirt boot tests exist.

They are not a substitute for QEMU evidence, but every host-testable behavior should be covered here first.

## Test Output Contract

Every test group must emit:

- `TEST_START <name>`
- `TEST_PASS <name>`
- `TEST_FAIL <name> <reason>`

The full harness must emit:

- `TEST_PASS host.tests` on full success.
- `TEST_FAIL host.tests failures=<count>` on failure.

Keep names stable because scripts and future CI will parse them.

## Test Style

- Keep tests deterministic.
- Avoid sleeping or timing-dependent assertions unless testing timers explicitly.
- Avoid external network, cloud, libvirt daemon, QEMU daemon, or file-system state unless the test is explicitly an integration test and can be skipped/dry-run.
- Prefer focused test methods over broad scenario tests.
- Test invalid input and boundary cases.
- Assert exact marker/status strings where they are part of the contract.
- Keep tests readable for future AI agents.
- Register independent cases with `[HostTest("stable marker name")]`; use
  `[HostFixture]` only for groups requiring shared setup/teardown. Never execute
  tests in module initializers. Discovery and `--list` must have no side effects.
- Use `HostCheck` for assertions and grouped execution. It reports failures
  without hiding later cases. Do not introduce another local runner.
- Shared reviewed expectations belong in `OS.Tooling.EfiContracts`; derive
  inventory sizes instead of copying literals. Keep independent byte/ABI vectors.
- Do not hash another test's entire source. Verify its behavioral coverage and
  preserve build-input identity through the runner's embedded input manifest.

## Allowed APIs

Tests may use normal .NET 11 and C# 14 APIs.

Do not let host-test convenience leak into kernel/std implementation. If a production path cannot use exceptions/reflection/allocation, tests should verify the production path's constrained contract instead of wrapping it in host-only behavior.

## Required Test Additions

Add or update host tests when changing:

- Boot marker behavior.
- `BootSequence`.
- `BootInfo` or capability contracts.
- Probe defaults.
- Panic/diagnostic formatters.
- Manifest validation.
- QEMU/libvirt dry-run output.
- std/no-runtime algorithms.
- Image builder parsing/layout behavior.
- Future packet/DNS/HTTP parsers.

## Commands

Normal test pass:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --list
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane full --filter DiagnosticsTests
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane full
```

If a test changes build outputs, run the build separately before `--no-build`.
The default lane is fast and emits `host.tests.fast`. Filtered runs emit
`host.tests.focused`. Native/full runs include real compiler and artifact checks;
do not relabel fast evidence as complete acceptance. Input changes
since compilation are rejected before execution and checked again afterward.
Use `--results <empty-directory>` to name a run and `--resume <directory>` to
continue its saved lane and selection. Reused groups emit `HOST_FIXTURE_REUSED`,
never replayed test passes. Keep unknown artifact/native dependencies fresh;
review ownership before adding a group to `HostNativeResumePolicy`.
