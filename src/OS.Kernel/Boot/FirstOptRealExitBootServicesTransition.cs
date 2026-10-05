using OS.Kernel.Platform;

namespace OS.Boot;

public enum FirstExitPhase : uint
{
    Empty, InFlight, Failed, OwnedContinuation, UnexpectedContinuationReturn
}

public enum FirstExitAdoptionPhase : uint
{
    Empty, Prepared, ExitStateCheck, ProcessorCheck, StackCheck, CanaryBeforeCheck,
    Cr3ReadBackCheck, Cr4ReadBackCheck, CanaryAfterCheck, OwnedDiagnostic,
    UnexpectedDiagnosticReturn
}

// Fixed x64 layout consumed by FirstExitPermanentCr3Stub. Retain the original
// context exclusively; do not change inputs after preparation or reset Phase.
public unsafe struct FirstExitPermanentCr3Context
{
    public ulong FirmwareCr3, OwnedCr3, FirmwareCr4;
    public ulong StackBottom, StackTop, MinimumRsp;
    public ulong LowerCanary, UpperCanary;
    public delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> Diagnostic;
    public void* DiagnosticContext;
    public ulong ObservedCr3, ObservedCr4;
    public FirstExitAdoptionPhase Phase;
    internal uint Reserved;
    public ulong ObservedRsp;
}

/// <summary>
/// Emits the terminal Win64 continuation passed to AttemptAndTransfer. It uses
/// the current, already-owned stack, checks successful exit and the exact CPU
/// state, clears PGE, loads the owned root, checks readback and both canaries,
/// then calls the owned diagnostic. Every explicit refusal and unexpected return halts;
/// there is no RET, firmware callback, CR3 restoration or success output here.
///
/// Preparation accepts only low identity-mapped addresses and roots below 4 GiB,
/// four-level/non-PCID paging and CR4 bits 0..10 with PAE set. The adapter must
/// prove all actual mappings and CPU support, authenticate Diagnostic, and have
/// owned GDT/TSS/IDT/IST, panic state and COM1 ready before the final map/exit.
/// Descriptor state must be installed and read-back validated before stub entry.
/// CLI does not mask NMI or exceptions. Descriptor installation/readback and
/// essential-map validation remain adapter obligations, not claims of this stub.
/// The declared diagnostic stack bound must include its entire reachable graph.
/// </summary>
public static unsafe class FirstExitPermanentCr3Stub
{
    public const uint ContextBytes = 112;
    // 56-byte call frame (32-byte shadow space, retained context, alignment)
    // plus the diagnostic call's eight-byte return address.
    public const ulong CallStackBytes = 64;
    private const ulong LowAddressLimit = 0x0000800000000000;

    public static bool Prepare(ref FirstExitPermanentCr3Context context,
        ulong firmwareCr3, ulong ownedCr3, ulong firmwareCr4,
        ulong stackBottom, ulong stackTop, ulong diagnosticStackBytes,
        ulong lowerCanary, ulong upperCanary,
        delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> diagnostic,
        void* diagnosticContext)
    {
        if (context.Phase != FirstExitAdoptionPhase.Empty || sizeof(FirstExitPermanentCr3Context) != ContextBytes ||
            (firmwareCr3 & ~0x18UL) == 0 || firmwareCr3 >= 0x100000000 || (firmwareCr3 & 0xfe7) != 0 ||
            ownedCr3 == 0 || ownedCr3 >= 0x100000000 || (ownedCr3 & 0xfff) != 0 ||
            ownedCr3 == (firmwareCr3 & ~0xfffUL) ||
            (firmwareCr4 & ~0x7ffUL) != 0 || (firmwareCr4 & 0x20) == 0 ||
            stackBottom < 8 || stackTop >= LowAddressLimit - 8 || stackTop <= stackBottom ||
            (stackBottom & 7) != 0 || (stackTop & 7) != 0 ||
            diagnosticStackBytes == 0 || diagnosticStackBytes >= stackTop - stackBottom ||
            stackTop - stackBottom - diagnosticStackBytes <= CallStackBytes ||
            lowerCanary == 0 || upperCanary == 0 || diagnostic == null ||
            (ulong)diagnostic >= LowAddressLimit)
            return false;

        fixed (FirstExitPermanentCr3Context* address = &context)
        {
            ulong start = (ulong)address;
            if (start == 0 || start >= LowAddressLimit - ContextBytes ||
                (start < stackTop + 8 && stackBottom - 8 < start + ContextBytes))
                return false;
        }

        context = default;
        context.FirmwareCr3 = firmwareCr3;
        context.OwnedCr3 = ownedCr3;
        context.FirmwareCr4 = firmwareCr4;
        context.StackBottom = stackBottom;
        context.StackTop = stackTop;
        context.MinimumRsp = stackBottom + diagnosticStackBytes + CallStackBytes;
        context.LowerCanary = lowerCanary;
        context.UpperCanary = upperCanary;
        context.Diagnostic = diagnostic;
        context.DiagnosticContext = diagnosticContext;
        context.Phase = FirstExitAdoptionPhase.Prepared;
        return true;
    }

