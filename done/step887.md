# Step 887 - Serialize all current EfiEntry instruction-publication epochs

## Summary

Completed M01-035 as a source-order, host-test, and fresh-build boundary.
The all-three-switch `EfiEntry` path now calls M01-034's immutable linked
instruction-publication serializer exactly once at each of the four current
generated-code publication epochs:

1. after the stage-one stack-probe bytes are written, read back, and installed;
2. after the stage-one stack-switch bytes are prepared and read back, before
   obtaining or invoking the switch entry;
3. inside `CaptureArchitecturalPreflight`, after the preflight/PAT bytes are
   materialized and read back and both entry addresses are bound, before either
   generated entry is invoked; and
4. after the final composite/call-gate/protection-probe readback and transfer
   binding, after `M01_PRE_CR3`, before the call gate is invoked.

Every call uses the fully qualified
`global::OS.Boot.X64InstructionPublication` binding, consumes the returned
`uint`, and compares it with `ExpectedSuccessCookie` before the corresponding
first execution. One `EfiEntry`-owned primitive state survives the one-way
stack switch and can advance only `0 -> 1 -> 3 -> 7 -> 15`.

Every epoch-state or cookie rejection emits
`M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED` before its cleanup, return, or
terminal spin. After the call gate returns, the existing audit/restoration and
continuation-canary contract must pass and the mask must equal `15` before
`M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED` is emitted immediately before
the existing `M01_POST_CR3` line.

The complete host harness, fresh all-three-switch EFI build, and independent
M01-034 final-artifact verifier passed. No QEMU smoke, guest boot, or QEMU
dry-run smoke workflow ran. The mandatory EFI preflight queried only
`qemu-system-x86_64 --version` and `qemu-img --version`; those tool-version
probes are not guest-execution evidence.

## Why

The generated stage-one stack probe, stage-one stack switch, stage-three
preflight/PAT/thunk group, and stage-three final call-gate/protection group
were previously executable without a separate checked immutable-text
serialization boundary at every publication epoch. A later CPUID in a
generated stream was too late to serialize that stream's own first fetch and
did not cover both stage-one streams.

M01-034 established one immutable, statically verified serializer in the
canonical EFI artifact. M01-035 had to consume that exact binding at each
current publication boundary, reject every wrong or synthetic cookie, preserve
the state through the one-way stack transfer, and prove structurally that no
write, readback mutation, rebinding, callback, unrelated managed call, or
generated-code execution can be inserted into a checked success window.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
  - Adds the one-shot initialized byte and four-bit publication mask under the
    existing all-three-M01-switch closure.
  - Rejects stale or repeated initialization before clearing the mask.
  - Adds the four checked, fully qualified immutable serializer calls and the
    exact `0 -> 1 -> 3 -> 7 -> 15` transitions.
  - Requires the owned-stack continuation to observe the same root-owned state.
  - Adds exact byte-authored failure and aggregate marker writers.
  - Keeps every probe default-off and preserves all existing `OS.*`,
    `src/OS`, `src/OS.Kernel`, and `OSHost_*` identities.
- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`
  - Adds eight focused groups for profile cardinality, state lifetime, each
    epoch, markers, mutation resistance, and evidence-boundary uniqueness.
  - Requires direct-call counts `0`, `2`, `2`, and `4` for the disabled,
    stage-one, stage-two, and all-switch source profiles.
  - Freezes the complete materialization/readback-to-guard caller windows and
    every relevant helper tail using compact-source length plus SHA-256
    identities.
  - Rejects missing, duplicate, early, late, branch-only, ignored, synthetic,
    wrong-cookie, wrong-binding, shadowed-binding, pre-set, partial-mask,
    reconstructed-state, and reordered-transition variants.
  - Rejects post-readback writes and direct or named generated-code execution
    at the earliest caller/helper seams.
  - Rejects dynamic-byte, published-stream CPUID, LFENCE, MFENCE, volatile,
    lock-prefixed, ordinary-managed-call, firmware-callback, and no-op
    substitutes.
  - Requires exact direct or `EfiEntry.`-qualified failure-marker domination;
    exact unqualified six-group byte writers; one unqualified CRLF; literal
    byte arguments; and the aggregate marker immediately before
    `M01_POST_CR3`.
- `docs/boot.md`
  - Records the four publication epochs, state transitions, marker placement,
    host/source evidence, no-QEMU boundary, and remaining execution nonclaims.
- `docs/architecture.md`
  - Records the root-state lifetime, immutable serializer placement, aggregate
    gate, and M01-026 as the owner of cumulative real-QEMU evidence.
- `docs/security.md`
  - Records wrong-cookie failure behavior, source/mutation coverage, the
    static evidence boundary, and unsupported speculative, cross-core, DMA,
    live-paging, and completion-gate claims.
- `done/step887.md`
  - Preserves this implementation and evidence record.

No forbidden M01-035 path changed. In particular, this work did not modify
`CLROS.slnx`, `src/OS/OS.csproj`,
`src/OS/Boot/X64InstructionPublication.cs`, either runtime attribute,
the generator, EFI build/verifier/QEMU scripts, M01-034 tests,
`docs/efi-build.md`, `docs/nativeaot-nostdlib-limits.md`,
`docs/completion-gates.md`, `README.md`, or any reference snapshot. No
first-party `.c`, `.cpp`, `.h`, `.asm`, or `.s` file was added.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit zero; zero warnings; zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Exit zero; zero warnings; zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Reached the explicit `failures == 0` / `return 0` path in
    `tests/OS.HostTests/Program.cs`.
  - 3,304 `TEST_START` lines.
  - 3,305 `TEST_PASS` lines.
  - Zero `TEST_FAIL` lines.
  - Exactly one terminal `TEST_PASS host.tests`.
  - All eight M01-035 focused groups passed, followed by
    `CLROS_M01_035_READY` and `TEST_PASS m01.035`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe
  -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe`
  - Exit zero.
  - Confirmed all three M01 switches.
  - Confirmed the exact 44-item M01 source closure.
  - Nested `TEST_PASS efi.artifact.verify`.
  - `TEST_PASS efi.shell.publish`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\test-efi-artifact.ps1 -EfiArtifact
  .\artifacts\efi\BOOTX64.EFI`
  - Exit zero.
  - `TEST_PASS efi.artifact.verify`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard`.
  - `TEST_PASS source.firewall`.
