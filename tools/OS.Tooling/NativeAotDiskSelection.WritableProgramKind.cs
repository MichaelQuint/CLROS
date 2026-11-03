namespace OS.Tooling;

public static partial class NativeAotDiskSelection
{
    /// <summary>Exact copied target policy, without package, index or execution authority.</summary>

    public enum WritableProgramKind : byte
    {
        Mechanical30,
        LogUpload40,
        BclWrite,
        BclVerify
    }
}
