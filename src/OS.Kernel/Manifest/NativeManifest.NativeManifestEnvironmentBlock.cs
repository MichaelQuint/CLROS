namespace OS.Manifest;
public struct NativeManifestEnvironmentBlock
{
    private NativeManifestEnvironmentEntry _entry0;
    private NativeManifestEnvironmentEntry _entry1;
    private NativeManifestEnvironmentEntry _entry2;
    private NativeManifestEnvironmentEntry _entry3;
    private NativeManifestEnvironmentEntry _entry4;
    private NativeManifestEnvironmentEntry _entry5;
    private NativeManifestEnvironmentEntry _entry6;
    private NativeManifestEnvironmentEntry _entry7;
    private NativeManifestEnvironmentEntry _entry8;
    private NativeManifestEnvironmentEntry _entry9;
    private NativeManifestEnvironmentEntry _entry10;
    private NativeManifestEnvironmentEntry _entry11;
    private NativeManifestEnvironmentEntry _entry12;
    private NativeManifestEnvironmentEntry _entry13;
    private NativeManifestEnvironmentEntry _entry14;
    private NativeManifestEnvironmentEntry _entry15;
    public int Count { get; private set; }

    public bool TryAdd(NativeManifestEnvironmentEntry entry)
    {
        if (Count >= NativeManifestParser.MaximumEnvironmentCount)
        {
            return false;
        }

        SetAt(Count, entry);
        Count++;
        return true;
    }

    public NativeManifestEnvironmentEntry GetAt(int index)
    {
        return index switch
        {
            0 => _entry0,
            1 => _entry1,
            2 => _entry2,
            3 => _entry3,
            4 => _entry4,
            5 => _entry5,
            6 => _entry6,
            7 => _entry7,
            8 => _entry8,
            9 => _entry9,
            10 => _entry10,
            11 => _entry11,
            12 => _entry12,
            13 => _entry13,
            14 => _entry14,
            15 => _entry15,
            _ => default
        };
    }

    private void SetAt(int index, NativeManifestEnvironmentEntry entry)
    {
        switch (index)
        {
            case 0:
                _entry0 = entry;
                break;
            case 1:
                _entry1 = entry;
                break;
            case 2:
                _entry2 = entry;
                break;
            case 3:
                _entry3 = entry;
                break;
            case 4:
                _entry4 = entry;
                break;
            case 5:
                _entry5 = entry;
                break;
            case 6:
                _entry6 = entry;
                break;
            case 7:
                _entry7 = entry;
                break;
            case 8:
                _entry8 = entry;
                break;
            case 9:
                _entry9 = entry;
                break;
            case 10:
                _entry10 = entry;
                break;
            case 11:
                _entry11 = entry;
                break;
            case 12:
                _entry12 = entry;
                break;
            case 13:
                _entry13 = entry;
                break;
            case 14:
                _entry14 = entry;
                break;
            case 15:
                _entry15 = entry;
                break;
        }
    }
}
