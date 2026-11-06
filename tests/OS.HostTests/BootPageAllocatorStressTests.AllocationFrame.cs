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
    private sealed class AllocationFrame
    {
        public AllocationFrame(BootPageAllocatorOnePageResult allocation, int pageIndex, ulong contentTag, byte[] nodeBytesBefore, byte[] reservationBytesBefore)
        {
            Allocation = allocation;
            PageIndex = pageIndex;
            ContentTag = contentTag;
            NodeBytesBefore = nodeBytesBefore;
            ReservationBytesBefore = reservationBytesBefore;
        }

        public BootPageAllocatorOnePageResult Allocation
        {
            get;
        }
        public int PageIndex
        {
            get;
        }
        public ulong ContentTag
        {
            get;
        }
        public byte[] NodeBytesBefore
        {
            get;
        }
        public byte[] ReservationBytesBefore
        {
            get;
        }
    }
}
