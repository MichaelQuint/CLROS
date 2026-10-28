using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Own one short diagnostic: health, actual CoreLib HEAD, exact64KiB
/// range and health, followed by intentional runner cancellation and joining.
/// Original image extraction supplies expected bytes. This observation grants
/// neither full HTTP/browser qualification nor guest shutdown/cleanup acceptance.</summary>
public static class NativeAotDiskTimingPeer
{
    public const string Target = "/_framework/System.Private.CoreLib.wasm";
    public const string Marker = "M10_018_TIMING ";
    public const string DispatchMarker = "M10_018_DISPATCH ";
    public const string DriverMarker = "M10_018_DRIVER ";
    private static readonly string[] DispatchFields = ["bytes", "phaseNs", "pollNs", "dispatchNs", "nestedPollNs",
        "dispatchNonPollNs", "supervisorNonPollNs", "completed", "clipped", "physicalActive", "phaseComplete"];
    private static readonly string[] DriverFields = ["bytes", "phaseNs", "pollNs", "driverNs", "nestedPollNs",
        "driverNonPollNs", "dispatchShellNonPollNs", "completed", "clipped", "physicalActive", "phaseComplete"];
    private static readonly string[] Fields = ["bytes", "calls", "steps", "pumpNs", "scanNs", "flushNs",
        "preNetworkNs", "postNetworkNs", "postReadinessNs", "readinessRefreshNs", "readinessWaitScanNs", "postWaitRecordNs", "networkCalls", "diskReady", "otherReady", "noAdvance",
        "elapsed", "stepLimit", "failures", "flushedPastBudget", "pollNs", "diskRoundNs",
        "clockCalls", "clockNs", "clockPreNs", "clockDiskNs", "clockPostNs",
        "syscallCalls", "syscallNs", "syscallPreNs", "syscallDiskNs", "syscallPostNs",
        "supervisorCalls", "supervisorNs", "supervisorPreNs", "supervisorDiskNs", "supervisorPostNs",
        "idleCalls", "idleNs", "idlePreNs", "idleDiskNs", "idlePostNs"];

    public sealed class Completion
    {
        internal Completion(int port, string directory, ProcessResult runner) { Port=port;Directory=directory;Runner=runner; }
        public int Port { get; }
        public string Directory { get; }
        public ProcessResult Runner { get; }
    }

