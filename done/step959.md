# Step 959 - Accept the unexpected-vector panic and nine-profile regression wave

## Summary

Parent joint acceptance at clean source
`dee6df223b471a86f4bc5aea10c9747d3ac2ee29`, tree
`b6eea59362b19ce89dc2f534f42d3a5c7475d192`, accepts the complete host suite
and nine fresh QEMU/OVMF profiles. M02-016's vector-32 experiment produces the
exact expected panic with independent native, raw, lifecycle and disk review.
The shared String-capable host allocator also retains the existing bounded
fixed/array guest behavior. This is not String guest execution.

Joint result:
`artifacts/orchestration-20261001/issue526-joint-acceptance-dee6df2/result.json`,
SHA-256 `A512693F128EA8BF936DC76D34EC1C5393608F232CBDAB85F3C4656AD254B733`.
Original capture/parser `GuestAccepted=false` fields remain untouched; this
separate result combines their explicitly limited scopes. PR527 merge and
#56/#526 closure are subsequent verified tracker actions, not asserted here.

## Why

Steps955/956 introduced the shared String storage host entry and default-off
panic adapter. Steps957/958 preserved two failed complete-host attempts and
repaired stale source/profile assertions. Those focused results did not replace
the required settled-source host gate and fresh guest/native regression proof.
This milestone records that proof and corrects the obsolete marker-only #56
contract. The intended outcome is a classified panic, not a fabricated
`CLROS_M02_016_READY` guest success marker.

## Files changed

- `README.md`, `docs/repo-audit.md`: current accepted checkpoint and remaining work.
- `docs/completion-gates.md`, `docs/diagnostics.md`, `docs/efi-build.md`,
  `docs/boot-order.md`, `docs/qemu.md`: actual panic evidence and its limits.
- `docs/nativeaot-nostdlib-limits.md`: accepted shared-core compatibility;
  native String remains pending.
- `.gitea/backlog/clros-issues.json`: concrete #56 expected-panic contract and
  conditional closed intent; exact three-field #76 closure reconciliation;
  current-evidence-only updates for #77 and #78. Other rows, ownership and
  dependencies are preserved.
- This newly reserved step. No runtime, test, project or build-script source
  changes are part of this documentation transaction.

## Tests run

From `C:\Projects\CLROS`, at the clean source above:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

The solution build returned0 with zero warnings/errors and empty stderr at
12:36:42–12:37:05 UTC on2026-10-03. The complete host run returned0 at
12:47:54–14:11:16 UTC: **3,636 starts,3,637 passes,zero failures,one final
host terminal**. Exact marker inventories, all11 READY records,508 source
identities and all three launch-binary identities passed independent review.
The three binaries remained byte-identical; each actual ProductVersion bound
the run to `dee6df2`. No active output was treated as completion evidence.

The frozen capture helper `Run-Issue526GuestCapture.ps1` SHA-256
`ED756B4FB76A680CFB232554C03B934CD63C49D38E77BDF048E1DD42D76AA513`
then ran all nine profiles, completing at14:40:11 UTC. The actual panic child
command used `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
scripts/run-qemu-smoke.ps1 -BuildEfi`, actual installed QEMU/OVMF paths,
`-TimeoutSeconds 30`, unique EFI/image/serial/summary paths, the eight
prerequisite switches and `-EnableM02UnexpectedVectorPanicProbe`.
Every exact argument array, timestamp, native exit and separate output is
retained in the lane's `*.command.json`/`*.result.json` files; the two negative
array lanes use their separately recorded bounded observer commands.

The completed matrix contains5,289 archived file records and13 native
wrapper/build/image/observer invocations, all0 with empty separate stderr.
All nine bind82 Compile items,95 raw inputs,32 evaluated/requested flags and
zero managed OS AssemblyRefs. Read-only metadata/native/raw/lifecycle/disk
review commands all completed at their stated scopes. The corrected panic
auditor additionally passed366 synthetic controls; these are parser controls,
not366 guest executions.

Final documentation/catalog checks passed on2026-10-03:

- The unchanged complete `GiteaBacklogManifestTests` initializer, via the
  previously reviewed focused harness, returned0 at15:23:43–15:24:05 UTC:
  exact17 starts/18 passes/zero failures/one terminal, empty stderr and all
  snapshotted inputs unchanged. Result SHA-256
  `F4813776858C1F793A78FB288107E822CF05EB62FCEF2EFB61AC235230F7DEDF`.
