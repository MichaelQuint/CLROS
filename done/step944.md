# Step 944 - Accepted complete host, terminal page-fault guest and image accounting

## Summary

The clean `7c71e7273738882e7c8d3566bd9bb1bae1543049` source (tree
`a5526072f96648bdfbf4432cf70b5106894da552`) passes the complete host suite,
fresh default and explicitly enabled page-fault QEMU/OVMF lanes, and separate
pristine/booted image accounting. All three evidence packages received an
independent review with no remaining findings before this documentation update.

The enabled guest executes one pre-`ExitBootServices` supervisor null read and
captures vector 14, hardware error zero, CR2 zero, the owned root, null/four-guard
absence, saved RIP/RF and an IST2-contained frame. Ordinary probes stay off.
The M04-007 allocator gains complete host integration evidence only; #76 stays
open. Gates 3 and 4 remain **Fail**.

This is a documentation/catalog checkpoint after execution, not another tested
source revision. PR #518 merge, reviewed evidence comments and #54/#517 closure
follow as separate tracker transactions. Catalog `desiredState: closed` for
M02-014 records accepted closed intent, not an already completed tracker action.

## Why

Steps940-943 implemented the bounded allocator and terminal fault adapter, then
repaired historical fixtures exposed by complete execution. Focused/compiler
checks could not establish the complete host gate or a real fault. This step
records the actual successful executions, retains every failed attempt and
replaces stale current-status claims in the canonical docs.

## Files changed

- `README.md`: current bounded fault checkpoint.
- `docs/architecture.md`: historical versus current EFI source linkage.
- `docs/diagnostics.md`, `docs/boot-order.md`, `docs/efi-build.md`,
  `docs/qemu.md`, `docs/completion-gates.md`: actual accepted evidence and limits.
- `.gitea/backlog/clros-issues.json`: four M02-014 evidence/command/intent fields
  and the M04-007 current-evidence field; all other entries remain unchanged.
- `done/step944.md`: this record, reserved with `scripts/new-done-step.ps1 -Reserve`.

No production/test source, build script, dependency or generated artifact changes
in this checkpoint. Earlier done records remain immutable.

## Tests run

From `C:\Projects\CLROS`:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Build: 2026-10-02 11:51:42.7535924–11:52:06.4402675 UTC, native exit 0,
zero warnings/errors and empty stderr. Complete host run:
11:52:34.6392029–12:55:54.5017058 UTC, native exit 0, 3,558 `TEST_START`,
3,559 `TEST_PASS`, zero `TEST_FAIL`, exactly one final `TEST_PASS host.tests`.
Raw stdout is 460,779 bytes; stderr is empty. The original launch binaries remain
unchanged with actual ProductVersion `1.0.0+7c71e7273738882e7c8d3566bd9bb1bae1543049`.
All 206 selected marker pairs across 24 affected modules and five ready records
were independently checked; the final complete suite is not a sum of partial runs.

Two actual `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
scripts/run-qemu-smoke.ps1` invocations used `-BuildEfi`, the installed
`C:\Program Files\qemu\qemu-system-x86_64.exe`, OVMF
`C:\Program Files\qemu\share\edk2-x86_64-code.fd`, `-TimeoutSeconds 30`,
and distinct fresh EFI/image/serial/summary output paths. Exact argument arrays
are preserved in each lane's `command.json`, bound by hashes below. The enabled
lane additionally supplied all nine switches:

```text
EnableM01LoadedImageStackProbe EnableM01LiveAllocatorProbe
EnableM01OwnedPagingProbe EnableM02SafeStubProbe
EnableM02CheckedPortIoBindingProbe EnableM02Com1OwnedPortIoProbe
EnableM02OwnedCom1ReadyProbe EnableM02PanicSafeOwnedCom1Probe
EnableM02PageFaultCr2ErrorCodeProbe
```

