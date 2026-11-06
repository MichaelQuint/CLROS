using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Block = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

internal static unsafe partial class LinuxWritableDiskProofFixtureTests
{
    private sealed class BlockSeries : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device = new(observe: true);
        internal readonly List<Block.Observation> Rows = [];
        internal readonly F.BlockBoundary Before;
        internal F.BlockBoundary After => Boundary(Device);

        internal BlockSeries()
        {
            HostCheck.Equal(Block.Status.Success, Device.Start());
            Before = Boundary(Device);
        }

        internal Block.RequestToken Write(byte[] bytes)
        {
            Block.RequestToken token = default;
            HostCheck.Equal(Block.Status.Success, Block.SubmitWrite(Device.State, 3, bytes, &token));
            Sample();
            return token;
        }

        internal Block.RequestToken Read()
        {
            Block.RequestToken token = default;
            HostCheck.Equal(Block.Status.Success, Block.SubmitRead(Device.State, 3, &token));
            Sample();
            return token;
        }

        internal Block.RequestToken Flush()
        {
            Block.RequestToken token = default;
            HostCheck.Equal(Block.Status.Success, Block.SubmitFlush(Device.State, &token));
            Sample();
            return token;
        }

        internal void Poll(Block.Status expected)
        {
            Block.Completion value = default;
            HostCheck.Equal(expected, Block.Poll(Device.State, &value));
            Sample();
        }

        internal void Sample()
        {
            if (Device.Observation->Flags != Block.ObservationFlags.None)
                Rows.Add(*Device.Observation);
        }

        internal F.BlockObservationInspection Inspect() => F.InspectBlockObservationValues(Rows.ToArray(), Before, After);
        public void Dispose() => Device.Dispose();
    }
}
