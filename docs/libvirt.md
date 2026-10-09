# Libvirt

## Current Status

Libvirt boot support is not implemented. Host-side deterministic domain XML generation exists and does not require libvirt to be installed. `tools/OS.LibvirtRunner validate-xml` validates generated domain XML shape and emits a deterministic summary without connecting to libvirt. `tools/OS.LibvirtRunner plan-lifecycle` and `dotnet run --project tools/OS.Tooling -- libvirt smoke --plan-lifecycle` now emit an opt-in real-libvirt lifecycle plan that records the future create/start/poll/timeout/cleanup order without starting a domain. `tools/OS.LibvirtRunner check-serial` validates an already-captured serial log against required and forbidden marker contracts using the same parser contract as `OS.QemuRunner`. `dotnet run --project tools/OS.Tooling -- libvirt smoke` also provides dry-run, check-serial-only, and missing-tool evidence for raw/qcow2 smoke wiring without claiming a real libvirt boot. The `libvirt-csharp` reference is present at `references/libvirt-csharp-main`.

## Implemented Behavior

- `tools/OS.LibvirtRunner generate-xml` emits deterministic libvirt domain XML.
- `tools/OS.LibvirtRunner validate-xml --domain-xml <path>` parses an existing domain XML file, validates the generated XML contract used by the runner, and emits `TEST_START libvirt.xml.validation`, `LIBVIRT_XML_*` summary lines, and `TEST_PASS libvirt.xml.validation` or deterministic `TEST_FAIL libvirt.xml.validation <reason>` markers.
- The XML validator requires the current generated shape: `domain type="kvm"`, q35/x86_64 HVM boot from disk, MiB memory/currentMemory, static vCPU count, ACPI/APIC features, one raw/qcow2 virtio file disk, one file-backed ISA serial device, optional pflash OVMF loader, and either no network or the default virtio network.
- `tools/OS.LibvirtRunner plan-lifecycle` emits a machine-readable, plan-only real-libvirt lifecycle contract. The plan records `LIBVIRT_LIFECYCLE_MODE plan-only`, `LIBVIRT_LIFECYCLE_DEFAULT_START_DOMAIN false`, `LIBVIRT_LIFECYCLE_REAL_RUN_IMPLEMENTED false`, required/forbidden serial markers, timeout seconds, disk format, network mode, XML byte count, and the future lifecycle order.
- The lifecycle plan order is: generate domain XML, validate host tools and paths, connect to libvirt, create a transient domain from XML, start the domain, poll the serial log for required markers, reject forbidden markers, enforce timeout, collect diagnostics, destroy the domain if running, undefine if persistent, and clean transient artifacts.
- The lifecycle plan records `--allow-real-libvirt-lifecycle` as the required future opt-in switch. This switch is only recorded today; real lifecycle execution is still not implemented.
- `tools/OS.LibvirtRunner --dry-run` uses the same XML-generation path.
- The generated XML includes domain name, memory, vCPU count, q35/x86_64 HVM type, raw/qcow2 virtio disk, serial file output, optional OVMF pflash loader, ACPI/APIC features, and `--network none|default`.
- `tools/OS.LibvirtRunner` accepts `--disk-format raw|qcow2`, defaults to `raw`, and writes that format into the libvirt QEMU disk driver type.
- XML generation is host-only and does not call libvirt by default.
- `--diagnostics` preserves XML on stdout and writes stable `LIBVIRT_DIAG` readiness lines to stderr for domain settings, image path existence, serial log directory existence, OVMF loader state, and XML byte count.
- `--require-marker <marker>` and `--forbid-marker <marker>` can be repeated to record the serial marker contract a future real libvirt smoke run should use. Diagnostics emit `LIBVIRT_DIAG required.markers=...` and `LIBVIRT_DIAG forbidden.markers=...`; when no required marker is provided, the default is `OS_SMOKE_PASS`.
- The current default libvirt marker contract matches the raw/qcow2 QEMU smoke boundary: `OS_SMOKE_PASS`, `UEFI_MEMORY_MAP_OWNED`, `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT`, `BOOT_ACPI_RSDP_HANDOFF_READY`, and `UEFI_ACPI_RSDP_READY`.
- The recorded required/forbidden marker contract is intentionally aligned with the QEMU runner's `SerialMarkerSummary` parser semantics: required markers must be present in serial text, forbidden markers must be absent, and marker-like serial lines keep their observed order for summary artifacts.
- `tools/OS.LibvirtRunner check-serial --serial-log <path> [--summary-log <path>] [--require-marker <marker>]... [--forbid-marker <marker>]...` reads an existing serial log and emits `TEST_START libvirt.serial.summary`, `LIBVIRT_SERIAL_SUMMARY_STATUS pass|fail`, `OBSERVED_MARKER`, `APP_EXIT_RESULT`, `MISSING_MARKERS`, `FORBIDDEN_MARKER_HIT`, `FAIL_REASON`, and `TEST_PASS`/`TEST_FAIL` markers.
- `check-serial` returns nonzero status for missing serial logs, missing required markers, and forbidden marker hits. It does not connect to libvirt or start a VM.
- `tools/Directory.Build.props` excludes each host tool's local `artifacts/**`
  tree from default SDK source inclusion. This keeps generated verification
  scratch projects under `tools/OS.LibvirtRunner/artifacts` or sibling tool
  directories from becoming accidental source files.
