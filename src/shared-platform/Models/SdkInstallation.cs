namespace JuliHyro.DotNetTool.Shared.Models;

/// <summary>
/// Describes an SDK discovered on this computer.
/// </summary>
public sealed record SdkInstallation(
    string Version,
    string RootPath,
    string Source,
    bool IsRegistered,
    bool IsPortable);
