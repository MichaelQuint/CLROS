# Manifest

M16-002 implements the [NativeAOT-first roadmap](workload-profiles.md) package
boundary: an explicit versioned Linux ELF profile, executable digest,
arguments/environment, endpoint policy and resource limits. The format is
qualified in [step1199](../done/step1199.md) for immutable data staging only.
Existing OSAPPv0/native and hosted package formats below
must not be silently reinterpreted as stock Linux NativeAOT executables.

## Linux ELF package version 1

`NativeManifestParser.ParseLinuxElf` is an explicit entry point; `Parse` remains
v0-only. The no-stdlib consumer uses the same `LinuxElfPackage` pointer parser.
Metadata borrows stable caller bytes; default results fail. Printable ASCII,
LF/CRLF, optional final newline and first-`=` value splitting are supported.
Unknown/missing/duplicate fields, unsafe paths and malformed inputs reject.

Required fields are `format=clros-linux-elf`, `version=1`,
`profile=linux-musl-x64-static-v1`, `app.path`, `args`, `digest`, `memory`,
`stack`, `threads`, `descriptors` and `listen`. Paths are relative ASCII
segments without empty/dot/dot-dot segments, drives or backslashes (128 bytes).
Args are at most 256 bytes, quote-balanced with no backslash escaping; they are
retained raw, not materialized into argv. Up to 16 `env.KEY=value` fields retain
offsets: uppercase identifier keys at most 64 bytes, values at most 256 bytes,
duplicate keys rejected. `listen` is canonical numeric IPv4:port; hostnames,
IPv6, leading-zero numbers and port 0 reject. Endpoint admission is not networking.

`digest` is 64 lowercase SHA-256 hex digits over the exact ELF bytes.
`memory` is page-aligned 4096..1 GiB, `stack` page-aligned 4096..8 MiB and no larger
than memory, `threads` 1..64 and `descriptors` 1..1024. These are declared process
limits; runtime enforcement belongs to the later loader/process/syscall slices.
Static ELF identity/program-table bounds, no interpreter/dynamic segment, no
executable stack, W^X load flags, bounded memory and file-backed entry are checked
before staging. Full load geometry, TLS materialization and entry remain M16-004.

The envelope is at most 16 MiB: a 64-byte little-endian header, manifest at most 4096
bytes, then one ELF. Header offsets: magic `CLRELF01` at 0; uint32 version 1 at 8,
profile 1 at 12, header length 64 at 16, manifest length at 20; uint64 ELF length
at 24; bytes 32..63 must be zero. Exact total length excludes gaps/trailers.
Neither legacy OSAPPv0 nor ordinary package launch automatically consumes it.
`ReadFile` exposes only the manifest's exact executable path, copies bounded
bytes to exclusive caller storage, returns EOF honestly and rejects aliases.
It revalidates the digest; there is no writable namespace or app execution.

The disk-manifest batch has a separate bounded pointer validator in
`OS.Storage.NativeManifestDisk`, host-tested in [step1153](../done/step1153.md).
It accepts at most 4,096 bytes of ASCII native v0 required fields and optional
package/native-aot kind fields, with LF/CRLF and source-backed offsets.
Environment/listen keys reject as unsupported; this does not reduce the
existing `NativeManifestParser` host contract. Strict relative app paths reject
absolute/drive/backslash, empty and dot/dot-dot segments.
The default-off package profile in [step1154](../done/step1154.md) now connects
this validator to bounded one-cluster `OS/NATIVE.MAN` reads and source-backed
package path selection. Its active no-stdlib compile and focused host controls
pass. [Step1155](../done/step1155.md) records strict native review and six package
guest variants; [step1156](../done/step1156.md) accepts the bounded batch after
4,393 unfiltered full-host checks on unchanged corrected inputs.
Process environment and app activation remain incomplete.

## Current Status

A host-side JSON manifest v0 validator exists, and a kernel-facing native manifest parser plus host-tested storage loaders now exist for the early app-launch path. The manifest loader now has an additive caller-buffer GPT readiness overload that uses CRC-validating GPT parsing with backup-GPT fallback before FAT32 reads, preserves GPT primary/backup table-source metadata plus GPT scratch and manifest required/available byte counts on caller-buffer failures, and `NativeAppSelectionLoader` composes manifest/package reads into selected-app metadata in host tests. The app-model path also has a host-tested bind-policy helper that validates a future PAL IPv4 socket-bind endpoint against the launch plan's manifest listen endpoint, a mock-only bridge that calls `OSHostSocketTable.Bind` only after that policy succeeds, a mock-only listener bridge that creates/binds/listens through `OSHostSocketTable` after policy success, and host-only PAL config-table projection for listen metadata. Full kernel app launch is not implemented. The host manifest reserves the runtime shape for native-tier apps and hosted .NET 10 workloads, including ASP.NET Minimal API/Kestrel app packages; the broad hosted-workload claim boundary remains `docs/completion-gates.md`.

