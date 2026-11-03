using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

ImageBuilderOptions options;
try
{
    options = ImageBuilderOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("TEST_FAIL " + ImageBuilderOptions.GetMarkerName(args) + " invalid_args " + ex.Message);
    return 2;
}

if (options.ShowHelp)
{
    ImageBuilderOptions.PrintHelp();
    return 0;
}

string markerName = ImageBuilderOptions.GetMarkerName(options.Command);
Console.WriteLine("TEST_START " + markerName);
Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
try
{
    if (options.Command == ImageBuilderCommand.BuildLinuxElfPackage)
    {
        LinuxElfPackageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.PlanRaw)
    {
        RawImageLayout layout = RawEspImageBuilder.Plan(options);
        PrintKestrelPackageEvidence(options);
        ImagePlanPrinter.Print(layout, Console.Out, options.WorkloadProfile);
        Console.WriteLine("IMAGE_PLAN_NO_WRITE " + options.OutputPath);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.ConvertQcow2)
    {
        Qcow2Converter.Convert(options, Console.Out, Console.Error);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.ConvertAzureVhd)
    {
        AzureVhdConverter.Convert(options, Console.Out, Console.Error);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.PlanAwsAmi)
    {
        AwsAmiImportPlanner.Plan(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.DescribeImage)
    {
        ImageArtifactMetadataWriter.Describe(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildPackage)
    {
        OsPackageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildNativePayload)
    {
        NativePayloadBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildNativeAppPackage)
    {
        NativeAppPackageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.BuildNativeAppImage)
    {
        NativeAppImageBuilder.Build(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    if (options.Command == ImageBuilderCommand.ValidatePackage)
    {
        PackagePayloadValidator.Validate(options, Console.Out);
        Console.WriteLine("TEST_PASS " + markerName);
        return 0;
    }

    RawEspImageBuilder.Build(options);
    PrintKestrelPackageEvidence(options);
    Console.WriteLine("IMAGE_ARTIFACT " + options.OutputPath);
    Console.WriteLine("TEST_PASS " + markerName);
    return 0;
}
catch (ImageBuilderException ex)
{
    Console.Error.WriteLine("TEST_FAIL " + markerName + " " + ex.Reason);
    Console.Error.WriteLine(ex.Message);
    return ex.ExitCode;
}

static void PrintKestrelPackageEvidence(ImageBuilderOptions options)
{
    if (options.KestrelPackageRootPath is null)
    {
        return;
    }

    Console.WriteLine("KESTREL_PACKAGE_CONTRACT " + KestrelSmokePackageContractValidator.ContractId);
    Console.WriteLine("KESTREL_PACKAGE_ROOT " + options.KestrelPackageRootPath);
}
