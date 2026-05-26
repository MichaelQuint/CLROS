# Step 627 - Libvirt Domain XML Validation

## Summary

Added a bounded host-only `OS.LibvirtRunner validate-xml` mode for Issue #7. The
new mode validates the generated libvirt domain XML contract and emits stable
machine-readable summary and pass/fail markers without connecting to libvirt,
starting a VM, or touching QEMU serial-tail behavior.

## Why

Issue #7 needs incremental host tooling evidence that can run on the current
host without libvirt/cloud credentials. XML shape validation gives deterministic
coverage for the libvirt runner's generated domain contract while keeping real
libvirt lifecycle, serial polling, and cloud import claims out of scope.

## Files changed

- `tools/OS.LibvirtRunner/Program.cs`
  - Added `validate-xml` / `--validate-xml` with `--domain-xml` / `--xml`.
  - Validates the current generated XML shape: `domain type="kvm"`, q35
    x86_64 HVM disk boot, MiB memory/currentMemory, vCPU count, ACPI/APIC,
    one raw/qcow2 virtio file disk, one file-backed ISA serial device, optional
    pflash OVMF loader, and either no network or the default virtio network.
  - Emits `TEST_START libvirt.xml.validation`,
    `LIBVIRT_XML_*` summary lines, and `TEST_PASS` / deterministic `TEST_FAIL`
    markers.
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
  - Added positive generated-XML summary coverage.
  - Added invalid disk-driver rejection coverage.
- `docs/libvirt.md`
  - Documented local XML validation behavior and its evidence boundary.
- `done/step627.md`
  - Recorded this step and validation evidence.

## Tests run

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo`
  - Failed before compilation because repo-wide profile validation rejected the
    empty `OSWorkloadProfile` value.
- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi`
  - Passed with 0 warnings and 0 errors.
- Generated `artifacts\tmp\issue7-libvirt-xml\domain.xml` with
  `OS.LibvirtRunner generate-xml`, then ran
  `OS.LibvirtRunner validate-xml --domain-xml artifacts\tmp\issue7-libvirt-xml\domain.xml`.
  - Passed.
- Generated `artifacts\tmp\issue7-libvirt-xml\domain-invalid.xml` by changing
  the disk driver type to `vmdk`, then ran `validate-xml`.
  - Failed deterministically with `disk_driver_invalid`.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed with `TEST_PASS host.tests`.
- `git diff --check`
  - Exited 0 with no whitespace errors; Git printed CRLF conversion warnings for
    existing dirty files.

## Evidence

Positive direct validator markers:

```text
TEST_START libvirt.xml.validation
LIBVIRT_XML_PATH C:\Projects\OS\artifacts\tmp\issue7-libvirt-xml\domain.xml
LIBVIRT_XML_STATUS valid
LIBVIRT_XML_BYTES 877
LIBVIRT_XML_DOMAIN name=os-xml-check type=kvm
LIBVIRT_XML_MEMORY mib=384 currentMib=384
LIBVIRT_XML_VCPU count=2
LIBVIRT_XML_OS type=hvm arch=x86_64 machine=q35
LIBVIRT_XML_FEATURES acpi;apic
LIBVIRT_XML_DISK format=qcow2 bus=virtio source=artifacts/images/os.qcow2
LIBVIRT_XML_SERIAL type=file target=isa-serial path=artifacts/logs/libvirt-xml-check.log
LIBVIRT_XML_NETWORK default
LIBVIRT_XML_OVMF_LOADER configured=true path=artifacts/ovmf/OVMF_CODE.fd
TEST_PASS libvirt.xml.validation
```

Negative direct validator markers:

```text
TEST_START libvirt.xml.validation
LIBVIRT_XML_PATH C:\Projects\OS\artifacts\tmp\issue7-libvirt-xml\domain-invalid.xml
TEST_FAIL libvirt.xml.validation disk_driver_invalid
```

Host-test markers:

```text
TEST_PASS libvirt validate-xml summarizes generated XML
TEST_PASS libvirt validate-xml rejects invalid disk XML
TEST_PASS host.tests
```

## Failures

- Plain `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo`
  failed at `Directory.Build.targets` with
  `Invalid OSWorkloadProfile ''. Valid values: SingleMinimalApi, MultiMinimalApi.`
  Retrying with `/p:OSWorkloadProfile=SingleMinimalApi` passed.

## Deferred work

- Real libvirt connection, transient domain creation, start, timeout handling,
  cleanup, and live serial polling remain pending.
- The smoke wrapper can optionally grow a validate-XML artifact lane later, but
  this step keeps wrapper behavior unchanged to avoid overlapping current
  serial-tail/check-serial work.

## Evidence boundary

This is host-only generated-domain-shape evidence. It does not prove libvirt
accepts or defines the XML, does not start a VM, does not poll live serial logs,
does not prove QEMU/cloud boot, and does not prove hosted .NET, Minimal API, or
Kestrel support.
