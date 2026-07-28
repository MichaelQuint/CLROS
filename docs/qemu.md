# QEMU

## Current Status

QEMU smoke now has real OVMF boot proof for the EFI smoke path from both raw and qcow2 image artifacts. `scripts/run-qemu-smoke.ps1` builds `artifacts/images/os.raw` through `OS.ImageBuilder`, boots it with QEMU/OVMF, captures serial output to `artifacts/logs/qemu-smoke.log`, writes a concise summary artifact to `artifacts/logs/qemu-smoke.summary.log`, and requires the current boot boundary markers by default. `scripts/run-qemu-qcow2-smoke.ps1` converts the raw image to `artifacts/images/os.qcow2` through `qemu-img`, boots it with `--disk-format qcow2`, and writes separate qcow2 serial and summary artifacts.

This is an EFI application smoke, not a complete OS boot. It proves OVMF can load `\EFI\BOOT\BOOTX64.EFI` from the raw image and that the EFI entry path emits firmware-visible markers.

The current source-bound raw-image proof was captured on 2026-07-25 from
clean, pushed commit
`32a8f3b91e17e1fc7e4b296730a5ea6565c38369`. QEMU `11.0.50` and the
OVMF file with SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`
ran from `2026-07-25T21:42:47.9237542Z` through
`2026-07-25T21:42:56.7816592Z`. The run observed all 12 required markers,
including `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`, and
observed neither `TEST_FAIL` nor `OS_SMOKE_FAIL`. The evidence-manifest
SHA-256 is
`D398F73FF5051D138D5F2A77CE4EF51277BD2DE289F58BA5383FC6FC74D02E65`;
the EFI, image, and serial-log SHA-256 values are respectively
`0580A45FC10B93C7C218CAB9BA179549C278B4CA970C82420CE74684EDA6036F`,
`484A7B8D2C9E9C01BFF6AAA63633E3CEC3406A0FBAC487100DFB40B0A463428A`,
and
`C190355FB48902635AB4BC7EBD4BAEBD3E029619C851407A599D3BC9FB412BC7`.
The generated tuple is under
`artifacts/logs/m00-final/32a8f3b91e17-20260725T213702Z/`; it is ignored
build evidence, while `done/step846.md` is the committed ledger.

A separate opt-in M01-022 proof was captured on 2026-07-27 from clean
implementation commit
`198be8ad12db388fec44d76f9270c8d754430940`. From
`2026-07-27T19:03:19.7090684Z` through
`2026-07-27T19:03:39.0168910Z`, QEMU `11.0.50` and the same OVMF bytes
rebuilt and booted the `M01_LOADED_IMAGE_STACK_PROBE` artifact. The streaming
runner accepted the 12 configured stage-one markers exactly once and in
order, reporting `orderedSeen=12`, `outOfOrder=0`, `uniqueSeen=12`,
`duplicateUnique=0`, and `TEST_PASS qemu.smoke`. The EFI, raw image, serial
log, summary, runner-output, and evidence-manifest SHA-256 values are,
respectively,
`62BCD4902A55DDDACCA8C490C6B5615C1BEEF78BA7DF96038FA1687FD2B8DC60`,
`0186D6B11DD31480B42F2ED3013E283ADEF7E425E5281872E58F9921E6F9CC02`,
`BF775826807B4B3E61B6E26C1DDD7101E0E16F141AC17384ADBC94C54CD1B1B9`,
`3EBDC0BCAAC146E2B8BEDBEE5B6C3ADA11413720558E874140975DF16186726A`,
`6CCF99739FB05CDAA0FA6F2A15C5ED0A14B156DAE48A8F30C56052F22C481F54`,
and
`2EC0D45A229CA749C5F238ED7156DBB29E70F4BBAAA563C9AB6D260B02742968`.
The ignored evidence tuple is under
`artifacts/logs/m01-022/198be8ad12db-20260727T190237Z/`; the durable ledger
is `done/step870.md`. This opt-in pre-`ExitBootServices` proof does not replace
the default Gate 2 tuple and does not change a completion-gate state.

A separate opt-in M01-024 proof was captured on 2026-07-27 from clean
implementation commit
`f7437521799e9fe974a9de4560db248f94addf6b`. From
`2026-07-27T21:21:20.2271722Z` through
`2026-07-27T21:21:43.5137043Z`, QEMU `11.0.50` and the same
3,653,632-byte OVMF file (SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`)
freshly rebuilt and booted an artifact with both
`M01_LOADED_IMAGE_STACK_PROBE` and `M01_LIVE_ALLOCATOR_PROBE` enabled.
The streaming runner reported
`status=Passed terminal=Success bytes=1511 lines=43 invalidBytes=15
invalidLines=1 orderedSeen=21 outOfOrder=0 uniqueSeen=21
duplicateUnique=0`; the invalid-byte/line counts were firmware noise and
contained no accepted marker. All 21 configured stage-one/stage-two markers
were observed exactly once and in order, and the summary reported
`MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
`DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
`FAIL_REASON none`.

The EFI, raw image, serial log, summary, runner output, and evidence-manifest
SHA-256 values are, respectively,
`ED1FDD62FF50ABE9AB30BBFAC85EBB7C095D3F31720C2571F31C5413726EFA13`,
`51D00F4B3302461C0FFF2466BAA26A7909C891B9E5434A3B2CE78374D9F6FF4B`,
`2C74EE4CC10475D85EAA090A1382BEF08F9C114FD213A5AEB6E487F4405BD1EC`,
`19925845684DBBEA93962C762B2653BFA2224BBFF9707B3E4C38D882F6E46FF5`,
`BC7D34774F8029BABC713E3AB19B0615E5BF9C896A2072965F53B74BDC76739C`,
and
`9FDB9B72EB708B2A1184725E009A168FBD0BC3FEA3B8FCB0EA93C7F416031A58`.
The ignored evidence tuple is under
`artifacts/logs/m01-024/f7437521799e-20260727T210930Z/`; the durable ledger
is `done/step872.md`. This source-bound result proves only the explicit
pre-`ExitBootServices` two-page loader-owned arena and one metadata
allocation/free/double-free-rejection cycle. It does not replace the default
Gate 2 tuple, satisfy the M01 exit criterion, or change a completion-gate
state.

## Implemented Behavior

- `tools/OS.QemuRunner` builds a QEMU command line for a raw or qcow2 GPT/FAT32 image, OVMF pflash, serial-file capture, AHCI disk attachment, and user-mode virtio networking.
- `tools/OS.QemuRunner` accepts `--disk-format raw|qcow2`, defaults to `raw`, and passes the selected format into the QEMU ESP drive argument.
- `tools/OS.QemuRunner` accepts `--workload-profile SingleMinimalApi|MultiMinimalApi`, defaults to `SingleMinimalApi` for direct CLI runs, and emits `WORKLOAD_PROFILE <value>` in dry-run, real-run console output, and real-run summary logs. Scripts still pass the selected workload profile explicitly.
- `tools/OS.QemuRunner` discovers QEMU and OVMF from explicit arguments, environment variables, and common install paths.
- Real mode fails with machine-readable reasons for missing image, missing QEMU, missing OVMF, timeout, missing serial marker, `OS_SMOKE_FAIL`, any `TEST_FAIL ...` marker, conflicting terminal state, cleanup failure, or a spontaneous nonzero QEMU exit.
- `scripts/run-qemu-smoke.ps1` preserves dry-run behavior without requiring QEMU, OVMF, or an image. Real mode now runs a script-level host preflight before optional EFI/image build and before launching `OS.QemuRunner`. The preflight resolves QEMU and OVMF, emits `TEST_START qemu.smoke.preflight`, `QEMU_SCRIPT_PREFLIGHT_QEMU`, `QEMU_SCRIPT_PREFLIGHT_OVMF`, and `TEST_PASS qemu.smoke.preflight`, or fails early with `TEST_FAIL qemu.smoke qemu_not_found` or `TEST_FAIL qemu.smoke ovmf_not_found`.
- Before launching the runner in real mode, `scripts/run-qemu-smoke.ps1` validates the selected image artifact and disk format, emits `QEMU_SCRIPT_PREFLIGHT_IMAGE` and `QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT`, and fails with `TEST_FAIL qemu.smoke image_not_found`, `image_empty`, or `disk_format_mismatch` when the artifact cannot be used.
- The runner polls the serial log while QEMU runs and terminates QEMU after all required markers appear. The boot timeout is a hard deadline checked before marker/drain completion on every poll. If an HTTP probe is configured, the runner keeps QEMU alive until the probe completes under the probe's separately bounded timeout.
- `tools/OS.QemuRunner` always requires the baseline markers `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`. Repeatable `--require-marker <marker>` arguments append requirements; they cannot replace the baseline or remove the success terminal.
- Repeatable `--require-ordered-marker <marker>` arguments define a
  case-sensitive line sequence, and repeatable
  `--require-unique-marker <marker>` arguments require exactly one matching
  line. Both forms imply required presence. Legacy `--require-marker` remains
  presence-only and continues to allow duplicate nonterminal markers.
- An order violation is latched while bounded collection continues, so a
  missing required marker remains distinguishable from a complete-but-reordered
  transcript. A duplicate unique marker is terminal immediately. When unique
  evidence has otherwise passed, a spontaneous QEMU exit with code zero is
  classified as `qemu_reboot_or_shutdown`; ordinary ordered presets and
  presence-only runs retain their earlier clean-exit behavior.
