# Step 998 - Include GC-static conflicts in existing diagnostic profile checks

The complete combined run at `58410d1ac1bd1ef72656cb84927260fcf0a2ef53`
passed the corrected output-authority initializer, then failed the M02-004
compile-closure case. Five older diagnostic fixtures omitted the new GC-static
conflict from their exact lists. The product already rejects these combinations.

Update the COM1 backend, COM1 ready, panic-safe COM1, GDT and guarded-IST EFI
integration tests together: add the exact GC-static conflict and increment
each corresponding count. All previous constraints remain required. No product,
native source, build profile or publisher input changes.

Validation: a temporary project includes the five original source files and
executes their complete module initializers unchanged. `dotnet run --project
artifacts/issue548-dependency-compat/Compatibility.csproj --nologo` exits 0:
27 starts, 28 passes including the final completion marker, zero failures.
The source audit compares every listed dependency against actual project XML;
each fixture was missing exactly one GC-static constraint. The memory-map
fixture's 41 conflicts correctly exclude its own flag and need no change.
`git diff --check` passes. Diagnostic artifacts remain untracked under
`artifacts/issue548-dependency-compat/` (`source-audit.json`, `run.log`).

Preserved full-run failure: `artifacts/issue548-efi/common-after-authority-run/`,
09:05:24..09:57:34 UTC on 2026-10-05, exit 3762504530. Sources and Debug launch
binaries were unchanged. Stdout SHA-256:
`D34D9C3CDE71C2567A1D165E3688569460D0EA7B675016296F99F23733564145`.

This focused pass does not replace complete combined acceptance. Next: one
corrected full original suite, then final default, typed-EBS and GC-static QEMU
captures. Existing native review remains reusable only after input identity
verification. Issues #61/#546/#81/#548 stay open; #62/#82 remain blocked.
No real ExitBootServices, collecting GC, hosted .NET/Kestrel or cloud claim.
