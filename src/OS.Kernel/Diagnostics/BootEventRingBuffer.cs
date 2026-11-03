namespace OS.Kernel.Diagnostics;

public sealed class BootEventRingBuffer
{
    private readonly string[] _events;
    private int _next;
    private int _count;

    public BootEventRingBuffer()
        : this(64)
    {
    }

    public BootEventRingBuffer(int capacity)
    {
        if (capacity < 1)
        {
            capacity = 1;
        }

        _events = new string[capacity];
    }

    public int Count
    {
        get
        {
            return _count;
        }
    }

    public int Capacity
    {
        get
        {
            return _events.Length;
        }
    }

    public void Record(string message)
    {
        _events[_next] = message ?? string.Empty;
        _next = (_next + 1) % _events.Length;

        if (_count < _events.Length)
        {
            _count++;
        }
    }

    public string[] Snapshot()
    {
        string[] result = new string[_count];
        int start = (_next - _count + _events.Length) % _events.Length;

        for (int i = 0; i < _count; i++)
        {
            result[i] = _events[(start + i) % _events.Length];
        }

        return result;
    }
}
