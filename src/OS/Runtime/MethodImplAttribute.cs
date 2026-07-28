namespace System.Runtime.CompilerServices
{
    public enum MethodImplOptions
    {
        InternalCall = 0x1000,
    }

    public sealed class MethodImplAttribute : System.Attribute
    {
        public MethodImplAttribute(MethodImplOptions methodImplOptions)
        {
        }
    }
}