    /// <summary>Validate a copied, single bounded aggregate. Every successful
    /// invocation has exactly one exclusive yield reason. Durations overlap
    /// existing serial/device intervals and must not be added to those totals.
    /// This parser supplies no original-source, runner or native authority.</summary>
    public static IReadOnlyDictionary<string, ulong> InspectMetrics(string text)
    {
        Need(text.Length<=32*1024*1024,"metric_extent");
        string[] lines=text.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n')
            .Where(l=>l.StartsWith(Marker,StringComparison.Ordinal)).ToArray();
        Need(lines.Length==1 && lines[0].Length<=2048,"metric_count");
        string[] tokens=lines[0][Marker.Length..].Split(' ');Need(tokens.Length==Fields.Length,"metric_fields");
        Dictionary<string,ulong> values=new(StringComparer.Ordinal);
        for(int i=0;i<Fields.Length;i++)
        {
            string prefix=Fields[i]+"=";
            Need(tokens[i].StartsWith(prefix,StringComparison.Ordinal),"metric_field");
            string number=tokens[i][prefix.Length..];
            Need(number.Length is >0 and <=20 && (number.Length==1 || number[0]!='0') &&
                number.All(c=>c is >='0' and <='9') && ulong.TryParse(number,NumberStyles.None,CultureInfo.InvariantCulture,out _),"metric_number");
            values.Add(Fields[i],ulong.Parse(number,CultureInfo.InvariantCulture));
        }
        ulong calls=values["calls"],steps=values["steps"];
        Need(values["bytes"]==65536 && calls is >0 and <=10_000_000 && steps>=calls && steps<=calls*4 &&
            values["networkCalls"]==calls && values["failures"]==0 && values["flushedPastBudget"]<=steps &&
            values["postReadinessNs"]<=ulong.MaxValue-values["postWaitRecordNs"] &&
            values["postReadinessNs"]+values["postWaitRecordNs"]==values["postNetworkNs"] &&
            values["readinessRefreshNs"]<=ulong.MaxValue-values["readinessWaitScanNs"] &&
            values["readinessRefreshNs"]+values["readinessWaitScanNs"]==values["postReadinessNs"],"metric_bounds");
        ulong reasons=0;
        foreach(string field in new[]{"diskReady","otherReady","noAdvance","elapsed","stepLimit"})
        {Need(values[field]<=calls,"metric_reason");reasons+=values[field];}
        Need(reasons==calls,"metric_reasons");
        ulong causeCalls=0,causeTotal=0,causePre=0,causeDisk=0,causePost=0;
        foreach(string cause in new[]{"clock","syscall","supervisor","idle"})
        {
            ulong count=values[cause+"Calls"],total=values[cause+"Ns"],pre=values[cause+"PreNs"],disk=values[cause+"DiskNs"],post=values[cause+"PostNs"];
            Need(count<=calls && (count!=0 || total==0) && pre<=ulong.MaxValue-disk && pre+disk<=ulong.MaxValue-post &&
                pre+disk+post==total && causeCalls<=ulong.MaxValue-count && causeTotal<=ulong.MaxValue-total &&
                causePre<=ulong.MaxValue-pre && causeDisk<=ulong.MaxValue-disk && causePost<=ulong.MaxValue-post,"metric_cause");
            causeCalls+=count;causeTotal+=total;causePre+=pre;causeDisk+=disk;causePost+=post;
        }
        Need(causeCalls==calls && causeTotal==values["pollNs"] && causePre==values["preNetworkNs"] &&
            causeDisk==values["diskRoundNs"] && causePost==values["postNetworkNs"],"metric_causes");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,ulong>(values);
    }

    /// <summary>Validate a separate phase-complete dispatch partition against
    /// the unchanged v5 poll record. Physical activity at Freeze is explicit;
    /// neither record proves dispatch return or guest shutdown. Copied values
    /// grant no original source, native, runner or execution authority.</summary>
    public static IReadOnlyDictionary<string, ulong> InspectDispatchMetrics(string text, IReadOnlyDictionary<string, ulong> pollMetrics)
    {
        Need(text.Length <= 32 * 1024 * 1024, "dispatch_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith(DispatchMarker, StringComparison.Ordinal)).ToArray();
        Need(lines.Length == 1 && lines[0].Length <= 1024, "dispatch_count");
        string[] tokens = lines[0][DispatchMarker.Length..].Split(' '); Need(tokens.Length == DispatchFields.Length, "dispatch_fields");
        Dictionary<string, ulong> values = new(StringComparer.Ordinal);
        for (int i = 0; i < DispatchFields.Length; i++)
        {
            string prefix = DispatchFields[i] + "="; Need(tokens[i].StartsWith(prefix, StringComparison.Ordinal), "dispatch_field");
            string number = tokens[i][prefix.Length..];
            Need(number.Length is > 0 and <= 20 && (number.Length == 1 || number[0] != '0') && number.All(c => c is >= '0' and <= '9') &&
                ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _), "dispatch_number");
            values.Add(DispatchFields[i], ulong.Parse(number, CultureInfo.InvariantCulture));
        }
        ulong phase = values["phaseNs"], poll = values["pollNs"], dispatch = values["dispatchNs"], nested = values["nestedPollNs"];
        Need(values["bytes"] == 65536 && phase > 0 && pollMetrics.TryGetValue("pollNs", out ulong oldPoll) && oldPoll == poll &&
            poll <= phase && dispatch <= phase && nested <= poll && nested <= dispatch &&
            values["dispatchNonPollNs"] == dispatch - nested && dispatch - nested <= phase - poll &&
            values["supervisorNonPollNs"] == phase - poll - (dispatch - nested) &&
            values["completed"] <= 10_000_000 && values["clipped"] is > 0 and <= 2 &&
            values["physicalActive"] == 1 && values["phaseComplete"] == 1, "dispatch_partition");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(values);
    }

