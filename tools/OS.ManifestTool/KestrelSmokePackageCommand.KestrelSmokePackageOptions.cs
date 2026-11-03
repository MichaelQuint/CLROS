using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

internal sealed class KestrelSmokePackageOptions
{
    private KestrelSmokePackageOptions(bool succeeded, string manifestPath, string packageRootPath, string profileName, string contractId, string errorMessage)
    {
        this.Succeeded = succeeded;
        this.ManifestPath = manifestPath;
        this.PackageRootPath = packageRootPath;
        this.ProfileName = profileName;
        this.ContractId = contractId;
        this.ErrorMessage = errorMessage;
    }

    public bool Succeeded
    {
        get;
    }
    public string ManifestPath
    {
        get;
    }
    public string PackageRootPath
    {
        get;
    }
    public string ProfileName
    {
        get;
    }
    public string ContractId
    {
        get;
    }
    public string ErrorMessage
    {
        get;
    }

    public static KestrelSmokePackageOptions Parse(string[] args)
    {
        string? manifestPath = null;
        string? packageRootPath = null;
        string profileName = WorkloadProfile.SelectedName;
        string contractId = KestrelSmokePackageContractValidator.ContractId;
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--manifest", StringComparison.Ordinal))
            {
                if (manifestPath is not null)
                {
                    return Invalid("Duplicate --manifest argument.");
                }

                if (index + 1 >= args.Length)
                {
                    return Invalid("Missing value for --manifest.");
                }

                index++;
                manifestPath = args[index];
                continue;
            }

            if (string.Equals(argument, "--package-root", StringComparison.Ordinal))
            {
                if (packageRootPath is not null)
                {
                    return Invalid("Duplicate --package-root argument.");
                }

                if (index + 1 >= args.Length)
                {
                    return Invalid("Missing value for --package-root.");
                }

                index++;
                packageRootPath = args[index];
                continue;
            }

            if (string.Equals(argument, "--profile", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    return Invalid("Missing value for --profile.");
                }

                index++;
                profileName = args[index];
                continue;
            }

            if (string.Equals(argument, "--contract", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    return Invalid("Missing value for --contract.");
                }

                index++;
                contractId = args[index];
                continue;
            }

            return Invalid("Unknown argument.");
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return Invalid("Manifest path is required.");
        }

        if (string.IsNullOrWhiteSpace(packageRootPath))
        {
            return Invalid("Package root path is required.");
        }

        return new KestrelSmokePackageOptions(true, manifestPath, packageRootPath, profileName, contractId, string.Empty);
    }

    private static KestrelSmokePackageOptions Invalid(string errorMessage)
    {
        return new KestrelSmokePackageOptions(false, string.Empty, string.Empty, WorkloadProfile.SelectedName, KestrelSmokePackageContractValidator.ContractId, errorMessage);
    }
}
