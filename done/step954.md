# Step 954 - Accept shared-page SZ-array guest evidence and repository scan repair

## What changed and why

The M04-008/#77 host leaf and #523 default-off EFI adapter from step952 now have
accepted complete host and real QEMU/OVMF evidence. Genuine byte/long arrays
share the original retained4KiB allocation page and cursor with fixed objects.
The mixed case returns five objects using160 bytes; exhaustion reaches4096 bytes
and169 objects, then rejects the next allocation. Before-bind rejects without
allocating. Complete state/heap rejection readbacks, prior-object retention,
length/index/store observations and canaries are independently reviewed.

Step953's #525 directory-pruning correction and explicit default-probe fixture
repair pass the integrated gate. This evidence-only transaction updates current
status and catalog closed intent for M04-008. PR524 merge and live closure of
#77/#523/#525 are subsequent verified tracker actions. #56/#526/#78 stay open.

## Source and files changed

Tested commit: `1ce1889bab1bc9307b3904becdc66cb3d12078d0`.
Tested tree: `a7822f33d61790514d1433cecd3d60aa40b6512e`.
The checkpoint was tracked-clean for build, complete host, all eight captures,
disk accounting and joint acceptance. This later documentation transaction does
not replace that tested source identity. Historical steps952/953 are unchanged.

