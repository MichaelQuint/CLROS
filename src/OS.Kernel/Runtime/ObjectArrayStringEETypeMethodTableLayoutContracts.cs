using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
// Source-derived x64 little-endian value contract, not emitted-object ABI proof.
// Authority at RuntimeSourceCommit: ObjectLayout.h, MethodTable.cs/constants,
// EETypeBuilderHelpers.cs, TargetDetails.cs, GCHelpers.cpp, String.NativeAot.cs.
// No memory access, allocation, type discovery, dispatch or GC is performed.
public static class ObjectArrayStringEETypeMethodTableLayoutContracts
{
    public const string ILCompilerVersion = "11.0.0-rc.3.26522.148";
    public const string RuntimeSourceCommit = "5254d8bcf59917edc1e1abc33bdf2442b77b1c70";
    public const int PointerBytes = 8;
    public const int AllocationAlignmentBytes = 8;
    // Object-reference origin: allocation begins eight bytes before it.
    public const int ObjectHeaderBytes = 8;
    public const int ObjectHeaderOffsetFromReference = -8;
    public const int MethodTableTokenOffsetFromReference = 0;
    public const int FixedDataOffsetFromReference = 8;
    public const int ComponentLengthOffsetFromReference = 8;
    public const int ArrayDataOffsetFromReference = 16;
    public const int StringDataOffsetFromReference = 12;
    // Total sizes start at the allocation origin and include the header.
    // String base includes exactly one UTF-16 terminator before rounding.
    public const uint MinimumFixedSizeBytes = 24;
    public const uint ArrayBaseSizeBytes = 24;
    public const uint StringBaseSizeBytes = 22;
    public const ushort StringComponentBytes = 2;
    public const int MaximumArrayLength = 0x7FFFFFC7;
    public const int MaximumStringLength = 0x3FFFFFDF;
    // Fixed-prefix offsets start at the MethodTable, not at an object.
    public const int MethodTablePrefixBytes = 24;
    public const int FlagsOffset = 0;
    public const int BaseSizeOffset = 4;
    public const int RelatedTypeTokenOffset = 8;
    public const int VTableSlotCountOffset = 16;
    public const int InterfaceCountOffset = 18;
    public const int TypeHashOffset = 20;
    public const uint HasComponentSizeFlag = 0x80000000;
    public const uint HasPointersFlag = 0x01000000;
    public const uint ReservedCollectibleFlag = 0x00200000;
    public const uint KindMask = 0x00030000;
    public const uint CanonicalKind = 0;
    public const uint FunctionPointerKind = 0x00010000;
    public const uint ParameterizedKind = 0x00020000;
    public const uint GenericDefinitionKind = 0x00030000;
    public const uint ElementTypeMask = 0x7C000000;
    public const int ElementTypeShift = 26;
    public const uint ClassElementType = 0x14;
    public const uint SzArrayElementType = 0x18;
    public const uint MdArrayElementType = 0x17;
    private const uint KnownExtendedFlags = 0x17FF;
    private const uint SupportedExtendedFlags = 0x0008;
    // Dynamic, finalizer, variance and generic metadata need future contracts.
    private const uint UnsupportedTypeFlags = 0x02980000;
    // Admission order is profile, invalid flags, shape, unsupported features,
    // base, component, count, then caller-declared allocation capacity.
    // The capacity is a numeric bound only; no storage availability is proven.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static NativeAotLayoutResult Evaluate(NativeAotLayoutProfile profile, NativeAotMethodTablePrefix prefix, int componentCount, ulong availableAllocationBytes)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian)
            return Fail(NativeAotLayoutStatus.UnsupportedProfile);
        uint flags = prefix.Flags;
        if ((flags & ReservedCollectibleFlag) != 0)
            return Fail(NativeAotLayoutStatus.InvalidFlags);
        uint kind = flags & KindMask;
        uint element = (flags & ElementTypeMask) >> ElementTypeShift;
        bool hasComponent = (flags & HasComponentSizeFlag) != 0;
        bool containsPointers = (flags & HasPointersFlag) != 0;
        uint lowBits = flags & 0xFFFF;
        NativeAotLayoutShape shape;
        if (kind == CanonicalKind && element == ClassElementType)
        {
            if (hasComponent)
            {
                if (containsPointers)
                    return Fail(NativeAotLayoutStatus.InvalidFlags);
                shape = NativeAotLayoutShape.String;
            }
            else
            {
                if ((lowBits & ~KnownExtendedFlags) != 0)
                    return Fail(NativeAotLayoutStatus.InvalidFlags);
                shape = NativeAotLayoutShape.FixedObject;
            }
        }
        else if (kind == ParameterizedKind && element == SzArrayElementType)
        {
            if (!hasComponent)
                return Fail(NativeAotLayoutStatus.InvalidFlags);
            shape = NativeAotLayoutShape.SzArray;
        }
        else
        {
            return Fail(NativeAotLayoutStatus.UnsupportedShape);
        }

        // Dispatch-map/sealed-vtable flags are retained metadata only. This
        // evaluator neither measures nor reads any optional MethodTable tail.
        if ((flags & UnsupportedTypeFlags) != 0 || (shape == NativeAotLayoutShape.FixedObject && (lowBits & ~SupportedExtendedFlags) != 0))
            return Fail(NativeAotLayoutStatus.UnsupportedTypeFeatures);
        ushort componentSize = 0;
        int lengthOffset = -1;
        int dataOffset = FixedDataOffsetFromReference;
        if (shape == NativeAotLayoutShape.FixedObject)
        {
            if (prefix.BaseSize < MinimumFixedSizeBytes || (prefix.BaseSize & (AllocationAlignmentBytes - 1)) != 0)
                return Fail(NativeAotLayoutStatus.InvalidBaseSize);
            if (componentCount != 0)
                return Fail(NativeAotLayoutStatus.InvalidLength);
        }
        else
        {
            bool isString = shape == NativeAotLayoutShape.String;
            if (prefix.BaseSize != (isString ? StringBaseSizeBytes : ArrayBaseSizeBytes))
                return Fail(NativeAotLayoutStatus.InvalidBaseSize);
            componentSize = (ushort)lowBits;
            if (componentSize == 0 || (isString && componentSize != StringComponentBytes))
                return Fail(NativeAotLayoutStatus.InvalidComponentSize);
            if (componentCount < 0 || componentCount > (isString ? MaximumStringLength : MaximumArrayLength))
                return Fail(NativeAotLayoutStatus.InvalidLength);
            lengthOffset = ComponentLengthOffsetFromReference;
            dataOffset = isString ? StringDataOffsetFromReference : ArrayDataOffsetFromReference;
        }

        // Admitted count <= 0x7FFFFFC7 and component <= 65535: this sum and
        // its eight-byte rounding fit ulong. Promote before multiplication.
        ulong rawSize = prefix.BaseSize + (ulong)componentCount * componentSize;
        ulong alignedSize = (rawSize + (AllocationAlignmentBytes - 1)) & ~7UL;
        if (availableAllocationBytes < alignedSize)
            return Fail(NativeAotLayoutStatus.CapacityTooSmall);
        return new NativeAotLayoutResult(NativeAotLayoutStatus.Success, shape, componentSize, containsPointers, lengthOffset, dataOffset, rawSize, alignedSize);
    }

    private static NativeAotLayoutResult Fail(NativeAotLayoutStatus status) => new(status, default, 0, false, 0, 0, 0, 0);
}