Default lane: 13:03:14.1577627–13:04:22.7715405 UTC. Enabled lane:
13:04:23.9646306–13:05:34.3324789 UTC. Each actual wrapper exits 0, records
QEMU exit -1 after runner termination and a 750 ms quiet drain, with no timeout,
cleanup error, invocation error or stderr. QEMU is 11.0.50
`v11.0.0-12631-g54e84cdc7a`. Both actual archived `OS.dll` files were inspected
in distinct fresh metadata-only processes: assembly `OS`, zero AssemblyRefs.
Requested/evaluated properties agree: 24 each, zero enabled by default and nine
enabled in the fault lane. Default marker inventory is 12/0/0/2 and fault
inventory is 77/68/68/44 (required/ordered/unique/forbidden).

Separate accounting executed once at 13:20:33–13:20:40 UTC using
`Run-PageFaultWaveImageAccounting-7c71e72.ps1` with the actual matrix directory.
Eight native commands (create pristine, extract pristine, extract booted and
audit FAT per lane) all exited 0 with no error and empty stderr. The accepted
archived Debug ImageBuilder and dependencies were used without rebuilding.
The reviewer reran only the two read-only auditors, reproducing their JSON
results exactly; no guest or image creation was repeated by the reviewer.

After the documentation/catalog edits, the existing focused manifest harness
(`dotnet run --project artifacts/orchestration-20261001/manifest-focused/ManifestFocused.csproj
--no-build`) passed 17 starts/18 passes, zero failures, native exit 0 and empty
stderr at 13:46:09–13:46:32 UTC. It executes the actual unchanged
`GiteaBacklogManifestTests` against the edited catalog. `git diff --check` also
passed. Results are in `issue517-final-checks/commands.json`; catalog stdout
SHA256 is `A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9`.
The earlier step943 naming/source-firewall checks remain applicable because
this checkpoint changes only documentation and the five catalog fields.

## Evidence

All following artifact paths are relative to
`artifacts/orchestration-20261001/`; generated evidence stays uncommitted.
The root was clean and source identities unchanged throughout execution and
independent acceptance. Subsequent documentation edits retain those source pins.

| Record | SHA256 |
| --- | --- |
| `issue517-build-7c71e72/result.json` | `B81C2B48E01CC4558E3A44C4F8C9759DC7B5C235A8CF8ADF70739DFD603EAE53` |
| `runtime-next-wave-7c71e72/result.json` | `48B67B5D9678BCB7DA65BBD470F6F17E87E371137FD06B354AD91077D21BCE8E` |
| `runtime-next-wave-7c71e72/host-tests.stdout.log` | `D75D81A259B0E39AB27CFF73B4478C2CBB2F0ADE680B1AF7FCE809FD71170B5B` |
| `runtime-next-wave-7c71e72-independent-review/completed-acceptance-report.md` | `86505398AB1A1E01EFB409FBA7832F2F587EED2ECF8F74A8A3E21614F54560E8` |
| `runtime-next-wave-7c71e72-independent-review/completed-acceptance-check.json` | `8D50F1343941930F32CAEC72157FA28A94D29E909067012FC2A352EB0A1982D9` |
| `issue517-guest-7c71e72-20261002T1302597862678Z/result.json` | `BDA4AF1938B1D457E9DCE548781B8C1E14FD20751159EE339B3F53F828044DBC` |
| `issue517-guest-7c71e72-independent-review/acceptance.md` | `B4F574195D786435112AAABC20204C7BC4311AB09BD3D72F04DD9F58307AAFC3` |
| `issue517-pristine-7c71e72-20261002T1320334877373Z/result.json` | `5E3A6C6633A71560226379535061416BE3E46CE5322E0B0E0E279D7433AD89EA` |
| `issue517-accounting-7c71e72-independent-review/acceptance.md` | `96B68EDB56FBB0C4228C4EFAA104F9D77273E0D623BD0B8BEE815D99A66D0026` |

