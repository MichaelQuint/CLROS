using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

internal static class KestrelSmokePackageCommand
{
    private const string CommandName = "validate-kestrel-package";
    private const string MarkerName = "kestrel.package.contract";
    public static bool IsCommand(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.Ordinal);
    }

    public static int Run(string[] args)
    {
        Console.WriteLine("TEST_START " + MarkerName);
        KestrelSmokePackageOptions options = KestrelSmokePackageOptions.Parse(args);
        if (!options.Succeeded)
        {
            Console.WriteLine("TEST_FAIL " + MarkerName + " invalid_args " + options.ErrorMessage);
            return 64;
        }

        ValidationResult result = KestrelSmokePackageContractValidator.Validate(options.ManifestPath, options.PackageRootPath, options.ProfileName, options.ContractId);
        if (result.Succeeded)
        {
            Console.WriteLine("KESTREL_PACKAGE_CONTRACT " + KestrelSmokePackageContractValidator.ContractId);
            Console.WriteLine("TEST_PASS " + MarkerName);
            return 0;
        }

        Console.WriteLine("TEST_FAIL " + MarkerName + " " + result.Code + " " + result.Message);
        return 1;
    }
}
