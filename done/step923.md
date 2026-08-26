# Step 923 - Factor M02-013 no-stdlib diagnostic authorities

## Summary

Factored the three M02-013 diagnostic authorities needed by the NativeAOT EFI
source closure into independently linkable, no-stdlib-safe C# files without
changing their public contracts or enabled behavior.

`CpuFaultFrame` now has one authoritative declaration in
`CpuFaultFrame.cs`. The default-false invalid-opcode probe constant now has one
authoritative declaration in `InvalidOpcodeProbeAuthority.cs`, and invalid
opcode vector `6` now has one authoritative declaration in
`InvalidOpcodeVectorAuthority.cs`. `Probes` and `FaultVectorCatalog` are partial
only so those declarations can remain part of their existing public types.

The focused M02-013 test compiles the actual four-source no-stdlib closure with
zero metadata references. It no longer substitutes host-only versions of
`Probes`, `FaultVectorCatalog`, or `CpuFaultFrame`.

This is a source-closure dependency for the separate terminal EFI integration.
It is not guest fault-delivery evidence and does not change the default-disabled
status of M02-013.

## Why

The frozen M02-013 host leaf depends on `Probes.EnableInvalidOpcodePanicProbe`,
`FaultVectorCatalog.InvalidOpcode`, and `CpuFaultFrame`. Their former containing
files also pulled unrelated formatting and diagnostic dependencies into the
explicit EFI source closure. Issue #503 tracks the smallest prerequisite:
factor only those existing authorities so the real production leaf and its
required types can compile under the no-stdlib EFI contract without duplicate
or synthetic definitions.

## Files changed

- `src/OS.Kernel/Diagnostics/CpuFaultFrame.cs`
  - Contains the byte-for-byte extracted 23-field `CpuFaultFrame` declaration.
- `src/OS.Kernel/Diagnostics/InvalidOpcodeProbeAuthority.cs`
  - Contains the sole unconditional `false` definition of
    `Probes.EnableInvalidOpcodePanicProbe`.
- `src/OS.Kernel/Diagnostics/InvalidOpcodeVectorAuthority.cs`
  - Contains the sole definition of `FaultVectorCatalog.InvalidOpcode` as `6`.
- `src/OS.Kernel/Diagnostics/PanicFormatter.cs`
  - Retains formatter behavior after the frame declaration moved.
- `src/OS.Kernel/Diagnostics/Probes.cs`
- `src/OS.Kernel/Diagnostics/FaultVectorInfo.cs`
  - Mark their existing authority types `partial`; no other behavior changes.
- `tests/OS.HostTests/DisabledDefaultUd2VectorStateTests.cs`
  - Compiles the four real production sources with a compiler-minimal runtime,
    zero metadata references, and no fake diagnostic authority types.
  - Enforces one-definition factoring and the unchanged production-leaf hash.
- `done/step923.md`
  - Records the reviewed dependency evidence and its narrow boundary.

## Tests run

At implementation commit
`7a36932b5405d5ce29ec12457474ba46b5406c98`:

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Exit `0`; zero warnings and zero errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Exit `0`; zero warnings and zero errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Exit `0`; 7,050 lines, 3,492 `TEST_START` records, 3,493
    `TEST_PASS` records, zero `TEST_FAIL` records, and one terminal
    `TEST_PASS host.tests` record.
  - The focused records were:

```text
TEST_START m02.013
CLROS_M02_013_HOST_READY requestBytes=32 resultBytes=72 ud2Bytes=2 vector=6 errorCodePresent=false errorCode=0 auditFields=23 probeEnabled=false executionRequested=false executionAttempted=false syntheticObservation=true guestExecution=false faultTriggered=false idtrLoaded=false postExitBootServices=false
TEST_PASS m02.013
```

- Independent zero-reference Roslyn compilation of the compiler-minimal
  runtime plus the four real production sources
  - Passed with five syntax trees, zero metadata references, and a 12,800-byte
    emitted assembly.
