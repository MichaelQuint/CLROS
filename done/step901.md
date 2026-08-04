# Step 901 - Classify the .NET 10 no-stdlib declaration and export surface

## Summary

Implemented M04-001's allocation-free `OS.Std.NoRuntime` compatibility ledger
for the current `src/OS/Runtime/MinimalRuntime.cs` surface. The production C#
ledger exposes stable declaration, export, category, behavior, and support-state
enums; readonly reference-free entries; switch-indexed retrieval; and
fail-closed unknown/default handling without adding a canonical `System.*`
namespace or runtime behavior.

Source-bound host tests derive all 40 current canonical `System.*` declarations
and all 17 `[RuntimeExport]` methods from `MinimalRuntime.cs`. They preserve the
observed split of four link-only no-op transition helpers, twelve fail-fast
helpers, and one audited stack-probe helper. The exact marker is:

```text
CLROS_M04_001_READY declarations=40 runtimeExports=17 linkOnlyNoOp=4 failFast=12 auditedStackProbe=1 gate4=Fail canonicalSystemNamespace=false
```

Completion Gate 4 remains `Fail`.

## Why

The EFI-only `MinimalRuntime.cs` surface mixes compiler-required type shapes,
narrow byte/memory behavior, metadata attributes, fail-fast placeholders,
link-only transition stubs, and one audited stack-probe bridge. Treating those
declarations as a broad runtime implementation would overstate current
behavior. M04-001 gives later runtime work a deterministic, allocation-free,
source-bound inventory that records what is narrow, stubbed, or forbidden
without copying the EFI shim into the normal host build or silently satisfying
the runtime gate.

## Files changed

M04-001 owns exactly:

- `std/no-runtime/shared/DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.cs`
- `tests/OS.HostTests/DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- this uniquely allocated `done/step901.md`

The source/test implementation authority is commit
`94145a8821a3ffdb12679d3a20e0358e3981bf02`, tree
`e699785b56494a96ba0c07f02242fa26cb994b05`. Canonical documentation is
committed through `4b15c834c81bc1c9a91977627af91afd70a486b5`, tree
`5adbe1aade5d70525ae62f58bbfdc46dac056701`.

No EFI entry point, project file, kernel boot sequence, probe toggle, README,
reference source, backlog manifest, existing done ledger, generated artifact,
or forbidden first-party native source changed.

## Tests run

The accepted source-frozen wave ran the issue's required commands:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1
git diff --check
git status --short
```

After the two owned canonical documents changed, a focused validation also
reran the solution build, HostTests build, exact M04-001 focused test,
naming/source firewall, `git diff --check`, and status inspection. Both builds
reported zero warnings and zero errors; the exact M04-001 marker remained
between its start/pass markers.

## Evidence

Accepted ignored evidence root:

```text
C:\Projects\CLROS-M04-001\artifacts\logs\m04-001-final\94145a88-20260804T065948Z-r3
```

The source-frozen host wave ended with `HOST_WAVE_PASS` and preserved:

| Command | Exit | Seconds | stderr bytes | stdout SHA-256 |
|---|---:|---:|---:|---|
| solution build | 0 | 2.953 | 0 | `18F979B77821B054EEAF5202D0FF5A6CE7F11DA29991D0194B931A92F55C1D33` |
| HostTests build | 0 | 2.028 | 0 | `561931D7CF77782BC202ADC9C0B410EDDA6551068F35D3EEB60999ECBE11D2FF` |
| complete HostTests | 0 | 2693.509 | 0 | `1E9A695A1868601DDA78C53681126BC7F98FA7B1105AF90B803B174924C61720` |

The complete harness recorded 3,348 `TEST_START` lines, 3,349 `TEST_PASS`
lines, zero `TEST_FAIL` lines, exactly one `TEST_PASS host.tests`, and exactly
one ordered M04-001 start/ready/pass sequence. The host-wave summary SHA-256 is
`F28BDAFAE8C0B6A260432FFA366087537B1D8AE07E21A67FB5EFD44CFE38C995`.

