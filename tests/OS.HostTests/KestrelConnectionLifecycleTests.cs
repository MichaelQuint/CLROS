using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static partial class KestrelConnectionLifecycleTests
{
    private static byte[] Body()
    {
        byte[] body = new byte[32768];
        for (int i = 0; i < body.Length; i++)
            body[i] = unchecked((byte)(31 + 17 * i));
        return body;
    }

    [HostTest("m10.011 lifecycle echo consumes partial reads with exact bytes hash and retained caller stream")]
    private static async Task PartialEcho()
    {
        var exercise = new ServiceLifecycleExercise();
        byte[] body = Body();
        using var input = new ShortInput(body);
        var result = await exercise.ReadEchoAsync(input, body.Length, CancellationToken.None);
        uint expected = 2166136261;
        foreach (byte value in body)
            expected = unchecked((expected ^ value) * 16777619);
        HostCheck.True(result.Accepted && result.Bytes == 32768 && result.Hash == expected && exercise.IsDrained);
        HostCheck.True(input.Reads > 1000 && input.LargestRequest <= 1024 && !input.Closed);
        HostCheck.True(result.Text.StartsWith("CLROS_LIFECYCLE_ECHO_OK bytes=32768 hash=", StringComparison.Ordinal));
        HostCheck.True(exercise.Read().Contains("echoes=1 streams=0 rejected=0 read=32768 written=0", StringComparison.Ordinal));
    }

    [HostTest("m10.011 a slow response leaves independent echo and cancellation progress while retaining its buffer")]
    private static async Task SlowPeerProgress()
    {
        var exercise = new ServiceLifecycleExercise();
        using var output = new SlowOutput();
        Task streaming = exercise.WriteStreamAsync(output, CancellationToken.None);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        HostCheck.True(!streaming.IsCompleted && !exercise.IsDrained);
        using var input = new ShortInput(Body());
        HostCheck.True((await exercise.ReadEchoAsync(input, 32768, CancellationToken.None)).Accepted);
        using var cancellation = new CancellationTokenSource();
        Task waiting = exercise.WaitForCancellationAsync(cancellation.Token);
        HostCheck.True(exercise.Read().Contains("active=2 waiting=1 waits=1 canceled=0", StringComparison.Ordinal));
        cancellation.Cancel();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        HostCheck.True(!streaming.IsCompleted && exercise.Read().Contains("waiting=0 waits=1 canceled=1", StringComparison.Ordinal));
        output.Release.SetResult();
        await streaming.WaitAsync(TimeSpan.FromSeconds(5));
        byte[] actual = output.ToArray();
        HostCheck.Equal(524288, actual.Length);
        for (int i = 0; i < actual.Length; i++)
            HostCheck.Equal(unchecked((byte)(31 + 17 * i)), actual[i]);
        HostCheck.True(exercise.IsDrained && output.LargestWrite <= 1024 && output.Writes == 512 && !output.Closed);
        HostCheck.True(exercise.Read().Contains("echoes=1 streams=1 rejected=0 read=32768 written=524288", StringComparison.Ordinal));
    }

    [HostTest("m10.011 lifecycle request cancellation drains a blocked write and admits reuse without closing caller storage")]
    private static async Task CanceledWriteReuse()
    {
        var exercise = new ServiceLifecycleExercise();
        using var output = new SlowOutput();
        using var cancellation = new CancellationTokenSource();
        Task writing = exercise.WriteStreamAsync(output, cancellation.Token);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        bool canceled = false;
        try
        {
            await writing.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        HostCheck.True(canceled && exercise.IsDrained && !output.Closed && output.Length == 0);
        HostCheck.True(exercise.Read().Contains("canceled=1 echoes=0 streams=0", StringComparison.Ordinal));
        using var input = new ShortInput(Body());
        HostCheck.True((await exercise.ReadEchoAsync(input, 32768, CancellationToken.None)).Accepted && exercise.IsDrained);
    }

    [HostTest("m10.011 malformed truncated and unknown-length bodies reject without completed echoes or retained workers")]
    private static async Task BadBodies()
    {
        var exercise = new ServiceLifecycleExercise();
        using var unknown = new ShortInput(Body());
        HostCheck.True(!(await exercise.ReadEchoAsync(unknown, null, CancellationToken.None)).Accepted && unknown.Reads == 0);
        HostCheck.True(!(await exercise.ReadEchoAsync(unknown, 32769, CancellationToken.None)).Accepted && unknown.Reads == 0);
        using var truncated = new ShortInput(Body()[..50]);
        HostCheck.True(!(await exercise.ReadEchoAsync(truncated, 32768, CancellationToken.None)).Accepted);
        byte[] bad = Body();
        bad[31] ^= 1;
        using var corrupt = new ShortInput(bad);
        HostCheck.True(!(await exercise.ReadEchoAsync(corrupt, 32768, CancellationToken.None)).Accepted);
        HostCheck.True(exercise.IsDrained && exercise.Read().Contains("echoes=0 streams=0 rejected=4", StringComparison.Ordinal));
        bool rejected = false;
        try
        {
            await exercise.WaitForCancellationAsync(CancellationToken.None);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        HostCheck.True(rejected && exercise.IsDrained);
    }

    [HostTest("m10.011 lifecycle peer performs real host loopback I-O pending-stream progress reset timeout and reuse")]
    private static async Task PeerLoopback()
    {
        await using var server = new PeerServer();
        var observed = await NativeAotServiceLifecyclePeer.RunAsync(server.Port);
        NativeAotServiceLifecyclePeer.Validate(observed);
        // Value-only boundary: the failed first guest used its old90s budget
        // before completing the fixed sequence. No slow test or guest is implied.
        NativeAotServiceLifecyclePeer.Validate(observed with
        {
            HoldStartedTicks = TimeSpan.FromSeconds(100).Ticks,
            HealthyCompletedTicks = TimeSpan.FromSeconds(110).Ticks,
            HoldReleasedTicks = TimeSpan.FromSeconds(120).Ticks
        });
        foreach (var changed in new[]
        {
            observed with
            {
                SequentialConnections = 8
            },
            observed with
            {
                EchoHash = observed.EchoHash ^ 1
            },
            observed with
            {
                StreamBytes = 524287
            },
            observed with
            {
                StreamSha256 = new string ('0', 64)
            },
            observed with
            {
                ResetRequests = 0
            },
            observed with
            {
                TimeoutRequests = 0
            },
            observed with
            {
                HoldReleasedTicks = observed.HealthyCompletedTicks - 1
            },
            observed with
            {
                HoldReleasedTicks = TimeSpan.FromSeconds(240).Ticks
            },
            observed with
            {
                TerminalStatus = observed.TerminalStatus.Replace("canceled=2", "canceled=1", StringComparison.Ordinal)
            },
            observed with
            {
                TerminalStatus = observed.TerminalStatus + " extra=1"
            }
        }

        )
            HostCheck.True(PeerRejected(() => NativeAotServiceLifecyclePeer.Validate(changed)));
    }

    [HostTest("m10.011 lifecycle peer rejects malformed truncated and trailing actual HTTP responses")]
    private static async Task PeerBadResponses()
    {
        foreach (string fault in new[]
        {
            "shape",
            "truncated",
            "trailing"
        }

        )
        {
            await using var server = new PeerServer(fault);
            bool rejected = false;
            try
            {
                _ = await NativeAotServiceLifecyclePeer.RunAsync(server.Port);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }
    }

    private static bool PeerRejected(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ToolFailure)
        {
            return true;
        }
    }
}
