namespace OS.Kernel.Diagnostics;

public static unsafe class OwnedX64GdtContainingNullN64BitCode
{
    public const int SegmentDescriptorByteCount = 8;
    public const int TssDescriptorByteCount = 16;
    public const int NullDescriptorOffset = 0;
    public const int KernelCodeDescriptorOffset = 8;
    public const int KernelDataDescriptorOffset = 16;
    public const int TssDescriptorOffset = 24;
    public const int RequiredByteCount = TssDescriptorOffset + TssDescriptorByteCount;
    public const int LogicalDescriptorCount = 4;
    public const int StorageSlotCount = 5;
    public const ushort TableLimit = RequiredByteCount - 1;
    public const ushort NullSelector = 0x0000;
    public const ushort KernelCodeSelector = 0x0008;
    public const ushort KernelDataSelector = 0x0010;
    public const ushort TssSelector = 0x0018;
    public const byte KernelCodeAccess = 0x9a;
    public const byte KernelDataAccess = 0x92;
    public const byte AvailableTssAccess = 0x89;
    public const byte KernelCodeFlagsAndLimitHigh = 0xaf;
    public const byte KernelDataFlagsAndLimitHigh = 0xcf;
    public const uint MinimumTssLimit = 103;
    public const uint MaximumTssLimit = 0x000f_ffff;
    public const ulong Fnv1A64OffsetBasis = 14_695_981_039_346_656_037UL;
    public const ulong Fnv1A64Prime = 1_099_511_628_211UL;
#if !NOSTDLIB_EFI
    public static OwnedX64GdtEncodingResult Encode(Span<byte> destination, ulong tssBaseAddress, uint tssLimit)
    {
        fixed (byte* destinationPointer = destination)
        {
            return Encode(destinationPointer, destination.Length, tssBaseAddress, tssLimit);
        }
    }

    public static OwnedX64GdtVerificationResult Verify(ReadOnlySpan<byte> source, ulong tssBaseAddress, uint tssLimit)
    {
        fixed (byte* sourcePointer = source)
        {
            return Verify(sourcePointer, source.Length, tssBaseAddress, tssLimit);
        }
    }

#endif
    public static OwnedX64GdtEncodingResult Encode(byte* destination, int destinationCapacity, ulong tssBaseAddress, uint tssLimit)
    {
        OwnedX64GdtEncodingStatus inputStatus = ValidateEncodingInput(destination, destinationCapacity, tssBaseAddress, tssLimit);
        if (inputStatus != OwnedX64GdtEncodingStatus.Success)
        {
            return OwnedX64GdtEncodingResult.Fail(inputStatus, 0, false, default(OwnedX64GdtVerificationResult));
        }

        for (int offset = 0; offset < RequiredByteCount; offset++)
        {
            destination[offset] = ExpectedByteAt(offset, tssBaseAddress, tssLimit);
        }

        OwnedX64GdtVerificationResult verification = Verify(destination, RequiredByteCount, tssBaseAddress, tssLimit);
        if (!verification.Succeeded)
        {
            return OwnedX64GdtEncodingResult.Fail(OwnedX64GdtEncodingStatus.ReadBackMismatch, RequiredByteCount, true, verification);
        }

        return OwnedX64GdtEncodingResult.Pass(verification.ObservedFingerprint, verification);
    }

    public static OwnedX64GdtVerificationResult Verify(byte* source, int sourceLength, ulong tssBaseAddress, uint tssLimit)
    {
        OwnedX64GdtVerificationStatus inputStatus = ValidateVerificationInput(source, sourceLength, tssBaseAddress, tssLimit);
        if (inputStatus != OwnedX64GdtVerificationStatus.Success)
        {
            return OwnedX64GdtVerificationResult.Fail(inputStatus, 0, -1, 0, 0);
        }

        ulong observedFingerprint = ComputeFingerprint(source, RequiredByteCount);
        ulong expectedFingerprint = ComputeExpectedFingerprint(tssBaseAddress, tssLimit);
        for (int offset = 0; offset < RequiredByteCount; offset++)
        {
            if (source[offset] != ExpectedByteAt(offset, tssBaseAddress, tssLimit))
            {
                return OwnedX64GdtVerificationResult.Fail(OwnedX64GdtVerificationStatus.ByteMismatch, offset + 1, offset, observedFingerprint, expectedFingerprint);
            }
        }

        return OwnedX64GdtVerificationResult.Pass(observedFingerprint, expectedFingerprint);
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong highBits = address >> 48;
        bool signBitSet = ((address >> 47) & 1UL) != 0;
        return signBitSet ? highBits == 0xffffUL : highBits == 0;
    }

