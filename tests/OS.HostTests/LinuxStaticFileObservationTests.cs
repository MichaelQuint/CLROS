using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe class LinuxStaticFileObservationTests
{
    private static bool Output(ref LinuxStaticFileObservation o, string text, ulong writer = 1, uint channel = 2)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* p = bytes)
            return o.ObserveOutput(writer, channel, p, (uint)bytes.Length);
    }
    private static LinuxStaticFileObservation Ready(uint fd = 7)
    {
        LinuxStaticFileObservation o = default;
        HostCheck.True(o.Begin(1));
        HostCheck.True(Output(ref o, "CLROS_STATIC_FILES_APP_ENTRY v1\n"));
        HostCheck.True(o.RecordBind(2, fd, true));
        HostCheck.True(o.RecordListen(2, fd, true, 100));
        return o;
    }
    private static ulong Due => 100 + LinuxStaticFileObservation.RequestDelayNanoseconds;
    private static LinuxStaticFileObservation Requested()
    {
        var o = Ready();
        HostCheck.True(o.RecordRequest(1, Due));
        return o;
    }

    [HostTest("static lifecycle observes fragmented real entry request end close and exit independently")]
    private static void Lifecycle()
    {
        LinuxStaticFileObservation o = default;
        HostCheck.True(o.Begin(1));
        HostCheck.True(!o.Begin(2));
        foreach (char c in "CLROS_STATIC_FILES_APP_ENTRY v1\n")
            HostCheck.True(Output(ref o, c.ToString()));
        HostCheck.True(Output(ref o, "actual upstream logger output\n", 3, 1));
        HostCheck.True(o.RecordBind(2, 7, true));
        HostCheck.True(o.RecordListen(2, 7, true, 100));
        HostCheck.True(o.Ready && !o.GracefulExit);
        HostCheck.Equal(LinuxStaticFileObservation.Progress.Waiting, o.Poll(Due - 1));
        HostCheck.Equal(LinuxStaticFileObservation.Progress.RequestTermination, o.Poll(Due));
        HostCheck.True(!o.Requested); // Poll never sends or records a signal.
        HostCheck.True(o.RecordRequest(1, Due));
        HostCheck.Equal(LinuxStaticFileObservation.Progress.Draining, o.Poll(Due + 1));
        HostCheck.True(o.RecordClose(3, true));
        HostCheck.True(o.RecordClose(7, true));
        HostCheck.True(Output(ref o, "CLROS_STATIC_FILES_APP_END "));
        HostCheck.True(Output(ref o, "v1\n"));
        HostCheck.True(!o.GracefulExit);
        HostCheck.True(o.RecordExit(1, 0, Due + 5));
        HostCheck.True(o.GracefulExit);
        HostCheck.Equal(LinuxStaticFileObservation.Progress.Exited, o.Poll(Due + LinuxStaticFileObservation.GraceNanoseconds));
        HostCheck.Equal(1UL, o.MainGeneration);
        HostCheck.Equal(2UL, o.BindGeneration);
        HostCheck.Equal(Due + 5, o.ExitedNanoseconds);
    }

    [HostTest("static lifecycle cannot infer real app exit from request deadline or copied end alone")]
    private static void MissingExit()
    {
        var o = Requested();
        HostCheck.True(o.RecordClose(7, true));
        HostCheck.True(Output(ref o, "CLROS_STATIC_FILES_APP_END v1\n"));
        HostCheck.True(!o.GracefulExit);
        HostCheck.Equal(LinuxStaticFileObservation.Progress.Draining, o.Poll(Due + LinuxStaticFileObservation.GraceNanoseconds - 1));
        HostCheck.Equal(LinuxStaticFileObservation.Progress.GraceExpired, o.Poll(Due + LinuxStaticFileObservation.GraceNanoseconds));
        HostCheck.True(!o.RecordExit(1, 0, Due + LinuxStaticFileObservation.GraceNanoseconds));
        HostCheck.True(o.Failed);
    }

    [HostTest("static lifecycle rejects early duplicate stale request and clock regression permanently")]
    private static void RequestRejections()
    {
        foreach (int variation in Enumerable.Range(0, 5))
        {
            var o = Ready();
            bool accepted = variation switch
            {
                0 => o.RecordRequest(1, Due - 1),
                1 => o.RecordRequest(2, Due),
                2 => o.RecordRequest(1, Due) && o.RecordRequest(1, Due + 1),
                3 => o.Poll(99) != LinuxStaticFileObservation.Progress.Rejected,
                _ => o.Poll(ulong.MaxValue) != LinuxStaticFileObservation.Progress.Rejected
            };
            HostCheck.True(!accepted && o.Failed && !o.GracefulExit);
        }
    }

    [HostTest("static lifecycle rejects wrong writer failure output early exit and missing listener close")]
    private static void IncompleteLifecycle()
    {
        foreach (int variation in Enumerable.Range(0, 8))
        {
            var o = Requested();
            bool accepted = variation switch
            {
                0 => Output(ref o, "CLROS_STATIC_FILES_APP_END v1\n", 2),
                1 => Output(ref o, "CLROS_STATIC_FILES_APP_FAIL v1\n"),
                2 => o.RecordExit(1, 0, Due + 1),
                3 => Output(ref o, "CLROS_STATIC_FILES_APP_END v1\n") && o.RecordExit(1, 0, Due + 1),
                4 => o.RecordClose(7, false),
                5 => o.RecordClose(7, true) && Output(ref o, "CLROS_STATIC_FILES_APP_END v1\n") && o.RecordExit(1, 1, Due + 1),
                6 => o.RecordClose(7, true) && Output(ref o, "CLROS_STATIC_FILES_APP_END v1\n") && o.RecordExit(2, 0, Due + 1),
                _ => o.RecordClose(7, true) && Output(ref o, "CLROS_STATIC_FILES_APP_END v1\n") && o.RecordExit(1, 0, Due + LinuxStaticFileObservation.GraceNanoseconds)
            };
            HostCheck.True(!accepted && o.Failed && !o.GracefulExit);
        }
    }

    [HostTest("static lifecycle cannot mistake a duplicated listener fd close for endpoint shutdown")]
    private static void DuplicatedListener()
    {
        using var m = new LinuxSocketServicesTests.Model(capacity: 1);
        uint fd = m.Listen();
        HostCheck.True(LinuxDescriptorServices.Capture(m.Files.State, fd, out var token));
        HostCheck.True(LinuxDescriptorServices.IsLastReference(m.Files.State, token));
        var o = Ready(fd);
        HostCheck.True(o.RecordRequest(1, Due));
        uint duplicate = (uint)m.Call(72, fd, 1030, 0);
        HostCheck.True(!LinuxDescriptorServices.IsLastReference(m.Files.State, token));
        bool finalClose = LinuxDescriptorServices.IsLastReference(m.Files.State, token);
        HostCheck.Equal(0L, m.Call(3, fd));
        HostCheck.True(!o.RecordClose(fd, finalClose) && o.Failed && !o.GracefulExit);
        HostCheck.True(!LinuxDescriptorServices.IsLastReference(m.Files.State, token));
        HostCheck.Equal(-105L, m.Call(41, 2, 1, 6)); // The duplicate still owns the only endpoint.
        HostCheck.True(LinuxDescriptorServices.Capture(m.Files.State, duplicate, out var last));
        HostCheck.True(LinuxDescriptorServices.IsLastReference(m.Files.State, last));
        HostCheck.Equal(0L, m.Call(3, duplicate));
        HostCheck.True(!LinuxDescriptorServices.IsLastReference(m.Files.State, last));
        HostCheck.Equal(fd, m.Socket());
        HostCheck.True(!LinuxDescriptorServices.IsLastReference(m.Files.State, token));
        m.End();
    }
}
