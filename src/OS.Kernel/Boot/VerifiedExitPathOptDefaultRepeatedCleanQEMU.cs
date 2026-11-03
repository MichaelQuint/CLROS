namespace OS.Boot;

/// <summary>The ordinary EFI boot policy, separate from diagnostic probe opt-ins.</summary>
public static class VerifiedExitPathOptDefaultRepeatedCleanQEMU
{
#if CLROS_NORMAL_VERIFIED_EXIT
    public const bool Enabled = true;
#else
    public const bool Enabled = false;
#endif

    public const int CompletionLineCount = 6;

    /// <summary>
    /// Emit only after the original post-exit subsystem operation completes.
    /// The caller supplies the admitted owned writer; a failed write stops the
    /// tail immediately. This formatter does not establish transition evidence.
    /// </summary>
    public static unsafe bool WriteCompletion(void* context, delegate*<void*, byte*, uint, bool> write)
    {
        if (context == null || write == null)
            return false;
        byte* line = stackalloc byte[32];
        for (int index = 0; index < CompletionLineCount; index++)
        {
            int length = index < 3 ? GetLiteralLength(index)
                : BootSequencePlan.GetLineLength(index + BootSequencePlan.NonSmokeLineCount - 3);
            for (int offset = 0; offset < length; offset++)
                line[offset] = index < 3 ? GetLiteralByte(index, offset)
                    : BootSequencePlan.GetLineByte(index + BootSequencePlan.NonSmokeLineCount - 3, offset);
            line[length] = 13;
            line[length + 1] = 10;
            if (!write(context, line, (uint)(length + 2)))
                return false;
        }
        return true;
    }

    private static int GetLiteralLength(int index) => index == 0 ? 18 : index == 1 ? 19 : 17;

    private static byte GetLiteralByte(int index, int offset)
    {
        // TEST_START m03.011 / CLROS_M03_011_READY / TEST_PASS m03.011.
        ulong word = (index * 3 + offset / 8) switch
        {
            0 => 0x4154535F54534554UL,
            1 => 0x302E33306D205452UL,
            2 => 0x3131UL,
            3 => 0x304D5F534F524C43UL,
            4 => 0x45525F3131305F33UL,
            5 => 0x594441UL,
            6 => 0x5341505F54534554UL,
            7 => 0x31302E33306D2053UL,
            8 => 0x31UL,
            _ => 0
        };
        return (byte)(word >> (offset % 8 * 8));
    }
}
