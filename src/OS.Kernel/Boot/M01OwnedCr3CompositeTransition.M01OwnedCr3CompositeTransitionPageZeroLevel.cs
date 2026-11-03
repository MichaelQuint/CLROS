namespace OS.Boot;

public enum M01OwnedCr3CompositeTransitionPageZeroLevel : byte
{
    Uninitialized = 0,
    AbsentAtPml4 = 1,
    AbsentAtPdpt = 2,
    AbsentAtPd = 3,
    AbsentAtPt = 4,
    Present = 5,
    Malformed = 6
}