- `--validate-paths` is an opt-in real-run readiness check that fails if the raw image file, serial log directory, or configured OVMF loader file is missing.
- `--validate-paths` also rejects empty images and checks the selected
  `--disk-format raw|qcow2` against the image header. A qcow2 magic header is
  required for `--disk-format qcow2`, and a qcow2 header is rejected when
  `--disk-format raw` is selected.
- `--diagnostics` emits `LIBVIRT_DIAG image.bytes=<n>` and
  `LIBVIRT_DIAG image.detected.format=raw-compatible|qcow2` so libvirt intent
  logs show the exact host-side image-format validation boundary before any
  domain lifecycle exists.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke --dry-run` writes a domain XML artifact and emits `TEST_PASS libvirt.smoke.dryrun` without requiring `virsh` or a libvirt daemon.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke --plan-lifecycle` writes the normal domain XML artifact, writes a lifecycle plan artifact, echoes the `LIBVIRT_LIFECYCLE_*` contract, and emits `TEST_PASS libvirt.lifecycle.plan` without requiring `virsh` or a libvirt daemon.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke --check-serial-only` invokes `OS.LibvirtRunner check-serial` against the configured serial log and writes the configured summary artifact without generating XML or requiring `virsh`.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke` accepts `--workload-profile SingleMinimalApi|MultiMinimalApi`, emits workload-profile evidence, and invokes the already-built .NET runner directly. No PowerShell or nested build is used.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke` will not start a domain by default even when `virsh` is present. The future real-run path requires `--allow-real-libvirt-lifecycle`; with the switch present the command still stops at `TEST_FAIL libvirt.smoke.real_run_not_implemented` until real lifecycle execution exists.
- `dotnet run --project tools/OS.Tooling -- libvirt smoke` without `--dry-run` validates the image, resolves `virsh` from `--virsh`, `VIRSH`, or PATH, and emits `TEST_SKIP libvirt.smoke missing_tool=virsh` plus `TEST_PASS libvirt.smoke.missing_tool_reported` when libvirt is not available. This is honest missing-tool evidence, not boot proof.
- `dotnet run --project tools/OS.Tooling -- ci smoke` and `dotnet run --project tools/OS.Tooling -- baseline evidence` record libvirt smoke intent by running the dry-run XML path and a deterministic missing-tool path with `--virsh` pointed at a non-existent file under the evidence directory. Both commands pass `--workload-profile` through those libvirt intent lanes. The missing-tool lane uses a small intent placeholder image so it can reach the `virsh` discovery branch without requiring a real qcow2 conversion.
- `dotnet run --project tools/OS.Tooling -- baseline evidence --plan-only` records libvirt dry-run and missing-tool artifact intent in `summary.json`, including the qcow2 image path, disk format, serial log, summary log, and domain XML path, without connecting to libvirt or starting a domain.

## Unsupported Behavior

Libvirt connection, transient domain creation, live serial marker polling, timeout handling, clean destroy, and host-side native library integration are pending. Lifecycle planning records the order and guardrails only; it does not connect to libvirt, define a domain, start a VM, poll a live serial log, or clean a live domain. `validate-xml` checks a local XML file only; it is generated-domain-shape evidence and does not prove libvirt accepts, defines, or boots the domain. `check-serial` validates an existing file only; it is preflight/parser evidence and does not prove libvirt can boot the domain. The path validation, XML validation, lifecycle-plan, check-serial-only, and missing-tool modes do not prove that libvirt can boot the domain.

## Tests

`tests/OS.HostTests/LibvirtRunnerTests.cs` covers network-none XML, OVMF loader XML, default virtio network XML, raw/qcow2 disk driver XML, XML validation pass/fail summaries, invalid disk-format rejection, lifecycle plan order/markers/opt-in guard, diagnostic readiness lines, required/forbidden marker diagnostics, smoke-wrapper dry-run/missing-tool/lifecycle/check-serial marker contracts, `check-serial` pass/missing-marker/forbidden-marker behavior, summary artifact writing, and opt-in image-path validation without requiring native libvirt. Image-path validation now covers missing images, empty images, diagnostic byte/header reporting, raw images accidentally passed as qcow2, and qcow2 images accidentally passed as raw. The runner tests execute the already-built `tools/OS.LibvirtRunner/bin/<configuration>/net10.0/OS.LibvirtRunner.dll` with `dotnet <dll>`, not nested `dotnet run --project`.

Host tool project-boundary tests validate the shared reviewed tool inventory and emit
`TEST_PASS tools.project boundary excludes artifact sources` and
`TEST_PASS tools.project boundary keeps expected host tools`.

CI-smoke failure-injection tests verify that the libvirt dry-run and
missing-tool lanes still pass when the injected process executor fails only
the managed build and host-test steps. Baseline evidence tests verify
that qcow2 and libvirt intent lanes carry the selected workload profile instead
of silently falling back to the default.

Evidence from 2026-05-24 for libvirt serial-marker preflight:

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo` built `OS.LibvirtRunner.dll` with 0 warnings/errors.
- `tools/OS.LibvirtRunner check-serial` emitted `TEST_PASS libvirt.serial.summary` for a fixture containing `OS_SMOKE_PASS`, `BOOT_HANDOFF_READY`, and a valid `APP_EXIT code=0 status=OSHOST_STATUS_SUCCESS` marker.
- The same check emitted `LIBVIRT_SERIAL_SUMMARY_STATUS pass`, `MISSING_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and wrote a summary artifact.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` built the host-test project with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS libvirt check-serial passes and writes summary`, `TEST_PASS libvirt check-serial rejects missing marker`, `TEST_PASS libvirt check-serial rejects forbidden marker`, and `TEST_PASS host.tests`.

Evidence from 2026-05-24 for libvirt image-format preflight:

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi` built `OS.LibvirtRunner.dll` with 0 warnings/errors.
- `OS.LibvirtRunner generate-xml --diagnostics --validate-paths` against a non-empty raw-compatible fixture emitted `LIBVIRT_DIAG image.bytes=8` and `LIBVIRT_DIAG image.detected.format=raw-compatible`.
- `OS.LibvirtRunner --dry-run --disk-format raw --validate-paths` against a qcow2-header fixture rejected the run with `Image disk format mismatch: expected raw but detected qcow2`.
- `OS.LibvirtRunner --dry-run --disk-format qcow2 --validate-paths` against a raw-compatible fixture rejected the run with `Image disk format mismatch: expected qcow2 but detected raw-compatible`.
- `OS.LibvirtRunner --dry-run --validate-paths` against an empty image rejected the run with `Image file is empty`.
- An isolated host-test run emitted `TEST_PASS libvirt validate-paths reports image format diagnostics`, `TEST_PASS libvirt validate-paths rejects raw qcow2 mismatch`, `TEST_PASS libvirt validate-paths rejects qcow2 raw mismatch`, and `TEST_PASS libvirt validate-paths rejects empty image`; the same run later failed in unrelated PAL status-contract tests because the isolated dependency build could not compile/load current PAL status-contract types.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` built `OS.LibvirtRunner.dll` with 0 warnings/errors.
- The integrated host-test run emitted `TEST_PASS libvirt generate-xml emits deterministic network-none XML`, `TEST_PASS libvirt --dry-run emits OVMF and default-network XML`, `TEST_PASS libvirt diagnostics emit marker requirements`, and `TEST_PASS libvirt validate-paths rejects missing image` through the built-DLL process path.

Evidence from 2026-05-22 for issue #302:

- `.\scripts\run-libvirt-smoke.ps1 -DryRun -Image artifacts/images/os.qcow2 -DiskFormat qcow2 -SerialLog artifacts/logs/libvirt-smoke-qcow2.log -DomainXml artifacts/logs/libvirt-smoke-qcow2.xml` emitted `TEST_PASS libvirt.smoke.dryrun`.
- The dry-run emitted `LIBVIRT_DISK_FORMAT qcow2` and the current default required markers including `BOOT_MEMORY_REGIONS_READY` and `BOOT_MEMORY_REGION_COUNT`.
- `.\scripts\run-libvirt-smoke.ps1 -Image artifacts/images/os.qcow2 -DiskFormat qcow2 -SerialLog artifacts/logs/libvirt-smoke-qcow2.log -DomainXml artifacts/logs/libvirt-smoke-qcow2.xml -Virsh C:\missing\virsh.exe` emitted `TEST_SKIP libvirt.smoke missing_tool=virsh` and `TEST_PASS libvirt.smoke.missing_tool_reported`.

Evidence from 2026-05-22 for issue #306:

- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\ci-smoke-step306-dryrun` emitted `TEST_PASS ci.smoke.dryrun includes libvirt smoke intent`.
- The generated `ci-smoke.artifacts.json` planned `ci.smoke.libvirt.dryrun` with `run-libvirt-smoke.ps1 -DryRun`, a qcow2 intent image, a serial log path, and `libvirt-dryrun.xml`.
- The generated `ci-smoke.artifacts.json` planned `ci.smoke.libvirt.missingtool` with `run-libvirt-smoke.ps1`, `-DiskFormat qcow2`, a serial log path, `libvirt-missing-tool.xml`, and a deterministic missing `-Virsh` path under the CI evidence directory.

