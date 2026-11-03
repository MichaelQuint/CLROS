using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Abi = KestrelSignalProbeAbi;

internal sealed partial class KestrelSignalProbe
{
    // One dedicated actor borrows both pipe ends only through this delegate.
    // The three nonblocking calls form one finite, preemptible sequence after Go.
    // Once entered, cancellation cannot abandon a successfully sent signal.
    private sealed class Actor(int process, int target, int read, int write, EventLifetime? lifetime) : IDisposable
    {
        internal readonly ManualResetEventSlim Go = new(false);
        internal int Tid, Ready, Finished, Sent, Abort;
        internal byte ConsumedByte;
        internal Exception? Error;
        internal void Run()
        {
            try
            {
                Tid = Abi.GetTid();
                Volatile.Write(ref Ready, 1);
                if (lifetime is null)
                {
                    if (!Go.Wait(TimeSpan.FromSeconds(15)))
                        Fatal("actor_release");
                }
                else
                    lifetime.Wait(Go);
                if (Volatile.Read(ref Abort) != 0)
                    return;
                if (lifetime is not null)
                {
                    // Finish clock/token preparation before yielding. A cached
                    // cancellation check adds no clock query after the yield;
                    // the same absolute lifetime is checked after the finite
                    // transaction. GC/preemption can still change its order.
                    lifetime.Check();
                    CancellationToken transactionStop = lifetime.Token;
                    Abi.YieldBeforeRace();
                    transactionStop.ThrowIfCancellationRequested();
                    if (Volatile.Read(ref Abort) != 0)
                        return;
                }

                Abi.WriteByte(write, Abi.WakeByte);
                Abi.SendSignal(process, target);
                Volatile.Write(ref Sent, 1);
                ConsumedByte = Abi.ReadByte(read);
                Need(ConsumedByte == Abi.WakeByte, "competing_byte");
                lifetime?.Check();
            }
            catch (Exception error)
            {
                Error = error;
            }
            finally
            {
                Volatile.Write(ref Finished, 1);
            }
        }

        public void Dispose() => Go.Dispose();
    }
}
