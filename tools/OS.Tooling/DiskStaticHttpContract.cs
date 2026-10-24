using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

/// <summary>Small first original-owner development contract. These five GETs
/// establish neither the full middleware matrix nor a browser/framework boot.
/// The enclosing consumer checks actual bytes, metadata, packets and shutdown.</summary>
public static class DiskStaticHttpContract
{
    public const string Schema="clros-disk-http-development-v1";
    public const string Scope="five-response-development";
    public const int MaximumBodyBytes=65536,MatrixTimeoutSeconds=110;
    public static IReadOnlyList<string> Paths {get;}=Array.AsReadOnly(new[]
        {"/healthz","/api/status","/api/items","/index.html","/fixture-nonce.txt"});

    public const string AssetSchema="clros-disk-http-assets-v1";
    public const string AssetScope="published-asset-matrix";
    public const int MaximumPublishedAssets=128,MaximumAssetBytes=2*1024*1024;
    public const int MaximumPublishedBytes=8*1024*1024,MaximumExpectedBodyBytes=16*1024*1024;
    public const int MaximumAssetRequests=320,MaximumReadCompletionBytes=8192;
    private static readonly string[] FixturePaths=["fixture-nonce.txt","fixture/empty.bin","fixture/large.bin",
        "fixture/fragmented.bin","fixture/nested/info.txt"];
    private static readonly IReadOnlyList<Header> NoHeaders=Array.AsReadOnly(Array.Empty<Header>());
    private static readonly string EmptyHash=Convert.ToHexString(SHA256.HashData(ReadOnlySpan<byte>.Empty));

    public readonly record struct Header(string Name,string Value);

    /// <summary>Copied immutable identity and upstream static metadata from one
    /// independently extracted physical file. Target escapes each path segment;
    /// physical Path remains the exact confined inventory key. No content buffer,
    /// mutable inventory alias, device or process authority is retained.</summary>
    public sealed class Asset
    {
        public string Path {get;}
        public string Target {get;}
        public ulong Inode {get;}
        public uint Bytes {get;}
        public string Sha256 {get;}
        public string MediaType {get;}
        public string ETag {get;}
        public string LastModified {get;}
        public bool Published {get;}
        internal Asset(WebRootDiskInventory.Entry file,bool published)
        {
            Path=file.Path;Target="/"+string.Join('/',file.Path[(WebRootDiskInventory.Root.Length+1)..].Split('/').Select(Uri.EscapeDataString));
            Inode=file.Inode;Bytes=file.Bytes;Sha256=file.Sha256;Published=published;MediaType=Media(file.Path);
            var modified=DateTimeOffset.FromUnixTimeSeconds(file.ModifiedSeconds);
            LastModified=modified.ToString("r",CultureInfo.InvariantCulture);
            ETag="\""+(modified.ToFileTime()^file.Bytes).ToString("x",CultureInfo.InvariantCulture)+"\"";
        }
    }

    /// <summary>One planned HTTP exchange. BodyOffset/BodyBytes describe exact
    /// entity bytes in Asset, independently hashed during planning. HEAD/304/416
    /// and missing routes have no body; nullable ContentLength allows 304 to omit
    /// that header. MinimumReadCompletions is only a lower bound from the current
    /// 8 KiB completion prefix, never a prediction of actual read sizes, cache
    /// state or timing. A consumer must join every actual read and body send.</summary>
    public sealed class Request
    {
        public int Ordinal {get;}
        public string Method {get;}
        public string Target {get;}
        public int Status {get;}
        public Asset? Asset {get;}
        public IReadOnlyList<Header> Headers {get;}
        public uint BodyOffset {get;}
        public uint BodyBytes {get;}
        public string BodySha256 {get;}
        public long? ContentLength {get;}
        public string? ContentRange {get;}
        public int MinimumReadCompletions=>checked((int)((BodyBytes+(ulong)MaximumReadCompletionBytes-1)/MaximumReadCompletionBytes));
        internal Request(int ordinal,string method,string target,int status,Asset? asset,IReadOnlyList<Header> headers,
            uint offset,uint bytes,string hash,long? contentLength,string? contentRange)
        {
            Ordinal=ordinal;Method=method;Target=target;Status=status;Asset=asset;Headers=headers;
            BodyOffset=offset;BodyBytes=bytes;BodySha256=hash;ContentLength=contentLength;ContentRange=contentRange;
        }
    }

