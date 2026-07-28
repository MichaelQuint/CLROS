# Step 886 - Link and verify the immutable instruction-publication serializer

## Summary

Completed M01-034 as a static build, link, and final-artifact boundary. The
reviewed C# 14/.NET 10 AMD64 COFF generator is now a canonical `CLROS.slnx`
host tool. The no-stdlib EFI project imports one audited targets file, creates
and links the exact 336-byte serializer object, retains exactly one
`CLROS_X64_SerializeInstructionPublication` symbol, and exposes it through a
direct parameterless `uint` `RuntimeImport`/`InternalCall` binding with no
managed body or fallback.

The accepted schema-3 artifact verifier resolves the same-build map symbol to
the final PE, proves the exact 20-byte serializer body and fixed
`0x53524C43` cookie, and requires exact x64 `RUNTIME_FUNCTION` and
`UNWIND_INFO` coverage. The serializer is file-backed, initialized, readable,
executable, and nonwritable. Its unwind data is file-backed, initialized,
readable, nonwritable, and nonexecutable. Ordinary import, bound-import, IAT,
and delay-import directories remain absent.

This is host/build evidence only. `EfiEntry.cs` is unchanged and the serializer
is not invoked. No QEMU or hardware execution occurred, no success cookie was
observed at runtime, and no completion gate advanced.

## Why

M01-025's generated-code path could not use a serialization instruction inside
a later generated stream to serialize the first fetch of the earlier stream.
M01-033 therefore produced a small immutable serializer as a deterministic
COFF object. M01-034 had to integrate that object into the canonical EFI link
and establish its exact final-image identity before M01-035 can place checked
calls at the four current instruction-publication epochs.

Link success alone was insufficient. A stale object, discarded or fallback
symbol, writable code, malformed unwind record, hostile MSBuild import,
mutable package input, output replacement race, escaped process tree, or
wall-clock-dependent link could all create a superficially successful output.
The implementation therefore freezes the causal source/package/tool inputs,
isolates and bounds external build processes, binds final output identities,
and makes the independent verifier reconstruct the relevant associations.

## Files changed

- `CLROS.slnx`
  - Includes `tools/OS.X64TextSerializerGenerator` exactly once.
- `src/OS/OS.csproj`
  - Adds the exact three no-stdlib binding sources, bringing the ordered
    compile closure to 44 items.
  - Imports exactly one literal serializer targets file.
  - Pins the selected NativeAOT compiler/runtime roots and requires `/BREPRO`.
- `src/OS/Boot/X64InstructionPublication.cs`
  - Declares the direct static extern binding and expected success cookie.
- `src/OS/Runtime/RuntimeImportAttribute.cs`
  - Supplies only the required two-string no-stdlib metadata constructor.
- `src/OS/Runtime/MethodImplAttribute.cs`
  - Supplies only the required `InternalCall = 0x1000` metadata shape.
- `tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets`
  - Builds the package-free host generator in an isolated child, validates its
    exact closure, generates the ephemeral object, registers it after runtime
    library removal, retains its symbol, emits map/response evidence, and
    participates in normal clean tracking.
- `tools/AGENTS.override.md`
  - Records the host-tool and EFI-link integration boundary.
- `scripts/build-efi-shell.ps1`
  - Pins the exact 57-source closure, five restore archives, six local restore
    inputs, five generated restore inputs, six package trees, nine active
    package build inputs, SDK/toolchain paths, and post-restore item closure.
  - Runs every owned external build/evaluation process inside a
    startup-gated, kill-on-close Windows Job Object with bounded output,
    timeout, node reuse disabled, and confirmed descendant termination.
  - Requires single-link, distinct Windows identities for all 57 source
    inputs and holds their read handles through verification.
  - Publishes only exact ordinary output leaves with create-new semantics.
  - Holds the log through one identity-bound handle.
  - Holds and revalidates eight distinct final verifier inputs through
    verifier completion.
  - Emits schema-3 link evidence.
