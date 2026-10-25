using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// Explicit finite qualification workload. Its synchronous FileStream calls and
// requested Flush(true) require separate Linux/CLROS measurement. The response
// callback requests shutdown; only an independent client can prove delivery.
internal static class Program
{
    private const int UploadBytes = 5126, FirstChunk = 4094, StartupBytes = 330;
    private const string LogName = "LOG.TXT", UploadName = "UPLOAD.BIN";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("TEST_START m10.027.http");
        try
        {
            Require(args is ["--serve-once", "--root", _, "--urls", _], "arguments");
            Require(Path.IsPathFullyQualified(args[2]), "absolute_root_required");
            string root = Path.GetFullPath(args[2]);
            Require(Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0, "root");
            Require(Uri.TryCreate(args[4], UriKind.Absolute, out var url) && url.Scheme == "http" &&
                url.AbsolutePath == "/" && url.Query.Length == 0 && url.Fragment.Length == 0 && url.UserInfo.Length == 0,
                "url");
            string log = Path.Combine(root, LogName), upload = Path.Combine(root, UploadName);
            RequirePlain(log); RequirePlain(upload);
            using (var first = Open(log, FileMode.Open, FileAccess.Read))
            using (var second = Open(upload, FileMode.Open, FileAccess.Read))
                Require(first.Length == 0 && second.Length == 0, "empty_inputs_required");
            Append(log, 1);
            Console.WriteLine("LOG_UPLOAD_HTTP_POLICY serve=once bodyBytes=5126 logRows=6+6 chunkBytes=4094+1032 flushRequested=true");
            Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = ["--urls", args[4]], ContentRootPath = root });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = UploadBytes;
                options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1);
            });
            await using var app = builder.Build();
            int phase = 0, delivered = 0;
            byte[]? accepted = null;
            string Phase() => Volatile.Read(ref phase) switch { 0 => "ready", 1 => "receiving", 2 => "writing", 3 => "complete", _ => "failed" };
            void FailAfterResponse(HttpContext context)
            {
                Interlocked.Exchange(ref phase, 4);
                context.Response.OnCompleted(() => { app.Lifetime.StopApplication(); return Task.CompletedTask; });
            }
            app.MapGet("/healthz", (RequestDelegate)(async context =>
            {
                context.Response.Headers["X-LogUpload-Phase"] = Phase();
                await Text(context, 200, "LOG_UPLOAD_HTTP_OK\n");
            }));
            app.MapPost("/upload", (RequestDelegate)(async context =>
            {
                if (context.Request.QueryString.HasValue) { context.Response.StatusCode = 400; return; }
                if (context.Request.ContentLength > UploadBytes) { context.Response.StatusCode = 413; return; }
                if (context.Request.ContentLength != UploadBytes) { context.Response.StatusCode = 400; return; }
                if (!string.Equals(context.Request.ContentType, "application/octet-stream", StringComparison.Ordinal))
                { context.Response.StatusCode = 415; return; }
                if (Interlocked.CompareExchange(ref phase, 1, 0) != 0) { context.Response.StatusCode = 409; return; }
                bool effectsStarted = false;
                try
                {
                    // No output is opened until the entire bounded request is
                    // received. An aborted or short body keeps startup state.
                    byte[] body = new byte[UploadBytes]; int offset = 0;
                    while (offset < body.Length)
                    {
                        int read = await context.Request.Body.ReadAsync(body.AsMemory(offset), context.RequestAborted);
                        if (read == 0) { context.Response.StatusCode = 400; return; }
                        offset += read;
                    }
                    context.RequestAborted.ThrowIfCancellationRequested();
                    effectsStarted = true; Interlocked.Exchange(ref phase, 2);
                    using (var input = Open(log, FileMode.Open, FileAccess.Read))
                        Require(input.Length == StartupBytes, "startup_log_changed");
                    using (var output = Open(upload, FileMode.Open, FileAccess.Write))
                    {
                        Require(output.Length == 0, "upload_changed");
                        output.Write(body.AsSpan(0, FirstChunk)); output.Write(body.AsSpan(FirstChunk));
                        output.Flush(flushToDisk: true);
                    }
                    Append(log, 7);
                    accepted = body; Interlocked.Exchange(ref phase, 3);
                    Console.WriteLine("LOG_UPLOAD_HTTP_ACCEPTED logBytes=660 uploadBytes=5126");
                    await Text(context, 201, "LOG_UPLOAD_HTTP_ACCEPTED\n");
                }
                catch (Exception error) when (!effectsStarted && error is IOException or OperationCanceledException or Microsoft.AspNetCore.Http.BadHttpRequestException)
                { if (!context.RequestAborted.IsCancellationRequested) context.Response.StatusCode = 400; }
                catch (Exception)
                {
                    // No rollback is asserted after an output was opened. The
                    // owner retains the actual bytes and a failed receipt.
                    FailAfterResponse(context); if (!context.Response.HasStarted) context.Response.StatusCode = 500;
                    if (context.RequestAborted.IsCancellationRequested) app.Lifetime.StopApplication();
                }
                finally { if (Volatile.Read(ref phase) == 1) Interlocked.Exchange(ref phase, 0); }
            }));
            app.MapGet("/log", (RequestDelegate)(async context =>
            {
                int current = Volatile.Read(ref phase);
                if (current is not (0 or 3) || context.Request.QueryString.HasValue) { context.Response.StatusCode = 409; return; }
                byte[] expected = LogPayload(current == 0 ? 6 : 12);
                try
                {
                    byte[] bytes = Read(log, expected);
                    context.Response.ContentType = "text/plain; charset=utf-8"; context.Response.ContentLength = bytes.Length;
                    await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
                }
                catch (Exception) { FailAfterResponse(context); if (!context.Response.HasStarted) context.Response.StatusCode = 500; }
            }));
            app.MapGet("/upload", (RequestDelegate)(async context =>
            {
                if (Volatile.Read(ref phase) != 3 || accepted is null || context.Request.QueryString.HasValue ||
                    Interlocked.CompareExchange(ref delivered, 1, 0) != 0) { context.Response.StatusCode = 409; return; }
                try
                {
                    Read(log, LogPayload(12)); byte[] bytes = Read(upload, accepted);
                    context.Response.ContentType = "application/octet-stream"; context.Response.ContentLength = bytes.Length;
                    context.Response.OnCompleted(() => { Interlocked.Exchange(ref delivered, 2); app.Lifetime.StopApplication(); return Task.CompletedTask; });
                    await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
                }
                catch (Exception) { FailAfterResponse(context); if (!context.Response.HasStarted) context.Response.StatusCode = 500; }
            }));
            app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine("LOG_UPLOAD_HTTP_READY startupLogBytes=330 uploadBytes=0"));
            await app.RunAsync();
            Require(Volatile.Read(ref phase) == 3 && Volatile.Read(ref delivered) == 2, "incomplete");
            Console.WriteLine("TEST_PASS m10.027.http"); return 0;
        }
        catch (Exception error) { Console.WriteLine("TEST_FAIL m10.027.http " + error.GetType().Name); return 1; }
    }

    private static async Task Text(HttpContext context, int status, string text)
    {
        byte[] bytes = Utf8.GetBytes(text); context.Response.StatusCode = status;
        context.Response.ContentType = "text/plain; charset=utf-8"; context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
    private static FileStream Open(string path, FileMode mode, FileAccess access)
    {
        var options = new FileStreamOptions { Mode = mode, Access = access, Share = FileShare.ReadWrite, BufferSize = 0, Options = FileOptions.None };
        if (OperatingSystem.IsLinux() && mode == FileMode.Append) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new(path, options);
    }
    private static void Append(string path, int first)
    {
        using var output = Open(path, FileMode.Append, FileAccess.Write);
        for (int row = first; row < first + 6; row++) output.Write(Record(row));
        output.Flush(flushToDisk: true);
    }
    private static byte[] Read(string path, byte[] expected)
    {
        using var input = Open(path, FileMode.Open, FileAccess.Read); Require(input.Length == expected.Length, "length");
        byte[] bytes = new byte[expected.Length]; int offset = 0;
        while (offset < bytes.Length)
        {
            int read = input.Read(bytes.AsSpan(offset, Math.Min(521, bytes.Length - offset)));
            Require(read > 0, "eof"); offset += read;
        }
        Require(input.ReadByte() == -1 && bytes.AsSpan().SequenceEqual(expected), "bytes"); return bytes;
    }
    private static byte[] Record(int row) => Utf8.GetBytes("CLROS_LOG v1 sequence=" + row.ToString("D2", CultureInfo.InvariantCulture) + " message=CLROS ready \u2014 caf\u00e9\n");
    private static byte[] LogPayload(int count)
    { using MemoryStream bytes = new(); for (int row = 1; row <= count; row++) bytes.Write(Record(row)); return bytes.ToArray(); }
    private static void RequirePlain(string path) => Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "file");
    private static void Require(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }
}
