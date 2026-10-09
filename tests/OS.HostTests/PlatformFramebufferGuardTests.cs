using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformFramebufferGuardTests
{

    [HostTest("platform.framebuffer snapshot allowed before UEFI gone", HostLane.Fast, 0)]
    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FramebufferSourceDouble source = FramebufferSourceDouble.Success(
            new PlatformFramebufferMetadata(
                0x80000000,
                800 * 600 * PlatformFramebufferResult.BytesPerPixel,
                800,
                600,
                800,
                PlatformFramebufferPixelFormat.Bgrx8888));
        IPlatformFramebuffer framebuffer = new GuardedPlatformFramebuffer(firmware, source);

        PlatformFramebufferResult result = framebuffer.CaptureSnapshot();

        return HostCheck.Result(result.Succeeded
            && result.Status == PlatformFramebufferStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.FirmwareMode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformFramebufferOperations.CaptureSnapshot
            && result.SourceStatus == PlatformFramebufferSourceStatus.Success
            && result.BaseAddress == 0x80000000
            && result.ByteLength == 800 * 600 * PlatformFramebufferResult.BytesPerPixel
            && result.Width == 800
            && result.Height == 600
            && result.StridePixels == 800
            && result.PixelFormat == PlatformFramebufferPixelFormat.Bgrx8888
            && result.RequiredByteLength == result.ByteLength
            && result.FirmwareStatus == 0
            && source.CaptureCount == 1);
    }

    [HostTest("platform.framebuffer snapshot denied in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestDeniedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        FramebufferSourceDouble source = FramebufferSourceDouble.Success(CreateValidMetadata());
        IPlatformFramebuffer framebuffer = new GuardedPlatformFramebuffer(firmware, source);

        PlatformFramebufferResult result = framebuffer.CaptureSnapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformFramebufferOperations.CaptureSnapshot
            && result.SourceStatus == PlatformFramebufferSourceStatus.Unknown
            && result.BaseAddress == 0
            && result.ByteLength == 0
            && source.CaptureCount == 0);
    }

    [HostTest("platform.framebuffer snapshot denied after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestDeniedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        FramebufferSourceDouble source = FramebufferSourceDouble.Success(CreateValidMetadata());
        IPlatformFramebuffer framebuffer = new GuardedPlatformFramebuffer(firmware, source);

        PlatformFramebufferResult result = framebuffer.CaptureSnapshot();

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformFramebufferOperations.CaptureSnapshot
            && result.SourceStatus == PlatformFramebufferSourceStatus.Unknown
            && source.CaptureCount == 0);
    }

    [HostTest("platform.framebuffer snapshot rejects zero base or length", HostLane.Fast, 3)]
    private static bool TestRejectsZeroBaseOrLength()
    {
        PlatformFramebufferResult zeroBase = Capture(
            new PlatformFramebufferMetadata(
                0,
                4096,
                32,
                32,
                32,
                PlatformFramebufferPixelFormat.Rgbx8888));
        PlatformFramebufferResult zeroLength = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                0,
                32,
                32,
                32,
                PlatformFramebufferPixelFormat.Rgbx8888));

        return HostCheck.Result(!zeroBase.Succeeded
            && zeroBase.Status == PlatformFramebufferStatus.InvalidBaseAddress
            && zeroBase.BaseAddress == 0
            && !zeroLength.Succeeded
            && zeroLength.Status == PlatformFramebufferStatus.InvalidByteLength
            && zeroLength.BaseAddress == 0x90000000
            && zeroLength.ByteLength == 0);
    }

    [HostTest("platform.framebuffer snapshot rejects zero dimensions", HostLane.Fast, 4)]
    private static bool TestRejectsZeroDimensions()
    {
        PlatformFramebufferResult zeroWidth = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                4096,
                0,
                32,
                32,
                PlatformFramebufferPixelFormat.Rgbx8888));
        PlatformFramebufferResult zeroHeight = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                4096,
                32,
                0,
                32,
                PlatformFramebufferPixelFormat.Rgbx8888));

        return HostCheck.Result(!zeroWidth.Succeeded
            && zeroWidth.Status == PlatformFramebufferStatus.InvalidDimensions
            && zeroWidth.Width == 0
            && !zeroHeight.Succeeded
            && zeroHeight.Status == PlatformFramebufferStatus.InvalidDimensions
            && zeroHeight.Height == 0);
    }

    [HostTest("platform.framebuffer snapshot rejects invalid stride", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidStride()
    {
        PlatformFramebufferResult result = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                4096,
                64,
                16,
                63,
                PlatformFramebufferPixelFormat.Rgbx8888));

        return HostCheck.Result(!result.Succeeded
            && result.Status == PlatformFramebufferStatus.InvalidStride
            && result.Width == 64
            && result.StridePixels == 63);
    }

    [HostTest("platform.framebuffer snapshot rejects unsupported pixel format", HostLane.Fast, 6)]
    private static bool TestRejectsUnsupportedPixelFormat()
    {
        PlatformFramebufferResult bitmask = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                4096,
                32,
                32,
                32,
                PlatformFramebufferPixelFormat.Bitmask));
        PlatformFramebufferResult bltOnly = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                4096,
                32,
                32,
                32,
                PlatformFramebufferPixelFormat.BltOnly));

        return HostCheck.Result(!bitmask.Succeeded
            && bitmask.Status == PlatformFramebufferStatus.UnsupportedPixelFormat
            && bitmask.PixelFormat == PlatformFramebufferPixelFormat.Bitmask
            && !bltOnly.Succeeded
            && bltOnly.Status == PlatformFramebufferStatus.UnsupportedPixelFormat
            && bltOnly.PixelFormat == PlatformFramebufferPixelFormat.BltOnly);
    }

    [HostTest("platform.framebuffer snapshot rejects size mismatch and overflow", HostLane.Fast, 7)]
    private static bool TestRejectsSizeMismatchAndOverflow()
    {
        PlatformFramebufferResult tooSmall = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                1023,
                16,
                16,
                16,
                PlatformFramebufferPixelFormat.Rgbx8888));
        PlatformFramebufferResult overflow = Capture(
            new PlatformFramebufferMetadata(
                0x90000000,
                ulong.MaxValue,
                uint.MaxValue,
                uint.MaxValue,
                uint.MaxValue,
                PlatformFramebufferPixelFormat.Rgbx8888));

        return HostCheck.Result(!tooSmall.Succeeded
            && tooSmall.Status == PlatformFramebufferStatus.InvalidBufferSize
            && tooSmall.RequiredByteLength == 1024
            && tooSmall.ByteLength == 1023
            && !overflow.Succeeded
            && overflow.Status == PlatformFramebufferStatus.ArithmeticOverflow
            && overflow.RequiredByteLength == 0);
    }

    [HostTest("platform.framebuffer snapshot reports source failures", HostLane.Fast, 8)]
    private static bool TestReportsSourceFailures()
    {
        const ulong firmwareStatus = 0x8000000000000007;
        PlatformFramebufferResult unavailable = Capture(PlatformFramebufferSourceResult.Unavailable());
        PlatformFramebufferResult firmwareError = Capture(
            PlatformFramebufferSourceResult.FirmwareError(firmwareStatus));

        return HostCheck.Result(!unavailable.Succeeded
            && unavailable.Status == PlatformFramebufferStatus.SourceUnavailable
            && unavailable.SourceStatus == PlatformFramebufferSourceStatus.Unavailable
            && unavailable.FirmwareStatus == 0
            && !firmwareError.Succeeded
            && firmwareError.Status == PlatformFramebufferStatus.SourceFailed
            && firmwareError.SourceStatus == PlatformFramebufferSourceStatus.FirmwareError
            && firmwareError.FirmwareStatus == firmwareStatus);
    }

    [HostTest("platform.framebuffer result contains no managed references", HostLane.Fast, 9)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFramebufferMetadata>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFramebufferSourceResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PlatformFramebufferResult>());
    }

    private static PlatformFramebufferResult Capture(PlatformFramebufferMetadata metadata)
    {
        return Capture(PlatformFramebufferSourceResult.Captured(metadata));
    }

    private static PlatformFramebufferResult Capture(PlatformFramebufferSourceResult sourceResult)
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        FramebufferSourceDouble source = new FramebufferSourceDouble(sourceResult);
        IPlatformFramebuffer framebuffer = new GuardedPlatformFramebuffer(firmware, source);
        return framebuffer.CaptureSnapshot();
    }

    private static PlatformFramebufferMetadata CreateValidMetadata()
    {
        return new PlatformFramebufferMetadata(
            0x80000000,
            800 * 600 * PlatformFramebufferResult.BytesPerPixel,
            800,
            600,
            800,
            PlatformFramebufferPixelFormat.Bgrx8888);
    }

    private sealed class FramebufferSourceDouble : IPlatformFramebufferSource
    {
        private readonly PlatformFramebufferSourceResult _result;

        public FramebufferSourceDouble(PlatformFramebufferSourceResult result)
        {
            _result = result;
        }

        public int CaptureCount { get; private set; }

        public static FramebufferSourceDouble Success(PlatformFramebufferMetadata metadata)
        {
            return new FramebufferSourceDouble(
                PlatformFramebufferSourceResult.Captured(metadata));
        }

        public PlatformFramebufferSourceResult CaptureSnapshot()
        {
            CaptureCount++;
            return _result;
        }
    }
}
