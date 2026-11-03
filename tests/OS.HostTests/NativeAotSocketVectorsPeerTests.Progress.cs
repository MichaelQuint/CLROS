using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Apps.NativeAotSocketVectors;
using OS.Tooling;

internal static partial class NativeAotSocketVectorsPeerTests
{
    private sealed class Progress : TextWriter
    {
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously), Partial = new(TaskCreationOptions.RunContinuationsAsynchronously), ResetRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Success;
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            if (value?.StartsWith("SOCKET_VECTOR_LISTEN ", StringComparison.Ordinal) == true)
                Ready.TrySetResult();
            if (value?.StartsWith("SOCKET_VECTOR_RECEIVE index=0 bytes=3 total=3 ", StringComparison.Ordinal) == true)
                Partial.TrySetResult();
            if (value == "SOCKET_VECTOR_REQUEST index=1 bytes=64")
                ResetRequest.TrySetResult();
            if (value?.StartsWith("TEST_PASS nativeaot.socket.vectors ", StringComparison.Ordinal) == true)
                Success = true;
            if (value?.StartsWith("TEST_FAIL ", StringComparison.Ordinal) == true)
            {
                var failure = new InvalidDataException(value);
                Ready.TrySetException(failure);
                Partial.TrySetException(failure);
                ResetRequest.TrySetException(failure);
            }
        }
    }
}
