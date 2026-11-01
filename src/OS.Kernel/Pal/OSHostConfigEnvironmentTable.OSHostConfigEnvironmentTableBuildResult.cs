using OS.Manifest;
using OS.Std;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Pal;
public readonly struct OSHostConfigEnvironmentTableBuildResult
{
    public OSHostConfigEnvironmentTableBuildResult(OSHostStatus status, OSHostConfigEnvironmentTable? table)
    {
        Status = status;
        Table = table;
    }

    public OSHostStatus Status { get; }
    public OSHostConfigEnvironmentTable? Table { get; }
    public bool Succeeded => Status == OSHostStatus.Ok && Table is not null;

    public static OSHostConfigEnvironmentTableBuildResult FromStatus(OSHostStatus status)
    {
        return new OSHostConfigEnvironmentTableBuildResult(status, null);
    }
}
