using System.Text.Json;
using System.Xml.Linq;
using OS.Tooling;

internal static partial class HostTestRunnerTests
{
    private static partial class AsyncSamples
    {
        private sealed class Source : System.Threading.Tasks.Sources.IValueTaskSource, System.Threading.Tasks.Sources.IValueTaskSource<bool>
        {
            private System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool> core = new()
            {
                RunContinuationsAsynchronously = true
            };
            internal short Token => core.Version;

            internal Source() => ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(10);
                Completed = true;
                core.SetResult(false);
            });
            public System.Threading.Tasks.Sources.ValueTaskSourceStatus GetStatus(short token) => core.GetStatus(token);
            public void OnCompleted(Action<object?> continuation, object? state, short token, System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags flags) => core.OnCompleted(continuation, state, token, flags);
            public bool GetResult(short token) => core.GetResult(token);
            void System.Threading.Tasks.Sources.IValueTaskSource.GetResult(short token)
            {
                _ = core.GetResult(token);
            }
        }
    }
}