`tools/OS.ImageBuilder` now places `\OS\MANIFEST.JSON` into every generated raw FAT32 EFI System Partition. With `--manifest <path>`, it copies the caller-provided manifest byte-for-byte. Without `--manifest`, it writes a deterministic native manifest v0 default. A host test now feeds the generated raw image back through `NativeManifestLoader`.

`docs/workload-profiles.md` defines `SingleMinimalApi` and `MultiMinimalApi` as planning profiles. Current manifest validators do not enforce a workload profile field, multi-app lists, per-app restart policy, per-app address spaces, or process-table semantics. Those fields remain future schema work.

`OSWorkloadProfile` is a build selector today, and `OS.ManifestTool` now accepts
`--profile <SingleMinimalApi|MultiMinimalApi>` for JSON validation. When the
argument is omitted, the tool uses the build-selected profile. JSON manifests
may declare `workloadProfile`, but it must match the selected profile. The
kernel-facing line manifest parser does not consume workload profiles yet.

## Implemented Behavior

- `tools/OS.ManifestTool validate --manifest <path>` validates a deterministic JSON manifest v0.
- The validator emits `TEST_START manifest.validate`, `TEST_PASS manifest.validate`, and `TEST_FAIL manifest.validate <code> <message>`.
- The validator enforces a 64 KiB max input size, `schemaVersion: 0`, required `app.id`, required safe relative `app.entry`, string-array `args`, string-map `env`, duplicate env-key rejection, malformed JSON rejection, and unknown schema-version rejection.
- `runtime` is optional. When omitted, the manifest remains compatible with the v0 native-app default.
- `runtime.kind: "native-aot"` is accepted for the native-tier runtime contract.
- `runtime.kind: "dotnet10"` is accepted as a reserved host-side contract only when it includes a safe relative `runtime.assembly` path and `runtime.kestrel.listen` entries.
- Reserved Kestrel listen entries require `scheme: "http"`, a stable ASCII `host`, and a `port` from 1 through 65535. Duplicate listen endpoints are rejected. `https` is rejected because TLS is not implemented or claimed.
- `--profile SingleMinimalApi` rejects the future multi-app `apps` field with
  `single_profile_multiple_apps`, and rejects unsupported `restartPolicy`,
  `resources`, `isolation`, and `supervisor` fields with profile-specific error
  codes that name `SingleMinimalApi` and the exact field/count.
- `--profile MultiMinimalApi` currently rejects manifests with
  `multi_minimal_api_unsupported` until process, address-space isolation, and
  supervisor behavior exists.
- A JSON `workloadProfile` property is accepted only when it matches the
  selected validation profile; mismatches return `workload_profile_mismatch`.
- `OS.Manifest.NativeManifestParser` parses a deterministic kernel-facing v0 line format from `ReadOnlySpan<byte>` without JSON reflection, exceptions for expected malformed input, or managed object graphs. It uses the shared `OS.Std.Text.AsciiKeyValueSplitter` for first-`=` line splitting so later delimiter bytes stay inside source-backed values.
- The kernel-facing parser accepts required ASCII keys: `format=os-native-manifest`, `version=0`, `app.name`, `app.path`, `args`, `network`, and `log`.
- The kernel-facing parser also accepts package-selection keys `app.kind=package` and `runtime.kind=native-aot`. Older v0 native manifests that omit those keys still default to packaged native AOT selection for compatibility.
- The kernel-facing parser now has host-tested maximum-boundary coverage for `app.name` at 64 bytes, `app.path` at 128 bytes, `args` at 256 bytes, environment keys at 64 bytes, environment values at 256 bytes, and listen hosts at 64 bytes. One byte over each limit produces a deterministic status-coded parse failure instead of truncation or hidden success.
- The kernel-facing parser accepts bounded environment fields in the form `env.KEY=value`. Environment keys must be uppercase ASCII identifiers, are capped at 64 bytes, values are capped at 256 bytes, and the manifest can carry at most 16 environment entries.
- Kernel-facing environment entries preserve key/value offsets into the caller-provided manifest bytes and reject duplicate keys with a dedicated status.
- `NativeManifestEnvironmentDiagnosticFormatter` formats already-parsed native
  manifest environment metadata into a caller-owned `NATIVE_MANIFEST_ENV
  status=` marker. The marker includes initialized state, environment count,
  maximum environment count, first key/value offset and length metadata, and
  explicit `processEnvironmentMaterialized=false`,
  `hostedDotNetStarted=false`, and `kestrelStarted=false` flags. It rejects
  default parse results, invalid source-backed ranges, and undersized
  destinations without creating process environment variables. Its decimal
  byte-count and marker write paths now use the shared
  `OS.Std.Text.AsciiDecimalFormatter` and `AsciiTextBuilder` helpers instead
  of manifest-local integer formatting and byte-copy loops.
