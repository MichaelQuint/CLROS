namespace OS.Kernel.Diagnostics;

public enum PanicMode
{
    RequestHalt,
    ReturnToKernel
}

public enum PanicReportAction
{
    HaltRequested,
    ReturnedToKernel
}

public static class Panic
{
    public static PanicReportAction Report(DiagnosticLog log, PanicContext context, PanicMode mode)
    {
        if (log is null)
        {
            return mode == PanicMode.ReturnToKernel
                ? PanicReportAction.ReturnedToKernel
                : PanicReportAction.HaltRequested;
        }

        log.Panic(PanicFormatter.Format(context));

        if (mode == PanicMode.ReturnToKernel)
        {
            return PanicReportAction.ReturnedToKernel;
        }

        return PanicReportAction.HaltRequested;
    }
}