    private static OwnedX64GdtEncodingStatus ValidateEncodingInput(byte* destination, int destinationCapacity, ulong tssBaseAddress, uint tssLimit)
    {
        if (destinationCapacity < 0)
        {
            return OwnedX64GdtEncodingStatus.InvalidDestination;
        }

        if (destinationCapacity < RequiredByteCount)
        {
            return OwnedX64GdtEncodingStatus.DestinationTooSmall;
        }

        if (destination == null)
        {
            return OwnedX64GdtEncodingStatus.InvalidDestination;
        }

        if (tssBaseAddress == 0)
        {
            return OwnedX64GdtEncodingStatus.NullTssBaseAddress;
        }

        if (!IsCanonicalX64Address(tssBaseAddress))
        {
            return OwnedX64GdtEncodingStatus.NonCanonicalTssBaseAddress;
        }

        if (tssLimit < MinimumTssLimit || tssLimit > MaximumTssLimit)
        {
            return OwnedX64GdtEncodingStatus.InvalidTssLimit;
        }

        return OwnedX64GdtEncodingStatus.Success;
    }

    private static OwnedX64GdtVerificationStatus ValidateVerificationInput(byte* source, int sourceLength, ulong tssBaseAddress, uint tssLimit)
    {
        if (sourceLength < 0)
        {
            return OwnedX64GdtVerificationStatus.InvalidSource;
        }

        if (sourceLength < RequiredByteCount)
        {
            return OwnedX64GdtVerificationStatus.SourceTooSmall;
        }

        if (source == null)
        {
            return OwnedX64GdtVerificationStatus.InvalidSource;
        }

        if (tssBaseAddress == 0)
        {
            return OwnedX64GdtVerificationStatus.NullTssBaseAddress;
        }

        if (!IsCanonicalX64Address(tssBaseAddress))
        {
            return OwnedX64GdtVerificationStatus.NonCanonicalTssBaseAddress;
        }

        if (tssLimit < MinimumTssLimit || tssLimit > MaximumTssLimit)
        {
            return OwnedX64GdtVerificationStatus.InvalidTssLimit;
        }

        return OwnedX64GdtVerificationStatus.Success;
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint *= Fnv1A64Prime;
        }

        return fingerprint;
    }

    private static ulong ComputeExpectedFingerprint(ulong tssBaseAddress, uint tssLimit)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < RequiredByteCount; offset++)
        {
            fingerprint ^= ExpectedByteAt(offset, tssBaseAddress, tssLimit);
            fingerprint *= Fnv1A64Prime;
        }

        return fingerprint;
    }

    private static byte ExpectedByteAt(int offset, ulong tssBaseAddress, uint tssLimit)
    {
        if (offset < KernelCodeDescriptorOffset)
        {
            return 0;
        }

        if (offset < KernelDataDescriptorOffset)
        {
            int codeOffset = offset - KernelCodeDescriptorOffset;
            switch (codeOffset)
            {
                case 0:
                case 1:
                    return 0xff;
                case 5:
                    return KernelCodeAccess;
                case 6:
                    return KernelCodeFlagsAndLimitHigh;
                default:
                    return 0;
            }
        }

        if (offset < TssDescriptorOffset)
        {
            int dataOffset = offset - KernelDataDescriptorOffset;
            switch (dataOffset)
            {
                case 0:
                case 1:
                    return 0xff;
                case 5:
                    return KernelDataAccess;
                case 6:
                    return KernelDataFlagsAndLimitHigh;
                default:
                    return 0;
            }
        }

        int tssOffset = offset - TssDescriptorOffset;
        switch (tssOffset)
        {
            case 0:
                return (byte)tssLimit;
            case 1:
                return (byte)(tssLimit >> 8);
            case 2:
                return (byte)tssBaseAddress;
            case 3:
                return (byte)(tssBaseAddress >> 8);
            case 4:
                return (byte)(tssBaseAddress >> 16);
            case 5:
                return AvailableTssAccess;
            case 6:
                return (byte)((tssLimit >> 16) & 0x0f);
            case 7:
                return (byte)(tssBaseAddress >> 24);
            case 8:
                return (byte)(tssBaseAddress >> 32);
            case 9:
                return (byte)(tssBaseAddress >> 40);
            case 10:
                return (byte)(tssBaseAddress >> 48);
            case 11:
                return (byte)(tssBaseAddress >> 56);
            default:
                return 0;
        }
    }
}
