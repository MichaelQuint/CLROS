# Step 993 - Preserve M01 source checks with the typed EBS probe disabled

The full regression at `fb4fd396fd7420f58c8f34c46f5cd9bbacee1b25` stopped in
M01-023 because its partial preprocessor left the new opt-in EBS callback in
the simulated default profile. The actual default compiler profile excludes
that callback. The four existing M01 source selectors now recognize
`M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE` as disabled. Existing source-firewall
assertions and test groups remain intact; applicable synthetic disabled-symbol
controls include the new symbol.

Changed files: `M01LoadedImageStackEfiEntryTests.cs`,
`M01LiveAllocatorEfiEntryTests.cs`, `M01LivePagingEfiEntryTests.cs`, and
`M01InstructionPublicationEfiEntryTests.cs` under `tests/OS.HostTests`.
No production source changed in this correction.

Validation: the focused harness under `artifacts/issue546-m01-compat/focused`
compiled with zero warnings/errors and ran all four complete initializers:
51 starts, 52 passes, zero failures, and the `TEST_PASS host.tests` terminal.
Commands were `dotnet build artifacts/issue546-m01-compat/focused/Focused.csproj
--nologo -m:1` and `dotnet run --project
artifacts/issue546-m01-compat/focused/Focused.csproj --no-build`.
Build/stdout/stderr logs are retained beside the harness. `git diff --check`
passed.

The failed full run is preserved at
`C:/Projects/CLROS/artifacts/orchestration-20261001/issue546-common-gate-fb4fd39/run`.
It ran from 2026-10-05 06:13:19 to 07:07:38 UTC; tracked inputs and launch
binaries remained unchanged. It did not complete and is not passing common
evidence. A complete regression rerun and fresh default/controlled QEMU
captures remain necessary before PR545 and issues #61/#546 can close.

This correction adds no real firmware exit, runtime, cloud, or production proof.
