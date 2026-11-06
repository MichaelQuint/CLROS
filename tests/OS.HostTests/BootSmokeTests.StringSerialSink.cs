using OS.Boot;
using OS.Kernel.Diagnostics;

sealed class StringSerialSink : ISerialSink
{
    private readonly List<string> _lines = new List<string>();
    public void WriteLine(string line)
    {
        _lines.Add(line);
    }

    public bool Contains(string line)
    {
        return _lines.Contains(line);
    }
}
