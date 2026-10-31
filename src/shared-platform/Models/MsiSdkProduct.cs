namespace JuliHyro.DotNetTool.Shared.Models;

/// <summary>
/// An SDK product registered with Windows Installer or the Windows uninstall registry.
/// </summary>
public sealed record MsiSdkProduct(
    string DisplayName,
    string DisplayVersion,
    string Publisher,
    string? ProductCode,
    string? UninstallString,
    string RegistryKey,
    string RegistryView);
