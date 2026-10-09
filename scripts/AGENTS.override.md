# `scripts` Override

Maintained automation has moved to the .NET 10/C# 14 console applications under
`tools`. Do not add PowerShell wrappers or embedded scripts. Root `AGENTS.md`
and `tools/AGENTS.override.md` define the current commands and evidence rules.

Historical step files retain the names of the scripts used for those captures.
Use `dotnet run --project tools/OS.Tooling -- --help` for the current command
surface. See `docs/efi-build.md`, `docs/qemu.md`, and `docs/host-requirements.md`
for native tool discovery, strict publication, dry runs, and acceptance boundaries.
