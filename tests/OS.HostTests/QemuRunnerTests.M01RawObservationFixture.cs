using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static partial class QemuRunnerTests
{
    private readonly struct M01RawObservationFixture
    {
        public M01RawObservationFixture(string observationPath, string privateSerialPath, string publicSerialPath, string qmpTranscriptPath, string captureScratchPath)
        {
            ObservationPath = observationPath;
            PrivateSerialPath = privateSerialPath;
            PublicSerialPath = publicSerialPath;
            QmpTranscriptPath = qmpTranscriptPath;
            CaptureScratchPath = captureScratchPath;
        }

        public string ObservationPath
        {
            get;
        }
        public string PrivateSerialPath
        {
            get;
        }
        public string PublicSerialPath
        {
            get;
        }
        public string QmpTranscriptPath
        {
            get;
        }
        public string CaptureScratchPath
        {
            get;
        }
    }
}
