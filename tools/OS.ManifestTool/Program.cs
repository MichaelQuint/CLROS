using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

if (KestrelSmokePackageCommand.IsCommand(args))
{
    return KestrelSmokePackageCommand.Run(args);
}

CliParseResult options = CliParser.Parse(args);
if (options.ShowHelp)
{
    CliParser.PrintUsage();
    return 0;
}

if (!options.ShouldValidate)
{
    CliParser.PrintUsage();
    return 64;
}

Console.WriteLine("TEST_START manifest.validate");
if (!options.Succeeded)
{
    Console.WriteLine("TEST_FAIL manifest.validate invalid_args " + options.ErrorMessage);
    return 64;
}

ValidationResult result = ManifestValidator.Validate(options.ManifestPath, options.ProfileName);
if (result.Succeeded)
{
    Console.WriteLine("TEST_PASS manifest.validate");
    return 0;
}

Console.WriteLine("TEST_FAIL manifest.validate " + result.Code + " " + result.Message);
return 1;
