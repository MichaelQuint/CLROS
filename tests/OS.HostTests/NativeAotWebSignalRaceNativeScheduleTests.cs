using System.Diagnostics;
using System.Reflection;
using OS.Tooling;

// Pure private-parser and non-Linux refusal controls. No Linux scheduler,
// pidfd, native child or private Completion is created by these tests.
internal static class NativeAotWebSignalRaceNativeScheduleTests
{
    private static Type Schedule=>typeof(NativeAotWebPayload).GetNestedType("SignalRaceNativeSchedule",BindingFlags.NonPublic)!;
    private static object Parse(string method,params object[] args)=>Schedule.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args)!;
    private static void Refused(string method,params object[] args)
    {
        Exception? failure=null;try{_=Parse(method,args);}catch(TargetInvocationException error){failure=error.InnerException;}
        HostCheck.True(failure is not null);
    }

    [HostTest("web native schedule stat parser retains field22 across spaces and closing parentheses in comm")]
    private static void StatFields()
    {
        string[] fields=Enumerable.Repeat("0",50).ToArray();fields[0]="S";fields[1]="11";fields[2]="12";fields[3]="13";fields[19]="987654321";
        object stat=Parse("ParseStat","17 (worker ) with spaces) "+string.Join(' ',fields));
        object Value(string name)=>stat.GetType().GetProperty(name)!.GetValue(stat)!;
        HostCheck.True((int)Value("Tid")==17&&(int)Value("Parent")==11&&(int)Value("Group")==12&&
            (int)Value("Session")==13&&(ulong)Value("Start")==987654321&&(string)Value("State")=="S");
        Refused("ParseStat","17 worker S 1 2 3");Refused("ParseStat","17 (worker) S 1 2 3");
        fields[19]="not-a-clock";Refused("ParseStat","17 (worker) "+string.Join(' ',fields));
    }

    [HostTest("web native schedule parses original octal pipe flags and anchored status fields")]
    private static void KernelFields()
    {
        HostCheck.Equal(0x80800U,(uint)Parse("PipeFlags","pos:\t0\nflags:\t02004000\nmnt_id:\t18\n"));
        HostCheck.Equal(0x80801U,(uint)Parse("PipeFlags","flags:\t02004001\n"));
        HostCheck.Equal(41,(int)Parse("StatusInt","Name:\tworker\nTgid:\t41\nPid:\t43\n","Tgid"));
        Refused("PipeFlags","flags:\t889\n");Refused("StatusInt","OtherTgid:\t41\n","Tgid");
    }

    [HostTest("web initial serial gate requires one complete waiter call and no later resume or replacement")]
    private static void InitialGate()
    {
        bool Pending(string text,int tid=7)=>NativeAotWebRacePeer.InspectInitialWaitForTests(text,tid);
        const string get="M16_008_CALL identity=4294967301 nr=186 a0=0 valueBits=7 action=1 status=1 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0\r\n";
        const string wait="M16_008_CALL identity=4294967301 nr=232 a0=9 valueBits=0 action=3 status=9 ordinal=2 waitSource=3 waitDeadline=2500000000 waitSequence=1\r\n";
        string before="TEST_START m10.004\r\n"+get,complete=before+wait;
        HostCheck.True(Pending(complete)&&!Pending(before)&&!Pending(complete,8)&&!Pending(before+wait[..^2]));
        HostCheck.True(!Pending(complete+"M16_008_RESUME identity=4294967301 valueBits=1 status=1\r\n")&&
            !Pending(complete+wait.Replace("ordinal=2","ordinal=3",StringComparison.Ordinal))&&
            !Pending(complete.Replace("waitSource=3","waitSource=0",StringComparison.Ordinal))&&
            !Pending(complete.Replace("action=3 status=9","action=3 status=1",StringComparison.Ordinal))&&
            !Pending(complete.Replace("identity=4294967301 nr=232","identity=4294967302 nr=232",StringComparison.Ordinal)));
    }

    [HostTest("web native schedule non-Linux refusal creates no journal or native mutation")]
    private static void PlatformRefusal()
    {
        if(OperatingSystem.IsLinux())return;
        string root=HostToolProcess.FindRepositoryRoot(),folder=Path.Combine(root,"artifacts","host-tests","native-schedule-"+Guid.NewGuid().ToString("N"));
        using var process=Process.GetCurrentProcess();Exception? failure=null;
        try{_=Activator.CreateInstance(Schedule,BindingFlags.Instance|BindingFlags.NonPublic,null,[process,"unused",folder],null);}
        catch(TargetInvocationException error){failure=error.InnerException;}
        HostCheck.True(failure is InvalidDataException&&failure.Message.Contains("signalrace_schedule_platform",StringComparison.Ordinal)&&!Directory.Exists(folder));
    }
}