- `tools/OS.QemuRunner` accepts repeatable `--marker-preset baseline|acpi|app-args|app-exit|app-lifecycle|app-selection|cctor-preflight|cctor-work-items|diagnostics|fetch-http|fetch-http-endpoint|fetch-http-tcp-exchange|http-static-response|http-static-tcp-response|manifest-env|nativeaot-heap|pal-export|uefi-gone|virtio-rx-tcp-dispatch` arguments. Presets append opt-in required serial markers for probe images without changing default smoke behavior.
- The `acpi` marker preset requires `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY` in addition to `OS_SMOKE_PASS`, and enforces that five-marker ACPI sequence in serial order.
- The `app-args` marker preset requires `APP_ARGS_VECTOR_READY` in addition to the baseline boot markers. It is targeted contract tooling for future native app argument-vector smoke images and does not change default smoke behavior.
- The `app-exit` marker preset requires `APP_EXIT_OBSERVER_READY` in addition to the baseline boot markers. It is targeted contract tooling for future native-app smoke images and does not change default smoke behavior.
- The `app-lifecycle` marker preset requires `APP_LIFECYCLE_READY` in addition to the baseline boot markers. It is targeted contract tooling for future native app lifecycle smoke images and does not change default smoke behavior.
- The `app-selection` marker preset requires `APP_SELECTION_READY` in addition to the baseline boot markers. It is targeted marker-contract tooling for future app-selection smoke images and does not change default smoke behavior.
- The `cctor-preflight` marker preset requires `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY` in addition to the baseline boot markers. It is targeted contract tooling for future runtime smoke images and does not change default smoke behavior.
- The `cctor-work-items` marker preset requires `NATIVEAOT_CCTOR_WORK_ITEMS_READY` in addition to the baseline boot markers. It is targeted contract tooling for future runtime smoke images and does not change default smoke behavior.
- The `diagnostics` marker preset requires `FAULT_INFRA_READY`, `EARLY_DIAGNOSTICS_READY`, `DIAGNOSTIC_STUBS_MATERIALIZED`, `CONTROLLED_FAULT_PROBES_READY`, `CPU_FAULT_PANIC_READY`, `DIAGNOSTICS_CHAIN_READY`, and `DIAGNOSTIC_STUB_INVOCATION_READY` in addition to `OS_SMOKE_PASS`, and enforces that diagnostics-readiness sequence in serial order.
- The `fetch-http` marker preset requires `FETCH_APP_HTTP_COMPLETION_READY` in addition to the baseline boot markers. It is targeted contract tooling for future FetchApp smoke images and does not change default smoke behavior.
- The `fetch-http-endpoint` marker preset requires `FETCHAPP_HTTP_ENDPOINT_READY` in addition to the baseline boot markers. It is targeted contract tooling for future FetchApp endpoint smoke images and does not change default smoke behavior.
- The `fetch-http-tcp-exchange` marker preset requires `FETCH_APP_HTTP_TCP_EXCHANGE_READY` in addition to the baseline boot markers. It is targeted contract tooling for future FetchApp HTTP-over-TCP exchange smoke images and does not change default smoke behavior. The nested diagnostic payload on that marker line remains `FETCH_HTTP_TCP_EXCHANGE ...`.
- The `http-static-response` marker preset requires `HTTP_STATIC_RESPONSE_READY` and `HTTP_STATIC_RESPONSE` in addition to the baseline boot markers. It is targeted contract tooling for future tiny HTTP static-response smoke images and does not change default smoke behavior.
- The `http-static-tcp-response` marker preset requires `HTTP_STATIC_TCP_RESPONSE_READY` and `HTTP_STATIC_TCP_RESPONSE` in addition to the baseline boot markers. It is targeted contract tooling for future tiny HTTP static TCP-response smoke images and does not change default smoke behavior.
- The `virtio-rx-tcp-dispatch` marker preset requires `VIRTIO_NET_RX_TCP_DISPATCH_READY` and `VIRTIO_NET_RX_TCP_DISPATCH` in addition to the baseline boot markers. It is targeted contract tooling for future virtio-net RX TCP dispatch smoke images and does not change default smoke behavior.
- The `manifest-env` marker preset requires `NATIVE_MANIFEST_ENV_READY` in addition to the baseline boot markers. It is targeted contract tooling for future native manifest environment smoke images and does not change default smoke behavior.
- The `nativeaot-heap` marker preset requires `NATIVEAOT_HEAP_BOOTSTRAP_READY` in addition to the baseline boot markers. It is targeted contract tooling for future NativeAOT heap-bootstrap smoke images and does not change default smoke behavior.
- The `pal-export` marker preset requires `PAL_EXPORT_CATALOG_READY` in addition to the baseline boot markers. It is targeted contract tooling for future PAL export smoke images and does not change default smoke behavior.
- The `uefi-gone` marker preset requires `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS` in addition to `OS_SMOKE_PASS`.
- `tools/OS.QemuRunner` forbids `OS_SMOKE_FAIL` and `TEST_FAIL` by default and accepts repeatable `--forbid-marker <marker>` arguments that append further exclusions. Exact marker or `marker + " " + metadata` matching makes every `TEST_FAIL <name> ...` line fatal. Forbidden markers are checked independently from required markers and fail the smoke with `OS_SMOKE_FAIL forbidden-marker=<marker>` plus `TEST_FAIL qemu.smoke forbidden_marker marker=<marker>`.
- `tools/OS.QemuRunner` accepts optional `--summary-log <path>` for real-mode runs. The summary is best-effort; write failures emit `SUMMARY_LOG_WRITE_FAILED ...` without changing the smoke exit code.
- Runner option validation occurs before any summary or serial artifact can be written and is repeated after QEMU/OVMF discovery. Canonical image, requested/resolved QEMU, requested/resolved OVMF, serial-log, and summary-log paths must be distinct, including case-insensitive aliases on Windows, so failure reporting cannot overwrite a tool, firmware, captured log, or input artifact.
- QEMU output is inherited rather than accumulated in unbounded redirected strings. Every post-launch path has a process-cleanup `finally`; runner-requested tree termination waits at most two seconds and a failed bounded cleanup is a smoke failure. The classifier records whether a kill was actually issued, rather than merely intended. `QEMU_PROCESS_RESULT disposition=<...> exitCode=<...>` therefore distinguishes runner termination, an exit observed before a kill, and cleanup failure; a spontaneous nonzero QEMU exit fails even when valid markers were emitted first.
- Real-mode summary files use stable fields: `QEMU_SMOKE_STATUS`, `IMAGE_ARTIFACT`, `SERIAL_LOG`, `WORKLOAD_PROFILE`, `REQUIRED_MARKERS`, `ORDERED_MARKERS`, `UNIQUE_MARKERS`, `FORBIDDEN_MARKERS`, `OBSERVED_MARKER`, `APP_EXIT_RESULT`, `APP_EXIT_MALFORMED`, `MISSING_MARKERS`, `OUT_OF_ORDER_MARKERS`, `DUPLICATE_UNIQUE_MARKERS`, `FORBIDDEN_MARKER_HIT`, `FAIL_REASON`, `SERIAL_TAIL_LIMIT`, and `SERIAL_TAIL_LINE`.
- `EvidenceManifestWriter` is a host-side deterministic JSON evidence writer
  for a completed run. It queries the supplied Git repository for `HEAD` and
  porcelain dirty state, compares those independently derived values with the
  expected commit/dirty assertions, captures and verifies all artifact bytes,
  then queries Git again before serialization. The manifest records the
  derived source state, UTC start/completion timestamps, command, exit
  classification, sorted tool versions, sorted artifact/config/serial-log
  paths with nonzero byte lengths and verified SHA-256 values, plus required,
  forbidden, and serial-derived observed markers.
- The `evidence-manifest` CLI requires `--repository`,
  `--expected-source-commit`, and `--expected-source-dirty`; it does not accept
  a caller-supplied observed source commit or dirty flag. It also requires
  `--output`, `--started-utc`, `--completed-utc`, `--command-file`,
  `--exit-code`, `--classification`, at least one `--tool`, and exactly one
  artifact in each required role (`config`, `efi`, `image`, and
  `serial-log`). Repeatable `--require-marker`, `--forbid-marker`, and
  `--observed-marker` values are claims checked against the serial-log bytes,
  not independent evidence.
- The serial log is read once under the same handle used to calculate its
  SHA-256, decoded as strict UTF-8, and parsed for marker lines. Required and
  forbidden marker claims are evaluated directly against that content;
  caller-reported observed marker lines must occur exactly in it; and the
  manifest writes marker lines derived from it. A passing record additionally
  requires exit code zero, `OS_SMOKE_PASS`, every required marker, no forbidden
  marker, no `TEST_FAIL`, no `OS_SMOKE_FAIL`, and no conflicting terminal
  markers. Failed and timed-out records remain preservable with nonzero exit
  codes and do not become passing evidence.
- The evidence writer rejects missing/default fields, malformed commit/hash
  values, non-UTC or reversed timestamps, a missing/non-Git repository,
  expected-versus-derived commit or dirty-state mismatches, source changes
  during capture, unreadable/empty inputs, artifact hash mismatches, duplicate
  markers/artifact names, missing or ambiguous required artifact roles,
  output/input overlap, marker claims not present in the hashed serial log,
  inconsistent pass records, invalid serial UTF-8, and simultaneous
  `OS_SMOKE_PASS`/`OS_SMOKE_FAIL`. Successful output remains deterministic
  UTF-8 without BOM, is installed through an atomic temporary-file
  replacement, and returns the manifest SHA-256.
- The summary includes a bounded tail of the serial log for triage. It records
  the last eight non-empty serial lines, trimming each line to 240 characters.
  This is diagnostic context for pass/fail analysis only; it does not expand
  the boot-proof claim beyond the required marker result.
- `tools/OS.QemuRunner preflight` and `--preflight` validate a local image
  artifact and runner input contract without launching QEMU, requiring libvirt,
  or doing host QEMU/OVMF discovery. The preflight emits
  `TEST_START qemu.runner.preflight`, `QEMU_PREFLIGHT_*` image, disk-format,
  serial-directory, optional explicit OVMF, and command-argument summary lines,
  then `TEST_PASS qemu.runner.preflight` or a deterministic `TEST_FAIL`
  reason.
- QEMU preflight checks that the configured image exists, is non-empty, and
  does not have a qcow2 magic header when `--disk-format raw` is selected. When
  `--disk-format qcow2` is selected, the preflight requires the qcow2 magic
  header. If `--ovmf-code` is explicitly supplied, the path must exist; omitted
  OVMF remains deferred to the real runner's normal host discovery.
- `SerialMarkerSummary` is the pure text parser for runner serial marker summaries. It is owned by `OS.QemuRunner` and source-linked into `OS.LibvirtRunner` so QEMU and libvirt preflight tooling share required-marker, forbidden-marker, observed-marker, ordered-marker, and `APP_EXIT` semantics without launching QEMU or starting a libvirt domain.
- `tools/OS.QemuRunner` accepts an optional HTTP probe contract through `--http-probe-url`, `--http-probe-expected-status`, `--http-probe-expected-body`, and `--http-probe-timeout`.
- Host tool projects inherit `tools/Directory.Build.props`, which excludes each
  tool's local `artifacts/**` tree from SDK default source globs. This keeps
  generated verification scratch files under a tool directory from being
  compiled into `OS.QemuRunner` or the other host tools.