    /// <summary>Validate the generated-driver child of the already validated
    /// dispatch record. Subsets must be nonnegative and conserve the complete
    /// dispatch non-poll total. Driver time includes worker, guest, kernel,
    /// transitions, logging and measurement overhead; it is not guest-only CPU.
    /// Copied records supply no source, nesting, late-return or guest authority.</summary>
    public static IReadOnlyDictionary<string, ulong> InspectDriverMetrics(string text, IReadOnlyDictionary<string, ulong> dispatchMetrics)
    {
        Need(text.Length <= 32 * 1024 * 1024, "driver_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.StartsWith(DriverMarker, StringComparison.Ordinal)).ToArray();
        Need(lines.Length == 1 && lines[0].Length <= 1024, "driver_count");
        string[] tokens = lines[0][DriverMarker.Length..].Split(' '); Need(tokens.Length == DriverFields.Length, "driver_fields");
        Dictionary<string, ulong> values = new(StringComparer.Ordinal);
        for (int i = 0; i < DriverFields.Length; i++)
        {
            string prefix = DriverFields[i] + "="; Need(tokens[i].StartsWith(prefix, StringComparison.Ordinal), "driver_field");
            string number = tokens[i][prefix.Length..];
            Need(number.Length is > 0 and <= 20 && (number.Length == 1 || number[0] != '0') && number.All(c => c is >= '0' and <= '9') &&
                ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _), "driver_number");
            values.Add(DriverFields[i], ulong.Parse(number, CultureInfo.InvariantCulture));
        }
        ulong phase = 0, poll = 0, dispatch = 0, parentPoll = 0, parentNonPoll = 0;
        Need(dispatchMetrics.TryGetValue("phaseNs", out phase) && dispatchMetrics.TryGetValue("pollNs", out poll) &&
            dispatchMetrics.TryGetValue("dispatchNs", out dispatch) && dispatchMetrics.TryGetValue("nestedPollNs", out parentPoll) &&
            dispatchMetrics.TryGetValue("dispatchNonPollNs", out parentNonPoll), "driver_parent");
        ulong driver = values["driverNs"], nested = values["nestedPollNs"];
        Need(values["bytes"] == 65536 && phase > 0 && values["phaseNs"] == phase && values["pollNs"] == poll &&
            poll <= phase && dispatch <= phase && parentPoll <= poll && parentPoll <= dispatch && parentNonPoll == dispatch - parentPoll &&
            parentNonPoll <= phase - poll &&
            driver <= dispatch && nested <= driver && nested <= parentPoll &&
            parentPoll - nested <= dispatch - driver &&
            values["driverNonPollNs"] == driver - nested && values["driverNonPollNs"] <= parentNonPoll &&
            values["dispatchShellNonPollNs"] == (dispatch - driver) - (parentPoll - nested) &&
            values["dispatchShellNonPollNs"] == parentNonPoll - values["driverNonPollNs"] &&
            values["completed"] <= 10_000_000 && values["clipped"] is > 0 and <= 2 &&
            values["physicalActive"] == 1 && values["phaseComplete"] == 1, "driver_partition");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(values);
    }

    /// <summary>Leaves thirty seconds inside the admitted runner deadline for
    /// owned cancellation and joining; the diagnostic itself never exceeds480s.</summary>
    public static int PeerTimeoutSeconds(int runnerTimeoutSeconds)
    {
        Need(runnerTimeoutSeconds is >=240 and <=600,"runner_timeout");
        return Math.Min(480,runnerTimeoutSeconds-30);
    }

