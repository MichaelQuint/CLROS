# Shared no-runtime host project

This project exposes the existing shared source alongside OS and OS.Kernel.
Implementation stays under `std/no-runtime/shared`; its local guide applies.
The EFI build source-links an audited subset from those stable paths.
Do not add a duplicate implementation here or let normal framework references
from this host-validation project enter the no-stdlib EFI project.