- HTTP probe URLs must use `http://`. `https://` and TLS are rejected until TLS support exists and has guest evidence.
- Dry-run output emits `HTTP_PROBE url=<url> expected_status=<code> expected_body_bytes=<bytes> timeout_seconds=<seconds>` without printing the expected body text.
- Real QEMU mode runs the HTTP probe only after configured serial required markers are satisfied. It emits `HTTP_PROBE_START`, `HTTP_PROBE_PASS`, or `HTTP_PROBE_FAIL` with endpoint, status, byte count, response body SHA-256, and elapsed time. It does not print the expected or actual response body.
- Real QEMU mode fails with `TEST_FAIL qemu.runner.http_probe reason=<reason>` and `OS_SMOKE_FAIL http-probe=<reason>` when status, expected body, request, or timeout validation fails.
- `scripts/run-qemu-smoke.ps1 -DryRun` validates runner wiring without requiring QEMU, OVMF, or an existing image.
- `scripts/run-qemu-smoke.ps1` accepts `-WorkloadProfile SingleMinimalApi|MultiMinimalApi`, validates it before invoking tools, passes it to MSBuild-backed `dotnet run` calls as `/p:OSWorkloadProfile=<value>`, and passes it to `OS.QemuRunner` as `--workload-profile <value>`.
- `scripts/run-qemu-smoke.ps1` defaults to `artifacts/efi/BOOTX64.EFI`, builds `artifacts/images/os.raw`, captures serial output to `artifacts/logs/qemu-smoke.log`, writes `artifacts/logs/qemu-smoke.summary.log`, and forwards one or more `-RequireMarker` values to the runner.
- `scripts/run-qemu-smoke.ps1` accepts `-DiskFormat raw|qcow2`, defaults to `raw`, validates the value before invoking the runner, and supports comma-delimited marker lists in script-level `-RequireMarker` and `-ForbidMarker` arguments.
- `scripts/run-qemu-smoke.ps1` accepts comma-delimited or repeated
  `-RequireOrderedMarker` and `-RequireUniqueMarker` values, removes empty
  entries and ordinal duplicates, and forwards each normalized value to the
  corresponding runner option. Its dry-run evidence prints
  `QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS` and
  `QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS`.
- `-EnableM01LoadedImageStackProbe` is fail-closed evidence selection. It
  requires `-BuildEfi`, rejects `-SkipImageBuild`, and forwards the probe only
  to the fresh EFI build; an old image cannot be silently accepted as
  M01-022 evidence.
- `-EnableM01LiveAllocatorProbe` is the stricter nested M01-024 evidence
  selector. It requires `-EnableM01LoadedImageStackProbe` and `-BuildEfi`,
  rejects `-SkipImageBuild`, emits
  `QEMU_SCRIPT_M01_LIVE_ALLOCATOR_PROBE`, and forwards both probe switches
  only to the fresh EFI build. A stage-two-only, stale-EFI, or stale-image
  selection fails before QEMU launch.
- Script defaults require `OS_BOOT_OK`, `TEST_PASS boot.sequence`,
  `OS_SMOKE_PASS`, `UEFI_MEMORY_MAP_OWNED`, `BOOT_HANDOFF_READY`,
  `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`,
  `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT`,
  `BOOT_PAGE_ALLOCATOR_READY`, `BOOT_ACPI_RSDP_HANDOFF_READY`, and
  `UEFI_ACPI_RSDP_READY`.
- `scripts/run-qemu-smoke.ps1` accepts `-MarkerPreset baseline|acpi|app-args|app-exit|app-lifecycle|app-selection|cctor-preflight|cctor-work-items|diagnostics|fetch-http|fetch-http-endpoint|fetch-http-tcp-exchange|http-static-response|http-static-tcp-response|manifest-env|nativeaot-heap|pal-export|uefi-gone|virtio-rx-tcp-dispatch` and forwards the selected preset to the runner. It supports repeated or comma-delimited preset values and emits `QEMU_MARKER_PRESETS <list>` before runner dry-run output.
- `app-args` also accepts aliases `app_args`, `app-arguments`, and `native-app-args`; these normalize to the canonical `app-args` preset before the runner receives them.
- `app-lifecycle` also accepts aliases `app_lifecycle`, `native-app-lifecycle`, and `applifecycle`; these normalize to the canonical `app-lifecycle` preset before the runner receives them.
- `app-selection` also accepts aliases `app_selection`, `native-app-selection`, and `appselection`; these normalize to the canonical `app-selection` preset before the runner receives them.
- `cctor-work-items` also accepts aliases `cctor_work_items`, `cctor-workitem`, and `cctor-work-item`; these normalize to the canonical `cctor-work-items` preset before the runner receives them.
- `fetch-http-tcp-exchange` also accepts aliases `fetch_http_tcp_exchange`, `fetch-http-exchange`, and `fetchapp-http-tcp-exchange`; these normalize to the canonical `fetch-http-tcp-exchange` preset before the runner receives them.
- `http-static-response` also accepts aliases `http_static_response`, `tiny-http-static`, and `tinyhttp-static`; these normalize to the canonical `http-static-response` preset before the runner receives them.
- `http-static-tcp-response` also accepts aliases `http_static_tcp_response`, `tiny-http-tcp-static`, and `tinyhttp-tcp-static`; these normalize to the canonical `http-static-tcp-response` preset before the runner receives them.
- `virtio-rx-tcp-dispatch` also accepts aliases `virtio_rx_tcp_dispatch`, `virtio-net-rx-tcp-dispatch`, and `virtionet-rx-tcp-dispatch`; these normalize to the canonical `virtio-rx-tcp-dispatch` preset before the runner receives them.
- `manifest-env` also accepts aliases `native-manifest-env`, `manifest_environment`, and `manifestenv`; these normalize to the canonical `manifest-env` preset before the runner receives them.
- `nativeaot-heap` also accepts aliases `nativeaot_heap`, `heap-bootstrap`, and `nativeaot-heap-bootstrap`; these normalize to the canonical `nativeaot-heap` preset before the runner receives them.
- `pal-export` also accepts aliases `pal_export`, `pal-catalog`, and `pal-export-catalog`; these normalize to the canonical `pal-export` preset before the runner receives them.
- Passing `-RequireMarker` explicitly replaces the defaults for targeted probes such as UEFI-gone simulation variants.
- Passing `-RequireNativeAppMarkers` appends the host-tested native app readiness marker sequence to the active required marker list: `APP_PACKAGE_READY`, `APP_LAUNCH_READY`, `APP_PAYLOAD_READY`, `APP_EXEC_MAPPING_READY`, `APP_EXEC_MEMORY_READY`, `APP_INVOCATION_READY`, and `APP_ENTRY_READY`. This is a dry-run-friendly preset for future native-app images; it does not make those markers part of the default smoke path.
- Passing `-ForbidMarker` adds negative marker assertions without changing required marker behavior. Use it to prove disabled firmware probes remain inactive on the default smoke path.
- `scripts/build-efi-shell.ps1 -EnableUefiGoneSimulationProbe` builds an explicit simulation artifact that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `scripts/build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe` builds an explicit ACPI physical discovery artifact that emits the five `ACPI_*` validation markers before the normal boot marker plan.
- `scripts/run-baseline-evidence.ps1` requires a new or empty evidence
  directory, runs the solution build, explicit host-test build, `--no-build`
  host execution, naming/source firewall, QEMU dry runs, qcow2/libvirt intent,
  `git diff --check`, and `git status --short`, and records `qemu-img`
  discovery before the qcow2 lane. An executed summary is accepted only when
  the fresh host log contains exactly one `TEST_PASS host.tests`, contains no
  terminal `TEST_FAIL host.tests`, and round-trips to a consistent summary.
  `-WorkloadProfile` remains propagated through the build, QEMU, qcow2, and
  libvirt lanes.
- `scripts/run-baseline-evidence.ps1 -PlanOnly` writes a plan-only `os.baseline.evidence.summary.v0` summary without executing build, QEMU, qcow2, or libvirt commands. The summary records stable artifact fields for `baseline.qemu.diagnostics.dryrun`, `baseline.qemu.qcow2.*`, and libvirt intent lanes, including serial logs, summary logs, qcow2 image/disk-format intent, `qemuImgDiscovery`, `qcow2ConversionIntent`, libvirt domain XML, and the diagnostics marker preset.
- `scripts/run-ci-smoke.ps1` creates a timestamped `artifacts/logs/ci-smoke-*` evidence directory, writes `ci-smoke.artifacts.json` and `ci-smoke.summary.json`, captures managed build and host-test logs, captures the default QEMU dry-run log, captures a native-app marker dry-run, captures diagnostics, app-args, app-exit, cctor-preflight, cctor-work-items, fetch-http, fetch-http-endpoint, fetch-http-tcp-exchange, http-static-response, http-static-tcp-response, virtio-rx-tcp-dispatch, manifest-env, nativeaot-heap, and pal-export marker-preset dry-run lanes, captures qcow2 smoke intent through `scripts/run-qemu-qcow2-smoke.ps1 -DryRun`, records `qemu-img` discovery and qcow2 conversion intent for the qcow2 lane, records libvirt dry-run/missing-tool intent artifacts, and records a `git diff --check` lane. These CI marker-preset lanes are artifact-contract dry-runs only and do not launch real QEMU.
- `scripts/run-ci-smoke.ps1` passes `-WorkloadProfile` through QEMU, qcow2,
  and libvirt lanes. This keeps nested tool builds aligned with the repo-wide
  `OSWorkloadProfile` validation target.
- `scripts/run-ci-smoke.ps1 -RunQcow2RealSmoke` switches only the qcow2 lane from intent dry-run to the configured real qcow2 wrapper. Use it only with valid `-Qemu`, `-QemuImg`, and `-OvmfCode` inputs or equivalent host discovery.
- `tools/OS.ImageBuilder convert-qcow2` can convert an existing raw image to qcow2 through `qemu-img`, with dry-run discovery and skip behavior that does not require `qemu-img`.
- `scripts/run-qemu-qcow2-smoke.ps1` wraps the current qcow2 evidence flow: optional EFI build, raw image build, `convert-qcow2`, and QEMU smoke with `-DiskFormat qcow2`.

Baseline qemu-img plan diagnostics:

