using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>Values only, not a Windows CONTEXT overlay or live register capture.</summary>

public struct X64UnwindContext
{
    public ulong Rip, Rsp, Rbx, Rbp, Rsi, Rdi, R12, R13, R14, R15;
    public X64UnwindVector Xmm6, Xmm7, Xmm8, Xmm9, Xmm10, Xmm11, Xmm12, Xmm13, Xmm14, Xmm15;
    public readonly ulong GetInteger(byte register) => register switch
    {
        3 => Rbx,
        5 => Rbp,
        6 => Rsi,
        7 => Rdi,
        12 => R12,
        13 => R13,
        14 => R14,
        15 => R15,
        _ => 0
    };
    public void SetInteger(byte register, ulong value)
    {
        switch (register)
        {
            case 3:
                Rbx = value;
                break;
            case 5:
                Rbp = value;
                break;
            case 6:
                Rsi = value;
                break;
            case 7:
                Rdi = value;
                break;
            case 12:
                R12 = value;
                break;
            case 13:
                R13 = value;
                break;
            case 14:
                R14 = value;
                break;
            case 15:
                R15 = value;
                break;
        }
    }

    public readonly X64UnwindVector GetVector(byte register) => register switch
    {
        6 => Xmm6,
        7 => Xmm7,
        8 => Xmm8,
        9 => Xmm9,
        10 => Xmm10,
        11 => Xmm11,
        12 => Xmm12,
        13 => Xmm13,
        14 => Xmm14,
        15 => Xmm15,
        _ => default
    };
    public void SetVector(byte register, X64UnwindVector value)
    {
        switch (register)
        {
            case 6:
                Xmm6 = value;
                break;
            case 7:
                Xmm7 = value;
                break;
            case 8:
                Xmm8 = value;
                break;
            case 9:
                Xmm9 = value;
                break;
            case 10:
                Xmm10 = value;
                break;
            case 11:
                Xmm11 = value;
                break;
            case 12:
                Xmm12 = value;
                break;
            case 13:
                Xmm13 = value;
                break;
            case 14:
                Xmm14 = value;
                break;
            case 15:
                Xmm15 = value;
                break;
        }
    }
}
