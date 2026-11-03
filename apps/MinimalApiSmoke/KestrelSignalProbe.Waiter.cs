using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Abi = KestrelSignalProbeAbi;

internal sealed partial class KestrelSignalProbe
{
    private sealed class Waiter(int epoll, int read, bool competing, EventLifetime? lifetime) : IDisposable
    {
        internal readonly ManualResetEventSlim Release = new(false);
        internal readonly ManualResetEventSlim BeginRecovery = new(false);
        internal int Phase, Tid, AbortRecovery;
        internal ulong MaskBefore, MaskDuring, MaskAfter;
        internal Abi.AltStack StackBefore, StackAfter;
        internal Abi.WaitResult Result, Recovery;
        internal byte RecoveredByte;
        internal Exception? Error;
        internal void Run()
        {
            bool maskOwned = false;
            try
            {
                Tid = Abi.GetTid();
                MaskBefore = Abi.ReadMask();
                StackBefore = Abi.ReadAltStack();
                Abi.SetMask(MaskBefore & ~SignalBit);
                maskOwned = true;
                MaskDuring = Abi.ReadMask();
                Need(MaskDuring == (MaskBefore & ~SignalBit), "waiter_mask");
                Volatile.Write(ref Phase, 1);
                Result = Abi.Wait(epoll, lifetime is null ? 2500 : -1);
                Volatile.Write(ref Phase, 2);
                if (competing)
                {
                    if (lifetime is null)
                    {
                        if (!BeginRecovery.Wait(TimeSpan.FromSeconds(15)))
                            Fatal("recovery_release");
                    }
                    else
                        lifetime.Wait(BeginRecovery);
                    if (Volatile.Read(ref AbortRecovery) == 0)
                    {
                        Volatile.Write(ref Phase, 3);
                        Recovery = Abi.Wait(epoll, lifetime is null ? 2500 : -1);
                        if (Volatile.Read(ref AbortRecovery) == 0 && Recovery.Value == 1 && Recovery.Events == 1 && Recovery.Data == ProbeTag)
                            RecoveredByte = Abi.ReadByte(read);
                    }
                }
            }
            catch (Exception error)
            {
                Error = error;
            }
            finally
            {
                Volatile.Write(ref Phase, 4);
                if (!Release.Wait(TimeSpan.FromSeconds(15)))
                    Fatal("waiter_release");
                try
                {
                    if (maskOwned)
                    {
                        Abi.SetMask(MaskBefore);
                        MaskAfter = Abi.ReadMask();
                        StackAfter = Abi.ReadAltStack();
                        Need(MaskAfter == MaskBefore && Abi.SameStack(StackBefore, StackAfter), "waiter_restore");
                    }
                }
                catch (Exception error)
                {
                    Error ??= error;
                }

                Volatile.Write(ref Phase, 5);
            }
        }

        public void Dispose()
        {
            BeginRecovery.Dispose();
            Release.Dispose();
        }
    }
}
