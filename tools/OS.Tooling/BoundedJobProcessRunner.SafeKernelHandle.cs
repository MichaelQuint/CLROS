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
        private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeKernelHandle() : base(true)
            {
            }

            internal SafeKernelHandle(IntPtr value) : base(true)
            {
                SetHandle(value);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }
    }
}
