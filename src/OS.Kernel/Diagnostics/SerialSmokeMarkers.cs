namespace OS.Kernel.Diagnostics;

public static class SerialSmokeMarkers
{
    public const string M09006Start = "TEST_START m09.006";
    public const string M09006Prepared = "M09_006_PREPARED";
    public const string M09006Transfer = "M09_006_TRANSFER";
    public const string M09006Tick = "M09_006_TICK";
    public const string M09006Restored = "M09_006_RESTORED";
    public const string M09006Pass = "TEST_PASS m09.006";
    public const string M09006Ready = "CLROS_M09_006_READY";
    public const string M09005Start = "TEST_START m09.005";
    public const string M09005Prepared = "M09_005_PREPARED";
    public const string M09005Armed = "M09_005_ARMED";
    public const string M09005Tick = "M09_005_TICK";
    public const string M09005Wake = "M09_005_WAKE";
    public const string M09005Restored = "M09_005_RESTORED";
    public const string M09005Pass = "TEST_PASS m09.005";
    public const string M09005Ready = "CLROS_M09_005_READY";
    public const string TestStart = "TEST_START";

    public const string TestPass = "TEST_PASS";

    public const string TestFail = "TEST_FAIL";

    public const string BootOk = "OS_BOOT_OK";

    public const string OsSmokePass = "OS_SMOKE_PASS";

    public const string OsSmokeFail = "OS_SMOKE_FAIL";

    public const string M09004Ready = "CLROS_M09_004_READY";
}
