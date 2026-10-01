namespace System.Runtime.CompilerServices
{
    public enum MethodImplOptions
    {
        NoInlining = 0x0008,
        InternalCall = 0x1000,
    }

    public sealed class MethodImplAttribute : System.Attribute
    {
        public MethodImplAttribute(MethodImplOptions methodImplOptions)
        {
        }
    }
}
