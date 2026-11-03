internal static class ThreadLocalState
{
    [ThreadStatic]
    internal static int Value;
}