- `scripts/test-efi-artifact.ps1`
  - Independently validates schema-3 source, restore, package, task/import,
    toolchain, managed assembly, map/response/object, PE, exception-directory,
    runtime-function, unwind, section-permission, and import contracts.
  - Pins the exact canonical project, entry source, and 57-source closure while
    allowing copied artifact bundles.
  - Uses a verifier-local suspended-create/job-assign/resume process runner for
    its MSBuild reevaluation, with strict UTF-8 bounded output, timeout,
    descendant cleanup, and stable failure classifications.
  - Accepts legitimate zero-byte pinned package members while still requiring
    their exact path, length, hash, identity, and tree accounting.
- `tests/OS.HostTests/M01InstructionPublicationBuildIntegrationTests.cs`
  - Decodes a freshly built managed assembly and validates the exact binding.
  - Adds 16 stable-code hostile managed-contract cases.
  - Covers generator lifecycle, clean, incremental, spaces, parallel, link,
    map, response, object, and no-invocation contracts.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
  - Covers exact project/import/source/package/tool authority, hostile
    MSBuild inputs, bounded processes, process-tree cleanup, output/log/source
    identity, hardlinks, reparse points, mutation attempts, and safe cleanup.
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
  - Replaces the stale schema-2 fixture with a disposable schema-3 repository,
    a real offline restore, exact closure parity, hostile PE/evidence/package
    cases, canonical source-path negatives, and bounded-runner behavior.
- `docs/efi-build.md`, `docs/nativeaot-nostdlib-limits.md`,
  `docs/boot.md`, and `docs/security.md`
  - Record the implemented static evidence and preserve all execution,
    QEMU, paging, gate, cloud, and production nonclaims.

No forbidden path changed. In particular, this work did not modify
`src/OS/Boot/EfiEntry.cs`, the generator C# implementation, QEMU tooling,
`README.md`, `docs/architecture.md`, `docs/completion-gates.md`, or any
reference snapshot. No first-party `.c`, `.cpp`, `.h`, `.asm`, or `.s` file
was added.

## Security and authority contract

- The canonical source closure contains exactly 57 mutually distinct,
  single-link files. Producer and verifier walk existing ancestors from the
  volume/share root, reject reparse points, bind pathname to Windows file
  identity, hold `FileShare.Read` handles, and rehash/reprobe before success.
- Restore uses five exact SHA-256-pinned archives copied to one fresh local
  source with ordinary, fallback, and audit sources cleared.
- The package closure contains six exact package/version trees, 900 stable
  package files, six metadata records, nine active target/task inputs, five
  import edges, and two task bindings. Package/tool inputs are reparse-checked,
  content-bound, and mutation-locked; they are not claimed to have globally
  unique file identities.
- SDK, extensions, user extensions, Common.props/Common.targets/CSharp/
  cross-targeting wildcard imports, shared compilation, MSBuild server reuse,
  and node reuse are explicitly controlled.
- The five publication leaves are create-new ordinary files. EFI, map,
  response, evidence JSON, preserved object, linked object, linked map, and
  managed assembly must be eight distinct single-link identities and remain
  locked through verification.
- `/NODEFAULTLIB`, exactly one `/BREPRO`, exact force-include/map arguments,
  and rejection of every `/FORCE` form prevent unresolved/fallback acceptance.
- The standalone verifier's reevaluation child is created suspended, assigned
  to a kill-on-close Job Object, and resumed only after assignment. Timeout,
  overflow, strict-decoding capture failure, nonzero exit, job-query failure,
  or unconfirmed descendant termination fails closed.

## Accepted tests and evidence

The final aggregate host harness completed without intervention:

- Command:
  `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`.
