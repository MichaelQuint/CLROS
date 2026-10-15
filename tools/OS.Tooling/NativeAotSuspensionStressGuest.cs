using System.Globalization;
using System.Text;

namespace OS.Tooling;

/// <summary>Strict host-only observations for the separate GC stress workload.
/// Output is borrowed synchronously and supplies no producer, native suspension,
/// exact exit/GC overlap, kernel wait ownership or CLROS execution authority.</summary>
public static class NativeAotSuspensionStressGuest
{
    /// <summary>Require all three lifetimes, nine explicit full collections,
    /// running-worker progress and joined root/interior/TLS/operation witnesses.
    /// Missing, failed, extra or inconsistent records throw ToolFailure. Actual
    /// native/guest trace and cleanup admission remain separate requirements.</summary>
    public static void ValidateOutput(string text)
    {
        Need(text.Length is >0 and <=4096,"size");
        text=text.Replace("\r\n","\n",StringComparison.Ordinal);
        if(text.EndsWith('\n'))text=text[..^1];
        string[] rows=text.Split('\n');
        Need(rows.Length==14 && rows[0]=="TEST_START nativeaot.gc-stress" && rows[^1]=="TEST_PASS nativeaot.gc-stress","inventory");
        long lastCollection=0;
        for(int lifetime=1;lifetime<=3;lifetime++)
        {
            long lastProgress=0;
            for(int epoch=1;epoch<=3;epoch++)
            {
                string[] row=rows[(lifetime-1)*4+epoch].Split(' ');
                Need(row.Length==9 && row[0]=="NATIVEAOT_GC_STRESS_COLLECT" && row[^1]=="waiting=3","collection_record");
                long life=Number(row[1],"lifetime",3),order=Number(row[2],"epoch",3);
                long before=Number(row[3],"before",int.MaxValue),after=Number(row[4],"after",int.MaxValue);
                long progressBefore=Number(row[5],"progressBefore",long.MaxValue),returned=Number(row[6],"progressReturned",long.MaxValue),progressAfter=Number(row[7],"progressAfter",long.MaxValue);
                Need(life==lifetime && order==epoch && before>=lastCollection && after>before && progressBefore>0 &&
                    progressBefore>=lastProgress && returned>=progressBefore && progressAfter>returned,"collection_progress");
                lastCollection=after;lastProgress=progressAfter;
            }
            Need(rows[lifetime*4]==FormattableString.Invariant($"NATIVEAOT_GC_STRESS_JOIN lifetime={lifetime} started=5 joined=5 completed=5 roots=5 interior=5 tls=5 sleepStarts=1 exitRequests=1"),"joined_witness");
        }
    }

    /// <summary>Read a bounded strict UTF8 capture without write/delete sharing.
    /// Caller retains file ownership. I/O, encoding and invalid output throw;
    /// no workload executes and the file is never changed.</summary>
    public static void ValidateFile(string path)
    {
        using FileStream stream=new(Path.GetFullPath(path),FileMode.Open,FileAccess.Read,FileShare.Read);
        Need(stream.Length is >0 and <=4096,"size");
        byte[] bytes=new byte[(int)stream.Length];stream.ReadExactly(bytes);
        Need(stream.ReadByte()==-1,"changed");
        ValidateOutput(new UTF8Encoding(false,true).GetString(bytes));
    }

    private static long Number(string field,string name,long maximum)
    {
        string prefix=name+"=";Need(field.StartsWith(prefix,StringComparison.Ordinal),"field");
        string value=field[prefix.Length..];
        Need(long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out long number) && number<=maximum &&
            number.ToString(CultureInfo.InvariantCulture)==value,"number");
        return number;
    }
    private static void Need(bool value,string reason)
    {if(!value)throw new ToolFailure("nativeaot_gc_stress_"+reason);}
}