- `git diff --check`
  - Exit zero.
- `git status --short`
  - Before reserving this step, the only changes were the five exact
    M01-035-owned implementation/test/documentation paths.

No QEMU smoke, guest boot, or QEMU dry-run smoke workflow was run for
M01-035. The required EFI preflight did execute
`qemu-system-x86_64 --version` and `qemu-img --version` for tool discovery
only; it created no guest/runtime evidence.

## Evidence

### Audited source contract

Two independent read-only adversarial audits accepted the final source/test
snapshot after earlier false-positive seams were closed. The accepted oracle
freezes these compact source windows:

- epoch-one caller: 440 bytes,
  `2BDB040797FBB46E5540B19C33DF1F7B22D8F5B69A941BDFED796EC56270F37F`;
- epoch-one probe-helper terminal: 307 bytes,
  `D57753E7529FE226196FF955D7DE237E4D0969567EC87740B8FA2F9818FEF0C8`;
- epoch-two caller: 2,869 bytes,
  `A38FC57AAD0CA7ED05ACD83261BF1DDBEAA22C2736349F2CCA4CCACC69139DDD`;
- epoch-three caller seam: 395 bytes,
  `4F67D6FB81477339251B2260B87353CB90D63C2BFD549F76CC270A413F5A3D26`;
- epoch-three transfer-helper terminal: 235 bytes,
  `29720B05C95CEAB16B29AC23BDEB84D925B5825483B6D3B1129FF47F33666BCD`;
- epoch-three planner-helper terminal: 244 bytes,
  `3585AB1D6ADC1FEBC83C3883F502910B52DBF901422E4A05796DBDDCBD911B7E`;
- epoch-three preflight/PAT capture: 1,539 bytes,
  `53EB01F963A345A5282A8D299E9E21082A8D456A5FFE881BA65E1BA350FAF6D1`;
- epoch-four protection-readback terminal: 151 bytes,
  `60CEA84DD3D8445F747A4AF5C2483F226B3B0C4973134C4F0B7F6C0D311DB69D`;
- epoch-four transfer-readback terminal: 128 bytes,
  `DC8541B8AF1F99F5B1429A6D2A875FFA6C7227ED1D76D9B73A49D3A1F88B510A`;
- epoch-four planner-readback terminal: 134 bytes,
  `A6C1AB2883A6D2E84132CD3A863805B985BDD4B2A10D1F240ADAA8AA8544AEA5`;
- epoch-four caller: 1,819 bytes,
  `05206471B0AA1B153DFF9ABC7B474C575EFB5A1503495655FEA794AFD90063F5`.

The final branch started from
`945043C941C28CF96C93680BD4442E1FA643471C`, which was also the then-current
`origin/master`.

### Full host gate

- Capture directory:
  `artifacts/logs/m01-035-final/20260729T022644Z`.
- Stdout capture bounds:
  - first file creation: `2026-07-29T02:28:24.9132504Z`;
  - final test write: `2026-07-29T03:03:56.8365280Z`;
  - elapsed capture interval: `00:35:31.9232776`.
- Stdout:
  - 422,461 bytes;
  - SHA-256
    `6E8508D43357A05A3391A8A80578D114D71B6C6521B109AF03EA3E460B6F0742`.
- Stderr:
  - zero bytes;
  - SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

The M01-035 aggregate was:

