# Step 970 - Accept the observed firmware map and owned-copy guest evidence

## What changed and why

Complete the common host and actual guest acceptance for M03-001/#58 and its
separately owned native supplement #532. The disabled-by-default profile observes
the original firmware query, LoaderData allocation, recapture and existing owned
copy. It forwards to retained original firmware callbacks without replacing the
table or adding another capture algorithm. Default behavior remains compatible.

The final acceptance uses clean source commit
`854a3359623f6a619b0ea769ce97cfed6c3a96b1`, tree
`733f769ae3be35f2d8621157e933b7edd2c02063`. Steps 968/969 retain the source changes,
earlier static review and failed common-run repair. This step changes only
documentation and catalog intent; no executable source or test input changes.

## Files changed

- `docs/boot.md`, `docs/efi-build.md`, `docs/qemu.md`: current capture, publication
  and QEMU evidence with the remaining firmware boundary.
- `docs/completion-gates.md`, `docs/nativeaot-nostdlib-limits.md`: accept this
  bounded slice and the common host gate without promoting later runtime gates.
- `.gitea/backlog/clros-issues.json`: M03-001 completion intent, its tracking
  epic's evidence and M04-010's common host result; later dependencies remain open.
- `done/step970.md`: reserved using `scripts/new-done-step.ps1 -Reserve` before
  creating this milestone.

## Tests run

Actual commands on 2026-10-04 UTC:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -BuildEfi -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
.\scripts\run-qemu-smoke.ps1 -BuildEfi -EnableM03MemoryMapCaptureProbe -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

The two real smoke invocations additionally used unique explicit EFI, raw-image,
serial and summary output paths recorded by the capture wrapper. No marker-only
override or additional guest profile was used. Each invoked the real EFI builder.

The solution build ran 03:27:04.904795-03:27:32.704432, native exit 0, zero
warnings/errors. The complete host suite ran 03:28:40.159899-05:06:52.869257,
native exit 0, empty stderr: **3670 START, 3671 PASS, zero FAIL**, one terminal
`TEST_PASS host.tests` and all 16 selected READY records. Independent review
checked the complete marker multiset/order, all 905 committed source/config
records and all five unchanged launch DLLs with the correct ProductVersion.
This includes every changed compatibility initializer and its artifact branches,
the original memory-map regression modules, and the M04-010 checked-store host
leaf. It does not prove returning native reference barriers.

The default capture lane ran 05:14:30.5578307-05:15:53.8449469; the memory-map lane ran
05:15:55.1506911-05:17:05.7623871. Both wrappers exited0, emitted `OS_BOOT_OK` and
`OS_SMOKE_PASS`, and retained complete serial/summary streams. The runner
terminated QEMU (recorded exit -1); its ordinary quiet interval was configured at
750ms and was not separately measured. Full firmware framing/tails were reviewed.
The final dry-run ran 05:24:41.803700-05:24:43.702262, native exit 0/empty stderr;
it adds configuration evidence only. Step 969 records the passing naming/source
firewall, focused verifier and catalog checks. Final catalog `-Offline` and
`-SelfTest` commands passed at 05:52:26-05:52:36 UTC, native exit 0/empty stderr;
the policy self-test made zero API calls. The final documentation/catalog diff
check passed, with no executable source or generated artifact added. These
records are joined in this step's transaction folder; no blanket Apply was run.

## Logs and evidence

Ignored root `artifacts/orchestration-20261001/` holds original commands, raw
streams, hashes and review results; generated artifacts are not committed.

- `issue532-common-gate/build-854a335/result.json`: SHA256
  `4D8D02484149D0D2B23FBF4F70A7F0924D061417270E75868D47F8F141011C9F`.
- `issue532-common-gate/run-854a335/result.json`: SHA256
  `F1465CCE758A31598B17E83610A1A6FE9D788CF001C6E29C600ECAF244AD59C3`.
  Full 473108-byte stdout:
  `CA08CBB445808989BBC8FCB70BD24D8A463CED1BFE203AF6D562372B2B275AFA`.
- `issue532-common-gate-independent-review/completed/final-join.json`:
  `19CD803132A17E38A01EDF6F92D6D36A29F65E7FD39EC062AD4AB9D984ECD5E5`.
- `issue532-guest-854a335-20261004T0514140533752Z/capture-result.json`:
  `C0322CFE8C8966D87C0DA1703636046A4E58A1D5E340D082CBEBF91F7DAF1294`.
  Each lane retains 591 artifact records, including the 470-file ImageBuilder
  closure with 452 source inputs, actual profile/source records, managed image,
  object, maps, responses, EFI image and complete disk/serial outputs.
