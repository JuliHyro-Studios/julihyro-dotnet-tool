namespace JuliHyro.DotNetTool.Shared.Models;

/// <summary>
/// Results from the deeper custom-installation environment scan.
/// </summary>
public sealed class ScanResult
{
    public List<string> DotNetRoots { get; } = [];
    public List<string> PathEntries { get; } = [];
    public List<string> RegistryLocations { get; } = [];
    public List<SdkInstallation> Installations { get; } = [];
    public List<string> Warnings { get; } = [];
}
