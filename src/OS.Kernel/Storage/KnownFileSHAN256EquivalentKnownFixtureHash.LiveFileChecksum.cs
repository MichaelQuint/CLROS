using System;
using OS.Std.Hash;

namespace OS.Storage;

public struct LiveFileChecksum
{
    public LiveFileStatus Status;
    public uint ByteCount, BytesRead, CrcState, ExpectedCrc;
    public readonly bool Succeeded => Status == LiveFileStatus.Complete;
}