- `issue532-actual-review-854a335/ordinary-result.json`: both complete raw
  streams and lifecycle records pass. The memory-map review replays the exact
  archived production contract and checks its 31-record inventory; this replay
  is not an independent numeric oracle. Parent reviewed the actual observations.
- `issue532-actual-review-854a335/actual-review-results.json`: independent
  GPT/FAT/payload and complete changed-byte accounting passed 14826 checks with
  1679 bound inputs. Eight reconstruction/decoder/audit commands passed.
- `issue532-final-transaction/`: final parent acceptance, native review binding,
  documentation/catalog checks, Git and Gitea transaction/readback records.
  Parent joint acceptance SHA256:
  `23CD43AC54AADC80244B2DECE6CBCBD0296E8D794F620AA5C5BE432EB5182FA4`.
- `issue532-captured-native-independent-review-854a335/review.md`:
  `1480B90439BF0CEA204C28FF21F41AFF5F6D472BEDF2C30F1E2D2CBF489D4A43`.
  Parent read the complete report and rehashed 40 outputs/1500 input records.

| Profile | EFI SHA256 | Complete serial SHA256 |
| --- | --- | --- |
| Default | `FDECE5DD4DBB61C63442099DCB1E7AEBD9EA4A3D6903C631A74C3F7134E9A703` | `0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1` |
| Memory-map | `9C5601226E68F4458A360EDAB02AB74E336B8953DAE671F45A372ED47BB1C92A` | `4E96B30B988932FE940A6ACB3AC58213B4D487CE70B01CAE12DC8E5F9E4D9467` |

Both full EFI images match the independently reviewed v8 images byte for byte.
Fresh managed images have zero AssemblyRefs. Fresh source/managed/COFF/map/PE
association checks bind 74/91 selected managed bodies, 112/124 complete native
methods and 57/65 REL32 targets to those actual captures. Only COFF debug data
differs; executable sections and relocations agree. The callback review covers
full-width arguments/results, x64 homespace/alignment, GetMemoryMap's fifth
pointer, original target provenance and complete copy/guard/tail scans. Actual
observer/guard/payload addresses share the same signed image-load delta.

The enabled capture observed query capacity 0/null buffer, required 5184 bytes,
stride 48, key0x4FE; one LoaderData 2 allocation of 5568 bytes; one successful
recapture of 4896 bytes, stride 48/version 1, 102 descriptors, key0x4FF. All 4896
copied bytes matched; all 32 guard bytes and 126176 remaining payload bytes
matched. Snapshot/handoff keys both matched0x4FF. The recorded READY explicitly
states `firmwareAvailable=true exitBootServices=false`. Real descriptor growth
was not observed or manufactured; controlled host tests cover that failure case.

The booted disks differ from their independently reconstructed pristine images.
Each has exactly 887 changed bytes in 341 ranges across seven sectors, fully
accounted for by primary FSInfo, both FAT copies, the root entry and a 1393-byte
three-cluster NVVARS file. Backup FSInfo retains pristine hints. This is bounded
image/payload/change accounting, not whole-filesystem conformance or equality of
pristine and booted images.

## Failures

No failure occurred in the accepted common build/full suite, fresh capture,
raw/lifecycle or image-accounting commands. Earlier focused attempts and the
stopped 1c37aa1 common run remain preserved in steps 968/969 and their original
archives. In particular, the original 92-property verifier fixture was repaired
to include the required 93rd property; no verifier assertion was weakened.
Reviewer-only path/schema/EOL readback corrections are retained with their
original failed records and changed no product source or acceptance predicate.

## Deferred work and evidence boundary

This accepts the original map capture and bounded owned-copy observation while
firmware remains available. It proves no fresh key for ExitBootServices, actual
ExitBootServices call/retry, post-firmware ownership or execution, live ACPI
copies, collecting GC, general runtime, hosted .NET 10/Kestrel, cloud readiness or
production support. Completion gates 3/4/5 remain unsatisfied.

After reviewed merge and evidence-qualified closure of #58/#532, #59 owns the
next ACPI-copy slice. #79 remains open for the returning checked-reference
bridge; #536/#537 are its isolated tooling/native work, and #80 remains blocked
until #79's actual acceptance. No future issue is closed by inference.
