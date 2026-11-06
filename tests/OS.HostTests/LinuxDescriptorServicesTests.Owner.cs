using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using Services = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe partial class LinuxDescriptorServicesTests
{
    internal sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServicesTests.Owner Memory = new();
        public readonly Services.State* State;
        public readonly Model* Context;
        public Services.Admission Admission;
        private readonly byte* _path;
        private readonly byte* _package;
        public Owner(uint descriptors = 12, uint descriptions = 12, bool ignoreSigpipe = true)
        {
            Memory.Begin();
            HostCheck.Equal((long)User, LinuxMemoryServices.Map(Memory.State, User, 32768, 3, 0x100022, ulong.MaxValue, 0));
            State = (Services.State*)NativeMemory.AlignedAlloc((nuint)sizeof(Services.State), 16);
            *State = default;
            Context = (Model*)NativeMemory.AlignedAlloc((nuint)sizeof(Model), 16);
            *Context = new()
            {
                Memory = Memory.Memory
            };
            _path = (byte*)NativeMemory.Alloc(17);
            Encoding.ASCII.GetBytes("/app/fixture.data").CopyTo(new Span<byte>(_path, 17));
            _package = (byte*)NativeMemory.Alloc(10);
            Encoding.ASCII.GetBytes("abcdefXYZ\n").CopyTo(new Span<byte>(_package, 10));
            Admission = new()
            {
                Slots = (Services.Slot*)NativeMemory.AllocZeroed(descriptors, (nuint)sizeof(Services.Slot)),
                Descriptions = (Services.Description*)NativeMemory.AllocZeroed(descriptions, (nuint)sizeof(Services.Description)),
                Pipes = (LinuxPipe*)NativeMemory.AllocZeroed(2, (nuint)sizeof(LinuxPipe)),
                PipeBuffers = (byte*)NativeMemory.Alloc(8192),
                Snapshots = (byte*)NativeMemory.Alloc(descriptions * Services.SnapshotBytes),
                Scratch = (byte*)NativeMemory.Alloc(8192),
                DescriptorCount = descriptors,
                DescriptionCount = descriptions,
                PipeCount = 2,
                PipeCapacity = 4096,
                ScratchBytes = 8192,
                Views = (Services.View*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Services.View)),
                ViewCount = 1,
                Memory = Memory.State,
                PhysicalBytes = 256 * 1024 * 1024,
                AvailableBytes = 64 * 1024 * 1024,
                User = new()
                {
                    Context = Context,
                    PageAccess = &Permissions,
                    CopyFromUser = &CopyIn,
                    Write = &Sink
                },
                CopyToUser = &CopyOut,
                FirmwareExited = true,
                SingleOwnerValidated = true,
                SigpipeIgnored = ignoreSigpipe
            };
            Admission.Views[0] = new()
            {
                Path = _path,
                PathBytes = 17,
                Data = _package,
                Bytes = 10,
                Inode = 256
            };
            new Span<byte>(Admission.PipeBuffers, 8192).Fill(0xa5);
            new Span<byte>(Admission.Snapshots, (int)(descriptions * Services.SnapshotBytes)).Fill(0xa5);
            new Span<byte>(Admission.Scratch, 8192).Fill(0xa5);
        }

        public void Begin() => HostCheck.True(Services.Begin(State, Admission));
        public long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) => Services.Dispatch(State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3 });
        public void Text(ulong address, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            bytes.CopyTo(new Span<byte>(Memory.At(address), bytes.Length));
            Memory.At(address)[bytes.Length] = 0;
        }

        public long Open(string path = "/app/fixture.data", ulong flags = 0)
        {
            Text(User, path);
            return Call(257, unchecked((uint)-100), User, flags);
        }

        public (uint Read, uint Write) Pipe(uint flags = Services.NonBlock | Services.CloseOnExec)
        {
            HostCheck.Equal(0L, Call(293, Output, flags));
            uint* fds = (uint*)Memory.At(Output);
            return (fds[0], fds[1]);
        }

        public string ReadText(uint fd)
        {
            long bytes = Call(0, fd, Output, 4096);
            HostCheck.True(bytes >= 0);
            return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(Memory.At(Output), (int)bytes));
        }

        public void Dispose()
        {
            NativeMemory.Free(Admission.Slots);
            NativeMemory.Free(Admission.Descriptions);
            NativeMemory.Free(Admission.Pipes);
            NativeMemory.Free(Admission.PipeBuffers);
            NativeMemory.Free(Admission.Snapshots);
            NativeMemory.Free(Admission.Scratch);
            NativeMemory.Free(Admission.Views);
            NativeMemory.Free(_path);
            NativeMemory.Free(_package);
            NativeMemory.AlignedFree(State);
            NativeMemory.AlignedFree(Context);
            Memory.Dispose();
        }
    }
}
