# Step 870 - M01-022 loaded-image and owned-stack QEMU evidence

## Summary

Completed Gitea issue M01-022's live-QEMU evidence boundary. A fresh
`M01_LOADED_IMAGE_STACK_PROBE` build from clean implementation commit
`198be8ad12db388fec44d76f9270c8d754430940` booted under real QEMU/OVMF and
proved the M01-021 adapter's exact stage-one transcript once and in order:

```text
TEST_START m01.stage1
M01_LOADED_IMAGE_CAPTURED
M01_STACK_SWITCH_STUB_READY
M01_STACK_ARENA_ALLOCATED
M01_STACK_GUARD_RESERVED
M01_STAGE1_MAP_RECAPTURED
M01_STAGE1_RESERVATIONS_VALIDATED
M01_OWNED_STACK_READY
M01_OWNED_STACK_SWITCHED
M01_RSP_CONTAINED
TEST_PASS m01.stage1
OS_SMOKE_PASS
```

The smoke runner and wrapper now make this evidence fail closed:

- ordered markers are tracked across arbitrary serial read boundaries;
- unique markers must occur exactly once;
- ordered and unique markers imply required presence, while the legacy
  `RequireMarker` contract remains presence-only;
- missing, reordered, duplicated, forbidden, partial-EOF, timeout, nonzero
  exit, conflicting-terminal, and spontaneous reboot/shutdown outcomes receive
  stable failure classifications;
- the M01 probe switch requires a fresh EFI build and rejects
  `-SkipImageBuild`; and
- the summary preserves both configured and observed order/uniqueness state.

The default EFI path is unchanged. This is a narrow, pre-`ExitBootServices`
proof and does not change any completion-gate state.

## Why

M01-021 established source linkage, generated-object shape, and host behavior,
but explicitly stopped before guest execution. M01-022 crosses exactly the
next live boundary: it binds a clean source commit to a fresh PE32+ EFI
application, boots that artifact, observes successful firmware protocol and
allocation checkpoints, verifies post-allocation map/ledger checkpoints,
executes the first-party stack-switch path, and confirms that the continuation
observed `RSP` inside the owned usable-stack range.

Simple marker-presence checking was not strong enough for this claim. A stale
artifact, duplicated success marker, reordered checkpoint, early reboot, or
chunk-split marker could otherwise be mistaken for a valid transition.
Order, uniqueness, fresh-build selection, and preserved hashes are therefore
part of the acceptance boundary rather than optional diagnostics.

The serial/QEMU testing approach was informed by these reference paths as
test-only design input:

- `references/nativeaot-patcher-main/tests/Cosmos.TestRunner.Engine/Hosts/QemuX64Host.cs`
- `references/nativeaot-patcher-main/tests/Cosmos.TestRunner.Engine/Protocol/UartMessageParser.cs`

No reference implementation source was copied into first-party CLROS code.

## Files changed

- `scripts/run-qemu-smoke.ps1`
  - adds normalized ordered/unique marker forwarding;
  - adds the fail-closed `-EnableM01LoadedImageStackProbe` build selection;
  - rejects a stale or skipped image-build path for M01 evidence; and
  - prints deterministic dry-run evidence for the new controls.
- `tools/OS.QemuRunner/SerialMarkerStateMachine.cs`
  - tracks ordered progress and unique-marker counts incrementally across
    arbitrary serial chunks;
  - latches irreversible order violations without manufacturing a
    missing-marker result; and
  - adds append-only completion states for reordered and duplicate-unique
    evidence.
- `tools/OS.QemuRunner/Program.cs`
  - parses, validates, reports, and enforces the new marker contracts;
  - preserves legacy presence-only and ordered-preset behavior;
  - drains a passing stream for 750 milliseconds to detect late duplicates;
    and
  - classifies a clean spontaneous exit as `qemu_reboot_or_shutdown` only for
    the strict unique-evidence mode.
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - adds script and fake-QEMU coverage for complete, missing, reordered,
    duplicate, chunked, partial, cleanup-failure, unexpected-return, timeout,
    reboot, terminal-conflict, and legacy-compatible transcripts.
- `docs/qemu.md`
  - documents the runner controls, classifications, exact live tuple, and
    source/evidence boundary.
- `docs/boot.md`
  - preserves M01-021 as historical build evidence and records the separate
    M01-022 guest proof and nonclaims.
- `docs/architecture.md`
  - replaces stale loaded-image/stack nonclaims with the narrow live fact set
    while retaining all unproven allocator, paging, firmware-exit, and later
    subsystem boundaries.
- `done/step870.md`
  - preserves this evidence.
- `.gitea/backlog/clros-issues.json`
  - is updated separately after reviewed tracker evidence to record closure
    intent without coupling implementation acceptance to tracker mutation.

