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
    private readonly struct OperationOutcome
    {
        public OperationOutcome(bool succeeded, BootPageAllocatorMutationStatus status, ulong physicalStart, BootPageAllocatorOnePageCoalescingKind coalescingKind)
        {
            Succeeded = succeeded;
            Status = status;
            PhysicalStart = physicalStart;
            CoalescingKind = coalescingKind;
        }

        public bool Succeeded
        {
            get;
        }
        public BootPageAllocatorMutationStatus Status
        {
            get;
        }
        public ulong PhysicalStart
        {
            get;
        }
        public BootPageAllocatorOnePageCoalescingKind CoalescingKind
        {
            get;
        }
    }
}