Changed paths: `README.md`, `docs/nativeaot-nostdlib-limits.md`,
`docs/efi-build.md`, `docs/qemu.md`, `docs/boot-order.md`,
`docs/completion-gates.md`, `docs/repo-audit.md`, `docs/diagnostics.md`,
`.gitea/backlog/clros-issues.json`, and this step. The catalog preserves345 rows,
all dependencies and original ownership text; only four M04-008 fields and one
M02-016 host-evidence field change. The compatibility ledger has45 declarations
and20 exports; the two appended SZ-array exports do not complete Gate4.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`: native0, zero warnings/errors,
  empty stderr, 2026-10-03 04:33:37.0345613–04:34:01.3905201Z.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  native0,3611 START/3612 PASS/zero FAIL/one final host.tests, empty stderr,
  04:37:26.9000586–05:57:24.2008272Z. Strict checker then exited0 at
  05:58:07.0465072Z; all504 source inputs,31 modules,257 required pairs and ten
  readiness markers match. All three launch/end DLL identities are unchanged.
- Eight fresh source-bound captures: native0,06:00:31.3016199–06:10:14.6337945Z.
  Profile vectors have31 properties with0/7/8/9/10/10/9/9 true; each archive
  binds80 Compile items,93 raw inputs and zero-AssemblyRef OS metadata.
- Disk reconstruction/accounting: native0,06:12:33.7554467–06:13:08.4461892Z;
  all32 recorded commands pass. Independent disk audit native0/empty stderr,
  67,256 checks and5,267 input identities. No audit launches another guest.
- Native/metadata and full raw/source/lifecycle reviews pass. Parent read all
  reports and rehashed280 native-review files/4922 inputs,66 raw-review files,
  12 disk-review files/5267 inputs:5750 distinct bound files in joint acceptance.

The final documentation/catalog transaction preserves all503 non-catalog
inputs from that accepted504-source inventory. The complete unchanged actual
manifest initializer ran through `ManifestFocused.dll`: native0, exact17 START
names/18 PASS names/zero failures/one final manifest.focused, empty stderr,
06:31:45.562378–06:32:18.479342Z. All21 before/after input identities agree;
the final catalog SHA256 is
`BB15F1847F8B1CF871E5C975DF03525FF72C5E8201E8C49BADB4817E3FF625CF`.
Result `issue523-final-doc-checks/attempt1/result.json` SHA256 is
`6A6D4F680AADE15AD7A29393FC275EAE889F2DB0DE83C42ADDD6A11F23B0AC20`.
Final `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check-naming.ps1`
passes naming.guard/source.firewall at06:32:23.770458Z, native0/empty stderr;
`git diff --check` then passes native0. Their commands and logs are retained in
`issue523-final-docs/checks-result.json`. Only this check-results paragraph was
filled after the focused run; no catalog, product or test input changed.

## Commands and evidence

Exact parent capture and disk commands, with arguments taken from saved native
invocation records (working directory `C:\Projects\CLROS`):

```powershell
& 'C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe' '-NoProfile' '-ExecutionPolicy' 'Bypass' '-File' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue523-guest-wrapper\retry-source-freeze\Run-Issue523GuestCapture.ps1' '-ExpectedHead' '1ce1889bab1bc9307b3904becdc66cb3d12078d0' '-HostResultPath' 'C:\Projects\CLROS\artifacts\orchestration-20261001\runtime-next-wave-1ce1889\result.json' '-AcceptedHostResultSha256' '0E40B00C9582A70AB0C2CDA260709927188CDA15010491A626A6F8A337384FA2' '-AcceptedHelperManifestSha256' '9DC691B5C7379CFB3A370C577336FB41E9ABAAC08E6AEBCFEF7A8E967F230CF3'
& 'C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe' '-NoProfile' '-ExecutionPolicy' 'Bypass' '-File' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue523-eight-lane-disk-preparation\Run-Issue523ImageAccounting.ps1' '-MatrixPath' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue523-guest-1ce1889-20261003T0600314911091Z' '-ExpectedHead' '1ce1889bab1bc9307b3904becdc66cb3d12078d0' '-AcceptedCaptureSha256' '8DFEC1651AC46333D424FA63BAB87EF947887775F241D78E1115A104E80A76D6' '-ExecutionAuthorized'
```

Per-lane capture.json and command/result files preserve each actual build,
runner or observer command, timestamps, tool hashes, native exits and images.
All artifacts below are under `artifacts/orchestration-20261001/` and remain
generated local evidence rather than committed binaries.

| Evidence | Relative file | SHA256 |
| --- | --- | --- |
| Solution build | `issue523-build-1ce1889/result.json` | `739E2033CAEDF23EC8F6662A92479AB9CEA86DC801BC24AD11CF822BD8B25E1C` |
| Complete host | `runtime-next-wave-1ce1889/result.json` | `0E40B00C9582A70AB0C2CDA260709927188CDA15010491A626A6F8A337384FA2` |
| Strict host/source/binary readback | `runtime-next-wave-1ce1889/gate-readback.json` | `A1AFC107F21E0A6031D40A85B5770FA0B28D2535AADBF18DD863F49A995250D0` |
| Independent completed host review | `issue523-completed-host-independent-review/review.md` | `09CCF89EEC779C4ED3213215571E0836F0EA93A2CB77DF9EFC42D54E4982FB3F` |
| Eight-lane capture | `issue523-guest-1ce1889-20261003T0600314911091Z/capture-result.json` | `8DFEC1651AC46333D424FA63BAB87EF947887775F241D78E1115A104E80A76D6` |
| Native/metadata/prefix/current-state review | `issue523-captured-native-review-1ce1889/review.md` | `B225CA343F52B58F817FAB04C29B1BFBB85067E08BDBAED4D0E6E5E1821B0D48` |
| Complete raw/lifecycle/source review | `issue523-captured-raw-review-1ce1889/review.md` | `81B907C4BC227E3F98B2C3660954C77D99661EC3B32425E7023A506F6D97E91D` |
| Disk recorder | `issue523-pristine-1ce1889-20261003T0612342246443Z/result.json` | `E343253D7B1C4DBF58376325304706C4D716E5824E13DB8C8402C5BCEAF4BD35` |
| Independent disk review | `issue523-captured-disk-independent-review-1ce1889/review.md` | `7F217B9C4C02DBB798311320C2BF47487AE21C5EDFB6907736D2785C860EC482` |
| Disk checks and5267 inputs | `issue523-captured-disk-independent-review-1ce1889/actual-review-results.json` | `90EC50B76BE9369E622E780BC04E9B898D18CFB2995339C996E840547ECC2047` |
| Parent joint acceptance | `issue523-joint-acceptance/acceptance.json` | `2AD5F0BA384FB39688E1923244ED076688FC2C28FCE7B5442EE96EE7621F932F` |

| Lane | CLROS records | Actual EFI SHA256 | Full raw serial SHA256 |
| --- | ---: | --- | --- |
| default | 21 | `387E1BA7F8CC22E9C869C795353D90F7B75A1FDFBCB789A0353D04C7050D4352` | `0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1` |
| heap | 52 | `08208500000F2F15A07A9ADEC6CD38DB3EE51FA530EA7C1889725C98B6ED368F` | `F81E9F6482471E9515840B82992194E86F296521DCC45D089A92A76921B443C7` |
| fixed | 78 | `7F859390CFFDADEA3EC9DDAD558337C3EAA6B0B02AE4D1239857226E0A9EA3B6` | `687C59C14AEB5209A4F5074103C9402E5486B73C03D76ABA7CFC4C4BB53A819D` |
| mixedarray | 68 | `FA59C09D7F827A1208C901D91629C2CCBF316C677DF6F2FDE3563A6884B3D41E` | `BEA15E6276F4B16F4E0C1A1075763BCCF87E5AF8E09B49C78176A8C0B30EEBA1` |
| array-beforebind | 49 | `A721A39E8DB8597B4DE85580F1D767AB008317F0174FFF5A989FFB272FD13A54` | `84FDEE8390807CE9158141D03CBD78265576FA12D428F4FFAE4188C8A5AD4304` |
| array-exhaustion | 61 | `9EA3C5E976B2D19A96F4704CAEA8DDEFCB679B3B74578DA749D1301F7187C0FD` | `11177CC10B86A945CDBBC850D3ACFCF0A02D7D30E21BAC4054CCB8F4A2C271F9` |
| pagefault | 86 | `624D9210FE52FC2B53AC34D9AEF90D23F72AAE55A32CC862778D96797B46DA77` | `6DA5612165BA04FEF0D0039DD590F16B50587729299BBA1B013AD38038480D5E` |
| doublefault | 88 | `BEC1C52670C1C0E10E19136C0A07D9442E3815E7E872FACF00E567E78BB3D46E` | `11210FD87B9FCFDD9A1F3173DE05A80AD7D4B9F56FBEDA7E50CC73ED1FD02DDB` |

## Review findings and limits

All24 export/profile cases pass. Fresh DLL/COFF inspection binds85 selected
array code blocks and18 metadata prefixes; static reuse is restricted to exact
equal PE/map bytes. Another130 method/serializer bodies bind the other profiles.
Actual relocation joins prove22 observed full24-byte prefixes: ten fixed and
six each in mixed/exhaustion. Before-bind supplies two attempted tokens and
zero observed prefixes. PE section flags alone are not hardware permissions.

Six ordinary profiles pass exact required/ordered/unique/forbidden inventories,
summary counters and a750ms quiet drain followed by owned QEMU termination.
The two negative profiles are still ordinary-runner failures: each exact
TEST_FAIL/CRLF/EOF is accepted only through the independent observer, with
measured750.2553/754.6722ms quiet intervals, no timeout, spontaneous exit or
cleanup failure. All eight observed QEMU exits are-1 from owned termination;
native0 above refers to successful capture/verification commands. The default
firmware-return suffix is allowed:6947 bytes including the pass-line CRLF,
6945 firmware-only bytes. No suffix is admitted after terminal profiles.

Fresh PF/DF managed/native/current-state associations pass. Saved DF state is
diagnostic-only, and no independent live generated C/D/T memory dump is claimed.
Each64MiB disk preserves its EFI payload and adds only1393-byte NVVARS in three
clusters; allowed changes are primary FSInfo, two FATs, root and NVVARS clusters.
Both negative pristine images exactly equal independent reconstructions.
Backup FSInfo remains pristine; advisory hints and these finite checks do not
establish whole-filesystem conformance.

## Failures preserved

The earlier b5376c0 complete host run failed at the old25-property double-fault
fixture after three new false-default array flags made the actual count28.
Its native exit,1377 START/1375 PASS/one FAIL,725-byte stderr and lack of final
host marker remain recorded. Step953 repairs only the two explicit expectations;
the fresh1ce1889 complete run supplies acceptance.

The first capture launcher exited1 before any lane because its Start-Process
PowerShell environment could not resolve Get-FileHash. Direct native Windows
PowerShell invocation with unchanged helpers/arguments passed all eight lanes.
Review-local schema/field-name mistakes and their corrected attempts remain in
the independent packages; none changed product or capture bytes. Initial step
reservation was blocked by process execution policy before writing; invoking
the same allocator with process-local ExecutionPolicy Bypass reserved954.

## Deferred work and evidence boundary

This is finite pre-EBS array/allocation and regression evidence. It establishes
neither arbitrary arrays nor managed reference stores/barriers, GC/roots,
covariance/casts/exceptions, string allocation, boxing-copy, growth/refill,
general runtime, real ExitBootServices, live drivers/networking, hosted .NET10,
Kestrel, cloud or production readiness. Gates3/4 remain **Fail** and dangerous
probes default false. Original capture/review provisional flags are preserved;
the separately recorded parent joint acceptance supplies the combined decision.

The integrated M02-016 C2/D6 host module passed, but its accepted later C2/D7
candidate is unintegrated and not native stack-safety or unexpected-vector guest
proof. After actual PR524 merge and prerequisite readback, the next disjoint
lanes are #78's four-file host string slice and #56/#526's36-file native panic
integration. Final native/host/guest verification must use their combined frozen
source; #79 remains blocked on #78.