```text
TEST_START baseline.qemu_img.discovery
BASELINE_QEMU_IMG_DISCOVERY requested=<auto|path> source=<configured|environment|PATH|well-known|auto> available=<true|false> resolved=<path|not_found>
BASELINE_QCOW2_CONVERSION_INTENT mode=<dry-run|real> status=<planned|blocked_by_missing_qemu_img> qemuImgRequired=<true|false> qemuImgAvailable=<true|false>
TEST_PASS baseline.qemu_img.discovery
```

CI smoke qemu-img plan diagnostics:

```text
TEST_START ci.smoke.qemu_img.discovery
CI_SMOKE_QEMU_IMG_DISCOVERY requested=<auto|path> source=<configured|environment|PATH|well-known|auto> available=<true|false> resolved=<path|not_found>
CI_SMOKE_QCOW2_CONVERSION_INTENT mode=<dry-run|real> status=<planned|blocked_by_missing_qemu_img> qemuImgRequired=<true|false> qemuImgAvailable=<true|false>
TEST_PASS ci.smoke.qemu_img.discovery
```

Dry run:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun
```

Expected workload profile evidence includes:

```text
WORKLOAD_PROFILE SingleMinimalApi
TEST_PASS workload.profile.tooling default single
```

Expected default dry-run marker evidence includes:

```text
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY
```

Direct local image preflight:

```powershell
dotnet run --project .\tools\OS.QemuRunner\OS.QemuRunner.csproj -- preflight --image artifacts/images/os.raw --disk-format raw --serial-log artifacts/logs/qemu-smoke.log
```

Expected preflight marker evidence includes:

```text
TEST_START qemu.runner.preflight
QEMU_PREFLIGHT_IMAGE path=artifacts/images/os.raw exists=true bytes=<n>
QEMU_PREFLIGHT_DISK_FORMAT expected=raw detected=raw-compatible
QEMU_PREFLIGHT_COMMAND_ARGUMENTS count=<n>
TEST_PASS qemu.runner.preflight
```

Dry run with the native app readiness preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
```

Expected dry-run marker evidence includes the default smoke markers plus the host-tested app readiness markers:

```text
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_PACKAGE_READY;APP_LAUNCH_READY;APP_PAYLOAD_READY;APP_EXEC_MAPPING_READY;APP_EXEC_MEMORY_READY;APP_INVOCATION_READY;APP_ENTRY_READY
```

Dry run with the opt-in ACPI physical discovery marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset acpi
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in ACPI validation markers:

```text
QEMU_MARKER_PRESETS acpi
MARKER_PRESETS acpi
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;ACPI_RSDP_SNAPSHOT_READY;ACPI_RSDP_VALIDATED;ACPI_ROOT_SDT_SNAPSHOT_READY;ACPI_ROOT_SDT_VALIDATED;ACPI_MCFG_DISCOVERY_READY
```

Dry run with the future app-exit observer marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-exit
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in app-exit observer readiness marker:

```text
QEMU_MARKER_PRESETS app-exit
MARKER_PRESETS app-exit
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_EXIT_OBSERVER_READY
```

Dry run with the future native app lifecycle marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-lifecycle
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in native app lifecycle readiness marker:

```text
QEMU_MARKER_PRESETS app-lifecycle
MARKER_PRESETS app-lifecycle
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_LIFECYCLE_READY
```

Dry run with the future app-selection marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-selection
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in app-selection readiness marker:

```text
QEMU_MARKER_PRESETS app-selection
MARKER_PRESETS app-selection
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_SELECTION_READY
```

Dry run with the future native app argument-vector marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-args
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in native app argument-vector readiness marker:

```text
QEMU_MARKER_PRESETS app-args
MARKER_PRESETS app-args
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_ARGS_VECTOR_READY
```

Dry run with the future NativeAOT cctor preflight marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset cctor-preflight
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in runtime cctor preflight readiness marker:

```text
QEMU_MARKER_PRESETS cctor-preflight
MARKER_PRESETS cctor-preflight
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY
```

Dry run with the future NativeAOT cctor work-item marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset cctor-work-items
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in runtime cctor work-item readiness marker:

```text
QEMU_MARKER_PRESETS cctor-work-items
MARKER_PRESETS cctor-work-items
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;NATIVEAOT_CCTOR_WORK_ITEMS_READY
```

Dry run with the host-only diagnostics readiness marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset diagnostics
```

Expected dry-run marker evidence includes the current script defaults plus the diagnostics readiness-chain markers:

```text
QEMU_MARKER_PRESETS diagnostics
MARKER_PRESETS diagnostics
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY;DIAGNOSTIC_STUB_INVOCATION_READY
ORDERED_MARKERS FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY;DIAGNOSTIC_STUB_INVOCATION_READY
```

Dry run with the future FetchApp HTTP completion marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in FetchApp completion readiness marker:

```text
QEMU_MARKER_PRESETS fetch-http
MARKER_PRESETS fetch-http
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;FETCH_APP_HTTP_COMPLETION_READY
```

Dry run with the future FetchApp HTTP endpoint marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http-endpoint
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in FetchApp endpoint readiness marker:

```text
QEMU_MARKER_PRESETS fetch-http-endpoint
MARKER_PRESETS fetch-http-endpoint
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;FETCHAPP_HTTP_ENDPOINT_READY
```

Dry run with the future FetchApp HTTP TCP exchange marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http-tcp-exchange
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in FetchApp HTTP TCP exchange marker:

```text
QEMU_MARKER_PRESETS fetch-http-tcp-exchange
MARKER_PRESETS fetch-http-tcp-exchange
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;FETCH_APP_HTTP_TCP_EXCHANGE_READY
```

Dry run with the future tiny HTTP static response marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset http-static-response
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in HTTP static response readiness and diagnostic markers:

```text
QEMU_MARKER_PRESETS http-static-response
MARKER_PRESETS http-static-response
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;HTTP_STATIC_RESPONSE_READY;HTTP_STATIC_RESPONSE
```

Dry run with the future tiny HTTP static TCP response marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset http-static-tcp-response
```

Expected dry-run marker evidence includes the current script defaults plus the
opt-in HTTP static TCP response readiness and diagnostic markers:

```text
QEMU_MARKER_PRESETS http-static-tcp-response
MARKER_PRESETS http-static-tcp-response
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;HTTP_STATIC_TCP_RESPONSE_READY;HTTP_STATIC_TCP_RESPONSE
```

Dry run with the future native manifest environment marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset manifest-env
```

Expected dry-run marker evidence includes the current script defaults plus the opt-in native manifest environment readiness marker:

```text
QEMU_MARKER_PRESETS manifest-env
MARKER_PRESETS manifest-env
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;NATIVE_MANIFEST_ENV_READY
```

Dry run with the future UEFI-gone and owned-serial readiness marker preset:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset uefi-gone
```

Expected dry-run marker evidence includes the current script defaults plus the future firmware-denial and owned-serial readiness markers:

```text
QEMU_MARKER_PRESETS uefi-gone
MARKER_PRESETS uefi-gone
REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;UEFI_GONE_SIM_START;UEFI_GONE_SIM_PLATFORM_DENIED;POST_FIRMWARE_DIAGNOSTICS_READY;UEFI_GONE_SIM_PASS
```

Dry run with the later multi-app profile selected:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -WorkloadProfile MultiMinimalApi
```

Expected workload profile evidence includes:

```text
WORKLOAD_PROFILE MultiMinimalApi
TEST_PASS workload.profile.tooling propagates multi
```

CI smoke artifact-plan dry run:

```powershell
.\scripts\run-ci-smoke.ps1 -DryRun
```

To record the multi profile in CI dry-run evidence:

```powershell
.\scripts\run-ci-smoke.ps1 -DryRun -WorkloadProfile MultiMinimalApi
```

Expected CI dry-run markers:

```text
TEST_PASS ci.smoke.qemu_img.discovery
TEST_PASS ci.smoke.dryrun writes artifact plan
TEST_PASS ci.smoke.dryrun includes serial and summary logs
TEST_PASS ci.smoke.dryrun includes diagnostics marker preset
TEST_PASS ci.smoke.dryrun includes app-args marker preset
TEST_PASS ci.smoke.dryrun includes app-exit marker preset
TEST_PASS ci.smoke.dryrun includes cctor-preflight marker preset
TEST_PASS ci.smoke.dryrun includes cctor-work-items marker preset
TEST_PASS ci.smoke.dryrun includes fetch-http marker preset
TEST_PASS ci.smoke.dryrun includes fetch-http-endpoint marker preset
TEST_PASS ci.smoke.dryrun includes fetch-http-tcp-exchange marker preset
TEST_PASS ci.smoke.dryrun includes http-static-response marker preset
TEST_PASS ci.smoke.dryrun includes http static tcp response marker preset
TEST_PASS ci.smoke.dryrun includes virtio-rx-tcp-dispatch marker preset
TEST_PASS ci.smoke.dryrun includes manifest-env marker preset
TEST_PASS ci.smoke.dryrun includes nativeaot-heap marker preset
TEST_PASS ci.smoke.dryrun includes pal-export marker preset
TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent
TEST_PASS ci.smoke.dryrun includes libvirt smoke intent
TEST_PASS ci.smoke.dryrun includes git diff check
```

Local CI smoke artifact collection:

```powershell
.\scripts\run-ci-smoke.ps1
```

This writes per-step logs and a machine-readable summary under `artifacts/logs/ci-smoke-*`. It returns nonzero if any managed build, host-test, QEMU dry-run, native-app-marker dry-run, diagnostics-marker dry-run, app-args marker dry-run, app-exit marker dry-run, cctor-preflight marker dry-run, cctor-work-items marker dry-run, fetch-http marker dry-run, fetch-http-endpoint marker dry-run, fetch-http-tcp-exchange marker dry-run, http-static-response marker dry-run, http-static-tcp-response marker dry-run, virtio-rx-tcp-dispatch marker dry-run, manifest-env marker dry-run, nativeaot-heap marker dry-run, pal-export marker dry-run, qcow2 smoke-intent dry-run, libvirt XML dry-run, libvirt missing-tool command, or `git diff --check` lane returns nonzero. The QEMU and qcow2 steps still pass `-DryRun` by default and are artifact/command-shape evidence only. The app-args lane requires only the `app-args` marker-preset contract in dry-run output, the app-exit lane requires only the `app-exit` marker-preset contract in dry-run output, the cctor-preflight lane requires only the `cctor-preflight` marker-preset contract in dry-run output, the cctor-work-items lane requires only the `cctor-work-items` marker-preset contract in dry-run output, the fetch-http lane requires only the `fetch-http` marker-preset contract in dry-run output, the fetch-http-endpoint lane requires only the `fetch-http-endpoint` marker-preset contract in dry-run output, the fetch-http-tcp-exchange lane requires only the `fetch-http-tcp-exchange` marker-preset contract in dry-run output, the http-static-response lane requires only the `http-static-response` marker-preset contract in dry-run output, the http-static-tcp-response lane requires only the `http-static-tcp-response` marker-preset contract in dry-run output, the virtio-rx-tcp-dispatch lane requires only the `virtio-rx-tcp-dispatch` marker-preset contract in dry-run output, the manifest-env lane requires only the `manifest-env` marker-preset contract in dry-run output, the nativeaot-heap lane requires only the `nativeaot-heap` marker-preset contract in dry-run output, and the pal-export lane requires only the `pal-export` marker-preset contract in dry-run output. They do not prove guest app execution, native app argument-vector materialization, live app-exit observation, class-constructor execution, NativeAOT heap bootstrap, PAL export consumption, NativeAOT runtime-helper completion, FetchApp execution, manifest loading from disk, process environment materialization, live networking, virtio-net RX/TCP dispatch, HTTP GET, HTTP server/static response, HTTP static TCP response, TCP exchange, hosted .NET, or Kestrel. The libvirt missing-tool lane intentionally passes a missing `virsh` path and expects `TEST_SKIP libvirt.smoke missing_tool=virsh`; it is not a real libvirt boot attempt. Windows CI agent prerequisites and bring-up commands are canonical in `docs/host-requirements.md`.

Configured real qcow2 CI lane:

```powershell
.\scripts\run-ci-smoke.ps1 -RunQcow2RealSmoke -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -QemuImg "C:\Program Files\qemu\qemu-img.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd"
```

This keeps the libvirt lanes in dry-run/missing-tool mode and only changes the qcow2 wrapper lane from intent dry-run to configured real-run evidence.

Dry run with an alternate required marker:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireMarker UEFI_GONE_SIM_PASS
```

