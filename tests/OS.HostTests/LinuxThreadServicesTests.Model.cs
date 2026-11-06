using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using KernelModel = KernelThreadTableTests.Model;
using Services = OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

internal static unsafe partial class LinuxThreadServicesTests
{
    // All storage is private host-test memory. The table's real guarded-context
    // contract is exercised; no native code, page table or CPL3 transfer occurs.
    internal sealed class Model : IDisposable
    {
        internal readonly KernelModel Kernel;
        internal readonly uint Capacity;
        // Extended fixture buffers must stay beyond every modeled worker stack.
        private int StorageOffset => Capacity > 8 ? 0x20000 : 0;
        internal Services* Service => (Services*)(Kernel.Memory + 0x6000);
        internal Services.Slot* Slots => (Services.Slot*)(Kernel.Memory + 0x64000 + StorageOffset);
        internal Services.Worker* Workers => (Services.Worker*)(Kernel.Memory + (Capacity > 8 ? 0x89000 : 0x68000));
        internal Transition.UserImage* Initial => (Transition.UserImage*)(Kernel.Memory + 0x9000);
        private UserMemory* User => (UserMemory*)(Kernel.Memory + 0xa000);

        private readonly bool _userInterrupts;
        internal LinuxSyscallAccess Access => new()
        {
            Context = User,
            UserInterrupts = _userInterrupts,
            PageAccess = &PageAccess,
            CopyFromUser = &CopyFromUser
        };
        internal LinuxSyscallAccess DescriptorAccess => new()
        {
            Context = User,
            UserInterrupts = _userInterrupts,
            PageAccess = &PageAccess,
            CopyFromUser = &CopyFromUser,
            Write = &Sink
        };

        internal LinuxSyscallAccess GatewayAccess
        {
            get
            {
                var access = DescriptorAccess;
                access.Startup = access.Descriptors = &Unsupported;
                access.Threads = &ThreadDispatch;
                return access;
            }
        }

        internal delegate* unmanaged<void*, ulong, byte*, uint, byte> UserCopyOut => &CopyToUser;
        internal uint Reads => User->Reads;
        internal uint Writes => User->Writes;
        internal uint FailedWrites => User->FailedWrites;
        internal ulong UserContextBytes => (ulong)sizeof(UserMemory);

        internal void FailWrite(ulong address) => User->FailWriteAddress = address;
        internal void FailRead(ulong address) => User->FailReadAddress = address;
        internal byte* At(ulong address) => User->Bytes + address - UserBase;
        internal ulong Now
        {
            get => User->Now; set => User->Now = value;
        }
        internal ulong ClockStep
        {
            get => User->ClockStep; set => User->ClockStep = value;
        }

        internal void ChangeMappingLifetime(ulong value) => User->Lifetime = value;
        internal void UseMemory(LinuxMemoryServices.Admission memory) => User->Memory = memory;
        internal void FailClock(bool value) => User->ClockFails = value;
        internal Services.FutexCell* FutexCells => (Services.FutexCell*)(Kernel.Memory + 0x60000 + StorageOffset);

        internal Model(bool futex = false, uint futexCapacity = 4, bool userInterrupts = false, uint capacity = 3)
        {
            HostCheck.True(capacity is > 0 and <= 16);
            Capacity = capacity;
            Kernel = new(capacity, capacity, capacity, generation: 5);
            _userInterrupts = userInterrupts;
            HostCheck.True(sizeof(Services) <= 0x1000 && sizeof(Services.Slot) * capacity <= (capacity > 8 ? 0x5000 : 0x4000) && sizeof(Services.Worker) * capacity <= 0x1000);
            *Service = default;
            new Span<byte>(Slots, checked(sizeof(Services.Slot) * (int)capacity)).Clear();
            new Span<byte>(Workers, checked(sizeof(Services.Worker) * (int)capacity)).Clear();
            *User = new()
            {
                Bytes = Kernel.Memory + 0xb000,
                Service = Service,
                FailWriteAddress = ulong.MaxValue,
                FailReadAddress = ulong.MaxValue,
                Now = 1000,
                Lifetime = 1
            };
            new Span<byte>(User->Bytes, (int)UserBytes).Fill(0x5a);
            for (int i = 0; i < capacity; i++)
                Workers[i] = new()
                {
                    Initial = WorkerContext(i, default),
                    Entry = Kernel.Code + 0x400 + (uint)i * 0x40
                };
            HostCheck.True(Service->Initialize(Key, 2, new()
            {
                Threads = Kernel.Table,
                ThreadKey = KernelThreadTableTests.Key,
                Slots = Slots,
                Workers = Workers,
                Capacity = capacity,
                SlotBytes = (uint)sizeof(Services.Slot) * capacity,
                WorkerBytes = (uint)sizeof(Services.Worker) * capacity,
                ProcessId = 101,
                ObservedMxcsrMask = 0xffbf,
                User = Access,
                CopyToUser = &CopyToUser,
                UserContextBytes = (ulong)sizeof(UserMemory),
                FirmwareExited = true,
                SingleOwnerValidated = true
            }));
            LinuxSyscallFrame frame = new()
            {
                Rip = UserBase + 0x100,
                Rsp = UserBase + 0x3000,
                Rflags = userInterrupts ? 0x202UL : 2,
                FsBase = UserBase + 0x1100,
                GsBase = UserBase + 0x1140
            };
            HostCheck.True(Transition.InitializeUserImage(Initial, Transition.UserImageBytes, frame, 0xffbf, Access));
            new Span<byte>(FutexCells, sizeof(Services.FutexCell) * 4).Clear();
            if (futex)
                HostCheck.True(Service->EnableFutex(Key, 2, FutexPlan(futexCapacity)));
            Initial->Value = -77;
            for (uint i = 0; i < 14; i++)
                Initial->Gpr[i] = 0x8100000000000001UL + i * 0x0101010101010101UL;
            for (int i = 32; i < 512; i++)
                Initial->Fx[i] = (byte)(i * 23 + 7);
        }

