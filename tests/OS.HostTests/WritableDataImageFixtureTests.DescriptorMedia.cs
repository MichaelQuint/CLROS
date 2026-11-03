using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;
using Image = OS.Tooling.WritableDataImageFixture;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

// Host image/extraction controls only. No native/device/guest persistence claim.
internal static unsafe partial class WritableDataImageFixtureTests
{
    // Uses the original committed provider, not a test-created fd model. The
    // provider is detached before the borrowed one-queue engine/device owners.
    private sealed class DescriptorMedia : IDisposable
    {
        private readonly MutationMedia media;
        internal readonly W.State* State = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        private readonly W.Descriptor* slots = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        private readonly W.Description* descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        private readonly byte* input = (byte*)NativeMemory.AllocZeroed(W.IoBytes);
        internal DescriptorMedia(MutationMedia owner, ulong epoch = 702)
        {
            media = owner;
            HostCheck.True(W.Start(State, new() { Engine = media.State, Epoch = epoch, Descriptors = slots, Descriptions = descriptions, DescriptorCount = 32, DescriptionCount = 32, Input = input, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }));
        }

        private W.Result Wait(W.Result begun)
        {
            if (begun.Phase != W.Progress.Pending)
                return begun;
            for (int turn = 0; turn < 100000; turn++)
            {
                var progress = W.Pump(State);
                media.CompleteHardware();
                if (progress.Phase == W.Progress.Ready)
                    return progress;
                HostCheck.Equal(W.Progress.Pending, progress.Phase);
            }

            throw new InvalidOperationException("named media provider did not finish");
        }

        internal W.Result Run(W.Result begun)
        {
            var ready = Wait(begun);
            var result = ready.Phase == W.Progress.Ready ? W.Complete(State, begun.Request) : ready;
            HostCheck.Equal(W.Progress.Immediate, result.Phase);
            HostCheck.True(result.Value >= 0);
            return result;
        }

        internal W.DescriptorToken Open(string path, uint flags = 0) => Run(W.Open(State, Encoding.ASCII.GetBytes(path), flags)).Descriptor;
        internal byte[] Read(W.DescriptorToken file, uint count, ulong offset = 0, bool positioned = false, uint? expectedBytes = null)
        {
            var begun = W.BeginRead(State, file, count, offset, positioned);
            HostCheck.Equal(W.Progress.Ready, Wait(begun).Phase);
            byte[] output = new byte[count];
            var copied = W.CopyRead(State, begun.Request, output);
            HostCheck.True(copied.Phase == W.Progress.Immediate && copied.Value == (expectedBytes ?? count));
            return output[..checked((int)copied.Value)];
        }

        internal void Sync(W.DescriptorToken file)
        {
            ulong submitted = media.Device.State->Submitted, completed = media.Device.State->Completed, writeBytes = media.Device.State->SubmittedBytes;
            uint flushes = media.Flushes, outs = media.Outs;
            HostCheck.Equal(0L, Run(W.BeginSync(State, file)).Value);
            // A clean barrier still performs one real FLUSH, rather than
            // fabricating success because write frontiers already agree.
            HostCheck.True(media.Flushes == flushes + 1 && media.Outs == outs && media.Device.State->Submitted > submitted && media.Device.State->Submitted - submitted == media.Device.State->Completed - completed && media.Device.State->SubmittedBytes == writeBytes && media.Device.State->CompletedBytes == writeBytes && media.Device.State->DurableBytes == writeBytes && media.Device.State->LastOperation == B.Operation.Flush);
        }

        public void Dispose()
        {
            HostCheck.Equal(W.Progress.Retired, W.Detach(State).Phase);
            NativeMemory.Free(input);
            NativeMemory.Free(descriptions);
            NativeMemory.Free(slots);
            NativeMemory.Free(State);
        }
    }
}
