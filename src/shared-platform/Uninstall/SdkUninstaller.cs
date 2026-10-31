using System.Diagnostics;
using JuliHyro.DotNetTool.Shared.Models;
using JuliHyro.DotNetTool.Shared.Security;

namespace JuliHyro.DotNetTool.Shared.Uninstall;

/// <summary>
/// Removes custom SDK folders or invokes Windows Installer for registered MSI SDK products.
/// MSI products are never removed by manually deleting their installation directories.
/// </summary>
public sealed class SdkUninstaller
{
    /// <summary>
    /// Removes one SDK version from a JH-DTM-managed custom installation root.
    /// </summary>
    public void RemovePortableSdk(SdkInstallation installation)
    {
        AdministratorGuard.DemandAdministrator("before portable SDK removal");
        if (!installation.IsPortable)
            throw new InvalidOperationException("This SDK was not identified as a JH-DTM-managed custom installation.");

        var root = Path.GetFullPath(installation.RootPath);
        var marker = Path.Combine(root, ".jh-dtm-install.json");
        if (!File.Exists(marker))
            throw new InvalidOperationException("The JH-DTM ownership marker is missing. Refusing to delete files.");

        var sdkDirectory = Path.GetFullPath(Path.Combine(root, "sdk", installation.Version));
        var expectedPrefix = Path.GetFullPath(Path.Combine(root, "sdk")) + Path.DirectorySeparatorChar;
        if (!sdkDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(sdkDirectory))
            throw new InvalidOperationException("The selected SDK directory is missing or failed the safety path check.");

        Directory.Delete(sdkDirectory, recursive: true);
    }

    /// <summary>
    /// Runs the registered MSI product's uninstall operation using its product code.
    /// Windows Installer owns the removal and performs its normal rollback/registration work.
    /// </summary>
    public async Task<int> UninstallMsiProductAsync(MsiSdkProduct product, CancellationToken cancellationToken = default)
    {
        AdministratorGuard.DemandAdministrator("before MSI SDK uninstall");

        if (string.IsNullOrWhiteSpace(product.ProductCode) ||
            !Guid.TryParse(product.ProductCode.Trim('{', '}'), out var productGuid))
            throw new InvalidOperationException("This registry entry has no valid MSI product code. It cannot be safely removed through msiexec.");

        var productCode = productGuid.ToString("B");
        var start = new ProcessStartInfo("msiexec.exe")
        {
            UseShellExecute = false,
            ArgumentList = { "/x", productCode, "/passive", "/norestart" }
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Windows Installer could not be started.");

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
