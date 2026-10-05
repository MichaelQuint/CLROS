# Step 999 - Prepare the allocation-free final-map/exit window

Prepare the host/no-stdlib portion of M03-005 (#62) in a separate worktree
while the prior EBS/GC merge wave runs. #62 remains blocked on #61 acceptance;
this step neither closes it nor changes the running candidate.

`src/OS.Kernel/Boot/NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs` adds
one status-based, single-use window over existing metadata validation and
the typed exit adapter. Admission precedes the final `GetMemoryMap` into an
already-owned buffer. Successful metadata validation leads directly to one
exit attempt using the returned key. No allocator, diagnostic hook, retry or
unrelated firmware entry point is exposed. Reentry is denied during capture
and exit; capture failures and invalid metadata consume the window without
calling exit. Returned results preserve full-width keys/statuses. The map
record describes the owned capture buffer, not a separate copied map.

Other changed files: the matching HostTests source, `docs/boot.md`, and
`docs/efi-build.md`. No shared EFI integration surface is changed.

## Validation

- `dotnet run --project artifacts/issue62-host/Window.csproj --nologo`: exit 0.
  Executes the complete new initializer: seven starts, eight passes including
  temporary-project completion, zero failures. Admission failures, exact callback
  order, zero/full-width keys, arbitrary returned statuses, buffer guards,
  buffer-too-small/device-error returns, six malformed-map shapes, reuse,
  reentry at both callbacks and 100 warmed zero-allocation windows pass.
- `dotnet build artifacts/issue62-host/nostdlib/Closure.csproj --nologo -m:1
  -c Release --disable-build-servers`: exit 0, zero warnings/errors. Seven actual
  sources, zero AssemblyRefs, actual `Execute` IL present. DLL SHA-256:
  `2652B977AC3489DFA2DC74AD5DC4CFC17A8C932D2340F8BB989227D02773B81B`.
- `git diff --check`: pass. Logs, source hashes and metadata are under
  `artifacts/issue62-host/`; generated files are not committed.

No failures in these focused checks. The complete repository suite, source-linked
EFI integration, native call-graph review and QEMU proof are deferred to the
M03-005 integration slice after prerequisite acceptance. No duplicate full suite
is started for this preparatory host change.

## Boundary

Controlled host callbacks and a no-stdlib compilation do not prove execution
in firmware or a real exit. Pointer/buffer authenticity and serialization are
caller obligations; callback exceptions or hostile memory mutation are not
contained. The window validates metadata geometry, not descriptor contents.
No map-buffer growth, implicit retry, freeing, global firmware-mode transition,
post-firmware execution, Kestrel or cloud readiness is implemented.