Dry run with disabled firmware probe markers forbidden:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -ForbidMarker UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_CALL_SKIPPED
```

Expected forbidden-marker evidence includes:

```text
FORBIDDEN_MARKERS UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_CALL_SKIPPED
```

Dry run requiring multiple serial markers:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireMarker OS_BOOT_OK,UEFI_GONE_SIM_PASS
```

Direct runner dry run with the future Kestrel HTTP probe contract:

```powershell
dotnet run --project .\tools\OS.QemuRunner\OS.QemuRunner.csproj -- --dry-run --image artifacts/images/os.raw --require-marker OS_SMOKE_PASS --require-marker "TEST_PASS kestrel.smoke" --http-probe-url http://127.0.0.1:8080/healthz --http-probe-expected-status 200 --http-probe-expected-body OS_MINIMAL_API_OK --http-probe-timeout 10
```

Expected dry-run evidence includes:

```text
REQUIRED_MARKERS OS_SMOKE_PASS;TEST_PASS kestrel.smoke
HTTP_PROBE url=http://127.0.0.1:8080/healthz expected_status=200 expected_body_bytes=17 timeout_seconds=10
```

Direct runner real mode can use the same probe options once the guest has
emitted the required serial marker and a reachable HTTP endpoint exists:

```powershell
dotnet run --project .\tools\OS.QemuRunner\OS.QemuRunner.csproj -- --image artifacts/images/os.raw --require-marker OS_SMOKE_PASS --require-marker "TEST_PASS kestrel.smoke" --http-probe-url http://127.0.0.1:8080/healthz --http-probe-expected-status 200 --http-probe-expected-body OS_MINIMAL_API_OK --http-probe-timeout 10
```

Expected real-mode HTTP probe evidence records metadata only:

```text
HTTP_PROBE_START endpoint=http://127.0.0.1:8080 expected_status=200 expected_body_bytes=17 timeout_seconds=10
HTTP_PROBE_PASS endpoint=http://127.0.0.1:8080 status=200 body_bytes=17 body_sha256=87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d elapsed_ms=<n>
```

Direct runner dry run for a converted qcow2 artifact:

```powershell
dotnet run --project .\tools\OS.QemuRunner\OS.QemuRunner.csproj -- --dry-run --image artifacts/images/os.qcow2 --disk-format qcow2
```

Expected dry-run evidence includes:

```text
if=none,id=esp,format=qcow2,file=artifacts/images/os.qcow2
TEST_PASS qemu.runner.dryrun
```

Wrapper dry run for the full qcow2 smoke flow:

```powershell
.\scripts\run-qemu-qcow2-smoke.ps1 -DryRun
```

Expected dry-run evidence includes:

```text
TEST_START qemu.qcow2.smoke
TEST_PASS imagebuilder.qcow2
if=none,id=esp,format=qcow2,file=artifacts/images/os.qcow2
TEST_PASS qemu.runner.dryrun
TEST_PASS qemu.qcow2.smoke
```

Real-run summary artifact shape:

```text
QEMU_SMOKE_STATUS pass
IMAGE_ARTIFACT artifacts/images/os.raw
SERIAL_LOG artifacts/logs/qemu-smoke.log
WORKLOAD_PROFILE SingleMinimalApi
REQUIRED_MARKERS OS_SMOKE_PASS
FORBIDDEN_MARKERS none
OBSERVED_MARKER OS_SMOKE_PASS
APP_EXIT_RESULT none
APP_EXIT_MALFORMED none
MISSING_MARKERS none
OUT_OF_ORDER_MARKERS none
FORBIDDEN_MARKER_HIT none
FAIL_REASON none
SERIAL_TAIL_LIMIT lines=8 chars=240
SERIAL_TAIL_LINE TEST_START efi.main
SERIAL_TAIL_LINE OS_SMOKE_PASS
```

`OBSERVED_MARKER` is emitted once for each marker-like serial line observed by the runner. If no marker-like lines were seen, the summary uses `OBSERVED_MARKER none`.

`SERIAL_TAIL_LINE` is emitted once for each retained non-empty serial tail line.
If the serial log is empty or unavailable, the summary uses
`SERIAL_TAIL_LINE none`.

Valid app-exit records are summarized separately as `APP_EXIT_RESULT code=<n> status=<OSHOST_STATUS_*>`. Malformed `APP_EXIT` records are summarized as `APP_EXIT_MALFORMED reason=<reason> line=<line>`, for example `APP_EXIT_MALFORMED reason=invalid_code line=APP_EXIT code=abc status=OSHOST_STATUS_OK`. Malformed app-exit records are diagnostic summary data only; they do not fail the QEMU smoke unless required markers are missing or forbidden markers are present.

Build and boot the UEFI-gone simulation EFI variant:

```powershell
.\scripts\build-efi-shell.ps1 -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -MarkerPreset uefi-gone
```

Build and boot the ACPI physical discovery EFI variant:

```powershell
.\scripts\build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe -EfiOutputPath artifacts/efi/BOOTX64-ACPI.EFI -LogPath artifacts/logs/efi-shell-publish-acpi.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset acpi
```

Real smoke on the current Windows host:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Real qcow2 smoke on the current Windows host:

```powershell
.\scripts\run-qemu-qcow2-smoke.ps1 -RawImage artifacts/images/os.raw -Qcow2Image artifacts/images/os.qcow2 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -QemuImg "C:\Program Files\qemu\qemu-img.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Real smoke proving disabled fresh-map probe markers are absent:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -ForbidMarker UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_CALL_SKIPPED
```

If `-OvmfCode` is omitted, the runner checks `OS_OVMF_CODE`, `OVMF_CODE`, `QEMU_OVMF_CODE`, the QEMU install directory, and common Windows/Linux OVMF locations.

## Unsupported Behavior

- WHPX/KVM acceleration selection is not implemented; QEMU uses TCG.
- ISO launch paths are not implemented in `tools/OS.QemuRunner`.
- libvirt real boot is not implemented.
- No ExitBootServices or owned driver path exists after the EFI smoke marker.
- QEMU runner HTTP probing is host-runner behavior only until it is pointed at a real guest endpoint. Local-loopback tests prove probe evaluation, not any hosted-workload completion gate; see `docs/completion-gates.md` and `docs/kestrel.md` for the full Kestrel/hosted .NET evidence boundary.
- QEMU runner `APP_EXIT` parsing is summary-parser behavior only. It does not prove a guest app ran, a live app exit was observed, a supervisor captured status, or restart policy executed.
- `-MarkerPreset http-static-response` is a required-marker preset only. It does not prove a live HTTP server, sockets, Kestrel, hosted .NET, or cloud readiness unless a real QEMU serial log from a matching guest image emits `HTTP_STATIC_RESPONSE_READY` and `HTTP_STATIC_RESPONSE`.
- `-MarkerPreset http-static-tcp-response` is a required-marker preset only. It does not prove live HTTP server behavior, sockets, hosted .NET, Minimal API, Kestrel, cloud readiness, or production readiness unless a real QEMU serial log from a matching guest image emits `HTTP_STATIC_TCP_RESPONSE_READY` and `HTTP_STATIC_TCP_RESPONSE`.
- Baseline/CI `qemu-img` discovery proves only host-tool discovery and qcow2
  lane planning. It does not prove conversion, QEMU boot, cloud import,
  provider readiness, hosted .NET, or Kestrel.
