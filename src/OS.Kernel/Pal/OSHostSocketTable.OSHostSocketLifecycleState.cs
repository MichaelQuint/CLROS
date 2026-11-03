namespace OS.Pal;

public enum OSHostSocketLifecycleState : uint
{
    Closed = 0,
    Created = 1,
    Bound = 2,
    Listening = 3,
    Connected = 4
}