- `BootSequence.RunNativeManifestEnvironmentReadiness` exposes that formatter
  through host-tested `NATIVE_MANIFEST_ENV_READY` and
  `NATIVE_MANIFEST_ENV_NOT_READY` markers over an already-produced
  `NativeManifestParseResult` plus original manifest bytes. The not-ready path
  distinguishes not-initialized parse results, parse failures, invalid
  source-backed ranges, and diagnostic formatting failures while preserving
  parse status, format status, initialized state, byte-count metadata, and the
  explicit no-process-environment/no-hosted-.NET/no-Kestrel boundary flags. It
  is not wired into the default boot path and performs no storage reads.
- FetchApp HTTP request endpoint planning can consume parsed environment
  entries `FETCH_HTTP_URL`, or explicit `FETCH_HTTP_HOST`, `FETCH_HTTP_PORT`,
  and `FETCH_HTTP_PATH`. The networking parser defaults to `example.com:80/`
  when absent, copies host/path bytes into caller-owned buffers, rejects
  `https` and other non-HTTP schemes, missing hosts, invalid ports, empty paths,
  oversized fields, short destinations, and failed manifest parses, and remains
  metadata-only: it does not resolve DNS, open sockets, send HTTP requests, or
  claim TLS/Kestrel behavior. Its diagnostic formatter emits only endpoint
  metadata in a caller-owned `FETCH_HTTP_ENDPOINT` buffer: status, port,
  host/path byte counts, required byte counts, and config-source flags. It does
  not dump configured host/path values. `BootSequence` can aggregate the
  already-produced endpoint result into `FETCHAPP_HTTP_ENDPOINT_READY` or
  `FETCHAPP_HTTP_ENDPOINT_NOT_READY` markers with the same compact metadata,
  without changing the default boot path or reading manifests from live
  storage.
- FetchApp HTTP completion expectation planning can consume parsed environment
  entries `FETCH_HTTP_EXPECTED_STATUS`, `FETCH_HTTP_EXPECTED_MARKER`,
  `FETCH_HTTP_EXPECTED_BODY`, and `OS_SMOKE_EXPECTED_BODY`. The expectation
  planner is a networking contract over already-parsed native manifests: it
  defaults to status `200` and marker `FETCH_OK`, rejects malformed status
  codes, empty markers, markers larger than the fixed evidence buffer, and
  missing required marker config when requested. Its diagnostic formatter emits
  only marker metadata in a caller-owned `FETCH_HTTP_EXPECTATION` buffer:
  status, initialization, expected HTTP status, marker length, and config-source
  flags. It does not dump configured marker or body bytes. This is
  configuration metadata only; it does not load manifests from storage, execute
  FetchApp, perform HTTP I/O, or claim TLS/Kestrel behavior.
- `OS.Manifest.NativeManifestFetchAppConfigPreflight` validates that an
  already-produced `NativeManifestLoadResult` is ready for FetchApp HTTP
  completion checks. It delegates status/body-marker interpretation to
  `FetchAppHttpCompletionExpectation`, copies the selected marker into
  caller-owned scratch storage, preserves nested manifest/expectation status
  metadata, and rejects default/uninitialized manifest loads, malformed status
  config, empty or oversized markers, missing required marker config, and short
  scratch buffers without reading files, parsing JSON, or performing HTTP I/O.
- `BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness` can turn the
  same preflight result into `FETCHAPP_CONFIG_PREFLIGHT_READY` or
  `FETCHAPP_CONFIG_PREFLIGHT_NOT_READY` markers with compact readiness,
  manifest, expectation, status-code, marker-length, config-source, and
  manifest byte-count metadata. This is host-tested marker aggregation only.