        internal Services.FutexAdmission FutexPlan(uint capacity = 4) => new()
        {
            Cells = FutexCells,
            Addresses = (WaitAddress.Slot*)(Kernel.Memory + 0x61000 + StorageOffset),
            Registrations = (WaitAddress.Registration*)(Kernel.Memory + 0x62000 + StorageOffset),
            Capacity = capacity,
            CellBytes = capacity * (uint)sizeof(Services.FutexCell),
            AddressBytes = capacity * (uint)sizeof(WaitAddress.Slot),
            RegistrationBytes = Capacity * (uint)sizeof(WaitAddress.Registration),
            OwnerKey = 0x6675746578616464,
            Resolve = &ResolveFutex,
            ReadMonotonic = &Monotonic
        };
        private ThreadContextSwitch.Descriptor WorkerContext(int i, OSHostHandle identity) => new()
        {
            Identity = identity.Value,
            CurrentIdentity = identity.Value,
            Context = Kernel.Context(i),
            ContextBytes = 688,
            StackGuardStart = Kernel.Stack(i),
            StackBytes = 0x3000,
            ResumeRip = Kernel.Code + 0x100,
            Kind = ThreadContextSwitch.ResumeKind.Entry
        };
        internal OSHostHandle PrepareNext()
        {
            HostCheck.Equal(KernelThreadTable.Status.Ok, Kernel.Table->InspectNextCreateIdentity(KernelThreadTableTests.Key, 2, out var identity));
            uint slot = unchecked((uint)identity.Value);
            int index = checked((int)slot - 1);
            HostCheck.True(index >= 0 && index < Capacity);
            Kernel.Prepare(index);
            Workers[index].Initial = WorkerContext(index, identity);
            HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.AdmitResume(Kernel.Owner, Workers[index].Initial));
            return identity;
        }

        internal OSHostHandle Start()
        {
            var expected = PrepareNext();
            HostCheck.Equal(101L, Service->Start(Key, 2, Initial, Transition.UserImageBytes, new(InheritedMask), out var identity));
            HostCheck.Equal(expected.Value, identity.Value);
            return identity;
        }

        internal Transition.UserImage* CurrentImage()
        {
            HostCheck.True(Service->CurrentImage(Key, 2, out var image));
            return image;
        }

        internal Services.View View(OSHostHandle identity)
        {
            HostCheck.True(Service->Inspect(Key, 2, identity, out var view));
            return view;
        }

        internal LinuxSyscallResult Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) => CallCore(false, number, a0, a1, a2, a3, a4, a5);
        internal LinuxSyscallResult DescriptorCall(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) => CallCore(true, number, a0, a1, a2, a3, 0, 0);
        private LinuxSyscallResult CallCore(bool descriptor, ulong number, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5)
        {
            var image = CurrentImage();
            var frame = image->Frame;
            frame.Number = number;
            frame.Argument0 = a0;
            frame.Argument1 = a1;
            frame.Argument2 = a2;
            frame.Argument3 = a3;
            frame.Argument4 = a4;
            frame.Argument5 = a5;
            image->Frame = frame; // Host model of the owner's already captured syscall image.
            var result = descriptor ? Service->DispatchDescriptor(Key, 2, frame) : Service->Dispatch(Key, 2, ref frame);
            image->Frame = frame;
            if (result.Action is LinuxSyscallAction.Return or LinuxSyscallAction.Yielded)
                image->Value = result.Value;
            return result;
        }

        internal OSHostHandle Clone()
        {
            var expected = PrepareNext();
            var result = Call(56, 0x7d0f00, UserBase + UserBytes, ParentTidAddress, ChildTidAddress, UserBase + 0x2100);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            HostCheck.Equal((long)Read32(ParentTidAddress), result.Value);
            HostCheck.Equal((uint)result.Value, View(expected).Tid);
            return expected;
        }

        internal void Yield(OSHostHandle identity)
        {
            HostCheck.True(Kernel.Table->Yield(KernelThreadTableTests.Key, 2, identity, Kernel.Capture(identity)).Succeeded);
            Kernel.Confirm();
        }

        internal void Put64(ulong address, ulong value) => *(ulong*)(User->Bytes + (address - UserBase)) = value;
        internal ulong Read64(ulong address) => *(ulong*)(User->Bytes + (address - UserBase));
        internal uint Read32(ulong address) => *(uint*)(User->Bytes + (address - UserBase));
        internal void Put32(ulong address, uint value) => *(uint*)(User->Bytes + (address - UserBase)) = value;
        public void Dispose() => Kernel.Dispose();
    }
}
