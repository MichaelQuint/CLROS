namespace OS.Boot;

public static class BootSequencePlan
{
    public const int NonSmokeLineCount = 5;

    public const int LineCount = 8;

    public static int GetLineLength(int lineIndex)
    {
        return lineIndex switch
        {
            0 => 24,
            1 => 29,
            2 => 28,
            3 => 35,
            4 => 34,
            5 => 10,
            6 => 23,
            7 => 13,
            _ => 0
        };
    }

    public static byte GetLineByte(int lineIndex, int offset)
    {
        return lineIndex switch
        {
            0 => GetTestStartBootSequenceByte(offset),
            1 => GetPhaseAAuditStartByte(offset),
            2 => GetPhaseAAuditPassByte(offset),
            3 => GetPhaseADiagnosticsStartByte(offset),
            4 => GetPhaseADiagnosticsPassByte(offset),
            5 => GetOsBootOkByte(offset),
            6 => GetTestPassBootSequenceByte(offset),
            7 => GetOsSmokePassByte(offset),
            _ => 0
        };
    }

    private static byte GetTestStartBootSequenceByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'T',
            1 => (byte)'E',
            2 => (byte)'S',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'S',
            6 => (byte)'T',
            7 => (byte)'A',
            8 => (byte)'R',
            9 => (byte)'T',
            10 => (byte)' ',
            11 => (byte)'b',
            12 => (byte)'o',
            13 => (byte)'o',
            14 => (byte)'t',
            15 => (byte)'.',
            16 => (byte)'s',
            17 => (byte)'e',
            18 => (byte)'q',
            19 => (byte)'u',
            20 => (byte)'e',
            21 => (byte)'n',
            22 => (byte)'c',
            23 => (byte)'e',
            _ => 0
        };
    }

    private static byte GetPhaseAAuditStartByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'B',
            1 => (byte)'O',
            2 => (byte)'O',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'P',
            6 => (byte)'H',
            7 => (byte)'A',
            8 => (byte)'S',
            9 => (byte)'E',
            10 => (byte)' ',
            11 => (byte)'P',
            12 => (byte)'h',
            13 => (byte)'a',
            14 => (byte)'s',
            15 => (byte)'e',
            16 => (byte)'A',
            17 => (byte)'.',
            18 => (byte)'A',
            19 => (byte)'u',
            20 => (byte)'d',
            21 => (byte)'i',
            22 => (byte)'t',
            23 => (byte)' ',
            24 => (byte)'S',
            25 => (byte)'T',
            26 => (byte)'A',
            27 => (byte)'R',
            28 => (byte)'T',
            _ => 0
        };
    }

    private static byte GetPhaseAAuditPassByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'B',
            1 => (byte)'O',
            2 => (byte)'O',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'P',
            6 => (byte)'H',
            7 => (byte)'A',
            8 => (byte)'S',
            9 => (byte)'E',
            10 => (byte)' ',
            11 => (byte)'P',
            12 => (byte)'h',
            13 => (byte)'a',
            14 => (byte)'s',
            15 => (byte)'e',
            16 => (byte)'A',
            17 => (byte)'.',
            18 => (byte)'A',
            19 => (byte)'u',
            20 => (byte)'d',
            21 => (byte)'i',
            22 => (byte)'t',
            23 => (byte)' ',
            24 => (byte)'P',
            25 => (byte)'A',
            26 => (byte)'S',
            27 => (byte)'S',
            _ => 0
        };
    }

    private static byte GetPhaseADiagnosticsStartByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'B',
            1 => (byte)'O',
            2 => (byte)'O',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'P',
            6 => (byte)'H',
            7 => (byte)'A',
            8 => (byte)'S',
            9 => (byte)'E',
            10 => (byte)' ',
            11 => (byte)'P',
            12 => (byte)'h',
            13 => (byte)'a',
            14 => (byte)'s',
            15 => (byte)'e',
            16 => (byte)'A',
            17 => (byte)'.',
            18 => (byte)'D',
            19 => (byte)'i',
            20 => (byte)'a',
            21 => (byte)'g',
            22 => (byte)'n',
            23 => (byte)'o',
            24 => (byte)'s',
            25 => (byte)'t',
            26 => (byte)'i',
            27 => (byte)'c',
            28 => (byte)'s',
            29 => (byte)' ',
            30 => (byte)'S',
            31 => (byte)'T',
            32 => (byte)'A',
            33 => (byte)'R',
            34 => (byte)'T',
            _ => 0
        };
    }

    private static byte GetPhaseADiagnosticsPassByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'B',
            1 => (byte)'O',
            2 => (byte)'O',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'P',
            6 => (byte)'H',
            7 => (byte)'A',
            8 => (byte)'S',
            9 => (byte)'E',
            10 => (byte)' ',
            11 => (byte)'P',
            12 => (byte)'h',
            13 => (byte)'a',
            14 => (byte)'s',
            15 => (byte)'e',
            16 => (byte)'A',
            17 => (byte)'.',
            18 => (byte)'D',
            19 => (byte)'i',
            20 => (byte)'a',
            21 => (byte)'g',
            22 => (byte)'n',
            23 => (byte)'o',
            24 => (byte)'s',
            25 => (byte)'t',
            26 => (byte)'i',
            27 => (byte)'c',
            28 => (byte)'s',
            29 => (byte)' ',
            30 => (byte)'P',
            31 => (byte)'A',
            32 => (byte)'S',
            33 => (byte)'S',
            _ => 0
        };
    }

    private static byte GetOsBootOkByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'O',
            1 => (byte)'S',
            2 => (byte)'_',
            3 => (byte)'B',
            4 => (byte)'O',
            5 => (byte)'O',
            6 => (byte)'T',
            7 => (byte)'_',
            8 => (byte)'O',
            9 => (byte)'K',
            _ => 0
        };
    }

    private static byte GetTestPassBootSequenceByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'T',
            1 => (byte)'E',
            2 => (byte)'S',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'P',
            6 => (byte)'A',
            7 => (byte)'S',
            8 => (byte)'S',
            9 => (byte)' ',
            10 => (byte)'b',
            11 => (byte)'o',
            12 => (byte)'o',
            13 => (byte)'t',
            14 => (byte)'.',
            15 => (byte)'s',
            16 => (byte)'e',
            17 => (byte)'q',
            18 => (byte)'u',
            19 => (byte)'e',
            20 => (byte)'n',
            21 => (byte)'c',
            22 => (byte)'e',
            _ => 0
        };
    }

    private static byte GetOsSmokePassByte(int offset)
    {
        return offset switch
        {
            0 => (byte)'O',
            1 => (byte)'S',
            2 => (byte)'_',
            3 => (byte)'S',
            4 => (byte)'M',
            5 => (byte)'O',
            6 => (byte)'K',
            7 => (byte)'E',
            8 => (byte)'_',
            9 => (byte)'P',
            10 => (byte)'A',
            11 => (byte)'S',
            12 => (byte)'S',
            _ => 0
        };
    }
}