Compile closure: 75 items,
`FAFC07E210DDE2A3A8E2D537F54032B2809CB01258A23B4D513E1D868DC74A9B`.
Raw source authority: 88 inputs,
`502857EFE5C5789C31FC5B4D51726522CBBEA366C48CD90BF634C3F343098F70`.
HostTests DLL: `DFD5941FFBC3FBEC954CB0682C8A090965BE0DF5272899F9884828E82532B6F8`;
both loaded and project Kernel DLLs:
`2F13B4A0B23939E3F47F00E00DA8B48BBDC7B9FBF49B2F6762CB15335A90D0FC`.
QEMU executable: `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`;
OVMF: `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

The guest matrix folder above contains `default` and `pagefault` archives.
The independent reviewer rehashed all 1,151 immutable archived files, inspected
flags, closure/link/map/tool/source provenance and reran raw/summary/lifecycle
auditors. The matrix's `ImageAccountingExecuted=false` remains its historical
fact; the later accounting result is a separate record.

| Actual artifact | Default | Page fault |
| --- | --- | --- |
| EFI bytes | 55,808 | 558,592 |
| EFI SHA256 | `EBAC4F464E5E9E21ECB8830D57DD15BEA4FC9705B5CD1B5AD1F8F67F1FFA77FD` | `9EC5A3C3494F5EF5E9B8405E17D8E9F3D63E1E71FCA32B4438FADC82C89D2961` |
| OS.dll SHA256 | `482FB11E61CF5B29C901E710715F49EA58EA7F5F59CD461366E1BADDF3BB210B` | `EA7ECF110B2F8F685E2D8A3433348680AA05DA73C0A2C0E16B36B0CE238A82C6` |
| Serial SHA256 | `0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1` | `6DA5612165BA04FEF0D0039DD590F16B50587729299BBA1B013AD38038480D5E` |
| Summary SHA256 | `9BB027012EA43D195529C7DAE5515CC243A7D0E14F549069ED51FFCCF8B72761` | `753DDA75FDE5137F05320848525F21F7D27EB90D40C006F8F067ED8479AD0D6F` |
| Command SHA256 | `1414AE1666F2E4C8B28FA37770463DA892F39BD100BF668753D718C36EF9EA01` | `B002EAD413A9AA99F72ACA8A1E6F2314DD0B6CBFE8B7266128E131AA58879B82` |
| Acceptance SHA256 | `89289A50674F3E88061FF93492ECA4BC462FF0903D57A41E8FCEC57DD6099759` | `7545F36A109D5DB83F5CA76D2CA0ACFC97C015C291EC1F8AC25F56DB9F21B0D3` |
| Pristine image SHA256 | `FBA535E0ADDFEC46D56C69A6151CBCFAABC4DCBCCA068C9F5556CF9F2AB267D6` | `F2D47EB394313CBF1A4BFEA21E4A34E30918B88DF2877B021253B9BA61619751` |
| Booted image SHA256 | `C08463DD47EB5DF9A4AB0AE2967DBD95E46D0050732D5D9575C9CC0EACC548B7` | `0F1EE8C38C32FF59D73215C79F83953791D82612CBE7E6B3D52B4A900EEEAC90` |

The fault transcript contains 86 CLROS records: 74 prefix records and twelve
terminal records, with 57 numeric fields and a 2,053-byte terminal schema.
Code/data/table bases are `0xDC3F000`/`0xDC35000`/`0xDC0A000`, with C3/D10/T43
resources and 52 mapped leaves. Fault RIP is `0xDC40B19` (code + `0x1B19`).
Firmware and saved CS are actually `0x38`, CR0 `0x80010033`, firmware CR3
`0xF801000`, CR4 before/current `0x668`, EFER `0xD00`, PAT
`0x0007040600070406`, and physical-address width 40. PGE was already clear.
Active CR3 equals the recorded table base. The zero-address walk stops at
level 3; the four guards stop at level 4 with absent PTEs.

The frame reports vector 14, a hardware error code of zero, CR2 zero, the exact
fault RIP, RFLAGS `0x10046`, saved RSP `0xDC3F000`, saved SS `0x10`, handler
RSP `0xDC3AF40` and handler SS zero. From the captured data base and audited
layout, IST2 usable storage is `[0xDC3A000,0xDC3B000)` and the complete 192-byte
frame is below its top. Current-handler CS=8 and LAR rights are checked by the
source/path; they are not separately printed raw descriptor dumps.

```text
CLROS_M02_014_READY vector=14 hardwareErrorCode=true errorCode=0 cr2=0 pageZeroAbsent=true guardPagesAbsent=4 ist=2 rf=true ownedCr3=true hardwareStateCaptured=true faultTriggered=true terminalHalt=true postExitBootServices=false
```

Default output preserves the established returning-OVMF contract. The enabled
terminal tail has no firmware-return suffix. The runner's bounded quiet drain
and termination are not an indefinite halt-duration observation.

Image accounting uses archived Debug/net10.0 ImageBuilder DLL
`FC2D3037767AE7D3E94E58EB5C120A7FCB4750092D82D357CED9F24405C9B13A`,
with source and dependencies bound by builder inventory
`E7CF6DEAB1D275A7E522A51CDCBAC7620D23AE09D634423EB0ED92D6218BCC66`.
Both images are 64 MiB with 512-byte sectors/clusters, 32 reserved sectors,
two matching 1,008-sector FATs, data LBA 4096, 126,943 data clusters and
129,024 FAT entries. Pristine census, zero unused FAT tail and both FSInfo
copies pass. FAT status is clean/no-hard-error in all four observations.

| Lane | Pristine allocated/free/next | Booted allocated/free/next | NVVARS clusters | Changed bytes/ranges |
| --- | --- | --- | --- | --- |
| Default | 114 / 126829 / 116 | 117 / 126826 / 119 | 116–118 | 887 / 341 |
| Page fault | 1096 / 125847 / 1098 | 1099 / 125844 / 1101 | 1098–1100 | 892 / 340 |

Each NVVARS file is 1,393 bytes in exactly three newly allocated clusters.
Every changed byte belongs to primary FSInfo, both FATs, the root entry or
NVVARS data. Payload, manifest and outside-partition bytes remain unchanged.
Primary FSInfo hints match the new census; backup hints remain pristine/stale.
`FilesystemConformanceEstablished=false` remains an explicit limitation.

## Failures

No failure occurred in the final build, complete host, actual guest or accounting
stages. Earlier evidence is preserved, not relabeled as passing:

- `05b2bde` complete host run: native -532462766; 1,210 starts, 1,206 passes,
  seven failures. Step942 records source-selector/lookup/page-zero fixture repairs.
- `fc561cb` complete host run: native -532462766; 1,362 starts, 1,360 passes,
  two failures. Step943 records obsolete Compile-offset corrections.
- Original nineteen-module focus: native -532462766; 78 starts, 76 passes,
  two failures from the stale owned-COM1 prerequisite count.
- Intermediate reduced seventeen-module harness: build exit 1, four CS0103
  diagnostics, zero tests. The separate corrected sixteen-module focus passes
  102 starts/103 passes, zero failures, native 0. It is not a nineteen-module pass.
- The earlier canceled standalone attempt in step941 remains canceled.

The final full suite independently reran all modules successfully. The review
rehashed preserved failed result files and found them unchanged.

## Deferred work

- Actual RhpNewFast runtime export, compiled-new helper/ABI routing, authentic
  MethodTable/object layout, physical object/header/cursor/canary evidence and
  required fixed class/value/generic/reference guest cases remain #76 work.
- After #54 merge and closure, the next diagnostics slice is #55 double-fault
  IST planning and host implementation, followed by separately reviewed guest
  integration.
- Reference barriers, boxing payload behavior and GC remain separate scopes.

## Evidence boundary

This proves one explicit pre-firmware terminal null-read profile under the
recorded local QEMU/OVMF environment. It does not establish default/persistent
IDT/GDT/TSS/IST ownership, recoverable or nested faults, NMI/SMI safety, general
paging, `ExitBootServices`, a functional object runtime, live devices/storage/
networking, hosted .NET/Kestrel, cloud support or production readiness. Host
readiness markers continue to distinguish their nonexecuting scope from the
actual guest transcript. Gates 3 and 4 remain **Fail**.
