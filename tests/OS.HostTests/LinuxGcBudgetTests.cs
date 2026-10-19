using System.Text;
using OS.Manifest;
using Config = OS.Manifest.LinuxAppConfiguration;

internal static unsafe class LinuxGcBudgetTests
{
    private static string Text(string env, uint memory = 64 * 1024 * 1024) =>
        "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/gc.elf\nargs=\ndigest=" + new string('0',64) +
        "\nmemory=" + memory + "\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\n" + env;
    private static Config.GcBudget Read(string env, uint pages = 8192, ulong statics = 16 * 1024 * 1024,
        ulong native = 16 * 1024 * 1024, bool required = true, uint memory = 64 * 1024 * 1024)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(Text(env,memory)), before = bytes.ToArray();
        fixed (byte* p = bytes)
        {
            var m = LinuxElfPackage.ParseManifest(p,(uint)bytes.Length);
            var result = Config.ReadGcBudget(p,(uint)bytes.Length,m,pages,statics,native,required);
            HostCheck.True(bytes.SequenceEqual(before)); return result;
        }
    }
    private const string Limit = "env.DOTNET_GCHeapHardLimit=1000000\n";

    [HostTest("linux app GC budget binds absolute limits to actual pool and static/native reservations without system RAM")]
    private static void AbsoluteBudget()
    {
        var result = Read(Limit); HostCheck.True(result.Succeeded);
        HostCheck.Equal(32UL*1024*1024,result.PoolBytes); HostCheck.Equal(16UL*1024*1024,result.GcLimitBytes);
        HostCheck.Equal(result.GcLimitBytes,result.NativeReserveBytes); HostCheck.Equal(16UL*1024*1024,result.StaticBytes);
        HostCheck.Equal(8UL*1024*1024,Read("env.DOTNET_GCHeapHardLimit=0X800000\n").GcLimitBytes);
        HostCheck.True(!default(Config.GcBudget).Succeeded);
    }

    [HostTest("linux app GC budget rejects malformed unaligned small and overflowing hexadecimal limits")]
    private static void InvalidLimits()
    {
        foreach(string value in new[]{"","0","7fffff","800001","-800000"," 800000","10000000000000000","NaN","0x"})
        { var result=Read("env.DOTNET_GCHeapHardLimit="+value+"\n");HostCheck.True(!result.Succeeded&&result.Result!=Config.Status.Unconfigured,value);HostCheck.Equal(0UL,result.GcLimitBytes); }
    }

    [HostTest("linux app GC budget rejects insufficient malformed and overflowing entitlements before reservation")]
    private static void Entitlements()
    {
        foreach(var (pool,statics,native,memory) in new (uint,ulong,ulong,uint)[]{(0,4096,4096,67108864),(uint.MaxValue,4096,4096,67108864),
            (8192,0,16777216,67108864),(8192,1,16777216,67108864),(8192,ulong.MaxValue-4095,16777216,67108864),
            (8192,16777216,0,67108864),(8192,16777216,1,67108864),(8192,16777216,33554432,67108864),
            (8192,16777216,20971520,67108864),(8192,16777216,16777216,33554432)})
            HostCheck.Equal(Config.Status.InvalidEntitlement,Read(Limit,pool,statics,native,memory:memory).Result);
    }

    [HostTest("linux app GC budget rejects percentage aliases and competing overrides while preserving ordinary environment")]
    private static void Overrides()
    {
        foreach(string key in new[]{"DOTNET_GCHEAPHARDLIMIT","DOTNET_GCHEAPHARDLIMITPERCENT","DOTNET_GCSERVER","COMPLUS_GCHEAPHARDLIMIT"})
            HostCheck.Equal(Config.Status.UnsupportedOverride,Read(Limit+"env."+key+"=800000\n").Result);
        HostCheck.True(Read(Limit+"env.TEST=value\n").Succeeded);
        HostCheck.Equal(Config.Status.InvalidManifest,Read(Limit+"env.DOTNET_GCHeapHardLimitPercent=19\n").Result);
    }

    [HostTest("linux app GC budget keeps legacy absence unconfigured and requires explicit settings only when selected")]
    private static void Missing()
    {
        HostCheck.Equal(Config.Status.MissingLimit,Read("").Result);
        HostCheck.Equal(Config.Status.Unconfigured,Read("env.TEST=value\n",pages:0,statics:0,native:0,required:false).Result);
    }

    [HostTest("linux app GC budget rejects forged field ranges duplicate settings and inconsistent manifest lengths")]
    private static void Metadata()
    {
        byte[] bytes=Encoding.ASCII.GetBytes(Text(Limit));fixed(byte* p=bytes)
        {
            uint length=(uint)bytes.Length;var m=LinuxElfPackage.ParseManifest(p,length);HostCheck.True(m.Succeeded);
            var bad=m;bad.EnvironmentRanges[0]=length;HostCheck.Equal(Config.Status.InvalidManifest,Config.ReadGcBudget(p,length,bad,8192,16777216,16777216).Result);
            bad=m;bad.EnvironmentRanges[3]=uint.MaxValue;HostCheck.Equal(Config.Status.InvalidManifest,Config.ReadGcBudget(p,length,bad,8192,16777216,16777216).Result);
            bad=m;bad.EnvironmentCount=2;for(int i=0;i<4;i++)bad.EnvironmentRanges[4+i]=bad.EnvironmentRanges[i];
            HostCheck.Equal(Config.Status.UnsupportedOverride,Config.ReadGcBudget(p,length,bad,8192,16777216,16777216).Result);
            HostCheck.Equal(Config.Status.InvalidManifest,Config.ReadGcBudget(p,length-1,m,8192,16777216,16777216).Result);
            HostCheck.Equal(Config.Status.InvalidManifest,Config.ReadGcBudget(null,length,m,8192,16777216,16777216).Result);
        }
    }
}
