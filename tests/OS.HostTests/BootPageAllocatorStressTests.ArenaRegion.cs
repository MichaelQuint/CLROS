using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorStressTests
{
    private readonly struct ArenaRegion
    {
        public ArenaRegion(int startPage, int pageCount, int sourceFreeSegmentIndex, int sourceUsableRangeIndex)
        {
            StartPage = startPage;
            PageCount = pageCount;
            SourceFreeSegmentIndex = sourceFreeSegmentIndex;
            SourceUsableRangeIndex = sourceUsableRangeIndex;
        }

        public int StartPage { get; }
        public int PageCount { get; }
        public int SourceFreeSegmentIndex { get; }
        public int SourceUsableRangeIndex { get; }
    }
}
