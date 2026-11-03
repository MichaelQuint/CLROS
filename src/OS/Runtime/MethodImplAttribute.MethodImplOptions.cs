namespace System.Runtime.CompilerServices
{
    public enum MethodImplOptions
    {
        NoInlining = 0x0008,
        /// <summary>Requests compiler inlining; emitted code must establish the actual stack shape.</summary>
        AggressiveInlining = 0x0100,
        InternalCall = 0x1000,
    }
}
