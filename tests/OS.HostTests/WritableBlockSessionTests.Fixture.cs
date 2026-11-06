using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.WritableBlockSession;

internal static unsafe partial class WritableBlockSessionTests
{
    internal sealed class Fixture : IDisposable
    {
        private static long epoch;
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal byte* Data => backing + 4096;
        internal readonly Block.State* State = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        internal readonly DmaBufferSlot* Slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        internal readonly Hardware* Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        internal readonly Block.Observation* Observation;
        private static readonly int ownerOffset = (int)Marshal.OffsetOf<DmaBufferSlot>("Owner");
        internal DmaBufferOwnership Owner => *(DmaBufferOwnership*)((byte*)Slot + ownerOffset);
        private readonly ulong lifetime;
        private readonly ulong expectedCapacity;
        private readonly VirtioMappedRegion common, notify, device;
        internal VirtioNegotiationResult Negotiation;
        internal Fixture(ulong capacity = Capacity, bool observe = false)
        {
            if (observe)
                Observation = (Block.Observation*)NativeMemory.AllocZeroed((nuint)sizeof(Block.Observation));
            new Span<byte>(backing, 12288).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, Physical, DeviceBase, 4096, 64, out var window));
            lifetime = (ulong)Interlocked.Increment(ref epoch);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, Slot, 1, lifetime));
            expectedCapacity = capacity;
            Hardware->Data = Data;
            Hardware->Pool = Pool;
            Hardware->Capacity = capacity;
            Hardware->Status = 0;
            Hardware->Generation = 7;
            Hardware->Offered = Block.Features | (1UL << 28);
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out device));
        }
        internal Block.Admission Input => new()
        {
            Common = common,
            Notify = notify,
            Device = device,
            NotifyMultiplier = 4,
            ResetLimit = 3,
            Pool = Pool,
            Epoch = lifetime,
            Negotiation = Negotiation,
            ExpectedCapacity = expectedCapacity,
            CompletionTimeoutNanos = 1000,
            Access = new()
            {
                Queue = new()
                {
                    Context = Hardware,
                    Read8 = &Read8,
                    Write8 = &Write8,
                    Read16 = &Read16,
                    Write16 = &Write16,
                    Read32 = &Read32,
                    Write32 = &Write32,
                    Fence = &Fence,
                    BusMasterOff = &BusOff
                },
                BusMasterOn = &BusOn
            },
            Clock = new()
            {
                Context = Hardware,
                Read = &Clock
            },
            Observation = Observation
        };
        internal VirtioNegotiationResult Negotiate() => Negotiation = Block.Negotiate(common, 3, Input.Access.Queue);
        internal Block.Status Start()
        {
            Negotiate();
            return Block.Start(State, Input);
        }
        internal void UseMedium(byte[] image, bool prefixOnly = false)
        {
            HostCheck.True(Hardware->Medium == null && image.Length % 512 == 0 && (prefixOnly ? (ulong)image.Length <= expectedCapacity * 512 : (ulong)image.Length == expectedCapacity * 512));
            Hardware->MediumSectors = (ulong)image.Length / 512;
            Hardware->Medium = (byte*)NativeMemory.AllocZeroed((nuint)image.Length);
            Hardware->Persistent = (byte*)NativeMemory.AllocZeroed((nuint)image.Length);
            image.CopyTo(new Span<byte>(Hardware->Medium, image.Length));
            image.CopyTo(new Span<byte>(Hardware->Persistent, image.Length));
        }
        internal void Complete(uint id = 0, uint length = uint.MaxValue, byte status = 0, ushort delta = 1)
        {
            if (!Hardware->Pending)
                Hardware->BadOrder = true;
            uint expected = Hardware->Type == 0 ? 513U : 1U;
            if (length == uint.MaxValue)
                length = expected;
            if (status == 0 && id == 0 && length == expected && delta == 1)
            {
                if (Hardware->Medium != null)
                {
                    HostCheck.True(Hardware->Sector < expectedCapacity);
                    if (Hardware->Type == 0)
                    {
                        if (Hardware->Sector < Hardware->MediumSectors)
                            new ReadOnlySpan<byte>(Hardware->Medium + Hardware->Sector * 512, 512).CopyTo(new Span<byte>(Data + 512, 512));
                        else
                            new Span<byte>(Data + 512, 512).Clear();
                    }
                    else if (Hardware->Type == 1)
                    {
                        if (Hardware->Sector < Hardware->MediumSectors)
                            new ReadOnlySpan<byte>(Data + 512, 512).CopyTo(new Span<byte>(Hardware->Medium + Hardware->Sector * 512, 512));
                        else
                            Hardware->BadOrder = true;
                    }
                    else
                        new ReadOnlySpan<byte>(Hardware->Medium, checked((int)Hardware->MediumSectors * 512)).CopyTo(new Span<byte>(Hardware->Persistent, checked((int)Hardware->MediumSectors * 512)));
                }
                else if (Hardware->Type == 0)
                    for (int i = 0; i < 512; i++)
                        Data[512 + i] = Hardware->Volatile[i];
                else if (Hardware->Type == 1)
                    for (int i = 0; i < 512; i++)
                        Hardware->Volatile[i] = Data[512 + i];
                else
                    for (int i = 0; i < 512; i++)
                        Hardware->Durable[i] = Hardware->Volatile[i];
            }
            // An error does not promise an unchanged medium. Simulate a torn
            // OUT independently of the session's counter/error decisions.
            if (status == 1 && Hardware->Type == 1)
                for (int i = 0; i < 256; i++)
                    if (Hardware->Medium != null)
                    {
                        if (Hardware->Sector < Hardware->MediumSectors)
                            Hardware->Medium[Hardware->Sector * 512 + (uint)i] = Data[512 + i];
                        else
                            Hardware->BadOrder = true;
                    }
                    else
                        Hardware->Volatile[i] = Data[512 + i];
            Data[272] = status;
            byte* element = Data + 152 + (Hardware->Produced & 7) * 8;
            *(uint*)element = id;
            *(uint*)(element + 4) = length;
            Hardware->Produced += delta;
            *(ushort*)(Data + 150) = unchecked((ushort)Hardware->Produced);
            Hardware->Pending = false;
        }
        internal void CheckGuards() => HostCheck.True(!Hardware->BadOrder &&
            new ReadOnlySpan<byte>(backing, 4096).IndexOfAnyExcept((byte)0xa5) < 0 && new ReadOnlySpan<byte>(Data + 4096, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        public void Dispose()
        {
            if (State->RequiresCleanup)
                HostCheck.Equal(Block.Status.Success, Block.Stop(State));
            NativeMemory.Free(Hardware->Medium);
            NativeMemory.Free(Hardware->Persistent);
            NativeMemory.AlignedFree(backing);
            NativeMemory.Free(State);
            NativeMemory.Free(Pool);
            NativeMemory.Free(Slot);
            NativeMemory.Free(Hardware);
            NativeMemory.Free(Observation);
        }
    }
}
