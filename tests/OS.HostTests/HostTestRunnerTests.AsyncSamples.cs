using System.Text.Json;
using System.Xml.Linq;
using OS.Tooling;

internal static partial class HostTestRunnerTests
{
    private static partial class AsyncSamples
    {
        internal static bool Completed;
        private static ValueTask SourcePass()
        {
            var source = new Source();
            return new(source, source.Token);
        }

        private static ValueTask<bool> SourceFalse()
        {
            var source = new Source();
            return new(source, source.Token);
        }

        private static async Task TaskPass()
        {
            await Task.Yield();
            Completed = true;
        }

        private static async Task<bool> TaskResultPass()
        {
            await Task.Yield();
            Completed = true;
            return true;
        }

        private static async ValueTask ValuePass()
        {
            await Task.Yield();
            Completed = true;
        }

        private static async ValueTask<bool> ValueResultPass()
        {
            await Task.Yield();
            Completed = true;
            return true;
        }

        private static async Task<bool> TaskFalse()
        {
            await Task.Yield();
            return false;
        }

        private static async ValueTask<bool> ValueFalse()
        {
            await Task.Yield();
            return false;
        }

        private static bool SyncFalse() => false;
        private static async Task TaskFault()
        {
            await Task.Yield();
            throw new InvalidOperationException("joined_async_failure");
        }

        private static async ValueTask ValueFault()
        {
            await Task.Yield();
            throw new InvalidOperationException("joined_async_failure");
        }

        private static Task NullTask() => null!;
        private static Task<bool> NullResult() => null!;
        private static async void AsyncVoid()
        {
            await Task.Yield();
            Completed = true;
        }

        private static int Unsupported()
        {
            Completed = true;
            return 1;
        }

        private static Task<int> UnsupportedTask()
        {
            Completed = true;
            return Task.FromResult(1);
        }

        private static ValueTask<int> UnsupportedValueTask()
        {
            Completed = true;
            return new(1);
        }
    }
}
