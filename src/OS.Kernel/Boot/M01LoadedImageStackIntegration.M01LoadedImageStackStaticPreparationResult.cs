namespace OS.Boot;
/// <summary>
/// Compact preparation status returned by the firmware-facing path while
/// the large diagnostic evidence is written to caller-owned static scratch.
/// This value may remain on the firmware stack.
/// </summary>

public readonly struct M01LoadedImageStackStaticPreparationResult
{
    private readonly M01LoadedImageStackStatus _status;
    private readonly M01LoadedImageStackCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private M01LoadedImageStackStaticPreparationResult(M01LoadedImageStackStatus status, M01LoadedImageStackCheckpoint checkpoint)
    {
        _status = status;
        _checkpoint = checkpoint;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
    }

    public M01LoadedImageStackStatus Status => _status;
    public M01LoadedImageStackCheckpoint Checkpoint => _checkpoint;
    public bool IsPrepared => _status == M01LoadedImageStackStatus.Success && _checkpoint == M01LoadedImageStackCheckpoint.SwitchArmed && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0;

    internal static M01LoadedImageStackStaticPreparationResult Success(M01LoadedImageStackCheckpoint checkpoint)
    {
        return new M01LoadedImageStackStaticPreparationResult(M01LoadedImageStackStatus.Success, checkpoint);
    }

    internal static M01LoadedImageStackStaticPreparationResult Fail(M01LoadedImageStackStatus status, M01LoadedImageStackCheckpoint checkpoint)
    {
        return new M01LoadedImageStackStaticPreparationResult(status, checkpoint);
    }
}
