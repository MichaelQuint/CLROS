# Step 871 - M01-023 pre-EBS allocator arena and EFI stage-two wiring

## Summary

Completed Gitea issue M01-023's source/host/build boundary. The existing
M01-022 owned-stack continuation now has a second, separately gated
`M01_LIVE_ALLOCATOR_PROBE` path that:

- accepts only the canonical final stage-one static state after a contained
  continuation `RSP`;
- makes one checked two-page `EfiLoaderData` allocation;
- performs one fresh bounded memory-map recapture and requires a changed key
  plus a matching loader-data descriptor;
- extends the retained four-record ledger with exactly one sorted and validated
  `AllocatorArena`;
- invokes `M01LiveAllocatorIntegration.Execute` exactly once over caller-owned
  static buffers;
- proves the one-page allocation is arena-confined and the free/double-free
  accounting is terminal and exact before success; and
- emits the existing nine-line stage-two marker plan only after all
  postconditions pass.

The new path is off by default and requires the M01 loaded-image/owned-stack
probe. It does not change the ordinary boot-marker sequence.

This step accepts source-contract, complete host-harness, no-stdlib NativeAOT
publish, PE, linker-map, and disassembly evidence. It deliberately does not
claim guest execution. M01-024 owns the separate QEMU/OVMF proof.

## Why

M01-018 provided an audited no-stdlib allocator coordinator over
caller-attested metadata, and M01-022 proved that the live EFI path could reach
a final continuation on the owned stack. The next smallest boundary was to
bind those two pieces without importing arbitrary conventional memory or
misrepresenting build success as live guest evidence.

The adapter treats an explicit EFI `AllocatePages` success as the only source
of arena ownership, then independently validates the returned geometry and the
post-allocation map. This keeps the allocator confined to one bounded
pre-`ExitBootServices` loader-owned allocation. The persistent output is
one-shot: all 312 bytes must be zero before the firmware call, and a completed
or partially initialized state is rejected rather than erased. Before success,
the complete returned state is byte-compared with the caller-owned durable
state.

The post-allocation negative cases were independently authored after reviewing
`references/RoseOS-master/Loader/src/MemoryMap.cs` as an owner-authorized,
test-only influence. The local snapshot SHA-256 is
`CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28`.
Its exact-size allocation, single-slack attempt, continue-after-error behavior,
unchecked narrowing, and cleanup hazards informed rejection tests. No RoseOS
implementation source or API was copied into first-party CLROS code.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
  - captures the boot-services pointer before the stage-one stack transfer;
  - validates the exact final stage-one state through a type-erased bridge;
  - adds the default-off stage-two adapter and disabled stub;
  - performs one bounded loader-data arena allocation;
  - reuses the existing static map source/owned-copy buffers;
  - builds and validates the five-record canonical ledger;
  - invokes the reviewed no-stdlib coordinator once with exact
    pointer/count/capacity arguments;
  - requires exact persistent/result state equality and final allocator
    accounting before retrospective success markers; and
  - centralizes one matching `FreePages` attempt for every failure after EFI
    grants ownership.
- `tests/OS.HostTests/M01LiveAllocatorEfiEntryTests.cs`
  - adds ten focused source/profile/marker/firewall/mutation slices;
  - locks exact bridge delegation, stage-one input propagation, firmware-call
    order, arena geometry, map recapture, ledger construction, coordinator
    arguments, durable-state one-shot behavior, and failure suppression; and
  - emits `CLROS_M01_023_READY`.
- `docs/boot.md`
  - records the M01-023 adapter, ordering, failure contract, reference
    influence, and strict no-guest-evidence boundary.
- `docs/efi-build.md`
  - records the dual-probe command, artifact/linkage evidence, hashes, frame
    audit, and M01-024 handoff.
- `done/step871.md`
  - preserves this evidence.

No change was made to `src/OS/OS.csproj`, `BootSequence.cs`,
`BootSequencePlan.cs`, `Probes.cs`, any build/QEMU script, `README.md`,
`docs/completion-gates.md`, or `references/**`.

## Tests run

- `dotnet build .\src\OS\OS.csproj --nologo -m:1
  --disable-build-servers
  -p:EnableM01LoadedImageStackProbe=true
  -p:EnableM01LiveAllocatorProbe=true`
  - passed with zero warnings and zero errors during implementation review.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
  --disable-build-servers`
  - passed with zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - exit code `0`;
  - exactly one `TEST_START m01.023`, `CLROS_M01_023_READY`,
    `TEST_PASS m01.023`, and `TEST_PASS host.tests`;
  - all ten focused M01-023 slices passed;
  - zero `TEST_FAIL` lines;
  - stderr length `0`;
  - accepted stdout:
    `artifacts/m01-023/host-tests-final.stdout.log`;
  - stdout length: 819,024 bytes;
  - stdout SHA-256:
    `4ECD25E46DB2EB41CC36944165EF67EE9EF1B74CEBB0797E59DF4DCC4BEDA60C`.
- `dotnet build .\CLROS.slnx --nologo -m:1 --disable-build-servers`
  - passed with zero warnings and zero errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - passed `TEST_PASS qemu.runner.dryrun`.
- Allocator-only EFI preflight:

  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass -File `
    .\scripts\build-efi-shell.ps1 `
    -EnableM01LiveAllocatorProbe
  ```

  - returned the expected exit code `1`;
  - emitted
    `reason=live_allocator_requires_loaded_image_stack`; and
  - did not publish an invalid profile.