No change was made to `EfiEntry.cs`, `OS.csproj`, `BootSequence.cs`,
`Probes.cs`, `README.md`, or `docs/completion-gates.md`.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - passed with zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - passed with zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - exit code `0`;
  - terminal marker `TEST_PASS host.tests`;
  - zero `TEST_FAIL` lines;
  - accepted log:
    `artifacts/m01-022-host-exact/host-tests.stdout.log`;
  - log size: 816,036 bytes;
  - log SHA-256:
    `350C20FD2EEF1003E9727003EA30BDAC3323789963F540CE3101754D220A9580`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - passed the default dry-run contract.
- The M01 dry-run with `-BuildEfi`,
  `-EnableM01LoadedImageStackProbe`, and the exact ordered/unique chain
  - printed the complete forwarded arguments; and
  - passed `TEST_PASS qemu.runner.dryrun`.
- The authoritative real run used:

  ```powershell
  powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\scripts\run-qemu-smoke.ps1 `
    -BuildEfi `
    -EnableM01LoadedImageStackProbe `
    -EfiArtifact <evidence-root>\BOOTX64.EFI `
    -Image <evidence-root>\clros.raw `
    -SerialLog <evidence-root>\serial.log `
    -SummaryLog <evidence-root>\qemu-smoke.summary.log `
    -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
    -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
    -TimeoutSeconds 30 `
    -RequireMarker <the-12-marker-chain> `
    -RequireOrderedMarker <the-12-marker-chain> `
    -RequireUniqueMarker <the-12-marker-chain> `
    -ForbidMarker "TEST_FAIL,OS_SMOKE_FAIL,M01_INTEGRATION_FAIL,M01_STAGE1_FAIL"
  ```

  - wrapper exit code `0`;
  - passed `TEST_PASS efi.artifact.verify`,
    `TEST_PASS efi.shell.publish`, `TEST_PASS imagebuilder.raw`, and
    `TEST_PASS qemu.smoke`;
  - `TEST_FAIL`, `OS_SMOKE_FAIL`, `M01_INTEGRATION_FAIL`, and
    `M01_STAGE1_FAIL` were absent;
  - QEMU passed on the first authoritative attempt.
- `git diff --check`
  - passed.

Focused fake-QEMU coverage emitted:

```text
TEST_PASS qemu smoke script forwards M01 stage-one evidence controls
TEST_PASS qemu smoke script rejects stale M01 probe artifact selection
TEST_PASS qemu M01 ordered and unique dry-run evidence
TEST_PASS qemu M01 ordered markers reject reordering
TEST_PASS qemu M01 unique markers reject duplicate stage start
TEST_PASS qemu M01 unique markers reject duplicate stage pass
TEST_PASS qemu M01 unique markers reject duplicate semantic checkpoint
TEST_PASS qemu legacy required markers remain presence-only
TEST_PASS qemu M01 missing checkpoint markers are rejected
TEST_PASS qemu M01 partial marker EOF is rejected
TEST_PASS qemu M01 chunked marker stream succeeds
TEST_PASS qemu M01 allocation cleanup failure is rejected
TEST_PASS qemu M01 unexpected switch return is rejected
TEST_PASS qemu M01 timeout is deterministic
TEST_PASS qemu M01 reboot signature is rejected
TEST_PASS qemu M01 conflicting terminals are rejected
```

## Evidence

The authoritative run started at
`2026-07-27T19:03:19.7090684+00:00` and completed at
`2026-07-27T19:03:39.0168910+00:00`. Its ignored evidence root is:

```text
artifacts/logs/m01-022/198be8ad12db-20260727T190237Z/
```

The streaming result was:

```text
status=Passed terminal=Success bytes=1286 lines=34 invalidBytes=15 invalidLines=1 orderedSeen=12 outOfOrder=0 uniqueSeen=12 duplicateUnique=0
```

The 15 invalid bytes formed one bounded OVMF noise line and did not contain or
corrupt a marker. Every configured stage marker occurred exactly once. The
summary records:

```text
MISSING_MARKERS none
OUT_OF_ORDER_MARKERS none
DUPLICATE_UNIQUE_MARKERS none
FORBIDDEN_MARKER_HIT none
FAIL_REASON none
```

The runner observed the passing transcript, drained the serial stream for 750
milliseconds, then terminated the intentionally nonreturning opt-in guest.
`QEMU_PROCESS_RESULT` therefore records
`disposition=runner-terminated exitCode=-1`; that is the expected harness
termination after success, not a guest failure.

The accepted tool tuple was:

- .NET SDK `10.0.302`;
- MSVC linker `14.51.36252.0`;
- Windows SDK `10.0.28000.0`;
- ILCompiler package `10.0.8`, file `10.0.826.23019`;
- NativeAOT runtime pack `10.0.8`;
- QEMU and qemu-img `11.0.50`
  (`v11.0.0-12631-g54e84cdc7a`); and
- OVMF `edk2-x86_64-code.fd`, 3,653,632 bytes, SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

The artifact verifier reported PE32+ AMD64 machine `0x8664`, EFI application
subsystem `10`, executable/file-backed `.managed` entrypoint RVA
`0x00010BD0`, same-build `EfiMain` association, and absent ordinary, bound,
IAT, and delay-import directories.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `BOOTX64.EFI` | 131,584 | `62BCD4902A55DDDACCA8C490C6B5615C1BEEF78BA7DF96038FA1687FD2B8DC60` |
| `BOOTX64.EFI.link.json` | 1,817 | `9011F437D1C108D951E44ED4A4E02D26F059A3F4BA86FEC7585E065FFC0898EB` |
| `BOOTX64.EFI.link.map` | 60,905 | `8836CA390BC6409FD96E966E7FCFF5E9DA013FB6230D1D4D27CF12F859297821` |
| `BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `qemu-command.txt` | 1,700 | `A7D85C9B58853F8398C1F159E156F7180587BEEA62482B8AFDECE3028781ED79` |
| `clros.raw` | 67,108,864 | `0186D6B11DD31480B42F2ED3013E283ADEF7E425E5281872E58F9921E6F9CC02` |
| `serial.log` | 1,286 | `BF775826807B4B3E61B6E26C1DDD7101E0E16F141AC17384ADBC94C54CD1B1B9` |
| `qemu-smoke.summary.log` | 3,333 | `3EBDC0BCAAC146E2B8BEDBEE5B6C3ADA11413720558E874140975DF16186726A` |
| `qemu-smoke.stdout.log` | 15,056 | `6CCF99739FB05CDAA0FA6F2A15C5ED0A14B156DAE48A8F30C56052F22C481F54` |
| `efi-shell-publish.log` | 2,969 | `34B0DEF0925B40E2EF0272ED811C2B251ACA1CFB361B370059A5DCD2009A22D3` |
| `evidence-manifest.json` | 9,805 | `2EC0D45A229CA749C5F238ED7156DBB29E70F4BBAAA563C9AB6D260B02742968` |