- Start UTC: `2026-07-28T22:37:35.9859818Z`.
- End UTC: `2026-07-28T23:14:14.3430668Z`.
- Elapsed: `00:36:38.3570850`.
- `TEST_START` count: 3,295.
- `TEST_PASS` count: 3,296.
- `TEST_FAIL` count: zero.
- Exact aggregate marker count: one `TEST_PASS host.tests`.
- Stdout:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-034-final4-fb9e0c6c606247ecbfe77cfff51e083a\host-tests-final-accepted2.stdout.log`;
  421,367 bytes; SHA-256
  `E3BF04052842AD31C5763C3860EFE8E9959750F77987CA4DBEDE03FA4B51C0EF`.
- Stderr:
  `C:\Users\Roger\AppData\Local\Temp\clros-m01-034-final4-fb9e0c6c606247ecbfe77cfff51e083a\host-tests-final-accepted2.stderr.log`;
  zero bytes; SHA-256
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

The issue-specific aggregate markers were:

```text
TEST_START m01.034
CLROS_M01_034_MANAGED_ABI_NEGATIVE_PASS cases=16
CLROS_M01_034_READY objectBytes=336 objectSha256=DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B successCookie=0x53524C43
TEST_PASS m01.034
```

The maximum hostile verifier fixture remained bounded independently of
process startup and teardown:

```text
EFI_ARTIFACT_PERFORMANCE distinctUnwindRecords=2048 processElapsedMs=20179 scanElapsedMs=189 processBudgetMs=30000 scanBudgetMs=1000
```

The following final-source checks also passed:

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  - Zero warnings and zero errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard`.
  - `TEST_PASS source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - `TEST_PASS workload.profile.tooling default single`.
  - `TEST_PASS qemu.runner.dryrun`.
  - This is command/marker planning evidence, not a guest boot.
- `git diff --check`
  - Exit zero.
- PowerShell 5.1 parsing for both EFI scripts.
- XML parsing for `CLROS.slnx`, `src/OS/OS.csproj`, and the serializer targets.
- Real `-PreflightOnly`
  - `EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=57`
  - exact 44 compile items;
  - pinned .NET SDK 10.0.302, linker, Windows SDK, ILCompiler, QEMU,
    qemu-img, and OVMF discovery;
  - `EFI_PREFLIGHT_OK`.
- Canonical all-three-probe publish
  - one final-source accepted run after the last verifier hardening;
  - nested `TEST_PASS efi.artifact.verify`;
  - `EFI_PUBLISH_VERIFIER_INPUT_AUTHORITY status=ok inputs=8`;
  - `EFI_PUBLISH_VERIFIER_INPUTS_UNCHANGED status=ok inputs=8`;
  - `TEST_PASS efi.shell.publish`.
- Standalone verifier
  - exit zero;
  - `TEST_PASS efi.artifact.verify`;
  - 804 runtime-function records and 619 distinct unwind records;
  - final runtime scan 115 ms.
- Reproducibility
  - the final-source publication was compared with the previously preserved
    two-run bundle;
  - all seven deterministic outputs compare byte-for-byte equal: EFI, map,
    response, preserved object, linked object, linked map, and managed
    assembly;
  - the evidence JSON is intentionally excluded from the byte comparison
    because it records the final verifier source hash and generation time.

## Accepted artifact evidence

Accepted final-source schema-3 bundle:

`C:\Users\Roger\AppData\Local\Temp\clros-m01-034-final4-fb9e0c6c606247ecbfe77cfff51e083a\accepted-final-source-schema3-output`

- Link-evidence schema: `3`.
- Generated UTC: `2026-07-28T23:15:19.1342999+00:00`.
- Link-evidence JSON:
  - 392,820 bytes;
  - SHA-256
    `D40FCD847855EC59971FA9E1395A23B0E3804D2079973E3D1DFEC6388BC2A302`.
- `BOOTX64.EFI`:
  - 456,704 bytes;
  - SHA-256
    `9A886C748EE8F6FFB5FF87005F5CD7EE1B6C19911186F52A93FEA433B427C37B`.
- Link map:
  - 200,398 bytes;
  - SHA-256
    `144C9FED566D7933BA97CF5D5D679EB59C3CA0A505BD27D6F4281B3487A8E6F5`.
- Link response:
  - 492 bytes;
  - SHA-256
    `0C36F982F5504237917C9430FBDE3286CBDEA8ADE55B3263E1DFA2211A01BF47`.
- Preserved and linked serializer objects:
  - 336 bytes each;
  - SHA-256
    `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
