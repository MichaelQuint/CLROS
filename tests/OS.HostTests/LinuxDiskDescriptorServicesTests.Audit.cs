using OS.Kernel.Compatibility.LinuxX64;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxDiskDescriptorServicesTests
{
    [HostTest("linux.disk public progress boundaries retain complete memory audit after adjacent validation sharing")]
    private static void PublicMemoryAuditBoundaries()
    {
        using Owner owner = new(large: true);
        uint fd = owner.Open();
        byte* output = owner.Thread.At(Output);
        for (int i = 0; i < 8192; i++)
            output[i] = (byte)(Payload(i) ^ 0xff);
        output[-1] = 0xc7;
        output[8192] = 0x3a;

        var call = owner.Call(17, fd, Output, 8192, 0);
        var begun = owner.Begin(call);
        HostCheck.Equal(Fds.DiskProgress.Pending, begun.Progress);
        ulong submitted = owner.Storage.Device->Submitted;
        ulong completed = owner.Storage.Device->Completed;
        ulong copied = owner.Storage.Device->Copied;
        uint writes = owner.Thread.Writes;
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PumpDiskOne(owner.Files.State, owner.ThreadOwner + 1).Progress);

        var memory = owner.Files.Memory.Memory;
        ulong* unused = memory.Tables + memory.TablePages * 512 - 1;
        ulong original = *unused;
        HostCheck.Equal(0UL, original);
        try
        {
            *unused = 1;
            HostCheck.True(!LinuxMemoryServices.Audit(owner.Files.Memory.State));
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PumpDiskOne(owner.Files.State, owner.ThreadOwner).Progress);
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(owner.Files.State, owner.ThreadOwner, begun.Token).Progress);
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.CompleteDisk(owner.Files.State, owner.ThreadOwner, owner.Worker, begun.Token, call).Progress);
            SameDeviceCounters(owner, submitted, completed, copied);
            HostCheck.Equal(writes, owner.Thread.Writes);
            for (int i = 0; i < 8192; i++)
                HostCheck.Equal((byte)(Payload(i) ^ 0xff), output[i]);
            HostCheck.Equal((byte)0xc7, output[-1]);
            HostCheck.Equal((byte)0x3a, output[8192]);
        }
        finally
        {
            *unused = original;
        }

        HostCheck.True(LinuxMemoryServices.Audit(owner.Files.Memory.State));
        HostCheck.Equal(Fds.DiskProgress.Ready, owner.Ready(begun.Token).Progress);
        var result = Fds.CompleteDisk(owner.Files.State, owner.ThreadOwner, owner.Worker, begun.Token, call);
        HostCheck.Equal(Fds.DiskProgress.Immediate, result.Progress);
        HostCheck.Equal(8192L, result.Value);
        HostCheck.Equal(writes + 1, owner.Thread.Writes);
        for (int i = 0; i < 8192; i++)
            HostCheck.Equal(Payload(i), output[i]);
        HostCheck.Equal((byte)0xc7, output[-1]);
        HostCheck.Equal((byte)0x3a, output[8192]);
    }
}