    public static uint RequiredBytes => Encode(null, 0);

    public static bool Emit(byte* destination, uint capacity, out uint written)
    {
        written = 0;
        uint bytes = RequiredBytes;
        if (destination == null || capacity < bytes ||
            (ulong)destination >= LowAddressLimit - bytes)
            return false;
        written = Encode(destination, bytes - 4);
        return true;
    }

    private static uint Encode(byte* destination, uint halt)
    {
        uint p = 0;
        Op(destination, ref p, 0xfcfa, 2);              // cli; cld
        Op(destination, ref p, 0xd28548, 3);            // test rdx,rdx
        StopIf(destination, ref p, halt, 0x84);         // jz halt
        Op(destination, ref p, 0xc98548, 3);            // test rcx,rcx
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x01607a83, 4);          // cmp dword [rdx+96],Prepared
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.ExitStateCheck);
        Op(destination, ref p, 0x033983, 3);            // cmp dword [rcx],OwnedContinuation
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x02047983, 4);          // cmp dword [rcx+4],BootServicesExited
        StopIf(destination, ref p, halt, 0x85);

        Phase(destination, ref p, FirstExitAdoptionPhase.ProcessorCheck);
        Op(destination, ref p, 0xd8200f, 3);            // mov rax,cr3
        Op(destination, ref p, 0x023b48, 3);            // cmp rax,[rdx]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0x10423b48, 4);          // cmp rax,[rdx+16]
        StopIf(destination, ref p, halt, 0x85);

        Phase(destination, ref p, FirstExitAdoptionPhase.StackCheck);
        Op(destination, ref p, 0x68628948, 4);          // mov [rdx+104],rsp
        Op(destination, ref p, 0xe08948, 3);            // mov rax,rsp
        Op(destination, ref p, 0x0fe083, 3);            // and eax,15
        Op(destination, ref p, 0x08f883, 3);            // cmp eax,8 (Win64 entry alignment)
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x28623b48, 4);          // cmp rsp,[rdx+40]
        StopIf(destination, ref p, halt, 0x82);         // jb halt
        Op(destination, ref p, 0x20623b48, 4);          // cmp rsp,[rdx+32]
        StopIf(destination, ref p, halt, 0x83);         // jae halt
        Phase(destination, ref p, FirstExitAdoptionPhase.CanaryBeforeCheck);
        Canaries(destination, ref p, halt);

        // No call or output before permanent CR3 adoption. PGE stays cleared:
        // flushing it prevents firmware global translations surviving the load.
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0xffffff7f2548, 6);      // and rax,~0x80
        Op(destination, ref p, 0xe0220f, 3);            // mov cr4,rax
        Op(destination, ref p, 0x08428b48, 4);          // mov rax,[rdx+8]
        Op(destination, ref p, 0xd8220f, 3);            // mov cr3,rax (only CR3 write)
        Phase(destination, ref p, FirstExitAdoptionPhase.Cr3ReadBackCheck);
        Op(destination, ref p, 0xd8200f, 3);            // mov rax,cr3
        Op(destination, ref p, 0x50428948, 4);          // mov [rdx+80],rax
        Op(destination, ref p, 0x08423b48, 4);          // cmp rax,[rdx+8]
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.Cr4ReadBackCheck);
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0x58428948, 4);          // mov [rdx+88],rax
        Op(destination, ref p, 0x10428b4c, 4);          // mov r8,[rdx+16]
        Op(destination, ref p, 0xffffff7fe08149, 7);    // and r8,~0x80
        Op(destination, ref p, 0xc0394c, 3);            // cmp rax,r8
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.CanaryAfterCheck);
        Canaries(destination, ref p, halt);

        Phase(destination, ref p, FirstExitAdoptionPhase.OwnedDiagnostic);
        Op(destination, ref p, 0x38ec8348, 4);          // sub rsp,56 (shadow/context/alignment)
        Op(destination, ref p, 0x2024548948, 5);        // mov [rsp+32],rdx
        Op(destination, ref p, 0x40428b48, 4);          // mov rax,[rdx+64]
        Op(destination, ref p, 0xd0ff, 2);              // call rax (owned diagnostic only)
        Op(destination, ref p, 0x2024548b48, 5);        // mov rdx,[rsp+32]
        Phase(destination, ref p, FirstExitAdoptionPhase.UnexpectedDiagnosticReturn);
        Op(destination, ref p, 0xfcebf4fa, 4);          // halt: cli; hlt; jmp halt
        return p;
    }

    private static void Canaries(byte* destination, ref uint p, uint halt)
    {
        Op(destination, ref p, 0x18428b48, 4);          // mov rax,[rdx+24]
        Op(destination, ref p, 0xf8408b48, 4);          // mov rax,[rax-8]
        Op(destination, ref p, 0x30423b48, 4);          // cmp rax,[rdx+48]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x20428b48, 4);          // mov rax,[rdx+32]
        Op(destination, ref p, 0x008b48, 3);            // mov rax,[rax]
        Op(destination, ref p, 0x38423b48, 4);          // cmp rax,[rdx+56]
        StopIf(destination, ref p, halt, 0x85);
    }

    private static void Phase(byte* destination, ref uint p, FirstExitAdoptionPhase phase)
    {
        Op(destination, ref p, 0x6042c7, 3);            // mov dword [rdx+96],phase
        Op(destination, ref p, (uint)phase, 4);
    }

    private static void StopIf(byte* destination, ref uint p, uint halt, byte condition)
    {
        Op(destination, ref p, 0x0f | ((ulong)condition << 8), 2);
        Op(destination, ref p, unchecked(halt - p - 4), 4);
    }

    // Fixed x64 instruction encoding, also used without a destination to measure.
    private static void Op(byte* destination, ref uint p, ulong instruction, uint bytes)
    {
        for (uint i = 0; i < bytes; i++)
        {
            if (destination != null) destination[p] = (byte)(instruction >> (int)(i * 8));
            p++;
        }
    }
}

