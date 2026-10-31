using JuliHyro.DotNetTool.Shared.Models;
using JuliHyro.DotNetTool.Shared.Security;

namespace JuliHyro.DotNetTool.Shared.Uninstall;

/// <summary>
/// Removes a selected SDK version from a portable/custom root.
/// Default MSI installations are deliberately not deleted by removing files.
/// </summary>
public sealed class SdkUninstaller
{
    public void RemovePortableSdk(SdkInstallation installation)
    {
        AdministratorGuard.DemandAdministrator("before portable SDK removal");
        if (!installation.IsPortable)
            throw new InvalidOperationException("This SDK was not identified as a portable/custom installation.");

        var root = Path.GetFullPath(installation.RootPath);
        var sdkDirectory = Path.GetFullPath(Path.Combine(root, "sdk", installation.Version));
        var expectedPrefix = Path.GetFullPath(Path.Combine(root, "sdk")) + Path.DirectorySeparatorChar;
        if (!sdkDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(sdkDirectory))
            throw new InvalidOperationException("The selected SDK directory is missing or failed the safety path check.");

        Directory.Delete(sdkDirectory, recursive: true);
    }
}