```text
TEST_START m01.035
TEST_START m01.035 profile matrix binds exactly four direct calls
TEST_PASS m01.035 profile matrix binds exactly four direct calls
TEST_START m01.035 shared state initializes once and survives switch
TEST_PASS m01.035 shared state initializes once and survives switch
TEST_START m01.035 epoch one follows stack-probe publication
TEST_PASS m01.035 epoch one follows stack-probe publication
TEST_START m01.035 epoch two dominates the one-way stack switch
TEST_PASS m01.035 epoch two dominates the one-way stack switch
TEST_START m01.035 epoch three is inside architectural preflight
TEST_PASS m01.035 epoch three is inside architectural preflight
TEST_START m01.035 epoch four and aggregate marker are exact
TEST_PASS m01.035 epoch four and aggregate marker are exact
TEST_START m01.035 malformed and bypass mutations fail closed
TEST_PASS m01.035 malformed and bypass mutations fail closed
TEST_START m01.035 issue marker and evidence boundary are unique
TEST_PASS m01.035 issue marker and evidence boundary are unique
CLROS_M01_035_READY
TEST_PASS m01.035
```

### Fresh all-switch EFI build

- Start UTC: `2026-07-29T02:26:45.0030495Z`.
- End UTC: `2026-07-29T02:27:33.2871776Z`.
- Build stdout:
  - 6,121 bytes;
  - SHA-256
    `18B42B15B037DFFCED8CF5EB55CE4BA0A1FA603D0D554ACEE89AB0BA00F65E18`.
- Build stderr:
  - zero bytes;
  - SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- `BOOTX64.EFI`:
  - 459,264 bytes;
  - last write UTC `2026-07-29T02:27:17.5541138Z`;
  - SHA-256
    `76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C`.
- Link map:
  - 201,098 bytes;
  - SHA-256
    `E53A92225D51717A88D406D136D48C53DC18E2BE619F38F56B80D8EF5315A49F`.
- Link response:
  - 476 bytes;
  - SHA-256
    `0B1CD8A1D90132E58798B234C4BE9CD8DE03027621B082D449EECD7CDB9486DC`.
- Schema-three link evidence:
  - 392,604 bytes;
  - SHA-256
    `C459157DF24CE1480E38DEC85DE930A7984A99320ECEA3A742B8C7D268982442`.
- Preserved and linked serializer objects:
  - 336 bytes each;
  - SHA-256
    `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.

### Independent final-artifact verification

- Start UTC: `2026-07-29T02:27:39.8329764Z`.
- End UTC: `2026-07-29T02:27:54.9619627Z`.
- Stdout:
  - 1,547 bytes;
  - SHA-256
    `CBAD6C8B5D2D7AD302ED88905F906CFEEF917DB772C14F00D30F9F834B588B6A`.
- Stderr:
  - zero bytes;
  - SHA-256
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- PE32+, AMD64 `0x8664`, EFI application subsystem `10`.
- `EfiMain` RVA `0x0004AC50`.
- Import, bound-import, IAT, and delay-import directories absent.
- Exception directory:
  - `.pdata` RVA `0x001A6000`;
  - 9,684 bytes;
  - 807 runtime-function records;
  - 621 distinct unwind records.
- Serializer:
  - exact symbol `CLROS_X64_SerializeInstructionPublication`;
  - `.text` RVA `0x00001000`;
  - 20 bytes;
  - unwind RVA `0x0006F0D8`;
  - expected cookie `0x53524C43`;
  - SHA-256
    `DF76C8E39E0768D26EA27206FF102D7EADCC35D2C9BDF547B52B1171FD47BDAF`.

## Failures

- No implementation, focused-test, host-gate, build, naming, firewall,
  whitespace, or artifact-verifier failure remained in the accepted snapshot.
- During host-gate capture, the PowerShell `Start-Process -Wait` wrapper
  remained open after the `dotnet run` child had disappeared and after the
  complete log reached its sole `TEST_PASS host.tests`. Only the inert wrapper
  was terminated. This did not truncate or restart the harness. The managed
  program's source emits that terminal marker only in its
  `failures == 0` branch immediately before `return 0`; the captured log has
  zero failure markers and zero stderr bytes.
- Several adversarial source audits found false-positive seams before the
  accepted run. Those seams were closed with exact caller/helper publication
  windows and corresponding mutations before the fresh artifact and full host
  evidence above were generated.

## Deferred work

- M01-026 owns the fresh real-QEMU/OVMF cumulative proof for the three M01
  stages, runtime-observed publication cookies, the exact aggregate marker,
  live reversible owned-CR3 behavior, scoped protection/invlpg/probe checks,
  page-zero absence, restoration, and completion-gate update.
- This step does not prove that the EFI artifact booted, that any serializer
  call executed or returned `0x53524C43`, or that either new marker appeared
  in guest serial output.
- It does not empirically exclude speculative code-store bypass, prove
  cross-core or coherent DMA-written-code publication, exercise live CR3,
  establish permanent or systemwide W^X/page-zero policy, support a cloud
  provider, satisfy CloudReady criteria, or establish production readiness.
- No completion gate advances in this step.
