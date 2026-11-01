namespace OS.Kernel.Diagnostics;
public readonly struct FaultVectorInfo
{
    public FaultVectorInfo(byte vector, string mnemonic, string name, bool pushesErrorCode, string errorCodeMeaning, string faultAddressRegister)
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
        get
        {
            return FaultAddressRegister.Length != 0;
        }
    }
}
