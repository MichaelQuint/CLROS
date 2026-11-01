using System;
using OS.Std.Text;

namespace OS.Storage;

public struct LiveManifestResult
{
    public LiveManifestStatus Status;
    public uint ByteCount, AppPathOffset, AppPathLength, AppNameOffset, AppNameLength, ArgsOffset, ArgsLength, ErrorOffset;
    public byte NetworkMode, LogMode;
    public readonly bool Succeeded => Status == LiveManifestStatus.Success;
}
