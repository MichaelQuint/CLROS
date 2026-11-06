using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>
/// One non-chained version-one AMD64 frame, ILC 11.0.0 RC2 runtime revision
/// 820230c386db5b0106c5430b3e5aee92a7826bf5. Semantics: Windows
/// CoffNativeCodeManager/regdisplay and unwinder/amd64/unwinder.cpp.
/// No handler/language-data calls, CPU transfer, leaf inference, machine frame,
/// cross-image return, volatile register or shadow-stack support.
/// </summary>
/// <remarks>
/// Caller retains the original image/module/index, readable stack and genuine
/// instruction-boundary context under exclusive stable access. Numeric bounds
/// are not mapping or processor-capture proof. Lookup revalidates its metadata
/// snapshot, but code and stack bytes are not snapshotted. Nothing roots an
/// object, extends lifetime or authenticates forged PODs. Only a local context
/// copy is changed; every failure has a zero payload.
/// </remarks>

public static unsafe partial class X64FrameUnwind
{
    public const int MaximumEpilogBytes = 32, MaximumEpilogInstructions = 16;
    public static X64FrameUnwindResult UnwindOne(NativeAotLayoutProfile profile, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle, byte* metadataStorage, ulong metadataCapacity, byte* stack, ulong stackBytes, X64UnwindContext input)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian)
            return Fail(X64FrameUnwindStatus.UnsupportedProfile);
        var metadata = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(metadataStorage, metadataCapacity, input.Rip);
        if (!metadata.Succeeded)
            return Fail(X64FrameUnwindStatus.MetadataRejected);
        if (metadata.ModuleOwner != (ulong)moduleOwner || metadata.ModuleHandle != moduleHandle || mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong imageBase) != AddressValueStatus.Success || metadata.ImageBase != imageBase || TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, imageBase + metadata.BeginRva, metadata.EndRva - metadata.BeginRva, TypeManagerRegisteredRangeKind.ExecutableEntry) != TypeManagerModuleStatus.Success)
            return Fail(X64FrameUnwindStatus.ModuleRejected);
        ulong start = (ulong)stack;
        if (!Span(start, stackBytes) || (start & 7) != 0 || !Position(start, stackBytes, input.Rsp))
            return Fail(X64FrameUnwindStatus.StackRangeRejected);
        if (Overlap(start, stackBytes, imageBase, capture.Range.ImageSize) || Overlap(start, stackBytes, (ulong)moduleOwner, moduleCapacity) || Overlap(start, stackBytes, (ulong)metadataStorage, metadataCapacity))
            return Fail(X64FrameUnwindStatus.StorageOverlap);
        if (metadata.ChainDepth != 0 || (metadata.Flags & 4) != 0)
            return Fail(X64FrameUnwindStatus.UnsupportedFrame);
        ulong infoAddress = imageBase + metadata.UnwindRva;
        uint infoBytes = 4U + (((uint)metadata.CodeSlots + 1) & ~1U) * 2;
        if (TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, infoAddress, infoBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata) != TypeManagerModuleStatus.Success)
            return Fail(X64FrameUnwindStatus.ModuleRejected);
        byte* info = (byte*)infoAddress;
        uint pcOffset = (uint)(input.Rip - imageBase - metadata.BeginRva);
        bool frameEstablished = false;
        // Lookup already validated every slot and operand. Reject machine frames
        // even when their dummy prolog operation would be skipped at this PC.
        for (uint i = 0; i < metadata.CodeSlots;)
        {
            byte* code = info + 4 + i * 2;
            byte op = (byte)(code[1] & 15), operand = (byte)(code[1] >> 4);
            if (op == 10)
                return Fail(X64FrameUnwindStatus.UnsupportedFrame);
            if (op == 3 && code[0] <= pcOffset)
                frameEstablished = true;
            i += Slots(op, operand);
        }

        EpilogOp* plan = stackalloc EpilogOp[MaximumEpilogInstructions];
        X64FrameUnwindStatus status = PlanEpilog((byte*)input.Rip, imageBase + metadata.EndRva - input.Rip, metadata.FrameRegister, plan, out int count);
        if (status != X64FrameUnwindStatus.Success)
            return Fail(status);
        bool epilog = count != 0;
        X64UnwindContext candidate = input;
        ulong frameBase = input.Rsp;
        // In a partial epilog the frame register may already have been popped.
        // Do not derive a stack address from that restored caller value.
        if (!epilog && frameEstablished)
        {
            if (!Add(input.GetInteger(metadata.FrameRegister), -(long)metadata.FrameOffset * 16, out frameBase))
                return Fail(X64FrameUnwindStatus.ArithmeticOverflow);
            if (!Position(start, stackBytes, frameBase))
                return Fail(X64FrameUnwindStatus.StackRangeRejected);
        }

        if (epilog)
        {
            for (int i = 0; i < count; i++)
            {
                EpilogOp operation = plan[i];
                if (operation.Kind == 3)
                {
                    if (!ReadStack(start, stackBytes, candidate.Rsp, 8, 8))
                        return Fail(X64FrameUnwindStatus.StackRangeRejected);
                    candidate.SetInteger(operation.Register, U64((byte*)candidate.Rsp));
                    candidate.Rsp += 8;
                }
                else if (operation.Kind != 4)
                {
                    ulong basis = operation.Kind == 1 ? candidate.Rsp : candidate.GetInteger(operation.Register);
                    if (!Add(basis, operation.Amount, out ulong next))
                        return Fail(X64FrameUnwindStatus.ArithmeticOverflow);
                    if (!Position(start, stackBytes, next))
                        return Fail(X64FrameUnwindStatus.StackRangeRejected);
                    candidate.Rsp = next;
                }
            }
        }
        else
        {
            status = UndoCodes(info, metadata.CodeSlots, pcOffset, frameBase, start, stackBytes, ref candidate);
            if (status != X64FrameUnwindStatus.Success)
                return Fail(status);
        }

        ulong returnSlot = candidate.Rsp;
        if (!ReadStack(start, stackBytes, returnSlot, 8, 8))
            return Fail(X64FrameUnwindStatus.StackRangeRejected);
        candidate.Rip = U64((byte*)returnSlot);
        candidate.Rsp += 8;
        if (candidate.Rsp <= input.Rsp)
            return Fail(X64FrameUnwindStatus.FrameTransitionRejected);
        if (TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, candidate.Rip, 1, TypeManagerRegisteredRangeKind.ExecutableEntry) != TypeManagerModuleStatus.Success)
            return Fail(X64FrameUnwindStatus.ReturnPcRejected);
        return new()
        {
            Status = X64FrameUnwindStatus.Success,
            Caller = candidate,
            ImageBase = imageBase,
            ModuleOwner = (ulong)moduleOwner,
            ModuleHandle = moduleHandle,
            FunctionIndex = metadata.FunctionIndex,
            // A partial epilog can already have restored the caller FP. No
            // handler establisher frame is supplied for an epilog result.
            EstablisherFrame = epilog ? 0 : frameBase,
            ReturnSlot = returnSlot,
            InEpilog = epilog
        };
    }

    private static X64FrameUnwindStatus UndoCodes(byte* info, uint count, uint pcOffset, ulong frameBase, ulong start, ulong bytes, ref X64UnwindContext context)
    {
        for (uint i = 0; i < count;)
        {
            byte* code = info + 4 + i * 2;
            byte op = (byte)(code[1] & 15), register = (byte)(code[1] >> 4);
            uint slots = Slots(op, register);
            i += slots;
            if (code[0] > pcOffset)
                continue;
            ulong next;
            if (op == 0)
            {
                if (!ReadStack(start, bytes, context.Rsp, 8, 8))
                    return X64FrameUnwindStatus.StackRangeRejected;
                context.SetInteger(register, U64((byte*)context.Rsp));
                context.Rsp += 8;
            }
            else if (op == 1 || op == 2 || op == 3)
            {
                long amount = op == 2 ? register * 8L + 8 : op == 3 ? 0 : register == 0 ? U16(code + 2) * 8L : U32(code + 2);
                // FrameBase belongs to the original callee context, even if a
                // preceding SAVE operation restored the caller's FP value.
                if (!Add(op == 3 ? frameBase : context.Rsp, amount, out next))
                    return X64FrameUnwindStatus.ArithmeticOverflow;
                if (!Position(start, bytes, next))
                    return X64FrameUnwindStatus.StackRangeRejected;
                context.Rsp = next;
            }
            else if (op == 4 || op == 5 || op == 8 || op == 9)
            {
                uint size = op <= 5 ? 8U : 16U;
                ulong offset = slots == 2 ? (ulong)U16(code + 2) * size : U32(code + 2);
                if (frameBase > ulong.MaxValue - offset)
                    return X64FrameUnwindStatus.ArithmeticOverflow;
                next = frameBase + offset;
                if (!ReadStack(start, bytes, next, size, size))
                    return X64FrameUnwindStatus.StackRangeRejected;
                if (size == 8)
                    context.SetInteger(register, U64((byte*)next));
                else
                    context.SetVector(register, new()
                    {
                        Low = U64((byte*)next),
                        High = U64((byte*)next + 8)
                    });
            }
            else
                return X64FrameUnwindStatus.UnsupportedFrame;
        }

        return X64FrameUnwindStatus.Success;
    }

    // A deliberately restricted v1 epilog decoder, not an x64 instruction
    // decoder. Recognize the entire remaining suffix before any stack reads.
    // Unsupported prefixes, tail transfers and incomplete candidates never
    // become body code. Noncandidate opcodes are conclusively not legal epilogs.
    private static X64FrameUnwindStatus PlanEpilog(byte* code, ulong available, byte frame, EpilogOp* plan, out int count)
    {
        count = 0;
        int cursor = 0;
        bool candidate = false;
        while (true)
        {
            if (count == MaximumEpilogInstructions)
                return X64FrameUnwindStatus.UnsupportedEpilog;
            int at = cursor;
            X64FrameUnwindStatus status = CodeSpan(available, at, 1);
            if (status != X64FrameUnwindStatus.Success)
                return status;
            byte opcode = code[at], rex = 0;
            if (opcode >= 0x40 && opcode <= 0x4F)
            {
                rex = opcode;
                at++;
                status = CodeSpan(available, at, 1);
                if (status != X64FrameUnwindStatus.Success)
                    return status;
                opcode = code[at];
                if (opcode >= 0x40 && opcode <= 0x4F)
                    return X64FrameUnwindStatus.UnsupportedEpilog;
            }

            if (!candidate && (opcode == 0x81 || opcode == 0x83 || opcode == 0x8D))
            {
                status = CodeSpan(available, at, 2);
                if (status != X64FrameUnwindStatus.Success)
                    return status;
                byte modrm = code[at + 1];
                bool add = opcode != 0x8D && modrm == 0xC4;
                bool lea = opcode == 0x8D && ((modrm >> 3) & 7) == 4 && (rex & 4) == 0;
                if (add || lea)
                {
                    if (add ? rex != 0x48 : (rex != 0x48 && rex != 0x49))
                        return X64FrameUnwindStatus.UnsupportedEpilog;
                    byte register = (byte)((modrm & 7) + ((rex & 1) << 3));
                    int displacement = add ? (opcode == 0x83 ? 1 : 4) : (modrm >> 6) == 1 ? 1 : (modrm >> 6) == 2 ? 4 : 0;
                    if (lea && (displacement == 0 || (modrm & 7) == 4 || frame == 0 || register != frame))
                        return X64FrameUnwindStatus.UnsupportedEpilog;
                    status = CodeSpan(available, at, 2 + displacement);
                    if (status != X64FrameUnwindStatus.Success)
                        return status;
                    long amount = displacement == 1 ? (sbyte)code[at + 2] : (int)U32(code + at + 2);
                    if (add && amount <= 0)
                        return X64FrameUnwindStatus.UnsupportedEpilog;
                    plan[count++] = new()
                    {
                        Kind = add ? (byte)1 : (byte)2,
                        Register = register,
                        Amount = amount
                    };
                    cursor = at + 2 + displacement;
                    candidate = true;
                    continue;
                }
            }

            if (opcode >= 0x58 && opcode <= 0x5F)
            {
                byte register = (byte)((opcode & 7) + ((rex & 1) << 3));
                if (!Nonvolatile(register))
                    return X64FrameUnwindStatus.UnsupportedEpilog;
                plan[count++] = new()
                {
                    Kind = 3,
                    Register = register
                };
                cursor = at + 1;
                candidate = true;
                continue;
            }

            if (opcode == 0xC3 && rex == 0)
            {
                plan[count++] = new()
                {
                    Kind = 4
                };
                return X64FrameUnwindStatus.Success;
            }

            if (opcode == 0xF3 && rex == 0)
            {
                status = CodeSpan(available, at, 2);
                if (status != X64FrameUnwindStatus.Success)
                    return status;
                if (code[at + 1] != 0xC3)
                    return X64FrameUnwindStatus.UnsupportedEpilog;
                plan[count++] = new()
                {
                    Kind = 4
                };
                return X64FrameUnwindStatus.Success;
            }

            // F2 is a pinned optional transfer prefix, deliberately unsupported.
            // Other legacy prefixes are rejected conservatively, including body
            // instructions using them. REX RET and RET imm16 are not admitted.
            if (opcode == 0xC2 || opcode == 0xC3 || opcode == 0xCA || opcode == 0xCB || opcode == 0xCF || opcode == 0xE9 || opcode == 0xEB || opcode == 0xF2 || opcode == 0xF3 || opcode == 0xF0 || opcode == 0x66 || opcode == 0x67 || opcode == 0x26 || opcode == 0x2E || opcode == 0x36 || opcode == 0x3E || opcode == 0x64 || opcode == 0x65)
                return X64FrameUnwindStatus.UnsupportedEpilog;
            if (opcode == 0xFF)
            {
                status = CodeSpan(available, at, 2);
                if (status != X64FrameUnwindStatus.Success)
                    return status;
                int group = (code[at + 1] >> 3) & 7;
                if (group == 4 || group == 5)
                    return X64FrameUnwindStatus.UnsupportedEpilog;
            }

            return candidate ? X64FrameUnwindStatus.UnsupportedEpilog : X64FrameUnwindStatus.Success;
        }
    }

    private static X64FrameUnwindStatus CodeSpan(ulong available, int offset, int bytes) => (ulong)(offset + bytes) > available ? X64FrameUnwindStatus.CodeRangeRejected : offset + bytes > MaximumEpilogBytes ? X64FrameUnwindStatus.UnsupportedEpilog : X64FrameUnwindStatus.Success;
    private static uint Slots(byte op, byte operand) => op == 1 ? (operand == 0 ? 2U : 3U) : op == 4 || op == 8 ? 2U : op == 5 || op == 9 ? 3U : 1U;
    private static bool Nonvolatile(byte r) => r == 3 || r == 5 || r == 6 || r == 7 || (r >= 12 && r <= 15);
    private static bool Add(ulong basis, long offset, out ulong value)
    {
        value = 0;
        if (offset >= 0)
        {
            if (basis > ulong.MaxValue - (ulong)offset)
                return false;
            value = basis + (ulong)offset;
        }
        else
        {
            ulong amount = (ulong)(-offset);
            if (basis < amount)
                return false;
            value = basis - amount;
        }

        return true;
    }

    private static bool Span(ulong start, ulong bytes) => start != 0 && bytes != 0 && start <= ulong.MaxValue - bytes;
    private static bool Position(ulong start, ulong bytes, ulong address) => (address & 7) == 0 && address >= start && address - start <= bytes;
    private static bool ReadStack(ulong start, ulong bytes, ulong address, uint length, uint alignment) => (address & (alignment - 1)) == 0 && address >= start && address - start <= bytes && length <= bytes - (address - start);
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a <= b ? b - a < an : a - b < bn;
    private static ushort U16(byte* p) => (ushort)(p[0] | p[1] << 8);
    private static uint U32(byte* p) => (uint)(p[0] | p[1] << 8 | p[2] << 16 | p[3] << 24);
    private static ulong U64(byte* p) => U32(p) | (ulong)U32(p + 4) << 32;
    private static X64FrameUnwindResult Fail(X64FrameUnwindStatus status) => new()
    {
        Status = status
    };
}
