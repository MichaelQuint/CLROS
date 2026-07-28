# Step 884 - Generate the deterministic AMD64 instruction-publication COFF serializer

## Summary

- Implemented the standalone C# 14/.NET 10
  `OS.X64TextSerializerGenerator` host tool for M01-033.
- The tool emits exactly one deterministic 336-byte AMD64 COFF object containing
  immutable serializer text, Windows-x64 unwind metadata, one runtime-function
  record, and the three required `IMAGE_REL_AMD64_ADDR32NB` relocations.
- Added an independent, bounds-checked host-test oracle that clean-builds the
  tool from two distinct temporary checkout roots, invokes it through a bounded
  subprocess helper, parses every COFF range, and rejects exhaustive truncation
  and single-bit mutation families.
- Extended the recursive host-tool inventory from four to five expected tools
  while keeping the new generator outside `CLROS.slnx` until M01-034.
- Documented exact object bytes, provenance, atomic publication behavior,
  stable markers, and the object-only evidence boundary.

## Why

The current EFI path publishes generated instruction bytes and relies on later
`CPUID` execution to serialize earlier publication. M01-033 requires a separate
immutable serializer body that later issues can link and invoke immediately
after each publication epoch. This leaf establishes the deterministic object,
symbol, relocation, ABI, and encoded unwind intent without prematurely changing
the solution, EFI link, runtime imports, or `EfiEntry`.

The writer and command are fail-closed because this object will eventually sit
on a privileged execution boundary. The command therefore validates every
input, builds the complete object in memory, writes a unique sibling temporary
file, atomically renames without overwrite, re-reads the accepted bytes, and
never deletes a destination pathname after publication.

## Files changed

- `tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj`
- `tools/OS.X64TextSerializerGenerator/Program.cs`
- `tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs`
- `tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs`
- `tools/AGENTS.override.md`
- `tests/OS.HostTests/M01InstructionPublicationSerializerGeneratorTests.cs`
- `tests/OS.HostTests/HostToolProjectBoundaryTests.cs`
- `docs/security.md`
- `docs/qemu.md`
- `docs/libvirt.md`
- `done/step884.md`

## Tests run

- `dotnet build
  .\tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.csproj
  --nologo`
  - Passed with 0 warnings and 0 errors.
- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Exit code 0 in 427.396 seconds.
  - 3,272 `TEST_START` markers.
  - 3,273 `TEST_PASS` markers.
  - 0 `TEST_FAIL` markers.
  - Exactly one `TEST_PASS host.tests`.
  - Exactly one M01-033 start, canonical ready, and pass marker.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -SelfTest`
  - Passed all 16 milestones, 68 labels, 16 epics, 324 children, 340 issue
    keys, managed-body checks, and state-policy checks.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\sync-gitea-backlog.ps1 -Offline`
  - Passed with all 424 managed objects skipped in manifest-only mode and zero
    failed, create, update, or would-change actions.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\check-naming.ps1`
  - Passed `naming.guard` and `source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Passed the default `SingleMinimalApi` QEMU runner contract.
- Direct generator invocation from the final tree:
  - Exit code 0.
  - Exact ready/pass markers.
  - 336-byte output.
  - Canonical SHA-256.
  - Zero `.clros-x64-*.tmp` residue.
- Focused M01-033 execution:
  - Passed the exact aggregate markers and frozen hash.
  - Rejected all 336 truncation points and all 2,688 one-bit object mutations.
  - Passed direct writer default, null, empty, malformed, duplicate, unsafe
    protection, symbol, relocation, overflow, capacity, and sentinel cases.
  - Passed path, timeout, partial-output, output-overflow, stale-output,
    mismatch-preservation, and actual publisher cleanup cases.
- Focused host-tool boundary execution:
  - Passed exact five-tool inventory.
  - Passed recursive native/object source exclusion outside generated
    `bin`/`obj` directories.
- `git diff --check`
  - Passed.
- Repository boundary scans:
  - 0 tracked `.obj` files.
  - 0 `.obj`, `.c`, `.cpp`, `.h`, `.asm`, or `.s` files in the generator
    source tree outside `bin`/`obj`.
  - 0 first-party `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under `src`,
    `std`, or `apps`.

## Evidence

- Base source state:
  `83a433fb91832c1b2641ad04979d6a16861bd536`.
- Final object:
  - Size: 336 bytes.
  - SHA-256:
    `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
  - Machine: AMD64 `0x8664`.
  - Timestamp: zero.
  - Sections: `.text$clrossm`, `.xdata$clrossm`, and `.pdata$clrossm`.
  - Text:
    `53 B8 00 00 00 00 B9 00 00 00 00 0F A2 B8 43 4C 52 53 5B C3`.
  - Unwind:
    `01 01 01 00 01 30 00 00`.
  - Exactly one external
    `CLROS_X64_SerializeInstructionPublication` function symbol and one static
    `.xdata$clrossm` relocation-base symbol.
  - Exactly three `IMAGE_REL_AMD64_ADDR32NB` relocations.
- Required host markers:

  ```text
  TEST_START m01.033
  CLROS_M01_033_READY bytes=336 sha256=DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B
  TEST_PASS m01.033
  TEST_PASS host.tests
  ```

