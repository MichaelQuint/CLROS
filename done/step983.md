# Step 983 - Validate one-module TypeManager metadata on the host

## What changed

`TypeManagerModuleMetadataLiveNativeAOTImage` now validates one loaded
PE/.modules/RTR image against its original M01 capture and typed identity map,
registers a separate TypeManager ABI record once, and returns checked section
addresses and lengths. The finite decoder admits the pinned .NET 10.0.8
RTR 16.0 format. All methods use POD status/results and caller-owned storage.

Registration checks the full storage capacity, all 16 writable slot bytes,
permissions, spans and aliases before writes. It retains an exact metadata
snapshot, fills the 48-byte ABI and index 0, then publishes the handle last.
Lookup rechecks the capture, owner, capacity, ABI, slot and original metadata;
even a structurally valid in-bounds row replacement fails. Present empty
sections differ from absent sections. Row payloads remain opaque.

## Why

PR540 merged at `f3b4c8310975186a566f04864c378000fa194cad` and issues #79/#537
closed with reviewed evidence, releasing #80. A host-tested decoder/owner lets
the later shared EFI adapter reuse one registration algorithm. This checkpoint
does not satisfy #80's live-image/QEMU acceptance requirement.

## Files changed

- `src/OS.Kernel/Runtime/TypeManagerModuleMetadataLiveNativeAOTImage.cs`
- `tests/OS.HostTests/TypeManagerModuleMetadataLiveNativeAOTImageTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `.gitea/backlog/clros-issues.json`: four fields across M04-EPIC, M04-010 and
  M04-011 reconcile actual dependency closure and the new host-only evidence.
- `done/step983.md`

No shared EFI, runtime export, boot sequence or probe file changed.

## Tests run

The actual serial attempt ran on the `f3b4c83`-based worktree on 2026-10-04,
18:07:19.941437–18:07:57.949076 UTC:

1. `dotnet build artifacts/issue80-type-manager/focused/Focused.csproj --nologo
   -c Release -m:1 --disable-build-servers -p:UseSharedCompilation=false`.
2. `dotnet artifacts/issue80-type-manager/focused/bin/Release/net10.0/Focused.dll`.
3. `dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -c Release -m:1
   --disable-build-servers -p:UseSharedCompilation=false`.

All commands returned native0 with empty stderr; both builds reported zero
warnings/errors. The focused harness invokes all five complete original
initializers: new TypeManager 15 groups, loaded-image capture 17, typed
addresses 19, M01 loaded-image stack 25, and helper ledger 5. Total 81 TEST_START,
82 TEST_PASS (including one `host.tests` terminal), zero TEST_FAIL. It does not
replace or trim those initializers.

The new tests cover malformed headers/rows/lengths, truncated image authority,
permissions/overlaps, module padding, unsupported formats, storage and slot
aliases, prewrite rejection preservation, repeat/foreign handles, owner/ABI/
slot/snapshot tampering, valid metadata replacement, absent/empty sections,
protected buffer tails/canaries and allocation-free validation/lookup loops.

Observed host sizes are ABI 48, private state 272, result 40 and slot 16. The test
also actually builds the 88 current explicit EFI sources plus the new core as
an 89-source managed no-stdlib library and verifies zero AssemblyRef rows.
The child uses C# 14/.NET 10 and the existing no-runtime shim. It is not a
NativeAOT publication. Its DLL SHA-256 is
`B0BFF9292EBD09E58DCDD97BA5A59D6EBFED103E0B523B9B3E004CABDC269F0B`.

## Logs and evidence

Original logs remain under the isolated worktree's
`artifacts/issue80-type-manager/attempt2`; exact sources and commands are frozen
in `source-checkpoint1`, and the summary is in `final`. The 926 input records
match before/after and their archived source bytes; all 9 launch records and
archived binaries are also preserved. Docs/catalog/done are excluded from
this product-input inventory; the parent updated them after the actual run.

| Evidence | SHA-256 |
| --- | --- |
| Core source | `7DEE5F032CF4DCA09358DFA8A36D9772B12140ED733E02190AACC2A50D7EB006` |
| Test source | `269CB87E31DC33F57F83779F85180A64A4792C2D7D7006D4FA88995D4C2D73D7` |
| Attempt2 result | `27F05E44A04B6652520C6FF88D2B07A8FB2B9835772D564DA5483BC798DD70DE` |
| Focused stdout | `0FF169369266E20E6D318D9745578F9FC5F121ADC8E1EAC38AF39ACB92DE19E4` |
| No-stdlib child result | `6C4263C21BB6734E5B9244B541315F0309322BDBA0F33B2F35AE77FF7FBF1DCA` |
| Final author report | `7E56885997DDC597C8E4409491B40A5C1BC853CAAE433644EF8C4A4760221389` |
| Final actual-results | `D46383B68284FAFA40A6ABC7357C9B6E897431D4AE3C7B685E98E0F92473CB4A` |
| Final 1923-row evidence inventory | `F1E6AAEE09594065B85AC1F088C9439652AA30B88F749F5E1DE3C0BDDD289E0D` |

The repo allocator reserved step983 at 18:10:20UTC using a coordination view
containing existing steps 980–982; its recorded command returned native0.

## Parent review and final repository checks

Independent review of the exact source and completed evidence found no
actionable issue. Its report is retained at
`artifacts/orchestration-20261001/issue80-host-independent-review-f3b4c83/review.md`
in the orchestration checkout, SHA-256
`D5633CEC47D851CC8AD6ECC19486C39067EEBDC2E2287CECAA7D5D7CE0F6BCF2`.
The parent separately read the complete source, test module and evidence.

The existing backlog script passed `-Offline` validation of all 345 catalog
rows and `-SelfTest` (28 policy cases, zero actual API calls), both native 0
with empty stderr. The unchanged `check-naming.ps1` passed both naming and
source-firewall groups, native 0/empty stderr, ending 18:16:51.167699 UTC.
`git diff --check` passed. These checks change no completion gate.

## Failures

Attempt1 failed compilation with CS0103 because the ignored focused harness
omitted the existing `BuildEfiShellPreflightTests` source used by the complete
M01 module. The repair links that unchanged helper source, leaving its
initializer inert through the existing harness attribute alias. The five
complete selected initializers and both product files are unchanged. The
original failed result remains at `attempt1/result.json`, SHA-256
`4CF54FAB817E17F86B773F5940494C113E38BF8672AFD76BE83B4B1A55347F23`.

The parent's first allocator invocation was rejected by the shell's script
execution policy before reservation. The recorded standard PowerShell command
with ExecutionPolicy Bypass ran the unchanged repo helper successfully.

## Deferred work and evidence boundary

#80 remains open/claimed. Its separately owned EFI supplement must connect the
actual original loaded-image capture, discover the current relocated module,
register it and exercise a real consumer. Fresh native, complete common-suite
and real QEMU/OVMF evidence are still required. No old artifact RVA may stand
in for actual discovery. #81 root enumeration and #82 collection remain blocked.

This contract is exclusive-access host-controlled mapped-memory evidence.
Actual memory mapping and lifetime are caller obligations; the retained
snapshot is not authentication against coordinated authority corruption.
There is no upstream multi-module registry, class-library consumer, GC/static
initialization, TLS, cctor execution, collector, concurrency or hardware-fault
rollback. `TypeManagerRtrScans` remains Deferred. Gates3/4, real ExitBootServices,
hosted .NET 10, Minimal API/Kestrel and cloud readiness remain incomplete.
