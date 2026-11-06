internal static unsafe partial class SchedulerTimerTests
{
    private partial struct Model
    {
        public enum Delivery : byte
        {
            None,
            Idle,
            SendPending,
            Requested,
            InService
        }
    }
}
