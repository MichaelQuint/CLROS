namespace OS.Drivers;
public static partial class NvmeRegisterMap
{
    public static class Version
    {
        public const uint Offset = 0x0008;
        public const uint Width = sizeof(uint);
    }
}
