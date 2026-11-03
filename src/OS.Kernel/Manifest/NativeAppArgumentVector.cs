namespace OS.Manifest;

public struct NativeAppArgumentVector
{
    private NativeAppArgument _argument0;
    private NativeAppArgument _argument1;
    private NativeAppArgument _argument2;
    private NativeAppArgument _argument3;
    private NativeAppArgument _argument4;
    private NativeAppArgument _argument5;
    private NativeAppArgument _argument6;
    private NativeAppArgument _argument7;
    private NativeAppArgument _argument8;
    private NativeAppArgument _argument9;
    private NativeAppArgument _argument10;
    private NativeAppArgument _argument11;
    private NativeAppArgument _argument12;
    private NativeAppArgument _argument13;
    private NativeAppArgument _argument14;
    private NativeAppArgument _argument15;
    public int Count
    {
        get; private set;
    }

    public bool TryAdd(NativeAppArgument argument)
    {
        if (Count >= NativeAppArgumentVectorPlanner.MaximumArgumentCount)
        {
            return false;
        }

        SetAt(Count, argument);
        Count++;
        return true;
    }

    public NativeAppArgument GetAt(int index)
    {
        return index switch
        {
            0 => _argument0,
            1 => _argument1,
            2 => _argument2,
            3 => _argument3,
            4 => _argument4,
            5 => _argument5,
            6 => _argument6,
            7 => _argument7,
            8 => _argument8,
            9 => _argument9,
            10 => _argument10,
            11 => _argument11,
            12 => _argument12,
            13 => _argument13,
            14 => _argument14,
            15 => _argument15,
            _ => default
        };
    }

    private void SetAt(int index, NativeAppArgument argument)
    {
        switch (index)
        {
            case 0:
                _argument0 = argument;
                break;
            case 1:
                _argument1 = argument;
                break;
            case 2:
                _argument2 = argument;
                break;
            case 3:
                _argument3 = argument;
                break;
            case 4:
                _argument4 = argument;
                break;
            case 5:
                _argument5 = argument;
                break;
            case 6:
                _argument6 = argument;
                break;
            case 7:
                _argument7 = argument;
                break;
            case 8:
                _argument8 = argument;
                break;
            case 9:
                _argument9 = argument;
                break;
            case 10:
                _argument10 = argument;
                break;
            case 11:
                _argument11 = argument;
                break;
            case 12:
                _argument12 = argument;
                break;
            case 13:
                _argument13 = argument;
                break;
            case 14:
                _argument14 = argument;
                break;
            case 15:
                _argument15 = argument;
                break;
        }
    }
}