- `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File
  scripts/check-naming.ps1` returned0 with both naming/source-firewall pairs
  and empty stderr at15:24:11–15:24:52 UTC. Result SHA-256
  `8F0B37D5BBCAB63ABA8C7EE7B19AA17B3BB0D2451B0AD4A1202B45F0D26E00CF`.
- The same PowerShell invocation of `scripts/run-qemu-smoke.ps1 -DryRun
  -BuildEfi -EnableM02UnexpectedVectorPanicProbe` returned0/empty stderr and
  `TEST_PASS qemu.runner.dryrun` at15:24:52–15:24:55 UTC. Result SHA-256
  `DC191BE90DEFF3AFE367965179DF34C27758999E544247519467E775E5433697`.
  This is command preflight, not additional guest proof.
- `git diff --check` returned0 with empty streams. Result SHA-256
  `21DFE845752195FF145F3C78DECC295043BE3F46EE7FE49E515ED361B9FB6224`.
- Independent documentation review found one stale M02-016 catalog boundary
  that still denied the newly accepted host/native/panic proof. After its
  one-field correction, the same complete manifest initializer returned0 at
  15:31:22–15:31:44 UTC with exact17 starts/18 passes/zero failures, empty
  stderr and all21 inputs unchanged. Final result SHA-256
  `B162FD3D0443B78708AD063A7DB803E287B6567251AAAD25B077B1B0045E139A`
  is in `issue526-final-doc-checks/attempt2/result.json`; the first passing
  attempt and the review finding remain preserved. Final catalog SHA-256 is
  `78E16AB8D029F26D8AD5A8C0D600E0B57CB9F60F44E1C9F254930369826EB585`.

These final results and full raw logs are under `issue526-final-doc-checks/`.
Only this step's check-result text was added after the manifest snapshot;
the catalog and other tested inputs were unchanged. The accepted complete
host suite is not rerun for documentation alone.

## Evidence

All following relative evidence paths are under
`artifacts/orchestration-20261001/` and remain generated, uncommitted output.

| Evidence | SHA-256 |
| --- | --- |
| `issue523-build-dee6df2/result.json` | `B3A9349FD364C1D3716B69B7EB76346C1E6870630DA773756660B2CA930501F8` |
| `runtime-next-wave-dee6df2/result.json` | `4458F1A82EE4B4FA4CBCB5348D505E42025B62DA53D75FC1322E8739038DF444` |
| `runtime-next-wave-dee6df2/host-tests.stdout.log` | `D359F812A52F1090E472B74FB1EF99C5EFC86846F2FA05808242E6FDD2C567B5` |
| `issue526-guest-dee6df2-20261003T1429206847696Z/capture-result.json` | `702AC7069814375B22B03DE8FB8CD0D5F38812755DBC9565191207FB913DC8BC` |
| `issue526-parent-capture-review-dee6df2/review.md` | `51B1912E99F39C6230D82FA04826F6E4D6F256E0854C631A0CFAB6CFB2057ED5` |
| `issue526-parent-capture-independent-review-dee6df2/review.md` | `B3AB443EB1EDF50D5B106F6EED71B052762FF4B3A69A6FDE0BBAF331F2F4DE8C` |
| `issue526-captured-panic-independent-review-dee6df2/review.md` | `07C7F91C03F9ADA8891E98D0DA9F90153399FE6D34E7FCFB0E22F87A40224B70` |
| `issue526-ud2-pf-df-actual-review-dee6df2/review.md` | `907EBE9CEC1486E20C9479AF257C795B4661539F56BE3007855010F3275927A2` |
| `issue526-fixed-array-native-review-dee6df2/review.md` | `0FBC0D49541F52D75AEE5B836146149AE2CF32AC0655AA1F8B76ECFE72A2887A` |
| `issue526-actual-disk-review-dee6df2/review.md` | `13777F963FFF23CB6EB1184E8D538985E2A3DDE3674B484EEA3E8EB56B90E64E` |

Parent read these reports and their relevant machine results and rehashed
the package/input manifests: panic315/1,453; UD2/PF/DF143/2,396;
fixed/array163/36 plus actual archived associations; independent coordinator
7/5,438; disk13/key inputs and all5,903 consumed identities.