- The kernel-facing parser accepts one optional bounded listen endpoint in the form `listen=host:port`. The host slice is source-backed, capped at 64 ASCII bytes, and the port is parsed as `1..65535`.
- Listen endpoint validation rejects malformed host/port text, port `0`, ports above `65535`, and `network=none` because listen metadata requires networking to be enabled. This records intent only; it does not bind sockets.
- Reserved hosted runtime values such as `runtime.kind=dotnet10` are recognized only as unsupported kernel-facing runtime selections. They do not launch hosted .NET or Kestrel.
- Kernel-facing network modes are `none`, `dhcp`, and `static`; log modes are `serial` and `none`.
- Kernel-facing values are represented by offsets and lengths into the caller-provided manifest bytes, so later boot/app-launch code can avoid copying strings unless it explicitly needs to.
- `NativeAppArgumentVectorPlanner` consumes a successful launch plan and turns the source-backed `args=` field into up to 16 source-backed argument spans. It accepts unquoted space-delimited arguments and quoted spans for arguments containing spaces, reports malformed quotes and count/length limits with status codes, and does not execute app code or allocate app process state. `NativeAppArgumentVectorDiagnosticFormatter` writes a caller-owned `APP_ARGS_VECTOR status=` marker for the plan result, including planner status, initialized state, argument count, maximum argument count, maximum argument length, first-argument offset/length when present, and no-execution/no-Kestrel flags. It reports required bytes, rejects default results, and preserves caller buffers when the destination is too small.
- `OS.Manifest.NativeManifestLoader.LoadFromPrimaryGptEsp` combines GPT ESP location, a partition-relative block-device view, FAT32 `OS/MANIFEST.JSON` content read, and `NativeManifestParser` into a status-coded host-testable loader.
- `OS.Manifest.NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers` uses caller-provided GPT header, partition-entry, and descriptor buffers to locate the ESP through `GptPartitionTableParser`. It preserves primary-success behavior, falls back to the backup GPT header and backup entry sectors when primary GPT location fails, preserves the original primary failure if backup fallback also fails, preserves GPT table, CRC, and source diagnostics in `NativeManifestLoadResult` before continuing through the existing FAT32 and native manifest parser pipeline, reports GPT header/partition-entry scratch required and available bytes, and reports the manifest file bytes required plus the caller-provided manifest buffer bytes available when the FAT32 read cannot fit `OS/MANIFEST.JSON`.
- `OS.Manifest.NativeAppLaunchPlan` converts a parsed native manifest plus boot/runtime capability flags into a status-coded launch readiness plan. It records app fields, network/log requirements, package-storage requirement, and missing boot/runtime capabilities without executing app code.
- `OS.Manifest.NativeAppLaunchPlanDiagnosticFormatter` formats an already-produced launch plan into caller-owned `APP_LAUNCH_PLAN status=` diagnostics with app field byte counts, environment/listen metadata, boot/runtime capability masks, and explicit no-load/no-execute/no-hosted-.NET/no-Kestrel boundary flags. It rejects default results and short destinations without treating the marker as app-launch evidence.
- `OS.Manifest.NativeAppSocketBindPolicy` validates a future PAL IPv4 bind endpoint against the manifest listen endpoint carried by `NativeAppLaunchPlan`. It accepts numeric IPv4 listen hosts, including `0.0.0.0`, and rejects failed launch plans, missing listen endpoints, unsupported host text, invalid PAL endpoints, port mismatches, and address mismatches without calling PAL socket bind/listen.
- `OS.Manifest.NativeAppSocketBindBridge` composes that policy with the host-only `OSHostSocketTable.Bind` mock. It preserves nested policy and socket-table statuses, rejects policy failures before mutating socket state, reports mock bind conflicts, and does not export PAL functions or open live sockets.
- `OS.Manifest.NativeAppSocketListenBridge` composes the same policy with host-only `OSHostSocketTable` create/bind/listen calls. It preserves create/bind/listen stage status, keeps policy failures from mutating socket-table state, and does not export PAL functions, open live sockets, wait on a scheduler, or start Kestrel.
- `OS.Pal.OSHostConfigEnvironmentTable.FromNativeLaunchPlan` now projects a present listen endpoint into host-only config keys `kestrel.listen.0`, `kestrel.listen.0.host`, and `kestrel.listen.0.port`. Plans without listen metadata omit those keys.
- `OS.Manifest.NativeAppPayloadHeader` validates the selected packaged native payload's first 32 bytes as a v0 header before the launch descriptor reports success.
- `OS.Manifest.NativeAppSelectionLoader.LoadFromPrimaryGptEspWithCallerBuffers` composes caller-buffer native manifest loading, caller-buffer package loading, package parsing, launch-plan creation, and launch-descriptor creation over the same injected block device while preserving the nested failure status, primary/backup GPT table source, and scratch-buffer required/available byte metadata for each loader layer. Because it uses the caller-buffer manifest/package loaders, host tests now prove the selection path inherits backup-GPT fallback when primary GPT location fails. Its result shape requires an explicitly initialized success plus successful nested layer results; default values do not satisfy readiness.
- `OS.Manifest.NativeAppSelectionBootScratchPlan` validates caller-owned GPT header, GPT partition-entry-sector, partition-descriptor, manifest, and package buffers before any storage read or parse call. Its default plan matches the current image-builder raw image contract: 512-byte sectors, 128 GPT entries at 128 bytes each, 32 partition-entry sectors, one ESP descriptor slot, a 64 KiB manifest buffer, and a 16 MiB package buffer.
- `OS.Manifest.NativeAppEntryStagingPreflight` converts the existing package-staging readiness value into the final app-entry preflight. It requires initialized manifest/package staging evidence, caller-owned package-file staging bytes, and caller-owned payload staging bytes before `BootSequence.RunAppEntryReadiness` can emit `APP_ENTRY_READY`. It performs no storage reads, allocation, page mapping, permission changes, relocation, or app execution.
- `tools/OS.ImageBuilder build-native-payload` creates that minimal v0 native payload shape from caller-supplied raw app bytes by writing the `OSAPPv0\0` header and appending the raw input unchanged.
- `tools/OS.ImageBuilder validate-package --manifest <native-manifest> --package <pkg>` parses the kernel-facing native manifest, builds the launch plan, opens the manifest-selected package file, and validates that selected file's `OSAPPv0` payload header without executing app code.
- `tools/OS.ImageBuilder build-native-app-package --manifest <native-manifest> --input <raw-app-bytes> --output <pkg>` parses the same kernel-facing native manifest, places an `OSAPPv0`-wrapped payload at the manifest-selected `app.path` inside a deterministic single-file `OSPKGv0` package, and runs the same manifest/package/payload validation before reporting success.