- TLS/HTTPS probing is not supported.
- `-RequireNativeAppMarkers` is a required-marker preset only. It does not prove the `APP_*` readiness markers are emitted by the live QEMU boot path and does not satisfy the app-model or hosted-workload completion gates.
- `-MarkerPreset app-exit` is a required-marker preset only. It does not prove live app execution, serial-log ingestion by a supervisor, restart-policy handling, or guest app-exit capture unless a real QEMU serial log from a matching guest image emits `APP_EXIT_OBSERVER_READY`.
- `-MarkerPreset app-lifecycle` is a required-marker preset only. It does not prove live app execution, supervisor start/stop behavior, app isolation, restart-policy handling, hosted .NET, Minimal API, or Kestrel unless a real QEMU serial log from a matching guest image emits `APP_LIFECYCLE_READY`.
- `-MarkerPreset app-selection` is a required-marker preset only. It does not prove live virtio-blk reads, disk-backed manifest/package loading, executable mapping, native app execution, hosted .NET, Minimal API, or Kestrel unless a real QEMU serial log from a matching guest image emits `APP_SELECTION_READY`.
- `-MarkerPreset cctor-preflight` is a required-marker preset only. It does not prove class constructors execute, static reference fields are fully supported, exception unwinding works, or NativeAOT runtime correctness is complete unless a real QEMU serial log from a matching guest image emits `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`.
- `-MarkerPreset cctor-work-items` is a required-marker preset only. It does not prove class constructors execute, metadata discovery works, module initializers run, exception unwinding works, or NativeAOT runtime correctness is complete unless a real QEMU serial log from a matching guest image emits `NATIVEAOT_CCTOR_WORK_ITEMS_READY`.
- `-MarkerPreset fetch-http` is a required-marker preset only. It does not prove live FetchApp execution, virtio-net, DHCP, DNS, TCP, HTTP GET, hosted .NET, Kestrel, or guest app completion unless a real QEMU serial log from a matching guest image emits `FETCH_APP_HTTP_COMPLETION_READY`.
- `-MarkerPreset fetch-http-endpoint` is a required-marker preset only. It does not prove live FetchApp execution, manifest loading from disk, virtio-net, DHCP, DNS, TCP, HTTP GET, hosted .NET, Kestrel, or guest endpoint use unless a real QEMU serial log from a matching guest image emits `FETCHAPP_HTTP_ENDPOINT_READY`.
- `-MarkerPreset fetch-http-tcp-exchange` is a required-marker preset only. It does not prove live FetchApp execution, virtio-net, DHCP, DNS, TCP connect/read/write/close, HTTP GET, hosted .NET, Kestrel, or guest app completion unless a real QEMU serial log from a matching guest image emits `FETCH_APP_HTTP_TCP_EXCHANGE_READY`.
- `-MarkerPreset virtio-rx-tcp-dispatch` is a required-marker preset only. It does not prove live virtio-net RX queue completion, packet I/O, TCP delivery, sockets, hosted .NET, Minimal API, or Kestrel unless a real QEMU serial log from a matching guest image emits `VIRTIO_NET_RX_TCP_DISPATCH_READY` and `VIRTIO_NET_RX_TCP_DISPATCH`.
- `-MarkerPreset manifest-env` is a required-marker preset only. It does not prove manifest loading from disk, process environment materialization, hosted .NET, Kestrel, or app execution unless a real QEMU serial log from a matching guest image emits `NATIVE_MANIFEST_ENV_READY`.
- `-MarkerPreset nativeaot-heap` is a required-marker preset only. It does not prove live heap bootstrap, allocation correctness, GC root scanning, hosted .NET, or Kestrel unless a real QEMU serial log from a matching guest image emits `NATIVEAOT_HEAP_BOOTSTRAP_READY`.
- `-MarkerPreset pal-export` is a required-marker preset only. It does not prove hosted CoreCLR consumption, ABI calls, hosted .NET, or Kestrel unless a real QEMU serial log from a matching guest image emits `PAL_EXPORT_CATALOG_READY`.
- `-MarkerPreset acpi` by itself is only an opt-in marker requirement. Paired with the ACPI physical discovery EFI artifact and a passing QEMU log, it proves only pre-`ExitBootServices` RSDP/root SDT/MCFG byte discovery on that tested firmware run.
- `-MarkerPreset acpi` does not prove ECAM mapping, PCI enumeration, BAR sizing/mapping, driver binding, MADT/APIC/HPET use, post-firmware physical-memory access, storage, networking, or hosted workload behavior.
- `-MarkerPreset uefi-gone` is an opt-in marker requirement only. It does not prove real `ExitBootServices` or post-firmware owned serial output until real QEMU serial logs emit those markers from a true firmware-shutdown path.
- `OS.QemuRunner preflight` is image/input validation only. It does not parse
  GPT/FAT32 contents, prove that QEMU accepts the command line, launch a VM,
  capture serial output, or satisfy boot/cloud completion gates.
- `scripts/run-qemu-smoke.ps1` host preflight is prerequisite evidence only. It
  proves the wrapper can find QEMU/OVMF and validate the selected image shape
  before launching the runner; it does not prove QEMU boot, serial markers,
  `ExitBootServices`, cloud readiness, hosted .NET, or Kestrel.

## Tests

Host tests validate the extracted QEMU command builder and runner contracts through dry-run output, including base command construction, serial log arguments, OVMF pflash arguments, default/custom required marker reporting, the script-level native app marker preset, currently supported virtio-net arguments, default raw disk format, qcow2 disk format, invalid disk format arguments, preflight pass/fail validation for raw image artifacts, missing images, and raw/qcow2 format mismatches, HTTP probe dry-run output, invalid HTTP probe arguments, real-mode local-loopback HTTP probe pass/fail/timeout behavior, app-exit summary output, and missing-image failure. Script-level host tests now also invoke `scripts/run-qemu-smoke.ps1` with fake paths to prove dry-run remains QEMU-free and real mode fails before the runner for missing QEMU, missing OVMF, and missing images. The QEMU runner CLI tests execute the already-built `tools/OS.QemuRunner/bin/<configuration>/net10.0/OS.QemuRunner.dll` with `dotnet <dll>`, not nested `dotnet run --project`. Host tests also validate `--summary-log` without requiring real QEMU by using deterministic fake QEMU command scripts that write serial markers, including app-exit summary artifact coverage. Focused parser tests validate that `SerialMarkerSummary` passes required markers, reports missing markers, rejects marker-substring false positives, reports forbidden markers, preserves observed marker order including `ACPI_` lines, reports ordered marker failures, observes `APP_EXIT` marker lines, extracts app-exit status records, reports absent app-exit status, reports malformed app-exit markers with reasons, and keeps required-marker behavior unchanged. QEMU preset host tests now cover the `app-args` preset and aliases `app_args`, `app-arguments`, and `native-app-args`, the `app-lifecycle` preset and aliases `app_lifecycle`, `native-app-lifecycle`, and `applifecycle`, the `fetch-http-endpoint` preset and aliases `fetch_endpoint`, `fetchhttp-endpoint`, and `fetchapp-http-endpoint`, the `fetch-http-tcp-exchange` preset and aliases `fetch_http_tcp_exchange`, `fetch-http-exchange`, and `fetchapp-http-tcp-exchange`, the `http-static-response` preset and aliases `http_static_response`, `tiny-http-static`, and `tinyhttp-static`, the `http-static-tcp-response` preset and aliases `http_static_tcp_response`, `tiny-http-tcp-static`, and `tinyhttp-tcp-static`, the `virtio-rx-tcp-dispatch` preset and aliases `virtio_rx_tcp_dispatch`, `virtio-net-rx-tcp-dispatch`, and `virtionet-rx-tcp-dispatch`, the `cctor-work-items` preset and aliases `cctor_work_items`, `cctor-workitem`, and `cctor-work-item`, and the `manifest-env` preset and aliases `native-manifest-env`, `manifest_environment`, and `manifestenv`, without claiming live argument-vector materialization, native app lifecycle execution, FetchApp, networking, TCP exchange, HTTP static response service, HTTP static TCP response service, virtio-net RX/TCP dispatch, manifest loading, environment materialization, app execution, or class-constructor execution proof. CI smoke host tests validate that `scripts/run-ci-smoke.ps1 -DryRun` writes the artifact plan and includes planned serial, summary, image, marker preset, and domain XML artifacts for raw QEMU dry-run, native-app marker dry-run, diagnostics marker-preset dry-run, app-args marker-preset dry-run, app-exit marker-preset dry-run, cctor-preflight marker-preset dry-run, fetch-http marker-preset dry-run, fetch-http-endpoint marker-preset dry-run, fetch-http-tcp-exchange marker-preset dry-run, http-static-response marker-preset dry-run, http-static-tcp-response marker-preset dry-run, virtio-rx-tcp-dispatch marker-preset dry-run, qcow2 smoke intent, and libvirt smoke intent. CI smoke tests also cover found and missing fake `qemu-img` discovery paths, including `CI_SMOKE_QEMU_IMG_DISCOVERY`, top-level `qemuImgDiscovery`/`qcow2ConversionIntent`, and matching qcow2-step fields without executing conversion. The executed CI failure-path test also verifies the diagnostics dry-run log contains `QEMU_MARKER_PRESETS diagnostics`, `MARKER_PRESETS diagnostics`, `DIAGNOSTICS_CHAIN_READY`, `DIAGNOSTIC_STUB_INVOCATION_READY`, and the ordered diagnostics marker chain; verifies the app-args dry-run log contains `QEMU_MARKER_PRESETS app-args`, `MARKER_PRESETS app-args`, and `APP_ARGS_VECTOR_READY`; verifies the app-exit dry-run log contains `QEMU_MARKER_PRESETS app-exit`, `MARKER_PRESETS app-exit`, and `APP_EXIT_OBSERVER_READY`; verifies the cctor-preflight dry-run log contains `QEMU_MARKER_PRESETS cctor-preflight`, `MARKER_PRESETS cctor-preflight`, and `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`; verifies the fetch-http dry-run log contains `QEMU_MARKER_PRESETS fetch-http`, `MARKER_PRESETS fetch-http`, and `FETCH_APP_HTTP_COMPLETION_READY`; verifies the fetch-http-endpoint dry-run log contains `QEMU_MARKER_PRESETS fetch-http-endpoint`, `MARKER_PRESETS fetch-http-endpoint`, and `FETCHAPP_HTTP_ENDPOINT_READY`; verifies the fetch-http-tcp-exchange dry-run log contains `QEMU_MARKER_PRESETS fetch-http-tcp-exchange`, `MARKER_PRESETS fetch-http-tcp-exchange`, and `FETCH_APP_HTTP_TCP_EXCHANGE_READY`; verifies the http-static-response dry-run log contains `QEMU_MARKER_PRESETS http-static-response`, `MARKER_PRESETS http-static-response`, `HTTP_STATIC_RESPONSE_READY`, and `HTTP_STATIC_RESPONSE`; verifies the http-static-tcp-response dry-run log contains `QEMU_MARKER_PRESETS http-static-tcp-response`, `MARKER_PRESETS http-static-tcp-response`, `HTTP_STATIC_TCP_RESPONSE_READY`, and `HTTP_STATIC_TCP_RESPONSE`; verifies the virtio-rx-tcp-dispatch dry-run log contains `QEMU_MARKER_PRESETS virtio-rx-tcp-dispatch`, `MARKER_PRESETS virtio-rx-tcp-dispatch`, `VIRTIO_NET_RX_TCP_DISPATCH_READY`, and `VIRTIO_NET_RX_TCP_DISPATCH`; and verifies the execute summary records a passing `ci.smoke.git.diffcheck` lane. Baseline evidence script host tests verify qcow2 and libvirt intent lanes keep workload-profile propagation wired, execute `run-baseline-evidence.ps1 -PlanOnly` to validate the artifact-summary shape for diagnostics, qcow2, and libvirt intent lanes, and cover found/missing fake `qemu-img` discovery paths without executing conversion.