- Independent focused execution of `DisabledDefaultUd2VectorStateTests`
  - Passed with the exact host-ready marker above.
- `.\scripts\check-naming.ps1`
  - `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check`
  - Exit `0` before and after the implementation commit.

## Evidence

The reviewed base was
`95e4d5eb5070520c9217d03cee5922a94354c671`. Independent review approved the
implementation at these exact SHA-256 identities:

| Path | SHA-256 |
| --- | --- |
| `src/OS.Kernel/Diagnostics/FaultVectorInfo.cs` | `E37BAD15265D8EC737C77D447AC7C7DA33B3BB5883996D034BDB7E6C9F4F1271` |
| `src/OS.Kernel/Diagnostics/PanicFormatter.cs` | `5B92EC29E5664FABC625E9318F858F26866AD7C84DBA57B9166A07ABA337557D` |
| `src/OS.Kernel/Diagnostics/Probes.cs` | `79D585D28C0A80A82F126AF5BF792C68BB54001BD479CDC6AE20072C046206C0` |
| `src/OS.Kernel/Diagnostics/CpuFaultFrame.cs` | `CC27301B99685C30FA1AC3A954C18E1CAA29BA2651B7A3E53B1C36EAE3CD157F` |
| `src/OS.Kernel/Diagnostics/InvalidOpcodeProbeAuthority.cs` | `40D0E23D0245B360833BDEA5D9D0F33BCBE640D0AFEE3A8687832B51833E5FC0` |
| `src/OS.Kernel/Diagnostics/InvalidOpcodeVectorAuthority.cs` | `4945ABF12885CD9BECB4858304F6849FE90550A80B4D14B356AE6DD4742A73D3` |
| `src/OS.Kernel/Diagnostics/DisabledDefaultUd2VectorState.cs` | `3320C4A32E67A424D56A7C598DDB622E0477368CFA3C1482F1B08399245BC4AD` |
| `tests/OS.HostTests/DisabledDefaultUd2VectorStateTests.cs` | `92EF5CA8BFE40CF5B2FACF2212882F8589BC6C0959F51E34C5F6B37D7451868D` |

Independent review also confirmed exactly one source definition for each moved
authority, identical `CpuFaultFrame` constructor/property ordering and types,
an unchanged M02-013 production leaf, normal project inclusion of all three new
files, and no new BCL-heavy dependency in those files.

## Failures

- An initial whole-solution build could not copy two test-output assemblies
  while the intentionally preserved full host harness held them open. No
  process was interrupted. After the harness completed successfully, the exact
  whole-solution build passed with zero warnings and zero errors.
- No accepted compilation, focused-test, full-host, naming, source-firewall, or
  diff-check failure remains.

## Deferred work

- Issue #501 owns the separate NativeAOT EFI profile, code/data materialization,
  descriptor activation, controlled UD2 execution, owned-COM1 transcript, and
  real QEMU/OVMF evidence.
- Parent issue #53 remains open until that guest evidence is merged and read
  back from Gitea.
- Persistent post-firmware descriptor ownership, real `ExitBootServices`, live
  hardware drivers, hosted .NET 10, Kestrel/Minimal API execution on CLROS,
  cloud-provider images, hardening, and production readiness remain unproved.

## Evidence boundary

This step proves a reviewed C# source refactor and a real zero-metadata-reference
no-stdlib compile of the M02-013 host leaf with its authoritative probe, vector,
and frame types. It preserves the default-disabled host-only M02-013 behavior.

It does not prove EFI inclusion, executable transition bytes, IDT/GDT/TSS/IST
activation, controlled invalid-opcode delivery, terminal halt behavior, owned
serial output after descriptor activation, post-`ExitBootServices` execution,
hosted .NET, Kestrel, cloud readiness, or production readiness. Gate 2 remains
the only passing numbered completion gate; Gate 3 and Gate 4 remain `Fail`.
