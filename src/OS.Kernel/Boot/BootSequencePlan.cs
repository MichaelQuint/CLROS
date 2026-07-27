namespace OS.Boot;

public static class BootSequencePlan
{
    public const int NonSmokeLineCount = 5;

    public const int LineCount = 8;

    public const int M01StageOneSuccessLineCount = 11;

    public const int M01StageTwoSuccessLineCount = 9;

    public const int M01StageThreeSuccessLineCount = 20;

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

    public static int GetM01StageTwoSuccessLineLength(int lineIndex)
    {
        return lineIndex switch
        {
            0 => 21,
            1 => 29,
            2 => 25,
            3 => 24,
            4 => 21,
            5 => 26,
            6 => 17,
            7 => 24,
            8 => 20,
            _ => 0
        };
    }

    public static byte GetM01StageTwoSuccessLineByte(int lineIndex, int offset)
    {
        int lineLength = GetM01StageTwoSuccessLineLength(lineIndex);
        if (offset < 0 || offset >= lineLength)
        {
            return 0;
        }

        return lineIndex switch
        {
            // TEST_START m01.stage2
            0 => GetPackedAsciiByte(
                offset,
                0x4154535F54534554UL,
                0x732E31306D205452UL,
                0x0000003265676174UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_ALLOCATOR_ARENA_ALLOCATED
            1 => GetPackedAsciiByte(
                offset,
                0x4F4C4C415F31304DUL,
                0x52415F524F544143UL,
                0x4F4C4C415F414E45UL,
                0x0000004445544143UL,
                0x0000000000000000UL),

            // M01_STAGE2_MAP_RECAPTURED
            2 => GetPackedAsciiByte(
                offset,
                0x474154535F31304DUL,
                0x525F50414D5F3245UL,
                0x4552555450414345UL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_RESERVATIONS_APPLIED
            3 => GetPackedAsciiByte(
                offset,
                0x455345525F31304DUL,
                0x534E4F4954415652UL,
                0x4445494C5050415FUL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_LIVE_ALLOCATED_4K
            4 => GetPackedAsciiByte(
                offset,
                0x4556494C5F31304DUL,
                0x5441434F4C4C415FUL,
                0x0000004B345F4445UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_LIVE_PAGE_WITHIN_ARENA
            5 => GetPackedAsciiByte(
                offset,
                0x4556494C5F31304DUL,
                0x49575F454741505FUL,
                0x4552415F4E494854UL,
                0x000000000000414EUL,
                0x0000000000000000UL),

            // M01_LIVE_FREED_4K
            6 => GetPackedAsciiByte(
                offset,
                0x4556494C5F31304DUL,
                0x345F44454552465FUL,
                0x000000000000004BUL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_DOUBLE_FREE_REJECTED
            7 => GetPackedAsciiByte(
                offset,
                0x42554F445F31304DUL,
                0x5F454552465F454CUL,
                0x44455443454A4552UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // TEST_PASS m01.stage2
            8 => GetPackedAsciiByte(
                offset,
                0x5341505F54534554UL,
                0x74732E31306D2053UL,
                0x0000000032656761UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            _ => 0
        };
    }

    public static int GetM01StageThreeSuccessLineLength(int lineIndex)
    {
        return lineIndex switch
        {
            0 => 21,
            1 => 27,
            2 => 35,
            3 => 36,
            4 => 25,
            5 => 25,
            6 => 21,
            7 => 25,
            8 => 11,
            9 => 25,
            10 => 19,
            11 => 22,
            12 => 29,
            13 => 12,
            14 => 21,
            15 => 29,
            16 => 21,
            17 => 20,
            18 => 20,
            19 => 32,
            _ => 0
        };
    }

    public static byte GetM01StageThreeSuccessLineByte(
        int lineIndex,
        int offset)
    {
        int lineLength = GetM01StageThreeSuccessLineLength(lineIndex);
        if (offset < 0 || offset >= lineLength)
        {
            return 0;
        }

        return lineIndex switch
        {
            // TEST_START m01.stage3
            0 => GetPackedAsciiByte(
                offset,
                0x4154535F54534554UL,
                0x732E31306D205452UL,
                0x0000003365676174UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_CR3_STUB_PAGE_ALLOCATED
            1 => GetPackedAsciiByte(
                offset,
                0x5F3352435F31304DUL,
                0x4741505F42555453UL,
                0x41434F4C4C415F45UL,
                0x0000000000444554UL,
                0x0000000000000000UL),

            // M01_PROTECTION_PROBE_PAGE_ALLOCATED
            2 => GetPackedAsciiByte(
                offset,
                0x544F52505F31304DUL,
                0x505F4E4F49544345UL,
                0x4741505F45424F52UL,
                0x41434F4C4C415F45UL,
                0x0000000000444554UL),

            // M01_PAGE_TABLE_STATE_ARENA_ALLOCATED
            3 => GetPackedAsciiByte(
                offset,
                0x454741505F31304DUL,
                0x535F454C4241545FUL,
                0x4552415F45544154UL,
                0x434F4C4C415F414EUL,
                0x0000000044455441UL),

            // M01_STAGE3_MAP_RECAPTURED
            4 => GetPackedAsciiByte(
                offset,
                0x474154535F31304DUL,
                0x525F50414D5F3345UL,
                0x4552555450414345UL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_CR3_STUB_MATERIALIZED
            5 => GetPackedAsciiByte(
                offset,
                0x5F3352435F31304DUL,
                0x54414D5F42555453UL,
                0x455A494C41495245UL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_PAGE_TABLES_BUILT
            6 => GetPackedAsciiByte(
                offset,
                0x454741505F31304DUL,
                0x5F53454C4241545FUL,
                0x000000544C495542UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_PAGING_MODE_VALIDATED
            7 => GetPackedAsciiByte(
                offset,
                0x494741505F31304DUL,
                0x5F45444F4D5F474EUL,
                0x45544144494C4156UL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_PRE_CR3
            8 => GetPackedAsciiByte(
                offset,
                0x5F4552505F31304DUL,
                0x0000000000335243UL,
                0x0000000000000000UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_FIRMWARE_CR3_RESTORED
            9 => GetPackedAsciiByte(
                offset,
                0x4D5249465F31304DUL,
                0x3352435F45524157UL,
                0x45524F545345525FUL,
                0x0000000000000044UL,
                0x0000000000000000UL),

            // M01_RFLAGS_RESTORED
            10 => GetPackedAsciiByte(
                offset,
                0x414C46525F31304DUL,
                0x4F545345525F5347UL,
                0x0000000000444552UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_PGE_CYCLE_VERIFIED
            11 => GetPackedAsciiByte(
                offset,
                0x5F4547505F31304DUL,
                0x45565F454C435943UL,
                0x0000444549464952UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_OWNED_CR3_CANARY_VERIFIED
            12 => GetPackedAsciiByte(
                offset,
                0x454E574F5F31304DUL,
                0x41435F3352435F44UL,
                0x5245565F5952414EUL,
                0x0000004445494649UL,
                0x0000000000000000UL),

            // M01_POST_CR3
            13 => GetPackedAsciiByte(
                offset,
                0x54534F505F31304DUL,
                0x000000003352435FUL,
                0x0000000000000000UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_RW_TO_RX_VERIFIED
            14 => GetPackedAsciiByte(
                offset,
                0x545F57525F31304DUL,
                0x5245565F58525F4FUL,
                0x0000004445494649UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_TLB_INVALIDATION_COMPLETE
            15 => GetPackedAsciiByte(
                offset,
                0x5F424C545F31304DUL,
                0x4144494C41564E49UL,
                0x4D4F435F4E4F4954UL,
                0x0000004554454C50UL,
                0x0000000000000000UL),

            // M01_RX_PROBE_EXECUTED
            16 => GetPackedAsciiByte(
                offset,
                0x505F58525F31304DUL,
                0x4558455F45424F52UL,
                0x0000004445545543UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // M01_PAGE_ZERO_ABSENT
            17 => GetPackedAsciiByte(
                offset,
                0x454741505F31304DUL,
                0x42415F4F52455A5FUL,
                0x00000000544E4553UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // TEST_PASS m01.stage3
            18 => GetPackedAsciiByte(
                offset,
                0x5341505F54534554UL,
                0x74732E31306D2053UL,
                0x0000000033656761UL,
                0x0000000000000000UL,
                0x0000000000000000UL),

            // CLROS_M01_LIVE_BOOT_MEMORY_READY
            19 => GetPackedAsciiByte(
                offset,
                0x304D5F534F524C43UL,
                0x425F4556494C5F31UL,
                0x4F4D454D5F544F4FUL,
                0x59444145525F5952UL,
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
