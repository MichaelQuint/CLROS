// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    public static partial class BoundedJobProcessRunner
    {
        /// <summary>Copied host join facts for a per-request test observer.
        /// These values grant no process or file authority. A control may only
        /// delay an otherwise proven join, never replace any native receipt.</summary>

        public readonly record struct OwnershipJoinObservation(uint ActiveProcesses, uint TotalProcesses, int CapturedProcesses, bool CaptureComplete, bool RootSignaled, bool MembersSignaled, bool Assigned, bool InitialWaitExpired, nint RootHandle, nint FirstDescendantHandle)
        {
            public bool HasCompleteHandleJoin => CaptureComplete && RootSignaled && MembersSignaled && ActiveProcesses == 0 && (!Assigned || TotalProcesses == CapturedProcesses);
        }
    }
}
