# Step 048 - FAT32 Root Long-File-Name Lookup

## What Changed

- Added VFAT long-file-name metadata parsing to `Fat32Parser.FindRootDirectoryEntry` for the first FAT32 root-directory cluster.
- Preserved existing short 8.3 root lookup behavior.
- Added checksum validation from LFN entries to the following short alias.
- Added `Fat32Status.MalformedLongFileNameMetadata` for malformed, incomplete, or orphan LFN sequences.
- Added host tests for `MANIFEST.JSON` root lookup success, missing root long-name lookup, malformed LFN checksum metadata, orphan LFN metadata, unsupported long-name paths, and existing short-name behavior.
- Updated `docs/cstorage.md` with the new evidence boundary.

## Why It Changed

The image builder writes `MANIFEST.JSON` with a VFAT long-file-name entry because the `.JSON` extension is not an 8.3 extension. The storage parser needs a narrow, host-tested root long-name lookup step before later subdirectory traversal and file-content reads can load packaged manifests.

## Files Changed

- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `src/OS.Kernel/Storage/Fat32Status.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `done/step048.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS storage fat32 finds MANIFEST.JSON root long-name file metadata`
- Host test marker: `TEST_PASS storage fat32 reports missing root long-name file`
- Host test marker: `TEST_PASS storage fat32 rejects malformed long-name metadata`
- Host test marker: `TEST_PASS storage fat32 rejects orphan long-name metadata`
- Existing short-name markers still passed:
  - `TEST_PASS storage fat32 reports missing root short-name file`
  - `TEST_PASS storage fat32 finds root short-name file metadata`
- Full host test marker: `TEST_PASS host.tests`

## Failures

- An initial solution build attempt failed with `MSB3027`/`MSB3021` because another process held `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe`. After that process exited, the normal solution build and normal `--no-build` host test run passed.

## Deferred Work

- Add FAT32 subdirectory traversal for `\OS\MANIFEST.JSON`.
- Add FAT chain walking and file-content reads.
- Wire manifest loading only after path traversal and content-read contracts have host tests.

## Evidence Boundary

This step proves host-tested FAT32 root-directory long-file-name lookup only. It does not read file contents, walk FAT chains, traverse `\OS`, run from a real block driver, load manifests in guest, or prove app/package loading.
