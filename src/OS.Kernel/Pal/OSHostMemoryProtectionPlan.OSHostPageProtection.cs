namespace OS.Pal;
[Flags]
public enum OSHostPageProtection : ulong
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
