using System.Reflection;
using System.Runtime.InteropServices;
using OS.Storage;
using OS.Std.Hash;
using K = OS.Storage.ContinuingFat32Bootstrap;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

internal static unsafe partial class ContinuingFat32BootstrapTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly K.State* State = (K.State*)NativeMemory.AllocZeroed((nuint)sizeof(K.State));
        private readonly byte* scratch = (byte*)NativeMemory.AlignedAlloc(5 * 4096, 4096);
        internal K.Admission Input;
        internal bool ReplacedOriginal;
        private bool started;
        internal Fixture(Action<byte[]>? mutate = null, byte[]? provided = null)
        {
            byte[] image = provided ?? Image();
            mutate?.Invoke(image);
            Device = new((ulong)image.Length / 512, observe: true);
            Device.UseMedium(image);
            HostCheck.Equal(B.Status.Success, Device.Start());
            Input = new()
            {
                Device = Device.State,
                Epoch = 91,
                DeviceEpoch = Device.Input.Epoch,
                Sector = scratch,
                Entries = scratch + 512,
                SectorBudget = K.MaximumReads
            };
        }
        internal K.Status Start()
        {
            var status = K.Start(State, Input);
            started = status == K.Status.Pending;
            return status;
        }
        internal K.Status Run()
        {
            for (int i = 0; i < 1000; i++)
            {
                var result = K.Pump(State);
                if (Device.Hardware->Pending)
                    Device.Complete();
                if (result != K.Status.Pending)
                    return result;
            }
            throw new InvalidOperationException("bootstrap did not terminate");
        }
        internal K.Result Copy()
        {
            K.Result result = default;
            HostCheck.Equal(K.Status.Success, K.CopyResult(State, Input.Epoch, &result));
            return result;
        }
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Device.State));
            if (started && !ReplacedOriginal)
                HostCheck.Equal(K.Status.Success, K.Stop(State));
            if (ReplacedOriginal)
                HostCheck.True(Device.State->Retired && !State->Retired);
            Device.CheckGuards();
            Device.Dispose();
            NativeMemory.AlignedFree(scratch);
            NativeMemory.Free(State);
        }
    }
}