// Every returned value is a refusal/failure. Successful exit never returns.
public enum FirstExitReturn
{
    NotStarted, ReentryRejected, AlreadyConsumed, FirmwareUnavailable,
    InvalidContinuation, InvalidStorage, ExitNotCompleted
}

public unsafe struct FirstExitTransitionState
{
    internal FirstExitPhase PhaseValue;
    internal FirmwareServicesMode ModeValue;
    internal BoundedExitRetryState Retry;
    internal BoundedExitRetryResult AttemptValue;

    public FirstExitPhase Phase => PhaseValue;
    public FirmwareServicesMode Mode => ModeValue;
    public BoundedExitRetryResult Attempt => AttemptValue;
    public bool RetainedFirmwareCallbacksCleared =>
        Retry.First.Exit.Function == null && Retry.First.Exit.Handle == null &&
        Retry.Retry.Exit.Function == null && Retry.Retry.Exit.Handle == null;
}

/// <summary>
/// One terminal handoff using the accepted final-map/two-attempt retry core.
/// A successful exit clears this coordinator's retained firmware callbacks and
/// transfers to the original owned continuation; it cannot return to an EFI
/// caller. An unexpected continuation return spins without calling anything.
///
/// Before entry, the adapter must authenticate the firmware pointers and owned
/// continuation, prepare all mappings/descriptors/COM1/stack/canaries, and prove
/// that state, map buffer, continuation code and context survive permanent CR3
/// adoption. The continuation must adopt that hierarchy before any output or
/// other callback and terminate using owned facilities. This coordinator does
/// not load CR3, authenticate pointers, or revoke independently copied handles.
///
/// Retain one zero-initialized state; never copy/reset it. Access is exclusive
/// and callbacks must not mutate its storage or throw. Returned failures leave
/// the last attempt available for owned diagnostics; after an issued exit call
/// the adapter must not resume ordinary firmware work even if exit failed.
/// </summary>
public static unsafe class FirstOptRealExitBootServicesTransition
{
    public static FirstExitReturn AttemptAndTransfer(ref FirstExitTransitionState state,
        FirmwareServicesMode mode,
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        delegate* unmanaged<void*, ulong, ulong> exitBootServices,
        void* imageHandle, byte* ownedMap, ulong mapCapacity,
        delegate* unmanaged<FirstExitTransitionState*, void*, void> ownedContinuation,
        void* ownedContext, ulong contextBytes)
    {
        if (state.PhaseValue == FirstExitPhase.InFlight)
            return FirstExitReturn.ReentryRejected;
        if (state.PhaseValue != FirstExitPhase.Empty)
            return FirstExitReturn.AlreadyConsumed;
        if (mode != FirmwareServicesMode.BootServicesAvailable)
            return FirstExitReturn.FirmwareUnavailable;
        if (ownedContinuation == null)
            return FirstExitReturn.InvalidContinuation;

        ulong map = (ulong)ownedMap, context = (ulong)ownedContext;
        if (!Extent(map, mapCapacity) || mapCapacity < UefiMemoryMapCopyPlan.MinimumDescriptorSize ||
            !Extent(context, contextBytes) || Overlap(map, mapCapacity, context, contextBytes))
            return FirstExitReturn.InvalidStorage;

        fixed (FirstExitTransitionState* address = &state)
        {
            ulong stateStart = (ulong)address, stateBytes = (ulong)sizeof(FirstExitTransitionState);
            if (!Extent(stateStart, stateBytes) ||
                Overlap(stateStart, stateBytes, map, mapCapacity) ||
                Overlap(stateStart, stateBytes, context, contextBytes))
                return FirstExitReturn.InvalidStorage;

            state.PhaseValue = FirstExitPhase.InFlight;
            state.ModeValue = mode;
            BoundedExitRetryResult attempt = BoundedRetryStaleKeyStatus.Execute(ref state.Retry,
                mode, getMemoryMap, exitBootServices, imageHandle, ownedMap, mapCapacity);
            // No logging, allocation or firmware operation follows a successful exit.
            if (attempt.Succeeded)
                state.ModeValue = FirmwareServicesMode.BootServicesExited;
            state.AttemptValue = attempt;
            state.Retry = default;
            if (!attempt.Succeeded)
            {
                state.PhaseValue = FirstExitPhase.Failed;
                return FirstExitReturn.ExitNotCompleted;
            }

            state.PhaseValue = FirstExitPhase.OwnedContinuation;
            ownedContinuation(address, ownedContext);
            state.PhaseValue = FirstExitPhase.UnexpectedContinuationReturn;
            while (true) { }
        }
    }

    private static bool Extent(ulong start, ulong bytes) =>
        start != 0 && bytes != 0 && bytes <= UefiMemoryMapCopyPlan.MaximumUnsignedLong - start;

    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) =>
        a < b + bn && b < a + an;
}