- Managed `OS.dll`:
  - 537,600 bytes;
  - SHA-256
    `87FF0EA20E0D209FF543A77ED2FC1289368525A8A412E65E8055088CA45F3C0F`.

Final PE:

- PE32+, AMD64 `0x8664`, EFI application subsystem `10`.
- `EfiMain` RVA `0x0004AC50`.
- Import, bound-import, IAT, and delay-import directories absent.
- Exception directory:
  - section `.pdata`;
  - RVA `0x001A5000`;
  - 9,648 bytes;
  - 804 sorted, nonoverlapping records.
- Serializer:
  - section `.text`;
  - RVA `0x00001000`;
  - 20 bytes;
  - SHA-256
    `DF76C8E39E0768D26EA27206FF102D7EADCC35D2C9BDF547B52B1171FD47BDAF`;
  - unwind RVA `0x0006EFE0`;
  - expected cookie `0x53524C43`.

Closure identities:

- Source inputs: 57.
- Source aggregate SHA-256:
  `B758A14A51AAB8478E9384E267C853D0DD15F1D0B2A160DAF296718B524AC591`.
- Generated restore inputs: 5.
- Generated restore aggregate SHA-256:
  `68CB91C141920F366BDCBA254E86B1AEC384F16B1BD66642C4DE0D4130B0E547`.
- Pinned restore-source inputs: 6.
- Pinned restore-source aggregate SHA-256:
  `03C9AF7A0203D51DD28C61392B734E8C486A2B44190D33FEEE05584F62960732`.
- Package trees: 6 packages, 900 files, 298,827,245 bytes, plus six metadata
  records.
- Package aggregate SHA-256:
  `1EA8343C612A8E36FA84EF4722D27FCEA132FDCF39B135C6E4BCB90A8D55A872`.
- Package build inputs: 9.
- Package build aggregate SHA-256:
  `DCA0505190B9B453D6E694666FDA3825C3D8C9E68DF5ADDEBB13767D98971518`.
- Active package import edges: 5.
- Package task bindings: 2.
- Post-restore closure: 44 compile items, zero project references, zero
  references, two analyzers, and 56 pinned properties.

Toolchain:

- .NET SDK `10.0.302`.
- MSVC linker `14.51.36252.0`, SHA-256
  `F233B8E337CEC96A69868A8CDE676808BFA81152493968D0B27B7CD0DAAC15BE`.
- Windows SDK `10.0.28000.0`.
- ILCompiler `10.0.8`, file version `10.0.826.23019`, SHA-256
  `DBCF62CC6C5240BC3441C9F6388CC670E2C113961BDBC8042E04080B5C296A95`.
- NativeAOT runtime pack `10.0.8`, 406 files, 109,016,717 bytes, aggregate
  SHA-256
  `73972C0D17F29FA389DE9C4A87051B372FA15AFE5CDADF55BEF731A3D8144FBC`.
- QEMU `11.0.50`.
- OVMF SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

Accepted final-source log hashes:

- Canonical publish:
  `48DD99EDD733F7614676565A3A08B624AC3875DB0BFEFA327D497836B3777324`.
- Standalone canonical verifier:
  `0DAFC523705EC1672677095CD0951D33A69F617F1CA42A14DC8A353787A681E7`.
- Aggregate host harness:
  `E3BF04052842AD31C5763C3860EFE8E9959750F77987CA4DBEDE03FA4B51C0EF`.

## Failures and corrections

No failed or superseded result below is accepted as completion evidence.