The remaining host-only tooling wave ended with `TOOLING_WAVE_PASS`:

| Command | Exit | Seconds | stderr bytes | stdout SHA-256 |
|---|---:|---:|---:|---|
| QEMU dry-run preflight | 0 | 2.510 | 0 | `46951645587E4E5420D88F14640884D30240647BF93522E7994002E28BA7DF95` |
| naming/source firewall | 0 | 32.631 | 0 | `F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651` |
| EFI build/preflight | 0 | 52.326 | 0 | `3E410FC96B3C5BBA545A6A3C56093ABC9C9ED8FEC3E52F10E1854961D7674F88` |
| `git diff --check` | 0 | 0.055 | 0 | empty SHA-256 |
| clean status | 0 | 0.064 | 0 | empty SHA-256 |

The EFI host build reported:

```text
EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=61
EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=48 prerequisiteGap=UefiMemoryType
EFI_PREFLIGHT_OK
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

The generated EFI artifact is 55,808 bytes with SHA-256
`158432A6F1BA692561A8CA047AD4E1C1362E3549E5CA8A7EF4E7288ABECD1741`.
This is build/preflight evidence only and is not guest execution proof.

Steps 895 through 900 were already present across current Git refs and step
901 was absent. The repository allocator ran against an ignored reservation
view seeded with the globally audited pending step 900 and emitted:

```text
TEST_START done.step.allocate
NEXT_DONE_STEP done/step901.md
TEST_PASS done.step.allocate
```

The allocation log SHA-256 is
`A12B6973417F8B3B2EAF191EE442AEA9CAEC8490A0977C75F99EA890DD3D172E`.

## Failures

- The first focused test derived 38 instead of 40 declarations because its
  namespace parser treated nested `Runtime.CompilerServices` as an absolute
  namespace. The parser now composes nested namespace names and derives all 40.
- The first host build reported one unreachable-code warning from a redundant
  check of the compile-time Gate 4 `false` constant. The redundant branch was
  removed; accepted builds have zero warnings.
- The first full-wave launcher did not start because the worktree had no
  `artifacts/logs` directory. No command ran and no result was promoted.
- Host attempt r1 completed its solution build but Windows PowerShell's inner
  `Start-Process -Wait` did not return after the direct `dotnet` child exited.
  The exact wrapper was stopped, marked
  `HOST_WAVE_FAIL wrapper_wait_did_not_advance_after_solution_build_child_exit`,
  and rejected.
- Host attempt r2 used direct process/stream capture, but its evidence writer
  rejected empty stderr before it could record the completed build. It ended
  `HOST_WAVE_FAIL` and was rejected. The writer now explicitly accepts empty
  strings.
- The first tooling-wave invocation failed PowerShell parsing on a diagnostic
  interpolation before any acceptance command ran. The diagnostic used an
  explicit variable delimiter, then the accepted tooling wave ran once.
- No timeout, partial build, focused-only result, r1 output, or r2 output was
  promoted into acceptance evidence. All rejected artifacts remain ignored and
  separate from r3.

## Deferred work

- Independent review of the exact final head/evidence, PR merge, issue marker
  transition, and backlog synchronization remain required before M04-001 is
  done.
- The ledger adds classification metadata only. It implements no object, array,
  or string allocation helper; heap or GC behavior; root scanning; write
  barrier; interface dispatch; metadata discovery; class-constructor execution;
  exception/catch/finally/unwind behavior; or thread statics.
- QEMU dry-run is tooling preflight only. Real QEMU is inapplicable to this
  host-only ledger leaf and no guest marker was requested or claimed.
- Hosted .NET 10, Minimal API, Kestrel, provider support, CloudReady status, and
  production readiness remain unproven.

The evidence boundary is an allocation-free, host-tested classification ledger
over the current 40 `MinimalRuntime.cs` canonical declarations and 17 runtime
exports. It records, but does not improve, their current behavior and leaves
completion Gate 4 failing.
