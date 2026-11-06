namespace OS.Boot;
/// <summary>
/// Separates the M01-029 preparation contract from the retained M01-019
/// synthetic execution model. Neither mode is processor execution evidence.
/// </summary>

public enum M01LivePagingExecutionMode : byte
{
    Uninitialized = 0,
    PreparationOnly = 1,
    ModelOnlyLegacy = 2
}