## Kernel Native V0 Format

The early kernel parser intentionally uses a compact line-oriented format instead of JSON:

```text
format=os-native-manifest
version=0
app.name=hello.app
app.path=apps/hello.bin
args=
app.kind=package
runtime.kind=native-aot
network=dhcp
listen=0.0.0.0:8080
log=serial
env.PORT=8080
env.ASPNETCORE_ENVIRONMENT=Production
env.FETCH_HTTP_URL=http://example.com/healthz
env.FETCH_HTTP_EXPECTED_STATUS=200
env.FETCH_HTTP_EXPECTED_MARKER=FETCH_OK
```

Validation rejects:

- Empty or oversized input.
- Non-ASCII bytes and control characters other than line endings.
- Malformed lines.
- Unknown or duplicate keys.
- Unsupported format or version.
- Missing required fields.
- Unsupported app or runtime selection.
- Oversized app name, app path, or args fields.
- Invalid, duplicate, too many, or oversized environment fields.
- Invalid network and log modes.
- Invalid listen endpoints, including missing host, malformed port, port `0`, port above `65535`, oversized host text, or listen metadata with `network=none`.

## Schema V0 Runtime Contract

Native-tier manifests can omit `runtime` or declare it explicitly:

```json
{
  "schemaVersion": 0,
  "app": {
    "id": "hello.app",
    "entry": "apps/hello.bin"
  },
  "runtime": {
    "kind": "native-aot"
  }
}
```

Hosted .NET 10 manifests reserve the Minimal API/Kestrel launch contract. This is validation only; it does not mean the OS can load CoreCLR, start Kestrel, or serve HTTP yet.

```json
{
  "schemaVersion": 0,
  "app": {
    "id": "minimal.api",
    "entry": "apps/api/host.bin"
  },
  "runtime": {
    "kind": "dotnet10",
    "assembly": "apps/api/MinimalApi.dll",
    "kestrel": {
      "listen": [
        {
          "scheme": "http",
          "host": "0.0.0.0",
          "port": 8080
        }
      ]
    }
  }
}
```

Validation rejects:

- Unknown `runtime.kind` values.
- `native-aot` runtime declarations that also contain hosted `assembly` or `kestrel` settings.
- Missing or malformed `dotnet10` `assembly` or `kestrel` sections.
- Absolute or traversal `runtime.assembly` paths.
- Non-HTTP Kestrel schemes, including `https`.
- Ports outside `1..65535`.
- Duplicate Kestrel listen endpoints.
- Non-object Kestrel listen entries.

## Unsupported Behavior

JSON-to-kernel-manifest conversion, app launcher execution, device requirements, multi-app manifest support, restart policy execution, resource/isolation enforcement, and init metadata source are pending. Kernel-facing listen metadata is parsed, carried, host-tested through bind-policy validation, and composed with the mock socket table only; no live socket is opened, no PAL function is exported, no real TCP listener is created, no scheduler wait is entered, and no server is started. Hosted .NET 10, real Minimal API binding, and Kestrel claims remain governed by `docs/completion-gates.md`.

Image-builder placement validates that a caller-provided source manifest exists, is nonempty, and is no larger than 64 KiB, then writes the bytes to the ESP. It does not currently run `OS.ManifestTool validate` as part of `create-raw`; callers should validate JSON manifests explicitly before packaging. The built-in default is the compact native line format, not JSON.

## Tests