Baseline evidence host tests also verify the explicit host build/no-build and
Git lanes, terminal-marker enforcement, overall success marker, and rejection
of a nonempty log root so stale output cannot be appended into a new record.
The script's `-SelfTest` mode dynamically rejects a missing host log, duplicate
pass terminals, a pass/fail conflict, a summary status mismatch, and malformed
summary JSON without launching builds or QEMU.

Evidence-manifest host tests emit `TEST_START evidence.manifest` and
`TEST_PASS evidence.manifest`. They cover deterministic byte-for-byte output,
UTF-8 without BOM, sorted fields, directly derived clean and dirty Git state,
verified nonempty artifact/config/serial hashes, failed- and timed-out-run
recording, CLI repository-state derivation, missing/malformed fields,
missing/unreadable/empty inputs, malformed/stale/mismatched hashes, stale commit
and dirty-state mismatches, forged observed-marker claims, required/forbidden
marker checks against hashed serial bytes, strict serial UTF-8, conflicting
terminal markers, inconsistent pass records, invalid timestamps, and output
overlap with an input.

Host tool project-boundary tests enumerate the current five expected projects
(`OS.ImageBuilder`, `OS.LibvirtRunner`, `OS.ManifestTool`, `OS.QemuRunner`, and
the standalone M01-033 `OS.X64TextSerializerGenerator`) and emit:

```text
TEST_PASS tools.project boundary excludes artifact sources
TEST_PASS tools.project boundary keeps expected host tools
```

M01-022 adds fake-QEMU coverage for the exact stage-one chain and the new
script/runner controls. The focused group proves dry-run forwarding,
fresh-build enforcement, complete ordered/unique success, irreversible
reordering, duplicate start/pass/checkpoint markers, legacy presence-only
duplicates, every required stage checkpoint missing in turn, partial EOF,
arbitrary chunking, allocation-cleanup failure, unexpected stack-switch
return, timeout, clean reboot/shutdown, conflicting terminals, and the
existing nonzero-exit path. The integrated run from the source-bound commit
emitted:

```text
TEST_PASS qemu smoke script forwards M01 stage-one evidence controls
TEST_PASS qemu smoke script rejects stale M01 probe artifact selection
TEST_PASS qemu M01 ordered and unique dry-run evidence
TEST_PASS qemu M01 ordered markers reject reordering
TEST_PASS qemu M01 unique markers reject duplicate stage start
TEST_PASS qemu M01 unique markers reject duplicate stage pass
TEST_PASS qemu M01 unique markers reject duplicate semantic checkpoint
TEST_PASS qemu legacy required markers remain presence-only
TEST_PASS qemu M01 missing checkpoint markers are rejected
TEST_PASS qemu M01 partial marker EOF is rejected
TEST_PASS qemu M01 chunked marker stream succeeds
TEST_PASS qemu M01 allocation cleanup failure is rejected
TEST_PASS qemu M01 unexpected switch return is rejected
TEST_PASS qemu M01 timeout is deterministic
TEST_PASS qemu M01 reboot signature is rejected
TEST_PASS qemu M01 conflicting terminals are rejected
TEST_PASS host.tests
```

M01-024 adds a second focused fake-QEMU group for the cumulative stage-one and
stage-two transcript. It covers both-switch forwarding and invalid selector
combinations, the complete ordered/unique chain, cross-stage and in-stage
reordering, duplicate start/pass/operation markers, each missing stage-two
checkpoint, structured EFI/map/reservation/coordinator failures, every
forbidden boundary marker, timeout, reset, and conflicting terminals. The
arbitrary read-chunk boundary coverage remains supplied by the shared
streaming-parser suite. The accepted host run emitted:

```text
TEST_PASS qemu smoke script forwards M01 stage-two evidence controls
TEST_PASS qemu smoke script rejects invalid M01 stage-two probe selection
TEST_PASS qemu M01 stage-two ordered and unique evidence succeeds
TEST_PASS qemu M01 stage-two rejects cross-stage and operation reordering
TEST_PASS qemu M01 stage-two rejects duplicate start pass and operation markers
TEST_PASS qemu M01 stage-two rejects every missing stage-two marker
TEST_PASS qemu M01 stage-two rejects adapter failure classifications
TEST_PASS qemu M01 stage-two rejects every forbidden boundary marker
TEST_PASS qemu M01 stage-two timeout is deterministic
TEST_PASS qemu M01 stage-two reset is rejected
TEST_PASS qemu M01 stage-two conflicting terminal is rejected
TEST_PASS host.tests
```

The adapter-failure corpus distinguishes EFI arena-allocation failure, arena
geometry rejection, memory-map recapture growth/overflow, unchanged map key,
arena descriptor mismatch, reservation and map/ledger evidence failures, page
allocation or confinement failure, inverse-free failure, and absent
double-free rejection. These are evidence-runner classification tests; the
M01-023 source/mutation suite remains the contract proof that the guest
adapter maps those conditions to fail-closed diagnostics.

The fake-QEMU HTTP probe fixture writes `TEST_START fake.qemu` and `OS_SMOKE_PASS` through one `FileStream` opened with `FileShare.ReadWrite | FileShare.Delete`. This mirrors the runner's permissive serial-log reader and avoids Windows-only file sharing races while the runner polls the serial log and the fake guest keeps serving the loopback HTTP endpoint.

QEMU marker-preset host tests emit:

```text
TEST_PASS qemu.smoke.preset default preserves current markers
TEST_PASS qemu.smoke.preset acpi adds validation and MCFG markers
TEST_PASS qemu.smoke.preset app args adds argument-vector marker
TEST_PASS qemu.smoke.preset app args aliases normalize
TEST_PASS qemu.smoke.preset app exit adds observer readiness marker
TEST_PASS qemu.smoke.preset app lifecycle adds lifecycle marker
TEST_PASS qemu.smoke.preset app lifecycle aliases normalize
TEST_PASS qemu.smoke.preset cctor preflight adds runtime readiness marker
TEST_PASS qemu.smoke.preset cctor work items adds runtime readiness marker
TEST_PASS qemu.smoke.preset cctor work items aliases normalize
TEST_PASS qemu.smoke.preset fetch http adds FetchApp completion marker
TEST_PASS qemu.smoke.preset fetch http endpoint adds endpoint marker
TEST_PASS qemu.smoke.preset fetch http endpoint aliases normalize
TEST_PASS qemu.smoke.preset fetch http tcp exchange adds exchange marker
TEST_PASS qemu.smoke.preset fetch http tcp exchange aliases normalize
TEST_PASS qemu.smoke.preset http static response adds marker requirements
TEST_PASS qemu.smoke.preset http static response aliases normalize
TEST_PASS qemu.smoke.preset http static tcp response includes marker requirements
TEST_PASS qemu.smoke.preset http static tcp response accepts aliases
TEST_PASS qemu.smoke.preset manifest env adds native manifest marker
TEST_PASS qemu.smoke.preset manifest env aliases normalize
TEST_PASS qemu.smoke.preset uefi gone adds denial and owned serial markers
TEST_PASS qemu.smoke.preset rejects unknown preset
TEST_PASS qemu.smoke.preset script dry-run includes selected preset
TEST_PASS qemu.smoke.preset script dry-run includes app args preset
TEST_PASS qemu.smoke.preset script dry-run includes app exit preset
TEST_PASS qemu.smoke.preset script dry-run includes app lifecycle preset
TEST_PASS qemu.smoke.preset script dry-run includes cctor preflight preset
TEST_PASS qemu.smoke.preset script dry-run includes cctor work items preset
TEST_PASS qemu.smoke.preset script dry-run includes fetch http preset
TEST_PASS qemu.smoke.preset script dry-run includes fetch http endpoint preset
TEST_PASS qemu.smoke.preset script dry-run includes fetch http tcp exchange preset
TEST_PASS qemu.smoke.preset script dry-run includes http static response preset
TEST_PASS qemu.smoke.preset script dry-run includes http static tcp response preset
TEST_PASS qemu.smoke.preset script dry-run includes manifest env preset
TEST_PASS qemu.smoke.preset real mode passes selected markers
TEST_PASS qemu.smoke.preset acpi fails when MCFG marker is missing
TEST_PASS qemu.smoke.preset acpi fails when markers are out of order
```

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

QEMU evidence from 2026-05-21:

- QEMU was installed with `winget install --id SoftwareFreedomConservancy.QEMU --silent --accept-package-agreements --accept-source-agreements`.
- QEMU path: `C:\Program Files\qemu\qemu-system-x86_64.exe`.
- OVMF path: `C:\Program Files\qemu\share\edk2-x86_64-code.fd`.
- Real smoke emitted `TEST_PASS qemu.smoke`.
- Simulation smoke emitted `TEST_PASS qemu.smoke` with the `uefi-gone` marker preset.
- This 2026-05-21 record predates the summary-log field. The later dated
  entries below preserve summary artifacts; current evidence is recorded in
  `docs/completion-gates.md` and the latest `done/stepNN.md`, not inferred from
  this historical bullet.
