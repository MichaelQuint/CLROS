using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    private sealed class Capture : IDisposable
    {
        internal const uint RecordCount = 12;
        internal readonly ObservationSink* Sink = (ObservationSink*)NativeMemory.AllocZeroed((nuint)sizeof(ObservationSink));
        internal Capture(Fixture f, uint bytes = 512)
        {
            Sink->State = f.State; Sink->Hardware = f.Hardware; Sink->ReenterOperation = -1;
            Sink->ExpectedBytes = bytes;
            Sink->Records = (Block.Observation*)NativeMemory.AllocZeroed(RecordCount, (nuint)sizeof(Block.Observation));
            Sink->Payload = (byte*)NativeMemory.AllocZeroed(bytes); Sink->Output = (byte*)NativeMemory.AllocZeroed(bytes + 8);
            Sink->Metadata = (byte*)NativeMemory.AllocZeroed(64);
            f.Observation = new() { Context = Sink, Observe = &CaptureObservation }; Sink->Input = f.Input;
        }
        public void Dispose()
        { NativeMemory.Free(Sink->Records); NativeMemory.Free(Sink->Payload); NativeMemory.Free(Sink->Output); NativeMemory.Free(Sink->Metadata); NativeMemory.Free(Sink); }
    }
}
