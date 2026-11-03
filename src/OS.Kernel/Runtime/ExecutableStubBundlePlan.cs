namespace OS.Kernel.Runtime;

public static class ExecutableStubBundlePlan
{
    public const int StableSlotCount = 7;
    public static readonly nuint MissingSlotOffset = ExecutableStubBundlePlanResult.MissingSlotOffset;
    public static ExecutableStubBundlePlanResult Create(ReadOnlySpan<ExecutableStubBundleSlot> slots, ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        if (!IsValidMemoryPlan(executableStubMemoryPlan))
        {
            return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.InvalidMemoryPlan, executableStubMemoryPlan.Status, 0, 0, 0);
        }

        byte seenSlots = 0;
        int slotCount = 0;
        nuint nextOffset = 0;
        nuint com1Out8Offset = MissingSlotOffset;
        nuint com1In8Offset = MissingSlotOffset;
        nuint installIdtOffset = MissingSlotOffset;
        nuint installGdtOffset = MissingSlotOffset;
        nuint loadTaskRegisterOffset = MissingSlotOffset;
        nuint invalidOpcodeProbeOffset = MissingSlotOffset;
        nuint nullWriteProbeOffset = MissingSlotOffset;
        for (int i = 0; i < slots.Length; i++)
        {
            ExecutableStubBundleSlot slot = slots[i];
            if (!IsKnownSlotKind(slot.Kind))
            {
                return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.UnknownSlotKind, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
            }

            if (slot.ByteLength == 0)
            {
                return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.ZeroLengthSlot, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
            }

            byte slotMask = GetSlotMask(slot.Kind);
            if ((seenSlots & slotMask) != 0)
            {
                return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.DuplicateSlot, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
            }

            seenSlots |= slotMask;
            nuint alignedOffset = ExecutableStubMemoryPlan.AlignUp(nextOffset, executableStubMemoryPlan.Alignment);
            if (alignedOffset < nextOffset)
            {
                return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.PayloadLengthOverflow, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
            }

            nuint endOffset = alignedOffset + slot.ByteLength;
            if (endOffset < alignedOffset)
            {
                return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.PayloadLengthOverflow, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
            }

            SetSlotOffset(slot.Kind, alignedOffset, ref com1Out8Offset, ref com1In8Offset, ref installIdtOffset, ref installGdtOffset, ref loadTaskRegisterOffset, ref invalidOpcodeProbeOffset, ref nullWriteProbeOffset);
            nextOffset = endOffset;
            slotCount++;
        }

        nuint requiredMemoryBytes = executableStubMemoryPlan.PayloadOffset + nextOffset;
        if (requiredMemoryBytes < executableStubMemoryPlan.PayloadOffset)
        {
            return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.RequiredMemoryLengthOverflow, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
        }

        requiredMemoryBytes = ExecutableStubMemoryPlan.AlignUp(requiredMemoryBytes, executableStubMemoryPlan.PageSize);
        if (requiredMemoryBytes < executableStubMemoryPlan.PayloadOffset)
        {
            return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.RequiredMemoryLengthOverflow, executableStubMemoryPlan.Status, slotCount, nextOffset, 0);
        }

        if (requiredMemoryBytes > executableStubMemoryPlan.RequiredByteLength)
        {
            return ExecutableStubBundlePlanResult.Fail(ExecutableStubBundlePlanStatus.InsufficientMemoryPlan, executableStubMemoryPlan.Status, slotCount, nextOffset, requiredMemoryBytes);
        }

        return ExecutableStubBundlePlanResult.Success(slotCount, nextOffset, requiredMemoryBytes, com1Out8Offset, com1In8Offset, installIdtOffset, installGdtOffset, loadTaskRegisterOffset, invalidOpcodeProbeOffset, nullWriteProbeOffset);
    }

    public static bool IsKnownSlotKind(ExecutableStubSlotKind kind)
    {
        return kind >= ExecutableStubSlotKind.Com1Out8 && kind <= ExecutableStubSlotKind.NullWriteProbe;
    }

    private static byte GetSlotMask(ExecutableStubSlotKind kind)
    {
        return (byte)(1 << ((int)kind - 1));
    }

    private static bool IsValidMemoryPlan(ExecutableStubMemoryPlanResult executableStubMemoryPlan)
    {
        return executableStubMemoryPlan.Succeeded && executableStubMemoryPlan.PageSize >= ExecutableStubMemoryPlan.MinimumPageSize && executableStubMemoryPlan.Alignment != 0 && executableStubMemoryPlan.Alignment <= executableStubMemoryPlan.PageSize && executableStubMemoryPlan.PayloadOffset <= executableStubMemoryPlan.RequiredByteLength;
    }

    private static void SetSlotOffset(ExecutableStubSlotKind kind, nuint offset, ref nuint com1Out8Offset, ref nuint com1In8Offset, ref nuint installIdtOffset, ref nuint installGdtOffset, ref nuint loadTaskRegisterOffset, ref nuint invalidOpcodeProbeOffset, ref nuint nullWriteProbeOffset)
    {
        switch (kind)
        {
            case ExecutableStubSlotKind.Com1Out8:
                com1Out8Offset = offset;
                break;
            case ExecutableStubSlotKind.Com1In8:
                com1In8Offset = offset;
                break;
            case ExecutableStubSlotKind.InstallIdt:
                installIdtOffset = offset;
                break;
            case ExecutableStubSlotKind.InstallGdt:
                installGdtOffset = offset;
                break;
            case ExecutableStubSlotKind.LoadTaskRegister:
                loadTaskRegisterOffset = offset;
                break;
            case ExecutableStubSlotKind.InvalidOpcodeProbe:
                invalidOpcodeProbeOffset = offset;
                break;
            case ExecutableStubSlotKind.NullWriteProbe:
                nullWriteProbeOffset = offset;
                break;
        }
    }
}
