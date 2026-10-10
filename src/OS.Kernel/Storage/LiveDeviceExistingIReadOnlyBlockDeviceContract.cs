using System;
using OS.Drivers;

namespace OS.Storage;

/// <summary>One fresh one-sector read through the existing block contract.
/// The caller owns negotiated FEATURES_OK, exclusive device access and mapped
/// regions, pool metadata and original backing through return. A nonempty
/// attempted read consumes this adapter, even on failure; recreate only after
/// observed reset and fresh negotiation. Native callers use closed constrained
/// calls; boxed or concurrent use and queue reuse are outside this slice.
/// Info is the original authenticated capacity; the core checks it again before
/// publication. Inspect LastRead for cleanup failure and retain uncertain DMA.</summary>
public unsafe struct LiveDeviceExistingIReadOnlyBlockDeviceContract : IReadOnlyBlockDevice
{
    private readonly BlockDeviceInfo _info;
    private readonly VirtioMappedRegion _common, _notify, _device;
    private readonly DmaBufferPool* _dma;
    private readonly VirtioBlkReadAccess _access;
    private readonly uint _multiplier, _pollLimit, _resetLimit;
    private bool _attempted;

    public LiveDeviceExistingIReadOnlyBlockDeviceContract(BlockDeviceInfo info,
        VirtioMappedRegion common, VirtioMappedRegion notify, VirtioMappedRegion device,
        uint multiplier, DmaBufferPool* dma, VirtioBlkReadAccess access, uint pollLimit, uint resetLimit)
    {
        _info = info; _common = common; _notify = notify; _device = device;
        _multiplier = multiplier; _dma = dma; _access = access;
        _pollLimit = pollLimit; _resetLimit = resetLimit;
    }

    public readonly BlockDeviceInfo Info => _info;
    public VirtioBlkReadResult LastRead { get; private set; }
    public readonly bool Attempted => _attempted;

    public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
    {
        var bounds = BlockDeviceRead.Validate(_info, firstBlock, blockCount, destination);
        if (!bounds.Succeeded) return bounds;
        if (_info.BlockSize != 512) return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockSize);
        if (blockCount == 0) return bounds;
        if (blockCount != 1) return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockCount);
        if (_attempted) return BlockReadResult.Fail(BlockDeviceStatus.IoError);
        fixed (LiveDeviceExistingIReadOnlyBlockDeviceContract* self = &this)
        fixed (byte* output = destination)
        {
            // The receiver changes even when transport fails. A destination
            // covering it would corrupt either preserved output or copied data.
            ulong start = (ulong)output, receiver = (ulong)self;
            ulong length = (ulong)destination.Length;
            if (start > ulong.MaxValue - length || receiver > ulong.MaxValue - (ulong)sizeof(LiveDeviceExistingIReadOnlyBlockDeviceContract) ||
                (start < receiver + (ulong)sizeof(LiveDeviceExistingIReadOnlyBlockDeviceContract) && receiver < start + length))
                return BlockReadResult.Fail(BlockDeviceStatus.IoError);
            _attempted = true;
            LastRead = SubmitPollSectorRequest.Run(_common, _notify, _device, _multiplier,
                _dma, _access, firstBlock, output, (uint)destination.Length, _pollLimit,
                _resetLimit, _info.BlockCount);
        }
        return LastRead.Succeeded ? BlockReadResult.Success(1, 512) : BlockReadResult.Fail(BlockDeviceStatus.IoError);
    }
}