Evidence from 2026-05-22 for issue #309:

- `dotnet run --project .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj -- plan-lifecycle --name os-plan --image artifacts/images/os.qcow2 --disk-format qcow2 --serial-log artifacts/logs/libvirt-plan.log --network default --timeout-seconds 45 --require-marker OS_SMOKE_PASS --require-marker BOOT_HANDOFF_READY --forbid-marker OS_SMOKE_FAIL` emitted `LIBVIRT_LIFECYCLE_MODE plan-only`, `LIBVIRT_LIFECYCLE_DEFAULT_START_DOMAIN false`, `LIBVIRT_LIFECYCLE_REAL_RUN_IMPLEMENTED false`, the required/forbidden marker contract, ordered lifecycle steps, cleanup order, and `LIBVIRT_LIFECYCLE_PLAN_READY`.
- `.\scripts\run-libvirt-smoke.ps1 -PlanLifecycle -Image artifacts/images/os.qcow2 -DiskFormat qcow2 -SerialLog artifacts/logs/libvirt-plan.log -DomainXml artifacts/logs/libvirt-plan.xml -LifecyclePlan artifacts/logs/libvirt-plan.lifecycle.log -TimeoutSeconds 45 -ForbidMarker OS_SMOKE_FAIL` emitted `TEST_PASS libvirt.lifecycle.plan` after writing XML and lifecycle plan artifacts.

## Known Limitations

`libvirt-csharp` is present locally and must remain host-side only. The current runner intentionally avoids a libvirt dependency until XML generation, diagnostics, missing-tool behavior, and tests are stable.

## Next Steps

1. Decide whether to reference/adapt `libvirt-csharp` directly in `OS.LibvirtRunner` or keep `virsh` as the first host dependency.
2. Add optional `test:///default` binding smoke after host-only dependency boundaries are documented.
3. Implement the planned lifecycle behind `-AllowRealLibvirtLifecycle` on a host with libvirt.
4. Wire real lifecycle serial polling to the now-host-tested `check-serial` parser contract.
5. Add transient-domain destroy/undefine cleanup evidence for timeout and failure paths.
6. Document host prerequisites for Linux/libvirt and Windows remote-libvirt workflows.