- `artifacts/logs/qemu-smoke.log` contains:
  - `TEST_START efi.main`
  - `EFI_CONSOLE_READY`
  - `UEFI_MEMORY_MAP_READY`
  - `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`
  - `UEFI_MEMORY_MAP_OWNED`
  - `BOOT_HANDOFF_READY`
  - `UEFI_MEMORY_MAP_NORMALIZED`
  - `BOOT_MEMORY_MAP_ACCEPTED`
  - `BOOT_MEMORY_REGIONS_READY`
  - `BOOT_MEMORY_REGION_COUNT <count>`
  - `BOOT_ACPI_RSDP_HANDOFF_READY`
  - `UEFI_ACPI_RSDP_READY`
  - `TEST_START boot.sequence`
  - ordered `BOOT_PHASE` markers
  - `OS_BOOT_OK`
  - `TEST_PASS boot.sequence`
  - `OS_SMOKE_PASS`
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- Real smoke on 2026-05-22 requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED` emitted `TEST_PASS qemu.smoke`; the summary artifact reported `MISSING_MARKERS none`.
- Real smoke on 2026-05-22 using the default script markers emitted `TEST_PASS qemu.smoke`; the summary artifact reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`.
- Real smoke on 2026-05-22 requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED` emitted `TEST_PASS qemu.smoke`; the summary artifact reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED`.
- Real smoke on 2026-05-22 using the previous default script markers emitted `TEST_PASS qemu.smoke`; the summary artifact reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- Real smoke on 2026-05-22 using the updated current default script markers emitted `TEST_PASS qemu.smoke`; the summary artifact reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`, and the serial log reported `BOOT_MEMORY_REGION_COUNT 102`.
- Fresh-map probe smoke on 2026-05-22 requiring `OS_SMOKE_PASS,UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_FRESH_MAP_READY,UEFI_EBS_CALL_SKIPPED` emitted `TEST_PASS qemu.smoke`; the summary artifact reported `MISSING_MARKERS none`.
- Real smoke on 2026-05-22 with forbidden markers `UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_CALL_SKIPPED` emitted `TEST_PASS qemu.smoke`; `artifacts/logs/qemu-smoke-forbidden.summary.log` reported `MISSING_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and `FAIL_REASON none`.
- Native app marker preset dry-run on 2026-05-22 emitted `TEST_PASS qemu.runner.dryrun` and `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;APP_PACKAGE_READY;APP_LAUNCH_READY;APP_PAYLOAD_READY;APP_EXEC_MAPPING_READY;APP_EXEC_MEMORY_READY;APP_INVOCATION_READY;APP_ENTRY_READY`.
- ACPI physical discovery probe smoke on 2026-05-22 emitted `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, `TEST_PASS efi.shell.publish`, and `TEST_PASS qemu.smoke` while requiring `-MarkerPreset acpi`.
- `artifacts/logs/qemu-smoke-acpi.log` contains `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- `artifacts/logs/qemu-smoke-acpi.summary.log` reports `MISSING_MARKERS none` for the default boot markers plus `ACPI_RSDP_SNAPSHOT_READY;ACPI_RSDP_VALIDATED;ACPI_ROOT_SDT_SNAPSHOT_READY;ACPI_ROOT_SDT_VALIDATED;ACPI_MCFG_DISCOVERY_READY`.
- qcow2 smoke dry-run on 2026-05-22 emitted `TEST_PASS imagebuilder.qcow2`, `if=none,id=esp,format=qcow2,file=artifacts/images/os.qcow2`, `TEST_PASS qemu.runner.dryrun`, and `TEST_PASS qemu.qcow2.smoke`.
- Real qcow2 smoke on 2026-05-22 emitted `TEST_PASS imagebuilder.raw`, `TEST_PASS imagebuilder.qcow2`, `TEST_PASS qemu.smoke`, and `TEST_PASS qemu.qcow2.smoke` using `C:\Program Files\qemu\qemu-img.exe`, `C:\Program Files\qemu\qemu-system-x86_64.exe`, and `C:\Program Files\qemu\share\edk2-x86_64-code.fd`.
- `artifacts/logs/qemu-smoke-qcow2.log` contains `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT 102`, `OS_BOOT_OK`, and `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-qcow2.summary.log` reports `QEMU_SMOKE_STATUS pass`, `IMAGE_ARTIFACT artifacts/images/os.qcow2`, `MISSING_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and `FAIL_REASON none`.
- CI dry-run on 2026-05-22 emitted `TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent` and planned `ci.smoke.qemu.qcow2.dryrun` with `run-qemu-qcow2-smoke.ps1 -DryRun`, `qemu-qcow2-serial.log`, `qemu-qcow2.summary.log`, and a qcow2 image path under the CI evidence directory.
- Serial summary parser host tests on 2026-05-22 emitted `TEST_PASS qemu.serial summary passes required markers`, `TEST_PASS qemu.serial summary reports missing markers`, `TEST_PASS qemu.serial summary reports forbidden markers`, and `TEST_PASS qemu.serial summary preserves marker order`.
- App-exit summary parser host tests on 2026-05-22 emitted `TEST_PASS qemu.summary app exit marker parsed`, `TEST_PASS qemu.summary app exit absent reported`, `TEST_PASS qemu.summary app exit malformed reported`, and `TEST_PASS qemu.serial summary keeps required marker behavior unchanged`.
- Fake-QEMU runner host tests on 2026-05-22 emitted `TEST_PASS qemu.runner app exit summary emitted`.
- Host-tool execution cleanup on 2026-05-22 kept the QEMU runner tests on the built-DLL process path; integrated host tests emitted `TEST_PASS qemu dry-run preserves command construction`, `TEST_PASS qemu dry-run emits marker requirements`, `TEST_PASS qemu real mode writes passing summary artifact`, and `TEST_PASS qemu real mode rejects missing image`.
- QEMU HTTP probe sharing fix on 2026-05-22 emitted `TEST_PASS qemu.runner.http_probe accepts expected response`, `TEST_PASS qemu.runner.http_probe rejects status mismatch`, `TEST_PASS qemu.runner.http_probe rejects body mismatch`, `TEST_PASS qemu.runner.http_probe times out deterministically`, and `TEST_PASS host.tests`.

## Known Limitations

- QEMU launch uses `-display none`; serial output is the evidence channel.
- The default EFI smoke still returns to firmware UI after `EfiMain`. The
  M01-022 stage-one and M01-024 dual-stage continuations intentionally do not
  return after the owned-stack transfer; after each validated transcript
  becomes quiet, the runner terminates QEMU. None of these behaviors is
  `ExitBootServices` evidence.
- OVMF discovery is best-effort. Pass `-OvmfCode <path>` for deterministic runs.

### Streaming serial-marker state machine

`tools/OS.QemuRunner/SerialMarkerStateMachine.cs` is the bounded streaming
parser contract for M00-019. It accepts arbitrary `ReadOnlySpan<byte>` chunks,
retains an incomplete line across calls, recognizes marker lines only at line
boundaries, and supports LF, CRLF, and a final unterminated complete line.
Required and forbidden markers use the same exact-line or
`marker + " " + metadata` shape as the snapshot summary parser.

The parser has explicit line and total-byte capacities. Overflow, strict
invalid-byte rejection, timeout, partial marker EOF, missing required markers,
missing success terminal, failure terminals, duplicate terminals, conflicting
success/failure terminals, forbidden markers, out-of-order markers, and
duplicate unique markers are distinct append-only completion statuses. A
successful result always requires exactly one `OS_SMOKE_PASS`, even if callers
append custom required markers. Ordered and unique markers are merged into the
required set without changing legacy presence-only behavior. Forbidden markers
take precedence over terminal conflicts, timeout, partial EOF, and
missing-marker classification after structural parser failures have been
ruled out. Default result structs never report success.

Marker text is printable ASCII. The default byte-noise policy treats a line
containing control or non-ASCII bytes as opaque noise, counts the invalid bytes
and line, and never recognizes a marker embedded in that line. This permits
firmware escape/binary noise without accepting a corrupted marker. If such a
noise line exceeds the ordinary line buffer, the parser stays in a bounded
discard-until-newline state; it does not reset mid-line and therefore cannot
accept a marker hidden after the invalid prefix. The independent total-byte
capacity still terminates an unbounded stream. The strict policy rejects the
first invalid byte and finalizes as `InvalidByteSequence`.

`tests/OS.HostTests/SerialMarkerStateMachineTests.cs` feeds required and
forbidden markers across every possible byte split and also covers one-byte and
irregular chunks, surrounding noise, CRLF/LF, substring lookalikes, partial
EOF, final unterminated markers, duplicate/conflicting terminals, forbidden
precedence, timeout, invalid-byte policies, line/total capacity exhaustion,
configuration bounds, default results, and repeated finalization. The group
emits:

```text
TEST_START serial.marker.state_machine
TEST_PASS serial.marker.state_machine
TEST_FAIL serial.marker.state_machine <reason>
```

`OS.QemuRunner/Program.cs` feeds only newly appended serial-log bytes into this
state machine while QEMU runs. The streaming result now controls early required
and forbidden marker detection and rejects capacity exhaustion, invalid bytes
under the strict policy, duplicate or conflicting terminals, a failure
terminal, serial-log truncation/read failure, a partial marker at EOF,
irreversible later-before-earlier order, and repeated unique lines before the
existing snapshot summary can report success. Order evidence remains latched
while the bounded stream continues so missing and reordered transcripts retain
different primary classifications; a duplicate unique line stops collection
immediately. The runner emits
`SERIAL_MARKER_STREAM status=<status> terminal=<state> ...` for the completed
stream. After the required markers first appear, a non-HTTP smoke keeps reading
until the serial stream is quiet for 750 milliseconds (or QEMU exits) before it
terminates QEMU. That bounded drain catches a delayed duplicate or failure
terminal; the smoke can pass only when both the byte-stream result and final
text summary agree. The final serial read is capped at the same 16 MiB total
capacity as the streaming parser before allocation. `SerialMarkerSummary`
remains the bounded final-reporting layer for observed lines, marker ordering,
application-exit records, and the serial tail.

The live integration is host-tested with fake QEMU processes for passing,
custom-marker-without-terminal rejection, delayed duplicate terminals,
generic `TEST_FAIL` rejection, spontaneous nonzero exit both immediately and
inside the drain window, hard-timeout precedence over late markers, pre-launch
invalid-marker rejection, explicit QEMU/output alias rejection, and
auto-discovered OVMF/output alias rejection with unchanged input bytes. The
state-machine group supplies the exhaustive chunk-boundary and malformed-input
cases. These tests prove runner/parser behavior only. A real QEMU run is still
required to prove that a guest emitted the required markers, and the markers
prove only the completion boundary defined by the corresponding gate.

## Next Steps

1. Add libvirt real-boot smoke on hosts with libvirt.
2. Point the real HTTP probe at the guest Kestrel endpoint only after guest networking and hosted Kestrel markers exist.
3. Add ISO launch support only if it remains useful for a concrete deployment path.
