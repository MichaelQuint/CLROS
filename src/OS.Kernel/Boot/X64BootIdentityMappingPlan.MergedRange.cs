namespace OS.Kernel.Boot;

public static unsafe partial class X64BootIdentityMappingPlan
{
    private readonly struct MergedRange
    {
        public MergedRange(ulong start, ulong endExclusive, ulong pageCount, ulong byteLength, ulong purposeMask, OS.Kernel.Memory.PageProtection protection, OS.Kernel.Memory.PageCachePolicy cachePolicy)
        {
            Start = start;
            EndExclusive = endExclusive;
            PageCount = pageCount;
            ByteLength = byteLength;
            PurposeMask = purposeMask;
            Protection = protection;
            CachePolicy = cachePolicy;
        }

        public ulong Start
        {
            get;
        }
        public ulong EndExclusive
        {
            get;
        }
        public ulong PageCount
        {
            get;
        }
        public ulong ByteLength
        {
            get;
        }
        public ulong PurposeMask
        {
            get;
        }
        public OS.Kernel.Memory.PageProtection Protection
        {
            get;
        }
        public OS.Kernel.Memory.PageCachePolicy CachePolicy
        {
            get;
        }
    }
}