| Profile | Accepted observation |
| --- | --- |
| Default | Exact prior7,947-byte returning serial;21 CLROS records; optional adapters absent. |
| Panic | 76 records;650-byte/24-field current-state record; exact vector32 fault and EOF; wrapper0, source-bound runner19, QEMU-1/owned termination, measured760ms quiet. |
| UD2 | 77 records; actual compiled23 comparisons/37 failure branches,341-byte success payload,4,096-byte stream readback and496-byte context contract. No numeric C/D/register observations are printed. |
| Page fault | 86 records; vector14/error0/CR2zero and current-state/native associations. |
| Double fault | 88 records; vector8/error0/current IST1 associations. All21 saved-state fields remain diagnostic only. |
| Fixed | Four compiled-new/constructor and six direct routes; ten actual24-byte metadata prefixes; used272/count10 and retained authority. |
| Mixed array | Genuine byte/long newarr/helper returns, length/data/zero storage and retained earlier objects; used160/count5; six observed full prefixes. |
| Before bind | Exact expected rejection, two attempted tokens and zero observed prefixes; unchanged storage; measured756.2468ms quiet. |
| Exhaustion | Full4096/count169 then unchanged rejection; six observed full prefixes; measured755.0241ms quiet. |

Ordinary returning/fault runners record configured750ms quiet drain; the
review does not invent an independently measured duration. Negative arrays
are accepted expected rejections, not ordinary smoke passes.

The panic's actual113-method/ten-table/218-entry graph bounds total access and
stack depth at5,208 bytes including the232-byte bridge, within8,176 and2,968
bytes above sentinel end. Fresh native method records, relocations, unwind,
private port targets and actual callback/C/D associations match the reviewed
proof. Whole managed/COFF files are not falsely claimed identical: the known
COFF debug section and response checkout paths differ. Firmware/NativeAOT
entry SSE support is still required before the later mode check.

All nine independently reconstructed/booted disk payloads preserve the exact
archived EFI. GPT/FAT geometry and all allocation chains pass bounded review;
firmware changes are confined to FSInfo/FAT/root entry and the1,393-byte
NVVARS payload. Both negatives preserve byte-identical original prelaunch
pristine images; ordinary lanes prove reconstruction/payload identity without
inventing unavailable original prelaunch copies. This is not general filesystem
conformance.

## Failures

- Failed complete suites at `74bb2e7` and `aa84d3d` remain recorded in
  steps957/958. The successful run supersedes their pending status, not their logs.
- The first panic audit rejected the LF host runner block as `extent_runner`.
  An independently reviewed strict-LF decoder was added only for that block;
  raw serial and summary remain strict CRLF. All19 runner records and every
  value/order check remain enforced. Original failure and366-control rerun
  are preserved; no product change or guest rerun was needed.
- Parent and fixed/array review commands initially inherited an incompatible
  PowerShell module path. Standard Windows PowerShell module-path retries
  ran the same validation and passed. Original failures remain.
- Native review glue exposed documented selector, relocation, serializer and
  branch-decoder assumptions; corrected read-only associations passed against
  unchanged actual images. Each companion report lists its preserved attempts.
- Direct step reservation encountered the host's unsigned-script policy before
  execution; the established `powershell.exe -ExecutionPolicy Bypass -File`
  invocation reserved this step successfully. No existing step was overwritten.

## Deferred work

- Merge PR527 and post/read back #56/#526 closure after the reviewed evidence.
- #58 memory-map growth/slack/overflow regression slice, then its separately
  owned native admission and guest requirements.
- #78 genuine String runtime export, returned constructor and character storage
  in a fresh guest; #79 remains blocked by that work.
- General allocation/GC/reference barriers, interrupt service/recovery,
  real ExitBootServices, hosted .NET10/Kestrel, cloud and production readiness.

## Evidence boundary

This is bounded pre-ExitBootServices QEMU/OVMF and NativeAOT evidence on the
recorded tools and images. No adversarial unsupported-SSE entry, nested fault,
post-report sentinel readback, independent general live-memory dump or mapping/
lifetime guarantee is established. Gates3/4 remain **Fail**. The allocation
regressions contain no String guest and do not complete M04-009.