- Canonical dual-probe EFI build:

  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass -File `
    .\scripts\build-efi-shell.ps1 `
    -EnableM01LoadedImageStackProbe `
    -EnableM01LiveAllocatorProbe
  ```

  - exit code `0`;
  - emitted both exact `EFI_PROBE` lines;
  - passed `TEST_PASS efi.artifact.verify`;
  - passed `TEST_PASS efi.shell.publish`;
  - reported PE32+ x64 machine `0x8664`, EFI application subsystem `10`,
    entrypoint association with same-build `EfiMain`, and no ordinary, bound,
    IAT, or delay-import directory.
- `git diff --check`
  - passed after implementation and documentation edits.

## Evidence

The accepted tool tuple was:

- .NET SDK `10.0.302`;
- MSVC linker `14.51.36252.0`;
- Windows SDK `10.0.28000.0`;
- ILCompiler `10.0.8` (`10.0.826.23019`);
- NativeAOT runtime pack `10.0.8`;
- QEMU/qemu-img `11.0.50`
  (`v11.0.0-12631-g54e84cdc7a`); and
- OVMF length 3,653,632 bytes, SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

The final build artifacts were:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `artifacts/efi/BOOTX64.EFI` | 208,384 | `620C37CC6CF517668A84F760CAC19241EB2FFBB6BB8A81B97B2CDA95AC063B44` |
| `artifacts/efi/BOOTX64.EFI.link.json` | 1,817 | `4D2486A4F3E48FC66480B82B591ACBE0604A41144896FAFFC06FD1AE227A288B` |
| `artifacts/efi/BOOTX64.EFI.link.map` | 93,728 | `97FD2283DA9C62AFEE4CB02B17B8BF340DC7827299D279866F688C4C86E4DBD9` |
| `artifacts/efi/BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `src/OS/obj/Release/net10.0/win-x64/native/OS.obj` | 710,442 | `55BCEEDBA5DCD8A1F9057BB5787FD3CDF153783149D7CA16BE8CC3AA378C9358` |

The final linker map contains:

- `M01LiveAllocatorIntegration__Execute`;
- `M01StageTwoAdapter__RunAfterStageOne`;
- `M01StageTwoAdapter__IsZeroedStaticState`;
- `M01StageTwoAdapter__StaticStatesEqual`; and
- `M01StageTwoAdapter__ValidateCompletedResult`.

Final `dumpbin /disasm:nobytes` output is preserved at
`artifacts/m01-023/OS.obj.disasm.txt`. It shows:

- `RunAfterStageOne` pushes eight nonvolatile registers and reserves `0xF38`
  bytes (3,960 bytes including pushes);
- `ValidateCompletedResult` pushes eight nonvolatile registers and reserves
  `0xED8` bytes (3,864 bytes including pushes); and
- `RhpStackProbe` remains a frameless fail-closed dispatch bridge that loads
  its target through `RAX`, tests it, tail-jumps through the installed target,
  and loops when no target is installed.

Both measured stage-two frames remain well inside the already proven
33-page/135,168-byte owned stack. This disassembly evidence is specific to the
reported NativeAOT object and tool tuple.

Two independent read-only reviews found no remaining merge-blocking production
or focused-test issue after corrections. In particular, review verified
one-shot durable state, exact persisted/result binding, centralized cleanup,
terminal failure behavior, no cross-enum coordinator status, no retry before
M03, and preservation of the no-stdlib source boundary.

## Failures

- The first exploratory host run found four focused source-test defects:
  mixed-line-ending-sensitive guard matching, two call-order searches that
  matched function-pointer availability checks instead of real calls, and a
  stale by-value result token. The source was normalized and the tests now
  locate the exact calls and pointer form.
- A reviewer-started full harness overlapped an exploratory parent run and
  caused a transient shared `bin/obj` copy failure in an unrelated M01-009
  subprocess. Both overlapping runs were discarded; no accepted evidence uses
  them.
- The initial mutation used
  `BypassStageTwoMapContainsAllocation`, which still contained the protected
  method-name substring. It was changed to `BypassMapAllocationCheck`, and the
  complete representative-mutation slice now passes.
- Independent production review found and corrected a real early defect:
  entry code had cleared durable state before each coordinator call, which
  could bypass duplicate-phase protection and leak a prior arena. The final
  path requires all 312 bytes to be zero before allocation and never clears a
  completed state on entry.
- Review also found that a map-recapture adapter status was being written into
  the allocator coordinator-status field and that final validation did not
  compare every durable field. The final path leaves coordinator status
  `Uninitialized` before the coordinator and performs exact 312-byte
  persisted/result comparison.
- A healthy but superseded harness was stopped after the final test-review
  strengthening landed; it was not accepted as evidence. The rebuilt
  authoritative run then passed in full.

No QEMU guest run was attempted or accepted for M01-023.

## Deferred work

- M01-024 must rebuild and execute the dual-probe artifact under real
  QEMU/OVMF, require the complete stage-one and stage-two sequences exactly
  once and in order, reject both stage failure markers, and preserve serial,
  artifact, tool, and source-state hashes.
- The current adapter fails closed on a grown/too-small post-allocation map and
  does not retry. Bounded fresh-map/stale-key retry belongs to the later real
  `ExitBootServices` milestone.
- The arena is retained only in the opt-in successful pre-EBS state. This step
  does not establish ownership after firmware exit or a general allocator API.
- No physical page content was read, written, zeroed, mapped, or protected by
  this issue.
- Guard-page enforcement, RW-to-RX transition, paging, CR3/TLB operations,
  NX/W^X, owned COM1, descriptors/fault handling, PCI, storage, networking,
  application execution, hosted .NET, Kestrel, AWS, Azure, and production
  readiness remain unproven.
- `docs/completion-gates.md` remains unchanged.