- Final complete-harness stdout:
  - Path at verification time:
    `C:\Users\Roger\AppData\Local\Temp\clros-m01-033-final-968aa26ffab04523be6ffea8c2993a07\stdout.log`.
  - 835,214 bytes.
  - SHA-256:
    `B16B4A41BB7BB2C006B5DCF3DC6036B39ED8A8F3587A15631816E97416F04DFF`.
  - Written from `2026-07-28T14:52:02Z` through
    `2026-07-28T14:59:09Z`.
- Final complete-harness stderr:
  - 0 bytes.
  - SHA-256:
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Final source hashes:
  - `OS.X64TextSerializerGenerator.csproj`:
    `920D8236BDD7A548BBA2B6C6A4BDBF073B1D52B702CEC5D0C5BA033BDFEAB8E5`.
  - `Program.cs`:
    `49314488FE6A615DCB9728B1C23A12CA992BFD305B9270AAA0DF6368AB01EFFB`.
  - `Amd64CoffWriter.cs`:
    `20688C4E22CAD825213855A412AF9242749C8ED4238D92F63629C4E589974B29`.
  - `InstructionPublicationSerializerGenerator.cs`:
    `605CBC106BDDC269969B6B8DF682D0A0F652619A9970A06F0037918AC82752E5`.
  - `M01InstructionPublicationSerializerGeneratorTests.cs`:
    `E627354BAE4B08642B28BD5559AF21FFB97020DB0FAEAA26B7BCB78A83D76B63`.
  - `HostToolProjectBoundaryTests.cs`:
    `8D2C642F810A417390C0986073F9D38D56CB04156E968F5B1CEDBEF627045D71`.
- Tool versions:
  - .NET SDK `10.0.302`.
  - Git `2.54.0.windows.1`.
  - Windows PowerShell `5.1.26100.8894`.
- Exact reference inputs:
  - `references/SharpOS-main/bootasm/CoffStub.Generator/CoffWriter.cs`:
    `3604F93946085BD7052A7350A382DEE0CD41BEE69DB9A8AB5C8C88B207C930D1`.
  - `references/SharpOS-main/bootasm/CoffStub.Generator/EmitCoffStubsTask.cs`:
    `9F52110767B74FF819205889ED8F3798B704958A058BA01B903E5794BCC01D41`.
  - Adaptation was limited to explicit little-endian emission, zero
    timestamps, long-name string-table encoding, and fail-closed host-side
    generation. CLROS independently implements the three-section,
    relocation, unwind, bounds, overflow, and atomic-publication behavior.
- Two independent final reviews found no remaining production, test,
  documentation, process-lifecycle, ownership, or evidence blocker.
- A review-only Microsoft `dumpbin`/link diagnostic parsed the object and
  decoded one runtime-function interval with `PUSH_NONVOL RBX`; its temporary
  output was removed and is not M01-034 evidence.

## Failures

- Initial review found that post-publication fingerprint-based cleanup could
  delete a raced destination replacement. The implementation now never deletes
  the destination after a successful non-overwriting rename.
- A second review found that unconditional temporary-path cleanup could delete
  a pre-existing GUID-collision file or a post-move reoccupation. Explicit
  ownership now begins only after successful `CreateNew` and ends immediately
  after successful `Move`.
- Focused CLI tests found that `.obj` was accepted despite having an empty
  basename. Empty and whitespace-only stems now fail with the stable
  `output_file_invalid` diagnostic.
- The first bounded-capture implementation capped files only after child exit,
  which did not bound disk consumption. It was rejected before merge.
- In-process concurrent drains cannot execute while the HostTests module
  initializer is active because of the CLR module-initialization lock.
  M01-033 now clean-builds an ephemeral helper with a normal `Main`; the helper
  uses fixed 4 KiB drains, hard retention caps, continued discard, timeout/tree
  termination, bounded result files, metadata-last publication, and guarded
  no-orphan cleanup. It is never committed or added as a project dependency.
- Exploratory full-harness attempts made before the final helper rewrite were
  deliberately terminated or timed out and are not evidence. Their remaining
  process trees were explicitly reaped. Only the final 427.396-second run and
  hashes above are accepted.
- No failure remained in the final direct, solution, host-build, complete-host,
  backlog, naming, source-firewall, dry-run, or diff gates.

## Deferred work

- M01-034 owns adding the generator to `CLROS.slnx`, integrating only its
  ephemeral object into the EFI link, and verifying the exact relocated text,
  function table, unwind coverage, symbol binding, imports, and final PE
  protections from the same build.
- M01-035 owns declaring the exact `InternalCall` runtime import and checking
  the returned cookie at every current `EfiEntry` publication epoch.
- M01-026 owns cumulative real-QEMU evidence after M01-033 through M01-035
  close.
- This step does not add a permanent helper project, commit an `.obj`, link the
  object, apply relocations, establish final-image unwindability, change
  `RuntimeImport`, edit `EfiEntry`, execute the serializer, build or boot EFI,
  run real QEMU, prove live paging, advance a completion gate, or prove cloud
  or production readiness.
