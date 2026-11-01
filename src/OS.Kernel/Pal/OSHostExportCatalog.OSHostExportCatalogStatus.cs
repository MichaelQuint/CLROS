namespace OS.Pal;
using OS.Std.Text;

public enum OSHostExportCatalogStatus : int
{
    Success = 0,
    EmptyCatalog = 1,
    InvalidName = 2,
    DuplicateName = 3,
    InvalidDomain = 4,
    InvalidSignature = 5
}
