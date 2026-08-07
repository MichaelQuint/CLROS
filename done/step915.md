# Step 915 - M02-008 guarded IST arena execution in the EFI guest

## Summary

Integrated the reviewed M02-008 guarded-IST arena contract into the explicit
no-stdlib EFI guest path. The opt-in cumulative profile allocates six temporary
UEFI LoaderData pages, constructs the exact three guard/stack layouts, writes
and reads bounded sentinels only inside the stack ranges, releases the lease,
and frees the same allocation before returning. It emits one exact
`CLROS_M02_008_READY` marker on success and fails closed with
`M02_GUARDED_IST_STACK_ALLOCATION_FAILED` otherwise.

## Why

The reviewed host contract in issue #48 proved allocation-free layout and lease
semantics, but it did not prove that the explicit EFI guest could allocate real
guest memory, execute that contract, touch only the intended stack ranges, and
release the firmware allocation. Supplemental issue #467 owns this serialized
EFI/project/script integration without re-reviewing or modifying the accepted
host contract.

## Files changed

- `src/OS/Boot/EfiEntry.cs`: passes the live boot-services pointer explicitly
  through the stage-three writer and executes the guarded-IST allocation,
  validation, stack readback, lease release, cleanup, and exact evidence line.
- `src/OS/OS.csproj`: adds the default-false M02-008 switch, dependency guard,
  define, and exact guarded-IST compile item after M02-007.
- `src/OS/Runtime/MinimalRuntime.cs`: provides the no-stdlib-compatible
  `UInt64.MaxValue` constant required by the reviewed overflow checks.
- `scripts/build-efi-shell.ps1`, `scripts/test-efi-artifact.ps1`, and
  `scripts/run-qemu-smoke.ps1`: propagate the switch, pin the 57-item/70-input
  closure, enforce cumulative dependencies, and own the exact 56-success /
  40-failure guest marker policy.
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`: adds focused
  source, ordering, marker, QEMU-policy, immutable-input, and documentation
  integration checks.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs` and
  `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`: update exact
  source/firewall/property fixtures for the new closure.
- Historical EFI integration tests for M01-025, M01-035, M02-003 through
  M02-007, M02-017, and M04-002: preserve their exact source/ordering checks
  across the explicit boot-services argument and new
  `GDT -> guarded IST -> endian catalog` compile order.
- `README.md`, `docs/efi-build.md`, and `docs/qemu.md`: document the implemented
  behavior, exact evidence, unsupported behavior, and tracker progress.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`: pass,
  zero warnings and zero errors.
- Focused M02-008 integration project: all five cases pass and terminal
  `TEST_PASS m02.008.efi.integration`.
- Focused regression projects: M01-025, M01-035, M02-005, M02-006, M02-007,
  M02-017, and M04-002 all pass after their exact-source fixtures were updated.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  pass with 3,446 `TEST_START`, 3,447 `TEST_PASS`, zero `TEST_FAIL`, one
  `TEST_PASS m02.008`, one `TEST_PASS m02.008.efi.integration`, and exactly one
  terminal `TEST_PASS host.tests`.
- `dotnet build .\CLROS.slnx --nologo -m:1`: pass, zero warnings/errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`: `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- PowerShell AST parsing for the three changed scripts: pass.
- `git diff --check`: pass.
- Real QEMU/OVMF cumulative profile with `-BuildEfi`, every required M01/M02
  switch through `-EnableM02GuardedIstStackAllocationProbe`, and
  `-RequireMarker CLROS_M02_008_READY`: pass.
- The already-built artifact was also rerun with issue #48's literal real-QEMU
  command and required marker: pass without rebuilding a different source.

## Evidence

- Exact compile closure: 57 compile items; SHA-256
  `39395b3906e8875aad6c08386e807d0c9568780091fe2401b69d121ca2454b83`.
- Exact source authority: 70 inputs; no imports.
- Canonical evaluated `OS.csproj` shape SHA-256:
  `ab9195a9121f99096fd5bdde228e75d01a1c375303a1cc14379d23ef8487f7a7`.
- EFI artifact: 517,632 bytes; SHA-256
  `58464cd09ece7ce392252588d5c3327ccf64cfe19af4087454292e6646366d05`.
- Serializer object SHA-256:
  `da97e50c95e479541b65eb3f96d9c6ded163735857650bee9416324086d55c7b`.
- Link map SHA-256:
  `6a20fe926cd905b8c6ef36a13ce7f21015145d9c91627209d21a22a82461e125`.
- Link response SHA-256:
  `207b4463f431847537556846662fc25fbf5ed26fd50713267c06c81808a47139`.
- Real QEMU streaming result: `status=Passed`, `bytes=4054`, `lines=78`,
  `orderedSeen=56`, `outOfOrder=0`, `uniqueSeen=56`, `duplicateUnique=0`.
- Serial log SHA-256:
  `96eaea7e7126332514b3ffa1316152e7471d5b67a82e20839f18d3ed620f865c`.
- QEMU summary SHA-256:
  `20ed7ae4664677a016f4071d6be17c45c60c868e1be10286e095401b183f366b`.
- Final host-test log SHA-256:
  `461c8639ca87dfa4508a578769d0534c44303cad65b76e7aac3d363a6f3f76e3`.
- Exact guest success line:

  ```text
  CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 pageFaultIst=2 nmiIst=3 writableMetadata=true executableMetadata=false allocationFree=true guestArenaAllocated=true guestStackWrites=true guestGuardMappings=false guestTss=false guestIdt=false arenaFreed=true markerTransport=uefiSerial postExitBootServices=false
  ```

## Failures

- The first no-stdlib compile exposed that the reviewed host contract used
  `ulong.MaxValue` while the explicit EFI `UInt64` surface did not define it.
  Issue #467 ownership was expanded before adding the exact constant; the
  source firewall permits that constant only.
- Early full-host attempts correctly stopped on stale exact fixtures: a hostile
  source used the newly valid `ulong.MaxValue`, the artifact schema retained a
  56-item count and omitted the new post-restore property, M01-025/M01-035 and
  M02-017 expected the old three-argument stage-three writer, and historical
  M02/M04 integration tests expected the endian catalog immediately after the
  GDT. Each fixture was corrected to preserve its original semantic boundary;
  the verifier and production behavior were not weakened.
- Every source/test correction was followed by focused proof and a complete
  from-the-beginning host rerun. Only the final zero-failure run is accepted.

## Deferred work

- Guard ranges remain intent metadata in firmware mappings; they are not made
  non-present, and NX is not applied to active page tables.
- No TSS, IST, GDT, or IDT is installed; no stack switch or fault is triggered.
- The arena is temporary pre-`ExitBootServices` UEFI memory and is freed before
  return. This is not post-firmware diagnostics or real `ExitBootServices` proof.
- Physical hardware, Azure/AWS, CloudReady, hosted .NET, Kestrel, and production
  readiness remain unproven and are governed by `docs/completion-gates.md`.
