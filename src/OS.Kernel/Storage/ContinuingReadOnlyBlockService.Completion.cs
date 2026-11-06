using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Validated observation, without an output pointer. Ready remains
    /// pending consumption; a cancelled completion reports zero bytes.</summary>
    public readonly struct Completion
    {
        public readonly ReadToken Token;
        public readonly ulong Sector;
        public readonly uint Bytes;
        internal Completion(ReadToken token, ulong sector, uint bytes)
        {
            Token = token;
            Sector = sector;
            Bytes = bytes;
        }
    }
}