The evidence manifest is schema version 1, UTF-8 without BOM, source-dirty
`false`, classification `pass`, and exit code `0`. It binds 10 tool versions,
19 nonempty hashed artifacts, 14 required markers, four forbidden markers,
and 31 markers parsed from the serial log. The manifest writer's stdout log
has SHA-256
`1C73BC67197C702396653133F353549ADC4E59FAC52EFF491C5B27736FB2E746`.

Independent review found no blocking issue after the two runner-classification
corrections described below. The reviewed source commit and its generated
evidence remained unchanged.

## Failures

- The first implementation classified every ordered-marker run as a
  reboot/shutdown failure when QEMU exited cleanly. That would have changed
  existing ACPI and diagnostics preset behavior. Strict spontaneous-exit
  rejection is now gated by the new unique-evidence mode, so legacy ordered
  presets retain their previous contract.
- The first implementation killed QEMU immediately on an order violation.
  That could stop the guest before later markers arrived and make a reordered
  transcript appear missing. Order failure now stays latched while bounded
  collection continues; the primary reordered classification is selected only
  when no required marker is missing.
- The first exact full-harness run encountered a pre-existing CRLF-sensitive
  source-regex check owned by M01-021 and reported
  `probe-guard-is-not-exact`. The authoritative host run used a local,
  canonical LF byte view for that unchanged source, then mechanically restored
  its CRLF working-copy bytes and refreshed the index. The M01-021 source has
  no diff and was not committed by M01-022. The accepted complete harness then
  exited `0` with `TEST_PASS host.tests`.
- The first evidence-manifest wrapper invocation had a PowerShell parser error
  in its local multiline argument construction. No manifest from that attempt
  was accepted. The corrected invocation generated and independently hashed
  the source-bound manifest listed above.
- No generated artifact, secret, forbidden first-party native source, or
  reference implementation was added to the commit.

## Deferred work

This step proves one fresh OVMF execution of the opt-in M01 adapter:

- the live loaded-image protocol call succeeded;
- one `LoaderCode` page and the `LoaderData` guard/stack arena were allocated;
- the post-allocation map was recaptured;
- the exact four-record reservation ledger was validated;
- the first-party switch bytes executed;
- the owned-stack continuation ran with contained `RSP`; and
- the unchanged default marker plan followed.

It does **not** prove that the reserved guard page is inaccessible, the switch
page transitions from writable to executable, NX/W^X is enforced, the
firmware-derived ranges form a reusable authoritative page allocator, CR3 or
descriptor tables are owned, COM1 is owned, `ExitBootServices` succeeds, or
the path works on firmware other than the recorded OVMF tuple.

It also does not prove PCI, storage, networking, application execution, the
`OSHost_*` PAL, hosted CoreCLR, ASP.NET Minimal API, Kestrel, AWS, Azure,
cloud readiness, or production readiness. Those remain separate issues and
completion gates.
