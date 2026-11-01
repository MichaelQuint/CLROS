namespace OS.Kernel.Platform;
public interface IPlatformConsoleSink
{
    bool Available { get; }

    void WriteLine(string line);
}
