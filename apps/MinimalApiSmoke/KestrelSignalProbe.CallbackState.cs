using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Abi = KestrelSignalProbeAbi;

internal sealed partial class KestrelSignalProbe
{
    // This callback state owns no native pointer, descriptor, event or waiter.
    // A late framework-owned callback can retain this managed object safely;
    // unregistering does not promise to join the global .NET signal thread.
    private sealed class CallbackState
    {
        internal int Count, Active, Failed, Tid;
        internal void Handle(PosixSignalContext context)
        {
            context.Cancel = true;
            Interlocked.Increment(ref Active);
            try
            {
                if (context.Signal != (PosixSignal)Abi.Signal || !Thread.CurrentThread.IsThreadPoolThread)
                    Interlocked.Exchange(ref Failed, 1);
                Volatile.Write(ref Tid, Abi.GetTid());
            }
            catch
            {
                Interlocked.Exchange(ref Failed, 1);
            }
            finally
            {
                Interlocked.Increment(ref Count);
                Interlocked.Decrement(ref Active);
            }
        }
    }
}
