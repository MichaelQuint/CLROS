namespace OS.Boot;

internal unsafe struct EfiConsoleAdapter
{
    private readonly EfiSimpleTextOutputProtocol* _console;
    private readonly EfiSerialIoProtocol* _serial;

    public EfiConsoleAdapter(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        _console = console;
        _serial = serial;
    }

    public bool HasSink
    {
        get
        {
            return _console != null || _serial != null;
        }
    }

    public void WriteByte(byte value)
    {
        if (_console != null)
        {
            EfiConsoleCharacter character;
            character.Value = value;
            character.NullTerminator = 0;
            _console->OutputString(_console, &character.Value);
        }

        if (_serial != null)
        {
            byte serialByte = value;
            ulong bytesToWrite = 1;
            _serial->Write(_serial, &bytesToWrite, &serialByte);
        }
    }

    public void WriteCrLf()
    {
        WriteByte(13);
        WriteByte(10);
    }
}
