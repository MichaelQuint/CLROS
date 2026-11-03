using OS.Std.Text;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotClassConstructorRunnerPreflightResult
{
    private const uint InitializedFlag = 1;
    private NativeAotClassConstructorRunnerPreflightResult(uint flags, NativeAotClassConstructorRunnerPreflightStatus status, int typeCount, int targetMetadataCount, int occupiedCount, int capacity, int firstProblemIndex, NativeAotClassConstructorState firstProblemState, uint firstProblemFailureCode)
    {
        Flags = flags;
        Status = status;
        TypeCount = typeCount;
        TargetMetadataCount = targetMetadataCount;
        OccupiedCount = occupiedCount;
        Capacity = capacity;
        FirstProblemIndex = firstProblemIndex;
        FirstProblemState = firstProblemState;
        FirstProblemFailureCode = firstProblemFailureCode;
    }

    public uint Flags
    {
        get;
    }
    public NativeAotClassConstructorRunnerPreflightStatus Status
    {
        get;
    }
    public int TypeCount
    {
        get;
    }
    public int TargetMetadataCount
    {
        get;
    }
    public int OccupiedCount
    {
        get;
    }
    public int Capacity
    {
        get;
    }
    public int FirstProblemIndex
    {
        get;
    }
    public NativeAotClassConstructorState FirstProblemState
    {
        get;
    }
    public uint FirstProblemFailureCode
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorRunnerPreflightStatus.Ready && TypeCount > 0 && TargetMetadataCount == TypeCount && OccupiedCount == TypeCount && Capacity >= TypeCount && FirstProblemIndex == -1;

    internal static NativeAotClassConstructorRunnerPreflightResult Create(NativeAotClassConstructorRunnerPreflightStatus status, int typeCount, int targetMetadataCount, int occupiedCount, int capacity, int firstProblemIndex, NativeAotClassConstructorState firstProblemState, uint firstProblemFailureCode)
    {
        return new NativeAotClassConstructorRunnerPreflightResult(InitializedFlag, status, typeCount, targetMetadataCount, occupiedCount, capacity, firstProblemIndex, firstProblemState, firstProblemFailureCode);
    }
}
