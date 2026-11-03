using System.Runtime.InteropServices;
using OS.Boot;
using OS.Tooling;

internal static unsafe partial class WorkloadServiceLoopTests
{
    private struct Context
    {
        public ulong Now, Step, LastService;
        public uint ClockCalls, Services, StopCalls, StopAt, FailClockAt, FailServiceAt;
        public bool Regress, FailStop, EarlyRefused, WrongState, StopChangesState;
        public WorkloadServiceLoopState* Original;
    }
}
