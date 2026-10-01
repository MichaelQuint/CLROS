# Step 928 - Arithmetic EFI adapter and reviewed integration contracts

## Summary

Supplemental #507 implements the default-off M04-004 arithmetic EFI adapter
over the frozen host catalog merged in PR506. It has focused host, no-stdlib
metadata, default/enabled NativeAOT publish, same-build verifier, and independent
review evidence. This step does not claim fresh real-QEMU acceptance; #507 and
parent #73 remain open pending the settled integration wave and guest tuple.

## Why

The 57-vector host catalog cannot establish NativeAOT guest execution. The
separate serialized EFI lane links that exact source, preserves the source
firewall, and propagates an explicit opt-in profile through project, build,
verifier, and smoke layers. Independent status/count/index/fingerprint checks
prevent a partial or forged result from enabling downstream boot markers.

## Files changed

- `src/OS/OS.csproj`: default-false property/symbol, explicit catalog link,
  M04-003 prerequisite, and M02-009/M02-013 terminal exclusions.
- `src/OS/Boot/EfiEntry.cs`: returning pre-map adapter and fixed-byte marker
  writers; no new BootSequence phase or dangerous default.
- `src/OS/Runtime/MethodImplAttribute.cs`: standard NoInlining enum flag only.
- `scripts/build-efi-shell.ps1`, `scripts/test-efi-artifact.ps1`, and
  `scripts/run-qemu-smoke.ps1`: exact source/property propagation, validation,
  68-compile-item/81-source-input closure, and strict profile requirements.
- `tests/OS.HostTests/M04ArithmeticConformanceEfiIntegrationTests.cs`: source
  identity, actual no-stdlib metadata, compiled adapter, profile rejection,
  and controlled serial tests.
- Existing `tests/OS.HostTests` fixture/closure updates:
  `BuildEfiShellPreflightTests.cs`, `EfiArtifactVerificationScriptTests.cs`,
  `M02SafeStubPageEfiEntryTests.cs`,
  `M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`,
  `M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`,
  `M02OwnedCom1ReadyEfiIntegrationTests.cs`,
  `M02PanicSafeOwnedCom1EfiIntegrationTests.cs`,
  `M02OwnedX64GdtEfiIntegrationTests.cs`,
  `M02GuardedIstStacksEfiIntegrationTests.cs`,
  `M02OwnedGdtTssTerminalEfiIntegrationTests.cs`,
  `M02NormalizedInterruptFramePageEfiIntegrationTests.cs`,
  `M02OwnedIdtGatePopulationEfiIntegrationTests.cs`,
  `M02IdtrContinuedExecutionEfiIntegrationTests.cs`,
  `M02Ud2VectorStateEfiIntegrationTests.cs`,
  `M04IndependentEndianConformanceEfiIntegrationTests.cs`, and
  `M04SpanMemoryConformanceEfiIntegrationTests.cs`.
- `docs/efi-build.md`, `docs/qemu.md`, `docs/nativeaot-nostdlib-limits.md`,
  `docs/completion-gates.md`, and this allocated step: scope and evidence.

The original arithmetic production and host-test Git content, MinimalRuntime,
BootSequence, Probes, and runner production source remain unchanged.

## Tests run

From `C:/Users/Roger/.codex/worktrees/m04-004-efi/CLROS`:

- Enabled `dotnet build src/OS/OS.csproj --nologo -m:1` with all three M04
  properties true: passed, zero warnings/errors.
- Focused Release integration harness: all six final groups passed, including
  LF/CRLF source identity, actual-shim NoInlining metadata with no framework
  references, exact compiled adapter output and five rejected results,
  build/verifier bad profiles, QEMU dry runs, and controlled serial failures.
- 51 selected existing regression checks passed. The artifact-only harness
  strips module initializers from copied test fixtures and invokes selected
  source/fake-firmware tests; it is explicitly not the full host suite.
- Enabled EFI preflight accepted 68 compile items and 81 source inputs.
- Fresh enabled and default EFI publishes and same-build verification passed.
- Naming/source firewall and whitespace checks passed.
- Independent reviewer reran the original five focused groups, verified the
  final six-group rerun log and correction, audited both artifact tuples and
  all 81 source hashes, and found no unresolved implementation issue.