    /// <summary>Fresh artifacts must be inside root/artifacts. Caller retains
    /// exact image/source/native authorities until return. All failures preserve
    /// copied responses and the joined runner disposition. Cancellation of the
    /// original runner is deliberate only after every diagnostic check passes.</summary>
    public static async Task<Completion> RunAsync(string root,string serial,
        Func<int,CancellationToken,Task<ProcessResult>> runRunner,WebRootDiskInventory.Inspection inventory,
        int runnerTimeoutSeconds=600,CancellationToken cancellation=default)
    {
        int peerTimeoutSeconds=PeerTimeoutSeconds(runnerTimeoutSeconds);
        root=Path.GetFullPath(root);serial=Path.GetFullPath(serial,root);
        var file=inventory.Find(WebRootDiskInventory.Root+Target);
        Need(file is not null && !file.IsDirectory && file.Bytes>=65536,"corelib_inventory");
        var asset=new DiskStaticHttpContract.Asset(file!,true);byte[] range=file!.CopyBytes()[..65536];
        string directory=serial+".disk-timing";
        Need(directory.StartsWith(Path.Combine(root,"artifacts")+Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) && !Path.Exists(directory),"fresh_artifacts");
        AuthorityFiles.RequirePlainPath(root,Path.GetDirectoryName(directory)!,"disk_timing_path");Directory.CreateDirectory(directory);
        int port;using(TcpListener reserve=new(IPAddress.Loopback,0)){reserve.Start();port=((IPEndPoint)reserve.LocalEndpoint).Port;}
        using var owned=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(owned.Token);deadline.CancelAfter(TimeSpan.FromSeconds(peerTimeoutSeconds));
        Task<ProcessResult> runner=runRunner(port,owned.Token);Task? peer=null;bool complete=false;List<object> rows=[];
        IReadOnlyDictionary<string,ulong>? metrics=null, dispatchMetrics=null, driverMetrics=null;string? failure=null;
        try
        {
            peer=Observe();
            if(await Task.WhenAny(peer,runner)==runner)throw new ToolFailure("disk_timing_runner_exited_before_peer");
            await peer;cancellation.ThrowIfCancellationRequested();
            owned.Cancel();var joined=await runner;
            Need(joined.Cancelled && joined.TerminationConfirmed && !joined.TimedOut && !joined.OutputLimitExceeded,"runner_join");
            // A frozen phase line cannot erase a later physical-End failure.
            // Re-read only after the original runner joins; keep frozen values
            // but fail the diagnostic if later guest bookkeeping failed.
            string finalSerial = await ReadSerial(CancellationToken.None);
            InspectGuestStatus(finalSerial);
            Need(metrics is not null && dispatchMetrics is not null && driverMetrics is not null && InspectMetrics(finalSerial).SequenceEqual(metrics) &&
                InspectDispatchMetrics(finalSerial, metrics).SequenceEqual(dispatchMetrics) &&
                InspectDriverMetrics(finalSerial, dispatchMetrics).SequenceEqual(driverMetrics), "final_metrics_changed");
            using Socket stopped=new(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            using var refusal=new CancellationTokenSource(TimeSpan.FromSeconds(5));bool refused=false;
            try{await stopped.ConnectAsync(IPAddress.Loopback,port,refusal.Token);}
            catch(SocketException error)when(error.SocketErrorCode==SocketError.ConnectionRefused){refused=true;}
            Need(refused,"endpoint_live");complete=true;return new(port,directory,joined);
        }
        catch(Exception error)
        {failure=error is ToolFailure tool?tool.Reason:error.GetType().FullName;throw;}
        finally
        {
            owned.Cancel();if(peer is not null)try{await peer;}catch{}
            ProcessResult? joined=null;string? joinFault=null;
            try{joined=await runner;}catch(Exception error){joinFault=error.GetType().FullName;}
            // Preserve bounded original observations even on failure. Never
            // replace the original error with a secondary diagnostic I/O error.
            try
            {
                using FileStream receipt=new(Path.Combine(directory,"diagnostic.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                await JsonSerializer.SerializeAsync(receipt,new{Schema="clros-disk-read-timing-v7",Scope="diagnostic-only",Complete=complete,
                    FullAndCleanupQualified=false,Port=port,RunnerTimeoutSeconds=runnerTimeoutSeconds,PeerTimeoutSeconds=peerTimeoutSeconds,
                    ImageSha256=inventory.ImageSha256,Asset=asset,ClockFrequency=Stopwatch.Frequency,
                    Responses=rows,Metrics=metrics,DispatchMetrics=dispatchMetrics,DriverMetrics=driverMetrics,Failure=failure,RunnerJoined=true,Runner=joined,JoinFault=joinFault},cancellationToken:CancellationToken.None);
                receipt.Flush(true);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }

        async Task Observe()
        {
            await NativeAotSocketPeer.WaitForSerialAsync(serial,text=>text.Contains("M10_018_LISTENING ",StringComparison.Ordinal),deadline.Token);
            using HttpClient client=new(new SocketsHttpHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None,
                UseProxy=false,MaxConnectionsPerServer=1}){BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(180),
                    DefaultRequestVersion=HttpVersion.Version11,DefaultVersionPolicy=HttpVersionPolicy.RequestVersionExact};
            await Request("GET","/healthz",200,"OS_MINIMAL_API_OK"u8.ToArray());
            await Request("HEAD",Target,200,[]);
            await Request("GET",Target,206,range);
            await Request("GET","/healthz",200,"OS_MINIMAL_API_OK"u8.ToArray());
            while(true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                string text = await ReadSerial(deadline.Token); InspectGuestStatus(text);
                if(text.Split('\n').Any(line=>line.StartsWith(DriverMarker,StringComparison.Ordinal)) &&
                    text.LastIndexOf('\n')>text.IndexOf(DriverMarker,StringComparison.Ordinal))
                { metrics=InspectMetrics(text);dispatchMetrics=InspectDispatchMetrics(text,metrics);driverMetrics=InspectDriverMetrics(text,dispatchMetrics);break; }
                await Task.Delay(50,deadline.Token);
            }

            async Task Request(string method,string path,int status,byte[] expected)
            {
                long start=Stopwatch.GetTimestamp();using var request=new HttpRequestMessage(new HttpMethod(method),path);
                bool ranged=status==206;if(ranged)request.Headers.Range=new(0,65535);
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
                using var source=await response.Content.ReadAsStreamAsync(deadline.Token);using MemoryStream body=new();byte[] buffer=new byte[8192];
                for(int count;(count=await source.ReadAsync(buffer,deadline.Token))!=0;)
                {Need(body.Length+count<=65536,"body_bound");body.Write(buffer,0,count);}
                byte[] actual=body.ToArray();int ordinal=rows.Count;
                string bodyFile=ordinal.ToString("D3",CultureInfo.InvariantCulture)+".body";
                using(FileStream output=new(Path.Combine(directory,bodyFile),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                {await output.WriteAsync(actual,deadline.Token);output.Flush(true);}
                rows.Add(new{Ordinal=ordinal,Method=method,Path=path,Status=(int)response.StatusCode,Started=start,Finished=Stopwatch.GetTimestamp(),
                    Bytes=actual.Length,Sha256=Convert.ToHexString(SHA256.HashData(actual)),BodyFile=bodyFile,
                    RequestHeaders=request.Headers.ToString(),ResponseHeaders=response.Headers.ToString()+response.Content.Headers.ToString()});
                Need((int)response.StatusCode==status && actual.AsSpan().SequenceEqual(expected),"response");
                if(path==Target)
                    Need(response.Headers.ETag?.ToString()==asset.ETag && response.Content.Headers.ContentType?.ToString()==asset.MediaType &&
                        response.Content.Headers.ContentLength==(ranged?65536:asset.Bytes) &&
                        (ranged?response.Content.Headers.ContentRange?.ToString()=="bytes 0-65535/"+asset.Bytes:response.Content.Headers.ContentRange is null),"metadata");
            }
        }

        async Task<string> ReadSerial(CancellationToken token)
        {
            using FileStream input = new(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Need(input.Length <= 32 * 1024 * 1024, "serial_extent");
            using StreamReader reader = new(input, Encoding.UTF8, false); return await reader.ReadToEndAsync(token);
        }
        void InspectGuestStatus(string text) => Need(!text.Contains("TEST_FAIL", StringComparison.Ordinal) &&
            !text.Contains("M16_008_STOP ", StringComparison.Ordinal), "guest_failure");
    }

    private static void Need(bool valid,string reason){if(!valid)throw new ToolFailure("disk_timing_"+reason);}
}
