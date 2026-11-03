namespace OS.Pal;

using OS.Std.Text;

public readonly struct OSHostExportCatalogEntry
{
    public readonly string Name;
    public readonly OSHostExportDomain Domain;
    public readonly OSHostExportSignature Signature;
    public readonly uint Flags;
    public OSHostExportCatalogEntry(string name, OSHostExportDomain domain, OSHostExportSignature signature, uint flags = 0)
    {
        Name = name;
        Domain = domain;
        Signature = signature;
        Flags = flags;
    }
}
