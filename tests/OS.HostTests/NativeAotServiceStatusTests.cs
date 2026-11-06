using System.Text;
using OS.Tools.QemuRunner;

internal static class NativeAotServiceStatusTests
{
    [HostTest("nativeaot.service.status exact schema ranges and stopped witness boundaries", HostLane.Fast, 0)]
    private static void Schema()
    {
        var idle = Read(Json(phase: 0, worker: 0));
        HostCheck.Equal("clros.service.v1", idle.Contract);
        HostCheck.Equal(0, idle.Phase);
        HostCheck.Equal(0, idle.Failure);
        HostCheck.Equal(0, idle.ManagedWorkerId);
        HostCheck.Equal(0L, idle.Calls);
        HostCheck.Equal(0L, idle.RandomRequests);
        HostCheck.Equal(0L, idle.RandomBytes);
        HostCheck.Equal(0L, idle.Iterations);
        HostCheck.Equal(0L, idle.ElapsedMilliseconds);
        HostCheck.True(!idle.ApplicationStoppedWitness);
        // Separately sampled counters are deliberately not required to agree.
        string stopped = Json(phase: 4, calls: 4097, bytes: 4097, iterations: 1, elapsed: 300001);
        HostCheck.True(Read(stopped).ApplicationStoppedWitness);
        foreach (string body in new[]
        {
            Json(phase: 2, calls: 4097, bytes: 4097, iterations: 1, elapsed: 300001),
            Json(phase: 4, failure: 1, calls: 4097, bytes: 4097, iterations: 1, elapsed: 300001),
            Json(phase: 4, worker: 0, calls: 4097, bytes: 4097, iterations: 1, elapsed: 300001),
            Json(phase: 4, calls: 4096, bytes: 4097, iterations: 1, elapsed: 300001),
            Json(phase: 4, calls: 4097, bytes: 4096, iterations: 1, elapsed: 300001),
            Json(phase: 4, calls: 4097, bytes: 4097, iterations: 0, elapsed: 300001),
            Json(phase: 4, calls: 4097, bytes: 4097, iterations: 1, elapsed: 300000)
        })
            HostCheck.True(!Read(body).ApplicationStoppedWitness);
        var maximum = Read(Json(phase: 4, failure: int.MaxValue, worker: int.MaxValue, calls: long.MaxValue,
            requests: long.MaxValue, bytes: long.MaxValue, iterations: long.MaxValue, elapsed: long.MaxValue));
        HostCheck.Equal(int.MaxValue, maximum.Failure);
        HostCheck.Equal(int.MaxValue, maximum.ManagedWorkerId);
        HostCheck.Equal(long.MaxValue, maximum.Calls);
        HostCheck.Equal(long.MaxValue, maximum.RandomRequests);
        HostCheck.Equal(long.MaxValue, maximum.RandomBytes);
        HostCheck.Equal(long.MaxValue, maximum.Iterations);
        HostCheck.Equal(long.MaxValue, maximum.ElapsedMilliseconds);
        HostCheck.True(Read(stopped + new string(' ', 8192 - Encoding.UTF8.GetByteCount(stopped))).ApplicationStoppedWitness);
        HostCheck.True(Read(stopped.Replace("\"contract\":\"clros.service.v1\",", "", StringComparison.Ordinal)
            .Replace("}", ",\"contract\":\"clros.service.v1\"}", StringComparison.Ordinal)).ApplicationStoppedWitness);
    }

