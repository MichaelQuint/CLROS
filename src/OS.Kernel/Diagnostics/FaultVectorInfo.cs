namespace OS.Kernel.Diagnostics;

public readonly struct FaultVectorInfo
{
    public FaultVectorInfo(
        byte vector,
        string mnemonic,
        string name,
        bool pushesErrorCode,
        string errorCodeMeaning,
        string faultAddressRegister)
    {
        Vector = vector;
        Mnemonic = mnemonic;
        Name = name;
        PushesErrorCode = pushesErrorCode;
        ErrorCodeMeaning = errorCodeMeaning;
        FaultAddressRegister = faultAddressRegister;
    }

    public byte Vector { get; }

    public string Mnemonic { get; }

    public string Name { get; }

    public bool PushesErrorCode { get; }

    public string ErrorCodeMeaning { get; }

    public string FaultAddressRegister { get; }

    public bool HasFaultAddressRegister
    {
        get { return FaultAddressRegister.Length != 0; }
    }
}

public static partial class FaultVectorCatalog
{
    public const byte DivideError = 0;
    public const byte Debug = 1;
    public const byte NonMaskableInterrupt = 2;
    public const byte Breakpoint = 3;
    public const byte Overflow = 4;
    public const byte BoundRangeExceeded = 5;
    public const byte DeviceNotAvailable = 7;
    public const byte DoubleFault = 8;
    public const byte CoprocessorSegmentOverrun = 9;
    public const byte InvalidTss = 10;
    public const byte SegmentNotPresent = 11;
    public const byte StackSegmentFault = 12;
    public const byte GeneralProtectionFault = 13;
    public const byte PageFault = 14;
    public const byte X87FloatingPoint = 16;
    public const byte AlignmentCheck = 17;
    public const byte MachineCheck = 18;
    public const byte SimdFloatingPoint = 19;
    public const byte VirtualizationException = 20;
    public const byte ControlProtectionException = 21;
    public const byte SecurityException = 30;

    public static bool TryGet(byte vector, out FaultVectorInfo info)
    {
        switch (vector)
        {
            case DivideError:
                info = Create(vector, "#DE", "Divide Error", false, string.Empty, string.Empty);
                return true;
            case Debug:
                info = Create(vector, "#DB", "Debug", false, string.Empty, string.Empty);
                return true;
            case NonMaskableInterrupt:
                info = Create(vector, "NMI", "Non-Maskable Interrupt", false, string.Empty, string.Empty);
                return true;
            case Breakpoint:
                info = Create(vector, "#BP", "Breakpoint", false, string.Empty, string.Empty);
                return true;
            case Overflow:
                info = Create(vector, "#OF", "Overflow", false, string.Empty, string.Empty);
                return true;
            case BoundRangeExceeded:
                info = Create(vector, "#BR", "BOUND Range Exceeded", false, string.Empty, string.Empty);
                return true;
            case InvalidOpcode:
                info = Create(vector, "#UD", "Invalid Opcode", false, string.Empty, string.Empty);
                return true;
            case DeviceNotAvailable:
                info = Create(vector, "#NM", "Device Not Available", false, string.Empty, string.Empty);
                return true;
            case DoubleFault:
                info = Create(vector, "#DF", "Double Fault", true, "always-zero", string.Empty);
                return true;
            case CoprocessorSegmentOverrun:
                info = Create(vector, "#CSO", "Coprocessor Segment Overrun", false, string.Empty, string.Empty);
                return true;
            case InvalidTss:
                info = Create(vector, "#TS", "Invalid TSS", true, "selector", string.Empty);
                return true;
            case SegmentNotPresent:
                info = Create(vector, "#NP", "Segment Not Present", true, "selector", string.Empty);
                return true;
            case StackSegmentFault:
                info = Create(vector, "#SS", "Stack-Segment Fault", true, "selector", string.Empty);
                return true;
            case GeneralProtectionFault:
                info = Create(vector, "#GP", "General Protection Fault", true, "selector", string.Empty);
                return true;
            case PageFault:
                info = Create(vector, "#PF", "Page Fault", true, "page-fault", "CR2");
                return true;
            case X87FloatingPoint:
                info = Create(vector, "#MF", "x87 Floating-Point Exception", false, string.Empty, string.Empty);
                return true;
            case AlignmentCheck:
                info = Create(vector, "#AC", "Alignment Check", true, "always-zero", string.Empty);
                return true;
            case MachineCheck:
                info = Create(vector, "#MC", "Machine Check", false, string.Empty, string.Empty);
                return true;
            case SimdFloatingPoint:
                info = Create(vector, "#XM", "SIMD Floating-Point Exception", false, string.Empty, string.Empty);
                return true;
            case VirtualizationException:
                info = Create(vector, "#VE", "Virtualization Exception", false, string.Empty, string.Empty);
                return true;
            case ControlProtectionException:
                info = Create(vector, "#CP", "Control Protection Exception", true, "control-protection", string.Empty);
                return true;
            case SecurityException:
                info = Create(vector, "#SX", "Security Exception", true, "security", string.Empty);
                return true;
            default:
                info = default;
                return false;
        }
    }

    private static FaultVectorInfo Create(
        byte vector,
        string mnemonic,
        string name,
        bool pushesErrorCode,
        string errorCodeMeaning,
        string faultAddressRegister)
    {
        return new FaultVectorInfo(
            vector,
            mnemonic,
            name,
            pushesErrorCode,
            errorCodeMeaning,
            faultAddressRegister);
    }
}