- The earlier schema-2 artifact, host run, hashes, and reproducibility bundle
  were invalidated when review found mutable package/tool authority and
  MSBuild import surfaces. They have been removed from the accepted evidence
  section rather than presented as current results.
- Adversarial review found unbounded producer processes, an unbounded
  standalone-verifier MSBuild child, verification-time output TOCTOU, reparse
  and hardlink aliases, noncanonical project/entry overrides, wildcard SDK
  imports, and stale-output risks. The final implementation closes those
  boundaries and the post-remediation review found no source-level blocker.
- The first final-source all-probe attempt reached the native link but failed
  under strict mode because package-tree hashing still appended an obsolete
  `$length` variable. It was changed to the overflow-checked
  `$packageFileByteLength`; that artifact was not accepted.
- The next attempt correctly rejected a legitimate zero-byte ILLink package
  member because the newly added package authority open had inherited the
  nonempty default. Package members now use `-AllowEmpty` while exact
  length/hash/tree accounting remains mandatory; that artifact was not
  accepted.
- Two complete host attempts found only stale static test anchors introduced
  by the hardening:
  - the package-authority test initially expected package cleanup to be the
    first statement in `finally`;
  - it then searched for obsolete direct restore/publish dotnet invocations.
  The assertions now begin at final verifier-input cleanup and locate the
  actual bounded restore/publish calls. Their ordering indices are nonnegative
  and strictly ordered before the final accepted host run.
- The first final aggregate run after those corrections exposed only a test
  wall-clock budget that conflated the verifier's bounded process startup and
  teardown with its actual hostile runtime-function scan. The acceptance test
  now independently bounds the entire process at 30 seconds and the scan at
  one second. The final run recorded 20,179 ms and 189 ms respectively.
- A standalone verification attempt against a convenience bundle renamed the
  EFI and serializer object, so the verifier correctly rejected it with
  `serializer_object_not_artifact_sibling`. The accepted standalone run used
  the canonical current-source artifact paths, exited zero, and produced the
  hash recorded above. No artifact or source changed because of the rejected
  invocation.
- An ad hoc byte-comparison helper used an unavailable PowerShell 5.1
  `AsSpan()` call. It did not alter any artifact. The check was rerun with
  `fc.exe /b` plus SHA-256 and all seven deterministic files matched exactly.

## Reference provenance

- `references/SharpOS-main/bootasm/CoffStub.Generator/CoffStub.Generator.targets`
  - SHA-256
    `C48D6A90385E162E9073B9BCCCF303156151905E598B1F4E84A5E1C6345DCA5F`.
  - Influence: authorized host-generator-to-link staging pattern.
- `references/SharpOS-main/apps_native/sdk/MinimalRuntime.cs`
  - SHA-256
    `4BB87E5027014177ABFB120B1C3D214D71A44AC6707E969F2FA8E7DE0BB43AE7`.
  - Influence: minimal no-stdlib `RuntimeImport`/`InternalCall` metadata shape.

The project-owner grant permits this bounded adaptation. CLROS independently
implements the closure, isolation, output authority, deterministic-link, and
hostile-verification controls. No reference artifact is accepted as CLROS
evidence.

## Deferred work and evidence boundary

- M01-035 must add checked serializer calls before every current
  instruction-publication transfer in `EfiEntry`.
- M01-026 must obtain cumulative real-QEMU/OVMF evidence after M01-035.
- The expected cookie is only a static declaration and instruction immediate;
  M01-034 does not observe it at runtime.
- The build and dry-run tooling do not prove a guest boot.
- This issue does not prove serializer invocation, publication ordering,
  cross-core visibility, speculative-code-store-bypass closure, live page
  protections, owned CR3 behavior, `ExitBootServices`, owned COM1/descriptors,
  devices, applications, hosted .NET, Kestrel, cloud support, CloudReady
  status, production readiness, or any completion-gate advancement.
- Generated `artifacts/` outputs and temporary evidence bundles remain local
  and are not committed.