    /// <summary>Owned immutable request inventory, without response buffers or
    /// execution authority. Published identity uses the original fixture's
    /// relative-path/length/hash format, excluding its five synthetic files.</summary>
    public sealed class AssetPlan
    {
        public string Schema=>AssetSchema;
        public string Scope=>AssetScope;
        public string ImageSha256 {get;}
        public string PublishedInventorySha256 {get;}
        public int PublishedFiles {get;}
        public long PublishedBytes {get;}
        public long ExpectedBodyBytes {get;}
        public int MinimumReadCompletions {get;}
        public IReadOnlyList<Asset> Assets {get;}
        public IReadOnlyList<Request> Requests {get;}
        public bool GrantsOriginalNativeProcessOrBrowserAuthority=>false;
        internal AssetPlan(string imageHash,string publishedHash,Asset[] assets,Request[] requests)
        {
            ImageSha256=imageHash;PublishedInventorySha256=publishedHash;
            Assets=Array.AsReadOnly(assets);Requests=Array.AsReadOnly(requests);
            PublishedFiles=assets.Count(a=>a.Published);PublishedBytes=assets.Where(a=>a.Published).Sum(a=>(long)a.Bytes);
            ExpectedBodyBytes=requests.Sum(r=>(long)r.BodyBytes);MinimumReadCompletions=requests.Sum(r=>r.MinimumReadCompletions);
        }
    }

