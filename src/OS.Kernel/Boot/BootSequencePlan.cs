namespace OS.Boot;

public static class BootSequencePlan
{
    public const int NonSmokeLineCount = 5;

    public const int LineCount = 8;

    public const int M01StageOneSuccessLineCount = 11;

    public static int GetM01StageOneSuccessLineLength(int lineIndex)
    {
        return lineIndex switch
        {
            0 => 21,
            1 => 25,
            2 => 27,
            3 => 25,
            4 => 24,
            5 => 25,
            6 => 33,
            7 => 21,
            8 => 24,
            9 => 17,
            10 => 20,
            _ => 0
        };
    }

    public static byte GetM01StageOneSuccessLineByte(int lineIndex, int offset)
    {
        int lineLength = GetM01StageOneSuccessLineLength(lineIndex);
        if (offset < 0 || offset >= lineLength)
        {
            return 0;
        }

        return lineIndex switch
        {
            // TEST_START m01.stage1
            0 => GetPackedAsciiByte(
                offset,
                0x4154535F54534554UL,
                0x732E31306D205452UL,
                0x0000003165676174UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_LOADED_IMAGE_CAPTURED
            1 => GetPackedAsciiByte(
                offset,
                0x44414F4C5F31304DUL,
                0x4547414D495F4445UL,
                0x455255545041435FUL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_STACK_SWITCH_STUB_READY
            2 => GetPackedAsciiByte(
                offset,
                0x434154535F31304DUL,
                0x4843544957535F4BUL,
                0x45525F425554535FUL,
                0x0000000000594441UL,
                0x0000000000000000UL),

            // M01_STACK_ARENA_ALLOCATED
            3 => GetPackedAsciiByte(
                offset,
                0x434154535F31304DUL,
                0x5F414E4552415F4BUL,
                0x455441434F4C4C41UL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_STACK_GUARD_RESERVED
            4 => GetPackedAsciiByte(
                offset,
                0x434154535F31304DUL,
                0x5F44524155475F4BUL,
                0x4445565245534552UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_STAGE1_MAP_RECAPTURED
            5 => GetPackedAsciiByte(
                offset,
                0x474154535F31304DUL,
                0x525F50414D5F3145UL,
                0x4552555450414345UL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_STAGE1_RESERVATIONS_VALIDATED
            6 => GetPackedAsciiByte(
                offset,
                0x474154535F31304DUL,
                0x52455345525F3145UL,
                0x5F534E4F49544156UL,
                0x45544144494C4156UL,
                0x0000000000000044UL),

            // M01_OWNED_STACK_READY
            7 => GetPackedAsciiByte(
                offset,
                0x454E574F5F31304DUL,
                0x5F4B434154535F44UL,
                0x0000005944414552UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_OWNED_STACK_SWITCHED
            8 => GetPackedAsciiByte(
                offset,
                0x454E574F5F31304DUL,
                0x5F4B434154535F44UL,
                0x4445484354495753UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_RSP_CONTAINED
            9 => GetPackedAsciiByte(
                offset,
                0x5F5053525F31304DUL,
                0x454E4941544E4F43UL,
                0x0000000000000044UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // TEST_PASS m01.stage1
            10 => GetPackedAsciiByte(
                offset,
                0x5341505F54534554UL,
                0x74732E31306D2053UL,
                0x0000000031656761UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            _ => 0
        };
    }

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

    private static byte GetPackedAsciiByte(
        int offset,
        ulong bytes0To7,
        ulong bytes8To15,
        ulong bytes16To23,
        ulong bytes24To31,
        ulong bytes32To39)
    {
        ulong bytes = (offset >> 3) switch
        {
            0 => bytes0To7,
            1 => bytes8To15,
            2 => bytes16To23,
            3 => bytes24To31,
            4 => bytes32To39,
            _ => 0
        };

        return (byte)(bytes >> ((offset & 7) * 8));
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
