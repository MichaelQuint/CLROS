namespace OS.Kernel.Platform;

public interface IPlatformConsoleSink
{
    bool Available { get; }

    void WriteLine(string line);
}

public sealed class GuardedPlatformConsole : IPlatformConsole
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformConsoleSink _sink;

    public GuardedPlatformConsole(IPlatformFirmware firmware, IPlatformConsoleSink sink)
    {
        _firmware = firmware;
        _sink = sink;
    }

    public PlatformConsoleResult WriteLine(string line)
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformConsoleOperations.OutputString);

        if (!guardResult.Succeeded)
        {
            return PlatformConsoleResult.FirmwareServicesUnavailable(guardResult);
        }

        PlatformConsoleResult result = _sink.Available
            ? PlatformConsoleResult.Written(guardResult, line)
            : PlatformConsoleResult.SinkUnavailable(guardResult, line);

        if (result.Succeeded)
        {
            _sink.WriteLine(line);
        }

        return result;
    }
}