    /// <summary>Synchronously borrow a completed independent image inspection
    /// and build a deterministic bounded GET/HEAD matrix over every published
    /// file and the five disk fixtures. Include the index alias, cross-sector,
    /// suffix and tail ranges, unsatisfiable range, ETag/mtime conditionals,
    /// matching/mismatching If-Range, and missing/private/POST controls. Only the
    /// server's explicit uncompressed media allowlist is admitted. Require a
    /// bootstrap, index, wasm and an asset long enough for the range controls.
    /// Invalid shape/capacity/type throws ToolFailure before any HTTP or process
    /// operation. Planning does not qualify middleware, disk reads or Blazor;
    /// browser traffic, concurrency, fault images and original owners are separate.</summary>
    public static AssetPlan CreateAssetPlan(WebRootDiskInventory.Inspection inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var files=inventory.Entries.Where(e=>!e.IsDirectory).OrderBy(e=>e.Path,StringComparer.Ordinal).ToArray();
        Need(files.Length is >5 and <=MaximumPublishedAssets+5 && files.All(f=>f.Bytes<=MaximumAssetBytes),"asset_capacity");
        bool Fixture(WebRootDiskInventory.Entry file)=>FixturePaths.Contains(file.Path[(WebRootDiskInventory.Root.Length+1)..],StringComparer.Ordinal);
        var published=files.Where(f=>!Fixture(f)).ToArray();
        Need(published.Length is >0 and <=MaximumPublishedAssets && published.Sum(f=>(long)f.Bytes)<=MaximumPublishedBytes &&
            FixturePaths.All(p=>files.Any(f=>f.Path==WebRootDiskInventory.Root+"/"+p)),"published_capacity");
        Need(published.Any(f=>f.Path==WebRootDiskInventory.Root+"/index.html") &&
            published.Any(f=>f.Path==WebRootDiskInventory.Root+"/_framework/blazor.webassembly.js") &&
            published.Any(f=>f.Path.EndsWith(".wasm",StringComparison.OrdinalIgnoreCase)),"bootstrap");
        Asset[] assets=files.Select(f=>new Asset(f,!Fixture(f))).ToArray();
        var byPath=assets.ToDictionary(a=>a.Path,StringComparer.Ordinal);
        var largest=published.OrderByDescending(f=>f.Bytes).ThenBy(f=>f.Path,StringComparer.Ordinal).First();
        Need(largest.Bytes>1031,"range_asset");
        List<Request> requests=[];long expectedBytes=0;
        void Add(string method,string target,int status,WebRootDiskInventory.Entry? file=null,uint offset=0,uint? length=null,
            long? contentLength=null,string? contentRange=null,params Header[] headers)
        {
            uint count=length??(method=="GET" && status==200?file?.Bytes??0:0);
            Need(requests.Count<MaximumAssetRequests && (file is not null || count==0) &&
                (file is null || offset<=file.Bytes && count<=file.Bytes-offset) &&
                expectedBytes+count<=MaximumExpectedBodyBytes,"request_capacity");
            string hash=count==0?EmptyHash:Convert.ToHexString(SHA256.HashData(file!.CopyBytes().AsSpan(checked((int)offset),checked((int)count))));
            long? actualLength=contentLength??(status==304?null:method=="HEAD"?file!.Bytes:count);
            requests.Add(new(requests.Count,method,target,status,file is null?null:byPath[file.Path],
                headers.Length==0?NoHeaders:Array.AsReadOnly(headers.ToArray()),offset,count,hash,actualLength,contentRange));
            expectedBytes+=count;
        }
        foreach(var file in files)
        {
            string target=byPath[file.Path].Target;
            Add("GET",target,200,file);Add("HEAD",target,200,file);
        }
        var index=files.Single(f=>f.Path==WebRootDiskInventory.Root+"/index.html");
        Add("GET","/",200,index);Add("HEAD","/",200,index);
        var selected=byPath[largest.Path];string total=largest.Bytes.ToString(CultureInfo.InvariantCulture);
        void Range(uint offset,uint count,string header,params Header[] extra)=>Add("GET",selected.Target,206,largest,offset,count,
            count,"bytes "+offset.ToString(CultureInfo.InvariantCulture)+"-"+(offset+count-1).ToString(CultureInfo.InvariantCulture)+"/"+total,
            [new("Range",header),..extra]);
        Range(510,522,"bytes=510-1031");
        Range(largest.Bytes-513,513,"bytes=-513");
        Range(largest.Bytes-32,32,"bytes="+(largest.Bytes-32).ToString(CultureInfo.InvariantCulture)+"-");
        Add("GET",selected.Target,416,largest,contentRange:"bytes */"+total,headers:[new("Range","bytes="+total+"-")]);
        Add("GET",selected.Target,304,largest,headers:[new("If-None-Match",selected.ETag)]);
        Add("GET",selected.Target,304,largest,headers:[new("If-Modified-Since",selected.LastModified)]);
        Range(510,522,"bytes=510-1031",new Header("If-Range",selected.ETag));
        Add("GET",selected.Target,200,largest,headers:[new("Range","bytes=510-1031"),new("If-Range","\"clros-other-file\"")]);
        foreach(string path in new[]{"/.clros-missing","/private/sentinel.txt","/index.html.exe"})
        {
            Need(inventory.Find(WebRootDiskInventory.Root+path) is null,"control_collision");Add("GET",path,404);
        }
        Add("POST","/index.html",404);
        string publishedText=string.Concat(published.Select(f=>f.Path[(WebRootDiskInventory.Root.Length+1)..]+"\t"+
            f.Bytes.ToString(CultureInfo.InvariantCulture)+"\t"+f.Sha256+"\n"));
        return new(inventory.ImageSha256,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(publishedText))),assets,requests.ToArray());
    }

    private static string Media(string path)=>System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html"=>"text/html",".css"=>"text/css",".js" or ".mjs"=>"text/javascript",".json"=>"application/json",
        ".txt"=>"text/plain",".wasm"=>"application/wasm",".dat" or ".bin"=>"application/octet-stream",
        _=>throw new ToolFailure("disk_http_plan_media")
    };
    private static void Need(bool condition,string reason){if(!condition)throw new ToolFailure("disk_http_plan_"+reason);}
}
