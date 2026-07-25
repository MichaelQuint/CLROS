# Step 166 - ACPI SDT Pointer List Parser

## What Changed

- Added `AcpiSdtParser` for caller-owned ACPI `RSDT` and `XSDT` table bytes.
- Added focused host tests for valid RSDT/XSDT pointer lists and deterministic failure cases.
- Updated boot, driver, and completion-gate docs with the new parser evidence and its boundary.

## Why

Gitea issue #187 tracks the next ACPI parsing slice after RSDP validation and MCFG parsing. The OS needs a safe, host-testable boundary for interpreting root SDT pointer lists before any future physical-memory walker can dereference firmware-provided ACPI addresses.

This step keeps the work limited to caller-owned byte buffers. It does not read from the RSDP pointer, dereference returned table pointers, or walk real physical memory.

## Files Changed

- `src/OS.Kernel/Boot/AcpiSdtParser.cs`
- `tests/OS.HostTests/AcpiSdtParserTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step166.md`

## Implemented Behavior

- Parses common ACPI SDT header fields from caller-provided bytes: signature, declared length, revision, checksum, OEM ID, OEM table ID, OEM revision, creator ID, and creator revision.
- Accepts only `RSDT` and `XSDT` signatures.
- Validates declared length against the source length before slicing the declared table range.
- Validates checksum over exactly the declared table length.
- Parses RSDT 32-bit pointer arrays and XSDT 64-bit pointer arrays.
- Rejects invalid pointer-array byte lengths, zero table pointers, and undersized caller-provided pointer destinations with deterministic status values.
- Copies validated physical-address values into caller-owned destination storage without dereferencing those addresses.

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host tests emitted `TEST_PASS host.tests`.
- Focused ACPI SDT markers emitted:
  - `TEST_PASS acpi.sdt parses valid rsdt`
  - `TEST_PASS acpi.sdt parses valid xsdt`
  - `TEST_PASS acpi.sdt rejects checksum failure`
  - `TEST_PASS acpi.sdt rejects declared source length mismatch`
  - `TEST_PASS acpi.sdt rejects invalid pointer array length`
  - `TEST_PASS acpi.sdt rejects zero table pointer`
  - `TEST_PASS acpi.sdt rejects destination capacity exhaustion`

## Failures

- One rerun of `dotnet build .\OS.slnx --nologo -m:1` failed with `CS2012` because `tools/OS.ImageBuilder\obj\Debug\net10.0\OS.ImageBuilder.dll` was locked by a concurrent `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` process.
- After that process exited, the solution build reran successfully with 0 warnings and 0 errors.
- No parser or host-test failures remained.

## Deferred Work

- Add safe physical-memory read rules before validating or walking firmware-provided ACPI pointers.
- Wire RSDP validation to controlled table reads before using `AcpiSdtParser` in the live boot path.
- Identify and parse target SDT signatures such as `MCFG`, `APIC`, and `FACP` after root table reads are safe.

## Evidence Boundary

This is host-tested parser evidence only. It proves deterministic parsing of caller-owned RSDT/XSDT bytes and copying of validated pointer values into caller-owned storage. It does not prove live RSDP dereferencing, real physical-memory reads, ACPI table discovery under QEMU, APIC setup, PCI enumeration, driver binding, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
