using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using D = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using LogUpload = OS.Tooling.NativeAotLogUploadContract;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class LinuxContinuingWritableDescriptorServicesTests
{
    private sealed class ForeignRead : IDisposable
    {
        private readonly Owner owner;
        internal readonly P.State* State = (P.State*)NativeMemory.AllocZeroed((nuint)sizeof(P.State));
        private readonly byte* output = (byte*)NativeMemory.AllocZeroed(512);
        private P.Token token;
        internal ForeignRead(Owner owner, bool held)
        {
            this.owner = owner;
            var a = new P.Admission
            {
                Engine = owner.Storage.State,
                Device = owner.Storage.Device.State,
                Epoch = 991,
                EngineEpoch = owner.Input.Epoch,
                DeviceEpoch = owner.Storage.Device.Input.Epoch,
                FirstLba = 1,
                LastLba = 7
            };
            HostCheck.Equal(P.Status.Success, P.Start(State, a));
            P.Token started = default;
            HostCheck.Equal(P.Status.Success, P.Submit(State, 1, &started));
            token = started;
            if (held)
            {
                owner.CompleteHardware();
                P.Completion result = default;
                HostCheck.Equal(P.Status.Ready, P.Poll(State, token, &result));
            }
        }

        internal byte[] Physical() => new ReadOnlySpan<byte>(owner.Storage.State, sizeof(C.State)).ToArray().Concat(new ReadOnlySpan<byte>(owner.Storage.Device.State, sizeof(B.State)).ToArray()).Concat(new ReadOnlySpan<byte>(owner.Storage.Device.Observation, sizeof(B.Observation)).ToArray()).Concat(new ReadOnlySpan<byte>(owner.Storage.Device.Data, 4096).ToArray()).ToArray();
        internal void Blocked(D.Result request)
        {
            HostCheck.True(request.Phase == D.Progress.Pending && request.Request.Owner != 0 && !owner.Storage.State->RequestActive);
            byte[] physical = Physical();
            uint calls = owner.Device->Calls, notifies = owner.Device->Notifies;
            for (int i = 0; i < 7; i++)
            {
                var progress = D.Pump(owner.State);
                HostCheck.True(progress.Phase == D.Progress.Pending && progress.Request.Generation == request.Request.Generation);
                B.Observation sample = *owner.Storage.Device.Observation;
                HostCheck.True(sample.Request.Generation != 0);
            }

            HostCheck.True(physical.SequenceEqual(Physical()) && owner.Device->Calls == calls && owner.Device->Notifies == notifies && State->RequestActive);
        }

        internal void Release()
        {
            owner.CompleteHardware();
            P.Completion result = default;
            HostCheck.Equal(P.Status.Ready, P.Poll(State, token, &result));
            B.Observation completed = *owner.Storage.Device.Observation;
            HostCheck.True(completed.Request.Generation == result.Physical.Token.Generation);
            HostCheck.Equal(P.Status.Success, P.Copy(State, token, new Span<byte>(output, 512)));
            B.Observation copied = *owner.Storage.Device.Observation;
            HostCheck.True((copied.Flags & B.ObservationFlags.ReadCopied) != 0);
        }

        public void Dispose()
        {
            if (State->RequestActive && !owner.Storage.Device.State->Retired)
                Release();
            HostCheck.Equal(P.Status.Success, P.Stop(State));
            NativeMemory.Free(output);
            NativeMemory.Free(State);
        }
    }
}