Parent copied all 23 reviewed code/script/test files byte-for-byte into the
integration branch and verified every copy hash. The later complete settled
host gate and real QEMU run are not claimed by this step.

## Evidence

Author logs and commands are under `artifacts/issue507/` in the worktree:
`implementation-report.md`, `evidence-hashes.json`, `compile-enabled.log`,
`focused-normalized-source.log`, `regression.log`, and build stdout logs.
The final focused log SHA-256 is
`426D7C194183D1A936F029FE103355A4EBFAFC01145B1EDBEEDB703AC4552A90`;
the new integration test's raw SHA-256 is
`3B176FEE75659EE27D83B429255DF4FE06F02271D36BD8F7CB7AA004903DB0B4`.

Default artifact `artifacts/efi/BOOTX64-issue507-default.EFI`: 55,808 bytes,
SHA-256 `EBAC4F464E5E9E21ECB8830D57DD15BEA4FC9705B5CD1B5AD1F8F67F1FFA77FD`.
Enabled artifact `artifacts/efi/BOOTX64-issue507-enabled.EFI`: 89,088 bytes,
SHA-256 `621969060496EF58853FAB6799014A7E1D00A8910C13FC56B0083ECA85020109`.
Both are PE32+ x64 EFI applications with ordinary/bound/IAT/delay imports
absent. Schema-three sidecars record all three M04 properties false or true
respectively, with artifact/map/response/source identities independently checked.

Ordered compile-key SHA-256:
`2ED2F5CA764F944EC8BAB4C7ADBBCB80A4FAD03448FEB13FF33F898911B307EC`.
Raw source-authority aggregate:
`A9748677402078D5407B80B93779A03F82833DC639EAEBC4ABE8A7266CCB6454`.
The minimal profile has 17 required, five ordered, three unique, and five
forbidden markers. The exact ready/failure lines and dependencies are in the
canonical EFI/QEMU docs. Generic runner success still permits a space-delimited
diagnostic suffix; issue acceptance additionally requires ordinal-equal complete
guest records, exact counts/order, and no alternate same-prefix or failure lines.

## Failures

- Initial publish produced CS0117 because the actual no-stdlib MethodImplOptions
  shim lacked NoInlining. Adding its standard `0x0008` flag fixed compilation;
  no extra helper, runtime library, or framework dependency was required.
- Review caught a raw-LF source-identity assertion that would reject the root
  checkout's equivalent mixed CRLF/LF arithmetic files from step925's formatting
  cleanup. The new test now normalizes CRLF to LF only; regression checks reject
  changed content, spaces, or lone CR bytes. Raw artifact/source-authority
  verification was not weakened.
- Before the settled gate, parent independently verified the normalized hashes
  and restored only its own two earlier arithmetic working files to LF. Their
  before hashes were `B9916F8F3404CF46493AA4D4A1882DE97BF82936F6C420B9DF4D743367425F5D`
  and `1BB854C00D09B2FFE2FFC85A8B6DF698F562EA88138C937B8645C0F63BA44794`.
  Their after hashes equal the frozen Git LF identities:
  `29397B76B8F7F6437CB5A2B54643CFAB824C4A403CDBFAC9CF5746F6EE9A23EB`
  and `DB7A8DA32A3D3988EC97D47DB306F7428F66C4BC94E0D94F3339E63FE4C93D72`.
  Git content diff remained empty. This preserves the exact raw closure used
  by the independently reviewed artifact/fixture evidence.

## Deferred work

- One complete settled-source solution/host gate in the parent branch; no
  concurrent canonical EFI publishing during the aggregate suite.
- Fresh default/enabled artifacts and real QEMU/OVMF acceptance from clean,
  committed source, with issue/head-specific paths, raw exact-line validation,
  tool versions, timestamps, hashes, serial and runner summaries.
- Review the complete tuple, merge the PR, post/read back evidence, then close
  #507 and #73 only if their acceptance is satisfied. Gate 4 stays Fail.
- General integer/runtime compatibility, overflow/divide exception delivery,
  GC/allocation, hosted .NET/Kestrel, firmware exit, and cloud gates are separate.

## Evidence boundary

This step proves a reviewed implementation and host/build evidence only.
The evaluator retains its documented conservative min/-1 rejection for both
division and remainder, rather than claiming CLR remainder exception semantics.
The M02-014 host leaf in step927 is disjoint and remains host-only. The complete
backlog goal stays active.
