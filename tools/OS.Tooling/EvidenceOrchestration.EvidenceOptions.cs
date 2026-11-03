using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed record EvidenceOptions
{
    public string WorkloadProfile { get; init; } = "SingleMinimalApi";
    public string LogRoot { get; init; } = "";
    public string DotNet { get; init; } = "dotnet";
    public string Qemu { get; init; } = "";
    public string QemuImg { get; init; } = "";
    public string OvmfCode { get; init; } = "";
    public bool PlanOnly
    {
        get; init;
    }
    public bool SelfTest
    {
        get; init;
    }
    public bool RunQcow2RealSmoke
    {
        get; init;
    }

    public static EvidenceOptions Parse(string[] args)
    {
        var value = new EvidenceOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("missing_option_value");
            value = CommandOptions.Key(args[i]) switch
            {
                "dryrun" or "planonly" => value with
                {
                    PlanOnly = true
                },
                "selftest" => value with
                {
                    SelfTest = true
                },
                "runqcow2realsmoke" => value with
                {
                    RunQcow2RealSmoke = true
                },
                "logroot" => value with
                {
                    LogRoot = Next()
                },
                "workloadprofile" => value with
                {
                    WorkloadProfile = Next()
                },
                "dotnet" => value with
                {
                    DotNet = Next()
                },
                "qemu" => value with
                {
                    Qemu = Next()
                },
                "qemuimg" => value with
                {
                    QemuImg = Next()
                },
                "ovmfcode" => value with
                {
                    OvmfCode = Next()
                },
                _ => throw new ArgumentException("unknown_option " + args[i])
            };
        }

        SmokeContracts.ValidateWorkload(value.WorkloadProfile);
        if (value.PlanOnly && value.SelfTest)
            throw new ArgumentException("self-test cannot be combined with plan-only");
        return value;
    }
}