    [HostTest("nativeaot.service.status rejects malformed duplicate missing extra typed and oversized input", HostLane.Fast, 1)]
    private static void Malformed()
    {
        string good = Json(calls: 1);
        foreach (string body in new[]
        {
            "", "null", "[]", "{}", good[..^1], good + "{}", good + "x",
            good.Replace("}", ",\"extra\":0}", StringComparison.Ordinal),
            good.Replace("}", ",\"phase\":1}", StringComparison.Ordinal),
            good.Replace("}", ",\"\\u0070hase\":1}", StringComparison.Ordinal),
            good.Replace("\"phase\"", "\"Phase\"", StringComparison.Ordinal),
            good.Replace("\"clros.service.v1\"", "\"clros.service.v2\"", StringComparison.Ordinal),
            good.Replace("\"clros.service.v1\"", "1", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":\"1\"", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":1.0", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":1e0", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":true", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":null", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":[]", StringComparison.Ordinal),
            good.Replace("\"calls\":1", "\"calls\":9223372036854775808", StringComparison.Ordinal),
            good.Replace("\"failure\":0", "\"failure\":2147483648", StringComparison.Ordinal),
            good.Replace("\"managedWorkerId\":7", "\"managedWorkerId\":2147483648", StringComparison.Ordinal),
            Json(phase: -1), Json(phase: 5), Json(failure: -1), Json(worker: -1), Json(calls: -1),
            Json(requests: -1), Json(bytes: -1), Json(iterations: -1), Json(elapsed: -1),
            good + new string(' ', 8193 - Encoding.UTF8.GetByteCount(good))
        })
            Reject(body);
        string[] fields = ["\"contract\":\"clros.service.v1\"", "\"phase\":1", "\"failure\":0",
            "\"managedWorkerId\":7", "\"calls\":1", "\"randomRequests\":0", "\"randomBytes\":0",
            "\"iterations\":0", "\"elapsedMilliseconds\":0"];
        foreach (string field in fields)
        {
            string missing = good.Replace(field + ",", "", StringComparison.Ordinal);
            if (missing == good)
                missing = good.Replace("," + field, "", StringComparison.Ordinal);
            Reject(missing);
        }
        bool rejected = false;
        try
        {
            _ = ServiceStatus.Parse([0xff]);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }

    [HostTest("nativeaot.service.status progress rejects failure identity phase counter and time regression", HostLane.Fast, 2)]
    private static void Regressions()
    {
        var prior = Read(Json(calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20));
        foreach (string body in new[]
        {
            Json(phase: 0, calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20),
            Json(phase: 3, calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20),
            Json(failure: 1, calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20),
            Json(worker: 8, calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20),
            Json(worker: 0, calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20),
            Json(calls: 99, requests: 10, bytes: 800, iterations: 25, elapsed: 20),
            Json(calls: 100, requests: 9, bytes: 800, iterations: 25, elapsed: 20),
            Json(calls: 100, requests: 10, bytes: 799, iterations: 25, elapsed: 20),
            Json(calls: 100, requests: 10, bytes: 800, iterations: 24, elapsed: 20),
            Json(calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 19)
        })
            RejectProgress(Read(body), prior);
        var stopped = Read(Json(phase: 4, calls: 100, requests: 10, bytes: 800, iterations: 25, elapsed: 20));
        stopped.RequireProgressFrom(prior);
        stopped.RequireProgressFrom(stopped);
        RejectProgress(prior, stopped);
        RejectProgress(stopped, Read(Json(phase: 3)));
        RejectProgress(stopped, Read(Json(failure: 1)));
    }

    [HostTest("nativeaot.service.status startup identity may appear once and transient phases may be skipped", HostLane.Fast, 3)]
    private static void Startup()
    {
        var idle = Read(Json(phase: 0, worker: 0));
        var starting = Read(Json(worker: 0, elapsed: 1));
        starting.RequireProgressFrom(idle);
        var running = Read(Json(calls: 1, elapsed: 2));
        running.RequireProgressFrom(starting);
        var observed = Read(Json(calls: 1, requests: 1, elapsed: 3));
        observed.RequireProgressFrom(running);
        var stopping = Read(Json(phase: 2, calls: 2, requests: 1, bytes: 8192, elapsed: 4));
        stopping.RequireProgressFrom(observed);
        var stopped = Read(Json(phase: 4, calls: 2, requests: 1, bytes: 8192, elapsed: 5));
        stopped.RequireProgressFrom(stopping);
        stopped.RequireProgressFrom(idle);
        RejectProgress(Read(Json(phase: 4, worker: 8, calls: 2, requests: 1, bytes: 8192, elapsed: 5)), stopped);
        HostCheck.True(!stopped.ApplicationStoppedWitness);
    }

    private static string Json(int phase = 1, int failure = 0, int worker = 7, long calls = 0,
        long requests = 0, long bytes = 0, long iterations = 0, long elapsed = 0) =>
        FormattableString.Invariant($"{{\"contract\":\"clros.service.v1\",\"phase\":{phase},\"failure\":{failure},\"managedWorkerId\":{worker},\"calls\":{calls},\"randomRequests\":{requests},\"randomBytes\":{bytes},\"iterations\":{iterations},\"elapsedMilliseconds\":{elapsed}}}");
    private static ServiceStatus Read(string body) => ServiceStatus.Parse(Encoding.UTF8.GetBytes(body));
    private static void Reject(string body)
    {
        bool rejected = false;
        try
        {
            _ = Read(body);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
    private static void RejectProgress(ServiceStatus current, ServiceStatus prior)
    {
        bool rejected = false;
        try
        {
            current.RequireProgressFrom(prior);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
