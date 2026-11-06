using System.Runtime.InteropServices;
using OS.Std.Binary;

namespace OS.Boot;

public readonly struct AcpiBootstrapCopyResult
{
    internal AcpiBootstrapCopyResult(AcpiBootstrapCopyStatus status, ulong requiredBytes = 0, AcpiTableDirectoryStatus directoryStatus = default, AcpiOwnedTableDescriptor rsdp = default, AcpiOwnedTableDescriptor root = default, AcpiOwnedTableDescriptor mcfg = default, AcpiOwnedTableDescriptor madt = default, AcpiOwnedTableDescriptor hpet = default)
    {
        Status = status;
        RequiredBytes = requiredBytes;
        DirectoryStatus = directoryStatus;
        Rsdp = rsdp;
        Root = root;
        Mcfg = mcfg;
        Madt = madt;
        Hpet = hpet;
    }

    public AcpiBootstrapCopyStatus Status
    {
        get;
    }
    public ulong RequiredBytes
    {
        get;
    }
    public AcpiTableDirectoryStatus DirectoryStatus
    {
        get;
    }
    public AcpiOwnedTableDescriptor Rsdp
    {
        get;
    }
    public AcpiOwnedTableDescriptor Root
    {
        get;
    }
    public AcpiOwnedTableDescriptor Mcfg
    {
        get;
    }
    public AcpiOwnedTableDescriptor Madt
    {
        get;
    }
    public AcpiOwnedTableDescriptor Hpet
    {
        get;
    }
    public bool Succeeded => Status == AcpiBootstrapCopyStatus.Success && RequiredBytes != 0;
    public int CopiedBytes => Succeeded ? (int)RequiredBytes : 0;
}
