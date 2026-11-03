namespace OS.Tooling;
public static partial class NativeDiskStaticReadWindows
{
    /// <summary>One immutable committed sector with its original three-stage
    /// positions. ObservationIndex indexes the supplied inspected IO list;
    /// the other indices identify records in the complete original transcript.</summary>

    public readonly record struct SectorCopy(int ObservationIndex, int SubmitRecordIndex, int ValidationRecordIndex, int CopyRecordIndex, NativeDiskReadObservations.Observation Observation);
}