`tests/OS.HostTests/ManifestToolTests.cs` covers valid manifests, optional/native runtime manifests, reserved `dotnet10` Kestrel contracts, malformed JSON, unknown schema versions, unknown runtime kinds, unsafe entry and assembly paths, malformed hosted runtime shapes, invalid Kestrel listen endpoints, duplicate env keys, and oversized input. The tests execute the already-built `tools/OS.ManifestTool/bin/<configuration>/net11.0/OS.ManifestTool.dll` with `dotnet <dll>`, not nested `dotnet run --project`.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` built `OS.ManifestTool.dll` with 0 warnings/errors.
- The integrated host-test run emitted `TEST_PASS manifesttool validates schema v0`, `TEST_PASS manifesttool validates reserved dotnet10 kestrel contract`, `TEST_PASS manifesttool rejects malformed json`, and `TEST_PASS manifesttool rejects invalid kestrel listen endpoints` through the built-DLL process path.
- The profile-aware host-test run emitted `TEST_PASS manifesttool validates default single minimal api profile`, `TEST_PASS manifesttool rejects multiple apps under single minimal api`, `TEST_PASS manifesttool rejects multi app restart policy under single minimal api`, `TEST_PASS manifesttool rejects isolation resources under single minimal api`, `TEST_PASS manifesttool rejects multi minimal api until process model exists`, and `TEST_PASS manifesttool rejects workload profile mismatch`.

`tests/OS.HostTests/ImageBuilderTests.cs` covers optional image-builder manifest placement, including dry-run plan entries, missing/empty/oversized manifest rejection, and byte-level FAT readback from `\OS\MANIFEST.JSON`.

It also covers host-side native payload generation:

```text
TEST_PASS imagebuilder.native-payload writes v0 header
TEST_PASS imagebuilder.native-payload preserves app bytes
TEST_PASS imagebuilder.native-payload rejects missing input
TEST_PASS imagebuilder.native-payload rejects empty input
TEST_PASS imagebuilder.package validates manifest app payload
TEST_PASS imagebuilder.package validation rejects missing manifest
TEST_PASS imagebuilder.package validation rejects malformed manifest
TEST_PASS imagebuilder.package validation rejects missing package
TEST_PASS imagebuilder.package validation rejects missing app file
TEST_PASS imagebuilder.package validation rejects invalid payload header
TEST_PASS imagebuilder.native-app-package builds validated package
TEST_PASS imagebuilder.native-app-package preserves manifest app path
TEST_PASS imagebuilder.native-app-package rejects malformed manifest
TEST_PASS imagebuilder.native-app-package rejects empty app input
```

`tests/OS.HostTests/NativeManifestParserTests.cs` covers kernel-facing native manifest parsing and malformed-input rejection.

`tests/OS.HostTests/NativeManifestLoaderTests.cs` covers host-side loading of a native manifest from a GPT ESP FAT32 image through kernel-facing storage/parser contracts, including the caller-buffer GPT path, primary/backup GPT table-source reporting, backup-GPT fallback, CRC-mismatch rejection, GPT scratch required/available byte reporting, and manifest-buffer required/available byte reporting.

`tests/OS.HostTests/NativeAppLaunchPlanTests.cs` covers launch-plan readiness from a parsed native manifest, missing boot/runtime capability rejection, and malformed requirement rejection. `tests/OS.HostTests/NativeAppLaunchPlanDiagnosticFormatterTests.cs` covers caller-owned `APP_LAUNCH_PLAN status=` diagnostic formatting for selected apps, missing runtime capability failures, short destinations, and default-result rejection.

`tests/OS.HostTests/NativeAppPayloadHeaderTests.cs` covers the v0 native payload header and status-coded rejection for bad magic, unsupported version, and invalid entry offsets.

`tests/OS.HostTests/NativeAppLaunchDescriptorTests.cs` covers launch-descriptor rejection of an invalid selected native payload header while preserving missing package-file behavior in the boot-sequence descriptor tests.

`tests/OS.HostTests/NativeAppSelectionLoaderTests.cs` covers one-shot caller-buffer selection loading from a synthetic GPT ESP image, including manifest/package primary/backup GPT table-source reporting, backup-GPT fallback after a primary GPT failure, manifest-load failure, package-load failure, manifest/package scratch-capacity propagation, launch-plan capability failure, descriptor failure propagation, and default-result rejection.

`tests/OS.HostTests/NativeAppSelectionBootScratchPlanTests.cs` covers the fixed boot-owned scratch sizing plan that should be satisfied before calling the selection loader from boot storage code.

`tests/OS.HostTests/NativeAppEntryStagingPreflightTests.cs` covers the final
app-entry staging preflight over already-produced package-staging readiness,
including success, payload-staging failure, default package-staging readiness,
default preflight rejection, and the no-live-storage/no-launch boundary.

Current kernel parser markers:

```text
TEST_PASS manifest kernel parses v0 native manifest
TEST_PASS manifest kernel parses package app fields
TEST_PASS manifest kernel parses environment fields
TEST_PASS manifest env diagnostic formats environment metadata
TEST_PASS manifest env diagnostic formats empty environment
TEST_PASS manifest env diagnostic reports short destination
TEST_PASS manifest env diagnostic rejects default result
TEST_PASS manifest env diagnostic rejects invalid source range
TEST_PASS manifest env formatter uses shared ascii text builder
TEST_PASS boot.sequence manifest env ready
TEST_PASS boot.sequence manifest env rejects parse failure
TEST_PASS boot.sequence manifest env rejects invalid source range
TEST_PASS boot.sequence manifest env rejects default result
TEST_PASS boot.sequence manifest env formats diagnostic failure marker
TEST_PASS boot.sequence manifest env preserves default markers
TEST_PASS manifest kernel parses listen endpoint
TEST_PASS manifest kernel rejects invalid listen endpoint
TEST_PASS manifest kernel rejects duplicate environment keys
TEST_PASS manifest kernel rejects invalid environment key
TEST_PASS manifest kernel rejects too many environment fields
TEST_PASS manifest kernel accepts maximum string field lengths
TEST_PASS manifest kernel rejects oversized string fields
TEST_PASS manifest kernel rejects unsupported runtime kind
TEST_PASS manifest kernel rejects missing app path
TEST_PASS manifest kernel rejects malformed network mode
TEST_PASS manifest kernel rejects unknown format
TEST_PASS manifest kernel rejects unknown version
TEST_PASS manifest kernel rejects duplicate keys
TEST_PASS manifest kernel rejects oversized input
TEST_PASS manifest kernel rejects non-ascii input
TEST_PASS manifest kernel rejects control characters
TEST_PASS manifest kernel rejects unknown keys
TEST_PASS manifest kernel rejects malformed log mode
TEST_PASS manifest kernel preserves token splitter semantics
TEST_PASS manifest kernel preserves equals inside values
TEST_PASS manifest kernel uses shared key value splitter
TEST_PASS manifest fetchapp config preflight valid config
TEST_PASS manifest fetchapp config preflight defaults
TEST_PASS manifest fetchapp config preflight invalid status
TEST_PASS manifest fetchapp config preflight empty marker
TEST_PASS manifest fetchapp config preflight oversized marker
TEST_PASS manifest fetchapp config preflight manifest not ready
TEST_PASS boot.sequence fetchapp config preflight ready
TEST_PASS boot.sequence fetchapp config preflight rejects failed result
TEST_PASS boot.sequence fetchapp config preflight rejects default result
TEST_PASS boot.sequence fetchapp config preflight preserves default markers
TEST_PASS boot.sequence fetch http endpoint ready
TEST_PASS boot.sequence fetch http endpoint rejects failed result
TEST_PASS boot.sequence fetch http endpoint rejects default result
TEST_PASS boot.sequence fetch http endpoint preserves default markers
TEST_PASS manifest loader reads native manifest from gpt esp
TEST_PASS manifest loader caller-buffer gpt reads native manifest
TEST_PASS manifest loader caller-buffer reports primary gpt source
TEST_PASS manifest loader caller-buffer gpt falls back to backup
TEST_PASS manifest loader caller-buffer reports backup gpt source
TEST_PASS manifest loader caller-buffer gpt rejects crc mismatch
TEST_PASS manifest loader caller-buffer reports gpt header capacity
TEST_PASS manifest loader caller-buffer reports gpt entry buffer capacity
TEST_PASS manifest loader caller-buffer reports manifest buffer capacity
TEST_PASS manifest loader rejects missing esp
TEST_PASS manifest loader rejects malformed native manifest
TEST_PASS appmodel launch plan accepts manifest
TEST_PASS appmodel launch plan accepts packaged native app
TEST_PASS appmodel launch plan carries environment fields
TEST_PASS appmodel launch plan records listen endpoint
TEST_PASS appmodel launch plan requires network for listen endpoint
TEST_PASS appmodel launch plan rejects missing capabilities
TEST_PASS appmodel launch plan rejects malformed requirements
TEST_PASS appmodel launch diagnostic formats selected app
TEST_PASS appmodel launch diagnostic reports short destination
TEST_PASS appmodel launch diagnostic formats missing runtime
TEST_PASS appmodel launch diagnostic rejects default result
TEST_PASS appmodel args vector splits manifest args
TEST_PASS appmodel args vector preserves quoted spaces
TEST_PASS appmodel args vector accepts empty args
TEST_PASS appmodel args vector rejects too many args
TEST_PASS appmodel args vector rejects malformed quotes
TEST_PASS appmodel args vector rejects failed launch plan
TEST_PASS appmodel args vector default result is not success
TEST_PASS appmodel args vector formatter formats success marker
TEST_PASS appmodel args vector formatter reports short destination
TEST_PASS appmodel args vector formatter rejects default result
TEST_PASS appmodel.socket.policy accepts manifest listen endpoint
TEST_PASS appmodel.socket.policy accepts wildcard manifest endpoint
TEST_PASS appmodel.socket.policy rejects missing listen endpoint
TEST_PASS appmodel.socket.policy rejects wrong port
TEST_PASS appmodel.socket.policy rejects wrong address
TEST_PASS appmodel.socket.policy preserves no-socket-io boundary
TEST_PASS appmodel.socket.bindbridge binds manifest-approved endpoint
TEST_PASS appmodel.socket.bindbridge rejects policy mismatch
TEST_PASS appmodel.socket.bindbridge propagates mock bind conflict
TEST_PASS appmodel.socket.bindbridge rejects missing table
TEST_PASS appmodel.socket.bindbridge preserves mock-only boundary
TEST_PASS appmodel.socket.listenbridge creates manifest listener
TEST_PASS appmodel.socket.listenbridge rejects policy mismatch
TEST_PASS appmodel.socket.listenbridge propagates create failure
TEST_PASS appmodel.socket.listenbridge propagates bind failure
TEST_PASS appmodel.socket.listenbridge propagates listen failure
TEST_PASS appmodel.socket.listenbridge preserves mock-only boundary
TEST_PASS pal.config table exposes manifest listen endpoint
TEST_PASS pal.config table exposes listen host and port
TEST_PASS pal.config facade returns manifest listen endpoint
TEST_PASS pal.config table omits listen endpoint when absent
TEST_PASS pal.config table preserves host-only boundary
TEST_PASS appmodel payload header accepts v0 native payload
TEST_PASS appmodel payload header rejects bad magic
TEST_PASS appmodel payload header rejects unsupported version
TEST_PASS appmodel payload header rejects invalid entry offset
TEST_PASS appmodel launch descriptor rejects invalid payload header
TEST_PASS appmodel selection loader composes image inputs
TEST_PASS appmodel selection loader reports primary gpt source
TEST_PASS appmodel selection loader gpt falls back to backup
TEST_PASS appmodel selection loader reports backup gpt source
TEST_PASS appmodel selection loader rejects manifest load
TEST_PASS appmodel selection loader reports manifest scratch capacity
TEST_PASS appmodel selection loader rejects package load
TEST_PASS appmodel selection loader reports package scratch capacity
TEST_PASS appmodel selection loader rejects launch capability
TEST_PASS appmodel selection loader rejects descriptor failure
TEST_PASS appmodel selection loader default result is not success
TEST_PASS appmodel.selectionscratch accepts default image buffers
TEST_PASS appmodel.selectionscratch rejects short manifest buffer
TEST_PASS appmodel.selectionscratch rejects short package buffer
TEST_PASS appmodel.selectionscratch rejects short gpt entry buffer
TEST_PASS appmodel.selectionscratch rejects invalid requested sizes
TEST_PASS appmodel.selectionscratch preserves no-live-storage boundary
TEST_PASS appmodel entry staging preflight accepts staged package and payload
TEST_PASS appmodel entry staging preflight rejects payload staging failure
TEST_PASS appmodel entry staging preflight rejects default staging readiness
TEST_PASS appmodel entry staging preflight default result is not success
TEST_PASS appmodel entry staging preflight preserves no-launch boundary
TEST_PASS imagebuilder.manifest-file
```

## Known Limitations

The JSON manifest validator is host-side only. The kernel native parser, loader, app selection loader, app launch plan, and native manifest environment readiness marker are host-tested only and are not wired to boot, virtio-blk, process environment materialization, listen/bind socket behavior, hosted .NET, Kestrel, or app launch execution. The caller-buffer manifest and selection loader overloads prove the GPT CRC/readiness boundary, primary/backup GPT source reporting, backup-GPT fallback, and GPT/manifest scratch-buffer size reporting over an in-memory block device only, and the selection-loader default-result guard only prevents accidental uninitialized success in that host-tested contract. The boot scratch plan now makes the required boot-owned buffer sizes explicit, but it does not allocate them, read a device, parse a manifest/package, or produce guest storage evidence; a real block-device implementation is still required.

Profile-aware JSON validation is host-side only. It prevents accidental
acceptance of future multi-app schema fields in `SingleMinimalApi` and blocks
`MultiMinimalApi` until required process/isolation/supervisor behavior exists,
but it does not implement those behaviors.

`validate-package` and `build-native-app-package` are also host-side only. They prove manifest/package/payload consistency for local artifacts, not in-guest package reads, executable mapping, or native app launch.

The accepted `dotnet10` contract is reserved for the mandatory hosted .NET 10/ASP.NET Minimal API/Kestrel milestone. It is not runtime evidence; use `docs/completion-gates.md` for the claim boundary.

## Next Steps

1. Add JSON-to-native conversion into image builder if the host JSON manifest should become the source of the kernel native manifest.
2. Add boot/package integration that calls the native manifest loader after real storage exists.
3. Map `NativeAppSelectionLoader` outputs to the future app launcher after live package reads exist.
4. Wire profile-aware validation into image-builder packaging commands if `OS.ManifestTool` should be mandatory before raw image creation.
5. Map the reserved hosted contract to the future `OSHost_*` PAL, scheduler, socket, timer, and file/package loading requirements for `SingleMinimalApi` first.
6. Add Kestrel runtime smoke evidence before claiming hosted .NET 10 support.
