namespace OS.Storage;

public enum VirtioBlockPackageFileStagingPreflightStatus
{
    Success = 0,
    PackagePreflightFailed = 1,
    PackageOpenFailed = 2,
    PackageStagingFailed = 3
}
